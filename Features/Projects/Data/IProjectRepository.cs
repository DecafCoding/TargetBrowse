using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Projects.Data;

/// <summary>
/// Repository interface for project-related database operations.
/// Handles projects, project videos, and project guides.
/// </summary>
public interface IProjectRepository
{
    /// <summary>
    /// Gets a project by its ID and verifies ownership.
    /// Returns null if project not found or user is not the owner.
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <param name="userId">User ID for ownership verification</param>
    /// <returns>Project entity or null if not found or unauthorized</returns>
    Task<ProjectEntity?> GetByIdAsync(Guid id, string userId);

    /// <summary>
    /// Gets all projects for a user (not deleted).
    /// Ordered by LastModifiedAt descending (most recently modified first).
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of user's projects</returns>
    Task<List<ProjectEntity>> GetUserProjectsAsync(string userId);

    /// <summary>
    /// Creates a new project.
    /// </summary>
    /// <param name="project">Project entity to create</param>
    /// <returns>Created project entity with generated ID</returns>
    Task<ProjectEntity> CreateAsync(ProjectEntity project);

    /// <summary>
    /// Updates an existing project.
    /// </summary>
    /// <param name="project">Project entity to update</param>
    /// <returns>Updated project entity</returns>
    Task<ProjectEntity> UpdateAsync(ProjectEntity project);

    /// <summary>
    /// Soft deletes a project and cascades to related entities.
    /// Sets IsDeleted=true on the project, all ProjectVideos, and ProjectGuide.
    /// </summary>
    /// <param name="id">Project ID</param>
    /// <param name="userId">User ID for ownership verification</param>
    Task DeleteAsync(Guid id, string userId);

    /// <summary>
    /// Gets the count of videos in a project (not deleted).
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Number of videos in the project</returns>
    Task<int> GetVideoCountAsync(Guid projectId);

    /// <summary>
    /// Gets all videos in a project (not deleted).
    /// Ordered by Order ascending (sequence order).
    /// Includes Video and Channel navigation properties.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>List of project videos with related data</returns>
    Task<List<ProjectVideoEntity>> GetProjectVideosAsync(Guid projectId);

    /// <summary>
    /// Gets a specific video in a project.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="videoId">Video ID</param>
    /// <returns>Project video entity or null if not found</returns>
    Task<ProjectVideoEntity?> GetProjectVideoAsync(Guid projectId, Guid videoId);

    /// <summary>
    /// Adds a video to a project.
    /// Checks for duplicates before adding.
    /// Sets Order to max(Order) + 1 for the project.
    /// </summary>
    /// <param name="projectVideo">Project video entity to add</param>
    /// <returns>Created project video entity</returns>
    Task<ProjectVideoEntity> AddVideoToProjectAsync(ProjectVideoEntity projectVideo);

    /// <summary>
    /// Removes a video from a project using soft delete.
    /// Sets IsDeleted=true on the ProjectVideo relationship.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <param name="videoId">Video ID</param>
    Task RemoveVideoFromProjectAsync(Guid projectId, Guid videoId);

    /// <summary>
    /// Gets the maximum Order value for videos in a project.
    /// Returns -1 if no videos exist (so first video gets Order=0).
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Maximum Order value, or -1 if no videos</returns>
    Task<int> GetMaxOrderAsync(Guid projectId);

    /// <summary>
    /// Gets the project guide for a project.
    /// Returns null if no guide exists.
    /// </summary>
    /// <param name="projectId">Project ID</param>
    /// <returns>Project guide entity or null if not found</returns>
    Task<ProjectGuideEntity?> GetProjectGuideAsync(Guid projectId);

    /// <summary>
    /// Creates a new project guide.
    /// </summary>
    /// <param name="guide">Project guide entity to create</param>
    /// <returns>Created project guide entity with generated ID</returns>
    Task<ProjectGuideEntity> CreateGuideAsync(ProjectGuideEntity guide);

    /// <summary>
    /// Updates an existing project guide.
    /// </summary>
    /// <param name="guide">Project guide entity to update</param>
    /// <returns>Updated project guide entity</returns>
    Task<ProjectGuideEntity> UpdateGuideAsync(ProjectGuideEntity guide);
}
