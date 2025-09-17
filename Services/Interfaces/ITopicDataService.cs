using TargetBrowse.Data.Entities;

namespace TargetBrowse.Services.Interfaces
{
    /// <summary>
    /// Data access service for topic-related operations.
    /// Provides shared data access methods used across multiple services.
    /// </summary>
    public interface ITopicDataService
    {
        /// <summary>
        /// Retrieves all topics for a specific user, ordered by name.
        /// </summary>
        /// <param name="userId">The user ID (string from ASP.NET Core Identity)</param>
        /// <returns>List of user's topics</returns>
        Task<List<TopicEntity>> GetUserTopicsAsync(string userId);
        
        /// <summary>
        /// Gets a specific topic by ID for a user.
        /// </summary>
        /// <param name="topicId">The topic's Guid ID</param>
        /// <param name="userId">The user ID (string from ASP.NET Core Identity)</param>
        /// <returns>Topic entity if found and belongs to user, null otherwise</returns>
        Task<TopicEntity?> GetTopicByIdAsync(Guid topicId, string userId);
        
        /// <summary>
        /// Checks if a user already has a topic with the given name (case-insensitive).
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="topicName">The topic name to check</param>
        /// <returns>True if topic exists, false otherwise</returns>
        Task<bool> UserHasTopicAsync(string userId, string topicName);
        
        /// <summary>
        /// Gets the count of topics for a user (used for limit enforcement).
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>Number of topics the user has</returns>
        Task<int> GetUserTopicCountAsync(string userId);
    }
}
