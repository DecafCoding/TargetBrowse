using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Projects.Models;

namespace TargetBrowse.Features.Projects.Services
{
    /// <summary>
    /// Service interface for generating video scripts from project videos.
    /// Handles the complete workflow: analysis, outline, and script generation.
    /// </summary>
    public interface IScriptGenerationService
    {
        /// <summary>
        /// Analyzes videos in a project to identify themes, conflicts, and cohesion (Phase 2).
        /// Creates or updates ScriptContent entity with analysis results.
        /// </summary>
        /// <param name="projectId">Project ID to analyze</param>
        /// <param name="userId">User ID for tracking and quota</param>
        /// <returns>Analysis result with main topic, subtopics, conflicts, etc.</returns>
        Task<ScriptAnalysisResult> AnalyzeProjectVideosAsync(Guid projectId, string userId);

        /// <summary>
        /// Configures script generation settings (Phase 3).
        /// Updates target length and validates user has profile.
        /// </summary>
        /// <param name="projectId">Project ID to configure</param>
        /// <param name="userId">User ID for validation</param>
        /// <param name="targetLengthMinutes">Desired script length in minutes</param>
        /// <returns>Result with success status and error message if failed</returns>
        Task<ScriptConfigurationResult> ConfigureScriptAsync(Guid projectId, string userId, int targetLengthMinutes);

        /// <summary>
        /// Generates a script outline based on analysis and configuration (Phase 4).
        /// Creates structured sections with timing and key points.
        /// </summary>
        /// <param name="projectId">Project ID to generate outline for</param>
        /// <param name="userId">User ID for tracking and quota</param>
        /// <returns>Outline result with sections, hook, and conclusion</returns>
        Task<ScriptOutlineResult> GenerateOutlineAsync(Guid projectId, string userId);

        /// <summary>
        /// Gets the script content entity for a project.
        /// Returns null if no script generation has been started.
        /// </summary>
        /// <param name="projectId">Project ID to retrieve script for</param>
        /// <returns>Script content entity or null</returns>
        Task<ScriptContentEntity?> GetScriptContentAsync(Guid projectId);

        /// <summary>
        /// Checks if a project can generate a script.
        /// Validates: min 3 videos, all have summaries, user has profile, daily limit not exceeded.
        /// </summary>
        /// <param name="projectId">Project ID to check</param>
        /// <param name="userId">User ID to check quota</param>
        /// <returns>True if script generation can proceed</returns>
        Task<bool> CanGenerateScriptAsync(Guid projectId, string userId);

        /// <summary>
        /// Gets the current daily AI call count for a user.
        /// Used to enforce daily generation limits.
        /// </summary>
        /// <param name="userId">User ID to check</param>
        /// <returns>Number of AI calls today</returns>
        Task<int> GetDailyAICallCountAsync(string userId);
    }
}
