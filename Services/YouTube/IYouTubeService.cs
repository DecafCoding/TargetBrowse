using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Services.YouTube;

/// <summary>
/// Service interface for YouTube Data API v3 integration.
/// Handles channel search, information retrieval, and API quota management.
/// </summary>
public interface IYouTubeService
{
    /// <summary>
    /// Searches for YouTube channels by name or keyword.
    /// Returns up to 10 channels ordered by relevance.
    /// </summary>
    /// <param name="searchQuery">Channel name or search term</param>
    /// <returns>List of channels found, empty list if none found or quota exceeded</returns>
    Task<YouTubeApiResult<List<YouTubeChannelResponse>>> SearchChannelsAsync(string searchQuery);

    /// <summary>
    /// Gets detailed information about a specific YouTube channel by channel ID.
    /// </summary>
    /// <param name="channelId">YouTube channel ID (UCxxxxx format)</param>
    /// <returns>Channel information or null if not found</returns>
    Task<YouTubeApiResult<YouTubeChannelResponse?>> GetChannelByIdAsync(string channelId);

    /// <summary>
    /// Gets channel information by username (legacy /user/ URLs).
    /// </summary>
    /// <param name="username">YouTube username</param>
    /// <returns>Channel information or null if not found</returns>
    Task<YouTubeApiResult<YouTubeChannelResponse?>> GetChannelByUsernameAsync(string username);

    /// <summary>
    /// Gets channel information by handle (modern @username format).
    /// </summary>
    /// <param name="handle">YouTube handle without @ symbol</param>
    /// <returns>Channel information or null if not found</returns>
    Task<YouTubeApiResult<YouTubeChannelResponse?>> GetChannelByHandleAsync(string handle);

    /// <summary>
    /// Gets channel information by custom URL (/c/ format).
    /// </summary>
    /// <param name="customUrl">Custom URL identifier</param>
    /// <returns>Channel information or null if not found</returns>
    Task<YouTubeApiResult<YouTubeChannelResponse?>> GetChannelByCustomUrlAsync(string customUrl);

    /// <summary>
    /// Checks if the YouTube API is currently available and within quota limits.
    /// </summary>
    /// <returns>True if API is available, false if quota exceeded or service unavailable</returns>
    Task<bool> IsApiAvailableAsync();

    /// <summary>
    /// Gets the estimated remaining API quota for today.
    /// </summary>
    /// <returns>Estimated remaining quota units</returns>
    Task<int> GetEstimatedRemainingQuotaAsync();
}