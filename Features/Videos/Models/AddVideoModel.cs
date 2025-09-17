using System.ComponentModel.DataAnnotations;
using TargetBrowse.Features.Videos.Utilities;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Model for adding a video to the user's library.
/// Used when a user wants to save a specific video.
/// UPDATED: Now supports existing video data to avoid redundant API calls.
/// </summary>
public class AddVideoModel
{
    /// <summary>
    /// YouTube video URL provided by the user.
    /// </summary>
    public string VideoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about why the video was added.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Extracted YouTube video ID (populated during validation).
    /// </summary>
    public string? VideoId { get; private set; }

    /// <summary>
    /// Whether the provided URL is a valid YouTube video URL.
    /// </summary>
    public bool IsValidUrl { get; private set; }

    /// <summary>
    /// Whether this is a direct video URL vs a search query.
    /// </summary>
    public bool IsDirectVideoUrl { get; private set; }

    /// <summary>
    /// ADDED: Existing video data to use instead of making API calls.
    /// When provided, the service will use this data instead of calling YouTube API.
    /// </summary>
    public VideoDisplayModel? ExistingVideoData { get; set; }

    /// <summary>
    /// Validates the video URL and extracts the video ID.
    /// Sets IsValidUrl and VideoId properties.
    /// </summary>
    public void ValidateAndExtractVideoId()
    {
        try
        {
            VideoId = YouTubeVideoParser.ExtractVideoId(VideoUrl);
            IsValidUrl = !string.IsNullOrEmpty(VideoId);
            IsDirectVideoUrl = IsValidUrl;
        }
        catch
        {
            IsValidUrl = false;
            VideoId = null;
            IsDirectVideoUrl = false;
        }
    }

    /// <summary>
    /// Extracts just the video ID from the URL without validation.
    /// </summary>
    public string? ExtractVideoId()
    {
        return YouTubeVideoParser.ExtractVideoId(VideoUrl);
    }

    /// <summary>
    /// Creates an AddVideoModel with existing video data.
    /// This constructor is used when we already have video information
    /// and want to avoid making redundant API calls.
    /// </summary>
    /// <param name="existingVideo">Video data from previous API call or search</param>
    /// <param name="notes">Optional notes about adding the video</param>
    /// <returns>Configured AddVideoModel ready for adding to library</returns>
    public static AddVideoModel FromExistingVideo(VideoDisplayModel existingVideo, string notes = "")
    {
        return new AddVideoModel
        {
            VideoUrl = existingVideo.YouTubeUrl,
            Notes = string.IsNullOrEmpty(notes) ? $"Added from search on {DateTime.Now:yyyy-MM-dd}" : notes,
            ExistingVideoData = existingVideo,
            VideoId = existingVideo.YouTubeVideoId,
            IsValidUrl = true,
            IsDirectVideoUrl = true
        };
    }
}