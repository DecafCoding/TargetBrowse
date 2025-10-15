using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents a reusable AI prompt template with configuration.
    /// Links to a specific model and stores all parameters needed for API calls.
    /// </summary>
    public class PromptEntity : BaseEntity
    {
        /// <summary>
        /// Friendly name for the prompt (e.g., "VideoSummaryPrompt", "ThumbnailAnalysisPrompt")
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Version identifier (e.g., "1.0", "2.1") for tracking prompt evolution
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// System/instruction prompt sent to the AI model
        /// </summary>
        [Required]
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// User prompt template with placeholders (e.g., "Summarize this transcript: {transcript}")
        /// Placeholders are replaced at runtime with actual values
        /// </summary>
        [Required]
        public string UserPromptTemplate { get; set; } = string.Empty;

        /// <summary>
        /// Model temperature setting (0.0 to 2.0, typically 0.0-1.0 for most use cases)
        /// Controls randomness in responses - lower is more deterministic
        /// </summary>
        public decimal? Temperature { get; set; }

        /// <summary>
        /// Maximum tokens to generate in response
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// Top-p sampling value (0.0 to 1.0)
        /// Alternative to temperature for controlling randomness
        /// </summary>
        public decimal? TopP { get; set; }

        /// <summary>
        /// Whether this prompt is currently active and available for use
        /// </summary>
        public bool IsActive { get; set; } = true;

        [Required]
        public Guid ModelId { get; set; }

        // Navigation properties
        public virtual ModelEntity Model { get; set; } = null!;
        public virtual ICollection<AICallEntity> AICalls { get; set; } = new List<AICallEntity>();
    }
}