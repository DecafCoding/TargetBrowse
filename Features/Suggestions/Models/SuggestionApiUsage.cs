namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Enhanced API usage tracking with detailed metrics.
/// </summary>
public class SuggestionApiUsage
{
    /// <summary>
    /// YouTube API calls made for channel video searches.
    /// </summary>
    public int ChannelSearchCalls { get; set; }

    /// <summary>
    /// YouTube API calls made for topic searches.
    /// </summary>
    public int TopicSearchCalls { get; set; }

    /// <summary>
    /// YouTube API calls made for video details.
    /// </summary>
    public int VideoDetailCalls { get; set; }

    /// <summary>
    /// YouTube API calls made for validation operations.
    /// </summary>
    public int ValidationCalls { get; set; }

    /// <summary>
    /// Total estimated quota units used during this operation.
    /// </summary>
    public int EstimatedQuotaUsed { get; set; }

    /// <summary>
    /// Actual quota units used (if available from API response headers).
    /// </summary>
    public int? ActualQuotaUsed { get; set; }

    /// <summary>
    /// Duration of all API operations combined.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Number of API errors encountered.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Number of cache hits that avoided API calls.
    /// </summary>
    public int CacheHits { get; set; }

    /// <summary>
    /// Breakdown of API calls by operation type.
    /// </summary>
    public Dictionary<string, ApiOperationStats> OperationStats { get; set; } = new();

    /// <summary>
    /// Gets total API calls made.
    /// </summary>
    public int TotalApiCalls => ChannelSearchCalls + TopicSearchCalls + VideoDetailCalls + ValidationCalls;

    /// <summary>
    /// Gets efficiency metric (successful calls / total calls).
    /// </summary>
    public double SuccessRate => TotalApiCalls > 0 ? (double)(TotalApiCalls - ErrorCount) / TotalApiCalls : 1.0;

    /// <summary>
    /// Gets formatted quota usage for display.
    /// </summary>
    public string FormattedQuotaUsage => $"{EstimatedQuotaUsed:N0} units";

    /// <summary>
    /// Gets average response time across all calls.
    /// </summary>
    public double AverageResponseTimeMs => OperationStats.Values.Any()
        ? OperationStats.Values.Average(s => s.AverageResponseTimeMs)
        : 0;

    /// <summary>
    /// Adds statistics for a specific operation type.
    /// </summary>
    public void AddOperationStats(string operationType, int calls, int quotaCost, TimeSpan duration, int errors = 0)
    {
        if (!OperationStats.ContainsKey(operationType))
        {
            OperationStats[operationType] = new ApiOperationStats { OperationType = operationType };
        }

        var stats = OperationStats[operationType];
        stats.CallCount += calls;
        stats.QuotaUsed += quotaCost;
        stats.TotalDuration += duration;
        stats.ErrorCount += errors;
    }
}
