namespace TargetBrowse.Features.Topics.Models;

/// <summary>
/// Result object for topic validation operations.
/// Provides structured feedback for UI and business logic.
/// </summary>
public class TopicValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new List<string>();
    public TopicDto? CreatedTopic { get; set; }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static TopicValidationResult Success(TopicDto? topic = null)
    {
        return new TopicValidationResult
        {
            IsValid = true,
            CreatedTopic = topic
        };
    }

    /// <summary>
    /// Creates a failed validation result with a single error message
    /// </summary>
    public static TopicValidationResult Failure(string errorMessage)
    {
        return new TopicValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            ValidationErrors = new List<string> { errorMessage }
        };
    }

    /// <summary>
    /// Creates a failed validation result with multiple error messages
    /// </summary>
    public static TopicValidationResult Failure(List<string> validationErrors)
    {
        return new TopicValidationResult
        {
            IsValid = false,
            ErrorMessage = string.Join(" ", validationErrors),
            ValidationErrors = validationErrors
        };
    }

    /// <summary>
    /// Adds an error to the validation result
    /// </summary>
    public void AddError(string errorMessage)
    {
        IsValid = false;
        ValidationErrors.Add(errorMessage);
        ErrorMessage = string.Join(" ", ValidationErrors);
    }
}