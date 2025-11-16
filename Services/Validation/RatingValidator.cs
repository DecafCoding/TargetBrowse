namespace TargetBrowse.Services.Validation;

/// <summary>
/// Provides shared validation logic for rating operations.
/// Used by both channel and video rating models to ensure consistent validation rules.
/// </summary>
public static class RatingValidator
{
    /// <summary>
    /// Minimum allowed star rating.
    /// </summary>
    public const int MinStars = 1;

    /// <summary>
    /// Maximum allowed star rating.
    /// </summary>
    public const int MaxStars = 5;

    /// <summary>
    /// Minimum required length for notes (after trimming whitespace).
    /// </summary>
    public const int MinNotesLength = 10;

    /// <summary>
    /// Maximum allowed length for notes.
    /// </summary>
    public const int MaxNotesLength = 1000;

    /// <summary>
    /// Validates a star rating value.
    /// </summary>
    /// <param name="stars">The star rating to validate</param>
    /// <returns>Error message if invalid, null if valid</returns>
    public static string? ValidateStars(int stars)
    {
        if (stars < MinStars || stars > MaxStars)
            return "Rating must be between 1 and 5 stars";

        return null;
    }

    /// <summary>
    /// Validates notes text with proper trimming and length checks.
    /// </summary>
    /// <param name="notes">The notes text to validate</param>
    /// <returns>Error message if invalid, null if valid</returns>
    public static string? ValidateNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return "Notes are required";

        var trimmedLength = notes.Trim().Length;

        if (trimmedLength < MinNotesLength)
            return "Notes must be at least 10 characters";

        if (notes.Length > MaxNotesLength)
            return "Notes must be less than 1000 characters";

        return null;
    }

    /// <summary>
    /// Validates both stars and notes, returning all validation errors.
    /// </summary>
    /// <param name="stars">The star rating to validate</param>
    /// <param name="notes">The notes text to validate</param>
    /// <returns>List of validation error messages (empty if valid)</returns>
    public static List<string> ValidateRating(int stars, string? notes)
    {
        var errors = new List<string>();

        var starsError = ValidateStars(stars);
        if (starsError != null)
            errors.Add(starsError);

        var notesError = ValidateNotes(notes);
        if (notesError != null)
            errors.Add(notesError);

        return errors;
    }

    /// <summary>
    /// Gets the display text for a star rating value.
    /// </summary>
    /// <param name="stars">The star rating value</param>
    /// <returns>Display text describing the rating</returns>
    public static string GetStarDisplayText(int stars) => stars switch
    {
        1 => "1 star - Poor",
        2 => "2 stars - Fair",
        3 => "3 stars - Good",
        4 => "4 stars - Very Good",
        5 => "5 stars - Excellent",
        _ => "Select a rating"
    };

    /// <summary>
    /// Gets the CSS class for displaying a star rating.
    /// </summary>
    /// <param name="stars">The star rating value</param>
    /// <returns>CSS class name for styling the rating</returns>
    public static string GetStarCssClass(int stars) => stars switch
    {
        1 => "text-danger",
        2 => "text-warning",
        3 => "text-info",
        4 => "text-success",
        5 => "text-success",
        _ => "text-muted"
    };
}
