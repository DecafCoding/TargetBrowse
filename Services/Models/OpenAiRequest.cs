using Newtonsoft.Json;

namespace TargetBrowse.Services.Models;

internal class OpenAiRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = "gpt-4o-mini";

    [JsonProperty("messages")]
    public List<OpenAiMessage> Messages { get; set; } = new List<OpenAiMessage>();

    [JsonProperty("max_completion_tokens")]
    public int MaxTokens { get; set; } = 1500;

    [JsonProperty("temperature")]
    public decimal Temperature { get; set; } = 0.3m;

    [JsonProperty("response_format")]
    public OpenAiResponseFormat ResponseFormat { get; set; } = new OpenAiResponseFormat();
}

internal class OpenAiMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("content")]
    public object Content { get; set; } = string.Empty;
}

/// <summary>
/// Represents a content part in a multimodal message (text or image).
/// Used for vision API calls that include images.
/// </summary>
internal class OpenAiContentPart
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty; // "text" or "image_url"

    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string? Text { get; set; }

    [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
    public OpenAiImageUrl? ImageUrl { get; set; }
}

/// <summary>
/// Represents an image URL in an OpenAI vision request.
/// </summary>
internal class OpenAiImageUrl
{
    [JsonProperty("url")]
    public string Url { get; set; } = string.Empty;

    [JsonProperty("detail", NullValueHandling = NullValueHandling.Ignore)]
    public string? Detail { get; set; } // "low", "high", or "auto"
}

internal class OpenAiResponseFormat
{
    [JsonProperty("type")]
    public string Type { get; set; } = "json_object";
}

internal class OpenAiResponse
{
    [JsonProperty("choices")]
    public List<OpenAiChoice> Choices { get; set; } = new List<OpenAiChoice>();

    [JsonProperty("usage")]
    public OpenAiUsage? Usage { get; set; }
}

internal class OpenAiChoice
{
    [JsonProperty("message")]
    public OpenAiMessage Message { get; set; } = new OpenAiMessage();
}

internal class OpenAiUsage
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }
}
