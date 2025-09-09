using Google.Apis.Discovery;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using TargetBrowse.Features.Topics.Models;
using TargetBrowse.Features.Topics.Services;

namespace TargetBrowse.Features.Topics.Components
{
    public partial class AddTopic : ComponentBase
    {
        [Inject] protected ITopicService TopicService { get; set; } = default!;

        [CascadingParameter]
        private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

        [Parameter]
        public EventCallback OnTopicAdded { get; set; }

        private AddTopicModel TopicModel { get; set; } = new();
        private bool IsSubmitting { get; set; } = false;

        protected override async Task OnInitializedAsync()
        {
            // Component initialization - no topic count loading needed
            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles form submission for adding a new topic.
        /// Validates user authentication and processes the request.
        /// </summary>
        private async Task HandleValidSubmit()
        {
            if (IsSubmitting) return;

            try
            {
                IsSubmitting = true;
                StateHasChanged();

                // Get current user
                var authState = await AuthenticationStateTask!;
                var userId = authState?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    // This shouldn't happen with [Authorize] attribute, but safety first
                    return;
                }

                // Attempt to add the topic
                var success = await TopicService.AddTopicAsync(userId, TopicModel.Name);

                if (success)
                {
                    // Reset form and notify parent component
                    TopicModel.Reset();

                    // Notify parent component that a topic was added
                    if (OnTopicAdded.HasDelegate)
                    {
                        await OnTopicAdded.InvokeAsync();
                    }
                }
            }
            finally
            {
                IsSubmitting = false;
                StateHasChanged();
            }
        }
    }
}