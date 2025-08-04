using System.Text.RegularExpressions;

namespace TargetBrowse.Features.Channels.Utilities;

/// <summary>
/// Utility class for parsing YouTube URLs and extracting channel information.
/// Supports various YouTube URL formats including channel IDs, usernames, and handles.
/// </summary>
public static class YouTubeUrlParser
{
    private static readonly Regex ChannelIdRegex = new(@"youtube\.com/channel/([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex UserRegex = new(@"youtube\.com/user/([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex CustomUrlRegex = new(@"youtube\.com/c/([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex HandleRegex = new(@"youtube\.com/@([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
    private static readonly Regex ChannelIdValidationRegex = new(@"^UC[a-zA-Z0-9_-]{22}$", RegexOptions.None);

    /// <summary>
    /// Represents the result of parsing a YouTube URL or search term.
    /// </summary>
    public class ParseResult
    {
        public bool IsUrl { get; set; }
        public bool IsValid { get; set; }
        public string? ChannelId { get; set; }
        public string? Username { get; set; }
        public string? Handle { get; set; }
        public string? CustomUrl { get; set; }
        public string SearchTerm { get; set; } = string.Empty;
        public ParseType Type { get; set; }
    }

    public enum ParseType
    {
        SearchTerm,
        ChannelId,
        Username,
        CustomUrl,
        Handle
    }

    /// <summary>
    /// Parses a search query to determine if it's a YouTube URL or search term.
    /// </summary>
    public static ParseResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new ParseResult { IsValid = false, SearchTerm = string.Empty };
        }

        input = input.Trim();

        // Check if it's a URL
        if (!IsYouTubeUrl(input))
        {
            // It's a search term
            return new ParseResult
            {
                IsUrl = false,
                IsValid = true,
                SearchTerm = input,
                Type = ParseType.SearchTerm
            };
        }

        // Parse as YouTube URL
        return ParseYouTubeUrl(input);
    }

    /// <summary>
    /// Checks if the input appears to be a YouTube URL.
    /// </summary>
    public static bool IsYouTubeUrl(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return input.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
               input.Contains("youtu.be", StringComparison.OrdinalIgnoreCase) ||
               input.StartsWith("http", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates if a string is a valid YouTube channel ID.
    /// </summary>
    public static bool IsValidChannelId(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
            return false;

        return ChannelIdValidationRegex.IsMatch(channelId);
    }

    /// <summary>
    /// Parses a YouTube URL and extracts channel information.
    /// </summary>
    private static ParseResult ParseYouTubeUrl(string url)
    {
        // Ensure URL has protocol
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        var result = new ParseResult
        {
            IsUrl = true,
            SearchTerm = url
        };

        try
        {
            // Try to match channel ID
            var channelMatch = ChannelIdRegex.Match(url);
            if (channelMatch.Success)
            {
                var channelId = channelMatch.Groups[1].Value;
                if (IsValidChannelId(channelId))
                {
                    result.ChannelId = channelId;
                    result.Type = ParseType.ChannelId;
                    result.IsValid = true;
                    return result;
                }
            }

            // Try to match handle (@username)
            var handleMatch = HandleRegex.Match(url);
            if (handleMatch.Success)
            {
                result.Handle = handleMatch.Groups[1].Value;
                result.Type = ParseType.Handle;
                result.IsValid = true;
                return result;
            }

            // Try to match custom URL (/c/)
            var customMatch = CustomUrlRegex.Match(url);
            if (customMatch.Success)
            {
                result.CustomUrl = customMatch.Groups[1].Value;
                result.Type = ParseType.CustomUrl;
                result.IsValid = true;
                return result;
            }

            // Try to match username (/user/)
            var userMatch = UserRegex.Match(url);
            if (userMatch.Success)
            {
                result.Username = userMatch.Groups[1].Value;
                result.Type = ParseType.Username;
                result.IsValid = true;
                return result;
            }

            // URL didn't match any known patterns
            result.IsValid = false;
            return result;
        }
        catch
        {
            result.IsValid = false;
            return result;
        }
    }

    /// <summary>
    /// Generates a clean YouTube channel URL from a channel ID.
    /// </summary>
    public static string GenerateChannelUrl(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
            return string.Empty;

        return $"https://www.youtube.com/channel/{channelId}";
    }

    /// <summary>
    /// Extracts the display name from a search term or URL for logging purposes.
    /// </summary>
    public static string GetDisplayName(ParseResult parseResult)
    {
        return parseResult.Type switch
        {
            ParseType.ChannelId => $"Channel ID: {parseResult.ChannelId}",
            ParseType.Handle => $"Handle: @{parseResult.Handle}",
            ParseType.CustomUrl => $"Custom URL: {parseResult.CustomUrl}",
            ParseType.Username => $"Username: {parseResult.Username}",
            ParseType.SearchTerm => $"Search: {parseResult.SearchTerm}",
            _ => parseResult.SearchTerm
        };
    }
}