namespace TargetBrowse.Services.YouTube.Models;

/// <summary>
/// Response model for YouTube video information from the API.
/// Maps to our application's video display needs.
/// </summary>
public class YouTubeVideoResponse
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string? Duration { get; set; } // ISO 8601 format (PT4M13S)
    public ulong? ViewCount { get; set; }
    public ulong? LikeCount { get; set; }
    public ulong? CommentCount { get; set; }
    public DateTime PublishedAt { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    public string? DefaultLanguage { get; set; }
    public bool IsLiveBroadcast { get; set; }
    public string? LiveBroadcastContent { get; set; }
}