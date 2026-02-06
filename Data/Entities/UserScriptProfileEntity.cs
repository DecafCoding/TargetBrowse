using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents a user's preferences for AI-generated video scripts.
    /// Stores voice, tone, and style preferences applied across all script generations.
    /// One profile per user.
    /// </summary>
    public class UserScriptProfileEntity : BaseEntity
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Script tone: Casual, Professional, Enthusiastic, Technical
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Tone { get; set; } = "Casual";

        /// <summary>
        /// Script pacing: Fast, Moderate, Deliberate
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Pacing { get; set; } = "Moderate";

        /// <summary>
        /// Complexity level: Beginner, Intermediate, Advanced
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Complexity { get; set; } = "Intermediate";

        /// <summary>
        /// Optional custom instructions for script generation.
        /// Examples: "Always use analogies", "Include real-world examples", "Avoid jargon"
        /// </summary>
        [StringLength(2000)]
        public string? CustomInstructions { get; set; }

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;
    }
}
