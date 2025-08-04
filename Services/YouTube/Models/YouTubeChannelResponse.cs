namespace TargetBrowse.Services.YouTube.Models;

/// <summary>
/// Response model for YouTube channel information from the API.
/// Maps to our application's channel display needs.
/// </summary>
public class YouTubeChannelResponse
{
    public string ChannelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public ulong? SubscriberCount { get; set; }
    public ulong? VideoCount { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? CustomUrl { get; set; }
    public string? Handle { get; set; }
}