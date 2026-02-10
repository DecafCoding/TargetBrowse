using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Watch.Data;
using TargetBrowse.Features.Watch.Models;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.Watch.Services
{
    /// <summary>
    /// Service implementation for Watch feature business logic.
    /// </summary>
    public class WatchService : IWatchService
    {
        private readonly IWatchRepository _watchRepository;
        private readonly ILogger<WatchService> _logger;

        public WatchService(
            IWatchRepository watchRepository,
            ILogger<WatchService> logger)
        {
            _watchRepository = watchRepository;
            _logger = logger;
        }

        public async Task<WatchViewModel> GetWatchDataAsync(string youTubeVideoId, string userId)
        {
            var model = new WatchViewModel
            {
                YouTubeVideoId = youTubeVideoId,
                IsLoading = false
            };

            try
            {
                // Retrieve video with channel data
                var video = await _watchRepository.GetVideoByYouTubeIdAsync(youTubeVideoId);

                if (video == null)
                {
                    model.VideoExists = false;
                    model.ErrorMessage = "Video not found in library. Add it to your library first to access all features.";
                    return model;
                }

                // Map video data to view model
                model.VideoId = video.Id;
                model.Title = video.Title;
                model.Description = video.Description ?? string.Empty;
                model.PublishedAt = video.PublishedAt;
                model.PublishedDisplay = FormatHelper.FormatDateDisplay(video.PublishedAt);
                model.ViewCount = video.ViewCount;
                model.RawTranscript = video.RawTranscript;
                model.VideoTypeId = video.VideoTypeId;
                model.VideoTypeName = video.VideoType?.Name;

                // Add " views" suffix for view count
                var viewCountFormatted = FormatHelper.FormatCount((ulong)video.ViewCount);
                model.ViewCountDisplay = string.IsNullOrEmpty(viewCountFormatted) ? "0 views" : $"{viewCountFormatted} views";

                model.Duration = video.Duration;
                model.DurationDisplay = FormatHelper.FormatDuration(video.Duration);
                model.LikeCount = video.LikeCount;
                model.LikeCountDisplay = FormatHelper.FormatCount(video.LikeCount);
                model.CommentCount = video.CommentCount;
                model.CommentCountDisplay = FormatHelper.FormatCount(video.CommentCount);

                // Map channel data if available
                if (video.Channel != null)
                {
                    model.ChannelId = video.Channel.Id;
                    model.ChannelName = video.Channel.Name;
                    model.ChannelThumbnailUrl = video.Channel.ThumbnailUrl ?? string.Empty;
                    model.ChannelYouTubeId = video.Channel.YouTubeChannelId;
                }

                // Build URLs
                model.EmbedUrl = GetYouTubeEmbedUrl(youTubeVideoId);
                model.YouTubeUrl = $"https://www.youtube.com/watch?v={youTubeVideoId}";
                model.ThumbnailUrl = video.ThumbnailUrl ?? $"https://img.youtube.com/vi/{youTubeVideoId}/maxresdefault.jpg";

                // Load user-specific context
                var userVideo = await _watchRepository.GetUserVideoAsync(userId, video.Id);
                if (userVideo != null)
                {
                    model.IsInLibrary = true;
                    model.WatchStatus = userVideo.Status;
                    model.VideoNotes = userVideo.Notes;
                }
                else
                {
                    model.IsInLibrary = false;
                    model.WatchStatus = WatchStatus.NotWatched;
                }

                model.HasTranscript = await _watchRepository.HasTranscriptAsync(video.Id);

                model.HasSummary = await _watchRepository.HasSummaryAsync(video.Id);

                if (model.HasSummary)
                {
                    // Load most recent summary if available
                    var summary = await _watchRepository.GetMostRecentSummaryAsync(video.Id);
                    model.SummaryContent = summary?.Content;
                }

                // Load user rating if exists
                var rating = await _watchRepository.GetUserVideoRatingAsync(userId, video.Id);
                if (rating != null)
                {
                    model.UserRating = rating.Stars;
                    model.RatingNotes = rating.Notes;
                }

                // Load available video types for the dropdown
                model.AvailableVideoTypes = await _watchRepository.GetAllVideoTypesAsync();

                _logger.LogInformation("Successfully loaded watch data for video {VideoId}", youTubeVideoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading watch data for video {VideoId}", youTubeVideoId);
                model.ErrorMessage = "An error occurred while loading the video. Please try again.";
                model.IsLoading = false;
            }

            return model;
        }

        public async Task<bool> VideoExistsAsync(string youTubeVideoId)
        {
            try
            {
                var video = await _watchRepository.GetVideoByYouTubeIdAsync(youTubeVideoId);
                return video != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if video exists: {VideoId}", youTubeVideoId);
                return false;
            }
        }

        public string GetYouTubeEmbedUrl(string youTubeVideoId)
        {
            // Build embed URL with autoplay disabled and modest branding
            return $"https://www.youtube.com/embed/{youTubeVideoId}?rel=0&modestbranding=1&autoplay=0";
        }

        public async Task<bool> UpdateVideoTypeAsync(Guid videoId, Guid? videoTypeId)
        {
            try
            {
                var result = await _watchRepository.UpdateVideoTypeAsync(videoId, videoTypeId);

                if (result)
                {
                    _logger.LogInformation("Successfully updated video type for video {VideoId} to {VideoTypeId}", videoId, videoTypeId);
                }
                else
                {
                    _logger.LogWarning("Failed to update video type for video {VideoId}", videoId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating video type for video {VideoId}", videoId);
                return false;
            }
        }

        public async Task<bool> UpdateVideoNotesAsync(string userId, Guid videoId, string? notes)
        {
            try
            {
                var result = await _watchRepository.UpdateVideoNotesAsync(userId, videoId, notes);

                if (result)
                {
                    _logger.LogInformation("Successfully updated notes for video {VideoId}", videoId);
                }
                else
                {
                    _logger.LogWarning("Failed to update notes for video {VideoId}", videoId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating notes for video {VideoId}", videoId);
                return false;
            }
        }
    }
}