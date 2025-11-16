using TargetBrowse.Features.Channels.Data;
using TargetBrowse.Features.ChannelVideos.Data;
using TargetBrowse.Features.ChannelVideos.Models;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.ChannelVideos.Services;

/// <summary>
/// Service implementation for channel videos functionality.
/// Now uses shared YouTube service instead of Suggestions-specific service.
/// </summary>
public class ChannelVideosService : IChannelVideosService
{
    private readonly IChannelVideosRepository _repository;
    private readonly ISharedYouTubeService _youTubeService;
    private readonly IMessageCenterService _messageCenter;
    private readonly IVideoDataService _videoDataService;
    private readonly IChannelRepository _channelRepository;
    private readonly ILogger<ChannelVideosService> _logger;

    public ChannelVideosService(
        IChannelVideosRepository repository,
        ISharedYouTubeService youTubeService,
        IMessageCenterService messageCenter,
        IChannelRepository channelRepository,
        IVideoDataService videoDataService,
        ILogger<ChannelVideosService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _youTubeService = youTubeService ?? throw new ArgumentNullException(nameof(youTubeService));
        _messageCenter = messageCenter ?? throw new ArgumentNullException(nameof(messageCenter));
        _channelRepository = channelRepository ?? throw new ArgumentNullException(nameof(channelRepository));
        _videoDataService = videoDataService ?? throw new ArgumentNullException(nameof(videoDataService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // File: Features/ChannelVideos/Services/ChannelVideosService.cs
    // Replace the entire GetChannelVideosAsync method with this implementation

    /// <summary>
    /// Gets recent videos from a channel along with channel information.
    /// Now checks database first and only calls YouTube API based on rating and LastCheckDate.
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

            // Get the count of videos we have in the database for this channel
            model.Channel.DatabaseVideoCount = await _repository.GetChannelVideosCountInDatabaseAsync(youTubeChannelId);

            // Get user tracking and rating status
            if (!string.IsNullOrWhiteSpace(userId))
            {
                model.IsTrackedByUser = await _repository.IsChannelTrackedByUserAsync(userId, youTubeChannelId);
                model.UserRating = await _repository.GetUserChannelRatingAsync(userId, youTubeChannelId);
            }

            // Check if we should update from YouTube API based on rating and LastCheckDate
            bool shouldUpdateFromAPI = ShouldUpdateFromAPI(model.UserRating, model.Channel.LastCheckDate);

            if (shouldUpdateFromAPI)
            {
                _logger.LogInformation("Updating channel {ChannelId} from YouTube API (rating: {Rating}, last check: {LastCheck})",
                    youTubeChannelId, model.UserRating?.ToString() ?? "none", model.Channel.LastCheckDate?.ToString() ?? "never");

                // Get recent videos from YouTube API
                var videosResult = await _youTubeService.GetChannelVideosFromAPI(youTubeChannelId);

                if (!videosResult.IsSuccess)
                {
                    _logger.LogWarning("Failed to get videos for channel {ChannelId}: {Error}",
                        youTubeChannelId, videosResult.ErrorMessage);

                    if (videosResult.IsQuotaExceeded)
                    {
                        // Fall back to database videos if API quota exceeded
                        _logger.LogInformation("API quota exceeded, falling back to database videos for channel {ChannelId}", youTubeChannelId);
                        model.Videos = await _repository.GetChannelVideosFromDatabaseAsync(youTubeChannelId);
                    }
                    else
                    {
                        model.ErrorMessage = "Unable to load recent videos. Please try again later.";
                        model.IsLoading = false;
                        return model;
                    }
                }
                else
                {
                    // Update LastCheckDate since we successfully checked the channel
                    await _channelRepository.UpdateLastCheckDateAsync(youTubeChannelId, DateTime.UtcNow);

                    // Store videos in database for future use
                    var videos = videosResult.Data ?? new List<VideoInfo>();
                    await StoreVideosInDatabase(videos);

                    // Get videos from database to ensure consistency
                    model.Videos = await _repository.GetChannelVideosFromDatabaseAsync(youTubeChannelId);
                }
            }
            else
            {
                _logger.LogInformation("Using cached videos for channel {ChannelId} (rating: {Rating}, last check: {LastCheck})",
                    youTubeChannelId, model.UserRating?.ToString() ?? "none", model.Channel.LastCheckDate?.ToString() ?? "never");

                // Get videos from database only
                model.Videos = await _repository.GetChannelVideosFromDatabaseAsync(youTubeChannelId);
            }

            _logger.LogInformation("Successfully loaded {Count} videos for channel {ChannelName} (from {Source})",
                model.Videos.Count, channelInfo.Name, shouldUpdateFromAPI ? "API" : "database");

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
    /// Determines if we should update from YouTube API based on channel rating and last check date.
    /// Rating-based refresh intervals:
    /// - 5 stars: 5 days
    /// - 4 stars: 7 days  
    /// - 3 stars: 10 days
    /// - 2 stars: 14 days
    /// - 1 star or no rating: Never update
    /// </summary>
    private bool ShouldUpdateFromAPI(int? userRating, DateTime? lastCheckDate)
    {
        // If never checked before, always update
        if (!lastCheckDate.HasValue)
            return true;

        var daysSinceCheck = (DateTime.UtcNow - lastCheckDate.Value).TotalDays;

        return userRating switch
        {
            5 => daysSinceCheck > 5,
            4 => daysSinceCheck > 7,
            3 => daysSinceCheck > 10,
            2 => daysSinceCheck > 14,
            1 => false, // Never update for 1-star channels
            null => false, // Never update for unrated channels
            _ => daysSinceCheck > 14 // Default for unexpected ratings
        };
    }

    /// <summary>
    /// Stores YouTube API video results in the database for future use.
    /// Uses the video repository to ensure videos exist in the database.
    /// </summary>
    private async Task StoreVideosInDatabase(List<VideoInfo> videos)
    {
        try
        {
            if (!videos.Any()) return;

            _logger.LogDebug("Storing {VideoCount} videos in database", videos.Count);

            // Store each video in the database using the video data service
            foreach (var video in videos)
            {
                try
                {
                    await _videoDataService.EnsureVideoExistsAsync(video);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to store video {VideoId} in database", video.YouTubeVideoId);
                    // Continue with other videos - don't fail the entire operation
                }
            }

            _logger.LogInformation("Successfully stored {VideoCount} videos in database", videos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing videos in database");
            // Don't fail the entire operation if storage fails - videos will still be returned from API
        }
    }

    /// <summary>
    /// Gets channel information only (no videos) including database video count.
    /// </summary>
    public async Task<ChannelInfoModel?> GetChannelInfoAsync(string youTubeChannelId, string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(youTubeChannelId))
                return null;

            var channelInfo = await _repository.GetChannelInfoAsync(youTubeChannelId);
            if (channelInfo != null)
            {
                // Get the count of videos we have in the database for this channel
                channelInfo.DatabaseVideoCount = await _repository.GetChannelVideosCountInDatabaseAsync(youTubeChannelId);
            }

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
    private static ChannelVideoModel MapToChannelVideoModel(VideoInfo video)
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