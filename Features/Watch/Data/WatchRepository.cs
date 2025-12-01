using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Watch.Data
{
    /// <summary>
    /// Repository implementation for Watch feature data operations.
    /// </summary>
    public class WatchRepository : IWatchRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WatchRepository> _logger;

        public WatchRepository(
            ApplicationDbContext context,
            ILogger<WatchRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<VideoEntity?> GetVideoByYouTubeIdAsync(string youTubeVideoId)
        {
            try
            {
                // Include channel and video type data for display
                return await _context.Videos
                    .Include(v => v.Channel)
                    .Include(v => v.VideoType)
                    .Where(v => !v.IsDeleted)
                    .FirstOrDefaultAsync(v => v.YouTubeVideoId == youTubeVideoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving video with YouTube ID {YouTubeVideoId}", youTubeVideoId);
                return null;
            }
        }

        public async Task<RatingEntity?> GetUserVideoRatingAsync(string userId, Guid videoId)
        {
            try
            {
                return await _context.Ratings
                    .Where(r => !r.IsDeleted)
                    .FirstOrDefaultAsync(r =>
                        r.UserId == userId &&
                        r.VideoId == videoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user rating for video {VideoId}", videoId);
                return null;
            }
        }

        public async Task<UserVideoEntity?> GetUserVideoAsync(string userId, Guid videoId)
        {
            try
            {
                return await _context.UserVideos
                    .Where(uv => !uv.IsDeleted)
                    .FirstOrDefaultAsync(uv =>
                        uv.UserId == userId &&
                        uv.VideoId == videoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user video relationship for video {VideoId}", videoId);
                return null;
            }
        }

        public async Task<bool> IsVideoInUserLibraryAsync(string userId, Guid videoId)
        {
            try
            {
                // Check if UserVideo relationship exists
                return await _context.UserVideos
                    .Where(uv => !uv.IsDeleted)
                    .AnyAsync(uv =>
                        uv.UserId == userId &&
                        uv.VideoId == videoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if video {VideoId} is in user library", videoId);
                return false;
            }
        }

        public async Task<bool> IsVideoWatchedAsync(string userId, Guid videoId)
        {
            try
            {
                // Check if video has WatchStatus.Watched in UserVideos table
                var userVideo = await GetUserVideoAsync(userId, videoId);
                return userVideo?.Status == WatchStatus.Watched;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking watch status for video {VideoId}", videoId);
                return false;
            }
        }

        public async Task<bool> HasTranscriptAsync(Guid videoId)
        {
            try
            {
                var video = await _context.Videos
                    .Where(v => !v.IsDeleted)
                    .Select(v => new { v.Id, HasTranscript = !string.IsNullOrEmpty(v.RawTranscript) })
                    .FirstOrDefaultAsync(v => v.Id == videoId);

                return video?.HasTranscript ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking transcript availability for video {VideoId}", videoId);
                return false;
            }
        }

        public async Task<bool> HasSummaryAsync(Guid videoId)
        {
            try
            {
                // Check if a summary exists for this video (one shared summary per video)
                return await _context.Summaries
                    .Where(s => !s.IsDeleted)
                    .AnyAsync(s => s.VideoId == videoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking summary availability for video {VideoId}", videoId);
                return false;
            }
        }

        public async Task<bool> UpdateVideoTranscriptAsync(Guid videoId, string transcript)
        {
            try
            {
                var video = await _context.Videos
                    .Where(v => !v.IsDeleted)
                    .FirstOrDefaultAsync(v => v.Id == videoId);

                if (video == null)
                {
                    _logger.LogWarning("Video {VideoId} not found for transcript update", videoId);
                    return false;
                }

                // Update the RawTranscript field
                video.RawTranscript = transcript;
                video.LastModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated transcript for video {VideoId}", videoId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transcript for video {VideoId}", videoId);
                return false;
            }
        }

        public async Task<SummaryEntity?> GetMostRecentSummaryAsync(Guid videoId)
        {
            try
            {
                // Get the most recent summary for this video (sorted by CreatedAt descending)
                return await _context.Summaries
                    .Where(s => !s.IsDeleted && s.VideoId == videoId)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving summary for video {VideoId}", videoId);
                return null;
            }
        }

        public async Task<List<VideoTypeEntity>> GetAllVideoTypesAsync()
        {
            try
            {
                return await _context.VideoTypes
                    .Where(vt => !vt.IsDeleted)
                    .OrderBy(vt => vt.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving video types");
                return new List<VideoTypeEntity>();
            }
        }

        public async Task<bool> UpdateVideoTypeAsync(Guid videoId, Guid? videoTypeId)
        {
            try
            {
                var video = await _context.Videos
                    .Where(v => !v.IsDeleted)
                    .FirstOrDefaultAsync(v => v.Id == videoId);

                if (video == null)
                {
                    _logger.LogWarning("Video {VideoId} not found for video type update", videoId);
                    return false;
                }

                // Update the VideoTypeId field
                video.VideoTypeId = videoTypeId;
                video.LastModifiedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully updated video type for video {VideoId} to {VideoTypeId}", videoId, videoTypeId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating video type for video {VideoId}", videoId);
                return false;
            }
        }
    }
}