using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Services;

/// <summary>
/// Service for categorizing videos in a user's library using AI classification.
/// Orchestrates the workflow of querying uncategorized videos, calling AI classification,
/// and updating video records with their assigned VideoType.
/// </summary>
public class LibraryVideoClassificationService : ILibraryVideoClassificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IVideoTitleClassificationService _classificationService;
    private readonly ILogger<LibraryVideoClassificationService> _logger;
    private const string UnknownCategoryCode = "UNKNOWN";

    public LibraryVideoClassificationService(
        ApplicationDbContext context,
        IVideoTitleClassificationService classificationService,
        ILogger<LibraryVideoClassificationService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _classificationService = classificationService ?? throw new ArgumentNullException(nameof(classificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Categorizes all uncategorized videos in a user's library.
    /// Processes videos in batches and updates video records with their assigned VideoType.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="progressCallback">Optional callback for progress updates</param>
    /// <returns>Result containing success count and statistics</returns>
    public async Task<CategorizationResult> CategorizeUserLibraryVideosAsync(
        string userId,
        Action<string>? progressCallback = null)
    {
        var result = new CategorizationResult();
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting categorization for user {UserId}", userId);
            progressCallback?.Invoke("Loading uncategorized videos...");

            // Get all uncategorized videos from user's library
            var uncategorizedVideos = await GetUncategorizedVideosAsync(userId);

            if (!uncategorizedVideos.Any())
            {
                _logger.LogInformation("No uncategorized videos found for user {UserId}", userId);
                result.Message = "All videos in your library are already categorized.";
                return result;
            }

            result.TotalVideos = uncategorizedVideos.Count;
            _logger.LogInformation("Found {Count} uncategorized videos for user {UserId}",
                uncategorizedVideos.Count, userId);

            // Convert to VideoInput format for classification service
            var videoInputs = uncategorizedVideos.Select(v => new VideoInput
            {
                VideoId = v.YouTubeVideoId,
                Title = v.Title
            }).ToList();

            // Get all video types for mapping
            var videoTypes = await _context.VideoTypes.ToDictionaryAsync(vt => vt.Code, vt => vt);

            // Ensure UNKNOWN type exists
            if (!videoTypes.ContainsKey(UnknownCategoryCode))
            {
                _logger.LogWarning("UNKNOWN video type not found in database, creating it");
                var unknownType = new VideoTypeEntity
                {
                    Code = UnknownCategoryCode,
                    Name = "Unknown",
                    Description = "Videos that could not be categorized"
                };
                _context.VideoTypes.Add(unknownType);
                await _context.SaveChangesAsync();
                videoTypes[UnknownCategoryCode] = unknownType;
            }

            // Call classification service (handles prompt retrieval, API calls, and logging internally)
            progressCallback?.Invoke($"Categorizing {uncategorizedVideos.Count} videos using AI...");
            var classificationResult = await _classificationService.ClassifyVideoTitlesAsync(videoInputs, userId);

            if (!classificationResult.Success)
            {
                _logger.LogError("Classification failed: {Error}", classificationResult.ErrorMessage);
                result.Message = $"Classification failed: {classificationResult.ErrorMessage}";
                return result;
            }

            // Update video records with classifications
            progressCallback?.Invoke("Updating video records...");
            var updateResult = await UpdateVideoTypesAsync(
                uncategorizedVideos,
                classificationResult,
                videoTypes,
                progressCallback);

            result.SuccessfullyCategorized = updateResult.SuccessCount;
            result.FailedCategorizations = updateResult.FailureCount;
            result.CategoryCounts = updateResult.CategoryCounts;
            result.Success = true;

            var duration = DateTime.UtcNow - startTime;
            result.Message = $"Successfully categorized {result.SuccessfullyCategorized} out of {result.TotalVideos} videos in {duration.TotalSeconds:F1} seconds.";

            _logger.LogInformation(
                "Categorization complete for user {UserId}. Success: {Success}/{Total}, Failures: {Failures}",
                userId, result.SuccessfullyCategorized, result.TotalVideos, result.FailedCategorizations);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during categorization for user {UserId}: {Message}", userId, ex.Message);
            result.Message = $"Categorization error: {ex.Message}";
            result.Success = false;
            return result;
        }
    }

    /// <summary>
    /// Gets count of uncategorized videos in user's library.
    /// Used to determine if categorization button should be enabled.
    /// </summary>
    public async Task<int> GetUncategorizedVideoCountAsync(string userId)
    {
        try
        {
            var count = await _context.UserVideos
                .Where(uv => uv.UserId == userId && uv.Video.VideoTypeId == null)
                .CountAsync();

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting uncategorized video count for user {UserId}", userId);
            return 0;
        }
    }

    /// <summary>
    /// Retrieves all videos in user's library that don't have a VideoTypeId assigned.
    /// FIXED: Apply Include before Select to avoid EF Core projection error.
    /// </summary>
    private async Task<List<VideoEntity>> GetUncategorizedVideosAsync(string userId)
    {
        return await _context.UserVideos
            .Include(uv => uv.Video)
                .ThenInclude(v => v.Channel)
            .Where(uv => uv.UserId == userId && uv.Video.VideoTypeId == null)
            .Select(uv => uv.Video)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// Updates video records with their assigned VideoType based on classification results.
    /// Videos that weren't successfully classified are assigned UNKNOWN type.
    /// </summary>
    private async Task<UpdateResult> UpdateVideoTypesAsync(
        List<VideoEntity> videos,
        ClassificationResult classificationResult,
        Dictionary<string, VideoTypeEntity> videoTypes,
        Action<string>? progressCallback)
    {
        var updateResult = new UpdateResult();
        var classificationMap = classificationResult.Classifications
            .ToDictionary(c => c.VideoId, c => c);

        var unknownTypeId = videoTypes[UnknownCategoryCode].Id;

        foreach (var video in videos)
        {
            try
            {
                VideoTypeEntity? videoType = null;

                // Try to find classification result for this video
                if (classificationMap.TryGetValue(video.YouTubeVideoId, out var classification))
                {
                    // Try to find the video type by code
                    if (videoTypes.TryGetValue(classification.Code, out videoType))
                    {
                        video.VideoTypeId = videoType.Id;
                        updateResult.CategoryCounts.TryGetValue(classification.Code, out int count);
                        updateResult.CategoryCounts[classification.Code] = count + 1;
                        updateResult.SuccessCount++;

                        _logger.LogDebug("Categorized video {VideoId} as {Category}",
                            video.YouTubeVideoId, classification.Code);
                    }
                    else
                    {
                        // Invalid category code returned by AI - use UNKNOWN
                        _logger.LogWarning("Invalid category code {Code} for video {VideoId}, using UNKNOWN",
                            classification.Code, video.YouTubeVideoId);
                        video.VideoTypeId = unknownTypeId;
                        updateResult.CategoryCounts.TryGetValue(UnknownCategoryCode, out int count);
                        updateResult.CategoryCounts[UnknownCategoryCode] = count + 1;
                        updateResult.FailureCount++;
                    }
                }
                else
                {
                    // No classification result found - use UNKNOWN
                    _logger.LogWarning("No classification result for video {VideoId}, using UNKNOWN",
                        video.YouTubeVideoId);
                    video.VideoTypeId = unknownTypeId;
                    updateResult.CategoryCounts.TryGetValue(UnknownCategoryCode, out int count);
                    updateResult.CategoryCounts[UnknownCategoryCode] = count + 1;
                    updateResult.FailureCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating video {VideoId}: {Message}",
                    video.YouTubeVideoId, ex.Message);

                // Assign UNKNOWN on error
                video.VideoTypeId = unknownTypeId;
                updateResult.FailureCount++;
            }
        }

        // Save all changes
        await _context.SaveChangesAsync();
        progressCallback?.Invoke($"Updated {updateResult.SuccessCount} video records");

        return updateResult;
    }

    /// <summary>
    /// Internal result class for update operations.
    /// </summary>
    private class UpdateResult
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    }
}

/// <summary>
/// Result of a video categorization operation.
/// </summary>
public class CategorizationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalVideos { get; set; }
    public int SuccessfullyCategorized { get; set; }
    public int FailedCategorizations { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
}
