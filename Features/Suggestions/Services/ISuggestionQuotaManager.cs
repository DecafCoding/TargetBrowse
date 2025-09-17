using Microsoft.Extensions.Options;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.YouTube.Models;

namespace TargetBrowse.Features.Suggestions.Services;

/// <summary>
/// Centralized quota management service for YouTube API operations.
/// Tracks usage, enforces limits, and provides intelligent quota planning.
/// Fixed to resolve service lifetime dependency issues.
/// </summary>
public interface ISuggestionQuotaManager
{
    /// <summary>
    /// Checks if sufficient quota is available for an operation.
    /// </summary>
    Task<bool> IsQuotaAvailableAsync(int requiredQuota);

    /// <summary>
    /// Records quota usage for an operation.
    /// </summary>
    Task RecordQuotaUsageAsync(int quotaUsed, string operationType, bool isSuccessful = true);

    /// <summary>
    /// Gets current quota status information.
    /// </summary>
    Task<QuotaStatus> GetQuotaStatusAsync();

    /// <summary>
    /// Estimates quota cost for a specific operation.
    /// </summary>
    Task<int> EstimateOperationCostAsync(string operationType, int itemCount = 1);

    /// <summary>
    /// Gets detailed quota cost estimate for suggestion generation.
    /// </summary>
    Task<QuotaCostEstimate> EstimateSuggestionCostAsync(int channelCount, int topicCount, int estimatedVideos = 100);

    /// <summary>
    /// Resets quota tracking for a new day.
    /// </summary>
    Task ResetDailyQuotaAsync();

    /// <summary>
    /// Gets API availability status with comprehensive information.
    /// </summary>
    Task<ApiAvailabilityResult> GetApiAvailabilityAsync();

    /// <summary>
    /// Records an API call for detailed tracking and analytics.
    /// </summary>
    Task RecordApiCallAsync(string operationType, int quotaCost, TimeSpan duration, bool isSuccessful, string? errorMessage = null, int? itemsReturned = null);

    /// <summary>
    /// Gets usage statistics for analytics and monitoring.
    /// </summary>
    Task<SuggestionApiUsage> GetUsageStatisticsAsync();

    /// <summary>
    /// Checks if quota reset is needed and performs it if necessary.
    /// </summary>
    Task CheckAndPerformQuotaResetAsync();
}