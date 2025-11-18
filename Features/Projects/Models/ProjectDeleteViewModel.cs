namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// View model for delete confirmation dialog.
    /// Contains all fields needed to display project information before deletion.
    /// </summary>
    public class ProjectDeleteViewModel
    {
        /// <summary>
        /// Project ID.
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
        /// Number of videos in the project.
        /// </summary>
        public int VideoCount { get; set; }

        /// <summary>
        /// Whether the project has a generated guide.
        /// </summary>
        public bool HasGuide { get; set; }
    }
}
