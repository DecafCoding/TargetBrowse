using TargetBrowse.Services;

namespace TargetBrowse.Features.Topics.Models;

/// <summary>
/// Display model for topic information in the UI.
/// Provides clean separation between database entity and UI presentation.
/// </summary>
public class TopicDisplayModel
{
    /// <summary>
    /// Unique identifier for the topic.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Topic name as entered by the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// When the topic was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// User-friendly display of when the topic was created.
    /// </summary>
    public string CreatedAtDisplay => FormatHelper.FormatDateDisplay(CreatedAt);
}