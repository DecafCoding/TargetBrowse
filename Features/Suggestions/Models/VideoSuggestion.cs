namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Represents a video suggestion with scoring information.
/// Used during the suggestion generation process before database persistence.
/// </summary>
public class VideoSuggestion
{
    /// <summary>
    /// The video information.
    /// </summary>
    public VideoInfo Video { get; set; } = null!;

    /// <summary>
    /// Total calculated score for this suggestion.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// How this video was discovered.
    /// </summary>
    public SuggestionSource Source { get; set; }

    /// <summary>
    /// What stage of scoring was applied.
    /// </summary>
    public ScoringStage Stage { get; set; }

    /// <summary>
    /// Human-readable reason for the suggestion.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// List of user topics that matched this video (if any).
    /// </summary>
    public List<string> MatchedTopics { get; set; } = new();

    /// <summary>
    /// Component scores for debugging and analytics.
    /// </summary>
    public SuggestionScoreBreakdown ScoreBreakdown { get; set; } = new();

    /// <summary>
    /// Gets the source badge for UI display.
    /// </summary>
    public string GetSourceBadge() => Source switch
    {
        SuggestionSource.TrackedChannel => "📺 Channel Update",
        SuggestionSource.TopicSearch => "🔍 Topic Match",
        SuggestionSource.Both => "⭐ Channel + Topic",
        _ => "❓ Unknown"
    };

    /// <summary>
    /// Gets formatted score for display.
    /// </summary>
    public string FormattedScore => $"{Score:F1}";

    /// <summary>
    /// Determines score quality for UI styling.
    /// </summary>
    public string GetScoreQualityCss() => Score switch
    {
        >= 8.0 => "text-success fw-bold",
        >= 6.0 => "text-info",
        >= 4.0 => "text-warning",
        _ => "text-muted"
    };
}

/// <summary>
/// Detailed breakdown of how a suggestion score was calculated.
/// Used for debugging and analytics.
/// </summary>
public class SuggestionScoreBreakdown
{
    /// <summary>
    /// Score from channel rating (0-10 scale).
    /// </summary>
    public double ChannelRatingScore { get; set; }

    /// <summary>
    /// Score from topic relevance matching (0-10 scale).
    /// </summary>
    public double TopicRelevanceScore { get; set; }

    /// <summary>
    /// Score from video recency (0-10 scale).
    /// </summary>
    public double RecencyScore { get; set; }

    /// <summary>
    /// Bonus points for dual-source discovery.
    /// </summary>
    public double DualSourceBonus { get; set; }

    /// <summary>
    /// Total weighted score before bonuses.
    /// </summary>
    public double BaseScore { get; set; }

    /// <summary>
    /// Final total score including all bonuses.
    /// </summary>
    public double TotalScore { get; set; }

    /// <summary>
    /// Gets a formatted breakdown string for display.
    /// </summary>
    public string GetBreakdownText()
    {
        var parts = new List<string>();

        if (ChannelRatingScore > 0)
            parts.Add($"Channel: {ChannelRatingScore:F1}");

        if (TopicRelevanceScore > 0)
            parts.Add($"Topics: {TopicRelevanceScore:F1}");

        if (RecencyScore > 0)
            parts.Add($"Recency: {RecencyScore:F1}");

        if (DualSourceBonus > 0)
            parts.Add($"Bonus: +{DualSourceBonus:F1}");

        return string.Join(" | ", parts);
    }
}