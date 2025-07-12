using Microsoft.AspNetCore.Identity;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Data;

/// <summary>
/// Application user with navigation properties to all related entities.
/// Extends IdentityUser to include YouTube Video Tracker specific relationships.
/// </summary>
public class ApplicationUser : IdentityUser
{
    // Navigation properties for user's YouTube Video Tracker data
    public virtual ICollection<TopicEntity> Topics { get; set; } = new List<TopicEntity>();
    public virtual ICollection<UserChannelEntity> TrackedChannels { get; set; } = new List<UserChannelEntity>();
    public virtual ICollection<UserVideoEntity> SavedVideos { get; set; } = new List<UserVideoEntity>();
    public virtual ICollection<RatingEntity> Ratings { get; set; } = new List<RatingEntity>();
    public virtual ICollection<SuggestionEntity> Suggestions { get; set; } = new List<SuggestionEntity>();
    public virtual ICollection<SummaryGenerationRequestEntity> SummaryGenerationRequests { get; set; } = new List<SummaryGenerationRequestEntity>();
}