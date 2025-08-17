using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using TargetBrowse.Services.YouTube.Models;
using TargetBrowse.Features.Videos.Models;

namespace TargetBrowse.Services.YouTube;

/// <summary>
/// Implementation of YouTube Data API v3 service.
/// Handles channel search, information retrieval, and quota management.
/// </summary>
public class YouTubeApiService : IYouTubeApiService, IDisposable
{
    private readonly Google.Apis.YouTube.v3.YouTubeService _youTubeClient;
    private readonly YouTubeApiSettings _settings;
    private readonly ILogger<YouTubeApiService> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore;

    // Simple in-memory quota tracking for MVP
    private static int _dailyQuotaUsed = 0;
    private static DateTime _lastQuotaReset = DateTime.UtcNow.Date;

    // API cost constants (approximate)
    private const int SearchCost = 100;
    private const int ChannelDetailsCost = 1;

    public YouTubeApiService(IOptions<YouTubeApiSettings> settings, ILogger<YouTubeApiService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _rateLimitSemaphore = new SemaphoreSlim(1, 1);

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("YouTube API key is required but not configured.");
        }

        _youTubeClient = new Google.Apis.YouTube.v3.YouTubeService(new BaseClientService.Initializer()
        {
            ApiKey = _settings.ApiKey,
            ApplicationName = "YouTube Video Tracker"
        });

        ResetQuotaIfNeeded();
    }

    /// <summary>
    /// Checks if the YouTube API is currently available and within quota limits.
    /// </summary>
    public async Task<bool> IsApiAvailableAsync()
    {
        try
        {
            ResetQuotaIfNeeded();
            return await CheckQuotaAvailableAsync(1);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the estimated remaining API quota for today.
    /// </summary>
    public async Task<int> GetEstimatedRemainingQuotaAsync()
    {
        ResetQuotaIfNeeded();
        return Math.Max(0, _settings.DailyQuotaLimit - _dailyQuotaUsed);
    }

    /// <summary>
    /// Checks if sufficient quota is available for an operation.
    /// </summary>
    private async Task<bool> CheckQuotaAvailableAsync(int requiredQuota)
    {
        ResetQuotaIfNeeded();
        return _dailyQuotaUsed + requiredQuota <= _settings.DailyQuotaLimit;
    }

    /// <summary>
    /// Resets quota tracking if a new day has started.
    /// </summary>
    private void ResetQuotaIfNeeded()
    {
        var today = DateTime.UtcNow.Date;
        if (_lastQuotaReset < today)
        {
            _dailyQuotaUsed = 0;
            _lastQuotaReset = today;
            _logger.LogInformation("YouTube API quota reset for new day");
        }
    }

    public void Dispose()
    {
        _youTubeClient?.Dispose();
        _rateLimitSemaphore?.Dispose();
    }


}