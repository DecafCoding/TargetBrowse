using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents a user's relationship with a video (saved to library, watched status, etc.)
    /// </summary>
    public class UserVideoEntity : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public Guid VideoId { get; set; }

        /// <summary>
        /// When the user added this video to their library
        /// </summary>
        public DateTime AddedToLibraryAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User's watch status for this video
        /// </summary>
        public WatchStatus Status { get; set; } = WatchStatus.NotWatched;

        /// <summary>
        /// When the user marked the video as watched or skipped
        /// </summary>
        public DateTime? StatusChangedAt { get; set; }

        /// <summary>
        /// Optional notes about why the video was added to the library.
        /// Includes context like topic relevance, channel source, or user-provided notes.
        /// </summary>
        public string? Notes { get; set; }

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual VideoEntity Video { get; set; } = null!;
    }

    public enum WatchStatus
    {
        NotWatched = 0,
        Watched = 1,
        Skipped = 2
    }
}