using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Display model for video information in the UI.
/// Provides formatted data and user-friendly display methods.
/// Handles missing data gracefully for search results vs detailed views.
/// </summary>
public class VideoDisplayModel
{
    /// <summary>
    /// Unique identifier for the video in our system.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// YouTube video ID (11-character identifier).
    /// </summary>
    public string YouTubeVideoId { get; set; } = string.Empty;

    /// <summary>
    /// Video title as displayed on YouTube.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Video description (truncated for display).
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// URL to video thumbnail image.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Video duration in ISO 8601 format (PT4M13S).
    /// May be null for search results - use detailed API call to get full info.
    /// </summary>
    public string? Duration { get; set; }

    /// <summary>
    /// Number of views on the video.
    /// May be null for search results - use detailed API call to get full info.
    /// </summary>
    public ulong? ViewCount { get; set; }

    /// <summary>
    /// Number of likes on the video.
    /// May be null for search results - use detailed API call to get full info.
    /// </summary>
    public ulong? LikeCount { get; set; }

    /// <summary>
    /// Number of comments on the video.
    /// May be null for search results - use detailed API call to get full info.
    /// </summary>
    public ulong? CommentCount { get; set; }

    /// <summary>
    /// Video tags/keywords.
    /// Empty for search results - use detailed API call to get full info.
    /// </summary>
    public List<string> Tags { get; set; } = new List<string>();

    /// <summary>
    /// Video category ID.
    /// May be null for search results.
    /// </summary>
    public string? CategoryId { get; set; }

    /// <summary>
    /// Video's default language.
    /// May be null for search results.
    /// </summary>
    public string? DefaultLanguage { get; set; }

    /// <summary>
    /// When the video was published on YouTube.
    /// </summary>
    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Channel ID that published this video.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Channel name that published this video.
    /// </summary>
    public string ChannelTitle { get; set; } = string.Empty;

    /// <summary>
    /// When the user added this video to their library.
    /// </summary>
    public DateTime? AddedToLibrary { get; set; }

    /// <summary>
    /// Whether this video is in the user's library.
    /// </summary>
    public bool IsInLibrary { get; set; }

    /// <summary>
    /// User's watch status for this video.
    /// </summary>
    public WatchStatus WatchStatus { get; set; } = WatchStatus.NotWatched;

    /// <summary>
    /// Indicates whether this model contains detailed information (from videos API)
    /// or basic information only (from search API).
    /// </summary>
    public bool HasDetailedInfo => Duration != null || ViewCount.HasValue || Tags.Any();

    /// <summary>
    /// Formatted view count for display (e.g., "1.2M", "45.3K").
    /// Returns empty string if view count is null.
    /// </summary>
    public string ViewCountDisplay => FormatCount(ViewCount);

    /// <summary>
    /// Formatted like count for display (e.g., "1.2K", "45").
    /// Returns empty string if like count is null.
    /// </summary>
    public string LikeCountDisplay => FormatCount(LikeCount);

    /// <summary>
    /// Formatted comment count for display (e.g., "1.2K", "45").
    /// Returns empty string if comment count is null.
    /// </summary>
    public string CommentCountDisplay => FormatCount(CommentCount);

    /// <summary>
    /// Formatted duration for display (e.g., "4:13", "1:02:45").
    /// Returns empty string if duration is null.
    /// </summary>
    public string DurationDisplay => FormatDuration(Duration);

    /// <summary>
    /// User-friendly display of when added to library.
    /// </summary>
    public string AddedToLibraryDisplay => FormatAddedDate();

    /// <summary>
    /// Truncated description for card display.
    /// </summary>
    public string ShortDescription => TruncateDescription(Description, 100);

    /// <summary>
    /// Truncated title for compact display.
    /// </summary>
    public string ShortTitle => TruncateDescription(Title, 60);

    /// <summary>
    /// Gets the YouTube video URL.
    /// </summary>
    public string YouTubeUrl => $"https://www.youtube.com/watch?v={YouTubeVideoId}";

    /// <summary>
    /// Gets the YouTube channel URL for this video's channel.
    /// </summary>
    public string ChannelUrl => $"https://www.youtube.com/channel/{ChannelId}";

    /// <summary>
    /// User-friendly display of publication date.
    /// </summary>
    public string PublishedDisplay => FormatPublishedDate();

    /// <summary>
    /// Formats large numbers into human-readable format (K, M, B).
    /// Returns empty string for null values.
    /// </summary>
    private static string FormatCount(ulong? count)
    {
        if (!count.HasValue)
            return string.Empty;

        if (count == 0)
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
    /// Formats ISO 8601 duration to human-readable format.
    /// Returns empty string for null/empty durations.
    /// </summary>
    private static string FormatDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return string.Empty;

        try
        {
            // Parse ISO 8601 duration (PT4M13S)
            var timespan = System.Xml.XmlConvert.ToTimeSpan(duration);
            
            if (timespan.TotalHours >= 1)
            {
                return $"{(int)timespan.TotalHours}:{timespan.Minutes:D2}:{timespan.Seconds:D2}";
            }
            else
            {
                return $"{timespan.Minutes}:{timespan.Seconds:D2}";
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Formats the library addition date for user-friendly display.
    /// </summary>
    private string FormatAddedDate()
    {
        if (!AddedToLibrary.HasValue)
            return "Not in library";

        var now = DateTime.UtcNow;
        var timeSpan = now - AddedToLibrary.Value;

        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => "Just now",
            < 1 when timeSpan.TotalHours < 24 => $"{(int)timeSpan.TotalHours}h ago",
            < 7 => $"{(int)timeSpan.TotalDays}d ago",
            < 30 => $"{(int)(timeSpan.TotalDays / 7)}w ago",
            _ => AddedToLibrary.Value.ToString("MMM d, yyyy")
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
    private static string TruncateDescription(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "No description available";

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength).TrimEnd() + "...";
    }
}
