using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Services.ProjectServices;
using TargetBrowse.Services.ProjectServices.Models;
using TargetBrowse.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace TargetBrowse.Components.Shared
{
    /// <summary>
    /// Modal component for adding a video to a project.
    /// User clicks a project card to immediately add the video and close the modal.
    /// </summary>
    public partial class AddToProjectModal : ComponentBase
    {
        [Inject] private IAddToProjectService AddToProjectService { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] private IOptions<ProjectSettings> ProjectSettings { get; set; } = default!;
        [Inject] private IMessageCenterService MessageCenter { get; set; } = default!;
        [Inject] private ILogger<AddToProjectModal> Logger { get; set; } = default!;

        /// <summary>
        /// Whether the modal is visible.
        /// </summary>
        [Parameter] public bool IsVisible { get; set; }

        /// <summary>
        /// The ID of the video to add to a project.
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
        /// Callback when the video is successfully added to a project.
        /// Passes the result with details about the operation.
        /// </summary>
        [Parameter] public EventCallback<AddToProjectResult> OnSuccess { get; set; }

        /// <summary>
        /// Whether to allow clicking the backdrop to close the modal.
        /// </summary>
        [Parameter] public bool CloseOnBackdropClick { get; set; } = true;

        private List<ProjectInfo> Projects { get; set; } = new();

        // Tracks which project card is currently being submitted — null means idle
        private Guid? SubmittingProjectId { get; set; }

        private bool IsLoading { get; set; }
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
                Projects.Clear();
                SubmittingProjectId = null;
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
                StateHasChanged();

                var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
                CurrentUserId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(CurrentUserId))
                {
                    await MessageCenter.ShowErrorAsync("Unable to identify current user.");
                    await CloseModal();
                    return;
                }

                Projects = await AddToProjectService.GetUserProjectsAsync(CurrentUserId, VideoId);

                Logger.LogInformation("Loaded {Count} projects for user {UserId}", Projects.Count, CurrentUserId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading projects for user {UserId}", CurrentUserId);
                await MessageCenter.ShowErrorAsync("Failed to load projects. Please try again.");
                await CloseModal();
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Handles a project card click — immediately adds the video to the selected project.
        /// </summary>
        private async Task HandleProjectClick(Guid projectId)
        {
            // Guard: ignore clicks while another submission is in flight
            if (SubmittingProjectId.HasValue || string.IsNullOrEmpty(CurrentUserId))
                return;

            try
            {
                SubmittingProjectId = projectId;
                StateHasChanged();

                var request = new AddToProjectRequest
                {
                    VideoId = VideoId,
                    VideoInfo = VideoInfo,
                    ProjectIds = new List<Guid> { projectId },
                    UserId = CurrentUserId
                };

                var result = await AddToProjectService.AddVideoToProjectsAsync(request);

                if (result.Success)
                {
                    Logger.LogInformation("Successfully added video {VideoId} to project {ProjectId}",
                        VideoId, projectId);

                    await OnSuccess.InvokeAsync(result);
                    await CloseModal();
                }
                else
                {
                    var errorMessage = result.ProjectErrors.Any()
                        ? string.Join("; ", result.ProjectErrors.Values.Take(3))
                        : result.ErrorMessage ?? "Failed to add video to project.";

                    Logger.LogWarning("Failed to add video {VideoId} to project {ProjectId}: {Error}",
                        VideoId, projectId, errorMessage);

                    await MessageCenter.ShowErrorAsync(errorMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error adding video {VideoId} to project {ProjectId}", VideoId, projectId);
                await MessageCenter.ShowErrorAsync("An unexpected error occurred. Please try again.");
            }
            finally
            {
                SubmittingProjectId = null;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Closes the modal and resets state.
        /// </summary>
        private async Task CloseModal()
        {
            // Don't allow closing while a submission is in flight
            if (SubmittingProjectId.HasValue) return;

            Projects.Clear();
            SubmittingProjectId = null;
            await OnClose.InvokeAsync();
        }

        /// <summary>
        /// Handles backdrop click events.
        /// </summary>
        private async Task HandleBackdropClick()
        {
            if (CloseOnBackdropClick && !SubmittingProjectId.HasValue && !IsLoading)
            {
                await CloseModal();
            }
        }
    }
}
