namespace TargetBrowse.Services.AI.Models;

/// <summary>
/// Represents a single video result from AI-powered video search.
/// </summary>
public class VideoSearchResult
{
    public string YouTubeVideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Complete result from a Perplexity video search including AI suggestions.
/// </summary>
public class PerplexitySearchResult
{
    public List<VideoSearchResult> Videos { get; set; } = new();

    /// <summary>
    /// AI-generated suggestion for refining the search or improving results.
    /// </summary>
    public string? AISuggestion { get; set; }
}
