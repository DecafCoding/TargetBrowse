using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents an AI-generated video script for a project.
    /// Stores analysis, outline, and final script content with full audit trail.
    /// One script per project (1-to-1 relationship).
    /// </summary>
    public class ScriptContentEntity : BaseEntity
    {
        [Required]
        public Guid ProjectId { get; set; }

        /// <summary>
        /// Current status of script generation workflow.
        /// Values: Analyzing, Analyzed, GeneratingOutline, OutlineGenerated, GeneratingScript, Complete
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ScriptStatus { get; set; } = "Analyzing";

        /// <summary>
        /// JSON result from video analysis phase (Phase 2).
        /// Contains main topic, subtopics, conflicts, unique claims, cohesion score, etc.
        /// </summary>
        public string? AnalysisJsonResult { get; set; }

        /// <summary>
        /// Main topic identified from video analysis.
        /// </summary>
        [StringLength(200)]
        public string? MainTopic { get; set; }

        /// <summary>
        /// Cohesion score (0-100) indicating how well videos work together.
        /// </summary>
        public int? CohesionScore { get; set; }

        /// <summary>
        /// JSON structure of the script outline (Phase 4).
        /// Contains sections, time allocations, key points, source notes.
        /// </summary>
        public string? OutlineJsonStructure { get; set; }

        /// <summary>
        /// Estimated length in minutes for the final script.
        /// </summary>
        public int? EstimatedLengthMinutes { get; set; }

        /// <summary>
        /// Target length in minutes configured by user (Phase 3).
        /// </summary>
        public int? TargetLengthMinutes { get; set; }

        /// <summary>
        /// Final script narration text (Phase 5).
        /// </summary>
        [Required]
        public string ScriptText { get; set; } = string.Empty;

        /// <summary>
        /// Word count of the final script.
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// Estimated duration in seconds for the final script.
        /// Calculated based on average speaking rate (150 words per minute).
        /// </summary>
        public int EstimatedDurationSeconds { get; set; }

        /// <summary>
        /// JSON containing internal notes about sources, conflicts resolved, unique claims.
        /// Used for transparency but not displayed in final export.
        /// </summary>
        public string? InternalNotesJson { get; set; }

        /// <summary>
        /// When the script was generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Snapshot of video count at generation time.
        /// Used to detect if project changed and regeneration is needed.
        /// </summary>
        public int VideoCount { get; set; }

        /// <summary>
        /// Foreign key to AI call that generated the analysis.
        /// </summary>
        public Guid? AnalysisAICallId { get; set; }

        /// <summary>
        /// Foreign key to AI call that generated the outline.
        /// </summary>
        public Guid? OutlineAICallId { get; set; }

        /// <summary>
        /// Foreign key to AI call that generated the final script.
        /// </summary>
        public Guid? ScriptAICallId { get; set; }

        // Navigation properties
        public virtual ProjectEntity Project { get; set; } = null!;
        public virtual AICallEntity? AnalysisAICall { get; set; }
        public virtual AICallEntity? OutlineAICall { get; set; }
        public virtual AICallEntity? ScriptAICall { get; set; }
    }
}
