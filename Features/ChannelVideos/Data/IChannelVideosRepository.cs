using TargetBrowse.Features.ChannelVideos.Models;

namespace TargetBrowse.Features.ChannelVideos.Data;

/// <summary>
/// Repository interface for channel video data access.
/// Provides methods to query database for channel and user tracking information.
/// </summary>
public interface IChannelVideosRepository
{
    /// <summary>
    /// Gets channel information from the database.
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <returns>Channel information or null if not found</returns>
    Task<ChannelInfoModel?> GetChannelInfoAsync(string youTubeChannelId);

    /// <summary>
    /// Checks if the user is tracking the specified channel.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <returns>True if user is tracking the channel</returns>
    Task<bool> IsChannelTrackedByUserAsync(string userId, string youTubeChannelId);

    /// <summary>
    /// Gets the user's rating for the specified channel.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <returns>User's rating (1-5) or null if not rated</returns>
    Task<int?> GetUserChannelRatingAsync(string userId, string youTubeChannelId);

    /// <summary>
    /// Gets channel information by the internal channel entity ID.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="channelId">Internal channel entity ID</param>
    /// <returns>Channel information or null if not found</returns>
    Task<ChannelInfoModel?> GetChannelInfoByIdAsync(Guid channelId);

    /// <summary>
    /// Gets multiple channel information records by YouTube channel IDs.
    /// Used for batch operations and validation.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="youTubeChannelIds">List of YouTube channel IDs</param>
    /// <returns>Dictionary mapping YouTube channel ID to channel info</returns>
    Task<Dictionary<string, ChannelInfoModel>> GetMultipleChannelInfoAsync(List<string> youTubeChannelIds);

    /// <summary>
    /// Gets videos from a specific channel that exist in the database.
    /// Returns videos ordered by published date (newest first).
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <returns>List of videos from the specified channel in the database</returns>
    Task<List<ChannelVideoModel>> GetChannelVideosFromDatabaseAsync(string youTubeChannelId);

    /// <summary>
    /// Gets the count of videos from a specific channel that exist in the database.
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <returns>Number of videos from this channel in the database</returns>
    Task<int> GetChannelVideosCountInDatabaseAsync(string youTubeChannelId);
}