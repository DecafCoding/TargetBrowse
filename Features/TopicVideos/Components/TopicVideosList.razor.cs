using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Features.ChannelVideos.Services;
using TargetBrowse.Features.TopicVideos.Models;
using TargetBrowse.Features.TopicVideos.Services;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.TopicVideos.Components;

public partial class TopicVideosList : ComponentBase
{
    [Inject]
    protected ITopicVideosService TopicVideosService { get; set; } = default!;

    [Inject]
    protected IMessageCenterService MessageCenter { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter, EditorRequired]
    public Guid TopicId { get; set; }

    [Parameter]
    public string? TopicName { get; set; }

    [Parameter]
    public int MaxResults { get; set; } = 50;

    [Parameter]
    public EventCallback<TopicVideoDisplayModel> OnVideoAdded { get; set; }

    public List<TopicVideoDisplayModel> Videos { get; set; } = new();
    public bool IsLoading { get; set; } = true;
    private bool HasError { get; set; } = false;
    private string? CurrentUserId { get; set; }

    public int HighRelevanceCount => Videos.Count(v => v.IsHighRelevance);

    protected override async Task OnInitializedAsync()
    {
        CurrentUserId = await AuthenticationHelper.GetCurrentUserIdAsync(AuthenticationStateTask);
        await LoadVideosAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Reload videos if TopicId changes
        if (Videos.Any() && Videos.First().TopicId != TopicId)
        {
            await LoadVideosAsync();
        }
    }

    /// <summary>
    /// Loads videos for the current topic from YouTube.
    /// </summary>
    public async Task LoadVideosAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            StateHasChanged();

            if (TopicId == Guid.Empty)
            {
                HasError = true;
                return;
            }

            Videos = await TopicVideosService.GetRecentVideosAsync(TopicId, CurrentUserId, MaxResults);

            // Update TopicName if we don't have it and we got results
            if (string.IsNullOrEmpty(TopicName) && Videos.Any())
            {
                TopicName = Videos.First().TopicName;
            }

            HasError = false;
        }
        catch (Exception ex)
        {
            HasError = true;
            await MessageCenter.ShowErrorAsync("Failed to load topic videos. Please try again.");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Handles when a video is successfully added to the user's library.
    /// </summary>
    private async Task HandleVideoAdded(VideoDisplayModel video)
    {
        // Update the video in our list to reflect it's now in the library
        var topicVideo = Videos.FirstOrDefault(v => v.YouTubeVideoId == video.YouTubeVideoId);
        if (topicVideo != null)
        {
            topicVideo.IsInLibrary = true;
            topicVideo.AddedToLibrary = video.AddedToLibrary;
            StateHasChanged();
        }

        // Notify parent component if needed
        if (OnVideoAdded.HasDelegate && topicVideo != null)
        {
            await OnVideoAdded.InvokeAsync(topicVideo);
        }
    }

    /// <summary>
    /// Public method to refresh videos (called from parent component).
    /// </summary>
    public async Task RefreshAsync()
    {
        await LoadVideosAsync();
    }
}