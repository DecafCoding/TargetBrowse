using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;
using TargetBrowse.Services.YouTube;

namespace TargetBrowse.Services.DataServices;

/// <summary>
/// Data access service implementation for user library management operations.
/// Handles UserVideoEntity operations and user-specific video library functionality.
/// Works with IVideoDataService for video entity management while focusing on library operations.
/// </summary>
public class LibraryDataService : ILibraryDataService
{
    private readonly ApplicationDbContext _context;
    private readonly IVideoDataService _videoDataService;
    private readonly ILogger<LibraryDataService> _logger;

    public LibraryDataService(
        ApplicationDbContext context,
        IVideoDataService videoDataService,
        ILogger<LibraryDataService> logger)
    {
        _context = context;
        _videoDataService = videoDataService;
        _logger = logger;
    }

    #region Library Management Operations

    /// <summary>
    /// Gets all videos in the user's library with rating information.
    /// TODO this is duplicated in the VideoRepository
    /// </summary>
    public async Task<List<UserLibraryVideoDto>> GetUserVideosAsync(string userId)
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

            return userVideos.Select(uv => MapToUserLibraryVideoDto(uv, userId)).ToList();
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
    public async Task<UserLibraryVideoDto?> GetVideoByYouTubeIdAsync(string userId, string youTubeVideoId)
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

            return userVideo != null ? MapToUserLibraryVideoDto(userVideo, userId) : null;
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
    public async Task<List<UserLibraryVideoDto>> SearchUserVideosAsync(string userId, string searchQuery)
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

            return userVideos.Select(uv => MapToUserLibraryVideoDto(uv, userId)).ToList();
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
    /// Adds a video to the user's library.
    /// Accepts domain-neutral VideoInfo DTO - features handle their own model conversions.
    /// </summary>
    public async Task<bool> AddVideoToLibraryAsync(string userId, VideoInfo video, string notes = "")
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

            // Ensure video entity exists using VideoDataService
            var videoEntity = await _videoDataService.EnsureVideoExistsAsync(video);

            // Create user-video relationship
            var userVideoEntity = new UserVideoEntity
            {
                UserId = userId,
                VideoId = videoEntity.Id,
                AddedToLibraryAt = DateTime.UtcNow,
                Status = WatchStatus.NotWatched,
                Notes = string.IsNullOrEmpty(notes) ? $"Added to library on {DateTime.Now:yyyy-MM-dd}" : notes
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

    #region Private Helper Methods

    /// <summary>
    /// Maps a UserVideoEntity to a UserLibraryVideoDto for presentation.
    /// Returns domain-neutral DTO that features can map to their own display models.
    /// </summary>
    private static UserLibraryVideoDto MapToUserLibraryVideoDto(UserVideoEntity userVideo, string userId)
    {
        var dto = new UserLibraryVideoDto
        {
            Video = new VideoInfo
            {
                YouTubeVideoId = userVideo.Video.YouTubeVideoId,
                Title = userVideo.Video.Title,
                ChannelId = userVideo.Video.Channel.YouTubeChannelId,
                ChannelName = userVideo.Video.Channel.Name,
                PublishedAt = userVideo.Video.PublishedAt,
                ViewCount = userVideo.Video.ViewCount,
                LikeCount = userVideo.Video.LikeCount,
                CommentCount = userVideo.Video.CommentCount,
                Duration = userVideo.Video.Duration,
                ThumbnailUrl = userVideo.Video.ThumbnailUrl ?? string.Empty,
                Description = userVideo.Video.Description ?? string.Empty
            },
            VideoId = userVideo.Video.Id,
            UserVideoId = userVideo.Id,
            AddedToLibraryAt = userVideo.AddedToLibraryAt,
            WatchStatus = userVideo.Status,
            Notes = userVideo.Notes
        };

        // Map user rating if exists
        var userRating = userVideo.Video.Ratings?.FirstOrDefault(r => r.UserId == userId);
        if (userRating != null)
        {
            dto.Rating = new UserVideoRating
            {
                RatingId = userRating.Id,
                Stars = userRating.Stars,
                Notes = userRating.Notes,
                CreatedAt = userRating.CreatedAt,
                UpdatedAt = userRating.LastModifiedAt
            };
        }

        return dto;
    }

    #endregion
}