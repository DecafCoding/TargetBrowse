using System.Text.Json.Serialization;

namespace TargetBrowse.Services.YouTube.Models;

/// <summary>
/// Enhanced YouTube API configuration settings with comprehensive quota management.
/// </summary>
public class YouTubeApiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public int MaxSearchResults { get; set; } = 10;
    public int DailyQuotaLimit { get; set; } = 10000;

    /// <summary>
    /// Threshold percentage (0-100) at which to trigger quota warnings.
    /// Default: 80% (triggers at 8000/10000 quota used)
    /// </summary>
    public int QuotaWarningThreshold { get; set; } = 80;

    /// <summary>
    /// Threshold percentage (0-100) at which to trigger critical quota alerts.
    /// Default: 95% (triggers at 9500/10000 quota used)
    /// </summary>
    public int QuotaCriticalThreshold { get; set; } = 95;

    /// <summary>
    /// Maximum number of concurrent API requests allowed.
    /// Default: 5 (prevents overwhelming the API)
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// Timeout in seconds for individual API requests.
    /// Default: 30 seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable persistent quota storage to file system.
    /// Default: true (maintains quota across application restarts)
    /// </summary>
    public bool EnablePersistentQuotaStorage { get; set; } = true;

    /// <summary>
    /// File path for storing quota data. If empty, uses default application data folder.
    /// </summary>
    public string QuotaStorageFilePath { get; set; } = string.Empty;

    /// <summary>
    /// UTC hour (0-23) when daily quota resets. 
    /// Default: 0 (midnight UTC, aligns with YouTube's quota reset)
    /// </summary>
    public int QuotaResetHour { get; set; } = 0;

    /// <summary>
    /// Enable detailed quota usage logging.
    /// Default: true (helps with monitoring and debugging)
    /// </summary>
    public bool EnableQuotaLogging { get; set; } = true;
}

/// <summary>
/// Represents different YouTube Data API v3 operations and their quota costs.
/// Based on official YouTube API documentation.
/// </summary>
public enum YouTubeApiOperation
{
    /// <summary>Search for channels - Cost: 100 units per request</summary>
    SearchChannels,

    /// <summary>Get channel details - Cost: 1 unit per request</summary>
    GetChannelDetails,

    /// <summary>Get channel videos - Cost: 1 unit per request</summary>
    GetChannelVideos,

    /// <summary>Search for videos - Cost: 100 units per request</summary>
    SearchVideos,

    /// <summary>Get video details - Cost: 1 unit per request</summary>
    GetVideoDetails,

    /// <summary>Get video comments - Cost: 1 unit per request</summary>
    GetVideoComments,

    /// <summary>Get playlist details - Cost: 1 unit per request</summary>
    GetPlaylistDetails,

    /// <summary>Get playlist items - Cost: 1 unit per request</summary>
    GetPlaylistItems
}

/// <summary>
/// Current status of YouTube API quota usage.
/// </summary>
public class YouTubeQuotaStatus
{
    public int DailyLimit { get; set; }
    public int Used { get; set; }
    public int Remaining => DailyLimit - Used;
    public int Reserved { get; set; }
    public int AvailableForUse => Remaining - Reserved;
    public double UsagePercentage => DailyLimit > 0 ? (double)Used / DailyLimit * 100 : 0;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public DateTime NextReset { get; set; }
    public TimeSpan TimeUntilReset => NextReset - DateTime.UtcNow;
    public bool IsExhausted => AvailableForUse <= 0;
    public bool IsNearLimit { get; set; }
    public bool IsCritical { get; set; }
}

/// <summary>
/// Result of attempting to reserve quota for operations.
/// </summary>
public class QuotaReservationResult
{
    public bool Success { get; set; }
    public string ReservationToken { get; set; } = string.Empty;
    public int ReservedQuota { get; set; }
    public DateTime ReservationExpiry { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public Dictionary<YouTubeApiOperation, int> RequestedOperations { get; set; } = new();
}

/// <summary>
/// Analytics data for quota usage monitoring and reporting.
/// </summary>
public class YouTubeQuotaAnalytics
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalQuotaUsed { get; set; }
    public int MaxDailyUsage { get; set; }
    public int MinDailyUsage { get; set; }
    public double AverageDaily { get; set; }
    public Dictionary<YouTubeApiOperation, int> UsageByOperation { get; set; } = new();
    public List<DailyQuotaUsage> DailyBreakdown { get; set; } = new();
    public int TotalApiCalls { get; set; }
    public int FailedCallsDueToQuota { get; set; }
    public double SuccessRate => TotalApiCalls > 0 ? (double)(TotalApiCalls - FailedCallsDueToQuota) / TotalApiCalls * 100 : 100;
}

/// <summary>
/// Daily quota usage breakdown for analytics.
/// </summary>
public class DailyQuotaUsage
{
    public DateTime Date { get; set; }
    public int QuotaUsed { get; set; }
    public Dictionary<YouTubeApiOperation, int> OperationBreakdown { get; set; } = new();
    public int ApiCalls { get; set; }
    public int FailedCalls { get; set; }
}

/// <summary>
/// Event arguments for quota threshold notifications.
/// </summary>
public class QuotaThresholdEventArgs : EventArgs
{
    public YouTubeQuotaStatus QuotaStatus { get; set; } = new();
    public string ThresholdType { get; set; } = string.Empty; // "Warning" or "Critical"
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for quota exhaustion notifications.
/// </summary>
public class QuotaExhaustedEventArgs : EventArgs
{
    public YouTubeQuotaStatus QuotaStatus { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public DateTime ExhaustedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextResetAt { get; set; }
}

/// <summary>
/// Persistent storage model for quota data.
/// Serialized to JSON for file-based persistence.
/// </summary>
public class QuotaStorageData
{
    public DateTime LastReset { get; set; }
    public int QuotaUsed { get; set; }
    public Dictionary<string, QuotaReservation> ActiveReservations { get; set; } = new();
    public List<DailyQuotaUsage> UsageHistory { get; set; } = new();
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents an active quota reservation.
/// </summary>
public class QuotaReservation
{
    public string Token { get; set; } = string.Empty;
    public int ReservedQuota { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public Dictionary<YouTubeApiOperation, int> Operations { get; set; } = new();
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

/// <summary>
/// Configuration for quota operation costs.
/// Allows for dynamic cost adjustment based on YouTube API changes.
/// </summary>
public static class YouTubeApiOperationCosts
{
    private static readonly Dictionary<YouTubeApiOperation, int> _operationCosts = new()
    {
        { YouTubeApiOperation.SearchChannels, 100 },
        { YouTubeApiOperation.GetChannelDetails, 1 },
        { YouTubeApiOperation.GetChannelVideos, 1 },
        { YouTubeApiOperation.SearchVideos, 100 },
        { YouTubeApiOperation.GetVideoDetails, 1 },
        { YouTubeApiOperation.GetVideoComments, 1 },
        { YouTubeApiOperation.GetPlaylistDetails, 1 },
        { YouTubeApiOperation.GetPlaylistItems, 1 }
    };

    /// <summary>
    /// Gets the quota cost for a specific operation.
    /// </summary>
    /// <param name="operation">YouTube API operation</param>
    /// <returns>Quota cost in units</returns>
    public static int GetCost(YouTubeApiOperation operation)
    {
        return _operationCosts.TryGetValue(operation, out var cost) ? cost : 1;
    }

    /// <summary>
    /// Gets all operation costs for reference.
    /// </summary>
    /// <returns>Dictionary of operations and their costs</returns>
    public static IReadOnlyDictionary<YouTubeApiOperation, int> GetAllCosts()
    {
        return _operationCosts.AsReadOnly();
    }
}