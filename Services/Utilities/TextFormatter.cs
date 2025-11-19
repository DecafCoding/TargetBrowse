namespace TargetBrowse.Services.Utilities;

/// <summary>
/// Static helper class for text formatting and manipulation.
/// Provides standardized text truncation, capitalization, and display text formatting.
/// </summary>
public static class TextFormatter
{
    /// <summary>
    /// Truncates text to specified length with ellipsis.
    /// </summary>
    /// <param name="text">Text to truncate</param>
    /// <param name="maxLength">Maximum length before truncation</param>
    /// <param name="fallbackText">Text to return if input is null or empty (default: "No description available")</param>
    /// <returns>Truncated text with ellipsis if needed, or fallback text</returns>
    public static string Truncate(string? text, int maxLength, string fallbackText = "No description available")
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallbackText;

        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength).TrimEnd() + "...";
    }

    /// <summary>
    /// Truncates text using more efficient string slicing (modern C# approach).
    /// </summary>
    /// <param name="text">Text to truncate</param>
    /// <param name="maxLength">Maximum length before truncation</param>
    /// <param name="fallbackText">Text to return if input is null or empty (default: empty string)</param>
    /// <returns>Truncated text with ellipsis if needed, or fallback text</returns>
    public static string TruncateSlice(string? text, int maxLength, string fallbackText = "")
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallbackText;

        if (text.Length <= maxLength)
            return text;

        return $"{text[..(maxLength - 3)]}...";
    }

    /// <summary>
    /// Formats a count with singular/plural text.
    /// </summary>
    /// <param name="count">The count to format</param>
    /// <param name="singularText">Text to use for count of 1</param>
    /// <param name="pluralText">Text to use for count != 1</param>
    /// <returns>Formatted text like "1 item" or "5 items"</returns>
    public static string FormatCount(int count, string singularText, string pluralText)
    {
        return count == 1 ? $"{count} {singularText}" : $"{count} {pluralText}";
    }

    /// <summary>
    /// Formats a count with zero, singular, and plural options.
    /// </summary>
    /// <param name="count">The count to format</param>
    /// <param name="zeroText">Text to use for count of 0</param>
    /// <param name="singularText">Text to use for count of 1</param>
    /// <param name="pluralText">Text to use for count > 1</param>
    /// <returns>Formatted text</returns>
    public static string FormatCountWithZero(int count, string zeroText, string singularText, string pluralText)
    {
        return count switch
        {
            0 => zeroText,
            1 => singularText,
            _ => $"{count} {pluralText}"
        };
    }
}
