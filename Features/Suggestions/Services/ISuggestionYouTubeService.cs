using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Suggestions.Services;

/// <summary>
/// Enhanced YouTube API service interface for suggestion generation with comprehensive error handling,
/// quota management, and Message Center integration.
/// </summary>
public interface ISuggestionYouTubeService
{
    /// <summary>
    /// Searches for videos across all of YouTube matching the specified topic.
    /// Optimized for topic-based content discovery with intelligent filtering.
    /// </summary>
    /// <param name="topicQuery">Topic or keyword to search for</param>
    /// <param name="publishedAfter">Only include videos published after this date (optional)</param>
    /// <param name="maxResults">Maximum number of videos to return (1-50)</param>
    /// <returns>API result containing list of videos or error information</returns>
    Task<YouTubeApiResult<List<VideoInfo>>> SearchVideosByTopicAsync(
        string topicQuery,
        DateTime? publishedAfter = null,
        int maxResults = 50);

    /// <summary>
    /// Gets detailed information about multiple videos by their YouTube IDs.
    /// Automatically batches requests to optimize quota usage.
    /// </summary>
    /// <param name="youTubeVideoIds">List of YouTube video IDs (max 50 per batch)</param>
    /// <returns>API result containing detailed video information</returns>
    Task<YouTubeApiResult<List<VideoInfo>>> GetVideoDetailsByIdsAsync(List<string> youTubeVideoIds);

    /// <summary>
    /// Performs bulk channel update checking for multiple channels.
    /// Optimizes API usage by batching requests where possible.
    /// </summary>
    /// <param name="channelUpdateRequests">List of channels to check with their last check dates</param>
    /// <returns>API result containing all discovered videos from all channels</returns>
    Task<YouTubeApiResult<List<VideoInfo>>> GetBulkChannelUpdatesAsync(
        List<ChannelUpdateRequest> channelUpdateRequests);

    /// <summary>
    /// Performs bulk topic searches for multiple topics.
    /// Optimizes API usage and combines results intelligently.
    /// </summary>
    /// <param name="topicQueries">List of topics to search</param>
    /// <param name="publishedAfter">Only include videos published after this date (optional)</param>
    /// <param name="maxResultsPerTopic">Maximum results per topic (default 25)</param>
    /// <returns>API result containing all discovered videos from all topics</returns>
    Task<YouTubeApiResult<List<VideoInfo>>> GetBulkTopicSearchesAsync(
        List<string> topicQueries,
        DateTime? publishedAfter = null,
        int maxResultsPerTopic = 25);

    /// <summary>
    /// Checks if the YouTube API is currently available and within quota limits.
    /// Provides detailed availability information.
    /// </summary>
    /// <returns>API availability status with quota information</returns>
    Task<ApiAvailabilityResult> GetApiAvailabilityAsync();

    /// <summary>
    /// Gets estimated API quota cost for a suggestion generation request.
    /// Provides detailed breakdown of expected costs.
    /// </summary>
    /// <param name="channelCount">Number of channels to check for updates</param>
    /// <param name="topicCount">Number of topics to search</param>
    /// <param name="estimatedVideosFound">Estimated number of videos that will be found</param>
    /// <returns>Detailed quota cost estimation</returns>
    Task<QuotaCostEstimate> EstimateQuotaCostAsync(
        int channelCount,
        int topicCount,
        int estimatedVideosFound = 100);

    /// <summary>
    /// Gets current API usage statistics for quota monitoring and analytics.
    /// </summary>
    /// <returns>Comprehensive API usage information</returns>
    Task<SuggestionApiUsage> GetCurrentApiUsageAsync();

    /// <summary>
    /// Validates that multiple YouTube video IDs exist and are accessible.
    /// Used for batch validation without retrieving full video details.
    /// </summary>
    /// <param name="youTubeVideoIds">Video IDs to validate</param>
    /// <returns>Validation results indicating which videos exist</returns>
    Task<YouTubeApiResult<Dictionary<string, bool>>> ValidateVideoIdsAsync(List<string> youTubeVideoIds);

    /// <summary>
    /// Searches for videos within specific channels matching a topic.
    /// More targeted than general topic search, useful for channel-specific content discovery.
    /// </summary>
    /// <param name="topicQuery">Topic or keyword to search for</param>
    /// <param name="youTubeChannelIds">List of channel IDs to search within</param>
    /// <param name="publishedAfter">Only include videos published after this date (optional)</param>
    /// <param name="maxResults">Maximum number of videos to return total</param>
    /// <returns>API result containing videos matching topic within specified channels</returns>
    //Task<YouTubeApiResult<List<VideoInfo>>> SearchTopicInChannelsAsync(
    //    string topicQuery,
    //    List<string> youTubeChannelIds,
    //    DateTime? publishedAfter = null,
    //    int maxResults = 25);

    /// <summary>
    /// Preloads video details for a list of videos to optimize future requests.
    /// Useful for warming cache before suggestion processing.
    /// </summary>
    /// <param name="videoInfoList">Videos to preload details for</param>
    /// <returns>Success status of preloading operation</returns>
    Task<bool> PreloadVideoDetailsAsync(List<VideoInfo> videoInfoList);

    /// <summary>
    /// Gets quota reset time and remaining quota for planning future requests.
    /// </summary>
    /// <returns>Quota status information</returns>
    Task<QuotaStatus> GetQuotaStatusAsync();
}