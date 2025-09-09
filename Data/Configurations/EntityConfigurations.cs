using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Data.Configurations
{
    /// <summary>
    /// Entity Framework configurations for all YouTube Video Tracker entities.
    /// Defines relationships, constraints, and indexes for optimal performance.
    /// </summary>
    public class TopicEntityConfiguration : IEntityTypeConfiguration<TopicEntity>
    {
        public void Configure(EntityTypeBuilder<TopicEntity> builder)
        {
            builder.ToTable("Topics");

            // Primary key and indexes
            builder.HasKey(t => t.Id);
            builder.HasIndex(t => new { t.UserId, t.Name }).IsUnique();
            builder.HasIndex(t => t.UserId);

            // Properties
            builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
            builder.Property(t => t.UserId).IsRequired();

            // Relationships
            builder.HasOne(t => t.User)
                   .WithMany(u => u.Topics)
                   .HasForeignKey(t => t.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class ChannelEntityConfiguration : IEntityTypeConfiguration<ChannelEntity>
    {
        public void Configure(EntityTypeBuilder<ChannelEntity> builder)
        {
            builder.ToTable("Channels");

            // Primary key and indexes
            builder.HasKey(c => c.Id);
            builder.HasIndex(c => c.YouTubeChannelId).IsUnique();
            builder.HasIndex(c => c.Name);
            builder.HasIndex(c => c.LastCheckDate);

            // Properties
            builder.Property(c => c.YouTubeChannelId).IsRequired().HasMaxLength(50);
            builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
            builder.Property(c => c.ThumbnailUrl).HasMaxLength(500);
        }
    }

    public class UserChannelEntityConfiguration : IEntityTypeConfiguration<UserChannelEntity>
    {
        public void Configure(EntityTypeBuilder<UserChannelEntity> builder)
        {
            builder.ToTable("UserChannels");

            // Primary key and indexes
            builder.HasKey(uc => uc.Id);
            builder.HasIndex(uc => new { uc.UserId, uc.ChannelId }).IsUnique();
            builder.HasIndex(uc => uc.UserId);
            builder.HasIndex(uc => uc.TrackedSince);

            // Properties
            builder.Property(uc => uc.UserId).IsRequired();
            builder.Property(uc => uc.ChannelId).IsRequired();

            // Relationships
            builder.HasOne(uc => uc.User)
                   .WithMany(u => u.TrackedChannels)
                   .HasForeignKey(uc => uc.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(uc => uc.Channel)
                   .WithMany(c => c.UserChannels)
                   .HasForeignKey(uc => uc.ChannelId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class VideoEntityConfiguration : IEntityTypeConfiguration<VideoEntity>
    {
        public void Configure(EntityTypeBuilder<VideoEntity> builder)
        {
            builder.ToTable("Videos");

            // Primary key and indexes
            builder.HasKey(v => v.Id);
            builder.HasIndex(v => v.YouTubeVideoId).IsUnique();
            builder.HasIndex(v => v.ChannelId);
            builder.HasIndex(v => v.PublishedAt);
            builder.HasIndex(v => v.Title);

            // Properties
            builder.Property(v => v.YouTubeVideoId).IsRequired().HasMaxLength(20);
            builder.Property(v => v.Title).IsRequired().HasMaxLength(300);
            builder.Property(v => v.RawTranscript).HasColumnType("nvarchar(max)");

            // Relationships
            builder.HasOne(v => v.Channel)
                   .WithMany(c => c.Videos)
                   .HasForeignKey(v => v.ChannelId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class UserVideoEntityConfiguration : IEntityTypeConfiguration<UserVideoEntity>
    {
        public void Configure(EntityTypeBuilder<UserVideoEntity> builder)
        {
            builder.ToTable("UserVideos");

            // Primary key and indexes
            builder.HasKey(uv => uv.Id);
            builder.HasIndex(uv => new { uv.UserId, uv.VideoId }).IsUnique();
            builder.HasIndex(uv => uv.UserId);
            builder.HasIndex(uv => uv.Status);
            builder.HasIndex(uv => uv.AddedToLibraryAt);

            // Properties
            builder.Property(uv => uv.UserId).IsRequired();
            builder.Property(uv => uv.VideoId).IsRequired();
            builder.Property(uv => uv.Status).HasConversion<int>();

            // Relationships
            builder.HasOne(uv => uv.User)
                   .WithMany(u => u.SavedVideos)
                   .HasForeignKey(uv => uv.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(uv => uv.Video)
                   .WithMany(v => v.UserVideos)
                   .HasForeignKey(uv => uv.VideoId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class RatingEntityConfiguration : IEntityTypeConfiguration<RatingEntity>
    {
        public void Configure(EntityTypeBuilder<RatingEntity> builder)
        {
            builder.ToTable("Ratings");

            // Primary key and indexes
            builder.HasKey(r => r.Id);
            builder.HasIndex(r => new { r.UserId, r.ChannelId }).IsUnique()
                   .HasFilter("[ChannelId] IS NOT NULL");
            builder.HasIndex(r => new { r.UserId, r.VideoId }).IsUnique()
                   .HasFilter("[VideoId] IS NOT NULL");
            builder.HasIndex(r => r.UserId);
            builder.HasIndex(r => r.Stars);

            // Properties
            builder.Property(r => r.Stars).IsRequired();
            builder.Property(r => r.Notes).IsRequired().HasMaxLength(1000);
            builder.Property(r => r.UserId).IsRequired();

            // Constraints - ensure either ChannelId or VideoId is set, not both
            builder.HasCheckConstraint("CK_Rating_OneTarget",
                "([ChannelId] IS NOT NULL AND [VideoId] IS NULL) OR ([ChannelId] IS NULL AND [VideoId] IS NOT NULL)");

            // Relationships
            builder.HasOne(r => r.User)
                   .WithMany(u => u.Ratings)
                   .HasForeignKey(r => r.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.Channel)
                   .WithMany(c => c.Ratings)
                   .HasForeignKey(r => r.ChannelId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Changed to NoAction to prevent cascade path conflict
            // Videos -> UserVideos -> User -> Ratings (CASCADE)
            // Videos -> Ratings (NO ACTION) - prevents multiple cascade paths
            builder.HasOne(r => r.Video)
                   .WithMany(v => v.Ratings)
                   .HasForeignKey(r => r.VideoId)
                   .OnDelete(DeleteBehavior.NoAction);
        }
    }

    public class SuggestionEntityConfiguration : IEntityTypeConfiguration<SuggestionEntity>
    {
        public void Configure(EntityTypeBuilder<SuggestionEntity> builder)
        {
            builder.ToTable("Suggestions");

            // Primary key and indexes
            builder.HasKey(s => s.Id);

            // REMOVE THIS LINE - it's causing the duplicate key error:
            // builder.HasIndex(s => new { s.UserId, s.VideoId }).IsUnique();

            // Replace with non-unique indexes for performance:
            builder.HasIndex(s => s.UserId);
            builder.HasIndex(s => s.VideoId);
            builder.HasIndex(s => new { s.UserId, s.VideoId }); // Non-unique for queries

            builder.HasIndex(s => s.CreatedAt);
            builder.HasIndex(s => s.IsApproved);
            builder.HasIndex(s => s.IsDenied);

            // Properties
            builder.Property(s => s.UserId).IsRequired();
            builder.Property(s => s.VideoId).IsRequired();
            builder.Property(s => s.Reason).IsRequired().HasMaxLength(200);

            // Relationships
            builder.HasOne(s => s.User)
                   .WithMany(u => u.Suggestions)
                   .HasForeignKey(s => s.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.Video)
                   .WithMany(v => v.Suggestions)
                   .HasForeignKey(s => s.VideoId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class SummaryEntityConfiguration : IEntityTypeConfiguration<SummaryEntity>
    {
        public void Configure(EntityTypeBuilder<SummaryEntity> builder)
        {
            builder.ToTable("Summaries");

            // Primary key and indexes
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.VideoId).IsUnique(); // One summary per video
            builder.HasIndex(s => s.CreatedAt);
            builder.HasIndex(s => s.PromptVersion);
            builder.HasIndex(s => s.GenerationCount);

            // Properties
            builder.Property(s => s.VideoId).IsRequired();
            builder.Property(s => s.Content).IsRequired().HasMaxLength(2000);
            builder.Property(s => s.PromptVersion).IsRequired().HasMaxLength(20);
            builder.Property(s => s.ModelUsed).HasMaxLength(50);

            // Relationships
            builder.HasOne(s => s.Video)
                   .WithOne(v => v.Summary)
                   .HasForeignKey<SummaryEntity>(s => s.VideoId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class SummaryGenerationRequestEntityConfiguration : IEntityTypeConfiguration<SummaryGenerationRequestEntity>
    {
        public void Configure(EntityTypeBuilder<SummaryGenerationRequestEntity> builder)
        {
            builder.ToTable("SummaryGenerationRequests");

            // Primary key and indexes
            builder.HasKey(sgr => sgr.Id);
            builder.HasIndex(sgr => sgr.UserId);
            builder.HasIndex(sgr => sgr.RequestedAt);
            builder.HasIndex(sgr => sgr.WasSuccessful);
            builder.HasIndex(sgr => new { sgr.UserId, sgr.RequestedAt }); // For daily limit queries

            // Properties
            builder.Property(sgr => sgr.UserId).IsRequired();
            builder.Property(sgr => sgr.VideoId).IsRequired();

            // Relationships
            builder.HasOne(sgr => sgr.User)
                   .WithMany(u => u.SummaryGenerationRequests)
                   .HasForeignKey(sgr => sgr.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Changed to NoAction to prevent cascade path conflict
            // Videos -> UserVideos -> User -> SummaryGenerationRequests (CASCADE)  
            // Videos -> SummaryGenerationRequests (NO ACTION) - prevents multiple cascade paths
            builder.HasOne(sgr => sgr.Video)
                   .WithMany(v => v.SummaryGenerationRequests)
                   .HasForeignKey(sgr => sgr.VideoId)
                   .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(sgr => sgr.Summary)
                   .WithMany(s => s.GenerationRequests)
                   .HasForeignKey(sgr => sgr.SummaryId)
                   .OnDelete(DeleteBehavior.SetNull); // Keep request record even if summary is deleted
        }
    }


    /// <summary>
    /// Entity configuration for SuggestionTopicEntity junction table.
    /// Configures many-to-many relationship between Suggestions and Topics.
    /// </summary>
    public class SuggestionTopicEntityConfiguration : IEntityTypeConfiguration<SuggestionTopicEntity>
    {
        public void Configure(EntityTypeBuilder<SuggestionTopicEntity> builder)
        {
            builder.ToTable("SuggestionTopics");

            // Configure relationships
            builder.HasOne(st => st.Suggestion)
                   .WithMany(s => s.SuggestionTopics)
                   .HasForeignKey(st => st.SuggestionId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(st => st.Topic)
                   .WithMany(t => t.SuggestionTopics)
                   .HasForeignKey(st => st.TopicId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Prevent duplicate suggestion-topic combinations
            builder.HasIndex(st => new { st.SuggestionId, st.TopicId })
                   .IsUnique()
                   .HasDatabaseName("IX_SuggestionTopics_SuggestionId_TopicId");

            // Configure properties
            builder.Property(st => st.SuggestionId)
                   .IsRequired();

            builder.Property(st => st.TopicId)
                   .IsRequired();
        }
    }
}