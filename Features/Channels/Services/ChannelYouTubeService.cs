using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Options;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Channel-specific implementation of YouTube Data API v3 service.
/// Handles channel search, information retrieval, and quota management for the Channel feature.
/// </summary>
public class ChannelYouTubeService : IChannelYouTubeService, IDisposable
{
    private readonly Google.Apis.YouTube.v3.YouTubeService _youTubeClient;
    private readonly YouTubeApiSettings _settings;
    private readonly ILogger<ChannelYouTubeService> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore;

    // Simple in-memory quota tracking for MVP
    private static int _dailyQuotaUsed = 0;
    private static DateTime _lastQuotaReset = DateTime.UtcNow.Date;

    // API cost constants (approximate)
    private const int SearchCost = 100;
    private const int ChannelDetailsCost = 1;

    public ChannelYouTubeService(IOptions<YouTubeApiSettings> settings, ILogger<ChannelYouTubeService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _rateLimitSemaphore = new SemaphoreSlim(1, 1);

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("YouTube API key is required but not configured.");
        }

        _youTubeClient = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer()
        {
            ApiKey = _settings.ApiKey,
            ApplicationName = "YouTube Video Tracker - Channels"
        });

        ResetQuotaIfNeeded();
    }

    /// <summary>
    /// Searches for YouTube channels by name or keyword.
    /// </summary>
    public async Task<YouTubeApiResult<List<YouTubeChannelResponse>>> SearchChannelsAsync(string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return YouTubeApiResult<List<YouTubeChannelResponse>>.Success(new List<YouTubeChannelResponse>());
        }

        try
        {
            if (!await CheckQuotaAvailableAsync(SearchCost))
            {
                return YouTubeApiResult<List<YouTubeChannelResponse>>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            await _rateLimitSemaphore.WaitAsync();

            try
            {
                var searchRequest = _youTubeClient.Search.List("snippet");
                searchRequest.Q = searchQuery.Trim();
                searchRequest.Type = "channel";
                searchRequest.Order = SearchResource.ListRequest.OrderEnum.Relevance;
                searchRequest.MaxResults = _settings.MaxSearchResults;
                searchRequest.Fields = "items(id/channelId,snippet(title,description,thumbnails/default,publishedAt))";

                var searchResponse = await searchRequest.ExecuteAsync();
                await IncrementQuotaUsageAsync(SearchCost);

                if (searchResponse.Items?.Count > 0)
                {
                    // Get detailed channel information for all found channels
                    var channelIds = searchResponse.Items.Select(item => item.Id.ChannelId).ToList();
                    var detailedChannels = await GetChannelDetailsBatchAsync(channelIds);

                    var results = new List<YouTubeChannelResponse>();

                    foreach (var searchItem in searchResponse.Items)
                    {
                        var detailedChannel = detailedChannels.FirstOrDefault(c => c.ChannelId == searchItem.Id.ChannelId);

                        var channelResponse = new YouTubeChannelResponse
                        {
                            ChannelId = searchItem.Id.ChannelId,
                            Name = searchItem.Snippet.Title,
                            Description = searchItem.Snippet.Description ?? string.Empty,
                            ThumbnailUrl = searchItem.Snippet.Thumbnails?.Default__?.Url,
                            PublishedAt = searchItem.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.MinValue,
                            SubscriberCount = detailedChannel?.SubscriberCount,
                            VideoCount = detailedChannel?.VideoCount
                        };

                        results.Add(channelResponse);
                    }

                    _logger.LogInformation("Channel search for '{SearchQuery}' returned {ResultCount} channels",
                        searchQuery, results.Count);

                    return YouTubeApiResult<List<YouTubeChannelResponse>>.Success(results);
                }

                return YouTubeApiResult<List<YouTubeChannelResponse>>.Success(new List<YouTubeChannelResponse>());
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Google.GoogleApiException ex) when (ex.Error?.Code == 403)
        {
            _logger.LogWarning("YouTube API quota exceeded: {Message}", ex.Message);
            return YouTubeApiResult<List<YouTubeChannelResponse>>.Failure(
                "YouTube API quota exceeded. Please try again later.",
                isQuotaExceeded: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching YouTube channels for query: {SearchQuery}", searchQuery);
            return YouTubeApiResult<List<YouTubeChannelResponse>>.Failure(
                "Unable to search YouTube channels. Please check your internet connection and try again.");
        }
    }

    /// <summary>
    /// Gets detailed information about a specific YouTube channel by channel ID.
    /// </summary>
    public async Task<YouTubeApiResult<YouTubeChannelResponse?>> GetChannelByIdAsync(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return YouTubeApiResult<YouTubeChannelResponse?>.Failure("Channel ID is required.");
        }

        try
        {
            if (!await CheckQuotaAvailableAsync(ChannelDetailsCost))
            {
                return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            var channelRequest = _youTubeClient.Channels.List("snippet,statistics");
            channelRequest.Id = channelId;
            channelRequest.Fields = "items(id,snippet(title,description,thumbnails/default,publishedAt,customUrl),statistics(subscriberCount,videoCount))";

            var channelResponse = await channelRequest.ExecuteAsync();
            await IncrementQuotaUsageAsync(ChannelDetailsCost);

            if (channelResponse.Items?.Count > 0)
            {
                var channel = channelResponse.Items[0];
                var result = new YouTubeChannelResponse
                {
                    ChannelId = channel.Id,
                    Name = channel.Snippet.Title,
                    Description = channel.Snippet.Description ?? string.Empty,
                    ThumbnailUrl = channel.Snippet.Thumbnails?.Default__?.Url,
                    PublishedAt = channel.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.MinValue,
                    SubscriberCount = channel.Statistics?.SubscriberCount,
                    VideoCount = channel.Statistics?.VideoCount,
                    CustomUrl = channel.Snippet.CustomUrl
                };

                return YouTubeApiResult<YouTubeChannelResponse?>.Success(result);
            }

            return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
                "Channel not found.", isInvalidChannel: true);
        }
        catch (Google.GoogleApiException ex) when (ex.Error?.Code == 403)
        {
            _logger.LogWarning("YouTube API quota exceeded: {Message}", ex.Message);
            return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
                "YouTube API quota exceeded. Please try again later.",
                isQuotaExceeded: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YouTube channel details for ID: {ChannelId}", channelId);
            return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
                "Unable to get channel information. Please try again.");
        }
    }

    /// <summary>
    /// Gets channel information by username (legacy /user/ URLs).
    /// </summary>
    public async Task<YouTubeApiResult<YouTubeChannelResponse?>> GetChannelByUsernameAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return YouTubeApiResult<YouTubeChannelResponse?>.Failure("Username is required.");
        }

        try
        {
            if (!await CheckQuotaAvailableAsync(ChannelDetailsCost))
            {
                return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            var channelRequest = _youTubeClient.Channels.List("snippet,statistics");
            channelRequest.ForUsername = username;
            channelRequest.Fields = "items(id,snippet(title,description,thumbnails/default,publishedAt,customUrl),statistics(subscriberCount,videoCount))";

            var channelResponse = await channelRequest.ExecuteAsync();
            await IncrementQuotaUsageAsync(ChannelDetailsCost);

            if (channelResponse.Items?.Count > 0)
            {
                var channel = channelResponse.Items[0];
                var result = new YouTubeChannelResponse
                {
                    ChannelId = channel.Id,
                    Name = channel.Snippet.Title,
                    Description = channel.Snippet.Description ?? string.Empty,
                    ThumbnailUrl = channel.Snippet.Thumbnails?.Default__?.Url,
                    PublishedAt = channel.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.MinValue,
                    SubscriberCount = channel.Statistics?.SubscriberCount,
                    VideoCount = channel.Statistics?.VideoCount,
                    CustomUrl = channel.Snippet.CustomUrl
                };

                return YouTubeApiResult<YouTubeChannelResponse?>.Success(result);
            }

            return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
                "Channel not found.", isInvalidChannel: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YouTube channel details for username: {Username}", username);
            return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
                "Unable to get channel information. Please try again.");
        }
    }

    /// <summary>
    /// Gets channel information by handle (modern @username format).
    /// Note: YouTube API doesn't directly support handle lookup, so we search and match.
    /// </summary>
    public async Task<YouTubeApiResult<YouTubeChannelResponse?>> GetChannelByHandleAsync(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle))
        {
            return YouTubeApiResult<YouTubeChannelResponse?>.Failure("Handle is required.");
        }

        // For handles, we need to search and find the exact match
        var searchResult = await SearchChannelsAsync($"@{handle}");

        if (!searchResult.IsSuccess)
        {
            return YouTubeApiResult<YouTubeChannelResponse?>.Failure(searchResult.ErrorMessage ?? "Search failed.");
        }

        // Look for exact handle match
        var exactMatch = searchResult.Data?.FirstOrDefault(c =>
            c.CustomUrl != null && c.CustomUrl.Equals(handle, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            return YouTubeApiResult<YouTubeChannelResponse?>.Success(exactMatch);
        }

        return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
            "Channel not found.", isInvalidChannel: true);
    }

    /// <summary>
    /// Gets channel information by custom URL (/c/ format).
    /// Note: YouTube API doesn't directly support custom URL lookup, so we search and match.
    /// </summary>
    public async Task<YouTubeApiResult<YouTubeChannelResponse?>> GetChannelByCustomUrlAsync(string customUrl)
    {
        if (string.IsNullOrWhiteSpace(customUrl))
        {
            return YouTubeApiResult<YouTubeChannelResponse?>.Failure("Custom URL is required.");
        }

        // For custom URLs, we need to search and find the exact match
        var searchResult = await SearchChannelsAsync(customUrl);

        if (!searchResult.IsSuccess)
        {
            return YouTubeApiResult<YouTubeChannelResponse?>.Failure(searchResult.ErrorMessage ?? "Search failed.");
        }

        // Look for exact custom URL match
        var exactMatch = searchResult.Data?.FirstOrDefault(c =>
            c.CustomUrl != null && c.CustomUrl.Equals(customUrl, StringComparison.OrdinalIgnoreCase));

        if (exactMatch != null)
        {
            return YouTubeApiResult<YouTubeChannelResponse?>.Success(exactMatch);
        }

        return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
            "Channel not found.", isInvalidChannel: true);
    }

    /// <summary>
    /// Checks if the YouTube API is currently available and within quota limits.
    /// </summary>
    public async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            ResetQuotaIfNeeded();
            return await CheckQuotaAvailableAsync(1);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the estimated remaining API quota for today.
    /// </summary>
    public async Task<int> GetEstimatedRemainingQuotaAsync()
    {
        ResetQuotaIfNeeded();
        return Math.Max(0, _settings.DailyQuotaLimit - _dailyQuotaUsed);
    }

    /// <summary>
    /// Gets detailed channel statistics for multiple channels in a single API call.
    /// </summary>
    private async Task<List<YouTubeChannelResponse>> GetChannelDetailsBatchAsync(List<string> channelIds)
    {
        if (!channelIds.Any()) return new List<YouTubeChannelResponse>();

        try
        {
            var channelRequest = _youTubeClient.Channels.List("statistics");
            channelRequest.Id = string.Join(",", channelIds);
            channelRequest.Fields = "items(id,statistics(subscriberCount,videoCount))";

            var channelResponse = await channelRequest.ExecuteAsync();
            await IncrementQuotaUsageAsync(ChannelDetailsCost);

            return channelResponse.Items?.Select(channel => new YouTubeChannelResponse
            {
                ChannelId = channel.Id,
                SubscriberCount = channel.Statistics?.SubscriberCount,
                VideoCount = channel.Statistics?.VideoCount
            }).ToList() ?? new List<YouTubeChannelResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get batch channel details for {ChannelCount} channels", channelIds.Count);
            return new List<YouTubeChannelResponse>();
        }
    }

    /// <summary>
    /// Checks if sufficient quota is available for an operation.
    /// </summary>
    private async Task<bool> CheckQuotaAvailableAsync(int requiredQuota)
    {
        ResetQuotaIfNeeded();
        return _dailyQuotaUsed + requiredQuota <= _settings.DailyQuotaLimit;
    }

    /// <summary>
    /// Increments the quota usage counter.
    /// </summary>
    private async Task IncrementQuotaUsageAsync(int quotaUsed)
    {
        _dailyQuotaUsed += quotaUsed;
        _logger.LogDebug("Channel YouTube API quota used: {QuotaUsed}, Total today: {TotalUsed}/{DailyLimit}",
            quotaUsed, _dailyQuotaUsed, _settings.DailyQuotaLimit);
    }

    /// <summary>
    /// Resets quota tracking if a new day has started.
    /// </summary>
    private void ResetQuotaIfNeeded()
    {
        var today = DateTime.UtcNow.Date;
        if (_lastQuotaReset < today)
        {
            _dailyQuotaUsed = 0;
            _lastQuotaReset = today;
            _logger.LogInformation("Channel YouTube API quota reset for new day");
        }
    }

    public void Dispose()
    {
        _youTubeClient?.Dispose();
        _rateLimitSemaphore?.Dispose();
    }
}
