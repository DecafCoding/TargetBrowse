using TargetBrowse.Data.Entities;

namespace TargetBrowse.Services.Models;

/// <summary>
/// Shared DTO representing a video in a user's library.
/// Combines video data (VideoInfo) with user-specific library context.
/// Domain-neutral model used across all features for library operations.
/// </summary>
public class UserLibraryVideoDto
{
    /// <summary>
    /// Core video information from YouTube.
    /// </summary>
    public VideoInfo Video { get; set; } = new();

    /// <summary>
    /// System-generated ID for the video entity.
    /// </summary>
    public Guid VideoId { get; set; }

    /// <summary>
    /// System-generated ID for the user-video relationship.
    /// </summary>
    public Guid UserVideoId { get; set; }

    /// <summary>
    /// When this video was added to the user's library.
    /// </summary>
    public DateTime AddedToLibraryAt { get; set; }

    /// <summary>
    /// User's watch status for this video.
    /// </summary>
    public WatchStatus WatchStatus { get; set; }

    /// <summary>
    /// Optional notes the user added when saving this video.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// User's rating for this video (if rated).
    /// </summary>
    public UserVideoRating? Rating { get; set; }

    /// <summary>
    /// Video type ID for content classification.
    /// Null if no type has been assigned.
    /// </summary>
    public Guid? VideoTypeId { get; set; }

    /// <summary>
    /// Video type name (e.g., "Tutorial", "Podcast", "Vlog").
    /// Null if no type has been assigned.
    /// </summary>
    public string? VideoTypeName { get; set; }

    /// <summary>
    /// Video type code (e.g., "TUTORIAL", "PODCAST", "VLOG").
    /// Null if no type has been assigned.
    /// </summary>
    public string? VideoTypeCode { get; set; }
}

/// <summary>
/// User's rating information for a video.
/// Simplified version for DTO purposes.
/// </summary>
public class UserVideoRating
{
    public Guid RatingId { get; set; }
    public int Stars { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
