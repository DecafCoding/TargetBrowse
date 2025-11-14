using TargetBrowse.Services.Models;

namespace TargetBrowse.Services.Interfaces
{
    /// <summary>
    /// Service for managing application-wide user feedback messages.
    /// Provides centralized messaging for success, error, and API limit notifications.
    /// Maintains a history of the last 5 messages (newest first).
    /// </summary>
    public interface IMessageCenterService
    {
        /// <summary>
        /// Event raised when a new message is added to the message history.
        /// Components can subscribe to this event to update their display.
        /// </summary>
        event Action<MessageState?>? MessageChanged;

        /// <summary>
        /// Displays a success message to the user.
        /// Adds to message history (max 5 messages, oldest removed).
        /// </summary>
        /// <param name="message">Success message text</param>
        Task ShowSuccessAsync(string message);

        /// <summary>
        /// Displays an error message to the user.
        /// Adds to message history (max 5 messages, oldest removed).
        /// </summary>
        /// <param name="message">Error message text</param>
        Task ShowErrorAsync(string message);

        /// <summary>
        /// Displays a warning message to the user.
        /// Adds to message history (max 5 messages, oldest removed).
        /// </summary>
        /// <param name="message">Warning message text</param>
        Task ShowWarningAsync(string message);

        /// <summary>
        /// Displays an information message to the user.
        /// Adds to message history (max 5 messages, oldest removed).
        /// </summary>
        /// <param name="message">Information message text</param>
        Task ShowInfoAsync(string message);

        /// <summary>
        /// Displays an API limit notification with reset time information.
        /// Adds to message history (max 5 messages, oldest removed).
        /// </summary>
        /// <param name="apiName">Name of the API that reached its limit</param>
        /// <param name="resetTime">When the API limit will reset (optional)</param>
        Task ShowApiLimitAsync(string apiName, DateTime? resetTime = null);

        /// <summary>
        /// Gets the recent message history (up to 5 messages, newest first).
        /// Returns an empty list if no messages are available.
        /// </summary>
        List<MessageState> GetRecentMessages();
    }
}