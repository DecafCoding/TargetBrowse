namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Represents a request to check for channel updates.
/// Used for bulk channel update operations.
/// </summary>
public class ChannelUpdateRequest
{
    /// <summary>
    /// YouTube channel identifier.
    /// </summary>
    public string YouTubeChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Channel display name for logging and error messages.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Last time this channel was checked for updates.
    /// </summary>
    public DateTime LastCheckDate { get; set; }

    /// <summary>
    /// User's rating for this channel (affects filtering).
    /// </summary>
    public int? UserRating { get; set; }

    /// <summary>
    /// Maximum number of results to retrieve for this specific channel.
    /// </summary>
    public int MaxResults { get; set; } = 100;
}
