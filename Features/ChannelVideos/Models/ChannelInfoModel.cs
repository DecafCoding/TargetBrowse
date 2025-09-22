namespace TargetBrowse.Features.ChannelVideos.Models;

/// <summary>
/// Channel metadata and information for display.
/// </summary>
public class ChannelInfoModel
{
    /// <summary>
    /// YouTube channel ID.
    /// </summary>
    public string YouTubeChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Channel name/title.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Channel thumbnail/avatar URL.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Number of subscribers (if available).
    /// </summary>
    public ulong? SubscriberCount { get; set; }

    /// <summary>
    /// Number of videos uploaded by this channel.
    /// </summary>
    public ulong? VideoCount { get; set; }



    /// <summary>
    /// When the channel was created.
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Channel URL for external link.
    /// </summary>
    public string ChannelUrl => $"https://www.youtube.com/channel/{YouTubeChannelId}";

    /// <summary>
    /// Formatted subscriber count for display.
    /// </summary>
    public string SubscriberCountDisplay => FormatCount(SubscriberCount);

    /// <summary>
    /// Formatted video count for display.
    /// </summary>
    public string VideoCountDisplay => FormatCount(VideoCount);

    /// <summary>
    /// Last time this channel was checked for new videos.
    /// </summary>
    public DateTime? LastCheckDate { get; set; }

    /// <summary>
    /// Formatted display text for the last check date.
    /// </summary>
    public string LastCheckDateDisplay
    {
        get
        {
            if (!LastCheckDate.HasValue)
                return "Never checked";

            var timeDiff = DateTime.UtcNow - LastCheckDate.Value;

            return timeDiff.TotalDays switch
            {
                < 1 when timeDiff.TotalHours < 1 => $"{(int)timeDiff.TotalMinutes} minutes ago",
                < 1 => $"{(int)timeDiff.TotalHours} hours ago",
                < 7 => $"{(int)timeDiff.TotalDays} days ago",
                < 30 => $"{(int)(timeDiff.TotalDays / 7)} weeks ago",
                _ => LastCheckDate.Value.ToString("MMM d, yyyy")
            };
        }
    }

    /// <summary>
    /// Number of videos from this channel that exist in our database.
    /// </summary>
    public int DatabaseVideoCount { get; set; } = 0;

    // Add this new property after the existing VideoCountDisplay property (around line 65)

    /// <summary>
    /// Formatted display for database video count to show alongside YouTube count.
    /// </summary>
    public string DatabaseVideoCountDisplay => DatabaseVideoCount > 0 ? $"({DatabaseVideoCount} in database)" : string.Empty;

    /// <summary>
    /// Formats large numbers into human-readable format.
    /// </summary>
    private static string FormatCount(ulong? count)
    {
        if (!count.HasValue || count == 0)
            return "N/A";

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
    /// Truncates text to specified length with ellipsis.
    /// </summary>
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength).TrimEnd() + "...";
    }
}