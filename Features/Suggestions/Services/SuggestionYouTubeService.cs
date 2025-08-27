using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.YouTube.Models;
using TargetBrowse.Services;

namespace TargetBrowse.Features.Suggestions.Services;

/// <summary>
/// Enhanced implementation of YouTube API service for suggestion generation.
/// Provides comprehensive error handling, quota management, and performance optimization.
/// </summary>
public class SuggestionYouTubeService : ISuggestionYouTubeService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeApiSettings _settings;
    private readonly IYouTubeQuotaManager _quotaManager;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<SuggestionYouTubeService> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore;

    // Caching for performance optimization
    private readonly Dictionary<string, (DateTime CachedAt, VideoInfo Video)> _videoCache;
    private readonly Dictionary<string, (DateTime CachedAt, List<VideoInfo> Videos)> _searchCache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(15);

    // API endpoints
    private const string YOUTUBE_API_BASE = "https://www.googleapis.com/youtube/v3";

    public SuggestionYouTubeService(
        HttpClient httpClient,
        IOptions<YouTubeApiSettings> settings,
        IYouTubeQuotaManager quotaManager,
        IMessageCenterService messageCenterService,
        ILogger<SuggestionYouTubeService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _quotaManager = quotaManager ?? throw new ArgumentNullException(nameof(quotaManager));
        _messageCenterService = messageCenterService ?? throw new ArgumentNullException(nameof(messageCenterService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _rateLimitSemaphore = new SemaphoreSlim(3, 3); // Allow up to 3 concurrent requests
        _videoCache = new Dictionary<string, (DateTime, VideoInfo)>();
        _searchCache = new Dictionary<string, (DateTime, List<VideoInfo>)>();

        // Configure HTTP client timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Gets new videos from a channel since the specified date.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> GetChannelVideosSinceAsync(
        string youTubeChannelId, DateTime since, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(youTubeChannelId))
            return YouTubeApiResult<List<VideoInfo>>.Failure("Channel ID is required");

        maxResults = Math.Min(Math.Max(1, maxResults), 50); // Clamp between 1-50

        var stopwatch = Stopwatch.StartNew();
        const int quotaCost = 100; // Search API cost

        try
        {
            // Check quota availability
            if (!await _quotaManager.IsQuotaAvailableAsync(quotaCost))
            {
                await _messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.Date.AddDays(1));
                return YouTubeApiResult<List<VideoInfo>>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            // Check cache first
            var cacheKey = $"channel_{youTubeChannelId}_{since:yyyyMMddHHmm}_{maxResults}";
            if (TryGetFromCache(cacheKey, out List<VideoInfo>? cachedVideos))
            {
                _logger.LogDebug("Returning cached results for channel {ChannelId}", youTubeChannelId);
                return YouTubeApiResult<List<VideoInfo>>.Success(cachedVideos);
            }

            await _rateLimitSemaphore.WaitAsync();

            try
            {
                var publishedAfter = since.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var url = $"{YOUTUBE_API_BASE}/search" +
                          $"?channelId={Uri.EscapeDataString(youTubeChannelId)}" +
                          $"&publishedAfter={publishedAfter}" +
                          $"&part=snippet" +
                          $"&type=video" +
                          $"&order=date" +
                          $"&maxResults={maxResults}" +
                          $"&key={_settings.ApiKey}";

                var response = await _httpClient.GetAsync(url);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleApiErrorResponse(response, "channel video search", stopwatch.Elapsed, quotaCost);
                }

                var content = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(content);

                var videos = await ProcessSearchResults(searchResult, "ChannelSearch");

                // Cache the results
                CacheSearchResults(cacheKey, videos);

                // Record successful API call
                await _quotaManager.RecordApiCallAsync("ChannelSearch", quotaCost, stopwatch.Elapsed,
                    true, null, videos.Count);

                _logger.LogInformation("Found {Count} new videos from channel {ChannelId} since {Since}",
                    videos.Count, youTubeChannelId, since);

                return YouTubeApiResult<List<VideoInfo>>.Success(videos);
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            await _quotaManager.RecordApiCallAsync("ChannelSearch", 0, stopwatch.Elapsed,
                false, "Request timeout");

            _logger.LogWarning("Timeout getting channel videos for {ChannelId}", youTubeChannelId);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "Request timed out. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            await _quotaManager.RecordApiCallAsync("ChannelSearch", 0, stopwatch.Elapsed,
                false, ex.Message);

            _logger.LogError(ex, "Network error getting channel videos for {ChannelId}", youTubeChannelId);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "Network error occurred. Please check your internet connection and try again.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _quotaManager.RecordApiCallAsync("ChannelSearch", 0, stopwatch.Elapsed,
                false, ex.Message);

            _logger.LogError(ex, "Error getting channel videos for {ChannelId} since {Since}",
                youTubeChannelId, since);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "An unexpected error occurred. Please try again later.");
        }
    }

    /// <summary>
    /// Searches for videos across all of YouTube matching the specified topic.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> SearchVideosByTopicAsync(
        string topicQuery, DateTime? publishedAfter = null, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(topicQuery))
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

        maxResults = Math.Min(Math.Max(1, maxResults), 50);

        var stopwatch = Stopwatch.StartNew();
        const int quotaCost = 100;

        try
        {
            if (!await _quotaManager.IsQuotaAvailableAsync(quotaCost))
            {
                await _messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.Date.AddDays(1));
                return YouTubeApiResult<List<VideoInfo>>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            // Check cache
            var publishedAfterStr = publishedAfter?.ToString("yyyyMMddHHmm") ?? "all";
            var cacheKey = $"topic_{topicQuery.GetHashCode()}_{publishedAfterStr}_{maxResults}";
            if (TryGetFromCache(cacheKey, out List<VideoInfo>? cachedVideos))
            {
                _logger.LogDebug("Returning cached results for topic '{Topic}'", topicQuery);
                return YouTubeApiResult<List<VideoInfo>>.Success(cachedVideos);
            }

            await _rateLimitSemaphore.WaitAsync();

            try
            {
                var url = $"{YOUTUBE_API_BASE}/search" +
                          $"?q={Uri.EscapeDataString(topicQuery)}" +
                          $"&part=snippet" +
                          $"&type=video" +
                          $"&order=relevance" +
                          $"&maxResults={maxResults}" +
                          $"&key={_settings.ApiKey}";

                if (publishedAfter.HasValue)
                {
                    var publishedAfterParam = publishedAfter.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    url += $"&publishedAfter={publishedAfterParam}";
                }

                var response = await _httpClient.GetAsync(url);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleApiErrorResponse(response, "topic video search", stopwatch.Elapsed, quotaCost);
                }

                var content = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(content);

                var videos = await ProcessSearchResults(searchResult, "TopicSearch");

                // Cache the results
                CacheSearchResults(cacheKey, videos);

                await _quotaManager.RecordApiCallAsync("TopicSearch", quotaCost, stopwatch.Elapsed,
                    true, null, videos.Count);

                _logger.LogInformation("Found {Count} videos for topic '{Topic}'", videos.Count, topicQuery);

                return YouTubeApiResult<List<VideoInfo>>.Success(videos);
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _quotaManager.RecordApiCallAsync("TopicSearch", 0, stopwatch.Elapsed, false, ex.Message);

            _logger.LogError(ex, "Error searching videos by topic '{Topic}'", topicQuery);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "An unexpected error occurred during search. Please try again later.");
        }
    }

    /// <summary>
    /// Gets detailed information about multiple videos by their YouTube IDs.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> GetVideoDetailsByIdsAsync(List<string> youTubeVideoIds)
    {
        if (!youTubeVideoIds?.Any() == true)
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

        // Remove duplicates and invalid IDs
        var validVideoIds = youTubeVideoIds.Distinct().Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (!validVideoIds.Any())
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

        var allVideos = new List<VideoInfo>();
        var batches = validVideoIds.Chunk(50); // YouTube API allows up to 50 IDs per request
        var totalQuotaCost = Math.Max(1, (int)Math.Ceiling(validVideoIds.Count / 50.0));

        try
        {
            if (!await _quotaManager.IsQuotaAvailableAsync(totalQuotaCost))
            {
                await _messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.Date.AddDays(1));
                return YouTubeApiResult<List<VideoInfo>>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            foreach (var batch in batches)
            {
                var result = await GetVideoDetailsBatchAsync(batch.ToList());
                if (result.IsSuccess)
                {
                    allVideos.AddRange(result.Data ?? new List<VideoInfo>());
                }
                else if (result.IsQuotaExceeded)
                {
                    // If quota exceeded mid-batch, return what we have so far
                    break;
                }
                // Continue with other batches even if one fails (partial success)
            }

            _logger.LogInformation("Retrieved details for {Count} videos out of {Requested} requested",
                allVideos.Count, validVideoIds.Count);

            return YouTubeApiResult<List<VideoInfo>>.Success(allVideos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video details for {Count} videos", validVideoIds.Count);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "An unexpected error occurred while retrieving video details.");
        }
    }

    /// <summary>
    /// Performs bulk channel update checking for multiple channels.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> GetBulkChannelUpdatesAsync(
        List<ChannelUpdateRequest> channelUpdateRequests)
    {
        if (!channelUpdateRequests?.Any() == true)
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

        var allVideos = new List<VideoInfo>();
        var errors = new List<string>();

        _logger.LogInformation("Starting bulk channel update check for {Count} channels",
            channelUpdateRequests.Count);

        foreach (var request in channelUpdateRequests)
        {
            // Skip 1-star rated channels
            if (request.UserRating == 1)
            {
                _logger.LogDebug("Skipping 1-star rated channel {ChannelId}", request.YouTubeChannelId);
                continue;
            }

            var result = await GetChannelVideosSinceAsync(
                request.YouTubeChannelId,
                request.LastCheckDate,
                request.MaxResults);

            if (result.IsSuccess)
            {
                allVideos.AddRange(result.Data ?? new List<VideoInfo>());
                _logger.LogDebug("Found {Count} videos from channel {ChannelName}",
                    result.Data?.Count ?? 0, request.ChannelName);
            }
            else
            {
                errors.Add($"Failed to check {request.ChannelName}: {result.ErrorMessage}");

                // If quota exceeded, stop processing remaining channels
                if (result.IsQuotaExceeded)
                {
                    break;
                }
            }
        }

        // Log any errors encountered
        if (errors.Any())
        {
            _logger.LogWarning("Encountered errors during bulk channel update: {Errors}",
                string.Join("; ", errors));
        }

        _logger.LogInformation("Bulk channel update completed: {VideoCount} videos from {ChannelCount} channels",
            allVideos.Count, channelUpdateRequests.Count);

        return YouTubeApiResult<List<VideoInfo>>.Success(allVideos);
    }

    /// <summary>
    /// Performs bulk topic searches for multiple topics.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> GetBulkTopicSearchesAsync(
        List<string> topicQueries, DateTime? publishedAfter = null, int maxResultsPerTopic = 25)
    {
        if (!topicQueries?.Any() == true)
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

        var validTopics = topicQueries.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
        var allVideos = new List<VideoInfo>();
        var errors = new List<string>();

        _logger.LogInformation("Starting bulk topic search for {Count} topics", validTopics.Count);

        foreach (var topic in validTopics)
        {
            var result = await SearchVideosByTopicAsync(topic, publishedAfter, maxResultsPerTopic);

            if (result.IsSuccess)
            {
                allVideos.AddRange(result.Data ?? new List<VideoInfo>());
                _logger.LogDebug("Found {Count} videos for topic '{Topic}'",
                    result.Data?.Count ?? 0, topic);
            }
            else
            {
                errors.Add($"Failed to search topic '{topic}': {result.ErrorMessage}");

                // If quota exceeded, stop processing remaining topics
                if (result.IsQuotaExceeded)
                {
                    break;
                }
            }
        }

        // Remove duplicates (videos found via multiple topics)
        var uniqueVideos = allVideos
            .GroupBy(v => v.YouTubeVideoId)
            .Select(g => g.First())
            .ToList();

        if (errors.Any())
        {
            _logger.LogWarning("Encountered errors during bulk topic search: {Errors}",
                string.Join("; ", errors));
        }

        _logger.LogInformation("Bulk topic search completed: {UniqueVideoCount} unique videos from {TopicCount} topics",
            uniqueVideos.Count, validTopics.Count);

        return YouTubeApiResult<List<VideoInfo>>.Success(uniqueVideos);
    }

    /// <summary>
    /// Gets API availability status.
    /// </summary>
    public async Task<ApiAvailabilityResult> GetApiAvailabilityAsync()
    {
        return await _quotaManager.GetApiAvailabilityAsync();
    }

    /// <summary>
    /// Gets estimated API quota cost for suggestion operations.
    /// </summary>
    public async Task<QuotaCostEstimate> EstimateQuotaCostAsync(
        int channelCount, int topicCount, int estimatedVideosFound = 100)
    {
        return await _quotaManager.EstimateSuggestionCostAsync(channelCount, topicCount, estimatedVideosFound);
    }

    /// <summary>
    /// Gets current API usage statistics.
    /// </summary>
    public async Task<SuggestionApiUsage> GetCurrentApiUsageAsync()
    {
        return await _quotaManager.GetUsageStatisticsAsync();
    }

    /// <summary>
    /// Validates that YouTube video IDs exist.
    /// </summary>
    public async Task<YouTubeApiResult<Dictionary<string, bool>>> ValidateVideoIdsAsync(List<string> youTubeVideoIds)
    {
        if (!youTubeVideoIds?.Any() == true)
            return YouTubeApiResult<Dictionary<string, bool>>.Success(new Dictionary<string, bool>());

        var validationResult = new Dictionary<string, bool>();
        var batches = youTubeVideoIds.Distinct().Chunk(50);

        foreach (var batch in batches)
        {
            var result = await GetVideoDetailsByIdsAsync(batch.ToList());

            if (result.IsSuccess)
            {
                var foundVideoIds = new HashSet<string>(result.Data?.Select(v => v.YouTubeVideoId) ?? Enumerable.Empty<string>());

                foreach (var videoId in batch)
                {
                    validationResult[videoId] = foundVideoIds.Contains(videoId);
                }
            }
            else
            {
                // Mark all in this batch as unknown/failed
                foreach (var videoId in batch)
                {
                    validationResult[videoId] = false;
                }

                if (result.IsQuotaExceeded)
                    break;
            }
        }

        return YouTubeApiResult<Dictionary<string, bool>>.Success(validationResult);
    }

    /// <summary>
    /// Searches for videos within specific channels matching a topic.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> SearchTopicInChannelsAsync(
        string topicQuery, List<string> youTubeChannelIds, DateTime? publishedAfter = null, int maxResults = 25)
    {
        if (string.IsNullOrWhiteSpace(topicQuery) || !youTubeChannelIds?.Any() == true)
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

        var allVideos = new List<VideoInfo>();
        var maxPerChannel = Math.Max(1, maxResults / youTubeChannelIds.Count);

        // Limit to prevent quota exhaustion
        var channelsToSearch = youTubeChannelIds.Take(10).ToList();

        foreach (var channelId in channelsToSearch)
        {
            try
            {
                var url = $"{YOUTUBE_API_BASE}/search" +
                          $"?channelId={Uri.EscapeDataString(channelId)}" +
                          $"&q={Uri.EscapeDataString(topicQuery)}" +
                          $"&part=snippet" +
                          $"&type=video" +
                          $"&order=relevance" +
                          $"&maxResults={maxPerChannel}" +
                          $"&key={_settings.ApiKey}";

                if (publishedAfter.HasValue)
                {
                    var publishedAfterStr = publishedAfter.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    url += $"&publishedAfter={publishedAfterStr}";
                }

                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var searchResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(content);

                    var videos = await ProcessSearchResults(searchResult, "TopicInChannel");
                    allVideos.AddRange(videos);

                    await _quotaManager.RecordQuotaUsageAsync(100, "TopicInChannel");
                }
                else
                {
                    _logger.LogWarning("API error searching topic in channel {ChannelId}: {StatusCode}",
                        channelId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching topic '{Topic}' in channel {ChannelId}", topicQuery, channelId);
            }
        }

        // Get detailed video information
        if (allVideos.Any())
        {
            var videoIds = allVideos.Select(v => v.YouTubeVideoId).ToList();
            var detailsResult = await GetVideoDetailsByIdsAsync(videoIds);

            if (detailsResult.IsSuccess)
            {
                // Merge detailed information
                var detailedVideos = detailsResult.Data ?? new List<VideoInfo>();
                var detailsLookup = detailedVideos.ToDictionary(v => v.YouTubeVideoId);

                foreach (var video in allVideos)
                {
                    if (detailsLookup.TryGetValue(video.YouTubeVideoId, out var detailed))
                    {
                        video.Duration = detailed.Duration;
                        video.ViewCount = detailed.ViewCount;
                        video.LikeCount = detailed.LikeCount;
                        video.CommentCount = detailed.CommentCount;
                    }
                }
            }
        }

        var uniqueResults = allVideos.Take(maxResults).ToList();

        _logger.LogInformation("Found {Count} videos for topic '{Topic}' in {ChannelCount} channels",
            uniqueResults.Count, topicQuery, channelsToSearch.Count);

        return YouTubeApiResult<List<VideoInfo>>.Success(uniqueResults);
    }

    /// <summary>
    /// Preloads video details for optimization.
    /// </summary>
    public async Task<bool> PreloadVideoDetailsAsync(List<VideoInfo> videoInfoList)
    {
        if (!videoInfoList?.Any() == true)
            return true;

        try
        {
            var videoIds = videoInfoList
                .Where(v => !string.IsNullOrWhiteSpace(v.YouTubeVideoId))
                .Select(v => v.YouTubeVideoId)
                .Distinct()
                .ToList();

            if (!videoIds.Any())
                return true;

            var result = await GetVideoDetailsByIdsAsync(videoIds);

            _logger.LogInformation("Preloaded details for {Count} videos, success: {Success}",
                videoIds.Count, result.IsSuccess);

            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preloading video details");
            return false;
        }
    }

    /// <summary>
    /// Gets current quota status.
    /// </summary>
    public async Task<QuotaStatus> GetQuotaStatusAsync()
    {
        return await _quotaManager.GetQuotaStatusAsync();
    }

    #region Private Helper Methods

    /// <summary>
    /// Processes search results from YouTube API and enriches with video details.
    /// </summary>
    private async Task<List<VideoInfo>> ProcessSearchResults(YouTubeSearchResponse? searchResult, string operationType)
    {
        if (searchResult?.Items == null || !searchResult.Items.Any())
            return new List<VideoInfo>();

        // Convert search results to VideoInfo objects
        var videos = searchResult.Items
            .Where(item => !string.IsNullOrEmpty(item.Id?.VideoId))
            .Select(item => new VideoInfo
            {
                YouTubeVideoId = item.Id?.VideoId ?? string.Empty,
                Title = item.Snippet?.Title ?? string.Empty,
                ChannelId = item.Snippet?.ChannelId ?? string.Empty,
                ChannelName = item.Snippet?.ChannelTitle ?? string.Empty,
                PublishedAt = item.Snippet?.PublishedAt ?? DateTime.UtcNow,
                ThumbnailUrl = item.Snippet?.Thumbnails?.Medium?.Url ?? string.Empty,
                Description = item.Snippet?.Description ?? string.Empty
            })
            .ToList();

        // Get detailed video information (duration, view counts, etc.)
        if (videos.Any())
        {
            var videoIds = videos.Select(v => v.YouTubeVideoId).ToList();
            var detailsResult = await GetVideoDetailsByIdsAsync(videoIds);

            if (detailsResult.IsSuccess)
            {
                var detailedVideos = detailsResult.Data ?? new List<VideoInfo>();
                var detailsLookup = detailedVideos.ToDictionary(v => v.YouTubeVideoId);

                // Merge the detailed information
                foreach (var video in videos)
                {
                    if (detailsLookup.TryGetValue(video.YouTubeVideoId, out var detailed))
                    {
                        video.Duration = detailed.Duration;
                        video.ViewCount = detailed.ViewCount;
                        video.LikeCount = detailed.LikeCount;
                        video.CommentCount = detailed.CommentCount;
                    }
                }
            }
            else
            {
                _logger.LogWarning("Failed to get detailed video information for {OperationType}: {Error}",
                    operationType, detailsResult.ErrorMessage);
            }
        }

        return videos;
    }

    /// <summary>
    /// Gets video details for a single batch of video IDs.
    /// </summary>
    private async Task<YouTubeApiResult<List<VideoInfo>>> GetVideoDetailsBatchAsync(List<string> videoIds)
    {
        if (!videoIds.Any())
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

        var stopwatch = Stopwatch.StartNew();
        const int quotaCost = 1;

        try
        {
            await _rateLimitSemaphore.WaitAsync();

            try
            {
                var videoIdsParam = string.Join(",", videoIds);
                var url = $"{YOUTUBE_API_BASE}/videos" +
                          $"?id={videoIdsParam}" +
                          $"&part=snippet,statistics,contentDetails" +
                          $"&key={_settings.ApiKey}";

                var response = await _httpClient.GetAsync(url);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleApiErrorResponse(response, "video details", stopwatch.Elapsed, quotaCost);
                }

                var content = await response.Content.ReadAsStringAsync();
                var videosResult = JsonSerializer.Deserialize<YouTubeVideosResponse>(content);

                var videos = new List<VideoInfo>();

                if (videosResult?.Items != null)
                {
                    foreach (var item in videosResult.Items)
                    {
                        var video = new VideoInfo
                        {
                            YouTubeVideoId = item.Id ?? string.Empty,
                            Title = item.Snippet?.Title ?? string.Empty,
                            ChannelId = item.Snippet?.ChannelId ?? string.Empty,
                            ChannelName = item.Snippet?.ChannelTitle ?? string.Empty,
                            PublishedAt = item.Snippet?.PublishedAt ?? DateTime.UtcNow,
                            ThumbnailUrl = item.Snippet?.Thumbnails?.Medium?.Url ?? string.Empty,
                            Description = item.Snippet?.Description ?? string.Empty,
                            ViewCount = int.TryParse(item.Statistics?.ViewCount, out var views) ? views : 0,
                            LikeCount = int.TryParse(item.Statistics?.LikeCount, out var likes) ? likes : 0,
                            CommentCount = int.TryParse(item.Statistics?.CommentCount, out var comments) ? comments : 0,
                            Duration = ParseDuration(item.ContentDetails?.Duration ?? string.Empty)
                        };

                        videos.Add(video);

                        // Cache individual video details
                        CacheVideoDetails(video);
                    }
                }

                await _quotaManager.RecordApiCallAsync("VideoDetails", quotaCost, stopwatch.Elapsed,
                    true, null, videos.Count);

                return YouTubeApiResult<List<VideoInfo>>.Success(videos);
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _quotaManager.RecordApiCallAsync("VideoDetails", 0, stopwatch.Elapsed, false, ex.Message);

            _logger.LogError(ex, "Error getting video details for batch of {Count} videos", videoIds.Count);
            return YouTubeApiResult<List<VideoInfo>>.Failure("Failed to retrieve video details");
        }
    }

    /// <summary>
    /// Handles API error responses with comprehensive error reporting.
    /// </summary>
    private async Task<YouTubeApiResult<List<VideoInfo>>> HandleApiErrorResponse(
        HttpResponseMessage response, string operation, TimeSpan duration, int quotaCost)
    {
        var content = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            await _quotaManager.RecordApiCallAsync(operation, 0, duration, false, "Quota exceeded");
            await _messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.Date.AddDays(1));

            _logger.LogWarning("YouTube API quota exceeded during {Operation}", operation);

            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "YouTube API quota exceeded for today. Please try again tomorrow.",
                isQuotaExceeded: true);
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            await _quotaManager.RecordApiCallAsync(operation, 0, duration, false, "Bad request");

            _logger.LogError("YouTube API bad request during {Operation}: {Content}", operation, content);

            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "Invalid request parameters. Please try again.");
        }
        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await _quotaManager.RecordApiCallAsync(operation, 0, duration, false, "Unauthorized");

            _logger.LogError("YouTube API unauthorized during {Operation}: Invalid API key", operation);

            await _messageCenterService.ShowErrorAsync("YouTube API key is invalid or expired. Please check configuration.");

            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "Authentication failed. Please check API key configuration.");
        }
        else
        {
            await _quotaManager.RecordApiCallAsync(operation, 0, duration, false,
                $"HTTP {response.StatusCode}");

            _logger.LogError("YouTube API error during {Operation}: {StatusCode} - {Content}",
                operation, response.StatusCode, content);

            await _messageCenterService.ShowErrorAsync($"YouTube API error during {operation}. Please try again later.");

            return YouTubeApiResult<List<VideoInfo>>.Failure(
                $"YouTube API returned error {response.StatusCode}. Please try again later.");
        }
    }

    /// <summary>
    /// Parses YouTube duration format (PT4M13S) to seconds.
    /// </summary>
    private int ParseDuration(string duration)
    {
        try
        {
            if (string.IsNullOrEmpty(duration) || !duration.StartsWith("PT"))
                return 0;

            var timeSpan = System.Xml.XmlConvert.ToTimeSpan(duration);
            return (int)timeSpan.TotalSeconds;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to parse duration '{Duration}': {Error}", duration, ex.Message);
            return 0;
        }
    }

    /// <summary>
    /// Tries to get cached search results.
    /// </summary>
    private bool TryGetFromCache<T>(string cacheKey, out T? cachedData) where T : class
    {
        cachedData = null;

        try
        {
            if (_searchCache.TryGetValue(cacheKey, out var cached) &&
                DateTime.UtcNow - cached.CachedAt < _cacheExpiry)
            {
                cachedData = cached.Videos as T;
                return cachedData != null;
            }

            // Clean expired cache entries
            var expiredKeys = _searchCache
                .Where(kvp => DateTime.UtcNow - kvp.Value.CachedAt >= _cacheExpiry)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var expiredKey in expiredKeys)
            {
                _searchCache.Remove(expiredKey);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error accessing cache for key '{CacheKey}': {Error}", cacheKey, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Caches search results.
    /// </summary>
    private void CacheSearchResults(string cacheKey, List<VideoInfo> videos)
    {
        try
        {
            _searchCache[cacheKey] = (DateTime.UtcNow, videos);

            // Limit cache size to prevent memory issues
            if (_searchCache.Count > 100)
            {
                var oldestKey = _searchCache.OrderBy(kvp => kvp.Value.CachedAt).First().Key;
                _searchCache.Remove(oldestKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error caching search results for key '{CacheKey}': {Error}", cacheKey, ex.Message);
        }
    }

    /// <summary>
    /// Caches individual video details.
    /// </summary>
    private void CacheVideoDetails(VideoInfo video)
    {
        try
        {
            _videoCache[video.YouTubeVideoId] = (DateTime.UtcNow, video);

            // Limit cache size
            if (_videoCache.Count > 500)
            {
                var oldestKey = _videoCache.OrderBy(kvp => kvp.Value.CachedAt).First().Key;
                _videoCache.Remove(oldestKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error caching video details for '{VideoId}': {Error}",
                video.YouTubeVideoId, ex.Message);
        }
    }

    #endregion

    public void Dispose()
    {
        _rateLimitSemaphore?.Dispose();
        _httpClient?.Dispose();
    }
}

// YouTube API response models
public class YouTubeSearchResponse
{
    public List<YouTubeSearchItem>? Items { get; set; }
}

public class YouTubeSearchItem
{
    public YouTubeSearchId? Id { get; set; }
    public YouTubeSnippet? Snippet { get; set; }
}

public class YouTubeSearchId
{
    public string? VideoId { get; set; }
}

public class YouTubeVideosResponse
{
    public List<YouTubeVideoItem>? Items { get; set; }
}

public class YouTubeVideoItem
{
    public string? Id { get; set; }
    public YouTubeSnippet? Snippet { get; set; }
    public YouTubeStatistics? Statistics { get; set; }
    public YouTubeContentDetails? ContentDetails { get; set; }
}

public class YouTubeSnippet
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ChannelId { get; set; }
    public string? ChannelTitle { get; set; }
    public DateTime PublishedAt { get; set; }
    public YouTubeThumbnails? Thumbnails { get; set; }
}

public class YouTubeStatistics
{
    public string? ViewCount { get; set; }
    public string? LikeCount { get; set; }
    public string? CommentCount { get; set; }
}

public class YouTubeContentDetails
{
    public string? Duration { get; set; }
}

public class YouTubeThumbnails
{
    public YouTubeThumbnail? Medium { get; set; }
}

public class YouTubeThumbnail
{
    public string? Url { get; set; }
}