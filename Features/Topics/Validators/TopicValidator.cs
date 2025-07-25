using TargetBrowse.Features.Topics.Models;
using System.Text.RegularExpressions;

namespace TargetBrowse.Features.Topics.Validators;

/// <summary>
/// Centralized validation logic for topic operations.
/// Handles business rules and data validation.
/// </summary>
public static class TopicValidator
{
    private const int MAX_TOPICS_PER_USER = 10;
    private const int MIN_NAME_LENGTH = 1;
    private const int MAX_NAME_LENGTH = 100;

    /// <summary>
    /// Validates a topic name according to business rules.
    /// </summary>
    /// <param name="topicName">The topic name to validate</param>
    /// <returns>Validation result</returns>
    public static TopicValidationResult ValidateTopicName(string topicName)
    {
        var result = new TopicValidationResult { IsValid = true };

        // Check if name is null or empty
        if (string.IsNullOrWhiteSpace(topicName))
        {
            result.AddError("Topic name is required.");
            return result;
        }

        // Trim whitespace for validation
        topicName = topicName.Trim();

        // Check length requirements
        if (topicName.Length < MIN_NAME_LENGTH)
        {
            result.AddError("Topic name cannot be empty.");
        }

        if (topicName.Length > MAX_NAME_LENGTH)
        {
            result.AddError($"Topic name cannot exceed {MAX_NAME_LENGTH} characters.");
        }

        // Check for invalid characters (optional - remove if not needed)
        if (ContainsInvalidCharacters(topicName))
        {
            result.AddError("Topic name contains invalid characters. Please use letters, numbers, spaces, and common punctuation only.");
        }

        return result;
    }

    /// <summary>
    /// Validates topic limit for a user.
    /// </summary>
    /// <param name="currentTopicCount">Current number of topics for the user</param>
    /// <returns>Validation result</returns>
    public static TopicValidationResult ValidateTopicLimit(int currentTopicCount)
    {
        if (currentTopicCount >= MAX_TOPICS_PER_USER)
        {
            return TopicValidationResult.Failure($"You can only have up to {MAX_TOPICS_PER_USER} topics. Please remove an existing topic before adding a new one.");
        }

        return TopicValidationResult.Success();
    }

    /// <summary>
    /// Validates topic name uniqueness within a user's existing topics.
    /// </summary>
    /// <param name="topicName">The topic name to check</param>
    /// <param name="existingTopics">List of user's existing topics</param>
    /// <param name="excludeTopicId">Topic ID to exclude from check (for updates)</param>
    /// <returns>Validation result</returns>
    public static TopicValidationResult ValidateTopicUniqueness(string topicName, List<TopicDto> existingTopics, Guid? excludeTopicId = null)
    {
        if (string.IsNullOrWhiteSpace(topicName))
        {
            return TopicValidationResult.Success(); // Let other validation handle empty names
        }

        var trimmedName = topicName.Trim();
        var isDuplicate = existingTopics.Any(t =>
            t.Id != excludeTopicId &&
            string.Equals(t.Name.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase));

        if (isDuplicate)
        {
            return TopicValidationResult.Failure("You already have a topic with this name. Please choose a different name.");
        }

        return TopicValidationResult.Success();
    }

    /// <summary>
    /// Normalizes a topic name by trimming whitespace and standardizing format.
    /// </summary>
    /// <param name="topicName">The topic name to normalize</param>
    /// <returns>Normalized topic name</returns>
    public static string NormalizeTopicName(string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return string.Empty;

        // Trim whitespace and normalize multiple spaces to single spaces
        return Regex.Replace(topicName.Trim(), @"\s+", " ");
    }

    /// <summary>
    /// Gets the maximum number of topics allowed per user.
    /// </summary>
    public static int GetMaxTopicsPerUser() => MAX_TOPICS_PER_USER;

    /// <summary>
    /// Checks if topic name contains invalid characters.
    /// Currently allows most printable characters but excludes some problematic ones.
    /// </summary>
    /// <param name="topicName">The topic name to check</param>
    /// <returns>True if contains invalid characters</returns>
    private static bool ContainsInvalidCharacters(string topicName)
    {
        // Allow letters, numbers, spaces, and common punctuation
        // Exclude control characters and potentially problematic characters
        var invalidCharsPattern = @"[^\p{L}\p{N}\p{P}\p{S}\s]|[\x00-\x1F\x7F]";
        return Regex.IsMatch(topicName, invalidCharsPattern);
    }
}