using Newtonsoft.Json;

namespace TargetBrowse.Services.Models;

/// <summary>
/// Represents the complete classification result from OpenAI API
/// </summary>
public class ClassificationResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    [JsonProperty("classifications")]
    public List<VideoClassification> Classifications { get; set; } = new List<VideoClassification>();

    /// <summary>
    /// Total number of videos that were attempted to be classified
    /// </summary>
    public int TotalVideos { get; set; }

    /// <summary>
    /// Number of videos successfully classified
    /// </summary>
    public int SuccessfulClassifications { get; set; }

    /// <summary>
    /// Statistics about the classification results
    /// </summary>
    public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();

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
    public int? DurationMs { get; set; }
}
