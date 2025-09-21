using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.ChannelVideos.Models;
using TargetBrowse.Features.TopicVideos.Models;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.YouTube;

namespace TargetBrowse.Services.DataServices;

/// <summary>
/// Data access service implementation for all video-related operations.
/// Handles video entity management, user library operations, and cross-feature video data access.
/// Replaces VideoRepository and provides centralized video data operations.
/// </summary>
public class VideoDataService : IVideoDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VideoDataService> _logger;

    public VideoDataService(ApplicationDbContext context, ILogger<VideoDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Library Management Operations

    /// <summary>
    /// Gets all videos in the user's library with rating information.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetUserVideosAsync(string userId)
    {
        try
        {
            var userVideos = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId))
                .Where(uv => uv.UserId == userId)
                .OrderByDescending(uv => uv.AddedToLibraryAt)
                .ToListAsync();

            return userVideos.Select(uv => MapToDisplayModel(uv, userId)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user videos for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific video by its YouTube video ID for a user.
    /// Returns null if the video is not in the user's library.
    /// </summary>
    public async Task<VideoDisplayModel?> GetVideoByYouTubeIdAsync(string userId, string youTubeVideoId)
    {
        try
        {
            var userVideo = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId))
                .FirstOrDefaultAsync(uv => uv.UserId == userId &&
                                          uv.Video.YouTubeVideoId == youTubeVideoId);

            return userVideo != null ? MapToDisplayModel(userVideo, userId) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video by YouTube ID {VideoId} for user {UserId}",
                youTubeVideoId, userId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a video is already in the user's library.
    /// </summary>
    public async Task<bool> IsVideoInLibraryAsync(string userId, string youTubeVideoId)
    {
        try
        {
            return await _context.UserVideos
                .AnyAsync(uv => uv.UserId == userId &&
                               uv.Video.YouTubeVideoId == youTubeVideoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if video {VideoId} is in library for user {UserId}",
                youTubeVideoId, userId);
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
            var userVideo = await _context.UserVideos
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.Id == videoId);

            if (userVideo == null)
            {
                return false;
            }

            _context.UserVideos.Remove(userVideo);
            await _context.SaveChangesAsync();

            return true;
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
            var userVideo = await _context.UserVideos
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.Id == videoId);

            if (userVideo == null)
            {
                return false;
            }

            userVideo.Status = watchStatus;
            userVideo.StatusChangedAt = DateTime.UtcNow;
            userVideo.LastModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating watch status for video {VideoId} for user {UserId}",
                videoId, userId);
            return false;
        }
    }

    /// <summary>
    /// Searches videos in the user's library by title or description.
    /// </summary>
    public async Task<List<VideoDisplayModel>> SearchUserVideosAsync(string userId, string searchQuery)
    {
        try
        {
            var userVideos = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId))
                .Where(uv => uv.UserId == userId &&
                            (uv.Video.Title.Contains(searchQuery) ||
                             uv.Video.Channel.Name.Contains(searchQuery)))
                .OrderByDescending(uv => uv.AddedToLibraryAt)
                .ToListAsync();

            return userVideos.Select(uv => MapToDisplayModel(uv, userId)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching user videos for user {UserId} with query {Query}",
                userId, searchQuery);
            throw;
        }
    }

    #endregion

    #region Feature-Specific Library Operations

    /// <summary>
    /// Adds a video from channel browsing to the user's library.
    /// Handles conversion from ChannelVideoModel and includes channel context.
    /// </summary>
    public async Task<bool> AddChannelVideoToLibraryAsync(string userId, ChannelVideoModel video, string notes = "")
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Check if already exists
            var existingUserVideo = await _context.UserVideos
                .FirstOrDefaultAsync(uv => uv.UserId == userId &&
                                          uv.Video.YouTubeVideoId == video.YouTubeVideoId);

            if (existingUserVideo != null)
            {
                _logger.LogInformation("Video {VideoId} already exists in library for user {UserId}",
                    video.YouTubeVideoId, userId);
                return false; // Already exists
            }

            // Convert ChannelVideoModel to VideoInfo for entity creation
            var videoInfo = new VideoInfo
            {
                YouTubeVideoId = video.YouTubeVideoId,
                Title = video.Title,
                ChannelId = ExtractChannelIdFromYouTubeUrl(video.YouTubeUrl), // Need to extract from URL if not available
                ChannelName = "Unknown Channel", // ChannelVideoModel doesn't include channel name
                PublishedAt = video.PublishedAt,
                Duration = video.Duration,
                ViewCount = video.ViewCount,
                LikeCount = video.LikeCount,
                CommentCount = video.CommentCount,
                Description = video.Description,
                ThumbnailUrl = video.ThumbnailUrl ?? string.Empty
            };

            // Ensure video entity exists
            var videoEntity = await EnsureVideoExistsAsync(videoInfo);

            // Create user-video relationship
            var userVideoEntity = new UserVideoEntity
            {
                UserId = userId,
                VideoId = videoEntity.Id,
                AddedToLibraryAt = DateTime.UtcNow,
                Status = WatchStatus.NotWatched,
                Notes = string.IsNullOrEmpty(notes) ? $"Added from channel videos on {DateTime.Now:yyyy-MM-dd}" : notes
            };

            _context.UserVideos.Add(userVideoEntity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Added channel video {VideoId} '{Title}' to library for user {UserId}",
                video.YouTubeVideoId, video.Title, userId);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error adding channel video {VideoId} to library for user {UserId}",
                video.YouTubeVideoId, userId);
            return false;
        }
    }

    /// <summary>
    /// Adds a video from topic search to the user's library.
    /// Handles conversion from TopicVideoDisplayModel and includes topic context.
    /// </summary>
    public async Task<bool> AddTopicVideoToLibraryAsync(string userId, TopicVideoDisplayModel video, string notes = "")
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Check if already exists
            var existingUserVideo = await _context.UserVideos
                .FirstOrDefaultAsync(uv => uv.UserId == userId &&
                                          uv.Video.YouTubeVideoId == video.YouTubeVideoId);

            if (existingUserVideo != null)
            {
                _logger.LogInformation("Video {VideoId} already exists in library for user {UserId}",
                    video.YouTubeVideoId, userId);
                return false; // Already exists
            }

            // Convert TopicVideoDisplayModel to VideoInfo for entity creation
            var videoInfo = new VideoInfo
            {
                YouTubeVideoId = video.YouTubeVideoId,
                Title = video.Title,
                ChannelId = video.ChannelId,
                ChannelName = video.ChannelTitle,
                PublishedAt = video.PublishedAt,
                Duration = DurationParser.ParseToSeconds(video.Duration),
                ViewCount = (int)(video.ViewCount ?? 0),
                LikeCount = (int)(video.LikeCount ?? 0),
                CommentCount = (int)(video.CommentCount ?? 0),
                Description = video.Description,
                ThumbnailUrl = video.ThumbnailUrl ?? string.Empty
            };

            // Ensure video entity exists
            var videoEntity = await EnsureVideoExistsAsync(videoInfo);

            // Create contextual notes with topic information
            var contextualNotes = string.IsNullOrEmpty(notes)
                ? $"Added from topic search '{video.TopicName}' on {DateTime.Now:yyyy-MM-dd}. Relevance: {video.RelevanceScore:F1}, Match: {video.MatchReason}"
                : $"{notes}. Topic: {video.TopicName}, Relevance: {video.RelevanceScore:F1}";

            // Create user-video relationship
            var userVideoEntity = new UserVideoEntity
            {
                UserId = userId,
                VideoId = videoEntity.Id,
                AddedToLibraryAt = DateTime.UtcNow,
                Status = WatchStatus.NotWatched,
                Notes = contextualNotes
            };

            _context.UserVideos.Add(userVideoEntity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Added topic video {VideoId} '{Title}' to library for user {UserId} from topic '{TopicName}' with relevance {Relevance}",
                video.YouTubeVideoId, video.Title, userId, video.TopicName, video.RelevanceScore);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error adding topic video {VideoId} to library for user {UserId} from topic {TopicName}",
                video.YouTubeVideoId, userId, video.TopicName);
            return false;
        }
    }

    /// <summary>
    /// Adds a video from general video display to the user's library.
    /// Handles VideoDisplayModel directly - used by video search results.
    /// </summary>
    public async Task<bool> AddVideoDisplayToLibraryAsync(string userId, VideoDisplayModel video, string notes = "")
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Check if already exists
            var existingUserVideo = await _context.UserVideos
                .FirstOrDefaultAsync(uv => uv.UserId == userId &&
                                          uv.Video.YouTubeVideoId == video.YouTubeVideoId);

            if (existingUserVideo != null)
            {
                _logger.LogInformation("Video {VideoId} already exists in library for user {UserId}",
                    video.YouTubeVideoId, userId);
                return false; // Already exists
            }

            // Convert VideoDisplayModel to VideoInfo for entity creation
            var videoInfo = new VideoInfo
            {
                YouTubeVideoId = video.YouTubeVideoId,
                Title = video.Title,
                ChannelId = video.ChannelId,
                ChannelName = video.ChannelTitle,
                PublishedAt = video.PublishedAt,
                Duration = DurationParser.ParseToSeconds(video.Duration),
                ViewCount = (int)(video.ViewCount ?? 0),
                LikeCount = (int)(video.LikeCount ?? 0),
                CommentCount = (int)(video.CommentCount ?? 0),
                Description = video.Description,
                ThumbnailUrl = video.ThumbnailUrl
            };

            // Ensure video entity exists
            var videoEntity = await EnsureVideoExistsAsync(videoInfo);

            // Create user-video relationship
            var userVideoEntity = new UserVideoEntity
            {
                UserId = userId,
                VideoId = videoEntity.Id,
                AddedToLibraryAt = video.AddedToLibrary ?? DateTime.UtcNow,
                Status = WatchStatus.NotWatched,
                Notes = string.IsNullOrEmpty(notes) ? $"Added from video search on {DateTime.Now:yyyy-MM-dd}" : notes
            };

            _context.UserVideos.Add(userVideoEntity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Added video {VideoId} '{Title}' to library for user {UserId}",
                video.YouTubeVideoId, video.Title, userId);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error adding video {VideoId} to library for user {UserId}",
                video.YouTubeVideoId, userId);
            return false;
        }
    }

    #endregion

    #region Video Entity Management

    /// <summary>
    /// Ensures a video entity exists in the database with complete metadata.
    /// Creates new video and channel entities if they don't exist, updates metadata if they do.
    /// </summary>
    public async Task<VideoEntity> EnsureVideoExistsAsync(VideoInfo video)
    {
        try
        {
            // First try to find existing video
            var existingVideo = await _context.Videos
                .Include(v => v.Channel)
                .FirstOrDefaultAsync(v => v.YouTubeVideoId == video.YouTubeVideoId);

            if (existingVideo != null)
            {
                // Return existing video as is for now
                // Could add metadata update logic here if needed
                return existingVideo;
            }

            // Ensure channel exists first
            var channel = await EnsureChannelExistsAsync(video.ChannelId, video.ChannelName);

            // Create new video entity
            var videoEntity = new VideoEntity
            {
                YouTubeVideoId = video.YouTubeVideoId,
                Title = video.Title,
                ChannelId = channel.Id,
                PublishedAt = video.PublishedAt,
                ViewCount = video.ViewCount,
                LikeCount = video.LikeCount,
                CommentCount = video.CommentCount,
                Duration = video.Duration,
                ThumbnailUrl = video.ThumbnailUrl,
                Description = video.Description,
                RawTranscript = string.Empty // Will be populated later if needed
            };

            _context.Videos.Add(videoEntity);
            await _context.SaveChangesAsync();

            // Load the channel relationship for return
            await _context.Entry(videoEntity)
                .Reference(v => v.Channel)
                .LoadAsync();

            _logger.LogDebug("Created new video entity for {VideoId}: {Title}", video.YouTubeVideoId, video.Title);

            return videoEntity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring video {VideoId} exists", video.YouTubeVideoId);
            throw;
        }
    }

    /// <summary>
    /// Ensures a channel entity exists in the database.
    /// Creates new channel entity if it doesn't exist, updates if it does.
    /// </summary>
    public async Task<ChannelEntity> EnsureChannelExistsAsync(string channelId, string channelName)
    {
        try
        {
            var existingChannel = await _context.Channels
                .FirstOrDefaultAsync(c => c.YouTubeChannelId == channelId);

            if (existingChannel != null)
            {
                // Update name if changed
                if (existingChannel.Name != channelName)
                {
                    existingChannel.Name = channelName;
                    existingChannel.LastModifiedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                return existingChannel;
            }

            // Create new channel
            var channelEntity = new ChannelEntity
            {
                YouTubeChannelId = channelId,
                Name = channelName,
                ThumbnailUrl = string.Empty, // Will be populated later if needed
                VideoCount = 0,
                SubscriberCount = 0,
                PublishedAt = DateTime.UtcNow // Default value, will be updated when we have real data
            };

            _context.Channels.Add(channelEntity);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Created new channel entity for {ChannelId}: {Name}", channelId, channelName);

            return channelEntity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring channel {ChannelId} exists", channelId);
            throw;
        }
    }

    /// <summary>
    /// Adds an existing video entity to the user's library.
    /// Used when we already have a validated video entity (e.g., from suggestions).
    /// </summary>
    public async Task<bool> AddExistingVideoToLibraryAsync(string userId, VideoEntity videoEntity, string notes = "")
    {
        try
        {
            // Check if already exists
            var existingUserVideo = await _context.UserVideos
                .FirstOrDefaultAsync(uv => uv.UserId == userId &&
                                          uv.Video.YouTubeVideoId == videoEntity.YouTubeVideoId);

            if (existingUserVideo != null)
            {
                _logger.LogInformation("Video {VideoId} already exists in library for user {UserId}",
                    videoEntity.YouTubeVideoId, userId);
                return false; // Already exists
            }

            // Create user-video relationship
            var userVideoEntity = new UserVideoEntity
            {
                UserId = userId,
                VideoId = videoEntity.Id,
                AddedToLibraryAt = DateTime.UtcNow,
                Status = WatchStatus.NotWatched,
                Notes = string.IsNullOrEmpty(notes) ? $"Added existing video on {DateTime.Now:yyyy-MM-dd}" : notes
            };

            _context.UserVideos.Add(userVideoEntity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added existing video {VideoId} '{Title}' to library for user {UserId}",
                videoEntity.YouTubeVideoId, videoEntity.Title, userId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding existing video {VideoId} to library for user {UserId}",
                videoEntity.YouTubeVideoId, userId);
            return false;
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Saves all discovered videos to the database for historical browsing.
    /// Handles duplicate prevention and ensures video metadata is stored.
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
    /// Gets video entities by their YouTube video IDs.
    /// Used for bulk operations and suggestion processing.
    /// </summary>
    public async Task<Dictionary<string, VideoEntity>> GetVideosByYouTubeIdsAsync(List<string> youTubeVideoIds)
    {
        try
        {
            if (!youTubeVideoIds.Any())
            {
                return new Dictionary<string, VideoEntity>();
            }

            var videos = await _context.Videos
                .Include(v => v.Channel)
                .Where(v => youTubeVideoIds.Contains(v.YouTubeVideoId))
                .ToListAsync();

            return videos.ToDictionary(v => v.YouTubeVideoId, v => v);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting videos by YouTube IDs");
            throw;
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Maps a UserVideoEntity to a VideoDisplayModel for presentation.
    /// </summary>
    private static VideoDisplayModel MapToDisplayModel(UserVideoEntity userVideo, string userId)
    {
        var displayModel = new VideoDisplayModel
        {
            Id = userVideo.Video.Id,
            YouTubeVideoId = userVideo.Video.YouTubeVideoId,
            Title = userVideo.Video.Title,
            Description = userVideo.Video.Description ?? string.Empty,
            ThumbnailUrl = userVideo.Video.ThumbnailUrl ?? string.Empty,
            Duration = ConvertSecondsToIso8601(userVideo.Video.Duration),
            ViewCount = userVideo.Video.ViewCount > 0 ? (ulong)userVideo.Video.ViewCount : null,
            LikeCount = userVideo.Video.LikeCount > 0 ? (ulong)userVideo.Video.LikeCount : null,
            CommentCount = userVideo.Video.CommentCount > 0 ? (ulong)userVideo.Video.CommentCount : null,
            PublishedAt = userVideo.Video.PublishedAt,
            ChannelId = userVideo.Video.Channel.YouTubeChannelId,
            ChannelTitle = userVideo.Video.Channel.Name,
            CategoryId = null, // Not stored currently
            Tags = new List<string>(), // Not stored currently
            DefaultLanguage = null, // Not stored currently
            IsInLibrary = true,
            AddedToLibrary = userVideo.AddedToLibraryAt,
            WatchStatus = userVideo.Status
        };

        // Map user rating if exists
        var userRating = userVideo.Video.Ratings?.FirstOrDefault(r => r.UserId == userId);
        if (userRating != null)
        {
            displayModel.UserRating = new VideoRatingModel
            {
                Id = userRating.Id,
                VideoId = userRating.VideoId ?? Guid.Empty,
                YouTubeVideoId = userVideo.Video.YouTubeVideoId,
                VideoTitle = userVideo.Video.Title,
                UserId = userRating.UserId,
                Stars = userRating.Stars,
                Notes = userRating.Notes,
                CreatedAt = userRating.CreatedAt,
                UpdatedAt = userRating.LastModifiedAt
            };
        }

        return displayModel;
    }

    /// <summary>
    /// Converts duration in seconds to ISO 8601 format (PT4M13S).
    /// Ensures compatibility with existing VideoDisplayModel.FormatDuration() method.
    /// </summary>
    private static string? ConvertSecondsToIso8601(int durationInSeconds)
    {
        if (durationInSeconds <= 0)
            return null;

        var timeSpan = TimeSpan.FromSeconds(durationInSeconds);

        // Build ISO 8601 duration format (PT4M13S)
        var result = "PT";

        if (timeSpan.TotalHours >= 1)
        {
            result += $"{(int)timeSpan.TotalHours}H";
        }

        if (timeSpan.Minutes > 0)
        {
            result += $"{timeSpan.Minutes}M";
        }

        if (timeSpan.Seconds > 0)
        {
            result += $"{timeSpan.Seconds}S";
        }

        // Handle edge case where duration is exactly on hour/minute boundaries
        if (result == "PT")
        {
            result = "PT0S";
        }

        return result;
    }

    /// <summary>
    /// Extracts channel ID from a YouTube URL.
    /// This is a temporary helper method for ChannelVideoModel conversion.
    /// TODO: ChannelVideoModel should include ChannelId directly in the future.
    /// </summary>
    private static string ExtractChannelIdFromYouTubeUrl(string youTubeUrl)
    {
        // This is a simplified implementation
        // In practice, you might need to make an API call to get the channel ID
        // For now, return a placeholder that indicates we need the actual channel ID
        return "UNKNOWN_CHANNEL_ID";
    }

    #endregion
}