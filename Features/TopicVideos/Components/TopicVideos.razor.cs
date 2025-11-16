using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Features.Topics.Models;
using TargetBrowse.Features.TopicVideos.Models;
using TargetBrowse.Features.TopicVideos.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.TopicVideos.Components;

public partial class TopicVideos : ComponentBase
{
    [Inject]
    protected ITopicVideosService TopicVideosService { get; set; } = default!;

    [Inject]
    protected IMessageCenterService MessageCenter { get; set; } = default!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter]
    public Guid TopicId { get; set; }

    private TopicVideosList? TopicVideosListComponent;
    private TopicDisplayModel? CurrentTopic { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsRefreshing { get; set; } = false;
    private bool HasError { get; set; } = false;
    private string? CurrentUserId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        CurrentUserId = await AuthenticationHelper.GetCurrentUserIdAsync(AuthenticationStateTask);
        await LoadTopicAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // Handle navigation to different topics
        if (CurrentTopic?.Id != TopicId)
        {
            IsLoading = true;
            HasError = false;
            await LoadTopicAsync();
        }
    }

    /// <summary>
    /// Loads the current topic information and validates user access.
    /// </summary>
    private async Task LoadTopicAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            StateHasChanged();

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                HasError = true;
                await MessageCenter.ShowErrorAsync("Please log in to view topic videos.");
                return;
            }

            if (TopicId == Guid.Empty)
            {
                HasError = true;
                await MessageCenter.ShowErrorAsync("Invalid topic identifier.");
                return;
            }

            // Validate user access to the topic
            var hasAccess = await TopicVideosService.ValidateTopicAccess(CurrentUserId, TopicId);
            if (!hasAccess)
            {
                HasError = true;
                await MessageCenter.ShowErrorAsync("You don't have access to this topic or it doesn't exist.");
                return;
            }

            // Get topic information
            CurrentTopic = await TopicVideosService.GetTopicAsync(CurrentUserId, TopicId);
            if (CurrentTopic == null)
            {
                HasError = true;
                await MessageCenter.ShowErrorAsync("Topic information could not be loaded.");
                return;
            }

            HasError = false;
        }
        catch (Exception ex)
        {
            HasError = true;
            await MessageCenter.ShowErrorAsync("Failed to load topic information. Please try again.");
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Refreshes the video search results.
    /// </summary>
    private async Task RefreshVideos()
    {
        if (TopicVideosListComponent == null || IsRefreshing)
            return;

        try
        {
            IsRefreshing = true;
            StateHasChanged();

            await TopicVideosListComponent.RefreshAsync();
        }
        finally
        {
            IsRefreshing = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Retries loading the topic information.
    /// </summary>
    private async Task RetryLoadTopic()
    {
        await LoadTopicAsync();
    }

    /// <summary>
    /// Handles when a video is successfully added to the user's library.
    /// </summary>
    private async Task HandleVideoAdded(TopicVideoDisplayModel video)
    {
        // Could add analytics tracking here
        // Could update any parent component state if needed
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the page title for the browser tab.
    /// </summary>
    private string GetPageTitle()
    {
        if (CurrentTopic != null)
        {
            return $"Recent Videos: {CurrentTopic.Name}";
        }
        else if (HasError)
        {
            return "Topic Not Found";
        }
        else
        {
            return "Loading Topic Videos";
        }
    }
}