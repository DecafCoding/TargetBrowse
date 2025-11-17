using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Services.ProjectServices.Models
{
    /// <summary>
    /// Request model for adding a video to one or more projects.
    /// </summary>
    public class AddToProjectRequest
    {
        /// <summary>
        /// ID of the video to add to projects.
        /// </summary>
        [Required]
        public Guid VideoId { get; set; }

        /// <summary>
        /// List of project IDs to add the video to.
        /// Can be one or multiple projects.
        /// </summary>
        [Required]
        [MinLength(1, ErrorMessage = "At least one project must be selected.")]
        public List<Guid> ProjectIds { get; set; } = new();

        /// <summary>
        /// ID of the user making the request.
        /// Used for authorization and project ownership validation.
        /// </summary>
        [Required]
        public string UserId { get; set; } = string.Empty;
    }
}
