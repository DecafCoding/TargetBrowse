using Microsoft.AspNetCore.Components;
using TargetBrowse.Features.Channels.Models;

namespace TargetBrowse.Features.Channels.Components;

/// <summary>
/// Component for displaying and managing channel ratings.
/// Provides a comprehensive UI for viewing rating details, editing ratings, and handling user interactions.
/// </summary>
public partial class ChannelRatingDisplay
{
    #region Parameters

    /// <summary>
    /// The channel rating to display. If null, shows a "Rate" button instead.
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

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the appropriate CSS class for individual stars based on the rating value.
    /// Provides color-coded feedback: red for 1-star, yellow for 2-star, blue for 3-star, green for 4-5 stars.
    /// </summary>
    /// <param name="starNumber">The position of the star (1-5)</param>
    /// <returns>Bootstrap CSS class for the star color</returns>
    private string GetStarClass(int starNumber)
    {
        if (Rating == null) return "text-muted";

        return Rating.Stars switch
        {
            1 when starNumber <= 1 => "text-danger",
            2 when starNumber <= 2 => "text-warning",
            3 when starNumber <= 3 => "text-info",
            4 when starNumber <= 4 => "text-success",
            5 when starNumber <= 5 => "text-success",
            _ when starNumber <= Rating.Stars => "text-warning",
            _ => "text-muted"
        };
    }

    /// <summary>
    /// Gets the appropriate CSS class for the rating badge.
    /// Matches the color scheme used for stars to provide consistent visual feedback.
    /// </summary>
    /// <returns>Bootstrap CSS class for the badge background</returns>
    private string GetRatingBadgeClass()
    {
        if (Rating == null) return "bg-secondary";

        return Rating.Stars switch
        {
            1 => "bg-danger",
            2 => "bg-warning text-dark",
            3 => "bg-info",
            4 => "bg-success",
            5 => "bg-success",
            _ => "bg-secondary"
        };
    }

    #endregion
}