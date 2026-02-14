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

        /// <summary>
        /// Number of days between checks for new content.
        /// Maps to: 3 = Every 3 days, 5 = Every 5 days, 7 = Weekly, 14 = Bi-Weekly
        /// </summary>
        public int CheckDays { get; set; } = 7; // Default to Weekly

        /// <summary>
        /// Last date this topic was checked for new content.
        /// Null indicates never checked.
        /// </summary>
        public DateTime? LastCheckedDate { get; set; }

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;

        public virtual ICollection<SuggestionTopicEntity> SuggestionTopics { get; set; } = new List<SuggestionTopicEntity>();
        public virtual ICollection<TopicVideoEntity> TopicVideos { get; set; } = new List<TopicVideoEntity>();
    }
}