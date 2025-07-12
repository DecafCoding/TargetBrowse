using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Many-to-many relationship between users and channels they track.
    /// Maximum 50 tracked channels per user enforced at business logic level.
    /// </summary>
    public class UserChannelEntity : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public Guid ChannelId { get; set; }

        /// <summary>
        /// When the user started tracking this channel
        /// </summary>
        public DateTime TrackedSince { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual ChannelEntity Channel { get; set; } = null!;
    }
}