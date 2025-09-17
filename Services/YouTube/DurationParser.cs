namespace TargetBrowse.Services.YouTube;

/// <summary>
/// Utility class for parsing YouTube API duration formats.
/// Extracted from SharedYouTubeService to avoid code duplication.
/// </summary>
public static class DurationParser
{
    /// <summary>
    /// Parses YouTube duration format (PT4M13S) to seconds.
    /// Returns 0 if the duration cannot be parsed.
    /// </summary>
    public static int ParseToSeconds(string? duration)
    {
        try
        {
            if (string.IsNullOrEmpty(duration) || !duration.StartsWith("PT"))
                return 0;

            var timeSpan = System.Xml.XmlConvert.ToTimeSpan(duration);
            return (int)timeSpan.TotalSeconds;
        }
        catch
        {
            return 0;
        }
    }
}