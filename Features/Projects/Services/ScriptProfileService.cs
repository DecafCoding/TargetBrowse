using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Projects.Models;

namespace TargetBrowse.Features.Projects.Services
{
    /// <summary>
    /// Service for managing user script profile preferences.
    /// Handles CRUD operations for user script generation settings.
    /// </summary>
    public class ScriptProfileService : IScriptProfileService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ScriptProfileService> _logger;

        public ScriptProfileService(
            ApplicationDbContext context,
            ILogger<ScriptProfileService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the script profile for a specific user.
        /// </summary>
        public async Task<UserScriptProfileEntity?> GetUserProfileAsync(string userId)
        {
            try
            {
                return await _context.UserScriptProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving script profile for user {userId}");
                throw;
            }
        }

        /// <summary>
        /// Checks if a user has a script profile configured.
        /// </summary>
        public async Task<bool> HasProfileAsync(string userId)
        {
            try
            {
                return await _context.UserScriptProfiles
                    .AnyAsync(p => p.UserId == userId && !p.IsDeleted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking profile existence for user {userId}");
                throw;
            }
        }

        /// <summary>
        /// Creates or updates a user's script profile.
        /// </summary>
        public async Task<UserScriptProfileEntity> CreateOrUpdateProfileAsync(
            string userId,
            UserScriptProfileModel model)
        {
            try
            {
                var existingProfile = await _context.UserScriptProfiles
                    .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted);

                if (existingProfile != null)
                {
                    // Update existing profile
                    existingProfile.Tone = model.Tone;
                    existingProfile.Pacing = model.Pacing;
                    existingProfile.Complexity = model.Complexity;
                    existingProfile.StructureStyle = model.StructureStyle;
                    existingProfile.HookStrategy = model.HookStrategy;
                    existingProfile.AudienceRelationship = model.AudienceRelationship;
                    existingProfile.InformationDensity = model.InformationDensity;
                    existingProfile.RhetoricalStyle = model.RhetoricalStyle;
                    existingProfile.CustomInstructions = model.CustomInstructions;

                    _logger.LogInformation($"Updating script profile for user {userId}");
                }
                else
                {
                    // Create new profile
                    existingProfile = new UserScriptProfileEntity
                    {
                        UserId = userId,
                        Tone = model.Tone,
                        Pacing = model.Pacing,
                        Complexity = model.Complexity,
                        StructureStyle = model.StructureStyle,
                        HookStrategy = model.HookStrategy,
                        AudienceRelationship = model.AudienceRelationship,
                        InformationDensity = model.InformationDensity,
                        RhetoricalStyle = model.RhetoricalStyle,
                        CustomInstructions = model.CustomInstructions
                    };

                    _context.UserScriptProfiles.Add(existingProfile);
                    _logger.LogInformation($"Creating new script profile for user {userId}");
                }

                await _context.SaveChangesAsync();
                return existingProfile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating/updating script profile for user {userId}");
                throw;
            }
        }

        /// <summary>
        /// Gets a user's profile or returns default values if none exists.
        /// </summary>
        public async Task<UserScriptProfileEntity> GetProfileOrDefaultAsync(string userId)
        {
            try
            {
                var profile = await GetUserProfileAsync(userId);

                if (profile != null)
                {
                    return profile;
                }

                // Return default profile (not saved to database)
                _logger.LogInformation($"No profile found for user {userId}, returning default values");
                return new UserScriptProfileEntity
                {
                    UserId = userId,
                    Tone = "Insider Conversational",
                    Pacing = "Build-Release",
                    Complexity = "Layered Progressive",
                    StructureStyle = "Enumerated Scaffolding",
                    HookStrategy = "Insider Secret",
                    AudienceRelationship = "Insider-Outsider",
                    InformationDensity = "High Density",
                    RhetoricalStyle = "Extended Metaphors",
                    CustomInstructions = null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving profile or default for user {userId}");
                throw;
            }
        }
    }
}
