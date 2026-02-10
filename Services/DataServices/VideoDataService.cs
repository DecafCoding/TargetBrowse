using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;
using TargetBrowse.Services.YouTube;

namespace TargetBrowse.Services.DataServices;

/// <summary>
/// Data access service implementation for video and channel entity management.
/// Handles VideoEntity and ChannelEntity operations - core video data storage and retrieval.
/// Library operations have been moved to LibraryDataService to maintain single responsibility principle.
/// </summary>
public class VideoDataService : IVideoDataService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<VideoDataService> _logger;

    public VideoDataService(IDbContextFactory<ApplicationDbContext> contextFactory, ILogger<VideoDataService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    #region Video Entity Management

    /// <summary>
    /// Ensures a video entity exists in the database with complete metadata.
    /// Creates new video and channel entities if they don't exist, updates metadata if they do.
    /// </summary>
    public async Task<VideoEntity> EnsureVideoExistsAsync(VideoInfo video)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            // First try to find existing video
            var existingVideo = await context.Videos
                .Include(v => v.Channel)
                .FirstOrDefaultAsync(v => v.YouTubeVideoId == video.YouTubeVideoId);

            if (existingVideo != null)
            {
                // Return existing video as is for now
                // Could add metadata update logic here if needed
                return existingVideo;
            }

            // Ensure channel exists first (uses its own context internally)
            var channel = await EnsureChannelExistsAsync(video.ChannelId, video.ChannelName);

            // Truncate description to stay within DB column limit
            var description = video.Description?.Length > 5000
                ? video.Description[..5000]
                : video.Description ?? string.Empty;

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
                Description = description,
                RawTranscript = string.Empty // Will be populated later if needed
            };

            context.Videos.Add(videoEntity);
            await context.SaveChangesAsync();

            // Load the channel relationship for return
            await context.Entry(videoEntity)
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
            await using var context = await _contextFactory.CreateDbContextAsync();

            var existingChannel = await context.Channels
                .FirstOrDefaultAsync(c => c.YouTubeChannelId == channelId);

            if (existingChannel != null)
            {
                // Update name if changed
                if (existingChannel.Name != channelName)
                {
                    existingChannel.Name = channelName;
                    existingChannel.LastModifiedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
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

            context.Channels.Add(channelEntity);
            await context.SaveChangesAsync();

            _logger.LogDebug("Created new channel entity for {ChannelId}: {Name}", channelId, channelName);

            return channelEntity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring channel {ChannelId} exists", channelId);
            throw;
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

            await using var context = await _contextFactory.CreateDbContextAsync();

            var videos = await context.Videos
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
}