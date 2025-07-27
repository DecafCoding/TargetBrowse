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
    public string CreatedAtDisplay => FormatCreatedAt();

    /// <summary>
    /// Formats the creation date for user-friendly display.
    /// </summary>
    private string FormatCreatedAt()
    {
        var now = DateTime.UtcNow;
        var timeSpan = now - CreatedAt;

        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => "Just now",
            < 1 when timeSpan.TotalHours < 24 => $"{(int)timeSpan.TotalHours}h ago",
            < 7 => $"{(int)timeSpan.TotalDays}d ago",
            < 30 => $"{(int)(timeSpan.TotalDays / 7)}w ago",
            _ => CreatedAt.ToString("MMM d, yyyy")
        };
    }
}