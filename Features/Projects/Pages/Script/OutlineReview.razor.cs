using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Features.Projects.Models;
using TargetBrowse.Features.Projects.Services;

namespace TargetBrowse.Features.Projects.Pages.Script
{
    /// <summary>
    /// Code-behind for Outline Review page.
    /// Displays or generates the script outline.
    /// </summary>
    public partial class OutlineReview : ComponentBase
    {
        [Parameter] public Guid Id { get; set; }

        [Inject] private IScriptGenerationService ScriptGenerationService { get; set; } = default!;
        [Inject] private ApplicationDbContext Context { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private ILogger<OutlineReview> Logger { get; set; } = default!;

        private string? ProjectName { get; set; }
        private ScriptOutlineModel? _outline;
        private bool _isGenerating = true;
        private string? _errorMessage;
        private string? _userId;
        private decimal _totalCost;
        private long _durationMs;
        private int _estimatedMinutes;
        private int _targetMinutes;

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
                    _isGenerating = false;
                    return;
                }

                // Load project
                var project = await Context.Projects
                    .FirstOrDefaultAsync(p => p.Id == Id && !p.IsDeleted);

                if (project == null)
                {
                    _errorMessage = "Project not found";
                    _isGenerating = false;
                    return;
                }

                ProjectName = project.Name;

                // Check if outline already exists
                var existingScript = await ScriptGenerationService.GetScriptContentAsync(Id);

                if (existingScript != null && !string.IsNullOrEmpty(existingScript.OutlineJsonStructure))
                {
                    // Load existing outline
                    _outline = Newtonsoft.Json.JsonConvert.DeserializeObject<ScriptOutlineModel>(existingScript.OutlineJsonStructure);
                    _targetMinutes = existingScript.TargetLengthMinutes ?? 15;
                    _estimatedMinutes = existingScript.EstimatedLengthMinutes ?? _targetMinutes;
                    _isGenerating = false;
                    Logger.LogInformation($"Loaded existing outline for project {Id}");
                }
                else
                {
                    // Generate new outline
                    await GenerateOutlineAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error initializing Outline Review page for project {Id}");
                _errorMessage = $"Error loading outline: {ex.Message}";
                _isGenerating = false;
            }
        }

        /// <summary>
        /// Generates a new script outline.
        /// </summary>
        private async Task GenerateOutlineAsync()
        {
            try
            {
                _isGenerating = true;
                _errorMessage = null;
                StateHasChanged();

                var result = await ScriptGenerationService.GenerateOutlineAsync(Id, _userId!);

                if (result.Success && result.Outline != null)
                {
                    _outline = result.Outline;
                    _totalCost = result.TotalCost;
                    _durationMs = result.DurationMs;

                    // Calculate estimated length
                    _estimatedMinutes = result.Outline.Sections.Sum(s => s.EstimatedMinutes);

                    // Get target length from script content
                    var scriptContent = await ScriptGenerationService.GetScriptContentAsync(Id);
                    _targetMinutes = scriptContent?.TargetLengthMinutes ?? 15;

                    Logger.LogInformation($"Successfully generated outline for project {Id}");
                }
                else
                {
                    _errorMessage = result.ErrorMessage ?? "Outline generation failed";
                    Logger.LogWarning($"Outline generation failed for project {Id}: {_errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error generating outline for project {Id}");
                _errorMessage = $"Outline generation error: {ex.Message}";
            }
            finally
            {
                _isGenerating = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Handles retry outline button click.
        /// </summary>
        private async Task HandleRetryOutline()
        {
            await GenerateOutlineAsync();
        }
    }
}
