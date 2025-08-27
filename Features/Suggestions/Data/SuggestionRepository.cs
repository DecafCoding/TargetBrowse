using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;

namespace TargetBrowse.Features.Suggestions.Data;

/// <summary>
/// Implementation of suggestion repository for data access operations.
/// Handles database operations for suggestion entities using Entity Framework Core.
/// </summary>
public class SuggestionRepository : ISuggestionRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SuggestionRepository> _logger;

    private const int SuggestionExpiryDays = 30;
    private const int MAX_PENDING_SUGGESTIONS = 100;  // Added for YT-010-03

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
        try
        {
            foreach (var suggestion in suggestions)
            {
                suggestion.Id = Guid.NewGuid();
                suggestion.CreatedAt = DateTime.UtcNow;
                _context.Suggestions.Add(suggestion);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Created {Count} suggestions", suggestions.Count);
            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating suggestions");
            throw;
        }
    }

    // NEW METHOD FOR YT-010-03: Create suggestions from VideoSuggestion objects with business logic
    /// <summary>
    /// Creates suggestion entities from video suggestions with enhanced business logic.
    /// Ensures proper user-video relationships and maintains business rules for YT-010-03.
    /// </summary>
    public async Task<List<SuggestionEntity>> CreateSuggestionsFromVideoSuggestionsAsync(List<VideoSuggestion> videoSuggestions, string userId)
    {
        try
        {
            if (!videoSuggestions.Any())
            {
                _logger.LogInformation("No video suggestions to create for user {UserId}", userId);
                return new List<SuggestionEntity>();
            }

            // Check if user can request more suggestions
            if (!await CanUserRequestSuggestionsAsync(userId))
            {
                _logger.LogWarning("User {UserId} cannot request more suggestions - at limit", userId);
                throw new InvalidOperationException("Cannot create suggestions: user has reached maximum pending suggestions limit");
            }

            var createdSuggestions = new List<SuggestionEntity>();

            // Process suggestions in batches to avoid overwhelming database
            const int batchSize = 25;
            for (int i = 0; i < videoSuggestions.Count; i += batchSize)
            {
                var batch = videoSuggestions.Skip(i).Take(batchSize).ToList();
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
                            UserId = userId,
                            VideoId = videoEntity.Id,
                            Reason = suggestion.Reason,
                            IsApproved = false,
                            IsDenied = false
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

            _logger.LogInformation("Created {CreatedCount} out of {TotalCount} suggestions for user {UserId}",
                createdSuggestions.Count, videoSuggestions.Count, userId);

            return createdSuggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating suggestions from video suggestions for user {UserId}", userId);
            throw;
        }
    }

    // NEW METHOD FOR YT-010-03: Check if user can request suggestions
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

            return await _context.Suggestions
                .Include(s => s.Video)
                    .ThenInclude(v => v.Channel)
                .Where(s => s.UserId == userId &&
                           !s.IsApproved &&
                           !s.IsDenied &&
                           !s.IsDeleted &&
                           s.CreatedAt > DateTime.UtcNow.AddDays(-SuggestionExpiryDays))
                .OrderByDescending(s => s.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
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
            return await _context.Suggestions
                .Where(s => s.UserId == userId &&
                           !s.IsApproved &&
                           !s.IsDenied &&
                           !s.IsDeleted &&
                           s.CreatedAt > DateTime.UtcNow.AddDays(-SuggestionExpiryDays))
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
            suggestion.LastModifiedAt = DateTime.UtcNow;  // Using UpdatedAt instead of LastModifiedAt for consistency
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
            foreach (var suggestion in suggestions)
            {
                suggestion.IsApproved = true;
                suggestion.ApprovedAt = DateTime.UtcNow;
                suggestion.LastModifiedAt = DateTime.UtcNow;
                approvedCount++;
            }

            if (approvedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Approved {Count} suggestions for user {UserId}", approvedCount, userId);
            }

            return approvedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving suggestions for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Marks suggestions as denied and removes from queue.
    /// </summary>
    public async Task<int> DenySuggestionsAsync(List<Guid> suggestionIds, string userId)
    {
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
            foreach (var suggestion in suggestions)
            {
                suggestion.IsDenied = true;
                suggestion.DeniedAt = DateTime.UtcNow;
                suggestion.LastModifiedAt = DateTime.UtcNow;
                deniedCount++;
            }

            if (deniedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Denied {Count} suggestions for user {UserId}", deniedCount, userId);
            }

            return deniedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying suggestions for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Removes expired suggestions (older than 30 days and not reviewed).
    /// NOTE: This method exists but is excluded from MVP scope per YT-010-03 requirements.
    /// </summary>
    public async Task<int> CleanupExpiredSuggestionsAsync()
    {
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
                foreach (var suggestion in expiredSuggestions)
                {
                    suggestion.IsDeleted = true;
                    suggestion.LastModifiedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Cleaned up {Count} expired suggestions", expiredSuggestions.Count);
            }

            return expiredSuggestions.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired suggestions");
            throw;
        }
    }

    /// <summary>
    /// Removes all suggestions from a specific channel for a user.
    /// </summary>
    public async Task<int> RemoveSuggestionsByChannelAsync(string userId, Guid channelId)
    {
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
                foreach (var suggestion in channelSuggestions)
                {
                    suggestion.IsDeleted = true;
                    suggestion.LastModifiedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Removed {Count} suggestions from channel {ChannelId} for user {UserId}",
                    channelSuggestions.Count, channelId, userId);
            }

            return channelSuggestions.Count;
        }
        catch (Exception ex)
        {
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
            return await _context.Suggestions
                .AnyAsync(s => s.UserId == userId &&
                              s.VideoId == videoId &&
                              !s.IsApproved &&
                              !s.IsDenied &&
                              !s.IsDeleted &&
                              s.CreatedAt > DateTime.UtcNow.AddDays(-SuggestionExpiryDays));
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
            var suggestions = await _context.Suggestions
                .Include(s => s.Video)
                    .ThenInclude(v => v.Channel)
                .Where(s => s.UserId == userId && !s.IsDeleted)
                .ToListAsync();

            var analytics = new SuggestionAnalytics
            {
                TotalSuggestionsGenerated = suggestions.Count,
                SuggestionsApproved = suggestions.Count(s => s.IsApproved),
                SuggestionsDenied = suggestions.Count(s => s.IsDenied),
                PendingSuggestions = suggestions.Count(s => !s.IsApproved && !s.IsDenied &&
                                                          s.CreatedAt > DateTime.UtcNow.AddDays(-SuggestionExpiryDays)),
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
                                                                 s.CreatedAt > DateTime.UtcNow.AddDays(-SuggestionExpiryDays)),
                    SuggestionStatus.Approved => query.Where(s => s.IsApproved),
                    SuggestionStatus.Denied => query.Where(s => s.IsDenied),
                    SuggestionStatus.Expired => query.Where(s => !s.IsApproved && !s.IsDenied &&
                                                                 s.CreatedAt <= DateTime.UtcNow.AddDays(-SuggestionExpiryDays)),
                    _ => query
                };
            }

            var skip = (pageNumber - 1) * pageSize;
            return await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
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
                                                                 s.CreatedAt > DateTime.UtcNow.AddDays(-SuggestionExpiryDays)),
                    SuggestionStatus.Approved => query.Where(s => s.IsApproved),
                    SuggestionStatus.Denied => query.Where(s => s.IsDenied),
                    SuggestionStatus.Expired => query.Where(s => !s.IsApproved && !s.IsDenied &&
                                                                 s.CreatedAt <= DateTime.UtcNow.AddDays(-SuggestionExpiryDays)),
                    _ => query
                };
            }

            return await query
                .OrderByDescending(s => s.CreatedAt)
                .Take(100) // Limit search results
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
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last check date for channel {ChannelId}", channelId);
            throw;
        }
    }

    /// <summary>
    /// Gets channels that need to be checked for new videos.
    /// </summary>
    public async Task<List<ChannelCheckInfo>> GetChannelsForUpdateCheckAsync(string userId)
    {
        try
        {
            return await _context.UserChannels
                .Include(uc => uc.Channel)
                .Where(uc => uc.UserId == userId && !uc.IsDeleted)
                .Select(uc => new ChannelCheckInfo
                {
                    Channel = uc.Channel,
                    LastCheckDate = uc.Channel.LastCheckDate,
                    UserRating = _context.Ratings
                        .Where(r => r.UserId == userId && r.ChannelId == uc.ChannelId)
                        .Select(r => (int?)r.Stars)
                        .FirstOrDefault()
                })
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channels for update check for user {UserId}", userId);
            return new List<ChannelCheckInfo>();
        }
    }

    /// <summary>
    /// Bulk creates video entities if they don't already exist.
    /// Enhanced version for YT-010-03 with improved error handling.
    /// </summary>
    public async Task<List<VideoEntity>> EnsureVideosExistAsync(List<VideoInfo> videos)
    {
        try
        {
            if (!videos.Any())
            {
                return new List<VideoEntity>();
            }

            var youTubeVideoIds = videos.Select(v => v.YouTubeVideoId).ToList();

            // Get existing videos
            var existingVideos = await _context.Videos
                .Where(v => youTubeVideoIds.Contains(v.YouTubeVideoId))
                .ToListAsync();

            var existingVideoIds = existingVideos.Select(v => v.YouTubeVideoId).ToHashSet();
            var newVideos = videos.Where(v => !existingVideoIds.Contains(v.YouTubeVideoId)).ToList();

            if (newVideos.Any())
            {
                var videoEntities = new List<VideoEntity>();

                // Process in batches to avoid database timeout
                const int batchSize = 50;
                for (int i = 0; i < newVideos.Count; i += batchSize)
                {
                    var batch = newVideos.Skip(i).Take(batchSize).ToList();

                    foreach (var video in batch)
                    {
                        try
                        {
                            // Find or create the channel entity first
                            var channel = await _context.Channels
                                .FirstOrDefaultAsync(c => c.YouTubeChannelId == video.ChannelId);

                            if (channel == null)
                            {
                                // Create channel if it doesn't exist
                                channel = new ChannelEntity
                                {
                                    YouTubeChannelId = video.ChannelId,
                                    Name = video.ChannelName,
                                    ThumbnailUrl = string.Empty,
                                    VideoCount = 0,
                                    SubscriberCount = 0,
                                    PublishedAt = DateTime.UtcNow
                                };

                                _context.Channels.Add(channel);
                                await _context.SaveChangesAsync(); // Save to get the ID
                            }

                            var videoEntity = new VideoEntity
                            {
                                YouTubeVideoId = video.YouTubeVideoId,
                                Title = video.Title,
                                ChannelId = channel.Id,
                                PublishedAt = video.PublishedAt,
                                ViewCount = video.ViewCount,
                                LikeCount = video.LikeCount,
                                CommentCount = video.CommentCount,
                                Duration = video.Duration,
                                RawTranscript = string.Empty
                            };

                            _context.Videos.Add(videoEntity);
                            videoEntities.Add(videoEntity);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to create video entity for {VideoId}", video.YouTubeVideoId);
                            // Continue processing other videos
                        }
                    }

                    if (videoEntities.Any())
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogDebug("Created batch of {Count} video entities", videoEntities.Count);
                    }
                }

                _logger.LogInformation("Created {Count} new video entities out of {Total} videos",
                    videoEntities.Count, newVideos.Count);

                existingVideos.AddRange(videoEntities);
            }

            return existingVideos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring videos exist");
            throw;
        }
    }

    /// <summary>
    /// Internal helper method to ensure a video exists and return the VideoEntity.
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
                    YouTubeChannelId = video.ChannelId,
                    Name = video.ChannelName,
                    ThumbnailUrl = string.Empty,
                    VideoCount = 0,
                    SubscriberCount = 0,
                    PublishedAt = DateTime.UtcNow
                };

                _context.Channels.Add(channel);
                await _context.SaveChangesAsync(); // Save to get the ID
            }

            // Create new video entity
            var videoEntity = new VideoEntity
            {
                YouTubeVideoId = video.YouTubeVideoId,
                Title = video.Title,
                ChannelId = channel.Id,
                PublishedAt = video.PublishedAt,
                ViewCount = video.ViewCount,
                LikeCount = video.LikeCount,
                CommentCount = video.CommentCount,
                Duration = video.Duration,
                RawTranscript = string.Empty
            };

            _context.Videos.Add(videoEntity);
            await _context.SaveChangesAsync();

            // Load the channel relationship for return
            await _context.Entry(videoEntity)
                .Reference(v => v.Channel)
                .LoadAsync();

            return videoEntity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring video {VideoId} exists internally", video.YouTubeVideoId);
            throw;
        }
    }

    /// <summary>
    /// Extracts the source type from a suggestion reason string for YT-010-03 analytics.
    /// </summary>
    private static string ExtractSourceFromReason(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return "Unknown";

        if (reason.StartsWith("📺"))
            return "Channel Update";
        if (reason.StartsWith("🔍"))
            return "Topic Match";
        if (reason.StartsWith("⭐"))
            return "Channel + Topic";

        return "Other";
    }
}