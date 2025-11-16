using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Suggestions.Services;

/// <summary>
/// Service interface for intelligent video suggestion generation.
/// Combines user topics, tracked channels, and ratings to provide personalized content recommendations.
/// </summary>
public interface ISuggestionService
{
    /// <summary>
    /// Generates video suggestions for a user based on their topics and tracked channels.
    /// Uses unified scoring algorithm with source-aware bonuses and configurable thresholds.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="scoreThreshold">Minimum score threshold for suggestions (default: 5.0)</param>
    /// <returns>Detailed suggestion generation result with analytics</returns>
    Task<SuggestionResult> GenerateSuggestions(string userId, double scoreThreshold = 5.0);

    /// <summary>
    /// Checks if a user can request new suggestions.
    /// Validates against business rules like maximum pending suggestions.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>True if user can request suggestions, false if at limit</returns>
    Task<bool> CanUserRequestSuggestions(string userId);

    /// <summary>
    /// Gets the current count of pending suggestions for a user.
    /// Used for limit validation and UI display.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Number of pending suggestions</returns>
    Task<int> GetPendingSuggestionsCount(string userId);

    /// <summary>
    /// Removes expired suggestions that are older than 30 days.
    /// Called automatically to keep suggestion queues fresh.
    /// </summary>
    /// <returns>Number of suggestions cleaned up</returns>
    Task<int> CleanupExpiredSuggestions();

    /// <summary>
    /// Performs enhanced scoring for a video using transcript analysis.
    /// This is for future implementation when transcript analysis is added.
    /// </summary>
    /// <param name="video">Video information</param>
    /// <param name="userId">User identifier</param>
    /// <param name="transcript">Video transcript text</param>
    /// <returns>Enhanced scoring result</returns>
    Task<VideoScore> ScoreVideoEnhanced(VideoInfo video, string userId, string transcript);

    /// <summary>
    /// Gets all pending suggestions for a user with pagination.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="pageNumber">Page number for pagination</param>
    /// <param name="pageSize">Number of suggestions per page</param>
    /// <returns>List of pending suggestions</returns>
    Task<List<SuggestionDisplayModel>> GetPendingSuggestionsAsync(string userId, int pageNumber = 1, int pageSize = 20);

    /// <summary>
    /// Approves a suggestion, adding the video to the user's library.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="suggestionId">Suggestion identifier</param>
    /// <returns>True if approved successfully</returns>
    Task<bool> ApproveSuggestionAsync(string userId, Guid suggestionId);

    /// <summary>
    /// Denies a suggestion, removing it from the user's queue.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="suggestionId">Suggestion identifier</param>
    /// <returns>True if denied successfully</returns>
    Task<bool> DenySuggestionAsync(string userId, Guid suggestionId);

    /// <summary>
    /// Gets suggestion statistics and analytics for a user.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Suggestion analytics data</returns>
    Task<SuggestionAnalytics> GetSuggestionAnalyticsAsync(string userId);
}