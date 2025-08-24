using System.ComponentModel.DataAnnotations;
using TargetBrowse.Features.Channels.Components;

namespace TargetBrowse.Features.Channels.Models;

/// <summary>
/// Input model for creating or updating channel ratings.
/// Used in forms and API endpoints for rating operations.
/// </summary>
public class RateChannelModel
{
    /// <summary>
    /// ID of the channel being rated.
    /// </summary>
    [Required(ErrorMessage = "Channel ID is required")]
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
    /// Star rating from 1 to 5.
    /// </summary>
    [Required(ErrorMessage = "Star rating is required")]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars")]
    public int Stars { get; set; }

    /// <summary>
    /// User's explanatory notes for the rating.
    /// Required with minimum length to ensure meaningful feedback.
    /// </summary>
    [Required(ErrorMessage = "Notes are required to explain your rating")]
    [StringLength(1000, MinimumLength = 10,
        ErrorMessage = "Notes must be between 10 and 1000 characters. Please explain why you gave this rating.")]
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this is an existing rating being updated.
    /// </summary>
    public bool IsUpdate { get; set; } = false;

    /// <summary>
    /// ID of existing rating if updating.
    /// </summary>
    public Guid? ExistingRatingId { get; set; }

    /// <summary>
    /// Gets validation summary for display.
    /// </summary>
    public List<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (ChannelId == Guid.Empty)
            errors.Add("Valid channel is required");

        if (Stars < 1 || Stars > 5)
            errors.Add("Rating must be between 1 and 5 stars");

        if (string.IsNullOrWhiteSpace(Notes))
            errors.Add("Notes are required");
        else if (Notes.Trim().Length < 10)
            errors.Add("Notes must be at least 10 characters long");
        else if (Notes.Length > 1000)
            errors.Add("Notes must be less than 1000 characters");

        return errors;
    }

    /// <summary>
    /// Validates the model and returns true if valid.
    /// </summary>
    public bool IsValid => !GetValidationErrors().Any();

    /// <summary>
    /// Gets display text for the selected star rating.
    /// </summary>
    public string StarDisplayText => Stars switch
    {
        1 => "1 star - Poor quality content",
        2 => "2 stars - Below average content",
        3 => "3 stars - Average content",
        4 => "4 stars - Good quality content",
        5 => "5 stars - Excellent content",
        _ => "Select a rating"
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
    /// Creates a new rating model for a channel.
    /// </summary>
    public static RateChannelModel CreateNew(Guid channelId, string youTubeChannelId, string channelName)
    {
        return new RateChannelModel
        {
            ChannelId = channelId,
            YouTubeChannelId = youTubeChannelId,
            ChannelName = channelName,
            Stars = 0,
            Notes = string.Empty,
            IsUpdate = false,
            ExistingRatingId = null
        };
    }

    /// <summary>
    /// Creates an update model from an existing rating.
    /// </summary>
    public static RateChannelModel CreateUpdate(ChannelRatingModel existingRating)
    {
        return new RateChannelModel
        {
            ChannelId = existingRating.ChannelId,
            YouTubeChannelId = existingRating.YouTubeChannelId,
            ChannelName = existingRating.ChannelName,
            Stars = existingRating.Stars,
            Notes = existingRating.Notes,
            IsUpdate = true,
            ExistingRatingId = existingRating.Id
        };
    }

    /// <summary>
    /// Trims and cleans the notes field.
    /// </summary>
    public void CleanNotes()
    {
        Notes = Notes?.Trim() ?? string.Empty;
    }
}