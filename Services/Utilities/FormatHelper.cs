using System;

namespace TargetBrowse.Services.Utilities;

/// <summary>
/// Static helper class for consistent formatting across the application.
/// Provides standardized formatting for counts, dates, and durations.
/// </summary>
public static class FormatHelper
{
    /// <summary>
    /// Formats large numbers into human-readable format.
    /// Rules: Billions "B" no decimals, Millions "M" no decimals, Thousands "K" 1 decimal, &lt;1000 actual number.
    /// </summary>
    public static string FormatCount(ulong? count)
    {
        if (!count.HasValue)
            return string.Empty;

        return FormatCountInternal(count.Value);
    }

    /// <summary>
    /// Formats large numbers into human-readable format.
    /// Rules: Billions "B" no decimals, Millions "M" no decimals, Thousands "K" 1 decimal, &lt;1000 actual number.
    /// </summary>
    public static string FormatCount(int? count)
    {
        if (!count.HasValue)
            return string.Empty;

        return FormatCountInternal((ulong)count.Value);
    }

    /// <summary>
    /// Internal method that contains the shared formatting logic.
    /// </summary>
    private static string FormatCountInternal(ulong count)
    {
        if (count == 0)
            return "0";

        var value = (double)count;

        return value switch
        {
            >= 1_000_000_000 => $"{Math.Round(value / 1_000_000_000)}B",
            >= 1_000_000 => $"{Math.Round(value / 1_000_000)}M",
            >= 1_000 => $"{value / 1_000:F1}K",
            _ => count.ToString("N0")
        };
    }

/// <summary>
    /// Formats dates for normal display with relative time.
    /// Rules: &lt;1 hour "just now", &lt;24 hours "X hours ago", then days/weeks/months/years ago.
    /// Uses proper singular and plural forms for time units.
    /// </summary>
    public static string FormatDateDisplay(DateTime? date)
    {
        if (!date.HasValue)
            return string.Empty;

        var now = DateTime.UtcNow;
        var timeSpan = now - date.Value;

        return timeSpan.TotalDays switch
        {
            < 1 when timeSpan.TotalHours < 1 => "just now",
            < 1 when timeSpan.TotalHours < 24 => $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours != 1 ? "s" : "")} ago",
            < 7 => $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays != 1 ? "s" : "")} ago",
            < 30 => $"{(int)(timeSpan.TotalDays / 7)} week{((int)(timeSpan.TotalDays / 7) != 1 ? "s" : "")} ago",
            < 365 => $"{(int)(timeSpan.TotalDays / 30)} month{((int)(timeSpan.TotalDays / 30) != 1 ? "s" : "")} ago",
            _ => $"{(int)(timeSpan.TotalDays / 365)} year{((int)(timeSpan.TotalDays / 365) != 1 ? "s" : "")} ago"
        };
    }

    /// <summary>
    /// Formats dates for update display with "updated" prefix.
    /// Uses the same logic as FormatDateDisplay but prefixes with "updated".
    /// </summary>
    public static string FormatUpdateDateDisplay(DateTime? date)
    {
        if (!date.HasValue)
            return string.Empty;

        var dateText = FormatDateDisplay(date);
        return string.IsNullOrEmpty(dateText) ? string.Empty : $"updated {dateText}";
    }

    /// <summary>
    /// Formats ISO 8601 duration to human-readable format.
    /// Preserves the existing logic from VideoDisplayModel.FormatDuration().
    /// </summary>
    public static string FormatDuration(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration))
            return string.Empty;

        try
        {
            // Parse ISO 8601 duration (PT4M13S)
            var timespan = System.Xml.XmlConvert.ToTimeSpan(duration);
            var totalSeconds = (int)timespan.TotalSeconds;

            return FormatDurationInternal(totalSeconds);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Formats duration from seconds to human-readable format.
    /// </summary>
    /// <param name="durationInSeconds">Duration in total seconds</param>
    /// <returns>Formatted duration (e.g., "4:13", "1:02:45")</returns>
    public static string FormatDuration(int? durationInSeconds)
    {
        if (!durationInSeconds.HasValue || durationInSeconds.Value < 0)
            return string.Empty;

        return FormatDurationInternal(durationInSeconds.Value);
    }

    /// <summary>
    /// Internal method that contains the shared duration formatting logic.
    /// </summary>
    private static string FormatDurationInternal(int totalSeconds)
    {
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours}:{minutes:D2}:{seconds:D2}";
        }
        else
        {
            return $"{minutes}:{seconds:D2}";
        }
    }
}