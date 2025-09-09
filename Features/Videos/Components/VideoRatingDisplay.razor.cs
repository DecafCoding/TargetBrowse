using Microsoft.AspNetCore.Components;
using TargetBrowse.Features.Videos.Models;

namespace TargetBrowse.Features.Videos.Components
{
    public partial class VideoRatingDisplay : ComponentBase
    {
        /// <summary>
        /// The video rating to display.
        /// </summary>
        [Parameter] public VideoRatingModel? Rating { get; set; }

        /// <summary>
        /// Additional CSS classes to apply.
        /// </summary>
        [Parameter] public string CssClass { get; set; } = string.Empty;

        /// <summary>
        /// Whether to show the star rating visual.
        /// </summary>
        [Parameter] public bool ShowStars { get; set; } = true;

        /// <summary>
        /// Whether to show the rating label and info.
        /// </summary>
        [Parameter] public bool ShowLabel { get; set; } = true;

        /// <summary>
        /// Whether to show the rating notes.
        /// </summary>
        [Parameter] public bool ShowNotes { get; set; } = true;

        /// <summary>
        /// Whether to show the rating timestamp.
        /// </summary>
        [Parameter] public bool ShowTimestamp { get; set; } = false;

        /// <summary>
        /// Whether to show action buttons (edit, etc.).
        /// </summary>
        [Parameter] public bool ShowActions { get; set; } = false;

        /// <summary>
        /// Whether to show empty state when no rating exists.
        /// </summary>
        [Parameter] public bool ShowEmptyState { get; set; } = false;

        /// <summary>
        /// Whether to truncate long notes.
        /// </summary>
        [Parameter] public bool TruncateNotes { get; set; } = true;

        /// <summary>
        /// Maximum length for notes before truncation.
        /// </summary>
        [Parameter] public int MaxNotesLength { get; set; } = 100;

        /// <summary>
        /// Whether to show expand/collapse button for notes.
        /// </summary>
        [Parameter] public bool ShowExpandButton { get; set; } = true;

        /// <summary>
        /// Callback when edit button is clicked.
        /// </summary>
        [Parameter] public EventCallback<VideoRatingModel> OnEdit { get; set; }

        private bool NotesExpanded = false;

        /// <summary>
        /// Gets the rating text based on stars.
        /// </summary>
        private string GetRatingText()
        {
            if (Rating == null) return string.Empty;

            return Rating.Stars switch
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
        /// Toggles the notes expansion state.
        /// </summary>
        private void ToggleNotesExpansion()
        {
            NotesExpanded = !NotesExpanded;
            StateHasChanged();
        }
    }
}