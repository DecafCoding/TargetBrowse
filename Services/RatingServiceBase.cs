using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Validation;

namespace TargetBrowse.Services;

/// <summary>
/// Abstract base class for rating services that provides common CRUD operations
/// for both channel and video ratings, eliminating code duplication.
/// </summary>
/// <typeparam name="TRatingModel">The type of rating model (e.g., ChannelRatingModel, VideoRatingModel)</typeparam>
/// <typeparam name="TRateModel">The type of rate input model (e.g., RateChannelModel, RateVideoModel)</typeparam>
public abstract class RatingServiceBase<TRatingModel, TRateModel>
    where TRatingModel : class
    where TRateModel : class, IRatingModel
{
    protected readonly ApplicationDbContext Context;
    protected readonly IMessageCenterService MessageCenterService;
    protected readonly ILogger Logger;

    protected RatingServiceBase(
        ApplicationDbContext context,
        IMessageCenterService messageCenterService,
        ILogger logger)
    {
        Context = context;
        MessageCenterService = messageCenterService;
        Logger = logger;
    }

    #region Abstract Methods - Must be implemented by derived classes

    /// <summary>
    /// Gets the entity ID from a rate model (VideoId or ChannelId).
    /// </summary>
    protected abstract Guid GetEntityId(TRateModel ratingModel);

    /// <summary>
    /// Gets the entity name from the model for logging and messages.
    /// </summary>
    protected abstract string GetEntityName();

    /// <summary>
    /// Builds a query to find a rating by user ID and entity ID.
    /// </summary>
    protected abstract IQueryable<RatingEntity> BuildUserRatingQuery(string userId, Guid entityId);

    /// <summary>
    /// Builds a query to find a rating by user ID and YouTube entity ID.
    /// </summary>
    protected abstract IQueryable<RatingEntity> BuildUserRatingByYouTubeIdQuery(string userId, string youTubeEntityId);

    /// <summary>
    /// Builds a query to get all user ratings for this entity type.
    /// </summary>
    protected abstract IQueryable<RatingEntity> BuildUserRatingsQuery(string userId);

    /// <summary>
    /// Builds a query for searching user ratings.
    /// </summary>
    protected abstract IQueryable<RatingEntity> BuildSearchQuery(string userId, string? searchQuery);

    /// <summary>
    /// Maps a RatingEntity to the specific rating model type.
    /// </summary>
    protected abstract TRatingModel MapToModel(RatingEntity entity, string? entityName = null, string? youTubeEntityId = null);

    /// <summary>
    /// Creates a new RatingEntity from the rate model.
    /// </summary>
    protected abstract RatingEntity CreateRatingEntity(string userId, TRateModel ratingModel);

    /// <summary>
    /// Updates an existing RatingEntity with values from the rate model.
    /// </summary>
    protected abstract void UpdateRatingEntity(RatingEntity entity, string userId, TRateModel ratingModel);

    /// <summary>
    /// Validates if a user can rate the entity.
    /// </summary>
    protected abstract Task<(bool CanRate, List<string> ErrorMessages)> ValidateCanRateAsync(string userId, Guid entityId);

    /// <summary>
    /// Cleans the notes field in the rate model using the IRatingModel interface.
    /// </summary>
    protected void CleanNotes(TRateModel ratingModel)
    {
        ratingModel.CleanNotes();
    }

    /// <summary>
    /// Gets the star rating from the rate model using the IRatingModel interface.
    /// </summary>
    protected int GetStars(TRateModel ratingModel)
    {
        return ratingModel.Stars;
    }

    /// <summary>
    /// Performs any additional operations after creating a rating (e.g., cleanup for 1-star ratings).
    /// </summary>
    protected abstract Task OnRatingCreatedAsync(string userId, TRateModel ratingModel, int stars);

    /// <summary>
    /// Performs any additional operations after updating a rating (e.g., cleanup when changed to 1-star).
    /// </summary>
    protected abstract Task OnRatingUpdatedAsync(string userId, TRateModel ratingModel, int oldStars, int newStars);

    #endregion

    #region Common CRUD Operations

    /// <summary>
    /// Gets a user's rating for a specific entity.
    /// </summary>
    protected async Task<TRatingModel?> GetUserRatingAsync(string userId, Guid entityId)
    {
        try
        {
            var ratingEntity = await BuildUserRatingQuery(userId, entityId)
                .FirstOrDefaultAsync();

            if (ratingEntity == null)
            {
                Logger.LogInformation("No rating found for user {UserId} and {EntityType} {EntityId}",
                    userId, GetEntityName(), entityId);
                return null;
            }

            var rating = MapToModel(ratingEntity);
            Logger.LogInformation("Retrieved rating {RatingId} for user {UserId} and {EntityType} {EntityId}",
                GetRatingId(rating), userId, GetEntityName(), entityId);

            return rating;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting user rating for user {UserId} and {EntityType} {EntityId}",
                userId, GetEntityName(), entityId);
            await MessageCenterService.ShowErrorAsync("Failed to retrieve rating. Please try again.");
            return null;
        }
    }

    /// <summary>
    /// Gets a user's rating by YouTube entity ID.
    /// </summary>
    protected async Task<TRatingModel?> GetUserRatingByYouTubeIdAsync(string userId, string youTubeEntityId)
    {
        try
        {
            var ratingEntity = await BuildUserRatingByYouTubeIdQuery(userId, youTubeEntityId)
                .FirstOrDefaultAsync();

            if (ratingEntity == null)
            {
                Logger.LogInformation("No rating found for user {UserId} and YouTube {EntityType} {YouTubeEntityId}",
                    userId, GetEntityName(), youTubeEntityId);
                return null;
            }

            var rating = MapToModel(ratingEntity);
            Logger.LogInformation("Retrieved rating {RatingId} for user {UserId} and YouTube {EntityType} {YouTubeEntityId}",
                GetRatingId(rating), userId, GetEntityName(), youTubeEntityId);

            return rating;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting user rating for user {UserId} and YouTube {EntityType} {YouTubeEntityId}",
                userId, GetEntityName(), youTubeEntityId);
            await MessageCenterService.ShowErrorAsync("Failed to retrieve rating. Please try again.");
            return null;
        }
    }

    /// <summary>
    /// Creates a new rating for an entity.
    /// </summary>
    protected async Task<TRatingModel> CreateRatingAsync(string userId, TRateModel ratingModel)
    {
        try
        {
            var entityId = GetEntityId(ratingModel);
            var entityName = GetEntityName();

            // Validate the rating model
            var validationResult = await ValidateCanRateAsync(userId, entityId);
            if (!validationResult.CanRate)
            {
                var errorMessage = string.Join(", ", validationResult.ErrorMessages);
                await MessageCenterService.ShowErrorAsync($"Cannot create rating: {errorMessage}");
                throw new InvalidOperationException($"Cannot create rating: {errorMessage}");
            }

            // Check for existing rating
            var existingRating = await GetUserRatingAsync(userId, entityId);
            if (existingRating != null)
            {
                await MessageCenterService.ShowErrorAsync($"You have already rated this {entityName}. Use edit to update your rating.");
                throw new InvalidOperationException($"Rating already exists for this {entityName}");
            }

            CleanNotes(ratingModel);

            // Create new rating entity
            var ratingEntity = CreateRatingEntity(userId, ratingModel);
            ratingEntity.Id = Guid.NewGuid();
            ratingEntity.CreatedAt = DateTime.UtcNow;
            ratingEntity.LastModifiedAt = DateTime.UtcNow;
            ratingEntity.CreatedBy = userId;
            ratingEntity.LastModifiedBy = userId;

            Context.Ratings.Add(ratingEntity);
            await Context.SaveChangesAsync();

            var stars = GetStars(ratingModel);

            // Perform any additional operations after rating creation
            await OnRatingCreatedAsync(userId, ratingModel, stars);

            var newRating = MapToModel(ratingEntity);

            Logger.LogInformation("Created new rating {RatingId} for user {UserId} and {EntityType} {EntityId} with {Stars} stars",
                ratingEntity.Id, userId, entityName, entityId, stars);

            await MessageCenterService.ShowSuccessAsync($"Rating saved successfully! You rated this {entityName} {stars} stars.");

            return newRating;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-throw validation errors
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating rating for user {UserId} and {EntityType} {EntityId}",
                userId, GetEntityName(), GetEntityId(ratingModel));
            await MessageCenterService.ShowErrorAsync("Failed to save rating. Please try again.");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing rating.
    /// </summary>
    protected async Task<TRatingModel> UpdateRatingAsync(string userId, Guid ratingId, TRateModel ratingModel)
    {
        try
        {
            var entityName = GetEntityName();
            var existingRating = await Context.Ratings
                .FirstOrDefaultAsync(r => r.Id == ratingId && r.UserId == userId);

            if (existingRating == null)
            {
                await MessageCenterService.ShowErrorAsync("Rating not found or you don't have permission to edit it.");
                throw new InvalidOperationException("Rating not found or access denied");
            }

            var oldStars = existingRating.Stars;
            CleanNotes(ratingModel);

            // Update the rating
            UpdateRatingEntity(existingRating, userId, ratingModel);
            existingRating.LastModifiedAt = DateTime.UtcNow;
            existingRating.LastModifiedBy = userId;

            await Context.SaveChangesAsync();

            var newStars = GetStars(ratingModel);

            // Perform any additional operations after rating update
            await OnRatingUpdatedAsync(userId, ratingModel, oldStars, newStars);

            var updatedRating = MapToModel(existingRating);

            Logger.LogInformation("Updated rating {RatingId} for user {UserId} from {OldStars} to {NewStars} stars",
                ratingId, userId, oldStars, newStars);

            await MessageCenterService.ShowSuccessAsync($"Rating updated successfully! You now rate this {entityName} {newStars} stars.");

            return updatedRating;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating rating {RatingId} for user {UserId}", ratingId, userId);
            await MessageCenterService.ShowErrorAsync("Failed to update rating. Please try again.");
            throw;
        }
    }

    /// <summary>
    /// Deletes a user's rating.
    /// </summary>
    protected async Task<bool> DeleteRatingAsync(string userId, Guid ratingId)
    {
        try
        {
            var ratingToDelete = await Context.Ratings
                .FirstOrDefaultAsync(r => r.Id == ratingId && r.UserId == userId);

            if (ratingToDelete == null)
            {
                Logger.LogWarning("Rating {RatingId} not found for user {UserId} during deletion", ratingId, userId);
                await MessageCenterService.ShowErrorAsync("Rating not found or you don't have permission to delete it.");
                return false;
            }

            Context.Ratings.Remove(ratingToDelete);
            await Context.SaveChangesAsync();

            Logger.LogInformation("Deleted rating {RatingId} for user {UserId}", ratingId, userId);
            await MessageCenterService.ShowSuccessAsync("Rating deleted successfully.");

            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting rating {RatingId} for user {UserId}", ratingId, userId);
            await MessageCenterService.ShowErrorAsync("Failed to delete rating. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Gets all ratings by a specific user with pagination.
    /// </summary>
    protected async Task<List<TRatingModel>> GetUserRatingsAsync(string userId, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            var ratingEntities = await BuildUserRatingsQuery(userId)
                .OrderByDescending(r => r.LastModifiedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var ratings = ratingEntities.Select(entity => MapToModel(entity)).ToList();

            Logger.LogInformation($"Retrieved {ratings.Count} ratings for user {userId} (page {pageNumber}, size {pageSize})");

            return ratings;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting user ratings for user {UserId}", userId);
            await MessageCenterService.ShowErrorAsync("Failed to retrieve your ratings. Please try again.");
            return new List<TRatingModel>();
        }
    }

    /// <summary>
    /// Searches user's ratings by notes content and star range.
    /// </summary>
    protected async Task<List<TRatingModel>> SearchUserRatingsAsync(
        string userId,
        string? searchQuery,
        int? minStars = null,
        int? maxStars = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchQuery) && minStars == null && maxStars == null)
            {
                return await GetUserRatingsAsync(userId);
            }

            var query = BuildSearchQuery(userId, searchQuery);

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

            Logger.LogInformation($"Search returned {results.Count} ratings for user {userId} with query '{searchQuery}' and stars {minStars}-{maxStars}");

            return results;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error searching ratings for user {UserId}", userId);
            await MessageCenterService.ShowErrorAsync("Failed to search ratings. Please try again.");
            return new List<TRatingModel>();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the rating ID from a rating model using reflection.
    /// This is a helper for logging purposes.
    /// </summary>
    private Guid GetRatingId(TRatingModel rating)
    {
        var idProperty = typeof(TRatingModel).GetProperty("Id");
        return idProperty?.GetValue(rating) as Guid? ?? Guid.Empty;
    }

    #endregion
}
