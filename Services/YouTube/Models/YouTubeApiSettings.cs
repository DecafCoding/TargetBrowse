namespace TargetBrowse.Services.YouTube.Models;

/// <summary>
/// YouTube API configuration settings.
/// </summary>
public class YouTubeApiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public int MaxSearchResults { get; set; } = 10;
    public int DailyQuotaLimit { get; set; } = 10000;
}
