using TargetBrowse.Services.Utilities;

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
    public string SubscriberCountDisplay => FormatHelper.FormatCount(SubscriberCount);

    /// <summary>
    /// Formatted video count for display.
    /// </summary>
    public string VideoCountDisplay => FormatHelper.FormatCount(VideoCount);

    /// <summary>
    /// Last time this channel was checked for new videos.
    /// </summary>
    public DateTime? LastCheckDate { get; set; }

    /// <summary>
    /// Formatted display text for the last check date.
    /// </summary>
    public string LastCheckDateDisplay => FormatHelper.FormatUpdateDateDisplay(LastCheckDate);

    /// <summary>
    /// Number of videos from this channel that exist in our database.
    /// </summary>
    public int DatabaseVideoCount { get; set; } = 0;

    // Add this new property after the existing VideoCountDisplay property (around line 65)

    /// <summary>
    /// Formatted display for database video count to show alongside YouTube count.
    /// </summary>
    public string DatabaseVideoCountDisplay => DatabaseVideoCount > 0 ? $"({DatabaseVideoCount} in database)" : string.Empty;

}