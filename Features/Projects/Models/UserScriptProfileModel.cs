using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// View model for user script profile settings.
    /// Used for creating and updating user preferences for script generation.
    /// </summary>
    public class UserScriptProfileModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Tone is required")]
        [StringLength(50)]
        public string Tone { get; set; } = "Insider Conversational";

        [Required(ErrorMessage = "Pacing is required")]
        [StringLength(50)]
        public string Pacing { get; set; } = "Build-Release";

        [Required(ErrorMessage = "Complexity is required")]
        [StringLength(50)]
        public string Complexity { get; set; } = "Layered Progressive";

        [Required(ErrorMessage = "Structure style is required")]
        [StringLength(50)]
        public string StructureStyle { get; set; } = "Enumerated Scaffolding";

        [Required(ErrorMessage = "Hook strategy is required")]
        [StringLength(50)]
        public string HookStrategy { get; set; } = "Insider Secret";

        [Required(ErrorMessage = "Audience relationship is required")]
        [StringLength(50)]
        public string AudienceRelationship { get; set; } = "Insider-Outsider";

        [Required(ErrorMessage = "Information density is required")]
        [StringLength(50)]
        public string InformationDensity { get; set; } = "High Density";

        [Required(ErrorMessage = "Rhetorical style is required")]
        [StringLength(50)]
        public string RhetoricalStyle { get; set; } = "Extended Metaphors";

        [StringLength(2000, ErrorMessage = "Custom instructions cannot exceed 2000 characters")]
        public string? CustomInstructions { get; set; }
    }
}
