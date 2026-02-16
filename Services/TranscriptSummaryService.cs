using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Services;

/// <summary>
/// Service for summarizing video transcripts using OpenAI API.
/// Handles the complete workflow: summary checking, video retrieval, prompt selection,
/// transcript preparation, API calls, and summary storage.
/// </summary>
public class TranscriptSummaryService : ITranscriptSummaryService
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TranscriptSummaryService> _logger;
    private readonly IPromptDataService _promptDataService;
    private readonly ISummaryDataService _summaryDataService;
    private readonly IAICallDataService _aiCallDataService;
    private readonly string _apiKey;
    private const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";
    private const decimal DefaultTemperature = 0.7m;
    private const int DefaultMaxTokens = 2000;

    public TranscriptSummaryService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<TranscriptSummaryService> logger,
        IPromptDataService promptDataService,
        ISummaryDataService summaryDataService,
        IAICallDataService aiCallDataService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _promptDataService = promptDataService ?? throw new ArgumentNullException(nameof(promptDataService));
        _summaryDataService = summaryDataService ?? throw new ArgumentNullException(nameof(summaryDataService));
        _aiCallDataService = aiCallDataService ?? throw new ArgumentNullException(nameof(aiCallDataService));

        _apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey configuration is missing");

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Summarizes a video transcript using AI.
    /// Retrieves the video, determines the appropriate prompt based on video type,
    /// calls OpenAI API, logs the call, and stores the summary.
    /// </summary>
    public async Task<SummaryResult> SummarizeVideoTranscriptAsync(Guid videoId, string? userId)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation($"Starting transcript summarization for video {videoId}");

            // Step 1: Check if summary already exists
            var existingSummary = await _summaryDataService.GetSummaryByVideoIdAsync(videoId);
            if (existingSummary != null)
            {
                _logger.LogInformation($"Summary already exists for video {videoId}");
                return CreateSkippedResult(
                    videoId,
                    "Summary already exists",
                    existingSummary.Id,
                    existingSummary.Content);
            }

            // Step 2: Retrieve video entity with VideoType navigation property
            var video = await _context.Videos
                .Include(v => v.VideoType)
                .FirstOrDefaultAsync(v => v.Id == videoId);

            if (video == null)
            {
                _logger.LogWarning($"Video {videoId} not found in database");
                return CreateFailedResult(videoId, "Video not found");
            }

            // Step 3: Validate video has required data
            if (string.IsNullOrWhiteSpace(video.RawTranscript))
            {
                _logger.LogInformation($"Video {videoId} has no transcript available");
                return CreateSkippedResult(videoId, "No transcript available");
            }

            // Step 4: Use the unified Video Analysis prompt
            var promptName = "Video Analysis";
            _logger.LogInformation($"Using prompt '{promptName}' for video {videoId}");

            // Step 5: Retrieve prompt with Model navigation property
            var promptEntity = await _promptDataService.GetActivePromptByNameAsync(promptName);
            if (promptEntity == null)
            {
                _logger.LogError($"Active prompt '{promptName}' not found in database");
                return CreateFailedResult(videoId, $"Prompt '{promptName}' not found or inactive");
            }

            _logger.LogInformation($"Using model: {promptEntity.Model.Name}");

            // Step 6: Prepare transcript (estimate tokens and truncate if needed)
            var (preparedTranscript, wasTruncated) = PrepareTranscript(
                video.RawTranscript,
                promptEntity.MaxTokens,
                promptEntity.SystemPrompt,
                promptEntity.UserPromptTemplate);

            if (wasTruncated)
            {
                _logger.LogWarning($"Transcript for video {videoId} was truncated to fit token limits");
            }

            // Step 7: Replace placeholder in prompt template
            var actualUserPrompt = promptEntity.UserPromptTemplate
                .Replace("{transcript}", preparedTranscript);

            // Step 8: Create and send OpenAI request
            var request = CreateSummaryRequest(promptEntity, actualUserPrompt);
            var response = await CallOpenAiApiAsync(request);

            stopwatch.Stop();

            // Step 9: Track the API call (whether success or failure)
            var aiCall = await TrackApiCallAsync(
                promptEntity,
                actualUserPrompt,
                response,
                stopwatch.ElapsedMilliseconds,
                userId,
                response == null ? "Failed to get response from OpenAI API" : null);

            if (response == null)
            {
                return CreateFailedResult(videoId, "Failed to get response from OpenAI API");
            }

            // Step 10: Parse response into structured analysis and render as HTML
            var analysisResponse = ParseAnalysisResponse(response);
            if (analysisResponse == null)
            {
                return CreateFailedResult(videoId, "Empty or invalid response from OpenAI API");
            }

            var renderedHtml = RenderAnalysisAsHtml(analysisResponse);
            var coreThesis = analysisResponse.CoreThesis ?? "Video analysis";

            // Step 11: Store analysis (Content = rendered HTML, Summary = core thesis)
            var summary = await _summaryDataService.CreateSummaryAsync(
                videoId,
                renderedHtml,
                coreThesis,
                aiCall.Id);

            // Step 12: Calculate costs and build result
            var inputTokens = response.Usage?.PromptTokens ?? EstimateTokenCount(promptEntity.SystemPrompt + actualUserPrompt);
            var outputTokens = response.Usage?.CompletionTokens ?? EstimateTokenCount(renderedHtml);
            var totalCost = CalculateCost(
                inputTokens,
                promptEntity.Model.CostPer1kInputTokens,
                outputTokens,
                promptEntity.Model.CostPer1kOutputTokens);

            _logger.LogInformation(
                $"Successfully created summary {summary.Id} for video {videoId}. " +
                $"Tokens: {inputTokens} input, {outputTokens} output. Cost: ${totalCost:F6}");

            return CreateSuccessResult(
                videoId,
                summary.Id,
                renderedHtml,
                inputTokens,
                outputTokens,
                totalCost,
                stopwatch.ElapsedMilliseconds,
                wasTruncated);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, $"Error summarizing video {videoId}: {ex.Message}");
            return CreateFailedResult(videoId, $"Summarization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Prepares the transcript for API submission.
    /// Estimates token count and truncates if necessary to fit within limits.
    /// </summary>
    private (string preparedTranscript, bool wasTruncated) PrepareTranscript(
        string rawTranscript,
        int? maxTokens,
        string? systemPrompt,
        string? userPromptTemplate)
    {
        if (!maxTokens.HasValue)
        {
            return (rawTranscript, false);
        }

        // Estimate tokens for system prompt and user prompt template (without transcript)
        var systemTokens = EstimateTokenCount(systemPrompt ?? string.Empty);
        var templateTokens = EstimateTokenCount(userPromptTemplate?.Replace("{transcript}", "") ?? string.Empty);
        var transcriptTokens = EstimateTokenCount(rawTranscript);

        // Calculate available tokens for transcript (reserve some for response)
        var reserveForResponse = 500; // Reserve tokens for the response
        var availableForTranscript = maxTokens.Value - systemTokens - templateTokens - reserveForResponse;

        if (transcriptTokens <= availableForTranscript)
        {
            return (rawTranscript, false);
        }

        // Truncate from the end
        var targetCharCount = availableForTranscript * 4; // 1 token ≈ 4 characters
        if (targetCharCount < rawTranscript.Length)
        {
            var truncated = rawTranscript.Substring(0, targetCharCount) + "... [truncated]";
            return (truncated, true);
        }

        return (rawTranscript, false);
    }

    /// <summary>
    /// Creates the OpenAI API request using the prompt configuration.
    /// </summary>
    private OpenAiRequest CreateSummaryRequest(PromptEntity promptEntity, string actualUserPrompt)
    {
        return new OpenAiRequest
        {
            Model = promptEntity.Model.Name ?? "gpt-4o-mini",
            Messages = new List<OpenAiMessage>
            {
                new OpenAiMessage
                {
                    Role = "system",
                    Content = promptEntity.SystemPrompt ?? "You are a helpful assistant that summarizes video transcripts."
                },
                new OpenAiMessage
                {
                    Role = "user",
                    Content = actualUserPrompt
                }
            },
            MaxTokens = promptEntity.MaxTokens ?? DefaultMaxTokens,
            Temperature = promptEntity.Temperature ?? DefaultTemperature,
            ResponseFormat = new OpenAiResponseFormat { Type = "json_object" }
        };
    }

    /// <summary>
    /// Sends the request to OpenAI API and handles the response.
    /// </summary>
    private async Task<OpenAiResponse?> CallOpenAiApiAsync(OpenAiRequest request)
    {
        try
        {
            var jsonRequest = JsonConvert.SerializeObject(request);
            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(OpenAiApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"OpenAI API request failed: {response.StatusCode} - {errorContent}");
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<OpenAiResponse>(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calling OpenAI API: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses the OpenAI response into a structured VideoAnalysisResponse.
    /// </summary>
    private VideoAnalysisResponse? ParseAnalysisResponse(OpenAiResponse response)
    {
        try
        {
            if (response.Choices == null || !response.Choices.Any())
            {
                _logger.LogWarning("No choices returned from OpenAI API");
                return null;
            }

            var messageContent = response.Choices.First().Message.Content;

            if (messageContent is string jsonContent)
            {
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("Empty response content from OpenAI API");
                    return null;
                }

                var analysisResponse = JsonConvert.DeserializeObject<VideoAnalysisResponse>(jsonContent);
                if (analysisResponse == null)
                {
                    _logger.LogWarning("Failed to deserialize analysis response JSON");
                    return null;
                }

                return analysisResponse;
            }

            _logger.LogWarning("Unexpected response content format from OpenAI API");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error parsing OpenAI response: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Renders the structured analysis response as Bootstrap-styled HTML.
    /// Sections with no content are skipped entirely.
    /// </summary>
    private string RenderAnalysisAsHtml(VideoAnalysisResponse analysis)
    {
        var sb = new StringBuilder();

        // Core Thesis
        if (!string.IsNullOrWhiteSpace(analysis.CoreThesis))
        {
            sb.Append("<h5>Core Thesis</h5>");
            sb.Append($"<p>{Encode(analysis.CoreThesis)}</p>");
        }

        // Topics with nested subtopics
        if (analysis.Topics?.Count > 0)
        {
            foreach (var topic in analysis.Topics)
            {
                sb.Append($"<h5>{Encode(topic.Topic)}</h5>");

                if (topic.Subtopics?.Count > 0)
                {
                    foreach (var sub in topic.Subtopics)
                    {
                        sb.Append($"<h6>{Encode(sub.Subtopic)}</h6>");

                        // Facts
                        if (sub.Facts?.Count > 0)
                        {
                            sb.Append("<ul>");
                            foreach (var fact in sub.Facts)
                                sb.Append($"<li>{Encode(fact)}</li>");
                            sb.Append("</ul>");
                        }

                        // Concrete Examples
                        if (sub.ConcreteExamples?.Count > 0)
                        {
                            sb.Append("<strong>Examples:</strong><ul>");
                            foreach (var ex in sub.ConcreteExamples)
                            {
                                sb.Append("<li>");
                                if (!string.IsNullOrWhiteSpace(ex.Illustrates))
                                    sb.Append($"<strong>{Encode(ex.Illustrates)}</strong>: ");
                                sb.Append(Encode(ex.Example));
                                sb.Append("</li>");
                            }
                            sb.Append("</ul>");
                        }

                        // Do This, Not That
                        if (sub.DoThisNotThat?.Count > 0)
                        {
                            sb.Append("<table class=\"table table-sm table-bordered\"><thead><tr><th>Don't</th><th>Do</th><th>Why</th></tr></thead><tbody>");
                            foreach (var item in sub.DoThisNotThat)
                                sb.Append($"<tr><td>{Encode(item.Bad)}</td><td>{Encode(item.Good)}</td><td>{Encode(item.Why)}</td></tr>");
                            sb.Append("</tbody></table>");
                        }

                        // Workflows
                        if (sub.Workflows?.Count > 0)
                        {
                            foreach (var wf in sub.Workflows)
                            {
                                if (!string.IsNullOrWhiteSpace(wf.Description))
                                    sb.Append($"<strong>{Encode(wf.Description)}</strong>");
                                if (wf.Steps?.Count > 0)
                                {
                                    sb.Append("<ol>");
                                    foreach (var step in wf.Steps)
                                        sb.Append($"<li>{Encode(step)}</li>");
                                    sb.Append("</ol>");
                                }
                            }
                        }

                        // People and Evidence
                        if (sub.PeopleAndEvidence?.Count > 0)
                        {
                            sb.Append("<strong>Evidence:</strong><ul>");
                            foreach (var pe in sub.PeopleAndEvidence.Where(p => !string.IsNullOrWhiteSpace(p.Detail)))
                                sb.Append($"<li><strong>{Encode(pe.Detail)}</strong>: {Encode(pe.Context)}</li>");
                            sb.Append("</ul>");
                        }

                        // Metaphors
                        if (sub.Metaphors?.Count > 0)
                        {
                            sb.Append("<strong>Analogies:</strong><ul>");
                            foreach (var m in sub.Metaphors)
                                sb.Append($"<li><strong>{Encode(m.Metaphor)}</strong> &mdash; {Encode(m.Explains)}</li>");
                            sb.Append("</ul>");
                        }
                    }
                }
            }
        }

        // Limitations
        if (analysis.Limitations?.Count > 0)
        {
            sb.Append("<h5>Limitations</h5><ul>");
            foreach (var lim in analysis.Limitations)
                sb.Append($"<li>{Encode(lim)}</li>");
            sb.Append("</ul>");
        }

        // Calls to Action
        if (analysis.CallsToAction?.Count > 0)
        {
            sb.Append("<h5>Calls to Action</h5><ul>");
            foreach (var cta in analysis.CallsToAction)
                sb.Append($"<li>{Encode(cta)}</li>");
            sb.Append("</ul>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// HTML-encodes a string to prevent XSS when rendering user/AI content.
    /// </summary>
    private static string Encode(string? value)
    {
        return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    }

    #region Analysis Response Models (v3 — topic-centric)

    private class VideoAnalysisResponse
    {
        [JsonProperty("videoTitle")]
        public string? VideoTitle { get; set; }

        [JsonProperty("coreThesis")]
        public string? CoreThesis { get; set; }

        [JsonProperty("topics")]
        public List<TopicItem>? Topics { get; set; }

        [JsonProperty("limitations")]
        public List<string>? Limitations { get; set; }

        [JsonProperty("callsToAction")]
        public List<string>? CallsToAction { get; set; }
    }

    private class TopicItem
    {
        [JsonProperty("topic")]
        public string Topic { get; set; } = string.Empty;

        [JsonProperty("subtopics")]
        public List<SubtopicItem>? Subtopics { get; set; }
    }

    private class SubtopicItem
    {
        [JsonProperty("subtopic")]
        public string Subtopic { get; set; } = string.Empty;

        [JsonProperty("facts")]
        public List<string>? Facts { get; set; }

        [JsonProperty("concreteExamples")]
        public List<ExampleItem>? ConcreteExamples { get; set; }

        [JsonProperty("doThisNotThat")]
        public List<DoThisNotThatItem>? DoThisNotThat { get; set; }

        [JsonProperty("workflows")]
        public List<WorkflowItem>? Workflows { get; set; }

        [JsonProperty("peopleAndEvidence")]
        public List<EvidenceItem>? PeopleAndEvidence { get; set; }

        [JsonProperty("metaphors")]
        public List<MetaphorItem>? Metaphors { get; set; }
    }

    private class ExampleItem
    {
        [JsonProperty("example")]
        public string Example { get; set; } = string.Empty;

        [JsonProperty("illustrates")]
        public string? Illustrates { get; set; }
    }

    private class DoThisNotThatItem
    {
        [JsonProperty("bad")]
        public string Bad { get; set; } = string.Empty;

        [JsonProperty("good")]
        public string Good { get; set; } = string.Empty;

        [JsonProperty("why")]
        public string Why { get; set; } = string.Empty;
    }

    private class WorkflowItem
    {
        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("steps")]
        public List<string>? Steps { get; set; }
    }

    private class EvidenceItem
    {
        [JsonProperty("detail")]
        public string Detail { get; set; } = string.Empty;

        [JsonProperty("context")]
        public string Context { get; set; } = string.Empty;
    }

    private class MetaphorItem
    {
        [JsonProperty("metaphor")]
        public string Metaphor { get; set; } = string.Empty;

        [JsonProperty("explains")]
        public string Explains { get; set; } = string.Empty;
    }

    #endregion

    /// <summary>
    /// Tracks the API call in the database for auditing and cost monitoring.
    /// </summary>
    private async Task<AICallEntity> TrackApiCallAsync(
        PromptEntity promptEntity,
        string actualUserPrompt,
        OpenAiResponse? response,
        long durationMs,
        string? userId,
        string? errorMessage = null)
    {
        try
        {
            int inputTokens = 0;
            int outputTokens = 0;
            decimal totalCost = 0m;
            bool success = response != null && string.IsNullOrEmpty(errorMessage);
            string responseContent = string.Empty;

            // Use actual token usage if available, otherwise estimate
            if (response?.Usage != null)
            {
                inputTokens = response.Usage.PromptTokens;
                outputTokens = response.Usage.CompletionTokens;
                totalCost = CalculateCost(
                    inputTokens,
                    promptEntity.Model.CostPer1kInputTokens,
                    outputTokens,
                    promptEntity.Model.CostPer1kOutputTokens);

                // Capture response content if available
                if (response.Choices?.Any() == true)
                {
                    var content = response.Choices.First().Message.Content;
                    responseContent = content is string str ? str : JsonConvert.SerializeObject(content);
                }
            }
            else
            {
                // Estimate tokens for failed or incomplete calls
                inputTokens = EstimateTokenCount(promptEntity.SystemPrompt + actualUserPrompt);
                outputTokens = EstimateTokenCount(responseContent);
                totalCost = CalculateCost(
                    inputTokens,
                    promptEntity.Model.CostPer1kInputTokens,
                    outputTokens,
                    promptEntity.Model.CostPer1kOutputTokens);
            }

            // Create AI call record
            var aiCall = await _aiCallDataService.CreateAICallAsync(
                promptId: promptEntity.Id,
                userId: userId,
                actualSystemPrompt: promptEntity.SystemPrompt ?? "You are a helpful assistant that summarizes video transcripts.",
                actualUserPrompt: actualUserPrompt,
                response: responseContent,
                inputTokens: inputTokens,
                outputTokens: outputTokens,
                totalCost: totalCost,
                durationMs: (int)durationMs,
                success: success,
                errorMessage: errorMessage);

            var statusLog = success ? "successfully" : "with errors";
            _logger.LogInformation(
                $"AI call tracked {statusLog}. " +
                $"User: {userId}, Tokens: {inputTokens + outputTokens}, Cost: ${totalCost:F6}");

            return aiCall;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to track API call for user {userId}. This won't stop processing.");
            throw; // Re-throw since we need the AICallEntity for summary creation
        }
    }

    /// <summary>
    /// Calculates the cost of an API call based on token usage and model pricing.
    /// </summary>
    private decimal CalculateCost(int inputTokens, decimal inputCostPer1k, int outputTokens, decimal outputCostPer1k)
    {
        return ((inputTokens * (inputCostPer1k / 1000))) + (outputTokens * (outputCostPer1k / 1000));
    }

    /// <summary>
    /// Estimates token count for cost tracking (rough approximation: 1 token ≈ 4 characters).
    /// </summary>
    private int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Length / 4;
    }

    /// <summary>
    /// Creates a skipped result.
    /// </summary>
    private SummaryResult CreateSkippedResult(Guid videoId, string skipReason, Guid? summaryId = null, string? existingContent = null)
    {
        return new SummaryResult
        {
            Success = false,
            VideoId = videoId,
            Skipped = true,
            SkipReason = skipReason,
            SummaryId = summaryId,
            SummaryContent = existingContent
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    private SummaryResult CreateFailedResult(Guid videoId, string errorMessage)
    {
        return new SummaryResult
        {
            Success = false,
            VideoId = videoId,
            ErrorMessage = errorMessage,
            Skipped = false
        };
    }

    /// <summary>
    /// Creates a success result.
    /// </summary>
    private SummaryResult CreateSuccessResult(
        Guid videoId,
        Guid summaryId,
        string summaryContent,
        int inputTokens,
        int outputTokens,
        decimal totalCost,
        long durationMs,
        bool wasTruncated)
    {
        return new SummaryResult
        {
            Success = true,
            VideoId = videoId,
            SummaryId = summaryId,
            SummaryContent = summaryContent,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalCost = totalCost,
            DurationMs = durationMs,
            WasTruncated = wasTruncated,
            Skipped = false
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
