using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.Watch.Models;
using TargetBrowse.Features.Watch.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Watch.Pages;

/// <summary>
/// Code-behind for the Watch page component. Handles loading video data
/// and managing the watch experience.
/// </summary>
public partial class Watch : ComponentBase
{
    #region Parameters and Dependencies

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter]
    public string YouTubeVideoId { get; set; } = string.Empty;

    [Inject]
    protected IWatchService WatchService { get; set; } = default!;

    [Inject]
    protected IMessageCenterService MessageCenter { get; set; } = default!;

    [Inject]
    protected ILogger<Watch> Logger { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    [Inject]
    protected ITranscriptRetrievalService TranscriptRetrievalService { get; set; } = default!;

    #endregion

    #region State

    /// <summary>
    /// The main data model containing video and user context information.
    /// </summary>
    protected WatchViewModel Model { get; set; } = new();

    /// <summary>
    /// The current authenticated user's ID.
    /// </summary>
    protected string? CurrentUserId { get; set; }

    /// <summary>
    /// Tracks whether transcript retrieval is currently in progress.
    /// </summary>
    protected bool IsRetrievingTranscript { get; set; } = false;

    /// <summary>
    /// Tracks whether user is viewing summary (true) or transcript (false)
    /// Only relevant when both are available
    /// </summary>
    protected bool IsViewingSummary { get; set; } = true;

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the component by getting the current user and loading video data.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserIdAsync();
        await LoadVideoData();
    }

    /// <summary>
    /// Handles parameter changes, specifically when the YouTubeVideoId parameter changes.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        // Reload if YouTube video ID parameter changes
        if (Model.YouTubeVideoId != YouTubeVideoId)
        {
            await LoadVideoData();
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
    /// Loads video data from the service. Handles validation,
    /// error states, and logging throughout the process.
    /// </summary>
    private async Task LoadVideoData()
    {
        try
        {
            // Validate YouTube video ID
            if (string.IsNullOrWhiteSpace(YouTubeVideoId))
            {
                Model.ErrorMessage = "Invalid video ID provided.";
                await MessageCenter.ShowErrorAsync("Invalid video ID in URL.");
                return;
            }

            // Validate user authentication
            if (string.IsNullOrWhiteSpace(CurrentUserId))
            {
                Model.ErrorMessage = "Please log in to watch videos.";
                await MessageCenter.ShowErrorAsync("Please log in to access this feature.");
                return;
            }

            Logger.LogInformation("Loading watch data for video {YouTubeVideoId}", YouTubeVideoId);

            // Load data from service
            Model = await WatchService.GetWatchDataAsync(YouTubeVideoId, CurrentUserId);

            // Handle service-level errors
            if (!string.IsNullOrEmpty(Model.ErrorMessage))
            {
                Logger.LogWarning("Error loading video data: {Error}", Model.ErrorMessage);
            }
            else if (Model.VideoExists)
            {
                Logger.LogInformation("Successfully loaded watch data for video: {Title}",
                    Model.Title);
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error loading watch data for {YouTubeVideoId}", YouTubeVideoId);

            Model.ErrorMessage = "An unexpected error occurred while loading the video.";
            Model.IsLoading = false;

            await MessageCenter.ShowErrorAsync("Failed to load video. Please try again.");
            StateHasChanged();
        }
    }

    #endregion

    #region User Actions

    /// <summary>
    /// Retries loading video data after an error. Resets the error state
    /// and triggers a new load operation.
    /// </summary>
    protected async Task RetryLoad()
    {
        Model.ErrorMessage = null;
        Model.IsLoading = true;
        StateHasChanged();

        await LoadVideoData();
    }

    /// <summary>
    /// Handles the user request to retrieve a video transcript.
    /// Triggers the Apify service and updates the UI accordingly.
    /// </summary>
    protected async Task HandleRetrieveTranscript()
    {
        if (IsRetrievingTranscript)
        {
            Logger.LogInformation("Transcript retrieval already in progress");
            return;
        }

        try
        {
            IsRetrievingTranscript = true;
            StateHasChanged();

            Logger.LogInformation("User requested transcript retrieval for {YouTubeVideoId}", YouTubeVideoId);

            // Call the transcript retrieval service (this takes 8-20 seconds)
            var success = await TranscriptRetrievalService.RetrieveAndStoreTranscriptAsync(YouTubeVideoId);

            if (success)
            {
                // Reload the watch data to reflect the updated transcript status
                await LoadVideoData();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during transcript retrieval for {YouTubeVideoId}", YouTubeVideoId);
            await MessageCenter.ShowErrorAsync("An unexpected error occurred. Please try again.");
        }
        finally
        {
            IsRetrievingTranscript = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Toggles between summary and transcript view
    /// </summary>
    protected void ToggleContentView()
    {
        IsViewingSummary = !IsViewingSummary;
        StateHasChanged();
    }

    #endregion

    #region UI Helper Methods

    /// <summary>
    /// Gets the appropriate page title for the browser tab based on the current state.
    /// </summary>
    protected string GetPageTitle()
    {
        if (Model.IsLoading)
            return "Loading Video - YouTube Video Tracker";

        if (!string.IsNullOrEmpty(Model.ErrorMessage))
            return "Error - YouTube Video Tracker";

        if (!string.IsNullOrEmpty(Model.Title))
            return $"{Model.Title} - Watch - YouTube Video Tracker";

        return "Watch Video - YouTube Video Tracker";
    }

    /// <summary>
    /// Gets the appropriate title for the content display card header
    /// </summary>
    protected string GetContentHeaderTitle()
    {
        // If summary exists and is being viewed
        if (!string.IsNullOrWhiteSpace(Model.SummaryContent) && IsViewingSummary)
            return "Summary";

        // If transcript exists and is being viewed (or is the only content)
        if (!string.IsNullOrWhiteSpace(Model.RawTranscript))
            return "Transcript";

        // If only description is available
        if (!string.IsNullOrWhiteSpace(Model.Description))
            return "Video Information";

        // No content available
        return "Content";
    }

    #endregion

    #region Status Display Helper Methods (Moved from WatchActionButtons)

    /// <summary>
    /// Gets the display text for the current watch status.
    /// </summary>
    protected string GetWatchStatusDisplay()
    {
        return Model.WatchStatus switch
        {
            WatchStatus.Watched => "Watched",
            WatchStatus.Skipped => "Skipped",
            _ => "Not Watched"
        };
    }

    /// <summary>
    /// Gets the CSS class for the watch status badge.
    /// </summary>
    protected string GetWatchStatusBadgeClass()
    {
        return Model.WatchStatus switch
        {
            WatchStatus.Watched => "bg-success",
            WatchStatus.Skipped => "bg-warning",
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// Gets the icon class for the watch status badge.
    /// </summary>
    protected string GetWatchStatusIcon()
    {
        return Model.WatchStatus switch
        {
            WatchStatus.Watched => "bi-check-circle-fill",
            WatchStatus.Skipped => "bi-skip-forward-fill",
            _ => "bi-circle"
        };
    }

    #endregion
}