using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.Watch.Models
{
    /// <summary>
    /// View model containing all data needed for the Watch page display.
    /// Consolidates video, channel, and user-specific information.
    /// </summary>
    public class WatchViewModel
    {
        // Video Information
        /// <summary>
        /// YouTube's unique identifier for the video
        /// </summary>
        public string YouTubeVideoId { get; set; } = string.Empty;

        /// <summary>
        /// Database ID for the video
        /// </summary>
        public Guid VideoId { get; set; }

        /// <summary>
        /// Video title as retrieved from YouTube
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Full video description from YouTube
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// When the video was published on YouTube
        /// </summary>
        public DateTime PublishedAt { get; set; }

        /// <summary>
        /// Formatted display string for published date (e.g., "2 days ago")
        /// </summary>
        public string PublishedDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Total view count from YouTube
        /// </summary>
        public long ViewCount { get; set; }

        /// <summary>
        /// Formatted view count for display (e.g., "1.2M views")
        /// </summary>
        public string ViewCountDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Video duration in seconds
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Formatted duration for display (e.g., "10:45")
        /// </summary>
        public string DurationDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Like count from YouTube (if available)
        /// </summary>
        public int LikeCount { get; set; }

        /// <summary>
        /// Formatted like count for display
        /// </summary>
        public string LikeCountDisplay { get; set; } = string.Empty;

        /// <summary>
        /// Comment count from YouTube (if available)
        /// </summary>
        public int CommentCount { get; set; }

        /// <summary>
        /// Formatted comment count for display
        /// </summary>
        public string CommentCountDisplay { get; set; } = string.Empty;

        // Channel Information
        /// <summary>
        /// Database ID for the channel
        /// </summary>
        public Guid ChannelId { get; set; }

        /// <summary>
        /// Channel name as displayed on YouTube
        /// </summary>
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>
        /// URL to channel's thumbnail image
        /// </summary>
        public string ChannelThumbnailUrl { get; set; } = string.Empty;

        /// <summary>
        /// YouTube's unique identifier for the channel
        /// </summary>
        public string ChannelYouTubeId { get; set; } = string.Empty;

        // User Context
        /// <summary>
        /// Whether the current user has this video in their library
        /// </summary>
        public bool IsInLibrary { get; set; }

        /// <summary>
        /// User's watch status for this video (NotWatched, Watched, Skipped)
        /// </summary>
        public WatchStatus WatchStatus { get; set; } = WatchStatus.NotWatched;

        /// <summary>
        /// User's rating for this video (1-5 stars, null if not rated)
        /// </summary>
        public int? UserRating { get; set; }

        /// <summary>
        /// User's rating notes/feedback for this video
        /// </summary>
        public string? RatingNotes { get; set; }

        /// <summary>
        /// Whether a transcript is available for this video
        /// </summary>
        public bool HasTranscript { get; set; }

        /// <summary>
        /// Whether the user has generated a summary for this video
        /// </summary>
        public bool HasSummary { get; set; }

        // URLs
        /// <summary>
        /// YouTube embed URL for iframe player
        /// </summary>
        public string EmbedUrl { get; set; } = string.Empty;

        /// <summary>
        /// Direct YouTube URL for external viewing
        /// </summary>
        public string YouTubeUrl { get; set; } = string.Empty;

        /// <summary>
        /// Video thumbnail URL for fallback display
        /// </summary>
        public string ThumbnailUrl { get; set; } = string.Empty;

        // UI State
        /// <summary>
        /// Whether the page is currently loading data
        /// </summary>
        public bool IsLoading { get; set; } = true;

        /// <summary>
        /// Error message to display if loading fails
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Whether the video was found in the database
        /// </summary>
        public bool VideoExists { get; set; } = true;

        /// <summary>
        /// Raw transcript text from the video (if available)
        /// </summary>
        public string? RawTranscript { get; set; }

        /// <summary>
        /// AI-generated summary content (if available)
        /// </summary>
        public string? SummaryContent { get; set; }

        // Helper Methods
        /// <summary>
        /// Gets the appropriate thumbnail URL with fallback
        /// </summary>
        public string GetThumbnailUrl()
        {
            return ThumbnailFormatter.GetVideoThumbnailUrl(ThumbnailUrl, YouTubeVideoId, ThumbnailQuality.MaxResDefault);
        }

        /// <summary>
        /// Gets a truncated description for preview (first 500 characters)
        /// </summary>
        public string GetShortDescription()
        {
            return TextFormatter.Truncate(Description, 500);
        }
    }
}