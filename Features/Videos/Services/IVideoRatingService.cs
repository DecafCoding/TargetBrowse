using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Services.Validation;

namespace TargetBrowse.Features.Videos.Services;

/// <summary>
/// Service interface for video rating operations.
/// Handles CRUD operations for video ratings with business logic validation.
/// This is a placeholder interface for future API integration.
/// </summary>
public interface IVideoRatingService
{
    /// <summary>
    /// Gets a user's rating for a specific video.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">Video identifier</param>
    /// <returns>User's rating or null if not rated</returns>
    Task<VideoRatingModel?> GetUserRatingAsync(string userId, Guid videoId);

    /// <summary>
    /// Gets a user's rating for a specific video by YouTube video ID.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <returns>User's rating or null if not rated</returns>
    Task<VideoRatingModel?> GetUserRatingByYouTubeIdAsync(string userId, string youTubeVideoId);

    /// <summary>
    /// Creates a new rating for a video.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="ratingModel">Rating information</param>
    /// <returns>Created rating model</returns>
    Task<VideoRatingModel> CreateRatingAsync(string userId, RateVideoModel ratingModel);

    /// <summary>
    /// Updates an existing rating.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="ratingId">Rating identifier</param>
    /// <param name="ratingModel">Updated rating information</param>
    /// <returns>Updated rating model</returns>
    Task<VideoRatingModel> UpdateRatingAsync(string userId, Guid ratingId, RateVideoModel ratingModel);

    /// <summary>
    /// Deletes a user's rating for a video.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="ratingId">Rating identifier</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteRatingAsync(string userId, Guid ratingId);

    /// <summary>
    /// Gets all ratings by a specific user.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="pageNumber">Page number for pagination</param>
    /// <param name="pageSize">Number of ratings per page</param>
    /// <returns>List of user's ratings</returns>
    Task<List<VideoRatingModel>> GetUserRatingsAsync(string userId, int pageNumber = 1, int pageSize = 20);

    /// <summary>
    /// Gets rating statistics for a user.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>User's rating statistics</returns>
    Task<UserRatingStats> GetUserRatingStatsAsync(string userId);

    /// <summary>
    /// Gets all ratings for a specific video (across all users).
    /// Used for displaying video popularity and average ratings.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="videoId">Video identifier</param>
    /// <returns>List of ratings for the video</returns>
    Task<List<VideoRatingModel>> GetVideoRatingsAsync(Guid videoId);

    /// <summary>
    /// Gets average rating and rating count for a video.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="videoId">Video identifier</param>
    /// <returns>Average rating information</returns>
    Task<VideoRatingSummary> GetVideoRatingSummaryAsync(Guid videoId);

    /// <summary>
    /// Searches user's ratings by notes content.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="searchQuery">Search term</param>
    /// <param name="minStars">Minimum star rating filter</param>
    /// <param name="maxStars">Maximum star rating filter</param>
    /// <returns>List of matching ratings</returns>
    Task<List<VideoRatingModel>> SearchUserRatingsAsync(string userId, string searchQuery, int? minStars = null, int? maxStars = null);

    /// <summary>
    /// Validates if a user can rate a specific video.
    /// Checks business rules like video must be in library, etc.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">Video identifier</param>
    /// <returns>Validation result with any error messages</returns>
    Task<RatingValidationResult> ValidateCanRateVideoAsync(string userId, Guid videoId);

    /// <summary>
    /// Gets videos that are highly rated by the user (4+ stars) for recommendation purposes.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of highly rated videos</returns>
    Task<List<VideoRatingModel>> GetHighlyRatedVideosAsync(string userId, int limit = 10);
}

/// <summary>
/// Statistics about a user's rating activity.
/// </summary>
public class UserRatingStats
{
    /// <summary>
    /// Total number of videos rated by the user.
    /// </summary>
    public int TotalRatings { get; set; }

    /// <summary>
    /// Average star rating given by the user.
    /// </summary>
    public double AverageStarsGiven { get; set; }

    /// <summary>
    /// Distribution of ratings by star count.
    /// </summary>
    public Dictionary<int, int> RatingDistribution { get; set; } = new();

    /// <summary>
    /// Number of ratings created this week.
    /// </summary>
    public int RatingsThisWeek { get; set; }

    /// <summary>
    /// Number of ratings created this month.
    /// </summary>
    public int RatingsThisMonth { get; set; }

    /// <summary>
    /// Date of the user's first rating.
    /// </summary>
    public DateTime? FirstRatingDate { get; set; }

    /// <summary>
    /// Date of the user's most recent rating.
    /// </summary>
    public DateTime? LastRatingDate { get; set; }

    /// <summary>
    /// Most frequently given star rating.
    /// </summary>
    public int MostCommonStarRating { get; set; }

    /// <summary>
    /// Percentage of videos that are highly rated (4+ stars).
    /// </summary>
    public double HighRatingPercentage { get; set; }
}

/// <summary>
/// Summary of ratings for a specific video.
/// </summary>
public class VideoRatingSummary
{
    /// <summary>
    /// Video identifier.
    /// </summary>
    public Guid VideoId { get; set; }

    /// <summary>
    /// Total number of ratings for the video.
    /// </summary>
    public int TotalRatings { get; set; }

    /// <summary>
    /// Average star rating for the video.
    /// </summary>
    public double AverageStars { get; set; }

    /// <summary>
    /// Distribution of ratings by star count.
    /// </summary>
    public Dictionary<int, int> RatingDistribution { get; set; } = new();

    /// <summary>
    /// Percentage of ratings that are positive (4+ stars).
    /// </summary>
    public double PositiveRatingPercentage { get; set; }

    /// <summary>
    /// Date of the most recent rating.
    /// </summary>
    public DateTime? LastRatingDate { get; set; }

    /// <summary>
    /// Formatted average rating for display.
    /// </summary>
    public string AverageStarsDisplay => AverageStars > 0 ? $"{AverageStars:F1}" : "No ratings";

    /// <summary>
    /// CSS class for average rating display based on value.
    /// </summary>
    public string AverageStarsCssClass => AverageStars switch
    {
        >= 4.0 => "text-success",
        >= 3.0 => "text-info",
        >= 2.0 => "text-warning",
        > 0 => "text-danger",
        _ => "text-muted"
    };
}