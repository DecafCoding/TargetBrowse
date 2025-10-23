using Newtonsoft.Json;

namespace TargetBrowse.Services.Models;

internal class OpenAiRequest
{
    [JsonProperty("model")]
    public string Model { get; set; } = "gpt-4o-mini";

    [JsonProperty("messages")]
    public List<OpenAiMessage> Messages { get; set; } = new List<OpenAiMessage>();

    [JsonProperty("max_tokens")]
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
    public string Content { get; set; } = string.Empty;
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
}

internal class OpenAiChoice
{
    [JsonProperty("message")]
    public OpenAiMessage Message { get; set; } = new OpenAiMessage();
}
