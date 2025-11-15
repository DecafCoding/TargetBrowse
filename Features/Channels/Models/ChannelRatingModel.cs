using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Channels.Models;

/// <summary>
/// Display model for channel rating information in the UI.
/// Represents a user's rating for a specific channel.
/// </summary>
public class ChannelRatingModel : RatingModelBase
{
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
    /// Gets the entity ID (ChannelId).
    /// </summary>
    public override Guid EntityId => ChannelId;

    /// <summary>
    /// Gets the YouTube entity ID (YouTubeChannelId).
    /// </summary>
    public override string YouTubeEntityId => YouTubeChannelId;

    /// <summary>
    /// Gets the entity name (ChannelName).
    /// </summary>
    public override string EntityName => ChannelName;

    /// <summary>
    /// Indicates if this is a low rating that should exclude channel from suggestions.
    /// </summary>
    public bool IsLowRating => Stars == 1;
}
