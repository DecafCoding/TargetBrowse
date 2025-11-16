using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Suggestions.Data;

/// <summary>
/// Implementation of suggestion repository for data access operations.
/// Handles database operations for suggestion entities using Entity Framework Core.
/// Enhanced implementation with improved error handling, performance optimization, and business logic.
/// Inherits common database patterns from BaseRepository.
/// </summary>
public class SuggestionRepository : BaseRepository<SuggestionEntity>, ISuggestionRepository
{
    private const int SuggestionExpiryDays = 30;
    private const int MAX_PENDING_SUGGESTIONS = 1000;
    private const int BATCH_SIZE = 50; // For bulk operations

    public SuggestionRepository(ApplicationDbContext context, ILogger<SuggestionRepository> logger)
        : base(context, logger)
    {
    }

    /// <summary>
    /// Creates new suggestion entities in the database.
    /// </summary>
    public async Task<List<SuggestionEntity>> CreateSuggestionsAsync(List<SuggestionEntity> suggestions)
    {
        if (!suggestions.Any())
        {
            _logger.LogInformation("No suggestions provided for creation");
            return new List<SuggestionEntity>();
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var suggestion in suggestions)
            {
                suggestion.Id = Guid.NewGuid();
                suggestion.CreatedAt = DateTime.UtcNow;
                _context.Suggestions.Add(suggestion);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Created {Count} suggestions", suggestions.Count);
            return suggestions;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating suggestions");
            throw;
        }
    }

    /// <summary>
    /// Gets pending suggestions for a user with pagination.
    /// </summary>
    public async Task<List<SuggestionEntity>> GetPendingSuggestionsAsync(string userId, int pageNumber = 1, int pageSize = 20)
    {
        try
        {
            var skip = (pageNumber - 1) * pageSize;
            var cutoffDate = DateTime.UtcNow.AddDays(-SuggestionExpiryDays);

            return await _context.Suggestions
                .Include(s => s.Video)
                    .ThenInclude(v => v.Channel)
                .Where(s => s.UserId == userId &&
                           !s.IsApproved &&
                           !s.IsDenied &&
                           !s.IsDeleted &&
                           s.CreatedAt > cutoffDate)
                .OrderByDescending(s => s.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .AsSplitQuery() // Optimize for multiple includes
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending suggestions for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets a suggestion by ID with user ownership validation.
    /// </summary>
    public async Task<SuggestionEntity?> GetSuggestionByIdAsync(Guid suggestionId, string userId)
    {
        try
        {
            return await _context.Suggestions
                .Include(s => s.Video)
                    .ThenInclude(v => v.Channel)
                .Where(s => s.Id == suggestionId &&
                           s.UserId == userId &&
                           !s.IsDeleted)
                .AsSplitQuery()
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggestion {SuggestionId} for user {UserId}", suggestionId, userId);
            return null;
        }
    }

    /// <summary>
    /// Marks suggestions as approved and moves to user's library.
    /// </summary>
    public async Task<int> ApproveSuggestionsAsync(List<Guid> suggestionIds, string userId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var suggestions = await _context.Suggestions
                .Where(s => suggestionIds.Contains(s.Id) &&
                           s.UserId == userId &&
                           !s.IsApproved &&
                           !s.IsDenied &&
                           !s.IsDeleted)
                .ToListAsync();

            var approvedCount = 0;
            var currentTime = DateTime.UtcNow;

            foreach (var suggestion in suggestions)
            {
                suggestion.IsApproved = true;
                suggestion.ApprovedAt = currentTime;
                suggestion.LastModifiedAt = currentTime;
                approvedCount++;
            }

            if (approvedCount > 0)
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Approved {Count} suggestions for user {UserId}", approvedCount, userId);
            }
            else
            {
                await transaction.RollbackAsync();
            }

            return approvedCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error approving suggestions for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Marks suggestions as denied and removes from queue.
    /// </summary>
    public async Task<int> DenySuggestionsAsync(List<Guid> suggestionIds, string userId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var suggestions = await _context.Suggestions
                .Where(s => suggestionIds.Contains(s.Id) &&
                           s.UserId == userId &&
                           !s.IsApproved &&
                           !s.IsDenied &&
                           !s.IsDeleted)
                .ToListAsync();

            var deniedCount = 0;
            var currentTime = DateTime.UtcNow;

            foreach (var suggestion in suggestions)
            {
                suggestion.IsDenied = true;
                suggestion.DeniedAt = currentTime;
                suggestion.LastModifiedAt = currentTime;
                deniedCount++;
            }

            if (deniedCount > 0)
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Denied {Count} suggestions for user {UserId}", deniedCount, userId);
            }
            else
            {
                await transaction.RollbackAsync();
            }

            return deniedCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error denying suggestions for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Removes expired suggestions (older than 30 days and not reviewed).
    /// </summary>
    public async Task<int> CleanupExpiredSuggestionsAsync()
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var expiryDate = DateTime.UtcNow.AddDays(-SuggestionExpiryDays);

            var expiredSuggestions = await _context.Suggestions
                .Where(s => s.CreatedAt < expiryDate &&
                           !s.IsApproved &&
                           !s.IsDenied &&
                           !s.IsDeleted)
                .ToListAsync();

            if (expiredSuggestions.Any())
            {
                var currentTime = DateTime.UtcNow;
                foreach (var suggestion in expiredSuggestions)
                {
                    suggestion.IsDeleted = true;
                    suggestion.LastModifiedAt = currentTime;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Cleaned up {Count} expired suggestions", expiredSuggestions.Count);
                return expiredSuggestions.Count;
            }

            await transaction.RollbackAsync();
            return 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error cleaning up expired suggestions");
            throw;
        }
    }

    /// <summary>
    /// Removes all suggestions from a specific channel for a user.
    /// Called when user rates a channel 1-star.
    /// </summary>
    public async Task<int> RemoveSuggestionsByChannelAsync(string userId, Guid channelId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var channelSuggestions = await _context.Suggestions
                .Include(s => s.Video)
                .Where(s => s.UserId == userId &&
                           s.Video.ChannelId == channelId &&
                           !s.IsApproved &&
                           !s.IsDenied &&
                           !s.IsDeleted)
                .ToListAsync();

            if (channelSuggestions.Any())
            {
                var currentTime = DateTime.UtcNow;
                foreach (var suggestion in channelSuggestions)
                {
                    suggestion.IsDeleted = true;
                    suggestion.LastModifiedAt = currentTime;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Removed {Count} suggestions from channel {ChannelId} for user {UserId}",
                    channelSuggestions.Count, channelId, userId);
                return channelSuggestions.Count;
            }

            await transaction.RollbackAsync();
            return 0;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error removing suggestions by channel {ChannelId} for user {UserId}", channelId, userId);
            throw;
        }
    }

    /// <summary>
    /// Gets suggestion analytics data for a user.
    /// </summary>
    public async Task<SuggestionAnalytics> GetSuggestionAnalyticsAsync(string userId)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-SuggestionExpiryDays);

            var suggestions = await _context.Suggestions
                .Where(s => s.UserId == userId && !s.IsDeleted)
                .ToListAsync();

            var analytics = new SuggestionAnalytics
            {
                TotalSuggestionsGenerated = suggestions.Count,
                SuggestionsApproved = suggestions.Count(s => s.IsApproved),
                SuggestionsDenied = suggestions.Count(s => s.IsDenied),
                PendingSuggestions = suggestions.Count(s => !s.IsApproved && !s.IsDenied &&
                                                          s.CreatedAt > cutoffDate),
                LastSuggestionGenerated = suggestions.Any() ? suggestions.Max(s => s.CreatedAt) : null
            };

            analytics.SuggestionsExpired = analytics.TotalSuggestionsGenerated -
                                         analytics.SuggestionsApproved -
                                         analytics.SuggestionsDenied -
                                         analytics.PendingSuggestions;

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting suggestion analytics for user {UserId}", userId);
            return new SuggestionAnalytics();
        }
    }

    /// <summary>
    /// Searches suggestions by video title or channel name.
    /// </summary>
    public async Task<List<SuggestionEntity>> SearchSuggestionsAsync(string userId, string searchQuery, SuggestionStatus? status = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return new List<SuggestionEntity>();

            var cutoffDate = DateTime.UtcNow.AddDays(-SuggestionExpiryDays);

            var query = _context.Suggestions
                .Include(s => s.Video)
                    .ThenInclude(v => v.Channel)
                .Where(s => s.UserId == userId &&
                           !s.IsDeleted &&
                           (s.Video.Title.Contains(searchQuery) ||
                            s.Video.Channel.Name.Contains(searchQuery) ||
                            s.Reason.Contains(searchQuery)));

            // Apply status filter if provided
            if (status.HasValue)
            {
                query = status.Value switch
                {
                    SuggestionStatus.Pending => query.Where(s => !s.IsApproved && !s.IsDenied &&
                                                                 s.CreatedAt > cutoffDate),
                    SuggestionStatus.Approved => query.Where(s => s.IsApproved),
                    SuggestionStatus.Denied => query.Where(s => s.IsDenied),
                    SuggestionStatus.Expired => query.Where(s => !s.IsApproved && !s.IsDenied &&
                                                                 s.CreatedAt <= cutoffDate),
                    _ => query
                };
            }

            return await query
                .OrderByDescending(s => s.CreatedAt)
                .Take(100) // Limit search results
                .AsSplitQuery()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching suggestions for user {UserId} with query {SearchQuery}", userId, searchQuery);
            return new List<SuggestionEntity>();
        }
    }

    /// <summary>
    /// Updates the last channel check date for suggestion generation.
    /// </summary>
    public async Task UpdateChannelLastCheckDateAsync(string userId, Guid channelId, DateTime lastCheckDate)
    {
        try
        {
            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.Id == channelId);

            if (channel != null)
            {
                channel.LastCheckDate = lastCheckDate;
                channel.LastModifiedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogDebug("Updated last check date for channel {ChannelId} to {LastCheckDate}",
                    channelId, lastCheckDate);
            }
            else
            {
                _logger.LogWarning("Channel {ChannelId} not found for last check date update", channelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last check date for channel {ChannelId}", channelId);
            throw;
        }
    }

    /// <summary>
    /// Gets channels that need to be checked for new videos with user ratings.
    /// Enhanced to include proper channel rating information and exclude 1-star channels.
    /// </summary>
    public async Task<List<ChannelCheckInfo>> GetChannelsForUpdateCheckAsync(string userId)
    {
        try
        {
            var query = from uc in _context.UserChannels
                        join c in _context.Channels on uc.ChannelId equals c.Id
                        where uc.UserId == userId && !uc.IsDeleted
                        select new { UserChannel = uc, Channel = c };

            var userChannelsWithChannels = await query.ToListAsync();

            var channelCheckInfos = new List<ChannelCheckInfo>();

            foreach (var item in userChannelsWithChannels)
            {
                // Get the user's rating for this channel
                var channelRating = await _context.Ratings
                    .Where(r => r.UserId == userId &&
                               r.ChannelId == item.Channel.Id &&
                               !r.IsDeleted)
                    .Select(r => (int?)r.Stars)
                    .FirstOrDefaultAsync();

                // Skip 1-star rated channels as per business rules
                if (channelRating == 1)
                {
                    _logger.LogDebug("Skipping 1-star rated channel {ChannelId} for user {UserId}",
                        item.Channel.Id, userId);
                    continue;
                }

                channelCheckInfos.Add(new ChannelCheckInfo
                {
                    Channel = item.Channel,
                    LastCheckDate = item.Channel.LastCheckDate,
                    UserRating = channelRating
                });
            }

            _logger.LogDebug("Found {Count} channels for update check for user {UserId} (excluding 1-star channels)",
                channelCheckInfos.Count, userId);

            return channelCheckInfos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channels for update check for user {UserId}", userId);
            return new List<ChannelCheckInfo>();
        }
    }

    /// <summary>
    /// Helper method to truncate description to fit database constraints.
    /// Ensures description fits within the 2000 character limit while preserving meaning.
    /// </summary>
    private static string TruncateDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        const int maxLength = 2000;

        if (description.Length <= maxLength)
            return description;

        // Find a good truncation point (prefer end of sentence or word)
        var truncated = description.Substring(0, maxLength - 3); // Leave room for "..."

        // Try to truncate at end of sentence
        var lastPeriod = truncated.LastIndexOf('.');
        if (lastPeriod > maxLength * 0.8) // Only if we don't lose too much content
        {
            return truncated.Substring(0, lastPeriod + 1);
        }

        // Try to truncate at end of word
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength * 0.9) // Only if we don't lose too much content
        {
            return truncated.Substring(0, lastSpace) + "...";
        }

        // Hard truncation with ellipsis
        return truncated + "...";
    }

    /// <summary>
    /// Helper method to select the optimal thumbnail URL from YouTube API response.
    /// Prioritizes medium size (320x180) for consistent display across the application.
    /// </summary>
    private static string GetOptimalThumbnailUrl(string? thumbnailUrl)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
            return string.Empty;

        // If it's already a direct URL, use it as-is
        if (thumbnailUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return thumbnailUrl;

        // This method assumes the VideoInfo.ThumbnailUrl is already the optimal URL
        // from the YouTube API processing in SharedYouTubeService
        return thumbnailUrl;
    }

    /// <summary>
    /// Creates suggestion with topic relationships.
    /// </summary>
    public async Task<SuggestionEntity> CreateSuggestionWithTopicsAsync(string userId, Guid videoId, string reason, List<Guid> topicIds)
    {
        var suggestion = new SuggestionEntity
        {
            UserId = userId,
            VideoId = videoId,
            Reason = reason, // Keep populating this
            IsApproved = false,
            IsDenied = false
        };

        _context.Suggestions.Add(suggestion);
        await _context.SaveChangesAsync(); // Save to get the SuggestionId

        // Create SuggestionTopicEntity records
        if (topicIds?.Any() == true)
        {
            var suggestionTopics = topicIds.Select(topicId => new SuggestionTopicEntity
            {
                SuggestionId = suggestion.Id,
                TopicId = topicId
            }).ToList();

            _context.SuggestionTopics.AddRange(suggestionTopics);
            await _context.SaveChangesAsync();
        }

        return suggestion;
    }

}