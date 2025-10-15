﻿using Microsoft.AspNetCore.Components;
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

    /// <summary>
    /// Represents the current content view mode in the watch page
    /// </summary>
    public enum ContentViewMode
    {
        Description,
        Transcript,
        Summary
    }

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
    /// Tracks which content type is currently being displayed
    /// </summary>
    protected ContentViewMode CurrentViewMode { get; set; } = ContentViewMode.Summary;

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

                // Initialize the content view based on available content
                InitializeContentView();
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

    /// <summary>
    /// Initializes the content view mode based on available content.
    /// Priority: Summary > Transcript > Description
    /// </summary>
    private void InitializeContentView()
    {
        if (IsContentAvailable(ContentViewMode.Summary))
        {
            CurrentViewMode = ContentViewMode.Summary;
        }
        else if (IsContentAvailable(ContentViewMode.Transcript))
        {
            CurrentViewMode = ContentViewMode.Transcript;
        }
        else if (IsContentAvailable(ContentViewMode.Description))
        {
            CurrentViewMode = ContentViewMode.Description;
        }
        else
        {
            // Default to Summary if nothing is available
            CurrentViewMode = ContentViewMode.Summary;
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

                // Switch to transcript view after successful retrieval
                CurrentViewMode = ContentViewMode.Transcript;
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
    //protected void ToggleContentView()
    //{
    //    IsViewingSummary = !IsViewingSummary;
    //    StateHasChanged();
    //}

    /// <summary>
    /// Switches the content view to the specified mode
    /// </summary>
    protected void SwitchContentView(ContentViewMode mode)
    {
        if (IsContentAvailable(mode))
        {
            CurrentViewMode = mode;
            StateHasChanged();
        }
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
    /// Determines if a specific content type is available for viewing
    /// </summary>
    protected bool IsContentAvailable(ContentViewMode mode)
    {
        return mode switch
        {
            ContentViewMode.Description => !string.IsNullOrWhiteSpace(Model.Description),
            ContentViewMode.Transcript => !string.IsNullOrWhiteSpace(Model.RawTranscript),
            ContentViewMode.Summary => !string.IsNullOrWhiteSpace(Model.SummaryContent),
            _ => false
        };
    }

    /// <summary>
    /// Gets the appropriate title for the content display card header based on current view mode
    /// </summary>
    protected string GetContentHeaderTitle()
    {
        return CurrentViewMode switch
        {
            ContentViewMode.Description => "Description",
            ContentViewMode.Transcript => "Transcript",
            ContentViewMode.Summary => "Summary",
            _ => "Content"
        };
    }

    /// <summary>
    /// Gets the appropriate CSS class for a content button based on content availability and current view
    /// </summary>
    protected string GetButtonClass(ContentViewMode mode)
    {
        // Active view - primary button
        if (CurrentViewMode == mode)
            return "btn-primary";

        // Content exists - outline primary (inactive but available)
        if (IsContentAvailable(mode))
            return "btn-outline-primary";

        // Content missing - warning (yellow)
        return "btn-warning";
    }

    /// <summary>
    /// Gets the button label - adds "Get" prefix when content is missing
    /// </summary>
    protected string GetButtonLabel(ContentViewMode mode)
    {
        var baseName = mode switch
        {
            ContentViewMode.Description => "Description",
            ContentViewMode.Transcript => "Transcript",
            ContentViewMode.Summary => "Summary",
            _ => "Content"
        };

        // Add "Get" prefix when content is not available
        if (!IsContentAvailable(mode))
        {
            return $"Get {baseName}";
        }

        return baseName;
    }

    /// <summary>
    /// Determines if a button should be disabled
    /// </summary>
    protected bool IsButtonDisabled(ContentViewMode mode)
    {
        // Summary button is disabled when no summary exists (no service yet)
        if (mode == ContentViewMode.Summary && !IsContentAvailable(mode))
            return true;

        // Transcript button is disabled during retrieval
        if (mode == ContentViewMode.Transcript && IsRetrievingTranscript)
            return true;

        return false;
    }

    /// <summary>
    /// Handles button clicks - either switches view or retrieves content
    /// </summary>
    protected async Task HandleContentButtonClick(ContentViewMode mode)
    {
        // If content exists, switch to that view
        if (IsContentAvailable(mode))
        {
            SwitchContentView(mode);
            return;
        }

        // If no content exists and it's Transcript, retrieve it
        if (mode == ContentViewMode.Transcript && !IsRetrievingTranscript)
        {
            await HandleRetrieveTranscript();
        }

        // For Description and Summary with no content, button click does nothing
        // (Summary button should be disabled anyway)
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