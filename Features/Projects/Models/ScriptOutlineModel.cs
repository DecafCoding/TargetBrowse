using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// Model representing a script outline.
    /// Contains title, hook, sections, and conclusion.
    /// </summary>
    public class ScriptOutlineModel
    {
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("hook")]
        public string Hook { get; set; } = string.Empty;

        [JsonProperty("sections")]
        public List<OutlineSection> Sections { get; set; } = new();

        [JsonProperty("conclusion")]
        public string Conclusion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a section in the script outline.
    /// </summary>
    public class OutlineSection
    {
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("keyPoints")]
        public List<string> KeyPoints { get; set; } = new();

        [JsonProperty("estimatedMinutes")]
        public decimal EstimatedMinutes { get; set; }

        /// <summary>
        /// Source videos for this section. Uses a custom converter because
        /// OpenAI may return this as a string or as an array of strings.
        /// </summary>
        [JsonProperty("sourceVideos")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        public string SourceVideos { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handles deserialization of values that may be a string or an array of strings.
    /// Joins arrays into a comma-separated string.
    /// </summary>
    public class FlexibleStringConverter : JsonConverter<string>
    {
        public override string? ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);

            if (token.Type == JTokenType.Array)
            {
                var items = token.ToObject<List<string>>();
                return items != null ? string.Join(", ", items) : string.Empty;
            }

            return token.ToString();
        }

        public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }
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
