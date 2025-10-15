using Newtonsoft.Json;

namespace TargetBrowse.Services.Models
{
    public class SummaryResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string VideoId { get; set; } = string.Empty;
        public string VideoTitle { get; set; } = string.Empty;

        [JsonProperty("video_topic")]
        public string VideoTopic { get; set; } = string.Empty;

        [JsonProperty("main_summary")]
        public string MainSummary { get; set; } = string.Empty;

        [JsonProperty("structured_content")]
        public string StructuredContent { get; set; } = string.Empty;
    }
}
