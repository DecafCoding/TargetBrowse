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
}
