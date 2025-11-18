using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Projects.Models;
using TargetBrowse.Features.Projects.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.Projects.Pages;

public partial class ProjectsList : ComponentBase
{
    #region Dependency Injection

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IProjectService ProjectService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IMessageCenterService MessageCenter { get; set; } = default!;

    [Inject]
    private ILogger<ProjectsList> Logger { get; set; } = default!;

    #endregion

    #region State Properties

    private List<ProjectListViewModel> Projects = new();
    private bool IsLoading = false;
    private string? CurrentUserId;
    private string? ErrorMessage;

    // Modal states
    private bool ShowEditModalState = false;
    private bool ShowDeleteModalState = false;
    private ProjectEntity? ProjectToEdit = null;
    private ProjectEntity? ProjectToDelete = null;

    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        CurrentUserId = await AuthenticationHelper.GetCurrentUserIdAsync(AuthenticationStateTask, Logger);
        await LoadProjectsAsync();
    }

    #endregion

    #region Data Loading

    private async Task LoadProjectsAsync()
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

            Projects = await ProjectService.GetUserProjectsAsync(CurrentUserId);

            // Sort by LastModifiedAt descending
            Projects = Projects.OrderByDescending(p => p.LastModifiedAt).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load projects: {ex.Message}";
            Logger.LogError(ex, "Error loading projects for user {UserId}", CurrentUserId);
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
        await LoadProjectsAsync();
    }

    #endregion

    #region Modal Management

    private void ShowCreateModal()
    {
        ProjectToEdit = null;
        ShowEditModalState = true;
        StateHasChanged();
    }

    private async Task HandleEdit(Guid projectId)
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MessageCenter.ShowErrorAsync("User not authenticated.");
                return;
            }

            // Fetch the full project entity for editing
            ProjectToEdit = await ProjectService.GetProjectByIdAsync(projectId, CurrentUserId);

            if (ProjectToEdit == null)
            {
                await MessageCenter.ShowErrorAsync("Project not found or you don't have permission to edit it.");
                return;
            }

            ShowEditModalState = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading project {ProjectId} for edit", projectId);
            await MessageCenter.ShowErrorAsync("Failed to load project details.");
        }
    }

    private async Task HandleDelete(Guid projectId)
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MessageCenter.ShowErrorAsync("User not authenticated.");
                return;
            }

            // Fetch the full project entity for deletion
            ProjectToDelete = await ProjectService.GetProjectByIdAsync(projectId, CurrentUserId);

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
            Logger.LogError(ex, "Error loading project {ProjectId} for delete", projectId);
            await MessageCenter.ShowErrorAsync("Failed to load project details.");
        }
    }

    private void HandleView(Guid projectId)
    {
        Navigation.NavigateTo($"/projects/{projectId}");
    }

    private void CloseModals()
    {
        ShowEditModalState = false;
        ShowDeleteModalState = false;
        ProjectToEdit = null;
        ProjectToDelete = null;
        StateHasChanged();
    }

    #endregion

    #region CRUD Operations

    private async Task HandleSave(UpdateProjectRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentUserId))
            {
                await MessageCenter.ShowErrorAsync("User not authenticated.");
                return;
            }

            if (request.Id == Guid.Empty)
            {
                // Create new project
                await ProjectService.CreateProjectAsync(
                    CurrentUserId,
                    request.Name,
                    request.Description,
                    request.UserGuidance);

                await MessageCenter.ShowSuccessAsync("Project created successfully!");
            }
            else
            {
                // Update existing project
                await ProjectService.UpdateProjectAsync(
                    request.Id,
                    CurrentUserId,
                    request.Name,
                    request.Description,
                    request.UserGuidance);

                await MessageCenter.ShowSuccessAsync("Project updated successfully!");
            }

            ShowEditModalState = false;
            ProjectToEdit = null;
            await LoadProjectsAsync();
        }
        catch (ArgumentException ex)
        {
            // Validation errors
            await MessageCenter.ShowWarningAsync(ex.Message);
            Logger.LogWarning(ex, "Validation error saving project for user {UserId}", CurrentUserId);
        }
        catch (InvalidOperationException ex)
        {
            // Business logic errors
            await MessageCenter.ShowErrorAsync(ex.Message);
            Logger.LogError(ex, "Business logic error saving project for user {UserId}", CurrentUserId);
        }
        catch (Exception ex)
        {
            // Unexpected errors
            Logger.LogError(ex, "Error saving project for user {UserId}", CurrentUserId);
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
            await LoadProjectsAsync();
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
}
