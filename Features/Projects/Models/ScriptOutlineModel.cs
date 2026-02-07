namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// Model representing a script outline.
    /// Contains title, hook, sections, and conclusion.
    /// </summary>
    public class ScriptOutlineModel
    {
        public string Title { get; set; } = string.Empty;
        public string Hook { get; set; } = string.Empty;
        public List<OutlineSection> Sections { get; set; } = new();
        public string Conclusion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a section in the script outline.
    /// </summary>
    public class OutlineSection
    {
        public string Title { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = new();
        public int EstimatedMinutes { get; set; }
        public string SourceVideos { get; set; } = string.Empty;
    }

    /// <summary>
    /// Result model for outline generation operation.
    /// </summary>
    public class ScriptOutlineResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public ScriptOutlineModel? Outline { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public decimal TotalCost { get; set; }
        public long DurationMs { get; set; }
    }
}
