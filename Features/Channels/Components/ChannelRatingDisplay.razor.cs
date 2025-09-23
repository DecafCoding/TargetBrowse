using Microsoft.AspNetCore.Components;
using TargetBrowse.Features.Channels.Models;

namespace TargetBrowse.Features.Channels.Components;

/// <summary>
/// Component for displaying and managing channel ratings.
/// Provides a simplified UI with clickable stars for rating/editing.
/// </summary>
public partial class ChannelRatingDisplay
{
    #region Parameters

    /// <summary>
    /// The channel rating to display. If null, shows empty stars for rating.
    /// </summary>
    [Parameter]
    public ChannelRatingModel? Rating { get; set; }

    /// <summary>
    /// Whether to show the details button that toggles rating details visibility.
    /// </summary>
    [Parameter]
    public bool ShowDetailsButton { get; set; } = true;

    /// <summary>
    /// Whether to show the edit button for modifying existing ratings.
    /// </summary>
    [Parameter]
    public bool ShowEditButton { get; set; } = true;

    /// <summary>
    /// Whether the rating buttons should be disabled.
    /// </summary>
    [Parameter]
    public bool IsDisabled { get; set; } = false;

    /// <summary>
    /// Event callback fired when the user clicks the "Rate" button (when no rating exists).
    /// </summary>
    [Parameter]
    public EventCallback OnRateClick { get; set; }

    /// <summary>
    /// Event callback fired when the user clicks the "Edit" button (when rating exists).
    /// </summary>
    [Parameter]
    public EventCallback OnEditClick { get; set; }

    #endregion

    #region Private Properties

    /// <summary>
    /// Controls whether the rating details panel is expanded.
    /// </summary>
    private bool ShowDetails { get; set; } = false;

    /// <summary>
    /// Controls whether the full notes text is displayed (vs truncated).
    /// </summary>
    private bool ShowFullNotes { get; set; } = false;

    #endregion

    #region Event Handlers

    /// <summary>
    /// Toggles the display of rating details.
    /// Automatically collapses full notes when details are hidden.
    /// </summary>
    private void ToggleDetails()
    {
        ShowDetails = !ShowDetails;
        if (!ShowDetails)
        {
            ShowFullNotes = false;
        }
        StateHasChanged();
    }

    /// <summary>
    /// Handles the rate button click.
    /// Invokes the OnRateClick callback if a delegate is provided.
    /// </summary>
    private async Task HandleRateClick()
    {
        if (OnRateClick.HasDelegate)
        {
            await OnRateClick.InvokeAsync();
        }
    }

    /// <summary>
    /// Handles the edit button click.
    /// Invokes the OnEditClick callback if a delegate is provided.
    /// </summary>
    private async Task HandleEditClick()
    {
        if (OnEditClick.HasDelegate)
        {
            await OnEditClick.InvokeAsync();
        }
    }

    /// <summary>
    /// Handles clicking on the star display.
    /// Routes to appropriate handler based on whether rating exists.
    /// </summary>
    private async Task HandleRatingClick()
    {
        if (IsDisabled) return;

        if (Rating != null)
        {
            await HandleEditClick();
        }
        else
        {
            await HandleRateClick();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the appropriate CSS class for individual stars.
    /// All filled stars are yellow (text-warning), empty stars are muted.
    /// </summary>
    /// <param name="starNumber">The position of the star (1-5)</param>
    /// <returns>Bootstrap CSS class for the star color</returns>
    private string GetStarClass(int starNumber)
    {
        if (Rating == null) return "text-muted";

        return starNumber <= Rating.Stars ? "text-warning" : "text-muted";
    }

    /// <summary>
    /// Gets the star fill type (filled or empty).
    /// </summary>
    /// <param name="starNumber">The position of the star (1-5)</param>
    /// <returns>String suffix for Bootstrap icon class</returns>
    private string GetStarFill(int starNumber)
    {
        if (Rating == null) return "";

        return starNumber <= Rating.Stars ? "-fill" : "";
    }

    /// <summary>
    /// Gets the appropriate tooltip text for the star display.
    /// </summary>
    /// <returns>Tooltip text based on current rating state</returns>
    private string GetClickTooltip()
    {
        if (IsDisabled) return "Rating disabled";

        return Rating != null ? "Click to edit rating" : "Click to rate this channel";
    }

    #endregion
}