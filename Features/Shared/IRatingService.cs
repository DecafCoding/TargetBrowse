using TargetBrowse.Features.Channels.Models;

namespace TargetBrowse.Features.Shared.Services;

/// <summary>
/// Unified rating service interface for suggestion generation.
/// Provides consolidated access to both channel and video ratings.
/// </summary>
public interface IRatingService
{
    /// <summary>
    /// Gets channel ratings optimized for suggestion processing.
    /// Returns a dictionary keyed by channel ID for fast lookup during suggestion scoring.
    /// Excludes 1-star rated channels to prevent them from appearing in suggestions.
    /// </summary>
    /// <param name="userId">User ID to get channel ratings for</param>
    /// <returns>Dictionary of channel ID to star rating (1-star channels excluded)</returns>
    Task<Dictionary<Guid, int>> GetChannelRatings(string userId);

    /// <summary>
    /// Gets YouTube channel IDs for channels rated 1-star by the user.
    /// These channels should be completely excluded from suggestion processing.
    /// </summary>
    /// <param name="userId">User ID to get low-rated channels for</param>
    /// <returns>List of YouTube channel IDs that are rated 1-star</returns>
    Task<List<string>> GetLowRatedYouTubeChannelIds(string userId);
}