using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using TargetBrowse.Data;
using TargetBrowse.Features.Projects.Models;
using TargetBrowse.Features.Projects.Services;

namespace TargetBrowse.Features.Projects.Pages.Script
{
    /// <summary>
    /// Code-behind for Final Script page.
    /// Displays or generates the complete video script.
    /// </summary>
    public partial class FinalScript : ComponentBase
    {
        [Parameter] public Guid Id { get; set; }

        [Inject] private IScriptGenerationService ScriptGenerationService { get; set; } = default!;
        [Inject] private ApplicationDbContext Context { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private ILogger<FinalScript> Logger { get; set; } = default!;

        private string? ProjectName { get; set; }
        private ScriptModel? _script;
        private bool _isGenerating = true;
        private string? _errorMessage;
        private string? _userId;
        private decimal _totalCost;
        private long _durationMs;
        private string _copyButtonText = "Copy";

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

                // Check if script already exists
                var existingScript = await ScriptGenerationService.GetScriptContentAsync(Id);

                if (existingScript != null && existingScript.ScriptStatus == "Complete"
                    && !string.IsNullOrEmpty(existingScript.ScriptText))
                {
                    // Load existing script
                    _script = new ScriptModel
                    {
                        ScriptText = existingScript.ScriptText,
                        WordCount = existingScript.WordCount,
                        EstimatedDurationSeconds = existingScript.EstimatedDurationSeconds,
                        InternalNotes = !string.IsNullOrEmpty(existingScript.InternalNotesJson)
                            ? Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, SectionNotes>>(existingScript.InternalNotesJson)
                              ?? new Dictionary<string, SectionNotes>()
                            : new Dictionary<string, SectionNotes>()
                    };
                    _isGenerating = false;
                    Logger.LogInformation($"Loaded existing script for project {Id}");
                }
                else
                {
                    // Generate new script
                    await GenerateScriptAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error initializing Final Script page for project {Id}");
                _errorMessage = $"Error loading script: {ex.Message}";
                _isGenerating = false;
            }
        }

        /// <summary>
        /// Generates a new script.
        /// </summary>
        private async Task GenerateScriptAsync()
        {
            try
            {
                _isGenerating = true;
                _errorMessage = null;
                StateHasChanged();

                var result = await ScriptGenerationService.GenerateScriptAsync(Id, _userId!);

                if (result.Success && result.Script != null)
                {
                    _script = result.Script;
                    _totalCost = result.TotalCost;
                    _durationMs = result.DurationMs;
                    Logger.LogInformation($"Successfully generated script for project {Id}");
                }
                else
                {
                    _errorMessage = result.ErrorMessage ?? "Script generation failed";
                    Logger.LogWarning($"Script generation failed for project {Id}: {_errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error generating script for project {Id}");
                _errorMessage = $"Script generation error: {ex.Message}";
            }
            finally
            {
                _isGenerating = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Handles retry script button click.
        /// </summary>
        private async Task HandleRetryScript()
        {
            await GenerateScriptAsync();
        }

        /// <summary>
        /// Handles regenerate script button click.
        /// </summary>
        private async Task HandleRegenerateScript()
        {
            _script = null;
            await GenerateScriptAsync();
        }

        /// <summary>
        /// Copies script text to clipboard using JavaScript interop.
        /// </summary>
        private async Task HandleCopyScript()
        {
            if (_script == null) return;

            try
            {
                await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", _script.ScriptText);
                _copyButtonText = "Copied!";
                StateHasChanged();

                // Reset button text after 2 seconds
                await Task.Delay(2000);
                _copyButtonText = "Copy";
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error copying script to clipboard");
                _copyButtonText = "Failed";
                StateHasChanged();
            }
        }

        /// <summary>
        /// Downloads script as a .txt file using JavaScript interop.
        /// </summary>
        private async Task HandleDownloadScript()
        {
            if (_script == null) return;

            try
            {
                var fileName = $"script-{ProjectName ?? "project"}.txt";
                // Sanitize filename
                fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

                await JSRuntime.InvokeVoidAsync("eval", $@"
                    (function() {{
                        var blob = new Blob([{Newtonsoft.Json.JsonConvert.SerializeObject(_script.ScriptText)}], {{ type: 'text/plain' }});
                        var url = URL.createObjectURL(blob);
                        var a = document.createElement('a');
                        a.href = url;
                        a.download = {Newtonsoft.Json.JsonConvert.SerializeObject(fileName)};
                        document.body.appendChild(a);
                        a.click();
                        document.body.removeChild(a);
                        URL.revokeObjectURL(url);
                    }})();
                ");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error downloading script");
            }
        }

        /// <summary>
        /// Formats seconds into a readable duration string.
        /// </summary>
        private string FormatDuration(int totalSeconds)
        {
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return seconds > 0 ? $"{minutes}m {seconds}s" : $"{minutes}m";
        }
    }
}
