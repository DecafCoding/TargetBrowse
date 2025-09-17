using Microsoft.EntityFrameworkCore;
using System.Linq;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Services.DataServices;

/// <summary>
/// Implementation of shared suggestion data access service.
/// Handles raw suggestion database operations used across multiple services.
/// Contains no business logic - pure data operations only.
/// </summary>
public class SuggestionDataService : ISuggestionDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SuggestionDataService> _logger;

    // Constants for data consistency
    private const int SUGGESTION_EXPIRY_DAYS = 30;
    private const int MAX_PENDING_SUGGESTIONS = 1000;
    private const int BATCH_SIZE = 50;

    public SuggestionDataService(ApplicationDbContext context, ILogger<SuggestionDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a video already has a pending suggestion for the user.
    /// Used by onboarding services to avoid creating duplicate suggestions.
    /// </summary>
    public async Task<bool> HasPendingSuggestionForVideoAsync(string userId, Guid videoId)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-SUGGESTION_EXPIRY_DAYS);

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
            _logger.LogError(ex, "Error checking pending suggestion for video {VideoId} and user {UserId}",
                videoId, userId);
            return false; // Default to false to allow suggestion creation
        }
    }

    /// <summary>
    /// Gets the count of pending suggestions for a user.
    /// Used for limit enforcement across different suggestion creation contexts.
    /// </summary>
    public async Task<int> GetPendingSuggestionsCountAsync(string userId)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-SUGGESTION_EXPIRY_DAYS);

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
    /// Creates suggestion entities for topic onboarding with proper topic relationships.
    /// Creates both SuggestionEntity and SuggestionTopicEntity records for data integrity.
    /// </summary>
    public async Task<List<SuggestionEntity>> CreateTopicOnboardingSuggestionsAsync(
        string userId,
        List<VideoEntity> videoEntities,
        Guid topicId,
        string topicName)
    {
        var suggestions = new List<SuggestionEntity>();

        if (!videoEntities.Any())
        {
            _logger.LogInformation("No video entities provided for topic onboarding suggestions");
            return suggestions;
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
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

            // Save suggestions first to get their IDs
            if (suggestions.Any())
            {
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
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Created {SuggestionCount} topic onboarding suggestions for topic {TopicId}",
                suggestions.Count, topicId);

            return suggestions;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating topic onboarding suggestions for topic {TopicId} and user {UserId}",
                topicId, userId);
            throw;
        }
    }

    /// <summary>
    /// Creates suggestion entities for channel onboarding.
    /// Creates suggestion entities for initial channel-based video suggestions.
    /// </summary>
    public async Task<List<SuggestionEntity>> CreateChannelOnboardingSuggestionsAsync(
        string userId,
        List<VideoEntity> videoEntities,
        string channelName)
    {
        var suggestions = new List<SuggestionEntity>();

        if (!videoEntities.Any())
        {
            _logger.LogInformation("No video entities provided for channel onboarding suggestions");
            return suggestions;
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            foreach (var videoEntity in videoEntities)
            {
                // Check for existing suggestions to avoid duplicates
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
                    Reason = $"📺 New Channel: {channelName}",
                    IsApproved = false,
                    IsDenied = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Suggestions.Add(suggestion);
                suggestions.Add(suggestion);
            }

            if (suggestions.Any())
            {
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Created {SuggestionCount} channel onboarding suggestions for channel {ChannelName}",
                suggestions.Count, channelName);

            return suggestions;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error creating channel onboarding suggestions for channel {ChannelName} and user {UserId}",
                channelName, userId);
            throw;
        }
    }

    /// <summary>
    /// Bulk creates video entities if they don't already exist.
    /// Ensures video entities are available for suggestion creation.
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

            // Update existing videos with missing metadata
            int updatedCount = await UpdateExistingVideoMetadata(videos, existingVideos);

            if (newVideos.Any())
            {
                var videoEntities = await CreateNewVideoEntities(newVideos);
                existingVideos.AddRange(videoEntities);

                _logger.LogInformation("Created {Count} new video entities, updated {UpdatedCount} existing videos",
                    videoEntities.Count, updatedCount);
            }

            await transaction.CommitAsync();
            return existingVideos;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error ensuring videos exist");
            throw;
        }
    }

    /// <summary>
    /// Checks if a user can create more suggestions based on current limits.
    /// Validates against maximum pending suggestions limit.
    /// </summary>
    public async Task<bool> CanUserCreateSuggestionsAsync(string userId)
    {
        try
        {
            var pendingCount = await GetPendingSuggestionsCountAsync(userId);
            return pendingCount < MAX_PENDING_SUGGESTIONS;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} can create suggestions", userId);
            return false; // Fail safe - don't allow creation if we can't verify limits
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Updates existing video entities with new metadata if available.
    /// </summary>
    private async Task<int> UpdateExistingVideoMetadata(List<VideoInfo> videos, List<VideoEntity> existingVideos)
    {
        var updatedCount = 0;
        var videosToUpdate = videos.Where(v => existingVideos.Any(ev => ev.YouTubeVideoId == v.YouTubeVideoId)).ToList();

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

        return updatedCount;
    }

    /// <summary>
    /// Creates new video entities for videos that don't exist in database.
    /// </summary>
    private async Task<List<VideoEntity>> CreateNewVideoEntities(List<VideoInfo> newVideos)
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
                    var videoEntity = await CreateVideoEntityFromInfo(video);
                    videoEntities.Add(videoEntity);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create video entity for {VideoId}", video.YouTubeVideoId);
                    // Continue processing other videos
                }
            }
        }

        return videoEntities;
    }

    /// <summary>
    /// Creates a single video entity from VideoInfo, ensuring channel exists.
    /// </summary>
    private async Task<VideoEntity> CreateVideoEntityFromInfo(VideoInfo video)
    {
        // Find or create the channel entity first
        var channel = await _context.Channels
            .FirstOrDefaultAsync(c => c.YouTubeChannelId == video.ChannelId);

        if (channel == null)
        {
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
        }

        // Create new video entity
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
            ThumbnailUrl = GetOptimalThumbnailUrl(video.ThumbnailUrl),
            Description = TruncateDescription(video.Description),
            RawTranscript = string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _context.Videos.Add(videoEntity);
        await _context.SaveChangesAsync();

        // Set the channel relationship for return
        videoEntity.Channel = channel;

        return videoEntity;
    }

    /// <summary>
    /// Truncates description to fit database constraints.
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
    /// Selects the optimal thumbnail URL from YouTube API response.
    /// </summary>
    private static string GetOptimalThumbnailUrl(string? thumbnailUrl)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
            return string.Empty;

        // If it's already a direct URL, use it as-is
        if (thumbnailUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return thumbnailUrl;

        return thumbnailUrl;
    }

    #endregion
}