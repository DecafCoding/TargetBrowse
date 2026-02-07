using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Features.Projects.Models;
using TargetBrowse.Features.Projects.Services;

namespace TargetBrowse.Components.Account.Pages.Manage
{
    /// <summary>
    /// Code-behind for Script Profile management page.
    /// Handles loading and saving user script generation preferences.
    /// </summary>
    public partial class ScriptProfile : ComponentBase
    {
        [Inject] private IScriptProfileService ScriptProfileService { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] private ILogger<ScriptProfile> Logger { get; set; } = default!;

        [SupplyParameterFromForm]
        private UserScriptProfileModel ProfileModel { get; set; } = new();

        private string? _message;
        private bool _isLoading = true;
        private bool _isSaving = false;
        private string? _userId;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Get current user ID
                var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                var user = authState.User;

                if (user.Identity?.IsAuthenticated == true)
                {
                    _userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                    if (!string.IsNullOrEmpty(_userId))
                    {
                        await LoadProfileAsync();
                    }
                    else
                    {
                        Logger.LogWarning("User ID not found in claims");
                        _message = "Error: Unable to identify user.";
                    }
                }

                _isLoading = false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing Script Profile page");
                _message = "Error loading profile. Please try again.";
                _isLoading = false;
            }
        }

        /// <summary>
        /// Loads the user's existing profile or sets default values.
        /// </summary>
        private async Task LoadProfileAsync()
        {
            try
            {
                var existingProfile = await ScriptProfileService.GetUserProfileAsync(_userId!);

                if (existingProfile != null)
                {
                    ProfileModel = new UserScriptProfileModel
                    {
                        Id = existingProfile.Id,
                        Tone = existingProfile.Tone,
                        Pacing = existingProfile.Pacing,
                        Complexity = existingProfile.Complexity,
                        CustomInstructions = existingProfile.CustomInstructions
                    };

                    Logger.LogInformation($"Loaded existing profile for user {_userId}");
                }
                else
                {
                    // Set default values
                    ProfileModel = new UserScriptProfileModel
                    {
                        Tone = "Casual",
                        Pacing = "Moderate",
                        Complexity = "Intermediate"
                    };

                    Logger.LogInformation($"No existing profile found for user {_userId}, using defaults");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error loading profile for user {_userId}");
                throw;
            }
        }

        /// <summary>
        /// Handles form submission to save profile changes.
        /// </summary>
        private async Task HandleSaveProfile()
        {
            if (string.IsNullOrEmpty(_userId))
            {
                _message = "Error: User not identified.";
                return;
            }

            _isSaving = true;
            _message = null;

            try
            {
                await ScriptProfileService.CreateOrUpdateProfileAsync(_userId, ProfileModel);

                _message = "Profile saved successfully!";
                Logger.LogInformation($"Script profile saved for user {_userId}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error saving script profile for user {_userId}");
                _message = "Error saving profile. Please try again.";
            }
            finally
            {
                _isSaving = false;
            }
        }
    }
}
