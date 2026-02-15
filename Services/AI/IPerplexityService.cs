using TargetBrowse.Services.AI.Models;

namespace TargetBrowse.Services.AI;

/// <summary>
/// Service for searching YouTube videos using Perplexity's Sonar API.
/// </summary>
public interface IPerplexityService
{
    /// <summary>
    /// Searches for relevant YouTube videos based on project context.
    /// Uses Perplexity's web search restricted to youtube.com with recency bias.
    /// </summary>
    /// <param name="projectName">The project name</param>
    /// <param name="description">The project description</param>
    /// <param name="userGuidance">AI guidance text describing what videos to find</param>
    /// <param name="userId">User ID for AI call tracking</param>
    /// <returns>Search result with video results and AI suggestions</returns>
    Task<PerplexitySearchResult> SearchVideosAsync(string projectName, string? description, string userGuidance, string? userId);
}
