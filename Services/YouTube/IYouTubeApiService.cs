using TargetBrowse.Services.YouTube.Models;
using TargetBrowse.Features.Videos.Models;

namespace TargetBrowse.Services.YouTube;

/// <summary>
/// Service interface for YouTube Data API v3 integration.
/// Handles channel search, information retrieval, and API quota management.
/// </summary>
public interface IYouTubeApiService
{
    /// <summary>
    /// Checks if the YouTube API is currently available and within quota limits.
    /// </summary>
    /// <returns>True if API is available, false if quota exceeded or service unavailable</returns>
    Task<bool> IsApiAvailableAsync();

    /// <summary>
    /// Gets the estimated remaining API quota for today.
    /// </summary>
    /// <returns>Estimated remaining quota units</returns>
    Task<int> GetEstimatedRemainingQuotaAsync();
}
