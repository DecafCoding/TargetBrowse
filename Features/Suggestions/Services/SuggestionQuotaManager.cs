using Microsoft.Extensions.Options;
using TargetBrowse.Services.Models;
using TargetBrowse.Features.Suggestions.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Suggestions.Services;

/// <summary>
/// Implementation of centralized YouTube quota management.
/// Thread-safe implementation with in-memory tracking for MVP.
/// Fixed to resolve service lifetime issues by using IServiceProvider.
/// </summary>
public class SuggestionQuotaManager : ISuggestionQuotaManager
{
    private readonly YouTubeApiSettings _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SuggestionQuotaManager> _logger;
    private readonly SemaphoreSlim _quotaSemaphore;

    // Thread-safe quota tracking
    private static int _dailyQuotaUsed = 0;
    private static DateTime _lastQuotaReset = DateTime.UtcNow.Date;
    private static readonly object _quotaLock = new object();
    private static readonly List<ApiCallRecord> _apiCallHistory = new();
    private static readonly Dictionary<string, ApiOperationStats> _operationStats = new();

    // API cost constants (YouTube Data API v3 quotas)
    private const int SEARCH_QUOTA_COST = 100;
    private const int VIDEO_DETAILS_QUOTA_COST = 1;
    private const int CHANNEL_DETAILS_QUOTA_COST = 1;
    private const int PLAYLIST_ITEMS_QUOTA_COST = 1;

    public SuggestionQuotaManager(
        IOptions<YouTubeApiSettings> settings,
        IServiceProvider serviceProvider,
        ILogger<SuggestionQuotaManager> logger)
    {
        _settings = settings.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _quotaSemaphore = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Checks if sufficient quota is available for an operation.
    /// </summary>
    public async Task<bool> IsQuotaAvailableAsync(int requiredQuota)
    {
        await CheckAndPerformQuotaResetAsync();

        lock (_quotaLock)
        {
            var available = _dailyQuotaUsed + requiredQuota <= _settings.DailyQuotaLimit;

            if (!available)
            {
                _logger.LogWarning("Quota check failed: Required {RequiredQuota}, Used {UsedQuota}, Limit {Limit}",
                    requiredQuota, _dailyQuotaUsed, _settings.DailyQuotaLimit);
            }

            return available;
        }
    }

    /// <summary>
    /// Records quota usage for an operation.
    /// </summary>
    public async Task RecordQuotaUsageAsync(int quotaUsed, string operationType, bool isSuccessful = true)
    {
        await _quotaSemaphore.WaitAsync();

        try
        {
            lock (_quotaLock)
            {
                _dailyQuotaUsed += quotaUsed;

                // Update operation statistics
                if (!_operationStats.ContainsKey(operationType))
                {
                    _operationStats[operationType] = new ApiOperationStats { OperationType = operationType };
                }

                var stats = _operationStats[operationType];
                stats.CallCount++;
                stats.QuotaUsed += quotaUsed;
                if (!isSuccessful)
                {
                    stats.ErrorCount++;
                }

                _logger.LogDebug("Quota recorded: {QuotaUsed} units for {OperationType}, Total: {TotalUsed}/{DailyLimit}",
                    quotaUsed, operationType, _dailyQuotaUsed, _settings.DailyQuotaLimit);
            }

            // Check for quota warnings
            await CheckQuotaThresholdsAsync();
        }
        finally
        {
            _quotaSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets current quota status information.
    /// </summary>
    public async Task<QuotaStatus> GetQuotaStatusAsync()
    {
        await CheckAndPerformQuotaResetAsync();

        lock (_quotaLock)
        {
            return new QuotaStatus
            {
                DailyQuotaLimit = _settings.DailyQuotaLimit,
                QuotaUsedToday = _dailyQuotaUsed,
                ResetTime = DateTime.UtcNow.Date.AddDays(1), // Midnight UTC tomorrow
                RecentCalls = _apiCallHistory.TakeLast(50).ToList()
            };
        }
    }

    /// <summary>
    /// Estimates quota cost for a specific operation.
    /// </summary>
    public async Task<int> EstimateOperationCostAsync(string operationType, int itemCount = 1)
    {
        return operationType.ToLower() switch
        {
            "search" => SEARCH_QUOTA_COST * itemCount,
            "videodetails" => Math.Max(1, (int)Math.Ceiling(itemCount / 50.0)) * VIDEO_DETAILS_QUOTA_COST,
            "channeldetails" => Math.Max(1, (int)Math.Ceiling(itemCount / 50.0)) * CHANNEL_DETAILS_QUOTA_COST,
            "playlistitems" => Math.Max(1, (int)Math.Ceiling(itemCount / 50.0)) * PLAYLIST_ITEMS_QUOTA_COST,
            _ => itemCount // Conservative fallback
        };
    }

    /// <summary>
    /// Gets detailed quota cost estimate for suggestion generation.
    /// </summary>
    public async Task<QuotaCostEstimate> EstimateSuggestionCostAsync(int channelCount, int topicCount, int estimatedVideos = 100)
    {
        var channelUpdatesCost = channelCount * SEARCH_QUOTA_COST; // Each channel requires a search
        var topicSearchesCost = topicCount * SEARCH_QUOTA_COST;    // Each topic requires a search

        // Video details are batched (50 videos per API call)
        var videoDetailsCost = Math.Max(1, (int)Math.Ceiling(estimatedVideos / 50.0)) * VIDEO_DETAILS_QUOTA_COST;

        var totalCost = channelUpdatesCost + topicSearchesCost + videoDetailsCost;
        var remainingQuota = Math.Max(0, _settings.DailyQuotaLimit - _dailyQuotaUsed);

        var estimate = new QuotaCostEstimate
        {
            ChannelUpdatesCost = channelUpdatesCost,
            TopicSearchesCost = topicSearchesCost,
            VideoDetailsCost = videoDetailsCost,
            TotalEstimatedCost = totalCost,
            ExceedsRemainingQuota = totalCost > remainingQuota,
            ProjectedQuotaUsagePercentage = Math.Min(100, (double)(_dailyQuotaUsed + totalCost) / _settings.DailyQuotaLimit * 100)
        };

        // Cost breakdown
        estimate.CostBreakdown["Channel Updates"] = channelUpdatesCost;
        estimate.CostBreakdown["Topic Searches"] = topicSearchesCost;
        estimate.CostBreakdown["Video Details"] = videoDetailsCost;

        // Optimization suggestions
        if (estimate.ExceedsRemainingQuota)
        {
            estimate.OptimizationSuggestions.Add("Reduce the number of topics to search");
            estimate.OptimizationSuggestions.Add("Limit channel update checks to most important channels");
            estimate.OptimizationSuggestions.Add("Consider running suggestions at different times to spread quota usage");
        }
        else if (estimate.ProjectedQuotaUsagePercentage > 80)
        {
            estimate.OptimizationSuggestions.Add("Consider reducing search scope to preserve quota for later");
        }

        return estimate;
    }

    /// <summary>
    /// Resets quota tracking for a new day.
    /// </summary>
    public async Task ResetDailyQuotaAsync()
    {
        await _quotaSemaphore.WaitAsync();

        try
        {
            lock (_quotaLock)
            {
                var previousUsage = _dailyQuotaUsed;
                _dailyQuotaUsed = 0;
                _lastQuotaReset = DateTime.UtcNow.Date;

                // Clear old API call history (keep last 24 hours)
                var cutoffTime = DateTime.UtcNow.AddDays(-1);
                _apiCallHistory.RemoveAll(call => call.Timestamp < cutoffTime);

                // Reset operation statistics
                _operationStats.Clear();

                _logger.LogInformation("Daily quota reset completed. Previous usage: {PreviousUsage}/{Limit}",
                    previousUsage, _settings.DailyQuotaLimit);
            }
        }
        finally
        {
            _quotaSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets API availability status with comprehensive information.
    /// </summary>
    public async Task<ApiAvailabilityResult> GetApiAvailabilityAsync()
    {
        await CheckAndPerformQuotaResetAsync();

        lock (_quotaLock)
        {
            var isQuotaExceeded = _dailyQuotaUsed >= _settings.DailyQuotaLimit;
            var usagePercentage = _settings.DailyQuotaLimit > 0
                ? Math.Min(100, (double)_dailyQuotaUsed / _settings.DailyQuotaLimit * 100)
                : 0;

            var lastSuccessfulCall = _apiCallHistory
                .Where(call => call.IsSuccessful)
                .OrderByDescending(call => call.Timestamp)
                .FirstOrDefault()?.Timestamp;

            return new ApiAvailabilityResult
            {
                IsAvailable = !isQuotaExceeded && !string.IsNullOrEmpty(_settings.ApiKey),
                IsQuotaExceeded = isQuotaExceeded,
                QuotaUsagePercentage = usagePercentage,
                EstimatedRemainingQuota = Math.Max(0, _settings.DailyQuotaLimit - _dailyQuotaUsed),
                QuotaResetTime = DateTime.UtcNow.Date.AddDays(1), // Midnight UTC tomorrow
                ErrorMessage = isQuotaExceeded ? "Daily quota exceeded" : null,
                LastSuccessfulCall = lastSuccessfulCall,
                IsApiKeyValid = !string.IsNullOrEmpty(_settings.ApiKey)
            };
        }
    }

    /// <summary>
    /// Records an API call for detailed tracking and analytics.
    /// </summary>
    public async Task RecordApiCallAsync(string operationType, int quotaCost, TimeSpan duration,
        bool isSuccessful, string? errorMessage = null, int? itemsReturned = null)
    {
        await _quotaSemaphore.WaitAsync();

        try
        {
            lock (_quotaLock)
            {
                // Record the API call
                var callRecord = new ApiCallRecord
                {
                    Timestamp = DateTime.UtcNow,
                    OperationType = operationType,
                    QuotaCost = quotaCost,
                    IsSuccessful = isSuccessful,
                    ErrorMessage = errorMessage,
                    ResponseTimeMs = (int)duration.TotalMilliseconds,
                    ItemsReturned = itemsReturned
                };

                _apiCallHistory.Add(callRecord);

                // Update operation statistics
                if (!_operationStats.ContainsKey(operationType))
                {
                    _operationStats[operationType] = new ApiOperationStats { OperationType = operationType };
                }

                var stats = _operationStats[operationType];
                stats.CallCount++;
                stats.QuotaUsed += quotaCost;
                stats.TotalDuration += duration;
                if (!isSuccessful)
                {
                    stats.ErrorCount++;
                }

                // Update daily quota usage
                if (isSuccessful)
                {
                    _dailyQuotaUsed += quotaCost;
                }

                _logger.LogDebug("API call recorded: {OperationType}, Cost: {QuotaCost}, Duration: {Duration}ms, Success: {IsSuccessful}",
                    operationType, quotaCost, duration.TotalMilliseconds, isSuccessful);
            }

            // Check for quota warnings after recording
            await CheckQuotaThresholdsAsync();
        }
        finally
        {
            _quotaSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets usage statistics for analytics and monitoring.
    /// </summary>
    public async Task<SuggestionApiUsage> GetUsageStatisticsAsync()
    {
        await CheckAndPerformQuotaResetAsync();

        lock (_quotaLock)
        {
            var usage = new SuggestionApiUsage
            {
                EstimatedQuotaUsed = _dailyQuotaUsed,
                TotalDuration = _operationStats.Values.Aggregate(TimeSpan.Zero, (sum, stat) => sum + stat.TotalDuration),
                ErrorCount = _operationStats.Values.Sum(stat => stat.ErrorCount)
            };

            // Map operation types to specific usage properties
            foreach (var (operationType, stats) in _operationStats)
            {
                usage.OperationStats[operationType] = stats;

                switch (operationType.ToLower())
                {
                    case "channelsearch":
                    case "channelupdate":
                        usage.ChannelSearchCalls += stats.CallCount;
                        break;
                    case "topicsearch":
                    case "search":
                        usage.TopicSearchCalls += stats.CallCount;
                        break;
                    case "videodetails":
                        usage.VideoDetailCalls += stats.CallCount;
                        break;
                    case "validation":
                        usage.ValidationCalls += stats.CallCount;
                        break;
                }
            }

            return usage;
        }
    }

    /// <summary>
    /// Checks if quota reset is needed and performs it if necessary.
    /// </summary>
    public async Task CheckAndPerformQuotaResetAsync()
    {
        var today = DateTime.UtcNow.Date;

        if (_lastQuotaReset < today)
        {
            await ResetDailyQuotaAsync();
        }
    }

    /// <summary>
    /// Checks quota usage thresholds and sends appropriate notifications.
    /// Uses IServiceProvider to resolve scoped MessageCenterService when needed.
    /// </summary>
    private async Task CheckQuotaThresholdsAsync()
    {
        if (_settings.DailyQuotaLimit <= 0) return;

        var usagePercentage = (double)_dailyQuotaUsed / _settings.DailyQuotaLimit * 100;

        // Only send notifications at critical thresholds to avoid service lifetime issues
        if (usagePercentage >= 100)
        {
            try
            {
                // Create a scope to resolve the scoped MessageCenterService
                using var scope = _serviceProvider.CreateScope();
                var messageCenterService = scope.ServiceProvider.GetService<IMessageCenterService>();

                if (messageCenterService != null)
                {
                    await messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.Date.AddDays(1));
                }

                _logger.LogWarning("YouTube API quota completely exhausted: {Used}/{Limit} ({Percentage:F1}%)",
                    _dailyQuotaUsed, _settings.DailyQuotaLimit, usagePercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending quota exceeded notification");
            }
        }
        else if (usagePercentage >= 90)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var messageCenterService = scope.ServiceProvider.GetService<IMessageCenterService>();

                if (messageCenterService != null)
                {
                    await messageCenterService.ShowWarningAsync(
                        $"YouTube API quota nearly exhausted: {usagePercentage:F1}% used. " +
                        $"Consider reducing suggestion frequency until quota resets at midnight UTC.");
                }

                _logger.LogWarning("YouTube API quota at {Percentage:F1}%: {Used}/{Limit}",
                    usagePercentage, _dailyQuotaUsed, _settings.DailyQuotaLimit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending quota warning notification");
            }
        }
        else if (usagePercentage >= 75)
        {
            _logger.LogInformation("YouTube API quota at {Percentage:F1}%: {Used}/{Limit}",
                usagePercentage, _dailyQuotaUsed, _settings.DailyQuotaLimit);
        }
    }
}