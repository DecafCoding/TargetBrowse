using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Features.Channels.Services;

namespace TargetBrowse.Features.Channels.Components;

/// <summary>
/// Base class for ChannelSearch component containing all the C# logic
/// </summary>
public partial class ChannelSearch : ComponentBase
{
    #region Injected Services

    [Inject]
    protected IChannelService ChannelService { get; set; } = default!;

    #endregion

    #region Parameters

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Parameter]
    public EventCallback OnChannelAdded { get; set; }

    [Parameter]
    public EventCallback OnSearchCompleted { get; set; }

    /// <summary>
    /// The tracked channel count from the parent component.
    /// Used to determine if the pulse animation should be shown.
    /// Default value of -1 means count hasn't been set by parent.
    /// </summary>
    [Parameter]
    public int ParentChannelCount { get; set; } = -1;

    #endregion

    #region Protected Properties

    protected ChannelSearchModel SearchModel { get; set; } = new();
    protected bool IsSearching { get; set; } = false;
    protected bool IsAdding { get; set; } = false;
    protected bool HasSearched { get; set; } = false;
    protected ChannelDisplayModel? _channelBeingAdded;

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the list of search results.
    /// </summary>
    public List<ChannelDisplayModel> SearchResults { get; set; } = new();

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles the search form submission.
    /// Searches for YouTube channels based on the user's query.
    /// </summary>
    protected async Task HandleSearch()
    {
        if (IsSearching) return;

        try
        {
            IsSearching = true;
            HasSearched = false;
            SearchResults.Clear();
            StateHasChanged();

            // Perform the search
            var results = await ChannelService.SearchChannelsAsync(SearchModel.SearchQuery);
            SearchResults = results ?? new List<ChannelDisplayModel>();
            HasSearched = true;

            // Clear search form if results found
            if (SearchResults.Any())
            {
                SearchModel.Reset();
            }

            // Notify parent component that search is completed
            if (OnSearchCompleted.HasDelegate)
            {
                await OnSearchCompleted.InvokeAsync();
            }
        }
        finally
        {
            IsSearching = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Adds a channel to the user's tracking list.
    /// Removes the channel from search results upon successful addition.
    /// </summary>
    /// <param name="channel">The channel to add to tracking</param>
    protected async Task AddChannelToTracking(ChannelDisplayModel channel)
    {
        if (IsAdding) return;

        try
        {
            IsAdding = true;
            _channelBeingAdded = channel;
            StateHasChanged();

            // Get current user
            var authState = await AuthenticationStateTask!;
            var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                // This shouldn't happen with [Authorize] attribute, but safety first
                return;
            }

            // Convert to AddChannelModel and add to tracking
            var addChannelModel = AddChannelModel.FromDisplayModel(channel);
            var success = await ChannelService.AddChannelToTrackingAsync(userId, addChannelModel);

            if (success)
            {
                // Remove the added channel from search results
                SearchResults.Remove(channel);

                // Notify parent component that a channel was added
                if (OnChannelAdded.HasDelegate)
                {
                    await OnChannelAdded.InvokeAsync();
                }
            }
        }
        finally
        {
            IsAdding = false;
            _channelBeingAdded = null;
            StateHasChanged();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Determines if the Search button and input should show the pulse animation.
    /// Shows pulse when there are no channels tracked (count is 0).
    /// </summary>
    protected bool ShouldShowPulse()
    {
        // Use parent channel count if available (preferred)
        if (ParentChannelCount >= 0)
        {
            return ParentChannelCount == 0;
        }

        // Default to not showing pulse if count not available
        return false;
    }

    #endregion
}