using TargetBrowse.Services.Models;

namespace TargetBrowse.Services.Interfaces
{
    /// <summary>
    /// Service for summarizing video transcripts using AI.
    /// Handles prompt retrieval, transcript preparation, API calls, and summary storage.
    /// </summary>
    public interface ITranscriptSummaryService
    {
        /// <summary>
        /// Summarizes a video transcript using AI.
        /// Retrieves the video, determines the appropriate prompt based on video type,
        /// calls OpenAI API, logs the call, and stores the summary.
        /// </summary>
        /// <param name="videoId">The ID of the video to summarize</param>
        /// <param name="userId">User who initiated the summarization (for logging)</param>
        /// <returns>Summary result containing the generated summary and metadata</returns>
        Task<SummaryResult> SummarizeVideoTranscriptAsync(Guid videoId, string? userId);
    }
}
