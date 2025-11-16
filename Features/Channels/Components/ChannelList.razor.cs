using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Features.Channels.Services;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.Channels.Components;

/// <summary>
/// Component for displaying and managing a list of tracked YouTube channels.
/// Provides functionality for viewing, rating, and removing channels from tracking.
/// </summary>
public partial class ChannelList : ComponentBase
{
    #region Injected Services

    [Inject] private IChannelService ChannelService { get; set; } = default!;
    [Inject] private IChannelRatingService ChannelRatingService { get; set; } = default!;

    #endregion

    #region Parameters

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter]
    public EventCallback OnChannelsChanged { get; set; }

    [Parameter]
    public List<ChannelDisplayModel> SearchResults { get; set; } = new();

    #endregion

    #region Private Fields

    private List<ChannelDisplayModel> TrackedChannels { get; set; } = new();
    private bool IsLoading { get; set; } = true;
    private bool IsRemoving { get; set; } = false;
    private bool _showRemoveModal { get; set; } = false;
    private ChannelDisplayModel? _channelToRemove { get; set; }
    private ChannelRatingModal? RatingModal { get; set; }
    private string? CurrentUserId;

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the count of tracked channels.
    /// </summary>
    public int TrackedChannelCount => TrackedChannels?.Count ?? 0;

    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        CurrentUserId = await AuthenticationHelper.GetCurrentUserIdAsync(AuthenticationStateTask);
        await LoadTrackedChannelsAsync();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Public method to refresh the tracked channels list.
    /// Called by parent component when channels are added/modified.
    /// </summary>
    public async Task RefreshAsync()
    {
        await LoadTrackedChannelsAsync();
    }

    #endregion

    #region Rating Methods

    /// <summary>
    /// Shows the rating modal for creating a new rating.
    /// Called from ChannelCard components via ChannelRatingDisplay.
    /// </summary>
    private void ShowRatingModal(ChannelDisplayModel channel)
    {
        RatingModal?.ShowForNewRating(channel);
    }

    /// <summary>
    /// Shows the rating modal for editing an existing rating.
    /// Called from ChannelCard components via ChannelRatingDisplay.
    /// </summary>
    private void ShowEditRatingModal(ChannelDisplayModel channel)
    {
        if (channel.UserRating != null)
        {
            RatingModal?.ShowForEditRating(channel, channel.UserRating);
        }
    }

    /// <summary>
    /// Handles when a new rating is created.
    /// </summary>
    private async Task HandleRatingCreated(ChannelRatingModel newRating)
    {
        await LoadTrackedChannelsAsync(); // Refresh to show the new rating
    }

    /// <summary>
    /// Handles when a rating is updated.
    /// </summary>
    private async Task HandleRatingUpdated(ChannelRatingModel updatedRating)
    {
        await LoadTrackedChannelsAsync(); // Refresh to show the updated rating
    }

    /// <summary>
    /// Handles when a rating is deleted.
    /// </summary>
    private async Task HandleRatingDeleted(Guid deletedRatingId)
    {
        await LoadTrackedChannelsAsync(); // Refresh to remove the rating display
    }

    #endregion

    #region Remove Channel Methods

    /// <summary>
    /// Shows the remove confirmation modal for the specified channel.
    /// </summary>
    private void ShowRemoveConfirmation(ChannelDisplayModel channel)
    {
        if (IsRemoving) return;

        _channelToRemove = channel;
        _showRemoveModal = true;
        StateHasChanged();
    }

    /// <summary>
    /// Hides the remove confirmation modal and resets state.
    /// </summary>
    private void HideRemoveConfirmation()
    {
        if (IsRemoving) return;

        _showRemoveModal = false;
        _channelToRemove = null;
        StateHasChanged();
    }

    /// <summary>
    /// Confirms the removal and calls the service to remove the channel.
    /// </summary>
    private async Task ConfirmRemove()
    {
        if (IsRemoving || _channelToRemove == null) return;

        try
        {
            IsRemoving = true;
            StateHasChanged();

            // Get current user
            var authState = await AuthenticationStateTask!;
            var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                var success = await ChannelService.RemoveChannelFromTrackingAsync(userId, _channelToRemove.Id);

                if (success)
                {
                    // Refresh the channels list to reflect the removal
                    await LoadTrackedChannelsAsync();

                    // Notify parent component of channel changes
                    if (OnChannelsChanged.HasDelegate)
                    {
                        await OnChannelsChanged.InvokeAsync();
                    }
                }
            }
        }
        finally
        {
            IsRemoving = false;
            HideRemoveConfirmation();
        }
    }

    #endregion

    #region Data Loading Methods

    /// <summary>
    /// Loads tracked channels with their ratings for the current user.
    /// </summary>
    private async Task LoadTrackedChannelsAsync()
    {
        try
        {
            IsLoading = true;
            StateHasChanged();

            var authState = await AuthenticationStateTask!;
            var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                // Get tracked channels
                TrackedChannels = await ChannelService.GetTrackedChannelsAsync(userId);

                // Load ratings for each channel
                foreach (var channel in TrackedChannels)
                {
                    channel.UserRating = await ChannelRatingService.GetUserRatingAsync(userId, channel.Id);
                }

                // Notify parent component of channel changes (for count updates)
                if (OnChannelsChanged.HasDelegate)
                {
                    await OnChannelsChanged.InvokeAsync();
                }
            }
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    #endregion

    #region ChannelCard Event Handlers

    /// <summary>
    /// Handles when a channel is deleted from a ChannelCard component.
    /// </summary>
    private async Task HandleChannelDeleted(ChannelDisplayModel deletedChannel)
    {
        // Remove the channel from the local list
        TrackedChannels.RemoveAll(c => c.Id == deletedChannel.Id);

        // Notify parent component of channel changes
        if (OnChannelsChanged.HasDelegate)
        {
            await OnChannelsChanged.InvokeAsync();
        }

        StateHasChanged();
    }

    /// <summary>
    /// Handles when a channel rating is requested from a ChannelCard component.
    /// Opens the rating modal for the specified channel.
    /// </summary>
    private void HandleChannelRatingRequested(ChannelDisplayModel channel)
    {
        if (channel.UserRating != null)
        {
            ShowEditRatingModal(channel);
        }
        else
        {
            ShowRatingModal(channel);
        }
    }

    /// <summary>
    /// Handles adding a channel to tracking from search results.
    /// </summary>
    private async Task HandleAddChannelToTracking(ChannelDisplayModel channel)
    {
        try
        {
            // Get current user
            var authState = await AuthenticationStateTask!;
            var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            // Convert to AddChannelModel and add to tracking
            var addChannelModel = AddChannelModel.FromDisplayModel(channel);
            var success = await ChannelService.AddChannelToTrackingAsync(userId, addChannelModel);

            if (success)
            {
                // Refresh the tracked channels list
                await LoadTrackedChannelsAsync();

                // Notify parent component that a channel was added
                if (OnChannelsChanged.HasDelegate)
                {
                    await OnChannelsChanged.InvokeAsync();
                }
            }
        }
        catch (Exception)
        {
            // Handle error silently for now
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the appropriate CSS class for the progress bar based on channel count.
    /// </summary>
    public string GetProgressBarClass()
    {
        return TrackedChannels.Count switch
        {
            >= 45 => "bg-danger",
            >= 35 => "bg-warning",
            >= 25 => "bg-info",
            _ => "bg-success"
        };
    }

    #endregion
}