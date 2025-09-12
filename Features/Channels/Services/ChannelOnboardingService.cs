using Microsoft.Extensions.Logging;
using TargetBrowse.Features.Channels.Data;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Features.Videos.Data;
using TargetBrowse.Features.Suggestions.Data;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Handles channel onboarding workflows including initial video suggestions.
/// Owns the complete user journey for adding a new channel and getting immediate value.
/// </summary>
public class ChannelOnboardingService : IChannelOnboardingService
{
    private readonly IChannelRepository _channelRepository;
    private readonly IVideoRepository _videoRepository;
    private readonly ISuggestionRepository _suggestionRepository;
    private readonly IChannelYouTubeService _youTubeService;
    private readonly ILogger<ChannelOnboardingService> _logger;

    private const int InitialVideosLimit = 50;
    private const int LookbackDays = 365; // Look back up to a year for initial videos

    public ChannelOnboardingService(
        IChannelRepository channelRepository,
        IVideoRepository videoRepository,
        ISuggestionRepository suggestionRepository,
        IChannelYouTubeService youTubeService,
        ILogger<ChannelOnboardingService> logger)
    {
        _channelRepository = channelRepository;
        _videoRepository = videoRepository;
        _suggestionRepository = suggestionRepository;
        _youTubeService = youTubeService;
        _logger = logger;
    }

    /// <summary>
    /// Adds initial videos from a newly tracked channel as suggestions.
    /// </summary>
    public async Task<int> AddInitialVideosAsync(string userId, string youTubeChannelId, string channelName)
    {
        try
        {
            _logger.LogInformation("Adding initial videos for channel {ChannelName} ({ChannelId}) for user {UserId}",
                channelName, youTubeChannelId, userId);

            // 1. Fetch recent videos from the channel using our YouTube service
            var channelVideosResult = await FetchChannelVideosAsync(youTubeChannelId, channelName);

            if (!channelVideosResult.IsSuccess || !channelVideosResult.Data?.Any() == true)
            {
                _logger.LogWarning("Failed to fetch initial videos for channel {ChannelName}: {Error}",
                    channelName, channelVideosResult.ErrorMessage);
                return 0;
            }

            var videos = channelVideosResult.Data.Take(InitialVideosLimit).ToList();
            _logger.LogInformation("Found {VideoCount} initial videos for channel {ChannelName}",
                videos.Count, channelName);

            // 2. Ensure all videos exist in the database (shared repository)
            var videoEntities = await _videoRepository.EnsureVideosExistAsync(videos);

            // 3. Create suggestion entities for initial videos
            var suggestions = await CreateInitialVideoSuggestions(userId, videoEntities, channelName);

            // 4. Save suggestions to database (bypassing normal limits)
            if (suggestions.Any())
            {
                var createdSuggestions = await _suggestionRepository.CreateSuggestionsAsync(suggestions);

                _logger.LogInformation("Created {SuggestionCount} initial suggestions for channel {ChannelName}",
                    createdSuggestions.Count(), channelName);

                return createdSuggestions.Count();
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding initial videos for channel {ChannelName} for user {UserId}",
                channelName, userId);
            return 0;
        }
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
    /// Fetches recent videos from a YouTube channel.
    /// </summary>
    private async Task<YouTubeApiResult<List<VideoInfo>>> FetchChannelVideosAsync(string youTubeChannelId, string channelName)
    {
        try
        {
            // Create a channel update request to get recent videos
            var channelRequest = new ChannelUpdateRequest
            {
                YouTubeChannelId = youTubeChannelId,
                ChannelName = channelName,
                LastCheckDate = DateTime.UtcNow.AddDays(-LookbackDays), // Look back far for initial videos
                MaxResults = InitialVideosLimit,
                UserRating = null // No rating yet for new channel
            };

            // Use the existing bulk channel updates method
            var apiResult = await _youTubeService.GetBulkChannelUpdatesAsync(new List<ChannelUpdateRequest> { channelRequest });

            if (apiResult.IsSuccess && apiResult.Data?.Any() == true)
            {
                return YouTubeApiResult<List<VideoInfo>>.Success(apiResult.Data);
            }
            else
            {
                return YouTubeApiResult<List<VideoInfo>>.Failure(
                    apiResult.ErrorMessage ?? "No videos found for channel");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching videos for channel {ChannelId}", youTubeChannelId);
            return YouTubeApiResult<List<VideoInfo>>.Failure($"Failed to fetch channel videos: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates suggestion entities for initial videos from a new channel.
    /// </summary>
    private async Task<List<SuggestionEntity>> CreateInitialVideoSuggestions(
        string userId, List<VideoEntity> videoEntities, string channelName)
    {
        var suggestions = new List<SuggestionEntity>();

        foreach (var videoEntity in videoEntities)
        {
            // Check if suggestion already exists to avoid duplicates
            var hasExisting = await _suggestionRepository.HasPendingSuggestionForVideoAsync(userId, videoEntity.Id);
            if (hasExisting)
            {
                _logger.LogDebug("Skipping video {VideoId} - suggestion already exists for user {UserId}",
                    videoEntity.YouTubeVideoId, userId);
                continue;
            }

            suggestions.Add(new SuggestionEntity
            {
                UserId = userId,
                VideoId = videoEntity.Id,
                Reason = $"🎯 New Channel: {channelName}",
                IsApproved = false,
                IsDenied = false
                // Note: SuggestionSource.NewChannel will be handled by the display logic
            });
        }

        _logger.LogDebug("Created {SuggestionCount} suggestion entities from {VideoCount} videos",
            suggestions.Count, videoEntities.Count);

        return suggestions;
    }

    #endregion
}