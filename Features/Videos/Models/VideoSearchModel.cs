using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Model for video search form data and validation.
/// Supports searching by video URL, title, or keywords.
/// </summary>
public class VideoSearchModel
{
    /// <summary>
    /// Search query - can be video URL, title, or keywords.
    /// </summary>
    [Required(ErrorMessage = "Please enter a video URL, title, or keywords")]
    [StringLength(200, ErrorMessage = "Search query cannot exceed 200 characters")]
    public string SearchQuery { get; set; } = string.Empty;

    /// <summary>
    /// Whether to search within specific channels only.
    /// </summary>
    public bool SearchInTrackedChannelsOnly { get; set; } = false;

    /// <summary>
    /// Maximum number of results to return (1-50).
    /// </summary>
    [Range(1, 50, ErrorMessage = "Results limit must be between 1 and 50")]
    public int MaxResults { get; set; } = 25;

    /// <summary>
    /// Filter by video duration.
    /// </summary>
    public VideoDurationFilter DurationFilter { get; set; } = VideoDurationFilter.Any;

    /// <summary>
    /// Filter by upload date.
    /// </summary>
    public VideoDateFilter DateFilter { get; set; } = VideoDateFilter.Any;

    /// <summary>
    /// Sort order for search results.
    /// </summary>
    public VideoSortOrder SortOrder { get; set; } = VideoSortOrder.Relevance;

    /// <summary>
    /// Whether this is a direct video URL search.
    /// </summary>
    public bool IsDirectVideoUrl => IsYouTubeVideoUrl(SearchQuery);

    /// <summary>
    /// Extracts video ID from YouTube URL if the search query is a valid video URL.
    /// </summary>
    public string? ExtractVideoId()
    {
        if (!IsDirectVideoUrl)
            return null;

        return TargetBrowse.Features.Videos.Utilities.YouTubeVideoParser.ExtractVideoId(SearchQuery);
    }

    /// <summary>
    /// Validates if the search query looks like a YouTube video URL.
    /// </summary>
    private static bool IsYouTubeVideoUrl(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return false;

        // Check for common YouTube video URL patterns
        return query.Contains("youtube.com/watch") || 
               query.Contains("youtu.be/") ||
               query.Contains("youtube.com/embed/") ||
               query.Contains("youtube.com/v/");
    }
}

/// <summary>
/// Video duration filter options for search.
/// </summary>
public enum VideoDurationFilter
{
    Any,
    Short,      // Under 4 minutes
    Medium,     // 4-20 minutes  
    Long        // Over 20 minutes
}

/// <summary>
/// Video upload date filter options for search.
/// </summary>
public enum VideoDateFilter
{
    Any,
    LastHour,
    Today,
    ThisWeek,
    ThisMonth,
    ThisYear
}

/// <summary>
/// Sort order options for video search results.
/// </summary>
public enum VideoSortOrder
{
    Relevance,
    UploadDate,
    ViewCount,
    Rating
}