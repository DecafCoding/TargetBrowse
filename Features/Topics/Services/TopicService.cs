using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Topics.Models;
using TargetBrowse.Features.Topics.Validators;
using TargetBrowse.Services;

namespace TargetBrowse.Features.Topics.Services;

/// <summary>
/// Implementation of topic management service.
/// Handles business logic, validation, and data persistence for user topics.
/// </summary>
public class TopicService : ITopicService
{
    private readonly ApplicationDbContext _context;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<TopicService> _logger;

    public TopicService(
        ApplicationDbContext context,
        IMessageCenterService messageCenterService,
        ILogger<TopicService> logger)
    {
        _context = context;
        _messageCenterService = messageCenterService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all topics for a specific user, ordered by creation date.
    /// </summary>
    public async Task<List<TopicDto>> GetUserTopicsAsync(string userId)
    {
        try
        {
            var topics = await _context.Topics
                .Where(t => t.UserId == userId && !t.IsDeleted)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();

            return topics.Select(TopicDto.FromEntity).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving topics for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to load topics. Please try again.");
            return new List<TopicDto>();
        }
    }

    /// <summary>
    /// Adds a new topic for a user with full validation.
    /// </summary>
    public async Task<TopicValidationResult> AddTopicAsync(string userId, string topicName)
    {
        try
        {
            // Validate topic name format
            var nameValidation = TopicValidator.ValidateTopicName(topicName);
            if (!nameValidation.IsValid)
            {
                await _messageCenterService.ShowErrorAsync(nameValidation.ErrorMessage);
                return nameValidation;
            }

            // Normalize the topic name
            var normalizedName = TopicValidator.NormalizeTopicName(topicName);

            // Check topic limit
            var currentCount = await GetTopicCountAsync(userId);
            var limitValidation = TopicValidator.ValidateTopicLimit(currentCount);
            if (!limitValidation.IsValid)
            {
                await _messageCenterService.ShowWarningAsync(limitValidation.ErrorMessage);
                return limitValidation;
            }

            // Check uniqueness
            var existingTopics = await GetUserTopicsAsync(userId);
            var uniquenessValidation = TopicValidator.ValidateTopicUniqueness(normalizedName, existingTopics);
            if (!uniquenessValidation.IsValid)
            {
                await _messageCenterService.ShowErrorAsync(uniquenessValidation.ErrorMessage);
                return uniquenessValidation;
            }

            // Create and save new topic
            var topicEntity = new TopicEntity
            {
                Id = Guid.NewGuid(),
                Name = normalizedName,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            };

            _context.Topics.Add(topicEntity);
            await _context.SaveChangesAsync();

            var createdTopic = TopicDto.FromEntity(topicEntity);

            _logger.LogInformation("Topic '{TopicName}' created for user {UserId}", normalizedName, userId);
            await _messageCenterService.ShowSuccessAsync($"Topic '{normalizedName}' added successfully!");

            return TopicValidationResult.Success(createdTopic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding topic '{TopicName}' for user {UserId}", topicName, userId);
            await _messageCenterService.ShowErrorAsync("Failed to add topic. Please try again.");
            return TopicValidationResult.Failure("An error occurred while adding the topic.");
        }
    }

    /// <summary>
    /// Updates an existing topic name with validation.
    /// </summary>
    public async Task<TopicValidationResult> UpdateTopicAsync(string userId, Guid topicId, string newName)
    {
        try
        {
            // Find the topic and verify ownership
            var topic = await _context.Topics
                .FirstOrDefaultAsync(t => t.Id == topicId && t.UserId == userId && !t.IsDeleted);

            if (topic == null)
            {
                await _messageCenterService.ShowErrorAsync("Topic not found or you don't have permission to edit it.");
                return TopicValidationResult.Failure("Topic not found.");
            }

            // Validate new name
            var nameValidation = TopicValidator.ValidateTopicName(newName);
            if (!nameValidation.IsValid)
            {
                await _messageCenterService.ShowErrorAsync(nameValidation.ErrorMessage);
                return nameValidation;
            }

            // Normalize the new name
            var normalizedName = TopicValidator.NormalizeTopicName(newName);

            // Check if name actually changed
            if (string.Equals(topic.Name, normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                return TopicValidationResult.Success(); // No change needed
            }

            // Check uniqueness (excluding current topic)
            var existingTopics = await GetUserTopicsAsync(userId);
            var uniquenessValidation = TopicValidator.ValidateTopicUniqueness(normalizedName, existingTopics, topicId);
            if (!uniquenessValidation.IsValid)
            {
                await _messageCenterService.ShowErrorAsync(uniquenessValidation.ErrorMessage);
                return uniquenessValidation;
            }

            // Update the topic
            var oldName = topic.Name;
            topic.Name = normalizedName;
            topic.LastModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Topic updated from '{OldName}' to '{NewName}' for user {UserId}", oldName, normalizedName, userId);
            await _messageCenterService.ShowSuccessAsync($"Topic updated to '{normalizedName}' successfully!");

            return TopicValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating topic {TopicId} for user {UserId}", topicId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to update topic. Please try again.");
            return TopicValidationResult.Failure("An error occurred while updating the topic.");
        }
    }

    /// <summary>
    /// Deletes a topic for a user (soft delete).
    /// </summary>
    public async Task<bool> DeleteTopicAsync(string userId, Guid topicId)
    {
        try
        {
            var topic = await _context.Topics
                .FirstOrDefaultAsync(t => t.Id == topicId && t.UserId == userId && !t.IsDeleted);

            if (topic == null)
            {
                await _messageCenterService.ShowErrorAsync("Topic not found or you don't have permission to delete it.");
                return false;
            }

            // Soft delete
            topic.IsDeleted = true;
            topic.LastModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Topic '{TopicName}' deleted for user {UserId}", topic.Name, userId);
            await _messageCenterService.ShowSuccessAsync($"Topic '{topic.Name}' deleted successfully!");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting topic {TopicId} for user {UserId}", topicId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to delete topic. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Checks if user can add more topics (under limit).
    /// </summary>
    public async Task<bool> CanAddTopicAsync(string userId)
    {
        try
        {
            var currentCount = await GetTopicCountAsync(userId);
            return currentCount < TopicValidator.GetMaxTopicsPerUser();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking topic limit for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Gets the current topic count for a user.
    /// </summary>
    public async Task<int> GetTopicCountAsync(string userId)
    {
        try
        {
            return await _context.Topics
                .CountAsync(t => t.UserId == userId && !t.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting topic count for user {UserId}", userId);
            return 0;
        }
    }

    /// <summary>
    /// Validates a topic name for a user without persisting changes.
    /// </summary>
    public async Task<TopicValidationResult> ValidateTopicNameAsync(string userId, string topicName, Guid? excludeTopicId = null)
    {
        try
        {
            // Validate format
            var nameValidation = TopicValidator.ValidateTopicName(topicName);
            if (!nameValidation.IsValid)
            {
                return nameValidation;
            }

            // Normalize name
            var normalizedName = TopicValidator.NormalizeTopicName(topicName);

            // Check uniqueness
            var existingTopics = await GetUserTopicsAsync(userId);
            var uniquenessValidation = TopicValidator.ValidateTopicUniqueness(normalizedName, existingTopics, excludeTopicId);

            return uniquenessValidation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating topic name for user {UserId}", userId);
            return TopicValidationResult.Failure("An error occurred while validating the topic name.");
        }
    }
}