using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Features.ChannelVideos.Models;

namespace TargetBrowse.Features.ChannelVideos.Data;

/// <summary>
/// Repository implementation for channel video data access.
/// Provides efficient database queries for channel information and user tracking status.
/// </summary>
public class ChannelVideosRepository : IChannelVideosRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChannelVideosRepository> _logger;

    public ChannelVideosRepository(
        ApplicationDbContext context,
        ILogger<ChannelVideosRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets channel information from the database by YouTube channel ID.
    /// </summary>
    public async Task<ChannelInfoModel?> GetChannelInfoAsync(string youTubeChannelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(youTubeChannelId))
                return null;

            var channel = await _context.Channels
                .Where(c => c.YouTubeChannelId == youTubeChannelId)
                .Select(c => new ChannelInfoModel
                {
                    YouTubeChannelId = c.YouTubeChannelId,
                    Name = c.Name,
                    ThumbnailUrl = c.ThumbnailUrl,
                    SubscriberCount = c.SubscriberCount,
                    VideoCount = c.VideoCount,
                    CreatedAt = c.PublishedAt,
                    LastCheckDate = c.LastCheckDate  // Added this line
                })
                .FirstOrDefaultAsync();

            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel info for YouTube channel ID: {ChannelId}", youTubeChannelId);
            return null;
        }
    }

    /// <summary>
    /// Checks if the user is tracking the specified channel.
    /// </summary>
    public async Task<bool> IsChannelTrackedByUserAsync(string userId, string youTubeChannelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(youTubeChannelId))
                return false;

            var isTracked = await _context.UserChannels
                .AnyAsync(uc => uc.UserId == userId && 
                               uc.Channel.YouTubeChannelId == youTubeChannelId);

            return isTracked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} tracks channel {ChannelId}", 
                userId, youTubeChannelId);
            return false;
        }
    }

    /// <summary>
    /// Gets the user's rating for the specified channel.
    /// </summary>
    public async Task<int?> GetUserChannelRatingAsync(string userId, string youTubeChannelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(youTubeChannelId))
                return null;

            var rating = await _context.Ratings
                .Where(r => r.UserId == userId && 
                           r.ChannelId.HasValue && 
                           r.Channel!.YouTubeChannelId == youTubeChannelId)
                .Select(r => (int?)r.Stars)
                .FirstOrDefaultAsync();

            return rating;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId} rating for channel {ChannelId}", 
                userId, youTubeChannelId);
            return null;
        }
    }

    /// <summary>
    /// Gets channel information by the internal channel entity ID.
    /// </summary>
    public async Task<ChannelInfoModel?> GetChannelInfoByIdAsync(Guid channelId)
    {
        try
        {
            if (channelId == Guid.Empty)
                return null;

            var channel = await _context.Channels
                .Where(c => c.Id == channelId)
                .Select(c => new ChannelInfoModel
                {
                    YouTubeChannelId = c.YouTubeChannelId,
                    Name = c.Name,
                    ThumbnailUrl = c.ThumbnailUrl,
                    SubscriberCount = c.SubscriberCount,
                    VideoCount = c.VideoCount,
                    CreatedAt = c.PublishedAt,
                    LastCheckDate = c.LastCheckDate  // Added this line
                })
                .FirstOrDefaultAsync();

            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel info for channel ID: {ChannelId}", channelId);
            return null;
        }
    }

    /// <summary>
    /// Gets multiple channel information records by YouTube channel IDs.
    /// </summary>
    public async Task<Dictionary<string, ChannelInfoModel>> GetMultipleChannelInfoAsync(List<string> youTubeChannelIds)
    {
        try
        {
            if (!youTubeChannelIds?.Any() == true)
                return new Dictionary<string, ChannelInfoModel>();

            var validChannelIds = youTubeChannelIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (!validChannelIds.Any())
                return new Dictionary<string, ChannelInfoModel>();

            var channels = await _context.Channels
                .Where(c => validChannelIds.Contains(c.YouTubeChannelId))
                .Select(c => new ChannelInfoModel
                {
                    YouTubeChannelId = c.YouTubeChannelId,
                    Name = c.Name,
                    ThumbnailUrl = c.ThumbnailUrl,
                    SubscriberCount = c.SubscriberCount,
                    VideoCount = c.VideoCount,
                    CreatedAt = c.PublishedAt,
                    LastCheckDate = c.LastCheckDate  // Added this line
                })
                .ToListAsync();

            return channels.ToDictionary(c => c.YouTubeChannelId, c => c);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting multiple channel info for {Count} channel IDs",
                youTubeChannelIds?.Count ?? 0);
            return new Dictionary<string, ChannelInfoModel>();
        }
    }

    /// <summary>
    /// Gets videos from a specific channel that exist in the database.
    /// Returns videos ordered by published date (newest first).
    /// </summary>
    public async Task<List<ChannelVideoModel>> GetChannelVideosFromDatabaseAsync(string youTubeChannelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(youTubeChannelId))
                return new List<ChannelVideoModel>();

            var videos = await _context.Videos
                .Where(v => v.Channel.YouTubeChannelId == youTubeChannelId)
                .OrderByDescending(v => v.PublishedAt)
                .Select(v => new ChannelVideoModel
                {
                    YouTubeVideoId = v.YouTubeVideoId,
                    Title = v.Title,
                    Description = v.Description ?? string.Empty,
                    ThumbnailUrl = v.ThumbnailUrl ?? string.Empty,
                    Duration = v.Duration,
                    ViewCount = v.ViewCount > 0 ? v.ViewCount : 0,
                    LikeCount = v.LikeCount > 0 ? v.LikeCount : 0,
                    CommentCount = v.CommentCount > 0 ? v.CommentCount : 0,
                    PublishedAt = v.PublishedAt
                })
                .ToListAsync();

            return videos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting videos from database for channel {ChannelId}", youTubeChannelId);
            return new List<ChannelVideoModel>();
        }
    }

    /// <summary>
    /// Gets the count of videos from a specific channel that exist in the database.
    /// </summary>
    public async Task<int> GetChannelVideosCountInDatabaseAsync(string youTubeChannelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(youTubeChannelId))
                return 0;

            var count = await _context.Videos
                .Where(v => v.Channel.YouTubeChannelId == youTubeChannelId)
                .CountAsync();

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video count from database for channel {ChannelId}", youTubeChannelId);
            return 0;
        }
    }
}