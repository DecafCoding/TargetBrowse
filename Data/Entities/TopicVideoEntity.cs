using System.ComponentModel.DataAnnotations;
using TargetBrowse.Data.Common;

namespace TargetBrowse.Data.Entities
{
    /// <summary>
    /// Junction table linking topics to cached video search results.
    /// Enables DB-first caching to reduce YouTube API quota usage.
    /// </summary>
    public class TopicVideoEntity : BaseEntity
    {
        [Required]
        public Guid TopicId { get; set; }

        [Required]
        public Guid VideoId { get; set; }

        /// <summary>
        /// Cached relevance score (0-10) from keyword matching at discovery time.
        /// </summary>
        public double RelevanceScore { get; set; }

        /// <summary>
        /// Comma-separated list of matched keywords from topic search.
        /// </summary>
        [StringLength(500)]
        public string MatchedKeywords { get; set; } = string.Empty;

        // Navigation properties
        public virtual TopicEntity Topic { get; set; } = null!;
        public virtual VideoEntity Video { get; set; } = null!;
    }
}
