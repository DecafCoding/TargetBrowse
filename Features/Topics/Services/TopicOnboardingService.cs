using Microsoft.Extensions.Logging;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Models;
using TargetBrowse.Features.Topics.Models;
using TargetBrowse.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Topics.Services;

/// <summary>
/// Handles topic onboarding workflows including initial video suggestions.
/// Refactored to use BaseOnboardingService to eliminate duplication with ChannelOnboardingService.
/// Provides immediate value to users by populating suggestions when they create new topics.
/// Includes topic-specific relevance scoring to prioritize best matches.
/// </summary>
public class TopicOnboardingService : BaseOnboardingService, ITopicOnboardingService
{
    private const int RelevanceThreshold = 5;
    private const int LookbackDays = 365; // Look back 365 days for initial topic videos

    public TopicOnboardingService(
        ISuggestionDataService suggestionDataService,
        ISharedYouTubeService sharedYouTubeService,
        ILogger<TopicOnboardingService> logger)
        : base(suggestionDataService, sharedYouTubeService, logger)
    {
    }

    /// <summary>
    /// Adds initial videos from a newly created topic as suggestions.
    /// Uses the common onboarding workflow from BaseOnboardingService with topic-specific relevance scoring.
    /// </summary>
    public async Task<int> AddInitialVideosAsync(string userId, string topicName, Guid topicId)
    {
        _logger.LogInformation("Adding initial videos for topic {TopicName} ({TopicId}) for user {UserId}",
            topicName, topicId, userId);

        return await ExecuteOnboardingWorkflowAsync(
            userId,
            topicName,
            fetchVideos: () => FetchTopicVideosAsync(topicName),
            selectVideos: (videos) => SelectVideosByRelevance(videos, topicName),
            createSuggestions: (videoEntities) => _suggestionDataService.CreateTopicOnboardingSuggestionsAsync(
                userId, videoEntities, topicId, topicName));
    }

    /// <summary>
    /// Selects videos based on relevance scoring.
    /// Only includes videos with relevance score above the threshold.
    /// </summary>
    private List<VideoInfo> SelectVideosByRelevance(List<VideoInfo> videos, string topicName)
    {
        _logger.LogInformation("Found {VideoCount} total videos for topic {TopicName} before scoring",
            videos.Count, topicName);

        // Apply relevance scoring to all videos
        var scoredVideos = ScoreVideosByRelevance(videos, topicName);

        // Select videos above relevance threshold, ordered by score and recency
        var selectedVideos = scoredVideos
            .Where(v => v.RelevanceScore >= RelevanceThreshold)
            .OrderByDescending(v => v.RelevanceScore)
            .ThenByDescending(v => v.VideoInfo.PublishedAt)
            .Take(InitialVideosLimit)
            .ToList();

        _logger.LogInformation("Selected {Count} videos for topic {TopicName} (relevance >= {Threshold}, avg score: {AvgScore:F1})",
            selectedVideos.Count, topicName, RelevanceThreshold,
            selectedVideos.Any() ? selectedVideos.Average(v => v.RelevanceScore) : 0);

        return selectedVideos.Select(sv => sv.VideoInfo).ToList();
    }

    /// <summary>
    /// Applies relevance scoring to all videos using the same algorithm as TopicVideosService.
    /// Reuses the proven 0-10 scoring logic for consistency across the application.
    /// </summary>
    private List<ScoredVideoInfo> ScoreVideosByRelevance(List<VideoInfo> videos, string topicName)
    {
        var scoredVideos = new List<ScoredVideoInfo>();

        try
        {
            foreach (var video in videos)
            {
                var (score, matchedKeywords) = CalculateRelevanceScore(
                    video.Title,
                    video.Description ?? string.Empty,
                    topicName);

                scoredVideos.Add(new ScoredVideoInfo
                {
                    VideoInfo = video,
                    RelevanceScore = score,
                    MatchedKeywords = matchedKeywords
                });
            }

            _logger.LogDebug("Scored {VideoCount} videos for topic {TopicName} - avg score: {AvgScore:F1}",
                videos.Count, topicName, scoredVideos.Any() ? scoredVideos.Average(v => v.RelevanceScore) : 0);

            return scoredVideos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scoring videos for topic {TopicName}", topicName);

            // Fallback: return videos with neutral scores
            return videos.Select(v => new ScoredVideoInfo
            {
                VideoInfo = v,
                RelevanceScore = 5.0,
                MatchedKeywords = new List<string>()
            }).ToList();
        }
    }

    /// <summary>
    /// Calculates relevance score for a video based on topic matching.
    /// Extracted from TopicVideosService to ensure consistent scoring across features.
    /// </summary>
    private (double Score, List<string> MatchedKeywords) CalculateRelevanceScore(
        string videoTitle, string videoDescription, string topicName)
    {
        try
        {
            // Basic keyword matching algorithm (same as TopicVideosService)
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
    }

    /// <summary>
    /// Applies the prioritized selection algorithm to choose the best 50 videos.
    /// Prioritizes highly relevant medium duration videos, then highly relevant long,
    /// then remaining medium, then remaining long videos.
    /// </summary>
    private List<ScoredVideoInfo> ApplyPrioritizedSelection(List<ScoredVideoInfo> scoredVideos, int maxResults)
    {
        try
        {
            var selectedVideos = new List<ScoredVideoInfo>();

            // Phase 1: Add all highly relevant medium duration videos
            var highlyRelevantMedium = scoredVideos
                .Where(v => v.IsHighlyRelevant && v.IsMediumDuration)
                .OrderByDescending(v => v.RelevanceScore)
                .ThenByDescending(v => v.VideoInfo.PublishedAt)
                .ToList();

            selectedVideos.AddRange(highlyRelevantMedium);
            _logger.LogDebug($"Phase 1: Added {highlyRelevantMedium.Count} highly relevant medium videos");

            // Phase 2: If under limit, add highly relevant long duration videos
            if (selectedVideos.Count < maxResults)
            {
                var remainingSlots = maxResults - selectedVideos.Count;
                var highlyRelevantLong = scoredVideos
                    .Where(v => v.IsHighlyRelevant && v.IsLongDuration)
                    .Where(v => !selectedVideos.Any(s => s.VideoInfo.YouTubeVideoId == v.VideoInfo.YouTubeVideoId)) // Avoid duplicates
                    .OrderByDescending(v => v.RelevanceScore)
                    .ThenByDescending(v => v.VideoInfo.PublishedAt)
                    .Take(remainingSlots)
                    .ToList();

                selectedVideos.AddRange(highlyRelevantLong);
                _logger.LogDebug($"Phase 2: Added {highlyRelevantLong.Count} highly relevant long videos");
            }

            // Phase 3: If still under limit, add remaining medium duration videos
            if (selectedVideos.Count < maxResults)
            {
                var remainingSlots = maxResults - selectedVideos.Count;
                var remainingMedium = scoredVideos
                    .Where(v => !v.IsHighlyRelevant && v.IsMediumDuration)
                    .Where(v => !selectedVideos.Any(s => s.VideoInfo.YouTubeVideoId == v.VideoInfo.YouTubeVideoId))
                    .OrderByDescending(v => v.RelevanceScore)
                    .ThenByDescending(v => v.VideoInfo.PublishedAt)
                    .Take(remainingSlots)
                    .ToList();

                selectedVideos.AddRange(remainingMedium);
                _logger.LogDebug("Phase 3: Added {Count} remaining medium videos", remainingMedium.Count);
            }

            // Phase 4: If still under limit, add remaining long duration videos
            if (selectedVideos.Count < maxResults)
            {
                var remainingSlots = maxResults - selectedVideos.Count;
                var remainingLong = scoredVideos
                    .Where(v => !v.IsHighlyRelevant && v.IsLongDuration)
                    .Where(v => !selectedVideos.Any(s => s.VideoInfo.YouTubeVideoId == v.VideoInfo.YouTubeVideoId))
                    .OrderByDescending(v => v.RelevanceScore)
                    .ThenByDescending(v => v.VideoInfo.PublishedAt)
                    .Take(remainingSlots)
                    .ToList();

                selectedVideos.AddRange(remainingLong);
                _logger.LogDebug("Phase 4: Added {Count} remaining long videos", remainingLong.Count);
            }

            // Final selection summary
            var finalSelection = selectedVideos.Take(maxResults).ToList();
            var highlyRelevantCount = finalSelection.Count(v => v.IsHighlyRelevant);
            var mediumCount = finalSelection.Count(v => v.IsMediumDuration);
            var longCount = finalSelection.Count(v => v.IsLongDuration);

            _logger.LogInformation("Prioritized selection complete: {Total} videos selected ({HighlyRelevant} highly relevant, {Medium} medium, {Long} long)",
                finalSelection.Count, highlyRelevantCount, mediumCount, longCount);

            return finalSelection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during prioritized selection");

            // Fallback: return top videos by score only
            return scoredVideos
                .OrderByDescending(v => v.RelevanceScore)
                .ThenByDescending(v => v.VideoInfo.PublishedAt)
                .Take(maxResults)
                .ToList();
        }
    }

    /// <summary>
    /// Performs complete topic onboarding including initial video discovery.
    /// </summary>
    public async Task<TopicOnboardingResult> OnboardTopicAsync(string userId, string topicName, Guid topicId)
    {
        var result = new TopicOnboardingResult
        {
            TopicCreated = true // Assume topic was already created successfully
        };

        try
        {
            _logger.LogInformation("Starting topic onboarding for {TopicName} for user {UserId}",
                topicName, userId);

            // Add initial videos (non-blocking - don't fail if this doesn't work)
            try
            {
                result.InitialVideosAdded = await AddInitialVideosAsync(userId, topicName, topicId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add initial videos for topic {TopicName}, but topic was created successfully",
                    topicName);
                result.Warnings.Add("Topic created successfully, but could not retrieve video suggestions at this time.");
            }

            _logger.LogInformation("Topic onboarding completed for {TopicName}: {VideoCount} initial videos added",
                topicName, result.InitialVideosAdded);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during topic onboarding for {TopicName} and user {UserId}",
                topicName, userId);
            result.Errors.Add("An unexpected error occurred during topic setup. The topic was created but video suggestions may be limited.");
            return result;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Fetches recent videos from YouTube matching the topic, excluding shorts.
    /// Uses SharedYouTubeService to ensure consistent filtering with suggestion generation.
    /// </summary>
    private async Task<YouTubeApiResult<List<VideoInfo>>> FetchTopicVideosAsync(string topicName)
    {
        try
        {
            _logger.LogInformation("Fetching initial videos for topic {TopicName} - excluding shorts",
                topicName);

            // Use SharedYouTubeService for topic search with lookback date
            var publishedAfter = DateTime.UtcNow.AddDays(-LookbackDays);

            var apiResult = await _sharedYouTubeService.SearchVideosByTopicAsync(topicName, publishedAfter);

            if (apiResult.IsSuccess && apiResult.Data?.Any() == true)
            {
                _logger.LogInformation("Successfully fetched {VideoCount} videos for topic {TopicName} (shorts excluded)",
                    apiResult.Data.Count, topicName);

                return YouTubeApiResult<List<VideoInfo>>.Success(apiResult.Data);
            }
            else
            {
                var errorMessage = apiResult.ErrorMessage ?? "No videos found for topic";
                _logger.LogWarning("Failed to fetch videos for topic {TopicName}: {Error}",
                    topicName, errorMessage);

                return YouTubeApiResult<List<VideoInfo>>.Failure(errorMessage, apiResult.IsQuotaExceeded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching videos for topic {TopicName}", topicName);
            return YouTubeApiResult<List<VideoInfo>>.Failure($"Failed to fetch topic videos: {ex.Message}");
        }
    }

    #endregion
}