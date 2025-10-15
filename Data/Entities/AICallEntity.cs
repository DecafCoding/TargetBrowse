using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Tracks every AI API call with full request/response details for auditing and cost tracking.
    /// Links to the prompt template used and optionally to the user who triggered the call.
    /// </summary>
    public class AICallEntity : BaseEntity
    {
        [Required]
        public Guid PromptId { get; set; }

        /// <summary>
        /// Optional user who triggered this AI call. Null for system-initiated calls.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Actual system prompt sent to the API (captured for auditing)
        /// </summary>
        [Required]
        public string ActualSystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Actual user prompt sent after placeholder replacement
        /// </summary>
        [Required]
        public string ActualUserPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Raw response from the AI API
        /// </summary>
        [Required]
        public string Response { get; set; } = string.Empty;

        /// <summary>
        /// Number of tokens sent in the request
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Number of tokens received in the response
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Calculated total cost for this call based on model pricing
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Duration of the API call in milliseconds
        /// </summary>
        public int? DurationMs { get; set; }

        /// <summary>
        /// Whether the API call succeeded
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the call failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        // Navigation properties
        public virtual PromptEntity Prompt { get; set; } = null!;
        public virtual ApplicationUser? User { get; set; }
        public virtual ICollection<SummaryEntity> Summaries { get; set; } = new List<SummaryEntity>();
    }
}