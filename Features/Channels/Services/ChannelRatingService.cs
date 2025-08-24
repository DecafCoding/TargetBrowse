using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Services;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Database-backed implementation of channel rating service.
/// Provides full rating functionality with Entity Framework Core persistence.
/// </summary>
public class ChannelRatingService : IChannelRatingService
{
    private readonly ApplicationDbContext _context;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<ChannelRatingService> _logger;

    public ChannelRatingService(
        ApplicationDbContext context,
        IMessageCenterService messageCenterService,
        ILogger<ChannelRatingService> logger)
    {
        _context = context;
        _messageCenterService = messageCenterService;
        _logger = logger;
    }

    public async Task<ChannelRatingModel?> GetUserRatingAsync(string userId, Guid channelId)
    {
        try
        {
            var ratingEntity = await _context.Ratings
                .Include(r => r.Channel)
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ChannelId == channelId);

            if (ratingEntity == null)
            {
                _logger.LogInformation("No rating found for user {UserId} and channel {ChannelId}", userId, channelId);
                return null;
            }

            var rating = MapToModel(ratingEntity);
            _logger.LogInformation("Retrieved rating {RatingId} for user {UserId} and channel {ChannelId}",
                rating.Id, userId, channelId);

            return rating;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user rating for user {UserId} and channel {ChannelId}", userId, channelId);
            await _messageCenterService.ShowErrorAsync("Failed to retrieve rating. Please try again.");
            return null;
        }
    }

    public async Task<ChannelRatingModel?> GetUserRatingByYouTubeIdAsync(string userId, string youTubeChannelId)
    {
        try
        {
            var ratingEntity = await _context.Ratings
                .Include(r => r.Channel)
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Channel!.YouTubeChannelId == youTubeChannelId);

            if (ratingEntity == null)
            {
                _logger.LogInformation("No rating found for user {UserId} and YouTube channel {YouTubeChannelId}",
                    userId, youTubeChannelId);
                return null;
            }

            var rating = MapToModel(ratingEntity);
            _logger.LogInformation("Retrieved rating {RatingId} for user {UserId} and YouTube channel {YouTubeChannelId}",
                rating.Id, userId, youTubeChannelId);

            return rating;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user rating for user {UserId} and YouTube channel {YouTubeChannelId}",
                userId, youTubeChannelId);
            await _messageCenterService.ShowErrorAsync("Failed to retrieve rating. Please try again.");
            return null;
        }
    }

    public async Task<ChannelRatingModel> CreateRatingAsync(string userId, RateChannelModel ratingModel)
    {
        try
        {
            // Validate the rating model
            var validationResult = await ValidateCanRateChannelAsync(userId, ratingModel.ChannelId);
            if (!validationResult.CanRate)
            {
                var errorMessage = string.Join(", ", validationResult.ErrorMessages);
                await _messageCenterService.ShowErrorAsync($"Cannot create rating: {errorMessage}");
                throw new InvalidOperationException($"Cannot create rating: {errorMessage}");
            }

            // Check for existing rating
            var existingRating = await GetUserRatingAsync(userId, ratingModel.ChannelId);
            if (existingRating != null)
            {
                await _messageCenterService.ShowErrorAsync("You have already rated this channel. Use edit to update your rating.");
                throw new InvalidOperationException("Rating already exists for this channel");
            }

            ratingModel.CleanNotes();

            // Create new rating entity
            var ratingEntity = new RatingEntity
            {
                Id = Guid.NewGuid(),
                ChannelId = ratingModel.ChannelId,
                UserId = userId,
                Stars = ratingModel.Stars,
                Notes = ratingModel.Notes,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow,
                CreatedBy = userId,
                LastModifiedBy = userId
            };

            _context.Ratings.Add(ratingEntity);
            await _context.SaveChangesAsync();

            // Handle 1-star rating cleanup
            if (ratingModel.Stars == 1)
            {
                var removedSuggestions = await CleanupSuggestionsFromLowRatedChannelAsync(userId, ratingModel.ChannelId);
                _logger.LogInformation("Removed {Count} suggestions for 1-star rated channel {ChannelId}", 
                    removedSuggestions, ratingModel.ChannelId);
            }

            var newRating = MapToModel(ratingEntity, ratingModel.ChannelName, ratingModel.YouTubeChannelId);

            _logger.LogInformation("Created new rating {RatingId} for user {UserId} and channel {ChannelId} with {Stars} stars",
                newRating.Id, userId, ratingModel.ChannelId, ratingModel.Stars);

            await _messageCenterService.ShowSuccessAsync($"Rating saved successfully! You rated this channel {ratingModel.Stars} stars.");

            return newRating;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw validation errors
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating rating for user {UserId} and channel {ChannelId}", userId, ratingModel.ChannelId);
            await _messageCenterService.ShowErrorAsync("Failed to save rating. Please try again.");
            throw;
        }
    }

    public async Task<ChannelRatingModel> UpdateRatingAsync(string userId, Guid ratingId, RateChannelModel ratingModel)
    {
        try
        {
            var existingRating = await _context.Ratings
                .Include(r => r.Channel)
                .FirstOrDefaultAsync(r => r.Id == ratingId && r.UserId == userId);

            if (existingRating == null)
            {
                await _messageCenterService.ShowErrorAsync("Rating not found or you don't have permission to edit it.");
                throw new InvalidOperationException("Rating not found or access denied");
            }

            var oldStars = existingRating.Stars;
            ratingModel.CleanNotes();

            // Update the rating
            existingRating.Stars = ratingModel.Stars;
            existingRating.Notes = ratingModel.Notes;
            existingRating.LastModifiedAt = DateTime.UtcNow;
            existingRating.LastModifiedBy = userId;

            await _context.SaveChangesAsync();

            // Handle 1-star rating cleanup (if rating changed to 1-star)
            if (ratingModel.Stars == 1 && oldStars != 1)
            {
                var removedSuggestions = await CleanupSuggestionsFromLowRatedChannelAsync(userId, ratingModel.ChannelId);
                _logger.LogInformation("Removed {Count} suggestions for newly 1-star rated channel {ChannelId}", 
                    removedSuggestions, ratingModel.ChannelId);
            }

            var updatedRating = MapToModel(existingRating, ratingModel.ChannelName, ratingModel.YouTubeChannelId);

            _logger.LogInformation("Updated rating {RatingId} for user {UserId} from {OldStars} to {NewStars} stars",
                ratingId, userId, oldStars, ratingModel.Stars);

            await _messageCenterService.ShowSuccessAsync($"Rating updated successfully! You now rate this channel {ratingModel.Stars} stars.");

            return updatedRating;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rating {RatingId} for user {UserId}", ratingId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to update rating. Please try again.");
            throw;
        }
    }

    public async Task<bool> DeleteRatingAsync(string userId, Guid ratingId)
    {
        try
        {
            var ratingToDelete = await _context.Ratings
                .FirstOrDefaultAsync(r => r.Id == ratingId && r.UserId == userId);

            if (ratingToDelete == null)
            {
                _logger.LogWarning("Rating {RatingId} not found for user {UserId} during deletion", ratingId, userId);
                await _messageCenterService.ShowErrorAsync("Rating not found or you don't have permission to delete it.");
                return false;
            }

            _context.Ratings.Remove(ratingToDelete);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted rating {RatingId} for user {UserId}", ratingId, userId);
            await _messageCenterService.ShowSuccessAsync("Rating deleted successfully.");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting rating {RatingId} for user {UserId}", ratingId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to delete rating. Please try again.");
            return false;
        }
    }

    public async Task<List<ChannelRatingModel>> GetUserRatingsAsync(string userId, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            var ratingEntities = await _context.Ratings
                .Include(r => r.Channel)
                .Where(r => r.UserId == userId && r.ChannelId != null)
                .OrderByDescending(r => r.LastModifiedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var ratings = ratingEntities.Select(entity => MapToModel(entity)).ToList();

            _logger.LogInformation($"Retrieved {ratings.Count} ratings for user {userId} (page {pageNumber}, size {pageSize})");

            return ratings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user ratings for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to retrieve your ratings. Please try again.");
            return new List<ChannelRatingModel>();
        }
    }

    public async Task<List<ChannelRatingModel>> GetChannelRatingsAsync(Guid channelId)
    {
        try
        {
            var ratingEntities = await _context.Ratings
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
            var ratingEntities = await _context.Ratings
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
            var lowRatedChannelIds = await _context.Ratings
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
            if (string.IsNullOrWhiteSpace(userId))
            {
                return ChannelRatingValidationResult.Failure("User must be authenticated to rate channels");
            }

            if (channelId == Guid.Empty)
            {
                return ChannelRatingValidationResult.Failure("Invalid channel identifier");
            }

            // Check if channel exists
            var channelExists = await _context.Channels.AnyAsync(c => c.Id == channelId);
            if (!channelExists)
            {
                return ChannelRatingValidationResult.Failure("Channel not found");
            }

            _logger.LogInformation("Validated rating permission for user {UserId} and channel {ChannelId}", userId, channelId);
            return ChannelRatingValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating rating permission for user {UserId} and channel {ChannelId}", userId, channelId);
            return ChannelRatingValidationResult.Failure("Unable to validate rating permission", "System error occurred");
        }
    }

    public async Task<List<ChannelRatingModel>> SearchUserRatingsAsync(string userId, string searchQuery, int? minStars = null, int? maxStars = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchQuery) && minStars == null && maxStars == null)
            {
                return await GetUserRatingsAsync(userId);
            }

            var query = _context.Ratings
                .Include(r => r.Channel)
                .Where(r => r.UserId == userId && r.ChannelId != null);

            // Filter by search query in notes or channel name
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                query = query.Where(r =>
                    EF.Functions.Like(r.Notes, $"%{searchQuery}%") ||
                    EF.Functions.Like(r.Channel!.Name, $"%{searchQuery}%"));
            }

            // Filter by star rating range
            if (minStars.HasValue)
            {
                query = query.Where(r => r.Stars >= minStars.Value);
            }

            if (maxStars.HasValue)
            {
                query = query.Where(r => r.Stars <= maxStars.Value);
            }

            var ratingEntities = await query.OrderByDescending(r => r.LastModifiedAt).ToListAsync();
            var results = ratingEntities.Select(entity => MapToModel(entity)).ToList();

            _logger.LogInformation($"Search returned {results.Count} ratings for user {userId} with query '{searchQuery}' and stars {minStars}-{maxStars}");

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching ratings for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to search ratings. Please try again.");
            return new List<ChannelRatingModel>();
        }
    }

    public async Task<int> CleanupSuggestionsFromLowRatedChannelAsync(string userId, Guid channelId)
    {
        try
        {
            // Find all suggestions for this user from videos belonging to the low-rated channel
            var suggestionsToRemove = await _context.Suggestions
                .Include(s => s.Video)
                .Where(s => s.UserId == userId && s.Video.ChannelId == channelId)
                .ToListAsync();

            if (suggestionsToRemove.Any())
            {
                _context.Suggestions.RemoveRange(suggestionsToRemove);
                await _context.SaveChangesAsync();

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
            var isLowRated = await _context.Ratings
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
    /// Maps a RatingEntity to a ChannelRatingModel.
    /// </summary>
    private static ChannelRatingModel MapToModel(RatingEntity entity, string? channelName = null, string? youTubeChannelId = null)
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
}