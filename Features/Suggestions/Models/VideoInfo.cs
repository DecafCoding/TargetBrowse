using TargetBrowse.Services;

namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Represents basic video information from YouTube API.
/// Used for suggestion processing before video entities are created.
/// </summary>
public class VideoInfo
{
    /// <summary>
    /// YouTube video identifier.
    /// </summary>
    public string YouTubeVideoId { get; set; } = string.Empty;

    /// <summary>
    /// Video title from YouTube.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// YouTube channel identifier that owns this video.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Channel name for display purposes.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// When the video was published on YouTube.
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// When the video was published on YouTube.
    /// </summary>
    public string PublishedAtDisplay => FormatHelper.FormatUpdateDateDisplay(PublishedAt);

    /// <summary>
    /// Video view count from YouTube.
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// Video view count formatted for UI display.
    /// </summary>
    public string ViewCountDisplay => FormatHelper.FormatCount(ViewCount);

    /// <summary>
    /// Video like count from YouTube.
    /// </summary>
    public int LikeCount { get; set; }

    /// <summary>
    /// Video like count formatted for UI display.
    /// </summary>
    public string LikeCountDisplay => FormatHelper.FormatCount(LikeCount);

    /// <summary>
    /// Video comment count from YouTube.
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Video comment count formatted for UI display.
    /// </summary>
    public string CommentCountDisplay => FormatHelper.FormatCount(CommentCount);

    /// <summary>
    /// Video duration in seconds.
    /// </summary>
    public int Duration { get; set; }

    /// <summary>
    /// Video duration formatted for display.
    /// </summary>
    public string DurationDisplay => FormatHelper.FormatDuration(Duration);

    /// <summary>
    /// Video thumbnail URL for display.
    /// </summary>
    public string ThumbnailUrl { get; set; } = string.Empty;

    /// <summary>
    /// Video description from YouTube.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Duration category based on YouTube API search filter
    /// </summary>
    public string DurationCategory { get; set; } = string.Empty; // "Medium", "Long", or empty
}