using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.Topics.Models;
using TargetBrowse.Features.Topics.Services;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Topics.Components;

/// <summary>
/// Base class for TopicList component handling topic display, deletion, and management.
/// Updated to use TopicDataService for improved performance on read operations.
/// </summary>
public partial class TopicList : ComponentBase
{
    #region Injected Services

    [Inject] protected ITopicService TopicService { get; set; } = default!;
    [Inject] protected ITopicDataService TopicDataService { get; set; } = default!; // Add data service

    #endregion

    #region Cascading Parameters

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    #endregion

    #region Parameters

    [Parameter]
    public EventCallback OnTopicsChanged { get; set; }

    #endregion

    #region Properties

    protected List<TopicDisplayModel> Topics { get; set; } = new();
    protected bool IsLoading { get; set; } = true;
    protected bool IsDeleting { get; set; } = false;
    protected bool _showDeleteModal { get; set; } = false;
    protected TopicDisplayModel? _topicToDelete { get; set; }

    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        await LoadTopicsAsync();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Public method to refresh the topic list.
    /// Called by parent component when topics are added/modified.
    /// </summary>
    public async Task RefreshAsync()
    {
        await LoadTopicsAsync();
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Shows the delete confirmation modal for the specified topic.
    /// </summary>
    protected void ShowDeleteConfirmation(TopicDisplayModel topic)
    {
        if (IsDeleting) return;

        _topicToDelete = topic;
        _showDeleteModal = true;
        StateHasChanged();
    }

    /// <summary>
    /// Hides the delete confirmation modal and resets state.
    /// </summary>
    protected void HideDeleteConfirmation()
    {
        if (IsDeleting) return;

        _showDeleteModal = false;
        _topicToDelete = null;
        StateHasChanged();
    }

    /// <summary>
    /// Confirms the deletion and calls the service to delete the topic.
    /// Uses TopicService for write operations (business logic).
    /// </summary>
    protected async Task ConfirmDelete()
    {
        if (IsDeleting || _topicToDelete == null) return;

        try
        {
            IsDeleting = true;
            StateHasChanged();

            var authState = await AuthenticationStateTask!;
            var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                // Use TopicService for delete operation (business logic)
                var success = await TopicService.DeleteTopicAsync(userId, _topicToDelete.Id);

                if (success)
                {
                    // Refresh the topic list to reflect the deletion
                    await LoadTopicsAsync();

                    // Notify parent component of topic changes
                    if (OnTopicsChanged.HasDelegate)
                    {
                        await OnTopicsChanged.InvokeAsync();
                    }
                }
            }
        }
        finally
        {
            IsDeleting = false;
            HideDeleteConfirmation();
        }
    }

    /// <summary>
    /// Gets the appropriate CSS class for the progress bar based on topic count.
    /// </summary>
    protected string GetProgressBarClass()
    {
        return Topics.Count switch
        {
            >= 9 => "bg-danger",
            >= 7 => "bg-warning",
            >= 5 => "bg-info",
            _ => "bg-success"
        };
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Loads topics for the current user from the data service.
    /// Updated to use TopicDataService for better performance.
    /// </summary>
    private async Task LoadTopicsAsync()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();

            var authState = await AuthenticationStateTask!;
            var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                // Use TopicDataService for read operations (better performance)
                var topicEntities = await TopicDataService.GetUserTopicsAsync(userId);
                
                // Convert entities to display models
                Topics = topicEntities.Select(ConvertToDisplayModel).ToList();

                // Notify parent component of topic changes (for count updates)
                if (OnTopicsChanged.HasDelegate)
                {
                    await OnTopicsChanged.InvokeAsync();
                }
            }
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Converts TopicEntity to TopicDisplayModel for UI display.
    /// </summary>
    private static TopicDisplayModel ConvertToDisplayModel(TopicEntity entity)
    {
        return new TopicDisplayModel
        {
            Id = entity.Id,
            Name = entity.Name,
            CreatedAt = entity.CreatedAt
        };
    }

    #endregion
}
