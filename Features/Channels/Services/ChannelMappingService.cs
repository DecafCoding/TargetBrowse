using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Service for mapping between different channel model representations.
/// Handles conversion between YouTube API responses, database entities, and display models.
/// </summary>
public class ChannelMappingService
{
    /// <summary>
    /// Maps a YouTube API response to a channel display model.
    /// </summary>
    public static ChannelDisplayModel MapToDisplayModel(YouTubeChannelResponse youTubeChannel, bool isTracked = false, DateTime? trackedSince = null)
    {
        return new ChannelDisplayModel
        {
            Id = Guid.Empty, // Will be set when mapped from database entity
            YouTubeChannelId = youTubeChannel.ChannelId,
            Name = youTubeChannel.Name,
            Description = youTubeChannel.Description,
            ThumbnailUrl = youTubeChannel.ThumbnailUrl,
            SubscriberCount = youTubeChannel.SubscriberCount,
            VideoCount = youTubeChannel.VideoCount,
            PublishedAt = youTubeChannel.PublishedAt,
            TrackedSince = trackedSince,
            IsTracked = isTracked
        };
    }

    /// <summary>
    /// Maps a channel entity to a channel display model.
    /// </summary>
    public static ChannelDisplayModel MapToDisplayModel(ChannelEntity channelEntity, bool isTracked = false, DateTime? trackedSince = null)
    {
        return new ChannelDisplayModel
        {
            Id = channelEntity.Id,
            YouTubeChannelId = channelEntity.YouTubeChannelId,
            Name = channelEntity.Name,
            Description = "Channel description", // TODO: Fix when entity has proper Description field
            ThumbnailUrl = channelEntity.ThumbnailUrl,
            SubscriberCount = channelEntity.SubscriberCount,
            VideoCount = channelEntity.VideoCount,
            PublishedAt = channelEntity.PublishedAt,
            TrackedSince = trackedSince,
            IsTracked = isTracked
        };
    }

    /// <summary>
    /// Maps a channel entity with user-channel relationship to a display model.
    /// </summary>
    public static ChannelDisplayModel MapToDisplayModel(ChannelEntity channelEntity, UserChannelEntity userChannel)
    {
        return new ChannelDisplayModel
        {
            Id = channelEntity.Id,
            YouTubeChannelId = channelEntity.YouTubeChannelId,
            Name = channelEntity.Name,
            Description = channelEntity.ThumbnailUrl ?? string.Empty, // Note: Fix this mapping when entity is corrected
            ThumbnailUrl = channelEntity.ThumbnailUrl,
            SubscriberCount = channelEntity.SubscriberCount,
            VideoCount = channelEntity.VideoCount,
            PublishedAt = channelEntity.PublishedAt,
            TrackedSince = userChannel.TrackedSince,
            IsTracked = true
        };
    }

    /// <summary>
    /// Maps a YouTube channel response to an AddChannelModel.
    /// </summary>
    public static AddChannelModel MapToAddChannelModel(YouTubeChannelResponse youTubeChannel)
    {
        return new AddChannelModel
        {
            YouTubeChannelId = youTubeChannel.ChannelId,
            Name = youTubeChannel.Name,
            Description = youTubeChannel.Description,
            ThumbnailUrl = youTubeChannel.ThumbnailUrl,
            SubscriberCount = youTubeChannel.SubscriberCount,
            VideoCount = youTubeChannel.VideoCount,
            PublishedAt = youTubeChannel.PublishedAt
        };
    }

    /// <summary>
    /// Maps an AddChannelModel to a channel entity.
    /// </summary>
    public static ChannelEntity MapToChannelEntity(AddChannelModel addChannelModel)
    {
        return new ChannelEntity
        {
            YouTubeChannelId = addChannelModel.YouTubeChannelId,
            Name = addChannelModel.Name,
            ThumbnailUrl = addChannelModel.ThumbnailUrl,
            SubscriberCount = addChannelModel.SubscriberCount,
            VideoCount = addChannelModel.VideoCount,
            PublishedAt = addChannelModel.PublishedAt
        };
    }

    /// <summary>
    /// Updates a channel entity with information from a YouTube API response.
    /// Preserves the entity's ID and creation metadata.
    /// </summary>
    public static void UpdateChannelEntity(ChannelEntity channelEntity, YouTubeChannelResponse youTubeChannel)
    {
        channelEntity.Name = youTubeChannel.Name;
        channelEntity.ThumbnailUrl = youTubeChannel.Description; // Note: Fix this mapping when entity is corrected
        channelEntity.ThumbnailUrl = youTubeChannel.ThumbnailUrl;
        channelEntity.SubscriberCount = youTubeChannel.SubscriberCount;
        channelEntity.VideoCount = youTubeChannel.VideoCount;

        // Only update published date if it's provided and different from default
        if (youTubeChannel.PublishedAt != default && youTubeChannel.PublishedAt != channelEntity.PublishedAt)
        {
            channelEntity.PublishedAt = youTubeChannel.PublishedAt;
        }
    }

    /// <summary>
    /// Maps a list of channel entities with user tracking information to display models.
    /// </summary>
    public static List<ChannelDisplayModel> MapTrackedChannelsToDisplayModels(List<ChannelEntity> channelEntities, string userId, ApplicationDbContext context)
    {
        var displayModels = new List<ChannelDisplayModel>();

        foreach (var channel in channelEntities)
        {
            // Get the user-channel relationship for tracking information
            var userChannel = context.UserChannels
                .Where(uc => uc.UserId == userId && uc.ChannelId == channel.Id && !uc.IsDeleted)
                .FirstOrDefault();

            if (userChannel != null)
            {
                displayModels.Add(MapToDisplayModel(channel, userChannel));
            }
            else
            {
                // Fallback if relationship not found
                displayModels.Add(MapToDisplayModel(channel, isTracked: false));
            }
        }

        return displayModels;
    }

    /// <summary>
    /// Validates that a YouTube channel response has all required fields for our system.
    /// </summary>
    public static bool IsValidYouTubeChannelResponse(YouTubeChannelResponse? youTubeChannel)
    {
        return youTubeChannel != null &&
               !string.IsNullOrWhiteSpace(youTubeChannel.ChannelId) &&
               !string.IsNullOrWhiteSpace(youTubeChannel.Name) &&
               youTubeChannel.PublishedAt != default;
    }

    /// <summary>
    /// Validates that an AddChannelModel has all required fields.
    /// </summary>
    public static bool IsValidAddChannelModel(AddChannelModel? addChannelModel)
    {
        return addChannelModel != null &&
               !string.IsNullOrWhiteSpace(addChannelModel.YouTubeChannelId) &&
               !string.IsNullOrWhiteSpace(addChannelModel.Name) &&
               addChannelModel.PublishedAt != default;
    }
}