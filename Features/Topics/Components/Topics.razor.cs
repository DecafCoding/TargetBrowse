using Microsoft.AspNetCore.Components;

namespace TargetBrowse.Features.Topics.Components;

/// <summary>
/// Code-behind for the Topics page component.
/// Manages coordination between the topic list and add topic components.
/// </summary>
public partial class Topics : ComponentBase
{
    // Component references for coordination between child components
    private AddTopic? AddTopicComponent;
    private TopicList? TopicListComponent;

    /// <summary>
    /// Handles when a new topic is successfully added.
    /// Refreshes the topic list to show the new topic.
    /// </summary>
    protected async Task HandleTopicAdded()
    {
        if (TopicListComponent != null)
        {
            await TopicListComponent.RefreshAsync();
        }
    }

    /// <summary>
    /// Handles when topics change (loaded, added, etc.).
    /// Can be used for additional coordination between components.
    /// </summary>
    protected async Task HandleTopicsChanged()
    {
        // Future: Could update global topic count, analytics, etc.
        await Task.CompletedTask;
    }
}
