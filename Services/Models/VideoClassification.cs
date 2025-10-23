using Newtonsoft.Json;

namespace TargetBrowse.Services.Models;

/// <summary>
/// Represents a single video classification result
/// </summary>
public class VideoClassification
{
    [JsonProperty("videoId")]
    public string VideoId { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("code")]
    public string Code { get; set; } = string.Empty;

    [JsonProperty("confidence")]
    public string Confidence { get; set; } = string.Empty;
}
