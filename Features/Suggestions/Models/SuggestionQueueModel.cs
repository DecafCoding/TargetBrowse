namespace TargetBrowse.Features.Suggestions.Models;

/// <summary>
/// View model for managing suggestion queue state and user interactions.
/// </summary>
public class SuggestionQueueModel
{
    /// <summary>
    /// All suggestions currently in the user's queue.
    /// </summary>
    public List<SuggestionDisplayModel> Suggestions { get; set; } = new();

    /// <summary>
    /// Currently selected suggestions for batch operations.
    /// </summary>
    public HashSet<Guid> SelectedSuggestionIds { get; set; } = new();

    /// <summary>
    /// Whether the queue is currently loading.
    /// </summary>
    public bool IsLoading { get; set; } = true;

    /// <summary>
    /// Whether suggestions are currently being generated.
    /// </summary>
    public bool IsGenerating { get; set; } = false;

    /// <summary>
    /// Error message if loading or generation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Current filter applied to the suggestions.
    /// </summary>
    public SuggestionFilter Filter { get; set; } = SuggestionFilter.All;

    /// <summary>
    /// Current sort order for suggestions.
    /// </summary>
    public SuggestionSort SortBy { get; set; } = SuggestionSort.CreatedDesc;

    /// <summary>
    /// Whether to show batch action controls.
    /// </summary>
    public bool ShowBatchActions => SelectedSuggestionIds.Any();

    /// <summary>
    /// Gets filtered suggestions based on current filter.
    /// </summary>
    public List<SuggestionDisplayModel> FilteredSuggestions
    {
        get
        {
            var filtered = Filter switch
            {
                SuggestionFilter.ChannelOnly => Suggestions.Where(s => s.GetSourceEnum() == SuggestionSource.TrackedChannel),
                SuggestionFilter.TopicOnly => Suggestions.Where(s => s.GetSourceEnum() == SuggestionSource.TopicSearch),
                SuggestionFilter.BothSources => Suggestions.Where(s => s.GetSourceEnum() == SuggestionSource.Both),
                SuggestionFilter.NearExpiry => Suggestions.Where(s => s.IsNearExpiry),
                _ => Suggestions
            };

            return SortBy switch
            {
                SuggestionSort.CreatedAsc => filtered.OrderBy(s => s.CreatedAt).ToList(),
                SuggestionSort.CreatedDesc => filtered.OrderByDescending(s => s.CreatedAt).ToList(),
                SuggestionSort.ScoreDesc => filtered.OrderByDescending(s => s.Score ?? 0).ToList(),
                SuggestionSort.ExpiryAsc => filtered.OrderBy(s => s.DaysUntilExpiry).ToList(),
                _ => filtered.OrderByDescending(s => s.CreatedAt).ToList()
            };
        }
    }

    /// <summary>
    /// Gets count of suggestions by source type.
    /// </summary>
    public SuggestionSourceCounts GetSourceCounts()
    {
        return new SuggestionSourceCounts
        {
            Total = Suggestions.Count,
            ChannelOnly = Suggestions.Count(s => s.GetSourceEnum() == SuggestionSource.TrackedChannel),
            TopicOnly = Suggestions.Count(s => s.GetSourceEnum() == SuggestionSource.TopicSearch),
            BothSources = Suggestions.Count(s => s.GetSourceEnum() == SuggestionSource.Both),
            NearExpiry = Suggestions.Count(s => s.IsNearExpiry)
        };
    }

    /// <summary>
    /// Gets the progress toward the 100-suggestion limit.
    /// </summary>
    public int ProgressPercentage => Math.Min((Suggestions.Count * 100) / 100, 100);

    /// <summary>
    /// Whether the user is approaching the suggestion limit.
    /// </summary>
    public bool IsNearLimit => Suggestions.Count >= 80;

    /// <summary>
    /// Whether the user has reached the suggestion limit.
    /// </summary>
    public bool IsAtLimit => Suggestions.Count >= 100;

    /// <summary>
    /// Selects or deselects a suggestion for batch operations.
    /// </summary>
    public void ToggleSelection(Guid suggestionId)
    {
        if (SelectedSuggestionIds.Contains(suggestionId))
        {
            SelectedSuggestionIds.Remove(suggestionId);
        }
        else
        {
            SelectedSuggestionIds.Add(suggestionId);
        }
    }

    /// <summary>
    /// Selects all currently filtered suggestions.
    /// </summary>
    public void SelectAll()
    {
        foreach (var suggestion in FilteredSuggestions)
        {
            SelectedSuggestionIds.Add(suggestion.Id);
        }
    }

    /// <summary>
    /// Clears all selections.
    /// </summary>
    public void ClearSelection()
    {
        SelectedSuggestionIds.Clear();
    }

    /// <summary>
    /// Removes suggestions from the model after they've been processed.
    /// </summary>
    public void RemoveSuggestions(IEnumerable<Guid> suggestionIds)
    {
        var idsToRemove = suggestionIds.ToHashSet();
        Suggestions.RemoveAll(s => idsToRemove.Contains(s.Id));
        SelectedSuggestionIds.RemoveWhere(id => idsToRemove.Contains(id));
    }
}

/// <summary>
/// Filter options for the suggestion queue.
/// </summary>
public enum SuggestionFilter
{
    All,
    ChannelOnly,
    TopicOnly,
    BothSources,
    NearExpiry
}

/// <summary>
/// Sort options for suggestions.
/// </summary>
public enum SuggestionSort
{
    CreatedDesc,
    CreatedAsc,
    ScoreDesc,
    ExpiryAsc
}

/// <summary>
/// Count of suggestions by source type for display.
/// </summary>
public class SuggestionSourceCounts
{
    public int Total { get; set; }
    public int ChannelOnly { get; set; }
    public int TopicOnly { get; set; }
    public int BothSources { get; set; }
    public int NearExpiry { get; set; }
}

/// <summary>
/// Extensions for SuggestionDisplayModel to support queue operations.
/// </summary>
public static class SuggestionDisplayModelExtensions
{
    /// <summary>
    /// Gets the suggestion source as an enum from the reason string.
    /// </summary>
    public static SuggestionSource GetSourceEnum(this SuggestionDisplayModel suggestion)
    {
        if (suggestion.Reason.Contains("Channel + Topic") || suggestion.Reason.Contains("⭐"))
        {
            return SuggestionSource.Both;
        }
        else if (suggestion.Reason.Contains("Channel") || suggestion.Reason.Contains("📺"))
        {
            return SuggestionSource.TrackedChannel;
        }
        else if (suggestion.Reason.Contains("Topic") || suggestion.Reason.Contains("🔍"))
        {
            return SuggestionSource.TopicSearch;
        }

        return SuggestionSource.TrackedChannel; // Default fallback
    }

    /// <summary>
    /// Gets a formatted display string for the source.
    /// </summary>
    public static string GetSourceDisplayText(this SuggestionDisplayModel suggestion)
    {
        return suggestion.GetSourceEnum() switch
        {
            SuggestionSource.Both => "Channel + Topic Match",
            SuggestionSource.TrackedChannel => "Channel Update",
            SuggestionSource.TopicSearch => "Topic Match",
            _ => "Unknown Source"
        };
    }

    /// <summary>
    /// Gets the CSS class for the source badge.
    /// </summary>
    public static string GetSourceBadgeClass(this SuggestionDisplayModel suggestion)
    {
        return suggestion.GetSourceEnum() switch
        {
            SuggestionSource.Both => "badge bg-primary",
            SuggestionSource.TrackedChannel => "badge bg-info",
            SuggestionSource.TopicSearch => "badge bg-success",
            _ => "badge bg-secondary"
        };
    }

    /// <summary>
    /// Gets the icon for the source badge.
    /// </summary>
    public static string GetSourceIcon(this SuggestionDisplayModel suggestion)
    {
        return suggestion.GetSourceEnum() switch
        {
            SuggestionSource.Both => "⭐",
            SuggestionSource.TrackedChannel => "📺",
            SuggestionSource.TopicSearch => "🔍",
            _ => "❓"
        };
    }
}

/// <summary>
/// Action mode for suggestion actions component.
/// Determines whether actions apply to individual suggestions or batches.
/// </summary>
public enum ActionMode
{
    /// <summary>
    /// Actions apply to a single suggestion.
    /// </summary>
    Individual,

    /// <summary>
    /// Actions apply to multiple selected suggestions.
    /// </summary>
    Batch
}