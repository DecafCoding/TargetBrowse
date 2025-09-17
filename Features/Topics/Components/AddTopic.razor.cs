using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.Topics.Models;
using TargetBrowse.Features.Topics.Services;
using TargetBrowse.Services.Interfaces; // Add data service

namespace TargetBrowse.Features.Topics.Components
{
    /// <summary>
    /// Component for adding new topics with improved validation and user feedback.
    /// Updated to prevent concurrent DbContext access during initialization.
    /// </summary>
    public partial class AddTopic : ComponentBase
    {
        #region Injected Services

        [Inject] protected ITopicService TopicService { get; set; } = default!;
        [Inject] protected ITopicDataService TopicDataService { get; set; } = default!; // Add data service

        #endregion

        #region Cascading Parameters

        [CascadingParameter]
        private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

        #endregion

        #region Parameters

        [Parameter]
        public EventCallback OnTopicAdded { get; set; }

        #endregion

        #region Properties

        private AddTopicModel TopicModel { get; set; } = new();
        private bool IsSubmitting { get; set; } = false;
        private int CurrentTopicCount { get; set; } = 0;
        private bool IsLoadingCount { get; set; } = false; // Changed to false initially
        private bool HasLoadedCount { get; set; } = false; // Track if count has been loaded

        #endregion

        #region Lifecycle Methods

        protected override async Task OnInitializedAsync()
        {
            // Don't load topic count during initialization to prevent concurrency issues
            // It will be loaded when the user starts typing or when form becomes active
            await Task.CompletedTask;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Loads the current topic count for the user.
        /// Used for better UX showing how many topics they have.
        /// </summary>
        private async Task LoadTopicCountAsync()
        {
            if (HasLoadedCount || IsLoadingCount) return; // Prevent multiple loads

            try
            {
                IsLoadingCount = true;
                StateHasChanged();

                var authState = await AuthenticationStateTask!;
                var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!string.IsNullOrEmpty(userId))
                {
                    // Use TopicDataService for efficient count retrieval
                    CurrentTopicCount = await TopicDataService.GetUserTopicCountAsync(userId);
                    HasLoadedCount = true;
                }
            }
            finally
            {
                IsLoadingCount = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Handles form submission for adding a new topic.
        /// Validates user authentication, checks for duplicates, and processes the request.
        /// </summary>
        private async Task HandleValidSubmit()
        {
            if (IsSubmitting) return;

            try
            {
                IsSubmitting = true;
                StateHasChanged();

                // Ensure topic count is loaded before validating
                if (!HasLoadedCount)
                {
                    await LoadTopicCountAsync();
                }

                // Get current user
                var authState = await AuthenticationStateTask!;
                var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    // This shouldn't happen with [Authorize] attribute, but safety first
                    return;
                }

                // Check if user already has this topic (improved UX)
                var hasExistingTopic = await TopicDataService.UserHasTopicAsync(userId, TopicModel.Name);
                if (hasExistingTopic)
                {
                    // Could add a message center notification here
                    // For now, the TopicService will handle this validation
                }

                // Attempt to add the topic using business service
                var success = await TopicService.AddTopicAsync(userId, TopicModel.Name);

                if (success)
                {
                    // Update topic count for UI
                    CurrentTopicCount++;

                    // Reset form and notify parent component
                    TopicModel.Reset();

                    // Notify parent component that a topic was added
                    if (OnTopicAdded.HasDelegate)
                    {
                        await OnTopicAdded.InvokeAsync();
                    }
                }
                else
                {
                    // Refresh topic count in case of failure
                    HasLoadedCount = false; // Force reload
                    await LoadTopicCountAsync();
                }
            }
            finally
            {
                IsSubmitting = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Handles when the user focuses on the topic name input.
        /// Loads the topic count if not already loaded.
        /// </summary>
        protected async Task OnTopicNameFocus()
        {
            if (!HasLoadedCount)
            {
                await LoadTopicCountAsync();
            }
        }

        /// <summary>
        /// Gets display text for the topic count status.
        /// </summary>
        protected string GetTopicCountDisplay()
        {
            if (IsLoadingCount)
            {
                return "Loading...";
            }

            if (!HasLoadedCount)
            {
                return "Click to see count";
            }

            return $"{CurrentTopicCount}/10 topics";
        }

        /// <summary>
        /// Gets CSS class for topic count display based on current count.
        /// </summary>
        protected string GetTopicCountClass()
        {
            if (!HasLoadedCount) return "text-muted";

            return CurrentTopicCount switch
            {
                >= 10 => "text-danger",
                >= 8 => "text-warning", 
                >= 6 => "text-info",
                _ => "text-muted"
            };
        }

        /// <summary>
        /// Checks if the add topic form should be disabled.
        /// </summary>
        protected bool IsAddDisabled()
        {
            return IsSubmitting || (HasLoadedCount && CurrentTopicCount >= 10) || string.IsNullOrWhiteSpace(TopicModel.Name);
        }

        #endregion
    }
}
