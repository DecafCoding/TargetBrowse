namespace TargetBrowse.Services.ProjectServices.Models
{
    /// <summary>
    /// Configuration settings for Project feature.
    /// Binds to "ProjectSettings" section in appsettings.json
    /// </summary>
    public class ProjectSettings
    {
        /// <summary>
        /// Maximum number of videos allowed per project.
        /// Default: 10
        /// </summary>
        public int MaxVideosPerProject { get; set; } = 5;

        /// <summary>
        /// Minimum number of videos required to generate a guide.
        /// Default: 3
        /// </summary>
        public int MinVideosForGuide { get; set; } = 1;
    }
}
