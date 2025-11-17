using TargetBrowse.Services.ProjectServices.Models;

namespace TargetBrowse.Services.ProjectServices
{
    /// <summary>
    /// Shared service for adding videos to projects across multiple features.
    /// Follows Vertical Slice Architecture (VSA) pattern - shared service in /Services/ProjectServices/
    /// </summary>
    public interface IAddToProjectService
    {
        /// <summary>
        /// Gets all projects for a user, with optional video context for checking if video already exists.
        /// </summary>
        /// <param name="userId">User ID to get projects for</param>
        /// <param name="videoId">Optional video ID to check if it exists in each project</param>
        /// <returns>List of user's projects with metadata</returns>
        Task<List<ProjectInfo>> GetUserProjectsAsync(string userId, Guid? videoId = null);

        /// <summary>
        /// Adds a video to one or more projects.
        /// Enforces business rules: max videos per project, no duplicates, proper ordering.
        /// </summary>
        /// <param name="request">Request containing video ID, project IDs, and user ID</param>
        /// <returns>Result with success status and details about the operation</returns>
        Task<AddToProjectResult> AddVideoToProjectsAsync(AddToProjectRequest request);

        /// <summary>
        /// Checks if a video can be added to a specific project.
        /// Validates: project exists, user owns project, project not full, video not already in project.
        /// </summary>
        /// <param name="projectId">Project ID to check</param>
        /// <param name="videoId">Video ID to check</param>
        /// <param name="userId">User ID for authorization</param>
        /// <returns>Tuple with (canAdd, errorMessage)</returns>
        Task<(bool canAdd, string? errorMessage)> CanAddVideoToProjectAsync(Guid projectId, Guid videoId, string userId);
    }
}
