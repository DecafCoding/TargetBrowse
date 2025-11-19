using System.ComponentModel.DataAnnotations;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Services.Models;

/// <summary>
/// Abstract base class for rating models, providing shared properties, validation,
/// and display logic for both channel and video ratings.
/// </summary>
public abstract class RatingModelBase
{
    /// <summary>
    /// Unique identifier for the rating.
    /// </summary>
    public Guid Id { get; set; }

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
    /// Gets the entity ID (ChannelId or VideoId) from the derived class.
    /// </summary>
    public abstract Guid EntityId { get; }

    /// <summary>
    /// Gets the YouTube entity ID (YouTubeChannelId or YouTubeVideoId) from the derived class.
    /// </summary>
    public abstract string YouTubeEntityId { get; }

    /// <summary>
    /// Gets the entity name (ChannelName or VideoTitle) from the derived class.
    /// </summary>
    public abstract string EntityName { get; }

    /// <summary>
    /// Indicates if this is a new rating (not yet saved).
    /// </summary>
    public bool IsNew => Id == Guid.Empty;

    /// <summary>
    /// Gets display text for the star rating.
    /// </summary>
    public string StarDisplayText => CssClassFormatter.GetStarRatingDisplayText(Stars);

    /// <summary>
    /// Gets CSS class for star rating display.
    /// </summary>
    public string StarCssClass => CssClassFormatter.GetStarRatingCssClass(Stars);

    /// <summary>
    /// Gets truncated notes for card display.
    /// </summary>
    public string ShortNotes => TextFormatter.TruncateSlice(Notes, 100, Notes);

    /// <summary>
    /// Gets user-friendly display of when the rating was created.
    /// </summary>
    public string CreatedAtDisplay => FormatHelper.FormatDateDisplay(CreatedAt);

    /// <summary>
    /// Gets user-friendly display of when the rating was updated.
    /// Fixed: Now uses FormatDateDisplay consistently (was using FormatUpdateDateDisplay in VideoRatingModel).
    /// </summary>
    public string UpdatedAtDisplay => FormatHelper.FormatDateDisplay(UpdatedAt);

    /// <summary>
    /// Indicates if the rating has been modified since creation.
    /// </summary>
    public bool WasModified => UpdatedAt > CreatedAt.AddMinutes(1);
}
