using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Models;

namespace TargetBrowse.Features.Videos.Data;

/// <summary>
/// Repository implementation for video data access operations.
/// Handles CRUD operations for user's video library using Entity Framework.
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
    /// Gets all videos in the user's library.
    /// FIXED: Now includes rating information for each video.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetUserVideosAsync(string userId)
    {
        try
        {
            return await _context.UserVideos
                .Where(uv => uv.UserId == userId)
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating for this video
                .OrderByDescending(uv => uv.AddedToLibraryAt)
                .Select(uv => new VideoDisplayModel
                {
                    Id = uv.Video.Id,
                    YouTubeVideoId = uv.Video.YouTubeVideoId,
                    Title = uv.Video.Title,
                    Description = "", // Description not stored in VideoEntity per schema
                    ThumbnailUrl = GetVideoThumbnailUrl(uv.Video.YouTubeVideoId), // Use proper YouTube thumbnail
                    Duration = ConvertSecondsToIso8601(uv.Video.Duration),
                    ViewCount = (ulong)uv.Video.ViewCount,
                    LikeCount = (ulong)uv.Video.LikeCount,
                    CommentCount = (ulong)uv.Video.CommentCount,
                    PublishedAt = uv.Video.PublishedAt,
                    ChannelId = uv.Video.Channel.YouTubeChannelId,
                    ChannelTitle = uv.Video.Channel.Name,
                    AddedToLibrary = uv.AddedToLibraryAt,
                    IsInLibrary = true,
                    WatchStatus = uv.Status,
                    // FIXED: Map the user's rating for this video
                    UserRating = uv.Video.Ratings
                        .Where(r => r.UserId == userId)
                        .Select(r => new VideoRatingModel
                        {
                            Id = r.Id,
                            VideoId = r.VideoId ?? Guid.Empty,
                            YouTubeVideoId = uv.Video.YouTubeVideoId,
                            VideoTitle = uv.Video.Title,
                            UserId = r.UserId,
                            Stars = r.Stars,
                            Notes = r.Notes,
                            CreatedAt = r.CreatedAt,
                            UpdatedAt = r.LastModifiedAt
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user videos for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific video by its YouTube video ID for a user.
    /// FIXED: Now includes rating information.
    /// </summary>
    public async Task<VideoDisplayModel?> GetVideoByYouTubeIdAsync(string userId, string youTubeVideoId)
    {
        try
        {
            return await _context.UserVideos
                .Where(uv => uv.UserId == userId && uv.Video.YouTubeVideoId == youTubeVideoId)
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating for this video
                .Select(uv => new VideoDisplayModel
                {
                    Id = uv.Video.Id,
                    YouTubeVideoId = uv.Video.YouTubeVideoId,
                    Title = uv.Video.Title,
                    Description = "", // Description not stored in VideoEntity per schema
                    ThumbnailUrl = GetVideoThumbnailUrl(uv.Video.YouTubeVideoId), // Use proper YouTube thumbnail
                    Duration = ConvertSecondsToIso8601(uv.Video.Duration),
                    ViewCount = (ulong)uv.Video.ViewCount,
                    LikeCount = (ulong)uv.Video.LikeCount,
                    CommentCount = (ulong)uv.Video.CommentCount,
                    PublishedAt = uv.Video.PublishedAt,
                    ChannelId = uv.Video.Channel.YouTubeChannelId,
                    ChannelTitle = uv.Video.Channel.Name,
                    AddedToLibrary = uv.AddedToLibraryAt,
                    IsInLibrary = true,
                    WatchStatus = uv.Status,
                    // FIXED: Map the user's rating for this video
                    UserRating = uv.Video.Ratings
                        .Where(r => r.UserId == userId)
                        .Select(r => new VideoRatingModel
                        {
                            Id = r.Id,
                            VideoId = r.VideoId ?? Guid.Empty,
                            YouTubeVideoId = uv.Video.YouTubeVideoId,
                            VideoTitle = uv.Video.Title,
                            UserId = r.UserId,
                            Stars = r.Stars,
                            Notes = r.Notes,
                            CreatedAt = r.CreatedAt,
                            UpdatedAt = r.LastModifiedAt
                        })
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video {VideoId} for user {UserId}", youTubeVideoId, userId);
            throw;
        }
    }

    /// <summary>
    /// Searches videos in the user's library by title or description.
    /// FIXED: Now includes rating information.
    /// </summary>
    public async Task<List<VideoDisplayModel>> SearchUserVideosAsync(string userId, string searchQuery)
    {
        try
        {
            var searchLower = searchQuery.ToLowerInvariant();

            return await _context.UserVideos
                .Where(uv => uv.UserId == userId &&
                           (uv.Video.Title.ToLower().Contains(searchLower) ||
                            uv.Video.Channel.Name.ToLower().Contains(searchLower)))
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating for this video
                .OrderByDescending(uv => uv.AddedToLibraryAt)
                .Select(uv => new VideoDisplayModel
                {
                    Id = uv.Video.Id,
                    YouTubeVideoId = uv.Video.YouTubeVideoId,
                    Title = uv.Video.Title,
                    Description = "",
                    ThumbnailUrl = GetVideoThumbnailUrl(uv.Video.YouTubeVideoId),
                    Duration = ConvertSecondsToIso8601(uv.Video.Duration),
                    ViewCount = (ulong)uv.Video.ViewCount,
                    LikeCount = (ulong)uv.Video.LikeCount,
                    CommentCount = (ulong)uv.Video.CommentCount,
                    PublishedAt = uv.Video.PublishedAt,
                    ChannelId = uv.Video.Channel.YouTubeChannelId,
                    ChannelTitle = uv.Video.Channel.Name,
                    AddedToLibrary = uv.AddedToLibraryAt,
                    IsInLibrary = true,
                    WatchStatus = uv.Status,
                    // FIXED: Map the user's rating for this video
                    UserRating = uv.Video.Ratings
                        .Where(r => r.UserId == userId)
                        .Select(r => new VideoRatingModel
                        {
                            Id = r.Id,
                            VideoId = r.VideoId ?? Guid.Empty,
                            YouTubeVideoId = uv.Video.YouTubeVideoId,
                            VideoTitle = uv.Video.Title,
                            UserId = r.UserId,
                            Stars = r.Stars,
                            Notes = r.Notes,
                            CreatedAt = r.CreatedAt,
                            UpdatedAt = r.LastModifiedAt
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching videos for user {UserId} with query {SearchQuery}", userId, searchQuery);
            throw;
        }
    }

    /// <summary>
    /// Gets videos added to library within a date range.
    /// FIXED: Now includes rating information.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetVideosByDateRangeAsync(string userId, DateTime fromDate, DateTime toDate)
    {
        try
        {
            return await _context.UserVideos
                .Where(uv => uv.UserId == userId &&
                           uv.AddedToLibraryAt >= fromDate &&
                           uv.AddedToLibraryAt <= toDate)
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating for this video
                .OrderByDescending(uv => uv.AddedToLibraryAt)
                .Select(uv => new VideoDisplayModel
                {
                    Id = uv.Video.Id,
                    YouTubeVideoId = uv.Video.YouTubeVideoId,
                    Title = uv.Video.Title,
                    Description = "",
                    ThumbnailUrl = GetVideoThumbnailUrl(uv.Video.YouTubeVideoId),
                    Duration = ConvertSecondsToIso8601(uv.Video.Duration),
                    ViewCount = (ulong)uv.Video.ViewCount,
                    LikeCount = (ulong)uv.Video.LikeCount,
                    CommentCount = (ulong)uv.Video.CommentCount,
                    PublishedAt = uv.Video.PublishedAt,
                    ChannelId = uv.Video.Channel.YouTubeChannelId,
                    ChannelTitle = uv.Video.Channel.Name,
                    AddedToLibrary = uv.AddedToLibraryAt,
                    IsInLibrary = true,
                    WatchStatus = uv.Status,
                    // FIXED: Map the user's rating for this video
                    UserRating = uv.Video.Ratings
                        .Where(r => r.UserId == userId)
                        .Select(r => new VideoRatingModel
                        {
                            Id = r.Id,
                            VideoId = r.VideoId ?? Guid.Empty,
                            YouTubeVideoId = uv.Video.YouTubeVideoId,
                            VideoTitle = uv.Video.Title,
                            UserId = r.UserId,
                            Stars = r.Stars,
                            Notes = r.Notes,
                            CreatedAt = r.CreatedAt,
                            UpdatedAt = r.LastModifiedAt
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting videos by date range for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets videos from a specific channel that are in the user's library.
    /// FIXED: Now includes rating information.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetVideosByChannelAsync(string userId, string channelId)
    {
        try
        {
            return await _context.UserVideos
                .Where(uv => uv.UserId == userId && uv.Video.Channel.YouTubeChannelId == channelId)
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId)) // Include user's rating for this video
                .OrderByDescending(uv => uv.Video.PublishedAt)
                .Select(uv => new VideoDisplayModel
                {
                    Id = uv.Video.Id,
                    YouTubeVideoId = uv.Video.YouTubeVideoId,
                    Title = uv.Video.Title,
                    Description = "",
                    ThumbnailUrl = GetVideoThumbnailUrl(uv.Video.YouTubeVideoId),
                    Duration = ConvertSecondsToIso8601(uv.Video.Duration),
                    ViewCount = (ulong)uv.Video.ViewCount,
                    LikeCount = (ulong)uv.Video.LikeCount,
                    CommentCount = (ulong)uv.Video.CommentCount,
                    PublishedAt = uv.Video.PublishedAt,
                    ChannelId = uv.Video.Channel.YouTubeChannelId,
                    ChannelTitle = uv.Video.Channel.Name,
                    AddedToLibrary = uv.AddedToLibraryAt,
                    IsInLibrary = true,
                    WatchStatus = uv.Status,
                    // FIXED: Map the user's rating for this video
                    UserRating = uv.Video.Ratings
                        .Where(r => r.UserId == userId)
                        .Select(r => new VideoRatingModel
                        {
                            Id = r.Id,
                            VideoId = r.VideoId ?? Guid.Empty,
                            YouTubeVideoId = uv.Video.YouTubeVideoId,
                            VideoTitle = uv.Video.Title,
                            UserId = r.UserId,
                            Stars = r.Stars,
                            Notes = r.Notes,
                            CreatedAt = r.CreatedAt,
                            UpdatedAt = r.LastModifiedAt
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting videos by channel {ChannelId} for user {UserId}", channelId, userId);
            throw;
        }
    }

    // ===== REST OF THE METHODS REMAIN UNCHANGED =====

    /// <summary>
    /// Adds a video to the user's library.
    /// Creates the video and channel entities if they don't exist.
    /// </summary>
    public async Task<bool> AddVideoAsync(string userId, VideoDisplayModel video)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Check if user already has this video in their library
            var existingUserVideo = await _context.UserVideos
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.Video.YouTubeVideoId == video.YouTubeVideoId);

            if (existingUserVideo != null)
            {
                _logger.LogInformation("Video {VideoId} already exists in library for user {UserId}", video.YouTubeVideoId, userId);
                return false; // Already exists
            }

            // Get or create the channel
            var channel = await GetOrCreateChannelAsync(video.ChannelId, video.ChannelTitle, video.ThumbnailUrl);

            // Get or create the video
            var videoEntity = await GetOrCreateVideoAsync(video, channel.Id);

            // Create the user-video relationship
            var userVideo = new UserVideoEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                VideoId = videoEntity.Id,
                AddedToLibraryAt = DateTime.UtcNow,
                Status = WatchStatus.NotWatched
            };

            _context.UserVideos.Add(userVideo);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Successfully added video {VideoId} to library for user {UserId}", video.YouTubeVideoId, userId);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error adding video {VideoId} for user {UserId}", video.YouTubeVideoId, userId);
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
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.Video.Id == videoId);

            if (userVideo == null)
            {
                return false;
            }

            _context.UserVideos.Remove(userVideo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully removed video {VideoId} from library for user {UserId}", videoId, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing video {VideoId} for user {UserId}", videoId, userId);
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
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.Video.Id == videoId);

            if (userVideo == null)
            {
                _logger.LogWarning("Video {VideoId} not found in library for user {UserId}", videoId, userId);
                return false;
            }

            userVideo.Status = watchStatus;
            userVideo.StatusChangedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated watch status for video {VideoId} to {Status} for user {UserId}",
                videoId, watchStatus, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating watch status for video {VideoId} for user {UserId}", videoId, userId);
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
                .AnyAsync(uv => uv.UserId == userId && uv.Video.YouTubeVideoId == youTubeVideoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if video {VideoId} exists for user {UserId}", youTubeVideoId, userId);
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
            return 0;
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
                .FirstOrDefaultAsync(uv => uv.UserId == userId && uv.Video.Id == videoId);

            if (userVideo == null)
            {
                return false;
            }

            // Update video metadata
            userVideo.Video.Title = updatedVideo.Title;
            userVideo.Video.Duration = ConvertIso8601ToSeconds(updatedVideo.Duration);
            userVideo.Video.ViewCount = (int)(updatedVideo.ViewCount ?? 0);
            userVideo.Video.LikeCount = (int)(updatedVideo.LikeCount ?? 0);
            userVideo.Video.CommentCount = (int)(updatedVideo.CommentCount ?? 0);

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating video {VideoId} for user {UserId}", videoId, userId);
            return false;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets the proper YouTube thumbnail URL for a video.
    /// Uses maxresdefault for best quality, with fallback handled by the UI.
    /// </summary>
    private static string GetVideoThumbnailUrl(string youTubeVideoId)
    {
        return $"https://img.youtube.com/vi/{youTubeVideoId}/maxresdefault.jpg";
    }

    /// <summary>
    /// Gets an existing channel or creates a new one.
    /// </summary>
    private async Task<ChannelEntity> GetOrCreateChannelAsync(string youTubeChannelId, string channelTitle, string? thumbnailUrl)
    {
        var existingChannel = await _context.Channels
            .FirstOrDefaultAsync(c => c.YouTubeChannelId == youTubeChannelId);

        if (existingChannel != null)
        {
            // Update channel info if needed
            if (existingChannel.Name != channelTitle && !string.IsNullOrEmpty(channelTitle))
            {
                existingChannel.Name = channelTitle;
            }
            if (!string.IsNullOrEmpty(thumbnailUrl) && existingChannel.ThumbnailUrl != thumbnailUrl)
            {
                existingChannel.ThumbnailUrl = thumbnailUrl;
            }
            return existingChannel;
        }

        // Create new channel
        var newChannel = new ChannelEntity
        {
            Id = Guid.NewGuid(),
            YouTubeChannelId = youTubeChannelId,
            Name = channelTitle ?? "Unknown Channel",
            ThumbnailUrl = thumbnailUrl,
            PublishedAt = DateTime.UtcNow, // We don't have channel creation date from video API
            VideoCount = 0,
            SubscriberCount = 0
        };

        _context.Channels.Add(newChannel);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new channel {ChannelId} with name {ChannelName}", youTubeChannelId, channelTitle);
        return newChannel;
    }

    /// <summary>
    /// Gets an existing video or creates a new one.
    /// </summary>
    private async Task<VideoEntity> GetOrCreateVideoAsync(VideoDisplayModel video, Guid channelId)
    {
        var existingVideo = await _context.Videos
            .FirstOrDefaultAsync(v => v.YouTubeVideoId == video.YouTubeVideoId);

        if (existingVideo != null)
        {
            // Update video metadata with latest info
            existingVideo.Title = video.Title;
            existingVideo.Duration = ConvertIso8601ToSeconds(video.Duration);
            existingVideo.ViewCount = (int)(video.ViewCount ?? 0);
            existingVideo.LikeCount = (int)(video.LikeCount ?? 0);
            existingVideo.CommentCount = (int)(video.CommentCount ?? 0);
            return existingVideo;
        }

        // Create new video
        var newVideo = new VideoEntity
        {
            Id = Guid.NewGuid(),
            YouTubeVideoId = video.YouTubeVideoId,
            Title = video.Title,
            ChannelId = channelId,
            PublishedAt = video.PublishedAt,
            ViewCount = (int)(video.ViewCount ?? 0),
            LikeCount = (int)(video.LikeCount ?? 0),
            CommentCount = (int)(video.CommentCount ?? 0),
            Duration = ConvertIso8601ToSeconds(video.Duration),
            RawTranscript = "" // Will be populated later when needed
        };

        _context.Videos.Add(newVideo);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new video {VideoId} with title {VideoTitle}", video.YouTubeVideoId, video.Title);
        return newVideo;
    }

    /// <summary>
    /// Converts ISO 8601 duration string to seconds.
    /// </summary>
    private static int ConvertIso8601ToSeconds(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return 0;

        try
        {
            var timespan = System.Xml.XmlConvert.ToTimeSpan(duration);
            return (int)timespan.TotalSeconds;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Converts seconds to ISO 8601 duration string.
    /// </summary>
    private static string ConvertSecondsToIso8601(int seconds)
    {
        var timespan = TimeSpan.FromSeconds(seconds);
        return System.Xml.XmlConvert.ToString(timespan);
    }

    #endregion
}