using System.ComponentModel.DataAnnotations;
using TargetBrowse.Features.Channels.Components;
using TargetBrowse.Features.Channels.Utilities;
using TargetBrowse.Services.Validation;

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
    [Range(RatingValidator.MinStars, RatingValidator.MaxStars, ErrorMessage = "Rating must be between 1 and 5 stars")]
    public int Stars { get; set; }

    /// <summary>
    /// User's explanatory notes for the rating.
    /// Required with minimum length to ensure meaningful feedback.
    /// </summary>
    [Required(ErrorMessage = "Notes are required to explain your rating")]
    [StringLength(RatingValidator.MaxNotesLength, MinimumLength = RatingValidator.MinNotesLength,
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
    /// Validates the model and returns validation errors.
    /// </summary>
    /// <returns>List of validation error messages</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();

        if (ChannelId == Guid.Empty)
            errors.Add("Valid channel is required");

        if (!string.IsNullOrWhiteSpace(YouTubeChannelId) && !YouTubeUrlParser.IsValidChannelId(YouTubeChannelId))
            errors.Add("YouTube channel ID format is invalid");

        // Use shared rating validator for common validation logic
        errors.AddRange(RatingValidator.ValidateRating(Stars, Notes));

        return errors;
    }

    /// <summary>
    /// Indicates if the model is valid for submission.
    /// </summary>
    public bool IsValid => !Validate().Any();

    /// <summary>
    /// Gets display text for the selected star rating.
    /// </summary>
    public string StarDisplayText => RatingValidator.GetStarDisplayText(Stars);

    /// <summary>
    /// Gets CSS class for star rating display.
    /// </summary>
    public string StarCssClass => RatingValidator.GetStarCssClass(Stars);

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