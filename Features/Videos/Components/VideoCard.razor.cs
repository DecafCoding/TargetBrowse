using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;
using TargetBrowse.Services.Utilities;
using TargetBrowse.Services.YouTube;
using TargetBrowse.Services.ProjectServices;
using TargetBrowse.Services.ProjectServices.Models;

namespace TargetBrowse.Features.Videos.Components;

public partial class VideoCard : ComponentBase
{
    [Inject] protected IVideoDataService VideoDataService { get; set; } = default!;
    [Inject] protected ILibraryDataService LibraryDataService { get; set; } = default!;
    [Inject] protected IVideoRatingService VideoRatingService { get; set; } = default!;
    [Inject] protected IMessageCenterService MessageCenter { get; set; } = default!;
    [Inject] protected ILogger<VideoCard> Logger { get; set; } = default!;
    [Inject] protected IAddToProjectService AddToProjectService { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter, EditorRequired] public VideoDisplayModel Video { get; set; } = null!;
    [Parameter, EditorRequired] public VideoDisplayMode DisplayMode { get; set; }
    [Parameter] public EventCallback<VideoDisplayModel> OnVideoAdded { get; set; }
    [Parameter] public EventCallback<VideoDisplayModel> OnVideoRemoved { get; set; }
    [Parameter] public EventCallback<(VideoDisplayModel Video, WatchStatus Status)> OnWatchStatusChanged { get; set; }
    [Parameter] public EventCallback<(VideoDisplayModel Video, VideoRatingModel? Rating)> OnVideoRated { get; set; }

    // State management
    private bool IsAddingToLibrary = false;
    private bool IsRemoving = false;
    private bool IsUpdatingStatus = false;
    private bool ShowConfirmDialog = false;
    private bool ShowRatingModalDialog = false;
    private bool ShowAddToProjectModalDialog = false;
    private WatchStatus? PendingStatus = null;
    private string? CurrentUserId;
    private RateVideoModel? CurrentRatingModel = null;

    // Add logger field for error handling
    private ILogger<VideoCard>? _logger => Logger;

    protected override async Task OnInitializedAsync()
    {
        CurrentUserId = await AuthenticationHelper.GetCurrentUserIdAsync(AuthenticationStateTask, Logger);
    }

    /// <summary>
    /// Gets the appropriate thumbnail URL for the video - start with most reliable option.
    /// </summary>
    private string GetThumbnailUrl()
    {
        return ThumbnailFormatter.GetVideoThumbnailUrl(Video.ThumbnailUrl, Video.YouTubeVideoId, ThumbnailQuality.HqDefault);
    }

    /// <summary>
    /// Gets the CSS class for watch status badge.
    /// </summary>
    private string GetWatchStatusBadgeClass()
    {
        return CssClassFormatter.GetWatchStatusBadgeClass(Video.WatchStatus);
    }

    /// <summary>
    /// Gets the icon for watch status.
    /// </summary>
    private string GetWatchStatusIcon()
    {
        return CssClassFormatter.GetWatchStatusIcon(Video.WatchStatus);
    }

    /// <summary>
    /// Gets the text for watch status.
    /// </summary>
    private string GetWatchStatusText()
    {
        return CssClassFormatter.GetWatchStatusText(Video.WatchStatus);
    }

    #region Search Mode Actions

    /// <summary>
    /// Adds the video to the user's library with full metadata.
    /// OPTIMIZED: Uses existing video data to avoid redundant API calls.
    /// </summary>
    private async Task AddToLibrary()
    {
        if (IsAddingToLibrary || string.IsNullOrEmpty(CurrentUserId))
        {
            if (string.IsNullOrEmpty(CurrentUserId))
                await MessageCenter.ShowErrorAsync("Please log in to add videos to your library.");
            return;
        }

        if (Video.IsInLibrary)
            return;

        IsAddingToLibrary = true;
        StateHasChanged();

        try
        {
            // Convert VideoDisplayModel to VideoInfo (shared DTO)
            var videoInfo = new VideoInfo
            {
                YouTubeVideoId = Video.YouTubeVideoId,
                ChannelId = Video.ChannelId,
                ChannelName = Video.ChannelTitle,
                Title = Video.Title,
                Description = Video.Description,
                ThumbnailUrl = Video.ThumbnailUrl ?? string.Empty,
                Duration = DurationParser.ParseToSeconds(Video.Duration),
                ViewCount = (int)(Video.ViewCount ?? 0),
                LikeCount = (int)(Video.LikeCount ?? 0),
                CommentCount = (int)(Video.CommentCount ?? 0),
                PublishedAt = Video.PublishedAt
            };

            var success = await LibraryDataService.AddVideoToLibraryAsync(
                CurrentUserId,
                videoInfo,
                $"Added from {DisplayMode.ToString().ToLower()} on {DateTime.Now:yyyy-MM-dd}");


            if (success)
            {
                // Fetch the complete video info from library to get the Video.Id
                var libraryVideo = await LibraryDataService.GetVideoByYouTubeIdAsync(CurrentUserId, Video.YouTubeVideoId);

                if (libraryVideo != null)
                {
                    Video.Id = libraryVideo.VideoId;
                    Video.UserVideoId = libraryVideo.UserVideoId;
                    Video.IsInLibrary = true;
                    Video.AddedToLibrary = libraryVideo.AddedToLibraryAt;
                }
                else
                {
                    // Fallback if we can't fetch the complete info
                    Video.IsInLibrary = true;
                    Video.AddedToLibrary = DateTime.UtcNow;
                }

                await MessageCenter.ShowSuccessAsync($"Added '{Video.ShortTitle}' to your library!");
                await OnVideoAdded.InvokeAsync(Video);
            }
            else
            {
                await MessageCenter.ShowErrorAsync("Failed to add video to library. It may already exist or there was a network error.");
            }
        }
        catch (Exception ex)
        {
            await MessageCenter.ShowErrorAsync($"Error adding video to library: {ex.Message}");
            Logger.LogError(ex, "Error adding video {VideoId} to library for user {UserId}",
                Video.YouTubeVideoId, CurrentUserId);
        }
        finally
        {
            IsAddingToLibrary = false;
            StateHasChanged();
        }
    }

    #endregion

    #region Library Mode Actions

    /// <summary>
    /// Shows the confirmation dialog for removing the video.
    /// </summary>
    private void ConfirmRemove()
    {
        ShowConfirmDialog = true;
        StateHasChanged();
    }

    /// <summary>
    /// Cancels the remove operation and hides the dialog.
    /// </summary>
    private void CancelRemove()
    {
        ShowConfirmDialog = false;
        StateHasChanged();
    }

    /// <summary>
    /// Removes the video from the library after confirmation.
    /// </summary>
    private async Task RemoveVideo()
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            await MessageCenter.ShowErrorAsync("Please log in to manage your library.");
            return;
        }

        IsRemoving = true;

        try
        {
            var success = await LibraryDataService.RemoveVideoFromLibraryAsync(CurrentUserId, Video.Id);

            if (success)
            {
                ShowConfirmDialog = false;
                await OnVideoRemoved.InvokeAsync(Video);
            }
            else
            {
                await MessageCenter.ShowErrorAsync("Failed to remove video from library. Please try again.");
            }
        }
        catch (Exception ex)
        {
            await MessageCenter.ShowErrorAsync($"Error removing video: {ex.Message}");
        }
        finally
        {
            IsRemoving = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Marks the video as watched.
    /// </summary>
    private async Task MarkAsWatched()
    {
        await UpdateWatchStatus(WatchStatus.Watched);
    }

    /// <summary>
    /// Marks the video as skipped.
    /// </summary>
    private async Task MarkAsSkipped()
    {
        await UpdateWatchStatus(WatchStatus.Skipped);
    }

    /// <summary>
    /// Updates the watch status for the video.
    /// </summary>
    private async Task UpdateWatchStatus(WatchStatus newStatus)
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId) || IsUpdatingStatus)
            return;

        // IMPORTANT: Check if we have the UserVideoId for library operations
        if (!Video.UserVideoId.HasValue)
        {
            await MessageCenter.ShowErrorAsync("Unable to update watch status. Video may not be properly loaded.");
            return;
        }

        // If clicking the same status, toggle back to NotWatched
        if (Video.WatchStatus == newStatus)
        {
            newStatus = WatchStatus.NotWatched;
        }

        IsUpdatingStatus = true;
        PendingStatus = newStatus;

        try
        {
            // Use UserVideoId instead of Video.Id
            var success = await LibraryDataService.UpdateVideoWatchStatusAsync(
                CurrentUserId,
                Video.UserVideoId.Value, // CHANGED: Use UserVideoId instead of Video.Id
                newStatus);

            if (success)
            {
                Video.WatchStatus = newStatus;
                var statusText = GetWatchStatusText().ToLower();
                await MessageCenter.ShowSuccessAsync($"Marked video as {statusText}");
                await OnWatchStatusChanged.InvokeAsync((Video, newStatus));
            }
            else
            {
                await MessageCenter.ShowErrorAsync("Failed to update watch status. Please try again.");
            }

            StateHasChanged();
        }
        catch (Exception ex)
        {
            await MessageCenter.ShowErrorAsync($"Failed to update watch status: {ex.Message}");
        }
        finally
        {
            IsUpdatingStatus = false;
            PendingStatus = null;
        }
    }

    #endregion

    #region Rating Actions

    /// <summary>
    /// Shows the rating modal for creating or editing a rating.
    /// </summary>
    private void ShowRatingModal()
    {
        if (!Video.CanBeRated)
        {
            MessageCenter.ShowErrorAsync(Video.CannotRateReason);
            return;
        }

        // Create rating model from video
        CurrentRatingModel = Video.CreateRatingModel();
        ShowRatingModalDialog = true;
        StateHasChanged();
    }

    /// <summary>
    /// Hides the rating modal.
    /// </summary>
    private void HideRatingModal()
    {
        ShowRatingModalDialog = false;
        CurrentRatingModel = null;
        StateHasChanged();
    }

    /// <summary>
    /// Handles rating submission from the modal.
    /// </summary>
    private async Task HandleRatingSubmit(RateVideoModel ratingModel)
    {
        if (string.IsNullOrEmpty(CurrentUserId) || ratingModel == null)
        {
            await MessageCenter.ShowErrorAsync("Unable to save rating. Please try again.");
            return;
        }

        try
        {
            VideoRatingModel savedRating;

            if (ratingModel.IsEditing && ratingModel.RatingId.HasValue)
            {
                // Update existing rating
                savedRating = await VideoRatingService.UpdateRatingAsync(
                    CurrentUserId,
                    ratingModel.RatingId.Value,
                    ratingModel);
            }
            else
            {
                // Create new rating
                savedRating = await VideoRatingService.CreateRatingAsync(CurrentUserId, ratingModel);
            }

            // Update the video model with the new rating
            Video.UserRating = savedRating;

            // Hide modal
            HideRatingModal();

            // Notify parent component
            await OnVideoRated.InvokeAsync((Video, savedRating));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save rating for video {VideoId}", ratingModel.VideoId);
            // The service already shows error messages via MessageCenter
            // Don't show duplicate error message here
        }
    }

    /// <summary>
    /// Handles rating deletion from the modal.
    /// </summary>
    private async Task HandleRatingDelete(RateVideoModel ratingModel)
    {
        if (string.IsNullOrEmpty(CurrentUserId) || ratingModel?.RatingId == null)
        {
            await MessageCenter.ShowErrorAsync("Unable to delete rating. Please try again.");
            return;
        }

        try
        {
            var success = await VideoRatingService.DeleteRatingAsync(CurrentUserId, ratingModel.RatingId.Value);

            if (success)
            {
                // Remove the rating from the video model
                Video.UserRating = null;

                // Hide modal
                HideRatingModal();

                // Notify parent component
                await OnVideoRated.InvokeAsync((Video, null));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete rating {RatingId}", ratingModel.RatingId.Value);
            // The service already shows error messages via MessageCenter
            // Don't show duplicate error message here
        }
    }

    /// <summary>
    /// Handles editing an existing rating.
    /// </summary>
    private void HandleEditRating(VideoRatingModel rating)
    {
        ShowRatingModal();
    }

    #endregion

    #region Add to Project Actions

    /// <summary>
    /// Shows the Add to Project modal.
    /// </summary>
    private void ShowAddToProjectModal()
    {
        ShowAddToProjectModalDialog = true;
        StateHasChanged();
    }

    /// <summary>
    /// Hides the Add to Project modal.
    /// </summary>
    private void HideAddToProjectModal()
    {
        ShowAddToProjectModalDialog = false;
        StateHasChanged();
    }

    /// <summary>
    /// Handles successful addition of video to projects.
    /// </summary>
    private async Task HandleAddToProjectSuccess(AddToProjectResult result)
    {
        if (result.Success && result.AddedToProjectsCount > 0)
        {
            var projectText = result.AddedToProjectsCount == 1 ? "project" : "projects";
            await MessageCenter.ShowSuccessAsync(
                $"Video added to {result.AddedToProjectsCount} {projectText} successfully!");

            Logger.LogInformation("Video {VideoId} added to {Count} projects",
                Video.Id, result.AddedToProjectsCount);
        }
        else if (result.FailedProjectIds.Any())
        {
            // Show partial success or failure message
            var errorMessages = string.Join(", ", result.ProjectErrors.Values.Take(3));
            await MessageCenter.ShowWarningAsync(
                $"Some projects could not be updated. {errorMessages}");

            Logger.LogWarning("Failed to add video {VideoId} to some projects: {Errors}",
                Video.Id, errorMessages);
        }

        HideAddToProjectModal();
    }

    #endregion
}