using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Watch.Data
{
    /// <summary>
    /// Repository interface for Watch feature data operations.
    /// Handles retrieving video information and user-specific context.
    /// </summary>
    public interface IWatchRepository
    {
        /// <summary>
        /// Retrieves a video by its YouTube ID, including related channel data.
        /// </summary>
        /// <param name="youTubeVideoId">The YouTube video ID</param>
        /// <returns>Video entity with channel data, or null if not found</returns>
        Task<VideoEntity?> GetVideoByYouTubeIdAsync(string youTubeVideoId);

        /// <summary>
        /// Gets the user's rating for a specific video if it exists.
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="videoId">The database video ID (Guid)</param>
        /// <returns>Rating entity or null if user hasn't rated the video</returns>
        Task<RatingEntity?> GetUserVideoRatingAsync(string userId, Guid videoId);

        /// <summary>
        /// Gets the user's relationship with a video (library status, watch status).
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="videoId">The database video ID (Guid)</param>
        /// <returns>UserVideo entity or null if no relationship exists</returns>
        Task<UserVideoEntity?> GetUserVideoAsync(string userId, Guid videoId);

        /// <summary>
        /// Checks if a video is in the user's library.
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="videoId">The database video ID (Guid)</param>
        /// <returns>True if video is in user's library, false otherwise</returns>
        Task<bool> IsVideoInUserLibraryAsync(string userId, Guid videoId);

        /// <summary>
        /// Checks if a video has been marked as watched by the user.
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="videoId">The database video ID (Guid)</param>
        /// <returns>True if video is marked as watched, false otherwise</returns>
        Task<bool> IsVideoWatchedAsync(string userId, Guid videoId);

        /// <summary>
        /// Checks if a transcript exists for the video.
        /// </summary>
        /// <param name="videoId">The database video ID (Guid)</param>
        /// <returns>True if transcript exists, false otherwise</returns>
        Task<bool> HasTranscriptAsync(Guid videoId);

        /// <summary>
        /// Checks if a summary exists for this video.
        /// </summary>
        /// <param name="videoId">The database video ID (Guid)</param>
        /// <returns>True if a summary exists for this video, false otherwise</returns>
        Task<bool> HasSummaryAsync(Guid videoId);

        /// <summary>
        /// Updates the transcript for a video in the database.
        /// </summary>
        /// <param name="videoId">The database video ID (Guid)</param>
        /// <param name="transcript">The transcript text to store</param>
        /// <returns>True if update was successful, false otherwise</returns>
        Task<bool> UpdateVideoTranscriptAsync(Guid videoId, string transcript);

        /// <summary>
        /// Gets the most recent summary for a video if available
        /// </summary>
        Task<SummaryEntity?> GetMostRecentSummaryAsync(Guid videoId);
    }
}