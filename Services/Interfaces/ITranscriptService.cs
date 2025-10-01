using TargetBrowse.Services.Models;

namespace TargetBrowse.Services.Interfaces;

public interface ITranscriptService
{
    Task<TranscriptResult> ScrapeVideoAsync(string videoUrl);
}
