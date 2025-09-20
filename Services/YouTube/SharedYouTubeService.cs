using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Services.YouTube;

/// <summary>
/// Shared YouTube API service implementation providing common functionality across features.
/// Extracted from SuggestionYouTubeService to support Vertical Slice Architecture.
/// Uses the standard IYouTubeQuotaManager interface for quota management.
/// </summary>
public class SharedYouTubeService : ISharedYouTubeService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeApiSettings _settings;
    private readonly IYouTubeQuotaManager _quotaManager;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<SharedYouTubeService> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore;

    // Caching for performance optimization
    private readonly Dictionary<string, (DateTime CachedAt, VideoInfo Video)> _videoCache;
    private readonly Dictionary<string, (DateTime CachedAt, List<VideoInfo> Videos)> _searchCache;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(15);

    // API endpoints
    private const string YOUTUBE_API_BASE = "https://www.googleapis.com/youtube/v3";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SharedYouTubeService(
        HttpClient httpClient,
        IOptions<YouTubeApiSettings> settings,
        IYouTubeQuotaManager quotaManager,
        IMessageCenterService messageCenterService,
        ILogger<SharedYouTubeService> logger)
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
    /// Currently returns a mix of medium and long duration videos for diversity.
    /// Max results is clamped between 1-100. 50 from medium and 50 from long duration searches.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> GetChannelVideosSinceAsync(
        string youTubeChannelId, DateTime since, int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(youTubeChannelId))
            return YouTubeApiResult<List<VideoInfo>>.Failure("Channel ID is required");

        // Clamp between 1-100
        maxResults = Math.Min(Math.Max(1, maxResults), 100); 

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Check quota availability for search operations (we'll do 2 searches)
            if (!await _quotaManager.CanPerformOperationAsync(YouTubeApiOperation.SearchVideos, 2))
            {
                await _messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.Date.AddDays(1));
                return YouTubeApiResult<List<VideoInfo>>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            // Check cache first
            var cacheKey = $"channel_comprehensive_{youTubeChannelId}_{since:yyyyMMddHHmm}_{maxResults}";
            if (TryGetFromCache(cacheKey, out List<VideoInfo>? cachedVideos))
            {
                _logger.LogDebug("Returning cached comprehensive results for channel {ChannelId}", youTubeChannelId);
                return YouTubeApiResult<List<VideoInfo>>.Success(cachedVideos);
            }

            await _rateLimitSemaphore.WaitAsync();

            try
            {
                var publishedAfter = since.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var halfResults = Math.Max(1, maxResults / 2); // Split quota between calls

                // Base URL for both searches
                var baseUrl = $"{YOUTUBE_API_BASE}/search" +
                              $"?channelId={Uri.EscapeDataString(youTubeChannelId)}" +
                              $"&publishedAfter={publishedAfter}" +
                              $"&part=snippet" +
                              $"&type=video" +
                              $"&order=date" +
                              $"&key={_settings.ApiKey}";

                var allVideos = new List<VideoInfo>();

                // Search 1: Medium duration videos (4-20 minutes)
                var mediumUrl = baseUrl + $"&videoDuration=medium&maxResults={halfResults}";
                _logger.LogDebug("Searching medium duration videos for channel {ChannelId}", youTubeChannelId);

                // Consume quota for this operation
                if (await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.SearchVideos))
                {
                    var mediumResponse = await _httpClient.GetAsync(mediumUrl);

                    if (mediumResponse.IsSuccessStatusCode)
                    {
                        var mediumContent = await mediumResponse.Content.ReadAsStringAsync();
                        var mediumResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(mediumContent, JsonOptions);

                        if (mediumResult != null)
                        {
                            var mediumVideos = await ProcessSearchResults(mediumResult, "ChannelSearch-Medium");
                            foreach (var video in mediumVideos)
                            {
                                video.DurationCategory = "Medium";
                            }
                            allVideos.AddRange(mediumVideos);
                        }
                    }
                }

                // Search 2: Long duration videos (20+ minutes)
                var longUrl = baseUrl + $"&videoDuration=long&maxResults={halfResults}";
                _logger.LogDebug("Searching long duration videos for channel {ChannelId}", youTubeChannelId);

                // Consume quota for this operation
                if (await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.SearchVideos))
                {
                    var longResponse = await _httpClient.GetAsync(longUrl);

                    if (longResponse.IsSuccessStatusCode)
                    {
                        var longContent = await longResponse.Content.ReadAsStringAsync();
                        var longResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(longContent, JsonOptions);

                        if (longResult != null)
                        {
                            var longVideos = await ProcessSearchResults(longResult, "ChannelSearch-Long");
                            foreach (var video in longVideos)
                            {
                                video.DurationCategory = "Long";
                            }
                            allVideos.AddRange(longVideos);
                        }
                    }
                }

                // Combine results and remove duplicates
                var uniqueVideos = allVideos
                    .GroupBy(v => v.YouTubeVideoId)
                    .Select(g => g.OrderByDescending(v => v.DurationCategory == "Long" ? 1 : 0).First())
                    .OrderByDescending(v => v.PublishedAt)
                    .Take(maxResults)
                    .ToList();

                stopwatch.Stop();

                // Cache the results
                CacheSearchResults(cacheKey, uniqueVideos);

                _logger.LogInformation("Channel search for {ChannelId}: {Count} unique videos found",
                    youTubeChannelId, uniqueVideos.Count);

                return YouTubeApiResult<List<VideoInfo>>.Success(uniqueVideos);
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during channel search for {ChannelId} since {Since}",
                youTubeChannelId, since);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "An unexpected error occurred. Please try again later.");
        }
    }

    /// <summary>
    /// Searches for videos across all of YouTube matching the specified topic.
    /// Currently returns a mix of medium and long duration videos for diversity.
    /// Max results is clamped between 1-100. 50 from medium and 50 from long duration searches.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> SearchVideosByTopicAsync(
        string topicQuery, DateTime? publishedAfter = null, int maxResults = 100)
    {
        if (string.IsNullOrWhiteSpace(topicQuery))
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

        // Clamping max results between 1 and 100
        maxResults = Math.Min(Math.Max(1, maxResults), 100);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Check quota availability for search operations (we'll do 2 searches)
            if (!await _quotaManager.CanPerformOperationAsync(YouTubeApiOperation.SearchVideos, 2))
            {
                await _messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.Date.AddDays(1));
                return YouTubeApiResult<List<VideoInfo>>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            // Check cache
            var publishedAfterStr = publishedAfter?.ToString("yyyyMMddHHmm") ?? "all";
            var cacheKey = $"topic_comprehensive_{topicQuery.GetHashCode()}_{publishedAfterStr}_{maxResults}";
            if (TryGetFromCache(cacheKey, out List<VideoInfo>? cachedVideos))
            {
                _logger.LogDebug("Returning cached comprehensive results for topic '{Topic}'", topicQuery);
                return YouTubeApiResult<List<VideoInfo>>.Success(cachedVideos);
            }

            await _rateLimitSemaphore.WaitAsync();

            try
            {
                var halfResults = Math.Max(1, maxResults / 2);

                // Base URL for both searches
                var baseUrl = $"{YOUTUBE_API_BASE}/search" +
                              $"?q={Uri.EscapeDataString(topicQuery)}" +
                              $"&part=snippet" +
                              $"&type=video" +
                              $"&order=relevance" +
                              $"&key={_settings.ApiKey}";

                if (publishedAfter.HasValue)
                {
                    var publishedAfterParam = publishedAfter.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    baseUrl += $"&publishedAfter={publishedAfterParam}";
                }

                var allVideos = new List<VideoInfo>();

                // Search 1: Medium duration videos (4-20 minutes)
                var mediumUrl = baseUrl + $"&videoDuration=medium&maxResults={halfResults}";

                if (await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.SearchVideos))
                {
                    var mediumResponse = await _httpClient.GetAsync(mediumUrl);

                    if (mediumResponse.IsSuccessStatusCode)
                    {
                        var mediumContent = await mediumResponse.Content.ReadAsStringAsync();
                        var mediumResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(mediumContent, JsonOptions);

                        if (mediumResult != null)
                        {
                            var mediumVideos = await ProcessSearchResults(mediumResult, "TopicSearch-Medium");
                            foreach (var video in mediumVideos)
                            {
                                video.DurationCategory = "Medium";
                            }
                            allVideos.AddRange(mediumVideos);
                        }
                    }
                }

                // Search 2: Long duration videos (20+ minutes)
                var longUrl = baseUrl + $"&videoDuration=long&maxResults={halfResults}";

                if (await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.SearchVideos))
                {
                    var longResponse = await _httpClient.GetAsync(longUrl);

                    if (longResponse.IsSuccessStatusCode)
                    {
                        var longContent = await longResponse.Content.ReadAsStringAsync();
                        var longResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(longContent, JsonOptions);

                        if (longResult != null)
                        {
                            var longVideos = await ProcessSearchResults(longResult, "TopicSearch-Long");
                            foreach (var video in longVideos)
                            {
                                video.DurationCategory = "Long";
                            }
                            allVideos.AddRange(longVideos);
                        }
                    }
                }

                // Combine and deduplicate
                var uniqueVideos = allVideos
                    .GroupBy(v => v.YouTubeVideoId)
                    .Select(g => g.OrderByDescending(v => v.DurationCategory == "Long" ? 1 : 0).First())
                    .Take(maxResults)
                    .ToList();

                stopwatch.Stop();

                // Cache the results
                CacheSearchResults(cacheKey, uniqueVideos);

                _logger.LogInformation("Topic search '{Topic}': {Count} unique videos found",
                    topicQuery, uniqueVideos.Count);

                return YouTubeApiResult<List<VideoInfo>>.Success(uniqueVideos);
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error during topic search '{Topic}'", topicQuery);
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

        var validVideoIds = youTubeVideoIds.Distinct().Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        if (!validVideoIds.Any())
            return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

        var allVideos = new List<VideoInfo>();
        var batches = validVideoIds.Chunk(50); // YouTube API allows up to 50 IDs per request

        try
        {
            foreach (var batch in batches)
            {
                var result = await GetVideoDetailsBatchAsync(batch.ToList());
                if (result.IsSuccess)
                {
                    allVideos.AddRange(result.Data ?? new List<VideoInfo>());
                }
                else if (result.IsQuotaExceeded)
                {
                    break; // Stop if quota exceeded
                }
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
    /// Validates that multiple YouTube video IDs exist and are accessible.
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
    /// Gets API availability status.
    /// </summary>
    public async Task<ApiAvailabilityResult> GetApiAvailabilityAsync()
    {
        var isAvailable = await _quotaManager.IsApiAvailableAsync();
        var quotaStatus = await _quotaManager.GetQuotaStatusAsync();

        return new ApiAvailabilityResult
        {
            IsAvailable = isAvailable,
            IsQuotaExceeded = quotaStatus.IsExhausted,
            QuotaUsagePercentage = quotaStatus.UsagePercentage,
            EstimatedRemainingQuota = quotaStatus.Remaining,
            QuotaResetTime = quotaStatus.NextReset,
            ErrorMessage = quotaStatus.IsExhausted ? "Daily quota exceeded" : null,
            IsApiKeyValid = !string.IsNullOrEmpty(_settings.ApiKey)
        };
    }

    /// <summary>
    /// Gets quota reset time and remaining quota for planning future requests.
    /// </summary>
    public async Task<QuotaStatus> GetQuotaStatusAsync()
    {
        var quotaStatus = await _quotaManager.GetQuotaStatusAsync();

        return new QuotaStatus
        {
            DailyQuotaLimit = quotaStatus.DailyLimit,
            QuotaUsedToday = quotaStatus.Used,
            ResetTime = quotaStatus.NextReset
        };
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

        try
        {
            // Check if we can perform this operation
            if (!await _quotaManager.CanPerformOperationAsync(YouTubeApiOperation.GetVideoDetails))
            {
                return YouTubeApiResult<List<VideoInfo>>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            await _rateLimitSemaphore.WaitAsync();

            try
            {
                var videoIdsParam = string.Join(",", videoIds);
                var url = $"{YOUTUBE_API_BASE}/videos" +
                          $"?id={videoIdsParam}" +
                          $"&part=snippet,statistics,contentDetails" +
                          $"&key={_settings.ApiKey}";

                // Consume quota for this operation
                if (!await _quotaManager.TryConsumeQuotaAsync(YouTubeApiOperation.GetVideoDetails))
                {
                    return YouTubeApiResult<List<VideoInfo>>.Failure(
                        "YouTube API quota exceeded for today. Please try again tomorrow.",
                        isQuotaExceeded: true);
                }

                var response = await _httpClient.GetAsync(url);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    return await HandleApiErrorResponse(response, "video details", stopwatch.Elapsed);
                }

                var content = await response.Content.ReadAsStringAsync();
                var videosResult = JsonSerializer.Deserialize<YouTubeVideosResponse>(content, JsonOptions);

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
                            Duration = DurationParser.ParseToSeconds(item.ContentDetails?.Duration ?? string.Empty)
                        };

                        videos.Add(video);

                        // Cache individual video details
                        CacheVideoDetails(video);
                    }
                }

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
            _logger.LogError(ex, "Error getting video details for batch of {Count} videos", videoIds.Count);
            return YouTubeApiResult<List<VideoInfo>>.Failure("Failed to retrieve video details");
        }
    }

    /// <summary>
    /// Handles API error responses with comprehensive error reporting.
    /// </summary>
    private async Task<YouTubeApiResult<List<VideoInfo>>> HandleApiErrorResponse(
        HttpResponseMessage response, string operation, TimeSpan duration)
    {
        var content = await response.Content.ReadAsStringAsync();

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            await _messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.Date.AddDays(1));

            _logger.LogWarning("YouTube API quota exceeded during {Operation}", operation);

            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "YouTube API quota exceeded for today. Please try again tomorrow.",
                isQuotaExceeded: true);
        }
        else
        {
            _logger.LogError("YouTube API error during {Operation}: {StatusCode} - {Content}",
                operation, response.StatusCode, content);

            await _messageCenterService.ShowErrorAsync($"YouTube API error during {operation}. Please try again later.");

            return YouTubeApiResult<List<VideoInfo>>.Failure(
                $"YouTube API returned error {response.StatusCode}. Please try again later.");
        }
    }

    /// <summary>
    /// Parses YouTube duration format (PT4M13S) to seconds.
    /// Moved to DurationParser.cs
    /// </summary>
    //private int ParseDuration(string duration)
    //{
    //    try
    //    {
    //        if (string.IsNullOrEmpty(duration) || !duration.StartsWith("PT"))
    //            return 0;

    //        var timeSpan = System.Xml.XmlConvert.ToTimeSpan(duration);
    //        return (int)timeSpan.TotalSeconds;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogDebug("Failed to parse duration '{Duration}': {Error}", duration, ex.Message);
    //        return 0;
    //    }
    //}

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

// YouTube API response models for deserialization
// These are different from the application models in Services/YouTube/Models/
// These represent the raw API response structure from Google's YouTube API
public class YouTubeSearchResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeSearchItem>? Items { get; set; }

    [JsonPropertyName("pageInfo")]
    public YouTubePageInfo? PageInfo { get; set; }
}

public class YouTubePageInfo
{
    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    [JsonPropertyName("resultsPerPage")]
    public int ResultsPerPage { get; set; }
}

public class YouTubeSearchItem
{
    [JsonPropertyName("id")]
    public YouTubeSearchId? Id { get; set; }

    [JsonPropertyName("snippet")]
    public YouTubeSnippet? Snippet { get; set; }
}

public class YouTubeSearchId
{
    [JsonPropertyName("videoId")]
    public string? VideoId { get; set; }
}

public class YouTubeVideosResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeVideoItem>? Items { get; set; }
}

public class YouTubeVideoItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("snippet")]
    public YouTubeSnippet? Snippet { get; set; }

    [JsonPropertyName("statistics")]
    public YouTubeStatistics? Statistics { get; set; }

    [JsonPropertyName("contentDetails")]
    public YouTubeContentDetails? ContentDetails { get; set; }
}

public class YouTubeSnippet
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("channelId")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("channelTitle")]
    public string? ChannelTitle { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("thumbnails")]
    public YouTubeThumbnails? Thumbnails { get; set; }

    [JsonPropertyName("liveBroadcastContent")]
    public string? LiveBroadcastContent { get; set; }

    [JsonPropertyName("publishTime")]
    public DateTime? PublishTime { get; set; }
}

public class YouTubeStatistics
{
    [JsonPropertyName("viewCount")]
    public string? ViewCount { get; set; }

    [JsonPropertyName("likeCount")]
    public string? LikeCount { get; set; }

    [JsonPropertyName("commentCount")]
    public string? CommentCount { get; set; }
}

public class YouTubeContentDetails
{
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
}

public class YouTubeThumbnails
{
    [JsonPropertyName("default")]
    public YouTubeThumbnail? Default { get; set; }

    [JsonPropertyName("medium")]
    public YouTubeThumbnail? Medium { get; set; }

    [JsonPropertyName("high")]
    public YouTubeThumbnail? High { get; set; }
}

public class YouTubeThumbnail
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}