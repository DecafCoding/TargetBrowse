using TargetBrowse.Data.Entities;

namespace TargetBrowse.Services.Interfaces
{
    /// <summary>
    /// Data access service for summary-related operations.
    /// Provides shared data access methods for video summaries.
    /// </summary>
    public interface ISummaryDataService
    {
        /// <summary>
        /// Creates a new summary for a video.
        /// </summary>
        /// <param name="videoId">The video ID (Guid) to associate with this summary</param>
        /// <param name="content">The detailed summary content (max 4000 characters)</param>
        /// <param name="summary">The short summary (max 1000 characters)</param>
        /// <param name="aiCallId">Optional AI call ID for audit trail</param>
        /// <returns>The created SummaryEntity with populated Id</returns>
        Task<SummaryEntity> CreateSummaryAsync(Guid videoId, string content, string summary, Guid? aiCallId = null);

        /// <summary>
        /// Gets a summary by video ID.
        /// Returns null if no summary exists for the video.
        /// </summary>
        /// <param name="videoId">The video ID (Guid) to look up</param>
        /// <returns>SummaryEntity if found, null otherwise</returns>
        Task<SummaryEntity?> GetSummaryByVideoIdAsync(Guid videoId);
    }
}
