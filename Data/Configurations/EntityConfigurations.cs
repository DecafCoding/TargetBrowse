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
            builder.HasIndex(v => v.VideoTypeId);

            // Properties
            builder.Property(v => v.YouTubeVideoId).IsRequired().HasMaxLength(20);
            builder.Property(v => v.Title).IsRequired().HasMaxLength(300);
            builder.Property(v => v.RawTranscript).HasColumnType("nvarchar(max)");

            // Relationships
            builder.HasOne(v => v.Channel)
                   .WithMany(c => c.Videos)
                   .HasForeignKey(v => v.ChannelId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(v => v.VideoType)
                   .WithMany(vt => vt.Videos)
                   .HasForeignKey(v => v.VideoTypeId)
                   .IsRequired(false) // Optional relationship
                   .OnDelete(DeleteBehavior.SetNull); // Set to null if type is deleted
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
            builder.HasIndex(s => s.AICallId);
            builder.HasIndex(s => s.GenerationCount);

            // Properties
            builder.Property(s => s.VideoId).IsRequired();
            builder.Property(s => s.Content).IsRequired().HasMaxLength(4000);
            builder.Property(s => s.Summary).IsRequired().HasMaxLength(1000);

            // Relationships
            builder.HasOne(s => s.Video)
                   .WithOne(v => v.Summary)
                   .HasForeignKey<SummaryEntity>(s => s.VideoId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.AICall)
                   .WithMany(a => a.Summaries)
                   .HasForeignKey(s => s.AICallId)
                   .OnDelete(DeleteBehavior.SetNull); // Keep summary even if AI call is deleted
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

    /// <summary>
    /// Entity configuration for ModelEntity.
    /// Configures AI models with provider and cost tracking.
    /// </summary>
    public class ModelEntityConfiguration : IEntityTypeConfiguration<ModelEntity>
    {
        public void Configure(EntityTypeBuilder<ModelEntity> builder)
        {
            builder.ToTable("Models");

            // Primary key and indexes
            builder.HasKey(m => m.Id);
            builder.HasIndex(m => new { m.Provider, m.Name }).IsUnique();
            builder.HasIndex(m => m.IsActive);

            // Properties
            builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
            builder.Property(m => m.Provider).IsRequired().HasMaxLength(50);
            builder.Property(m => m.CostPer1kInputTokens).IsRequired().HasPrecision(18, 6);
            builder.Property(m => m.CostPer1kOutputTokens).IsRequired().HasPrecision(18, 6);
            builder.Property(m => m.IsActive).IsRequired();
        }
    }

    /// <summary>
    /// Entity configuration for PromptEntity.
    /// Configures AI prompt templates with model relationships.
    /// </summary>
    public class PromptEntityConfiguration : IEntityTypeConfiguration<PromptEntity>
    {
        public void Configure(EntityTypeBuilder<PromptEntity> builder)
        {
            builder.ToTable("Prompts");

            // Primary key and indexes
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => new { p.Name, p.Version }).IsUnique();
            builder.HasIndex(p => p.IsActive);
            builder.HasIndex(p => p.ModelId);

            // Properties
            builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
            builder.Property(p => p.Version).IsRequired().HasMaxLength(20);
            builder.Property(p => p.SystemPrompt).IsRequired().HasColumnType("nvarchar(max)");
            builder.Property(p => p.UserPromptTemplate).IsRequired().HasColumnType("nvarchar(max)");
            builder.Property(p => p.Temperature).HasPrecision(3, 2);
            builder.Property(p => p.TopP).HasPrecision(3, 2);
            builder.Property(p => p.IsActive).IsRequired();

            // Relationships
            builder.HasOne(p => p.Model)
                   .WithMany(m => m.Prompts)
                   .HasForeignKey(p => p.ModelId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    /// <summary>
    /// Entity configuration for AICallEntity.
    /// Configures AI API call tracking with full audit trail.
    /// </summary>
    public class AICallEntityConfiguration : IEntityTypeConfiguration<AICallEntity>
    {
        public void Configure(EntityTypeBuilder<AICallEntity> builder)
        {
            builder.ToTable("AICalls");

            // Primary key and indexes
            builder.HasKey(a => a.Id);
            builder.HasIndex(a => a.PromptId);
            builder.HasIndex(a => a.UserId);
            builder.HasIndex(a => a.CreatedAt);
            builder.HasIndex(a => a.Success);
            builder.HasIndex(a => new { a.UserId, a.CreatedAt }); // For cost analysis queries

            // Properties
            builder.Property(a => a.ActualSystemPrompt).IsRequired().HasColumnType("nvarchar(max)");
            builder.Property(a => a.ActualUserPrompt).IsRequired().HasColumnType("nvarchar(max)");
            builder.Property(a => a.Response).IsRequired().HasColumnType("nvarchar(max)");
            builder.Property(a => a.InputTokens).IsRequired();
            builder.Property(a => a.OutputTokens).IsRequired();
            builder.Property(a => a.TotalCost).IsRequired().HasPrecision(18, 6);
            builder.Property(a => a.Success).IsRequired();
            builder.Property(a => a.ErrorMessage).HasColumnType("nvarchar(max)");

            // Relationships
            builder.HasOne(a => a.Prompt)
                   .WithMany(p => p.AICalls)
                   .HasForeignKey(a => a.PromptId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.User)
                   .WithMany(u => u.AICalls)
                   .HasForeignKey(a => a.UserId)
                   .OnDelete(DeleteBehavior.SetNull); // Keep AI call records even if user is deleted
        }
    }

    public class VideoTypeEntityConfiguration : IEntityTypeConfiguration<VideoTypeEntity>
    {
        public void Configure(EntityTypeBuilder<VideoTypeEntity> builder)
        {
            builder.ToTable("VideoTypes");

            // Primary key and indexes
            builder.HasKey(vt => vt.Id);
            builder.HasIndex(vt => vt.Code).IsUnique(); // Code must be unique
            builder.HasIndex(vt => vt.Name);

            // Properties
            builder.Property(vt => vt.Name).IsRequired().HasMaxLength(100);
            builder.Property(vt => vt.Description).HasMaxLength(500);
            builder.Property(vt => vt.Code).IsRequired().HasMaxLength(50);

            // Relationships
            builder.HasMany(vt => vt.Videos)
                   .WithOne(v => v.VideoType)
                   .HasForeignKey(v => v.VideoTypeId)
                   .OnDelete(DeleteBehavior.SetNull); // Preserve video if type is deleted
        }
    }

    /// <summary>
    /// Entity configuration for ProjectEntity.
    /// Configures user projects for organizing videos and generating guides.
    /// </summary>
    public class ProjectEntityConfiguration : IEntityTypeConfiguration<ProjectEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectEntity> builder)
        {
            builder.ToTable("Projects");

            // Primary key and indexes
            builder.HasKey(p => p.Id);
            builder.HasIndex(p => p.UserId);
            builder.HasIndex(p => p.Name);
            builder.HasIndex(p => p.CreatedAt);

            // Properties
            builder.Property(p => p.UserId).IsRequired();
            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
            builder.Property(p => p.Description).HasMaxLength(2000);
            builder.Property(p => p.UserGuidance).HasMaxLength(1000);

            // Relationships
            builder.HasOne(p => p.User)
                   .WithMany(u => u.Projects)
                   .HasForeignKey(p => p.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>
    /// Entity configuration for ProjectVideoEntity junction table.
    /// Configures many-to-many relationship between Projects and Videos.
    /// </summary>
    public class ProjectVideoEntityConfiguration : IEntityTypeConfiguration<ProjectVideoEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectVideoEntity> builder)
        {
            builder.ToTable("ProjectVideos");

            // Primary key and indexes
            builder.HasKey(pv => pv.Id);
            builder.HasIndex(pv => new { pv.ProjectId, pv.VideoId }).IsUnique(); // Prevent duplicate videos in same project
            builder.HasIndex(pv => pv.ProjectId);
            builder.HasIndex(pv => pv.VideoId);
            builder.HasIndex(pv => new { pv.ProjectId, pv.Order }); // For ordered retrieval

            // Properties
            builder.Property(pv => pv.ProjectId).IsRequired();
            builder.Property(pv => pv.VideoId).IsRequired();
            builder.Property(pv => pv.Order).IsRequired();
            builder.Property(pv => pv.AddedAt).IsRequired();

            // Relationships
            builder.HasOne(pv => pv.Project)
                   .WithMany(p => p.ProjectVideos)
                   .HasForeignKey(pv => pv.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pv => pv.Video)
                   .WithMany(v => v.ProjectVideos)
                   .HasForeignKey(pv => pv.VideoId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    /// <summary>
    /// Entity configuration for ProjectGuideEntity.
    /// Configures AI-generated guides for projects with 1-to-1 relationship.
    /// </summary>
    public class ProjectGuideEntityConfiguration : IEntityTypeConfiguration<ProjectGuideEntity>
    {
        public void Configure(EntityTypeBuilder<ProjectGuideEntity> builder)
        {
            builder.ToTable("ProjectGuides");

            // Primary key and indexes
            builder.HasKey(pg => pg.Id);
            builder.HasIndex(pg => pg.ProjectId).IsUnique(); // One guide per project
            builder.HasIndex(pg => pg.AICallId);
            builder.HasIndex(pg => pg.GeneratedAt);

            // Properties
            builder.Property(pg => pg.ProjectId).IsRequired();
            builder.Property(pg => pg.Content).IsRequired().HasColumnType("nvarchar(max)");
            builder.Property(pg => pg.UserGuidanceSnapshot).HasMaxLength(1000);
            builder.Property(pg => pg.VideoCount).IsRequired();
            builder.Property(pg => pg.GeneratedAt).IsRequired();

            // Relationships
            builder.HasOne(pg => pg.Project)
                   .WithOne(p => p.ProjectGuide)
                   .HasForeignKey<ProjectGuideEntity>(pg => pg.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pg => pg.AICall)
                   .WithMany(a => a.ProjectGuides)
                   .HasForeignKey(pg => pg.AICallId)
                   .OnDelete(DeleteBehavior.SetNull); // Keep guide even if AI call is deleted
        }
    }
}