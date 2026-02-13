using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;
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
        private readonly ISharedYouTubeService _sharedYouTubeService;

        public AddToProjectService(
            ApplicationDbContext context,
            IOptions<ProjectSettings> projectSettings,
            ILogger<AddToProjectService> logger,
            IVideoDataService videoDataService,
            ISharedYouTubeService sharedYouTubeService)
        {
            _context = context;
            _projectSettings = projectSettings.Value;
            _logger = logger;
            _videoDataService = videoDataService;
            _sharedYouTubeService = sharedYouTubeService;
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
                // Enrich stats from YouTube API if missing (e.g. videos added from search results)
                if (request.VideoInfo.ViewCount == 0 && request.VideoInfo.LikeCount == 0 && request.VideoInfo.Duration == 0)
                {
                    await EnrichVideoStatsAsync(request.VideoInfo);
                }

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

        /// <summary>
        /// Fetches full video details from YouTube API and merges stats into the VideoInfo.
        /// Used when videos come from search results which only include snippet data.
        /// </summary>
        private async Task EnrichVideoStatsAsync(VideoInfo videoInfo)
        {
            try
            {
                var result = await _sharedYouTubeService.GetVideoDetailsByIdsAsync(
                    new List<string> { videoInfo.YouTubeVideoId });

                if (result.IsSuccess && result.Data?.Count > 0)
                {
                    var details = result.Data[0];
                    videoInfo.ViewCount = details.ViewCount;
                    videoInfo.LikeCount = details.LikeCount;
                    videoInfo.CommentCount = details.CommentCount;
                    videoInfo.Duration = details.Duration;

                    _logger.LogInformation(
                        "Enriched video {YouTubeVideoId} with stats: {Views} views, {Likes} likes, {Duration}s duration",
                        videoInfo.YouTubeVideoId, details.ViewCount, details.LikeCount, details.Duration);
                }
                else
                {
                    _logger.LogWarning("Could not fetch stats for video {YouTubeVideoId}, saving with zero stats",
                        videoInfo.YouTubeVideoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich stats for video {YouTubeVideoId}, saving with zero stats",
                    videoInfo.YouTubeVideoId);
            }
        }
    }
}
