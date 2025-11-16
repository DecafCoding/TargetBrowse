using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace TargetBrowse.Services.Utilities;

/// <summary>
/// Helper utility for extracting user authentication information from Blazor components.
/// Provides a single, consistent way to get the current user ID across all features.
/// </summary>
public static class AuthenticationHelper
{
    /// <summary>
    /// Extracts the current authenticated user's ID from the authentication state.
    /// </summary>
    /// <param name="authenticationStateTask">The cascading authentication state parameter from the component</param>
    /// <param name="logger">Optional logger for error logging</param>
    /// <returns>The current user's ID, or null if not authenticated or an error occurs</returns>
    public static async Task<string?> GetCurrentUserIdAsync(
        Task<AuthenticationState>? authenticationStateTask,
        ILogger? logger = null)
    {
        try
        {
            if (authenticationStateTask == null)
            {
                logger?.LogWarning("AuthenticationStateTask is null");
                return null;
            }

            var authState = await authenticationStateTask;
            return authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to get current user ID from authentication state");
            return null;
        }
    }
}
