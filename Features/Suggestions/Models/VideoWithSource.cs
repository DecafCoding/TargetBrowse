using TargetBrowse.Services.Models;

namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Represents a video with information about how it was discovered.
/// Used for smart deduplication and source-aware scoring bonuses.
/// </summary>
public class VideoWithSource
{
    /// <summary>
    /// The video information from YouTube API.
    /// </summary>
    public VideoInfo Video { get; set; } = null!;

    /// <summary>
    /// How this video was discovered (channel, topic, or both).
    /// </summary>
    public SuggestionSource Source { get; set; }

    /// <summary>
    /// Whether this video was found via tracked channel updates.
    /// </summary>
    public bool FoundViaChannel { get; set; }

    /// <summary>
    /// Whether this video was found via topic searches.
    /// </summary>
    public bool FoundViaTopic { get; set; }

    /// <summary>
    /// If found via channel, the channel's rating from the user.
    /// </summary>
    public int? ChannelRating { get; set; }

    /// <summary>
    /// If found via topics, the list of topics that matched.
    /// </summary>
    public List<string> MatchedTopics { get; set; } = new();

    /// <summary>
    /// Helper property to determine if this is a high-priority video (found via both sources).
    /// </summary>
    public bool IsHighPriority => Source == SuggestionSource.Both;

    /// <summary>
    /// Gets a display-friendly source indicator for UI.
    /// </summary>
    public string GetSourceBadge() => Source switch
    {
        SuggestionSource.TrackedChannel => "📺 Channel Update",
        SuggestionSource.TopicSearch => "🔍 Topic Match",
        SuggestionSource.Both => "⭐ Channel + Topic",
        SuggestionSource.NewChannel => "New Channel",
        SuggestionSource.NewTopic => "New Topic",
        _ => "❓ Unknown"
    };

    /// <summary>
    /// Gets CSS class for source badge styling.
    /// </summary>
    public string GetSourceBadgeCss() => Source switch
    {
        SuggestionSource.TrackedChannel => "badge bg-primary",
        SuggestionSource.TopicSearch => "badge bg-info",
        SuggestionSource.Both => "badge bg-success",
        SuggestionSource.NewTopic => "badge bg-success",
        SuggestionSource.NewChannel => "badge bg-success",
        _ => "badge bg-secondary"
    };

    /// <summary>
    /// Creates a human-readable reason for why this video was suggested.
    /// </summary>
    public string CreateSuggestionReason()
    {
        return Source switch
        {
            SuggestionSource.TrackedChannel => $"📺 New from {Video.ChannelName}",
            SuggestionSource.TopicSearch => $"🔍 Topics: {string.Join(", ", MatchedTopics)}",
            SuggestionSource.Both => $"⭐ {Video.ChannelName} + Topics: {string.Join(", ", MatchedTopics)}",
            SuggestionSource.NewChannel => $"⭐ {Video.ChannelName}: Intial Add",
            SuggestionSource.NewTopic => $"Initial Topic Add",
            _ => "Suggested video"
        };
    }
}