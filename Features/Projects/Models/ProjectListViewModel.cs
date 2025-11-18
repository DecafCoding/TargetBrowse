namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// View model for displaying projects in a list.
    /// </summary>
    public class ProjectListViewModel
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
        /// Project description (truncated for list view).
        /// </summary>
        public string? DescriptionPreview { get; set; }

        /// <summary>
        /// Number of videos in the project.
        /// </summary>
        public int VideoCount { get; set; }

        /// <summary>
        /// Whether the project has a generated guide.
        /// </summary>
        public bool HasGuide { get; set; }

        /// <summary>
        /// When the project was last modified.
        /// </summary>
        public DateTime LastModifiedAt { get; set; }

        /// <summary>
        /// When the project was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
