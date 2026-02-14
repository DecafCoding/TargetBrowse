using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Services.DataServices
{
    /// <summary>
    /// Data access service implementation for topic-related operations.
    /// Handles database queries for topics across multiple feature services.
    /// </summary>
    public class TopicDataService : ITopicDataService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TopicDataService> _logger;

        public TopicDataService(ApplicationDbContext context, ILogger<TopicDataService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all topics for a specific user, ordered by name.
        /// Used by multiple services for topic-based operations.
        /// Includes navigation properties for video count calculations.
        /// </summary>
        public async Task<List<TopicEntity>> GetUserTopicsAsync(string userId)
        {
            try
            {
                return await _context.Topics
                    .Where(t => t.UserId == userId)
                    .Include(t => t.SuggestionTopics)
                        .ThenInclude(st => st.Suggestion)
                            .ThenInclude(s => s.Video)
                                .ThenInclude(v => v.UserVideos.Where(uv => uv.UserId == userId))
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving topics for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Gets a specific topic by ID for a user.
        /// Ensures the topic belongs to the requesting user.
        /// </summary>
        public async Task<TopicEntity?> GetTopicByIdAsync(Guid topicId, string userId)
        {
            try
            {
                return await _context.Topics
                    .FirstOrDefaultAsync(t => t.Id == topicId && t.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving topic {TopicId} for user {UserId}", topicId, userId);
                throw;
            }
        }

        /// <summary>
        /// Checks if a user already has a topic with the given name.
        /// Uses case-insensitive comparison to prevent near-duplicates.
        /// </summary>
        public async Task<bool> UserHasTopicAsync(string userId, string topicName)
        {
            try
            {
                return await _context.Topics
                    .AnyAsync(t => t.UserId == userId && t.Name.ToLower() == topicName.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} has topic {TopicName}", userId, topicName);
                throw;
            }
        }

        /// <summary>
        /// Gets the count of topics for a user.
        /// Used for enforcing the 10-topic limit at business logic level.
        /// </summary>
        public async Task<int> GetUserTopicCountAsync(string userId)
        {
            try
            {
                return await _context.Topics
                    .CountAsync(t => t.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting topic count for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Updates the LastCheckedDate on a topic after a successful API search.
        /// </summary>
        public async Task UpdateLastCheckedDateAsync(Guid topicId, DateTime checkedDate)
        {
            try
            {
                var topic = await _context.Topics.FindAsync(topicId);
                if (topic != null)
                {
                    topic.LastCheckedDate = checkedDate;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating LastCheckedDate for topic {TopicId}", topicId);
                throw;
            }
        }
    }
}
