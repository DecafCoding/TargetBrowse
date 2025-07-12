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
        [StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PromptVersion { get; set; } = string.Empty;

        /// <summary>
        /// OpenAI model used for generation (e.g., "gpt-4o-mini")
        /// </summary>
        [StringLength(50)]
        public string? ModelUsed { get; set; }

        /// <summary>
        /// Token count for the summary generation (for cost tracking)
        /// </summary>
        public int? TokensUsed { get; set; }

        /// <summary>
        /// Number of times this summary has been requested/generated (for analytics)
        /// </summary>
        public int GenerationCount { get; set; } = 1;

        // Navigation properties
        public virtual VideoEntity Video { get; set; } = null!;
        public virtual ICollection<SummaryGenerationRequestEntity> GenerationRequests { get; set; } = new List<SummaryGenerationRequestEntity>();
    }
}