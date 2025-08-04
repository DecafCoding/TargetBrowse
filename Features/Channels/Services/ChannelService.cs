using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using TargetBrowse.Features.Channels.Data;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Features.Channels.Utilities;
using TargetBrowse.Services;
using TargetBrowse.Services.YouTube;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Real implementation of channel service with YouTube API integration.
/// Handles channel search, tracking, and database operations.
/// </summary>
public class ChannelService : IChannelService
{
    private readonly IYouTubeService _youTubeService;
    private readonly IChannelRepository _channelRepository;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<ChannelService> _logger;

    private const int MaxChannelsPerUser = 50;

    public ChannelService(
        IYouTubeService youTubeService,
        IChannelRepository channelRepository,
        IMessageCenterService messageCenterService,
        ILogger<ChannelService> logger)
    {
        _youTubeService = youTubeService;
        _channelRepository = channelRepository;
        _messageCenterService = messageCenterService;
        _logger = logger;
    }

    /// <summary>
    /// Removes a channel from the user's tracking list.
    /// </summary>
    public async Task<bool> RemoveChannelFromTrackingAsync(string userId, Guid channelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                await _messageCenterService.ShowErrorAsync("User authentication required.");
                return false;
            }

            if (channelId == Guid.Empty)
            {
                await _messageCenterService.ShowErrorAsync("Invalid channel selected.");
                return false;
            }

            // Get the channel information for feedback
            var userChannel = await _channelRepository.GetUserChannelRelationshipAsync(userId, channelId);
            if (userChannel?.Channel == null)
            {
                await _messageCenterService.ShowWarningAsync("Channel not found in your tracking list.");
                return false;
            }

            // Remove the tracking relationship
            var success = await _channelRepository.RemoveChannelFromUserTrackingAsync(userId, channelId);

            if (success)
            {
                await _messageCenterService.ShowSuccessAsync($"Channel '{userChannel.Channel.Name}' removed from your tracking list.");
                _logger.LogInformation("User {UserId} removed channel: {ChannelName} ({ChannelId})",
                    userId, userChannel.Channel.Name, channelId);
                return true;
            }
            else
            {
                await _messageCenterService.ShowWarningAsync("Channel not found in your tracking list.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing channel {ChannelId} for user {UserId}", channelId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to remove channel. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Gets all channels tracked by the specified user.
    /// </summary>
    public async Task<List<ChannelDisplayModel>> GetTrackedChannelsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<ChannelDisplayModel>();
            }

            var trackedChannels = await _channelRepository.GetTrackedChannelsByUserAsync(userId);
            var displayModels = new List<ChannelDisplayModel>();

            foreach (var channel in trackedChannels)
            {
                // Get the tracking relationship for each channel
                var userChannel = await _channelRepository.GetUserChannelRelationshipAsync(userId, channel.Id);

                if (userChannel != null)
                {
                    displayModels.Add(ChannelMappingService.MapToDisplayModel(channel, userChannel));
                }
                else
                {
                    // Fallback if relationship not found
                    displayModels.Add(ChannelMappingService.MapToDisplayModel(channel, isTracked: true));
                }
            }

            _logger.LogDebug("Retrieved {ChannelCount} tracked channels for user {UserId}", displayModels.Count, userId);
            return displayModels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tracked channels for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to load tracked channels. Please refresh the page and try again.");
            return new List<ChannelDisplayModel>();
        }
    }

    /// <summary>
    /// Gets the current count of tracked channels for a user.
    /// </summary>
    public async Task<int> GetTrackedChannelCountAsync(string userId)
    {
        try
        {
            return await _channelRepository.GetTrackedChannelCountAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracked channel count for user {UserId}", userId);
            return 0;
        }
    }

    /// <summary>
    /// Checks if a channel is already being tracked by the user.
    /// </summary>
    public async Task<bool> IsChannelTrackedAsync(string userId, string youTubeChannelId)
    {
        try
        {
            return await _channelRepository.IsChannelTrackedByUserAsync(userId, youTubeChannelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if channel {ChannelId} is tracked for user {UserId}", youTubeChannelId, userId);
            return false;
        }
    }

    /// <summary>
    /// Gets detailed information about a specific YouTube channel.
    /// </summary>
    public async Task<ChannelDisplayModel?> GetChannelDetailsAsync(string youTubeChannelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(youTubeChannelId))
            {
                return null;
            }

            // First check our database
            var channelEntity = await _channelRepository.GetChannelByYouTubeIdAsync(youTubeChannelId);
            if (channelEntity != null)
            {
                return ChannelMappingService.MapToDisplayModel(channelEntity);
            }

            // If not in database, get from YouTube API
            var apiResult = await _youTubeService.GetChannelByIdAsync(youTubeChannelId);

            if (!apiResult.IsSuccess || apiResult.Data == null)
            {
                return null;
            }

            return ChannelMappingService.MapToDisplayModel(apiResult.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel details for {ChannelId}", youTubeChannelId);
            return null;
        }
    }

    /// <summary>
    /// Searches for a channel using URL parsing results.
    /// </summary>
    private async Task<YouTubeApiResult<YouTubeChannelResponse?>> SearchByUrlAsync(YouTubeUrlParser.ParseResult parseResult)
    {
        return parseResult.Type switch
        {
            YouTubeUrlParser.ParseType.ChannelId => await _youTubeService.GetChannelByIdAsync(parseResult.ChannelId!),
            YouTubeUrlParser.ParseType.Username => await _youTubeService.GetChannelByUsernameAsync(parseResult.Username!),
            YouTubeUrlParser.ParseType.Handle => await _youTubeService.GetChannelByHandleAsync(parseResult.Handle!),
            YouTubeUrlParser.ParseType.CustomUrl => await _youTubeService.GetChannelByCustomUrlAsync(parseResult.CustomUrl!),
            _ => YouTubeApiResult<YouTubeChannelResponse?>.Failure("Unsupported URL format.")
        };
    }

    /// <summary>
    /// Handles API errors and provides appropriate user feedback.
    /// </summary>
    private async Task HandleApiError<T>(YouTubeApiResult<T> result, string searchQuery)
    {
        if (result.IsQuotaExceeded)
        {
            await _messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.AddDays(1));
        }
        else if (result.IsInvalidChannel)
        {
            await _messageCenterService.ShowWarningAsync($"Channel not found for '{searchQuery}'. Please check the URL or try a different search term.");
        }
        else
        {
            await _messageCenterService.ShowErrorAsync(result.ErrorMessage ?? "An error occurred while searching YouTube. Please try again.");
        }

        _logger.LogWarning("YouTube API error for search '{SearchQuery}': {ErrorMessage}",
            searchQuery, result.ErrorMessage);
    }
    /// Searches for YouTube channels by name or analyzes a YouTube URL.
    /// </summary>
    public async Task<List<ChannelDisplayModel>> SearchChannelsAsync(string searchQuery)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return new List<ChannelDisplayModel>();
            }

            // Parse the search query to determine if it's a URL or search term
            var parseResult = YouTubeUrlParser.Parse(searchQuery);

            if (!parseResult.IsValid)
            {
                await _messageCenterService.ShowErrorAsync("Invalid search query. Please enter a channel name or valid YouTube URL.");
                return new List<ChannelDisplayModel>();
            }

            YouTubeApiResult<List<YouTubeChannelResponse>>? searchResult = null;
            YouTubeApiResult<YouTubeChannelResponse?>? directResult = null;

            if (parseResult.IsUrl)
            {
                // Handle URL-based searches
                directResult = await SearchByUrlAsync(parseResult);
            }
            else
            {
                // Handle text-based searches
                searchResult = await _youTubeService.SearchChannelsAsync(parseResult.SearchTerm);
            }

            // Process results
            var channels = new List<YouTubeChannelResponse>();

            if (directResult != null)
            {
                if (!directResult.IsSuccess)
                {
                    await HandleApiError(directResult, parseResult.SearchTerm);
                    return new List<ChannelDisplayModel>();
                }

                if (directResult.Data != null)
                {
                    channels.Add(directResult.Data);
                }
            }
            else if (searchResult != null)
            {
                if (!searchResult.IsSuccess)
                {
                    await HandleApiError(searchResult, parseResult.SearchTerm);
                    return new List<ChannelDisplayModel>();
                }

                channels.AddRange(searchResult.Data ?? new List<YouTubeChannelResponse>());
            }

            if (!channels.Any())
            {
                await _messageCenterService.ShowInfoAsync($"No channels found for '{searchQuery}'. Try a different search term or check the URL.");
                return new List<ChannelDisplayModel>();
            }

            // Convert to display models
            var displayModels = channels
                .Where(ChannelMappingService.IsValidYouTubeChannelResponse)
                .Select(channel => ChannelMappingService.MapToDisplayModel(channel))
                .ToList();

            _logger.LogInformation("YouTube search for '{SearchQuery}' returned {ResultCount} channels",
                searchQuery, displayModels.Count);

            return displayModels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during channel search for query: {SearchQuery}", searchQuery);
            await _messageCenterService.ShowErrorAsync("An error occurred while searching for channels. Please try again.");
            return new List<ChannelDisplayModel>();
        }
    }

    /// <summary>
    /// Adds a channel to the user's tracking list.
    /// </summary>
    public async Task<bool> AddChannelToTrackingAsync(string userId, AddChannelModel channelModel)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(userId))
            {
                await _messageCenterService.ShowErrorAsync("User authentication required.");
                return false;
            }

            if (!ChannelMappingService.IsValidAddChannelModel(channelModel))
            {
                await _messageCenterService.ShowErrorAsync("Invalid channel data. Please try searching again.");
                return false;
            }

            // Check current tracking count
            var currentCount = await _channelRepository.GetTrackedChannelCountAsync(userId);
            if (currentCount >= MaxChannelsPerUser)
            {
                await _messageCenterService.ShowWarningAsync($"You have reached the maximum limit of {MaxChannelsPerUser} tracked channels. Remove some channels before adding new ones.");
                return false;
            }

            // Check for duplicates
            if (await _channelRepository.IsChannelTrackedByUserAsync(userId, channelModel.YouTubeChannelId))
            {
                await _messageCenterService.ShowWarningAsync($"Channel '{channelModel.Name}' is already in your tracking list.");
                return false;
            }

            // Find or create the channel in the database
            var channelEntity = await _channelRepository.FindOrCreateChannelAsync(
                channelModel.YouTubeChannelId,
                channelModel.Name,
                channelModel.Description,
                channelModel.ThumbnailUrl,
                channelModel.SubscriberCount,
                channelModel.VideoCount,
                channelModel.PublishedAt);

            // Add the user-channel relationship
            await _channelRepository.AddChannelToUserTrackingAsync(userId, channelEntity.Id);

            await _messageCenterService.ShowSuccessAsync($"Channel '{channelModel.Name}' added to your tracking list!");

            _logger.LogInformation("User {UserId} added channel: {ChannelName} ({YouTubeChannelId})",
                userId, channelModel.Name, channelModel.YouTubeChannelId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding channel {ChannelName} for user {UserId}", channelModel.Name, userId);
            await _messageCenterService.ShowErrorAsync("Failed to add channel. Please try again.");
            return false;
        }
    }
}