using Microsoft.AspNetCore.Components;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Videos.Pages;

public partial class VideoSearch : ComponentBase
{
    [Inject] private IVideoService VideoService { get; set; } = default!;
    [Inject] private IMessageCenterService MessageCenter { get; set; } = default!;

    private VideoSearchModel SearchModel = new();
    private List<VideoDisplayModel> SearchResults = new();
    private bool IsSearching = false;
    private bool HasSearched = false;
    private string? CurrentUserId;

    protected override async Task OnInitializedAsync()
    {
        // Get current user ID - this would come from authentication
        CurrentUserId = "temp-user-id"; // TODO: Get from AuthenticationStateProvider
        await Task.CompletedTask;
    }

    /// <summary>
    /// Handles the search form submission.
    /// </summary>
    private async Task HandleSearchSubmit()
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            await MessageCenter.ShowErrorAsync("Please log in to search for videos.");
            return;
        }

        IsSearching = true;
        HasSearched = true;

        try
        {
            // If it's a direct video URL, handle it differently
            if (SearchModel.IsDirectVideoUrl)
            {
                var videoId = SearchModel.ExtractVideoId();
                if (!string.IsNullOrEmpty(videoId))
                {
                    var video = await VideoService.GetVideoByIdAsync(CurrentUserId, videoId);
                    if (video != null)
                    {
                        SearchResults = new List<VideoDisplayModel> { video };
                    }
                    else
                    {
                        await MessageCenter.ShowErrorAsync("Video not found or unavailable.");
                        SearchResults.Clear();
                    }
                }
                else
                {
                    await MessageCenter.ShowErrorAsync("Invalid YouTube video URL.");
                    SearchResults.Clear();
                }
            }
            else
            {
                // Regular search
                SearchResults = await VideoService.SearchVideosAsync(CurrentUserId, SearchModel);
            }

            if (!SearchResults.Any() && !SearchModel.IsDirectVideoUrl)
            {
                await MessageCenter.ShowErrorAsync("No videos found. Try different search terms or check your internet connection.");
            }
        }
        catch (Exception ex)
        {
            await MessageCenter.ShowErrorAsync($"Search failed: {ex.Message}");
            SearchResults.Clear();
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Clears the current search results.
    /// </summary>
    private void ClearResults()
    {
        SearchResults.Clear();
        HasSearched = false;
    }

    /// <summary>
    /// Handles when a video is added to the library from search results.
    /// </summary>
    private async Task HandleVideoAddedToLibrary(VideoDisplayModel video)
    {
        // Update the video's library status in current results
        var existingVideo = SearchResults.FirstOrDefault(v => v.YouTubeVideoId == video.YouTubeVideoId);
        if (existingVideo != null)
        {
            existingVideo.IsInLibrary = true;
            existingVideo.AddedToLibrary = DateTime.UtcNow;
        }

        await MessageCenter.ShowSuccessAsync($"Added '{video.ShortTitle}' to your library!");
        StateHasChanged(); // Force UI update to reflect the change
    }

    /// <summary>
    /// Resets the search form and results.
    /// </summary>
    public void Reset()
    {
        SearchModel = new VideoSearchModel();
        SearchResults.Clear();
        HasSearched = false;
        StateHasChanged();
    }
}