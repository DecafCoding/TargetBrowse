namespace TargetBrowse.Features.ChannelVideos.Models;

/// <summary>
/// Page model for the Channel Videos page containing channel info and video list.
/// </summary>
public class ChannelVideosModel
{
    /// <summary>
    /// Channel information and metadata.
    /// </summary>
    public ChannelInfoModel Channel { get; set; } = new();

    /// <summary>
    /// List of recent videos from this channel.
    /// </summary>
    public List<ChannelVideoModel> Videos { get; set; } = new();

    /// <summary>
    /// Whether the page is currently loading data.
    /// </summary>
    public bool IsLoading { get; set; } = false;

    /// <summary>
    /// Error message if loading failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the current user is tracking this channel.
    /// </summary>
    public bool IsTrackedByUser { get; set; } = false;

    /// <summary>
    /// User's rating for this channel (1-5 stars, null if not rated).
    /// </summary>
    public int? UserRating { get; set; }

    /// <summary>
    /// Whether any videos were found.
    /// </summary>
    public bool HasVideos => Videos.Any();

    /// <summary>
    /// Display text for the number of videos found.
    /// </summary>
    public string VideoCountDisplay => Videos.Count switch
    {
        0 => "No recent videos found",
        1 => "1 recent video",
        _ => $"{Videos.Count} recent videos"
    };

    /// <summary>
    /// Gets the breadcrumb text for navigation.
    /// </summary>
    public string BreadcrumbText => $"Library → {Channel.Name}";
}