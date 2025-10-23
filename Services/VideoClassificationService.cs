using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using TargetBrowse.Services.DataServices;
using TargetBrowse.Services.Models;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Services;

/// <summary>
/// Service for classifying video titles into categories using OpenAI API
/// Follows the same pattern as TranscriptSummaryService
/// </summary>
public class VideoClassificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VideoClassificationService> _logger;
    private readonly PromptDataService _promptDataService;
    private readonly string _apiKey;
    private const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string PromptName = "Video Type Classification";
    private const int BatchSize = 25;

    public VideoClassificationService(
        IConfiguration configuration,
        ILogger<VideoClassificationService> logger,
        PromptDataService promptDataService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _promptDataService = promptDataService ?? throw new ArgumentNullException(nameof(promptDataService));

        _apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey configuration is missing");

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Classifies a list of video titles into predefined categories
    /// </summary>
    /// <param name="videos">List of videos with IDs and titles to classify</param>
    /// <returns>Classification result containing category assignments for each video</returns>
    public async Task<ClassificationResult> ClassifyVideoTitlesAsync(List<VideoInput> videos)
    {
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

            _logger.LogInformation($"Classifying {validVideos.Count} videos into categories");

            // Retrieve the active prompt from database
            var promptEntity = await _promptDataService.GetActivePromptByNameAsync(PromptName);
            if (promptEntity == null)
            {
                _logger.LogError($"Active prompt '{PromptName}' not found in database");
                return CreateFailedResult($"Classification prompt '{PromptName}' not found or inactive");
            }

            _logger.LogInformation($"Using prompt: {PromptName} with model: {promptEntity.Model}");

            // Process videos in batches
            var allClassifications = new List<VideoClassification>();
            var batches = CreateBatches(validVideos, BatchSize);

            foreach (var batch in batches)
            {
                var batchResult = await ProcessBatchAsync(batch, promptEntity);
                if (batchResult.Success)
                {
                    allClassifications.AddRange(batchResult.Classifications);
                }
                else
                {
                    _logger.LogWarning($"Batch classification failed: {batchResult.ErrorMessage}");
                }
            }

            // Create final result with statistics
            var result = new ClassificationResult
            {
                Success = allClassifications.Any(),
                Classifications = allClassifications,
                TotalVideos = validVideos.Count,
                SuccessfulClassifications = allClassifications.Count,
                CategoryCounts = CalculateCategoryCounts(allClassifications)
            };

            if (!result.Success)
            {
                result.ErrorMessage = "Failed to classify any videos";
            }

            _logger.LogInformation($"Successfully classified {result.SuccessfulClassifications} out of {result.TotalVideos} videos");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error classifying videos: {ex.Message}");
            return CreateFailedResult($"Classification failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a single batch of videos for classification
    /// </summary>
    private async Task<ClassificationResult> ProcessBatchAsync(List<VideoInput> batch, PromptEntity promptEntity)
    {
        try
        {
            // Format the video list for the prompt
            var videoListText = FormatVideoListForPrompt(batch);

            // Create the classification request
            var request = CreateClassificationRequest(videoListText, promptEntity);

            // Send request to OpenAI
            var response = await CallOpenAiApiAsync(request);

            if (response == null)
            {
                return CreateFailedResult("Failed to get response from OpenAI API");
            }

            // Parse and validate the response
            var result = ParseOpenAiResponse(response, batch);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing batch: {ex.Message}");
            return CreateFailedResult($"Batch processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Formats the video list into a text format suitable for the prompt
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
    /// Creates the OpenAI API request using the prompt from database
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
    /// Sends the request to OpenAI API and handles the response
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
    /// Parses the OpenAI response and extracts classification information
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
    /// Splits videos into batches for processing
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
    /// Calculates category distribution statistics
    /// </summary>
    private Dictionary<string, int> CalculateCategoryCounts(List<VideoClassification> classifications)
    {
        return classifications
            .GroupBy(c => c.Code)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Creates a failed ClassificationResult with error information
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
