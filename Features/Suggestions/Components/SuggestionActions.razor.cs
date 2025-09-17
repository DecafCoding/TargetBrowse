using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Features.Suggestions.Services;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Suggestions.Components;

public partial class SuggestionActions : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter] public ActionMode Mode { get; set; } = ActionMode.Individual;
    [Parameter] public SuggestionDisplayModel? Suggestion { get; set; }
    [Parameter] public HashSet<Guid> SelectedSuggestionIds { get; set; } = new();
    [Parameter] public EventCallback<SuggestionDisplayModel> OnSuggestionApproved { get; set; }
    [Parameter] public EventCallback<SuggestionDisplayModel> OnSuggestionDenied { get; set; }
    [Parameter] public EventCallback<HashSet<Guid>> OnBatchApproved { get; set; }
    [Parameter] public EventCallback<HashSet<Guid>> OnBatchDenied { get; set; }

    [Inject] private ISuggestionService SuggestionService { get; set; } = default!;
    [Inject] private IMessageCenterService MessageCenter { get; set; } = default!;
    [Inject] private ILogger<SuggestionActions> Logger { get; set; } = default!;

    private bool IsProcessing { get; set; } = false;
    private string? PendingAction { get; set; }
    private bool ShowConfirmModal { get; set; } = false;
    private string? ConfirmAction { get; set; }
    private string? CurrentUserId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserIdAsync();
    }

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

    #region Individual Actions

    private async Task ApproveSuggestion()
    {
        if (Suggestion == null || string.IsNullOrEmpty(CurrentUserId) || IsProcessing)
            return;

        await ProcessSuggestionAction("approve", async () =>
        {
            var success = await SuggestionService.ApproveSuggestionAsync(CurrentUserId, Suggestion.Id);
            if (success)
            {
                await MessageCenter.ShowSuccessAsync($"Added '{Suggestion.Video.Title}' to your library!");
                await OnSuggestionApproved.InvokeAsync(Suggestion);
            }
            else
            {
                await MessageCenter.ShowErrorAsync("Failed to approve suggestion");
            }
            return success;
        });
    }

    private async Task DenySuggestion()
    {
        if (Suggestion == null || string.IsNullOrEmpty(CurrentUserId) || IsProcessing)
            return;

        await ProcessSuggestionAction("deny", async () =>
        {
            var success = await SuggestionService.DenySuggestionAsync(CurrentUserId, Suggestion.Id);
            if (success)
            {
                await MessageCenter.ShowSuccessAsync("Suggestion removed from your queue");
                await OnSuggestionDenied.InvokeAsync(Suggestion);
            }
            else
            {
                await MessageCenter.ShowErrorAsync("Failed to remove suggestion");
            }
            return success;
        });
    }

    #endregion

    #region Batch Actions

    private void ApproveSelected()
    {
        if (!SelectedSuggestionIds.Any() || IsProcessing)
            return;

        ConfirmAction = "approve-batch";
        ShowConfirmModal = true;
        StateHasChanged();
    }

    private void DenySelected()
    {
        if (!SelectedSuggestionIds.Any() || IsProcessing)
            return;

        ConfirmAction = "deny-batch";
        ShowConfirmModal = true;
        StateHasChanged();
    }

    private void CancelConfirmation()
    {
        if (IsProcessing) return;

        ShowConfirmModal = false;
        ConfirmAction = null;
        StateHasChanged();
    }

    private async Task ConfirmBatchAction()
    {
        if (string.IsNullOrEmpty(CurrentUserId) || IsProcessing || !SelectedSuggestionIds.Any())
            return;

        if (ConfirmAction == "approve-batch")
        {
            await ProcessSuggestionAction("approve-batch", async () =>
            {
                var successCount = 0;
                var totalCount = SelectedSuggestionIds.Count;

                foreach (var suggestionId in SelectedSuggestionIds.ToList())
                {
                    var success = await SuggestionService.ApproveSuggestionAsync(CurrentUserId, suggestionId);
                    if (success) successCount++;
                }

                if (successCount > 0)
                {
                    var message = successCount == totalCount
                        ? $"Approved {successCount} suggestion{(successCount > 1 ? "s" : "")} and added to your library!"
                        : $"Approved {successCount} of {totalCount} suggestions. Some may have failed.";
                    await MessageCenter.ShowSuccessAsync(message);
                    await OnBatchApproved.InvokeAsync(new HashSet<Guid>(SelectedSuggestionIds));
                }
                else
                {
                    await MessageCenter.ShowErrorAsync("Failed to approve any suggestions");
                }
                return successCount > 0;
            });
        }
        else if (ConfirmAction == "deny-batch")
        {
            await ProcessSuggestionAction("deny-batch", async () =>
            {
                var successCount = 0;
                var totalCount = SelectedSuggestionIds.Count;

                foreach (var suggestionId in SelectedSuggestionIds.ToList())
                {
                    var success = await SuggestionService.DenySuggestionAsync(CurrentUserId, suggestionId);
                    if (success) successCount++;
                }

                if (successCount > 0)
                {
                    var message = successCount == totalCount
                        ? $"Removed {successCount} suggestion{(successCount > 1 ? "s" : "")} from your queue"
                        : $"Removed {successCount} of {totalCount} suggestions. Some may have failed.";
                    await MessageCenter.ShowSuccessAsync(message);
                    await OnBatchDenied.InvokeAsync(new HashSet<Guid>(SelectedSuggestionIds));
                }
                else
                {
                    await MessageCenter.ShowErrorAsync("Failed to remove any suggestions");
                }
                return successCount > 0;
            });
        }

        ShowConfirmModal = false;
        ConfirmAction = null;
    }

    #endregion

    #region Helper Methods

    private async Task ProcessSuggestionAction(string action, Func<Task<bool>> actionHandler)
    {
        try
        {
            IsProcessing = true;
            PendingAction = action;
            StateHasChanged();

            await actionHandler();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing suggestion action: {Action}", action);
            await MessageCenter.ShowErrorAsync("An unexpected error occurred. Please try again.");
        }
        finally
        {
            IsProcessing = false;
            PendingAction = null;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Constructs the YouTube URL for a video.
    /// </summary>
    private static string GetYouTubeUrl(VideoInfo video)
    {
        return $"https://www.youtube.com/watch?v={video.YouTubeVideoId}";
    }

    #endregion
}