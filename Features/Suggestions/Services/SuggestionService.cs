using System.Diagnostics;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Features.Suggestions.Data;
using TargetBrowse.Features.Topics.Services;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Features.Channels.Services;
using TargetBrowse.Services;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Suggestions.Services;

/// <summary>
/// Core service implementation for intelligent video suggestion generation.
/// Combines user topics, tracked channels, and ratings using unified scoring algorithm.
/// </summary>
public class SuggestionService : ISuggestionService
{
    private readonly ISuggestionRepository _suggestionRepository;
    private readonly ISuggestionYouTubeService _youTubeService;
    private readonly ITopicService _topicService;
    private readonly IVideoService _videoService;
    private readonly IChannelRatingService _channelRatingService;
    private readonly IVideoRatingService _videoRatingService;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<SuggestionService> _logger;

    // Scoring algorithm weights
    private const double CHANNEL_RATING_WEIGHT = 0.6;
    private const double TOPIC_RELEVANCE_WEIGHT = 0.25;
    private const double RECENCY_WEIGHT = 0.15;
    private const double DUAL_SOURCE_BONUS = 1.0;

    // Business rules
    private const int MAX_PENDING_SUGGESTIONS = 1000;
    private const int SUGGESTION_EXPIRY_DAYS = 30;
    private const int MAX_SUGGESTIONS_PER_REQUEST = 50;
    private const int DEFAULT_DAYS_LOOKBACK = 30;

    public SuggestionService(
        ISuggestionRepository suggestionRepository,
        ISuggestionYouTubeService youTubeService,
        ITopicService topicService,
        IVideoService videoService,
        IChannelRatingService channelRatingService,
        IVideoRatingService videoRatingService,
        IMessageCenterService messageCenterService,
        ILogger<SuggestionService> logger)
    {
        _suggestionRepository = suggestionRepository;
        _youTubeService = youTubeService;
        _topicService = topicService;
        _videoService = videoService;
        _channelRatingService = channelRatingService;
        _videoRatingService = videoRatingService;
        _messageCenterService = messageCenterService;
        _logger = logger;
    }

    /// <summary>
    /// Generates video suggestions for a user based on their topics and tracked channels.
    /// </summary>
    public async Task<SuggestionResult> GenerateSuggestions(string userId, double scoreThreshold = 5.0)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting suggestion generation for user {UserId} with threshold {Threshold}",
                userId, scoreThreshold);

            // Check if user can request suggestions
            if (!await CanUserRequestSuggestions(userId))
            {
                await _messageCenterService.ShowErrorAsync("You have reached the maximum number of pending suggestions. Please review existing suggestions first.");
                return SuggestionResult.Failure("Maximum pending suggestions reached");
            }

            var result = new SuggestionResult();

            // 1. Get videos from both sources
            var channelVideos = await GetChannelUpdateVideos(userId);
            var topicVideos = await GetTopicSearchVideos(userId);

            result.ChannelVideosFound = channelVideos.Count;
            result.TopicVideosFound = topicVideos.Count;

            _logger.LogInformation("Found {ChannelCount} channel videos and {TopicCount} topic videos for user {UserId}",
                channelVideos.Count, topicVideos.Count, userId);

            // 2. Smart deduplication with source tracking
            var consolidatedVideos = ConsolidateVideoSources(channelVideos, topicVideos);

            result.DuplicatesFound = (channelVideos.Count + topicVideos.Count) - consolidatedVideos.Count;
            result.AllDiscoveredVideos = consolidatedVideos.Select(v => v.Video).ToList();

            // 3. Save all discovered videos to database for historical browsing
            var videoEntities = await _suggestionRepository.EnsureVideosExistAsync(result.AllDiscoveredVideos);

            // 4. Preliminary scoring with unified approach
            var scoredSuggestions = await ScoreVideosWithSourceContext(consolidatedVideos, userId);

            if (scoredSuggestions.Any())
            {
                result.AverageScore = scoredSuggestions.Average(s => s.Score);
                result.ScoreDistribution = CalculateScoreDistribution(scoredSuggestions);
            }

            // 5. Filter by threshold and exclude videos already suggested
            var qualifyingVideos = scoredSuggestions.Where(s => s.Score >= scoreThreshold).ToList();

            // Remove videos that already have pending suggestions
            var filteredVideos = new List<VideoSuggestion>();
            foreach (var video in qualifyingVideos)
            {
                var videoEntity = videoEntities.FirstOrDefault(v => v.YouTubeVideoId == video.Video.YouTubeVideoId);
                if (videoEntity != null)
                {
                    var hasPending = await _suggestionRepository.HasPendingSuggestionForVideoAsync(userId, videoEntity.Id);
                    if (!hasPending)
                    {
                        filteredVideos.Add(video);
                    }
                }
            }

            _logger.LogInformation("Scored {TotalCount} videos, {QualifyingCount} scored above {Threshold}, {FilteredCount} after deduplication",
                scoredSuggestions.Count, qualifyingVideos.Count, scoreThreshold, filteredVideos.Count);

            // 6. Apply safety cap and sort by score
            var selectedSuggestions = filteredVideos
                .OrderByDescending(s => s.Score)
                .Take(MAX_SUGGESTIONS_PER_REQUEST)
                .ToList();

            // 7. Create suggestion entities and save to database
            result.NewSuggestions = await CreateSuggestionEntities(selectedSuggestions, videoEntities, userId);

            // 8. Set processing metrics
            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            var quotaEstimate = await _youTubeService.EstimateQuotaCostAsync(result.ChannelVideosFound, result.TopicVideosFound);
            result.ApiUsage = new SuggestionApiUsage
            {
                ChannelSearchCalls = result.ChannelVideosFound > 0 ? 1 : 0,
                TopicSearchCalls = result.TopicVideosFound > 0 ? 1 : 0,
                VideoDetailCalls = Math.Max(result.ChannelVideosFound, result.TopicVideosFound) > 0 ? 1 : 0,
                EstimatedQuotaUsed = quotaEstimate.TotalEstimatedCost
            };

            // 9. Provide user feedback
            var summaryMessage = result.GetSummaryMessage();
            if (result.NewSuggestions.Any())
            {
                await _messageCenterService.ShowSuccessAsync(summaryMessage);
            }
            else if (result.AllDiscoveredVideos.Any())
            {
                await _messageCenterService.ShowInfoAsync("Found videos but none met the quality threshold for suggestions. Consider adjusting your topics or channel preferences.");
            }
            else
            {
                await _messageCenterService.ShowInfoAsync("No new videos found from your tracked channels or topics. Try adding more channels or topics to improve discovery.");
            }

            _logger.LogInformation("Successfully generated {Count} suggestions for user {UserId} in {Duration}ms",
                result.NewSuggestions.Count, userId, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error generating suggestions for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to generate suggestions. Please try again later.");
            return SuggestionResult.Failure($"Suggestion generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a user can request new suggestions.
    /// </summary>
    public async Task<bool> CanUserRequestSuggestions(string userId)
    {
        try
        {
            var pendingCount = await GetPendingSuggestionsCount(userId);
            return pendingCount < MAX_PENDING_SUGGESTIONS;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking suggestion request eligibility for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Gets the current count of pending suggestions for a user.
    /// </summary>
    public async Task<int> GetPendingSuggestionsCount(string userId)
    {
        try
        {
            return await _suggestionRepository.GetPendingSuggestionsCountAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending suggestions count for user {UserId}", userId);
            return 0;
        }
    }

    /// <summary>
    /// Removes expired suggestions that are older than 30 days.
    /// </summary>
    public async Task<int> CleanupExpiredSuggestions()
    {
        try
        {
            var cleanedUp = await _suggestionRepository.CleanupExpiredSuggestionsAsync();

            if (cleanedUp > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired suggestions", cleanedUp);
            }

            return cleanedUp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired suggestions");
            return 0;
        }
    }

    /// <summary>
    /// Performs enhanced scoring for a video using transcript analysis.
    /// Future implementation for transcript-based scoring.
    /// </summary>
    public async Task<VideoScore> ScoreVideoEnhanced(VideoInfo video, string userId, string transcript)
    {
        try
        {
            // Get user data for scoring
            var userTopics = await _topicService.GetUserTopicsAsync(userId);
            var channelRatings = await _channelRatingService.GetUserRatingsAsync(userId);

            var score = new VideoScore
            {
                Video = video,
                Stage = ScoringStage.Enhanced
            };

            // For now, use preliminary scoring logic
            // Future: Implement transcript analysis here
            var topicResult = CalculateTopicRelevanceFromTitle(video, userTopics.Select(t => t.Name).ToList());
            var channelRating = channelRatings.FirstOrDefault(r => r.YouTubeChannelId == video.ChannelId);

            score.ChannelRatingScore = channelRating != null ? channelRating.Stars * 2 : 6.0;
            score.TopicRelevanceScore = topicResult.Score;
            score.RecencyScore = CalculateRecencyScore(video.PublishedAt);
            score.MatchedTopics = topicResult.MatchedTopics;

            score.TotalScore =
                (score.ChannelRatingScore * CHANNEL_RATING_WEIGHT) +
                (score.TopicRelevanceScore * TOPIC_RELEVANCE_WEIGHT) +
                (score.RecencyScore * RECENCY_WEIGHT);

            return score;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing enhanced scoring for video {VideoId}", video.YouTubeVideoId);
            return new VideoScore { Video = video, Stage = ScoringStage.Enhanced };
        }
    }

    /// <summary>
    /// Gets all pending suggestions for a user with pagination.
    /// </summary>
    public async Task<List<SuggestionDisplayModel>> GetPendingSuggestionsAsync(string userId, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            var suggestions = await _suggestionRepository.GetPendingSuggestionsAsync(userId, pageNumber, pageSize);

            return suggestions.Select(s => new SuggestionDisplayModel
            {
                Id = s.Id,
                Video = MapVideoEntityToInfo(s.Video),
                Reason = s.Reason,
                CreatedAt = s.CreatedAt,
                Status = SuggestionStatus.Pending
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending suggestions for user {UserId}", userId);
            return new List<SuggestionDisplayModel>();
        }
    }

    /// <summary>
    /// Approves a suggestion, adding the video to the user's library.
    /// FIXED: Now directly adds the video to library using existing video entity data
    /// instead of making unnecessary YouTube API calls.
    /// </summary>
    public async Task<bool> ApproveSuggestionAsync(string userId, Guid suggestionId)
    {
        try
        {
            var suggestion = await _suggestionRepository.GetSuggestionByIdAsync(suggestionId, userId);
            if (suggestion == null)
            {
                await _messageCenterService.ShowErrorAsync("Suggestion not found or you don't have permission to approve it.");
                return false;
            }

            // Check if video is already in user's library to avoid duplicates
            var isAlreadyInLibrary = await _videoService.IsVideoInLibraryAsync(userId, suggestion.Video.YouTubeVideoId);
            if (isAlreadyInLibrary)
            {
                // Still approve the suggestion since that's what the user requested
                await _suggestionRepository.ApproveSuggestionsAsync(new List<Guid> { suggestionId }, userId);

                await _messageCenterService.ShowSuccessAsync($"Suggestion approved! Video '{suggestion.Video.Title}' was already in your library.");
                _logger.LogInformation("User {UserId} approved suggestion {SuggestionId} - video already in library", userId, suggestionId);
                return true;
            }

            // Approve the suggestion first
            await _suggestionRepository.ApproveSuggestionsAsync(new List<Guid> { suggestionId }, userId);

            // Add video to user's library using VideoService
            // Uses the optimized method for existing video entities
            var success = await _videoService.AddExistingVideoToLibraryAsync(userId, suggestion.Video);

            if (success)
            {
                await _messageCenterService.ShowSuccessAsync($"Video '{suggestion.Video.Title}' approved and added to your library!");
                _logger.LogInformation("User {UserId} approved suggestion {SuggestionId} and added video {VideoId} to library",
                    userId, suggestionId, suggestion.Video.YouTubeVideoId);
            }
            else
            {
                await _messageCenterService.ShowErrorAsync("Video was approved but failed to add to library. It may already exist.");
                _logger.LogWarning("Failed to add video {VideoId} to library after approving suggestion {SuggestionId} for user {UserId}",
                    suggestion.Video.YouTubeVideoId, suggestionId, userId);
            }

            return true; // Return true since the suggestion was approved, even if library add failed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving suggestion {SuggestionId} for user {UserId}", suggestionId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to approve suggestion. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Denies a suggestion, removing it from the user's queue.
    /// </summary>
    public async Task<bool> DenySuggestionAsync(string userId, Guid suggestionId)
    {
        try
        {
            var suggestion = await _suggestionRepository.GetSuggestionByIdAsync(suggestionId, userId);
            if (suggestion == null)
            {
                await _messageCenterService.ShowErrorAsync("Suggestion not found or you don't have permission to deny it.");
                return false;
            }

            // Deny the suggestion
            await _suggestionRepository.DenySuggestionsAsync(new List<Guid> { suggestionId }, userId);

            await _messageCenterService.ShowSuccessAsync($"Video '{suggestion.Video.Title}' removed from suggestions.");

            _logger.LogInformation("User {UserId} denied suggestion {SuggestionId}", userId, suggestionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying suggestion {SuggestionId} for user {UserId}", suggestionId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to deny suggestion. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Gets suggestion statistics and analytics for a user.
    /// </summary>
    public async Task<SuggestionAnalytics> GetSuggestionAnalyticsAsync(string userId)
    {
        try
        {
            return await _suggestionRepository.GetSuggestionAnalyticsAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggestion analytics for user {UserId}", userId);
            return new SuggestionAnalytics();
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Gets new videos from user's tracked channels since last check.
    /// </summary>
    private async Task<List<VideoInfo>> GetChannelUpdateVideos(string userId)
    {
        try
        {
            var channelsToCheck = await _suggestionRepository.GetChannelsForUpdateCheckAsync(userId);
            var channelRequests = new List<ChannelUpdateRequest>();

            foreach (var channelInfo in channelsToCheck)
            {
                // Skip 1-star rated channels
                if (channelInfo.UserRating == 1)
                {
                    continue;
                }

                channelRequests.Add(new ChannelUpdateRequest
                {
                    YouTubeChannelId = channelInfo.Channel.YouTubeChannelId,
                    ChannelName = channelInfo.Channel.Name,
                    LastCheckDate = channelInfo.LastCheckDate ?? DateTime.UtcNow.AddDays(-DEFAULT_DAYS_LOOKBACK),
                    UserRating = channelInfo.UserRating,
                    MaxResults = 50
                });
            }

            if (!channelRequests.Any())
            {
                return new List<VideoInfo>();
            }

            // Use bulk channel updates method
            var result = await _youTubeService.GetBulkChannelUpdatesAsync(channelRequests);

            if (result.IsSuccess)
            {
                // Update last check dates for all channels
                foreach (var request in channelRequests)
                {
                    var channelEntity = channelsToCheck.FirstOrDefault(c => c.Channel.YouTubeChannelId == request.YouTubeChannelId);
                    if (channelEntity != null)
                    {
                        await _suggestionRepository.UpdateChannelLastCheckDateAsync(
                            userId, channelEntity.Channel.Id, DateTime.UtcNow);
                    }
                }

                return result.Data ?? new List<VideoInfo>();
            }
            else
            {
                // Log the error but don't fail completely - return what we can
                _logger.LogWarning("Channel updates failed: {Error}", result.ErrorMessage);
                if (!result.IsQuotaExceeded)
                {
                    await _messageCenterService.ShowErrorAsync("Failed to check some channel updates. Some suggestions may be missing.");
                }
                return new List<VideoInfo>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel update videos for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to check channel updates. Some suggestions may be missing.");
            return new List<VideoInfo>();
        }
    }

    /// <summary>
    /// Gets videos from topic searches across all of YouTube.
    /// </summary>
    private async Task<List<VideoInfo>> GetTopicSearchVideos(string userId)
    {
        try
        {
            var userTopics = await _topicService.GetUserTopicsAsync(userId);
            if (!userTopics.Any())
            {
                return new List<VideoInfo>();
            }

            var topicQueries = userTopics.Select(t => t.Name).ToList();
            var publishedAfter = DateTime.UtcNow.AddDays(-DEFAULT_DAYS_LOOKBACK);

            // Use bulk topic searches method
            var result = await _youTubeService.GetBulkTopicSearchesAsync(
                topicQueries, publishedAfter, maxResultsPerTopic: 25);

            if (result.IsSuccess)
            {
                return result.Data ?? new List<VideoInfo>();
            }
            else
            {
                // Log the error but don't fail completely
                _logger.LogWarning("Topic searches failed: {Error}", result.ErrorMessage);
                if (!result.IsQuotaExceeded)
                {
                    await _messageCenterService.ShowErrorAsync("Failed to search for topic videos. Some suggestions may be missing.");
                }
                return new List<VideoInfo>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting topic search videos for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to search for topic videos. Some suggestions may be missing.");
            return new List<VideoInfo>();
        }
    }

    /// <summary>
    /// Smart deduplication with source tracking for bonus scoring.
    /// </summary>
    private List<VideoWithSource> ConsolidateVideoSources(List<VideoInfo> channelVideos, List<VideoInfo> topicVideos)
    {
        var videoMap = new Dictionary<string, VideoWithSource>();

        // Add channel videos
        foreach (var video in channelVideos)
        {
            videoMap[video.YouTubeVideoId] = new VideoWithSource
            {
                Video = video,
                Source = SuggestionSource.TrackedChannel,
                FoundViaChannel = true
            };
        }

        // Add topic videos, marking overlaps
        foreach (var video in topicVideos)
        {
            if (videoMap.TryGetValue(video.YouTubeVideoId, out var existing))
            {
                // Video found via both methods - this is significant!
                existing.Source = SuggestionSource.Both;
                existing.FoundViaTopic = true;
            }
            else
            {
                videoMap[video.YouTubeVideoId] = new VideoWithSource
                {
                    Video = video,
                    Source = SuggestionSource.TopicSearch,
                    FoundViaTopic = true
                };
            }
        }

        return videoMap.Values.ToList();
    }

    /// <summary>
    /// Scores videos with source-aware bonuses and unified algorithm.
    /// </summary>
    private async Task<List<VideoSuggestion>> ScoreVideosWithSourceContext(List<VideoWithSource> videos, string userId)
    {
        var suggestions = new List<VideoSuggestion>();
        var userTopics = await _topicService.GetUserTopicsAsync(userId);
        var channelRatings = await _channelRatingService.GetUserRatingsAsync(userId);

        foreach (var videoSource in videos)
        {
            var suggestion = new VideoSuggestion
            {
                Video = videoSource.Video,
                Source = videoSource.Source,
                Stage = ScoringStage.Preliminary
            };

            // Calculate component scores
            var channelRating = channelRatings.FirstOrDefault(r => r.YouTubeChannelId == videoSource.Video.ChannelId);
            var channelScore = channelRating != null ? channelRating.Stars * 2 : 6.0; // Convert 1-5 to 0-10 scale
            var recencyScore = CalculateRecencyScore(videoSource.Video.PublishedAt);
            var topicResult = CalculateTopicRelevanceFromTitle(videoSource.Video, userTopics.Select(t => t.Name).ToList());

            // Source-aware scoring with bonuses
            switch (videoSource.Source)
            {
                case SuggestionSource.TrackedChannel:
                    suggestion.Score =
                        (channelScore * CHANNEL_RATING_WEIGHT) +
                        (5.0 * TOPIC_RELEVANCE_WEIGHT) + // Neutral topic score
                        (recencyScore * RECENCY_WEIGHT);
                    suggestion.Reason = $"📺 New from {videoSource.Video.ChannelName}";
                    break;

                case SuggestionSource.TopicSearch:
                    suggestion.Score =
                        (6.0 * CHANNEL_RATING_WEIGHT) + // Neutral channel score
                        (topicResult.Score * TOPIC_RELEVANCE_WEIGHT) +
                        (recencyScore * RECENCY_WEIGHT);
                    suggestion.MatchedTopics = topicResult.MatchedTopics;
                    suggestion.Reason = $"🔍 Topics: {string.Join(", ", topicResult.MatchedTopics)}";
                    break;

                case SuggestionSource.Both:
                    // Best of both worlds with bonus!
                    suggestion.Score =
                        (channelScore * CHANNEL_RATING_WEIGHT) +
                        (topicResult.Score * TOPIC_RELEVANCE_WEIGHT) +
                        (recencyScore * RECENCY_WEIGHT) +
                        DUAL_SOURCE_BONUS; // Bonus for dual-source discovery

                    suggestion.MatchedTopics = topicResult.MatchedTopics;
                    suggestion.Reason = $"⭐ {videoSource.Video.ChannelName} + Topics: {string.Join(", ", topicResult.MatchedTopics)}";
                    break;
            }

            // Set score breakdown for analytics
            suggestion.ScoreBreakdown = new SuggestionScoreBreakdown
            {
                ChannelRatingScore = channelScore,
                TopicRelevanceScore = topicResult.Score,
                RecencyScore = recencyScore,
                DualSourceBonus = videoSource.Source == SuggestionSource.Both ? DUAL_SOURCE_BONUS : 0,
                BaseScore = (channelScore * CHANNEL_RATING_WEIGHT) +
                           (topicResult.Score * TOPIC_RELEVANCE_WEIGHT) +
                           (recencyScore * RECENCY_WEIGHT),
                TotalScore = suggestion.Score
            };

            suggestions.Add(suggestion);
        }

        return suggestions;
    }

    /// <summary>
    /// Calculates recency score based on how recently the video was published.
    /// </summary>
    private double CalculateRecencyScore(DateTime publishedAt)
    {
        var daysSincePublished = (DateTime.UtcNow - publishedAt).TotalDays;

        return daysSincePublished switch
        {
            <= 1 => 10,    // Today
            <= 3 => 8,     // Last 3 days
            <= 7 => 6,     // Last week
            <= 14 => 4,    // Last 2 weeks
            <= 30 => 2,    // Last month
            _ => 1         // Older than month
        };
    }

    /// <summary>
    /// Calculates topic relevance score based on title matching.
    /// </summary>
    private TopicRelevanceResult CalculateTopicRelevanceFromTitle(VideoInfo video, List<string> userTopicNames)
    {
        var result = new TopicRelevanceResult { Score = 5.0 }; // Neutral default

        if (!userTopicNames.Any())
            return result;

        var titleMatches = 0;
        var totalTopicWords = 0;
        var matchedTopics = new List<string>();

        foreach (var topicName in userTopicNames)
        {
            var topicWords = topicName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            totalTopicWords += topicWords.Length;

            var topicMatchCount = topicWords.Count(word =>
                video.Title.Contains(word, StringComparison.OrdinalIgnoreCase));

            titleMatches += topicMatchCount;

            // Require at least half the topic words to match for topic inclusion
            if (topicMatchCount >= Math.Ceiling(topicWords.Length / 2.0))
            {
                matchedTopics.Add(topicName);
            }
        }

        // Calculate score based on match ratio
        if (totalTopicWords > 0)
        {
            var matchRatio = (double)titleMatches / totalTopicWords;
            result.Score = Math.Min(10, matchRatio * 10);
        }

        result.MatchedTopics = matchedTopics;
        return result;
    }

    /// <summary>
    /// Calculates score distribution for analytics.
    /// </summary>
    private Dictionary<string, int> CalculateScoreDistribution(List<VideoSuggestion> suggestions)
    {
        var distribution = new Dictionary<string, int>();

        foreach (var suggestion in suggestions)
        {
            var bucket = $"{Math.Floor(suggestion.Score)}-{Math.Floor(suggestion.Score) + 1}";
            distribution[bucket] = distribution.GetValueOrDefault(bucket, 0) + 1;
        }

        return distribution;
    }

    /// <summary>
    /// Creates suggestion entities and saves them to the database.
    /// Enhanced to support topic relationships.
    /// </summary>
    private async Task<List<SuggestionEntity>> CreateSuggestionEntities(
        List<VideoSuggestion> videoSuggestions, List<VideoEntity> videoEntities, string userId)
    {
        var suggestions = new List<SuggestionEntity>();

        foreach (var videoSuggestion in videoSuggestions)
        {
            // Find the corresponding video entity
            var videoEntity = videoEntities.FirstOrDefault(v => v.YouTubeVideoId == videoSuggestion.Video.YouTubeVideoId);
            if (videoEntity == null)
                continue;

            // Get topic IDs if this suggestion has matched topics
            var topicIds = new List<Guid>();
            if (videoSuggestion.MatchedTopics?.Any() == true)
            {
                var userTopics = await _topicService.GetUserTopicsAsync(userId);
                topicIds = userTopics
                    .Where(t => videoSuggestion.MatchedTopics.Contains(t.Name))
                    .Select(t => t.Id)
                    .ToList();
            }

            // Use enhanced repository method if we have topic relationships
            SuggestionEntity suggestion;
            if (topicIds.Any())
            {
                suggestion = await _suggestionRepository.CreateSuggestionWithTopicsAsync(
                    userId, videoEntity.Id, videoSuggestion.Reason, topicIds);
            }
            else
            {
                // Fallback to existing method for channel-only suggestions
                suggestion = new SuggestionEntity
                {
                    UserId = userId,
                    VideoId = videoEntity.Id,
                    Reason = videoSuggestion.Reason,
                    IsApproved = false,
                    IsDenied = false
                };
                suggestions.Add(suggestion);
            }

            if (topicIds.Any())
            {
                suggestions.Add(suggestion);
            }
        }

        // Create any remaining suggestions without topics using existing bulk method
        var suggestionsWithoutTopics = suggestions.Where(s => s.Id == Guid.Empty).ToList();
        if (suggestionsWithoutTopics.Any())
        {
            var createdSuggestions = await _suggestionRepository.CreateSuggestionsAsync(suggestionsWithoutTopics);

            // Replace the temporary suggestions with the created ones
            for (int i = 0; i < suggestions.Count; i++)
            {
                if (suggestions[i].Id == Guid.Empty)
                {
                    var created = createdSuggestions.FirstOrDefault();
                    if (created != null)
                    {
                        suggestions[i] = created;
                        createdSuggestions.Remove(created);
                    }
                }
            }
        }

        return suggestions.Where(s => s.Id != Guid.Empty).ToList();
    }

    /// <summary>
    /// Maps a video entity to VideoInfo for display purposes.
    /// Enhanced to use stored video thumbnail URL when available.
    /// </summary>
    private VideoInfo MapVideoEntityToInfo(VideoEntity entity)
    {
        return new VideoInfo
        {
            YouTubeVideoId = entity.YouTubeVideoId,
            Title = entity.Title,
            ChannelId = entity.Channel.YouTubeChannelId,
            ChannelName = entity.Channel.Name,
            PublishedAt = entity.PublishedAt,
            ViewCount = entity.ViewCount,
            LikeCount = entity.LikeCount,
            CommentCount = entity.CommentCount,
            Duration = entity.Duration,
            // Use stored thumbnail URL if available, otherwise empty string for fallback
            ThumbnailUrl = entity.ThumbnailUrl ?? string.Empty,
            Description = entity.Description ?? string.Empty
        };
    }

    #endregion
}

/// <summary>
/// Result of topic relevance calculation.
/// </summary>
public class TopicRelevanceResult
{
    public double Score { get; set; }
    public List<string> MatchedTopics { get; set; } = new();
}