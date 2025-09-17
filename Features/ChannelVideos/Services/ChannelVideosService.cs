using TargetBrowse.Features.ChannelVideos.Data;
using TargetBrowse.Features.ChannelVideos.Models;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.ChannelVideos.Services;

/// <summary>
/// Service implementation for channel videos functionality.
/// Now uses shared YouTube service instead of Suggestions-specific service.
/// </summary>
public class ChannelVideosService : IChannelVideosService
{
    private readonly IChannelVideosRepository _repository;
    private readonly ISharedYouTubeService _youTubeService; // Updated to use shared service
    private readonly IMessageCenterService _messageCenter;
    private readonly ILogger<ChannelVideosService> _logger;

    public ChannelVideosService(
        IChannelVideosRepository repository,
        ISharedYouTubeService youTubeService, // Updated to use shared service
        IMessageCenterService messageCenter,
        ILogger<ChannelVideosService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _youTubeService = youTubeService ?? throw new ArgumentNullException(nameof(youTubeService));
        _messageCenter = messageCenter ?? throw new ArgumentNullException(nameof(messageCenter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets recent videos from a channel along with channel information.
    /// Uses shared YouTube service with 1 year lookback as specified.
    /// </summary>
    public async Task<ChannelVideosModel> GetChannelVideosAsync(string youTubeChannelId, string userId)
    {
        var model = new ChannelVideosModel
        {
            IsLoading = true
        };

        try
        {
            if (string.IsNullOrWhiteSpace(youTubeChannelId))
            {
                model.ErrorMessage = "Invalid channel ID provided.";
                model.IsLoading = false;
                return model;
            }

            _logger.LogInformation("Loading channel videos for channel {ChannelId}", youTubeChannelId);

            // Get channel info from database
            var channelInfo = await _repository.GetChannelInfoAsync(youTubeChannelId);
            if (channelInfo == null)
            {
                model.ErrorMessage = "Channel not found in your tracked channels.";
                model.IsLoading = false;
                return model;
            }

            model.Channel = channelInfo;

            // Get user tracking and rating status
            if (!string.IsNullOrWhiteSpace(userId))
            {
                model.IsTrackedByUser = await _repository.IsChannelTrackedByUserAsync(userId, youTubeChannelId);
                model.UserRating = await _repository.GetUserChannelRatingAsync(userId, youTubeChannelId);
            }

            // Get recent videos from YouTube API (1 year lookback as specified)
            var oneYearAgo = DateTime.UtcNow.AddYears(-1);
            var videosResult = await _youTubeService.GetChannelVideosSinceAsync(
                youTubeChannelId,
                oneYearAgo,
                maxResults: 50); // Use default from service

            if (!videosResult.IsSuccess)
            {
                _logger.LogWarning("Failed to get videos for channel {ChannelId}: {Error}",
                    youTubeChannelId, videosResult.ErrorMessage);

                if (videosResult.IsQuotaExceeded)
                {
                    model.ErrorMessage = "YouTube API quota exceeded. Please try again later.";
                }
                else
                {
                    model.ErrorMessage = "Unable to load recent videos. Please try again later.";
                }

                model.IsLoading = false;
                return model;
            }

            // Convert to channel video models
            var videos = videosResult.Data ?? new List<Features.Suggestions.Models.VideoInfo>();
            model.Videos = videos.Select(MapToChannelVideoModel).ToList();

            _logger.LogInformation("Successfully loaded {Count} recent videos for channel {ChannelName}",
                model.Videos.Count, channelInfo.Name);

            model.IsLoading = false;
            return model;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading channel videos for {ChannelId}", youTubeChannelId);

            model.ErrorMessage = "An unexpected error occurred while loading channel videos.";
            model.IsLoading = false;

            await _messageCenter.ShowErrorAsync("Failed to load channel videos. Please try again.");

            return model;
        }
    }

    /// <summary>
    /// Gets channel information only (no videos).
    /// </summary>
    public async Task<ChannelInfoModel?> GetChannelInfoAsync(string youTubeChannelId, string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(youTubeChannelId))
                return null;

            var channelInfo = await _repository.GetChannelInfoAsync(youTubeChannelId);
            return channelInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel info for {ChannelId}", youTubeChannelId);
            return null;
        }
    }

    /// <summary>
    /// Validates that a YouTube channel ID exists and is accessible.
    /// Uses database first, then YouTube API if needed.
    /// </summary>
    public async Task<bool> ValidateChannelExistsAsync(string youTubeChannelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(youTubeChannelId))
                return false;

            // First check if we have the channel in our database
            var channelInfo = await _repository.GetChannelInfoAsync(youTubeChannelId);
            if (channelInfo != null)
                return true;

            // If not in database, we could optionally check YouTube API
            // For now, we only validate channels that are being tracked
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating channel existence for {ChannelId}", youTubeChannelId);
            return false;
        }
    }

    /// <summary>
    /// Gets the user's tracking status for a channel.
    /// </summary>
    public async Task<bool> IsChannelTrackedByUserAsync(string userId, string youTubeChannelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(youTubeChannelId))
                return false;

            return await _repository.IsChannelTrackedByUserAsync(userId, youTubeChannelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking tracking status for user {UserId} and channel {ChannelId}",
                userId, youTubeChannelId);
            return false;
        }
    }

    /// <summary>
    /// Maps VideoInfo from shared YouTube service to ChannelVideoModel for display.
    /// </summary>
    private static ChannelVideoModel MapToChannelVideoModel(Features.Suggestions.Models.VideoInfo video)
    {
        return new ChannelVideoModel
        {
            YouTubeVideoId = video.YouTubeVideoId,
            Title = video.Title,
            Description = video.Description,
            ThumbnailUrl = video.ThumbnailUrl,
            Duration = video.Duration,
            ViewCount = video.ViewCount,
            LikeCount = video.LikeCount,
            CommentCount = video.CommentCount,
            PublishedAt = video.PublishedAt
        };
    }
}