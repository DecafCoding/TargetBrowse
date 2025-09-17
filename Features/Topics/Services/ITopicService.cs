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
    /// Deletes a topic for the specified user.
    /// Uses soft delete to maintain data integrity.
    /// </summary>
    /// <param name="userId">User ID requesting the deletion</param>
    /// <param name="topicId">ID of the topic to delete</param>
    /// <returns>True if successful, false if validation failed or topic not found</returns>
    Task<bool> DeleteTopicAsync(string userId, Guid topicId);
}