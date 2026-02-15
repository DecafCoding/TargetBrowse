using TargetBrowse.Services.AI.Models;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Projects.Services;

/// <summary>
/// Orchestrates AI-powered video search for projects.
/// Combines Perplexity search with YouTube API enrichment.
/// </summary>
public interface IProjectVideoSearchService
{
    /// <summary>
    /// Searches for relevant YouTube videos using the project's guidance,
    /// enriches results with YouTube API data, and filters out already-added videos.
    /// </summary>
    /// <param name="projectId">The project to search videos for</param>
    /// <param name="userId">The authenticated user's ID</param>
    /// <param name="customQuery">Optional custom search query that overrides UserGuidance</param>
    /// <returns>Search result containing enriched video info and relevance reasons</returns>
    Task<ProjectVideoSearchResult> SearchVideosAsync(Guid projectId, string userId, string? customQuery = null);
}

/// <summary>
/// Result of an AI-powered project video search.
/// </summary>
public class ProjectVideoSearchResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<VideoSearchItem> Videos { get; set; } = new();

    /// <summary>
    /// AI-generated suggestion for refining the search or improving results.
    /// </summary>
    public string? AISuggestion { get; set; }

    public static ProjectVideoSearchResult CreateSuccess(List<VideoSearchItem> videos, string? aiSuggestion = null)
        => new() { Success = true, Videos = videos, AISuggestion = aiSuggestion };

    public static ProjectVideoSearchResult CreateFailure(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// A single video search result with YouTube details and AI relevance reason.
/// </summary>
public class VideoSearchItem
{
    public VideoInfo VideoInfo { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}
