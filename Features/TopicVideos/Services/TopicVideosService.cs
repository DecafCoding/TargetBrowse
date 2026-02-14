using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.TopicVideos.Models;
using TargetBrowse.Features.Topics.Services;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.TopicVideos.Services;

/// <summary>
/// Service implementation for topic-based video discovery.
/// Uses DB-first caching to reduce YouTube API quota usage.
/// </summary>
public class TopicVideosService : ITopicVideosService
{
    private readonly ISharedYouTubeService _youTubeService;
    private readonly ITopicDataService _topicDataService;
    private readonly IVideoDataService _videoDataService;
    private readonly IVideoService _videoService;
    private readonly IMessageCenterService _messageCenterService;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<TopicVideosService> _logger;

    // Constants for video discovery
    private const int DEFAULT_MAX_RESULTS = 50;
    private const int LOOKBACK_YEARS = 1;

    public TopicVideosService(
        ISharedYouTubeService youTubeService,
        ITopicDataService topicDataService,
        IVideoDataService videoDataService,
        IVideoService videoService,
        IMessageCenterService messageCenterService,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<TopicVideosService> logger)
    {
        _youTubeService = youTubeService;
        _topicDataService = topicDataService;
        _videoDataService = videoDataService;
        _videoService = videoService;
        _messageCenterService = messageCenterService;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets recent videos for a topic. Checks DB cache first; calls YouTube API only if stale.
    /// </summary>
    public async Task<List<TopicVideoDisplayModel>> GetRecentVideosAsync(Guid topicId, string currentUserId, int maxResults = DEFAULT_MAX_RESULTS, bool forceRefresh = false)
    {
        try
        {
            _logger.LogInformation("Getting recent videos for topic {TopicId} (forceRefresh: {ForceRefresh})",
                topicId, forceRefresh);

            var topic = await _topicDataService.GetTopicByIdAsync(topicId, currentUserId);

            if (topic == null)
            {
                _logger.LogWarning("Topic {TopicId} not found for user {UserId}", topicId, currentUserId);
                await _messageCenterService.ShowErrorAsync("Topic not found. It may have been deleted or you don't have access to it.");
                return new List<TopicVideoDisplayModel>();
            }

            bool shouldSearch = forceRefresh || ShouldSearchYouTube(topic.CheckDays, topic.LastCheckedDate);

            if (shouldSearch)
            {
                var results = await SearchAndCacheVideosAsync(topic, currentUserId, maxResults);

                // If API call returned results, return them
                if (results.Any())
                    return results;

                // If API failed (quota exceeded, error), fall back to cached results
                _logger.LogInformation("API returned no results for topic {TopicId}, falling back to cache", topicId);
            }

            // Return cached results from DB
            return await GetCachedVideosAsync(topicId, topic.Name, currentUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent videos for topic {TopicId}", topicId);
            await _messageCenterService.ShowErrorAsync("Failed to load videos for this topic. Please try again.");
            return new List<TopicVideoDisplayModel>();
        }
    }

    /// <summary>
    /// Determines if a YouTube API search is needed based on CheckDays and LastCheckedDate.
    /// </summary>
    private bool ShouldSearchYouTube(int checkDays, DateTime? lastCheckedDate)
    {
        if (!lastCheckedDate.HasValue)
            return true;

        var daysSinceCheck = (DateTime.UtcNow - lastCheckedDate.Value).TotalDays;
        return daysSinceCheck >= checkDays;
    }

    /// <summary>
    /// Searches YouTube, saves results to DB, and returns display models.
    /// </summary>
    private async Task<List<TopicVideoDisplayModel>> SearchAndCacheVideosAsync(TopicEntity topic, string currentUserId, int maxResults)
    {
        var publishedAfter = DateTime.UtcNow.AddYears(-LOOKBACK_YEARS);
        var youTubeResult = await _youTubeService.SearchVideosByTopicAsync(topic.Name, publishedAfter);

        if (!youTubeResult.IsSuccess)
        {
            _logger.LogWarning("YouTube search failed for topic '{TopicName}': {Error}",
                topic.Name, youTubeResult.ErrorMessage);

            if (youTubeResult.IsQuotaExceeded)
            {
                await _messageCenterService.ShowApiLimitAsync("YouTube API", DateTime.UtcNow.Date.AddDays(1));
            }
            else
            {
                await _messageCenterService.ShowErrorAsync($"Failed to search YouTube: {youTubeResult.ErrorMessage}");
            }

            return new List<TopicVideoDisplayModel>();
        }

        var videos = youTubeResult.Data ?? new List<VideoInfo>();

        if (!videos.Any())
        {
            _logger.LogInformation("No videos found for topic '{TopicName}'", topic.Name);
            await _messageCenterService.ShowInfoAsync($"No recent videos found for '{topic.Name}'. Try adjusting your topic or searching for a broader term.");
            // Still update LastCheckedDate so we don't re-query immediately
            await _topicDataService.UpdateLastCheckedDateAsync(topic.Id, DateTime.UtcNow);
            return new List<TopicVideoDisplayModel>();
        }

        // Get user's library video IDs for IsInLibrary check
        var libraryVideoIds = await GetUserLibraryVideoIdsAsync(currentUserId);

        // Save videos to DB and create junction records
        var topicVideos = new List<TopicVideoDisplayModel>();

        await using var context = await _contextFactory.CreateDbContextAsync();

        foreach (var video in videos)
        {
            try
            {
                // Ensure video exists in Videos table (deduplicates with suggestions, etc.)
                var videoEntity = await _videoDataService.EnsureVideoExistsAsync(video);

                // Calculate relevance
                var (relevanceScore, matchedKeywords) = CalculateRelevanceScoreInternal(
                    video.Title, video.Description ?? string.Empty, topic.Name);

                // Create or update junction record
                var existingJunction = await context.TopicVideos
                    .FirstOrDefaultAsync(tv => tv.TopicId == topic.Id && tv.VideoId == videoEntity.Id);

                if (existingJunction == null)
                {
                    var junction = new TopicVideoEntity
                    {
                        TopicId = topic.Id,
                        VideoId = videoEntity.Id,
                        RelevanceScore = relevanceScore,
                        MatchedKeywords = string.Join(",", matchedKeywords)
                    };
                    context.TopicVideos.Add(junction);
                }
                else
                {
                    // Update cached scores on refresh
                    existingJunction.RelevanceScore = relevanceScore;
                    existingJunction.MatchedKeywords = string.Join(",", matchedKeywords);
                }

                var displayModel = new TopicVideoDisplayModel
                {
                    YouTubeVideoId = video.YouTubeVideoId,
                    Title = video.Title,
                    Description = video.Description ?? "No description available",
                    ThumbnailUrl = video.ThumbnailUrl,
                    PublishedAt = video.PublishedAt,
                    ChannelId = video.ChannelId,
                    ChannelTitle = video.ChannelName,
                    ViewCount = (ulong?)video.ViewCount,
                    Duration = ConvertSecondsToISO8601(video.Duration),
                    TopicName = topic.Name,
                    TopicId = topic.Id,
                    RelevanceScore = relevanceScore,
                    MatchedKeywords = matchedKeywords,
                    IsInLibrary = libraryVideoIds.Contains(videoEntity.YouTubeVideoId),
                    WatchStatus = WatchStatus.NotWatched
                };

                topicVideos.Add(displayModel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process video {VideoId} for topic {TopicId}",
                    video.YouTubeVideoId, topic.Id);
            }
        }

        await context.SaveChangesAsync();
        await _topicDataService.UpdateLastCheckedDateAsync(topic.Id, DateTime.UtcNow);

        var sortedVideos = topicVideos
            .OrderByDescending(v => v.PublishedAt)
            .ToList();

        _logger.LogInformation("Found {Count} videos for topic '{TopicName}' with average relevance {AvgRelevance:F1}",
            sortedVideos.Count, topic.Name, sortedVideos.Any() ? sortedVideos.Average(v => v.RelevanceScore) : 0);

        var highRelevanceCount = sortedVideos.Count(v => v.IsHighRelevance);
        var messageText = highRelevanceCount > 0
            ? $"Found {sortedVideos.Count} videos for '{topic.Name}' ({highRelevanceCount} highly relevant)"
            : $"Found {sortedVideos.Count} videos for '{topic.Name}'";

        await _messageCenterService.ShowSuccessAsync(messageText);

        return sortedVideos;
    }

    /// <summary>
    /// Returns cached topic videos from the DB junction table.
    /// </summary>
    private async Task<List<TopicVideoDisplayModel>> GetCachedVideosAsync(Guid topicId, string topicName, string currentUserId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var cachedRecords = await context.TopicVideos
            .Where(tv => tv.TopicId == topicId)
            .Include(tv => tv.Video)
                .ThenInclude(v => v.Channel)
            .ToListAsync();

        if (!cachedRecords.Any())
        {
            _logger.LogInformation("No cached videos for topic {TopicId}", topicId);
            return new List<TopicVideoDisplayModel>();
        }

        var libraryVideoIds = await GetUserLibraryVideoIdsAsync(currentUserId);

        var displayModels = cachedRecords.Select(tv => new TopicVideoDisplayModel
        {
            YouTubeVideoId = tv.Video.YouTubeVideoId,
            Title = tv.Video.Title,
            Description = tv.Video.Description,
            ThumbnailUrl = tv.Video.ThumbnailUrl,
            PublishedAt = tv.Video.PublishedAt,
            ChannelId = tv.Video.Channel.YouTubeChannelId,
            ChannelTitle = tv.Video.Channel.Name,
            ViewCount = (ulong?)tv.Video.ViewCount,
            Duration = ConvertSecondsToISO8601(tv.Video.Duration),
            TopicName = topicName,
            TopicId = topicId,
            RelevanceScore = tv.RelevanceScore,
            MatchedKeywords = string.IsNullOrEmpty(tv.MatchedKeywords)
                ? new List<string>()
                : tv.MatchedKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            IsInLibrary = libraryVideoIds.Contains(tv.Video.YouTubeVideoId),
            WatchStatus = WatchStatus.NotWatched
        })
        .OrderByDescending(v => v.PublishedAt)
        .ToList();

        _logger.LogInformation("Loaded {Count} cached videos for topic '{TopicName}'", displayModels.Count, topicName);
        await _messageCenterService.ShowSuccessAsync($"Loaded {displayModels.Count} cached videos for '{topicName}'");

        return displayModels;
    }

    /// <summary>
    /// Gets YouTube video IDs that are in the user's library.
    /// </summary>
    private async Task<HashSet<string>> GetUserLibraryVideoIdsAsync(string userId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var ids = await context.UserVideos
            .Where(uv => uv.UserId == userId)
            .Include(uv => uv.Video)
            .Select(uv => uv.Video.YouTubeVideoId)
            .ToListAsync();

        return ids.ToHashSet();
    }

    /// <summary>
    /// Gets recent videos from YouTube for a specific topic by name.
    /// No caching — used for ad-hoc searches without a topic entity.
    /// </summary>
    public async Task<List<TopicVideoDisplayModel>> GetRecentVideosByNameAsync(string topicName, int maxResults = DEFAULT_MAX_RESULTS)
    {
        try
        {
            _logger.LogInformation("Searching YouTube for topic '{TopicName}' with max results {MaxResults}",
                topicName, maxResults);

            var publishedAfter = DateTime.UtcNow.AddYears(-LOOKBACK_YEARS);
            var youTubeResult = await _youTubeService.SearchVideosByTopicAsync(topicName, publishedAfter);

            if (!youTubeResult.IsSuccess)
            {
                _logger.LogWarning("YouTube search failed for topic '{TopicName}': {Error}",
                    topicName, youTubeResult.ErrorMessage);

                if (youTubeResult.IsQuotaExceeded)
                {
                    await _messageCenterService.ShowApiLimitAsync("YouTube API", DateTime.UtcNow.Date.AddDays(1));
                }
                else
                {
                    await _messageCenterService.ShowErrorAsync($"Failed to search YouTube: {youTubeResult.ErrorMessage}");
                }

                return new List<TopicVideoDisplayModel>();
            }

            var videos = youTubeResult.Data ?? new List<VideoInfo>();

            if (!videos.Any())
            {
                _logger.LogInformation("No videos found for topic '{TopicName}'", topicName);
                await _messageCenterService.ShowInfoAsync($"No recent videos found for '{topicName}'. Try adjusting your topic or searching for a broader term.");
                return new List<TopicVideoDisplayModel>();
            }

            var topicVideos = new List<TopicVideoDisplayModel>();

            foreach (var video in videos)
            {
                var (relevanceScore, matchedKeywords) = CalculateRelevanceScoreInternal(
                    video.Title,
                    video.Description ?? string.Empty,
                    topicName);

                var topicVideo = new TopicVideoDisplayModel
                {
                    YouTubeVideoId = video.YouTubeVideoId,
                    Title = video.Title,
                    Description = video.Description ?? "No description available",
                    ThumbnailUrl = video.ThumbnailUrl,
                    PublishedAt = video.PublishedAt,
                    ChannelId = video.ChannelId,
                    ChannelTitle = video.ChannelName,
                    ViewCount = (ulong?)video.ViewCount,
                    Duration = ConvertSecondsToISO8601(video.Duration),
                    TopicName = topicName,
                    RelevanceScore = relevanceScore,
                    MatchedKeywords = matchedKeywords,
                    IsInLibrary = false,
                    WatchStatus = WatchStatus.NotWatched
                };

                topicVideos.Add(topicVideo);
            }

            var sortedVideos = topicVideos
                .OrderByDescending(v => v.PublishedAt)
                .ToList();

            _logger.LogInformation("Found {Count} videos for topic '{TopicName}' with average relevance {AvgRelevance:F1}",
                sortedVideos.Count, topicName, sortedVideos.Any() ? sortedVideos.Average(v => v.RelevanceScore) : 0);

            var highRelevanceCount = sortedVideos.Count(v => v.IsHighRelevance);
            var messageText = highRelevanceCount > 0
                ? $"Found {sortedVideos.Count} videos for '{topicName}' ({highRelevanceCount} highly relevant)"
                : $"Found {sortedVideos.Count} videos for '{topicName}'";

            await _messageCenterService.ShowSuccessAsync(messageText);

            return sortedVideos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching YouTube for topic '{TopicName}'", topicName);
            await _messageCenterService.ShowErrorAsync("Failed to search for topic videos. Please try again.");
            return new List<TopicVideoDisplayModel>();
        }
    }

    /// <summary>
    /// Calculates relevance score for a video based on topic matching.
    /// </summary>
    public Task<(double Score, List<string> MatchedKeywords)> CalculateRelevanceScore(
        string videoTitle, string videoDescription, string topicName)
    {
        var result = CalculateRelevanceScoreInternal(videoTitle, videoDescription, topicName);
        return Task.FromResult(result);
    }

    private (double Score, List<string> MatchedKeywords) CalculateRelevanceScoreInternal(
        string videoTitle, string videoDescription, string topicName)
    {
        try
        {
            var topicWords = topicName.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length > 2)
                .ToList();

            if (!topicWords.Any())
            {
                return (5.0, new List<string>());
            }

            var titleLower = videoTitle.ToLowerInvariant();
            var descriptionLower = videoDescription.ToLowerInvariant();
            var matchedKeywords = new List<string>();

            double score = 5.0;
            int titleMatches = 0;

            foreach (var word in topicWords)
            {
                bool foundInTitle = titleLower.Contains(word);
                bool foundInDescription = descriptionLower.Contains(word);

                if (foundInTitle)
                {
                    titleMatches++;
                    matchedKeywords.Add(word);
                    score += 1.5;
                }
                else if (foundInDescription)
                {
                    if (!matchedKeywords.Contains(word))
                    {
                        matchedKeywords.Add(word);
                    }
                    score += 0.5;
                }
            }

            if (titleMatches > 1)
            {
                score += titleMatches * 0.5;
            }

            if (titleLower.Contains(topicName.ToLowerInvariant()))
            {
                score += 2.0;
            }

            score = Math.Min(score, 10.0);

            return (score, matchedKeywords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating relevance score for video '{Title}' and topic '{Topic}'",
                videoTitle, topicName);
            return (5.0, new List<string>());
        }
    }

    /// <summary>
    /// Converts duration from seconds to ISO 8601 format (PT4M13S).
    /// </summary>
    private static string ConvertSecondsToISO8601(int durationSeconds)
    {
        try
        {
            var timeSpan = TimeSpan.FromSeconds(durationSeconds);

            if (timeSpan.TotalHours >= 1)
            {
                return $"PT{(int)timeSpan.TotalHours}H{timeSpan.Minutes}M{timeSpan.Seconds}S";
            }
            else if (timeSpan.TotalMinutes >= 1)
            {
                return $"PT{timeSpan.Minutes}M{timeSpan.Seconds}S";
            }
            else
            {
                return $"PT{timeSpan.Seconds}S";
            }
        }
        catch
        {
            return "PT0S";
        }
    }

    /// <summary>
    /// Checks if the topic exists and the user has access to it.
    /// </summary>
    public async Task<bool> ValidateTopicAccess(string userId, Guid topicId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var topic = await _topicDataService.GetTopicByIdAsync(topicId, userId);
            return topic != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating topic access for user {UserId} and topic {TopicId}",
                userId, topicId);
            return false;
        }
    }

    /// <summary>
    /// Gets topic information for display purposes.
    /// </summary>
    public async Task<Topics.Models.TopicDisplayModel?> GetTopicAsync(string userId, Guid topicId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var topicEntity = await _topicDataService.GetTopicByIdAsync(topicId, userId);

            if (topicEntity == null)
            {
                return null;
            }

            return new Topics.Models.TopicDisplayModel
            {
                Id = topicEntity.Id,
                Name = topicEntity.Name,
                CreatedAt = topicEntity.CreatedAt,
                LastCheckedDate = topicEntity.LastCheckedDate,
                CheckDays = topicEntity.CheckDays
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting topic {TopicId} for user {UserId}", topicId, userId);
            return null;
        }
    }
}
