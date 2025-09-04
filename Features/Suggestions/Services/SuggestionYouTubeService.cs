using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services;
using TargetBrowse.Services.YouTube;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Suggestions.Services;

/// <summary>
/// Enhanced implementation of YouTube API service for suggestion generation.
/// Provides comprehensive error handling, quota management, and performance optimization.
/// </summary>
public class SuggestionYouTubeService : ISuggestionYouTubeService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly YouTubeApiSettings _settings;
    private readonly ISuggestionQuotaManager _quotaManager;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<SuggestionYouTubeService> _logger;
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

    public SuggestionYouTubeService(
        HttpClient httpClient,
        IOptions<YouTubeApiSettings> settings,
        ISuggestionQuotaManager quotaManager,
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
        const int quotaCostPerCall = 100; // Each search API call costs 100 quota
        const int totalQuotaCost = quotaCostPerCall * 2; // We make 2 calls

        try
        {
            // Check quota availability for both calls
            if (!await _quotaManager.IsQuotaAvailableAsync(totalQuotaCost))
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

                var mediumResponse = await _httpClient.GetAsync(mediumUrl);

                if (mediumResponse.IsSuccessStatusCode)
                {
                    var mediumContent = await mediumResponse.Content.ReadAsStringAsync();

                    try
                    {
                        var mediumResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(mediumContent, JsonOptions);

                        if (mediumResult != null)
                        {
                            var mediumVideos = await ProcessSearchResults(mediumResult, "ChannelSearch-Medium");

                            // Mark all medium videos with duration category
                            foreach (var video in mediumVideos)
                            {
                                video.DurationCategory = "Medium";
                            }

                            allVideos.AddRange(mediumVideos);

                            await _quotaManager.RecordApiCallAsync("ChannelSearch-Medium", quotaCostPerCall,
                                TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), true, null, mediumVideos.Count);

                            _logger.LogDebug("Found {Count} medium duration videos from channel {ChannelId}",
                                mediumVideos.Count, youTubeChannelId);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON deserialization failed for medium duration search on channel {ChannelId}. Content: {Content}",
                            youTubeChannelId, mediumContent.Substring(0, Math.Min(300, mediumContent.Length)));

                        await _quotaManager.RecordApiCallAsync("ChannelSearch-Medium", quotaCostPerCall,
                            TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), false, $"JSON deserialization failed: {jsonEx.Message}");
                    }
                }
                else
                {
                    _logger.LogWarning("Medium duration search failed for channel {ChannelId}: {StatusCode}",
                        youTubeChannelId, mediumResponse.StatusCode);

                    await _quotaManager.RecordApiCallAsync("ChannelSearch-Medium", quotaCostPerCall,
                        TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), false, $"HTTP {mediumResponse.StatusCode}");
                }

                // Search 2: Long duration videos (20+ minutes)
                var longUrl = baseUrl + $"&videoDuration=long&maxResults={halfResults}";
                _logger.LogDebug("Searching long duration videos for channel {ChannelId}", youTubeChannelId);

                var longResponse = await _httpClient.GetAsync(longUrl);

                if (longResponse.IsSuccessStatusCode)
                {
                    var longContent = await longResponse.Content.ReadAsStringAsync();

                    try
                    {
                        var longResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(longContent, JsonOptions);

                        if (longResult != null)
                        {
                            var longVideos = await ProcessSearchResults(longResult, "ChannelSearch-Long");

                            // Mark all long videos with duration category
                            foreach (var video in longVideos)
                            {
                                video.DurationCategory = "Long";
                            }

                            allVideos.AddRange(longVideos);

                            await _quotaManager.RecordApiCallAsync("ChannelSearch-Long", quotaCostPerCall,
                                TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), true, null, longVideos.Count);

                            _logger.LogDebug("Found {Count} long duration videos from channel {ChannelId}",
                                longVideos.Count, youTubeChannelId);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON deserialization failed for long duration search on channel {ChannelId}. Content: {Content}",
                            youTubeChannelId, longContent.Substring(0, Math.Min(300, longContent.Length)));

                        await _quotaManager.RecordApiCallAsync("ChannelSearch-Long", quotaCostPerCall,
                            TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), false, $"JSON deserialization failed: {jsonEx.Message}");
                    }
                }
                else
                {
                    _logger.LogWarning("Long duration search failed for channel {ChannelId}: {StatusCode}",
                        youTubeChannelId, longResponse.StatusCode);

                    await _quotaManager.RecordApiCallAsync("ChannelSearch-Long", quotaCostPerCall,
                        TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), false, $"HTTP {longResponse.StatusCode}");
                }

                // Combine results and remove duplicates (prefer the more specific duration category if duplicate)
                var uniqueVideos = allVideos
                    .GroupBy(v => v.YouTubeVideoId)
                    .Select(g => g.OrderByDescending(v => v.DurationCategory == "Long" ? 1 : 0).First()) // Prefer "Long" over "Medium" if duplicate
                    .OrderByDescending(v => v.PublishedAt)
                    .Take(maxResults)
                    .ToList();

                stopwatch.Stop();

                // Cache the results
                CacheSearchResults(cacheKey, uniqueVideos);

                _logger.LogInformation("Comprehensive search for channel {ChannelId}: {MediumCount} medium + {LongCount} long = {TotalCount} unique videos",
                    youTubeChannelId,
                    uniqueVideos.Count(v => v.DurationCategory == "Medium"),
                    uniqueVideos.Count(v => v.DurationCategory == "Long"),
                    uniqueVideos.Count);

                return YouTubeApiResult<List<VideoInfo>>.Success(uniqueVideos);
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            stopwatch.Stop();
            await _quotaManager.RecordApiCallAsync("ChannelSearch-Comprehensive", 0, stopwatch.Elapsed,
                false, "Request timeout");

            _logger.LogWarning("Timeout during comprehensive search for channel {ChannelId}", youTubeChannelId);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "Request timed out. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            await _quotaManager.RecordApiCallAsync("ChannelSearch-Comprehensive", 0, stopwatch.Elapsed,
                false, ex.Message);

            _logger.LogError(ex, "Network error during comprehensive search for channel {ChannelId}", youTubeChannelId);
            return YouTubeApiResult<List<VideoInfo>>.Failure(
                "Network error occurred. Please check your internet connection and try again.");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _quotaManager.RecordApiCallAsync("ChannelSearch-Comprehensive", 0, stopwatch.Elapsed,
                false, ex.Message);

            _logger.LogError(ex, "Error during comprehensive search for channel {ChannelId} since {Since}",
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
        const int quotaCostPerCall = 100;
        const int totalQuotaCost = quotaCostPerCall * 2;

        try
        {
            if (!await _quotaManager.IsQuotaAvailableAsync(totalQuotaCost))
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
                var mediumResponse = await _httpClient.GetAsync(mediumUrl);

                if (mediumResponse.IsSuccessStatusCode)
                {
                    var mediumContent = await mediumResponse.Content.ReadAsStringAsync();

                    try
                    {
                        var mediumResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(mediumContent, JsonOptions);

                        if (mediumResult != null)
                        {
                            var mediumVideos = await ProcessSearchResults(mediumResult, "TopicSearch-Medium");

                            // Mark all medium videos with duration category
                            foreach (var video in mediumVideos)
                            {
                                video.DurationCategory = "Medium";
                            }

                            allVideos.AddRange(mediumVideos);

                            await _quotaManager.RecordApiCallAsync("TopicSearch-Medium", quotaCostPerCall,
                                TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), true, null, mediumVideos.Count);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON deserialization failed for medium duration topic search '{Topic}'. Content: {Content}",
                            topicQuery, mediumContent.Substring(0, Math.Min(300, mediumContent.Length)));

                        await _quotaManager.RecordApiCallAsync("TopicSearch-Medium", quotaCostPerCall,
                            TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), false, $"JSON deserialization failed: {jsonEx.Message}");
                    }
                }

                // Search 2: Long duration videos (20+ minutes)
                var longUrl = baseUrl + $"&videoDuration=long&maxResults={halfResults}";
                var longResponse = await _httpClient.GetAsync(longUrl);

                if (longResponse.IsSuccessStatusCode)
                {
                    var longContent = await longResponse.Content.ReadAsStringAsync();

                    try
                    {
                        var longResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(longContent, JsonOptions);

                        if (longResult != null)
                        {
                            var longVideos = await ProcessSearchResults(longResult, "TopicSearch-Long");

                            // Mark all long videos with duration category
                            foreach (var video in longVideos)
                            {
                                video.DurationCategory = "Long";
                            }

                            allVideos.AddRange(longVideos);

                            await _quotaManager.RecordApiCallAsync("TopicSearch-Long", quotaCostPerCall,
                                TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), true, null, longVideos.Count);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON deserialization failed for long duration topic search '{Topic}'. Content: {Content}",
                            topicQuery, longContent.Substring(0, Math.Min(300, longContent.Length)));

                        await _quotaManager.RecordApiCallAsync("TopicSearch-Long", quotaCostPerCall,
                            TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds), false, $"JSON deserialization failed: {jsonEx.Message}");
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

                _logger.LogInformation("Comprehensive topic search '{Topic}': {MediumCount} medium + {LongCount} long = {TotalCount} unique videos",
                    topicQuery,
                    uniqueVideos.Count(v => v.DurationCategory == "Medium"),
                    uniqueVideos.Count(v => v.DurationCategory == "Long"),
                    uniqueVideos.Count);

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
            await _quotaManager.RecordApiCallAsync("TopicSearch-Comprehensive", 0, stopwatch.Elapsed, false, ex.Message);

            _logger.LogError(ex, "Error during comprehensive topic search '{Topic}'", topicQuery);
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
    /// Searches for videos within specific channels matching a topic with comprehensive duration filtering.
    /// </summary>
    //public async Task<YouTubeApiResult<List<VideoInfo>>> SearchTopicInChannelsAsync(
    //    string topicQuery, List<string> youTubeChannelIds, DateTime? publishedAfter = null, int maxResults = 25)
    //{
    //    if (string.IsNullOrWhiteSpace(topicQuery) || !youTubeChannelIds?.Any() == true)
    //        return YouTubeApiResult<List<VideoInfo>>.Success(new List<VideoInfo>());

    //    var stopwatch = Stopwatch.StartNew();
    //    const int quotaCostPerChannelPerDuration = 100; // Each search call costs 100 quota

    //    // Limit to prevent quota exhaustion - 2 calls per channel (medium + long)
    //    var channelsToSearch = youTubeChannelIds.Take(5).ToList(); // Reduced from 10 to 5 due to 2x API calls
    //    var totalQuotaCost = channelsToSearch.Count * 2 * quotaCostPerChannelPerDuration; // 2 duration searches per channel

    //    try
    //    {
    //        // Check quota availability for all planned searches
    //        if (!await _quotaManager.IsQuotaAvailableAsync(totalQuotaCost))
    //        {
    //            await _messageCenterService.ShowApiLimitAsync("YouTube Data API", DateTime.UtcNow.Date.AddDays(1));
    //            return YouTubeApiResult<List<VideoInfo>>.Failure(
    //                "YouTube API quota exceeded for today. Please try again tomorrow.",
    //                isQuotaExceeded: true);
    //        }

    //        var allVideos = new List<VideoInfo>();
    //        var maxPerChannelPerDuration = Math.Max(1, maxResults / (channelsToSearch.Count * 2)); // Split across channels and durations
    //        var errors = new List<string>();

    //        _logger.LogInformation("Starting comprehensive topic-in-channels search for '{Topic}' across {ChannelCount} channels",
    //            topicQuery, channelsToSearch.Count);

    //        await _rateLimitSemaphore.WaitAsync();

    //        try
    //        {
    //            foreach (var channelId in channelsToSearch)
    //            {
    //                try
    //                {
    //                    // Base URL for both duration searches within this channel
    //                    var baseUrl = $"{YOUTUBE_API_BASE}/search" +
    //                                  $"?channelId={Uri.EscapeDataString(channelId)}" +
    //                                  $"&q={Uri.EscapeDataString(topicQuery)}" +
    //                                  $"&part=snippet" +
    //                                  $"&type=video" +
    //                                  $"&order=relevance" +
    //                                  $"&key={_settings.ApiKey}";

    //                    if (publishedAfter.HasValue)
    //                    {
    //                        var publishedAfterStr = publishedAfter.Value.ToString("yyyy-MM-ddTHH:mm:ssZ");
    //                        baseUrl += $"&publishedAfter={publishedAfterStr}";
    //                    }

    //                    var channelVideos = new List<VideoInfo>();

    //                    // Search 1: Medium duration videos (4-20 minutes) in this channel
    //                    var mediumUrl = baseUrl + $"&videoDuration=medium&maxResults={maxPerChannelPerDuration}";

    //                    try
    //                    {
    //                        var mediumResponse = await _httpClient.GetAsync(mediumUrl);

    //                        if (mediumResponse.IsSuccessStatusCode)
    //                        {
    //                            var mediumContent = await mediumResponse.Content.ReadAsStringAsync();

    //                            try
    //                            {
    //                                var mediumResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(mediumContent, JsonOptions);

    //                                if (mediumResult?.Items != null)
    //                                {
    //                                    var mediumVideos = await ProcessSearchResults(mediumResult, "TopicInChannel-Medium");

    //                                    // Mark all medium videos with duration category
    //                                    foreach (var video in mediumVideos)
    //                                    {
    //                                        video.DurationCategory = "Medium";
    //                                    }

    //                                    channelVideos.AddRange(mediumVideos);

    //                                    _logger.LogDebug("Found {Count} medium duration videos for topic '{Topic}' in channel {ChannelId}",
    //                                        mediumVideos.Count, topicQuery, channelId);
    //                                }

    //                                await _quotaManager.RecordQuotaUsageAsync(quotaCostPerChannelPerDuration, "TopicInChannel-Medium");
    //                            }
    //                            catch (JsonException jsonEx)
    //                            {
    //                                _logger.LogError(jsonEx, "JSON deserialization failed for medium duration topic-in-channel search. Channel: {ChannelId}, Topic: '{Topic}'. Content: {Content}",
    //                                    channelId, topicQuery, mediumContent.Substring(0, Math.Min(200, mediumContent.Length)));

    //                                errors.Add($"Medium search failed for channel {channelId}: JSON parsing error");
    //                                await _quotaManager.RecordQuotaUsageAsync(quotaCostPerChannelPerDuration, "TopicInChannel-Medium-Error");
    //                            }
    //                        }
    //                        else
    //                        {
    //                            _logger.LogWarning("Medium duration API error searching topic '{Topic}' in channel {ChannelId}: {StatusCode}",
    //                                topicQuery, channelId, mediumResponse.StatusCode);

    //                            errors.Add($"Medium search failed for channel {channelId}: HTTP {mediumResponse.StatusCode}");
    //                            await _quotaManager.RecordQuotaUsageAsync(quotaCostPerChannelPerDuration, "TopicInChannel-Medium-Error");
    //                        }
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.LogError(ex, "Error during medium duration search for topic '{Topic}' in channel {ChannelId}", topicQuery, channelId);
    //                        errors.Add($"Medium search failed for channel {channelId}: {ex.Message}");
    //                    }

    //                    // Search 2: Long duration videos (20+ minutes) in this channel
    //                    var longUrl = baseUrl + $"&videoDuration=long&maxResults={maxPerChannelPerDuration}";

    //                    try
    //                    {
    //                        var longResponse = await _httpClient.GetAsync(longUrl);

    //                        if (longResponse.IsSuccessStatusCode)
    //                        {
    //                            var longContent = await longResponse.Content.ReadAsStringAsync();

    //                            try
    //                            {
    //                                var longResult = JsonSerializer.Deserialize<YouTubeSearchResponse>(longContent, JsonOptions);

    //                                if (longResult?.Items != null)
    //                                {
    //                                    var longVideos = await ProcessSearchResults(longResult, "TopicInChannel-Long");

    //                                    // Mark all long videos with duration category
    //                                    foreach (var video in longVideos)
    //                                    {
    //                                        video.DurationCategory = "Long";
    //                                    }

    //                                    channelVideos.AddRange(longVideos);

    //                                    _logger.LogDebug("Found {Count} long duration videos for topic '{Topic}' in channel {ChannelId}",
    //                                        longVideos.Count, topicQuery, channelId);
    //                                }

    //                                await _quotaManager.RecordQuotaUsageAsync(quotaCostPerChannelPerDuration, "TopicInChannel-Long");
    //                            }
    //                            catch (JsonException jsonEx)
    //                            {
    //                                _logger.LogError(jsonEx, "JSON deserialization failed for long duration topic-in-channel search. Channel: {ChannelId}, Topic: '{Topic}'. Content: {Content}",
    //                                    channelId, topicQuery, longContent.Substring(0, Math.Min(200, longContent.Length)));

    //                                errors.Add($"Long search failed for channel {channelId}: JSON parsing error");
    //                                await _quotaManager.RecordQuotaUsageAsync(quotaCostPerChannelPerDuration, "TopicInChannel-Long-Error");
    //                            }
    //                        }
    //                        else
    //                        {
    //                            _logger.LogWarning("Long duration API error searching topic '{Topic}' in channel {ChannelId}: {StatusCode}",
    //                                topicQuery, channelId, longResponse.StatusCode);

    //                            errors.Add($"Long search failed for channel {channelId}: HTTP {longResponse.StatusCode}");
    //                            await _quotaManager.RecordQuotaUsageAsync(quotaCostPerChannelPerDuration, "TopicInChannel-Long-Error");
    //                        }
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        _logger.LogError(ex, "Error during long duration search for topic '{Topic}' in channel {ChannelId}", topicQuery, channelId);
    //                        errors.Add($"Long search failed for channel {channelId}: {ex.Message}");
    //                    }

    //                    // Add channel videos to overall results (deduplication happens later)
    //                    allVideos.AddRange(channelVideos);

    //                    _logger.LogDebug("Channel {ChannelId} contributed {Count} videos ({MediumCount} medium, {LongCount} long) for topic '{Topic}'",
    //                        channelId, channelVideos.Count,
    //                        channelVideos.Count(v => v.DurationCategory == "Medium"),
    //                        channelVideos.Count(v => v.DurationCategory == "Long"),
    //                        topicQuery);
    //                }
    //                catch (Exception ex)
    //                {
    //                    _logger.LogError(ex, "Comprehensive error searching topic '{Topic}' in channel {ChannelId}", topicQuery, channelId);
    //                    errors.Add($"Complete failure for channel {channelId}: {ex.Message}");
    //                }
    //            }
    //        }
    //        finally
    //        {
    //            _rateLimitSemaphore.Release();
    //        }

    //        // Deduplicate videos across channels and duration searches
    //        var uniqueVideos = allVideos
    //            .GroupBy(v => v.YouTubeVideoId)
    //            .Select(g => g.OrderByDescending(v => v.DurationCategory == "Long" ? 1 : 0).First()) // Prefer "Long" over "Medium" if duplicate
    //            .OrderByDescending(v => v.PublishedAt) // Most recent first
    //            .Take(maxResults)
    //            .ToList();

    //        // Get detailed video information if we have results
    //        if (uniqueVideos.Any())
    //        {
    //            var videoIds = uniqueVideos.Select(v => v.YouTubeVideoId).ToList();
    //            var detailsResult = await GetVideoDetailsByIdsAsync(videoIds);

    //            if (detailsResult.IsSuccess)
    //            {
    //                // Merge detailed information while preserving duration categories
    //                var detailedVideos = detailsResult.Data ?? new List<VideoInfo>();
    //                var detailsLookup = detailedVideos.ToDictionary(v => v.YouTubeVideoId);

    //                foreach (var video in uniqueVideos)
    //                {
    //                    if (detailsLookup.TryGetValue(video.YouTubeVideoId, out var detailed))
    //                    {
    //                        // Preserve the duration category from our search
    //                        var originalDurationCategory = video.DurationCategory;

    //                        // Copy detailed information
    //                        video.Duration = detailed.Duration;
    //                        video.ViewCount = detailed.ViewCount;
    //                        video.LikeCount = detailed.LikeCount;
    //                        video.CommentCount = detailed.CommentCount;

    //                        // Restore our duration category (more accurate than parsing actual duration)
    //                        video.DurationCategory = originalDurationCategory;
    //                    }
    //                }
    //            }
    //            else
    //            {
    //                _logger.LogWarning("Failed to get detailed video information for topic-in-channels search: {Error}",
    //                    detailsResult.ErrorMessage);
    //            }
    //        }

    //        stopwatch.Stop();

    //        // Log comprehensive results
    //        var mediumCount = uniqueVideos.Count(v => v.DurationCategory == "Medium");
    //        var longCount = uniqueVideos.Count(v => v.DurationCategory == "Long");

    //        if (errors.Any())
    //        {
    //            _logger.LogWarning("Comprehensive topic-in-channels search completed with errors. Topic: '{Topic}', Errors: {Errors}",
    //                topicQuery, string.Join("; ", errors));
    //        }

    //        _logger.LogInformation("Comprehensive topic-in-channels search for '{Topic}' completed: {MediumCount} medium + {LongCount} long = {TotalCount} unique videos from {ChannelCount} channels in {Duration}ms",
    //            topicQuery, mediumCount, longCount, uniqueVideos.Count, channelsToSearch.Count, stopwatch.ElapsedMilliseconds);

    //        return YouTubeApiResult<List<VideoInfo>>.Success(uniqueVideos);
    //    }
    //    catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
    //    {
    //        stopwatch.Stop();
    //        _logger.LogWarning("Timeout during comprehensive topic-in-channels search for '{Topic}'", topicQuery);
    //        return YouTubeApiResult<List<VideoInfo>>.Failure("Request timed out. Please try again.");
    //    }
    //    catch (HttpRequestException ex)
    //    {
    //        stopwatch.Stop();
    //        _logger.LogError(ex, "Network error during comprehensive topic-in-channels search for '{Topic}'", topicQuery);
    //        return YouTubeApiResult<List<VideoInfo>>.Failure("Network error occurred. Please check your internet connection and try again.");
    //    }
    //    catch (Exception ex)
    //    {
    //        stopwatch.Stop();
    //        _logger.LogError(ex, "Error during comprehensive topic-in-channels search for '{Topic}'", topicQuery);
    //        return YouTubeApiResult<List<VideoInfo>>.Failure("An unexpected error occurred during search. Please try again later.");
    //    }
    //}

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

                try
                {
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
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON deserialization failed for video details batch. Content: {Content}",
                        content.Substring(0, Math.Min(500, content.Length)));

                    await _quotaManager.RecordApiCallAsync("VideoDetails", quotaCost, stopwatch.Elapsed,
                        false, $"JSON deserialization failed: {jsonEx.Message}");

                    return YouTubeApiResult<List<VideoInfo>>.Failure("Failed to retrieve video details");
                }
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