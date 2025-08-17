using System.Text.RegularExpressions;

namespace TargetBrowse.Features.Videos.Utilities;

/// <summary>
/// Utility class for parsing YouTube video URLs and extracting video IDs.
/// Supports all common YouTube URL formats.
/// </summary>
public static class YouTubeVideoParser
{
    // Regex patterns for different YouTube URL formats
    private static readonly Regex[] VideoIdPatterns = new[]
    {
        // youtube.com/watch?v=VIDEO_ID
        new Regex(@"(?:youtube\.com\/watch\?v=)([a-zA-Z0-9_-]{11})", RegexOptions.IgnoreCase),
        
        // youtu.be/VIDEO_ID
        new Regex(@"(?:youtu\.be\/)([a-zA-Z0-9_-]{11})", RegexOptions.IgnoreCase),
        
        // youtube.com/embed/VIDEO_ID
        new Regex(@"(?:youtube\.com\/embed\/)([a-zA-Z0-9_-]{11})", RegexOptions.IgnoreCase),
        
        // youtube.com/v/VIDEO_ID
        new Regex(@"(?:youtube\.com\/v\/)([a-zA-Z0-9_-]{11})", RegexOptions.IgnoreCase),
        
        // Direct video ID (11 characters)
        new Regex(@"^([a-zA-Z0-9_-]{11})$", RegexOptions.IgnoreCase)
    };

    /// <summary>
    /// Extracts YouTube video ID from various URL formats.
    /// Returns null if no valid video ID is found.
    /// </summary>
    /// <param name="input">YouTube URL or video ID</param>
    /// <returns>11-character video ID or null if invalid</returns>
    public static string? ExtractVideoId(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Clean up the input
        input = input.Trim();

        // Try each pattern until we find a match
        foreach (var pattern in VideoIdPatterns)
        {
            var match = pattern.Match(input);
            if (match.Success && match.Groups.Count > 1)
            {
                var videoId = match.Groups[1].Value;
                if (IsValidVideoId(videoId))
                {
                    return videoId;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Validates if a string is a properly formatted YouTube video ID.
    /// YouTube video IDs are exactly 11 characters: letters, numbers, hyphens, underscores.
    /// </summary>
    /// <param name="videoId">Potential video ID to validate</param>
    /// <returns>True if valid video ID format</returns>
    public static bool IsValidVideoId(string? videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return false;

        // YouTube video IDs are exactly 11 characters
        if (videoId.Length != 11)
            return false;

        // Only allow letters, numbers, hyphens, and underscores
        return videoId.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }

    /// <summary>
    /// Checks if the input string appears to be any type of YouTube URL.
    /// </summary>
    /// <param name="input">Input string to check</param>
    /// <returns>True if input looks like a YouTube URL</returns>
    public static bool IsYouTubeUrl(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        input = input.ToLowerInvariant();
        
        return input.Contains("youtube.com") || 
               input.Contains("youtu.be") ||
               input.Contains("m.youtube.com");
    }

    /// <summary>
    /// Generates a standard YouTube watch URL from a video ID.
    /// </summary>
    /// <param name="videoId">YouTube video ID</param>
    /// <returns>Full YouTube watch URL</returns>
    public static string GenerateWatchUrl(string videoId)
    {
        if (!IsValidVideoId(videoId))
            throw new ArgumentException("Invalid video ID format", nameof(videoId));

        return $"https://www.youtube.com/watch?v={videoId}";
    }

    /// <summary>
    /// Generates a YouTube thumbnail URL from a video ID.
    /// </summary>
    /// <param name="videoId">YouTube video ID</param>
    /// <param name="quality">Thumbnail quality (default, medium, high, standard, maxres)</param>
    /// <returns>YouTube thumbnail URL</returns>
    public static string GenerateThumbnailUrl(string videoId, string quality = "medium")
    {
        if (!IsValidVideoId(videoId))
            throw new ArgumentException("Invalid video ID format", nameof(videoId));

        var qualityMap = new Dictionary<string, string>
        {
            { "default", "default" },
            { "medium", "mqdefault" },
            { "high", "hqdefault" },
            { "standard", "sddefault" },
            { "maxres", "maxresdefault" }
        };

        var thumbnailQuality = qualityMap.ContainsKey(quality.ToLowerInvariant()) 
            ? qualityMap[quality.ToLowerInvariant()] 
            : "mqdefault";

        return $"https://img.youtube.com/vi/{videoId}/{thumbnailQuality}.jpg";
    }

    /// <summary>
    /// Parse result containing the extracted video ID and validation status.
    /// </summary>
    public class ParseResult
    {
        public string? VideoId { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public string? OriginalInput { get; set; }

        public static ParseResult Success(string videoId, string originalInput)
        {
            return new ParseResult
            {
                VideoId = videoId,
                IsValid = true,
                OriginalInput = originalInput
            };
        }

        public static ParseResult Failure(string errorMessage, string originalInput)
        {
            return new ParseResult
            {
                IsValid = false,
                ErrorMessage = errorMessage,
                OriginalInput = originalInput
            };
        }
    }

    /// <summary>
    /// Comprehensive parsing method that returns detailed result information.
    /// </summary>
    /// <param name="input">Input string to parse</param>
    /// <returns>ParseResult with detailed information</returns>
    public static ParseResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ParseResult.Failure("Input cannot be empty", input);
        }

        var videoId = ExtractVideoId(input);
        
        if (string.IsNullOrEmpty(videoId))
        {
            if (IsYouTubeUrl(input))
            {
                return ParseResult.Failure("YouTube URL found but no valid video ID could be extracted", input);
            }
            else
            {
                return ParseResult.Failure("Input does not appear to be a valid YouTube video URL or ID", input);
            }
        }

        return ParseResult.Success(videoId, input);
    }
}