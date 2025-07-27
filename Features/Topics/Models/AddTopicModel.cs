using System.ComponentModel.DataAnnotations;

namespace TargetBrowse.Features.Topics.Models;

/// <summary>
/// Form model for adding a new topic.
/// Includes validation rules for user input.
/// </summary>
public class AddTopicModel
{
    [Required(ErrorMessage = "Topic name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Topic name must be between 2 and 100 characters.")]
    [Display(Name = "Topic Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Resets the model to initial state for reuse after successful submission.
    /// </summary>
    public void Reset()
    {
        Name = string.Empty;
    }
}