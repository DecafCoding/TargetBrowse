using Microsoft.Extensions.Options;
using TargetBrowse.Services.YouTube.Models;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Suggestions.BackgroundServices;

/// <summary>
/// Background service that monitors YouTube API quota usage and handles automatic resets.
/// Runs continuously to ensure quota tracking is accurate and provides monitoring alerts.
/// </summary>
public class QuotaResetBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly YouTubeApiSettings _settings;
    private readonly ILogger<QuotaResetBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Check every 15 minutes

    public QuotaResetBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<YouTubeApiSettings> settings,
        ILogger<QuotaResetBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Quota Reset Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformQuotaMaintenanceAsync();
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quota maintenance background service");

                // Wait longer on error to prevent spam
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        _logger.LogInformation("Quota Reset Background Service stopped");
    }

    /// <summary>
    /// Performs routine quota maintenance tasks.
    /// </summary>
    private async Task PerformQuotaMaintenanceAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var quotaManager = scope.ServiceProvider.GetRequiredService<IYouTubeQuotaManager>();
        var messageCenter = scope.ServiceProvider.GetService<IMessageCenterService>();

        try
        {
            // Get current quota status
            var status = await quotaManager.GetQuotaStatusAsync();

            // Log quota status periodically (every hour at the top of the hour)
            if (DateTime.Now.Minute == 0)
            {
                _logger.LogInformation("YouTube API Quota Status - Used: {Used}/{Limit} ({Percentage:F1}%), Available: {Available}, Reserved: {Reserved}, Reset in: {ResetTime}",
                    status.Used, status.DailyLimit, status.UsagePercentage, status.AvailableForUse, status.Reserved, status.TimeUntilReset);
            }

            // Check for approaching quota limits and send notifications
            await CheckQuotaThresholdsAsync(status, messageCenter);

            // Log analytics periodically (once per day at reset time)
            if (ShouldGenerateAnalytics())
            {
                await LogQuotaAnalyticsAsync(quotaManager);
            }

            // Clean up old analytics data (keep last 30 days)
            // This is handled internally by the quota manager, but we could extend it here if needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during quota maintenance");
        }
    }

    /// <summary>
    /// Checks quota thresholds and sends appropriate notifications.
    /// </summary>
    private async Task CheckQuotaThresholdsAsync(YouTubeQuotaStatus status, IMessageCenterService? messageCenter)
    {
        if (messageCenter == null) return;

        try
        {
            // Critical threshold reached
            if (status.IsCritical)
            {
                await messageCenter.ShowApiLimitAsync(
                    "YouTube API",
                    status.NextReset);
            }
            // Warning threshold reached
            else if (status.IsNearLimit)
            {
                await messageCenter.ShowWarningAsync(
                    $"Warning: YouTube API quota at {status.UsagePercentage:F1}% ({status.Used:N0}/{status.DailyLimit:N0}). Consider reducing API usage.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending quota threshold notifications");
        }
    }

    /// <summary>
    /// Determines if quota analytics should be generated based on time.
    /// </summary>
    private bool ShouldGenerateAnalytics()
    {
        var now = DateTime.UtcNow;
        var resetHour = _settings.QuotaResetHour;

        // Generate analytics shortly after daily reset (within 15 minutes)
        return now.Hour == resetHour && now.Minute <= 15;
    }

    /// <summary>
    /// Logs comprehensive quota analytics for monitoring purposes.
    /// </summary>
    private async Task LogQuotaAnalyticsAsync(IYouTubeQuotaManager quotaManager)
    {
        try
        {
            // Get analytics for the last 7 days
            var analytics = await quotaManager.GetQuotaAnalyticsAsync(DateTime.UtcNow.AddDays(-7));

            _logger.LogInformation("Weekly YouTube API Quota Analytics:");
            _logger.LogInformation("  Period: {FromDate} to {ToDate}", analytics.FromDate.ToString("yyyy-MM-dd"), analytics.ToDate.ToString("yyyy-MM-dd"));
            _logger.LogInformation("  Total Quota Used: {TotalUsed:N0}", analytics.TotalQuotaUsed);
            _logger.LogInformation("  Daily Average: {Average:F0}", analytics.AverageDaily);
            _logger.LogInformation("  Max Daily Usage: {Max:N0}", analytics.MaxDailyUsage);
            _logger.LogInformation("  Total API Calls: {TotalCalls:N0}", analytics.TotalApiCalls);
            _logger.LogInformation("  Success Rate: {SuccessRate:F1}%", analytics.SuccessRate);

            if (analytics.UsageByOperation.Any())
            {
                _logger.LogInformation("  Usage by Operation:");
                foreach (var operation in analytics.UsageByOperation.OrderByDescending(x => x.Value))
                {
                    _logger.LogInformation("    {Operation}: {Count:N0}", operation.Key, operation.Value);
                }
            }

            // Log recent daily breakdown
            var recentDays = analytics.DailyBreakdown.OrderByDescending(d => d.Date).Take(3);
            _logger.LogInformation("  Recent Daily Usage:");
            foreach (var day in recentDays)
            {
                _logger.LogInformation("    {Date}: {Usage:N0} quota ({Calls:N0} calls)",
                    day.Date.ToString("yyyy-MM-dd"), day.QuotaUsed, day.ApiCalls);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating quota analytics");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Quota Reset Background Service is stopping...");

        // Perform final quota maintenance
        try
        {
            await PerformQuotaMaintenanceAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during final quota maintenance");
        }

        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Extension methods for enhanced message center integration.
/// Provides specific methods for quota-related notifications.
/// </summary>
public static class MessageCenterQuotaExtensions
{
    /// <summary>
    /// Shows quota warning with usage details.
    /// </summary>
    public static async Task ShowQuotaWarningAsync(this IMessageCenterService messageCenter,
        YouTubeQuotaStatus status)
    {
        var message = $"YouTube API quota at {status.UsagePercentage:F1}% ({status.Used:N0}/{status.DailyLimit:N0}). " +
                     $"Resets in {status.TimeUntilReset.Hours}h {status.TimeUntilReset.Minutes}m.";

        await messageCenter.ShowWarningAsync(message);
    }

    /// <summary>
    /// Shows quota exhausted notification with next reset information.
    /// </summary>
    public static async Task ShowQuotaExhaustedAsync(this IMessageCenterService messageCenter,
        YouTubeQuotaStatus status)
    {
        var message = $"YouTube API quota exhausted ({status.Used:N0}/{status.DailyLimit:N0}). " +
                     $"Service will resume at {status.NextReset:HH:mm} UTC.";

        await messageCenter.ShowErrorAsync(message);
    }
}