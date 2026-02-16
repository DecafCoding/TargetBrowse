using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Services.DataServices
{
    /// <summary>
    /// Data access service implementation for summary-related operations.
    /// Handles database operations for video summaries.
    /// </summary>
    public class SummaryDataService : ISummaryDataService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SummaryDataService> _logger;

        public SummaryDataService(ApplicationDbContext context, ILogger<SummaryDataService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new summary for a video.
        /// Note: VideoId has a unique index, so duplicate summaries for the same video will fail.
        /// </summary>
        public async Task<SummaryEntity> CreateSummaryAsync(Guid videoId, string content, string summary, Guid? aiCallId = null)
        {
            try
            {
                // Check for soft-deleted row (unique index includes deleted rows)
                var existing = await _context.Summaries
                    .FirstOrDefaultAsync(s => s.VideoId == videoId && s.IsDeleted);

                SummaryEntity summaryEntity;

                if (existing != null)
                {
                    // Re-use the soft-deleted row
                    existing.Content = content;
                    existing.Summary = summary;
                    existing.AICallId = aiCallId;
                    existing.GenerationCount += 1;
                    existing.IsDeleted = false;
                    summaryEntity = existing;
                }
                else
                {
                    summaryEntity = new SummaryEntity
                    {
                        VideoId = videoId,
                        Content = content,
                        Summary = summary,
                        AICallId = aiCallId,
                        GenerationCount = 1
                    };
                    _context.Summaries.Add(summaryEntity);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Created summary {SummaryId} for video {VideoId}. Content length: {ContentLength}, Summary length: {SummaryLength}",
                    summaryEntity.Id,
                    videoId,
                    content.Length,
                    summary.Length);

                return summaryEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error creating summary for video {VideoId}: {Message}",
                    videoId,
                    ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets a summary by video ID.
        /// Returns null if no summary exists for the video.
        /// </summary>
        public async Task<SummaryEntity?> GetSummaryByVideoIdAsync(Guid videoId)
        {
            try
            {
                return await _context.Summaries
                    .FirstOrDefaultAsync(s => s.VideoId == videoId && !s.IsDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving summary for video {VideoId}: {Message}",
                    videoId,
                    ex.Message);
                throw;
            }
        }
    }
}
