using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Junction table linking suggestions to topics for many-to-many relationship.
    /// Allows tracking which topics influenced each suggestion.
    /// </summary>
    public class SuggestionTopicEntity : BaseEntity
    {
        [Required]
        public Guid SuggestionId { get; set; }

        [Required]
        public Guid TopicId { get; set; }

        // Navigation properties
        public virtual SuggestionEntity Suggestion { get; set; } = null!;
        public virtual TopicEntity Topic { get; set; } = null!;
    }
}