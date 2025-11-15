using Microsoft.Extensions.Logging;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Services;

/// <summary>
/// Abstract base class for onboarding services that provides common workflow pattern
/// for adding initial videos when users create new channels or topics.
/// Eliminates code duplication between ChannelOnboardingService and TopicOnboardingService.
/// </summary>
public abstract class BaseOnboardingService
{
    protected readonly ISuggestionDataService _suggestionDataService;
    protected readonly ISharedYouTubeService _sharedYouTubeService;
    protected readonly ILogger _logger;

    protected const int InitialVideosLimit = 100;

    protected BaseOnboardingService(
        ISuggestionDataService suggestionDataService,
        ISharedYouTubeService sharedYouTubeService,
        ILogger logger)
    {
        _suggestionDataService = suggestionDataService;
        _sharedYouTubeService = sharedYouTubeService;
        _logger = logger;
    }

    /// <summary>
    /// Template method that executes the common onboarding workflow.
    /// Orchestrates the process of fetching videos, filtering them, and creating suggestions.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="entityName">Name of the entity (channel or topic) being onboarded</param>
    /// <param name="fetchVideos">Function to fetch videos from YouTube</param>
    /// <param name="selectVideos">Function to select/filter which videos to use</param>
    /// <param name="createSuggestions">Function to create suggestion entities</param>
    /// <returns>Number of suggestions created</returns>
    protected async Task<int> ExecuteOnboardingWorkflowAsync(
        string userId,
        string entityName,
        Func<Task<YouTubeApiResult<List<VideoInfo>>>> fetchVideos,
        Func<List<VideoInfo>, List<VideoInfo>> selectVideos,
        Func<List<VideoEntity>, Task<List<SuggestionEntity>>> createSuggestions)
    {
        try
        {
            // 1. Check if user can create more suggestions
            if (!await _suggestionDataService.CanUserCreateSuggestionsAsync(userId))
            {
                _logger.LogWarning("User {UserId} cannot create more suggestions - at limit", userId);
                return 0;
            }

            // 2. Fetch videos from YouTube (feature-specific)
            var videosResult = await fetchVideos();

            if (!videosResult.IsSuccess || !videosResult.Data?.Any() == true)
            {
                _logger.LogWarning("Failed to fetch initial videos for {EntityName}: {Error}",
                    entityName, videosResult.ErrorMessage);
                return 0;
            }

            _logger.LogInformation("Found {VideoCount} initial videos for {EntityName}",
                videosResult.Data.Count, entityName);

            // 3. Select videos to use (feature-specific filtering/scoring)
            var selectedVideos = selectVideos(videosResult.Data);

            if (!selectedVideos.Any())
            {
                _logger.LogInformation("No videos selected after filtering for {EntityName}", entityName);
                return 0;
            }

            // 4. Ensure all videos exist in the database using shared service
            var videoEntities = await _suggestionDataService.EnsureVideosExistAsync(selectedVideos);

            // 5. Filter out videos that already have pending suggestions
            var filteredVideoEntities = await FilterPendingSuggestionsAsync(userId, videoEntities);

            // 6. Create suggestions using feature-specific method
            if (filteredVideoEntities.Any())
            {
                var createdSuggestions = await createSuggestions(filteredVideoEntities);

                _logger.LogInformation("Created {SuggestionCount} initial suggestions for {EntityName} (filtered from {TotalCount} videos)",
                    createdSuggestions.Count, entityName, videoEntities.Count);

                return createdSuggestions.Count;
            }

            _logger.LogInformation("No new suggestions created for {EntityName} - all videos already have pending suggestions", entityName);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding initial videos for {EntityName} for user {UserId}",
                entityName, userId);
            return 0;
        }
    }

    /// <summary>
    /// Filters out video entities that already have pending suggestions for the user.
    /// Common helper method used by both channel and topic onboarding.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoEntities">List of video entities to filter</param>
    /// <returns>List of video entities without pending suggestions</returns>
    protected async Task<List<VideoEntity>> FilterPendingSuggestionsAsync(
        string userId,
        List<VideoEntity> videoEntities)
    {
        var filteredVideoEntities = new List<VideoEntity>();

        foreach (var videoEntity in videoEntities)
        {
            var hasPending = await _suggestionDataService.HasPendingSuggestionForVideoAsync(userId, videoEntity.Id);
            if (!hasPending)
            {
                filteredVideoEntities.Add(videoEntity);
            }
            else
            {
                _logger.LogDebug("Skipping video {VideoId} - already has pending suggestion for user {UserId}",
                    videoEntity.YouTubeVideoId, userId);
            }
        }

        return filteredVideoEntities;
    }
}
