using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Channels.Data;

/// <summary>
/// Implementation of channel repository for database operations.
/// Handles channel entities and user-channel relationships using Entity Framework Core.
/// </summary>
public class ChannelRepository : IChannelRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChannelRepository> _logger;

    public ChannelRepository(ApplicationDbContext context, ILogger<ChannelRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets a channel by its YouTube channel ID.
    /// </summary>
    public async Task<ChannelEntity?> GetChannelByYouTubeIdAsync(string youTubeChannelId)
    {
        try
        {
            return await _context.Channels
                .Where(c => c.YouTubeChannelId == youTubeChannelId && !c.IsDeleted)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel by YouTube ID: {YouTubeChannelId}", youTubeChannelId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new channel entity in the database.
    /// </summary>
    public async Task<ChannelEntity> CreateChannelAsync(ChannelEntity channel)
    {
        try
        {
            _context.Channels.Add(channel);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new channel: {ChannelName} (ID: {YouTubeChannelId})",
                channel.Name, channel.YouTubeChannelId);

            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating channel: {ChannelName}", channel.Name);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing channel entity with new information.
    /// </summary>
    public async Task<ChannelEntity> UpdateChannelAsync(ChannelEntity channel)
    {
        try
        {
            _context.Channels.Update(channel);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated channel: {ChannelName} (ID: {YouTubeChannelId})",
                channel.Name, channel.YouTubeChannelId);

            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating channel: {ChannelName}", channel.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets all channels tracked by a specific user.
    /// </summary>
    public async Task<List<ChannelEntity>> GetTrackedChannelsByUserAsync(string userId)
    {
        try
        {
            return await _context.UserChannels
                .Where(uc => uc.UserId == userId && !uc.IsDeleted)
                .Include(uc => uc.Channel)
                .Where(uc => uc.Channel != null && !uc.Channel.IsDeleted)
                .Select(uc => uc.Channel)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracked channels for user: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets the count of channels tracked by a specific user.
    /// </summary>
    public async Task<int> GetTrackedChannelCountAsync(string userId)
    {
        try
        {
            return await _context.UserChannels
                .Where(uc => uc.UserId == userId && !uc.IsDeleted)
                .Include(uc => uc.Channel)
                .Where(uc => uc.Channel != null && !uc.Channel.IsDeleted)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracked channel count for user: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a user is already tracking a specific channel.
    /// </summary>
    public async Task<bool> IsChannelTrackedByUserAsync(string userId, string youTubeChannelId)
    {
        try
        {
            return await _context.UserChannels
                .Where(uc => uc.UserId == userId && !uc.IsDeleted)
                .Include(uc => uc.Channel)
                .AnyAsync(uc => uc.Channel != null &&
                               uc.Channel.YouTubeChannelId == youTubeChannelId &&
                               !uc.Channel.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if channel is tracked by user: {UserId}, Channel: {YouTubeChannelId}",
                userId, youTubeChannelId);
            throw;
        }
    }

    /// <summary>
    /// Adds a channel to a user's tracking list.
    /// </summary>
    public async Task<UserChannelEntity> AddChannelToUserTrackingAsync(string userId, Guid channelId)
    {
        try
        {
            var userChannel = new UserChannelEntity
            {
                UserId = userId,
                ChannelId = channelId,
                TrackedSince = DateTime.UtcNow
            };

            _context.UserChannels.Add(userChannel);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} started tracking channel {ChannelId}", userId, channelId);

            return userChannel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding channel to user tracking: User {UserId}, Channel {ChannelId}",
                userId, channelId);
            throw;
        }
    }

    /// <summary>
    /// Removes a channel from a user's tracking list using soft delete.
    /// </summary>
    public async Task<bool> RemoveChannelFromUserTrackingAsync(string userId, Guid channelId)
    {
        try
        {
            var userChannel = await _context.UserChannels
                .Where(uc => uc.UserId == userId && uc.ChannelId == channelId && !uc.IsDeleted)
                .FirstOrDefaultAsync();

            if (userChannel == null)
            {
                _logger.LogWarning("User-channel relationship not found: User {UserId}, Channel {ChannelId}",
                    userId, channelId);
                return false;
            }

            userChannel.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} stopped tracking channel {ChannelId}", userId, channelId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing channel from user tracking: User {UserId}, Channel {ChannelId}",
                userId, channelId);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific user-channel relationship.
    /// </summary>
    public async Task<UserChannelEntity?> GetUserChannelRelationshipAsync(string userId, Guid channelId)
    {
        try
        {
            return await _context.UserChannels
                .Where(uc => uc.UserId == userId && uc.ChannelId == channelId && !uc.IsDeleted)
                .Include(uc => uc.Channel)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user-channel relationship: User {UserId}, Channel {ChannelId}",
                userId, channelId);
            throw;
        }
    }

    /// <summary>
    /// Finds or creates a channel entity based on YouTube channel information.
    /// </summary>
    public async Task<ChannelEntity> FindOrCreateChannelAsync(
        string youTubeChannelId,
        string name,
        string? description = null,
        string? thumbnailUrl = null,
        ulong? subscriberCount = null,
        ulong? videoCount = null,
        DateTime? publishedAt = null)
    {
        try
        {
            // First, try to find existing channel
            var existingChannel = await GetChannelByYouTubeIdAsync(youTubeChannelId);

            if (existingChannel != null)
            {
                // Update existing channel with new information
                existingChannel.Name = name;
                existingChannel.ThumbnailUrl = thumbnailUrl;
                existingChannel.SubscriberCount = subscriberCount;
                existingChannel.VideoCount = videoCount;

                // Only update published date if provided and different
                if (publishedAt.HasValue && publishedAt.Value != default)
                {
                    existingChannel.PublishedAt = publishedAt.Value;
                }

                return await UpdateChannelAsync(existingChannel);
            }

            // Create new channel
            var newChannel = new ChannelEntity
            {
                YouTubeChannelId = youTubeChannelId,
                Name = name,
                ThumbnailUrl = thumbnailUrl,
                SubscriberCount = subscriberCount,
                VideoCount = videoCount,
                PublishedAt = publishedAt ?? DateTime.UtcNow
            };

            return await CreateChannelAsync(newChannel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding or creating channel: {YouTubeChannelId}", youTubeChannelId);
            throw;
        }
    }
}