namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// View model for displaying full project details.
    /// </summary>
    public class ProjectDetailViewModel
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
        /// Full project description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// User guidance for AI guide generation.
        /// </summary>
        public string? UserGuidance { get; set; }

        /// <summary>
        /// Videos in the project (ordered).
        /// </summary>
        public List<ProjectVideoViewModel> Videos { get; set; } = new List<ProjectVideoViewModel>();

        /// <summary>
        /// Whether the project has a generated guide.
        /// </summary>
        public bool HasGuide { get; set; }

        /// <summary>
        /// Guide content (if available).
        /// </summary>
        public string? GuideContent { get; set; }

        /// <summary>
        /// When the guide was generated (if available).
        /// </summary>
        public DateTime? GuideGeneratedAt { get; set; }

        /// <summary>
        /// Whether the guide needs regeneration due to changes.
        /// </summary>
        public bool NeedsRegeneration { get; set; }

        /// <summary>
        /// Whether all videos have summaries (required for guide generation).
        /// </summary>
        public bool AllVideosHaveSummaries { get; set; }

        /// <summary>
        /// Whether the project meets minimum video count for guide generation.
        /// </summary>
        public bool MeetsMinimumVideoCount { get; set; }

        /// <summary>
        /// When the project was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the project was last modified.
        /// </summary>
        public DateTime LastModifiedAt { get; set; }
    }

    /// <summary>
    /// View model for a video within a project.
    /// </summary>
    public class ProjectVideoViewModel
    {
        /// <summary>
        /// Video ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// YouTube video ID.
        /// </summary>
        public string YouTubeVideoId { get; set; } = string.Empty;

        /// <summary>
        /// Video title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Video thumbnail URL.
        /// </summary>
        public string? ThumbnailUrl { get; set; }

        /// <summary>
        /// Video duration in seconds.
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// Channel name.
        /// </summary>
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>
        /// Whether the video has a summary.
        /// </summary>
        public bool HasSummary { get; set; }

        /// <summary>
        /// Order in the project.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// When the video was added to the project.
        /// </summary>
        public DateTime AddedAt { get; set; }
    }
}
