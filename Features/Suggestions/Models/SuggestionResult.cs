using TargetBrowse.Data.Entities;

using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Comprehensive result of a suggestion generation process.
/// Contains analytics, metrics, and generated suggestions.
/// </summary>
public class SuggestionResult
{
    /// <summary>
    /// List of new suggestions created and saved to database.
    /// </summary>
    public List<SuggestionEntity> NewSuggestions { get; set; } = new();

    /// <summary>
    /// All videos discovered during the process (regardless of scoring).
    /// These are saved for historical browsing.
    /// </summary>
    public List<VideoInfo> AllDiscoveredVideos { get; set; } = new();

    /// <summary>
    /// Number of videos found from tracked channel updates.
    /// </summary>
    public int ChannelVideosFound { get; set; }

    /// <summary>
    /// Number of videos found from topic searches.
    /// </summary>
    public int TopicVideosFound { get; set; }

    /// <summary>
    /// Number of duplicate videos found via both sources.
    /// </summary>
    public int DuplicatesFound { get; set; }

    /// <summary>
    /// Average score across all evaluated videos.
    /// </summary>
    public double AverageScore { get; set; }

    /// <summary>
    /// Distribution of scores for analytics.
    /// Key is score range (e.g., "4-5"), value is count.
    /// </summary>
    public Dictionary<string, int> ScoreDistribution { get; set; } = new();

    /// <summary>
    /// Processing time for the suggestion generation.
    /// </summary>
    public TimeSpan ProcessingTime { get; set; }

    /// <summary>
    /// Any warnings or issues encountered during processing.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Success status of the operation.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// API calls made during the process for quota tracking.
    /// </summary>
    public SuggestionApiUsage ApiUsage { get; set; } = new();

    /// <summary>
    /// Gets total unique videos discovered.
    /// </summary>
    public int TotalUniqueVideos => ChannelVideosFound + TopicVideosFound - DuplicatesFound;

    /// <summary>
    /// Gets success rate (suggestions created / total videos evaluated).
    /// </summary>
    public double SuccessRate => AllDiscoveredVideos.Count > 0
        ? (double)NewSuggestions.Count / AllDiscoveredVideos.Count
        : 0;

    /// <summary>
    /// Gets formatted success rate for display.
    /// </summary>
    public string FormattedSuccessRate => $"{SuccessRate:P1}";

    /// <summary>
    /// Gets a summary message for user feedback.
    /// </summary>
    public string GetSummaryMessage()
    {
        if (!IsSuccess)
            return ErrorMessage ?? "Suggestion generation failed";

        if (NewSuggestions.Count == 0)
            return "No new suggestions found based on your topics and channels";

        return $"Generated {NewSuggestions.Count} new suggestion{(NewSuggestions.Count == 1 ? "" : "s")} " +
               $"from {TotalUniqueVideos} videos discovered";
    }

    /// <summary>
    /// Creates a failed result with error message.
    /// </summary>
    public static SuggestionResult Failure(string errorMessage)
    {
        return new SuggestionResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}