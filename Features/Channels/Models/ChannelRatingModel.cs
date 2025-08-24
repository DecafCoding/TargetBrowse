using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Channels.Models;

/// <summary>
/// Display model for channel rating information in the UI.
/// Represents a user's rating for a specific channel.
/// </summary>
public class ChannelRatingModel
{
    /// <summary>
    /// Unique identifier for the rating.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID of the channel being rated.
    /// </summary>
    public Guid ChannelId { get; set; }

    /// <summary>
    /// YouTube channel ID for reference.
    /// </summary>
    public string YouTubeChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Channel name for display purposes.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

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
    public string CreatedAtDisplay => FormatCreatedDate();

    /// <summary>
    /// Gets user-friendly display of when the rating was updated.
    /// </summary>
    public string UpdatedAtDisplay => FormatUpdatedDate();

    /// <summary>
    /// Indicates if the rating has been modified since creation.
    /// </summary>
    public bool WasModified => UpdatedAt > CreatedAt.AddMinutes(1);

    /// <summary>
    /// Indicates if this is a low rating that should exclude channel from suggestions.
    /// </summary>
    public bool IsLowRating => Stars == 1;

    /// <summary>
    /// Formats the creation date for user-friendly display.
    /// </summary>
    private string FormatCreatedDate()
    {
        var now = DateTime.UtcNow;
        var timeSpan = now - CreatedAt;

        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => "Just now",
            < 1 when timeSpan.TotalHours < 24 => $"{(int)timeSpan.TotalHours}h ago",
            < 7 => $"{(int)timeSpan.TotalDays}d ago",
            < 30 => $"{(int)(timeSpan.TotalDays / 7)}w ago",
            _ => CreatedAt.ToString("MMM d, yyyy")
        };
    }

    /// <summary>
    /// Formats the update date for user-friendly display.
    /// </summary>
    private string FormatUpdatedDate()
    {
        if (!WasModified)
            return string.Empty;

        var now = DateTime.UtcNow;
        var timeSpan = now - UpdatedAt;

        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => "Updated just now",
            < 1 when timeSpan.TotalHours < 24 => $"Updated {(int)timeSpan.TotalHours}h ago",
            < 7 => $"Updated {(int)timeSpan.TotalDays}d ago",
            < 30 => $"Updated {(int)(timeSpan.TotalDays / 7)}w ago",
            _ => $"Updated {UpdatedAt:MMM d, yyyy}"
        };
    }
}