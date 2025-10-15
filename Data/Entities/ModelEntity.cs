using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents an AI model with its provider and cost structure.
    /// Used to track available models and calculate API costs.
    /// </summary>
    public class ModelEntity : BaseEntity
    {
        /// <summary>
        /// Model identifier (e.g., "gpt-4o-mini", "claude-sonnet-4")
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Provider name (e.g., "OpenAI", "Anthropic")
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Cost per 1,000 input tokens in USD
        /// </summary>
        [Required]
        public decimal CostPer1kInputTokens { get; set; }

        /// <summary>
        /// Cost per 1,000 output tokens in USD
        /// </summary>
        [Required]
        public decimal CostPer1kOutputTokens { get; set; }

        /// <summary>
        /// Whether this model is currently active and available for use
        /// </summary>
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<PromptEntity> Prompts { get; set; } = new List<PromptEntity>();
    }
}
