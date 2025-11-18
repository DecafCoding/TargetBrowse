using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Projects.Models;

namespace TargetBrowse.Features.Projects.Services
{
    /// <summary>
    /// Service interface for project management business logic.
    /// Handles CRUD operations, validation, and video management for projects.
    /// </summary>
    public interface IProjectService
    {
        /// <summary>
        /// Gets a project by ID with videos included.
        /// Returns null if project not found or user doesn't own it.
        /// </summary>
        /// <param name="id">Project ID</param>
        /// <param name="userId">User ID for ownership validation</param>
        /// <returns>Project entity with videos or null</returns>
        Task<ProjectEntity?> GetProjectByIdAsync(Guid id, string userId);

        /// <summary>
        /// Gets a project for editing.
        /// Returns null if project not found or user doesn't own it.
        /// </summary>
        /// <param name="id">Project ID</param>
        /// <param name="userId">User ID for ownership validation</param>
        /// <returns>Project view model for editing or null</returns>
        Task<ProjectEditViewModel?> GetProjectForEditAsync(Guid id, string userId);

        /// <summary>
        /// Gets a project for deletion confirmation.
        /// Returns null if project not found or user doesn't own it.
        /// </summary>
        /// <param name="id">Project ID</param>
        /// <param name="userId">User ID for ownership validation</param>
        /// <returns>Project view model for deletion or null</returns>
        Task<ProjectDeleteViewModel?> GetProjectForDeleteAsync(Guid id, string userId);

        /// <summary>
        /// Gets all projects for a user with video counts.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of user's projects as view models</returns>
        Task<List<ProjectListViewModel>> GetUserProjectsAsync(string userId);

        /// <summary>
        /// Creates a new project with validation.
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="name">Project name (required, 1-200 characters)</param>
        /// <param name="description">Project description (optional, max 2000 characters)</param>
        /// <param name="userGuidance">User guidance for AI guide generation (optional, max 1000 characters)</param>
        /// <returns>Created project</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails</exception>
        Task<ProjectEntity> CreateProjectAsync(string userId, string name, string? description, string? userGuidance);

        /// <summary>
        /// Updates an existing project with validation.
        /// </summary>
        /// <param name="id">Project ID</param>
        /// <param name="userId">User ID for ownership validation</param>
        /// <param name="name">Project name (required, 1-200 characters)</param>
        /// <param name="description">Project description (optional, max 2000 characters)</param>
        /// <param name="userGuidance">User guidance for AI guide generation (optional, max 1000 characters)</param>
        /// <returns>Updated project</returns>
        /// <exception cref="ArgumentException">Thrown when validation fails or project not found</exception>
        Task<ProjectEntity> UpdateProjectAsync(Guid id, string userId, string name, string? description, string? userGuidance);

        /// <summary>
        /// Deletes a project (soft delete).
        /// </summary>
        /// <param name="id">Project ID</param>
        /// <param name="userId">User ID for ownership validation</param>
        /// <exception cref="ArgumentException">Thrown when project not found or user doesn't own it</exception>
        Task DeleteProjectAsync(Guid id, string userId);

        /// <summary>
        /// Checks if a video can be added to a project (not duplicate, under limit).
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <param name="videoId">Video ID</param>
        /// <returns>True if video can be added</returns>
        Task<bool> CanAddVideoToProjectAsync(Guid projectId, Guid videoId);

        /// <summary>
        /// Removes a video from a project.
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <param name="videoId">Video ID</param>
        /// <param name="userId">User ID for ownership validation</param>
        /// <exception cref="ArgumentException">Thrown when project not found or user doesn't own it</exception>
        Task RemoveVideoFromProjectAsync(Guid projectId, Guid videoId, string userId);

        /// <summary>
        /// Checks if all videos in the project have summaries.
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <returns>True if all videos have summaries</returns>
        Task<bool> HasAllVideoSummariesAsync(Guid projectId);

        /// <summary>
        /// Gets videos in the project that don't have summaries.
        /// </summary>
        /// <param name="projectId">Project ID</param>
        /// <returns>List of videos without summaries</returns>
        Task<List<VideoEntity>> GetVideosWithoutSummariesAsync(Guid projectId);
    }
}
