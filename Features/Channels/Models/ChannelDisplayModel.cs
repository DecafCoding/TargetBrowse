using TargetBrowse.Services;

namespace TargetBrowse.Features.Channels.Models;

/// <summary>
/// Display model for channel information in the UI.
/// Provides formatted data and user-friendly display methods.
/// </summary>
public class ChannelDisplayModel
{
    /// <summary>
    /// Unique identifier for the channel in our system.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// YouTube channel ID (e.g., UCxxxxx).
    /// </summary>
    public string YouTubeChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Channel name as displayed on YouTube.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Channel description (truncated for display).
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// URL to channel thumbnail image.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Number of subscribers (raw count).
    /// </summary>
    public ulong? SubscriberCount { get; set; }

    /// <summary>
    /// Number of videos on the channel.
    /// </summary>
    public ulong? VideoCount { get; set; }

    /// <summary>
    /// When the channel was created on YouTube.
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// When the user started tracking this channel (for tracked channels).
    /// </summary>
    public DateTime? TrackedSince { get; set; }

    /// <summary>
    /// Whether this channel is currently being tracked by the user.
    /// </summary>
    public bool IsTracked { get; set; }

    /// <summary>
    /// When the channel was last checked for new videos.
    /// </summary>
    public DateTime? LastCheckDate { get; set; }

    /// <summary>
    /// User-friendly display of when the channel was last checked.
    /// </summary>
    public string LastCheckedDisplay => FormatHelper.FormatUpdateDateDisplay(LastCheckDate);

    /// <summary>
    /// Gets the year the channel was started/published.
    /// </summary>
    public string PublishedYear => PublishedAt.Year.ToString();

    /// <summary>
    /// The current user's rating for this channel (if any).
    /// </summary>
    public ChannelRatingModel? UserRating { get; set; }

    /// <summary>
    /// Formatted subscriber count for display (e.g., "1.2M", "45.3K").
    /// </summary>
    public string SubscriberCountDisplay => FormatHelper.FormatCount(SubscriberCount);

    /// <summary>
    /// Formatted video count for display.
    /// </summary>
    public string VideoCountDisplay => FormatHelper.FormatCount(VideoCount);

    /// <summary>
    /// User-friendly display of when tracking started.
    /// </summary>
    public string TrackedSinceDisplay => FormatHelper.FormatUpdateDateDisplay(TrackedSince);

    /// <summary>
    /// Truncated description for card display.
    /// </summary>
    public string ShortDescription => TruncateDescription(Description, 120);

    /// <summary>
    /// Gets the YouTube channel URL.
    /// </summary>
    public string YouTubeUrl => $"https://www.youtube.com/channel/{YouTubeChannelId}/videos";

    /// <summary>
    /// Indicates if the user has rated this channel.
    /// </summary>
    public bool IsRated => UserRating != null;

    /// <summary>
    /// Indicates if the channel is rated 1-star (should be excluded from suggestions).
    /// </summary>
    public bool IsLowRated => UserRating?.IsLowRating ?? false;

    /// <summary>
    /// Gets the star rating if available, 0 if not rated.
    /// </summary>
    public int StarRating => UserRating?.Stars ?? 0;

    /// <summary>
    /// Gets display text for the channel's rating status.
    /// </summary>
    public string RatingStatusDisplay => UserRating switch
    {
        null => "Not rated",
        var rating when rating.IsLowRating => "Low rated (excluded from suggestions)",
        var rating => $"Rated {rating.Stars} stars"
    };

    /// <summary>
    /// Gets CSS class for rating status display.
    /// </summary>
    public string RatingStatusCssClass => UserRating switch
    {
        null => "text-muted",
        var rating when rating.IsLowRating => "text-danger",
        var rating when rating.Stars >= 4 => "text-success",
        var rating when rating.Stars == 3 => "text-info",
        var rating => "text-warning"
    };

    /// <summary>
    /// Truncates description text to specified length with ellipsis.
    /// </summary>
    private static string TruncateDescription(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "No description available";

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength).TrimEnd() + "...";
    }
}