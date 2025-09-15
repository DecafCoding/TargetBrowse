using TargetBrowse.Features.Suggestions.Models;

namespace TargetBrowse.Features.Topics.Models;

/// <summary>
/// Represents a video with its relevance score for prioritized selection.
/// Used internally by TopicOnboardingService for enhanced video selection.
/// </summary>
internal class ScoredVideoInfo
{
    /// <summary>
    /// The original video information from YouTube API.
    /// </summary>
    public required VideoInfo VideoInfo { get; set; }

    /// <summary>
    /// Relevance score (0-10) based on topic matching algorithm.
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Keywords that matched between video and topic.
    /// </summary>
    public List<string> MatchedKeywords { get; set; } = new();

    /// <summary>
    /// Whether this video is considered highly relevant (score >= 7.0).
    /// </summary>
    public bool IsHighlyRelevant => RelevanceScore >= 7.0;

    /// <summary>
    /// Whether this is a medium duration video (4-20 minutes).
    /// </summary>
    public bool IsMediumDuration => VideoInfo.DurationCategory == "Medium";

    /// <summary>
    /// Whether this is a long duration video (20+ minutes).
    /// </summary>
    public bool IsLongDuration => VideoInfo.DurationCategory == "Long";
}