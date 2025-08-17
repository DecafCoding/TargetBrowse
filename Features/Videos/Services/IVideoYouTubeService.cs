using TargetBrowse.Services.YouTube.Models;
using TargetBrowse.Features.Videos.Models;

namespace TargetBrowse.Features.Videos.Services;

/// <summary>
/// Video-specific YouTube Data API service interface.
/// Handles video search, information retrieval, and quota management for the Videos feature.
/// </summary>
public interface IVideoYouTubeService
{
    /// <summary>
    /// Searches for YouTube videos by keyword or phrase with advanced filtering options.
    /// Returns up to specified number of videos with applied filters and sorting.
    /// </summary>
    /// <param name="searchQuery">Video search keywords or phrase</param>
    /// <param name="maxResults">Maximum number of results (1-50, default 25)</param>
    /// <param name="channelId">Optional: limit search to specific channel</param>
    /// <param name="sortOrder">Sort order for results (default: Relevance)</param>
    /// <param name="durationFilter">Filter by video duration (default: Any)</param>
    /// <param name="dateFilter">Filter by upload date (default: Any)</param>
    /// <returns>List of videos found, empty list if none found or quota exceeded</returns>
    Task<YouTubeApiResult<List<YouTubeVideoResponse>>> SearchVideosAsync(
        string searchQuery, 
        int maxResults = 25, 
        string? channelId = null,
        VideoSortOrder sortOrder = VideoSortOrder.Relevance,
        VideoDurationFilter durationFilter = VideoDurationFilter.Any,
        VideoDateFilter dateFilter = VideoDateFilter.Any);

    /// <summary>
    /// Gets detailed information about a specific YouTube video by video ID.
    /// </summary>
    /// <param name="videoId">YouTube video ID (11-character format)</param>
    /// <returns>Video information or null if not found</returns>
    Task<YouTubeApiResult<YouTubeVideoResponse?>> GetVideoByIdAsync(string videoId);

    /// <summary>
    /// Gets detailed information about multiple YouTube videos by their IDs.
    /// More efficient than calling GetVideoByIdAsync multiple times.
    /// </summary>
    /// <param name="videoIds">List of YouTube video IDs (up to 50)</param>
    /// <returns>List of video information, may be fewer than requested if some videos don't exist</returns>
    Task<YouTubeApiResult<List<YouTubeVideoResponse>>> GetVideosByIdsAsync(IEnumerable<string> videoIds);

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