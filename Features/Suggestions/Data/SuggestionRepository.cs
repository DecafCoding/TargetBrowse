using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;

namespace TargetBrowse.Features.Suggestions.Data;

/// <summary>
/// Implementation of suggestion repository for data access operations.
/// Handles database operations for suggestion entities using Entity Framework Core.
/// Enhanced implementation with improved error handling, performance optimization, and business logic.
/// </summary>
public class SuggestionRepository : ISuggestionRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SuggestionRepository> _logger;

    private const int SuggestionExpiryDays = 30;
    private const int MAX_PENDING_SUGGESTIONS = 1000;
    private const int BATCH_SIZE = 50; // For bulk operations

    public SuggestionRepository(ApplicationDbContext context, ILogger<SuggestionRepository> logger)
    {
        _context = context;
        _logger = logger;
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
    /// Creates suggestion entities from video suggestions with enhanced business logic.
    /// Ensures proper user-video relationships and maintains business rules for YT-010-03.
    /// </summary>
    public async Task<List<SuggestionEntity>> CreateSuggestionsFromVideoSuggestionsAsync(List<VideoSuggestion> videoSuggestions, string userId)
    {
        if (!videoSuggestions.Any())
        {
            _logger.LogInformation("No video suggestions to create for user {UserId}", userId);
            return new List<SuggestionEntity>();
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Check if user can request more suggestions
            if (!await CanUserRequestSuggestionsAsync(userId))
            {
                _logger.LogWarning("User {UserId} cannot request more suggestions - at limit", userId);
                throw new InvalidOperationException("Cannot create suggestions: user has reached maximum pending suggestions limit");
            }

            var createdSuggestions = new List<SuggestionEntity>();

            // Process suggestions in batches to avoid overwhelming database
            for (int i = 0; i < videoSuggestions.Count; i += BATCH_SIZE)
            {
                var batch = videoSuggestions.Skip(i).Take(BATCH_SIZE).ToList();
                var batchEntities = new List<SuggestionEntity>();

                foreach (var suggestion in batch)
                {
                    try
                    {
                        // First ensure the video exists in database to get the VideoEntity
                        var videoEntity = await EnsureVideoExistsInternalAsync(suggestion.Video);

                        // Check if this video has already been suggested to avoid duplicates
                        if (await HasPendingSuggestionForVideoAsync(userId, videoEntity.Id))
                        {
                            _logger.LogDebug("Video {VideoId} already suggested to user {UserId}, skipping",
                                suggestion.Video.YouTubeVideoId, userId);
                            continue;
                        }

                        var suggestionEntity = new SuggestionEntity
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            VideoId = videoEntity.Id,
                            Reason = suggestion.Reason,
                            IsApproved = false,
                            IsDenied = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Suggestions.Add(suggestionEntity);
                        batchEntities.Add(suggestionEntity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create suggestion for video {VideoId} for user {UserId}",
                            suggestion.Video.YouTubeVideoId, userId);
                        // Continue processing other suggestions
                    }
                }

                if (batchEntities.Any())
                {
                    await _context.SaveChangesAsync();
                    createdSuggestions.AddRange(batchEntities);

                    _logger.LogDebug("Created batch of {Count} suggestions for user {UserId}",
                        batchEntities.Count, userId);
                }
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Created {CreatedCount} out of {TotalCount} suggestions for user {UserId}",
                createdSuggestions.Count, videoSuggestions.Count, userId);

            return createdSuggestions;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating suggestions from video suggestions for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a user can request new suggestions based on business rules.
    /// Validates against maximum pending suggestions limit for YT-010-03.
    /// </summary>
    public async Task<bool> CanUserRequestSuggestionsAsync(string userId)
    {
        try
        {
            var pendingCount = await GetPendingSuggestionsCountAsync(userId);
            return pendingCount < MAX_PENDING_SUGGESTIONS;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} can request suggestions", userId);
            return false;
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
    /// Gets the count of pending suggestions for a user.
    /// </summary>
    public async Task<int> GetPendingSuggestionsCountAsync(string userId)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-SuggestionExpiryDays);

            return await _context.Suggestions
                .Where(s => s.UserId == userId &&
                           !s.IsApproved &&
                           !s.IsDenied &&
                           !s.IsDeleted &&
                           s.CreatedAt > cutoffDate)
                .CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending suggestions count for user {UserId}", userId);
            return 0;
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
    /// Updates a suggestion entity in the database.
    /// </summary>
    public async Task<SuggestionEntity> UpdateSuggestionAsync(SuggestionEntity suggestion)
    {
        try
        {
            suggestion.LastModifiedAt = DateTime.UtcNow;
            _context.Suggestions.Update(suggestion);
            await _context.SaveChangesAsync();
            return suggestion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating suggestion {SuggestionId}", suggestion.Id);
            throw;
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
    /// Checks if a video already has a pending suggestion for the user.
    /// </summary>
    public async Task<bool> HasPendingSuggestionForVideoAsync(string userId, Guid videoId)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-SuggestionExpiryDays);

            return await _context.Suggestions
                .AnyAsync(s => s.UserId == userId &&
                              s.VideoId == videoId &&
                              !s.IsApproved &&
                              !s.IsDenied &&
                              !s.IsDeleted &&
                              s.CreatedAt > cutoffDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking pending suggestion for video {VideoId} and user {UserId}", videoId, userId);
            return false;
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
    /// Gets all suggestions for a user with optional filtering.
    /// </summary>
    public async Task<List<SuggestionEntity>> GetUserSuggestionsAsync(string userId, SuggestionStatus? status = null, int pageNumber = 1, int pageSize = 50)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-SuggestionExpiryDays);

            var query = _context.Suggestions
                .Include(s => s.Video)
                    .ThenInclude(v => v.Channel)
                .Where(s => s.UserId == userId && !s.IsDeleted);

            // Apply status filter
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

            var skip = (pageNumber - 1) * pageSize;
            return await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .AsSplitQuery()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user suggestions for user {UserId}", userId);
            return new List<SuggestionEntity>();
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
    /// Gets the most recent suggestion generation date for a user.
    /// </summary>
    public async Task<DateTime?> GetLastSuggestionGenerationDateAsync(string userId)
    {
        try
        {
            return await _context.Suggestions
                .Where(s => s.UserId == userId && !s.IsDeleted)
                .MaxAsync(s => (DateTime?)s.CreatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last suggestion generation date for user {UserId}", userId);
            return null;
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
    /// Bulk creates video entities if they don't already exist.
    /// Enhanced version for YT-010-03 with improved error handling, batch processing,
    /// and proper metadata population including thumbnails and descriptions.
    /// </summary>
    public async Task<List<VideoEntity>> EnsureVideosExistAsync(List<VideoInfo> videos)
    {
        if (!videos.Any())
        {
            return new List<VideoEntity>();
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var youTubeVideoIds = videos.Select(v => v.YouTubeVideoId).Distinct().ToList();

            // Get existing videos
            var existingVideos = await _context.Videos
                .Include(v => v.Channel)
                .Where(v => youTubeVideoIds.Contains(v.YouTubeVideoId))
                .ToListAsync();

            var existingVideoIds = existingVideos.Select(v => v.YouTubeVideoId).ToHashSet();
            var newVideos = videos.Where(v => !existingVideoIds.Contains(v.YouTubeVideoId)).ToList();
            var videosToUpdate = videos.Where(v => existingVideoIds.Contains(v.YouTubeVideoId)).ToList();

            // Update existing videos with missing metadata
            int updatedCount = 0;
            foreach (var videoInfo in videosToUpdate)
            {
                var existingVideo = existingVideos.First(v => v.YouTubeVideoId == videoInfo.YouTubeVideoId);
                bool hasChanges = false;

                // Update thumbnail URL if we have a better one and existing is empty
                if (!string.IsNullOrEmpty(videoInfo.ThumbnailUrl) &&
                    string.IsNullOrEmpty(existingVideo.ThumbnailUrl))
                {
                    existingVideo.ThumbnailUrl = GetOptimalThumbnailUrl(videoInfo.ThumbnailUrl);
                    hasChanges = true;
                }

                // Update description if we have one and existing is empty
                if (!string.IsNullOrEmpty(videoInfo.Description) &&
                    string.IsNullOrEmpty(existingVideo.Description))
                {
                    existingVideo.Description = TruncateDescription(videoInfo.Description);
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    existingVideo.LastModifiedAt = DateTime.UtcNow;
                    updatedCount++;
                }
            }

            if (updatedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogDebug("Updated metadata for {Count} existing videos", updatedCount);
            }

            if (newVideos.Any())
            {
                var videoEntities = new List<VideoEntity>();

                // Process in batches to avoid database timeout
                for (int i = 0; i < newVideos.Count; i += BATCH_SIZE)
                {
                    var batch = newVideos.Skip(i).Take(BATCH_SIZE).ToList();

                    foreach (var video in batch)
                    {
                        try
                        {
                            var videoEntity = await EnsureVideoExistsInternalAsync(video);
                            videoEntities.Add(videoEntity);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to create video entity for {VideoId}", video.YouTubeVideoId);
                            // Continue processing other videos
                        }
                    }

                    _logger.LogDebug("Processed batch {BatchNumber} of {TotalBatches} for video creation",
                        (i / BATCH_SIZE) + 1, (newVideos.Count + BATCH_SIZE - 1) / BATCH_SIZE);
                }

                existingVideos.AddRange(videoEntities);

                _logger.LogInformation("Created {Count} new video entities out of {Total} videos with complete metadata",
                    videoEntities.Count, newVideos.Count);
            }

            await transaction.CommitAsync();
            return existingVideos;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error ensuring videos exist with metadata");
            throw;
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

    /// <summary>
    /// Creates suggestion entities for topic onboarding with proper topic relationships.
    /// Creates both SuggestionEntity and SuggestionTopicEntity records for proper data modeling.
    /// </summary>
    public async Task<List<SuggestionEntity>> CreateTopicOnboardingSuggestionsAsync(
        string userId,
        List<VideoEntity> videoEntities,
        Guid topicId,
        string topicName)
    {
        var suggestions = new List<SuggestionEntity>();

        try
        {
            foreach (var videoEntity in videoEntities)
            {
                // Double-check for existing suggestions to avoid duplicates
                var hasExisting = await HasPendingSuggestionForVideoAsync(userId, videoEntity.Id);
                if (hasExisting)
                {
                    _logger.LogDebug("Skipping video {VideoId} - suggestion already exists for user {UserId}",
                        videoEntity.YouTubeVideoId, userId);
                    continue;
                }

                var suggestion = new SuggestionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    VideoId = videoEntity.Id,
                    Reason = $"🎯 New Topic: {topicName}",
                    IsApproved = false,
                    IsDenied = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Suggestions.Add(suggestion);
                suggestions.Add(suggestion);
            }

            // Save suggestions first
            await _context.SaveChangesAsync();

            // Create topic relationships
            foreach (var suggestion in suggestions)
            {
                var suggestionTopic = new SuggestionTopicEntity
                {
                    Id = Guid.NewGuid(),
                    SuggestionId = suggestion.Id,
                    TopicId = topicId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.SuggestionTopics.Add(suggestionTopic);
            }

            // Save topic relationships
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created {SuggestionCount} topic onboarding suggestions with relationships for topic {TopicId}",
                suggestions.Count, topicId);

            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating topic onboarding suggestions for topic {TopicId} and user {UserId}",
                topicId, userId);
            throw;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Internal helper method to ensure a video exists and return the VideoEntity.
    /// Enhanced to properly populate ThumbnailUrl and Description from VideoInfo.
    /// Used by CreateSuggestionsFromVideoSuggestionsAsync to get proper entity IDs.
    /// </summary>
    private async Task<VideoEntity> EnsureVideoExistsInternalAsync(VideoInfo video)
    {
        try
        {
            // First try to find existing video
            var existingVideo = await _context.Videos
                .Include(v => v.Channel)
                .FirstOrDefaultAsync(v => v.YouTubeVideoId == video.YouTubeVideoId);

            if (existingVideo != null)
            {
                // Update existing video with new metadata if needed
                bool hasChanges = false;

                // Update thumbnail URL if we have a better one and existing is empty
                if (!string.IsNullOrEmpty(video.ThumbnailUrl) &&
                    string.IsNullOrEmpty(existingVideo.ThumbnailUrl))
                {
                    existingVideo.ThumbnailUrl = GetOptimalThumbnailUrl(video.ThumbnailUrl);
                    hasChanges = true;
                }

                // Update description if we have one and existing is empty
                if (!string.IsNullOrEmpty(video.Description) &&
                    string.IsNullOrEmpty(existingVideo.Description))
                {
                    existingVideo.Description = TruncateDescription(video.Description);
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    existingVideo.LastModifiedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    _logger.LogDebug("Updated metadata for existing video {VideoId}",
                        video.YouTubeVideoId);
                }

                return existingVideo;
            }

            // Find or create the channel entity first
            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.YouTubeChannelId == video.ChannelId);

            if (channel == null)
            {
                // Create channel if it doesn't exist
                channel = new ChannelEntity
                {
                    Id = Guid.NewGuid(),
                    YouTubeChannelId = video.ChannelId,
                    Name = video.ChannelName,
                    ThumbnailUrl = string.Empty,
                    VideoCount = 0,
                    SubscriberCount = 0,
                    PublishedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Channels.Add(channel);
                await _context.SaveChangesAsync(); // Save to get the ID

                _logger.LogDebug("Created new channel entity {ChannelId} for {ChannelName}",
                    channel.Id, channel.Name);
            }

            // Create new video entity with complete metadata
            var videoEntity = new VideoEntity
            {
                Id = Guid.NewGuid(),
                YouTubeVideoId = video.YouTubeVideoId,
                Title = video.Title,
                ChannelId = channel.Id,
                PublishedAt = video.PublishedAt,
                ViewCount = video.ViewCount,
                LikeCount = video.LikeCount,
                CommentCount = video.CommentCount,
                Duration = video.Duration,
                // Properly populate new fields from VideoInfo
                ThumbnailUrl = GetOptimalThumbnailUrl(video.ThumbnailUrl),
                Description = TruncateDescription(video.Description),
                RawTranscript = string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.Videos.Add(videoEntity);
            await _context.SaveChangesAsync();

            // Load the channel relationship for return
            videoEntity.Channel = channel;

            _logger.LogDebug("Created new video entity {VideoId} for {VideoTitle} with thumbnail and description",
                videoEntity.Id, videoEntity.Title);

            return videoEntity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring video {VideoId} exists internally", video.YouTubeVideoId);
            throw;
        }
    }

    #endregion
}