using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;
using TargetBrowse.Services.Validation;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Represents a user's rating for either a video or a channel.
    /// Either VideoId or ChannelId will be set, not both.
    /// </summary>
    public class RatingEntity : BaseEntity
    {
        [Required]
        [Range(RatingValidator.MinStars, RatingValidator.MaxStars)]
        public int Stars { get; set; }

        [Required]
        [StringLength(RatingValidator.MaxNotesLength, MinimumLength = RatingValidator.MinNotesLength)]
        public string Notes { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Either ChannelId or VideoId will be set, not both
        /// </summary>
        public Guid? ChannelId { get; set; }

        /// <summary>
        /// Either ChannelId or VideoId will be set, not both
        /// </summary>
        public Guid? VideoId { get; set; }

        // Navigation properties
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual ChannelEntity? Channel { get; set; }
        public virtual VideoEntity? Video { get; set; }
    }
}