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