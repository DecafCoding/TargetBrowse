using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Services;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Videos.Services;

/// <summary>
/// Database-backed implementation of video rating service.
/// Provides full rating functionality with Entity Framework Core persistence.
/// </summary>
public class VideoRatingService : RatingServiceBase<VideoRatingModel, RateVideoModel>, IVideoRatingService
{
    private readonly ILogger<VideoRatingService> _logger;

    public VideoRatingService(
        ApplicationDbContext context,
        IMessageCenterService messageCenterService,
        ILogger<VideoRatingService> logger)
        : base(context, messageCenterService, logger)
    {
        _logger = logger;
    }

    #region Base Class Implementation

    protected override Guid GetEntityId(RateVideoModel ratingModel) => ratingModel.VideoId;

    protected override string GetEntityName() => "video";

    protected override IQueryable<RatingEntity> BuildUserRatingQuery(string userId, Guid entityId)
    {
        return Context.Ratings
            .Where(r => r.UserId == userId && r.VideoId == entityId);
    }

    protected override IQueryable<RatingEntity> BuildUserRatingByYouTubeIdQuery(string userId, string youTubeEntityId)
    {
        return Context.Ratings
            .Include(r => r.Video)
            .Where(r => r.UserId == userId && r.Video!.YouTubeVideoId == youTubeEntityId);
    }

    protected override IQueryable<RatingEntity> BuildUserRatingsQuery(string userId)
    {
        return Context.Ratings
            .Include(r => r.Video)
            .Where(r => r.UserId == userId && r.VideoId != null);
    }

    protected override IQueryable<RatingEntity> BuildSearchQuery(string userId, string? searchQuery)
    {
        var query = Context.Ratings
            .Include(r => r.Video)
            .Where(r => r.UserId == userId && r.VideoId != null);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(r =>
                EF.Functions.Like(r.Notes, $"%{searchQuery}%") ||
                EF.Functions.Like(r.Video!.Title, $"%{searchQuery}%"));
        }

        return query;
    }

    protected override VideoRatingModel MapToModel(RatingEntity entity, string? videoTitle = null, string? youTubeVideoId = null)
    {
        return new VideoRatingModel
        {
            Id = entity.Id,
            VideoId = entity.VideoId ?? Guid.Empty,
            YouTubeVideoId = youTubeVideoId ?? entity.Video?.YouTubeVideoId ?? string.Empty,
            VideoTitle = videoTitle ?? entity.Video?.Title ?? string.Empty,
            UserId = entity.UserId,
            Stars = entity.Stars,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.LastModifiedAt
        };
    }

    protected override RatingEntity CreateRatingEntity(string userId, RateVideoModel ratingModel)
    {
        return new RatingEntity
        {
            VideoId = ratingModel.VideoId,
            UserId = userId,
            Stars = ratingModel.Stars,
            Notes = ratingModel.Notes
        };
    }

    protected override void UpdateRatingEntity(RatingEntity entity, string userId, RateVideoModel ratingModel)
    {
        entity.Stars = ratingModel.Stars;
        entity.Notes = ratingModel.Notes;
    }

    protected override async Task<(bool CanRate, List<string> ErrorMessages)> ValidateCanRateAsync(string userId, Guid entityId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(userId))
        {
            errors.Add("User must be authenticated to rate videos");
        }

        if (entityId == Guid.Empty)
        {
            errors.Add("Invalid video identifier");
        }

        // Check if video exists
        if (errors.Count == 0)
        {
            var videoExists = await Context.Videos.AnyAsync(v => v.Id == entityId);
            if (!videoExists)
            {
                errors.Add("Video not found");
            }
        }

        if (errors.Count == 0)
        {
            Logger.LogInformation("Validated rating permission for user {UserId} and video {VideoId}", userId, entityId);
        }

        return (errors.Count == 0, errors);
    }

    protected override void CleanNotes(RateVideoModel ratingModel)
    {
        ratingModel.CleanNotes();
    }

    protected override int GetStars(RateVideoModel ratingModel) => ratingModel.Stars;

    protected override Task OnRatingCreatedAsync(string userId, RateVideoModel ratingModel, int stars)
    {
        // No additional operations needed for video ratings on creation
        return Task.CompletedTask;
    }

    protected override Task OnRatingUpdatedAsync(string userId, RateVideoModel ratingModel, int oldStars, int newStars)
    {
        // No additional operations needed for video ratings on update
        return Task.CompletedTask;
    }

    #endregion

    #region Public Interface Methods - Delegate to Base Class

    public Task<VideoRatingModel?> GetUserRatingAsync(string userId, Guid videoId)
        => base.GetUserRatingAsync(userId, videoId);

    public Task<VideoRatingModel?> GetUserRatingByYouTubeIdAsync(string userId, string youTubeVideoId)
        => base.GetUserRatingByYouTubeIdAsync(userId, youTubeVideoId);

    public async Task<VideoRatingModel> CreateRatingAsync(string userId, RateVideoModel ratingModel)
    {
        var result = await base.CreateRatingAsync(userId, ratingModel);
        return MapToModel(await Context.Ratings
            .Include(r => r.Video)
            .FirstAsync(r => r.Id == result.Id), ratingModel.VideoTitle, ratingModel.YouTubeVideoId);
    }

    public async Task<VideoRatingModel> UpdateRatingAsync(string userId, Guid ratingId, RateVideoModel ratingModel)
    {
        var result = await base.UpdateRatingAsync(userId, ratingId, ratingModel);
        return MapToModel(await Context.Ratings
            .Include(r => r.Video)
            .FirstAsync(r => r.Id == result.Id), ratingModel.VideoTitle, ratingModel.YouTubeVideoId);
    }

    public Task<bool> DeleteRatingAsync(string userId, Guid ratingId)
        => base.DeleteRatingAsync(userId, ratingId);

    public Task<List<VideoRatingModel>> GetUserRatingsAsync(string userId, int pageNumber = 1, int pageSize = 20)
        => base.GetUserRatingsAsync(userId, pageNumber, pageSize);

    public Task<List<VideoRatingModel>> SearchUserRatingsAsync(string userId, string searchQuery, int? minStars = null, int? maxStars = null)
        => base.SearchUserRatingsAsync(userId, searchQuery, minStars, maxStars);

    #endregion

    #region Video-Specific Methods

    /// <summary>
    /// Gets rating statistics for a user.
    /// </summary>
    public async Task<UserRatingStats> GetUserRatingStatsAsync(string userId)
    {
        try
        {
            var userRatings = await Context.Ratings
                .Where(r => r.UserId == userId && r.VideoId != null)
                .ToListAsync();

            if (!userRatings.Any())
            {
                return new UserRatingStats();
            }

            var now = DateTime.UtcNow;
            var weekAgo = now.AddDays(-7);
            var monthAgo = now.AddDays(-30);

            var stats = new UserRatingStats
            {
                TotalRatings = userRatings.Count,
                AverageStarsGiven = userRatings.Average(r => r.Stars),
                RatingDistribution = userRatings.GroupBy(r => r.Stars)
                                              .ToDictionary(g => g.Key, g => g.Count()),
                RatingsThisWeek = userRatings.Count(r => r.CreatedAt >= weekAgo),
                RatingsThisMonth = userRatings.Count(r => r.CreatedAt >= monthAgo),
                FirstRatingDate = userRatings.Min(r => r.CreatedAt),
                LastRatingDate = userRatings.Max(r => r.LastModifiedAt),
                HighRatingPercentage = (double)userRatings.Count(r => r.Stars >= 4) / userRatings.Count * 100
            };

            stats.MostCommonStarRating = stats.RatingDistribution
                .OrderByDescending(kvp => kvp.Value)
                .First().Key;

            _logger.LogInformation("Generated rating stats for user {UserId}: {Total} total, {Average:F1} avg stars",
                userId, stats.TotalRatings, stats.AverageStarsGiven);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rating stats for user {UserId}", userId);
            await MessageCenterService.ShowErrorAsync("Failed to retrieve rating statistics. Please try again.");
            return new UserRatingStats();
        }
    }

    /// <summary>
    /// Gets all ratings for a specific video (across all users).
    /// </summary>
    public async Task<List<VideoRatingModel>> GetVideoRatingsAsync(Guid videoId)
    {
        try
        {
            var ratingEntities = await Context.Ratings
                .Include(r => r.Video)
                .Where(r => r.VideoId == videoId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var ratings = ratingEntities.Select(entity => MapToModel(entity)).ToList();

            _logger.LogInformation($"Retrieved {ratings.Count()} ratings for video {videoId}");
            return ratings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ratings for video {VideoId}", videoId);
            return new List<VideoRatingModel>();
        }
    }

    /// <summary>
    /// Gets average rating and rating count for a video.
    /// </summary>
    public async Task<VideoRatingSummary> GetVideoRatingSummaryAsync(Guid videoId)
    {
        try
        {
            var videoRatings = await Context.Ratings
                .Where(r => r.VideoId == videoId)
                .ToListAsync();

            var summary = new VideoRatingSummary
            {
                VideoId = videoId,
                TotalRatings = videoRatings.Count
            };

            if (videoRatings.Any())
            {
                summary.AverageStars = videoRatings.Average(r => r.Stars);
                summary.RatingDistribution = videoRatings.GroupBy(r => r.Stars)
                                                        .ToDictionary(g => g.Key, g => g.Count());
                summary.PositiveRatingPercentage = (double)videoRatings.Count(r => r.Stars >= 4) / videoRatings.Count * 100;
                summary.LastRatingDate = videoRatings.Max(r => r.CreatedAt);
            }

            _logger.LogInformation("Generated rating summary for video {VideoId}: {Total} ratings, {Average:F1} avg stars",
                videoId, summary.TotalRatings, summary.AverageStars);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rating summary for video {VideoId}", videoId);
            return new VideoRatingSummary { VideoId = videoId };
        }
    }

    /// <summary>
    /// Validates if a user can rate a specific video.
    /// </summary>
    public async Task<RatingValidationResult> ValidateCanRateVideoAsync(string userId, Guid videoId)
    {
        try
        {
            var (canRate, errors) = await ValidateCanRateAsync(userId, videoId);
            return canRate
                ? RatingValidationResult.Success()
                : RatingValidationResult.Failure(errors.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating rating permission for user {UserId} and video {VideoId}", userId, videoId);
            return RatingValidationResult.Failure("Unable to validate rating permission", "System error occurred");
        }
    }

    /// <summary>
    /// Gets videos that are highly rated by the user (4+ stars) for recommendation purposes.
    /// </summary>
    public async Task<List<VideoRatingModel>> GetHighlyRatedVideosAsync(string userId, int limit = 10)
    {
        try
        {
            var ratingEntities = await Context.Ratings
                .Include(r => r.Video)
                .Where(r => r.UserId == userId && r.VideoId != null && r.Stars >= 4)
                .OrderByDescending(r => r.Stars)
                .ThenByDescending(r => r.LastModifiedAt)
                .Take(limit)
                .ToListAsync();

            var highlyRatedVideos = ratingEntities.Select(entity => MapToModel(entity)).ToList();

            _logger.LogInformation($"Retrieved {highlyRatedVideos.Count()} highly rated videos for user {userId}");

            return highlyRatedVideos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting highly rated videos for user {UserId}", userId);
            return new List<VideoRatingModel>();
        }
    }

    #endregion
}
