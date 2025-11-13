namespace TargetBrowse.Services.Models;

/// <summary>
/// Represents the result of a video transcript summarization operation
/// </summary>
public class SummaryResult
{
    /// <summary>
    /// Indicates if the operation completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The ID of the video being summarized
    /// </summary>
    public Guid VideoId { get; set; }

    /// <summary>
    /// The generated summary content
    /// </summary>
    public string? SummaryContent { get; set; }

    /// <summary>
    /// The ID of the created or existing summary entity
    /// </summary>
    public Guid? SummaryId { get; set; }

    /// <summary>
    /// Indicates if the operation was skipped
    /// </summary>
    public bool Skipped { get; set; }

    /// <summary>
    /// Reason for skipping the operation (e.g., "Summary already exists", "No transcript available")
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    /// Number of tokens used in the API request (for cost tracking)
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// Number of tokens used in the API response (for cost tracking)
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// Estimated cost of the API call in USD
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Duration of the API call in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Indicates if the transcript was truncated due to token limits
    /// </summary>
    public bool WasTruncated { get; set; }
}
