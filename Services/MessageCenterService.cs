using TargetBrowse.Services.Interfaces;
using TargetBrowse.Services.Models;

namespace TargetBrowse.Services
{
    /// <summary>
    /// Implementation of the Message Center Service for centralized user feedback.
    /// Manages application-wide messages with persistence until replacement.
    /// Thread-safe for concurrent access in Blazor Server applications.
    /// </summary>
    public class MessageCenterService : IMessageCenterService
    {
        private MessageState? _currentMessage;
        private readonly object _lock = new object();

        /// <summary>
        /// Event raised when the current message changes or is cleared.
        /// </summary>
        public event Action<MessageState?>? MessageChanged;

        /// <summary>
        /// Displays a success message to the user.
        /// </summary>
        /// <param name="message">Success message text</param>
        public Task ShowSuccessAsync(string message)
        {
            SetMessage(message, MessageType.Success);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Displays an error message to the user.
        /// </summary>
        /// <param name="message">Error message text</param>
        public Task ShowErrorAsync(string message)
        {
            SetMessage(message, MessageType.Error);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Displays a warning message to the user.
        /// </summary>
        /// <param name="message">Warning message text</param>
        public Task ShowWarningAsync(string message)
        {
            SetMessage(message, MessageType.Warning);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Displays an information message to the user.
        /// </summary>
        /// <param name="message">Information message text</param>
        public Task ShowInfoAsync(string message)
        {
            SetMessage(message, MessageType.Info);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Displays an API limit notification with optional reset time.
        /// </summary>
        /// <param name="apiName">Name of the API that reached its limit</param>
        /// <param name="resetTime">When the API limit will reset</param>
        public Task ShowApiLimitAsync(string apiName, DateTime? resetTime = null)
        {
            var message = $"API limit reached for {apiName}.";
            string? context = null;

            if (resetTime.HasValue)
            {
                var timeUntilReset = resetTime.Value - DateTime.UtcNow;
                if (timeUntilReset.TotalMinutes > 60)
                {
                    context = $"Resets in {timeUntilReset.Hours}h {timeUntilReset.Minutes}m.";
                }
                else if (timeUntilReset.TotalMinutes > 1)
                {
                    context = $"Resets in {(int)timeUntilReset.TotalMinutes} minutes.";
                }
                else if (timeUntilReset.TotalSeconds > 0)
                {
                    context = "Resets in less than 1 minute.";
                }
                else
                {
                    context = "Limit should reset momentarily.";
                }
            }

            SetMessage(message, MessageType.ApiLimit, context);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the current message state.
        /// Returns null if no message is currently displayed.
        /// </summary>
        public MessageState? GetCurrentMessage()
        {
            lock (_lock)
            {
                return _currentMessage;
            }
        }

        /// <summary>
        /// Clears the current message.
        /// </summary>
        public Task ClearMessageAsync()
        {
            lock (_lock)
            {
                _currentMessage = null;
            }

            // Notify subscribers that message was cleared
            MessageChanged?.Invoke(null);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Sets a new message and notifies subscribers.
        /// Thread-safe implementation for concurrent access.
        /// </summary>
        /// <param name="text">Message text</param>
        /// <param name="type">Message type</param>
        /// <param name="context">Optional additional context</param>
        private void SetMessage(string text, MessageType type, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            MessageState newMessage;
            lock (_lock)
            {
                newMessage = new MessageState(text.Trim(), type, context);
                _currentMessage = newMessage;
            }

            // Notify subscribers of the new message
            MessageChanged?.Invoke(newMessage);
        }
    }
}