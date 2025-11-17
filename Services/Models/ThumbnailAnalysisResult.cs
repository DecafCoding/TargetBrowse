namespace TargetBrowse.Services.Models;

/// <summary>
/// Result of a thumbnail analysis operation.
/// Contains the AI-generated description and metadata about the API call.
/// </summary>
public class ThumbnailAnalysisResult
{
    /// <summary>
    /// Whether the analysis was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The AI-generated description of the thumbnail
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Number of input tokens used in the API call
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// Number of output tokens used in the API call
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// Total cost of the API call in USD
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Duration of the API call in milliseconds
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// Error message if the analysis failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The thumbnail URL that was analyzed
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// The video title that was provided
    /// </summary>
    public string? VideoTitle { get; set; }
}
