using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Model for adding a video to the user's library.
/// Used when a user wants to save a specific video.
/// </summary>
public class AddVideoModel
{
    /// <summary>
    /// Video URL or search term to add to library.
    /// </summary>
    [Required(ErrorMessage = "Please enter a YouTube video URL or search for a video")]
    [StringLength(500, ErrorMessage = "Video URL cannot exceed 500 characters")]
    public string VideoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about why this video was added to the library.
    /// </summary>
    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }

    /// <summary>
    /// Whether to mark this video as priority/important.
    /// </summary>
    public bool IsPriority { get; set; } = false;

    /// <summary>
    /// Tags to categorize this video (comma-separated).
    /// </summary>
    [StringLength(200, ErrorMessage = "Tags cannot exceed 200 characters")]
    public string? Tags { get; set; }

    /// <summary>
    /// Extracted YouTube video ID from the URL.
    /// </summary>
    public string? VideoId { get; set; }

    /// <summary>
    /// Whether the URL validation has passed.
    /// </summary>
    public bool IsValidUrl { get; set; }

    /// <summary>
    /// Error message if URL validation failed.
    /// </summary>
    public string? ValidationError { get; set; }

    /// <summary>
    /// Validates the video URL and extracts the video ID.
    /// </summary>
    public void ValidateAndExtractVideoId()
    {
        if (string.IsNullOrWhiteSpace(VideoUrl))
        {
            IsValidUrl = false;
            ValidationError = "Video URL is required";
            VideoId = null;
            return;
        }

        VideoId = TargetBrowse.Features.Videos.Utilities.YouTubeVideoParser.ExtractVideoId(VideoUrl);
        
        if (string.IsNullOrEmpty(VideoId))
        {
            IsValidUrl = false;
            ValidationError = "Please enter a valid YouTube video URL";
        }
        else
        {
            IsValidUrl = true;
            ValidationError = null;
        }
    }

    /// <summary>
    /// Gets the parsed tags as a list.
    /// </summary>
    public List<string> GetTagsList()
    {
        if (string.IsNullOrWhiteSpace(Tags))
            return new List<string>();

        return Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(tag => tag.Trim())
                  .Where(tag => !string.IsNullOrWhiteSpace(tag))
                  .Take(10) // Limit to 10 tags
                  .ToList();
    }

    /// <summary>
    /// Resets the model to default state.
    /// </summary>
    public void Reset()
    {
        VideoUrl = string.Empty;
        Notes = null;
        IsPriority = false;
        Tags = null;
        VideoId = null;
        IsValidUrl = false;
        ValidationError = null;
    }
}