using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Projects.Models;

namespace TargetBrowse.Features.Projects.Services
{
    /// <summary>
    /// Service interface for managing user script profile preferences.
    /// Handles creation, retrieval, and updates of user script generation settings.
    /// </summary>
    public interface IScriptProfileService
    {
        /// <summary>
        /// Gets the script profile for a specific user.
        /// Returns null if user has no profile yet.
        /// </summary>
        /// <param name="userId">User ID to retrieve profile for</param>
        /// <returns>User's script profile entity or null</returns>
        Task<UserScriptProfileEntity?> GetUserProfileAsync(string userId);

        /// <summary>
        /// Checks if a user has a script profile configured.
        /// </summary>
        /// <param name="userId">User ID to check</param>
        /// <returns>True if profile exists, false otherwise</returns>
        Task<bool> HasProfileAsync(string userId);

        /// <summary>
        /// Creates or updates a user's script profile.
        /// If profile exists, updates it. If not, creates a new one.
        /// </summary>
        /// <param name="userId">User ID to create/update profile for</param>
        /// <param name="model">Profile data from user input</param>
        /// <returns>Created or updated profile entity</returns>
        Task<UserScriptProfileEntity> CreateOrUpdateProfileAsync(string userId, UserScriptProfileModel model);

        /// <summary>
        /// Gets a user's profile or returns default values if none exists.
        /// Used when generating scripts to always have profile data available.
        /// </summary>
        /// <param name="userId">User ID to retrieve profile for</param>
        /// <returns>User's profile or default profile values</returns>
        Task<UserScriptProfileEntity> GetProfileOrDefaultAsync(string userId);
    }
}
