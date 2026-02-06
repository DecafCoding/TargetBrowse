using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.RegularExpressions;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Projects.Data;
using TargetBrowse.Features.Projects.Models;
using TargetBrowse.Services.ProjectServices.Models;

namespace TargetBrowse.Features.Projects.Services
{
    /// <summary>
    /// Implementation of project management service.
    /// Handles business logic for project CRUD operations, validation, and video management.
    /// </summary>
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly ApplicationDbContext _context;
        private readonly ProjectSettings _projectSettings;
        private readonly ILogger<ProjectService> _logger;

        public ProjectService(
            IProjectRepository projectRepository,
            ApplicationDbContext context,
            IOptions<ProjectSettings> projectSettings,
            ILogger<ProjectService> logger)
        {
            _projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _projectSettings = projectSettings?.Value ?? throw new ArgumentNullException(nameof(projectSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a project by ID with videos included.
        /// Returns null if project not found or user doesn't own it.
        /// </summary>
        public async Task<ProjectEntity?> GetProjectByIdAsync(Guid id, string userId)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(id, userId);

                if (project == null || project.UserId != userId)
                {
                    return null;
                }

                return project;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project {ProjectId} for user {UserId}", id, userId);
                throw;
            }
        }

        /// <summary>
        /// Gets a project for editing.
        /// Returns null if project not found or user doesn't own it.
        /// </summary>
        public async Task<ProjectEditViewModel?> GetProjectForEditAsync(Guid id, string userId)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(id, userId);

                if (project == null || project.UserId != userId)
                {
                    return null;
                }

                return new ProjectEditViewModel
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    UserGuidance = project.UserGuidance
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project {ProjectId} for edit for user {UserId}", id, userId);
                throw;
            }
        }

        /// <summary>
        /// Gets a project for deletion confirmation.
        /// Returns null if project not found or user doesn't own it.
        /// </summary>
        public async Task<ProjectDeleteViewModel?> GetProjectForDeleteAsync(Guid id, string userId)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(id, userId);

                if (project == null || project.UserId != userId)
                {
                    return null;
                }

                return new ProjectDeleteViewModel
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    VideoCount = project.ProjectVideos?.Count(pv => !pv.IsDeleted) ?? 0,
                    HasGuide = project.ProjectGuide != null && !project.ProjectGuide.IsDeleted
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project {ProjectId} for delete for user {UserId}", id, userId);
                throw;
            }
        }

        /// <summary>
        /// Gets detailed project information for the detail page.
        /// Returns null if project not found or user doesn't own it.
        /// </summary>
        public async Task<ProjectDetailViewModel?> GetProjectDetailAsync(Guid id, string userId)
        {
            try
            {
                var project = await _projectRepository.GetByIdAsync(id, userId);

                if (project == null || project.UserId != userId)
                {
                    return null;
                }

                // Map videos
                var videos = project.ProjectVideos?
                    .Where(pv => !pv.IsDeleted)
                    .OrderBy(pv => pv.Order)
                    .Select(pv => new ProjectVideoViewModel
                    {
                        Id = pv.Video.Id,
                        YouTubeVideoId = pv.Video.YouTubeVideoId,
                        Title = pv.Video.Title,
                        ThumbnailUrl = pv.Video.ThumbnailUrl,
                        Duration = pv.Video.Duration,
                        ChannelName = pv.Video.Channel.Name,
                        HasSummary = pv.Video.Summary != null && !pv.Video.Summary.IsDeleted,
                        SummaryPreview = GetSummaryPreview(pv?.Video?.Summary?.Content),
                        ViewCount = pv.Video.ViewCount,
                        LikeCount = pv.Video.LikeCount,
                        PublishedAt = pv.Video.PublishedAt,
                        Order = pv.Order,
                        AddedAt = pv.CreatedAt
                    })
                    .ToList() ?? new List<ProjectVideoViewModel>();

                // Check if all videos have summaries
                var allVideosHaveSummaries = videos.Count > 0 && videos.All(v => v.HasSummary);

                // Check minimum video count (assuming 1 as minimum, adjust if needed)
                var meetsMinimumVideoCount = videos.Count >= 1;

                return new ProjectDetailViewModel
                {
                    Id = project.Id,
                    Name = project.Name,
                    Description = project.Description,
                    UserGuidance = project.UserGuidance,
                    Videos = videos,
                    HasGuide = project.ProjectGuide != null && !project.ProjectGuide.IsDeleted,
                    GuideContent = project.ProjectGuide?.Content,
                    GuideGeneratedAt = project.ProjectGuide?.CreatedAt,
                    NeedsRegeneration = false,
                    AllVideosHaveSummaries = allVideosHaveSummaries,
                    MeetsMinimumVideoCount = meetsMinimumVideoCount,
                    CreatedAt = project.CreatedAt,
                    LastModifiedAt = project.LastModifiedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting project detail {ProjectId} for user {UserId}", id, userId);
                throw;
            }
        }

        /// <summary>
        /// Gets all projects for a user with video counts.
        /// </summary>
        public async Task<List<ProjectListViewModel>> GetUserProjectsAsync(string userId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new ArgumentException("User ID is required", nameof(userId));
                }

                var projects = await _projectRepository.GetUserProjectsAsync(userId);

                // Map to view models
                return projects.Select(p => new ProjectListViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    DescriptionPreview = p.Description?.Length > 100
                        ? p.Description.Substring(0, 100) + "..."
                        : p.Description,
                    VideoCount = p.ProjectVideos?.Count(pv => !pv.IsDeleted) ?? 0,
                    HasGuide = p.ProjectGuide != null && !p.ProjectGuide.IsDeleted,
                    LastModifiedAt = p.LastModifiedAt,
                    CreatedAt = p.CreatedAt
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting projects for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Creates a new project with validation.
        /// </summary>
        public async Task<ProjectEntity> CreateProjectAsync(string userId, string name, string? description, string? userGuidance)
        {
            try
            {
                // Validate user ID
                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new ArgumentException("User ID is required", nameof(userId));
                }

                // Validate name
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Project name is required", nameof(name));
                }

                name = name.Trim();
                if (name.Length < 1 || name.Length > 200)
                {
                    throw new ArgumentException("Project name must be between 1 and 200 characters", nameof(name));
                }

                // Validate description
                if (!string.IsNullOrWhiteSpace(description))
                {
                    description = description.Trim();
                    if (description.Length > 2000)
                    {
                        throw new ArgumentException("Project description cannot exceed 2000 characters", nameof(description));
                    }
                }

                // Validate user guidance
                if (!string.IsNullOrWhiteSpace(userGuidance))
                {
                    userGuidance = userGuidance.Trim();
                    if (userGuidance.Length > 1000)
                    {
                        throw new ArgumentException("User guidance cannot exceed 1000 characters", nameof(userGuidance));
                    }
                }

                // Create project
                var project = new ProjectEntity
                {
                    UserId = userId,
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description,
                    UserGuidance = string.IsNullOrWhiteSpace(userGuidance) ? null : userGuidance
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} created project {ProjectId}: {ProjectName}", userId, project.Id, name);

                return project;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Updates an existing project with validation.
        /// </summary>
        public async Task<ProjectEntity> UpdateProjectAsync(Guid id, string userId, string name, string? description, string? userGuidance)
        {
            try
            {
                // Validate user ID
                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new ArgumentException("User ID is required", nameof(userId));
                }

                // Validate name
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Project name is required", nameof(name));
                }

                name = name.Trim();
                if (name.Length < 1 || name.Length > 200)
                {
                    throw new ArgumentException("Project name must be between 1 and 200 characters", nameof(name));
                }

                // Validate description
                if (!string.IsNullOrWhiteSpace(description))
                {
                    description = description.Trim();
                    if (description.Length > 2000)
                    {
                        throw new ArgumentException("Project description cannot exceed 2000 characters", nameof(description));
                    }
                }

                // Validate user guidance
                if (!string.IsNullOrWhiteSpace(userGuidance))
                {
                    userGuidance = userGuidance.Trim();
                    if (userGuidance.Length > 1000)
                    {
                        throw new ArgumentException("User guidance cannot exceed 1000 characters", nameof(userGuidance));
                    }
                }

                // Get project
                var project = await _projectRepository.GetByIdAsync(id, userId);

                if (project == null)
                {
                    throw new ArgumentException("Project not found", nameof(id));
                }

                if (project.UserId != userId)
                {
                    throw new ArgumentException("You don't have permission to update this project", nameof(userId));
                }

                // Update project
                project.Name = name;
                project.Description = string.IsNullOrWhiteSpace(description) ? null : description;
                project.UserGuidance = string.IsNullOrWhiteSpace(userGuidance) ? null : userGuidance;

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} updated project {ProjectId}: {ProjectName}", userId, project.Id, name);

                return project;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project {ProjectId} for user {UserId}", id, userId);
                throw;
            }
        }

        /// <summary>
        /// Deletes a project (soft delete).
        /// </summary>
        public async Task DeleteProjectAsync(Guid id, string userId)
        {
            try
            {
                // Validate user ID
                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new ArgumentException("User ID is required", nameof(userId));
                }

                // Get project
                var project = await _projectRepository.GetByIdAsync(id, userId);

                if (project == null)
                {
                    throw new ArgumentException("Project not found", nameof(id));
                }

                if (project.UserId != userId)
                {
                    throw new ArgumentException("You don't have permission to delete this project", nameof(userId));
                }

                // Soft delete project
                project.IsDeleted = true;

                // Also soft delete the project guide if it exists
                if (project.ProjectGuide != null)
                {
                    project.ProjectGuide.IsDeleted = true;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} deleted project {ProjectId}: {ProjectName}", userId, project.Id, project.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {ProjectId} for user {UserId}", id, userId);
                throw;
            }
        }

        /// <summary>
        /// Checks if a video can be added to a project (not duplicate, under limit).
        /// </summary>
        public async Task<bool> CanAddVideoToProjectAsync(Guid projectId, Guid videoId)
        {
            try
            {
                // Check if video already exists in project
                var exists = await _context.ProjectVideos
                    .AnyAsync(pv => pv.ProjectId == projectId && pv.VideoId == videoId && !pv.IsDeleted);

                if (exists)
                {
                    return false;
                }

                // Check video count limit
                var videoCount = await _context.ProjectVideos
                    .CountAsync(pv => pv.ProjectId == projectId && !pv.IsDeleted);

                if (videoCount >= _projectSettings.MaxVideosPerProject)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if video {VideoId} can be added to project {ProjectId}", videoId, projectId);
                throw;
            }
        }

        /// <summary>
        /// Removes a video from a project.
        /// </summary>
        public async Task RemoveVideoFromProjectAsync(Guid projectId, Guid videoId, string userId)
        {
            try
            {
                // Validate user ID
                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new ArgumentException("User ID is required", nameof(userId));
                }

                // Get project
                var project = await _projectRepository.GetByIdAsync(projectId, userId);

                if (project == null)
                {
                    throw new ArgumentException("Project not found", nameof(projectId));
                }

                if (project.UserId != userId)
                {
                    throw new ArgumentException("You don't have permission to modify this project", nameof(userId));
                }

                // Get project video
                var projectVideo = await _context.ProjectVideos
                    .FirstOrDefaultAsync(pv => pv.ProjectId == projectId && pv.VideoId == videoId && !pv.IsDeleted);

                if (projectVideo == null)
                {
                    throw new ArgumentException("Video not found in project", nameof(videoId));
                }

                // Hard delete project video
                _context.ProjectVideos.Remove(projectVideo);

                await _context.SaveChangesAsync();

                _logger.LogInformation("User {UserId} removed video {VideoId} from project {ProjectId}", userId, videoId, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing video {VideoId} from project {ProjectId} for user {UserId}", videoId, projectId, userId);
                throw;
            }
        }

        /// <summary>
        /// Checks if all videos in the project have summaries.
        /// </summary>
        public async Task<bool> HasAllVideoSummariesAsync(Guid projectId)
        {
            try
            {
                var videosWithoutSummaries = await GetVideosWithoutSummariesAsync(projectId);
                return videosWithoutSummaries.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if all videos have summaries in project {ProjectId}", projectId);
                throw;
            }
        }

        /// <summary>
        /// Gets videos in the project that don't have summaries.
        /// </summary>
        public async Task<List<VideoEntity>> GetVideosWithoutSummariesAsync(Guid projectId)
        {
            try
            {
                var videos = await _context.ProjectVideos
                    .Where(pv => pv.ProjectId == projectId && !pv.IsDeleted)
                    .Include(pv => pv.Video)
                        .ThenInclude(v => v.Summary)
                    .OrderBy(pv => pv.Order)
                    .Select(pv => pv.Video)
                    .Where(v => v.Summary == null)
                    .ToListAsync();

                return videos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting videos without summaries for project {ProjectId}", projectId);
                throw;
            }
        }

        private string GetSummaryPreview(string htmlContent)
        {
            if (string.IsNullOrEmpty(htmlContent))
                return string.Empty;

            // Extract first paragraph from HTML
            var firstParagraph = GetFirstParagraph(htmlContent);

            // Truncate to max length (increased to show more content on project info page)
            const int maxLength = 1200;
            if (firstParagraph.Length <= maxLength)
                return firstParagraph;

            return firstParagraph.Substring(0, maxLength) + "...";
        }

        private string GetFirstParagraph(string htmlContent)
        {
            // Extract ALL paragraph tags and combine them (instead of just the first one)
            var paragraphMatches = Regex.Matches(htmlContent, @"<p[^>]*>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (paragraphMatches.Count > 0)
            {
                // Combine all paragraphs with double line breaks
                var paragraphs = paragraphMatches
                    .Cast<Match>()
                    .Select(m => StripHtmlTags(m.Groups[1].Value))
                    .Where(p => !string.IsNullOrWhiteSpace(p));

                return string.Join("\n\n", paragraphs);
            }

            // If no <p> tag found, strip all HTML and return the entire content
            var stripped = StripHtmlTags(htmlContent);
            return stripped;
        }

        private string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Remove HTML tags
            var withoutTags = Regex.Replace(html, @"<[^>]+>", " ");

            // Decode HTML entities
            var decoded = WebUtility.HtmlDecode(withoutTags);

            // Clean up whitespace
            var cleaned = Regex.Replace(decoded, @"\s+", " ").Trim();

            return cleaned;
        }
    }
}
