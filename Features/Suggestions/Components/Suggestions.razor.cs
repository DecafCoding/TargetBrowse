using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using TargetBrowse.Features.Suggestions.Models;
using TargetBrowse.Features.Suggestions.Services;
using TargetBrowse.Services;

namespace TargetBrowse.Features.Suggestions.Components;

/// <summary>
/// Code-behind for the main Suggestions page component.
/// Handles suggestion generation, statistics, and user interactions.
/// </summary>
public partial class SuggestionsBase : ComponentBase
{
    #region Injected Services

    [Inject] protected ISuggestionService SuggestionService { get; set; } = null!;
    [Inject] protected IMessageCenterService MessageCenter { get; set; } = null!;
    [Inject] protected ILogger<Suggestions> Logger { get; set; } = null!;

    #endregion

    #region Parameters and Cascading Parameters

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    #endregion

    #region Protected Properties - UI State

    protected SuggestionQueue? SuggestionQueueComponent;
    protected bool IsGenerating { get; set; } = false;
    protected string? GenerationStatus { get; set; }
    protected string? CurrentUserId;

    // Quick Stats
    protected bool ShowQuickStats { get; set; } = true;
    protected int TrackedChannelsCount { get; set; } = 0;
    protected int TopicsCount { get; set; } = 0;
    protected int PendingSuggestionsCount { get; set; } = 0;
    protected int LastGeneratedDaysAgo { get; set; } = -1;

    // Generation History
    protected List<SuggestionGeneration> RecentGenerations { get; set; } = new();

    #endregion

    #region Lifecycle Methods

    protected override async Task OnInitializedAsync()
    {
        await GetCurrentUserIdAsync();
        await LoadQuickStatsAsync();
        await LoadRecentGenerationsAsync();
    }

    #endregion

    #region Private Methods - Initialization

    private async Task GetCurrentUserIdAsync()
    {
        try
        {
            var authState = await AuthenticationStateTask!;
            CurrentUserId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get current user ID");
            CurrentUserId = null;
        }
    }

    #endregion

    #region Data Loading

    private async Task LoadQuickStatsAsync()
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return;

        try
        {
            // These would be actual service calls in a real implementation
            // For now, using placeholder values that would be replaced with:
            // TrackedChannelsCount = await ChannelService.GetTrackedChannelsCountAsync(CurrentUserId);
            // TopicsCount = await TopicService.GetTopicsCountAsync(CurrentUserId);
            // PendingSuggestionsCount = await SuggestionService.GetPendingSuggestionsCountAsync(CurrentUserId);
            // LastGeneratedDaysAgo = await SuggestionService.GetDaysSinceLastGenerationAsync(CurrentUserId);

            TrackedChannelsCount = 12;
            TopicsCount = 6;
            PendingSuggestionsCount = 0; // Will be updated when suggestions load
            LastGeneratedDaysAgo = 3;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load quick stats for user {UserId}", CurrentUserId);
            ShowQuickStats = false;
        }
    }

    private async Task LoadRecentGenerationsAsync()
    {
        if (string.IsNullOrEmpty(CurrentUserId)) return;

        try
        {
            // In a real implementation, this would be:
            // RecentGenerations = await SuggestionService.GetRecentGenerationsAsync(CurrentUserId, 5);

            // Placeholder data for demonstration
            RecentGenerations = new List<SuggestionGeneration>
            {
                new() { GeneratedAt = DateTime.UtcNow.AddDays(-1), SuggestionsCount = 8, SuccessRate = "87%" },
                new() { GeneratedAt = DateTime.UtcNow.AddDays(-3), SuggestionsCount = 12, SuccessRate = "92%" },
                new() { GeneratedAt = DateTime.UtcNow.AddDays(-7), SuggestionsCount = 6, SuccessRate = "83%" }
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load recent generations for user {UserId}", CurrentUserId);
        }
    }

    #endregion

    #region Suggestion Generation

    protected async Task GenerateNewSuggestions()
    {
        if (string.IsNullOrEmpty(CurrentUserId) || IsGenerating)
            return;

        try
        {
            IsGenerating = true;
            GenerationStatus = "Initializing suggestion generation...";
            StateHasChanged();

            // Show progress updates for better user experience
            await UpdateGenerationStatus("Checking tracked channels for new videos...");
            await Task.Delay(500); // Small delay for UX - allows user to see progress

            await UpdateGenerationStatus("Searching for topics across YouTube...");
            await Task.Delay(500);

            await UpdateGenerationStatus("Analyzing and scoring suggestions...");
            await Task.Delay(500);

            // Actual suggestion generation service call
            var result = await SuggestionService.GenerateSuggestions(CurrentUserId);

            if (result.IsSuccess) // Changed from result.Success to result.IsSuccess
            {
                await UpdateGenerationStatus($"Generated {result.NewSuggestions.Count} new suggestions!"); // Changed from result.SuggestionsGenerated

                // Refresh the suggestion queue component
                if (SuggestionQueueComponent != null)
                {
                    await SuggestionQueueComponent.RefreshSuggestionsAsync();
                }

                // Update local statistics
                PendingSuggestionsCount = result.NewSuggestions.Count; // Changed from result.SuggestionsGenerated
                LastGeneratedDaysAgo = 0;

                // Add to recent generations history
                RecentGenerations.Insert(0, new SuggestionGeneration
                {
                    GeneratedAt = DateTime.UtcNow,
                    SuggestionsCount = result.NewSuggestions.Count, // Changed from result.SuggestionsGenerated
                    SuccessRate = "New"
                });

                // Keep only the last 5 generations
                if (RecentGenerations.Count > 5)
                {
                    RecentGenerations = RecentGenerations.Take(5).ToList();
                }

                await MessageCenter.ShowSuccessAsync(
                    $"Generated {result.NewSuggestions.Count} new suggestions based on your preferences!"); // Changed from result.SuggestionsGenerated
            }
            else
            {
                await MessageCenter.ShowErrorAsync(
                    result.ErrorMessage ?? "Failed to generate suggestions. Please try again.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate suggestions for user {UserId}", CurrentUserId);
            await MessageCenter.ShowErrorAsync(
                "An unexpected error occurred while generating suggestions. Please try again.");
        }
        finally
        {
            IsGenerating = false;
            GenerationStatus = null;
            StateHasChanged();
        }
    }

    private async Task UpdateGenerationStatus(string status)
    {
        GenerationStatus = status;
        StateHasChanged();
        await Task.Delay(50); // Allow UI to update
    }

    protected string GetGenerateButtonTooltip()
    {
        if (IsGenerating)
            return "Please wait while suggestions are being generated...";

        if (TrackedChannelsCount == 0 && TopicsCount == 0)
            return "Add some channels and topics first to get personalized suggestions";

        if (PendingSuggestionsCount >= 90)
            return "Your queue is nearly full. Consider reviewing existing suggestions first.";

        return "Generate new personalized video suggestions based on your channels and topics";
    }

    #endregion

    #region Event Handlers

    protected async Task HandleSuggestionsChanged()
    {
        // Update the pending suggestions count when the queue changes
        try
        {
            if (!string.IsNullOrEmpty(CurrentUserId))
            {
                var suggestions = await SuggestionService.GetPendingSuggestionsAsync(CurrentUserId);
                PendingSuggestionsCount = suggestions.Count();
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update suggestions count for user {UserId}", CurrentUserId);
        }
    }

    #endregion
}

/// <summary>
/// Model for displaying recent suggestion generation history.
/// </summary>
public class SuggestionGeneration
{
    /// <summary>
    /// When the suggestions were generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Number of suggestions that were generated.
    /// </summary>
    public int SuggestionsCount { get; set; }

    /// <summary>
    /// Success rate or approval rate for this generation batch.
    /// </summary>
    public string SuccessRate { get; set; } = "N/A";
}