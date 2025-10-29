using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Services.DataServices;

/// <summary>
/// Service for retrieving prompt configurations from the database
/// </summary>
public class PromptDataService : IPromptDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PromptDataService> _logger;

    public PromptDataService(ApplicationDbContext context, ILogger<PromptDataService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieves an active prompt by name from the database
    /// </summary>
    /// <param name="promptName">Name of the prompt to retrieve</param>
    /// <returns>PromptEntity if found and active, null otherwise</returns>
    public async Task<PromptEntity?> GetActivePromptByNameAsync(string promptName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(promptName))
            {
                _logger.LogWarning("Attempted to retrieve prompt with null or empty name");
                return null;
            }

            _logger.LogInformation($"Retrieving active prompt: {promptName}");

            var prompt = await _context.Prompts
                .AsNoTracking()
                .Include(p => p.Model)
                .FirstOrDefaultAsync(p => p.Name == promptName && p.IsActive);

            if (prompt == null)
            {
                _logger.LogWarning($"Active prompt not found: {promptName}");
            }
            else
            {
                _logger.LogInformation($"Successfully retrieved prompt: {promptName} (Model: {prompt.Model})");
            }

            return prompt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving prompt '{promptName}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all active prompts from the database
    /// </summary>
    /// <returns>List of active prompts</returns>
    public async Task<List<PromptEntity>> GetAllActivePromptsAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all active prompts");

            var prompts = await _context.Prompts
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            _logger.LogInformation($"Retrieved {prompts.Count} active prompts");

            return prompts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving all active prompts: {ex.Message}");
            throw;
        }
    }
}
