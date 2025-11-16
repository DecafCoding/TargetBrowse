using Microsoft.Extensions.Logging;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Channels.Data;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Services.Models;
using TargetBrowse.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Handles channel onboarding workflows including initial video suggestions.
/// Owns the complete user journey for adding a new channel and getting immediate value.
/// Refactored to use BaseOnboardingService to eliminate duplication with TopicOnboardingService.
/// Uses SharedYouTubeService to ensure consistent shorts exclusion with suggestion generation.
/// </summary>
public class ChannelOnboardingService : BaseOnboardingService, IChannelOnboardingService
{
    private readonly IChannelRepository _channelRepository;

    public ChannelOnboardingService(
        IChannelRepository channelRepository,
        ISuggestionDataService suggestionDataService,
        ISharedYouTubeService sharedYouTubeService,
        ILogger<ChannelOnboardingService> logger)
        : base(suggestionDataService, sharedYouTubeService, logger)
    {
        _channelRepository = channelRepository;
    }

    /// <summary>
    /// Adds initial videos from a newly tracked channel as suggestions.
    /// Uses the common onboarding workflow from BaseOnboardingService.
    /// </summary>
    public async Task<int> AddInitialVideosAsync(string userId, string youTubeChannelId, string channelName)
    {
        _logger.LogInformation("Adding initial videos for channel {ChannelName} ({ChannelId}) for user {UserId}",
            channelName, youTubeChannelId, userId);

        return await ExecuteOnboardingWorkflowAsync(
            userId,
            channelName,
            fetchVideos: () => FetchChannelVideosAsync(youTubeChannelId, channelName),
            selectVideos: (videos) => videos.Take(InitialVideosLimit).ToList(),
            createSuggestions: (videoEntities) => _suggestionDataService.CreateChannelOnboardingSuggestionsAsync(
                userId, videoEntities, channelName));
    }

    /// <summary>
    /// Performs complete channel onboarding including tracking setup and initial videos.
    /// </summary>
    public async Task<ChannelOnboardingResult> OnboardChannelAsync(string userId, AddChannelModel channelModel)
    {
        var result = new ChannelOnboardingResult();

        try
        {
            _logger.LogInformation("Starting channel onboarding for {ChannelName} for user {UserId}",
                channelModel.Name, userId);

            // 1. Validate input
            if (!ChannelMappingService.IsValidAddChannelModel(channelModel))
            {
                result.Errors.Add("Invalid channel data. Please try searching again.");
                return result;
            }

            // 2. Check channel limits
            var currentCount = await _channelRepository.GetTrackedChannelCountAsync(userId);
            if (currentCount >= 50) // Max channels per user
            {
                result.Errors.Add("You have reached the maximum limit of 50 tracked channels. Remove some channels before adding new ones.");
                return result;
            }

            // 3. Check for duplicates
            if (await _channelRepository.IsChannelTrackedByUserAsync(userId, channelModel.YouTubeChannelId))
            {
                result.Errors.Add($"Channel '{channelModel.Name}' is already in your tracking list.");
                return result;
            }

            // 4. Add channel to tracking
            var channelEntity = await _channelRepository.FindOrCreateChannelAsync(
                channelModel.YouTubeChannelId,
                channelModel.Name,
                channelModel.Description,
                channelModel.ThumbnailUrl,
                channelModel.SubscriberCount,
                channelModel.VideoCount,
                channelModel.PublishedAt);

            await _channelRepository.AddChannelToUserTrackingAsync(userId, channelEntity.Id);
            result.ChannelAdded = true;

            _logger.LogInformation("Successfully added channel {ChannelName} to tracking for user {UserId}",
                channelModel.Name, userId);

            // 5. Add initial videos (non-blocking - don't fail if this doesn't work)
            try
            {
                result.InitialVideosAdded = await AddInitialVideosAsync(
                    userId, channelModel.YouTubeChannelId, channelModel.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add initial videos for channel {ChannelName}, but channel was added successfully",
                    channelModel.Name);
                result.Warnings.Add("Channel added successfully, but could not retrieve recent videos at this time.");
            }

            _logger.LogInformation("Channel onboarding completed for {ChannelName}: {VideoCount} initial videos added",
                channelModel.Name, result.InitialVideosAdded);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during channel onboarding for {ChannelName} and user {UserId}",
                channelModel.Name, userId);
            result.Errors.Add("An unexpected error occurred during channel setup. Please try again.");
            return result;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Fetches recent videos from a YouTube channel, excluding shorts.
    /// Uses SharedYouTubeService to ensure consistent filtering with suggestion generation.
    /// </summary>
    private async Task<YouTubeApiResult<List<VideoInfo>>> FetchChannelVideosAsync(string youTubeChannelId, string channelName)
    {
        try
        {
            _logger.LogInformation("Fetching initial videos for channel {ChannelName} ({ChannelId}) - excluding shorts",
                channelName, youTubeChannelId);

            // Use SharedYouTubeService directly to get videos since lookback date
            // This ensures we use the same shorts-exclusion logic as suggestion generation
            // var lookbackDate = DateTime.UtcNow.AddDays(-LookbackDays);

            var apiResult = await _sharedYouTubeService.GetChannelVideosFromAPI(youTubeChannelId);

            if (apiResult.IsSuccess && apiResult.Data?.Any() == true)
            {
                _logger.LogInformation("Successfully fetched {VideoCount} videos for channel {ChannelName} (shorts excluded)",
                    apiResult.Data.Count, channelName);

                // Update LastCheckDate to now since we successfully checked the channel
                await _channelRepository.UpdateLastCheckDateAsync(youTubeChannelId, DateTime.UtcNow);

                return YouTubeApiResult<List<VideoInfo>>.Success(apiResult.Data);
            }
            else
            {
                var errorMessage = apiResult.ErrorMessage ?? "No videos found for channel";
                _logger.LogWarning("Failed to fetch videos for channel {ChannelName}: {Error}",
                    channelName, errorMessage);

                return YouTubeApiResult<List<VideoInfo>>.Failure(errorMessage, apiResult.IsQuotaExceeded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching videos for channel {ChannelId}", youTubeChannelId);
            return YouTubeApiResult<List<VideoInfo>>.Failure($"Failed to fetch channel videos: {ex.Message}");
        }
    }

    #endregion
}