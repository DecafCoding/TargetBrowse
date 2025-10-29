using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.DataServices;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Services;

/// <summary>
/// Service for classifying video titles into categories using OpenAI API.
/// Handles the complete workflow: prompt retrieval, API calls, and AI call logging.
/// </summary>
public class VideoTitleClassificationService : IVideoTitleClassificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VideoTitleClassificationService> _logger;
    private readonly IPromptDataService _promptDataService;
    private readonly IAICallDataService _aiCallDataService;
    private readonly string _apiKey;
    private const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string PromptName = "Video Type Classification";
    private const int BatchSize = 25;

    // Token cost estimation
    // private decimal InputCostPerToken = default;
    // private decimal OutputCostPerToken = default;

    public VideoTitleClassificationService(
        IConfiguration configuration,
        ILogger<VideoTitleClassificationService> logger,
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
    /// Classifies a list of video titles into predefined categories.
    /// Retrieves prompt, calls OpenAI API, logs the call, and returns results.
    /// </summary>
    public async Task<ClassificationResult> ClassifyVideoTitlesAsync(List<VideoInput> videos, string userId)
    {
        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            if (videos == null || !videos.Any())
            {
                _logger.LogWarning("Empty or null video list provided for classification");
                return CreateFailedResult("No videos provided for classification");
            }

            // Validate video inputs
            var validVideos = videos.Where(v => !string.IsNullOrWhiteSpace(v.VideoId) && !string.IsNullOrWhiteSpace(v.Title)).ToList();
            if (!validVideos.Any())
            {
                _logger.LogWarning("No valid videos found in the provided list");
                return CreateFailedResult("No valid videos with IDs and titles found");
            }

            _logger.LogInformation($"Classifying {validVideos.Count} videos into categories for user {userId}");

            // Step 1: Retrieve the active prompt from database
            var promptEntity = await _promptDataService.GetActivePromptByNameAsync(PromptName);
            if (promptEntity == null)
            {
                _logger.LogError($"Active prompt '{PromptName}' not found in database");
                return CreateFailedResult($"Classification prompt '{PromptName}' not found or inactive");
            }

            _logger.LogInformation($"Using prompt: {PromptName} with model: {promptEntity.Model.Name}");

            // Step 2: Process videos in batches and call OpenAI API
            // Each batch is tracked separately as an individual API call
            var allClassifications = new List<VideoClassification>();
            var totalInputTokens = 0;
            var totalOutputTokens = 0;
            var totalCost = 0m;
            var batches = CreateBatches(validVideos, BatchSize);

            foreach (var batch in batches)
            {
                var batchResult = await ProcessBatchAsync(batch, promptEntity, userId);
                if (batchResult.Success)
                {
                    allClassifications.AddRange(batchResult.Classifications);
                    totalInputTokens += batchResult.InputTokens;
                    totalOutputTokens += batchResult.OutputTokens;
                    totalCost += batchResult.TotalCost;
                }
                else
                {
                    _logger.LogWarning($"Batch classification failed: {batchResult.ErrorMessage}");
                }
            }

            overallStopwatch.Stop();

            // Create final result with statistics and metadata
            var result = new ClassificationResult
            {
                Success = allClassifications.Any(),
                Classifications = allClassifications,
                TotalVideos = validVideos.Count,
                SuccessfulClassifications = allClassifications.Count,
                CategoryCounts = CalculateCategoryCounts(allClassifications),
                InputTokens = totalInputTokens,
                OutputTokens = totalOutputTokens,
                TotalCost = totalCost,
                DurationMs = (int)overallStopwatch.ElapsedMilliseconds
            };

            if (!result.Success)
            {
                result.ErrorMessage = "Failed to classify any videos";
            }

            _logger.LogInformation(
                $"Successfully classified {result.SuccessfulClassifications} out of {result.TotalVideos} videos. " +
                $"Tokens: {result.InputTokens} input, {result.OutputTokens} output. Cost: ${result.TotalCost:F6}");

            return result;
        }
        catch (Exception ex)
        {
            overallStopwatch.Stop();
            _logger.LogError(ex, $"Error classifying videos: {ex.Message}");
            return CreateFailedResult($"Classification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a single batch of videos for classification.
    /// Tracks each batch as a separate API call immediately after the HTTP request.
    /// </summary>
    private async Task<ClassificationResult> ProcessBatchAsync(List<VideoInput> batch, PromptEntity promptEntity, string userId)
    {
        var stopwatch = Stopwatch.StartNew();
        OpenAiResponse? response = null;
        string videoListText = string.Empty;
        string actualUserPrompt = string.Empty;

        try
        {
            // Format the video list for the prompt
            videoListText = FormatVideoListForPrompt(batch);

            // Create the classification request
            var request = CreateClassificationRequest(videoListText, promptEntity);

            // Store the actual user prompt for tracking
            actualUserPrompt = promptEntity.UserPromptTemplate.Replace("{VIDEO_LIST}", videoListText);

            // Send request to OpenAI and track timing
            response = await CallOpenAiApiAsync(request);
            stopwatch.Stop();

            // Track API call immediately after HTTP request completes
            await TrackBatchApiCallAsync(
                promptEntity,
                batch,
                response,
                actualUserPrompt,
                stopwatch.ElapsedMilliseconds,
                userId
            );

            if (response == null)
            {
                return CreateFailedResult("Failed to get response from OpenAI API");
            }

            // Parse the response
            var result = ParseOpenAiResponse(response, batch);

            // Calculate token usage and cost from actual API response
            if (response.Usage != null)
            {
                result.InputTokens = response.Usage.PromptTokens;
                result.OutputTokens = response.Usage.CompletionTokens;
                result.TotalCost = CalculateCost(response.Usage.PromptTokens, promptEntity.Model.CostPer1kInputTokens, response.Usage.CompletionTokens, promptEntity.Model.CostPer1kOutputTokens);
            }
            else
            {
                // Fallback to estimation if usage data not available
                var estimatedInput = EstimateTokenCount(promptEntity.SystemPrompt + videoListText);
                var estimatedOutput = EstimateTokenCount(JsonConvert.SerializeObject(result.Classifications));
                result.InputTokens = estimatedInput;
                result.OutputTokens = estimatedOutput;
                result.TotalCost = CalculateCost(estimatedInput, promptEntity.Model.CostPer1kInputTokens, estimatedOutput, promptEntity.Model.CostPer1kOutputTokens);
            }

            result.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Track failed API call with partial data
            await TrackBatchApiCallAsync(
                promptEntity,
                batch,
                response,
                actualUserPrompt,
                stopwatch.ElapsedMilliseconds,
                userId,
                errorMessage: ex.Message
            );

            _logger.LogError(ex, $"Error processing batch: {ex.Message}");
            return CreateFailedResult($"Batch processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tracks a single batch API call immediately after the HTTP request.
    /// Captures both successful and failed calls with partial data when needed.
    /// </summary>
    private async Task TrackBatchApiCallAsync(
        PromptEntity promptEntity,
        List<VideoInput> batch,
        OpenAiResponse? response,
        string actualUserPrompt,
        long durationMs,
        string userId,
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
                    promptEntity.Model.CostPer1kOutputTokens
                );

                // Capture response content if available
                if (response.Choices?.Any() == true)
                {
                    responseContent = response.Choices.First().Message.Content ?? string.Empty;
                }
            }
            else
            {
                // Estimate tokens for failed or incomplete calls
                var videoListText = FormatVideoListForPrompt(batch);
                inputTokens = EstimateTokenCount(promptEntity.SystemPrompt + actualUserPrompt);
                outputTokens = EstimateTokenCount(responseContent);
                totalCost = CalculateCost(
                    inputTokens,
                    promptEntity.Model.CostPer1kInputTokens,
                    outputTokens,
                    promptEntity.Model.CostPer1kOutputTokens
                );
            }

            // Create AI call record for this batch
            await _aiCallDataService.CreateAICallAsync(
                promptId: promptEntity.Id,
                userId: userId,
                actualSystemPrompt: promptEntity.SystemPrompt ?? "You are a video content classification assistant.",
                actualUserPrompt: actualUserPrompt,
                response: string.IsNullOrEmpty(responseContent) ? $"Batch of {batch.Count} videos" : responseContent,
                inputTokens: inputTokens,
                outputTokens: outputTokens,
                totalCost: totalCost,
                durationMs: (int)durationMs,
                success: success,
                errorMessage: errorMessage
            );

            var statusLog = success ? "successfully" : "with errors";
            _logger.LogInformation(
                $"AI call tracked {statusLog} for batch of {batch.Count} videos. " +
                $"User: {userId}, Tokens: {inputTokens + outputTokens}, Cost: ${totalCost:F6}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to track batch API call for user {userId}. This won't stop processing.");
            // Don't throw - tracking failure shouldn't stop classification
        }
    }

    /// <summary>
    /// Formats the video list into a text format suitable for the prompt.
    /// </summary>
    private string FormatVideoListForPrompt(List<VideoInput> videos)
    {
        var sb = new StringBuilder();
        foreach (var video in videos)
        {
            sb.AppendLine($"- Video ID: {video.VideoId}, Title: \"{video.Title}\"");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Creates the OpenAI API request using the prompt from database.
    /// </summary>
    private OpenAiRequest CreateClassificationRequest(string videoListText, PromptEntity promptEntity)
    {
        // Insert the video list into the prompt template
        var formattedPrompt = promptEntity.UserPromptTemplate.Replace("{VIDEO_LIST}", videoListText);

        return new OpenAiRequest
        {
            Model = promptEntity.Model.Name ?? "gpt-4o-mini",
            Messages = new List<OpenAiMessage>
            {
                new OpenAiMessage
                {
                    Role = "system",
                    Content = promptEntity.SystemPrompt ?? "You are a video content classification assistant."
                },
                new OpenAiMessage
                {
                    Role = "user",
                    Content = formattedPrompt
                }
            },
            MaxTokens = promptEntity.MaxTokens ?? 2000,
            Temperature = promptEntity.Temperature ?? 0.3m,
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
    /// Parses the OpenAI response and extracts classification information.
    /// </summary>
    private ClassificationResult ParseOpenAiResponse(OpenAiResponse response, List<VideoInput> originalVideos)
    {
        try
        {
            if (response.Choices == null || !response.Choices.Any())
            {
                return CreateFailedResult("No choices returned from OpenAI API");
            }

            var messageContent = response.Choices.First().Message.Content;

            if (string.IsNullOrWhiteSpace(messageContent))
            {
                return CreateFailedResult("Empty response content from OpenAI API");
            }

            // Parse the JSON response
            var classificationData = JsonConvert.DeserializeObject<ClassificationResult>(messageContent);

            if (classificationData == null || classificationData.Classifications == null)
            {
                return CreateFailedResult("Failed to parse JSON response from OpenAI");
            }

            // Validate that we got classifications
            if (!classificationData.Classifications.Any())
            {
                return CreateFailedResult("No classifications returned from OpenAI");
            }

            // Validate category codes
            var validCodes = new HashSet<string> { "LIST", "TUTORIAL", "REVIEW", "STORY", "EDUCATIONAL", "SHOWCASE", "ENTERTAINMENT" };
            foreach (var classification in classificationData.Classifications)
            {
                if (!validCodes.Contains(classification.Code))
                {
                    _logger.LogWarning($"Invalid category code '{classification.Code}' for video {classification.VideoId}. Setting to EDUCATIONAL as default.");
                    classification.Code = "EDUCATIONAL";
                }
            }

            classificationData.Success = true;
            classificationData.TotalVideos = originalVideos.Count;
            classificationData.SuccessfulClassifications = classificationData.Classifications.Count;
            classificationData.CategoryCounts = CalculateCategoryCounts(classificationData.Classifications);

            return classificationData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error parsing OpenAI response: {ex.Message}");
            return CreateFailedResult($"Failed to parse response: {ex.Message}");
        }
    }

    /// <summary>
    /// Splits videos into batches for processing.
    /// </summary>
    private List<List<VideoInput>> CreateBatches(List<VideoInput> videos, int batchSize)
    {
        var batches = new List<List<VideoInput>>();
        for (int i = 0; i < videos.Count; i += batchSize)
        {
            batches.Add(videos.Skip(i).Take(batchSize).ToList());
        }
        return batches;
    }

    /// <summary>
    /// Calculates category distribution statistics.
    /// </summary>
    private Dictionary<string, int> CalculateCategoryCounts(List<VideoClassification> classifications)
    {
        return classifications
            .GroupBy(c => c.Code)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Calculates the cost of an API call based on token usage.
    /// Uses gpt-4o-mini pricing.
    /// </summary>
    private decimal CalculateCost(int inputTokens, decimal inputcostper1k, int outputTokens, decimal outputcostper1k)
    {
        return ((inputTokens * (inputcostper1k / 1000))) + (outputTokens * (outputcostper1k / 1000));
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
    /// Creates a failed ClassificationResult with error information.
    /// </summary>
    private ClassificationResult CreateFailedResult(string errorMessage)
    {
        return new ClassificationResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            Classifications = new List<VideoClassification>(),
            TotalVideos = 0,
            SuccessfulClassifications = 0
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}