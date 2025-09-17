using TargetBrowse.Features.TopicVideos.Models;

namespace TargetBrowse.Features.TopicVideos.Services;

/// <summary>
/// Service interface for topic-based video discovery.
/// Handles searching YouTube for videos related to specific topics.
/// </summary>
public interface ITopicVideosService
{
    /// <summary>
    /// Gets recent videos from YouTube for a specific topic.
    /// Searches for videos published within the last year and returns them sorted by relevance.
    /// </summary>
    /// <param name="topicId">Unique identifier of the topic</param>
    /// <param name="maxResults">Maximum number of videos to return (default: 50)</param>
    /// <returns>List of videos related to the topic, or empty list if no videos found</returns>
    Task<List<TopicVideoDisplayModel>> GetRecentVideosAsync(Guid topicId, string currentUserId, int maxResults = 50);

    /// <summary>
    /// Gets recent videos from YouTube for a specific topic by topic name.
    /// Alternative method when only the topic name is available.
    /// </summary>
    /// <param name="topicName">Name of the topic to search for</param>
    /// <param name="maxResults">Maximum number of videos to return (default: 50)</param>
    /// <returns>List of videos related to the topic, or empty list if no videos found</returns>
    Task<List<TopicVideoDisplayModel>> GetRecentVideosByNameAsync(string topicName, int maxResults = 50);

    /// <summary>
    /// Calculates relevance score for a video based on topic matching.
    /// Used internally to rank video results by relevance to the topic.
    /// </summary>
    /// <param name="videoTitle">Title of the video</param>
    /// <param name="videoDescription">Description of the video</param>
    /// <param name="topicName">Topic name to match against</param>
    /// <returns>Relevance score (0-10) and matched keywords</returns>
    Task<(double Score, List<string> MatchedKeywords)> CalculateRelevanceScore(
        string videoTitle, string videoDescription, string topicName);

    /// <summary>
    /// Checks if the topic exists and the user has access to it.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="topicId">Topic identifier</param>
    /// <returns>True if topic exists and user has access, false otherwise</returns>
    Task<bool> ValidateTopicAccess(string userId, Guid topicId);

    /// <summary>
    /// Gets topic information for display purposes.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="topicId">Topic identifier</param>
    /// <returns>Topic display model or null if not found</returns>
    Task<Topics.Models.TopicDisplayModel?> GetTopicAsync(string userId, Guid topicId);
}