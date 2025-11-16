namespace TargetBrowse.Services.Validation;

/// <summary>
/// Represents the result of rating validation operations.
/// Provides success/failure status and detailed error messaging.
/// Used by both channel and video rating validation.
/// </summary>
public class RatingValidationResult
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
    private RatingValidationResult(bool canRate, List<string> errorMessages)
    {
        CanRate = canRate;
        ErrorMessages = errorMessages ?? new List<string>();
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static RatingValidationResult Success()
    {
        return new RatingValidationResult(true, new List<string>());
    }

    /// <summary>
    /// Creates a failed validation result with a single error message.
    /// </summary>
    public static RatingValidationResult Failure(string errorMessage)
    {
        return new RatingValidationResult(false, new List<string> { errorMessage });
    }

    /// <summary>
    /// Creates a failed validation result with multiple error messages.
    /// </summary>
    public static RatingValidationResult Failure(params string[] errorMessages)
    {
        return new RatingValidationResult(false, errorMessages.ToList());
    }

    /// <summary>
    /// Creates a failed validation result with a primary error and additional details.
    /// </summary>
    public static RatingValidationResult Failure(string primaryError, string detailError)
    {
        return new RatingValidationResult(false, new List<string> { primaryError, detailError });
    }

    /// <summary>
    /// Creates a failed validation result from a list of error messages.
    /// </summary>
    public static RatingValidationResult Failure(List<string> errorMessages)
    {
        return new RatingValidationResult(false, errorMessages);
    }

    /// <summary>
    /// Adds an additional error message to an existing result.
    /// </summary>
    public RatingValidationResult AddError(string errorMessage)
    {
        ErrorMessages.Add(errorMessage);
        CanRate = false;
        return this;
    }

    /// <summary>
    /// Combines this result with another validation result.
    /// </summary>
    public RatingValidationResult Combine(RatingValidationResult other)
    {
        if (other == null) return this;

        var combinedErrors = ErrorMessages.Concat(other.ErrorMessages).ToList();
        var combinedCanRate = CanRate && other.CanRate;

        return new RatingValidationResult(combinedCanRate, combinedErrors);
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
    public static RatingValidationResult FromModelErrors(List<string> modelErrors)
    {
        if (!modelErrors.Any())
            return Success();

        return Failure(modelErrors);
    }

    /// <summary>
    /// Implicitly converts boolean to validation result.
    /// </summary>
    public static implicit operator bool(RatingValidationResult result)
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
