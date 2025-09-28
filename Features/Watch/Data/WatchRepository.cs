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

        /// <inheritdoc/>
        public async Task<VideoEntity?> GetVideoByYouTubeIdAsync(string youTubeVideoId)
        {
            try
            {
                // Include channel data for display
                return await _context.Videos
                    .Include(v => v.Channel)
                    .Where(v => !v.IsDeleted)
                    .FirstOrDefaultAsync(v => v.YouTubeVideoId == youTubeVideoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving video with YouTube ID {YouTubeVideoId}", youTubeVideoId);
                return null;
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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
    }
}