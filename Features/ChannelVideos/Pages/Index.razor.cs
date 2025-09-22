using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.ChannelVideos.Models;
using TargetBrowse.Features.ChannelVideos.Services;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.ChannelVideos.Pages;

/// <summary>
/// Code-behind for the Channel Videos Index component. Handles the business logic
/// for displaying channel videos, managing loading states, and user interactions.
/// </summary>
public partial class Index : ComponentBase
{
    #region Parameters and Dependencies

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter]
    public string ChannelId { get; set; } = string.Empty;

    [Inject]
    protected IChannelVideosService ChannelVideosService { get; set; } = default!;

    [Inject]
    protected IMessageCenterService MessageCenter { get; set; } = default!;

    [Inject]
    protected ILogger<Index> Logger { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The main data model containing channel and video information.
    /// </summary>
    protected ChannelVideosModel Model { get; set; } = new();

    /// <summary>
    /// The current authenticated user's ID.
    /// </summary>
    protected string? CurrentUserId { get; set; }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the component by getting the current user and loading channel videos.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserIdAsync();
        await LoadChannelVideos();
    }

    /// <summary>
    /// Handles parameter changes, specifically when the channelId parameter changes.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        // Reload if channelId parameter changes
        if (Model.Channel.YouTubeChannelId != ChannelId)
        {
            await LoadChannelVideos();
        }
    }

    #endregion

    #region User Authentication

    /// <summary>
    /// Gets the current authenticated user's ID from the authentication state.
    /// </summary>
    private async Task GetCurrentUserIdAsync()
    {
        try
        {
            if (AuthenticationStateTask != null)
            {
                var authState = await AuthenticationStateTask;
                CurrentUserId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting current user ID");
            CurrentUserId = null;
        }
    }

    #endregion

    #region Data Loading

    /// <summary>
    /// Loads channel videos data from the service. Handles validation,
    /// error states, and logging throughout the process.
    /// TODO - This method should fist check LastCheckedDate and then deteremine
    /// if it needs to call the service or just load from the Db.
    /// </summary>
    private async Task LoadChannelVideos()
    {
        try
        {
            // Validate channel ID
            if (string.IsNullOrWhiteSpace(ChannelId))
            {
                Model.ErrorMessage = "Invalid channel ID provided.";
                await MessageCenter.ShowErrorAsync("Invalid channel ID in URL.");
                return;
            }

            // Validate user authentication
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                Model.ErrorMessage = "Please log in to view channel videos.";
                await MessageCenter.ShowErrorAsync("Please log in to access this feature.");
                return;
            }

            Logger.LogInformation("Loading channel videos for channel {ChannelId}", ChannelId);

            // Load data from service
            Model = await ChannelVideosService.GetChannelVideosAsync(ChannelId, CurrentUserId);

            // Handle service-level errors
            if (!string.IsNullOrEmpty(Model.ErrorMessage))
            {
                Logger.LogWarning("Error loading channel videos: {Error}", Model.ErrorMessage);
            }
            else
            {
                Logger.LogInformation("Successfully loaded {Count} videos for channel {ChannelName}",
                    Model.Videos.Count, Model.Channel.Name);
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error loading channel videos for {ChannelId}", ChannelId);

            Model.ErrorMessage = "An unexpected error occurred while loading channel videos.";
            Model.IsLoading = false;

            await MessageCenter.ShowErrorAsync("Failed to load channel videos. Please try again.");
            StateHasChanged();
        }
    }

    #endregion

    #region User Actions

    /// <summary>
    /// Retries loading channel videos after an error. Resets the error state
    /// and triggers a new load operation.
    /// </summary>
    protected async Task RetryLoad()
    {
        Model.ErrorMessage = null;
        Model.IsLoading = true;
        StateHasChanged();

        await LoadChannelVideos();
    }

    #endregion

    #region UI Helper Methods

    /// <summary>
    /// Gets the appropriate page title for the browser tab based on the current state.
    /// </summary>
    protected string GetPageTitle()
    {
        if (Model.IsLoading)
            return "Loading Channel Videos - YouTube Video Tracker";

        if (!string.IsNullOrEmpty(Model.ErrorMessage))
            return "Error - YouTube Video Tracker";

        if (!string.IsNullOrEmpty(Model.Channel.Name))
            return $"{Model.Channel.Name} - Recent Videos - YouTube Video Tracker";

        return "Channel Videos - YouTube Video Tracker";
    }

    /// <summary>
    /// Gets the channel thumbnail URL with a fallback placeholder if no thumbnail is available.
    /// </summary>
    protected string GetChannelThumbnailUrl()
    {
        if (!string.IsNullOrEmpty(Model.Channel.ThumbnailUrl))
            return Model.Channel.ThumbnailUrl;

        // Fallback placeholder
        return "https://via.placeholder.com/80x80?text=Channel";
    }

    #endregion
}