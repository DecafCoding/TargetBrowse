using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Options;
using TargetBrowse.Services.YouTube.Models;
using TargetBrowse.Features.Videos.Models;

namespace TargetBrowse.Features.Videos.Services;

/// <summary>
/// Video-specific implementation of YouTube Data API v3 service.
/// Handles video search, information retrieval, and quota management for the Videos feature.
/// </summary>
public class VideoYouTubeService : IVideoYouTubeService, IDisposable
{
    private readonly Google.Apis.YouTube.v3.YouTubeService _youTubeClient;
    private readonly YouTubeApiSettings _settings;
    private readonly ILogger<VideoYouTubeService> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore;

    // Simple in-memory quota tracking for MVP (shared across all YouTube services)
    private static int _dailyQuotaUsed = 0;
    private static DateTime _lastQuotaReset = DateTime.UtcNow.Date;

    // API cost constants (approximate)
    private const int SearchCost = 100;
    private const int VideoDetailsCost = 1;

    public VideoYouTubeService(IOptions<YouTubeApiSettings> settings, ILogger<VideoYouTubeService> logger)
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
            ApplicationName = "YouTube Video Tracker - Videos"
        });

        ResetQuotaIfNeeded();
    }

    /// <summary>
    /// Searches for YouTube videos by keyword or phrase with advanced filtering options.
    /// Returns basic video information from search results only (no additional API calls for detailed stats).
    /// </summary>
    public async Task<YouTubeApiResult<List<YouTubeVideoResponse>>> SearchVideosAsync(
        string searchQuery,
        int maxResults = 25,
        string? channelId = null,
        VideoSortOrder sortOrder = VideoSortOrder.Relevance,
        VideoDurationFilter durationFilter = VideoDurationFilter.Any,
        VideoDateFilter dateFilter = VideoDateFilter.Any)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
        {
            return YouTubeApiResult<List<YouTubeVideoResponse>>.Success(new List<YouTubeVideoResponse>());
        }

        if (maxResults < 1 || maxResults > 50)
        {
            maxResults = 25;
        }

        try
        {
            if (!await CheckQuotaAvailableAsync(SearchCost))
            {
                return YouTubeApiResult<List<YouTubeVideoResponse>>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            await _rateLimitSemaphore.WaitAsync();

            try
            {
                var searchRequest = _youTubeClient.Search.List("snippet");
                searchRequest.Q = searchQuery.Trim();
                searchRequest.Type = "video";
                searchRequest.MaxResults = maxResults;

                // Only request fields available in search results - no contentDetails or statistics
                searchRequest.Fields = "items(id/videoId,snippet(title,description,thumbnails/medium,publishedAt,channelId,channelTitle))";

                // Apply sort order
                searchRequest.Order = MapSortOrderToApi(sortOrder);

                // Apply duration filter
                var apiDurationFilter = MapDurationFilterToApi(durationFilter);
                if (apiDurationFilter.HasValue)
                {
                    searchRequest.VideoDuration = apiDurationFilter.Value;
                }

                // Apply date filter
                var dateRange = MapDateFilterToApi(dateFilter);
                if (dateRange.publishedAfter.HasValue)
                {
                    searchRequest.PublishedAfter = dateRange.publishedAfter.Value;
                }
                if (dateRange.publishedBefore.HasValue)
                {
                    searchRequest.PublishedBefore = dateRange.publishedBefore.Value;
                }

                // If channelId is specified, limit search to that channel
                if (!string.IsNullOrWhiteSpace(channelId))
                {
                    searchRequest.ChannelId = channelId;
                }

                var searchResponse = await searchRequest.ExecuteAsync();
                await IncrementQuotaUsageAsync(SearchCost);

                if (searchResponse.Items?.Count > 0)
                {
                    var results = new List<YouTubeVideoResponse>();

                    foreach (var searchItem in searchResponse.Items)
                    {
                        var videoResponse = new YouTubeVideoResponse
                        {
                            VideoId = searchItem.Id.VideoId,
                            Title = searchItem.Snippet.Title,
                            Description = searchItem.Snippet.Description ?? string.Empty,
                            ThumbnailUrl = searchItem.Snippet.Thumbnails?.Medium?.Url,
                            PublishedAt = searchItem.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.MinValue,
                            ChannelId = searchItem.Snippet.ChannelId,
                            ChannelTitle = searchItem.Snippet.ChannelTitle,
                            // Note: Duration, ViewCount, LikeCount, CommentCount are not available in search results
                            // These fields will remain null, requiring separate API calls if needed
                            Duration = null,
                            ViewCount = null,
                            LikeCount = null,
                            CommentCount = null
                        };

                        results.Add(videoResponse);
                    }

                    _logger.LogInformation("YouTube video search for '{SearchQuery}' with filters (Sort: {SortOrder}, Duration: {DurationFilter}, Date: {DateFilter}) returned {ResultCount} videos (basic info only)",
                        searchQuery, sortOrder, durationFilter, dateFilter, results.Count);

                    return YouTubeApiResult<List<YouTubeVideoResponse>>.Success(results);
                }

                return YouTubeApiResult<List<YouTubeVideoResponse>>.Success(new List<YouTubeVideoResponse>());
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }
        catch (Google.GoogleApiException ex) when (ex.Error?.Code == 403)
        {
            _logger.LogWarning("YouTube API quota exceeded: {Message}", ex.Message);
            return YouTubeApiResult<List<YouTubeVideoResponse>>.Failure(
                "YouTube API quota exceeded. Please try again later.",
                isQuotaExceeded: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching YouTube videos for query: {SearchQuery}", searchQuery);
            return YouTubeApiResult<List<YouTubeVideoResponse>>.Failure(
                "Unable to search YouTube videos. Please check your internet connection and try again.");
        }
    }

    /// <summary>
    /// Gets detailed information about a specific YouTube video by video ID.
    /// This method provides full video details when specifically requested.
    /// </summary>
    public async Task<YouTubeApiResult<YouTubeVideoResponse?>> GetVideoByIdAsync(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return YouTubeApiResult<YouTubeVideoResponse?>.Failure("Video ID is required.");
        }

        try
        {
            if (!await CheckQuotaAvailableAsync(VideoDetailsCost))
            {
                return YouTubeApiResult<YouTubeVideoResponse?>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            var videoRequest = _youTubeClient.Videos.List("snippet,contentDetails,statistics");
            videoRequest.Id = videoId;
            videoRequest.Fields = "items(id,snippet(title,description,thumbnails/medium,publishedAt,channelId,channelTitle,tags,defaultLanguage,categoryId),contentDetails(duration),statistics(viewCount,likeCount,commentCount))";

            var videoResponse = await videoRequest.ExecuteAsync();
            await IncrementQuotaUsageAsync(VideoDetailsCost);

            if (videoResponse.Items?.Count > 0)
            {
                var video = videoResponse.Items[0];
                var result = new YouTubeVideoResponse
                {
                    VideoId = video.Id,
                    Title = video.Snippet.Title,
                    Description = video.Snippet.Description ?? string.Empty,
                    ThumbnailUrl = video.Snippet.Thumbnails?.Medium?.Url,
                    Duration = video.ContentDetails?.Duration,
                    ViewCount = video.Statistics?.ViewCount,
                    LikeCount = video.Statistics?.LikeCount,
                    CommentCount = video.Statistics?.CommentCount,
                    PublishedAt = video.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.MinValue,
                    ChannelId = video.Snippet.ChannelId,
                    ChannelTitle = video.Snippet.ChannelTitle,
                    CategoryId = video.Snippet.CategoryId,
                    Tags = video.Snippet.Tags?.ToList() ?? new List<string>(),
                    DefaultLanguage = video.Snippet.DefaultLanguage
                };

                return YouTubeApiResult<YouTubeVideoResponse?>.Success(result);
            }

            return YouTubeApiResult<YouTubeVideoResponse?>.Failure(
                "Video not found.", isInvalidChannel: true);
        }
        catch (Google.GoogleApiException ex) when (ex.Error?.Code == 403)
        {
            _logger.LogWarning("YouTube API quota exceeded: {Message}", ex.Message);
            return YouTubeApiResult<YouTubeVideoResponse?>.Failure(
                "YouTube API quota exceeded. Please try again later.",
                isQuotaExceeded: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YouTube video details for ID: {VideoId}", videoId);
            return YouTubeApiResult<YouTubeVideoResponse?>.Failure(
                "Unable to get video information. Please try again.");
        }
    }

    /// <summary>
    /// Gets detailed information about multiple YouTube videos by their IDs.
    /// This method provides full video details when specifically requested.
    /// </summary>
    public async Task<YouTubeApiResult<List<YouTubeVideoResponse>>> GetVideosByIdsAsync(IEnumerable<string> videoIds)
    {
        var videoIdList = videoIds?.ToList() ?? new List<string>();

        if (!videoIdList.Any())
        {
            return YouTubeApiResult<List<YouTubeVideoResponse>>.Success(new List<YouTubeVideoResponse>());
        }

        if (videoIdList.Count > 50)
        {
            videoIdList = videoIdList.Take(50).ToList();
            _logger.LogWarning("Video IDs list truncated to 50 items for API limit compliance");
        }

        try
        {
            if (!await CheckQuotaAvailableAsync(VideoDetailsCost))
            {
                return YouTubeApiResult<List<YouTubeVideoResponse>>.Failure(
                    "YouTube API quota exceeded for today. Please try again tomorrow.",
                    isQuotaExceeded: true);
            }

            var videoRequest = _youTubeClient.Videos.List("snippet,contentDetails,statistics");
            videoRequest.Id = string.Join(",", videoIdList);
            videoRequest.Fields = "items(id,snippet(title,description,thumbnails/medium,publishedAt,channelId,channelTitle,tags,defaultLanguage,categoryId),contentDetails(duration),statistics(viewCount,likeCount,commentCount))";

            var videoResponse = await videoRequest.ExecuteAsync();
            await IncrementQuotaUsageAsync(VideoDetailsCost);

            var results = new List<YouTubeVideoResponse>();

            if (videoResponse.Items?.Count > 0)
            {
                foreach (var video in videoResponse.Items)
                {
                    var result = new YouTubeVideoResponse
                    {
                        VideoId = video.Id,
                        Title = video.Snippet.Title,
                        Description = video.Snippet.Description ?? string.Empty,
                        ThumbnailUrl = video.Snippet.Thumbnails?.Medium?.Url,
                        Duration = video.ContentDetails?.Duration,
                        ViewCount = video.Statistics?.ViewCount,
                        LikeCount = video.Statistics?.LikeCount,
                        CommentCount = video.Statistics?.CommentCount,
                        PublishedAt = video.Snippet.PublishedAtDateTimeOffset?.DateTime ?? DateTime.MinValue,
                        ChannelId = video.Snippet.ChannelId,
                        ChannelTitle = video.Snippet.ChannelTitle,
                        CategoryId = video.Snippet.CategoryId,
                        Tags = video.Snippet.Tags?.ToList() ?? new List<string>(),
                        DefaultLanguage = video.Snippet.DefaultLanguage
                    };

                    results.Add(result);
                }
            }

            _logger.LogInformation("Retrieved details for {ResultCount} of {RequestedCount} videos",
                results.Count, videoIdList.Count);

            return YouTubeApiResult<List<YouTubeVideoResponse>>.Success(results);
        }
        catch (Google.GoogleApiException ex) when (ex.Error?.Code == 403)
        {
            _logger.LogWarning("YouTube API quota exceeded: {Message}", ex.Message);
            return YouTubeApiResult<List<YouTubeVideoResponse>>.Failure(
                "YouTube API quota exceeded. Please try again later.",
                isQuotaExceeded: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YouTube video details for {VideoCount} videos", videoIdList.Count);
            return YouTubeApiResult<List<YouTubeVideoResponse>>.Failure(
                "Unable to get video information. Please try again.");
        }
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

    #region Private Helper Methods

    /// <summary>
    /// Maps the application's sort order enum to YouTube API sort order.
    /// </summary>
    private static SearchResource.ListRequest.OrderEnum MapSortOrderToApi(VideoSortOrder sortOrder)
    {
        return sortOrder switch
        {
            VideoSortOrder.UploadDate => SearchResource.ListRequest.OrderEnum.Date,
            VideoSortOrder.ViewCount => SearchResource.ListRequest.OrderEnum.ViewCount,
            VideoSortOrder.Rating => SearchResource.ListRequest.OrderEnum.Rating,
            VideoSortOrder.Relevance => SearchResource.ListRequest.OrderEnum.Relevance,
            _ => SearchResource.ListRequest.OrderEnum.Relevance
        };
    }

    /// <summary>
    /// Maps the application's duration filter enum to YouTube API duration filter.
    /// </summary>
    private static SearchResource.ListRequest.VideoDurationEnum? MapDurationFilterToApi(VideoDurationFilter durationFilter)
    {
        return durationFilter switch
        {
            VideoDurationFilter.Short => SearchResource.ListRequest.VideoDurationEnum.Short__,
            VideoDurationFilter.Medium => SearchResource.ListRequest.VideoDurationEnum.Medium,
            VideoDurationFilter.Long => SearchResource.ListRequest.VideoDurationEnum.Long__,
            VideoDurationFilter.Any => null,
            _ => null
        };
    }

    /// <summary>
    /// Maps the application's date filter enum to YouTube API date range.
    /// </summary>
    private static (DateTimeOffset? publishedAfter, DateTimeOffset? publishedBefore) MapDateFilterToApi(VideoDateFilter dateFilter)
    {
        var now = DateTimeOffset.UtcNow;

        return dateFilter switch
        {
            VideoDateFilter.LastHour => (now.AddHours(-1), null),
            VideoDateFilter.Today => (now.Date, null),
            VideoDateFilter.ThisWeek => (now.AddDays(-7), null),
            VideoDateFilter.ThisMonth => (now.AddDays(-30), null),
            VideoDateFilter.ThisYear => (now.AddDays(-365), null),
            VideoDateFilter.Any => (null, null),
            _ => (null, null)
        };
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
        _logger.LogDebug("YouTube API quota used: {QuotaUsed}, Total today: {TotalUsed}/{DailyLimit}",
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
            _logger.LogInformation("YouTube API quota reset for new day");
        }
    }

    #endregion

    public void Dispose()
    {
        _youTubeClient?.Dispose();
        _rateLimitSemaphore?.Dispose();
    }
}