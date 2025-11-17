namespace TargetBrowse.Services.ProjectServices.Models
{
    /// <summary>
    /// Result of adding a video to projects operation.
    /// </summary>
    public class AddToProjectResult
    {
        /// <summary>
        /// Whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Number of projects the video was successfully added to.
        /// </summary>
        public int AddedToProjectsCount { get; set; }

        /// <summary>
        /// List of project IDs that failed validation or addition.
        /// </summary>
        public List<Guid> FailedProjectIds { get; set; } = new();

        /// <summary>
        /// Validation errors per project (ProjectId -> Error message).
        /// </summary>
        public Dictionary<Guid, string> ProjectErrors { get; set; } = new();

        public static AddToProjectResult CreateSuccess(int addedCount)
        {
            return new AddToProjectResult
            {
                Success = true,
                AddedToProjectsCount = addedCount
            };
        }

        public static AddToProjectResult CreateFailure(string errorMessage)
        {
            return new AddToProjectResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }
}
