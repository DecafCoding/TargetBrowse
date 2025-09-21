using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.TopicVideos.Models;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.TopicVideos.Components;

public partial class TopicVideoCard : ComponentBase
{
    //[Inject] protected IVideoService VideoService { get; set; } = default!;
    [Inject] protected IVideoDataService VideoDataService { get; set; } = default!;
    [Inject] protected ILibraryDataService LibraryDataService { get; set; } = default!;
    [Inject] protected IMessageCenterService MessageCenter { get; set; } = default!;
    [Inject] protected ILogger<TopicVideoCard> Logger { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter, EditorRequired] public TopicVideoDisplayModel Video { get; set; } = null!;
    [Parameter] public EventCallback<TopicVideoDisplayModel> OnVideoAdded { get; set; }

    // State management
    private bool IsAddingToLibrary = false;
    private string? CurrentUserId;

    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserIdAsync();
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
    /// Gets the appropriate thumbnail URL for the video - start with most reliable option.
    /// </summary>
    private string GetThumbnailUrl()
    {
        if (!string.IsNullOrEmpty(Video.ThumbnailUrl))
        {
            return Video.ThumbnailUrl;
        }

        // Start with hqdefault which is more reliable than maxresdefault
        return $"https://img.youtube.com/vi/{Video.YouTubeVideoId}/hqdefault.jpg";
    }

    /// <summary>
    /// Gets the CSS class for the relevance indicator based on the relevance score.
    /// </summary>
    private string GetRelevanceIndicatorClass()
    {
        return Video.RelevanceScore switch
        {
            >= 8.0 => "relevance-indicator high-relevance",
            >= 6.0 => "relevance-indicator medium-relevance",
            _ => "relevance-indicator low-relevance"
        };
    }

    /// <summary>
    /// Gets the title for the relevance indicator tooltip.
    /// </summary>
    private string GetRelevanceTitle()
    {
        var level = Video.RelevanceScore switch
        {
            >= 8.0 => "Highly relevant",
            >= 6.0 => "Moderately relevant",
            _ => "Somewhat relevant"
        };
        return $"{level} to {Video.TopicName} (Score: {Video.RelevanceScore:F1})";
    }

    /// <summary>
    /// Adds the video to the user's library with topic context.
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
            // Create add model with topic context
            var addModel = AddVideoModel.FromExistingVideo(
                Video,
                $"Added from topic search '{Video.TopicName}' on {DateTime.Now:yyyy-MM-dd}. Match reason: {Video.MatchReason}");

            var success = await LibraryDataService.AddTopicVideoToLibraryAsync(
                CurrentUserId,
                Video, // TopicVideoDisplayModel directly - no conversion needed!
                $"Added from topic search '{Video.TopicName}' on {DateTime.Now:yyyy-MM-dd}. Match reason: {Video.MatchReason}");


            if (success)
            {
                Video.IsInLibrary = true;
                Video.AddedToLibrary = DateTime.UtcNow;

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
            Logger.LogError(ex, "Error adding video {VideoId} to library for user {UserId} from topic {TopicName}",
                Video.YouTubeVideoId, CurrentUserId, Video.TopicName);
        }
        finally
        {
            IsAddingToLibrary = false;
            StateHasChanged();
        }
    }
}