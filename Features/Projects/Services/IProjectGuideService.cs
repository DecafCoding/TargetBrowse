using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Projects.Services
{
    /// <summary>
    /// Service interface for project guide generation and management.
    /// Handles AI-powered guide generation from video summaries.
    /// </summary>
    public interface IProjectGuideService
    {
        /// <summary>
        /// Checks if a guide can be generated for a project.
        /// Verifies all videos have summaries and daily AI limit not exceeded.
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <param name="userId">User ID for daily limit checking</param>
        /// <returns>True if guide can be generated</returns>
        Task<bool> CanGenerateGuideAsync(Guid projectId, string userId);

        /// <summary>
        /// Generates a new guide for a project using AI.
        /// Creates AICallEntity record with full audit trail.
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <param name="userId">User ID for AI call tracking</param>
        /// <returns>Generated project guide</returns>
        /// <exception cref="InvalidOperationException">Thrown when prerequisites not met or daily limit exceeded</exception>
        Task<ProjectGuideEntity> GenerateGuideAsync(Guid projectId, string userId);

        /// <summary>
        /// Regenerates an existing guide (soft deletes old one, creates new one).
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <param name="userId">User ID for AI call tracking</param>
        /// <returns>Regenerated project guide</returns>
        /// <exception cref="InvalidOperationException">Thrown when prerequisites not met or daily limit exceeded</exception>
        Task<ProjectGuideEntity> RegenerateGuideAsync(Guid projectId, string userId);

        /// <summary>
        /// Checks if a guide needs regeneration based on changes.
        /// Compares UserGuidance snapshot and video count.
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <returns>True if regeneration needed</returns>
        Task<bool> ShouldRegenerateGuideAsync(Guid projectId);

        /// <summary>
        /// Gets the count of AI calls made by user today (summaries + guides).
        /// Used for enforcing daily limit (10 per day).
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Count of AI calls today</returns>
        Task<int> GetDailyAICallCountAsync(string userId);
    }
}
