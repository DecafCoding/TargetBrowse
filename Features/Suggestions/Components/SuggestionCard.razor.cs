using Microsoft.AspNetCore.Components;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Suggestions.Components;

/// <summary>
/// Component for displaying individual suggestion cards with interactive elements.
/// </summary>
public partial class SuggestionCard : ComponentBase
{
    #region Parameters

    /// <summary>
    /// The suggestion data to display in the card.
    /// </summary>
    [Parameter, EditorRequired]
    public SuggestionDisplayModel Suggestion { get; set; } = null!;

    /// <summary>
    /// Whether the card allows selection (for batch operations).
    /// </summary>
    [Parameter]
    public bool AllowSelection { get; set; } = false;

    /// <summary>
    /// Whether this card is currently selected.
    /// </summary>
    [Parameter]
    public bool IsSelected { get; set; } = false;

    /// <summary>
    /// Callback invoked when the selection state changes.
    /// </summary>
    [Parameter]
    public EventCallback<SuggestionDisplayModel> OnSelectionChanged { get; set; }

    /// <summary>
    /// Callback invoked when the suggestion is approved.
    /// </summary>
    [Parameter]
    public EventCallback<SuggestionDisplayModel> OnSuggestionApproved { get; set; }

    /// <summary>
    /// Callback invoked when the suggestion is denied.
    /// </summary>
    [Parameter]
    public EventCallback<SuggestionDisplayModel> OnSuggestionDenied { get; set; }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Toggles the selection state of this card and notifies parent component.
    /// </summary>
    private async Task ToggleSelection()
    {
        IsSelected = !IsSelected;
        await OnSelectionChanged.InvokeAsync(Suggestion);
    }

    #endregion

    #region UI Helper Methods

    /// <summary>
    /// Generates the CSS class string for the card based on its state.
    /// </summary>
    private string GetCardCssClass()
    {
        var classes = new List<string>();

        if (IsSelected)
        {
            classes.Add("selected");
        }

        if (Suggestion.IsNearExpiry)
        {
            classes.Add("near-expiry");
        }

        var source = Suggestion.GetSourceEnum();
        if (source == SuggestionSource.Both)
        {
            classes.Add("high-priority");
        }

        return string.Join(" ", classes);
    }

    /// <summary>
    /// Gets the appropriate thumbnail URL for the video.
    /// Falls back to YouTube's thumbnail service if no custom URL is available.
    /// </summary>
    private string GetThumbnailUrl()
    {
        if (!string.IsNullOrEmpty(Suggestion.Video.ThumbnailUrl))
        {
            return Suggestion.Video.ThumbnailUrl;
        }

        // Start with hqdefault which is more reliable than maxresdefault
        return $"https://img.youtube.com/vi/{Suggestion.Video.YouTubeVideoId}/hqdefault.jpg";
    }

    ///// <summary>
    ///// Gets a human-readable description of why this video was suggested.
    ///// </summary>
    //private string GetReasonDescription()
    //{
    //    var source = Suggestion.GetSourceEnum();
    //    return source switch
    //    {
    //        SuggestionSource.Both => "Matches your topics AND from a tracked channel",
    //        SuggestionSource.TrackedChannel => $"New video from {Suggestion.Video.ChannelName}",
    //        SuggestionSource.TopicSearch => "Matches your learning topics",
    //        _ => "Suggested based on your preferences"
    //    };
    //}

    /// <summary>
    /// Gets a truncated version of the video description for display.
    /// Handles null values, removes HTML tags, and truncates at word boundaries.
    /// </summary>
    private string GetTruncatedDescription()
    {
        var description = Suggestion?.Video?.Description;
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        // Remove any HTML tags if present
        description = System.Text.RegularExpressions.Regex.Replace(description, "<.*?>", string.Empty);

        // Truncate to approximately 120 characters, breaking at word boundaries
        const int maxLength = 120;
        if (description.Length <= maxLength)
            return description.Trim();

        var truncated = description.Substring(0, maxLength - 3);
        var lastSpace = truncated.LastIndexOf(' ');

        // If we can find a good word boundary, use it
        if (lastSpace > maxLength / 2)
        {
            return truncated.Substring(0, lastSpace).Trim() + "...";
        }

        // Otherwise, just truncate at the character limit
        return truncated.Trim() + "...";
    }

    #endregion

    #region Static Helper Methods

    /// <summary>
    /// Constructs the YouTube URL for a video.
    /// </summary>
    /// <param name="video">The video information object.</param>
    /// <returns>The complete YouTube watch URL.</returns>
    private static string GetYouTubeUrl(VideoInfo video)
    {
        return $"https://www.youtube.com/watch?v={video.YouTubeVideoId}";
    }

    /// <summary>
    /// Constructs the YouTube channel URL.
    /// </summary>
    /// <param name="video">The video information object.</param>
    /// <returns>The complete YouTube channel URL.</returns>
    private static string GetChannelUrl(VideoInfo video)
    {
        return $"https://www.youtube.com/channel/{video.ChannelId}";
    }

    #endregion
}