using TargetBrowse.Features.TopicVideos.Models;
using TargetBrowse.Features.Topics.Services;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Services;
using TargetBrowse.Services.YouTube; // Updated to use shared service
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.TopicVideos.Services;

/// <summary>
/// Service implementation for topic-based video discovery.
/// Now uses shared YouTube service instead of Suggestions-specific service.
/// </summary>
public class TopicVideosService : ITopicVideosService
{
    private readonly ISharedYouTubeService _youTubeService; // Updated to use shared service
    private readonly ITopicService _topicService;
    private readonly IVideoService _videoService;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<TopicVideosService> _logger;

    // Constants for video discovery
    private const int DEFAULT_MAX_RESULTS = 50;
    private const int LOOKBACK_YEARS = 1;

    public TopicVideosService(
        ISharedYouTubeService youTubeService, // Updated to use shared service
        ITopicService topicService,
        IVideoService videoService,
        IMessageCenterService messageCenterService,
        ILogger<TopicVideosService> logger)
    {
        _youTubeService = youTubeService;
        _topicService = topicService;
        _videoService = videoService;
        _messageCenterService = messageCenterService;
        _logger = logger;
    }

    /// <summary>
    /// Gets recent videos from YouTube for a specific topic.
    /// </summary>
    public async Task<List<TopicVideoDisplayModel>> GetRecentVideosAsync(Guid topicId, string currentUserId, int maxResults = DEFAULT_MAX_RESULTS)
    {
        try
        {
            _logger.LogInformation("Getting recent videos for topic {TopicId} with max results {MaxResults}",
                topicId, maxResults);

            // Get topic information first
            var userTopics = await _topicService.GetUserTopicsAsync(currentUserId);
            var topic = userTopics.FirstOrDefault(t => t.Id == topicId);

            if (topic == null)
            {
                _logger.LogWarning("Topic {TopicId} not found", topicId);
                await _messageCenterService.ShowErrorAsync("Topic not found. It may have been deleted.");
                return new List<TopicVideoDisplayModel>();
            }

            return await GetRecentVideosByNameAsync(topic.Name, maxResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent videos for topic {TopicId}", topicId);
            await _messageCenterService.ShowErrorAsync("Failed to load videos for this topic. Please try again.");
            return new List<TopicVideoDisplayModel>();
        }
    }

    /// <summary>
    /// Gets recent videos from YouTube for a specific topic by name.
    /// </summary>
    public async Task<List<TopicVideoDisplayModel>> GetRecentVideosByNameAsync(string topicName, int maxResults = DEFAULT_MAX_RESULTS)
    {
        try
        {
            _logger.LogInformation("Searching YouTube for topic '{TopicName}' with max results {MaxResults}",
                topicName, maxResults);

            // Calculate the lookback date (1 year from now)
            var publishedAfter = DateTime.UtcNow.AddYears(-LOOKBACK_YEARS);

            // Search YouTube using the shared service
            var youTubeResult = await _youTubeService.SearchVideosByTopicAsync(
                topicName,
                publishedAfter,
                maxResults);

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

            var videos = youTubeResult.Data ?? new List<Features.Suggestions.Models.VideoInfo>();

            if (!videos.Any())
            {
                _logger.LogInformation("No videos found for topic '{TopicName}'", topicName);
                await _messageCenterService.ShowInfoAsync($"No recent videos found for '{topicName}'. Try adjusting your topic or searching for a broader term.");
                return new List<TopicVideoDisplayModel>();
            }

            // Convert to TopicVideoDisplayModel with relevance scoring
            var topicVideos = new List<TopicVideoDisplayModel>();

            foreach (var video in videos)
            {
                var (relevanceScore, matchedKeywords) = await CalculateRelevanceScore(
                    video.Title,
                    video.Description ?? string.Empty,
                    topicName);

                var topicVideo = new TopicVideoDisplayModel
                {
                    // Basic video information
                    YouTubeVideoId = video.YouTubeVideoId,
                    Title = video.Title,
                    Description = video.Description ?? "No description available",
                    ThumbnailUrl = video.ThumbnailUrl,
                    PublishedAt = video.PublishedAt,
                    ChannelId = video.ChannelId,
                    ChannelTitle = video.ChannelName,
                    ViewCount = (ulong?)video.ViewCount,
                    Duration = ConvertSecondsToISO8601(video.Duration),

                    // Topic-specific information
                    TopicName = topicName,
                    RelevanceScore = relevanceScore,
                    MatchedKeywords = matchedKeywords,

                    // Default values for inherited properties
                    IsInLibrary = false,
                    WatchStatus = WatchStatus.NotWatched
                };

                topicVideos.Add(topicVideo);
            }

            // Sort by relevance score (highest first), then by publish date (newest first)
            var sortedVideos = topicVideos
                .OrderByDescending(v => v.RelevanceScore)
                .ThenByDescending(v => v.PublishedAt)
                .ToList();

            _logger.LogInformation("Found {Count} videos for topic '{TopicName}' with average relevance {AvgRelevance:F1}",
                sortedVideos.Count, topicName, sortedVideos.Any() ? sortedVideos.Average(v => v.RelevanceScore) : 0);

            // Show success message to user
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
    public async Task<(double Score, List<string> MatchedKeywords)> CalculateRelevanceScore(
        string videoTitle, string videoDescription, string topicName)
    {
        try
        {
            // Basic keyword matching algorithm
            var topicWords = topicName.ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length > 2) // Ignore short words like "a", "an", "the"
                .ToList();

            if (!topicWords.Any())
            {
                return (5.0, new List<string>()); // Neutral score if no meaningful words
            }

            var titleLower = videoTitle.ToLowerInvariant();
            var descriptionLower = videoDescription.ToLowerInvariant();
            var matchedKeywords = new List<string>();

            double score = 5.0; // Base neutral score
            int titleMatches = 0;
            int descriptionMatches = 0;

            foreach (var word in topicWords)
            {
                bool foundInTitle = titleLower.Contains(word);
                bool foundInDescription = descriptionLower.Contains(word);

                if (foundInTitle)
                {
                    titleMatches++;
                    matchedKeywords.Add(word);
                    score += 1.5; // Title matches are more valuable
                }
                else if (foundInDescription)
                {
                    descriptionMatches++;
                    if (!matchedKeywords.Contains(word))
                    {
                        matchedKeywords.Add(word);
                    }
                    score += 0.5; // Description matches are less valuable
                }
            }

            // Bonus for multiple word matches
            if (titleMatches > 1)
            {
                score += titleMatches * 0.5;
            }

            // Exact phrase match bonus
            if (titleLower.Contains(topicName.ToLowerInvariant()))
            {
                score += 2.0;
            }

            // Cap the score at 10.0
            score = Math.Min(score, 10.0);

            return (score, matchedKeywords);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating relevance score for video '{Title}' and topic '{Topic}'",
                videoTitle, topicName);
            return (5.0, new List<string>()); // Return neutral score on error
        }

        await Task.CompletedTask; // This method is synchronous but interface requires async
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
            return "PT0S"; // Return zero duration on error
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

            var userTopics = await _topicService.GetUserTopicsAsync(userId);
            return userTopics.Any(t => t.Id == topicId);
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
    public async Task<Features.Topics.Models.TopicDisplayModel?> GetTopicAsync(string userId, Guid topicId)
    {
        try
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var userTopics = await _topicService.GetUserTopicsAsync(userId);
            return userTopics.FirstOrDefault(t => t.Id == topicId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting topic {TopicId} for user {UserId}", topicId, userId);
            return null;
        }
    }
}