using Google.Apis.YouTube.v3.Data;
using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Services.YouTube;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Videos.Data;

/// <summary>
/// Repository implementation for video data access operations.
/// Handles CRUD operations for user's video library with Entity Framework Core.
/// Fixed to include rating information when loading user videos.
/// Updated to include video type information for content classification.
/// Inherits common database patterns from BaseRepository.
/// </summary>
public class VideoRepository : BaseRepository<VideoEntity>, IVideoRepository
{
    private readonly IVideoDataService _videoDataService;

    public VideoRepository(
        ApplicationDbContext context,
        ILogger<VideoRepository> logger,
        IVideoDataService videoDataService)
        : base(context, logger)
    {
        _videoDataService = videoDataService ?? throw new ArgumentNullException(nameof(videoDataService));
    }

    /// <summary>
    /// Gets all videos in the user's library with rating and video type information.
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
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.VideoType)
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
    /// Includes rating and video type information.
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
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.VideoType)
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
    /// Includes rating and video type information.
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
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.VideoType)
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
    /// Includes rating and video type information.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetVideosByDateRangeAsync(string userId, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var userVideos = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId))
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.VideoType)
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
    /// Includes rating and video type information.
    /// </summary>
    public async Task<List<VideoDisplayModel>> GetVideosByChannelAsync(string userId, string channelId)
    {
        try
        {
            var userVideos = await _context.UserVideos
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Channel)
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.Ratings.Where(r => r.UserId == userId))
                .Include(uv => uv.Video)
                    .ThenInclude(v => v.VideoType)
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

            // Ensure video exists (this also ensures channel exists)
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

            var videoEntity = await _videoDataService.EnsureVideoExistsAsync(videoInfo);

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
    /// Maps a UserVideoEntity to a VideoDisplayModel for presentation.
    /// Includes video type information for content classification.
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
            WatchStatus = userVideo.Status,
            // Map video type properties
            VideoTypeId = userVideo.Video.VideoTypeId,
            VideoTypeName = userVideo.Video.VideoType?.Name,
            VideoTypeCode = userVideo.Video.VideoType?.Code
        };

        // Map user rating if it exists
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
    /// This ensures compatibility with the existing VideoDisplayModel.FormatDuration() method.
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
}
