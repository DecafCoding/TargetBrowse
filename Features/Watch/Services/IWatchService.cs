using TargetBrowse.Features.Watch.Models;

namespace TargetBrowse.Features.Watch.Services
{
    /// <summary>
    /// Service interface for Watch feature business logic.
    /// Handles video data retrieval and formatting for display.
    /// </summary>
    public interface IWatchService
    {
        /// <summary>
        /// Retrieves all data needed for the Watch page display.
        /// </summary>
        /// <param name="youTubeVideoId">The YouTube video ID from the URL</param>
        /// <param name="userId">The current user's ID for personalized data</param>
        /// <returns>Complete view model with video, channel, and user context</returns>
        Task<WatchViewModel> GetWatchDataAsync(string youTubeVideoId, string userId);

        /// <summary>
        /// Validates if a video exists in the database.
        /// </summary>
        /// <param name="youTubeVideoId">The YouTube video ID to check</param>
        /// <returns>True if video exists in database, false otherwise</returns>
        Task<bool> VideoExistsAsync(string youTubeVideoId);

        /// <summary>
        /// Builds the YouTube embed URL with appropriate parameters.
        /// </summary>
        /// <param name="youTubeVideoId">The YouTube video ID</param>
        /// <returns>Formatted embed URL for iframe usage</returns>
        string GetYouTubeEmbedUrl(string youTubeVideoId);
    }
}