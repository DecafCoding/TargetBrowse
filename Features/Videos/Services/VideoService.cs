using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Videos.Services;

/// <summary>
/// Service implementation for video management business logic.
/// Handles video search, YouTube API integration, and coordinates with VideoDataService for data operations.
/// </summary>
public class VideoService : IVideoService
{
    private readonly IVideoYouTubeService _youTubeService;
    private readonly IVideoDataService _videoDataService;
    private readonly ILibraryDataService _libraryDataService;
    private readonly ILogger<VideoService> _logger;

    public VideoService(
        IVideoYouTubeService youTubeService,
        IVideoDataService videoDataService,
        ILibraryDataService libraryDataService,
        ILogger<VideoService> logger)
    {
        _youTubeService = youTubeService;
        _videoDataService = videoDataService;
        _libraryDataService = libraryDataService;
        _logger = logger;
    }

    /// <summary>
    /// Searches for YouTube videos based on search criteria.
    /// Combines YouTube API search with user's library status.
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

                // Check if video is already in user's library using VideoDataService
                video.IsInLibrary = await _libraryDataService.IsVideoInLibraryAsync(userId, video.YouTubeVideoId);
                if (video.IsInLibrary)
                {
                    var libraryVideo = await _libraryDataService.GetVideoByYouTubeIdAsync(userId, video.YouTubeVideoId);
                    if (libraryVideo != null)
                    {
                        video.AddedToLibrary = libraryVideo.AddedToLibraryAt;
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
    /// Includes whether the video is already in the user's library.
    /// </summary>
    public async Task<VideoDisplayModel?> GetVideoByIdAsync(string userId, string youTubeVideoId)
    {
        try
        {
            // First check if video is in user's library using LibraryDataService
            var libraryVideo = await _libraryDataService.GetVideoByYouTubeIdAsync(userId, youTubeVideoId);
            if (libraryVideo != null)
            {
                return libraryVideo.ToVideoDisplayModel();
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
    /// Gets all videos in the user's library.
    /// Delegates to LibraryDataService and maps to VideoDisplayModel.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetUserLibraryAsync(string userId)
    {
        try
        {
            var libraryVideos = await _libraryDataService.GetUserVideosAsync(userId);
            return libraryVideos.ToVideoDisplayModels();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user library for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a video is already in the user's library.
    /// Delegates to VideoDataService.
    /// </summary>
    public async Task<bool> IsVideoInLibraryAsync(string userId, string youTubeVideoId)
    {
        try
        {
            return await _libraryDataService.IsVideoInLibraryAsync(userId, youTubeVideoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if video {VideoId} is in library for user {UserId}",
                youTubeVideoId, userId);
            return false;
        }
    }

    /// <summary>
    /// Adds an existing video entity to the user's library.
    /// Delegates to VideoDataService.
    /// </summary>
    public async Task<bool> AddExistingVideoToLibraryAsync(string userId, VideoEntity videoEntity)
    {
        try
        {
            var success = await _libraryDataService.AddExistingVideoToLibraryAsync(userId, videoEntity);

            if (success)
            {
                _logger.LogInformation("Added existing video {VideoId} '{Title}' to library for user {UserId}",
                    videoEntity.YouTubeVideoId, videoEntity.Title, userId);
            }
            else
            {
                _logger.LogInformation("Video {VideoId} already exists in library for user {UserId}",
                    videoEntity.YouTubeVideoId, userId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding existing video {VideoId} to library for user {UserId}",
                videoEntity.YouTubeVideoId, userId);
            return false;
        }
    }

    /// <summary>
    /// Maps a YouTube API video response to a video display model.
    /// Handles both search results (limited data) and detailed video responses (full data).
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
            Duration = youTubeVideo.Duration, // Will be null for search results, populated for detailed calls
            ViewCount = youTubeVideo.ViewCount, // Will be null for search results, populated for detailed calls
            LikeCount = youTubeVideo.LikeCount, // Will be null for search results, populated for detailed calls
            CommentCount = youTubeVideo.CommentCount, // Will be null for search results, populated for detailed calls
            PublishedAt = youTubeVideo.PublishedAt,
            ChannelId = youTubeVideo.ChannelId,
            ChannelTitle = youTubeVideo.ChannelTitle,
            CategoryId = youTubeVideo.CategoryId,
            Tags = youTubeVideo.Tags ?? new List<string>(),
            DefaultLanguage = youTubeVideo.DefaultLanguage,
            IsInLibrary = false // Will be set by calling methods
        };
    }
}