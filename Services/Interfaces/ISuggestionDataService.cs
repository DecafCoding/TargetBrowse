using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Services.Interfaces
{
    /// <summary>
    /// Shared data access service for suggestion-related operations.
    /// Provides raw suggestion data access methods used across multiple services.
    /// Contains no business logic - pure data operations only.
    /// </summary>
    public interface ISuggestionDataService
    {
        /// <summary>
        /// Checks if a video already has a pending suggestion for the user.
        /// Used by onboarding services to avoid creating duplicate suggestions.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="videoId">The video entity ID (Guid)</param>
        /// <returns>True if pending suggestion exists, false otherwise</returns>
        Task<bool> HasPendingSuggestionForVideoAsync(string userId, Guid videoId);

        /// <summary>
        /// Gets the count of pending suggestions for a user.
        /// Used for limit enforcement across different suggestion creation contexts.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>Number of pending suggestions</returns>
        Task<int> GetPendingSuggestionsCountAsync(string userId);

        /// <summary>
        /// Creates suggestion entities for topic onboarding with proper topic relationships.
        /// Creates both SuggestionEntity and SuggestionTopicEntity records for data integrity.
        /// Used by TopicOnboardingService for initial topic-based suggestions.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="videoEntities">List of video entities to create suggestions for</param>
        /// <param name="topicId">The topic ID that triggered these suggestions</param>
        /// <param name="topicName">The topic name for reason display</param>
        /// <returns>List of created suggestion entities</returns>
        Task<List<SuggestionEntity>> CreateTopicOnboardingSuggestionsAsync(
            string userId,
            List<VideoEntity> videoEntities,
            Guid topicId,
            string topicName);

        /// <summary>
        /// Creates suggestion entities for channel onboarding.
        /// Creates suggestion entities for initial channel-based video suggestions.
        /// Used by ChannelOnboardingService for new channel additions.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="videoEntities">List of video entities to create suggestions for</param>
        /// <param name="channelName">The channel name for reason display</param>
        /// <returns>List of created suggestion entities</returns>
        Task<List<SuggestionEntity>> CreateChannelOnboardingSuggestionsAsync(
            string userId,
            List<VideoEntity> videoEntities,
            string channelName);

        /// <summary>
        /// Bulk creates video entities if they don't already exist.
        /// Ensures video entities are available for suggestion creation.
        /// Used by onboarding services to prepare video data.
        /// </summary>
        /// <param name="videos">List of VideoInfo objects from YouTube API</param>
        /// <returns>List of video entities (existing or newly created)</returns>
        Task<List<VideoEntity>> EnsureVideosExistAsync(List<VideoInfo> videos);

        /// <summary>
        /// Checks if a user can create more suggestions based on current limits.
        /// Validates against maximum pending suggestions limit.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>True if user can create more suggestions, false otherwise</returns>
        Task<bool> CanUserCreateSuggestionsAsync(string userId);
    }
}