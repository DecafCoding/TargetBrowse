using TargetBrowse.Features.Topics.Models;

namespace TargetBrowse.Features.Topics.Services;

/// <summary>
/// Service interface for topic onboarding workflows including initial video suggestions.
/// Mirrors IChannelOnboardingService pattern for consistency in the application architecture.
/// </summary>
public interface ITopicOnboardingService
{
    /// <summary>
    /// Adds initial videos for a newly created topic as suggestions.
    /// Searches YouTube for videos matching the topic and creates suggestion entities.
    /// </summary>
    /// <param name="userId">User ID who created the topic</param>
    /// <param name="topicName">Name of the newly created topic</param>
    /// <param name="topicId">ID of the newly created topic entity</param>
    /// <returns>Number of video suggestions created</returns>
    Task<int> AddInitialVideosAsync(string userId, string topicName, Guid topicId);

    /// <summary>
    /// Performs complete topic onboarding including initial video discovery.
    /// Called after successful topic creation to provide immediate value to users.
    /// Non-blocking design ensures topic creation succeeds even if video discovery fails.
    /// </summary>
    /// <param name="userId">User ID who created the topic</param>
    /// <param name="topicName">Name of the newly created topic</param>
    /// <param name="topicId">ID of the newly created topic entity</param>
    /// <returns>Onboarding result with success status and metrics</returns>
    Task<TopicOnboardingResult> OnboardTopicAsync(string userId, string topicName, Guid topicId);
}