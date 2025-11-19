using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Services.Utilities;

/// <summary>
/// Static helper class for generating consistent CSS classes across the application.
/// Provides standardized CSS class formatting for ratings, statuses, and UI elements.
/// </summary>
public static class CssClassFormatter
{
    #region Star Rating CSS Classes

    /// <summary>
    /// Gets CSS class for star rating display (1-5 stars).
    /// Used for video and channel ratings.
    /// </summary>
    /// <param name="stars">Star rating from 1 to 5</param>
    /// <returns>Bootstrap text color class</returns>
    public static string GetStarRatingCssClass(int stars)
    {
        return stars switch
        {
            1 => "text-danger",      // Poor - Red
            2 => "text-warning",     // Fair - Yellow/Orange
            3 => "text-info",        // Good - Blue
            4 => "text-success",     // Very Good - Green
            5 => "text-success",     // Excellent - Green
            _ => "text-muted"        // No rating - Gray
        };
    }

    /// <summary>
    /// Gets display text for star rating (1-5 stars).
    /// </summary>
    /// <param name="stars">Star rating from 1 to 5</param>
    /// <returns>Human-readable rating text</returns>
    public static string GetStarRatingDisplayText(int stars)
    {
        return stars switch
        {
            1 => "1 star - Poor",
            2 => "2 stars - Fair",
            3 => "3 stars - Good",
            4 => "4 stars - Very Good",
            5 => "5 stars - Excellent",
            _ => "No rating"
        };
    }

    /// <summary>
    /// Gets CSS class for channel rating status display with low-rating emphasis.
    /// Used in channel display models.
    /// </summary>
    /// <param name="stars">Star rating from 1 to 5, or 0 for not rated</param>
    /// <param name="isLowRating">Whether this is a low (1-star) rating</param>
    /// <returns>Bootstrap text color class</returns>
    public static string GetChannelRatingCssClass(int stars, bool isLowRating)
    {
        if (stars == 0)
            return "text-muted";

        if (isLowRating)
            return "text-danger";

        return stars switch
        {
            >= 4 => "text-success",
            3 => "text-info",
            _ => "text-warning"
        };
    }

    #endregion

    #region Watch Status CSS Classes

    /// <summary>
    /// Gets CSS class for watch status badge.
    /// </summary>
    /// <param name="status">Watch status</param>
    /// <returns>Bootstrap badge background class</returns>
    public static string GetWatchStatusBadgeClass(WatchStatus status)
    {
        return status switch
        {
            WatchStatus.Watched => "bg-success",
            WatchStatus.Skipped => "bg-secondary",
            _ => "bg-primary"
        };
    }

    /// <summary>
    /// Gets icon class for watch status.
    /// </summary>
    /// <param name="status">Watch status</param>
    /// <returns>Bootstrap icon class</returns>
    public static string GetWatchStatusIcon(WatchStatus status)
    {
        return status switch
        {
            WatchStatus.Watched => "bi-check-circle-fill",
            WatchStatus.Skipped => "bi-skip-forward-fill",
            _ => "bi-circle"
        };
    }

    /// <summary>
    /// Gets display text for watch status.
    /// </summary>
    /// <param name="status">Watch status</param>
    /// <returns>Human-readable status text</returns>
    public static string GetWatchStatusText(WatchStatus status)
    {
        return status switch
        {
            WatchStatus.Watched => "Watched",
            WatchStatus.Skipped => "Skipped",
            _ => "Not Watched"
        };
    }

    #endregion

    #region Suggestion Status CSS Classes

    /// <summary>
    /// Gets CSS class for suggestion status badge.
    /// </summary>
    /// <param name="status">Suggestion status</param>
    /// <returns>Bootstrap badge class with background</returns>
    public static string GetSuggestionStatusBadgeClass(SuggestionStatus status)
    {
        return status switch
        {
            SuggestionStatus.Pending => "badge bg-warning",
            SuggestionStatus.Approved => "badge bg-success",
            SuggestionStatus.Denied => "badge bg-danger",
            SuggestionStatus.Expired => "badge bg-secondary",
            _ => "badge bg-light"
        };
    }

    /// <summary>
    /// Gets display text for suggestion status.
    /// </summary>
    /// <param name="status">Suggestion status</param>
    /// <returns>Human-readable status text</returns>
    public static string GetSuggestionStatusText(SuggestionStatus status)
    {
        return status switch
        {
            SuggestionStatus.Pending => "Pending Review",
            SuggestionStatus.Approved => "Approved",
            SuggestionStatus.Denied => "Denied",
            SuggestionStatus.Expired => "Expired",
            _ => "Unknown"
        };
    }

    #endregion

    #region Score-Based CSS Classes

    /// <summary>
    /// Gets CSS class based on a score value (0-100 or normalized 0-1).
    /// </summary>
    /// <param name="score">Score value</param>
    /// <param name="isNormalized">Whether score is normalized (0-1) or percentage (0-100)</param>
    /// <returns>Bootstrap text color class</returns>
    public static string GetScoreBasedCssClass(double score, bool isNormalized = false)
    {
        var normalizedScore = isNormalized ? score : score / 100.0;

        return normalizedScore switch
        {
            >= 0.8 => "text-success",
            >= 0.6 => "text-info",
            >= 0.4 => "text-warning",
            >= 0.2 => "text-danger",
            _ => "text-muted"
        };
    }

    #endregion
}
