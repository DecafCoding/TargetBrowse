using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Utilities;

namespace TargetBrowse.Features.RatingHistory.Pages;

public partial class RatingHistory : ComponentBase, IDisposable
{
    [Inject] private IVideoRatingService VideoRatingService { get; set; } = default!;
    [Inject] private IMessageCenterService MessageCenter { get; set; } = default!;
    [Inject] private ILogger<RatingHistory> Logger { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    // State
    private List<VideoRatingModel> ratings = new();
    private UserRatingStats? stats;
    private VideoRatingModel? selectedRating;
    private RateVideoModel? currentRatingModel;
    private bool isLoading = true;
    private bool showEditModal = false;

    // Pagination
    private int currentPage = 1;
    private int pageSize = 20;
    private int totalRatings = 0;
    private int totalPages => totalRatings > 0 ? (int)Math.Ceiling(totalRatings / (double)pageSize) : 1;

    // Filters
    private string searchQuery = string.Empty;
    private string filterStars = string.Empty;
    private string sortOrder = "newest";

    // Debounce
    private System.Timers.Timer? searchDebounceTimer;
    private string? currentUserId;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            currentUserId = await AuthenticationHelper.GetCurrentUserIdAsync(AuthenticationStateTask, Logger);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                await MessageCenter.ShowErrorAsync("Please log in to view your rating history.");
                return;
            }

            await LoadStats();
            await LoadRatings();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initializing rating history page");
            await MessageCenter.ShowErrorAsync("Error loading rating history. Please refresh the page.");
        }
    }

    private async Task LoadStats()
    {
        if (string.IsNullOrWhiteSpace(currentUserId)) return;

        try
        {
            stats = await VideoRatingService.GetUserRatingStatsAsync(currentUserId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading rating statistics");
            // Don't show error to user, stats are not critical
        }
    }

    private async Task LoadRatings()
    {
        if (string.IsNullOrWhiteSpace(currentUserId)) return;

        isLoading = true;
        StateHasChanged();

        try
        {
            List<VideoRatingModel> allRatings;

            if (!string.IsNullOrWhiteSpace(searchQuery) || !string.IsNullOrEmpty(filterStars))
            {
                // Use search endpoint
                int? minStars = null;
                int? maxStars = null;

                if (!string.IsNullOrEmpty(filterStars))
                {
                    if (filterStars == "1-2")
                    {
                        minStars = 1;
                        maxStars = 2;
                    }
                    else
                    {
                        minStars = int.Parse(filterStars);
                        maxStars = 5;
                    }
                }

                allRatings = await VideoRatingService.SearchUserRatingsAsync(
                    currentUserId,
                    searchQuery ?? string.Empty,
                    minStars,
                    maxStars
                );

                totalRatings = allRatings.Count;
            }
            else
            {
                // Use standard pagination endpoint - get all for now since we need to sort
                allRatings = await VideoRatingService.GetUserRatingsAsync(currentUserId, 1, int.MaxValue);
                totalRatings = allRatings.Count;
            }

            // Apply sorting
            var sortedRatings = ApplySort(allRatings);

            // Apply pagination manually
            ratings = sortedRatings
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading ratings");
            await MessageCenter.ShowErrorAsync("Error loading ratings. Please try again.");
            ratings = new List<VideoRatingModel>();
            totalRatings = 0;
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private IEnumerable<VideoRatingModel> ApplySort(IEnumerable<VideoRatingModel> source)
    {
        return sortOrder switch
        {
            "newest" => source.OrderByDescending(r => r.CreatedAt),
            "oldest" => source.OrderBy(r => r.CreatedAt),
            "highest" => source.OrderByDescending(r => r.Stars).ThenByDescending(r => r.CreatedAt),
            "lowest" => source.OrderBy(r => r.Stars).ThenByDescending(r => r.CreatedAt),
            _ => source.OrderByDescending(r => r.CreatedAt)
        };
    }

    private async Task OnSearchChanged()
    {
        // Debounce search
        searchDebounceTimer?.Stop();
        searchDebounceTimer?.Dispose();
        searchDebounceTimer = new System.Timers.Timer(300);
        searchDebounceTimer.Elapsed += async (s, e) =>
        {
            await InvokeAsync(async () =>
            {
                currentPage = 1;
                await LoadRatings();
            });
        };
        searchDebounceTimer.AutoReset = false;
        searchDebounceTimer.Start();
    }

    private async Task OnFilterChanged()
    {
        currentPage = 1;
        await LoadRatings();
    }

    private async Task OnSortChanged()
    {
        await LoadRatings();
    }

    private async Task OnPageSizeChanged()
    {
        currentPage = 1;
        await LoadRatings();
    }

    private async Task ChangePage(int newPage)
    {
        if (newPage < 1 || newPage > totalPages) return;
        currentPage = newPage;
        await LoadRatings();
    }

    private void EditRating(VideoRatingModel rating)
    {
        selectedRating = rating;

        // Create RateVideoModel from VideoRatingModel for editing
        currentRatingModel = new RateVideoModel
        {
            RatingId = rating.Id,
            VideoId = rating.VideoId,
            YouTubeVideoId = rating.YouTubeVideoId,
            VideoTitle = rating.VideoTitle,
            Stars = rating.Stars,
            Notes = rating.Notes
        };

        showEditModal = true;
        StateHasChanged();
    }

    private async Task HandleRatingSubmit(RateVideoModel ratingModel)
    {
        if (string.IsNullOrWhiteSpace(currentUserId) || ratingModel == null) return;

        try
        {
            if (ratingModel.IsEditing && ratingModel.RatingId.HasValue)
            {
                // Update existing rating
                await VideoRatingService.UpdateRatingAsync(currentUserId, ratingModel.RatingId.Value, ratingModel);
                await MessageCenter.ShowSuccessAsync("Rating updated successfully!");
            }
            else
            {
                // Create new rating (shouldn't happen in this context, but handle it anyway)
                await VideoRatingService.CreateRatingAsync(currentUserId, ratingModel);
                await MessageCenter.ShowSuccessAsync("Rating created successfully!");
            }

            // Close modal and refresh data
            showEditModal = false;
            selectedRating = null;
            currentRatingModel = null;

            await LoadStats();
            await LoadRatings();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error submitting rating");
            await MessageCenter.ShowErrorAsync("Error saving rating. Please try again.");
        }
    }

    private async Task HandleRatingDelete(RateVideoModel ratingModel)
    {
        if (string.IsNullOrWhiteSpace(currentUserId) || ratingModel == null || !ratingModel.RatingId.HasValue) return;

        try
        {
            var success = await VideoRatingService.DeleteRatingAsync(currentUserId, ratingModel.RatingId.Value);

            if (success)
            {
                await MessageCenter.ShowSuccessAsync("Rating deleted successfully!");

                // Close modal and refresh data
                showEditModal = false;
                selectedRating = null;
                currentRatingModel = null;

                await LoadStats();
                await LoadRatings();
            }
            else
            {
                await MessageCenter.ShowErrorAsync("Failed to delete rating. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting rating");
            await MessageCenter.ShowErrorAsync("Error deleting rating. Please try again.");
        }
    }

    private async Task OnModalClose()
    {
        showEditModal = false;
        selectedRating = null;
        currentRatingModel = null;
        StateHasChanged();
    }

    // Helper methods
    private string GetThumbnailUrl(string youTubeVideoId)
        => $"https://img.youtube.com/vi/{youTubeVideoId}/mqdefault.jpg";

    private int GetStartRecord()
        => ratings.Count == 0 ? 0 : (currentPage - 1) * pageSize + 1;

    private int GetEndRecord()
        => Math.Min(currentPage * pageSize, totalRatings);

    private IEnumerable<int> GetVisiblePages()
    {
        // Show max 5 page numbers
        var start = Math.Max(1, currentPage - 2);
        var end = Math.Min(totalPages, currentPage + 2);

        // Adjust if we're near the beginning or end
        if (end - start < 4)
        {
            if (start == 1)
            {
                end = Math.Min(totalPages, start + 4);
            }
            else if (end == totalPages)
            {
                start = Math.Max(1, end - 4);
            }
        }

        return Enumerable.Range(start, end - start + 1);
    }

    private int GetStarCount(int stars)
    {
        if (stats?.RatingDistribution == null) return 0;
        return stats.RatingDistribution.TryGetValue(stars, out var count) ? count : 0;
    }

    public void Dispose()
    {
        searchDebounceTimer?.Dispose();
    }
}
