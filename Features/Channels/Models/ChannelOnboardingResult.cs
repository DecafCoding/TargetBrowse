using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Channels.Models;

/// <summary>
/// Result of channel onboarding process including initial video suggestions.
/// </summary>
public class ChannelOnboardingResult
{
    /// <summary>
    /// Whether the channel was successfully added to tracking.
    /// </summary>
    public bool ChannelAdded { get; set; }

    /// <summary>
    /// Number of initial videos added as suggestions.
    /// </summary>
    public int InitialVideosAdded { get; set; }

    /// <summary>
    /// Any errors that occurred during the process.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Any warnings that occurred during the process.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Whether the overall operation was successful.
    /// </summary>
    public bool IsSuccess => ChannelAdded && !Errors.Any();

    /// <summary>
    /// User-friendly summary message for the operation.
    /// </summary>
    public string GetSummaryMessage()
    {
        if (!ChannelAdded)
        {
            return "Failed to add channel to tracking.";
        }

        var message = "Channel added to your tracking list!";

        if (InitialVideosAdded > 0)
        {
            message += $" {InitialVideosAdded} recent videos have been added to your suggestions.";
        }
        else if (Warnings.Any())
        {
            message += " (No recent videos could be retrieved at this time)";
        }

        return message;
    }
}