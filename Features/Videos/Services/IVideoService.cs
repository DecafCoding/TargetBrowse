using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;

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

    // NEW METHODS FOR YT-010-03: Enhanced Suggestion Generation

    /// <summary>
    /// Saves all discovered videos to the database for historical browsing.
    /// Handles duplicate prevention and ensures video metadata is stored.
    /// Used by suggestion generation to persist all found videos regardless of approval status.
    /// </summary>
    /// <param name="videos">List of videos discovered during suggestion generation</param>
    /// <param name="userId">User identifier for logging context</param>
    /// <returns>List of video entities that were created or updated</returns>
    Task<List<VideoEntity>> SaveDiscoveredVideosAsync(List<VideoInfo> videos, string userId);

    /// <summary>
    /// Ensures a video exists in the database with complete metadata.
    /// Creates the video entity if it doesn't exist, updates metadata if it does.
    /// Handles channel relationship and maintains data integrity.
    /// </summary>
    /// <param name="video">Video information to ensure exists</param>
    /// <returns>Video entity from database</returns>
    Task<VideoEntity> EnsureVideoExistsAsync(VideoInfo video);

    /// <summary>
    /// Gets videos from a specific channel published since a given date.
    /// Delegates to YouTube service but provides consistent interface for suggestion generation.
    /// </summary>
    /// <param name="channelId">YouTube channel ID</param>
    /// <param name="since">Only return videos published after this date</param>
    /// <returns>List of videos from the channel</returns>
    //Task<List<VideoInfo>> GetChannelVideosAsync(string channelId, DateTime since);
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