using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents an AI-generated summary of a video shared across all users.
    /// One summary per video to avoid duplication and improve efficiency.
    /// User daily limits (10 per day) are tracked separately through usage logs.
    /// </summary>
    public class SummaryEntity : BaseEntity
    {
        [Required]
        public Guid VideoId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Short summary of the video content (max 1000 characters).
        /// </summary>
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// Links to the AI call that generated this summary.
        /// Provides full audit trail including prompt used, tokens, and cost.
        /// </summary>
        public Guid? AICallId { get; set; }

        /// <summary>
        /// Number of times this summary has been requested/generated (for analytics)
        /// </summary>
        public int GenerationCount { get; set; } = 1;

        // Navigation properties
        public virtual VideoEntity Video { get; set; } = null!;
        public virtual AICallEntity? AICall { get; set; }
        public virtual ICollection<SummaryGenerationRequestEntity> GenerationRequests { get; set; } = new List<SummaryGenerationRequestEntity>();
    }
}