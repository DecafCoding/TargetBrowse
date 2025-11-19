using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Topics.Models;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Topics.Services;

/// <summary>
/// Implementation of topic management service.
/// Handles business logic for user topics including validation and persistence.
/// Updated to use TopicDataService for consistent data access patterns.
/// </summary>
public class TopicService : ITopicService
{
    private readonly ApplicationDbContext _context;
    private readonly ITopicDataService _topicDataService;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<TopicService> _logger;

    private const int MaxTopicsPerUser = 10;

    private readonly ITopicOnboardingService _topicOnboardingService;

    public TopicService(
        ApplicationDbContext context,
        ITopicDataService topicDataService,
        IMessageCenterService messageCenterService,
        ITopicOnboardingService topicOnboardingService,
        ILogger<TopicService> logger)
    {
        _context = context;
        _topicDataService = topicDataService;
        _messageCenterService = messageCenterService;
        _topicOnboardingService = topicOnboardingService;
        _logger = logger;
    }

    /// <summary>
    /// Adds a new topic for the specified user with business rule validation.
    /// </summary>
    public async Task<bool> AddTopicAsync(string userId, string topicName, int checkDays = 7)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(userId))
            {
                await _messageCenterService.ShowErrorAsync("User authentication required.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(topicName))
            {
                await _messageCenterService.ShowErrorAsync("Topic name is required.");
                return false;
            }

            // Trim and validate topic name
            topicName = topicName.Trim();
            if (topicName.Length < 2 || topicName.Length > 100)
            {
                await _messageCenterService.ShowErrorAsync("Topic name must be between 2 and 100 characters.");
                return false;
            }

            // Check topic limit directly using TopicDataService
            var currentCount = await _topicDataService.GetUserTopicCountAsync(userId);
            if (currentCount >= MaxTopicsPerUser)
            {
                await _messageCenterService.ShowWarningAsync($"You have reached the maximum limit of {MaxTopicsPerUser} topics. Remove unused topics before adding new ones.");
                return false;
            }

            // Check for duplicate directly using TopicDataService
            if (await _topicDataService.UserHasTopicAsync(userId, topicName))
            {
                await _messageCenterService.ShowWarningAsync($"Topic '{topicName}' already exists in your list.");
                return false;
            }

            // Create and save new topic
            var topicEntity = new TopicEntity
            {
                Name = topicName,
                UserId = userId,
                CheckDays = checkDays
            };

            _context.Topics.Add(topicEntity);
            await _context.SaveChangesAsync();

            // Trigger topic onboarding for immediate video suggestions
            try
            {
                var onboardingResult = await _topicOnboardingService.OnboardTopicAsync(
                    userId, topicName, topicEntity.Id);

                if (onboardingResult.InitialVideosAdded > 0)
                {
                    await _messageCenterService.ShowSuccessAsync(
                        $"Topic '{topicName}' added with {onboardingResult.InitialVideosAdded} video suggestions!");
                }
                else if (onboardingResult.Warnings.Any())
                {
                    await _messageCenterService.ShowSuccessAsync(
                        $"Topic '{topicName}' added successfully! Video suggestions will be available on your next suggestion request.");
                }
                else
                {
                    await _messageCenterService.ShowSuccessAsync($"Topic '{topicName}' added successfully!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Topic onboarding failed for {TopicName}, but topic was created successfully", topicName);
                await _messageCenterService.ShowSuccessAsync(
                    $"Topic '{topicName}' added successfully! Video suggestions will be available on your next suggestion request.");
            }

            _logger.LogInformation("User {UserId} added topic: {TopicName} with onboarding", userId, topicName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding topic {TopicName} for user {UserId}", topicName, userId);
            await _messageCenterService.ShowErrorAsync("An error occurred while adding the topic. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Deletes a topic for the specified user using soft delete pattern.
    /// </summary>
    public async Task<bool> DeleteTopicAsync(string userId, Guid topicId)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(userId))
            {
                await _messageCenterService.ShowErrorAsync("User authentication required.");
                return false;
            }

            if (topicId == Guid.Empty)
            {
                await _messageCenterService.ShowErrorAsync("Invalid topic selected.");
                return false;
            }

            // Use TopicDataService to find the topic
            var topic = await _topicDataService.GetTopicByIdAsync(topicId, userId);

            if (topic == null || topic.IsDeleted)
            {
                await _messageCenterService.ShowWarningAsync("Topic not found or you don't have permission to delete it.");
                return false;
            }

            // Perform soft delete
            topic.IsDeleted = true;
            await _context.SaveChangesAsync();

            await _messageCenterService.ShowSuccessAsync($"Topic '{topic.Name}' deleted successfully!");

            _logger.LogInformation("User {UserId} deleted topic: {TopicName} (ID: {TopicId})", userId, topic.Name, topicId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting topic {TopicId} for user {UserId}", topicId, userId);
            await _messageCenterService.ShowErrorAsync("An error occurred while deleting the topic. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Gets the current count of topics for a user.
    /// Lightweight wrapper around TopicDataService for external callers.
    /// </summary>
    //public async Task<int> GetTopicCountAsync(string userId)
    //{
    //    try
    //    {
    //        return await _topicDataService.GetUserTopicCountAsync(userId);
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error getting topic count for user {UserId}", userId);
    //        return 0; // Don't show error message for read operations used by UI
    //    }
    //}

    /// <summary>
    /// Checks if a topic name already exists for the user (case-insensitive).
    /// Lightweight wrapper around TopicDataService for external callers.
    /// </summary>
    //public async Task<bool> TopicExistsAsync(string userId, string topicName)
    //{
    //    try
    //    {
    //        if (string.IsNullOrWhiteSpace(topicName))
    //            return false;

    //        return await _topicDataService.UserHasTopicAsync(userId, topicName);
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error checking topic existence for user {UserId}, topic {TopicName}", userId, topicName);
    //        return false; // Don't show error message for read operations used by UI
    //    }
    //}

    /// <summary>
    /// Gets all topic names for the specified user as simple strings.
    /// Used by suggestion service for YouTube search queries.
    /// Now uses TopicDataService for consistent data access.
    /// </summary>
    /// <param name="userId">User ID to get topic names for</param>
    /// <returns>List of topic names as strings, empty list if none found</returns>
    //public async Task<List<string>> GetUserTopicNamesAsync(string userId)
    //{
    //    try
    //    {
    //        if (string.IsNullOrWhiteSpace(userId))
    //        {
    //            _logger.LogWarning("GetUserTopicNamesAsync called with null or empty userId");
    //            return new List<string>();
    //        }

    //        // Use TopicDataService to get topic entities, then extract names
    //        var topics = await _topicDataService.GetUserTopicsAsync(userId);
    //        var topicNames = topics.Select(t => t.Name).ToList();

    //        _logger.LogDebug("Retrieved {TopicCount} topic names for user {UserId}", topicNames.Count, userId);
    //        return topicNames;
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error retrieving topic names for user {UserId}", userId);
    //        return new List<string>();
    //    }
    //}
}