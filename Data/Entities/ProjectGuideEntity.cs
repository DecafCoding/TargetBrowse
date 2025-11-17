using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents an AI-generated guide for a project.
    /// Synthesizes information from multiple video summaries into a cohesive tutorial/guide.
    /// One guide per project (1-to-1 relationship).
    /// </summary>
    public class ProjectGuideEntity : BaseEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        /// <summary>
        /// HTML content of the generated guide.
        /// Contains AI-generated synthesis of all video summaries in the project.
        /// </summary>
        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Links to the AI call that generated this guide.
        /// Provides full audit trail including prompt used, tokens, and cost.
        /// </summary>
        public Guid? AICallId { get; set; }

        /// <summary>
        /// Snapshot of the UserGuidance at the time of generation.
        /// Used to detect if guidance has changed and regeneration is needed.
        /// </summary>
        [StringLength(1000)]
        public string? UserGuidanceSnapshot { get; set; }

        /// <summary>
        /// When the guide was generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Snapshot of the number of videos in the project at generation time.
        /// Used to detect if videos were added/removed and regeneration is needed.
        /// </summary>
        public int VideoCount { get; set; }

        // Navigation properties
        public virtual ProjectEntity Project { get; set; } = null!;
        public virtual AICallEntity? AICall { get; set; }
    }
}
