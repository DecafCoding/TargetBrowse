namespace TargetBrowse.Features.Channels.Models;

/// <summary>
/// Represents the result of channel rating validation operations.
/// Provides success/failure status and detailed error messaging.
/// </summary>
public class ChannelRatingValidationResult
{
    /// <summary>
    /// Indicates if the validation passed.
    /// </summary>
    public bool CanRate { get; private set; }

    /// <summary>
    /// List of validation error messages.
    /// </summary>
    public List<string> ErrorMessages { get; private set; }

    /// <summary>
    /// Primary error message for display.
    /// </summary>
    public string PrimaryError => ErrorMessages.FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// All error messages joined as a single string.
    /// </summary>
    public string AllErrors => string.Join(", ", ErrorMessages);

    /// <summary>
    /// Private constructor to enforce factory methods.
    /// </summary>
    private ChannelRatingValidationResult(bool canRate, List<string> errorMessages)
    {
        CanRate = canRate;
        ErrorMessages = errorMessages ?? new List<string>();
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ChannelRatingValidationResult Success()
    {
        return new ChannelRatingValidationResult(true, new List<string>());
    }

    /// <summary>
    /// Creates a failed validation result with a single error message.
    /// </summary>
    public static ChannelRatingValidationResult Failure(string errorMessage)
    {
        return new ChannelRatingValidationResult(false, new List<string> { errorMessage });
    }

    /// <summary>
    /// Creates a failed validation result with multiple error messages.
    /// </summary>
    public static ChannelRatingValidationResult Failure(params string[] errorMessages)
    {
        return new ChannelRatingValidationResult(false, errorMessages.ToList());
    }

    /// <summary>
    /// Creates a failed validation result with a primary error and additional details.
    /// </summary>
    public static ChannelRatingValidationResult Failure(string primaryError, string detailError)
    {
        return new ChannelRatingValidationResult(false, new List<string> { primaryError, detailError });
    }

    /// <summary>
    /// Creates a failed validation result from a list of error messages.
    /// </summary>
    public static ChannelRatingValidationResult Failure(List<string> errorMessages)
    {
        return new ChannelRatingValidationResult(false, errorMessages);
    }

    /// <summary>
    /// Adds an additional error message to an existing result.
    /// </summary>
    public ChannelRatingValidationResult AddError(string errorMessage)
    {
        ErrorMessages.Add(errorMessage);
        CanRate = false;
        return this;
    }

    /// <summary>
    /// Combines this result with another validation result.
    /// </summary>
    public ChannelRatingValidationResult Combine(ChannelRatingValidationResult other)
    {
        if (other == null) return this;

        var combinedErrors = ErrorMessages.Concat(other.ErrorMessages).ToList();
        var combinedCanRate = CanRate && other.CanRate;

        return new ChannelRatingValidationResult(combinedCanRate, combinedErrors);
    }

    /// <summary>
    /// Returns true if there are any error messages.
    /// </summary>
    public bool HasErrors => ErrorMessages.Any();

    /// <summary>
    /// Returns the count of error messages.
    /// </summary>
    public int ErrorCount => ErrorMessages.Count;

    /// <summary>
    /// Creates a validation result from model validation errors.
    /// </summary>
    public static ChannelRatingValidationResult FromModelErrors(List<string> modelErrors)
    {
        if (!modelErrors.Any())
            return Success();

        return Failure(modelErrors);
    }

    /// <summary>
    /// Implicitly converts boolean to validation result.
    /// </summary>
    public static implicit operator bool(ChannelRatingValidationResult result)
    {
        return result?.CanRate ?? false;
    }

    /// <summary>
    /// String representation for debugging.
    /// </summary>
    public override string ToString()
    {
        if (CanRate)
            return "Validation successful";

        return $"Validation failed: {AllErrors}";
    }
}