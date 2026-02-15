using Microsoft.Extensions.Logging;
using TargetBrowse.Services.AI;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Projects.Services;

/// <summary>
/// Orchestrates AI-powered video search: loads project context, calls Perplexity for video discovery,
/// enriches results with YouTube API, and filters out videos already in the project.
/// </summary>
public class ProjectVideoSearchService : IProjectVideoSearchService
{
    private readonly IProjectService _projectService;
    private readonly IPerplexityService _perplexityService;
    private readonly ISharedYouTubeService _sharedYouTubeService;
    private readonly IProjectGuideService _guideService;
    private readonly ILogger<ProjectVideoSearchService> _logger;

    private const int DailyAICallLimit = 10;

    public ProjectVideoSearchService(
        IProjectService projectService,
        IPerplexityService perplexityService,
        ISharedYouTubeService sharedYouTubeService,
        IProjectGuideService guideService,
        ILogger<ProjectVideoSearchService> logger)
    {
        _projectService = projectService;
        _perplexityService = perplexityService;
        _sharedYouTubeService = sharedYouTubeService;
        _guideService = guideService;
        _logger = logger;
    }

    public async Task<ProjectVideoSearchResult> SearchVideosAsync(Guid projectId, string userId, string? customQuery = null)
    {
        try
        {
            // Check daily AI limit
            var dailyCount = await _guideService.GetDailyAICallCountAsync(userId);
            if (dailyCount >= DailyAICallLimit)
            {
                return ProjectVideoSearchResult.CreateFailure(
                    $"Daily AI call limit reached ({DailyAICallLimit}/day). Try again tomorrow.");
            }

            // Load project
            var project = await _projectService.GetProjectDetailAsync(projectId, userId);
            if (project == null)
            {
                return ProjectVideoSearchResult.CreateFailure("Project not found.");
            }

            // Use custom query if provided, otherwise fall back to UserGuidance
            var searchGuidance = !string.IsNullOrWhiteSpace(customQuery) ? customQuery : project.UserGuidance;

            if (string.IsNullOrWhiteSpace(searchGuidance))
            {
                return ProjectVideoSearchResult.CreateFailure(
                    "Enter a search query or set AI Guidance in project settings.");
            }

            // Search with Perplexity
            var searchResult = await _perplexityService.SearchVideosAsync(
                project.Name, project.Description, searchGuidance, userId);

            if (searchResult.Videos.Count == 0)
            {
                return ProjectVideoSearchResult.CreateFailure(
                    "No videos found. Try adjusting your AI Guidance to be more specific.");
            }

            // Get existing video IDs in the project to filter duplicates
            var existingYouTubeIds = project.Videos
                .Select(v => v.YouTubeVideoId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Build reason lookup before filtering
            var reasonLookup = searchResult.Videos.ToDictionary(
                r => r.YouTubeVideoId,
                r => r.Reason,
                StringComparer.OrdinalIgnoreCase);

            // Get video IDs that aren't already in the project
            var newVideoIds = searchResult.Videos
                .Select(r => r.YouTubeVideoId)
                .Where(id => !existingYouTubeIds.Contains(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (newVideoIds.Count == 0)
            {
                return ProjectVideoSearchResult.CreateFailure(
                    "All found videos are already in this project.");
            }

            // Enrich with YouTube API
            var youtubeResult = await _sharedYouTubeService.GetVideoDetailsByIdsAsync(newVideoIds);
            if (!youtubeResult.IsSuccess || youtubeResult.Data == null || youtubeResult.Data.Count == 0)
            {
                return ProjectVideoSearchResult.CreateFailure(
                    "Found videos but could not retrieve details from YouTube. Try again later.");
            }

            // Combine YouTube details with relevance reasons
            var items = youtubeResult.Data.Select(videoInfo => new VideoSearchItem
            {
                VideoInfo = videoInfo,
                Reason = reasonLookup.GetValueOrDefault(videoInfo.YouTubeVideoId, "")
            }).ToList();

            _logger.LogInformation("AI video search for project {ProjectId} returned {Count} new videos",
                projectId, items.Count);

            return ProjectVideoSearchResult.CreateSuccess(items, searchResult.AISuggestion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI video search for project {ProjectId}", projectId);
            return ProjectVideoSearchResult.CreateFailure("An unexpected error occurred during search. Please try again.");
        }
    }
}
