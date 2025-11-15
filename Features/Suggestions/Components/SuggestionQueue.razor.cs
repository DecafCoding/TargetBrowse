using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Features.Suggestions.Services;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Suggestions.Components;

/// <summary>
/// Component for displaying and managing the user's suggestion queue.
/// Provides filtering, sorting, batch operations, and individual suggestion actions.
/// </summary>
public partial class SuggestionQueue : ComponentBase
{

    [Inject] protected ISuggestionService SuggestionService { get; set; } = default!;
    [Inject] protected IMessageCenterService MessageCenter { get; set; } = default!;
    [Inject] protected ILogger<SuggestionQueue> Logger { get; set; } = default!;

    #region Parameters and Cascading Parameters

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter]
    public EventCallback OnSuggestionsChanged { get; set; }

    #endregion

    #region Private Fields

    private SuggestionQueueModel QueueModel { get; set; } = new();
    private bool ShowBatchMode { get; set; } = false;
    private bool HasMoreSuggestions { get; set; } = false;
    private string? CurrentUserId { get; set; }
    private int CurrentPage { get; set; } = 1;
    private const int PageSize = 20; // Number of suggestions per page
    private bool IsLoadingMore { get; set; } = false;

    #endregion

    #region Component Lifecycle

    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserIdAsync();
        await LoadSuggestionsAsync();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Public method to refresh suggestions from parent component.
    /// </summary>
    public async Task RefreshSuggestionsAsync()
    {
        await LoadSuggestionsAsync();
    }

    #endregion

    #region Private Methods - Authentication

    private async Task GetCurrentUserIdAsync()
    {
        try
        {
            var authState = await AuthenticationStateTask!;
            CurrentUserId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get current user ID");
            CurrentUserId = null;
        }
    }

    #endregion

    #region Data Loading


    private async Task LoadSuggestionsAsync(bool isLoadMore = false)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            QueueModel.ErrorMessage = "Please log in to view suggestions";
            return;
        }

        try
        {
            if (!isLoadMore)
            {
                QueueModel.IsLoading = true;
                CurrentPage = 1; // Reset to first page for fresh load
            }
            else
            {
                IsLoadingMore = true;
            }

            QueueModel.ErrorMessage = null;
            StateHasChanged();

            // Use pageNumber and pageSize as expected by the service
            var suggestions = await SuggestionService.GetPendingSuggestionsAsync(CurrentUserId, CurrentPage, PageSize);

            if (!isLoadMore)
            {
                // Fresh load - replace all suggestions
                QueueModel.Suggestions = suggestions.ToList();
            }
            else
            {
                // Load more - append to existing suggestions
                QueueModel.Suggestions.AddRange(suggestions);
            }

            // Determine if there are more suggestions available
            HasMoreSuggestions = suggestions.Count == PageSize;

            if (!isLoadMore)
            {
                await OnSuggestionsChanged.InvokeAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load suggestions for user {UserId}", CurrentUserId);
            QueueModel.ErrorMessage = "Failed to load suggestions. Please try again.";
            await MessageCenter.ShowErrorAsync("Failed to load suggestions. Please try again.");
        }
        finally
        {
            QueueModel.IsLoading = false;
            IsLoadingMore = false;
            StateHasChanged();
        }
    }

    private async Task RefreshSuggestions()
    {
        await LoadSuggestionsAsync();
    }

private async Task LoadMoreSuggestions()
{
    if (IsLoadingMore) return; // Prevent multiple concurrent loads
    
    CurrentPage++; // Move to next page
    await LoadSuggestionsAsync(isLoadMore: true);
}

    #endregion

    #region Filter and Sort

    private void SetFilter(SuggestionFilter filter)
    {
        QueueModel.Filter = filter;
        StateHasChanged();
    }

    private void SetSort(SuggestionSort sort)
    {
        QueueModel.SortBy = sort;
        StateHasChanged();
    }

    private void ClearFilters()
    {
        QueueModel.Filter = SuggestionFilter.All;
        QueueModel.SortBy = SuggestionSort.CreatedDesc;
        StateHasChanged();
    }

    private string GetFilterDisplayText()
    {
        return QueueModel.Filter switch
        {
            SuggestionFilter.All => "All",
            SuggestionFilter.BothSources => "High Priority",
            SuggestionFilter.ChannelOnly => "Channels",
            SuggestionFilter.TopicOnly => "Topics",
            SuggestionFilter.NearExpiry => "Expiring",
            _ => "Filter"
        };
    }

    #endregion

    #region Batch Actions

    private void ToggleBatchMode()
    {
        ShowBatchMode = !ShowBatchMode;
        if (!ShowBatchMode)
        {
            QueueModel.ClearSelection();
        }
        StateHasChanged();
    }

    private void SelectAll()
    {
        QueueModel.SelectAll();
        StateHasChanged();
    }

    private void ClearSelection()
    {
        QueueModel.ClearSelection();
        StateHasChanged();
    }

    private void HandleSelectionChanged(SuggestionDisplayModel suggestion)
    {
        QueueModel.ToggleSelection(suggestion.Id);
        StateHasChanged();
    }

    private async Task HandleBatchApproved(HashSet<Guid> approvedIds)
    {
        QueueModel.RemoveSuggestions(approvedIds);
        ShowBatchMode = false;
        await OnSuggestionsChanged.InvokeAsync();
        StateHasChanged();
    }

    private async Task HandleBatchDenied(HashSet<Guid> deniedIds)
    {
        QueueModel.RemoveSuggestions(deniedIds);
        ShowBatchMode = false;
        await OnSuggestionsChanged.InvokeAsync();
        StateHasChanged();
    }

    #endregion

    #region Individual Actions

    private async Task HandleSuggestionApproved(SuggestionDisplayModel suggestion)
    {
        QueueModel.RemoveSuggestions(new[] { suggestion.Id });
        await OnSuggestionsChanged.InvokeAsync();
        StateHasChanged();
    }

    private async Task HandleSuggestionDenied(SuggestionDisplayModel suggestion)
    {
        QueueModel.RemoveSuggestions(new[] { suggestion.Id });
        await OnSuggestionsChanged.InvokeAsync();
        StateHasChanged();
    }

    #endregion
}