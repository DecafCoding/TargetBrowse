using TargetBrowse.Services.Models;

namespace TargetBrowse.Services.Interfaces;

/// <summary>
/// Service interface for categorizing videos in a user's library using AI classification.
/// Handles the workflow of querying uncategorized videos, calling AI classification,
/// and updating video records with their assigned VideoType.
/// </summary>
public interface ILibraryVideoClassificationService
{
    /// <summary>
    /// Categorizes all uncategorized videos in a user's library.
    /// Processes videos in batches, logs AI calls, and updates video records.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="progressCallback">Optional callback for progress updates</param>
    /// <returns>Result containing success count and statistics</returns>
    Task<CategorizationResult> CategorizeUserLibraryVideosAsync(
        string userId,
        Action<string>? progressCallback = null);

    /// <summary>
    /// Gets count of uncategorized videos in user's library.
    /// Used to determine if categorization button should be enabled.
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Number of videos without VideoTypeId</returns>
    Task<int> GetUncategorizedVideoCountAsync(string userId);
}