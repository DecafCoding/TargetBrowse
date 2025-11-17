using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Junction table linking videos to projects.
    /// A video can belong to multiple projects, and a project can contain multiple videos.
    /// Maintains the order in which videos were added to the project.
    /// </summary>
    public class ProjectVideoEntity : BaseEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        public Guid VideoId { get; set; }

        /// <summary>
        /// Sequence order of the video in the project.
        /// Used to maintain the order videos were added for guide generation.
        /// Auto-increments from max(Order) + 1 when adding new videos.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// When the video was added to this project.
        /// </summary>
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual ProjectEntity Project { get; set; } = null!;
        public virtual VideoEntity Video { get; set; } = null!;
    }
}
