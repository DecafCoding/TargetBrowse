using TargetBrowse.Features.Channels.Services;
using TargetBrowse.Features.Videos.Services;

namespace TargetBrowse.Features.Shared.Services;

/// <summary>
/// Unified rating service implementation for suggestion generation.
/// Delegates to feature-specific rating services while providing a consolidated interface.
/// </summary>
public class RatingService : IRatingService
{
    private readonly IChannelRatingService _channelRatingService;
    private readonly ILogger<RatingService> _logger;

    public RatingService(
        IChannelRatingService channelRatingService,
        ILogger<RatingService> logger)
    {
        _channelRatingService = channelRatingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets channel ratings optimized for suggestion processing.
    /// </summary>
    public async Task<Dictionary<Guid, int>> GetChannelRatings(string userId)
    {
        try
        {
            return await _channelRatingService.GetChannelRatingsForSuggestionsAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel ratings for suggestions for user {UserId}", userId);
            return new Dictionary<Guid, int>();
        }
    }

    /// <summary>
    /// Gets YouTube channel IDs for channels rated 1-star by the user.
    /// </summary>
    public async Task<List<string>> GetLowRatedYouTubeChannelIds(string userId)
    {
        try
        {
            return await _channelRatingService.GetLowRatedYouTubeChannelIdsAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting low-rated YouTube channels for user {UserId}", userId);
            return new List<string>();
        }
    }
}