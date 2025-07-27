using TargetBrowse.Features.Topics.Models;

namespace TargetBrowse.Features.Topics.Services;

/// <summary>
/// Service interface for topic management operations.
/// Handles business logic for user topics with validation and limits.
/// </summary>
public interface ITopicService
{
    /// <summary>
    /// Adds a new topic for the specified user.
    /// Enforces business rules: 10 topic limit, no duplicates.
    /// </summary>
    /// <param name="userId">User ID to add topic for</param>
    /// <param name="topicName">Name of the topic to add</param>
    /// <returns>True if successful, false if validation failed</returns>
    Task<bool> AddTopicAsync(string userId, string topicName);

    /// <summary>
    /// Gets all topics for the specified user ordered by creation date (newest first).
    /// Returns display models suitable for UI presentation.
    /// </summary>
    /// <param name="userId">User ID to get topics for</param>
    /// <returns>List of topics for display, empty list if none found</returns>
    Task<List<TopicDisplayModel>> GetUserTopicsAsync(string userId);

    /// <summary>
    /// Gets the current count of topics for a user.
    /// Used for validation and UI display.
    /// </summary>
    /// <param name="userId">User ID to count topics for</param>
    /// <returns>Number of topics the user currently has</returns>
    Task<int> GetTopicCountAsync(string userId);

    /// <summary>
    /// Checks if a topic name already exists for the user (case-insensitive).
    /// </summary>
    /// <param name="userId">User ID to check for</param>
    /// <param name="topicName">Topic name to check</param>
    /// <returns>True if topic already exists, false otherwise</returns>
    Task<bool> TopicExistsAsync(string userId, string topicName);
}