using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services;
using TargetBrowse.Services.YouTube;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Suggestions.Services;

/// <summary>
/// Enhanced implementation of YouTube API service for suggestion generation.
/// Now uses SharedYouTubeService for common operations and focuses on suggestion-specific functionality.
/// </summary>
public class SuggestionYouTubeService : ISuggestionYouTubeService
{
    private readonly ISharedYouTubeService _sharedYouTubeService;
    private readonly ISuggestionQuotaManager _quotaManager;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<SuggestionYouTubeService> _logger;

    public SuggestionYouTubeService(
        ISharedYouTubeService sharedYouTubeService,
        ISuggestionQuotaManager quotaManager,
        IMessageCenterService messageCenterService,
        ILogger<SuggestionYouTubeService> logger)
    {
        _sharedYouTubeService = sharedYouTubeService ?? throw new ArgumentNullException(nameof(sharedYouTubeService));
        _quotaManager = quotaManager ?? throw new ArgumentNullException(nameof(quotaManager));
        _messageCenterService = messageCenterService ?? throw new ArgumentNullException(nameof(messageCenterService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets new videos from a channel since the specified date.
    /// Delegates to shared service for common functionality.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> GetChannelVideosSinceAsync(
        string youTubeChannelId, DateTime since, int maxResults = 50)
    {
        return await _sharedYouTubeService.GetChannelVideosSinceAsync(youTubeChannelId, since, maxResults);
    }

    /// <summary>
    /// Searches for videos across all of YouTube matching the specified topic.
    /// Delegates to shared service for common functionality.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> SearchVideosByTopicAsync(
        string topicQuery, DateTime? publishedAfter = null, int maxResults = 50)
    {
        return await _sharedYouTubeService.SearchVideosByTopicAsync(topicQuery, publishedAfter, maxResults);
    }

    /// <summary>
    /// Gets detailed information about multiple videos by their YouTube IDs.
    /// Delegates to shared service for common functionality.
    /// </summary>
    public async Task<YouTubeApiResult<List<VideoInfo>>> GetVideoDetailsByIdsAsync(List<string> youTubeVideoIds)
    {
        return await _sharedYouTubeService.GetVideoDetailsByIdsAsync(youTubeVideoIds);
    }

    /// <summary>
    /// Performs bulk channel update checking for multiple channels.
    /// Suggestion-specific implementation with enhanced filtering and processing.
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
            // Skip 1-star rated channels (suggestion-specific business logic)
            if (request.UserRating == 1)
            {
                _logger.LogDebug("Skipping 1-star rated channel {ChannelId}", request.YouTubeChannelId);
                continue;
            }

            var result = await _sharedYouTubeService.GetChannelVideosSinceAsync(
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
    /// Suggestion-specific implementation with enhanced processing.
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
            var result = await _sharedYouTubeService.SearchVideosByTopicAsync(topic, publishedAfter, maxResultsPerTopic);

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

        // Remove duplicates (videos found via multiple topics) - suggestion-specific logic
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
    /// Delegates to shared service.
    /// </summary>
    public async Task<ApiAvailabilityResult> GetApiAvailabilityAsync()
    {
        return await _sharedYouTubeService.GetApiAvailabilityAsync();
    }

    /// <summary>
    /// Gets estimated API quota cost for suggestion operations.
    /// Suggestion-specific implementation.
    /// </summary>
    public async Task<QuotaCostEstimate> EstimateQuotaCostAsync(
        int channelCount, int topicCount, int estimatedVideosFound = 100)
    {
        return await _quotaManager.EstimateSuggestionCostAsync(channelCount, topicCount, estimatedVideosFound);
    }

    /// <summary>
    /// Gets current API usage statistics.
    /// Suggestion-specific implementation.
    /// </summary>
    public async Task<SuggestionApiUsage> GetCurrentApiUsageAsync()
    {
        return await _quotaManager.GetUsageStatisticsAsync();
    }

    /// <summary>
    /// Validates that YouTube video IDs exist.
    /// Delegates to shared service.
    /// </summary>
    public async Task<YouTubeApiResult<Dictionary<string, bool>>> ValidateVideoIdsAsync(List<string> youTubeVideoIds)
    {
        return await _sharedYouTubeService.ValidateVideoIdsAsync(youTubeVideoIds);
    }

    /// <summary>
    /// Preloads video details for optimization.
    /// Suggestion-specific implementation with enhanced caching strategy.
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

            var result = await _sharedYouTubeService.GetVideoDetailsByIdsAsync(videoIds);

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
    /// Delegates to shared service.
    /// </summary>
    public async Task<QuotaStatus> GetQuotaStatusAsync()
    {
        return await _sharedYouTubeService.GetQuotaStatusAsync();
    }
}