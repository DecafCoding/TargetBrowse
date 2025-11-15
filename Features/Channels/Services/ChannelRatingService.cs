using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Services;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Database-backed implementation of channel rating service.
/// Provides full rating functionality with Entity Framework Core persistence.
/// </summary>
public class ChannelRatingService : RatingServiceBase<ChannelRatingModel, RateChannelModel>, IChannelRatingService
{
    private readonly ILogger<ChannelRatingService> _logger;

    public ChannelRatingService(
        ApplicationDbContext context,
        IMessageCenterService messageCenterService,
        ILogger<ChannelRatingService> logger)
        : base(context, messageCenterService, logger)
    {
        _logger = logger;
    }

    #region Base Class Implementation

    protected override Guid GetEntityId(RateChannelModel ratingModel) => ratingModel.ChannelId;

    protected override string GetEntityName() => "channel";

    protected override IQueryable<RatingEntity> BuildUserRatingQuery(string userId, Guid entityId)
    {
        return Context.Ratings
            .Include(r => r.Channel)
            .Where(r => r.UserId == userId && r.ChannelId == entityId);
    }

    protected override IQueryable<RatingEntity> BuildUserRatingByYouTubeIdQuery(string userId, string youTubeEntityId)
    {
        return Context.Ratings
            .Include(r => r.Channel)
            .Where(r => r.UserId == userId && r.Channel!.YouTubeChannelId == youTubeEntityId);
    }

    protected override IQueryable<RatingEntity> BuildUserRatingsQuery(string userId)
    {
        return Context.Ratings
            .Include(r => r.Channel)
            .Where(r => r.UserId == userId && r.ChannelId != null);
    }

    protected override IQueryable<RatingEntity> BuildSearchQuery(string userId, string? searchQuery)
    {
        var query = Context.Ratings
            .Include(r => r.Channel)
            .Where(r => r.UserId == userId && r.ChannelId != null);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(r =>
                EF.Functions.Like(r.Notes, $"%{searchQuery}%") ||
                EF.Functions.Like(r.Channel!.Name, $"%{searchQuery}%"));
        }

        return query;
    }

    protected override ChannelRatingModel MapToModel(RatingEntity entity, string? channelName = null, string? youTubeChannelId = null)
    {
        return new ChannelRatingModel
        {
            Id = entity.Id,
            ChannelId = entity.ChannelId ?? Guid.Empty,
            YouTubeChannelId = youTubeChannelId ?? entity.Channel?.YouTubeChannelId ?? string.Empty,
            ChannelName = channelName ?? entity.Channel?.Name ?? string.Empty,
            UserId = entity.UserId,
            Stars = entity.Stars,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.LastModifiedAt
        };
    }

    protected override RatingEntity CreateRatingEntity(string userId, RateChannelModel ratingModel)
    {
        return new RatingEntity
        {
            ChannelId = ratingModel.ChannelId,
            UserId = userId,
            Stars = ratingModel.Stars,
            Notes = ratingModel.Notes
        };
    }

    protected override void UpdateRatingEntity(RatingEntity entity, string userId, RateChannelModel ratingModel)
    {
        entity.Stars = ratingModel.Stars;
        entity.Notes = ratingModel.Notes;
    }

    protected override async Task<(bool CanRate, List<string> ErrorMessages)> ValidateCanRateAsync(string userId, Guid entityId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(userId))
        {
            errors.Add("User must be authenticated to rate channels");
        }

        if (entityId == Guid.Empty)
        {
            errors.Add("Invalid channel identifier");
        }

        // Check if channel exists
        if (errors.Count == 0)
        {
            var channelExists = await Context.Channels.AnyAsync(c => c.Id == entityId);
            if (!channelExists)
            {
                errors.Add("Channel not found");
            }
        }

        if (errors.Count == 0)
        {
            Logger.LogInformation("Validated rating permission for user {UserId} and channel {ChannelId}", userId, entityId);
        }

        return (errors.Count == 0, errors);
    }

    protected override void CleanNotes(RateChannelModel ratingModel)
    {
        ratingModel.CleanNotes();
    }

    protected override int GetStars(RateChannelModel ratingModel) => ratingModel.Stars;

    protected override async Task OnRatingCreatedAsync(string userId, RateChannelModel ratingModel, int stars)
    {
        // Handle 1-star rating cleanup
        if (stars == 1)
        {
            var removedSuggestions = await CleanupSuggestionsFromLowRatedChannelAsync(userId, ratingModel.ChannelId);
            _logger.LogInformation("Removed {Count} suggestions for 1-star rated channel {ChannelId}",
                removedSuggestions, ratingModel.ChannelId);
        }
    }

    protected override async Task OnRatingUpdatedAsync(string userId, RateChannelModel ratingModel, int oldStars, int newStars)
    {
        // Handle 1-star rating cleanup (if rating changed to 1-star)
        if (newStars == 1 && oldStars != 1)
        {
            var removedSuggestions = await CleanupSuggestionsFromLowRatedChannelAsync(userId, ratingModel.ChannelId);
            _logger.LogInformation("Removed {Count} suggestions for newly 1-star rated channel {ChannelId}",
                removedSuggestions, ratingModel.ChannelId);
        }
    }

    #endregion

    #region Public Interface Methods - Delegate to Base Class

    public Task<ChannelRatingModel?> GetUserRatingAsync(string userId, Guid channelId)
        => base.GetUserRatingAsync(userId, channelId);

    public Task<ChannelRatingModel?> GetUserRatingByYouTubeIdAsync(string userId, string youTubeChannelId)
        => base.GetUserRatingByYouTubeIdAsync(userId, youTubeChannelId);

    public async Task<ChannelRatingModel> CreateRatingAsync(string userId, RateChannelModel ratingModel)
    {
        var result = await base.CreateRatingAsync(userId, ratingModel);
        return MapToModel(await Context.Ratings
            .Include(r => r.Channel)
            .FirstAsync(r => r.Id == result.Id), ratingModel.ChannelName, ratingModel.YouTubeChannelId);
    }

    public async Task<ChannelRatingModel> UpdateRatingAsync(string userId, Guid ratingId, RateChannelModel ratingModel)
    {
        var result = await base.UpdateRatingAsync(userId, ratingId, ratingModel);
        return MapToModel(await Context.Ratings
            .Include(r => r.Channel)
            .FirstAsync(r => r.Id == result.Id), ratingModel.ChannelName, ratingModel.YouTubeChannelId);
    }

    public Task<bool> DeleteRatingAsync(string userId, Guid ratingId)
        => base.DeleteRatingAsync(userId, ratingId);

    public Task<List<ChannelRatingModel>> GetUserRatingsAsync(string userId, int pageNumber = 1, int pageSize = 20)
        => base.GetUserRatingsAsync(userId, pageNumber, pageSize);

    public Task<List<ChannelRatingModel>> SearchUserRatingsAsync(string userId, string searchQuery, int? minStars = null, int? maxStars = null)
        => base.SearchUserRatingsAsync(userId, searchQuery, minStars, maxStars);

    #endregion

    #region Channel-Specific Methods

    public async Task<List<ChannelRatingModel>> GetChannelRatingsAsync(Guid channelId)
    {
        try
        {
            var ratingEntities = await Context.Ratings
                .Include(r => r.Channel)
                .Where(r => r.ChannelId == channelId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var ratings = ratingEntities.Select(entity => MapToModel(entity)).ToList();

            _logger.LogInformation($"Retrieved {ratings.Count} ratings for channel {channelId}");
            return ratings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ratings for channel {ChannelId}", channelId);
            return new List<ChannelRatingModel>();
        }
    }

    public async Task<List<ChannelRatingModel>> GetHighlyRatedChannelsAsync(string userId, int limit = 10)
    {
        try
        {
            var ratingEntities = await Context.Ratings
                .Include(r => r.Channel)
                .Where(r => r.UserId == userId && r.ChannelId != null && r.Stars >= 4)
                .OrderByDescending(r => r.Stars)
                .ThenByDescending(r => r.LastModifiedAt)
                .Take(limit)
                .ToListAsync();

            var highlyRatedChannels = ratingEntities.Select(entity => MapToModel(entity)).ToList();

            _logger.LogInformation($"Retrieved {highlyRatedChannels.Count} highly rated channels for user {userId}");

            return highlyRatedChannels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting highly rated channels for user {UserId}", userId);
            return new List<ChannelRatingModel>();
        }
    }

    public async Task<List<Guid>> GetLowRatedChannelIdsAsync(string userId)
    {
        try
        {
            var lowRatedChannelIds = await Context.Ratings
                .Where(r => r.UserId == userId && r.ChannelId != null && r.Stars == 1)
                .Select(r => r.ChannelId!.Value)
                .ToListAsync();

            _logger.LogInformation($"Retrieved {lowRatedChannelIds.Count} low-rated channels for user {userId}");

            return lowRatedChannelIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting low-rated channels for user {UserId}", userId);
            return new List<Guid>();
        }
    }

    public async Task<ChannelRatingValidationResult> ValidateCanRateChannelAsync(string userId, Guid channelId)
    {
        try
        {
            var (canRate, errors) = await ValidateCanRateAsync(userId, channelId);
            return canRate
                ? ChannelRatingValidationResult.Success()
                : ChannelRatingValidationResult.Failure(errors.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating rating permission for user {UserId} and channel {ChannelId}", userId, channelId);
            return ChannelRatingValidationResult.Failure("Unable to validate rating permission", "System error occurred");
        }
    }

    public async Task<int> CleanupSuggestionsFromLowRatedChannelAsync(string userId, Guid channelId)
    {
        try
        {
            // Find all suggestions for this user from videos belonging to the low-rated channel
            var suggestionsToRemove = await Context.Suggestions
                .Include(s => s.Video)
                .Where(s => s.UserId == userId && s.Video.ChannelId == channelId)
                .ToListAsync();

            if (suggestionsToRemove.Any())
            {
                Context.Suggestions.RemoveRange(suggestionsToRemove);
                await Context.SaveChangesAsync();

                _logger.LogInformation("Removed {Count} suggestions from low-rated channel {ChannelId} for user {UserId}",
                    suggestionsToRemove.Count, channelId, userId);
            }

            return suggestionsToRemove.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up suggestions for channel {ChannelId} and user {UserId}", channelId, userId);
            return 0;
        }
    }

    public async Task<bool> IsChannelLowRatedAsync(string userId, Guid channelId)
    {
        try
        {
            var isLowRated = await Context.Ratings
                .AnyAsync(r => r.UserId == userId && r.ChannelId == channelId && r.Stars == 1);

            return isLowRated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if channel {ChannelId} is low-rated by user {UserId}", channelId, userId);
            return false;
        }
    }

    /// <summary>
    /// Gets channel ratings optimized for suggestion processing.
    /// Returns a dictionary keyed by channel ID for fast lookup during suggestion scoring.
    /// Excludes 1-star rated channels to prevent them from appearing in suggestions.
    /// </summary>
    /// <param name="userId">User ID to get channel ratings for</param>
    /// <returns>Dictionary of channel ID to star rating (1-star channels excluded)</returns>
    public async Task<Dictionary<Guid, int>> GetChannelRatingsForSuggestionsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetChannelRatingsForSuggestionsAsync called with null or empty userId");
                return new Dictionary<Guid, int>();
            }

            // Get all channel ratings except 1-star (which should be excluded from suggestions)
            var channelRatings = await Context.Ratings
                .Where(r => r.UserId == userId && r.ChannelId != null && r.Stars > 1)
                .Select(r => new { ChannelId = r.ChannelId!.Value, Stars = r.Stars })
                .ToDictionaryAsync(r => r.ChannelId, r => r.Stars);

            _logger.LogDebug("Retrieved {RatingCount} channel ratings for suggestions for user {UserId}",
                channelRatings.Count, userId);

            return channelRatings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel ratings for suggestions for user {UserId}", userId);
            return new Dictionary<Guid, int>();
        }
    }

    /// <summary>
    /// Gets YouTube channel IDs for channels rated 1-star by the user.
    /// These channels should be completely excluded from suggestion processing.
    /// </summary>
    /// <param name="userId">User ID to get low-rated channels for</param>
    /// <returns>List of YouTube channel IDs that are rated 1-star</returns>
    public async Task<List<string>> GetLowRatedYouTubeChannelIdsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<string>();
            }

            var lowRatedChannelIds = await Context.Ratings
                .Include(r => r.Channel)
                .Where(r => r.UserId == userId && r.ChannelId != null && r.Stars == 1)
                .Select(r => r.Channel!.YouTubeChannelId)
                .Where(channelId => !string.IsNullOrEmpty(channelId))
                .ToListAsync();

            _logger.LogDebug("Found {Count} low-rated YouTube channels for user {UserId}",
                lowRatedChannelIds.Count, userId);

            return lowRatedChannelIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting low-rated YouTube channels for user {UserId}", userId);
            return new List<string>();
        }
    }

    #endregion
}
