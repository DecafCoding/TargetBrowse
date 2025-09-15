using Microsoft.Extensions.Logging;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Data;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Features.Topics.Models;
using TargetBrowse.Features.Videos.Data;
using TargetBrowse.Services.YouTube;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Topics.Services;

/// <summary>
/// Handles topic onboarding workflows including initial video suggestions.
/// Mirrors ChannelOnboardingService pattern for consistency and maintainability.
/// Provides immediate value to users by populating suggestions when they create new topics.
/// </summary>
public class TopicOnboardingService : ITopicOnboardingService
{
    private readonly IVideoRepository _videoRepository;
    private readonly ISuggestionRepository _suggestionRepository;
    private readonly ISharedYouTubeService _sharedYouTubeService;
    private readonly ILogger<TopicOnboardingService> _logger;

    private const int InitialVideosLimit = 50;
    private const int LookbackDays = 365; // Look back 30 days for initial topic videos (vs 365 for channels)

    public TopicOnboardingService(
        IVideoRepository videoRepository,
        ISuggestionRepository suggestionRepository,
        ISharedYouTubeService sharedYouTubeService,
        ILogger<TopicOnboardingService> logger)
    {
        _videoRepository = videoRepository;
        _suggestionRepository = suggestionRepository;
        _sharedYouTubeService = sharedYouTubeService;
        _logger = logger;
    }

    /// <summary>
    /// Adds initial videos from a newly created topic as suggestions.
    /// Enhanced to use relevance scoring and prioritized selection.
    /// </summary>
    public async Task<int> AddInitialVideosAsync(string userId, string topicName, Guid topicId)
    {
        try
        {
            _logger.LogInformation("Adding initial videos for topic {TopicName} ({TopicId}) for user {UserId}",
                topicName, topicId, userId);

            // 1. Fetch videos from YouTube (already does dual medium+long search with deduplication)
            var topicVideosResult = await FetchTopicVideosAsync(topicName);

            if (!topicVideosResult.IsSuccess || !topicVideosResult.Data?.Any() == true)
            {
                _logger.LogWarning("Failed to fetch initial videos for topic {TopicName}: {Error}",
                    topicName, topicVideosResult.ErrorMessage);
                return 0;
            }

            var allVideos = topicVideosResult.Data;
            _logger.LogInformation("Found {VideoCount} total videos for topic {TopicName} before scoring",
                allVideos.Count, topicName);

            // 2. Apply relevance scoring to all videos
            var scoredVideos = ScoreVideosByRelevance(allVideos, topicName);

            // 3. Apply prioritized selection algorithm
            var selectedVideos = ApplyPrioritizedSelection(scoredVideos, InitialVideosLimit);

            _logger.LogInformation("Selected {SelectedCount} videos for topic {TopicName} after prioritized selection (highly relevant: {HighlyRelevantCount})",
                selectedVideos.Count, topicName, selectedVideos.Count(v => v.RelevanceScore >= 7.0));

            // 4. Ensure all selected videos exist in the database
            var videoEntities = await _videoRepository.EnsureVideosExistAsync(selectedVideos.Select(sv => sv.VideoInfo).ToList());

            // 5. Create suggestion entities for selected videos
            var suggestions = await CreateInitialVideoSuggestions(userId, videoEntities, topicName, topicId);

            // 6. Save suggestions to database (bypassing normal limits for onboarding)
            if (suggestions.Any())
            {
                var createdSuggestions = await _suggestionRepository.CreateTopicOnboardingSuggestionsAsync(
                    userId, videoEntities, topicId, topicName);

                _logger.LogInformation("Created {SuggestionCount} initial suggestions for topic {TopicName} (avg relevance: {AvgRelevance:F1})",
                    createdSuggestions.Count, topicName, selectedVideos.Any() ? selectedVideos.Average(v => v.RelevanceScore) : 0);

                return createdSuggestions.Count;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding initial videos for topic {TopicName} for user {UserId}",
                topicName, userId);
            return 0;
        }
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
            _logger.LogDebug("Phase 1: Added {Count} highly relevant medium videos", highlyRelevantMedium.Count);

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
                _logger.LogDebug("Phase 2: Added {Count} highly relevant long videos", highlyRelevantLong.Count);
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

            var apiResult = await _sharedYouTubeService.SearchVideosByTopicAsync(
                topicName,
                publishedAfter,
                InitialVideosLimit);

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

    /// <summary>
    /// Creates suggestion entities for initial videos from a new topic.
    /// This method prepares the data but doesn't save - the repository method handles persistence.
    /// </summary>
    private async Task<List<SuggestionEntity>> CreateInitialVideoSuggestions(
        string userId, List<VideoEntity> videoEntities, string topicName, Guid topicId)
    {
        var suggestions = new List<SuggestionEntity>();

        foreach (var videoEntity in videoEntities)
        {
            // Check if suggestion already exists to avoid duplicates
            var hasExisting = await _suggestionRepository.HasPendingSuggestionForVideoAsync(userId, videoEntity.Id);
            if (hasExisting)
            {
                _logger.LogDebug("Skipping video {VideoId} - suggestion already exists for user {UserId}",
                    videoEntity.YouTubeVideoId, userId);
                continue;
            }

            suggestions.Add(new SuggestionEntity
            {
                UserId = userId,
                VideoId = videoEntity.Id,
                Reason = $"🎯 New Topic: {topicName}",
                IsApproved = false,
                IsDenied = false
                // Note: SuggestionSource.NewTopic will be handled by the display logic
            });
        }

        _logger.LogDebug("Created {SuggestionCount} suggestion entities from {VideoCount} videos for topic {TopicName}",
            suggestions.Count, videoEntities.Count, topicName);

        return suggestions;
    }

    #endregion
}