namespace TargetBrowse.Services.ProjectServices.Models
{
    /// <summary>
    /// Lightweight project information for display in UI.
    /// </summary>
    public class ProjectInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CurrentVideoCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Whether the project has reached its maximum video limit.
        /// </summary>
        public bool IsFull { get; set; }

        /// <summary>
        /// Whether the specified video already exists in this project.
        /// </summary>
        public bool ContainsVideo { get; set; }
    }
}
