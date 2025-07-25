using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Topics.Models
{
    /// <summary>
    /// Data transfer object for topic information.
    /// Used for UI binding and validation.
    /// </summary>
    public class TopicDto
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Topic name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Topic name must be between 1 and 100 characters")]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// UI state property for inline editing functionality
        /// </summary>
        public bool IsEditing { get; set; } = false;

        /// <summary>
        /// Creates a new TopicDto from a TopicEntity
        /// </summary>
        public static TopicDto FromEntity(Data.Entities.TopicEntity entity)
        {
            return new TopicDto
            {
                Id = entity.Id,
                Name = entity.Name,
                CreatedAt = entity.CreatedAt
            };
        }
    }
}