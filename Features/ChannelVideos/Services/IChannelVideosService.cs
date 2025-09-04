using TargetBrowse.Features.ChannelVideos.Models;

namespace TargetBrowse.Features.ChannelVideos.Services;

/// <summary>
/// Service interface for channel videos functionality.
/// Coordinates between data access, YouTube API, and business logic.
/// </summary>
public interface IChannelVideosService
{
    /// <summary>
    /// Gets recent videos from a channel along with channel information.
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <param name="userId">Current user ID for tracking status</param>
    /// <returns>Complete channel videos model for display</returns>
    Task<ChannelVideosModel> GetChannelVideosAsync(string youTubeChannelId, string userId);

    /// <summary>
    /// Gets channel information only (no videos).
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <param name="userId">Current user ID for tracking status</param>
    /// <returns>Channel information model</returns>
    Task<ChannelInfoModel?> GetChannelInfoAsync(string youTubeChannelId, string userId);

    /// <summary>
    /// Validates that a YouTube channel ID exists and is accessible.
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel ID to validate</param>
    /// <returns>True if channel exists and is accessible</returns>
    Task<bool> ValidateChannelExistsAsync(string youTubeChannelId);

    /// <summary>
    /// Gets the user's tracking status for a channel.
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="youTubeChannelId">YouTube channel ID</param>
    /// <returns>True if user is tracking the channel</returns>
    Task<bool> IsChannelTrackedByUserAsync(string userId, string youTubeChannelId);
}