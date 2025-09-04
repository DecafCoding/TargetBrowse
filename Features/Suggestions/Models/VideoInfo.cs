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
    /// Video view count from YouTube.
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// Video like count from YouTube.
    /// </summary>
    public int LikeCount { get; set; }

    /// <summary>
    /// Video comment count from YouTube.
    /// </summary>
    public int CommentCount { get; set; }

    /// <summary>
    /// Video duration in seconds.
    /// </summary>
    public int Duration { get; set; }

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

    /// <summary>
    /// Helper property to get formatted duration for display.
    /// </summary>
    public string FormattedDuration
    {
        get
        {
            var timeSpan = TimeSpan.FromSeconds(Duration);
            if (timeSpan.TotalHours >= 1)
                return timeSpan.ToString(@"h\:mm\:ss");
            return timeSpan.ToString(@"m\:ss");
        }
    }

    /// <summary>
    /// Helper property to get formatted view count for display.
    /// </summary>
    public string FormattedViewCount
    {
        get
        {
            return ViewCount switch
            {
                >= 1_000_000 => $"{ViewCount / 1_000_000.0:F1}M views",
                >= 1_000 => $"{ViewCount / 1_000.0:F1}K views",
                _ => $"{ViewCount} views"
            };
        }
    }

    /// <summary>
    /// Helper property to get time since publication for display.
    /// </summary>
    public string TimeSincePublished
    {
        get
        {
            var timeSince = DateTime.UtcNow - PublishedAt;
            return timeSince.TotalDays switch
            {
                < 1 => "Today",
                < 7 => $"{(int)timeSince.TotalDays} days ago",
                < 30 => $"{(int)(timeSince.TotalDays / 7)} weeks ago",
                < 365 => $"{(int)(timeSince.TotalDays / 30)} months ago",
                _ => $"{(int)(timeSince.TotalDays / 365)} years ago"
            };
        }
    }
}