using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using TargetBrowse.Features.Projects.Models;
using TargetBrowse.Features.Projects.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.ProjectServices;
using TargetBrowse.Services.ProjectServices.Models;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.Projects.Pages;

public partial class AIVideoSearch : ComponentBase
{
    #region Dependency Injection

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IProjectVideoSearchService SearchService { get; set; } = default!;

    [Inject]
    private IProjectService ProjectService { get; set; } = default!;

    [Inject]
    private IAddToProjectService AddToProjectService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IMessageCenterService MessageCenter { get; set; } = default!;

    [Inject]
    private ILogger<AIVideoSearch> Logger { get; set; } = default!;

    #endregion

    #region Route Parameters

    [Parameter]
    public Guid Id { get; set; }

    #endregion

    #region State Properties

    private ProjectDetailViewModel? Project;
    private List<VideoSearchItem>? SearchResults;
    private string? AISuggestion;
    private string? CustomQuery;
    private bool IsSearching;
    private string? ErrorMessage;
    private string? CurrentUserId;

    // Track which videos have been added and which is currently being added
    private HashSet<string> AddedVideoIds = new(StringComparer.OrdinalIgnoreCase);
    private string? AddingVideoId;

    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        CurrentUserId = await AuthenticationHelper.GetCurrentUserIdAsync(AuthenticationStateTask, Logger);

        if (string.IsNullOrEmpty(CurrentUserId))
        {
            ErrorMessage = "User not authenticated.";
            return;
        }

        // Read custom query from URL if provided
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        if (QueryHelpers.ParseQuery(uri.Query).TryGetValue("q", out var queryValue))
        {
            CustomQuery = queryValue.ToString();
        }

        // Load project info for sidebar display
        try
        {
            Project = await ProjectService.GetProjectDetailAsync(Id, CurrentUserId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not load project {ProjectId} for AI search", Id);
            ErrorMessage = "Project not found or you don't have access to it.";
            return;
        }

        // Automatically start the search
        await PerformSearchAsync();
    }

    #endregion

    #region Search Operations

    private async Task PerformSearchAsync()
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return;

        try
        {
            IsSearching = true;
            ErrorMessage = null;
            StateHasChanged();

            var result = await SearchService.SearchVideosAsync(Id, CurrentUserId, CustomQuery);

            if (result.Success)
            {
                SearchResults = result.Videos;
                AISuggestion = result.AISuggestion;
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error performing AI video search for project {ProjectId}", Id);
            ErrorMessage = "An unexpected error occurred. Please try again.";
        }
        finally
        {
            IsSearching = false;
            StateHasChanged();
        }
    }

    #endregion

    #region Video Operations

    private async Task HandleAddToProject(VideoSearchItem item)
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return;

        try
        {
            AddingVideoId = item.VideoInfo.YouTubeVideoId;
            StateHasChanged();

            var request = new AddToProjectRequest
            {
                VideoInfo = item.VideoInfo,
                ProjectIds = new List<Guid> { Id },
                UserId = CurrentUserId
            };

            var result = await AddToProjectService.AddVideoToProjectsAsync(request);

            if (result.Success && result.AddedToProjectsCount > 0)
            {
                AddedVideoIds.Add(item.VideoInfo.YouTubeVideoId);
                await MessageCenter.ShowSuccessAsync($"'{item.VideoInfo.Title}' added to project.");
            }
            else
            {
                var error = result.ErrorMessage ?? result.ProjectErrors.Values.FirstOrDefault() ?? "Failed to add video.";
                await MessageCenter.ShowErrorAsync(error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding video {VideoId} to project {ProjectId}",
                item.VideoInfo.YouTubeVideoId, Id);
            await MessageCenter.ShowErrorAsync("An unexpected error occurred. Please try again.");
        }
        finally
        {
            AddingVideoId = null;
            StateHasChanged();
        }
    }

    #endregion
}
