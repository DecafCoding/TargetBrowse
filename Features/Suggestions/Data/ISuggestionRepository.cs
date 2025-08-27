using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;

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

    // NEW METHOD FOR YT-010-03: Enhanced suggestion creation from VideoSuggestion objects
    /// <summary>
    /// Creates suggestion entities from video suggestions with enhanced business logic.
    /// Ensures proper user-video relationships, duplicate prevention, and business rule enforcement.
    /// Used by the Enhanced Manual Suggestion Generation system (YT-010-03).
    /// </summary>
    /// <param name="videoSuggestions">List of video suggestions with scoring and reasoning</param>
    /// <param name="userId">User identifier</param>
    /// <returns>List of created suggestion entities</returns>
    Task<List<SuggestionEntity>> CreateSuggestionsFromVideoSuggestionsAsync(List<VideoSuggestion> videoSuggestions, string userId);

    // NEW METHOD FOR YT-010-03: Business rule validation
    /// <summary>
    /// Checks if a user can request new suggestions based on business rules.
    /// Validates against maximum pending suggestions limit (100 for MVP).
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>True if user can request more suggestions, false if at limit</returns>
    Task<bool> CanUserRequestSuggestionsAsync(string userId);

    /// <summary>
    /// Gets pending suggestions for a user with pagination.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="pageNumber">Page number for pagination</param>
    /// <param name="pageSize">Number of suggestions per page</param>
    /// <returns>List of pending suggestions</returns>
    Task<List<SuggestionEntity>> GetPendingSuggestionsAsync(string userId, int pageNumber = 1, int pageSize = 20);

    /// <summary>
    /// Gets the count of pending suggestions for a user.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Number of pending suggestions</returns>
    Task<int> GetPendingSuggestionsCountAsync(string userId);

    /// <summary>
    /// Gets a suggestion by ID with user ownership validation.
    /// </summary>
    /// <param name="suggestionId">Suggestion identifier</param>
    /// <param name="userId">User identifier for ownership validation</param>
    /// <returns>Suggestion entity if found and owned by user, null otherwise</returns>
    Task<SuggestionEntity?> GetSuggestionByIdAsync(Guid suggestionId, string userId);

    /// <summary>
    /// Updates a suggestion entity in the database.
    /// </summary>
    /// <param name="suggestion">Suggestion entity to update</param>
    /// <returns>Updated suggestion entity</returns>
    Task<SuggestionEntity> UpdateSuggestionAsync(SuggestionEntity suggestion);

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
    /// Checks if a video already has a pending suggestion for the user.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="videoId">Video identifier</param>
    /// <returns>True if suggestion already exists</returns>
    Task<bool> HasPendingSuggestionForVideoAsync(string userId, Guid videoId);

    /// <summary>
    /// Gets suggestion analytics data for a user.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Analytics data</returns>
    Task<SuggestionAnalytics> GetSuggestionAnalyticsAsync(string userId);

    /// <summary>
    /// Gets all suggestions for a user with optional filtering.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="pageNumber">Page number for pagination</param>
    /// <param name="pageSize">Number of suggestions per page</param>
    /// <returns>List of suggestions matching criteria</returns>
    Task<List<SuggestionEntity>> GetUserSuggestionsAsync(string userId, SuggestionStatus? status = null, int pageNumber = 1, int pageSize = 50);

    /// <summary>
    /// Searches suggestions by video title or channel name.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="searchQuery">Search term</param>
    /// <param name="status">Optional status filter</param>
    /// <returns>List of matching suggestions</returns>
    Task<List<SuggestionEntity>> SearchSuggestionsAsync(string userId, string searchQuery, SuggestionStatus? status = null);

    /// <summary>
    /// Gets the most recent suggestion generation date for a user.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Date of most recent suggestion generation, null if none</returns>
    Task<DateTime?> GetLastSuggestionGenerationDateAsync(string userId);

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
    /// Bulk creates video entities if they don't already exist.
    /// Enhanced for YT-010-03 with improved error handling and batch processing.
    /// </summary>
    /// <param name="videos">List of video information</param>
    /// <returns>List of created/existing video entities</returns>
    Task<List<VideoEntity>> EnsureVideosExistAsync(List<VideoInfo> videos);
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