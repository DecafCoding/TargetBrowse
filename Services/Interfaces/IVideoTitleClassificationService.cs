using TargetBrowse.Services.Models;

namespace TargetBrowse.Services.Interfaces
{
    /// <summary>
    /// Service for classifying video titles into categories using AI.
    /// Handles prompt retrieval, API calls, and AI call logging.
    /// </summary>
    public interface IVideoTitleClassificationService
    {
        /// <summary>
        /// Classifies a list of video titles into predefined categories.
        /// Retrieves prompt, calls OpenAI API, logs the call, and returns results.
        /// </summary>
        /// <param name="videos">List of videos with IDs and titles to classify</param>
        /// <param name="userId">User who initiated the classification (for logging)</param>
        /// <returns>Classification result containing category assignments and metadata</returns>
        Task<ClassificationResult> ClassifyVideoTitlesAsync(List<VideoInput> videos, string userId);
    }
}
