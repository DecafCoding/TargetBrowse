using TargetBrowse.Features.Videos.Data;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.Videos.Utilities;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Videos.Services;

/// <summary>
/// Service implementation for video management business logic.
/// Handles video search, library management, and YouTube API integration.
/// </summary>
public class VideoService : IVideoService
{
    private readonly IVideoYouTubeService _youTubeService;
    private readonly IVideoRepository _videoRepository;
    private readonly ILogger<VideoService> _logger;

    public VideoService(
        IVideoYouTubeService youTubeService,
        IVideoRepository videoRepository,
        ILogger<VideoService> logger)
    {
        _youTubeService = youTubeService;
        _videoRepository = videoRepository;
        _logger = logger;
    }

    /// <summary>
    /// Searches for YouTube videos based on search criteria.
    /// </summary>
    public async Task<List<VideoDisplayModel>> SearchVideosAsync(string userId, VideoSearchModel searchModel)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchModel.SearchQuery))
            {
                return new List<VideoDisplayModel>();
            }

            // If it's a direct video URL, handle separately
            if (searchModel.IsDirectVideoUrl)
            {
                var videoId = searchModel.ExtractVideoId();
                if (!string.IsNullOrEmpty(videoId))
                {
                    var singleVideo = await GetVideoByIdAsync(userId, videoId);
                    return singleVideo != null ? new List<VideoDisplayModel> { singleVideo } : new List<VideoDisplayModel>();
                }
                return new List<VideoDisplayModel>();
            }

            // Search videos via YouTube API with advanced options
            string? channelId = null;
            if (searchModel.SearchInTrackedChannelsOnly)
            {
            // TODO: Get user's tracked channels and search within them
            // For now, search all channels
            }

            var searchResult = await _youTubeService.SearchVideosAsync(
            searchModel.SearchQuery, 
            searchModel.MaxResults, 
            channelId,
            searchModel.SortOrder,
            searchModel.DurationFilter,
            searchModel.DateFilter);

            if (!searchResult.IsSuccess || searchResult.Data == null)
            {
                _logger.LogWarning("YouTube video search failed: {Error}", searchResult.ErrorMessage);
                return new List<VideoDisplayModel>();
            }

            // Convert YouTube API results to display models
            var videos = new List<VideoDisplayModel>();
            foreach (var youTubeVideo in searchResult.Data)
            {
                var video = MapYouTubeVideoToDisplayModel(youTubeVideo);
                
                // Check if video is already in user's library
                video.IsInLibrary = await _videoRepository.IsVideoInLibraryAsync(userId, video.YouTubeVideoId);
                if (video.IsInLibrary)
                {
                    var libraryVideo = await _videoRepository.GetVideoByYouTubeIdAsync(userId, video.YouTubeVideoId);
                    if (libraryVideo != null)
                    {
                        video.AddedToLibrary = libraryVideo.AddedToLibrary;
                    }
                }

                videos.Add(video);
            }

            _logger.LogInformation("Video search for '{Query}' with advanced filters returned {Count} results", 
                searchModel.SearchQuery, videos.Count);

            return videos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching videos for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets detailed information about a specific video by YouTube video ID.
    /// </summary>
    public async Task<VideoDisplayModel?> GetVideoByIdAsync(string userId, string youTubeVideoId)
    {
        try
        {
            // First check if video is in user's library
            var libraryVideo = await _videoRepository.GetVideoByYouTubeIdAsync(userId, youTubeVideoId);
            if (libraryVideo != null)
            {
                return libraryVideo;
            }

            // Get video details from YouTube API
            var apiResult = await _youTubeService.GetVideoByIdAsync(youTubeVideoId);
            if (!apiResult.IsSuccess || apiResult.Data == null)
            {
                _logger.LogWarning("Failed to get video details for {VideoId}: {Error}", 
                    youTubeVideoId, apiResult.ErrorMessage);
                return null;
            }

            var video = MapYouTubeVideoToDisplayModel(apiResult.Data);
            video.IsInLibrary = false;

            return video;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video {VideoId} for user {UserId}", youTubeVideoId, userId);
            throw;
        }
    }

    /// <summary>
    /// Adds a video to the user's library.
    /// </summary>
    public async Task<bool> AddVideoToLibraryAsync(string userId, AddVideoModel addVideoModel)
    {
        try
        {
            // Validate the video URL and extract video ID
            addVideoModel.ValidateAndExtractVideoId();
            if (!addVideoModel.IsValidUrl || string.IsNullOrEmpty(addVideoModel.VideoId))
            {
                _logger.LogWarning("Invalid video URL provided: {Url}", addVideoModel.VideoUrl);
                return false;
            }

            // Check if video is already in library
            var existingVideo = await _videoRepository.GetVideoByYouTubeIdAsync(userId, addVideoModel.VideoId);
            if (existingVideo != null)
            {
                _logger.LogInformation("Video {VideoId} already exists in library for user {UserId}", 
                    addVideoModel.VideoId, userId);
                return false;
            }

            // Get video details from YouTube API
            var apiResult = await _youTubeService.GetVideoByIdAsync(addVideoModel.VideoId);
            if (!apiResult.IsSuccess || apiResult.Data == null)
            {
                _logger.LogWarning("Failed to get video details for {VideoId}: {Error}", 
                    addVideoModel.VideoId, apiResult.ErrorMessage);
                return false;
            }

            // Convert to display model and add to library
            var video = MapYouTubeVideoToDisplayModel(apiResult.Data);
            video.IsInLibrary = true;
            video.AddedToLibrary = DateTime.UtcNow;

            var success = await _videoRepository.AddVideoAsync(userId, video);
            if (success)
            {
                _logger.LogInformation("Added video {VideoId} to library for user {UserId}", 
                    addVideoModel.VideoId, userId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding video to library for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Removes a video from the user's library.
    /// </summary>
    public async Task<bool> RemoveVideoFromLibraryAsync(string userId, Guid videoId)
    {
        try
        {
            var success = await _videoRepository.RemoveVideoAsync(userId, videoId);
            if (success)
            {
                _logger.LogInformation("Removed video {VideoId} from library for user {UserId}", 
                    videoId, userId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing video {VideoId} from library for user {UserId}", 
                videoId, userId);
            return false;
        }
    }

    /// <summary>
    /// Gets all videos in the user's library.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetUserLibraryAsync(string userId)
    {
        try
        {
            return await _videoRepository.GetUserVideosAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user library for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Searches videos within the user's library.
    /// </summary>
    public async Task<List<VideoDisplayModel>> SearchLibraryAsync(string userId, string searchQuery)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return await GetUserLibraryAsync(userId);
            }

            return await _videoRepository.SearchUserVideosAsync(userId, searchQuery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching library for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets library statistics for the user.
    /// </summary>
    public async Task<VideoLibraryStats> GetLibraryStatsAsync(string userId)
    {
        try
        {
            var videos = await _videoRepository.GetUserVideosAsync(userId);
            var now = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            var stats = new VideoLibraryStats
            {
                TotalVideos = videos.Count,
                VideosAddedThisWeek = videos.Count(v => v.AddedToLibrary >= weekAgo),
                VideosAddedThisMonth = videos.Count(v => v.AddedToLibrary >= monthAgo),
                VideosByChannel = videos.GroupBy(v => v.ChannelTitle)
                                      .ToDictionary(g => g.Key, g => g.Count()),
                LastAddedDate = videos.Max(v => v.AddedToLibrary)
            };

            if (stats.VideosByChannel.Any())
            {
                stats.MostActiveChannel = stats.VideosByChannel
                    .OrderByDescending(kvp => kvp.Value)
                    .First().Key;
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting library stats for user {UserId}", userId);
            return new VideoLibraryStats();
        }
    }

    /// <summary>
    /// Checks if a video is already in the user's library.
    /// </summary>
    public async Task<bool> IsVideoInLibraryAsync(string userId, string youTubeVideoId)
    {
        try
        {
            return await _videoRepository.IsVideoInLibraryAsync(userId, youTubeVideoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if video {VideoId} is in library for user {UserId}", 
                youTubeVideoId, userId);
            return false;
        }
    }

    /// <summary>
    /// Validates a video URL and extracts video information.
    /// </summary>
    public async Task<VideoDisplayModel?> ValidateVideoUrlAsync(string videoUrl)
    {
        try
        {
            var videoId = YouTubeVideoParser.ExtractVideoId(videoUrl);
            if (string.IsNullOrEmpty(videoId))
            {
                return null;
            }

            var apiResult = await _youTubeService.GetVideoByIdAsync(videoId);
            if (!apiResult.IsSuccess || apiResult.Data == null)
            {
                return null;
            }

            return MapYouTubeVideoToDisplayModel(apiResult.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating video URL: {Url}", videoUrl);
            return null;
        }
    }

    /// <summary>
    /// Gets videos from the user's tracked channels that aren't in their library.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetSuggestedVideosFromChannelsAsync(string userId, int maxResults = 20)
    {
        try
        {
            // TODO: Implement when channel tracking is integrated
            // This would get the user's tracked channels and find recent videos from those channels
            // that aren't already in the user's library
            await Task.CompletedTask;
            return new List<VideoDisplayModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggested videos for user {UserId}", userId);
            return new List<VideoDisplayModel>();
        }
    }

    /// <summary>
    /// Maps a YouTube API video response to a video display model.
    /// </summary>
    private static VideoDisplayModel MapYouTubeVideoToDisplayModel(YouTubeVideoResponse youTubeVideo)
    {
        return new VideoDisplayModel
        {
            Id = Guid.NewGuid(), // This will be set properly when saving to database
            YouTubeVideoId = youTubeVideo.VideoId,
            Title = youTubeVideo.Title,
            Description = youTubeVideo.Description,
            ThumbnailUrl = youTubeVideo.ThumbnailUrl,
            Duration = youTubeVideo.Duration,
            ViewCount = youTubeVideo.ViewCount,
            PublishedAt = youTubeVideo.PublishedAt,
            ChannelId = youTubeVideo.ChannelId,
            ChannelTitle = youTubeVideo.ChannelTitle,
            IsInLibrary = false // Will be set by calling methods
        };
    }
}