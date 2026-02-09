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
        /// Script tone: Insider Conversational, Informed Skeptic, Enthusiastic Guide, Authoritative Expert
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Tone { get; set; } = "Insider Conversational";

        /// <summary>
        /// Script pacing: Build-Release, Rapid-Fire, Steady Cruise, Deliberate Deep-Dive
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Pacing { get; set; } = "Build-Release";

        /// <summary>
        /// Complexity level: Layered Progressive, Assumes Competence, Ground-Up, Technical Immersion
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Complexity { get; set; } = "Layered Progressive";

        /// <summary>
        /// Structure style: Enumerated Scaffolding, Narrative Flow, Comparative Framework, Preview-Deliver-Recap
        /// </summary>
        [Required]
        [StringLength(50)]
        public string StructureStyle { get; set; } = "Enumerated Scaffolding";

        /// <summary>
        /// Hook strategy: Insider Secret, Provocative Question, Bold Claim, Shared Frustration
        /// </summary>
        [Required]
        [StringLength(50)]
        public string HookStrategy { get; set; } = "Insider Secret";

        /// <summary>
        /// Audience relationship: Insider-Outsider, Collaborative Partner, Mentor-Student, Peer-to-Peer
        /// </summary>
        [Required]
        [StringLength(50)]
        public string AudienceRelationship { get; set; } = "Insider-Outsider";

        /// <summary>
        /// Information density: High Density, Balanced, Focused Essentials, Story-Rich
        /// </summary>
        [Required]
        [StringLength(50)]
        public string InformationDensity { get; set; } = "High Density";

        /// <summary>
        /// Rhetorical style: Extended Metaphors, Direct and Punchy, Socratic, Parenthetical Asides
        /// </summary>
        [Required]
        [StringLength(50)]
        public string RhetoricalStyle { get; set; } = "Extended Metaphors";

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
