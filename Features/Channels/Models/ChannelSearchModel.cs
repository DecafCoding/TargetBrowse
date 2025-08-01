using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Channels.Models;

/// <summary>
/// Form model for searching YouTube channels.
/// Supports both channel name search and direct YouTube URL input.
/// </summary>
public class ChannelSearchModel
{
    [Required(ErrorMessage = "Please enter a channel name or YouTube URL.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Search term must be between 2 and 200 characters.")]
    [Display(Name = "Channel Name or URL")]
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if the search query appears to be a YouTube URL.
    /// </summary>
    public bool IsUrl => IsYouTubeUrl(SearchQuery);

    /// <summary>
    /// Gets the search type for display purposes.
    /// </summary>
    public string SearchType => IsUrl ? "URL" : "Name";

    /// <summary>
    /// Resets the model to initial state for reuse after search.
    /// </summary>
    public void Reset()
    {
        SearchQuery = string.Empty;
    }

    /// <summary>
    /// Checks if the search query appears to be a YouTube URL.
    /// Simple validation for UI purposes - detailed parsing will happen in the service.
    /// </summary>
    private static bool IsYouTubeUrl(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        return query.Contains("youtube.com") ||
               query.Contains("youtu.be") ||
               query.StartsWith("http", StringComparison.OrdinalIgnoreCase);
    }
}