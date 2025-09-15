namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// Indicates how a video was discovered for suggestions.
/// Used for scoring bonuses and display purposes.
/// </summary>
public enum SuggestionSource
{
    /// <summary>
    /// Video found only through tracked channel updates.
    /// </summary>
    TrackedChannel,

    /// <summary>
    /// Video found only through topic-based searches.
    /// </summary>
    TopicSearch,

    /// <summary>
    /// Video found through both tracked channels AND topic searches.
    /// Receives highest priority with bonus scoring.
    /// </summary>
    Both,

    /// <summary>
    /// Video added as part of initial channel onboarding.
    /// Bypasses normal suggestion limits to provide immediate value to users.
    /// </summary>
    NewChannel,

    /// <summary>
    /// Video added as part of initial topic onboarding.
    /// Bypasses normal suggestion limits to provide immediate value when user creates new topic.
    /// </summary>
    NewTopic
}

/// <summary>
/// Indicates the scoring stage for analytics and debugging.
/// </summary>
public enum ScoringStage
{
    /// <summary>
    /// Initial scoring using title analysis only (fast, cost-effective).
    /// </summary>
    Preliminary,

    /// <summary>
    /// Enhanced scoring using transcript analysis (future implementation).
    /// </summary>
    Enhanced
}

/// <summary>
/// Status of a suggestion in the user's queue.
/// </summary>
public enum SuggestionStatus
{
    /// <summary>
    /// Suggestion is pending user review.
    /// </summary>
    Pending,

    /// <summary>
    /// User approved the suggestion (video added to library).
    /// </summary>
    Approved,

    /// <summary>
    /// User denied the suggestion (removed from queue).
    /// </summary>
    Denied,

    /// <summary>
    /// Suggestion expired due to 30-day limit.
    /// </summary>
    Expired
}