using TargetBrowse.Features.Channels.Components;
using TargetBrowse.Features.Channels.Models;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Service interface for channel rating operations.
/// Handles business logic for channel rating, editing, and management.
/// </summary>
public interface IChannelRatingService
{
    /// <summary>
    /// Gets a user's rating for a specific channel.
    /// </summary>
    /// <param name="userId">User ID to get rating for</param>
    /// <param name="channelId">Channel ID to get rating for</param>
    /// <returns>Channel rating model if found, null if no rating exists</returns>
    Task<ChannelRatingModel?> GetUserRatingAsync(string userId, Guid channelId);

    /// <summary>
    /// Gets a user's rating for a specific channel by YouTube channel ID.
    /// </summary>
    /// <param name="userId">User ID to get rating for</param>
    /// <param name="youTubeChannelId">YouTube channel ID to get rating for</param>
    /// <returns>Channel rating model if found, null if no rating exists</returns>
    Task<ChannelRatingModel?> GetUserRatingByYouTubeIdAsync(string userId, string youTubeChannelId);

    /// <summary>
    /// Creates a new rating for a channel.
    /// Validates business rules and enforces one rating per user per channel.
    /// </summary>
    /// <param name="userId">User ID creating the rating</param>
    /// <param name="ratingModel">Rating information to create</param>
    /// <returns>Created channel rating model</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails or rating already exists</exception>
    Task<ChannelRatingModel> CreateRatingAsync(string userId, RateChannelModel ratingModel);

    /// <summary>
    /// Updates an existing rating for a channel.
    /// Validates ownership and business rules.
    /// </summary>
    /// <param name="userId">User ID updating the rating</param>
    /// <param name="ratingId">ID of the rating to update</param>
    /// <param name="ratingModel">Updated rating information</param>
    /// <returns>Updated channel rating model</returns>
    /// <exception cref="InvalidOperationException">Thrown when validation fails or rating not found</exception>
    Task<ChannelRatingModel> UpdateRatingAsync(string userId, Guid ratingId, RateChannelModel ratingModel);

    /// <summary>
    /// Deletes a user's rating for a channel.
    /// Validates ownership before deletion.
    /// </summary>
    /// <param name="userId">User ID requesting deletion</param>
    /// <param name="ratingId">ID of the rating to delete</param>
    /// <returns>True if successfully deleted, false if rating not found or access denied</returns>
    Task<bool> DeleteRatingAsync(string userId, Guid ratingId);

    /// <summary>
    /// Gets all ratings by a specific user.
    /// Supports pagination for performance.
    /// </summary>
    /// <param name="userId">User ID to get ratings for</param>
    /// <param name="pageNumber">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Page size for pagination (default: 20)</param>
    /// <returns>List of channel ratings for the user</returns>
    Task<List<ChannelRatingModel>> GetUserRatingsAsync(string userId, int pageNumber = 1, int pageSize = 20);

    /// <summary>
    /// Gets all ratings for a specific channel (across all users).
    /// Used for analytics and channel performance tracking.
    /// </summary>
    /// <param name="channelId">Channel ID to get ratings for</param>
    /// <returns>List of all ratings for the channel</returns>
    Task<List<ChannelRatingModel>> GetChannelRatingsAsync(Guid channelId);

    /// <summary>
    /// Gets channels that are highly rated by the user (4+ stars) for recommendation purposes.
    /// </summary>
    /// <param name="userId">User ID to get highly rated channels for</param>
    /// <param name="limit">Maximum number of channels to return (default: 10)</param>
    /// <returns>List of highly rated channel ratings</returns>
    Task<List<ChannelRatingModel>> GetHighlyRatedChannelsAsync(string userId, int limit = 10);

    /// <summary>
    /// Gets channels that are low rated (1 star) by the user.
    /// These channels should be excluded from suggestions.
    /// </summary>
    /// <param name="userId">User ID to get low rated channels for</param>
    /// <returns>List of channel IDs that are rated 1 star</returns>
    Task<List<Guid>> GetLowRatedChannelIdsAsync(string userId);

    /// <summary>
    /// Validates if a user can rate a specific channel.
    /// Checks business rules and system constraints.
    /// </summary>
    /// <param name="userId">User ID to validate rating permission for</param>
    /// <param name="channelId">Channel ID to validate rating for</param>
    /// <returns>Validation result with success status and error messages</returns>
    Task<ChannelRatingValidationResult> ValidateCanRateChannelAsync(string userId, Guid channelId);

    /// <summary>
    /// Searches user's ratings by notes content and rating range.
    /// </summary>
    /// <param name="userId">User ID to search ratings for</param>
    /// <param name="searchQuery">Search query to match against notes or channel names</param>
    /// <param name="minStars">Minimum star rating filter (optional)</param>
    /// <param name="maxStars">Maximum star rating filter (optional)</param>
    /// <returns>List of matching channel ratings</returns>
    Task<List<ChannelRatingModel>> SearchUserRatingsAsync(string userId, string searchQuery, int? minStars = null, int? maxStars = null);

    /// <summary>
    /// Removes all suggestions from 1-star rated channels for a user.
    /// Called automatically when a channel receives a 1-star rating.
    /// </summary>
    /// <param name="userId">User ID to clean suggestions for</param>
    /// <param name="channelId">Channel ID that was rated 1-star</param>
    /// <returns>Number of suggestions removed</returns>
    Task<int> CleanupSuggestionsFromLowRatedChannelAsync(string userId, Guid channelId);

    /// <summary>
    /// Checks if a channel is rated 1-star by the user (should be excluded from suggestions).
    /// </summary>
    /// <param name="userId">User ID to check for</param>
    /// <param name="channelId">Channel ID to check</param>
    /// <returns>True if channel is rated 1-star by user, false otherwise</returns>
    Task<bool> IsChannelLowRatedAsync(string userId, Guid channelId);

    /// <summary>
    /// Gets channel ratings optimized for suggestion processing.
    /// Returns a dictionary keyed by channel ID for fast lookup during suggestion scoring.
    /// Excludes 1-star rated channels to prevent them from appearing in suggestions.
    /// </summary>
    /// <param name="userId">User ID to get channel ratings for</param>
    /// <returns>Dictionary of channel ID to star rating (1-star channels excluded)</returns>
    Task<Dictionary<Guid, int>> GetChannelRatingsForSuggestionsAsync(string userId);

    /// <summary>
    /// Gets YouTube channel IDs for channels rated 1-star by the user.
    /// These channels should be completely excluded from suggestion processing.
    /// </summary>
    /// <param name="userId">User ID to get low-rated channels for</param>
    /// <returns>List of YouTube channel IDs that are rated 1-star</returns>
    Task<List<string>> GetLowRatedYouTubeChannelIdsAsync(string userId);
}