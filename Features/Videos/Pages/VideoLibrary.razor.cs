using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Data.Entities;
using TargetBrowse.Features.Videos.Models;
using TargetBrowse.Features.Videos.Services;
using TargetBrowse.Services.Interfaces;

namespace TargetBrowse.Features.Videos.Pages;

public partial class VideoLibrary : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IVideoService VideoService { get; set; } = default!;

    [Inject]
    private IVideoRatingService VideoRatingService { get; set; } = default!;

    [Inject]
    private IMessageCenterService MessageCenter { get; set; } = default!;

    private List<VideoDisplayModel> Videos = new();
    private List<VideoDisplayModel> FilteredVideos = new();
    private bool IsLoading = false;
    private bool IsGridView = true;
    private string LibrarySearchQuery = string.Empty;
    private string CurrentSortOption = "Date Added";
    private string CurrentRatingFilter = "All Videos";
    private string? CurrentUserId;
    private string? LastError;
    private bool ShowDebugInfo = false;

    // Rating statistics properties
    private int RatedVideosCount => Videos.Count(v => v.IsRatedByUser);
    private double AverageUserRating => RatedVideosCount > 0 ? Videos.Where(v => v.IsRatedByUser).Average(v => v.UserStars) : 0;

    // Statistics properties
    private string TotalDurationDisplay => CalculateTotalDuration();
    private int VideosThisWeek => CalculateVideosThisWeek();
    private int VideosThisMonth => CalculateVideosThisMonth();
    private int NotWatchedCount => Videos.Count(v => v.WatchStatus == WatchStatus.NotWatched);
    private int WatchedCount => Videos.Count(v => v.WatchStatus == WatchStatus.Watched);
    private int SkippedCount => Videos.Count(v => v.WatchStatus == WatchStatus.Skipped);
    private double NotWatchedPercentage => Videos.Any() ? (double)NotWatchedCount / Videos.Count * 100 : 0;
    private double WatchedPercentage => Videos.Any() ? (double)WatchedCount / Videos.Count * 100 : 0;
    private double SkippedPercentage => Videos.Any() ? (double)SkippedCount / Videos.Count * 100 : 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadLibraryAsync();
    }

    /// <summary>
    /// Loads the user's video library from the service.
    /// FIXED: Removed mock rating data loading - now uses real database data.
    /// </summary>
    private async Task LoadLibraryAsync()
    {
        try
        {
            IsLoading = true;
            LastError = null;
            StateHasChanged();

            // Get current user using the same pattern as the working components
            var authState = await AuthenticationStateTask!;
            CurrentUserId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(CurrentUserId))
            {
                LastError = "User not authenticated or user ID not available.";
                return;
            }

            // Load videos from database - this should now include rating information
            Videos = await VideoService.GetUserLibraryAsync(CurrentUserId);

            // REMOVED: LoadMockRatingData() call - we now use real data from the database

            ApplyFiltersAndSort();

            if (!Videos.Any())
            {
                // This is not an error, just an empty library
                await MessageCenter.ShowInfoAsync("Your video library is empty. Start by searching for videos to add!");
            }
        }
        catch (Exception ex)
        {
            LastError = $"Failed to load video library: {ex.Message}";
            Videos.Clear();
            FilteredVideos.Clear();
            await MessageCenter.ShowErrorAsync(LastError);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Refreshes the library data.
    /// </summary>
    public async Task RefreshAsync()
    {
        await LoadLibraryAsync();
    }

    /// <summary>
    /// Toggles debug information display.
    /// </summary>
    private void ToggleDebugInfo()
    {
        ShowDebugInfo = !ShowDebugInfo;
        StateHasChanged();
    }

    /// <summary>
    /// Handles library search input with debouncing.
    /// </summary>
    private async Task OnLibrarySearchInput(ChangeEventArgs e)
    {
        LibrarySearchQuery = e.Value?.ToString() ?? string.Empty;
        ApplyFiltersAndSort();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Clears the library search.
    /// </summary>
    private void ClearLibrarySearch()
    {
        LibrarySearchQuery = string.Empty;
        ApplyFiltersAndSort();
    }

    /// <summary>
    /// Sets the rating filter.
    /// </summary>
    private void SetRatingFilter(string filter)
    {
        CurrentRatingFilter = filter;
        ApplyFiltersAndSort();
    }

    /// <summary>
    /// Sorts videos by the specified criteria.
    /// </summary>
    private void SortVideos(string sortOption)
    {
        CurrentSortOption = sortOption;
        ApplyFiltersAndSort();
    }

    /// <summary>
    /// Sort methods for dropdown menu items.
    /// </summary>
    private void SortByDateAdded() => SortVideos("Date Added");
    private void SortByTitle() => SortVideos("Title");
    private void SortByDuration() => SortVideos("Duration");
    private void SortByViews() => SortVideos("Views");
    private void SortByPublished() => SortVideos("Published");
    private void SortByRating() => SortVideos("Rating (High to Low)");
    private void SortByRatingLowToHigh() => SortVideos("Rating (Low to High)");

    /// <summary>
    /// Applies current filters and sorting to the video list.
    /// </summary>
    private void ApplyFiltersAndSort()
    {
        var query = Videos.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(LibrarySearchQuery))
        {
            var searchLower = LibrarySearchQuery.ToLowerInvariant();
            query = query.Where(v =>
                v.Title.ToLowerInvariant().Contains(searchLower) ||
                v.ChannelTitle.ToLowerInvariant().Contains(searchLower) ||
                v.Description.ToLowerInvariant().Contains(searchLower) ||
                (v.UserRating?.Notes.ToLowerInvariant().Contains(searchLower) ?? false));
        }

        // Apply rating filter
        query = CurrentRatingFilter switch
        {
            "Rated" => query.Where(v => v.IsRatedByUser),
            "Not Rated" => query.Where(v => !v.IsRatedByUser),
            "5 Stars" => query.Where(v => v.UserStars == 5),
            "4+ Stars" => query.Where(v => v.UserStars >= 4),
            "3+ Stars" => query.Where(v => v.UserStars >= 3),
            "1-2 Stars" => query.Where(v => v.UserStars >= 1 && v.UserStars <= 2),
            _ => query // "All Videos"
        };

        // Apply sorting
        query = CurrentSortOption switch
        {
            "Title" => query.OrderBy(v => v.Title),
            "Duration" => query.OrderByDescending(v => GetDurationInMinutes(v.Duration)),
            "Views" => query.OrderByDescending(v => v.ViewCount ?? 0),
            "Published" => query.OrderByDescending(v => v.PublishedAt),
            "Rating (High to Low)" => query.OrderByDescending(v => v.UserStars).ThenByDescending(v => v.AddedToLibrary),
            "Rating (Low to High)" => query.OrderBy(v => v.UserStars == 0 ? int.MaxValue : v.UserStars).ThenByDescending(v => v.AddedToLibrary),
            _ => query.OrderByDescending(v => v.AddedToLibrary ?? DateTime.MinValue) // Date Added
        };

        FilteredVideos = query.ToList();
        StateHasChanged();
    }

    /// <summary>
    /// Handles when a video is removed from the library.
    /// </summary>
    private async Task HandleVideoRemoved(VideoDisplayModel video)
    {
        Videos.RemoveAll(v => v.Id == video.Id);
        ApplyFiltersAndSort();
        await MessageCenter.ShowSuccessAsync($"Removed '{video.ShortTitle}' from your library.");
    }

    /// <summary>
    /// Handles when a video's watch status is changed.
    /// </summary>
    private async Task HandleWatchStatusChanged((VideoDisplayModel Video, WatchStatus Status) data)
    {
        // Update the video in our local list
        var video = Videos.FirstOrDefault(v => v.Id == data.Video.Id);
        if (video != null)
        {
            video.WatchStatus = data.Status;
        }

        // Apply filters and sort to update the display
        ApplyFiltersAndSort();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Handles when a video is rated or rating is updated.
    /// </summary>
    private async Task HandleVideoRated((VideoDisplayModel Video, VideoRatingModel? Rating) data)
    {
        // Update the video in our local list
        var video = Videos.FirstOrDefault(v => v.Id == data.Video.Id);
        if (video != null)
        {
            video.UserRating = data.Rating;
        }

        // Apply filters and sort to update the display
        ApplyFiltersAndSort();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the count of videos with a specific star rating.
    /// </summary>
    private int GetRatingCount(int stars)
    {
        return Videos.Count(v => v.UserStars == stars);
    }

    /// <summary>
    /// Gets the CSS class for star rating badges.
    /// </summary>
    private string GetStarBadgeClass(int stars)
    {
        return stars switch
        {
            5 => "bg-success",
            4 => "bg-info",
            3 => "bg-warning",
            2 => "bg-danger",
            1 => "bg-dark",
            _ => "bg-secondary"
        };
    }

    /// <summary>
    /// Calculates total duration display string.
    /// </summary>
    private string CalculateTotalDuration()
    {
        var totalMinutes = 0;

        foreach (var video in Videos)
        {
            totalMinutes += GetDurationInMinutes(video.Duration);
        }

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        if (hours > 0)
            return $"{hours}h {minutes}m";
        else
            return $"{minutes}m";
    }

    /// <summary>
    /// Helper method to convert ISO 8601 duration to minutes.
    /// </summary>
    private static int GetDurationInMinutes(string? duration)
    {
        if (string.IsNullOrEmpty(duration))
            return 0;

        try
        {
            var timespan = System.Xml.XmlConvert.ToTimeSpan(duration);
            return (int)timespan.TotalMinutes;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Calculates videos added this week.
    /// </summary>
    private int CalculateVideosThisWeek()
    {
        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
        return Videos.Count(v => v.AddedToLibrary >= oneWeekAgo);
    }

    /// <summary>
    /// Calculates videos added this month.
    /// </summary>
    private int CalculateVideosThisMonth()
    {
        var oneMonthAgo = DateTime.UtcNow.AddDays(-30);
        return Videos.Count(v => v.AddedToLibrary >= oneMonthAgo);
    }
}