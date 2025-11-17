using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Services;

/// <summary>
/// Service for analyzing YouTube video thumbnails using OpenAI's vision capabilities.
/// Handles the complete workflow: prompt retrieval, API calls with image analysis, and AI call logging.
/// </summary>
public class ThumbnailAnalysisService : IThumbnailAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ThumbnailAnalysisService> _logger;
    private readonly IPromptDataService _promptDataService;
    private readonly IAICallDataService _aiCallDataService;
    private readonly string _apiKey;
    private const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string PromptName = "Thumbnail Description";
    private const string TitlePlaceholder = "[video-title]";

    public ThumbnailAnalysisService(
        IConfiguration configuration,
        ILogger<ThumbnailAnalysisService> logger,
        IPromptDataService promptDataService,
        IAICallDataService aiCallDataService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _promptDataService = promptDataService ?? throw new ArgumentNullException(nameof(promptDataService));
        _aiCallDataService = aiCallDataService ?? throw new ArgumentNullException(nameof(aiCallDataService));

        _apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey configuration is missing");

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Analyzes a YouTube video thumbnail and generates a description.
    /// Retrieves prompt, replaces video title placeholder, calls OpenAI vision API, logs the call, and returns results.
    /// </summary>
    public async Task<ThumbnailAnalysisResult> AnalyzeThumbnailAsync(
        string thumbnailUrl,
        string videoTitle,
        string? userId = null)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(thumbnailUrl))
            {
                _logger.LogWarning("Empty or null thumbnail URL provided");
                return CreateFailedResult("Thumbnail URL is required", thumbnailUrl, videoTitle);
            }

            if (string.IsNullOrWhiteSpace(videoTitle))
            {
                _logger.LogWarning("Empty or null video title provided");
                return CreateFailedResult("Video title is required", thumbnailUrl, videoTitle);
            }

            _logger.LogInformation($"Analyzing thumbnail for video: {videoTitle}");

            // Step 1: Retrieve the active prompt from database
            var promptEntity = await _promptDataService.GetActivePromptByNameAsync(PromptName);
            if (promptEntity == null)
            {
                _logger.LogError($"Active prompt '{PromptName}' not found in database");
                return CreateFailedResult($"Prompt '{PromptName}' not found or inactive", thumbnailUrl, videoTitle);
            }

            _logger.LogInformation($"Using prompt: {PromptName} with model: {promptEntity.Model.Name}");

            // Step 2: Replace the video title placeholder in the prompt
            var actualUserPrompt = promptEntity.UserPromptTemplate.Replace(TitlePlaceholder, videoTitle);

            // Step 3: Create the vision request with image URL
            var request = CreateVisionRequest(thumbnailUrl, actualUserPrompt, promptEntity);

            // Step 4: Send request to OpenAI and track timing
            var response = await CallOpenAiApiAsync(request);
            stopwatch.Stop();

            // Step 5: Track API call immediately after HTTP request completes
            await TrackApiCallAsync(
                promptEntity,
                actualUserPrompt,
                thumbnailUrl,
                response,
                stopwatch.ElapsedMilliseconds,
                userId,
                response == null ? "Failed to get response from OpenAI API" : null
            );

            if (response == null)
            {
                return CreateFailedResult("Failed to get response from OpenAI API", thumbnailUrl, videoTitle);
            }

            // Step 6: Parse the response
            var description = ParseDescriptionResponse(response);
            if (string.IsNullOrWhiteSpace(description))
            {
                return CreateFailedResult("Empty or invalid response from OpenAI API", thumbnailUrl, videoTitle);
            }

            // Step 7: Calculate costs and build result
            var inputTokens = response.Usage?.PromptTokens ?? EstimateTokenCount(promptEntity.SystemPrompt + actualUserPrompt);
            var outputTokens = response.Usage?.CompletionTokens ?? EstimateTokenCount(description);
            var totalCost = CalculateCost(
                inputTokens,
                promptEntity.Model.CostPer1kInputTokens,
                outputTokens,
                promptEntity.Model.CostPer1kOutputTokens);

            _logger.LogInformation(
                $"Successfully analyzed thumbnail for '{videoTitle}'. " +
                $"Tokens: {inputTokens} input, {outputTokens} output. Cost: ${totalCost:F6}");

            return CreateSuccessResult(
                description,
                inputTokens,
                outputTokens,
                totalCost,
                stopwatch.ElapsedMilliseconds,
                thumbnailUrl,
                videoTitle);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, $"Error analyzing thumbnail for '{videoTitle}': {ex.Message}");
            return CreateFailedResult($"Analysis failed: {ex.Message}", thumbnailUrl, videoTitle);
        }
    }

    /// <summary>
    /// Creates the OpenAI vision API request with the thumbnail image and text prompt.
    /// </summary>
    private OpenAiRequest CreateVisionRequest(string thumbnailUrl, string textPrompt, Data.Entities.PromptEntity promptEntity)
    {
        // Create multimodal content with text and image
        var contentParts = new List<OpenAiContentPart>
        {
            new OpenAiContentPart
            {
                Type = "text",
                Text = textPrompt
            },
            new OpenAiContentPart
            {
                Type = "image_url",
                ImageUrl = new OpenAiImageUrl
                {
                    Url = thumbnailUrl,
                    Detail = "auto" // Let OpenAI decide the detail level
                }
            }
        };

        return new OpenAiRequest
        {
            Model = promptEntity.Model.Name ?? "gpt-4o-mini",
            Messages = new List<OpenAiMessage>
            {
                new OpenAiMessage
                {
                    Role = "system",
                    Content = promptEntity.SystemPrompt ?? "You are an AI assistant that analyzes video thumbnails and provides descriptions."
                },
                new OpenAiMessage
                {
                    Role = "user",
                    Content = contentParts
                }
            },
            MaxTokens = promptEntity.MaxTokens ?? 500,
            Temperature = promptEntity.Temperature ?? 0.5m,
            ResponseFormat = new OpenAiResponseFormat { Type = "text" }
        };
    }

    /// <summary>
    /// Sends the request to OpenAI API and handles the response.
    /// </summary>
    private async Task<OpenAiResponse?> CallOpenAiApiAsync(OpenAiRequest request)
    {
        try
        {
            var jsonRequest = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
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
    /// Parses the OpenAI response and extracts the description.
    /// </summary>
    private string? ParseDescriptionResponse(OpenAiResponse response)
    {
        try
        {
            if (response.Choices == null || !response.Choices.Any())
            {
                _logger.LogWarning("No choices returned from OpenAI API");
                return null;
            }

            var messageContent = response.Choices.First().Message.Content;

            // Content could be string or object, handle both cases
            if (messageContent is string textContent)
            {
                return string.IsNullOrWhiteSpace(textContent) ? null : textContent;
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
    /// Tracks the API call in the database for auditing and cost monitoring.
    /// </summary>
    private async Task TrackApiCallAsync(
        Data.Entities.PromptEntity promptEntity,
        string actualUserPrompt,
        string thumbnailUrl,
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

            // Create AI call record with thumbnail URL in the user prompt for reference
            var promptWithMetadata = $"Thumbnail URL: {thumbnailUrl}\n\n{actualUserPrompt}";

            await _aiCallDataService.CreateAICallAsync(
                promptId: promptEntity.Id,
                userId: userId,
                actualSystemPrompt: promptEntity.SystemPrompt ?? "You are an AI assistant that analyzes video thumbnails and provides descriptions.",
                actualUserPrompt: promptWithMetadata,
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
                $"User: {userId ?? "system"}, Tokens: {inputTokens + outputTokens}, Cost: ${totalCost:F6}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to track API call for user {userId ?? "system"}. This won't stop processing.");
            // Don't throw - tracking failure shouldn't stop analysis
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
    /// Note: This is a simple estimate and doesn't account for image tokens.
    /// </summary>
    private int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Length / 4;
    }

    /// <summary>
    /// Creates a failed result with error information.
    /// </summary>
    private ThumbnailAnalysisResult CreateFailedResult(string errorMessage, string? thumbnailUrl, string? videoTitle)
    {
        return new ThumbnailAnalysisResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ThumbnailUrl = thumbnailUrl,
            VideoTitle = videoTitle
        };
    }

    /// <summary>
    /// Creates a success result with the description and metadata.
    /// </summary>
    private ThumbnailAnalysisResult CreateSuccessResult(
        string description,
        int inputTokens,
        int outputTokens,
        decimal totalCost,
        long durationMs,
        string thumbnailUrl,
        string videoTitle)
    {
        return new ThumbnailAnalysisResult
        {
            Success = true,
            Description = description,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalCost = totalCost,
            DurationMs = (int)durationMs,
            ThumbnailUrl = thumbnailUrl,
            VideoTitle = videoTitle
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
