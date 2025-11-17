using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Projects.Data;

/// <summary>
/// Implementation of project repository for database operations.
/// Handles projects, project videos, and project guides using Entity Framework Core.
/// Inherits common database patterns from BaseRepository.
/// </summary>
public class ProjectRepository : BaseRepository<ProjectEntity>, IProjectRepository
{
    public ProjectRepository(ApplicationDbContext context, ILogger<ProjectRepository> logger)
        : base(context, logger)
    {
    }

    /// <summary>
    /// Gets a project by its ID and verifies ownership.
    /// </summary>
    public async Task<ProjectEntity?> GetByIdAsync(Guid id, string userId)
    {
        try
        {
            return await _context.Projects
                .Include(p => p.ProjectVideos.Where(pv => !pv.IsDeleted))
                    .ThenInclude(pv => pv.Video)
                        .ThenInclude(v => v.Channel)
                .Include(p => p.ProjectGuide)
                .Where(p => p.Id == id && p.UserId == userId && !p.IsDeleted)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project by ID: {ProjectId} for user: {UserId}", id, userId);
            throw;
        }
    }

    /// <summary>
    /// Gets all projects for a user (not deleted).
    /// Ordered by LastModifiedAt descending.
    /// </summary>
    public async Task<List<ProjectEntity>> GetUserProjectsAsync(string userId)
    {
        try
        {
            return await _context.Projects
                .Include(p => p.ProjectVideos.Where(pv => !pv.IsDeleted))
                    .ThenInclude(pv => pv.Video)
                .Where(p => p.UserId == userId && !p.IsDeleted)
                .OrderByDescending(p => p.LastModifiedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects for user: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new project.
    /// </summary>
    public async Task<ProjectEntity> CreateAsync(ProjectEntity project)
    {
        try
        {
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new project: {ProjectName} (ID: {ProjectId}) for user: {UserId}",
                project.Name, project.Id, project.UserId);

            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project: {ProjectName} for user: {UserId}",
                project.Name, project.UserId);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing project.
    /// </summary>
    public async Task<ProjectEntity> UpdateAsync(ProjectEntity project)
    {
        try
        {
            _context.Projects.Update(project);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated project: {ProjectName} (ID: {ProjectId})",
                project.Name, project.Id);

            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project: {ProjectId}", project.Id);
            throw;
        }
    }

    /// <summary>
    /// Soft deletes a project and cascades to related entities.
    /// </summary>
    public async Task DeleteAsync(Guid id, string userId)
    {
        try
        {
            var project = await _context.Projects
                .Include(p => p.ProjectVideos)
                .Include(p => p.ProjectGuide)
                .Where(p => p.Id == id && p.UserId == userId && !p.IsDeleted)
                .FirstOrDefaultAsync();

            if (project == null)
            {
                _logger.LogWarning("Project not found or unauthorized: {ProjectId} for user: {UserId}", id, userId);
                return;
            }

            // Soft delete the project
            project.IsDeleted = true;

            // Cascade soft delete to ProjectVideos
            foreach (var projectVideo in project.ProjectVideos.Where(pv => !pv.IsDeleted))
            {
                projectVideo.IsDeleted = true;
            }

            // Cascade soft delete to ProjectGuide
            if (project.ProjectGuide != null && !project.ProjectGuide.IsDeleted)
            {
                project.ProjectGuide.IsDeleted = true;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted project: {ProjectId} for user: {UserId}", id, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project: {ProjectId} for user: {UserId}", id, userId);
            throw;
        }
    }

    /// <summary>
    /// Gets the count of videos in a project (not deleted).
    /// </summary>
    public async Task<int> GetVideoCountAsync(Guid projectId)
    {
        try
        {
            return await _context.ProjectVideos
                .Where(pv => pv.ProjectId == projectId && !pv.IsDeleted)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video count for project: {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Gets all videos in a project (not deleted).
    /// Ordered by Order ascending.
    /// </summary>
    public async Task<List<ProjectVideoEntity>> GetProjectVideosAsync(Guid projectId)
    {
        try
        {
            return await _context.ProjectVideos
                .Include(pv => pv.Video)
                    .ThenInclude(v => v.Channel)
                .Where(pv => pv.ProjectId == projectId && !pv.IsDeleted)
                .OrderBy(pv => pv.Order)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting videos for project: {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Gets a specific video in a project.
    /// </summary>
    public async Task<ProjectVideoEntity?> GetProjectVideoAsync(Guid projectId, Guid videoId)
    {
        try
        {
            return await _context.ProjectVideos
                .Include(pv => pv.Video)
                    .ThenInclude(v => v.Channel)
                .Where(pv => pv.ProjectId == projectId && pv.VideoId == videoId && !pv.IsDeleted)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video {VideoId} for project: {ProjectId}", videoId, projectId);
            throw;
        }
    }

    /// <summary>
    /// Adds a video to a project.
    /// Checks for duplicates before adding.
    /// </summary>
    public async Task<ProjectVideoEntity> AddVideoToProjectAsync(ProjectVideoEntity projectVideo)
    {
        try
        {
            // Check for duplicates
            var existingVideo = await _context.ProjectVideos
                .Where(pv => pv.ProjectId == projectVideo.ProjectId &&
                            pv.VideoId == projectVideo.VideoId &&
                            !pv.IsDeleted)
                .FirstOrDefaultAsync();

            if (existingVideo != null)
            {
                _logger.LogWarning("Video {VideoId} already exists in project {ProjectId}",
                    projectVideo.VideoId, projectVideo.ProjectId);
                return existingVideo;
            }

            _context.ProjectVideos.Add(projectVideo);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Added video {VideoId} to project {ProjectId} at order {Order}",
                projectVideo.VideoId, projectVideo.ProjectId, projectVideo.Order);

            return projectVideo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding video {VideoId} to project: {ProjectId}",
                projectVideo.VideoId, projectVideo.ProjectId);
            throw;
        }
    }

    /// <summary>
    /// Removes a video from a project using soft delete.
    /// </summary>
    public async Task RemoveVideoFromProjectAsync(Guid projectId, Guid videoId)
    {
        try
        {
            var projectVideo = await _context.ProjectVideos
                .Where(pv => pv.ProjectId == projectId && pv.VideoId == videoId && !pv.IsDeleted)
                .FirstOrDefaultAsync();

            if (projectVideo == null)
            {
                _logger.LogWarning("Project video not found: Project {ProjectId}, Video {VideoId}",
                    projectId, videoId);
                return;
            }

            projectVideo.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Removed video {VideoId} from project {ProjectId}", videoId, projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing video {VideoId} from project: {ProjectId}",
                videoId, projectId);
            throw;
        }
    }

    /// <summary>
    /// Gets the maximum Order value for videos in a project.
    /// Returns -1 if no videos exist.
    /// </summary>
    public async Task<int> GetMaxOrderAsync(Guid projectId)
    {
        try
        {
            var maxOrder = await _context.ProjectVideos
                .Where(pv => pv.ProjectId == projectId && !pv.IsDeleted)
                .MaxAsync(pv => (int?)pv.Order);

            return maxOrder ?? -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting max order for project: {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Gets the project guide for a project.
    /// </summary>
    public async Task<ProjectGuideEntity?> GetProjectGuideAsync(Guid projectId)
    {
        try
        {
            return await _context.ProjectGuides
                .Include(pg => pg.AICall)
                    .ThenInclude(ac => ac.Prompt.Model)
                .Include(pg => pg.AICall)
                    .ThenInclude(ac => ac.Prompt)
                .Where(pg => pg.ProjectId == projectId && !pg.IsDeleted)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting guide for project: {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new project guide.
    /// </summary>
    public async Task<ProjectGuideEntity> CreateGuideAsync(ProjectGuideEntity guide)
    {
        try
        {
            _context.ProjectGuides.Add(guide);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created guide for project: {ProjectId}", guide.ProjectId);

            return guide;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating guide for project: {ProjectId}", guide.ProjectId);
            throw;
        }
    }

    /// <summary>
    /// Updates an existing project guide.
    /// </summary>
    public async Task<ProjectGuideEntity> UpdateGuideAsync(ProjectGuideEntity guide)
    {
        try
        {
            _context.ProjectGuides.Update(guide);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated guide for project: {ProjectId}", guide.ProjectId);

            return guide;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating guide for project: {ProjectId}", guide.ProjectId);
            throw;
        }
    }
}
