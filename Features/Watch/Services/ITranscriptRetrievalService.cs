namespace TargetBrowse.Features.Watch.Services;

/// <summary>
/// Service interface for retrieving and storing YouTube video transcripts.
/// Bridges the gap between the Watch feature and the core TranscriptService.
/// </summary>
public interface ITranscriptRetrievalService
{
    /// <summary>
    /// Retrieves a transcript for the specified YouTube video and stores it in the database.
    /// This is an async operation that typically takes 8-20 seconds.
    /// </summary>
    /// <param name="youTubeVideoId">The YouTube video ID</param>
    /// <returns>True if transcript was successfully retrieved and stored, false otherwise</returns>
    Task<bool> RetrieveAndStoreTranscriptAsync(string youTubeVideoId);

    /// <summary>
    /// Checks if a transcript retrieval is currently in progress for the specified video.
    /// Prevents duplicate API calls for the same video.
    /// </summary>
    /// <param name="youTubeVideoId">The YouTube video ID</param>
    /// <returns>True if retrieval is in progress, false otherwise</returns>
    bool IsRetrievalInProgress(string youTubeVideoId);
}