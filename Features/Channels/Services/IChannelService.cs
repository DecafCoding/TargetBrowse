using TargetBrowse.Features.Channels.Models;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Service interface for channel management operations.
/// Handles business logic for YouTube channel search, tracking, and management.
/// </summary>
public interface IChannelService
{
    /// <summary>
    /// Searches for YouTube channels by name or analyzes a YouTube URL.
    /// Returns search results that can be added to tracking.
    /// </summary>
    /// <param name="searchQuery">Channel name or YouTube URL</param>
    /// <returns>List of channels found, empty list if none found</returns>
    Task<List<ChannelDisplayModel>> SearchChannelsAsync(string searchQuery);

    /// <summary>
    /// Adds a channel to the user's tracking list.
    /// Enforces business rules: 50 channel limit, no duplicates.
    /// </summary>
    /// <param name="userId">User ID to add channel for</param>
    /// <param name="channelModel">Channel information to add</param>
    /// <returns>True if successful, false if validation failed</returns>
    Task<bool> AddChannelToTrackingAsync(string userId, AddChannelModel channelModel);

    /// <summary>
    /// Removes a channel from the user's tracking list.
    /// Uses soft delete to maintain data integrity.
    /// </summary>
    /// <param name="userId">User ID requesting the removal</param>
    /// <param name="channelId">ID of the channel to remove</param>
    /// <returns>True if successful, false if validation failed or channel not found</returns>
    Task<bool> RemoveChannelFromTrackingAsync(string userId, Guid channelId);

    /// <summary>
    /// Gets all channels tracked by the specified user.
    /// Returns display models suitable for UI presentation.
    /// </summary>
    /// <param name="userId">User ID to get tracked channels for</param>
    /// <returns>List of tracked channels, empty list if none found</returns>
    Task<List<ChannelDisplayModel>> GetTrackedChannelsAsync(string userId);

    /// <summary>
    /// Gets the current count of tracked channels for a user.
    /// Used for validation and UI display.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User ID to count channels for</param>
    /// <returns>Number of channels the user currently tracks</returns>
    Task<int> GetTrackedChannelCountAsync(string userId);

    /// <summary>
    /// Checks if a channel is already being tracked by the user.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User ID to check for</param>
    /// <param name="youTubeChannelId">YouTube channel ID to check</param>
    /// <returns>True if channel is already tracked, false otherwise</returns>
    Task<bool> IsChannelTrackedAsync(string userId, string youTubeChannelId);

    /// <summary>
    /// Gets detailed information about a specific YouTube channel.
    /// Used for displaying channel details and metadata.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <returns>Channel display model with full information, null if not found</returns>
    Task<ChannelDisplayModel?> GetChannelDetailsAsync(string youTubeChannelId);
}