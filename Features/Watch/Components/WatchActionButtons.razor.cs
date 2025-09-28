using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TargetBrowse.Features.Watch.Models;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Features.Watch.Components;

/// <summary>
/// Code-behind for the WatchActionButtons component. 
/// Handles the action buttons in the left sidebar of the watch page.
/// </summary>
public partial class WatchActionButtons : ComponentBase
{
    #region Parameters and Dependencies

    [Parameter, EditorRequired]
    public WatchViewModel Model { get; set; } = default!;

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    #endregion

    #region Action Methods

    /// <summary>
    /// Opens the video in a picture-in-picture style popup window.
    /// </summary>
    protected async Task OpenPictureInPicture()
    {
        try
        {
            var pipUrl = $"https://www.youtube.com/embed/{Model.YouTubeVideoId}?autoplay=1&controls=1&modestbranding=1";

            await JSRuntime.InvokeVoidAsync("window.open",
                pipUrl,
                "pip_video",
                "width=640,height=360,scrollbars=no,resizable=yes,status=no,location=no,toolbar=no,menubar=no"
            );
        }
        catch (Exception)
        {
            // Fallback: open in new tab if popup is blocked
            await JSRuntime.InvokeVoidAsync("window.open", Model.YouTubeUrl, "_blank");
        }
    }

    #endregion

    #region Watch Status Helper Methods

    /// <summary>
    /// Gets the CSS class for the Mark Watched button based on current watch status.
    /// </summary>
    protected string GetWatchedButtonClass()
    {
        return Model.WatchStatus switch
        {
            WatchStatus.Watched => "btn-success",
            WatchStatus.Skipped => "btn-outline-success",
            _ => "btn-outline-success"
        };
    }

    /// <summary>
    /// Gets the text for the Mark Watched button based on current watch status.
    /// </summary>
    protected string GetWatchedButtonText()
    {
        return Model.WatchStatus switch
        {
            WatchStatus.Watched => "Watched",
            WatchStatus.Skipped => "Mark Watched",
            _ => "Mark Watched"
        };
    }

    /// <summary>
    /// Gets the icon class for the Mark Watched button based on current watch status.
    /// </summary>
    protected string GetWatchedButtonIcon()
    {
        return Model.WatchStatus switch
        {
            WatchStatus.Watched => "bi-check-circle-fill",
            _ => "bi-check-circle"
        };
    }

    /// <summary>
    /// Gets the title attribute for the Mark Watched button.
    /// </summary>
    protected string GetWatchedButtonTitle()
    {
        return Model.WatchStatus switch
        {
            WatchStatus.Watched => "Video marked as watched",
            WatchStatus.Skipped => "Mark this video as watched",
            _ => "Mark this video as watched"
        };
    }

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

    #endregion
}