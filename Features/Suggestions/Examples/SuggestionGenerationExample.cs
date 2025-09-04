using TargetBrowse.Features.Suggestions.Services;
using TargetBrowse.Features.Suggestions.Models;
using Microsoft.AspNetCore.Mvc;

namespace TargetBrowse.Features.Suggestions.Examples;

/// <summary>
/// Example of how to use the enhanced YouTube API service in the suggestion generation workflow.
/// This demonstrates the integration patterns and best practices for the enhanced service.
/// </summary>
public class SuggestionGenerationExample
{
    private readonly ISuggestionYouTubeService _youTubeService;
    private readonly ILogger<SuggestionGenerationExample> _logger;

    public SuggestionGenerationExample(
        ISuggestionYouTubeService youTubeService,
        ILogger<SuggestionGenerationExample> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    /// <summary>
    /// Example: Generate suggestions with comprehensive error handling and quota management.
    /// </summary>
    public async Task<SuggestionResult> GenerateSuggestionsExample(string userId)
    {
        var result = new SuggestionResult();

        try
        {
            // 1. Check API availability before starting
            var apiAvailability = await _youTubeService.GetApiAvailabilityAsync();
            if (!apiAvailability.IsAvailable)
            {
                _logger.LogWarning("YouTube API not available: {Reason}", apiAvailability.ErrorMessage);
                return SuggestionResult.Failure($"YouTube API unavailable: {apiAvailability.ErrorMessage}");
            }

            // 2. Estimate quota cost before proceeding
            var channelRequests = await GetChannelUpdateRequests(userId);
            var topicQueries = await GetUserTopicQueries(userId);

            var quotaCost = await _youTubeService.EstimateQuotaCostAsync(
                channelRequests.Count, topicQueries.Count, estimatedVideosFound: 200);

            if (quotaCost.ExceedsRemainingQuota)
            {
                _logger.LogWarning("Insufficient quota for suggestion generation: {Required} > {Available}",
                    quotaCost.TotalEstimatedCost, quotaCost.ProjectedQuotaUsagePercentage);

                return SuggestionResult.Failure(
                    "Insufficient YouTube API quota remaining for suggestion generation. " +
                    "Please try again later or reduce the number of topics and channels.");
            }

            _logger.LogInformation("Starting suggestion generation with estimated cost: {Cost} quota units",
                quotaCost.TotalEstimatedCost);

            // 3. Get channel updates using bulk operation
            var channelResult = await _youTubeService.GetBulkChannelUpdatesAsync(channelRequests);

            if (channelResult.IsSuccess)
            {
                result.ChannelVideosFound = channelResult.Data?.Count ?? 0;
                _logger.LogInformation("Found {Count} videos from channel updates", result.ChannelVideosFound);
            }
            else if (channelResult.IsQuotaExceeded)
            {
                _logger.LogWarning("Quota exceeded during channel updates");
                return SuggestionResult.Failure("YouTube API quota exceeded during channel updates");
            }
            else
            {
                _logger.LogWarning("Channel updates failed: {Error}", channelResult.ErrorMessage);
                // Continue with topic search even if channel updates fail
            }

            // 4. Get topic-based videos using bulk operation
            var publishedAfter = DateTime.UtcNow.AddDays(-7); // Last 7 days
            var topicResult = await _youTubeService.GetBulkTopicSearchesAsync(
                topicQueries, publishedAfter, maxResultsPerTopic: 25);

            if (topicResult.IsSuccess)
            {
                result.TopicVideosFound = topicResult.Data?.Count ?? 0;
                _logger.LogInformation("Found {Count} videos from topic searches", result.TopicVideosFound);
            }
            else if (topicResult.IsQuotaExceeded)
            {
                _logger.LogWarning("Quota exceeded during topic searches");
                // Proceed with whatever channel videos we found
            }
            else
            {
                _logger.LogWarning("Topic searches failed: {Error}", topicResult.ErrorMessage);
            }

            // 5. Combine and deduplicate results
            var allVideos = new List<VideoInfo>();
            if (channelResult.IsSuccess)
                allVideos.AddRange(channelResult.Data ?? new List<VideoInfo>());
            if (topicResult.IsSuccess)
                allVideos.AddRange(topicResult.Data ?? new List<VideoInfo>());

            var uniqueVideos = allVideos
                .GroupBy(v => v.YouTubeVideoId)
                .Select(g => g.First())
                .ToList();

            result.AllDiscoveredVideos = uniqueVideos;
            result.DuplicatesFound = allVideos.Count - uniqueVideos.Count;

            _logger.LogInformation("Discovered {Unique} unique videos ({Duplicates} duplicates removed)",
                uniqueVideos.Count, result.DuplicatesFound);

            // 6. Get final API usage statistics
            result.ApiUsage = await _youTubeService.GetCurrentApiUsageAsync();

            _logger.LogInformation("Suggestion generation completed. API usage: {Usage}",
                result.ApiUsage.FormattedQuotaUsage);

            result.IsSuccess = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during suggestion generation for user {UserId}", userId);
            return SuggestionResult.Failure($"Suggestion generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Example: Check API status and provide user-friendly feedback.
    /// </summary>
    public async Task<string> GetApiStatusMessage()
    {
        try
        {
            var availability = await _youTubeService.GetApiAvailabilityAsync();
            var quotaStatus = await _youTubeService.GetQuotaStatusAsync();

            if (!availability.IsAvailable)
            {
                if (availability.IsQuotaExceeded)
                {
                    return $"YouTube API quota exhausted ({quotaStatus.UsagePercentage:F1}%). " +
                           $"Service will resume at {quotaStatus.ResetTime:HH:mm} UTC.";
                }

                return $"YouTube API temporarily unavailable: {availability.ErrorMessage}";
            }

            if (quotaStatus.IsApproachingLimit)
            {
                return $"YouTube API quota at {quotaStatus.UsagePercentage:F1}%. " +
                       $"Limited functionality until reset at {quotaStatus.ResetTime:HH:mm} UTC.";
            }

            return $"YouTube API available. Quota: {quotaStatus.RemainingQuota:N0} remaining " +
                   $"({quotaStatus.UsagePercentage:F1}% used)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking API status");
            return "Unable to check YouTube API status. Please try again.";
        }
    }

    /// <summary>
    /// Example: Validate video IDs before processing.
    /// </summary>
    public async Task<List<string>> ValidateVideoIds(List<string> videoIds)
    {
        try
        {
            var validationResult = await _youTubeService.ValidateVideoIdsAsync(videoIds);

            if (validationResult.IsSuccess)
            {
                var validIds = validationResult.Data?
                    .Where(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .ToList() ?? new List<string>();

                var invalidCount = videoIds.Count - validIds.Count;
                if (invalidCount > 0)
                {
                    _logger.LogInformation("Video validation: {Valid} valid, {Invalid} invalid out of {Total}",
                        validIds.Count, invalidCount, videoIds.Count);
                }

                return validIds;
            }

            _logger.LogWarning("Video validation failed: {Error}", validationResult.ErrorMessage);
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating video IDs");
            return new List<string>();
        }
    }

    /// <summary>
    /// Example: Preload video details for better performance.
    /// </summary>
    public async Task<bool> PreloadVideoDetailsForSuggestions(List<VideoInfo> videos)
    {
        try
        {
            _logger.LogInformation("Preloading details for {Count} videos", videos.Count);

            var success = await _youTubeService.PreloadVideoDetailsAsync(videos);

            if (success)
            {
                _logger.LogInformation("Successfully preloaded video details");
            }
            else
            {
                _logger.LogWarning("Failed to preload some video details");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preloading video details");
            return false;
        }
    }

    /// <summary>
    /// Example: Search within specific channels for targeted content discovery.
    /// </summary>
    //public async Task<List<VideoInfo>> SearchTopicInUserChannels(string userId, string topic)
    //{
    //    try
    //    {
    //        var userChannels = await GetUserTrackedChannelIds(userId);

    //        if (!userChannels.Any())
    //        {
    //            _logger.LogInformation("No tracked channels found for user {UserId}", userId);
    //            return new List<VideoInfo>();
    //        }

    //        var publishedAfter = DateTime.UtcNow.AddDays(-30); // Last 30 days
    //        var result = await _youTubeService.SearchTopicInChannelsAsync(
    //            topic, userChannels, publishedAfter, maxResults: 50);

    //        if (result.IsSuccess)
    //        {
    //            _logger.LogInformation("Found {Count} videos for topic '{Topic}' in user's {ChannelCount} channels",
    //                result.Data?.Count ?? 0, topic, userChannels.Count);

    //            return result.Data ?? new List<VideoInfo>();
    //        }

    //        _logger.LogWarning("Failed to search topic in channels: {Error}", result.ErrorMessage);
    //        return new List<VideoInfo>();
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error searching topic '{Topic}' in user channels", topic);
    //        return new List<VideoInfo>();
    //    }
    //}

    #region Helper Methods (Mock implementations for example)

    /// <summary>
    /// Mock method to get channel update requests for a user.
    /// In real implementation, this would query the database.
    /// </summary>
    private async Task<List<ChannelUpdateRequest>> GetChannelUpdateRequests(string userId)
    {
        // This is a mock implementation
        // In reality, this would query your database for user's tracked channels
        await Task.Delay(1); // Simulate async operation

        return new List<ChannelUpdateRequest>
        {
            new ChannelUpdateRequest
            {
                YouTubeChannelId = "UC_x5XG1OV2P6uZZ5FSM9Ttw", // Example channel ID
                ChannelName = "Google for Developers",
                LastCheckDate = DateTime.UtcNow.AddDays(-1),
                UserRating = 5,
                MaxResults = 25
            },
            new ChannelUpdateRequest
            {
                YouTubeChannelId = "UCsBjURrPoezykLs9EqgamOA", // Example channel ID
                ChannelName = "Fireship",
                LastCheckDate = DateTime.UtcNow.AddHours(-12),
                UserRating = 4,
                MaxResults = 25
            }
        };
    }

    /// <summary>
    /// Mock method to get user's topic queries.
    /// In real implementation, this would query the database.
    /// </summary>
    private async Task<List<string>> GetUserTopicQueries(string userId)
    {
        // This is a mock implementation
        await Task.Delay(1);

        return new List<string>
        {
            "ASP.NET Core",
            "Blazor",
            "C# programming",
            "Web development",
            "Software architecture"
        };
    }

    /// <summary>
    /// Mock method to get user's tracked channel IDs.
    /// In real implementation, this would query the database.
    /// </summary>
    private async Task<List<string>> GetUserTrackedChannelIds(string userId)
    {
        await Task.Delay(1);

        return new List<string>
        {
            "UC_x5XG1OV2P6uZZ5FSM9Ttw", // Google for Developers
            "UCsBjURrPoezykLs9EqgamOA", // Fireship
            "UCVhQ2NnY5Rskt6UjCUkJ_DA"  // dotNET
        };
    }

    #endregion
}