using TargetBrowse.Data.Entities;

namespace TargetBrowse.Services.Interfaces
{
    public interface IPromptDataService
    {
        Task<PromptEntity?> GetActivePromptByNameAsync(string promptName);
        Task<List<PromptEntity>> GetAllActivePromptsAsync();
    }
}
