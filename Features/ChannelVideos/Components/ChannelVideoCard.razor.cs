using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.ChannelVideos.Models;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.ChannelVideos.Components;

public partial class ChannelVideoCard : ComponentBase
{
    /// <summary>
    /// The video data to display in this card.
    /// </summary>
    [Parameter, EditorRequired]
    public ChannelVideoModel Video { get; set; } = default!;

    /// <summary>
    /// Event callback fired when a video is successfully added to the library.
    /// </summary>
    [Parameter]
    public EventCallback<ChannelVideoModel> OnVideoAdded { get; set; }

    // Injected Services
    [Inject] protected IVideoDataService VideoDataService { get; set; } = default!;
    [Inject] protected ILibraryDataService LibraryDataService { get; set; } = default!;
    [Inject] protected IMessageCenterService MessageCenter { get; set; } = default!;
    [Inject] protected ILogger<ChannelVideoCard> Logger { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    // State management
    private bool IsAddingToLibrary = false;
    private bool IsInLibrary = false;
    private string? CurrentUserId;

    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserIdAsync();
        await CheckLibraryStatus();
    }

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

    /// <summary>
    /// Checks if the video is already in the user's library.
    /// </summary>
    private async Task CheckLibraryStatus()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return;

        try
        {
            IsInLibrary = await LibraryDataService.IsVideoInLibraryAsync(CurrentUserId, Video.YouTubeVideoId);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking library status for video {VideoId}", Video.YouTubeVideoId);
        }
    }

    /// <summary>
    /// Adds the video to the user's library.
    /// </summary>
    private async Task AddToLibrary()
    {
        if (IsAddingToLibrary || string.IsNullOrEmpty(CurrentUserId))
        {
            if (string.IsNullOrEmpty(CurrentUserId))
                await MessageCenter.ShowErrorAsync("Please log in to add videos to your library.");
            return;
        }

        if (IsInLibrary)
            return;

        IsAddingToLibrary = true;
        StateHasChanged();

        try
        {
            // Convert ChannelVideoModel to VideoInfo (shared DTO)
            var videoInfo = new VideoInfo
            {
                YouTubeVideoId = Video.YouTubeVideoId,
                ChannelId = Video.ChannelId,
                ChannelName = Video.ChannelName,
                Title = Video.Title,
                Description = Video.Description,
                ThumbnailUrl = Video.ThumbnailUrl ?? string.Empty,
                Duration = Video.Duration,
                ViewCount = Video.ViewCount,
                LikeCount = Video.LikeCount,
                CommentCount = Video.CommentCount,
                PublishedAt = Video.PublishedAt
            };

            var success = await LibraryDataService.AddVideoToLibraryAsync(
                CurrentUserId,
                videoInfo,
                $"Added from channel videos on {DateTime.Now:yyyy-MM-dd}");

            if (success)
            {
                IsInLibrary = true;
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
}
