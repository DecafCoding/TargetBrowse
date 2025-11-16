using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Models;
using TargetBrowse.Services.YouTube;

namespace TargetBrowse.Services.Interfaces;

/// <summary>
/// Data access service interface for video and channel entity management.
/// Handles VideoEntity and ChannelEntity operations - core video data storage and retrieval.
/// Separated from library operations which are handled by ILibraryDataService.
/// </summary>
public interface IVideoDataService
{
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