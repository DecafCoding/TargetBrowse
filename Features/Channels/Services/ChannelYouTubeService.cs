using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Options;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Channel-specific implementation of YouTube Data API v3 service.
/// Handles channel search, information retrieval, and quota management for the Channel feature.
/// Uses the shared IYouTubeQuotaManager for centralized quota tracking across all YouTube services.
/// </summary>
public class ChannelYouTubeService : IChannelYouTubeService, IDisposable
{
    private readonly Google.Apis.YouTube.v3.YouTubeService _youTubeClient;
    private readonly YouTubeApiSettings _settings;
    private readonly IYouTubeQuotaManager _quotaManager;
    private readonly ILogger<ChannelYouTubeService> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore;

    public ChannelYouTubeService(
        IOptions<YouTubeApiSettings> settings,
        IYouTubeQuotaManager quotaManager,
        ILogger<ChannelYouTubeService> logger)
    {
        _settings = settings.Value;
        _quotaManager = quotaManager ?? throw new ArgumentNullException(nameof(quotaManager));
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
            if (!await _quotaManager.CanPerformOperationAsync(YouTubeApiOperation.SearchChannels))
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
                await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.SearchChannels);

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
            if (!await _quotaManager.CanPerformOperationAsync(YouTubeApiOperation.GetChannelDetails))
            {
                return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            var channelRequest = _youTubeClient.Channels.List("snippet,statistics");
            channelRequest.Id = channelId;
            channelRequest.Fields = "items(id,snippet(title,description,thumbnails/default,publishedAt,customUrl),statistics(subscriberCount,videoCount))";

            var channelResponse = await channelRequest.ExecuteAsync();
            await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.GetChannelDetails);

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
            if (!await _quotaManager.CanPerformOperationAsync(YouTubeApiOperation.GetChannelDetails))
            {
                return YouTubeApiResult<YouTubeChannelResponse?>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            var channelRequest = _youTubeClient.Channels.List("snippet,statistics");
            channelRequest.ForUsername = username;
            channelRequest.Fields = "items(id,snippet(title,description,thumbnails/default,publishedAt,customUrl),statistics(subscriberCount,videoCount))";

            var channelResponse = await channelRequest.ExecuteAsync();
            await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.GetChannelDetails);

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
    /// Gets recent videos from multiple channels for suggestion generation.
    /// Used by channel onboarding to fetch initial videos from newly added channels.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> GetBulkChannelUpdatesAsync(List<ChannelUpdateRequest> channelRequests)
    {
        if (!channelRequests?.Any() == true)
        {
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());
        }

        try
        {
            _logger.LogInformation("Getting bulk channel updates for {ChannelCount} channels", channelRequests.Count);

            var allVideos = new List<VideoInfo>();
            var processedChannels = 0;
            var failedChannels = 0;

            // Process channels individually to handle errors gracefully
            foreach (var channelRequest in channelRequests)
            {
                try
                {
                    var channelVideos = await GetChannelVideosAsync(channelRequest);
                    if (channelVideos.IsSuccess && channelVideos.Data?.Any() == true)
                    {
                        allVideos.AddRange(channelVideos.Data);
                        processedChannels++;

                        _logger.LogDebug("Retrieved {VideoCount} videos from channel {ChannelName}",
                            channelVideos.Data.Count, channelRequest.ChannelName);
                    }
                    else if (channelVideos.IsQuotaExceeded)
                    {
                        // If quota exceeded, stop processing and return what we have
                        _logger.LogWarning("YouTube API quota exceeded while processing channel {ChannelName}. Returning {VideoCount} videos from {ProcessedCount} channels.",
                            channelRequest.ChannelName, allVideos.Count, processedChannels);

                        if (allVideos.Any())
                        {
                            return YouTubeApiResult<List<VideoInfo>>.Success(allVideos);
                        }
                        else
                        {
                            return YouTubeApiResult<List<VideoInfo>>.Failure(
                                "YouTube API quota exceeded before any videos could be retrieved.",
                                isQuotaExceeded: true);
                        }
                    }
                    else
                    {
                        failedChannels++;
                        _logger.LogWarning("Failed to get videos from channel {ChannelName}: {Error}",
                            channelRequest.ChannelName, channelVideos.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    failedChannels++;
                    _logger.LogWarning(ex, "Exception while processing channel {ChannelName}",
                        channelRequest.ChannelName);
                }
            }

            _logger.LogInformation("Bulk channel update completed: {TotalVideos} videos from {ProcessedChannels}/{TotalChannels} channels ({FailedChannels} failed)",
                allVideos.Count, processedChannels, channelRequests.Count, failedChannels);

            // Remove duplicates by video ID (in case same video appears in multiple channels)
            var uniqueVideos = allVideos
                .GroupBy(v => v.YouTubeVideoId)
                .Select(g => g.First())
                .OrderByDescending(v => v.PublishedAt)
                .ToList();

            if (uniqueVideos.Count != allVideos.Count)
            {
                _logger.LogDebug("Removed {DuplicateCount} duplicate videos", allVideos.Count - uniqueVideos.Count);
            }

            return YouTubeApiResult<List<VideoInfo>>.Success(uniqueVideos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk channel updates for {ChannelCount} channels", channelRequests.Count);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "Failed to retrieve channel videos. Please try again later.");
        }
    }

    /// <summary>
    /// Gets recent videos from a single channel.
    /// Private helper method for bulk channel updates.
    /// </summary>
    private async Task<YouTubeApiResult<List<VideoInfo>>> GetChannelVideosAsync(ChannelUpdateRequest channelRequest)
    {
        try
        {
            // Check quota availability for search operation
            if (!await _quotaManager.CanPerformOperationAsync(YouTubeApiOperation.SearchVideos))
            {
                return YouTubeApiResult<List<VideoInfo>>.Failure(
                    "YouTube API quota insufficient for this operation.",
                    isQuotaExceeded: true);
            }

            await _rateLimitSemaphore.WaitAsync();

            try
            {
                // Search for videos from this specific channel
                var searchRequest = _youTubeClient.Search.List("snippet");
                searchRequest.ChannelId = channelRequest.YouTubeChannelId;
                searchRequest.Type = "video";
                searchRequest.Order = SearchResource.ListRequest.OrderEnum.Date; // Most recent first
                searchRequest.MaxResults = Math.Min(channelRequest.MaxResults, 50); // YouTube API limit
                searchRequest.PublishedAfter = channelRequest.LastCheckDate;
                searchRequest.Fields = "items(id/videoId,snippet(title,channelId,channelTitle,publishedAt,thumbnails/medium))";

                var searchResponse = await searchRequest.ExecuteAsync();
                await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.SearchVideos);

                if (!searchResponse.Items?.Any() == true)
                {
                    _logger.LogDebug("No recent videos found for channel {ChannelName} since {LastCheck}",
                        channelRequest.ChannelName, channelRequest.LastCheckDate);
                    return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());
                }

                // Get detailed video information in batches
                var videoIds = searchResponse.Items.Select(item => item.Id.VideoId).ToList();
                var videoDetails = await GetVideoDetailsBatchAsync(videoIds);

                // Combine search results with detailed information
                var videos = new List<VideoInfo>();

                foreach (var searchItem in searchResponse.Items)
                {
                    var details = videoDetails.FirstOrDefault(d => d.VideoId == searchItem.Id.VideoId);

                    var videoInfo = new VideoInfo
                    {
                        YouTubeVideoId = searchItem.Id.VideoId,
                        Title = searchItem.Snippet.Title,
                        ChannelId = searchItem.Snippet.ChannelId,
                        ChannelName = searchItem.Snippet.ChannelTitle,
                        PublishedAt = searchItem.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.MinValue,
                        ThumbnailUrl = searchItem.Snippet.Thumbnails?.Medium?.Url ?? string.Empty,
                        Description = string.Empty, // Not included in search snippet to save quota

                        // Add detailed statistics if available
                        ViewCount = details?.ViewCount ?? 0,
                        LikeCount = details?.LikeCount ?? 0,
                        CommentCount = details?.CommentCount ?? 0,
                        Duration = details?.Duration ?? 0
                    };

                    videos.Add(videoInfo);
                }

                _logger.LogDebug("Retrieved {VideoCount} videos from channel {ChannelName}",
                    videos.Count, channelRequest.ChannelName);

                return YouTubeApiResult<List<VideoInfo>>.Success(videos);
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Google.GoogleApiException ex) when (ex.Error?.Code == 403)
        {
            _logger.LogWarning("YouTube API quota exceeded while getting videos for channel {ChannelName}: {Message}",
                channelRequest.ChannelName, ex.Message);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "YouTube API quota exceeded. Please try again later.",
                isQuotaExceeded: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting videos for channel {ChannelName} ({ChannelId})",
                channelRequest.ChannelName, channelRequest.YouTubeChannelId);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                $"Failed to get videos from channel {channelRequest.ChannelName}. Please try again.");
        }
    }

    /// <summary>
    /// Gets detailed statistics for multiple videos in batches.
    /// Private helper method to efficiently retrieve video details.
    /// </summary>
    private async Task<List<VideoDetail>> GetVideoDetailsBatchAsync(List<string> videoIds)
    {
        if (!videoIds.Any()) return new List<VideoDetail>();

        try
        {
            var allDetails = new List<VideoDetail>();
            const int batchSize = 50; // YouTube API limit for videos.list

            // Process videos in batches
            for (int i = 0; i < videoIds.Count; i += batchSize)
            {
                var batch = videoIds.Skip(i).Take(batchSize).ToList();

                try
                {
                    var videoRequest = _youTubeClient.Videos.List("statistics,contentDetails");
                    videoRequest.Id = string.Join(",", batch);
                    videoRequest.Fields = "items(id,statistics(viewCount,likeCount,commentCount),contentDetails/duration)";

                    var videoResponse = await videoRequest.ExecuteAsync();
                    await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.GetVideoDetails); // Video details cost

                    if (videoResponse.Items?.Any() == true)
                    {
                        var batchDetails = videoResponse.Items.Select(video => new VideoDetail
                        {
                            VideoId = video.Id,
                            ViewCount = ParseLongSafely(video.Statistics?.ViewCount),
                            LikeCount = ParseLongSafely(video.Statistics?.LikeCount),
                            CommentCount = ParseLongSafely(video.Statistics?.CommentCount),
                            Duration = ParseIsoDuration(video.ContentDetails?.Duration)
                        }).ToList();

                        allDetails.AddRange(batchDetails);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get details for video batch starting at index {Index}", i);
                    // Continue with other batches
                }
            }

            return allDetails;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get video details for {VideoCount} videos", videoIds.Count);
            return new List<VideoDetail>();
        }
    }

    /// <summary>
    /// Helper class for video detail information.
    /// </summary>
    private class VideoDetail
    {
        public string VideoId { get; set; } = string.Empty;
        public int ViewCount { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int Duration { get; set; }
    }

    /// <summary>
    /// Safely parses a ulong? value from YouTube API statistics.
    /// </summary>
    private static int ParseLongSafely(ulong? value)
    {
        if (!value.HasValue)
            return 0;

        // Clamp to int range to avoid overflow
        return (int)Math.Min(value.Value, int.MaxValue);
    }

    /// <summary>
    /// Parses ISO 8601 duration format (PT4M13S) to seconds.
    /// </summary>
    private static int ParseIsoDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return 0;

        try
        {
            // YouTube uses ISO 8601 duration format: PT4M13S
            var timeSpan = System.Xml.XmlConvert.ToTimeSpan(duration);
            return (int)timeSpan.TotalSeconds;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Checks if the YouTube API is currently available and within quota limits.
    /// </summary>
    public async Task<bool> IsApiAvailableAsync()
    {
        return await _quotaManager.IsApiAvailableAsync();
    }

    /// <summary>
    /// Gets the estimated remaining API quota for today.
    /// </summary>
    public async Task<int> GetEstimatedRemainingQuotaAsync()
    {
        var status = await _quotaManager.GetQuotaStatusAsync();
        return status.Remaining;
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
            await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.GetChannelDetails);

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

    public void Dispose()
    {
        _youTubeClient?.Dispose();
        _rateLimitSemaphore?.Dispose();
    }
}
