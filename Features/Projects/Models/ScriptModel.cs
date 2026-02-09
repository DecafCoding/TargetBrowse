using Newtonsoft.Json;

namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// Model representing the final generated script.
    /// Contains full narration text, metadata, and internal source notes.
    /// </summary>
    public class ScriptModel
    {
        [JsonProperty("scriptText")]
        public string ScriptText { get; set; } = string.Empty;

        [JsonProperty("wordCount")]
        public int WordCount { get; set; }

        [JsonProperty("estimatedDurationSeconds")]
        public int EstimatedDurationSeconds { get; set; }

        [JsonProperty("internalNotes")]
        public Dictionary<string, SectionNotes> InternalNotes { get; set; } = new();
    }

    /// <summary>
    /// Source tracking notes for a script section.
    /// Provides transparency about AI decision-making per section.
    /// </summary>
    public class SectionNotes
    {
        [JsonProperty("sources")]
        public List<string> Sources { get; set; } = new();

        [JsonProperty("conflictsResolved")]
        public List<string> ConflictsResolved { get; set; } = new();

        [JsonProperty("uniqueClaims")]
        public List<string> UniqueClaims { get; set; } = new();
    }

    /// <summary>
    /// Result model for script generation operation.
    /// </summary>
    public class ScriptGenerationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public ScriptModel? Script { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public decimal TotalCost { get; set; }
        public long DurationMs { get; set; }
    }
}
