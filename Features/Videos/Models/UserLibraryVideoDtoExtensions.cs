using TargetBrowse.Services.Models;
using TargetBrowse.Services.YouTube;

namespace TargetBrowse.Features.Videos.Models;

/// <summary>
/// Extension methods to convert UserLibraryVideoDto (shared DTO) to VideoDisplayModel (feature model).
/// Handles mapping from domain-neutral library data to Videos feature display model.
/// </summary>
public static class UserLibraryVideoDtoExtensions
{
    /// <summary>
    /// Converts UserLibraryVideoDto to VideoDisplayModel for Videos feature display.
    /// </summary>
    public static VideoDisplayModel ToVideoDisplayModel(this UserLibraryVideoDto dto)
    {
        var model = new VideoDisplayModel
        {
            Id = dto.VideoId,
            UserVideoId = dto.UserVideoId,
            YouTubeVideoId = dto.Video.YouTubeVideoId,
            Title = dto.Video.Title,
            Description = dto.Video.Description,
            ThumbnailUrl = dto.Video.ThumbnailUrl,
            Duration = ConvertSecondsToIso8601(dto.Video.Duration),
            ViewCount = dto.Video.ViewCount > 0 ? (ulong)dto.Video.ViewCount : null,
            LikeCount = dto.Video.LikeCount > 0 ? (ulong)dto.Video.LikeCount : null,
            CommentCount = dto.Video.CommentCount > 0 ? (ulong)dto.Video.CommentCount : null,
            PublishedAt = dto.Video.PublishedAt,
            ChannelId = dto.Video.ChannelId,
            ChannelTitle = dto.Video.ChannelName,
            IsInLibrary = true,
            AddedToLibrary = dto.AddedToLibraryAt,
            WatchStatus = dto.WatchStatus,
            // Note: VideoType information is not in UserLibraryVideoDto
            // Features can query separately if needed
            VideoTypeId = null,
            VideoTypeName = null,
            VideoTypeCode = null
        };

        // Map rating if exists
        if (dto.Rating != null)
        {
            model.UserRating = new VideoRatingModel
            {
                Id = dto.Rating.RatingId,
                VideoId = dto.VideoId,
                YouTubeVideoId = dto.Video.YouTubeVideoId,
                VideoTitle = dto.Video.Title,
                UserId = string.Empty, // Not in DTO
                Stars = dto.Rating.Stars,
                Notes = dto.Rating.Notes,
                CreatedAt = dto.Rating.CreatedAt,
                UpdatedAt = dto.Rating.UpdatedAt
            };
        }

        return model;
    }

    /// <summary>
    /// Converts a list of UserLibraryVideoDto to VideoDisplayModel.
    /// </summary>
    public static List<VideoDisplayModel> ToVideoDisplayModels(this List<UserLibraryVideoDto> dtos)
    {
        return dtos.Select(dto => dto.ToVideoDisplayModel()).ToList();
    }

    private static string? ConvertSecondsToIso8601(int durationInSeconds)
    {
        if (durationInSeconds <= 0)
            return null;

        var timeSpan = TimeSpan.FromSeconds(durationInSeconds);
        var result = "PT";

        if (timeSpan.TotalHours >= 1)
        {
            result += $"{(int)timeSpan.TotalHours}H";
        }

        if (timeSpan.Minutes > 0)
        {
            result += $"{timeSpan.Minutes}M";
        }

        if (timeSpan.Seconds > 0)
        {
            result += $"{timeSpan.Seconds}S";
        }

        if (result == "PT")
        {
            result = "PT0S";
        }

        return result;
    }
}
