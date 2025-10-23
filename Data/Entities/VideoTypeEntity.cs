using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities;

/// <summary>
/// Represents a video type/category for content classification.
/// Examples: Tutorial, Podcast, Vlog, Live Stream, Educational Series, etc.
/// Shared across all users.
/// </summary>
public class VideoTypeEntity : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Short code identifier for the video type (e.g., "TUTORIAL", "PODCAST", "VLOG").
    /// Must be unique across all video types.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<VideoEntity> Videos { get; set; } = new List<VideoEntity>();
}