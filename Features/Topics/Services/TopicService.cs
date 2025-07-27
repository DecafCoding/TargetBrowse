using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Topics.Models;
using TargetBrowse.Services;

namespace TargetBrowse.Features.Topics.Services;

/// <summary>
/// Implementation of topic management service.
/// Handles business logic for user topics including validation and persistence.
/// </summary>
public class TopicService : ITopicService
{
    private readonly ApplicationDbContext _context;
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<TopicService> _logger;

    private const int MaxTopicsPerUser = 10;

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
    /// Adds a new topic for the specified user with business rule validation.
    /// </summary>
    public async Task<bool> AddTopicAsync(string userId, string topicName)
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

            // Check topic limit
            var currentCount = await GetTopicCountAsync(userId);
            if (currentCount >= MaxTopicsPerUser)
            {
                await _messageCenterService.ShowWarningAsync($"You have reached the maximum limit of {MaxTopicsPerUser} topics. Remove unused topics before adding new ones.");
                return false;
            }

            // Check for duplicate (case-insensitive)
            if (await TopicExistsAsync(userId, topicName))
            {
                await _messageCenterService.ShowWarningAsync($"Topic '{topicName}' already exists in your list.");
                return false;
            }

            // Create and save new topic
            var topicEntity = new TopicEntity
            {
                Name = topicName,
                UserId = userId
            };

            _context.Topics.Add(topicEntity);
            await _context.SaveChangesAsync();

            await _messageCenterService.ShowSuccessAsync($"Topic '{topicName}' added successfully!");

            _logger.LogInformation("User {UserId} added topic: {TopicName}", userId, topicName);
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
    /// Gets all topics for the specified user ordered by creation date (newest first).
    /// </summary>
    public async Task<List<TopicDisplayModel>> GetUserTopicsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUserTopicsAsync called with null or empty userId");
                return new List<TopicDisplayModel>();
            }

            var topics = await _context.Topics
                .Where(t => t.UserId == userId && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TopicDisplayModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            _logger.LogDebug("Retrieved {TopicCount} topics for user {UserId}", topics.Count, userId);
            return topics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving topics for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Unable to load your topics. Please refresh the page and try again.");
            return new List<TopicDisplayModel>();
        }
    }

    /// <summary>
    /// Gets the current count of topics for a user.
    /// </summary>
    public async Task<int> GetTopicCountAsync(string userId)
    {
        try
        {
            return await _context.Topics
                .Where(t => t.UserId == userId && !t.IsDeleted)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting topic count for user {UserId}", userId);
            return 0;
        }
    }

    /// <summary>
    /// Checks if a topic name already exists for the user (case-insensitive).
    /// </summary>
    public async Task<bool> TopicExistsAsync(string userId, string topicName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(topicName))
                return false;

            return await _context.Topics
                .Where(t => t.UserId == userId && !t.IsDeleted)
                .AnyAsync(t => t.Name.ToLower() == topicName.ToLower());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking topic existence for user {UserId}, topic {TopicName}", userId, topicName);
            return false;
        }
    }
}