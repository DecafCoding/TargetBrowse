using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Projects.Models;
using TargetBrowse.Services.AI;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Projects.Services
{
    /// <summary>
    /// Service for generating video scripts from project videos.
    /// Handles analysis, outline generation, and final script creation.
    /// </summary>
    public class ScriptGenerationService : IScriptGenerationService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ScriptGenerationService> _logger;
        private readonly IPromptDataService _promptDataService;
        private readonly IAICallDataService _aiCallDataService;
        private readonly string _apiKey;
        private const string OpenAiApiUrl = "https://api.openai.com/v1/chat/completions";
        private const int DailyAICallLimit = 10;

        // MVP: Hard-coded model settings for script analysis
        private const string DefaultModel = "gpt-4o-mini";
        private const decimal InputCostPer1kTokens = 0.000150m; // gpt-4o-mini pricing
        private const decimal OutputCostPer1kTokens = 0.000600m;

        // MVP: Placeholder prompt ID for script generation (created on first use)
        private static Guid? _scriptPromptId = null;
        private static readonly SemaphoreSlim _promptLock = new SemaphoreSlim(1, 1);

        public ScriptGenerationService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<ScriptGenerationService> logger,
            IPromptDataService promptDataService,
            IAICallDataService aiCallDataService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
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
        /// Analyzes videos in a project to identify themes, conflicts, and cohesion.
        /// </summary>
        public async Task<ScriptAnalysisResult> AnalyzeProjectVideosAsync(Guid projectId, string userId)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Starting script analysis for project {projectId}, user {userId}");

                // Step 1: Validate project and get videos with summaries
                var project = await _context.Projects
                    .Include(p => p.ProjectVideos)
                        .ThenInclude(pv => pv.Video)
                            .ThenInclude(v => v.Summary)
                    .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);

                if (project == null)
                {
                    return CreateFailedResult("Project not found");
                }

                if (project.ProjectVideos.Count < 3)
                {
                    return CreateFailedResult("Project must have at least 3 videos");
                }

                var videoSummaries = project.ProjectVideos
                    .Where(pv => pv.Video?.Summary != null)
                    .Select(pv => new VideoSummaryData
                    {
                        VideoId = pv.Video.YouTubeVideoId,
                        Title = pv.Video.Title,
                        Summary = pv.Video.Summary!.Content
                    })
                    .ToList();

                if (videoSummaries.Count < project.ProjectVideos.Count)
                {
                    return CreateFailedResult("All videos must have summaries");
                }

                // Step 2: Check daily limit
                var dailyCount = await GetDailyAICallCountAsync(userId);
                if (dailyCount >= DailyAICallLimit)
                {
                    return CreateFailedResult($"Daily AI call limit reached ({DailyAICallLimit}/day)");
                }

                // Step 3: Build analysis prompt
                var analysisPrompt = ScriptPromptBuilder.BuildAnalysisPrompt(videoSummaries);

                // Step 4: Call OpenAI API
                var request = CreateAnalysisRequest(analysisPrompt);
                var response = await CallOpenAiApiAsync(request);

                stopwatch.Stop();

                // Step 5: Track API call
                var aiCall = await TrackApiCallAsync(
                    analysisPrompt,
                    response,
                    stopwatch.ElapsedMilliseconds,
                    userId,
                    response == null ? "Failed to get response from OpenAI API" : null);

                if (response == null)
                {
                    return CreateFailedResult("Failed to get response from OpenAI API");
                }

                // Step 6: Parse response
                var analysis = ParseAnalysisResponse(response);
                if (analysis == null)
                {
                    return CreateFailedResult("Failed to parse analysis response");
                }

                // Step 7: Create or update ScriptContent entity
                await SaveAnalysisToScriptContentAsync(projectId, analysis, aiCall.Id, project.ProjectVideos.Count);

                // Step 8: Calculate costs
                var inputTokens = response.Usage?.PromptTokens ?? EstimateTokenCount(analysisPrompt);
                var outputTokens = response.Usage?.CompletionTokens ?? EstimateTokenCount(JsonConvert.SerializeObject(analysis));
                var totalCost = CalculateCost(inputTokens, outputTokens);

                _logger.LogInformation(
                    $"Successfully analyzed project {projectId}. " +
                    $"Tokens: {inputTokens} input, {outputTokens} output. Cost: ${totalCost:F6}");

                return new ScriptAnalysisResult
                {
                    Success = true,
                    Analysis = analysis,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    TotalCost = totalCost,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, $"Error analyzing project {projectId}: {ex.Message}");

                // Include inner exception details for debugging
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner Exception: {ex.InnerException.Message}";
                }

                return CreateFailedResult($"Analysis failed: {errorMessage}");
            }
        }

        /// <summary>
        /// Configures script generation settings.
        /// </summary>
        public async Task<ScriptConfigurationResult> ConfigureScriptAsync(Guid projectId, string userId, int targetLengthMinutes)
        {
            try
            {
                // Verify project exists
                var project = await _context.Projects
                    .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);

                if (project == null)
                {
                    return new ScriptConfigurationResult
                    {
                        Success = false,
                        ErrorMessage = "Project not found"
                    };
                }

                // Verify user has script profile
                var profile = await _context.UserScriptProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

                if (profile == null)
                {
                    return new ScriptConfigurationResult
                    {
                        Success = false,
                        ErrorMessage = "You must set up your script profile before configuring script generation"
                    };
                }

                // Get script content
                var scriptContent = await _context.ScriptContents
                    .FirstOrDefaultAsync(sc => sc.ProjectId == projectId && !sc.IsDeleted);

                if (scriptContent == null)
                {
                    return new ScriptConfigurationResult
                    {
                        Success = false,
                        ErrorMessage = "No analysis found. Please run analysis first."
                    };
                }

                // Validate target length
                if (targetLengthMinutes < 5 || targetLengthMinutes > 30)
                {
                    return new ScriptConfigurationResult
                    {
                        Success = false,
                        ErrorMessage = "Target length must be between 5 and 30 minutes"
                    };
                }

                // Update configuration
                scriptContent.TargetLengthMinutes = targetLengthMinutes;
                scriptContent.ScriptStatus = "Configured";

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Configured script for project {projectId} with target length {targetLengthMinutes} minutes");

                return new ScriptConfigurationResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error configuring script for project {projectId}");

                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner Exception: {ex.InnerException.Message}";
                }

                return new ScriptConfigurationResult
                {
                    Success = false,
                    ErrorMessage = $"Configuration failed: {errorMessage}"
                };
            }
        }

        /// <summary>
        /// Generates a script outline based on analysis and user preferences.
        /// </summary>
        public async Task<ScriptOutlineResult> GenerateOutlineAsync(Guid projectId, string userId)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Starting outline generation for project {projectId}, user {userId}");

                // Step 1: Get script content with analysis
                var scriptContent = await _context.ScriptContents
                    .FirstOrDefaultAsync(sc => sc.ProjectId == projectId && !sc.IsDeleted);

                if (scriptContent == null || string.IsNullOrEmpty(scriptContent.AnalysisJsonResult))
                {
                    return CreateFailedOutlineResult("No analysis found. Please run analysis first.");
                }

                if (!scriptContent.TargetLengthMinutes.HasValue)
                {
                    return CreateFailedOutlineResult("Script not configured. Please configure script settings first.");
                }

                // Step 2: Get user profile
                var profile = await _context.UserScriptProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

                if (profile == null)
                {
                    return CreateFailedOutlineResult("User script profile not found. Please set up your profile.");
                }

                // Step 3: Check daily limit
                var dailyCount = await GetDailyAICallCountAsync(userId);
                if (dailyCount >= DailyAICallLimit)
                {
                    return CreateFailedOutlineResult($"Daily AI call limit reached ({DailyAICallLimit}/day)");
                }

                // Step 4: Build outline prompt
                var userProfileData = new UserProfileData
                {
                    Tone = profile.Tone,
                    Pacing = profile.Pacing,
                    Complexity = profile.Complexity,
                    StructureStyle = profile.StructureStyle,
                    HookStrategy = profile.HookStrategy,
                    AudienceRelationship = profile.AudienceRelationship,
                    InformationDensity = profile.InformationDensity,
                    RhetoricalStyle = profile.RhetoricalStyle,
                    CustomInstructions = profile.CustomInstructions
                };

                var outlinePrompt = ScriptPromptBuilder.BuildOutlinePrompt(
                    scriptContent.AnalysisJsonResult,
                    userProfileData,
                    scriptContent.TargetLengthMinutes.Value);

                // Step 5: Call OpenAI API
                var request = CreateAnalysisRequest(outlinePrompt);
                var response = await CallOpenAiApiAsync(request);

                stopwatch.Stop();

                // Step 6: Track API call
                var aiCall = await TrackApiCallAsync(
                    outlinePrompt,
                    response,
                    stopwatch.ElapsedMilliseconds,
                    userId,
                    response == null ? "Failed to get response from OpenAI API" : null);

                if (response == null)
                {
                    return CreateFailedOutlineResult("Failed to get response from OpenAI API");
                }

                // Step 7: Parse response
                var outline = ParseOutlineResponse(response);
                if (outline == null)
                {
                    var parseDetail = _lastParseError != null ? $": {_lastParseError}" : "";
                    return CreateFailedOutlineResult($"Failed to parse outline response{parseDetail}");
                }

                // Step 8: Save outline to ScriptContent entity
                await SaveOutlineToScriptContentAsync(projectId, outline, aiCall.Id);

                // Step 9: Calculate costs
                var inputTokens = response.Usage?.PromptTokens ?? EstimateTokenCount(outlinePrompt);
                var outputTokens = response.Usage?.CompletionTokens ?? EstimateTokenCount(JsonConvert.SerializeObject(outline));
                var totalCost = CalculateCost(inputTokens, outputTokens);

                _logger.LogInformation(
                    $"Successfully generated outline for project {projectId}. " +
                    $"Tokens: {inputTokens} input, {outputTokens} output. Cost: ${totalCost:F6}");

                return new ScriptOutlineResult
                {
                    Success = true,
                    Outline = outline,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    TotalCost = totalCost,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, $"Error generating outline for project {projectId}: {ex.Message}");

                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner Exception: {ex.InnerException.Message}";
                }

                return CreateFailedOutlineResult($"Outline generation failed: {errorMessage}");
            }
        }

        /// <summary>
        /// Generates the final script based on outline and video transcripts (Phase 5).
        /// </summary>
        public async Task<ScriptGenerationResult> GenerateScriptAsync(Guid projectId, string userId)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Starting script generation for project {projectId}, user {userId}");

                // Step 1: Get script content with outline
                var scriptContent = await _context.ScriptContents
                    .FirstOrDefaultAsync(sc => sc.ProjectId == projectId && !sc.IsDeleted);

                if (scriptContent == null || string.IsNullOrEmpty(scriptContent.OutlineJsonStructure))
                {
                    return CreateFailedScriptResult("No outline found. Please generate an outline first.");
                }

                // Step 2: Get user profile for style preferences
                var profile = await _context.UserScriptProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

                if (profile == null)
                {
                    return CreateFailedScriptResult("User script profile not found. Please set up your profile.");
                }

                // Step 3: Get project videos with transcripts
                var project = await _context.Projects
                    .Include(p => p.ProjectVideos)
                        .ThenInclude(pv => pv.Video)
                            .ThenInclude(v => v.Summary)
                    .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);

                if (project == null)
                {
                    return CreateFailedScriptResult("Project not found");
                }

                // Step 4: Check daily limit
                var dailyCount = await GetDailyAICallCountAsync(userId);
                if (dailyCount >= DailyAICallLimit)
                {
                    return CreateFailedScriptResult($"Daily AI call limit reached ({DailyAICallLimit}/day)");
                }

                // Step 5: Build video transcripts list (use raw transcript, fallback to summary)
                var videoTranscripts = project.ProjectVideos
                    .Where(pv => pv.Video != null)
                    .Select(pv => new VideoTranscriptData
                    {
                        VideoId = pv.Video.YouTubeVideoId,
                        Title = pv.Video.Title,
                        Transcript = !string.IsNullOrEmpty(pv.Video.RawTranscript)
                            ? pv.Video.RawTranscript
                            : pv.Video.Summary?.Content ?? string.Empty
                    })
                    .Where(vt => !string.IsNullOrEmpty(vt.Transcript))
                    .ToList();

                // Step 6: Build script prompt
                var userProfileData = new UserProfileData
                {
                    Tone = profile.Tone,
                    Pacing = profile.Pacing,
                    Complexity = profile.Complexity,
                    StructureStyle = profile.StructureStyle,
                    HookStrategy = profile.HookStrategy,
                    AudienceRelationship = profile.AudienceRelationship,
                    InformationDensity = profile.InformationDensity,
                    RhetoricalStyle = profile.RhetoricalStyle,
                    CustomInstructions = profile.CustomInstructions
                };

                var scriptPrompt = ScriptPromptBuilder.BuildScriptPrompt(
                    scriptContent.OutlineJsonStructure,
                    userProfileData,
                    videoTranscripts);

                // Step 7: Call OpenAI API (higher token limit for full script)
                var request = CreateScriptRequest(scriptPrompt);
                var response = await CallOpenAiApiAsync(request);

                stopwatch.Stop();

                // Step 8: Track API call
                var aiCall = await TrackApiCallAsync(
                    scriptPrompt,
                    response,
                    stopwatch.ElapsedMilliseconds,
                    userId,
                    response == null ? "Failed to get response from OpenAI API" : null);

                if (response == null)
                {
                    return CreateFailedScriptResult("Failed to get response from OpenAI API");
                }

                // Step 9: Parse response
                var script = ParseScriptResponse(response);
                if (script == null)
                {
                    return CreateFailedScriptResult("Failed to parse script response");
                }

                // Step 10: Save to ScriptContent entity
                await SaveScriptToScriptContentAsync(projectId, script, aiCall.Id);

                // Step 11: Calculate costs
                var inputTokens = response.Usage?.PromptTokens ?? EstimateTokenCount(scriptPrompt);
                var outputTokens = response.Usage?.CompletionTokens ?? EstimateTokenCount(JsonConvert.SerializeObject(script));
                var totalCost = CalculateCost(inputTokens, outputTokens);

                _logger.LogInformation(
                    $"Successfully generated script for project {projectId}. " +
                    $"Tokens: {inputTokens} input, {outputTokens} output. Cost: ${totalCost:F6}");

                return new ScriptGenerationResult
                {
                    Success = true,
                    Script = script,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    TotalCost = totalCost,
                    DurationMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, $"Error generating script for project {projectId}: {ex.Message}");

                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner Exception: {ex.InnerException.Message}";
                }

                return CreateFailedScriptResult($"Script generation failed: {errorMessage}");
            }
        }

        /// <summary>
        /// Gets the script content entity for a project.
        /// </summary>
        public async Task<ScriptContentEntity?> GetScriptContentAsync(Guid projectId)
        {
            return await _context.ScriptContents
                .FirstOrDefaultAsync(sc => sc.ProjectId == projectId && !sc.IsDeleted);
        }

        /// <summary>
        /// Checks if a project can generate a script.
        /// </summary>
        public async Task<bool> CanGenerateScriptAsync(Guid projectId, string userId)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.ProjectVideos)
                        .ThenInclude(pv => pv.Video)
                            .ThenInclude(v => v.Summary)
                    .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);

                if (project == null) return false;
                if (project.ProjectVideos.Count < 3) return false;
                if (project.ProjectVideos.Any(pv => pv.Video?.Summary == null)) return false;

                var dailyCount = await GetDailyAICallCountAsync(userId);
                if (dailyCount >= DailyAICallLimit) return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if script can be generated for project {projectId}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current daily AI call count for a user.
        /// </summary>
        public async Task<int> GetDailyAICallCountAsync(string userId)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                return await _context.AICalls
                    .Where(ac => ac.UserId == userId
                        && ac.CreatedAt >= today
                        && ac.CreatedAt < tomorrow
                        && ac.Success)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting daily AI call count for user {userId}");
                return 0;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Gets or creates a placeholder prompt for script generation.
        /// MVP: Creates a minimal prompt record in database for FK constraint.
        /// </summary>
        private async Task<Guid> GetOrCreateScriptPromptIdAsync()
        {
            if (_scriptPromptId.HasValue)
            {
                return _scriptPromptId.Value;
            }

            await _promptLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_scriptPromptId.HasValue)
                {
                    return _scriptPromptId.Value;
                }

                // Check if placeholder prompt already exists
                var existingPrompt = await _context.Prompts
                    .FirstOrDefaultAsync(p => p.Name == "Script Analysis (MVP)" && p.Version == "1.0");

                if (existingPrompt != null)
                {
                    _scriptPromptId = existingPrompt.Id;
                    return existingPrompt.Id;
                }

                // Get or create a model for gpt-4o-mini
                var model = await _context.Models
                    .FirstOrDefaultAsync(m => m.Name == DefaultModel);

                if (model == null)
                {
                    // Create placeholder model
                    model = new ModelEntity
                    {
                        Name = DefaultModel,
                        Provider = "OpenAI",
                        CostPer1kInputTokens = InputCostPer1kTokens,
                        CostPer1kOutputTokens = OutputCostPer1kTokens,
                        IsActive = true
                    };
                    _context.Models.Add(model);
                    await _context.SaveChangesAsync();
                }

                // Create placeholder prompt
                var prompt = new PromptEntity
                {
                    Name = "Script Analysis (MVP)",
                    Version = "1.0",
                    ModelId = model.Id,
                    SystemPrompt = "You are an expert video content analyst.",
                    UserPromptTemplate = "[Generated by ScriptPromptBuilder]",
                    Temperature = 0.7m,
                    MaxTokens = 2000,
                    IsActive = true
                };

                _context.Prompts.Add(prompt);
                await _context.SaveChangesAsync();

                _scriptPromptId = prompt.Id;
                return prompt.Id;
            }
            finally
            {
                _promptLock.Release();
            }
        }

        /// <summary>
        /// Creates the OpenAI API request for analysis.
        /// MVP: Uses hard-coded model and settings.
        /// </summary>
        private OpenAiRequest CreateAnalysisRequest(string analysisPrompt)
        {
            return new OpenAiRequest
            {
                Model = DefaultModel,
                Messages = new List<OpenAiMessage>
                {
                    new OpenAiMessage
                    {
                        Role = "system",
                        Content = "You are an expert video content analyst. Analyze video summaries to identify themes, conflicts, and create cohesive video scripts."
                    },
                    new OpenAiMessage
                    {
                        Role = "user",
                        Content = analysisPrompt
                    }
                },
                MaxTokens = 2000,
                Temperature = 0.7m,
                ResponseFormat = new OpenAiResponseFormat { Type = "json_object" }
            };
        }

        /// <summary>
        /// Calls the OpenAI API.
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
        /// Parses the OpenAI response into ScriptAnalysisModel.
        /// </summary>
        private ScriptAnalysisModel? ParseAnalysisResponse(OpenAiResponse response)
        {
            try
            {
                if (response.Choices == null || !response.Choices.Any())
                {
                    _logger.LogWarning("No choices returned from OpenAI API");
                    return null;
                }

                var messageContent = response.Choices.First().Message.Content;

                if (messageContent is string jsonContent && !string.IsNullOrWhiteSpace(jsonContent))
                {
                    return JsonConvert.DeserializeObject<ScriptAnalysisModel>(jsonContent);
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
        /// Saves analysis results to ScriptContent entity.
        /// </summary>
        private async Task SaveAnalysisToScriptContentAsync(
            Guid projectId,
            ScriptAnalysisModel analysis,
            Guid aiCallId,
            int videoCount)
        {
            var existingContent = await _context.ScriptContents
                .FirstOrDefaultAsync(sc => sc.ProjectId == projectId && !sc.IsDeleted);

            var analysisJson = JsonConvert.SerializeObject(analysis);

            if (existingContent != null)
            {
                // Update existing
                existingContent.AnalysisJsonResult = analysisJson;
                existingContent.MainTopic = analysis.MainTopic;
                existingContent.CohesionScore = analysis.CohesionScore;
                existingContent.ScriptStatus = "Analyzed";
                existingContent.AnalysisAICallId = aiCallId;
                existingContent.VideoCount = videoCount;
            }
            else
            {
                // Create new
                var scriptContent = new ScriptContentEntity
                {
                    ProjectId = projectId,
                    AnalysisJsonResult = analysisJson,
                    MainTopic = analysis.MainTopic,
                    CohesionScore = analysis.CohesionScore,
                    ScriptStatus = "Analyzed",
                    AnalysisAICallId = aiCallId,
                    VideoCount = videoCount,
                    ScriptText = string.Empty // Required field, will be populated later
                };

                _context.ScriptContents.Add(scriptContent);
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Tracks API call for auditing and cost monitoring.
        /// MVP: Uses Guid.Empty as placeholder for prompt ID.
        /// </summary>
        private async Task<AICallEntity> TrackApiCallAsync(
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

                if (response?.Usage != null)
                {
                    inputTokens = response.Usage.PromptTokens;
                    outputTokens = response.Usage.CompletionTokens;
                    totalCost = CalculateCost(inputTokens, outputTokens);

                    if (response.Choices?.Any() == true)
                    {
                        var content = response.Choices.First().Message.Content;
                        responseContent = content is string str ? str : JsonConvert.SerializeObject(content);
                    }
                }
                else
                {
                    inputTokens = EstimateTokenCount(actualUserPrompt);
                    outputTokens = EstimateTokenCount(responseContent);
                    totalCost = CalculateCost(inputTokens, outputTokens);
                }

                // Get or create placeholder prompt ID for FK constraint
                var promptId = await GetOrCreateScriptPromptIdAsync();

                var aiCall = await _aiCallDataService.CreateAICallAsync(
                    promptId: promptId,
                    userId: userId,
                    actualSystemPrompt: "You are an expert video content analyst.",
                    actualUserPrompt: actualUserPrompt,
                    response: responseContent,
                    inputTokens: inputTokens,
                    outputTokens: outputTokens,
                    totalCost: totalCost,
                    durationMs: (int)durationMs,
                    success: success,
                    errorMessage: errorMessage);

                return aiCall;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to track API call for user {userId}");
                throw;
            }
        }

        /// <summary>
        /// Calculates cost based on token usage.
        /// MVP: Uses hard-coded pricing for gpt-4o-mini.
        /// </summary>
        private decimal CalculateCost(int inputTokens, int outputTokens)
        {
            return ((inputTokens * (InputCostPer1kTokens / 1000))) + (outputTokens * (OutputCostPer1kTokens / 1000));
        }

        /// <summary>
        /// Estimates token count (rough approximation: 1 token ≈ 4 characters).
        /// </summary>
        private int EstimateTokenCount(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Length / 4;
        }

        /// <summary>
        /// Parses the OpenAI response into ScriptOutlineModel.
        /// Stores last parse error for surfacing in error messages.
        /// </summary>
        private string? _lastParseError;

        private ScriptOutlineModel? ParseOutlineResponse(OpenAiResponse response)
        {
            _lastParseError = null;

            try
            {
                if (response.Choices == null || !response.Choices.Any())
                {
                    _lastParseError = "No choices returned from OpenAI API";
                    _logger.LogWarning(_lastParseError);
                    return null;
                }

                var messageContent = response.Choices.First().Message.Content;

                if (messageContent is string jsonContent && !string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogInformation($"Outline response JSON (first 500 chars): {jsonContent.Substring(0, Math.Min(500, jsonContent.Length))}");

                    var outline = JsonConvert.DeserializeObject<ScriptOutlineModel>(jsonContent);

                    if (outline == null)
                    {
                        _lastParseError = "Deserialization returned null";
                        _logger.LogWarning($"Outline deserialization returned null. JSON: {jsonContent.Substring(0, Math.Min(1000, jsonContent.Length))}");
                    }

                    return outline;
                }

                _lastParseError = $"Unexpected response content type: {messageContent?.GetType().Name ?? "null"}";
                _logger.LogWarning(_lastParseError);
                return null;
            }
            catch (Exception ex)
            {
                _lastParseError = $"{ex.Message}";
                _logger.LogError(ex, $"Error parsing OpenAI outline response: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves outline results to ScriptContent entity.
        /// </summary>
        private async Task SaveOutlineToScriptContentAsync(
            Guid projectId,
            ScriptOutlineModel outline,
            Guid aiCallId)
        {
            var scriptContent = await _context.ScriptContents
                .FirstOrDefaultAsync(sc => sc.ProjectId == projectId && !sc.IsDeleted);

            if (scriptContent == null)
            {
                throw new InvalidOperationException("Script content not found");
            }

            var outlineJson = JsonConvert.SerializeObject(outline);

            // Calculate estimated length from sections
            var estimatedMinutes = outline.Sections.Sum(s => s.EstimatedMinutes);

            scriptContent.OutlineJsonStructure = outlineJson;
            scriptContent.EstimatedLengthMinutes = estimatedMinutes;
            scriptContent.ScriptStatus = "OutlineGenerated";
            scriptContent.OutlineAICallId = aiCallId;

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Creates a failed analysis result.
        /// </summary>
        private ScriptAnalysisResult CreateFailedResult(string errorMessage)
        {
            return new ScriptAnalysisResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Creates a failed outline result.
        /// </summary>
        private ScriptOutlineResult CreateFailedOutlineResult(string errorMessage)
        {
            return new ScriptOutlineResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Creates the OpenAI API request for script generation.
        /// Uses higher token limit than analysis/outline since full scripts are longer.
        /// </summary>
        private OpenAiRequest CreateScriptRequest(string scriptPrompt)
        {
            return new OpenAiRequest
            {
                Model = DefaultModel,
                Messages = new List<OpenAiMessage>
                {
                    new OpenAiMessage
                    {
                        Role = "system",
                        Content = "You are an expert video script writer. Generate engaging, well-structured video scripts based on outlines and source transcripts."
                    },
                    new OpenAiMessage
                    {
                        Role = "user",
                        Content = scriptPrompt
                    }
                },
                MaxTokens = 8000,
                Temperature = 0.7m,
                ResponseFormat = new OpenAiResponseFormat { Type = "json_object" }
            };
        }

        /// <summary>
        /// Parses the OpenAI response into ScriptModel.
        /// </summary>
        private ScriptModel? ParseScriptResponse(OpenAiResponse response)
        {
            try
            {
                if (response.Choices == null || !response.Choices.Any())
                {
                    _logger.LogWarning("No choices returned from OpenAI API");
                    return null;
                }

                var messageContent = response.Choices.First().Message.Content;

                if (messageContent is string jsonContent && !string.IsNullOrWhiteSpace(jsonContent))
                {
                    return JsonConvert.DeserializeObject<ScriptModel>(jsonContent);
                }

                _logger.LogWarning("Unexpected response content format from OpenAI API");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error parsing OpenAI script response: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Saves generated script to ScriptContent entity.
        /// </summary>
        private async Task SaveScriptToScriptContentAsync(
            Guid projectId,
            ScriptModel script,
            Guid aiCallId)
        {
            var scriptContent = await _context.ScriptContents
                .FirstOrDefaultAsync(sc => sc.ProjectId == projectId && !sc.IsDeleted);

            if (scriptContent == null)
            {
                throw new InvalidOperationException("Script content not found");
            }

            scriptContent.ScriptText = script.ScriptText;
            scriptContent.WordCount = script.WordCount;
            scriptContent.EstimatedDurationSeconds = script.EstimatedDurationSeconds;
            scriptContent.InternalNotesJson = JsonConvert.SerializeObject(script.InternalNotes);
            scriptContent.ScriptStatus = "Complete";
            scriptContent.ScriptAICallId = aiCallId;
            scriptContent.GeneratedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Creates a failed script generation result.
        /// </summary>
        private ScriptGenerationResult CreateFailedScriptResult(string errorMessage)
        {
            return new ScriptGenerationResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        #endregion
    }
}
