namespace TargetBrowse.Services.Validation;

/// <summary>
/// Interface for rating models that share common validation and display properties.
/// Implemented by both video and channel rating models to ensure consistency.
/// </summary>
public interface IRatingModel
{
    /// <summary>
    /// Star rating value (1-5).
    /// </summary>
    int Stars { get; set; }

    /// <summary>
    /// User's explanatory notes for the rating.
    /// </summary>
    string Notes { get; set; }

    /// <summary>
    /// Validates the model and returns a list of validation errors.
    /// </summary>
    /// <returns>List of validation error messages (empty if valid)</returns>
    List<string> Validate();

    /// <summary>
    /// Indicates if the model is valid for submission.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Gets display text for the selected star rating.
    /// </summary>
    string StarDisplayText { get; }

    /// <summary>
    /// Gets CSS class for star rating display.
    /// </summary>
    string StarCssClass { get; }

    /// <summary>
    /// Trims and cleans the notes field.
    /// </summary>
    void CleanNotes();
}
