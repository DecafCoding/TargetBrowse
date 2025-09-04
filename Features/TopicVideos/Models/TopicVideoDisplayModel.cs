using TargetBrowse.Features.Videos.Models;

namespace TargetBrowse.Features.TopicVideos.Models;

/// <summary>
/// Display model for topic-based video discovery.
/// Extends VideoDisplayModel with topic-specific context and information.
/// </summary>
public class TopicVideoDisplayModel : VideoDisplayModel
{
    /// <summary>
    /// Name of the topic that was searched to find this video.
    /// </summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier of the topic.
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Relevance score for this video relative to the topic.
    /// Higher scores indicate better topic relevance.
    /// </summary>
    public double RelevanceScore { get; set; }

    /// <summary>
    /// Specific keywords from the topic that were found in the video title.
    /// </summary>
    public List<string> MatchedKeywords { get; set; } = new();

    /// <summary>
    /// Display text explaining why this video matches the topic.
    /// </summary>
    public string MatchReason => GenerateMatchReason();

    /// <summary>
    /// Whether this video has a high relevance to the topic.
    /// </summary>
    public bool IsHighRelevance => RelevanceScore >= 7.0;

    /// <summary>
    /// CSS class for relevance indicator.
    /// </summary>
    public string RelevanceCssClass => RelevanceScore switch
    {
        >= 8.0 => "text-success",
        >= 6.0 => "text-warning",
        _ => "text-muted"
    };

    /// <summary>
    /// Icon for relevance indicator.
    /// </summary>
    public string RelevanceIcon => RelevanceScore switch
    {
        >= 8.0 => "bi-bullseye",
        >= 6.0 => "bi-target",
        _ => "bi-circle"
    };

    /// <summary>
    /// Generates a user-friendly explanation of why this video matches the topic.
    /// </summary>
    private string GenerateMatchReason()
    {
        if (!MatchedKeywords.Any())
        {
            return "General topic match";
        }

        var keywordCount = MatchedKeywords.Count;
        if (keywordCount == 1)
        {
            return $"Contains '{MatchedKeywords[0]}'";
        }
        else if (keywordCount == 2)
        {
            return $"Contains '{string.Join("' and '", MatchedKeywords)}'";
        }
        else
        {
            var firstTwo = string.Join("', '", MatchedKeywords.Take(2));
            var remaining = keywordCount - 2;
            return $"Contains '{firstTwo}' and {remaining} other keyword{(remaining == 1 ? "" : "s")}";
        }
    }
}