using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents a YouTube video with metadata and transcript.
    /// Shared across users but saved/rated individually.
    /// </summary>
    public class VideoEntity : BaseEntity
    {
        [Required]
        [StringLength(20)]
        public string YouTubeVideoId { get; set; } = string.Empty;

        [Required]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public Guid ChannelId { get; set; }

        public DateTime PublishedAt { get; set; }

        public int ViewCount { get; set; }

        public int LikeCount { get; set; }

        public int CommentCount { get; set; }

        /// <summary>
        /// Video duration in seconds
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Video thumbnail URL for display.
        /// Stored to reduce API calls and improve performance.
        /// </summary>
        [StringLength(500)]
        public string? ThumbnailUrl { get; set; }

        /// <summary>
        /// Video description from YouTube API.
        /// Stored for search and display purposes.
        /// </summary>
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Raw transcript text from Apify service
        /// </summary>
        public string RawTranscript { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to VideoType for content classification.
        /// Optional - videos can exist without a type initially.
        /// </summary>
        public Guid? VideoTypeId { get; set; }

        // Navigation properties
        public virtual ChannelEntity Channel { get; set; } = null!;
        public virtual ICollection<RatingEntity> Ratings { get; set; } = new List<RatingEntity>();
        public virtual SummaryEntity? Summary { get; set; } // One shared summary per video
        public virtual ICollection<SummaryGenerationRequestEntity> SummaryGenerationRequests { get; set; } = new List<SummaryGenerationRequestEntity>();
        public virtual ICollection<SuggestionEntity> Suggestions { get; set; } = new List<SuggestionEntity>();
        public virtual ICollection<UserVideoEntity> UserVideos { get; set; } = new List<UserVideoEntity>();
        public virtual ICollection<ProjectVideoEntity> ProjectVideos { get; set; } = new List<ProjectVideoEntity>();

        /// <summary>
        /// Video type classification (Tutorial, Podcast, Vlog, etc.).
        /// Nullable - videos may not have a type assigned.
        /// </summary>
        public virtual VideoTypeEntity? VideoType { get; set; }
    }
}