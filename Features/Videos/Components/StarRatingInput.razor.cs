using Microsoft.AspNetCore.Components;

namespace TargetBrowse.Features.Videos.Components;

/// <summary>
/// Reusable star rating input component with hover effects and accessibility.
/// </summary>
public partial class StarRatingInput : ComponentBase
{
    /// <summary>
    /// The current star rating value (1-5).
    /// </summary>
    [Parameter] public int CurrentValue { get; set; }

    /// <summary>
    /// Callback when the rating value changes.
    /// </summary>
    [Parameter] public EventCallback<int> CurrentValueChanged { get; set; }

    /// <summary>
    /// Additional CSS classes to apply to the container.
    /// </summary>
    [Parameter] public string CssClass { get; set; } = string.Empty;

    /// <summary>
    /// CSS classes for the rating label.
    /// </summary>
    [Parameter] public string LabelCssClass { get; set; } = "text-muted small";

    /// <summary>
    /// Whether to show the rating label text.
    /// </summary>
    [Parameter] public bool ShowLabel { get; set; } = true;

    /// <summary>
    /// Whether the component is disabled.
    /// </summary>
    [Parameter] public bool IsDisabled { get; set; }

    /// <summary>
    /// Size of the stars (small, normal, large).
    /// </summary>
    [Parameter] public StarSize Size { get; set; } = StarSize.Normal;

    /// <summary>
    /// Whether to show hover effects.
    /// </summary>
    [Parameter] public bool ShowHoverEffects { get; set; } = true;

    private int HoveredValue = 0;

    /// <summary>
    /// Handles star click events.
    /// </summary>
    private async Task OnStarClick(int starValue)
    {
        if (IsDisabled) return;

        // Allow clicking the same star to clear the rating
        var newValue = CurrentValue == starValue ? 0 : starValue;
        CurrentValue = newValue;
        await CurrentValueChanged.InvokeAsync(newValue);
    }

    /// <summary>
    /// Handles star hover events.
    /// </summary>
    private void OnStarHover(int starValue)
    {
        if (IsDisabled || !ShowHoverEffects) return;
        HoveredValue = starValue;
        StateHasChanged();
    }

    /// <summary>
    /// Handles mouse leave events.
    /// </summary>
    private void OnStarLeave()
    {
        if (IsDisabled || !ShowHoverEffects) return;
        HoveredValue = 0;
        StateHasChanged();
    }

    /// <summary>
    /// Gets the CSS class for a specific star.
    /// </summary>
    private string GetStarClass(int starNumber, bool isSelected, bool isHovered)
    {
        var classes = new List<string>();

        // Size classes
        classes.Add(Size switch
        {
            StarSize.Small => "star-small",
            StarSize.Large => "star-large",
            _ => "star-normal"
        });

        // State classes
        if (IsDisabled)
        {
            classes.Add("star-disabled");
        }
        else if (isHovered && ShowHoverEffects)
        {
            classes.Add("star-hovered");
        }
        else if (isSelected)
        {
            classes.Add("star-selected");
        }
        else
        {
            classes.Add("star-unselected");
        }

        return string.Join(" ", classes);
    }

    /// <summary>
    /// Gets the ARIA label for a specific star.
    /// </summary>
    private string GetStarAriaLabel(int starNumber)
    {
        var label = starNumber switch
        {
            1 => "1 star - Poor",
            2 => "2 stars - Fair",
            3 => "3 stars - Good",
            4 => "4 stars - Very Good",
            5 => "5 stars - Excellent",
            _ => $"{starNumber} stars"
        };

        return CurrentValue == starNumber ? $"{label} (currently selected)" : label;
    }

    /// <summary>
    /// Gets the title tooltip for a specific star.
    /// </summary>
    private string GetStarTitle(int starNumber) => GetStarAriaLabel(starNumber);

    /// <summary>
    /// Gets the rating label text.
    /// </summary>
    private string GetRatingLabel()
    {
        return CurrentValue switch
        {
            1 => "Poor",
            2 => "Fair",
            3 => "Good",
            4 => "Very Good",
            5 => "Excellent",
            _ => "No rating"
        };
    }

    /// <summary>
    /// Enumeration for star rating sizes.
    /// </summary>
    public enum StarSize
    {
        Small,
        Normal,
        Large
    }
}