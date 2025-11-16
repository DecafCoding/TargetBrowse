using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Models;

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
    /// Gets all videos in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>List of videos in the library</returns>
    Task<List<VideoDisplayModel>> GetUserLibraryAsync(string userId);

    /// <summary>
    /// Checks if a video is already in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <returns>True if video is in library</returns>
    Task<bool> IsVideoInLibraryAsync(string userId, string youTubeVideoId);

    /// <summary>
    /// Adds an existing video entity to the user's library.
    /// Used when we already have a validated video entity (e.g., from suggestions).
    /// Skips YouTube API validation since the video is already in our database.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoEntity">Existing video entity</param>
    /// <returns>True if added successfully, false if already exists or error occurred</returns>
    Task<bool> AddExistingVideoToLibraryAsync(string userId, VideoEntity videoEntity);
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