
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.ChannelVideos.Models;
using TargetBrowse.Features.TopicVideos.Models;
using TargetBrowse.Features.Suggestions.Models;

namespace TargetBrowse.Services.Interfaces;

/// <summary>
/// Data access service interface for all video-related operations.
/// Handles video entity management, user library operations, and cross-feature video data access.
/// Replaces VideoRepository and provides shared data operations for all video features.
/// </summary>
public interface IVideoDataService
{
    #region Library Management Operations

    /// <summary>
    /// Gets all videos in the user's library.
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

    #endregion

    #region Video Entity Management

    /// <summary>
    /// Ensures a video entity exists in the database with complete metadata.
    /// Creates new video and channel entities if they don't exist, updates metadata if they do.
    /// Handles channel relationships and maintains referential integrity.
    /// </summary>
    /// <param name="video">Video information to ensure exists</param>
    /// <returns>Video entity from database with proper ID and relationships</returns>
    Task<VideoEntity> EnsureVideoExistsAsync(VideoInfo video);

    /// <summary>
    /// Ensures a channel entity exists in the database.
    /// Creates new channel entity if it doesn't exist, updates if it does.
    /// Used by EnsureVideoExistsAsync to maintain channel relationships.
    /// </summary>
    /// <param name="channelId">YouTube channel ID</param>
    /// <param name="channelName">Channel name/title</param>
    /// <returns>Channel entity from database with proper ID</returns>
    Task<ChannelEntity> EnsureChannelExistsAsync(string channelId, string channelName);

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

    #region Bulk Operations

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
    /// Gets video entities by their YouTube video IDs.
    /// Used for bulk operations and suggestion processing.
    /// </summary>
    /// <param name="youTubeVideoIds">List of YouTube video IDs</param>
    /// <returns>Dictionary mapping YouTube video IDs to video entities</returns>
    Task<Dictionary<string, VideoEntity>> GetVideosByYouTubeIdsAsync(List<string> youTubeVideoIds);

    #endregion
}