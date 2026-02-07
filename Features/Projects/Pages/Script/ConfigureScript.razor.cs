using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data;
using TargetBrowse.Features.Projects.Services;

namespace TargetBrowse.Features.Projects.Pages.Script
{
    /// <summary>
    /// Code-behind for Configure Script page.
    /// Allows user to configure script generation settings.
    /// </summary>
    public partial class ConfigureScript : ComponentBase
    {
        [Parameter] public Guid Id { get; set; }

        [Inject] private IScriptGenerationService ScriptGenerationService { get; set; } = default!;
        [Inject] private IScriptProfileService ScriptProfileService { get; set; } = default!;
        [Inject] private ApplicationDbContext Context { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        [Inject] private ILogger<ConfigureScript> Logger { get; set; } = default!;

        [SupplyParameterFromForm]
        private ConfigurationFormModel ConfigModel { get; set; } = new();

        private string? ProjectName { get; set; }
        private bool _isLoading = true;
        private bool _isSaving = false;
        private bool _hasProfile = false;
        private string? _errorMessage;
        private string? _userId;

        // Profile display fields
        private string _profileTone = string.Empty;
        private string _profilePacing = string.Empty;
        private string _profileComplexity = string.Empty;
        private string? _profileInstructions;

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
                    _isLoading = false;
                    return;
                }

                // Check if user has a script profile
                _hasProfile = await ScriptProfileService.HasProfileAsync(_userId);

                if (!_hasProfile)
                {
                    _isLoading = false;
                    return;
                }

                // Load user's script profile
                var profile = await ScriptProfileService.GetUserProfileAsync(_userId);
                if (profile != null)
                {
                    _profileTone = profile.Tone;
                    _profilePacing = profile.Pacing;
                    _profileComplexity = profile.Complexity;
                    _profileInstructions = profile.CustomInstructions;
                }

                // Load project
                var project = await Context.Projects
                    .FirstOrDefaultAsync(p => p.Id == Id && !p.IsDeleted);

                if (project == null)
                {
                    _errorMessage = "Project not found";
                    _isLoading = false;
                    return;
                }

                ProjectName = project.Name;

                // Load existing script content to get current configuration
                var scriptContent = await ScriptGenerationService.GetScriptContentAsync(Id);

                if (scriptContent != null && scriptContent.TargetLengthMinutes.HasValue)
                {
                    // Pre-fill form with existing configuration
                    ConfigModel.TargetLengthMinutes = scriptContent.TargetLengthMinutes.Value;
                }
                else
                {
                    // Set default value
                    ConfigModel.TargetLengthMinutes = 15;
                }

                _isLoading = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error loading configuration page for project {Id}");
                _errorMessage = $"Error loading page: {ex.Message}";
                _isLoading = false;
            }
        }

        /// <summary>
        /// Handles form submission to save configuration and proceed to outline generation.
        /// </summary>
        private async Task HandleSaveConfiguration()
        {
            try
            {
                _isSaving = true;
                _errorMessage = null;
                StateHasChanged();

                // Save configuration
                var result = await ScriptGenerationService.ConfigureScriptAsync(
                    Id,
                    _userId!,
                    ConfigModel.TargetLengthMinutes);

                if (result.Success)
                {
                    Logger.LogInformation($"Successfully configured script for project {Id}");

                    // Navigate to outline page
                    NavigationManager.NavigateTo($"/projects/{Id}/script/outline");
                }
                else
                {
                    _errorMessage = result.ErrorMessage ?? "Failed to save configuration";
                    Logger.LogWarning($"Configuration failed for project {Id}: {_errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error saving configuration for project {Id}");
                _errorMessage = $"Error saving configuration: {ex.Message}";
            }
            finally
            {
                _isSaving = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Form model for script configuration.
        /// </summary>
        public class ConfigurationFormModel
        {
            [Required(ErrorMessage = "Target length is required")]
            [Range(5, 30, ErrorMessage = "Target length must be between 5 and 30 minutes")]
            public int TargetLengthMinutes { get; set; } = 15;
        }
    }
}
