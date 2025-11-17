using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Projects.Models
{
    /// <summary>
    /// Request model for generating a project guide.
    /// </summary>
    public class GenerateGuideRequest
    {
        /// <summary>
        /// Project ID to generate guide for.
        /// </summary>
        [Required(ErrorMessage = "Project ID is required")]
        public Guid ProjectId { get; set; }
    }
}
