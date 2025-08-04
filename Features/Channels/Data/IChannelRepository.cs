using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Channels.Data;

/// <summary>
/// Repository interface for channel-related database operations.
/// Handles both channel entities and user-channel relationships.
/// </summary>
public interface IChannelRepository
{
    /// <summary>
    /// Gets a channel by its YouTube channel ID.
    /// Returns null if the channel doesn't exist in our database.
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <returns>Channel entity or null if not found</returns>
    Task<ChannelEntity?> GetChannelByYouTubeIdAsync(string youTubeChannelId);

    /// <summary>
    /// Creates a new channel entity in the database.
    /// </summary>
    /// <param name="channel">Channel entity to create</param>
    /// <returns>Created channel entity with generated ID</returns>
    Task<ChannelEntity> CreateChannelAsync(ChannelEntity channel);

    /// <summary>
    /// Updates an existing channel entity with new information.
    /// </summary>
    /// <param name="channel">Channel entity to update</param>
    /// <returns>Updated channel entity</returns>
    Task<ChannelEntity> UpdateChannelAsync(ChannelEntity channel);

    /// <summary>
    /// Gets all channels tracked by a specific user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of tracked channels with tracking information</returns>
    Task<List<ChannelEntity>> GetTrackedChannelsByUserAsync(string userId);

    /// <summary>
    /// Gets the count of channels tracked by a specific user.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Number of channels tracked by the user</returns>
    Task<int> GetTrackedChannelCountAsync(string userId);

    /// <summary>
    /// Checks if a user is already tracking a specific channel.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <returns>True if user is tracking the channel, false otherwise</returns>
    Task<bool> IsChannelTrackedByUserAsync(string userId, string youTubeChannelId);

    /// <summary>
    /// Adds a channel to a user's tracking list.
    /// Creates the user-channel relationship.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="channelId">Channel entity ID</param>
    /// <returns>Created user-channel relationship</returns>
    Task<UserChannelEntity> AddChannelToUserTrackingAsync(string userId, Guid channelId);

    /// <summary>
    /// Removes a channel from a user's tracking list.
    /// Uses soft delete by setting IsDeleted flag.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="channelId">Channel entity ID</param>
    /// <returns>True if removal was successful, false if relationship not found</returns>
    Task<bool> RemoveChannelFromUserTrackingAsync(string userId, Guid channelId);

    /// <summary>
    /// Gets a specific user-channel relationship.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="channelId">Channel entity ID</param>
    /// <returns>User-channel relationship or null if not found</returns>
    Task<UserChannelEntity?> GetUserChannelRelationshipAsync(string userId, Guid channelId);

    /// <summary>
    /// Finds or creates a channel entity based on YouTube channel information.
    /// If the channel exists, updates its information. If not, creates a new one.
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <param name="name">Channel name</param>
    /// <param name="description">Channel description</param>
    /// <param name="thumbnailUrl">Channel thumbnail URL</param>
    /// <param name="subscriberCount">Channel subscriber count</param>
    /// <param name="videoCount">Channel video count</param>
    /// <param name="publishedAt">Channel creation date</param>
    /// <returns>Channel entity (existing or newly created)</returns>
    Task<ChannelEntity> FindOrCreateChannelAsync(
        string youTubeChannelId,
        string name,
        string? description = null,
        string? thumbnailUrl = null,
        ulong? subscriberCount = null,
        ulong? videoCount = null,
        DateTime? publishedAt = null);
}