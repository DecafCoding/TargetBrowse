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
    public string LastCheckedDisplay => FormatLastChecked();

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
    public string SubscriberCountDisplay => FormatCount(SubscriberCount);

    /// <summary>
    /// Formatted video count for display.
    /// </summary>
    public string VideoCountDisplay => FormatCount(VideoCount);

    /// <summary>
    /// User-friendly display of when tracking started.
    /// </summary>
    public string TrackedSinceDisplay => FormatTrackedSince();

    /// <summary>
    /// Truncated description for card display.
    /// </summary>
    public string ShortDescription => TruncateDescription(Description, 120);

    /// <summary>
    /// Gets the YouTube channel URL.
    /// </summary>
    public string YouTubeUrl => $"https://www.youtube.com/channel/{YouTubeChannelId}";

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
    /// Formats large numbers into human-readable format (k, M, B).
    /// </summary>
    private static string FormatCount(ulong? count)
    {
        if (!count.HasValue || count == 0)
            return "0";

        var value = (double)count.Value;

        return value switch
        {
            >= 1_000_000 => $"{value / 1_000_000:F1}M",
            >= 1_000 => $"{(int)(value / 1_000)}k",
            _ => count.Value.ToString("N0")
        };
    }

    /// <summary>
    /// Formats the tracking start date for user-friendly display.
    /// </summary>
    private string FormatTrackedSince()
    {
        if (!TrackedSince.HasValue)
            return "Not tracked";

        var now = DateTime.UtcNow;
        var timeSpan = now - TrackedSince.Value;

        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => "Just now",
            < 1 when timeSpan.TotalHours < 24 => $"{(int)timeSpan.TotalHours}h ago",
            < 7 => $"{(int)timeSpan.TotalDays}d ago",
            < 30 => $"{(int)(timeSpan.TotalDays / 7)}w ago",
            _ => TrackedSince.Value.ToString("MMM d, yyyy")
        };
    }

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

    /// <summary>
    /// Formats the last checked date for user-friendly display.
    /// </summary>
    private string FormatLastChecked()
    {
        if (!LastCheckDate.HasValue)
            return "Never";

        var now = DateTime.UtcNow;
        var timeSpan = now - LastCheckDate.Value;

        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => "Just now",
            < 1 when timeSpan.TotalHours < 24 => $"{(int)timeSpan.TotalHours}h ago",
            < 7 => $"{(int)timeSpan.TotalDays}d ago",
            < 30 => $"{(int)(timeSpan.TotalDays / 7)}w ago",
            _ => LastCheckDate.Value.ToString("MMM d, yyyy")
        };
    }
}