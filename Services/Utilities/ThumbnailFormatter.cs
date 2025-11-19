namespace TargetBrowse.Services.Utilities;

/// <summary>
/// Static helper class for generating YouTube thumbnail URLs.
/// Provides standardized thumbnail URL generation with fallback options.
/// </summary>
public static class ThumbnailFormatter
{
    /// <summary>
    /// Gets the thumbnail URL for a YouTube video with fallback.
    /// Prioritizes provided thumbnail URL, then falls back to YouTube's standard thumbnail.
    /// </summary>
    /// <param name="providedThumbnailUrl">Thumbnail URL from API or database</param>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <param name="quality">Thumbnail quality (hqdefault, maxresdefault, etc.)</param>
    /// <returns>Thumbnail URL</returns>
    public static string GetVideoThumbnailUrl(
        string? providedThumbnailUrl,
        string youTubeVideoId,
        ThumbnailQuality quality = ThumbnailQuality.HqDefault)
    {
        if (!string.IsNullOrEmpty(providedThumbnailUrl))
        {
            return providedThumbnailUrl;
        }

        return GetYouTubeThumbnailUrl(youTubeVideoId, quality);
    }

    /// <summary>
    /// Gets a YouTube thumbnail URL for a specific video ID and quality.
    /// </summary>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <param name="quality">Thumbnail quality</param>
    /// <returns>YouTube thumbnail URL</returns>
    public static string GetYouTubeThumbnailUrl(string youTubeVideoId, ThumbnailQuality quality = ThumbnailQuality.HqDefault)
    {
        var qualityString = quality switch
        {
            ThumbnailQuality.Default => "default",
            ThumbnailQuality.MqDefault => "mqdefault",
            ThumbnailQuality.HqDefault => "hqdefault",
            ThumbnailQuality.SdDefault => "sddefault",
            ThumbnailQuality.MaxResDefault => "maxresdefault",
            _ => "hqdefault"
        };

        return $"https://img.youtube.com/vi/{youTubeVideoId}/{qualityString}.jpg";
    }

    /// <summary>
    /// Gets multiple thumbnail URLs for different qualities (for progressive loading).
    /// </summary>
    /// <param name="youTubeVideoId">YouTube video ID</param>
    /// <returns>Dictionary of quality to URL mappings</returns>
    public static Dictionary<ThumbnailQuality, string> GetAllThumbnailQualities(string youTubeVideoId)
    {
        return new Dictionary<ThumbnailQuality, string>
        {
            { ThumbnailQuality.Default, GetYouTubeThumbnailUrl(youTubeVideoId, ThumbnailQuality.Default) },
            { ThumbnailQuality.MqDefault, GetYouTubeThumbnailUrl(youTubeVideoId, ThumbnailQuality.MqDefault) },
            { ThumbnailQuality.HqDefault, GetYouTubeThumbnailUrl(youTubeVideoId, ThumbnailQuality.HqDefault) },
            { ThumbnailQuality.SdDefault, GetYouTubeThumbnailUrl(youTubeVideoId, ThumbnailQuality.SdDefault) },
            { ThumbnailQuality.MaxResDefault, GetYouTubeThumbnailUrl(youTubeVideoId, ThumbnailQuality.MaxResDefault) }
        };
    }
}

/// <summary>
/// YouTube thumbnail quality options.
/// Each quality corresponds to a specific resolution.
/// </summary>
public enum ThumbnailQuality
{
    /// <summary>Default quality (120x90)</summary>
    Default,

    /// <summary>Medium quality (320x180)</summary>
    MqDefault,

    /// <summary>High quality (480x360) - Most reliable</summary>
    HqDefault,

    /// <summary>Standard definition (640x480)</summary>
    SdDefault,

    /// <summary>Maximum resolution (1280x720) - May not exist for all videos</summary>
    MaxResDefault
}
