using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Services.Interfaces;

/// <summary>
/// Interface for managing YouTube Data API v3 quota tracking, rate limiting, and usage monitoring.
/// Provides centralized quota management across all application features using YouTube API.
/// </summary>
public interface IYouTubeQuotaManager
{
    /// <summary>
    /// Checks if the YouTube API is currently available and within quota limits.
    /// </summary>
    /// <returns>True if API is available, false if quota exceeded or service unavailable</returns>
    Task<bool> IsApiAvailableAsync();

    /// <summary>
    /// Gets the current quota usage information including remaining quota and reset times.
    /// </summary>
    /// <returns>Current quota usage status</returns>
    Task<YouTubeQuotaStatus> GetQuotaStatusAsync();

    /// <summary>
    /// Attempts to consume quota for a specific YouTube API operation.
    /// Returns false if insufficient quota is available.
    /// </summary>
    /// <param name="operation">The type of YouTube API operation</param>
    /// <param name="requestCount">Number of requests (default: 1)</param>
    /// <returns>True if quota was successfully consumed, false if insufficient quota</returns>
    Task<bool> TryConsumeQuotaAsync(YouTubeApiOperation operation, int requestCount = 1);

    /// <summary>
    /// Reserves quota for a batch of operations. Useful for planning large operations.
    /// Returns a reservation token that must be used or released.
    /// </summary>
    /// <param name="operations">Dictionary of operations and their request counts</param>
    /// <returns>Quota reservation result with token or failure reason</returns>
    Task<QuotaReservationResult> ReserveQuotaAsync(Dictionary<YouTubeApiOperation, int> operations);

    /// <summary>
    /// Confirms a quota reservation and consumes the reserved quota.
    /// </summary>
    /// <param name="reservationToken">Token from a previous reservation</param>
    /// <returns>True if reservation was confirmed and quota consumed</returns>
    Task<bool> ConfirmReservationAsync(string reservationToken);

    /// <summary>
    /// Releases a quota reservation without consuming quota.
    /// </summary>
    /// <param name="reservationToken">Token from a previous reservation</param>
    /// <returns>True if reservation was successfully released</returns>
    Task<bool> ReleaseReservationAsync(string reservationToken);

    /// <summary>
    /// Gets the estimated cost for a specific YouTube API operation.
    /// </summary>
    /// <param name="operation">The type of YouTube API operation</param>
    /// <param name="requestCount">Number of requests (default: 1)</param>
    /// <returns>Estimated quota cost</returns>
    int GetOperationCost(YouTubeApiOperation operation, int requestCount = 1);

    /// <summary>
    /// Forces a quota reset. Should only be used for testing or emergency scenarios.
    /// </summary>
    /// <returns>True if quota was successfully reset</returns>
    Task<bool> ForceQuotaResetAsync();

    /// <summary>
    /// Gets quota usage analytics for monitoring and reporting.
    /// </summary>
    /// <param name="fromDate">Start date for analytics (optional)</param>
    /// <param name="toDate">End date for analytics (optional)</param>
    /// <returns>Quota usage analytics</returns>
    Task<YouTubeQuotaAnalytics> GetQuotaAnalyticsAsync(DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Checks if a specific operation can be performed given current quota limits.
    /// Does not consume quota, only checks availability.
    /// </summary>
    /// <param name="operation">The type of YouTube API operation</param>
    /// <param name="requestCount">Number of requests (default: 1)</param>
    /// <returns>True if operation can be performed within quota limits</returns>
    Task<bool> CanPerformOperationAsync(YouTubeApiOperation operation, int requestCount = 1);

    /// <summary>
    /// Gets the estimated time until quota reset.
    /// </summary>
    /// <returns>Time span until next quota reset</returns>
    TimeSpan GetTimeUntilReset();

    /// <summary>
    /// Event fired when quota limits are approaching (configurable threshold).
    /// </summary>
    event EventHandler<QuotaThresholdEventArgs> QuotaThresholdReached;

    /// <summary>
    /// Event fired when quota is exhausted.
    /// </summary>
    event EventHandler<QuotaExhaustedEventArgs> QuotaExhausted;
}