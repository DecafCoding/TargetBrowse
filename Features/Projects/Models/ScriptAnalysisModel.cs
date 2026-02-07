namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// Model representing the analysis of videos for script generation.
    /// Contains main topic, subtopics, conflicts, and recommendations.
    /// </summary>
    public class ScriptAnalysisModel
    {
        public string MainTopic { get; set; } = string.Empty;
        public List<SubtopicAnalysis> Subtopics { get; set; } = new();
        public List<ConflictAnalysis> Conflicts { get; set; } = new();
        public List<UniqueClaimAnalysis> UniqueClaims { get; set; } = new();
        public int CohesionScore { get; set; }
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Represents analysis of a subtopic covered across videos.
    /// </summary>
    public class SubtopicAnalysis
    {
        public string Name { get; set; } = string.Empty;
        public List<string> CoveredBy { get; set; } = new();
        public string BestSource { get; set; } = string.Empty;
        public string Depth { get; set; } = string.Empty; // comprehensive, moderate, brief
    }

    /// <summary>
    /// Represents a conflict or contradiction between videos.
    /// </summary>
    public class ConflictAnalysis
    {
        public string Topic { get; set; } = string.Empty;
        public string Video1Claim { get; set; } = string.Empty;
        public string Video2Claim { get; set; } = string.Empty;
        public string AiRecommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a claim made by only one video.
    /// </summary>
    public class UniqueClaimAnalysis
    {
        public string Source { get; set; } = string.Empty;
        public string Claim { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result model for script analysis operation.
    /// </summary>
    public class ScriptAnalysisResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public ScriptAnalysisModel? Analysis { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public decimal TotalCost { get; set; }
        public long DurationMs { get; set; }
    }

    /// <summary>
    /// Result model for script configuration operation.
    /// </summary>
    public class ScriptConfigurationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
