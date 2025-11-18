using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// Request model for creating a new project.
    /// </summary>
    public class CreateProjectRequest
    {
        /// <summary>
        /// Project name (required, 1-200 characters).
        /// </summary>
        [Required(ErrorMessage = "Project name is required")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Project name must be between 1 and 200 characters")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional project description (max 2000 characters).
        /// </summary>
        [StringLength(2000, ErrorMessage = "Project description cannot exceed 2000 characters")]
        public string? Description { get; set; }

        /// <summary>
        /// Optional user guidance for AI guide generation (max 1000 characters).
        /// </summary>
        [StringLength(1000, ErrorMessage = "User guidance cannot exceed 1000 characters")]
        public string? UserGuidance { get; set; }
    }
}
