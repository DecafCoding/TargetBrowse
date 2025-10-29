using TargetBrowse.Data.Entities;

namespace TargetBrowse.Services.Interfaces
{
    /// <summary>
    /// Data access service for AI API call logging operations.
    /// Tracks all AI API interactions for auditing and cost monitoring.
    /// </summary>
    public interface IAICallDataService
    {
        /// <summary>
        /// Creates a new AI call record in the database.
        /// Logs complete request/response details for auditing and cost tracking.
        /// </summary>
        /// <param name="promptId">The prompt template ID used for this call</param>
        /// <param name="userId">Optional user who triggered the call (null for system calls)</param>
        /// <param name="actualSystemPrompt">The complete system prompt sent to the API</param>
        /// <param name="actualUserPrompt">The user prompt after placeholder replacement</param>
        /// <param name="response">The raw response from the AI API</param>
        /// <param name="inputTokens">Number of tokens in the request</param>
        /// <param name="outputTokens">Number of tokens in the response</param>
        /// <param name="totalCost">Calculated cost for this API call</param>
        /// <param name="durationMs">Duration of the API call in milliseconds (optional)</param>
        /// <param name="success">Whether the API call succeeded</param>
        /// <param name="errorMessage">Error details if the call failed (optional)</param>
        /// <returns>The created AICallEntity with populated Id</returns>
        Task<AICallEntity> CreateAICallAsync(
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
            string? errorMessage);
    }
}