using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Videos.Services;

/// <summary>
/// Service interface for video management business logic.
/// Handles video search, library management, and YouTube API integration.
/// </summary>
public interface IVideoService
{
    /// <summary>
    /// Searches for YouTube videos based on search criteria.
    /// Combines YouTube API search with user's library status.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="searchModel">Search parameters and filters</param>
    /// <returns>List of video search results with library status</returns>
    Task<List<VideoDisplayModel>> SearchVideosAsync(string userId, VideoSearchModel searchModel);

    /// <summary>
    /// Gets detailed information about a specific video by YouTube video ID.
    /// Includes whether the video is already in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <returns>Video information or null if not found</returns>
    Task<VideoDisplayModel?> GetVideoByIdAsync(string userId, string youTubeVideoId);

    /// <summary>
    /// Adds a video to the user's library.
    /// Validates the video URL/ID and fetches metadata from YouTube.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="addVideoModel">Video addition request</param>
    /// <returns>True if added successfully, false if already exists or invalid</returns>
    Task<bool> AddVideoToLibraryAsync(string userId, AddVideoModel addVideoModel);

    /// <summary>
    /// Removes a video from the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">System video ID</param>
    /// <returns>True if removed successfully</returns>
    Task<bool> RemoveVideoFromLibraryAsync(string userId, Guid videoId);

    /// <summary>
    /// Updates the watch status for a video in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">System video ID</param>
    /// <param name="watchStatus">New watch status</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateVideoWatchStatusAsync(string userId, Guid videoId, WatchStatus watchStatus);

    /// <summary>
    /// Gets all videos in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>List of videos in the library</returns>
    Task<List<VideoDisplayModel>> GetUserLibraryAsync(string userId);

    /// <summary>
    /// Searches videos within the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="searchQuery">Search term</param>
    /// <returns>List of matching videos from the library</returns>
    Task<List<VideoDisplayModel>> SearchLibraryAsync(string userId, string searchQuery);

    /// <summary>
    /// Gets library statistics for the user.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Library statistics</returns>
    Task<VideoLibraryStats> GetLibraryStatsAsync(string userId);

    /// <summary>
    /// Checks if a video is already in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <returns>True if video is in library</returns>
    Task<bool> IsVideoInLibraryAsync(string userId, string youTubeVideoId);

    /// <summary>
    /// Validates a video URL and extracts video information.
    /// Does not add to library, just validates and fetches metadata.
    /// </summary>
    /// <param name="videoUrl">YouTube video URL</param>
    /// <returns>Video information if valid, null if invalid</returns>
    Task<VideoDisplayModel?> ValidateVideoUrlAsync(string videoUrl);

    /// <summary>
    /// Gets videos from the user's tracked channels that aren't in their library.
    /// Useful for suggesting new videos from followed channels.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="maxResults">Maximum number of suggestions</param>
    /// <returns>List of suggested videos</returns>
    Task<List<VideoDisplayModel>> GetSuggestedVideosFromChannelsAsync(string userId, int maxResults = 20);
}

/// <summary>
/// Statistics about the user's video library.
/// </summary>
public class VideoLibraryStats
{
    public int TotalVideos { get; set; }
    public int VideosAddedThisWeek { get; set; }
    public int VideosAddedThisMonth { get; set; }
    public Dictionary<string, int> VideosByChannel { get; set; } = new Dictionary<string, int>();
    public TimeSpan TotalDuration { get; set; }
    public DateTime? LastAddedDate { get; set; }
    public string? MostActiveChannel { get; set; }
}