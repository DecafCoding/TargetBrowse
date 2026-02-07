using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Projects.Models;
using TargetBrowse.Features.Projects.Services;

namespace TargetBrowse.Features.Projects.Pages.Script
{
    /// <summary>
    /// Code-behind for Analysis Results page.
    /// Displays the analysis of videos for script generation.
    /// </summary>
    public partial class AnalysisResults : ComponentBase
    {
        [Parameter] public Guid Id { get; set; }

        [Inject] private IScriptGenerationService ScriptGenerationService { get; set; } = default!;
        [Inject] private ApplicationDbContext Context { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private ILogger<AnalysisResults> Logger { get; set; } = default!;

        private ProjectEntity? Project { get; set; }
        private ScriptAnalysisModel? _analysis;
        private bool _isAnalyzing = true;
        private string? _errorMessage;
        private string? _userId;
        private decimal _totalCost;
        private long _durationMs;
        private int _videoCount;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Get current user
                var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;

                if (user.Identity?.IsAuthenticated == true)
                {
                    _userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                }

                if (string.IsNullOrEmpty(_userId))
                {
                    _errorMessage = "User not authenticated";
                    _isAnalyzing = false;
                    return;
                }

                // Load project
                Project = await Context.Projects
                    .Include(p => p.ProjectVideos)
                    .FirstOrDefaultAsync(p => p.Id == Id && !p.IsDeleted);

                if (Project == null)
                {
                    _errorMessage = "Project not found";
                    _isAnalyzing = false;
                    return;
                }

                _videoCount = Project.ProjectVideos.Count;

                // Check if analysis already exists
                var existingScript = await ScriptGenerationService.GetScriptContentAsync(Id);

                if (existingScript != null && !string.IsNullOrEmpty(existingScript.AnalysisJsonResult))
                {
                    // Load existing analysis
                    _analysis = Newtonsoft.Json.JsonConvert.DeserializeObject<ScriptAnalysisModel>(existingScript.AnalysisJsonResult);
                    _isAnalyzing = false;
                    Logger.LogInformation($"Loaded existing analysis for project {Id}");
                }
                else
                {
                    // Perform new analysis
                    await PerformAnalysisAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error initializing Analysis Results page for project {Id}");
                _errorMessage = $"Error loading analysis: {ex.Message}";
                _isAnalyzing = false;
            }
        }

        /// <summary>
        /// Performs video analysis for script generation.
        /// </summary>
        private async Task PerformAnalysisAsync()
        {
            try
            {
                _isAnalyzing = true;
                _errorMessage = null;
                StateHasChanged();

                var result = await ScriptGenerationService.AnalyzeProjectVideosAsync(Id, _userId!);

                if (result.Success && result.Analysis != null)
                {
                    _analysis = result.Analysis;
                    _totalCost = result.TotalCost;
                    _durationMs = result.DurationMs;
                    Logger.LogInformation($"Successfully analyzed project {Id}");
                }
                else
                {
                    _errorMessage = result.ErrorMessage ?? "Analysis failed";
                    Logger.LogWarning($"Analysis failed for project {Id}: {_errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error performing analysis for project {Id}");
                _errorMessage = $"Analysis error: {ex.Message}";
            }
            finally
            {
                _isAnalyzing = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Handles retry analysis button click.
        /// </summary>
        private async Task HandleRetryAnalysis()
        {
            await PerformAnalysisAsync();
        }

        /// <summary>
        /// Gets CSS class for cohesion score display.
        /// </summary>
        private string GetCohesionScoreClass(int score)
        {
            if (score >= 80) return "text-success";
            if (score >= 60) return "text-warning";
            return "text-danger";
        }

        /// <summary>
        /// Gets description for cohesion score.
        /// </summary>
        private string GetCohesionDescription(int score)
        {
            if (score >= 80) return "Excellent fit";
            if (score >= 60) return "Good fit";
            if (score >= 40) return "Moderate fit";
            return "Poor fit";
        }

        /// <summary>
        /// Gets badge class for depth indicator.
        /// </summary>
        private string GetDepthBadgeClass(string depth)
        {
            return depth.ToLower() switch
            {
                "comprehensive" => "bg-success",
                "moderate" => "bg-primary",
                "brief" => "bg-secondary",
                _ => "bg-secondary"
            };
        }
    }
}
