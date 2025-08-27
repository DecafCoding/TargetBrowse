using TargetBrowse.Features.Suggestions.Services;

namespace TargetBrowse.Features.Suggestions.BackgroundServices;

/// <summary>
/// Background service that automatically resets YouTube API quota tracking at midnight UTC.
/// Ensures quota limits are properly reset for each new day.
/// </summary>
public class QuotaResetBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QuotaResetBackgroundService> _logger;

    public QuotaResetBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<QuotaResetBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Main execution loop that runs continuously and resets quota at midnight UTC.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Quota Reset Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextMidnight = now.Date.AddDays(1); // Next midnight UTC
                var delayUntilMidnight = nextMidnight - now;

                // Wait until midnight UTC
                _logger.LogDebug("Waiting {Delay} until next quota reset at {NextReset}",
                    delayUntilMidnight, nextMidnight);

                await Task.Delay(delayUntilMidnight, stoppingToken);

                // Perform quota reset
                await PerformQuotaReset();

                // After reset, wait a bit to avoid tight loop if something goes wrong
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when service is stopping
                _logger.LogInformation("Quota Reset Background Service stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Quota Reset Background Service");

                // Wait before retrying to avoid tight error loop
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Quota Reset Background Service stopped");
    }

    /// <summary>
    /// Performs the actual quota reset using the quota manager service.
    /// </summary>
    private async Task PerformQuotaReset()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var quotaManager = scope.ServiceProvider.GetRequiredService<IYouTubeQuotaManager>();

            await quotaManager.CheckAndPerformQuotaResetAsync();

            _logger.LogInformation("Daily quota reset completed successfully at {ResetTime}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform quota reset");
        }
    }

    /// <summary>
    /// Cleanup when the service stops.
    /// </summary>
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Quota Reset Background Service is stopping");
        return base.StopAsync(cancellationToken);
    }
}