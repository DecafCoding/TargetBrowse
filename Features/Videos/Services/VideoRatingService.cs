using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Videos.Services;

/// <summary>
/// Database-backed implementation of video rating service.
/// Provides full rating functionality with Entity Framework Core persistence.
/// </summary>
public class VideoRatingService : IVideoRatingService
{
    private readonly ApplicationDbContext _context;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<VideoRatingService> _logger;

    public VideoRatingService(
        ApplicationDbContext context,
        IMessageCenterService messageCenterService,
        ILogger<VideoRatingService> logger)
    {
        _context = context;
        _messageCenterService = messageCenterService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a user's rating for a specific video.
    /// </summary>
    public async Task<VideoRatingModel?> GetUserRatingAsync(string userId, Guid videoId)
    {
        try
        {
            var ratingEntity = await _context.Ratings
                .FirstOrDefaultAsync(r => r.UserId == userId && r.VideoId == videoId);

            if (ratingEntity == null)
            {
                _logger.LogInformation("No rating found for user {UserId} and video {VideoId}", userId, videoId);
                return null;
            }

            var rating = MapToModel(ratingEntity);
            _logger.LogInformation("Retrieved rating {RatingId} for user {UserId} and video {VideoId}",
                rating.Id, userId, videoId);

            return rating;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user rating for user {UserId} and video {VideoId}", userId, videoId);
            await _messageCenterService.ShowErrorAsync("Failed to retrieve rating. Please try again.");
            return null;
        }
    }

    /// <summary>
    /// Gets a user's rating for a specific video by YouTube video ID.
    /// </summary>
    public async Task<VideoRatingModel?> GetUserRatingByYouTubeIdAsync(string userId, string youTubeVideoId)
    {
        try
        {
            var ratingEntity = await _context.Ratings
                .Include(r => r.Video)
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Video!.YouTubeVideoId == youTubeVideoId);

            if (ratingEntity == null)
            {
                _logger.LogInformation("No rating found for user {UserId} and YouTube video {YouTubeVideoId}",
                    userId, youTubeVideoId);
                return null;
            }

            var rating = MapToModel(ratingEntity);
            _logger.LogInformation("Retrieved rating {RatingId} for user {UserId} and YouTube video {YouTubeVideoId}",
                rating.Id, userId, youTubeVideoId);

            return rating;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user rating for user {UserId} and YouTube video {YouTubeVideoId}",
                userId, youTubeVideoId);
            await _messageCenterService.ShowErrorAsync("Failed to retrieve rating. Please try again.");
            return null;
        }
    }

    /// <summary>
    /// Creates a new rating for a video.
    /// </summary>
    public async Task<VideoRatingModel> CreateRatingAsync(string userId, RateVideoModel ratingModel)
    {
        try
        {
            // Validate the rating model
            var validationResult = await ValidateCanRateVideoAsync(userId, ratingModel.VideoId);
            if (!validationResult.CanRate)
            {
                var errorMessage = string.Join(", ", validationResult.ErrorMessages);
                await _messageCenterService.ShowErrorAsync($"Cannot create rating: {errorMessage}");
                throw new InvalidOperationException($"Cannot create rating: {errorMessage}");
            }

            // Check for existing rating
            var existingRating = await GetUserRatingAsync(userId, ratingModel.VideoId);
            if (existingRating != null)
            {
                await _messageCenterService.ShowErrorAsync("You have already rated this video. Use edit to update your rating.");
                throw new InvalidOperationException("Rating already exists for this video");
            }

            // Create new rating entity
            var ratingEntity = new RatingEntity
            {
                Id = Guid.NewGuid(),
                VideoId = ratingModel.VideoId,
                UserId = userId,
                Stars = ratingModel.Stars,
                Notes = ratingModel.Notes.Trim(),
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow,
                CreatedBy = userId,
                LastModifiedBy = userId
            };

            _context.Ratings.Add(ratingEntity);
            await _context.SaveChangesAsync();

            var newRating = MapToModel(ratingEntity, ratingModel.VideoTitle, ratingModel.YouTubeVideoId);

            _logger.LogInformation("Created new rating {RatingId} for user {UserId} and video {VideoId} with {Stars} stars",
                newRating.Id, userId, ratingModel.VideoId, ratingModel.Stars);

            await _messageCenterService.ShowSuccessAsync($"Rating saved successfully! You rated this video {ratingModel.Stars} stars.");

            return newRating;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw validation errors
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rating for user {UserId} and video {VideoId}", userId, ratingModel.VideoId);
            await _messageCenterService.ShowErrorAsync("Failed to save rating. Please try again.");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing rating.
    /// </summary>
    public async Task<VideoRatingModel> UpdateRatingAsync(string userId, Guid ratingId, RateVideoModel ratingModel)
    {
        try
        {
            var existingRating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.Id == ratingId && r.UserId == userId);

            if (existingRating == null)
            {
                await _messageCenterService.ShowErrorAsync("Rating not found or you don't have permission to edit it.");
                throw new InvalidOperationException("Rating not found or access denied");
            }

            var oldStars = existingRating.Stars;

            // Update the rating
            existingRating.Stars = ratingModel.Stars;
            existingRating.Notes = ratingModel.Notes.Trim();
            existingRating.LastModifiedAt = DateTime.UtcNow;
            existingRating.LastModifiedBy = userId;

            await _context.SaveChangesAsync();

            var updatedRating = MapToModel(existingRating, ratingModel.VideoTitle, ratingModel.YouTubeVideoId);

            _logger.LogInformation("Updated rating {RatingId} for user {UserId} from {OldStars} to {NewStars} stars",
                ratingId, userId, oldStars, ratingModel.Stars);

            await _messageCenterService.ShowSuccessAsync($"Rating updated successfully! You now rate this video {ratingModel.Stars} stars.");

            return updatedRating;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rating {RatingId} for user {UserId}", ratingId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to update rating. Please try again.");
            throw;
        }
    }

    /// <summary>
    /// Deletes a user's rating for a video.
    /// </summary>
    public async Task<bool> DeleteRatingAsync(string userId, Guid ratingId)
    {
        try
        {
            var ratingToDelete = await _context.Ratings
                .FirstOrDefaultAsync(r => r.Id == ratingId && r.UserId == userId);

            if (ratingToDelete == null)
            {
                _logger.LogWarning("Rating {RatingId} not found for user {UserId} during deletion", ratingId, userId);
                await _messageCenterService.ShowErrorAsync("Rating not found or you don't have permission to delete it.");
                return false;
            }

            _context.Ratings.Remove(ratingToDelete);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted rating {RatingId} for user {UserId}", ratingId, userId);
            await _messageCenterService.ShowSuccessAsync("Rating deleted successfully.");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rating {RatingId} for user {UserId}", ratingId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to delete rating. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Gets all ratings by a specific user.
    /// </summary>
    public async Task<List<VideoRatingModel>> GetUserRatingsAsync(string userId, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            var ratingEntities = await _context.Ratings
                .Include(r => r.Video)
                .Where(r => r.UserId == userId && r.VideoId != null)
                .OrderByDescending(r => r.LastModifiedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var ratings = ratingEntities.Select(entity => MapToModel(entity)).ToList();

            _logger.LogInformation($"Retrieved {ratings.Count()} ratings for user {userId} (page {pageNumber}, size {pageSize})");

            return ratings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user ratings for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to retrieve your ratings. Please try again.");
            return new List<VideoRatingModel>();
        }
    }

    /// <summary>
    /// Gets rating statistics for a user.
    /// </summary>
    public async Task<UserRatingStats> GetUserRatingStatsAsync(string userId)
    {
        try
        {
            var userRatings = await _context.Ratings
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
            await _messageCenterService.ShowErrorAsync("Failed to retrieve rating statistics. Please try again.");
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
            var ratingEntities = await _context.Ratings
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
            var videoRatings = await _context.Ratings
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
    /// Searches user's ratings by notes content.
    /// </summary>
    public async Task<List<VideoRatingModel>> SearchUserRatingsAsync(string userId, string searchQuery, int? minStars = null, int? maxStars = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchQuery) && minStars == null && maxStars == null)
            {
                return await GetUserRatingsAsync(userId);
            }

            var query = _context.Ratings
                .Include(r => r.Video)
                .Where(r => r.UserId == userId && r.VideoId != null);

            // Filter by search query in notes or video title
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(r =>
                    EF.Functions.Like(r.Notes, $"%{searchQuery}%") ||
                    EF.Functions.Like(r.Video!.Title, $"%{searchQuery}%"));
            }

            // Filter by star rating range
            if (minStars.HasValue)
            {
                query = query.Where(r => r.Stars >= minStars.Value);
            }

            if (maxStars.HasValue)
            {
                query = query.Where(r => r.Stars <= maxStars.Value);
            }

            var ratingEntities = await query.OrderByDescending(r => r.LastModifiedAt).ToListAsync();
            var results = ratingEntities.Select(entity => MapToModel(entity)).ToList();

            _logger.LogInformation($"Search returned {results.Count()} ratings for user {userId} with query '{searchQuery}' and stars {minStars}-{maxStars}");

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching ratings for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to search ratings. Please try again.");
            return new List<VideoRatingModel>();
        }
    }

    /// <summary>
    /// Validates if a user can rate a specific video.
    /// </summary>
    public async Task<RatingValidationResult> ValidateCanRateVideoAsync(string userId, Guid videoId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RatingValidationResult.Failure("User must be authenticated to rate videos");
            }

            if (videoId == Guid.Empty)
            {
                return RatingValidationResult.Failure("Invalid video identifier");
            }

            // Check if video exists
            var videoExists = await _context.Videos.AnyAsync(v => v.Id == videoId);
            if (!videoExists)
            {
                return RatingValidationResult.Failure("Video not found");
            }

            _logger.LogInformation("Validated rating permission for user {UserId} and video {VideoId}", userId, videoId);
            return RatingValidationResult.Success();
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
            var ratingEntities = await _context.Ratings
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

    /// <summary>
    /// Maps a RatingEntity to a VideoRatingModel.
    /// </summary>
    private static VideoRatingModel MapToModel(RatingEntity entity, string? videoTitle = null, string? youTubeVideoId = null)
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
}