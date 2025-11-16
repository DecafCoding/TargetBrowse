using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Suggestions.Data;

/// <summary>
/// Repository interface for suggestion data access operations.
/// Handles database operations for suggestion entities and related data.
/// </summary>
public interface ISuggestionRepository
{
    /// <summary>
    /// Creates new suggestion entities in the database.
    /// </summary>
    /// <param name="suggestions">List of suggestions to create</param>
    /// <returns>List of created suggestion entities with IDs</returns>
    Task<List<SuggestionEntity>> CreateSuggestionsAsync(List<SuggestionEntity> suggestions);

    /// <summary>
    /// Gets pending suggestions for a user with pagination.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="pageNumber">Page number for pagination</param>
    /// <param name="pageSize">Number of suggestions per page</param>
    /// <returns>List of pending suggestions</returns>
    Task<List<SuggestionEntity>> GetPendingSuggestionsAsync(string userId, int pageNumber = 1, int pageSize = 20);

    /// <summary>
    /// Gets a suggestion by ID with user ownership validation.
    /// </summary>
    /// <param name="suggestionId">Suggestion identifier</param>
    /// <param name="userId">User identifier for ownership validation</param>
    /// <returns>Suggestion entity if found and owned by user, null otherwise</returns>
    Task<SuggestionEntity?> GetSuggestionByIdAsync(Guid suggestionId, string userId);

    /// <summary>
    /// Marks suggestions as approved and moves to user's library.
    /// </summary>
    /// <param name="suggestionIds">List of suggestion IDs to approve</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Number of suggestions approved</returns>
    Task<int> ApproveSuggestionsAsync(List<Guid> suggestionIds, string userId);

    /// <summary>
    /// Marks suggestions as denied and removes from queue.
    /// </summary>
    /// <param name="suggestionIds">List of suggestion IDs to deny</param>
    /// <param name="userId">User identifier</param>
    /// <returns>Number of suggestions denied</returns>
    Task<int> DenySuggestionsAsync(List<Guid> suggestionIds, string userId);

    /// <summary>
    /// Removes expired suggestions (older than 30 days and not reviewed).
    /// NOTE: This method exists but is excluded from MVP scope per YT-010-03 requirements.
    /// </summary>
    Task<int> CleanupExpiredSuggestionsAsync();

    /// <summary>
    /// Removes all suggestions from a specific channel for a user.
    /// Called when user rates a channel 1-star.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="channelId">Channel identifier</param>
    /// <returns>Number of suggestions removed</returns>
    Task<int> RemoveSuggestionsByChannelAsync(string userId, Guid channelId);

    /// <summary>
    /// Gets suggestion analytics data for a user.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Analytics data</returns>
    Task<SuggestionAnalytics> GetSuggestionAnalyticsAsync(string userId);

    /// <summary>
    /// Searches suggestions by video title or channel name.
    /// UNUSED - 9/16/2025
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="searchQuery">Search term</param>
    /// <param name="status">Optional status filter</param>
    /// <returns>List of matching suggestions</returns>
    Task<List<SuggestionEntity>> SearchSuggestionsAsync(string userId, string searchQuery, SuggestionStatus? status = null);

    /// <summary>
    /// Updates the last channel check date for suggestion generation.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="channelId">Channel identifier</param>
    /// <param name="lastCheckDate">Last check date</param>
    /// <returns>Task completion</returns>
    Task UpdateChannelLastCheckDateAsync(string userId, Guid channelId, DateTime lastCheckDate);

    /// <summary>
    /// Gets channels that need to be checked for new videos.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>List of channels with their last check dates</returns>
    Task<List<ChannelCheckInfo>> GetChannelsForUpdateCheckAsync(string userId);

    /// <summary>
    /// Creates suggestion with topic relationships.
    /// Enhanced version that also creates SuggestionTopic junction records.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">Video identifier</param>
    /// <param name="reason">Suggestion reason text</param>
    /// <param name="topicIds">List of topic IDs that matched this suggestion</param>
    /// <returns>Created suggestion entity</returns>
    Task<SuggestionEntity> CreateSuggestionWithTopicsAsync(string userId, Guid videoId, string reason, List<Guid> topicIds);
}

/// <summary>
/// Information about a channel's last check status for updates.
/// </summary>
public class ChannelCheckInfo
{
    /// <summary>
    /// Channel entity information.
    /// </summary>
    public ChannelEntity Channel { get; set; } = null!;

    /// <summary>
    /// Last time this channel was checked for new videos.
    /// </summary>
    public DateTime? LastCheckDate { get; set; }

    /// <summary>
    /// User's rating for this channel (affects suggestion filtering).
    /// </summary>
    public int? UserRating { get; set; }
}