using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Display model for video information in the UI.
/// Provides formatted data and user-friendly display methods.
/// Handles missing data gracefully for search results vs detailed views.
/// Extended with rating information for the rating system.
/// </summary>
public class VideoDisplayModel
{
    /// <summary>
    /// Unique identifier for the video in our system.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// YouTube video ID (11-character identifier).
    /// </summary>
    public string YouTubeVideoId { get; set; } = string.Empty;

    /// <summary>
    /// Video title as displayed on YouTube.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Video description (truncated for display).
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// URL to video thumbnail image.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Video duration in ISO 8601 format (PT4M13S).
    /// May be null for search results - use detailed API call to get full info.
    /// </summary>
    public string? Duration { get; set; }

    /// <summary>
    /// Number of views on the video.
    /// May be null for search results - use detailed API call to get full info.
    /// </summary>
    public ulong? ViewCount { get; set; }

    /// <summary>
    /// Number of likes on the video.
    /// May be null for search results - use detailed API call to get full info.
    /// </summary>
    public ulong? LikeCount { get; set; }

    /// <summary>
    /// Number of comments on the video.
    /// May be null for search results - use detailed API call to get full info.
    /// </summary>
    public ulong? CommentCount { get; set; }

    /// <summary>
    /// Video tags/keywords.
    /// Empty for search results - use detailed API call to get full info.
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Video category ID.
    /// May be null for search results.
    /// </summary>
    public string? CategoryId { get; set; }

    /// <summary>
    /// Video's default language.
    /// May be null for search results.
    /// </summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>
    /// When the video was published on YouTube.
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Channel ID that published this video.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Channel name that published this video.
    /// </summary>
    public string ChannelTitle { get; set; } = string.Empty;

    /// <summary>
    /// When the user added this video to their library.
    /// </summary>
    public DateTime? AddedToLibrary { get; set; }

    /// <summary>
    /// Whether this video is in the user's library.
    /// </summary>
    public bool IsInLibrary { get; set; }

    /// <summary>
    /// ID of the UserVideo entity (the library relationship record).
    /// Only populated when the video is in the user's library.
    /// </summary>
    public Guid? UserVideoId { get; set; }

    /// <summary>
    /// User's watch status for this video.
    /// </summary>
    public WatchStatus WatchStatus { get; set; } = WatchStatus.NotWatched;

    // ===== VIDEO TYPE PROPERTIES =====

    /// <summary>
    /// Video type ID for content classification.
    /// Null if no type has been assigned.
    /// </summary>
    public Guid? VideoTypeId { get; set; }

    /// <summary>
    /// Video type name (e.g., "Tutorial", "Podcast", "Vlog").
    /// Null if no type has been assigned.
    /// </summary>
    public string? VideoTypeName { get; set; }

    /// <summary>
    /// Video type code (e.g., "TUTORIAL", "PODCAST", "VLOG").
    /// Null if no type has been assigned.
    /// </summary>
    public string? VideoTypeCode { get; set; }

    /// <summary>
    /// Display text for video type with fallback for unclassified videos.
    /// </summary>
    public string VideoTypeDisplay => VideoTypeName ?? "Unclassified";

    // ===== RATING SYSTEM PROPERTIES =====

    /// <summary>
    /// User's rating for this video. Null if not rated.
    /// </summary>
    public VideoRatingModel? UserRating { get; set; }

    /// <summary>
    /// Whether the current user has rated this video.
    /// </summary>
    public bool IsRatedByUser => UserRating != null;

    /// <summary>
    /// User's star rating (1-5) if rated, 0 if not rated.
    /// </summary>
    public int UserStars => UserRating?.Stars ?? 0;

    /// <summary>
    /// Summary of all ratings for this video (across all users).
    /// </summary>
    public VideoRatingSummary? RatingSummary { get; set; }

    /// <summary>
    /// Whether this video has any ratings from any users.
    /// </summary>
    public bool HasRatings => RatingSummary?.TotalRatings > 0;

    /// <summary>
    /// Average rating across all users.
    /// </summary>
    public double AverageRating => RatingSummary?.AverageStars ?? 0;

    /// <summary>
    /// Total number of ratings from all users.
    /// </summary>
    public int TotalRatings => RatingSummary?.TotalRatings ?? 0;

    // ===== EXISTING PROPERTIES CONTINUE =====

    /// <summary>
    /// Indicates whether this model contains detailed information (from videos API)
    /// or basic information only (from search API).
    /// </summary>
    public bool HasDetailedInfo => Duration != null || ViewCount.HasValue || Tags.Any();

    /// <summary>
    /// Formatted view count for display (e.g., "1.2M", "45.3K").
    /// Returns empty string if view count is null.
    /// </summary>
    public string ViewCountDisplay => FormatHelper.FormatCount(ViewCount);

    /// <summary>
    /// Formatted like count for display (e.g., "1.2K", "45").
    /// Returns empty string if like count is null.
    /// </summary>
    public string LikeCountDisplay => FormatHelper.FormatCount(LikeCount);

    /// <summary>
    /// Formatted comment count for display (e.g., "1.2K", "45").
    /// Returns empty string if comment count is null.
    /// </summary>
    public string CommentCountDisplay => FormatHelper.FormatCount(CommentCount);

    /// <summary>
    /// Formatted duration for display (e.g., "4:13", "1:02:45").
    /// Returns empty string if duration is null.
    /// </summary>
    public string DurationDisplay => FormatHelper.FormatDuration(Duration);

    /// <summary>
    /// User-friendly display of when added to library.
    /// </summary>
    public string AddedToLibraryDisplay => FormatHelper.FormatDateDisplay(AddedToLibrary);

    /// <summary>
    /// Truncated description for card display.
    /// </summary>
    public string ShortDescription => TextFormatter.Truncate(Description, 100);

    /// <summary>
    /// Truncated title for compact display.
    /// </summary>
    public string ShortTitle => TextFormatter.Truncate(Title, 60, Title);

    /// <summary>
    /// Gets the YouTube video URL.
    /// </summary>
    public string YouTubeUrl => $"https://www.youtube.com/watch?v={YouTubeVideoId}";

    /// <summary>
    /// Gets the YouTube channel URL for this video's channel.
    /// </summary>
    public string ChannelUrl => $"https://www.youtube.com/channel/{ChannelId}";

    /// <summary>
    /// User-friendly display of publication date.
    /// </summary>
    public string PublishedDisplay => FormatHelper.FormatDateDisplay(PublishedAt);

    // ===== NEW RATING DISPLAY PROPERTIES =====

    /// <summary>
    /// Display text for user's rating.
    /// </summary>
    public string UserRatingDisplay => UserRating?.StarDisplayText ?? "Not rated";

    /// <summary>
    /// CSS class for user's rating display.
    /// </summary>
    public string UserRatingCssClass => UserRating?.StarCssClass ?? "text-muted";

    /// <summary>
    /// Short version of user's rating notes for card display.
    /// </summary>
    public string UserRatingNotesShort => UserRating?.ShortNotes ?? string.Empty;

    /// <summary>
    /// Display text for average rating.
    /// </summary>
    public string AverageRatingDisplay => HasRatings ? $"{AverageRating:F1} stars" : "No ratings";

    /// <summary>
    /// Display text for rating count.
    /// </summary>
    public string RatingCountDisplay => TotalRatings switch
    {
        0 => "No ratings",
        1 => "1 rating",
        _ => $"{TotalRatings} ratings"
    };

    /// <summary>
    /// Combined rating summary for compact display.
    /// </summary>
    public string RatingSummaryDisplay => HasRatings
        ? $"{AverageRating:F1} stars ({TotalRatings} {(TotalRatings == 1 ? "rating" : "ratings")})"
        : "No ratings yet";

    /// <summary>
    /// CSS class for average rating display.
    /// </summary>
    public string AverageRatingCssClass => RatingSummary?.AverageStarsCssClass ?? "text-muted";

    /// <summary>
    /// Whether the video can be rated by the user.
    /// Currently requires the video to be in the user's library.
    /// </summary>
    public bool CanBeRated => IsInLibrary;

    /// <summary>
    /// Tooltip text explaining why the video can't be rated (if applicable).
    /// </summary>
    public string CannotRateReason => !IsInLibrary
        ? "Add video to your library to rate it"
        : string.Empty;

    /// <summary>
    /// Creates a RateVideoModel from this video for rating purposes.
    /// </summary>
    /// <returns>RateVideoModel for this video</returns>
    public RateVideoModel CreateRatingModel()
    {
        return new RateVideoModel
        {
            VideoId = Id,
            YouTubeVideoId = YouTubeVideoId,
            VideoTitle = Title,
            VideoThumbnailUrl = ThumbnailUrl,
            ChannelTitle = ChannelTitle,
            VideoTypeId = VideoTypeId,
            VideoTypeName = VideoTypeName,
            Stars = UserStars,
            Notes = UserRating?.Notes ?? string.Empty,
            RatingId = UserRating?.Id
        };
    }

    /// <summary>
    /// Updates this model with rating information.
    /// </summary>
    /// <param name="userRating">User's rating for this video</param>
    /// <param name="ratingSummary">Summary of all ratings for this video</param>
    public void UpdateWithRatingInfo(VideoRatingModel? userRating, VideoRatingSummary? ratingSummary)
    {
        UserRating = userRating;
        RatingSummary = ratingSummary;
    }
}
