using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents a YouTube channel with metadata.
    /// Shared across users but tracked individually through UserChannelEntity.
    /// </summary>
    public class ChannelEntity : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string YouTubeChannelId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ThumbnailUrl { get; set; }

        public ulong? VideoCount { get; set; }

        public ulong? SubscriberCount { get; set; }

        public DateTime PublishedAt { get; set; }

        /// <summary>
        /// Last time this channel was checked for new videos
        /// </summary>
        public DateTime? LastCheckDate { get; set; }

        // Navigation properties
        public virtual ICollection<UserChannelEntity> UserChannels { get; set; } = new List<UserChannelEntity>();
        public virtual ICollection<VideoEntity> Videos { get; set; } = new List<VideoEntity>();
        public virtual ICollection<RatingEntity> Ratings { get; set; } = new List<RatingEntity>();
    }
}