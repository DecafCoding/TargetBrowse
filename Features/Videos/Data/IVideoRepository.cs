using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;

namespace TargetBrowse.Features.Videos.Data;

/// <summary>
/// Repository interface for video data access operations.
/// Handles CRUD operations for user's video library.
/// </summary>
public interface IVideoRepository
{
    /// <summary>
    /// Gets all videos in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>List of videos in the user's library</returns>
    Task<List<VideoDisplayModel>> GetUserVideosAsync(string userId);

    /// <summary>
    /// Gets a specific video by its YouTube video ID for a user.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <returns>Video if found, null otherwise</returns>
    Task<VideoDisplayModel?> GetVideoByYouTubeIdAsync(string userId, string youTubeVideoId);

    /// <summary>
    /// Adds a video to the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="video">Video information to add</param>
    /// <returns>True if added successfully, false if already exists or error occurred</returns>
    Task<bool> AddVideoAsync(string userId, VideoDisplayModel video);

    /// <summary>
    /// Removes a video from the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">System video ID (not YouTube ID)</param>
    /// <returns>True if removed successfully, false if not found</returns>
    Task<bool> RemoveVideoAsync(string userId, Guid videoId);

    /// <summary>
    /// Updates the watch status for a video in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">System video ID</param>
    /// <param name="watchStatus">New watch status</param>
    /// <returns>True if updated successfully, false if not found</returns>
    Task<bool> UpdateVideoWatchStatusAsync(string userId, Guid videoId, WatchStatus watchStatus);

    /// <summary>
    /// Checks if a video is already in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <returns>True if video is in library, false otherwise</returns>
    Task<bool> IsVideoInLibraryAsync(string userId, string youTubeVideoId);

    /// <summary>
    /// Gets the count of videos in the user's library.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Number of videos in the library</returns>
    Task<int> GetVideoCountAsync(string userId);

    /// <summary>
    /// Searches videos in the user's library by title or description.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="searchQuery">Search term</param>
    /// <returns>List of matching videos</returns>
    Task<List<VideoDisplayModel>> SearchUserVideosAsync(string userId, string searchQuery);

    /// <summary>
    /// Gets videos added to library within a date range.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="fromDate">Start date (inclusive)</param>
    /// <param name="toDate">End date (inclusive)</param>
    /// <returns>List of videos added in the date range</returns>
    Task<List<VideoDisplayModel>> GetVideosByDateRangeAsync(string userId, DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Gets videos from a specific channel that are in the user's library.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="channelId">YouTube channel ID</param>
    /// <returns>List of videos from the specified channel</returns>
    Task<List<VideoDisplayModel>> GetVideosByChannelAsync(string userId, string channelId);

    /// <summary>
    /// Updates video information (for metadata refresh).
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">System video ID</param>
    /// <param name="updatedVideo">Updated video information</param>
    /// <returns>True if updated successfully</returns>
    Task<bool> UpdateVideoAsync(string userId, Guid videoId, VideoDisplayModel updatedVideo);
}