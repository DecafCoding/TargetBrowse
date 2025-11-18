namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// View model for editing a project.
    /// Contains all fields needed for the edit form.
    /// </summary>
    public class ProjectEditViewModel
    {
        /// <summary>
        /// Project ID. Empty Guid for new projects.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Project name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Project description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// User guidance for AI guide generation.
        /// </summary>
        public string? UserGuidance { get; set; }
    }
}
