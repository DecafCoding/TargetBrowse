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
                    CreatedAt = c.PublishedAt
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
                    CreatedAt = c.PublishedAt
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
                    CreatedAt = c.PublishedAt
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
}