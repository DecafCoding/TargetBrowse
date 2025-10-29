using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data;
using TargetBrowse.Data.Entities;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Services.DataServices
{
    /// <summary>
    /// Data access service implementation for AI API call logging.
    /// Handles storage of all AI interactions for auditing and cost tracking.
    /// </summary>
    public class AICallDataService : IAICallDataService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AICallDataService> _logger;

        public AICallDataService(ApplicationDbContext context, ILogger<AICallDataService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new AI call record in the database.
        /// Logs complete request/response details for auditing and cost tracking.
        /// </summary>
        public async Task<AICallEntity> CreateAICallAsync(
            Guid promptId,
            string? userId,
            string actualSystemPrompt,
            string actualUserPrompt,
            string response,
            int inputTokens,
            int outputTokens,
            decimal totalCost,
            int? durationMs,
            bool success,
            string? errorMessage)
        {
            try
            {
                var aiCall = new AICallEntity
                {
                    PromptId = promptId,
                    UserId = userId,
                    ActualSystemPrompt = actualSystemPrompt,
                    ActualUserPrompt = actualUserPrompt,
                    Response = response,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    TotalCost = totalCost,
                    DurationMs = durationMs,
                    Success = success,
                    ErrorMessage = errorMessage
                };

                _context.AICalls.Add(aiCall);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Created AI call record {CallId} for prompt {PromptId}. Success: {Success}, Tokens: {InputTokens}→{OutputTokens}, Cost: ${Cost:F4}",
                    aiCall.Id,
                    promptId,
                    success,
                    inputTokens,
                    outputTokens,
                    totalCost);

                return aiCall;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error creating AI call record for prompt {PromptId}: {Message}",
                    promptId,
                    ex.Message);
                throw;
            }
        }
    }
}