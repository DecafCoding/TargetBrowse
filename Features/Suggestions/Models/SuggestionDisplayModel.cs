namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Display model for showing suggestions in the UI.
/// </summary>
public class SuggestionDisplayModel
{
    /// <summary>
    /// Suggestion entity identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Video information for display.
    /// </summary>
    public VideoInfo Video { get; set; } = null!;

    /// <summary>
    /// Human-readable reason for the suggestion.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// When the suggestion was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Current status of the suggestion.
    /// </summary>
    public SuggestionStatus Status { get; set; }

    /// <summary>
    /// Suggestion score for display (optional).
    /// </summary>
    public double? Score { get; set; }

    /// <summary>
    /// Days until this suggestion expires.
    /// </summary>
    public int DaysUntilExpiry
    {
        get
        {
            var expiryDate = CreatedAt.AddDays(30);
            var daysRemaining = (expiryDate - DateTime.UtcNow).Days;
            return Math.Max(0, daysRemaining);
        }
    }

    /// <summary>
    /// Whether this suggestion is close to expiring (< 3 days).
    /// </summary>
    public bool IsNearExpiry => DaysUntilExpiry <= 3;

    /// <summary>
    /// Gets CSS class for status badge.
    /// </summary>
    public string GetStatusBadgeCss() => Status switch
    {
        SuggestionStatus.Pending => "badge bg-warning",
        SuggestionStatus.Approved => "badge bg-success",
        SuggestionStatus.Denied => "badge bg-danger",
        SuggestionStatus.Expired => "badge bg-secondary",
        _ => "badge bg-light"
    };

    /// <summary>
    /// Gets display text for status.
    /// </summary>
    public string GetStatusText() => Status switch
    {
        SuggestionStatus.Pending => "Pending Review",
        SuggestionStatus.Approved => "Approved",
        SuggestionStatus.Denied => "Denied",
        SuggestionStatus.Expired => "Expired",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets formatted score for display.
    /// </summary>
    public string FormattedScore => Score?.ToString("F1") ?? "N/A";

    /// <summary>
    /// Gets time since creation for display.
    /// </summary>
    public string TimeSinceCreated
    {
        get
        {
            var timeSince = DateTime.UtcNow - CreatedAt;
            return timeSince.TotalDays switch
            {
                < 1 => "Added Today",
                < 7 => $"Added {(int)timeSince.TotalDays} days ago",
                < 30 => $"Added {(int)(timeSince.TotalDays / 7)} weeks ago",
                _ => $"Added {(int)(timeSince.TotalDays / 30)} months ago"
            };
        }
    }

    /// <summary>
    /// Gets the suggestion source enum based on the reason text.
    /// Analyzes the reason string to determine how this suggestion was generated.
    /// </summary>
    /// <returns>The corresponding SuggestionSource enum value</returns>
    public SuggestionSource GetSourceEnum()
    {
        if (Reason.Contains("🎯 New Channel:"))
            return SuggestionSource.NewChannel;
        else if (Reason.Contains("🎯 New Topic:"))
            return SuggestionSource.NewTopic;
        else if (Reason.Contains("⭐"))
            return SuggestionSource.Both;
        else if (Reason.Contains("📺"))
            return SuggestionSource.TrackedChannel;
        else if (Reason.Contains("🔍"))
            return SuggestionSource.TopicSearch;
        else
            return SuggestionSource.TrackedChannel; // Default fallback
    }
}

/// <summary>
/// Statistics for a channel's suggestion performance.
/// </summary>
public class ChannelSuggestionStats
{
    /// <summary>
    /// Channel name.
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Number of suggestions generated from this channel.
    /// </summary>
    public int SuggestionCount { get; set; }

    /// <summary>
    /// Number of suggestions approved from this channel.
    /// </summary>
    public int ApprovedCount { get; set; }

    /// <summary>
    /// Approval rate for suggestions from this channel.
    /// </summary>
    public double ApprovalRate => SuggestionCount > 0 ? (double)ApprovedCount / SuggestionCount : 0;

    /// <summary>
    /// Gets formatted approval rate for display.
    /// </summary>
    public string FormattedApprovalRate => $"{ApprovalRate:P0}";
}

/// <summary>
/// Statistics for a topic's suggestion performance.
/// </summary>
public class TopicSuggestionStats
{
    /// <summary>
    /// Topic name.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Number of suggestions that matched this topic.
    /// </summary>
    public int MatchCount { get; set; }

    /// <summary>
    /// Number of topic-matched suggestions that were approved.
    /// </summary>
    public int ApprovedCount { get; set; }

    /// <summary>
    /// Average score of suggestions that matched this topic.
    /// </summary>
    public double AverageScore { get; set; }

    /// <summary>
    /// Approval rate for suggestions matching this topic.
    /// </summary>
    public double ApprovalRate => MatchCount > 0 ? (double)ApprovedCount / MatchCount : 0;

    /// <summary>
    /// Gets formatted approval rate for display.
    /// </summary>
    public string FormattedApprovalRate => $"{ApprovalRate:P0}";

    /// <summary>
    /// Gets formatted average score for display.
    /// </summary>
    public string FormattedAverageScore => $"{AverageScore:F1}";
}

/// <summary>
/// Scoring result for enhanced video analysis.
/// Future implementation for transcript-based scoring.
/// </summary>
public class VideoScore
{
    /// <summary>
    /// The video that was scored.
    /// </summary>
    public VideoInfo Video { get; set; } = null!;

    /// <summary>
    /// Scoring stage that was applied.
    /// </summary>
    public ScoringStage Stage { get; set; }

    /// <summary>
    /// Channel rating component score.
    /// </summary>
    public double ChannelRatingScore { get; set; }

    /// <summary>
    /// Topic relevance component score.
    /// </summary>
    public double TopicRelevanceScore { get; set; }

    /// <summary>
    /// Recency component score.
    /// </summary>
    public double RecencyScore { get; set; }

    /// <summary>
    /// Topics that matched this video.
    /// </summary>
    public List<string> MatchedTopics { get; set; } = new();

    /// <summary>
    /// Total calculated score.
    /// </summary>
    public double TotalScore { get; set; }

    /// <summary>
    /// Confidence level in the scoring (0-1).
    /// </summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>
    /// Additional context or reasoning for the score.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Gets formatted total score for display.
    /// </summary>
    public string FormattedScore => $"{TotalScore:F1}";

    /// <summary>
    /// Gets score breakdown for debugging.
    /// </summary>
    public string GetScoreBreakdown()
    {
        return $"Channel: {ChannelRatingScore:F1} | Topics: {TopicRelevanceScore:F1} | " +
               $"Recency: {RecencyScore:F1} | Total: {TotalScore:F1}";
    }
}