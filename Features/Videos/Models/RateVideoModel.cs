using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Model for rating a video through the UI form.
/// Used for both creating new ratings and editing existing ones.
/// </summary>
public class RateVideoModel
{
    /// <summary>
    /// Rating ID for editing existing ratings. Null for new ratings.
    /// </summary>
    public Guid? RatingId { get; set; }

    /// <summary>
    /// ID of the video being rated.
    /// </summary>
    [Required]
    public Guid VideoId { get; set; }

    /// <summary>
    /// YouTube video ID for reference and validation.
    /// </summary>
    [Required]
    public string YouTubeVideoId { get; set; } = string.Empty;

    /// <summary>
    /// Video title for display in the rating form.
    /// </summary>
    [Required]
    public string VideoTitle { get; set; } = string.Empty;

    /// <summary>
    /// Video thumbnail URL for display in the rating form.
    /// </summary>
    public string? VideoThumbnailUrl { get; set; }

    /// <summary>
    /// Channel title for context in the rating form.
    /// </summary>
    public string ChannelTitle { get; set; } = string.Empty;

    /// <summary>
    /// Video type ID for content classification.
    /// Null if no type has been assigned.
    /// </summary>
    public Guid? VideoTypeId { get; set; }

    /// <summary>
    /// Video type name for display in the rating form.
    /// Null if no type has been assigned.
    /// </summary>
    public string? VideoTypeName { get; set; }

    /// <summary>
    /// Display text for video type with fallback for unclassified videos.
    /// </summary>
    public string VideoTypeDisplay => VideoTypeName ?? "Unclassified";

    /// <summary>
    /// Star rating from 1 to 5.
    /// </summary>
    [Required(ErrorMessage = "Please select a star rating")]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars")]
    public int Stars { get; set; }

    /// <summary>
    /// User's explanatory notes for the rating.
    /// Required with minimum length validation.
    /// </summary>
    [Required(ErrorMessage = "Please provide notes explaining your rating")]
    [StringLength(1000, MinimumLength = 10,
        ErrorMessage = "Notes must be between 10 and 1000 characters")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this is editing an existing rating.
    /// </summary>
    public bool IsEditing => RatingId.HasValue && RatingId != Guid.Empty;

    /// <summary>
    /// Gets the current character count for notes validation display.
    /// </summary>
    public int NotesCharacterCount => Notes?.Length ?? 0;

    /// <summary>
    /// Gets the remaining characters allowed for notes.
    /// </summary>
    public int NotesCharactersRemaining => 1000 - NotesCharacterCount;

    /// <summary>
    /// Indicates if the minimum character requirement is met.
    /// </summary>
    public bool NotesMinimumMet => NotesCharacterCount >= 10;

    /// <summary>
    /// Gets CSS class for character count display based on validation state.
    /// </summary>
    public string NotesCharacterCountCssClass => NotesCharacterCount switch
    {
        < 10 => "text-danger",
        >= 10 and < 900 => "text-muted",
        >= 900 and < 1000 => "text-warning",
        _ => "text-danger"
    };

    /// <summary>
    /// Gets display text for the selected star rating.
    /// </summary>
    public string StarDisplayText => Stars switch
    {
        1 => "1 star - Poor",
        2 => "2 stars - Fair",
        3 => "3 stars - Good",
        4 => "4 stars - Very Good",
        5 => "5 stars - Excellent",
        _ => "Select a rating"
    };

    /// <summary>
    /// Validates the model and returns validation errors.
    /// </summary>
    /// <returns>List of validation error messages</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (VideoId == Guid.Empty)
            errors.Add("Video ID is required");

        if (string.IsNullOrWhiteSpace(YouTubeVideoId))
            errors.Add("YouTube video ID is required");

        if (string.IsNullOrWhiteSpace(VideoTitle))
            errors.Add("Video title is required");

        if (Stars < 1 || Stars > 5)
            errors.Add("Rating must be between 1 and 5 stars");

        if (string.IsNullOrWhiteSpace(Notes))
            errors.Add("Notes are required");
        else if (Notes.Length < 10)
            errors.Add("Notes must be at least 10 characters");
        else if (Notes.Length > 1000)
            errors.Add("Notes cannot exceed 1000 characters");

        return errors;
    }

    /// <summary>
    /// Indicates if the model is valid for submission.
    /// </summary>
    public bool IsValid => !Validate().Any();

    /// <summary>
    /// Creates a new RateVideoModel from a VideoDisplayModel.
    /// </summary>
    /// <param name="video">Video to create rating model for</param>
    /// <returns>New RateVideoModel</returns>
    public static RateVideoModel FromVideo(VideoDisplayModel video)
    {
        return new RateVideoModel
        {
            VideoId = video.Id,
            YouTubeVideoId = video.YouTubeVideoId,
            VideoTitle = video.Title,
            VideoThumbnailUrl = video.ThumbnailUrl,
            ChannelTitle = video.ChannelTitle,
            VideoTypeId = video.VideoTypeId,
            VideoTypeName = video.VideoTypeName,
            Stars = 0,
            Notes = string.Empty
        };
    }

    /// <summary>
    /// Creates a RateVideoModel from an existing rating for editing.
    /// </summary>
    /// <param name="video">Video being rated</param>
    /// <param name="rating">Existing rating to edit</param>
    /// <returns>RateVideoModel populated with existing rating data</returns>
    public static RateVideoModel FromExistingRating(VideoDisplayModel video, VideoRatingModel rating)
    {
        return new RateVideoModel
        {
            RatingId = rating.Id,
            VideoId = video.Id,
            YouTubeVideoId = video.YouTubeVideoId,
            VideoTitle = video.Title,
            VideoThumbnailUrl = video.ThumbnailUrl,
            ChannelTitle = video.ChannelTitle,
            VideoTypeId = video.VideoTypeId,
            VideoTypeName = video.VideoTypeName,
            Stars = rating.Stars,
            Notes = rating.Notes
        };
    }
}
