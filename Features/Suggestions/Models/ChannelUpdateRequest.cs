namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Represents a request to check for channel updates.
/// Used for bulk channel update operations.
/// </summary>
public class ChannelUpdateRequest
{
    /// <summary>
    /// YouTube channel identifier.
    /// </summary>
    public string YouTubeChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Channel display name for logging and error messages.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Last time this channel was checked for updates.
    /// </summary>
    public DateTime LastCheckDate { get; set; }

    /// <summary>
    /// User's rating for this channel (affects filtering).
    /// </summary>
    public int? UserRating { get; set; }

    /// <summary>
    /// Maximum number of results to retrieve for this specific channel.
    /// </summary>
    public int MaxResults { get; set; } = 50;
}

/// <summary>
/// Comprehensive API availability information.
/// </summary>
public class ApiAvailabilityResult
{
    /// <summary>
    /// Whether the API is currently available for requests.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Whether quota limits are preventing API usage.
    /// </summary>
    public bool IsQuotaExceeded { get; set; }

    /// <summary>
    /// Current quota usage percentage (0-100).
    /// </summary>
    public double QuotaUsagePercentage { get; set; }

    /// <summary>
    /// Estimated remaining quota units for today.
    /// </summary>
    public int EstimatedRemainingQuota { get; set; }

    /// <summary>
    /// When the quota will reset (usually midnight UTC).
    /// </summary>
    public DateTime QuotaResetTime { get; set; }

    /// <summary>
    /// Any error message if API is not available.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Last successful API call timestamp.
    /// </summary>
    public DateTime? LastSuccessfulCall { get; set; }

    /// <summary>
    /// Whether the API key is valid and authenticated.
    /// </summary>
    public bool IsApiKeyValid { get; set; }
}

/// <summary>
/// Detailed quota cost estimation for planning API usage.
/// </summary>
public class QuotaCostEstimate
{
    /// <summary>
    /// Estimated cost for channel update searches.
    /// </summary>
    public int ChannelUpdatesCost { get; set; }

    /// <summary>
    /// Estimated cost for topic-based searches.
    /// </summary>
    public int TopicSearchesCost { get; set; }

    /// <summary>
    /// Estimated cost for video details retrieval.
    /// </summary>
    public int VideoDetailsCost { get; set; }

    /// <summary>
    /// Total estimated quota cost for the operation.
    /// </summary>
    public int TotalEstimatedCost { get; set; }

    /// <summary>
    /// Whether the estimated cost exceeds remaining quota.
    /// </summary>
    public bool ExceedsRemainingQuota { get; set; }

    /// <summary>
    /// Breakdown of costs by operation type.
    /// </summary>
    public Dictionary<string, int> CostBreakdown { get; set; } = new();

    /// <summary>
    /// Recommendations for reducing quota usage.
    /// </summary>
    public List<string> OptimizationSuggestions { get; set; } = new();

    /// <summary>
    /// Expected quota usage percentage after this operation.
    /// </summary>
    public double ProjectedQuotaUsagePercentage { get; set; }
}

/// <summary>
/// Current quota status information.
/// </summary>
public class QuotaStatus
{
    /// <summary>
    /// Total daily quota limit.
    /// </summary>
    public int DailyQuotaLimit { get; set; }

    /// <summary>
    /// Quota used so far today.
    /// </summary>
    public int QuotaUsedToday { get; set; }

    /// <summary>
    /// Remaining quota for today.
    /// </summary>
    public int RemainingQuota => Math.Max(0, DailyQuotaLimit - QuotaUsedToday);

    /// <summary>
    /// Percentage of daily quota used.
    /// </summary>
    public double UsagePercentage => DailyQuotaLimit > 0
        ? Math.Min(100, (double)QuotaUsedToday / DailyQuotaLimit * 100)
        : 0;

    /// <summary>
    /// When the quota will reset.
    /// </summary>
    public DateTime ResetTime { get; set; }

    /// <summary>
    /// Time until quota reset.
    /// </summary>
    public TimeSpan TimeUntilReset => ResetTime > DateTime.UtcNow
        ? ResetTime - DateTime.UtcNow
        : TimeSpan.Zero;

    /// <summary>
    /// Whether quota usage is approaching the limit (>80%).
    /// </summary>
    public bool IsApproachingLimit => UsagePercentage > 80;

    /// <summary>
    /// Whether quota has been exceeded.
    /// </summary>
    public bool IsExceeded => QuotaUsedToday >= DailyQuotaLimit;

    /// <summary>
    /// Recent API call statistics.
    /// </summary>
    public List<ApiCallRecord> RecentCalls { get; set; } = new();

    /// <summary>
    /// Gets formatted status message for user display.
    /// </summary>
    public string GetStatusMessage()
    {
        if (IsExceeded)
            return $"Daily quota exceeded. Resets at {ResetTime:HH:mm} UTC.";

        if (IsApproachingLimit)
            return $"Quota usage: {UsagePercentage:F1}% ({RemainingQuota:N0} remaining)";

        return $"Quota available: {RemainingQuota:N0} of {DailyQuotaLimit:N0} remaining";
    }
}

/// <summary>
/// Record of an individual API call for tracking and analytics.
/// </summary>
public class ApiCallRecord
{
    /// <summary>
    /// When the API call was made.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Type of API operation (search, details, etc.).
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Quota cost of the operation.
    /// </summary>
    public int QuotaCost { get; set; }

    /// <summary>
    /// Whether the call was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Error message if the call failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; set; }

    /// <summary>
    /// Number of items returned (for successful calls).
    /// </summary>
    public int? ItemsReturned { get; set; }
}



/// <summary>
/// Statistics for a specific type of API operation.
/// </summary>
public class ApiOperationStats
{
    /// <summary>
    /// Type of operation (search, details, etc.).
    /// </summary>
    public string OperationType { get; set; } = string.Empty;

    /// <summary>
    /// Number of calls made for this operation type.
    /// </summary>
    public int CallCount { get; set; }

    /// <summary>
    /// Total quota used by this operation type.
    /// </summary>
    public int QuotaUsed { get; set; }

    /// <summary>
    /// Total time spent on this operation type.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Number of errors for this operation type.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Average response time for this operation type.
    /// </summary>
    public double AverageResponseTimeMs => CallCount > 0 ? TotalDuration.TotalMilliseconds / CallCount : 0;

    /// <summary>
    /// Success rate for this operation type.
    /// </summary>
    public double SuccessRate => CallCount > 0 ? (double)(CallCount - ErrorCount) / CallCount : 1.0;

    /// <summary>
    /// Average quota cost per call for this operation type.
    /// </summary>
    public double AverageQuotaPerCall => CallCount > 0 ? (double)QuotaUsed / CallCount : 0;
}