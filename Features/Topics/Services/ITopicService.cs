using TargetBrowse.Features.Topics.Models;

namespace TargetBrowse.Features.Topics.Services
{
    /// <summary>
    /// Service interface for managing user topics.
    /// Handles business logic for topic CRUD operations and validation.
    /// </summary>
    public interface ITopicService
    {
        /// <summary>
        /// Gets all topics for a specific user.
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <returns>List of user's topics</returns>
        Task<List<TopicDto>> GetUserTopicsAsync(string userId);

        /// <summary>
        /// Adds a new topic for a user.
        /// Validates topic limit and name uniqueness.
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="topicName">The topic name to add</param>
        /// <returns>Validation result with created topic if successful</returns>
        Task<TopicValidationResult> AddTopicAsync(string userId, string topicName);

        /// <summary>
        /// Updates an existing topic name.
        /// Validates name uniqueness within user's topics.
        /// </summary>
        /// <param name="userId">The user's ID (for security)</param>
        /// <param name="topicId">The topic ID to update</param>
        /// <param name="newName">The new topic name</param>
        /// <returns>Validation result</returns>
        Task<TopicValidationResult> UpdateTopicAsync(string userId, Guid topicId, string newName);

        /// <summary>
        /// Deletes a topic for a user.
        /// </summary>
        /// <param name="userId">The user's ID (for security)</param>
        /// <param name="topicId">The topic ID to delete</param>
        /// <returns>True if deleted successfully</returns>
        Task<bool> DeleteTopicAsync(string userId, Guid topicId);

        /// <summary>
        /// Checks if user can add more topics (under 10 limit).
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <returns>True if user can add more topics</returns>
        Task<bool> CanAddTopicAsync(string userId);

        /// <summary>
        /// Gets the current topic count for a user.
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <returns>Number of topics user currently has</returns>
        Task<int> GetTopicCountAsync(string userId);

        /// <summary>
        /// Validates a topic name for a user.
        /// Checks length, uniqueness, and formatting.
        /// </summary>
        /// <param name="userId">The user's ID</param>
        /// <param name="topicName">The topic name to validate</param>
        /// <param name="excludeTopicId">Topic ID to exclude from uniqueness check (for updates)</param>
        /// <returns>Validation result</returns>
        Task<TopicValidationResult> ValidateTopicNameAsync(string userId, string topicName, Guid? excludeTopicId = null);
    }
}