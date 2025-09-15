using TargetBrowse.Features.Channels.Models;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Service interface for channel onboarding workflows.
/// Handles adding new channels and creating initial video suggestions.
/// Uses SharedYouTubeService to ensure consistent shorts exclusion across the application.
/// </summary>
public interface IChannelOnboardingService
{
    /// <summary>
    /// Adds initial videos from a newly tracked channel as suggestions.
    /// This provides immediate value to users when they add a new channel.
    /// Bypasses normal suggestion limits for onboarding purposes.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="youTubeChannelId">YouTube channel identifier</param>
    /// <param name="channelName">Channel display name for user feedback</param>
    /// <returns>Number of videos successfully added as suggestions</returns>
    Task<int> AddInitialVideosAsync(string userId, string youTubeChannelId, string channelName);

    /// <summary>
    /// Performs complete channel onboarding including tracking setup and initial videos.
    /// This is the main orchestration method for the channel addition workflow.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="channelModel">Channel information to add</param>
    /// <returns>Complete onboarding result with success status and metrics</returns>
    Task<ChannelOnboardingResult> OnboardChannelAsync(string userId, AddChannelModel channelModel);
}