using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.AI.Models;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Services.AI;

/// <summary>
/// Searches for YouTube videos using Perplexity's Sonar API.
/// Uses the OpenAI-compatible chat completions endpoint with web search restricted to youtube.com.
/// Extracts video IDs from both AI response content and the API's citations/search_results fields.
/// </summary>
public class PerplexityService : IPerplexityService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PerplexityService> _logger;
    private readonly IAICallDataService _aiCallDataService;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly string _apiKey;

    private const string ApiUrl = "https://api.perplexity.ai/chat/completions";
    private const string ModelName = "sonar";
    private const string PromptName = "Perplexity Video Search";
    // Perplexity Sonar pricing: $1/M input tokens, $1/M output tokens
    private const decimal CostPer1kInput = 0.001m;
    private const decimal CostPer1kOutput = 0.001m;

    // Cached prompt ID to avoid DB lookups on every call
    private Guid? _cachedPromptId;

    public PerplexityService(
        IConfiguration configuration,
        ILogger<PerplexityService> logger,
        IAICallDataService aiCallDataService,
        IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiCallDataService = aiCallDataService ?? throw new ArgumentNullException(nameof(aiCallDataService));
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));

        _apiKey = configuration["Perplexity:ApiKey"]
            ?? throw new InvalidOperationException("Perplexity:ApiKey configuration is missing. Add it to User Secrets.");

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<PerplexitySearchResult> SearchVideosAsync(
        string projectName, string? description, string userGuidance, string? userId)
    {
        var stopwatch = Stopwatch.StartNew();
        string userPrompt = BuildPrompt(projectName, description, userGuidance);
        string systemPrompt = "You are a YouTube video research assistant. You search for relevant YouTube videos and return structured results. Prioritize recent, high-quality videos.";
        string? responseContent = null;
        string? errorMessage = null;

        try
        {
            var requestBody = new
            {
                model = ModelName,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                web_search_options = new
                {
                    search_domain_filter = new[] { "youtube.com" },
                    search_context_size = "high"
                },
                search_recency_filter = "year"
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(ApiUrl, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                errorMessage = $"Perplexity API returned {response.StatusCode}: {responseJson}";
                _logger.LogError("Perplexity API error: {Error}", errorMessage);
                return new PerplexitySearchResult();
            }

            var responseObj = JObject.Parse(responseJson);
            responseContent = responseObj["choices"]?[0]?["message"]?["content"]?.ToString();

            if (string.IsNullOrEmpty(responseContent))
            {
                errorMessage = "Perplexity returned empty response";
                _logger.LogWarning("Empty response from Perplexity API");
                return new PerplexitySearchResult();
            }

            // Parse video results from AI content
            var results = ParseResultsFromContent(responseContent);

            // Also extract video IDs from citations and search_results for completeness
            var citationIds = ExtractVideoIdsFromCitations(responseObj);
            var searchResultData = ExtractFromSearchResults(responseObj);

            // Merge: add any videos found in citations/search_results but not in content
            MergeResults(results, citationIds, searchResultData);

            // Extract AI suggestion (any non-JSON text in the response)
            var suggestion = ExtractSuggestion(responseContent);

            stopwatch.Stop();

            // Track AI call
            int inputTokens = responseObj["usage"]?["prompt_tokens"]?.Value<int>() ?? EstimateTokenCount(systemPrompt + userPrompt);
            int outputTokens = responseObj["usage"]?["completion_tokens"]?.Value<int>() ?? EstimateTokenCount(responseContent);

            await TrackApiCallAsync(systemPrompt, userPrompt, responseContent,
                inputTokens, outputTokens, stopwatch.ElapsedMilliseconds, userId, null);

            _logger.LogInformation(
                "Perplexity search returned {Count} video results ({CitationCount} from citations) for project '{Project}'",
                results.Count, citationIds.Count, projectName);

            return new PerplexitySearchResult
            {
                Videos = results,
                AISuggestion = suggestion
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            errorMessage = ex.Message;
            _logger.LogError(ex, "Error calling Perplexity API for project '{Project}'", projectName);

            await TrackApiCallAsync(systemPrompt, userPrompt, responseContent ?? "",
                EstimateTokenCount(systemPrompt + userPrompt), 0,
                stopwatch.ElapsedMilliseconds, userId, errorMessage);

            return new PerplexitySearchResult();
        }
    }

    private string BuildPrompt(string projectName, string? description, string userGuidance)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Find up to 25 relevant YouTube videos for my project. Prefer recent videos (published within the last year). The more results the better.");
        sb.AppendLine();
        sb.AppendLine($"Project: {projectName}");

        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine($"Description: {description}");
        }

        sb.AppendLine($"What I'm looking for: {userGuidance}");
        sb.AppendLine();
        sb.AppendLine("Return a JSON array followed by a brief suggestion for refining the search.");
        sb.AppendLine("The JSON array should have items with:");
        sb.AppendLine("- \"url\": the full YouTube video URL");
        sb.AppendLine("- \"title\": the video title");
        sb.AppendLine("- \"reason\": a brief explanation of why this video is relevant (1 sentence)");
        sb.AppendLine();
        sb.AppendLine("After the JSON array, add a line starting with \"Suggestion:\" with advice on how to narrow or improve the search (e.g., specific topics, channels, or keywords to try).");
        sb.AppendLine();
        sb.AppendLine("Example:");
        sb.AppendLine("```json");
        sb.AppendLine("[{\"url\": \"https://www.youtube.com/watch?v=abc123\", \"title\": \"Video Title\", \"reason\": \"Covers the topic in depth\"}]");
        sb.AppendLine("```");
        sb.AppendLine("Suggestion: Try searching for videos specifically about X by channel Y for more targeted results.");

        return sb.ToString();
    }

    /// <summary>
    /// Parses video results from the AI response content (the JSON array in the message).
    /// </summary>
    private List<VideoSearchResult> ParseResultsFromContent(string responseContent)
    {
        var results = new List<VideoSearchResult>();

        try
        {
            var jsonContent = ExtractJson(responseContent);
            if (string.IsNullOrEmpty(jsonContent))
            {
                _logger.LogWarning("Could not extract JSON from Perplexity response");
                return results;
            }

            var items = JArray.Parse(jsonContent);

            foreach (var item in items)
            {
                var url = item["url"]?.ToString();
                var title = item["title"]?.ToString();
                var reason = item["reason"]?.ToString();

                if (string.IsNullOrEmpty(url)) continue;

                var videoId = ExtractYouTubeVideoId(url);
                if (string.IsNullOrEmpty(videoId)) continue;

                results.Add(new VideoSearchResult
                {
                    YouTubeVideoId = videoId,
                    Title = title ?? "Unknown",
                    Reason = reason ?? ""
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Perplexity response content as JSON");
        }

        return results;
    }

    /// <summary>
    /// Extracts YouTube video IDs from the citations array in the API response.
    /// Citations are URLs that Perplexity used as sources.
    /// </summary>
    private List<string> ExtractVideoIdsFromCitations(JObject responseObj)
    {
        var ids = new List<string>();

        try
        {
            var citations = responseObj["citations"] as JArray;
            if (citations == null) return ids;

            foreach (var citation in citations)
            {
                var url = citation.ToString();
                var videoId = ExtractYouTubeVideoId(url);
                if (!string.IsNullOrEmpty(videoId))
                {
                    ids.Add(videoId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting video IDs from citations");
        }

        return ids;
    }

    /// <summary>
    /// Extracts video info from the search_results array in the API response.
    /// Search results include title, URL, date, and snippet.
    /// </summary>
    private Dictionary<string, (string Title, string? Date)> ExtractFromSearchResults(JObject responseObj)
    {
        var data = new Dictionary<string, (string Title, string? Date)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var searchResults = responseObj["search_results"] as JArray;
            if (searchResults == null) return data;

            foreach (var result in searchResults)
            {
                var url = result["url"]?.ToString();
                if (string.IsNullOrEmpty(url)) continue;

                var videoId = ExtractYouTubeVideoId(url);
                if (string.IsNullOrEmpty(videoId)) continue;

                var title = result["title"]?.ToString() ?? "Unknown";
                var date = result["date"]?.ToString();

                if (!data.ContainsKey(videoId))
                {
                    data[videoId] = (title, date);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting data from search_results");
        }

        return data;
    }

    /// <summary>
    /// Merges videos found in citations/search_results that weren't in the AI's JSON response.
    /// </summary>
    private void MergeResults(
        List<VideoSearchResult> results,
        List<string> citationIds,
        Dictionary<string, (string Title, string? Date)> searchResultData)
    {
        var existingIds = results.Select(r => r.YouTubeVideoId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Add videos from citations/search_results not already in results
        foreach (var videoId in citationIds)
        {
            if (existingIds.Contains(videoId)) continue;

            var title = "Unknown";
            if (searchResultData.TryGetValue(videoId, out var data))
            {
                title = data.Title;
            }

            results.Add(new VideoSearchResult
            {
                YouTubeVideoId = videoId,
                Title = title,
                Reason = "Found in search sources"
            });
            existingIds.Add(videoId);
        }
    }

    /// <summary>
    /// Extracts the AI suggestion text from the response (text after the JSON array).
    /// </summary>
    private static string? ExtractSuggestion(string responseContent)
    {
        // Look for "Suggestion:" line
        var suggestionMatch = Regex.Match(responseContent, @"(?:^|\n)\s*\*?\*?Suggestion:?\*?\*?\s*(.+)", RegexOptions.IgnoreCase);
        if (suggestionMatch.Success)
        {
            return suggestionMatch.Groups[1].Value.Trim();
        }

        // Also try to capture text after the JSON block that isn't code
        var afterJson = Regex.Match(responseContent, @"```\s*\n(.+?)$", RegexOptions.Singleline);
        if (afterJson.Success)
        {
            var text = afterJson.Groups[1].Value.Trim();
            // Only return if it's meaningful (not just whitespace or very short)
            if (text.Length > 20)
                return text;
        }

        return null;
    }

    /// <summary>
    /// Extracts JSON array from response that may contain markdown formatting.
    /// </summary>
    private static string? ExtractJson(string content)
    {
        // Try to find JSON in code blocks first
        var codeBlockMatch = Regex.Match(content, @"```(?:json)?\s*(\[[\s\S]*?\])\s*```");
        if (codeBlockMatch.Success)
            return codeBlockMatch.Groups[1].Value;

        // Try to find a raw JSON array
        var arrayMatch = Regex.Match(content, @"\[[\s\S]*\]");
        if (arrayMatch.Success)
            return arrayMatch.Value;

        return null;
    }

    /// <summary>
    /// Extracts YouTube video ID from various URL formats.
    /// </summary>
    private static string? ExtractYouTubeVideoId(string input)
    {
        if (Regex.IsMatch(input, @"^[a-zA-Z0-9_-]{11}$"))
            return input;

        var match = Regex.Match(input,
            @"(?:youtube\.com/watch\?.*v=|youtu\.be/|youtube\.com/embed/|youtube\.com/shorts/)([a-zA-Z0-9_-]{11})");

        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task TrackApiCallAsync(
        string systemPrompt, string userPrompt, string response,
        int inputTokens, int outputTokens, long durationMs,
        string? userId, string? errorMessage)
    {
        try
        {
            var promptId = await EnsurePromptExistsAsync();
            decimal totalCost = (inputTokens * CostPer1kInput / 1000) + (outputTokens * CostPer1kOutput / 1000);
            bool success = string.IsNullOrEmpty(errorMessage);

            await _aiCallDataService.CreateAICallAsync(
                promptId: promptId,
                userId: userId,
                actualSystemPrompt: systemPrompt,
                actualUserPrompt: userPrompt,
                response: response,
                inputTokens: inputTokens,
                outputTokens: outputTokens,
                totalCost: totalCost,
                durationMs: (int)durationMs,
                success: success,
                errorMessage: errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track Perplexity API call. This won't stop processing.");
        }
    }

    /// <summary>
    /// Ensures the Perplexity model and prompt records exist in the database.
    /// Creates them on first use and caches the prompt ID for subsequent calls.
    /// </summary>
    private async Task<Guid> EnsurePromptExistsAsync()
    {
        if (_cachedPromptId.HasValue)
            return _cachedPromptId.Value;

        using var context = await _dbContextFactory.CreateDbContextAsync();

        // Check if prompt already exists
        var existingPrompt = await context.Prompts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == PromptName && p.IsActive);

        if (existingPrompt != null)
        {
            _cachedPromptId = existingPrompt.Id;
            return existingPrompt.Id;
        }

        // Ensure Perplexity model exists
        var model = await context.Models
            .FirstOrDefaultAsync(m => m.Provider == "Perplexity" && m.Name == ModelName);

        if (model == null)
        {
            model = new ModelEntity
            {
                Name = ModelName,
                Provider = "Perplexity",
                CostPer1kInputTokens = CostPer1kInput,
                CostPer1kOutputTokens = CostPer1kOutput,
                IsActive = true
            };
            context.Models.Add(model);
            await context.SaveChangesAsync();
        }

        // Create prompt
        var prompt = new PromptEntity
        {
            Name = PromptName,
            Version = "1.0",
            SystemPrompt = "YouTube video search assistant",
            UserPromptTemplate = "Search for YouTube videos based on project guidance",
            ModelId = model.Id,
            IsActive = true
        };
        context.Prompts.Add(prompt);
        await context.SaveChangesAsync();

        _cachedPromptId = prompt.Id;
        _logger.LogInformation("Created Perplexity prompt record with ID {PromptId}", prompt.Id);
        return prompt.Id;
    }

    private static int EstimateTokenCount(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return text.Length / 4;
    }
}
