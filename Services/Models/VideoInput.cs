namespace TargetBrowse.Services.Models;

/// <summary>
/// Input model for video classification containing video ID and title
/// </summary>
public class VideoInput
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public VideoInput() { }

    public VideoInput(string videoId, string title)
    {
        VideoId = videoId;
        Title = title;
    }
}
