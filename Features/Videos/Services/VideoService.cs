using TargetBrowse.Features.Videos.Data;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.Videos.Utilities;
using TargetBrowse.Services.YouTube.Models;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Google.Apis.YouTube.v3.Data;

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
    /// Ensures we get full video details including duration, views, and likes.
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

            // Get FULL video details from YouTube API (not just search results)
            // This ensures we have duration, views, likes, and all metadata
            var apiResult = await _youTubeService.GetVideoByIdAsync(addVideoModel.VideoId);
            if (!apiResult.IsSuccess || apiResult.Data == null)
            {
                _logger.LogWarning("Failed to get video details for {VideoId}: {Error}",
                    addVideoModel.VideoId, apiResult.ErrorMessage);
                return false;
            }

            // Convert to display model with full metadata
            var video = MapYouTubeVideoToDisplayModel(apiResult.Data);

            // Ensure we have the enhanced metadata fields
            if (!video.HasDetailedInfo)
            {
                _logger.LogWarning("Video {VideoId} does not have detailed metadata (duration, views, likes)", addVideoModel.VideoId);
                // Still allow adding, but log the warning
            }

            video.IsInLibrary = true;
            video.AddedToLibrary = DateTime.UtcNow;

            var success = await _videoRepository.AddVideoAsync(userId, video);
            if (success)
            {
                _logger.LogInformation("Added video {VideoId} '{Title}' to library for user {UserId} with metadata: Duration={Duration}, Views={Views}, Likes={Likes}",
                    addVideoModel.VideoId, video.Title, userId, video.DurationDisplay, video.ViewCountDisplay, video.LikeCountDisplay);
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
    /// Updates the watch status for a video in the user's library.
    /// </summary>
    public async Task<bool> UpdateVideoWatchStatusAsync(string userId, Guid videoId, WatchStatus watchStatus)
    {
        try
        {
            var success = await _videoRepository.UpdateVideoWatchStatusAsync(userId, videoId, watchStatus);
            if (success)
            {
                _logger.LogInformation("Updated watch status for video {VideoId} to {Status} for user {UserId}",
                    videoId, watchStatus, userId);
            }
            else
            {
                _logger.LogWarning("Failed to update watch status for video {VideoId} for user {UserId} - video not found in library",
                    videoId, userId);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating watch status for video {VideoId} for user {UserId}",
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
                LastAddedDate = videos.Any() ? videos.Max(v => v.AddedToLibrary) : null
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
    /// Gets full video details if valid.
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

            // Get full video details for validation
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
    /// Saves all discovered videos to the database for historical browsing.
    /// Handles duplicate prevention and ensures video metadata is stored.
    /// Used by suggestion generation to persist all found videos regardless of approval status.
    /// </summary>
    public async Task<List<VideoEntity>> SaveDiscoveredVideosAsync(List<VideoInfo> videos, string userId)
    {
        try
        {
            if (!videos.Any())
            {
                _logger.LogInformation("No videos to save for user {UserId}", userId);
                return new List<VideoEntity>();
            }

            _logger.LogInformation("Saving {Count} discovered videos for user {UserId}", videos.Count, userId);

            var savedEntities = new List<VideoEntity>();

            // Process videos in batches to avoid overwhelming the database
            const int batchSize = 20;
            for (int i = 0; i < videos.Count; i += batchSize)
            {
                var batch = videos.Skip(i).Take(batchSize).ToList();
                var batchEntities = new List<VideoEntity>();

                foreach (var video in batch)
                {
                    try
                    {
                        var entity = await EnsureVideoExistsAsync(video);
                        batchEntities.Add(entity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to save video {VideoId} in batch for user {UserId}",
                            video.YouTubeVideoId, userId);
                        // Continue processing other videos
                    }
                }

                savedEntities.AddRange(batchEntities);

                // Log progress for large batches
                if (videos.Count > 50)
                {
                    _logger.LogDebug("Processed batch {BatchNumber} of {TotalBatches} for user {UserId}",
                        (i / batchSize) + 1, (videos.Count + batchSize - 1) / batchSize, userId);
                }
            }

            _logger.LogInformation("Successfully saved {SavedCount} out of {TotalCount} videos for user {UserId}",
                savedEntities.Count, videos.Count, userId);

            return savedEntities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving discovered videos for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Ensures a video exists in the database with complete metadata.
    /// Creates the video entity if it doesn't exist, updates if it does.
    /// </summary>
    public async Task<VideoEntity> EnsureVideoExistsAsync(VideoInfo video)
    {
        try
        {
            // Use repository to ensure video exists with proper channel handling
            return await _videoRepository.EnsureVideoExistsAsync(video);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring video {VideoId} exists", video.YouTubeVideoId);
            throw;
        }
    }

    /// <summary>
    /// Gets videos from a specific channel published since a given date.
    /// Delegates to YouTube service for consistency.
    /// </summary>
    //public async Task<List<VideoInfo>> GetChannelVideosAsync(string channelId, DateTime since)
    //{
    //    try
    //    {
    //        _logger.LogDebug("Getting videos from channel {ChannelId} since {Since}", channelId, since);

    //        // Delegate to the YouTube service which already handles this functionality
    //        var result = await _youTubeService.GetChannelVideosAsync(channelId, since);

    //        if (!result.IsSuccess || result.Data == null)
    //        {
    //            _logger.LogWarning("Failed to get channel videos for {ChannelId}: {Error}",
    //                channelId, result.ErrorMessage);
    //            return new List<VideoInfo>();
    //        }

    //        // Convert YouTube API response to VideoInfo objects
    //        var videos = result.Data.Select(ConvertYouTubeResponseToVideoInfo).ToList();

    //        _logger.LogInformation($"Retrieved {videos.Count()} videos from channel {channelId} since {since}");

    //        return videos;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error getting channel videos for {ChannelId}", channelId);
    //        return new List<VideoInfo>();
    //    }
    //}

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

    // Add this implementation to VideoService:
    /// <summary>
    /// Adds an existing video entity to the user's library.
    /// Converts the entity to a display model and uses the existing repository method.
    /// </summary>
    public async Task<bool> AddExistingVideoToLibraryAsync(string userId, VideoEntity videoEntity)
    {
        try
        {
            // Convert VideoEntity to VideoDisplayModel
            var videoDisplayModel = new VideoDisplayModel
            {
                Id = videoEntity.Id,
                YouTubeVideoId = videoEntity.YouTubeVideoId,
                Title = videoEntity.Title,
                Description = videoEntity.Description ?? string.Empty,
                ThumbnailUrl = videoEntity.ThumbnailUrl ?? string.Empty,
                Duration = videoEntity.Duration > 0 ? videoEntity.Duration.ToString() : null,
                ViewCount = videoEntity.ViewCount > 0 ? (ulong)videoEntity.ViewCount : null,
                LikeCount = videoEntity.LikeCount > 0 ? (ulong)videoEntity.LikeCount : null,
                CommentCount = videoEntity.CommentCount > 0 ? (ulong)videoEntity.CommentCount : null,
                PublishedAt = videoEntity.PublishedAt,
                ChannelId = videoEntity.Channel.YouTubeChannelId,
                ChannelTitle = videoEntity.Channel.Name,
                CategoryId = null,
                Tags = new List<string>(),
                DefaultLanguage = null,
                IsInLibrary = true,
                AddedToLibrary = DateTime.UtcNow
            };

            // Use existing repository method
            var success = await _videoRepository.AddVideoAsync(userId, videoDisplayModel);

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
    /// Converts a YouTube API response to a VideoInfo object for suggestion processing.
    /// </summary>
    private static VideoInfo ConvertYouTubeResponseToVideoInfo(YouTubeVideoResponse youTubeVideo)
    {
        return new VideoInfo
        {
            YouTubeVideoId = youTubeVideo.VideoId,
            Title = youTubeVideo.Title,
            ChannelId = youTubeVideo.ChannelId,
            ChannelName = youTubeVideo.ChannelTitle,
            PublishedAt = youTubeVideo.PublishedAt,
            Duration = !string.IsNullOrEmpty(youTubeVideo.Duration) ? int.Parse(youTubeVideo.Duration) : 0,
            ViewCount = (int)(youTubeVideo.ViewCount ?? 0),
            LikeCount = (int)(youTubeVideo.LikeCount ?? 0),
            CommentCount = (int)(youTubeVideo.CommentCount ?? 0),
            Description = youTubeVideo.Description ?? string.Empty,
            ThumbnailUrl = youTubeVideo.ThumbnailUrl ?? string.Empty
        };
    }
}