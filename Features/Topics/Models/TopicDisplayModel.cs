using TargetBrowse.Services.Utilities;

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

    /// <summary>
    /// Number of days between checks for new content.
    /// </summary>
    public int CheckDays { get; set; }

    /// <summary>
    /// User-friendly display of check frequency.
    /// </summary>
    public string CheckFrequencyDisplay => CheckDays switch
    {
        3 => "Every 3 days",
        5 => "Every 5 days",
        7 => "Weekly",
        14 => "Bi-Weekly",
        _ => $"Every {CheckDays} days"
    };

    /// <summary>
    /// Last date this topic was checked for new content.
    /// </summary>
    public DateTime? LastCheckedDate { get; set; }

    /// <summary>
    /// User-friendly display of when the topic was last checked.
    /// </summary>
    public string LastCheckedDateDisplay => LastCheckedDate.HasValue
        ? FormatHelper.FormatDateDisplay(LastCheckedDate.Value)
        : "Never";

    /// <summary>
    /// Number of videos in the database for this topic.
    /// </summary>
    public int VideosInDatabase { get; set; }

    /// <summary>
    /// Number of videos in the user's library for this topic.
    /// </summary>
    public int VideosInLibrary { get; set; }

    /// <summary>
    /// Number of video suggestions for this topic.
    /// </summary>
    public int VideoSuggestions { get; set; }

    /// <summary>
    /// Display string for video counts.
    /// </summary>
    public string VideoCountsDisplay => $"{VideosInDatabase} / {VideosInLibrary} / {VideoSuggestions}";
}