using Microsoft.AspNetCore.Components;
using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Components.Shared
{
    public partial class MessageSidebar : IDisposable
    {
        [Inject]
        private IMessageCenterService MessageCenterService { get; set; } = default!;

        private List<MessageState> _messages = new();

        protected override void OnInitialized()
        {
            // Subscribe to message changes
            MessageCenterService.MessageChanged += OnMessageChanged;

            // Load existing messages
            _messages = MessageCenterService.GetRecentMessages();
        }

        private void OnMessageChanged(MessageState? newMessage)
        {
            // Refresh the message list
            _messages = MessageCenterService.GetRecentMessages();

            // Trigger UI update
            InvokeAsync(StateHasChanged);
        }

        private string GetTextColorClass(MessageType type)
        {
            return type switch
            {
                MessageType.Success => "text-success",
                MessageType.Error => "text-danger",
                MessageType.Warning => "text-warning",
                MessageType.Info => "text-info",
                MessageType.ApiLimit => "text-warning",
                _ => "text-secondary"
            };
        }

        public void Dispose()
        {
            // Unsubscribe from message changes
            MessageCenterService.MessageChanged -= OnMessageChanged;
        }
    }
}
