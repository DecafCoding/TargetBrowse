using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Services.ProjectServices;
using TargetBrowse.Services.ProjectServices.Models;
using Microsoft.Extensions.Options;

namespace TargetBrowse.Components.Shared
{
    /// <summary>
    /// Modal component for adding a video to one or more projects.
    /// Shared component used across multiple features.
    /// </summary>
    public partial class AddToProjectModal : ComponentBase
    {
        [Inject] private IAddToProjectService AddToProjectService { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] private IOptions<ProjectSettings> ProjectSettings { get; set; } = default!;
        [Inject] private ILogger<AddToProjectModal> Logger { get; set; } = default!;

        /// <summary>
        /// Whether the modal is visible.
        /// </summary>
        [Parameter] public bool IsVisible { get; set; }

        /// <summary>
        /// The ID of the video to add to projects.
        /// Required if VideoInfo is not provided.
        /// </summary>
        [Parameter] public Guid VideoId { get; set; }

        /// <summary>
        /// Video information for videos not yet in the database.
        /// If provided, will be used instead of VideoId.
        /// </summary>
        [Parameter] public TargetBrowse.Services.Models.VideoInfo? VideoInfo { get; set; }

        /// <summary>
        /// Callback when the modal should be closed.
        /// </summary>
        [Parameter] public EventCallback OnClose { get; set; }

        /// <summary>
        /// Callback when the video is successfully added to projects.
        /// Passes the result with details about the operation.
        /// </summary>
        [Parameter] public EventCallback<AddToProjectResult> OnSuccess { get; set; }

        /// <summary>
        /// Whether to allow clicking backdrop to close modal.
        /// </summary>
        [Parameter] public bool CloseOnBackdropClick { get; set; } = true;

        private List<ProjectInfo> Projects { get; set; } = new();
        private HashSet<Guid> SelectedProjectIds { get; set; } = new();
        private bool IsLoading { get; set; }
        private bool IsSubmitting { get; set; }
        private string? ErrorMessage { get; set; }
        private string? CurrentUserId { get; set; }
        private int MaxVideosPerProject => ProjectSettings.Value.MaxVideosPerProject;

        /// <summary>
        /// Loads user projects when the modal becomes visible.
        /// </summary>
        protected override async Task OnParametersSetAsync()
        {
            if (IsVisible && Projects.Count == 0)
            {
                await LoadProjectsAsync();
            }
            else if (!IsVisible)
            {
                // Reset state when modal is closed
                SelectedProjectIds.Clear();
                ErrorMessage = null;
            }
        }

        /// <summary>
        /// Loads the user's projects from the service.
        /// </summary>
        private async Task LoadProjectsAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                StateHasChanged();

                // Get current user ID
                var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                CurrentUserId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(CurrentUserId))
                {
                    ErrorMessage = "Unable to identify current user.";
                    return;
                }

                // Load projects with video context
                Projects = await AddToProjectService.GetUserProjectsAsync(CurrentUserId, VideoId);

                Logger.LogInformation("Loaded {Count} projects for user {UserId}", Projects.Count, CurrentUserId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading projects for user {UserId}", CurrentUserId);
                ErrorMessage = "Failed to load projects. Please try again.";
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Toggles a project's selection state.
        /// </summary>
        private void ToggleProjectSelection(Guid projectId, ChangeEventArgs e)
        {
            var isChecked = e.Value is bool value && value;

            if (isChecked)
            {
                SelectedProjectIds.Add(projectId);
            }
            else
            {
                SelectedProjectIds.Remove(projectId);
            }

            ErrorMessage = null;
            StateHasChanged();
        }

        /// <summary>
        /// Handles form submission to add video to selected projects.
        /// </summary>
        private async Task HandleSubmit()
        {
            if (IsSubmitting || !SelectedProjectIds.Any() || string.IsNullOrEmpty(CurrentUserId))
                return;

            try
            {
                IsSubmitting = true;
                ErrorMessage = null;
                StateHasChanged();

                var request = new AddToProjectRequest
                {
                    VideoId = VideoId,
                    VideoInfo = VideoInfo,
                    ProjectIds = SelectedProjectIds.ToList(),
                    UserId = CurrentUserId
                };

                var result = await AddToProjectService.AddVideoToProjectsAsync(request);

                if (result.Success)
                {
                    Logger.LogInformation("Successfully added video {VideoId} to {Count} projects",
                        VideoId, result.AddedToProjectsCount);

                    // Notify parent component of success
                    await OnSuccess.InvokeAsync(result);

                    // Close modal
                    await CloseModal();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage ?? "Failed to add video to projects.";

                    // Show specific errors if available
                    if (result.ProjectErrors.Any())
                    {
                        var errorDetails = string.Join("; ", result.ProjectErrors.Values.Take(3));
                        ErrorMessage = $"{ErrorMessage} Details: {errorDetails}";
                    }

                    Logger.LogWarning("Failed to add video {VideoId} to some projects: {Error}",
                        VideoId, ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error adding video {VideoId} to projects", VideoId);
                ErrorMessage = "An unexpected error occurred. Please try again.";
            }
            finally
            {
                IsSubmitting = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Closes the modal.
        /// </summary>
        private async Task CloseModal()
        {
            if (IsSubmitting) return;

            SelectedProjectIds.Clear();
            ErrorMessage = null;
            await OnClose.InvokeAsync();
        }

        /// <summary>
        /// Handles backdrop click events.
        /// </summary>
        private async Task HandleBackdropClick()
        {
            if (CloseOnBackdropClick && !IsSubmitting && !IsLoading)
            {
                await CloseModal();
            }
        }
    }
}
