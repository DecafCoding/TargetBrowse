using TargetBrowse.Features.Watch.Data;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Watch.Services;

/// <summary>
/// Service implementation for retrieving and storing YouTube video transcripts.
/// Coordinates between TranscriptService (Apify) and WatchRepository (database persistence).
/// </summary>
public class TranscriptRetrievalService : ITranscriptRetrievalService
{
    private readonly ITranscriptService _transcriptService;
    private readonly IWatchRepository _watchRepository;
    private readonly IMessageCenterService _messageCenter;
    private readonly ILogger<TranscriptRetrievalService> _logger;

    // Track in-progress retrievals to prevent duplicate API calls
    private static readonly HashSet<string> _inProgressRetrievals = new();
    private static readonly object _lockObject = new();

    public TranscriptRetrievalService(
        ITranscriptService transcriptService,
        IWatchRepository watchRepository,
        IMessageCenterService messageCenter,
        ILogger<TranscriptRetrievalService> logger)
    {
        _transcriptService = transcriptService;
        _watchRepository = watchRepository;
        _messageCenter = messageCenter;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> RetrieveAndStoreTranscriptAsync(string youTubeVideoId)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(youTubeVideoId))
        {
            _logger.LogWarning("Transcript retrieval attempted with empty video ID");
            await _messageCenter.ShowErrorAsync("Invalid video ID provided.");
            return false;
        }

        // Check if already in progress
        lock (_lockObject)
        {
            if (_inProgressRetrievals.Contains(youTubeVideoId))
            {
                _logger.LogInformation("Transcript retrieval already in progress for {YouTubeVideoId}", youTubeVideoId);
                return false;
            }
            _inProgressRetrievals.Add(youTubeVideoId);
        }

        try
        {
            _logger.LogInformation("Starting transcript retrieval for video {YouTubeVideoId}", youTubeVideoId);

            // Get video from database to ensure it exists
            var video = await _watchRepository.GetVideoByYouTubeIdAsync(youTubeVideoId);
            if (video == null)
            {
                _logger.LogWarning("Video {YouTubeVideoId} not found in database", youTubeVideoId);
                await _messageCenter.ShowErrorAsync("Video not found. Please add it to your library first.");
                return false;
            }

            // Check if transcript already exists
            if (await _watchRepository.HasTranscriptAsync(video.Id))
            {
                _logger.LogInformation("Transcript already exists for video {YouTubeVideoId}", youTubeVideoId);
                await _messageCenter.ShowSuccessAsync("Transcript already available for this video.");
                return true;
            }

            // Build YouTube URL for Apify service
            var videoUrl = $"https://www.youtube.com/watch?v={youTubeVideoId}";

            // Call Apify service to scrape transcript (this takes 8-20 seconds)
            var transcriptResult = await _transcriptService.ScrapeVideoAsync(videoUrl);

            if (transcriptResult == null || string.IsNullOrWhiteSpace(transcriptResult.Subtitles))
            {
                _logger.LogWarning("No transcript available for video {YouTubeVideoId}", youTubeVideoId);
                await _messageCenter.ShowErrorAsync("No transcript available for this video. The video may not have captions.");
                return false;
            }

            // Store transcript in database
            var success = await _watchRepository.UpdateVideoTranscriptAsync(video.Id, transcriptResult.Subtitles);

            if (success)
            {
                _logger.LogInformation("Successfully retrieved and stored transcript for video {YouTubeVideoId}", youTubeVideoId);
                await _messageCenter.ShowSuccessAsync("Transcript successfully retrieved!");
                return true;
            }
            else
            {
                _logger.LogError("Failed to store transcript for video {YouTubeVideoId}", youTubeVideoId);
                await _messageCenter.ShowErrorAsync("Failed to save transcript. Please try again.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transcript for video {YouTubeVideoId}", youTubeVideoId);
            await _messageCenter.ShowErrorAsync("An error occurred while retrieving the transcript. Please try again later.");
            return false;
        }
        finally
        {
            // Remove from in-progress tracking
            lock (_lockObject)
            {
                _inProgressRetrievals.Remove(youTubeVideoId);
            }
        }
    }

    /// <inheritdoc/>
    public bool IsRetrievalInProgress(string youTubeVideoId)
    {
        lock (_lockObject)
        {
            return _inProgressRetrievals.Contains(youTubeVideoId);
        }
    }
}