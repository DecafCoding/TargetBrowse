using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Features.Watch.Models;
using TargetBrowse.Features.Watch.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Utilities;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Watch.Pages;

/// <summary>
/// Represents the current content view mode in the watch page
/// </summary>
public enum ContentViewMode
{
    Description,
    Transcript,
    Summary
}

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

    [Inject]
    protected ITranscriptSummaryService TranscriptSummaryService { get; set; } = default!;

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
    /// Tracks whether summary generation is currently in progress.
    /// </summary>
    protected bool IsGeneratingSummary { get; set; } = false;

    /// <summary>
    /// Tracks which content type is currently being displayed
    /// </summary>
    protected ContentViewMode CurrentViewMode { get; set; } = ContentViewMode.Summary;

    /// <summary>
    /// Whether the notes field is in edit mode.
    /// </summary>
    protected bool IsEditingNotes { get; set; } = false;

    /// <summary>
    /// Holds the in-progress notes value while editing.
    /// </summary>
    protected string? NotesEditValue { get; set; }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Initializes the component by getting the current user and loading video data.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        CurrentUserId = await AuthenticationHelper.GetCurrentUserIdAsync(AuthenticationStateTask, Logger);
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
    /// Handles the user request to generate a video summary.
    /// Calls the TranscriptSummaryService and updates the UI accordingly.
    /// </summary>
    protected async Task HandleGetSummary()
    {
        if (IsGeneratingSummary)
        {
            Logger.LogInformation("Summary generation already in progress");
            return;
        }

        // Verify we have a video ID
        if (Model.VideoId == Guid.Empty)
        {
            Logger.LogWarning("Cannot generate summary: Video ID is not set");
            await MessageCenter.ShowErrorAsync("Unable to generate summary. Please try reloading the page.");
            return;
        }

        try
        {
            IsGeneratingSummary = true;
            StateHasChanged();

            Logger.LogInformation("User requested summary generation for video {VideoId}", Model.VideoId);

            // Call the transcript summary service
            var result = await TranscriptSummaryService.SummarizeVideoTranscriptAsync(Model.VideoId, CurrentUserId);

            if (result.Success)
            {
                // Update the model with the new summary
                Model.SummaryContent = result.SummaryContent;

                // Switch to summary view
                CurrentViewMode = ContentViewMode.Summary;

                // Show success message with cost information
                var costMessage = $"Summary generated successfully! (Cost: ${result.TotalCost:F4})";
                await MessageCenter.ShowSuccessAsync(costMessage);

                Logger.LogInformation("Summary generated successfully for video {VideoId}. Cost: ${Cost:F4}",
                    Model.VideoId, result.TotalCost);
            }
            else if (result.Skipped)
            {
                // Summary was skipped (e.g., already exists, no transcript)
                if (result.SummaryContent != null)
                {
                    // Update with existing summary
                    Model.SummaryContent = result.SummaryContent;
                    CurrentViewMode = ContentViewMode.Summary;
                }

                await MessageCenter.ShowWarningAsync($"Summary not generated: {result.SkipReason}");
                Logger.LogInformation("Summary generation skipped for video {VideoId}: {Reason}",
                    Model.VideoId, result.SkipReason);
            }
            else
            {
                // Generation failed
                await MessageCenter.ShowErrorAsync($"Failed to generate summary: {result.ErrorMessage}");
                Logger.LogError("Summary generation failed for video {VideoId}: {Error}",
                    Model.VideoId, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during summary generation for video {VideoId}", Model.VideoId);
            await MessageCenter.ShowErrorAsync("An unexpected error occurred. Please try again.");
        }
        finally
        {
            IsGeneratingSummary = false;
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

    /// <summary>
    /// Handles video type changes from the VideoDetailsCard dropdown
    /// </summary>
    protected async Task HandleVideoTypeChanged(Guid? newVideoTypeId)
    {
        try
        {
            if (Model.VideoId == Guid.Empty)
            {
                Logger.LogWarning("Cannot update video type: Video ID is not set");
                await MessageCenter.ShowErrorAsync("Unable to update video type. Please try reloading the page.");
                return;
            }

            Logger.LogInformation("Updating video type for video {VideoId} to {VideoTypeId}", Model.VideoId, newVideoTypeId);

            var success = await WatchService.UpdateVideoTypeAsync(Model.VideoId, newVideoTypeId);

            if (success)
            {
                // Update the model to reflect the change
                Model.VideoTypeId = newVideoTypeId;

                // Find and set the video type name
                if (newVideoTypeId.HasValue)
                {
                    var selectedType = Model.AvailableVideoTypes.FirstOrDefault(vt => vt.Id == newVideoTypeId.Value);
                    Model.VideoTypeName = selectedType?.Name;
                }
                else
                {
                    Model.VideoTypeName = null;
                }

                await MessageCenter.ShowSuccessAsync("Video type updated successfully!");
                Logger.LogInformation("Successfully updated video type for video {VideoId}", Model.VideoId);
                StateHasChanged();
            }
            else
            {
                await MessageCenter.ShowErrorAsync("Failed to update video type. Please try again.");
                Logger.LogWarning("Failed to update video type for video {VideoId}", Model.VideoId);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error updating video type for video {VideoId}", Model.VideoId);
            await MessageCenter.ShowErrorAsync("An unexpected error occurred. Please try again.");
        }
    }

    /// <summary>
    /// Enters edit mode for the video notes, pre-populating the editor with existing notes.
    /// </summary>
    protected void HandleEditNotes()
    {
        NotesEditValue = Model.VideoNotes;
        IsEditingNotes = true;
        StateHasChanged();
    }

    /// <summary>
    /// Saves the edited notes to the database and exits edit mode.
    /// </summary>
    protected async Task HandleSaveNotes()
    {
        try
        {
            // Treat empty/whitespace as null to clear notes
            var notesToSave = string.IsNullOrWhiteSpace(NotesEditValue) ? null : NotesEditValue.Trim();

            var success = await WatchService.UpdateVideoNotesAsync(CurrentUserId!, Model.VideoId, notesToSave);

            if (success)
            {
                Model.VideoNotes = notesToSave;
                IsEditingNotes = false;
                await MessageCenter.ShowSuccessAsync("Notes saved.");
            }
            else
            {
                await MessageCenter.ShowErrorAsync("Failed to save notes. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error saving notes for video {VideoId}", Model.VideoId);
            await MessageCenter.ShowErrorAsync("An unexpected error occurred. Please try again.");
        }

        StateHasChanged();
    }

    /// <summary>
    /// Cancels the notes edit and returns to read-only view.
    /// </summary>
    protected void HandleCancelNotes()
    {
        IsEditingNotes = false;
        NotesEditValue = null;
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
        // Summary button is disabled during generation
        if (mode == ContentViewMode.Summary && IsGeneratingSummary)
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
            return;
        }

        // If no content exists and it's Summary, generate it
        if (mode == ContentViewMode.Summary && !IsGeneratingSummary)
        {
            await HandleGetSummary();
            return;
        }

        // For Description with no content, button click does nothing
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