using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using TargetBrowse.Features.Suggestions.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Services.YouTube;

/// <summary>
/// Comprehensive YouTube Data API quota management system.
/// Provides thread-safe quota tracking, rate limiting, persistent storage, and usage analytics.
/// </summary>
public class YouTubeQuotaManager : IYouTubeQuotaManager, IDisposable
{
    private readonly YouTubeApiSettings _settings;
    private readonly ILogger<YouTubeQuotaManager> _logger;
    private readonly SemaphoreSlim _quotaSemaphore;
    private readonly ConcurrentDictionary<string, QuotaReservation> _activeReservations;
    private readonly object _quotaLock = new object();

    // Quota tracking state
    private int _quotaUsed;
    private DateTime _lastReset;
    private readonly List<DailyQuotaUsage> _usageHistory;
    private readonly string _quotaStorageFilePath;

    // Event tracking
    private bool _warningThresholdTriggered;
    private bool _criticalThresholdTriggered;

    public event EventHandler<QuotaThresholdEventArgs>? QuotaThresholdReached;
    public event EventHandler<QuotaExhaustedEventArgs>? QuotaExhausted;

    public YouTubeQuotaManager(IOptions<YouTubeApiSettings> settings, ILogger<YouTubeQuotaManager> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _quotaSemaphore = new SemaphoreSlim(_settings.MaxConcurrentRequests, _settings.MaxConcurrentRequests);
        _activeReservations = new ConcurrentDictionary<string, QuotaReservation>();
        _usageHistory = new List<DailyQuotaUsage>();

        // Initialize quota storage file path
        _quotaStorageFilePath = GetQuotaStorageFilePath();

        // Load persisted quota data
        LoadQuotaData();

        // Ensure quota is reset if needed
        ResetQuotaIfNeeded();

        _logger.LogInformation("YouTube Quota Manager initialized. Daily limit: {DailyLimit}, Current usage: {QuotaUsed}",
            _settings.DailyQuotaLimit, _quotaUsed);
    }

    /// <summary>
    /// Checks if the YouTube API is currently available and within quota limits.
    /// </summary>
    public async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            ResetQuotaIfNeeded();
            var status = await GetQuotaStatusAsync();
            return !status.IsExhausted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking API availability");
            return false;
        }
    }

    /// <summary>
    /// Gets the current quota usage information including remaining quota and reset times.
    /// </summary>
    public async Task<YouTubeQuotaStatus> GetQuotaStatusAsync()
    {
        ResetQuotaIfNeeded();
        CleanupExpiredReservations();

        int reservedQuota = _activeReservations.Values.Sum(r => r.ReservedQuota);
        var nextReset = GetNextResetTime();

        var status = new YouTubeQuotaStatus
        {
            DailyLimit = _settings.DailyQuotaLimit,
            Used = _quotaUsed,
            Reserved = reservedQuota,
            LastUpdated = DateTime.UtcNow,
            NextReset = nextReset,
            IsNearLimit = _quotaUsed >= (_settings.DailyQuotaLimit * _settings.QuotaWarningThreshold / 100.0),
            IsCritical = _quotaUsed >= (_settings.DailyQuotaLimit * _settings.QuotaCriticalThreshold / 100.0)
        };

        // Trigger threshold events if needed
        await CheckAndTriggerThresholdEventsAsync(status);

        return status;
    }

    /// <summary>
    /// Attempts to consume quota for a specific YouTube API operation.
    /// </summary>
    public async Task<bool> TryConsumeQuotaAsync(YouTubeApiOperation operation, int requestCount = 1)
    {
        if (requestCount <= 0)
        {
            throw new ArgumentException("Request count must be positive", nameof(requestCount));
        }

        ResetQuotaIfNeeded();
        CleanupExpiredReservations();

        int cost = GetOperationCost(operation, requestCount);
        bool success = false;

        // Use semaphore to control concurrent API access
        await _quotaSemaphore.WaitAsync();
        try
        {
            lock (_quotaLock)
            {
                var status = GetQuotaStatusSync();

                if (status.AvailableForUse >= cost)
                {
                    _quotaUsed += cost;
                    success = true;

                    if (_settings.EnableQuotaLogging)
                    {
                        _logger.LogInformation("Quota consumed: {Operation} x{RequestCount} = {Cost} units. Total used: {TotalUsed}/{Limit}",
                            operation, requestCount, cost, _quotaUsed, _settings.DailyQuotaLimit);
                    }

                    // Update today's usage in history
                    UpdateDailyUsageHistory(operation, requestCount, cost);
                }
                else
                {
                    _logger.LogWarning("Insufficient quota for operation: {Operation} x{RequestCount} (Cost: {Cost}). Available: {Available}",
                        operation, requestCount, cost, status.AvailableForUse);
                }
            }
        }
        finally
        {
            _quotaSemaphore.Release();
        }

        // Save quota data if operation was successful
        if (success && _settings.EnablePersistentQuotaStorage)
        {
            await SaveQuotaDataAsync();
        }

        // Check for quota exhaustion
        if (success)
        {
            var currentStatus = await GetQuotaStatusAsync();
            if (currentStatus.IsExhausted)
            {
                await TriggerQuotaExhaustedEventAsync(currentStatus);
            }
        }

        return success;
    }

    /// <summary>
    /// Reserves quota for a batch of operations.
    /// </summary>
    public async Task<QuotaReservationResult> ReserveQuotaAsync(Dictionary<YouTubeApiOperation, int> operations)
    {
        if (operations == null || !operations.Any())
        {
            return new QuotaReservationResult
            {
                Success = false,
                FailureReason = "No operations specified"
            };
        }

        ResetQuotaIfNeeded();
        CleanupExpiredReservations();

        int totalCost = operations.Sum(op => GetOperationCost(op.Key, op.Value));
        var reservationToken = Guid.NewGuid().ToString();
        var expiryTime = DateTime.UtcNow.AddHours(1); // Reservations expire after 1 hour

        lock (_quotaLock)
        {
            var status = GetQuotaStatusSync();

            if (status.AvailableForUse >= totalCost)
            {
                var reservation = new QuotaReservation
                {
                    Token = reservationToken,
                    ReservedQuota = totalCost,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiryTime,
                    Operations = operations
                };

                _activeReservations[reservationToken] = reservation;

                _logger.LogInformation("Quota reserved: {Token} for {Cost} units (expires: {Expiry})",
                    reservationToken, totalCost, expiryTime);

                return new QuotaReservationResult
                {
                    Success = true,
                    ReservationToken = reservationToken,
                    ReservedQuota = totalCost,
                    ReservationExpiry = expiryTime,
                    RequestedOperations = operations
                };
            }
            else
            {
                return new QuotaReservationResult
                {
                    Success = false,
                    FailureReason = $"Insufficient quota. Required: {totalCost}, Available: {status.AvailableForUse}",
                    RequestedOperations = operations
                };
            }
        }
    }

    /// <summary>
    /// Confirms a quota reservation and consumes the reserved quota.
    /// </summary>
    public async Task<bool> ConfirmReservationAsync(string reservationToken)
    {
        if (string.IsNullOrWhiteSpace(reservationToken))
        {
            return false;
        }

        if (!_activeReservations.TryRemove(reservationToken, out var reservation))
        {
            _logger.LogWarning("Reservation not found: {Token}", reservationToken);
            return false;
        }

        if (reservation.IsExpired)
        {
            _logger.LogWarning("Reservation expired: {Token}", reservationToken);
            return false;
        }

        lock (_quotaLock)
        {
            _quotaUsed += reservation.ReservedQuota;

            _logger.LogInformation("Reservation confirmed: {Token} for {Cost} units. Total used: {TotalUsed}/{Limit}",
                reservationToken, reservation.ReservedQuota, _quotaUsed, _settings.DailyQuotaLimit);

            // Update usage history
            foreach (var operation in reservation.Operations)
            {
                UpdateDailyUsageHistory(operation.Key, operation.Value, GetOperationCost(operation.Key, operation.Value));
            }
        }

        if (_settings.EnablePersistentQuotaStorage)
        {
            await SaveQuotaDataAsync();
        }

        return true;
    }

    /// <summary>
    /// Releases a quota reservation without consuming quota.
    /// </summary>
    public async Task<bool> ReleaseReservationAsync(string reservationToken)
    {
        if (string.IsNullOrWhiteSpace(reservationToken))
        {
            return false;
        }

        if (_activeReservations.TryRemove(reservationToken, out var reservation))
        {
            _logger.LogInformation("Reservation released: {Token} for {Cost} units",
                reservationToken, reservation.ReservedQuota);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the estimated cost for a specific YouTube API operation.
    /// </summary>
    public int GetOperationCost(YouTubeApiOperation operation, int requestCount = 1)
    {
        if (requestCount <= 0)
        {
            throw new ArgumentException("Request count must be positive", nameof(requestCount));
        }

        return YouTubeApiOperationCosts.GetCost(operation) * requestCount;
    }

    /// <summary>
    /// Forces a quota reset. Should only be used for testing or emergency scenarios.
    /// </summary>
    public async Task<bool> ForceQuotaResetAsync()
    {
        try
        {
            lock (_quotaLock)
            {
                _quotaUsed = 0;
                _lastReset = DateTime.UtcNow;
                _activeReservations.Clear();
                _warningThresholdTriggered = false;
                _criticalThresholdTriggered = false;
            }

            if (_settings.EnablePersistentQuotaStorage)
            {
                await SaveQuotaDataAsync();
            }

            _logger.LogWarning("Quota manually reset. All usage and reservations cleared.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual quota reset");
            return false;
        }
    }

    /// <summary>
    /// Gets quota usage analytics for monitoring and reporting.
    /// </summary>
    public async Task<YouTubeQuotaAnalytics> GetQuotaAnalyticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow.Date;

        var relevantHistory = _usageHistory
            .Where(h => h.Date >= from.Date && h.Date <= to.Date)
            .ToList();

        var analytics = new YouTubeQuotaAnalytics
        {
            FromDate = from,
            ToDate = to,
            TotalQuotaUsed = relevantHistory.Sum(h => h.QuotaUsed),
            MaxDailyUsage = relevantHistory.Any() ? relevantHistory.Max(h => h.QuotaUsed) : 0,
            MinDailyUsage = relevantHistory.Any() ? relevantHistory.Min(h => h.QuotaUsed) : 0,
            AverageDaily = relevantHistory.Any() ? relevantHistory.Average(h => h.QuotaUsed) : 0,
            DailyBreakdown = relevantHistory,
            TotalApiCalls = relevantHistory.Sum(h => h.ApiCalls),
            FailedCallsDueToQuota = relevantHistory.Sum(h => h.FailedCalls)
        };

        // Aggregate operation usage
        foreach (var day in relevantHistory)
        {
            foreach (var operation in day.OperationBreakdown)
            {
                if (analytics.UsageByOperation.ContainsKey(operation.Key))
                {
                    analytics.UsageByOperation[operation.Key] += operation.Value;
                }
                else
                {
                    analytics.UsageByOperation[operation.Key] = operation.Value;
                }
            }
        }

        return analytics;
    }

    /// <summary>
    /// Checks if a specific operation can be performed given current quota limits.
    /// </summary>
    public async Task<bool> CanPerformOperationAsync(YouTubeApiOperation operation, int requestCount = 1)
    {
        ResetQuotaIfNeeded();
        CleanupExpiredReservations();

        int cost = GetOperationCost(operation, requestCount);
        var status = await GetQuotaStatusAsync();

        return status.AvailableForUse >= cost;
    }

    /// <summary>
    /// Gets the estimated time until quota reset.
    /// </summary>
    public TimeSpan GetTimeUntilReset()
    {
        var nextReset = GetNextResetTime();
        var timeUntilReset = nextReset - DateTime.UtcNow;
        return timeUntilReset > TimeSpan.Zero ? timeUntilReset : TimeSpan.Zero;
    }

    #region Private Helper Methods

    /// <summary>
    /// Resets quota if a new day has started based on configured reset hour.
    /// </summary>
    private void ResetQuotaIfNeeded()
    {
        var nextReset = GetNextResetTime();
        var shouldReset = DateTime.UtcNow >= nextReset && _lastReset < nextReset.AddDays(-1);

        if (shouldReset)
        {
            lock (_quotaLock)
            {
                // Only reset if we haven't already reset today
                if (_lastReset.Date < DateTime.UtcNow.Date)
                {
                    _quotaUsed = 0;
                    _lastReset = DateTime.UtcNow;
                    _activeReservations.Clear();
                    _warningThresholdTriggered = false;
                    _criticalThresholdTriggered = false;

                    _logger.LogInformation("Daily quota reset completed. New limit: {DailyLimit}", _settings.DailyQuotaLimit);
                }
            }

            // Save the reset state
            if (_settings.EnablePersistentQuotaStorage)
            {
                _ = Task.Run(SaveQuotaDataAsync); // Fire and forget
            }
        }
    }

    /// <summary>
    /// Gets the next quota reset time based on configured reset hour.
    /// </summary>
    private DateTime GetNextResetTime()
    {
        var today = DateTime.UtcNow.Date;
        var resetTime = today.AddHours(_settings.QuotaResetHour);

        // If reset time has passed today, next reset is tomorrow
        if (DateTime.UtcNow >= resetTime)
        {
            resetTime = resetTime.AddDays(1);
        }

        return resetTime;
    }

    /// <summary>
    /// Gets quota status synchronously (for use within locks).
    /// </summary>
    private YouTubeQuotaStatus GetQuotaStatusSync()
    {
        int reservedQuota = _activeReservations.Values.Sum(r => r.ReservedQuota);
        var nextReset = GetNextResetTime();

        return new YouTubeQuotaStatus
        {
            DailyLimit = _settings.DailyQuotaLimit,
            Used = _quotaUsed,
            Reserved = reservedQuota,
            LastUpdated = DateTime.UtcNow,
            NextReset = nextReset
        };
    }

    /// <summary>
    /// Removes expired reservations from the active reservations collection.
    /// </summary>
    private void CleanupExpiredReservations()
    {
        var expiredTokens = _activeReservations
            .Where(r => r.Value.IsExpired)
            .Select(r => r.Key)
            .ToList();

        foreach (var token in expiredTokens)
        {
            if (_activeReservations.TryRemove(token, out var reservation))
            {
                _logger.LogInformation("Expired reservation removed: {Token} for {Cost} units",
                    token, reservation.ReservedQuota);
            }
        }
    }

    /// <summary>
    /// Updates daily usage history with operation details.
    /// </summary>
    private void UpdateDailyUsageHistory(YouTubeApiOperation operation, int requestCount, int cost)
    {
        var today = DateTime.UtcNow.Date;
        var todayUsage = _usageHistory.FirstOrDefault(h => h.Date.Date == today);

        if (todayUsage == null)
        {
            todayUsage = new DailyQuotaUsage { Date = today };
            _usageHistory.Add(todayUsage);
        }

        todayUsage.QuotaUsed += cost;
        todayUsage.ApiCalls += requestCount;

        if (todayUsage.OperationBreakdown.ContainsKey(operation))
        {
            todayUsage.OperationBreakdown[operation] += requestCount;
        }
        else
        {
            todayUsage.OperationBreakdown[operation] = requestCount;
        }

        // Keep only last 30 days of history
        if (_usageHistory.Count > 30)
        {
            _usageHistory.RemoveAll(h => h.Date < DateTime.UtcNow.Date.AddDays(-30));
        }
    }

    /// <summary>
    /// Checks and triggers threshold events if needed.
    /// </summary>
    private async Task CheckAndTriggerThresholdEventsAsync(YouTubeQuotaStatus status)
    {
        // Check warning threshold
        if (!_warningThresholdTriggered && status.IsNearLimit && !status.IsCritical)
        {
            _warningThresholdTriggered = true;
            QuotaThresholdReached?.Invoke(this, new QuotaThresholdEventArgs
            {
                QuotaStatus = status,
                ThresholdType = "Warning",
                Message = $"Quota usage has reached {status.UsagePercentage:F1}% ({status.Used}/{status.DailyLimit})"
            });
        }

        // Check critical threshold
        if (!_criticalThresholdTriggered && status.IsCritical)
        {
            _criticalThresholdTriggered = true;
            QuotaThresholdReached?.Invoke(this, new QuotaThresholdEventArgs
            {
                QuotaStatus = status,
                ThresholdType = "Critical",
                Message = $"Quota usage has reached critical level: {status.UsagePercentage:F1}% ({status.Used}/{status.DailyLimit})"
            });
        }
    }

    /// <summary>
    /// Triggers quota exhausted event.
    /// </summary>
    private async Task TriggerQuotaExhaustedEventAsync(YouTubeQuotaStatus status)
    {
        QuotaExhausted?.Invoke(this, new QuotaExhaustedEventArgs
        {
            QuotaStatus = status,
            Message = $"YouTube API quota exhausted. Used: {status.Used}/{status.DailyLimit}",
            ExhaustedAt = DateTime.UtcNow,
            NextResetAt = status.NextReset
        });

        _logger.LogError("YouTube API quota exhausted. Next reset: {NextReset}", status.NextReset);
    }

    /// <summary>
    /// Gets the file path for quota storage.
    /// </summary>
    private string GetQuotaStorageFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_settings.QuotaStorageFilePath))
        {
            return _settings.QuotaStorageFilePath;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appDataPath, "TargetBrowse");
        Directory.CreateDirectory(appFolder);

        return Path.Combine(appFolder, "youtube_quota.json");
    }

    /// <summary>
    /// Loads quota data from persistent storage.
    /// </summary>
    private void LoadQuotaData()
    {
        if (!_settings.EnablePersistentQuotaStorage || !File.Exists(_quotaStorageFilePath))
        {
            _lastReset = DateTime.UtcNow.Date;
            return;
        }

        try
        {
            var json = File.ReadAllText(_quotaStorageFilePath);
            var data = JsonSerializer.Deserialize<QuotaStorageData>(json);

            if (data != null)
            {
                _quotaUsed = data.QuotaUsed;
                _lastReset = data.LastReset;
                _usageHistory.AddRange(data.UsageHistory);

                // Restore active reservations (only non-expired ones)
                foreach (var reservation in data.ActiveReservations.Values.Where(r => !r.IsExpired))
                {
                    _activeReservations[reservation.Token] = reservation;
                }

                _logger.LogInformation("Quota data loaded from storage. Used: {Used}, Last Reset: {LastReset}",
                    _quotaUsed, _lastReset);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading quota data from storage. Starting fresh.");
            _lastReset = DateTime.UtcNow.Date;
        }
    }

    /// <summary>
    /// Saves quota data to persistent storage.
    /// </summary>
    private async Task SaveQuotaDataAsync()
    {
        if (!_settings.EnablePersistentQuotaStorage)
        {
            return;
        }

        try
        {
            var data = new QuotaStorageData
            {
                LastReset = _lastReset,
                QuotaUsed = _quotaUsed,
                ActiveReservations = _activeReservations.ToDictionary(r => r.Key, r => r.Value),
                UsageHistory = _usageHistory.ToList(),
                LastSaved = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_quotaStorageFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving quota data to storage");
        }
    }

    #endregion

    public void Dispose()
    {
        // Save final quota state
        if (_settings.EnablePersistentQuotaStorage)
        {
            SaveQuotaDataAsync().GetAwaiter().GetResult();
        }

        _quotaSemaphore?.Dispose();
        _logger.LogInformation("YouTube Quota Manager disposed");
    }
}