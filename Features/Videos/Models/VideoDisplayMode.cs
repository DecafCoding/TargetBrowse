namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Defines the display mode for the VideoCard component.
/// Controls which actions and information are shown.
/// </summary>
public enum VideoDisplayMode
{
    /// <summary>
    /// Search mode - shows "Add to Library" actions and search-focused metadata.
    /// Used in search results where users are discovering new videos.
    /// </summary>
    Search,

    /// <summary>
    /// Library mode - shows watch status management and library-specific actions.
    /// Used in the user's video library where they manage saved videos.
    /// </summary>
    Library
}
