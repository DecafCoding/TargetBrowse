using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Display model for video rating information in the UI.
/// Represents a user's rating for a specific video.
/// </summary>
public class VideoRatingModel : RatingModelBase
{
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
    /// Gets the entity ID (VideoId).
    /// </summary>
    public override Guid EntityId => VideoId;

    /// <summary>
    /// Gets the YouTube entity ID (YouTubeVideoId).
    /// </summary>
    public override string YouTubeEntityId => YouTubeVideoId;

    /// <summary>
    /// Gets the entity name (VideoTitle).
    /// </summary>
    public override string EntityName => VideoTitle;
}
