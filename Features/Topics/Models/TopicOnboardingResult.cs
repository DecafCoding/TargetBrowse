namespace TargetBrowse.Features.Topics.Models;

/// <summary>
/// Result of topic onboarding operation including initial video suggestions.
/// Mirrors ChannelOnboardingResult pattern for consistency.
/// </summary>
public class TopicOnboardingResult
{
    /// <summary>
    /// Indicates if the topic was successfully created.
    /// </summary>
    public bool TopicCreated { get; set; }

    /// <summary>
    /// Number of initial video suggestions added for the new topic.
    /// </summary>
    public int InitialVideosAdded { get; set; }

    /// <summary>
    /// Collection of error messages if any occurred during onboarding.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Collection of warning messages for non-critical issues.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Indicates if the overall onboarding operation was successful.
    /// Topic creation succeeds even if video discovery fails.
    /// </summary>
    public bool IsSuccess => TopicCreated && !Errors.Any();

    /// <summary>
    /// Gets a user-friendly summary message for the onboarding result.
    /// </summary>
    public string GetSummaryMessage()
    {
        if (!TopicCreated)
        {
            return "Failed to create topic.";
        }

        if (InitialVideosAdded > 0)
        {
            return $"Topic created successfully with {InitialVideosAdded} video suggestions added to your queue!";
        }

        if (Warnings.Any())
        {
            return "Topic created successfully, but no video suggestions could be retrieved at this time.";
        }

        return "Topic created successfully!";
    }
}