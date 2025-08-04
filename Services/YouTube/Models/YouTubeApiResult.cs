namespace TargetBrowse.Services.YouTube.Models;

/// <summary>
/// Result wrapper for YouTube API operations.
/// Provides consistent error handling across the application.
/// </summary>
public class YouTubeApiResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsQuotaExceeded { get; set; }
    public bool IsInvalidChannel { get; set; }

    public static YouTubeApiResult<T> Success(T data)
    {
        return new YouTubeApiResult<T>
        {
            IsSuccess = true,
            Data = data
        };
    }

    public static YouTubeApiResult<T> Failure(string errorMessage, bool isQuotaExceeded = false, bool isInvalidChannel = false)
    {
        return new YouTubeApiResult<T>
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            IsQuotaExceeded = isQuotaExceeded,
            IsInvalidChannel = isInvalidChannel
        };
    }
}
