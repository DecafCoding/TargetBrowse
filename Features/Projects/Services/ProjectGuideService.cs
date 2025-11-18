using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Projects.Data;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;
using TargetBrowse.Services.ProjectServices.Models;

namespace TargetBrowse.Features.Projects.Services
{
    /// <summary>
    /// Implementation of project guide generation service.
    /// Handles AI-powered guide generation from video summaries with daily limit enforcement.
    /// </summary>
    public class ProjectGuideService : IProjectGuideService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly ApplicationDbContext _context;
        private readonly IPromptDataService _promptDataService;
        private readonly IAICallDataService _aiCallDataService;
        private readonly ProjectSettings _projectSettings;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProjectGuideService> _logger;
        private readonly string _apiKey;

        private const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";
        private const decimal DefaultTemperature = 0.7m;
        private const int DefaultMaxTokens = 4000;
        private const int DailyAICallLimit = 10;
        private const string GuidePromptName = "Create Guide";

        public ProjectGuideService(
            IProjectRepository projectRepository,
            ApplicationDbContext context,
            IPromptDataService promptDataService,
            IAICallDataService aiCallDataService,
            IOptions<ProjectSettings> projectSettings,
            IConfiguration configuration,
            ILogger<ProjectGuideService> logger)
        {
            _projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _promptDataService = promptDataService ?? throw new ArgumentNullException(nameof(promptDataService));
            _aiCallDataService = aiCallDataService ?? throw new ArgumentNullException(nameof(aiCallDataService));
            _projectSettings = projectSettings?.Value ?? throw new ArgumentNullException(nameof(projectSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey configuration is missing");

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Checks if a guide can be generated for a project.
        /// </summary>
        public async Task<bool> CanGenerateGuideAsync(Guid projectId, string userId)
        {
            try
            {
                // Check if daily limit exceeded
                var dailyCount = await GetDailyAICallCountAsync(userId);
                if (dailyCount >= DailyAICallLimit)
                {
                    return false;
                }

                // Check if project exists and has minimum videos
                var project = await _projectRepository.GetByIdAsync(projectId, userId);
                if (project == null)
                {
                    return false;
                }

                var videoCount = project.ProjectVideos.Count(pv => !pv.IsDeleted);
                if (videoCount < _projectSettings.MinVideosForGuide)
                {
                    return false;
                }

                // Check if all videos have summaries
                var hasAllSummaries = project.ProjectVideos
                    .Where(pv => !pv.IsDeleted)
                    .All(pv => pv.Video.Summary != null);

                return hasAllSummaries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if guide can be generated for project {ProjectId}", projectId);
                return false;
            }
        }

        /// <summary>
        /// Generates a new guide for a project using AI.
        /// </summary>
        public async Task<ProjectGuideEntity> GenerateGuideAsync(Guid projectId, string userId)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Starting guide generation for project {ProjectId}", projectId);

                // Check prerequisites
                if (!await CanGenerateGuideAsync(projectId, userId))
                {
                    var dailyCount = await GetDailyAICallCountAsync(userId);
                    if (dailyCount >= DailyAICallLimit)
                    {
                        throw new InvalidOperationException($"Daily AI call limit ({DailyAICallLimit}) exceeded. Try again tomorrow.");
                    }

                    throw new InvalidOperationException("Project does not meet prerequisites for guide generation. Ensure all videos have summaries and minimum video count is met.");
                }

                // Get project with all required data
                var project = await _context.Projects
                    .Include(p => p.ProjectVideos.Where(pv => !pv.IsDeleted))
                        .ThenInclude(pv => pv.Video)
                            .ThenInclude(v => v.Summary)
                    .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);

                if (project == null)
                {
                    throw new InvalidOperationException("Project not found");
                }

                // Get prompt template
                var promptEntity = await _promptDataService.GetActivePromptByNameAsync(GuidePromptName);
                if (promptEntity == null)
                {
                    throw new InvalidOperationException($"Prompt '{GuidePromptName}' not found or inactive");
                }

                // Build the prompt with video summaries
                var actualUserPrompt = BuildPromptWithVideoSummaries(project, promptEntity.UserPromptTemplate);

                // Create and send OpenAI request
                var request = CreateGuideRequest(promptEntity, actualUserPrompt);
                var response = await CallOpenAiApiAsync(request);

                stopwatch.Stop();

                // Track the API call
                var aiCall = await TrackApiCallAsync(
                    promptEntity,
                    actualUserPrompt,
                    response,
                    stopwatch.ElapsedMilliseconds,
                    userId,
                    response == null ? "Failed to get response from OpenAI API" : null);

                if (response == null)
                {
                    throw new InvalidOperationException("Failed to get response from OpenAI API");
                }

                // Parse response
                var guideContent = ParseGuideResponse(response);
                if (string.IsNullOrWhiteSpace(guideContent))
                {
                    throw new InvalidOperationException("Empty or invalid response from OpenAI API");
                }

                // Create project guide
                var videoCount = project.ProjectVideos.Count(pv => !pv.IsDeleted);
                var projectGuide = new ProjectGuideEntity
                {
                    ProjectId = projectId,
                    Content = guideContent,
                    AICallId = aiCall.Id,
                    UserGuidanceSnapshot = project.UserGuidance,
                    VideoCount = videoCount,
                    GeneratedAt = DateTime.UtcNow
                };

                _context.ProjectGuides.Add(projectGuide);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Successfully created guide {GuideId} for project {ProjectId}. " +
                    "Duration: {DurationMs}ms",
                    projectGuide.Id,
                    projectId,
                    stopwatch.ElapsedMilliseconds);

                return projectGuide;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error generating guide for project {ProjectId}: {Message}", projectId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Regenerates an existing guide (soft deletes old one, creates new one).
        /// </summary>
        public async Task<ProjectGuideEntity> RegenerateGuideAsync(Guid projectId, string userId)
        {
            try
            {
                _logger.LogInformation("Starting guide regeneration for project {ProjectId}", projectId);

                // Get existing guide
                var existingGuide = await _context.ProjectGuides
                    .FirstOrDefaultAsync(pg => pg.ProjectId == projectId && !pg.IsDeleted);

                if (existingGuide != null)
                {
                    // Soft delete existing guide
                    existingGuide.IsDeleted = true;
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Soft deleted existing guide {GuideId} for project {ProjectId}", existingGuide.Id, projectId);
                }

                // Generate new guide
                return await GenerateGuideAsync(projectId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating guide for project {ProjectId}: {Message}", projectId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Checks if a guide needs regeneration based on changes.
        /// </summary>
        public async Task<bool> ShouldRegenerateGuideAsync(Guid projectId)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.ProjectGuide)
                    .Include(p => p.ProjectVideos.Where(pv => !pv.IsDeleted))
                    .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);

                if (project == null || project.ProjectGuide == null || project.ProjectGuide.IsDeleted)
                {
                    return false; // No guide to regenerate
                }

                var guide = project.ProjectGuide;

                // Check if user guidance changed
                var guidanceChanged = guide.UserGuidanceSnapshot != project.UserGuidance;

                // Check if video count changed
                var currentVideoCount = project.ProjectVideos.Count(pv => !pv.IsDeleted);
                var videoCountChanged = guide.VideoCount != currentVideoCount;

                return guidanceChanged || videoCountChanged;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if guide should be regenerated for project {ProjectId}", projectId);
                return false;
            }
        }

        /// <summary>
        /// Gets the count of AI calls made by user today (summaries + guides).
        /// </summary>
        public async Task<int> GetDailyAICallCountAsync(string userId)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                // Count summary generation requests today
                var summaryCount = await _context.SummaryGenerationRequests
                    .Where(sgr => sgr.UserId == userId && sgr.RequestedAt >= today && sgr.RequestedAt < tomorrow)
                    .CountAsync();

                // Count project guide AI calls today
                var guideCount = await _context.AICalls
                    .Where(ac => ac.UserId == userId && ac.CreatedAt >= today && ac.CreatedAt < tomorrow)
                    .Join(_context.Prompts,
                        ac => ac.PromptId,
                        p => p.Id,
                        (ac, p) => new { ac, p })
                    .Where(x => x.p.Name == GuidePromptName)
                    .CountAsync();

                return summaryCount + guideCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily AI call count for user {UserId}", userId);
                return 0;
            }
        }

        /// <summary>
        /// Builds the prompt with video summaries dynamically.
        /// </summary>
        private string BuildPromptWithVideoSummaries(ProjectEntity project, string? templatePrompt)
        {
            if (string.IsNullOrWhiteSpace(templatePrompt))
            {
                throw new InvalidOperationException("Prompt template is empty");
            }

            var prompt = templatePrompt;

            // Replace project-level variables
            prompt = prompt.Replace("[project-name]", project.Name ?? "");
            prompt = prompt.Replace("[project-description]", project.Description ?? "");
            prompt = prompt.Replace("[user-guidance]", project.UserGuidance ?? "");

            // Get videos in order
            var orderedVideos = project.ProjectVideos
                .Where(pv => !pv.IsDeleted)
                .OrderBy(pv => pv.Order)
                .Select(pv => pv.Video)
                .ToList();

            // Replace video variables dynamically
            for (int i = 0; i < orderedVideos.Count; i++)
            {
                var video = orderedVideos[i];
                var videoNumber = i + 1;

                prompt = prompt.Replace($"[video-{videoNumber}-title]", video.Title ?? "");
                prompt = prompt.Replace($"[video-{videoNumber}-summary]", video.Summary?.Content ?? "");
            }

            return prompt;
        }

        /// <summary>
        /// Creates the OpenAI API request for guide generation.
        /// </summary>
        private OpenAiRequest CreateGuideRequest(PromptEntity promptEntity, string actualUserPrompt)
        {
            return new OpenAiRequest
            {
                Model = promptEntity.Model?.Name ?? "gpt-4o-mini",
                Messages = new List<OpenAiMessage>
                {
                    new OpenAiMessage
                    {
                        Role = "system",
                        Content = promptEntity.SystemPrompt ?? "You are a helpful assistant that creates comprehensive guides from video summaries."
                    },
                    new OpenAiMessage
                    {
                        Role = "user",
                        Content = actualUserPrompt
                    }
                },
                MaxTokens = promptEntity.MaxTokens ?? DefaultMaxTokens,
                Temperature = promptEntity.Temperature ?? DefaultTemperature,
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
                var jsonRequest = JsonConvert.SerializeObject(request);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(OpenAiApiUrl, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenAI API request failed: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<OpenAiResponse>(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Parses the OpenAI response and extracts the guide content.
        /// </summary>
        private string? ParseGuideResponse(OpenAiResponse response)
        {
            try
            {
                if (response.Choices == null || !response.Choices.Any())
                {
                    _logger.LogWarning("No choices returned from OpenAI API");
                    return null;
                }

                var messageContent = response.Choices.First().Message.Content;

                if (messageContent is string textContent)
                {
                    if (string.IsNullOrWhiteSpace(textContent))
                    {
                        _logger.LogWarning("Empty response content from OpenAI API");
                        return null;
                    }
                    return textContent;
                }

                _logger.LogWarning("Unexpected response content format from OpenAI API");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing OpenAI response: {Message}", ex.Message);
                return null;
            }
        }

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
                    actualSystemPrompt: promptEntity.SystemPrompt ?? "You are a helpful assistant that creates comprehensive guides from video summaries.",
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
                    "AI call tracked {StatusLog}. User: {UserId}, Tokens: {TotalTokens}, Cost: ${TotalCost:F6}",
                    statusLog,
                    userId,
                    inputTokens + outputTokens,
                    totalCost);

                return aiCall;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track API call for user {UserId}", userId);
                throw;
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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
