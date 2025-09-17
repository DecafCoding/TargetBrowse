using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Services.Interfaces;

/// <summary>
/// Shared YouTube API service interface for common functionality across multiple features.
/// Provides core YouTube API operations used by multiple vertical slices.
/// </summary>
public interface ISharedYouTubeService
{
    /// <summary>
    /// Gets new videos from a channel since the specified date.
    /// Used by ChannelVideos and Suggestions features.
    /// </summary>
    /// <param name="youTubeChannelId">YouTube channel identifier</param>
    /// <param name="since">Get videos published after this date</param>
    /// <param name="maxResults">Maximum number of videos to return (1-50)</param>
    /// <returns>API result containing list of videos or error information</returns>
    Task<YouTubeApiResult<List<VideoInfo>>> GetChannelVideosSinceAsync(
        string youTubeChannelId,
        DateTime since,
        int maxResults = 50);

    /// <summary>
    /// Searches for videos across all of YouTube matching the specified topic.
    /// Used by TopicVideos and Suggestions features.
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
    /// Used by multiple features for enriching video information.
    /// </summary>
    /// <param name="youTubeVideoIds">List of YouTube video IDs (max 50 per batch)</param>
    /// <returns>API result containing detailed video information</returns>
    Task<YouTubeApiResult<List<VideoInfo>>> GetVideoDetailsByIdsAsync(List<string> youTubeVideoIds);

    /// <summary>
    /// Validates that multiple YouTube video IDs exist and are accessible.
    /// Used for batch validation without retrieving full video details.
    /// </summary>
    /// <param name="youTubeVideoIds">Video IDs to validate</param>
    /// <returns>Validation results indicating which videos exist</returns>
    Task<YouTubeApiResult<Dictionary<string, bool>>> ValidateVideoIdsAsync(List<string> youTubeVideoIds);

    /// <summary>
    /// Checks if the YouTube API is currently available and within quota limits.
    /// </summary>
    /// <returns>API availability status with quota information</returns>
    Task<ApiAvailabilityResult> GetApiAvailabilityAsync();

    /// <summary>
    /// Gets quota reset time and remaining quota for planning future requests.
    /// </summary>
    /// <returns>Quota status information</returns>
    Task<QuotaStatus> GetQuotaStatusAsync();
}