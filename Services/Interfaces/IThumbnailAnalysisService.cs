using TargetBrowse.Services.Models;

namespace TargetBrowse.Services.Interfaces;

/// <summary>
/// Service for analyzing YouTube video thumbnails using AI vision capabilities.
/// Generates descriptions of thumbnails based on the video title context.
/// </summary>
public interface IThumbnailAnalysisService : IDisposable
{
    /// <summary>
    /// Analyzes a YouTube video thumbnail and generates a description.
    /// Uses the "Thumbnail Description" prompt from the database and replaces [video-title] placeholder.
    /// </summary>
    /// <param name="thumbnailUrl">The URL of the YouTube thumbnail image</param>
    /// <param name="videoTitle">The title of the video for context</param>
    /// <param name="userId">Optional user ID for tracking (null for system calls)</param>
    /// <returns>Result containing the description, token usage, and cost information</returns>
    Task<ThumbnailAnalysisResult> AnalyzeThumbnailAsync(
        string thumbnailUrl,
        string videoTitle,
        string? userId = null);
}
