using Google.Apis.YouTube.v3.Data;
using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Services.YouTube;

namespace TargetBrowse.Features.Videos.Data;

/// <summary>
/// Repository implementation for video data access operations.
/// Handles CRUD operations for user's video library with Entity Framework Core.
/// Fixed to include rating information when loading user videos.
/// </summary>
public class VideoRepository : IVideoRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VideoRepository> _logger;

    public VideoRepository(ApplicationDbContext context, ILogger<VideoRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all videos in the user's library with rating information.
    /// FIXED: Now includes rating data for each video.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetUserVideosAsync(string userId)
    {
        try
        {
            var userVideos = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating
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
    /// FIXED: Now includes rating data.
    /// </summary>
    public async Task<VideoDisplayModel?> GetVideoByYouTubeIdAsync(string userId, string youTubeVideoId)
    {
        try
        {
            var userVideo = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating
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
    /// Searches videos in the user's library by title or description.
    /// FIXED: Now includes rating data.
    /// </summary>
    public async Task<List<VideoDisplayModel>> SearchUserVideosAsync(string userId, string searchQuery)
    {
        try
        {
            var userVideos = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating
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

    /// <summary>
    /// Gets videos added to library within a date range.
    /// FIXED: Now includes rating data.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetVideosByDateRangeAsync(string userId, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var userVideos = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating
                .Where(uv => uv.UserId == userId &&
                            uv.AddedToLibraryAt >= fromDate &&
                            uv.AddedToLibraryAt <= toDate)
                .OrderByDescending(uv => uv.AddedToLibraryAt)
                .ToListAsync();

            return userVideos.Select(uv => MapToDisplayModel(uv, userId)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting videos by date range for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets videos from a specific channel that are in the user's library.
    /// FIXED: Now includes rating data.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetVideosByChannelAsync(string userId, string channelId)
    {
        try
        {
            var userVideos = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating
                .Where(uv => uv.UserId == userId &&
                            uv.Video.Channel.YouTubeChannelId == channelId)
                .OrderByDescending(uv => uv.AddedToLibraryAt)
                .ToListAsync();

            return userVideos.Select(uv => MapToDisplayModel(uv, userId)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting videos by channel {ChannelId} for user {UserId}",
                channelId, userId);
            throw;
        }
    }

    /// <summary>
    /// Adds a video to the user's library.
    /// </summary>
    public async Task<bool> AddVideoAsync(string userId, VideoDisplayModel video)
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
                return false; // Already exists
            }

            // Ensure channel exists
            var channel = await EnsureChannelExistsAsync(video.ChannelId, video.ChannelTitle);

            // Ensure video exists
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
                Description = video.Description ?? string.Empty,
                ThumbnailUrl = video.ThumbnailUrl ?? string.Empty
            };

            var videoEntity = await EnsureVideoExistsAsync(videoInfo);

            // Create user-video relationship
            var userVideoEntity = new UserVideoEntity
            {
                UserId = userId,
                VideoId = videoEntity.Id,
                AddedToLibraryAt = video.AddedToLibrary ?? DateTime.UtcNow,
                Status = WatchStatus.NotWatched
            };

            _context.UserVideos.Add(userVideoEntity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

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

    /// <summary>
    /// Removes a video from the user's library.
    /// </summary>
    public async Task<bool> RemoveVideoAsync(string userId, Guid videoId)
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
    /// Gets the count of videos in the user's library.
    /// </summary>
    public async Task<int> GetVideoCountAsync(string userId)
    {
        try
        {
            return await _context.UserVideos
                .CountAsync(uv => uv.UserId == userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video count for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Updates video information (for metadata refresh).
    /// </summary>
    public async Task<bool> UpdateVideoAsync(string userId, Guid videoId, VideoDisplayModel updatedVideo)
    {
        try
        {
            var userVideo = await _context.UserVideos
                .Include(uv => uv.Video)
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.Id == videoId);

            if (userVideo == null)
            {
                return false;
            }

            // Update video metadata
            userVideo.Video.Title = updatedVideo.Title;
            userVideo.Video.ViewCount = (int)(updatedVideo.ViewCount ?? 0);
            userVideo.Video.LikeCount = (int)(updatedVideo.LikeCount ?? 0);
            userVideo.Video.CommentCount = (int)(updatedVideo.CommentCount ?? 0);
            userVideo.Video.Duration = !string.IsNullOrEmpty(updatedVideo.Duration) ? int.Parse(updatedVideo.Duration) : 0;
            userVideo.Video.LastModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating video {VideoId} for user {UserId}", videoId, userId);
            return false;
        }
    }

    /// <summary>
    /// Ensures a video entity exists in the database with complete metadata.
    /// Creates new video and channel entities if they don't exist.
    /// UPDATED: Now properly saves thumbnail and description data.
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
                // Update metadata if video exists
                existingVideo.Title = video.Title;
                existingVideo.ViewCount = video.ViewCount;
                existingVideo.LikeCount = video.LikeCount;
                existingVideo.CommentCount = video.CommentCount;
                existingVideo.Duration = video.Duration;
                existingVideo.ThumbnailUrl = video.ThumbnailUrl; // ADDED: Save thumbnail URL
                existingVideo.Description = video.Description;   // ADDED: Save description
                existingVideo.LastModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
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
                ThumbnailUrl = video.ThumbnailUrl, // ADDED: Save thumbnail URL
                Description = video.Description,   // ADDED: Save description
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
    /// Bulk ensures multiple video entities exist in the database.
    /// Optimized for channel onboarding and suggestion generation when processing many videos at once.
    /// Uses efficient batch processing to minimize database round trips.
    /// MOVED TO SUGGESTION DATA SERVICE - 9/16/2025
    /// </summary>
    //public async Task<List<VideoEntity>> EnsureVideosExistAsync(List<VideoInfo> videos)
    //{
    //    try
    //    {
    //        if (!videos.Any())
    //        {
    //            return new List<VideoEntity>();
    //        }

    //        _logger.LogDebug("Ensuring {VideoCount} videos exist in database", videos.Count);

    //        // 1. Get all YouTube video IDs to check
    //        var youTubeVideoIds = videos.Select(v => v.YouTubeVideoId).Distinct().ToList();

    //        // 2. Find existing videos in one query
    //        var existingVideos = await _context.Videos
    //            .Include(v => v.Channel)
    //            .Where(v => youTubeVideoIds.Contains(v.YouTubeVideoId))
    //            .ToListAsync();

    //        var existingVideoDict = existingVideos.ToDictionary(v => v.YouTubeVideoId, v => v);

    //        // 3. Identify videos that need to be created
    //        var videosToCreate = videos
    //            .Where(v => !existingVideoDict.ContainsKey(v.YouTubeVideoId))
    //            .ToList();

    //        // 4. Update metadata for existing videos (in case it changed)
    //        foreach (var video in videos.Where(v => existingVideoDict.ContainsKey(v.YouTubeVideoId)))
    //        {
    //            var existingVideo = existingVideoDict[video.YouTubeVideoId];

    //            // Update metadata if it has changed
    //            bool hasChanges = false;

    //            if (existingVideo.Title != video.Title)
    //            {
    //                existingVideo.Title = video.Title;
    //                hasChanges = true;
    //            }

    //            if (existingVideo.ViewCount != video.ViewCount)
    //            {
    //                existingVideo.ViewCount = video.ViewCount;
    //                hasChanges = true;
    //            }

    //            if (existingVideo.LikeCount != video.LikeCount)
    //            {
    //                existingVideo.LikeCount = video.LikeCount;
    //                hasChanges = true;
    //            }

    //            if (existingVideo.CommentCount != video.CommentCount)
    //            {
    //                existingVideo.CommentCount = video.CommentCount;
    //                hasChanges = true;
    //            }

    //            if (existingVideo.Duration != video.Duration)
    //            {
    //                existingVideo.Duration = video.Duration;
    //                hasChanges = true;
    //            }

    //            // ADDED: Update thumbnail URL if changed
    //            if (existingVideo.ThumbnailUrl != video.ThumbnailUrl)
    //            {
    //                existingVideo.ThumbnailUrl = video.ThumbnailUrl;
    //                hasChanges = true;
    //            }

    //            // ADDED: Update description if changed
    //            if (existingVideo.Description != video.Description)
    //            {
    //                existingVideo.Description = video.Description;
    //                hasChanges = true;
    //            }

    //            if (hasChanges)
    //            {
    //                existingVideo.LastModifiedAt = DateTime.UtcNow;
    //            }
    //        }

    //        // 5. Create new videos if any are needed
    //        if (videosToCreate.Any())
    //        {
    //            _logger.LogDebug("Creating {NewVideoCount} new videos", videosToCreate.Count);

    //            // Ensure all required channels exist first
    //            var channelIds = videosToCreate.Select(v => v.ChannelId).Distinct().ToList();
    //            var channelDict = new Dictionary<string, ChannelEntity>();

    //            foreach (var channelId in channelIds)
    //            {
    //                var video = videosToCreate.First(v => v.ChannelId == channelId);
    //                var channel = await EnsureChannelExistsAsync(channelId, video.ChannelName);
    //                channelDict[channelId] = channel;
    //            }

    //            // Create new video entities
    //            var newVideoEntities = new List<VideoEntity>();

    //            foreach (var video in videosToCreate)
    //            {
    //                try
    //                {
    //                    var channel = channelDict[video.ChannelId];

    //                    var videoEntity = new VideoEntity
    //                    {
    //                        YouTubeVideoId = video.YouTubeVideoId,
    //                        Title = video.Title,
    //                        ChannelId = channel.Id,
    //                        PublishedAt = video.PublishedAt,
    //                        ViewCount = video.ViewCount,
    //                        LikeCount = video.LikeCount,
    //                        CommentCount = video.CommentCount,
    //                        Duration = video.Duration,
    //                        ThumbnailUrl = video.ThumbnailUrl, // ADDED: Save thumbnail URL
    //                        Description = video.Description,   // ADDED: Save description
    //                        RawTranscript = string.Empty // Will be populated later if needed
    //                    };

    //                    // Set the channel navigation property
    //                    videoEntity.Channel = channel;

    //                    newVideoEntities.Add(videoEntity);
    //                    existingVideoDict[video.YouTubeVideoId] = videoEntity;
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.LogWarning(ex, "Failed to prepare video entity for {VideoId} in bulk operation",
    //                        video.YouTubeVideoId);
    //                    // Continue with other videos
    //                }
    //            }

    //            // Batch insert new videos
    //            if (newVideoEntities.Any())
    //            {
    //                _context.Videos.AddRange(newVideoEntities);
    //            }
    //        }

    //        // 6. Save all changes in one transaction
    //        if (videosToCreate.Any() || existingVideos.Any(v => _context.Entry(v).State == EntityState.Modified))
    //        {
    //            await _context.SaveChangesAsync();
    //        }

    //        // 7. Return all requested videos (existing + newly created)
    //        var resultVideos = youTubeVideoIds
    //            .Where(id => existingVideoDict.ContainsKey(id))
    //            .Select(id => existingVideoDict[id])
    //            .ToList();

    //        _logger.LogInformation("Ensured {ResultCount} videos exist ({ExistingCount} existing, {NewCount} created)",
    //            resultVideos.Count, existingVideos.Count, videosToCreate.Count);

    //        return resultVideos;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error ensuring {VideoCount} videos exist", videos.Count);
    //        throw;
    //    }
    //}

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

    /// <summary>
    /// Bulk creates video entities from VideoInfo objects.
    /// Optimized for suggestion generation when processing many videos at once.
    /// </summary>
    public async Task<List<VideoEntity>> BulkCreateVideosAsync(List<VideoInfo> videos)
    {
        try
        {
            if (!videos.Any())
            {
                return new List<VideoEntity>();
            }

            var createdEntities = new List<VideoEntity>();

            // Process in batches to avoid database timeout
            const int batchSize = 50;
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
                        _logger.LogWarning(ex, "Failed to create video {VideoId} in bulk operation", video.YouTubeVideoId);
                        // Continue with other videos
                    }
                }

                createdEntities.AddRange(batchEntities);
            }

            _logger.LogInformation("Bulk created {CreatedCount} out of {TotalCount} videos",
                createdEntities.Count, videos.Count);

            return createdEntities;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk create videos operation");
            throw;
        }
    }

    /// <summary>
    /// Maps a UserVideoEntity to a VideoDisplayModel for presentation.
    /// FIXED: Now properly includes rating information from the loaded data.
    /// </summary>
    private static VideoDisplayModel MapToDisplayModel(UserVideoEntity userVideo, string userId)
    {
        var displayModel = new VideoDisplayModel
        {
            Id = userVideo.Id,
            YouTubeVideoId = userVideo.Video.YouTubeVideoId,
            Title = userVideo.Video.Title,
            Description = string.Empty, // Not stored in VideoEntity currently
            ThumbnailUrl = string.Empty, // Not stored in VideoEntity currently
            Duration = userVideo.Video.Duration > 0 ? userVideo.Video.Duration.ToString() : null,
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

        // FIXED: Map the rating information if it exists
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
}