using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Features.Projects.Models;
using TargetBrowse.Features.Projects.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.Projects.Pages;

public partial class ProjectDetail : ComponentBase
{
    #region Dependency Injection

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IProjectService ProjectService { get; set; } = default!;

    [Inject]
    private IProjectGuideService GuideService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IMessageCenterService MessageCenter { get; set; } = default!;

    [Inject]
    private ILogger<ProjectDetail> Logger { get; set; } = default!;

    #endregion

    #region Route Parameters

    [Parameter]
    public Guid Id { get; set; }

    #endregion

    #region State Properties

    private ProjectDetailViewModel? Project = null;
    private bool IsLoading = false;
    private bool IsGeneratingGuide = false;
    private bool CanGenerate = false;
    private int DailyCallCount = 0;
    private string? CurrentUserId;
    private string? ErrorMessage;

    // Modal states
    private bool ShowEditModalState = false;
    private bool ShowDeleteModalState = false;
    private bool ShowRemoveConfirmation = false;
    private ProjectVideoViewModel? VideoToRemove = null;
    private bool IsRemovingVideo = false;
    private ProjectEditViewModel? ProjectToEdit = null;
    private ProjectDeleteViewModel? ProjectToDelete = null;

    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        CurrentUserId = await AuthenticationHelper.GetCurrentUserIdAsync(AuthenticationStateTask, Logger);
        await LoadProjectAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Reload if the ID parameter changes
        if (Project != null && Project.Id != Id)
        {
            await LoadProjectAsync();
        }
    }

    #endregion

    #region Data Loading

    private async Task LoadProjectAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            StateHasChanged();

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                ErrorMessage = "User not authenticated.";
                return;
            }

            // Load project details
            Project = await ProjectService.GetProjectDetailAsync(Id, CurrentUserId);

            // Check daily limit
            DailyCallCount = await GuideService.GetDailyAICallCountAsync(CurrentUserId);
            CanGenerate = DailyCallCount < 10;
        }
        catch (InvalidOperationException ex)
        {
            // Project not found or unauthorized
            ErrorMessage = ex.Message;
            Project = null;
            Logger.LogWarning(ex, "Project {ProjectId} not found or unauthorized for user {UserId}", Id, CurrentUserId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load project: {ex.Message}";
            Logger.LogError(ex, "Error loading project {ProjectId} for user {UserId}", Id, CurrentUserId);
            await MessageCenter.ShowErrorAsync(ErrorMessage);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task RetryLoad()
    {
        await LoadProjectAsync();
    }

    #endregion

    #region Modal Management

    private void ShowEditModal()
    {
        if (Project != null)
        {
            // Convert ProjectDetailViewModel to ProjectEditViewModel
            ProjectToEdit = new ProjectEditViewModel
            {
                Id = Project.Id,
                Name = Project.Name,
                Description = Project.Description,
                UserGuidance = Project.UserGuidance
            };
            ShowEditModalState = true;
            StateHasChanged();
        }
    }

    private void CloseModals()
    {
        ShowEditModalState = false;
        ProjectToEdit = null;
        StateHasChanged();
    }

    private async Task ShowDeleteModal()
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MessageCenter.ShowErrorAsync("User not authenticated.");
                return;
            }

            // Fetch the project view model for deletion
            ProjectToDelete = await ProjectService.GetProjectForDeleteAsync(Id, CurrentUserId);

            if (ProjectToDelete == null)
            {
                await MessageCenter.ShowErrorAsync("Project not found or you don't have permission to delete it.");
                return;
            }

            ShowDeleteModalState = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading project {ProjectId} for delete", Id);
            await MessageCenter.ShowErrorAsync("Failed to load project details.");
        }
    }

    private void CloseDeleteModal()
    {
        ShowDeleteModalState = false;
        ProjectToDelete = null;
        StateHasChanged();
    }

    #endregion

    #region Project Operations

    private async Task HandleUpdateProject(UpdateProjectRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MessageCenter.ShowErrorAsync("User not authenticated.");
                return;
            }

            await ProjectService.UpdateProjectAsync(
                request.Id,
                CurrentUserId,
                request.Name,
                request.Description,
                request.UserGuidance);

            await MessageCenter.ShowSuccessAsync("Project updated successfully!");

            ShowEditModalState = false;
            await LoadProjectAsync();
        }
        catch (ArgumentException ex)
        {
            await MessageCenter.ShowWarningAsync(ex.Message);
            Logger.LogWarning(ex, "Validation error updating project {ProjectId} for user {UserId}", request.Id, CurrentUserId);
        }
        catch (InvalidOperationException ex)
        {
            await MessageCenter.ShowErrorAsync(ex.Message);
            Logger.LogError(ex, "Business logic error updating project {ProjectId} for user {UserId}", request.Id, CurrentUserId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating project {ProjectId} for user {UserId}", request.Id, CurrentUserId);
            await MessageCenter.ShowErrorAsync("An unexpected error occurred. Please try again.");
        }
    }

    private async Task HandleDeleteConfirm()
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MessageCenter.ShowErrorAsync("User not authenticated.");
                return;
            }

            if (ProjectToDelete == null)
            {
                await MessageCenter.ShowErrorAsync("No project selected for deletion.");
                return;
            }

            await ProjectService.DeleteProjectAsync(ProjectToDelete.Id, CurrentUserId);
            await MessageCenter.ShowSuccessAsync("Project deleted successfully!");

            ShowDeleteModalState = false;
            ProjectToDelete = null;

            // Navigate back to projects list
            Navigation.NavigateTo("/projects");
        }
        catch (InvalidOperationException ex)
        {
            // Business logic errors (e.g., project not found, not owned by user)
            await MessageCenter.ShowErrorAsync(ex.Message);
            Logger.LogError(ex, "Business logic error deleting project {ProjectId} for user {UserId}", ProjectToDelete?.Id, CurrentUserId);
        }
        catch (Exception ex)
        {
            // Unexpected errors
            Logger.LogError(ex, "Error deleting project {ProjectId} for user {UserId}", ProjectToDelete?.Id, CurrentUserId);
            await MessageCenter.ShowErrorAsync("An unexpected error occurred while deleting the project. Please try again.");
        }
    }

    #endregion

    #region Video Operations

    private void HandleRemoveVideo(Guid videoId)
    {
        if (Project == null) return;

        VideoToRemove = Project.Videos.FirstOrDefault(v => v.Id == videoId);
        if (VideoToRemove != null)
        {
            ShowRemoveConfirmation = true;
            StateHasChanged();
        }
    }

    private void CloseRemoveConfirmation()
    {
        if (!IsRemovingVideo)
        {
            ShowRemoveConfirmation = false;
            VideoToRemove = null;
            StateHasChanged();
        }
    }

    private async Task ConfirmRemoveVideo()
    {
        if (VideoToRemove == null || string.IsNullOrEmpty(CurrentUserId))
        {
            return;
        }

        try
        {
            IsRemovingVideo = true;
            StateHasChanged();

            await ProjectService.RemoveVideoFromProjectAsync(Id, VideoToRemove.Id, CurrentUserId);

            await MessageCenter.ShowSuccessAsync($"'{VideoToRemove.Title}' removed from project.");

            ShowRemoveConfirmation = false;
            VideoToRemove = null;
            await LoadProjectAsync();
        }
        catch (InvalidOperationException ex)
        {
            await MessageCenter.ShowErrorAsync(ex.Message);
            Logger.LogError(ex, "Error removing video {VideoId} from project {ProjectId} for user {UserId}",
                VideoToRemove?.Id, Id, CurrentUserId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error removing video {VideoId} from project {ProjectId} for user {UserId}",
                VideoToRemove?.Id, Id, CurrentUserId);
            await MessageCenter.ShowErrorAsync("An unexpected error occurred. Please try again.");
        }
        finally
        {
            IsRemovingVideo = false;
            StateHasChanged();
        }
    }

    #endregion

    #region Guide Operations

    private async Task HandleGenerateGuide()
    {
        if (Project == null || string.IsNullOrEmpty(CurrentUserId))
        {
            return;
        }

        try
        {
            IsGeneratingGuide = true;
            StateHasChanged();

            // Pre-check if guide can be generated
            var canGenerateResult = await GuideService.CanGenerateGuideAsync(Id, CurrentUserId);
            if (!canGenerateResult)
            {
                await MessageCenter.ShowWarningAsync("Cannot generate guide at this time.");
                return;
            }

            // Generate the guide
            await GuideService.GenerateGuideAsync(Id, CurrentUserId);

            await MessageCenter.ShowSuccessAsync("Project guide generated successfully!");

            // Reload to get the new guide
            await LoadProjectAsync();
        }
        catch (InvalidOperationException ex)
        {
            // Business logic errors (missing summaries, daily limit, etc.)
            await MessageCenter.ShowErrorAsync(ex.Message);
            Logger.LogWarning(ex, "Cannot generate guide for project {ProjectId}, user {UserId}", Id, CurrentUserId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating guide for project {ProjectId}, user {UserId}", Id, CurrentUserId);
            await MessageCenter.ShowErrorAsync("Failed to generate guide. Please try again later.");
        }
        finally
        {
            IsGeneratingGuide = false;
            StateHasChanged();
        }
    }

    private async Task HandleRegenerateGuide()
    {
        if (Project == null || string.IsNullOrEmpty(CurrentUserId))
        {
            return;
        }

        try
        {
            IsGeneratingGuide = true;
            StateHasChanged();

            // Check if user can regenerate (daily limit)
            if (!CanGenerate)
            {
                await MessageCenter.ShowWarningAsync("You've reached your daily limit of AI operations. Please try again tomorrow.");
                return;
            }

            // Regenerate the guide
            await GuideService.RegenerateGuideAsync(Id, CurrentUserId);

            await MessageCenter.ShowSuccessAsync("Project guide regenerated successfully!");

            // Reload to get the updated guide
            await LoadProjectAsync();
        }
        catch (InvalidOperationException ex)
        {
            // Business logic errors
            await MessageCenter.ShowErrorAsync(ex.Message);
            Logger.LogWarning(ex, "Cannot regenerate guide for project {ProjectId}, user {UserId}", Id, CurrentUserId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error regenerating guide for project {ProjectId}, user {UserId}", Id, CurrentUserId);
            await MessageCenter.ShowErrorAsync("Failed to regenerate guide. Please try again later.");
        }
        finally
        {
            IsGeneratingGuide = false;
            StateHasChanged();
        }
    }

    #endregion
}
