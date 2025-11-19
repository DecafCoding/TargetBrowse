using TargetBrowse.Services.Utilities;

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
    /// YouTube channel ID for this video.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Channel name/title.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

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
    public string DurationDisplay => FormatHelper.FormatDuration(Duration.ToString());

    /// <summary>
    /// Formatted view count for display.
    /// </summary>
    public string ViewCountDisplay => FormatHelper.FormatCount(ViewCount);

    /// <summary>
    /// Formatted like count for display.
    /// </summary>
    public string LikeCountDisplay => FormatHelper.FormatCount(LikeCount);

    /// <summary>
    /// Formatted comment count for display.
    /// </summary>
    public string CommentCountDisplay => FormatHelper.FormatCount(CommentCount);

    /// <summary>
    /// Formatted publication date for display.
    /// </summary>
    public string PublishedDisplay => FormatHelper.FormatDateDisplay(PublishedAt);

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