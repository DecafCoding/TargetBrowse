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
    /// Formats large numbers into human-readable format (K, M, B).
    /// </summary>
    private static string FormatCount(ulong? count)
    {
        if (!count.HasValue || count == 0)
            return "0";

        var value = (double)count.Value;

        return value switch
        {
            >= 1_000_000_000 => $"{value / 1_000_000_000:F1}B",
            >= 1_000_000 => $"{value / 1_000_000:F1}M",
            >= 1_000 => $"{value / 1_000:F1}K",
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
}