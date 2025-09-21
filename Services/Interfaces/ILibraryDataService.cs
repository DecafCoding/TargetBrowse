using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.ChannelVideos.Models;
using TargetBrowse.Features.TopicVideos.Models;

namespace TargetBrowse.Services.Interfaces;

/// <summary>
/// Data access service interface for user library management operations.
/// Handles UserVideoEntity operations and user-specific video library functionality.
/// Separated from IVideoDataService to maintain single responsibility principle.
/// </summary>
public interface ILibraryDataService
{
    #region Library Management Operations

    /// <summary>
    /// Gets all videos in the user's library with rating information.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>List of videos in the user's library</returns>
    Task<List<VideoDisplayModel>> GetUserVideosAsync(string userId);

    /// <summary>
    /// Gets a specific video by its YouTube video ID for a user.
    /// Returns null if the video is not in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <returns>Video if found in user's library, null otherwise</returns>
    Task<VideoDisplayModel?> GetVideoByYouTubeIdAsync(string userId, string youTubeVideoId);

    /// <summary>
    /// Checks if a video is already in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <returns>True if video is in library, false otherwise</returns>
    Task<bool> IsVideoInLibraryAsync(string userId, string youTubeVideoId);

    /// <summary>
    /// Removes a video from the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">System video ID (not YouTube ID)</param>
    /// <returns>True if removed successfully, false if not found</returns>
    Task<bool> RemoveVideoFromLibraryAsync(string userId, Guid videoId);

    /// <summary>
    /// Updates the watch status for a video in the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">System video ID</param>
    /// <param name="watchStatus">New watch status</param>
    /// <returns>True if updated successfully, false if not found</returns>
    Task<bool> UpdateVideoWatchStatusAsync(string userId, Guid videoId, WatchStatus watchStatus);

    /// <summary>
    /// Searches videos in the user's library by title or description.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="searchQuery">Search term</param>
    /// <returns>List of matching videos from user's library</returns>
    Task<List<VideoDisplayModel>> SearchUserVideosAsync(string userId, string searchQuery);

    #endregion

    #region Feature-Specific Library Operations

    /// <summary>
    /// Adds a video from channel browsing to the user's library.
    /// Handles conversion from ChannelVideoModel and includes channel context.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="video">Channel video model</param>
    /// <param name="notes">Optional notes about adding the video</param>
    /// <returns>True if added successfully, false if already exists or error occurred</returns>
    Task<bool> AddChannelVideoToLibraryAsync(string userId, ChannelVideoModel video, string notes = "");

    /// <summary>
    /// Adds a video from topic search to the user's library.
    /// Handles conversion from TopicVideoDisplayModel and includes topic context.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="video">Topic video model with relevance information</param>
    /// <param name="notes">Optional notes about adding the video</param>
    /// <returns>True if added successfully, false if already exists or error occurred</returns>
    Task<bool> AddTopicVideoToLibraryAsync(string userId, TopicVideoDisplayModel video, string notes = "");

    /// <summary>
    /// Adds a video from general video display to the user's library.
    /// Handles VideoDisplayModel directly - used by video search results.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="video">Video display model</param>
    /// <param name="notes">Optional notes about adding the video</param>
    /// <returns>True if added successfully, false if already exists or error occurred</returns>
    Task<bool> AddVideoDisplayToLibraryAsync(string userId, VideoDisplayModel video, string notes = "");

    /// <summary>
    /// Adds an existing video entity to the user's library.
    /// Used when we already have a validated video entity (e.g., from suggestions).
    /// Skips video existence checks since the entity is already in our database.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoEntity">Existing video entity</param>
    /// <param name="notes">Optional notes about adding the video</param>
    /// <returns>True if added successfully, false if already exists or error occurred</returns>
    Task<bool> AddExistingVideoToLibraryAsync(string userId, VideoEntity videoEntity, string notes = "");

    #endregion
}