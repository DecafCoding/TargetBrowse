using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents a user-defined topic of interest for content discovery.
    /// Maximum 10 topics per user enforced at business logic level.
    /// </summary>
    public class TopicEntity : BaseEntity
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;
    }
}