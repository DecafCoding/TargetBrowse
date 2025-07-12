using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Tracks when users REQUEST summary generation for daily limit enforcement (10 generations per day).
    /// Only records when a user requests a NEW summary to be generated, not when viewing existing summaries.
    /// Users can view existing summaries unlimited times without any tracking.
    /// </summary>
    public class SummaryGenerationRequestEntity : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public Guid VideoId { get; set; }

        /// <summary>
        /// When the user requested this summary to be generated
        /// </summary>
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the generation was successful (true) or failed (false)
        /// Failed generations still count toward daily limit to prevent abuse
        /// </summary>
        public bool WasSuccessful { get; set; } = true;

        /// <summary>
        /// Reference to the summary that was created (null if generation failed)
        /// </summary>
        public Guid? SummaryId { get; set; }

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual VideoEntity Video { get; set; } = null!;
        public virtual SummaryEntity? Summary { get; set; }
    }
}