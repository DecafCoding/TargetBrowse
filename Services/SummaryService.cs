using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Services
{
    public class SummaryService : ISummaryService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SummaryService> _logger;
        private readonly string _apiKey;
        private const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";

        public SummaryService(IConfiguration configuration, ILogger<SummaryService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey configuration is missing");

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Analyzes a video transcript using OpenAI API and returns structured summary
        /// </summary>
        /// <param name="transcript">Raw video transcript text</param>
        /// <param name="videoId">YouTube video ID for logging</param>
        /// <param name="videoTitle">Video title for context</param>
        /// <returns>Summary result with video topic, main summary, and structured content</returns>
        public async Task<SummaryResult> AnalyzeTranscriptAsync(string transcript, string videoId, string videoTitle)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(transcript))
                {
                    _logger.LogWarning($"Empty transcript provided for video {videoId}");
                    return CreateFailedResult("Transcript is empty or null", videoId, videoTitle);
                }

                _logger.LogInformation($"Analyzing transcript for video: {videoTitle} ({videoId}) using model: {Prompts.AnalyzeTranscript.ModelConfig.Model}");

                // Truncate transcript if too long (OpenAI has token limits)
                var processedTranscript = TruncateTranscriptIfNeeded(transcript);

                // Create the analysis request using centralized configuration
                var request = CreateAnalysisRequest(processedTranscript);

                // Send request to OpenAI
                var response = await CallOpenAiApiAsync(request);

                if (response == null)
                {
                    return CreateFailedResult("Failed to get response from OpenAI API", videoId, videoTitle);
                }

                // Parse and validate the response
                var summaryResult = ParseOpenAiResponse(response, videoId, videoTitle);

                if (summaryResult.Success)
                {
                    _logger.LogInformation($"Successfully analyzed transcript for video: {videoTitle} using {Prompts.AnalyzeTranscript.ModelConfig.Model}");
                }
                else
                {
                    _logger.LogWarning($"Failed to analyze transcript for video: {videoTitle} - {summaryResult.ErrorMessage}");
                }

                return summaryResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error analyzing transcript for video {videoId}: {ex.Message}");
                return CreateFailedResult($"Analysis failed: {ex.Message}", videoId, videoTitle);
            }
        }

        /// <summary>
        /// Creates the OpenAI API request with the analysis prompt using centralized configuration
        /// </summary>
        private OpenAiRequest CreateAnalysisRequest(string transcript)
        {
            var formattedPrompt = Prompts.AnalyzeTranscript.GetFormattedPrompt(transcript);

            return new OpenAiRequest
            {
                Model = Prompts.AnalyzeTranscript.ModelConfig.Model,
                Messages = new List<OpenAiMessage>
            {
                new OpenAiMessage
                {
                    Role = "system",
                    Content = Prompts.AnalyzeTranscript.ModelConfig.SystemMessage
                },
                new OpenAiMessage
                {
                    Role = "user",
                    Content = formattedPrompt
                }
            },
                MaxTokens = Prompts.AnalyzeTranscript.ModelConfig.MaxTokens,
                Temperature = Prompts.AnalyzeTranscript.ModelConfig.Temperature,
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
        /// Parses the OpenAI response and extracts the summary information
        /// </summary>
        private SummaryResult ParseOpenAiResponse(OpenAiResponse response, string videoId, string videoTitle)
        {
            try
            {
                if (response.Choices == null || !response.Choices.Any())
                {
                    return CreateFailedResult("No choices returned from OpenAI API", videoId, videoTitle);
                }

                var messageContent = response.Choices.First().Message.Content;

                if (string.IsNullOrWhiteSpace(messageContent))
                {
                    return CreateFailedResult("Empty response content from OpenAI API", videoId, videoTitle);
                }

                // Parse the JSON response
                var summaryData = JsonConvert.DeserializeObject<SummaryResult>(messageContent);

                if (summaryData == null)
                {
                    return CreateFailedResult("Failed to parse JSON response from OpenAI", videoId, videoTitle);
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(summaryData.VideoTopic) ||
                    string.IsNullOrWhiteSpace(summaryData.MainSummary))
                {
                    return CreateFailedResult("Incomplete summary data returned from OpenAI", videoId, videoTitle);
                }

                // Set additional fields and truncate if necessary
                summaryData.Success = true;
                summaryData.VideoId = videoId;
                summaryData.VideoTitle = videoTitle;
                summaryData.VideoTopic = TruncateString(summaryData.VideoTopic, 200);
                summaryData.MainSummary = TruncateString(summaryData.MainSummary, 2000);
                summaryData.StructuredContent = summaryData.StructuredContent ?? string.Empty;

                return summaryData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing OpenAI response: {ex.Message}");
                return CreateFailedResult($"Failed to parse response: {ex.Message}", videoId, videoTitle);
            }
        }

        /// <summary>
        /// Truncates transcript if it exceeds reasonable limits for API processing
        /// </summary>
        private string TruncateTranscriptIfNeeded(string transcript)
        {
            // Approximate token limit - OpenAI recommends staying well under limits
            const int maxCharacters = 100000; // Roughly 1 hr of video transcript

            if (transcript.Length <= maxCharacters)
                return transcript;

            _logger.LogInformation($"Truncating transcript from {transcript.Length} to {maxCharacters} characters");

            // Try to truncate at a sentence boundary
            var truncated = transcript.Substring(0, maxCharacters);
            var lastPeriod = truncated.LastIndexOf(". ");

            if (lastPeriod > maxCharacters * 0.8) // Only use sentence boundary if it's not too far back
            {
                return truncated.Substring(0, lastPeriod + 1);
            }

            return truncated + "...";
        }

        /// <summary>
        /// Truncates string to specified length while preserving word boundaries
        /// </summary>
        private string TruncateString(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input ?? string.Empty;

            // Find the last space before the max length to avoid cutting words
            var truncated = input.Substring(0, maxLength);
            var lastSpace = truncated.LastIndexOf(' ');

            if (lastSpace > 0 && lastSpace > maxLength * 0.8) // Only use word boundary if it's not too far back
                return truncated.Substring(0, lastSpace) + "...";

            return truncated + "...";
        }

        /// <summary>
        /// Creates a failed SummaryResult with error information
        /// </summary>
        private SummaryResult CreateFailedResult(string errorMessage, string videoId, string videoTitle)
        {
            return new SummaryResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                VideoId = videoId,
                VideoTitle = videoTitle
            };
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
