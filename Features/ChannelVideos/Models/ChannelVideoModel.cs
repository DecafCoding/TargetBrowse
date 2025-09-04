namespace TargetBrowse.Features.ChannelVideos.Models;

/// <summary>
/// Video model specifically for channel video display.
/// Contains formatting methods optimized for channel video grid.
/// </summary>
public class ChannelVideoModel
{
    /// <summary>
    /// YouTube video ID.
    /// </summary>
    public string YouTubeVideoId { get; set; } = string.Empty;

    /// <summary>
    /// Video title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Video description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Video thumbnail URL.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Video duration in seconds.
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Number of views.
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// Number of likes.
    /// </summary>
    public int LikeCount { get; set; }

    /// <summary>
    /// Number of comments.
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// When the video was published.
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// YouTube URL for this video.
    /// </summary>
    public string YouTubeUrl => $"https://www.youtube.com/watch?v={YouTubeVideoId}";

    /// <summary>
    /// Formatted duration for display (e.g., "4:13", "1:02:45").
    /// </summary>
    public string DurationDisplay => FormatDuration(Duration);

    /// <summary>
    /// Formatted view count for display.
    /// </summary>
    public string ViewCountDisplay => FormatCount(ViewCount);

    /// <summary>
    /// Formatted like count for display.
    /// </summary>
    public string LikeCountDisplay => FormatCount(LikeCount);

    /// <summary>
    /// Formatted comment count for display.
    /// </summary>
    public string CommentCountDisplay => FormatCount(CommentCount);

    /// <summary>
    /// Formatted publication date for display.
    /// </summary>
    public string PublishedDisplay => FormatPublishedDate();

    /// <summary>
    /// Short description for card display.
    /// </summary>
    public string ShortDescription => TruncateText(Description, 100);

    /// <summary>
    /// Short title for compact display.
    /// </summary>
    public string ShortTitle => TruncateText(Title, 60);

    /// <summary>
    /// Gets the best thumbnail URL with fallbacks.
    /// </summary>
    public string GetThumbnailUrl()
    {
        if (!string.IsNullOrEmpty(ThumbnailUrl))
            return ThumbnailUrl;

        // Fallback to YouTube thumbnail
        return $"https://img.youtube.com/vi/{YouTubeVideoId}/hqdefault.jpg";
    }

    /// <summary>
    /// Formats duration from seconds to human-readable format.
    /// </summary>
    private static string FormatDuration(int durationSeconds)
    {
        if (durationSeconds <= 0)
            return "0:00";

        var timespan = TimeSpan.FromSeconds(durationSeconds);

        if (timespan.TotalHours >= 1)
        {
            return $"{(int)timespan.TotalHours}:{timespan.Minutes:D2}:{timespan.Seconds:D2}";
        }
        else
        {
            return $"{timespan.Minutes}:{timespan.Seconds:D2}";
        }
    }

    /// <summary>
    /// Formats large numbers into human-readable format.
    /// </summary>
    private static string FormatCount(int count)
    {
        if (count == 0)
            return "0";

        var value = (double)count;
        return value switch
        {
            >= 1_000_000_000 => $"{value / 1_000_000_000:F1}B",
            >= 1_000_000 => $"{value / 1_000_000:F1}M",
            >= 1_000 => $"{value / 1_000:F1}K",
            _ => count.ToString("N0")
        };
    }

    /// <summary>
    /// Formats the publication date for user-friendly display.
    /// </summary>
    private string FormatPublishedDate()
    {
        var now = DateTime.UtcNow;
        var timeSpan = now - PublishedAt;

        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => "Just published",
            < 1 when timeSpan.TotalHours < 24 => $"{(int)timeSpan.TotalHours}h ago",
            < 7 => $"{(int)timeSpan.TotalDays}d ago",
            < 30 => $"{(int)(timeSpan.TotalDays / 7)}w ago",
            < 365 => $"{(int)(timeSpan.TotalDays / 30)}mo ago",
            _ => PublishedAt.ToString("MMM d, yyyy")
        };
    }

    /// <summary>
    /// Truncates text to specified length with ellipsis.
    /// </summary>
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "No description available";

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength).TrimEnd() + "...";
    }
}