using System.ComponentModel.DataAnnotations;
using TargetBrowse.Services.Validation;
using TargetBrowse.Features.Videos.Utilities;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Model for rating a video through the UI form.
/// Used for both creating new ratings and editing existing ones.
/// </summary>
public class RateVideoModel : IRatingModel
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
    [Range(RatingValidator.MinStars, RatingValidator.MaxStars, ErrorMessage = "Rating must be between 1 and 5 stars")]
    public int Stars { get; set; }

    /// <summary>
    /// User's explanatory notes for the rating.
    /// Required with minimum length validation.
    /// </summary>
    [Required(ErrorMessage = "Please provide notes explaining your rating")]
    [StringLength(RatingValidator.MaxNotesLength, MinimumLength = RatingValidator.MinNotesLength,
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
    public string StarDisplayText => RatingValidator.GetStarDisplayText(Stars);

    /// <summary>
    /// Gets CSS class for star rating display.
    /// </summary>
    public string StarCssClass => RatingValidator.GetStarCssClass(Stars);

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
        else if (!YouTubeVideoParser.IsValidVideoId(YouTubeVideoId))
            errors.Add("YouTube video ID format is invalid");

        if (string.IsNullOrWhiteSpace(VideoTitle))
            errors.Add("Video title is required");

        // Use shared rating validator for common validation logic
        errors.AddRange(RatingValidator.ValidateRating(Stars, Notes));

        return errors;
    }

    /// <summary>
    /// Indicates if the model is valid for submission.
    /// </summary>
    public bool IsValid => !Validate().Any();

    /// <summary>
    /// Trims and cleans the notes field.
    /// </summary>
    public void CleanNotes()
    {
        Notes = RatingValidator.CleanNotes(Notes);
    }

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
