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
    private bool isLoading = true;
    private bool showEditModal = false;

    // Selected rating for editing
    private Guid selectedRatingId = Guid.Empty;
    private string selectedVideoTitle = "";
    private int selectedStars = 0;
    private string selectedNotes = "";

    // Pagination
    private int currentPage = 1;
    private int pageSize = 20;
    private int totalRatings = 0;
    private int totalPages => totalRatings > 0 ? (int)Math.Ceiling(totalRatings / (double)pageSize) : 1;

    // Filters
    private string searchQuery = string.Empty;
    private string filterStars = string.Empty;
    private string sortOrder = "newest";

    // Filter/Sort labels
    private string currentFilterLabel = "All Ratings";
    private string currentSortLabel = "Newest First";

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

    private void OnSearchInput(ChangeEventArgs e)
    {
        searchQuery = e.Value?.ToString() ?? string.Empty;

        // Debounce search
        searchDebounceTimer?.Stop();
        searchDebounceTimer?.Dispose();
        searchDebounceTimer = new System.Timers.Timer(300);
        searchDebounceTimer.Elapsed += async (s, evt) =>
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

    private async Task ClearSearch()
    {
        searchQuery = string.Empty;
        currentPage = 1;
        await LoadRatings();
    }

    private async Task SetRatingFilter(string filter, string label)
    {
        filterStars = filter;
        currentFilterLabel = label;
        currentPage = 1;
        await LoadRatings();
    }

    private async Task SetSort(string sort, string label)
    {
        sortOrder = sort;
        currentSortLabel = label;
        await LoadRatings();
    }

    private async Task SetPageSize(int size)
    {
        pageSize = size;
        currentPage = 1;
        await LoadRatings();
    }

    private async Task ChangePage(int newPage)
    {
        if (newPage < 1 || newPage > totalPages) return;
        currentPage = newPage;
        await LoadRatings();
    }

    private async Task ClearFilters()
    {
        searchQuery = string.Empty;
        filterStars = string.Empty;
        currentFilterLabel = "All Ratings";
        currentPage = 1;
        await LoadRatings();
    }

    private void EditRating(VideoRatingModel rating)
    {
        selectedRatingId = rating.Id;
        selectedVideoTitle = rating.VideoTitle;
        selectedStars = rating.Stars;
        selectedNotes = rating.Notes;
        showEditModal = true;
        StateHasChanged();
    }

    private async Task HandleRatingSubmit((Guid RatingId, int Stars, string Notes) data)
    {
        if (string.IsNullOrWhiteSpace(currentUserId)) return;

        try
        {
            var ratingModel = new RateVideoModel
            {
                RatingId = data.RatingId,
                VideoId = Guid.Empty, // Will be retrieved from existing rating
                YouTubeVideoId = "", // Will be retrieved from existing rating
                VideoTitle = selectedVideoTitle,
                Stars = data.Stars,
                Notes = data.Notes
            };

            await VideoRatingService.UpdateRatingAsync(currentUserId, data.RatingId, ratingModel);
            await MessageCenter.ShowSuccessAsync("Rating updated successfully!");

            // Close modal and refresh data
            showEditModal = false;
            await LoadStats();
            await LoadRatings();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error submitting rating");
            await MessageCenter.ShowErrorAsync("Error saving rating. Please try again.");
            // Don't close modal on error so user can retry
        }
    }

    private async Task HandleRatingDelete(Guid ratingId)
    {
        if (string.IsNullOrWhiteSpace(currentUserId)) return;

        try
        {
            var success = await VideoRatingService.DeleteRatingAsync(currentUserId, ratingId);

            if (success)
            {
                await MessageCenter.ShowSuccessAsync("Rating deleted successfully!");

                // Close modal and refresh data
                showEditModal = false;
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

    private string GetCurrentFilterDisplay()
    {
        if (string.IsNullOrEmpty(filterStars))
        {
            return $"All Ratings ({stats?.TotalRatings ?? 0})";
        }

        return currentFilterLabel;
    }

    private string GetFilterLabel(int stars)
    {
        // stars parameter: 5 = 5 stars, 4 = 4 stars, 3 = 3 stars, 12 = 1-2 stars
        return stars switch
        {
            5 => "5 Stars",
            4 => "4 Stars",
            3 => "3 Stars",
            12 => "1-2 Stars",
            _ => "All Ratings"
        };
    }

    public void Dispose()
    {
        searchDebounceTimer?.Dispose();
    }
}
