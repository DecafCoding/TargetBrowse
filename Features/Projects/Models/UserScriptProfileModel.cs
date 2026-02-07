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
        public string Tone { get; set; } = "Casual";

        [Required(ErrorMessage = "Pacing is required")]
        [StringLength(50)]
        public string Pacing { get; set; } = "Moderate";

        [Required(ErrorMessage = "Complexity is required")]
        [StringLength(50)]
        public string Complexity { get; set; } = "Intermediate";

        [StringLength(2000, ErrorMessage = "Custom instructions cannot exceed 2000 characters")]
        public string? CustomInstructions { get; set; }
    }
}
