namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Analytics data for user suggestions.
/// Provides insights into suggestion usage patterns and system effectiveness.
/// </summary>
public class SuggestionAnalytics
{
    /// <summary>
    /// Total number of suggestions ever generated for this user.
    /// </summary>
    public int TotalSuggestionsGenerated { get; set; }

    /// <summary>
    /// Number of suggestions the user approved (added to library).
    /// </summary>
    public int SuggestionsApproved { get; set; }

    /// <summary>
    /// Number of suggestions the user explicitly denied.
    /// </summary>
    public int SuggestionsDenied { get; set; }

    /// <summary>
    /// Number of suggestions currently pending user review.
    /// </summary>
    public int PendingSuggestions { get; set; }

    /// <summary>
    /// Number of suggestions that expired without being reviewed.
    /// </summary>
    public int SuggestionsExpired { get; set; }

    /// <summary>
    /// When the user last received suggestions (null if never).
    /// </summary>
    public DateTime? LastSuggestionGenerated { get; set; }

    /// <summary>
    /// Approval rate as a percentage (0-100).
    /// </summary>
    public double ApprovalRate => TotalSuggestionsGenerated > 0 
        ? (SuggestionsApproved / (double)TotalSuggestionsGenerated) * 100 
        : 0;

    /// <summary>
    /// Denial rate as a percentage (0-100).
    /// </summary>
    public double DenialRate => TotalSuggestionsGenerated > 0 
        ? (SuggestionsDenied / (double)TotalSuggestionsGenerated) * 100 
        : 0;

    /// <summary>
    /// Percentage of suggestions that were reviewed (approved or denied).
    /// </summary>
    public double ReviewRate => TotalSuggestionsGenerated > 0 
        ? ((SuggestionsApproved + SuggestionsDenied) / (double)TotalSuggestionsGenerated) * 100 
        : 0;

    /// <summary>
    /// How many days since last suggestion generation.
    /// </summary>
    public int DaysSinceLastSuggestion => LastSuggestionGenerated.HasValue 
        ? (int)(DateTime.UtcNow - LastSuggestionGenerated.Value).TotalDays 
        : -1;

    /// <summary>
    /// Gets formatted analytics summary for display.
    /// </summary>
    public string GetSummaryText()
    {
        if (TotalSuggestionsGenerated == 0)
            return "No suggestions generated yet.";

        return $"{TotalSuggestionsGenerated} total, {SuggestionsApproved} approved ({ApprovalRate:F1}%), " +
               $"{PendingSuggestions} pending, {SuggestionsDenied} denied";
    }

    /// <summary>
    /// Determines if user engagement is healthy.
    /// </summary>
    public bool IsHealthyEngagement => ReviewRate >= 50.0 && ApprovalRate >= 20.0;

    /// <summary>
    /// Gets CSS class for approval rate display styling.
    /// </summary>
    public string GetApprovalRateCss() => ApprovalRate switch
    {
        >= 70 => "text-success",
        >= 40 => "text-info", 
        >= 20 => "text-warning",
        _ => "text-danger"
    };

    /// <summary>
    /// Average score of generated suggestions.
    /// </summary>
    public double AverageSuggestionScore { get; set; }

    /// <summary>
    /// Most common suggestion source.
    /// </summary>
    public SuggestionSource MostCommonSource { get; set; }

    /// <summary>
    /// Distribution of suggestions by source.
    /// </summary>
    public Dictionary<SuggestionSource, int> SourceDistribution { get; set; } = new();

    /// <summary>
    /// Top channels that generated suggestions.
    /// </summary>
    public List<ChannelSuggestionStats> TopSuggestionChannels { get; set; } = new();

    /// <summary>
    /// Top topics that matched suggestions.
    /// </summary>
    public List<TopicSuggestionStats> TopSuggestionTopics { get; set; } = new();

    /// <summary>
    /// Gets formatted approval rate for display.
    /// </summary>
    public string FormattedApprovalRate => $"{ApprovalRate:P1}";
}
