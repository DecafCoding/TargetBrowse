using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Features.Channels.Services;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Channels.Components;

/// <summary>
/// Component for displaying a single channel in card format.
/// Provides functionality for rating and removing channels from tracking.
/// </summary>
public partial class ChannelCard : ComponentBase
{
    #region Injected Services

    [Inject] protected IChannelService ChannelService { get; set; } = default!;
    [Inject] protected IChannelRatingService ChannelRatingService { get; set; } = default!;
    [Inject] protected IMessageCenterService MessageCenter { get; set; } = default!;
    [Inject] protected ILogger<ChannelCard> Logger { get; set; } = default!;

    #endregion

    #region Parameters

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter, EditorRequired]
    public ChannelDisplayModel Channel { get; set; } = null!;

    [Parameter]
    public EventCallback<ChannelDisplayModel> OnChannelDeleted { get; set; }

    [Parameter]
    public EventCallback<ChannelDisplayModel> OnChannelRatingRequested { get; set; }

    #endregion

    #region Private Fields

    private bool IsDeleting = false;
    private bool ShowDeleteModal = false;
    private string? CurrentUserId;
    private ChannelRatingModal? RatingModal;

    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserIdAsync();
    }

    #endregion

    #region Authentication Methods

    /// <summary>
    /// Gets the current authenticated user's ID.
    /// </summary>
    private async Task GetCurrentUserIdAsync()
    {
        try
        {
            var authState = await AuthenticationStateTask!;
            CurrentUserId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        catch (Exception)
        {
            CurrentUserId = null;
        }
    }

    #endregion

    #region Delete Methods

    /// <summary>
    /// Shows the delete confirmation modal.
    /// </summary>
    private void ShowDeleteConfirmation()
    {
        if (IsDeleting) return;

        ShowDeleteModal = true;
        StateHasChanged();
    }

    /// <summary>
    /// Hides the delete confirmation modal.
    /// </summary>
    private void HideDeleteConfirmation()
    {
        if (IsDeleting) return;

        ShowDeleteModal = false;
        StateHasChanged();
    }

    /// <summary>
    /// Confirms the deletion and removes the channel.
    /// </summary>
    private async Task ConfirmDelete()
    {
        if (IsDeleting || string.IsNullOrEmpty(CurrentUserId)) return;

        try
        {
            IsDeleting = true;
            StateHasChanged();

            var success = await ChannelService.RemoveChannelFromTrackingAsync(CurrentUserId, Channel.Id);

            if (success)
            {
                await MessageCenter.ShowSuccessAsync($"Removed '{Channel.Name}' from tracking");
                await OnChannelDeleted.InvokeAsync(Channel);
                HideDeleteConfirmation();
            }
            else
            {
                await MessageCenter.ShowErrorAsync("Failed to remove channel. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing channel {ChannelId} for user {UserId}",
                Channel.Id, CurrentUserId);
            await MessageCenter.ShowErrorAsync($"Error removing channel: {ex.Message}");
        }
        finally
        {
            IsDeleting = false;
            StateHasChanged();
        }
    }

    #endregion

    #region Rating Methods

    /// <summary>
    /// Shows the rating modal for creating a new rating.
    /// Called by ChannelRatingDisplay component when user clicks on empty stars.
    /// </summary>
    private async Task ShowRatingModal()
    {
        if (OnChannelRatingRequested.HasDelegate)
        {
            // Notify parent to show rating modal
            await OnChannelRatingRequested.InvokeAsync(Channel);
        }
    }

    /// <summary>
    /// Shows the rating modal for editing an existing rating.
    /// Called by ChannelRatingDisplay component when user clicks on filled stars.
    /// </summary>
    private async Task ShowEditRatingModal()
    {
        if (OnChannelRatingRequested.HasDelegate)
        {
            // Notify parent to show rating modal for editing
            await OnChannelRatingRequested.InvokeAsync(Channel);
        }
    }

    /// <summary>
    /// Handles rating events from the ChannelRatingDisplay component.
    /// </summary>
    private async Task HandleRatingClick()
    {
        if (Channel.UserRating != null)
        {
            await ShowEditRatingModal();
        }
        else
        {
            await ShowRatingModal();
        }
    }

    #endregion
}