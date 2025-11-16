using System.ComponentModel.DataAnnotations;
using TargetBrowse.Features.Channels.Utilities;

namespace TargetBrowse.Features.Channels.Models;

/// <summary>
/// Model for adding a channel to user's tracking list.
/// Contains channel information and validation rules.
/// </summary>
public class AddChannelModel
{
    [Required(ErrorMessage = "Channel ID is required.")]
    public string YouTubeChannelId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Channel name is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Channel name must be between 1 and 200 characters.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    public ulong? SubscriberCount { get; set; }

    public ulong? VideoCount { get; set; }

    public DateTime PublishedAt { get; set; }

    /// <summary>
    /// Creates an AddChannelModel from a ChannelDisplayModel.
    /// Used when converting search results to trackable channels.
    /// </summary>
    public static AddChannelModel FromDisplayModel(ChannelDisplayModel displayModel)
    {
        return new AddChannelModel
        {
            YouTubeChannelId = displayModel.YouTubeChannelId,
            Name = displayModel.Name,
            Description = displayModel.Description,
            ThumbnailUrl = displayModel.ThumbnailUrl,
            SubscriberCount = displayModel.SubscriberCount,
            VideoCount = displayModel.VideoCount,
            PublishedAt = displayModel.PublishedAt
        };
    }

    /// <summary>
    /// Validates that the channel data is complete and valid.
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(YouTubeChannelId) &&
               YouTubeUrlParser.IsValidChannelId(YouTubeChannelId) &&
               !string.IsNullOrWhiteSpace(Name) &&
               PublishedAt != default;
    }
}