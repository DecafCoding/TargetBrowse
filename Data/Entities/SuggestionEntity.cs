using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents a video suggestion for a user.
    /// Maximum 100 pending suggestions per user, auto-expires after 30 days.
    /// </summary>
    public class SuggestionEntity : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public Guid VideoId { get; set; }

        [Required]
        [StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Whether the user has approved this suggestion
        /// </summary>
        public bool IsApproved { get; set; } = false;

        /// <summary>
        /// When the user approved this suggestion (null if not approved)
        /// </summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>
        /// Whether the user has explicitly denied this suggestion
        /// </summary>
        public bool IsDenied { get; set; } = false;

        /// <summary>
        /// When the user denied this suggestion (null if not denied)
        /// </summary>
        public DateTime? DeniedAt { get; set; }

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual VideoEntity Video { get; set; } = null!;
    }
}