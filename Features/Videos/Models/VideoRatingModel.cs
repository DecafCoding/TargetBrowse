using System.ComponentModel.DataAnnotations;

using TargetBrowse.Services;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Display model for video rating information in the UI.
/// Represents a user's rating for a specific video.
/// </summary>
public class VideoRatingModel
{
    /// <summary>
    /// Unique identifier for the rating.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the video being rated.
    /// </summary>
    public Guid VideoId { get; set; }

    /// <summary>
    /// YouTube video ID for reference.
    /// </summary>
    public string YouTubeVideoId { get; set; } = string.Empty;

    /// <summary>
    /// Video title for display purposes.
    /// </summary>
    public string VideoTitle { get; set; } = string.Empty;

    /// <summary>
    /// User ID who created the rating.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Star rating from 1 to 5.
    /// </summary>
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars")]
    public int Stars { get; set; }

    /// <summary>
    /// User's explanatory notes for the rating.
    /// </summary>
    [Required(ErrorMessage = "Notes are required")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Notes must be between 10 and 1000 characters")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// When the rating was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the rating was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Indicates if this is a new rating (not yet saved).
    /// </summary>
    public bool IsNew => Id == Guid.Empty;

    /// <summary>
    /// Gets display text for the star rating.
    /// </summary>
    public string StarDisplayText => Stars switch
    {
        1 => "1 star - Poor",
        2 => "2 stars - Fair",
        3 => "3 stars - Good",
        4 => "4 stars - Very Good",
        5 => "5 stars - Excellent",
        _ => "No rating"
    };

    /// <summary>
    /// Gets CSS class for star rating display.
    /// </summary>
    public string StarCssClass => Stars switch
    {
        1 => "text-danger",
        2 => "text-warning",
        3 => "text-info",
        4 => "text-success",
        5 => "text-success",
        _ => "text-muted"
    };

    /// <summary>
    /// Gets truncated notes for card display.
    /// </summary>
    public string ShortNotes => Notes.Length > 100 ? $"{Notes[..97]}..." : Notes;

    /// <summary>
    /// Gets user-friendly display of when the rating was created.
    /// </summary>
    public string CreatedAtDisplay => FormatHelper.FormatDateDisplay(CreatedAt);

    /// <summary>
    /// Gets user-friendly display of when the rating was updated.
    /// </summary>
    public string UpdatedAtDisplay => FormatHelper.FormatUpdateDateDisplay(UpdatedAt);

    /// <summary>
    /// Indicates if the rating has been modified since creation.
    /// </summary>
    public bool WasModified => UpdatedAt > CreatedAt.AddMinutes(1);

}