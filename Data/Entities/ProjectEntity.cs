using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents a user's project containing multiple videos for guide and script generation.
    /// Users can organize 3-10 videos into a project and generate AI-powered guides or video scripts.
    /// </summary>
    public class ProjectEntity : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional description of the project's purpose or topic.
        /// </summary>
        [StringLength(2000)]
        public string? Description { get; set; }

        /// <summary>
        /// User-provided guidance for AI guide generation.
        /// Helps focus the guide on specific topics or aspects across the videos.
        /// Example: "Focus on error handling techniques" or "Emphasize best practices"
        /// </summary>
        [StringLength(1000)]
        public string? UserGuidance { get; set; }

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual ICollection<ProjectVideoEntity> ProjectVideos { get; set; } = new List<ProjectVideoEntity>();
        public virtual ProjectGuideEntity? ProjectGuide { get; set; } // One guide per project (1-to-1)
        public virtual ScriptContentEntity? ScriptContent { get; set; } // One script per project (1-to-1)
    }
}
