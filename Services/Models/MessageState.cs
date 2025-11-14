namespace TargetBrowse.Services.Models
{
    /// <summary>
    /// Represents the current state of a user message in the application.
    /// Used by the Message Center Service to communicate UI feedback.
    /// </summary>
    public class MessageState
    {
        /// <summary>
        /// The message text to display to the user.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// The type of message, which determines styling and icon.
        /// </summary>
        public MessageType Type { get; set; }

        /// <summary>
        /// When this message was created.
        /// Used for debugging and potential future features.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional additional context for the message.
        /// Currently used for API reset times.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// Creates a new message state.
        /// </summary>
        /// <param name="text">Message text</param>
        /// <param name="type">Message type</param>
        /// <param name="context">Optional additional context</param>
        public MessageState(string text, MessageType type, string? context = null)
        {
            Text = text;
            Type = type;
            Context = context;
        }

        /// <summary>
        /// Gets the Bootstrap CSS class for this message type.
        /// </summary>
        //public string GetBootstrapClass()
        //{
        //    return Type switch
        //    {
        //        MessageType.Success => "text-success",
        //        MessageType.Error => "text-danger",
        //        MessageType.Warning => "text-warning",
        //        MessageType.Info => "text-info",
        //        MessageType.ApiLimit => "text-warning",
        //        _ => "text-info"
        //    };
        //}

        /// <summary>
        /// Gets the full formatted message including context if available.
        /// </summary>
        public string GetFullMessage()
        {
            if (string.IsNullOrEmpty(Context))
                return Text;

            return $"{Text} {Context}";
        }
    }

    /// <summary>
    /// Types of messages that can be displayed in the Message Center.
    /// Each type has associated styling and behavior.
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// Success message - green styling, checkmark icon
        /// </summary>
        Success,

        /// <summary>
        /// Error message - red styling, warning icon
        /// </summary>
        Error,

        /// <summary>
        /// Warning message - yellow styling, warning icon
        /// </summary>
        Warning,

        /// <summary>
        /// Information message - blue styling, info icon
        /// </summary>
        Info,

        /// <summary>
        /// API limit notification - yellow styling, clock icon
        /// </summary>
        ApiLimit
    }
}