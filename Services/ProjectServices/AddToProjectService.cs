using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.ProjectServices.Models;

namespace TargetBrowse.Services.ProjectServices
{
    /// <summary>
    /// Shared service for adding videos to projects across multiple features.
    /// Implements business rules for project video management.
    /// </summary>
    public class AddToProjectService : IAddToProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly ProjectSettings _projectSettings;
        private readonly ILogger<AddToProjectService> _logger;
        private readonly IVideoDataService _videoDataService;

        public AddToProjectService(
            ApplicationDbContext context,
            IOptions<ProjectSettings> projectSettings,
            ILogger<AddToProjectService> logger,
            IVideoDataService videoDataService)
        {
            _context = context;
            _projectSettings = projectSettings.Value;
            _logger = logger;
            _videoDataService = videoDataService;
        }

        /// <inheritdoc />
        public async Task<List<ProjectInfo>> GetUserProjectsAsync(string userId, Guid? videoId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
            }

            var projects = await _context.Projects
                .Where(p => p.UserId == userId)
                .Select(p => new ProjectInfo
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    CurrentVideoCount = p.ProjectVideos.Count,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.LastModifiedAt,
                    IsFull = p.ProjectVideos.Count >= _projectSettings.MaxVideosPerProject,
                    ContainsVideo = videoId.HasValue && p.ProjectVideos.Any(pv => pv.VideoId == videoId.Value)
                })
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();

            return projects;
        }

        /// <inheritdoc />
        public async Task<AddToProjectResult> AddVideoToProjectsAsync(AddToProjectRequest request)
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                return AddToProjectResult.CreateFailure("User ID is required.");
            }

            if (request.ProjectIds == null || !request.ProjectIds.Any())
            {
                return AddToProjectResult.CreateFailure("At least one project must be selected.");
            }

            Guid videoId = request.VideoId;

            // If VideoInfo is provided, ensure video exists in database (create if needed)
            if (request.VideoInfo != null)
            {
                try
                {
                    var videoEntity = await _videoDataService.EnsureVideoExistsAsync(request.VideoInfo);
                    videoId = videoEntity.Id;
                    _logger.LogInformation("Ensured video entity {VideoId} exists for YouTube video {YouTubeVideoId}",
                        videoId, request.VideoInfo.YouTubeVideoId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to ensure video entity exists for {YouTubeVideoId}",
                        request.VideoInfo.YouTubeVideoId);
                    return AddToProjectResult.CreateFailure("Failed to create video in database.");
                }
            }
            else if (videoId == Guid.Empty)
            {
                return AddToProjectResult.CreateFailure("Either VideoId or VideoInfo must be provided.");
            }

            // Check if video exists
            var videoExists = await _context.Videos.AnyAsync(v => v.Id == videoId);
            if (!videoExists)
            {
                return AddToProjectResult.CreateFailure("Video not found.");
            }

            var result = new AddToProjectResult { Success = true };
            var addedCount = 0;

            foreach (var projectId in request.ProjectIds)
            {
                // Validate each project
                var (canAdd, errorMessage) = await CanAddVideoToProjectAsync(projectId, videoId, request.UserId);

                if (!canAdd)
                {
                    result.FailedProjectIds.Add(projectId);
                    result.ProjectErrors[projectId] = errorMessage ?? "Unknown error";
                    _logger.LogWarning("Cannot add video {VideoId} to project {ProjectId}: {Error}",
                        videoId, projectId, errorMessage);
                    continue;
                }

                try
                {
                    // Get the current max order for this project
                    var maxOrder = await _context.ProjectVideos
                        .Where(pv => pv.ProjectId == projectId)
                        .MaxAsync(pv => (int?)pv.Order) ?? 0;

                    // Create new project video entry
                    var projectVideo = new ProjectVideoEntity
                    {
                        ProjectId = projectId,
                        VideoId = videoId,
                        Order = maxOrder + 1,
                        AddedAt = DateTime.UtcNow
                    };

                    _context.ProjectVideos.Add(projectVideo);
                    addedCount++;

                    _logger.LogInformation("Added video {VideoId} to project {ProjectId} at order {Order}",
                        videoId, projectId, projectVideo.Order);
                }
                catch (Exception ex)
                {
                    result.FailedProjectIds.Add(projectId);
                    result.ProjectErrors[projectId] = "Failed to add video to project.";
                    _logger.LogError(ex, "Error adding video {VideoId} to project {ProjectId}",
                        videoId, projectId);
                }
            }

            // Save all changes
            if (addedCount > 0)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    result.AddedToProjectsCount = addedCount;
                    _logger.LogInformation("Successfully added video {VideoId} to {Count} projects",
                        videoId, addedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving changes for video {VideoId}", videoId);
                    return AddToProjectResult.CreateFailure("Failed to save changes to database.");
                }
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = "Video could not be added to any of the selected projects.";
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<(bool canAdd, string? errorMessage)> CanAddVideoToProjectAsync(
            Guid projectId,
            Guid videoId,
            string userId)
        {
            // Check if project exists and belongs to user
            var project = await _context.Projects
                .Include(p => p.ProjectVideos)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return (false, "Project not found.");
            }

            if (project.UserId != userId)
            {
                return (false, "You do not have permission to modify this project.");
            }

            // Check if project is full
            if (project.ProjectVideos.Count >= _projectSettings.MaxVideosPerProject)
            {
                return (false, $"Project has reached the maximum limit of {_projectSettings.MaxVideosPerProject} videos.");
            }

            // Check if video already exists in project
            var videoExists = project.ProjectVideos.Any(pv => pv.VideoId == videoId);
            if (videoExists)
            {
                return (false, "Video already exists in this project.");
            }

            return (true, null);
        }
    }
}
