using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TargetBrowse.Data.Configurations;
using TargetBrowse.Data.Entities;

namespace TargetBrowse.Data;

/// <summary>
/// Application database context for YouTube Video Tracker.
/// Includes all entities for user management, content tracking, and AI features.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // YouTube Video Tracker DbSets
    public DbSet<TopicEntity> Topics { get; set; }
    public DbSet<ChannelEntity> Channels { get; set; }
    public DbSet<UserChannelEntity> UserChannels { get; set; }
    public DbSet<VideoEntity> Videos { get; set; }
    public DbSet<UserVideoEntity> UserVideos { get; set; }
    public DbSet<RatingEntity> Ratings { get; set; }
    public DbSet<SuggestionEntity> Suggestions { get; set; }
    public DbSet<SummaryEntity> Summaries { get; set; }
    public DbSet<SummaryGenerationRequestEntity> SummaryGenerationRequests { get; set; }
    public DbSet<SuggestionTopicEntity> SuggestionTopics { get; set; }

    // AI Tracking DbSets
    public DbSet<ModelEntity> Models { get; set; }
    public DbSet<PromptEntity> Prompts { get; set; }
    public DbSet<AICallEntity> AICalls { get; set; }
    public DbSet<VideoTypeEntity> VideoTypes { get; set; }

    // Project DbSets
    public DbSet<ProjectEntity> Projects { get; set; }
    public DbSet<ProjectVideoEntity> ProjectVideos { get; set; }
    public DbSet<ProjectGuideEntity> ProjectGuides { get; set; }

    // Script Generation DbSets
    public DbSet<ScriptContentEntity> ScriptContents { get; set; }
    public DbSet<UserScriptProfileEntity> UserScriptProfiles { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all entity configurations
        builder.ApplyConfiguration(new TopicEntityConfiguration());
        builder.ApplyConfiguration(new ChannelEntityConfiguration());
        builder.ApplyConfiguration(new UserChannelEntityConfiguration());
        builder.ApplyConfiguration(new VideoEntityConfiguration());
        builder.ApplyConfiguration(new UserVideoEntityConfiguration());
        builder.ApplyConfiguration(new RatingEntityConfiguration());
        builder.ApplyConfiguration(new SuggestionEntityConfiguration());
        builder.ApplyConfiguration(new SummaryEntityConfiguration());
        builder.ApplyConfiguration(new SummaryGenerationRequestEntityConfiguration());
        builder.ApplyConfiguration(new SuggestionTopicEntityConfiguration());

        // Apply AI tracking configurations
        builder.ApplyConfiguration(new ModelEntityConfiguration());
        builder.ApplyConfiguration(new PromptEntityConfiguration());
        builder.ApplyConfiguration(new AICallEntityConfiguration());
        builder.ApplyConfiguration(new VideoTypeEntityConfiguration());

        // Apply Project configurations
        builder.ApplyConfiguration(new ProjectEntityConfiguration());
        builder.ApplyConfiguration(new ProjectVideoEntityConfiguration());
        builder.ApplyConfiguration(new ProjectGuideEntityConfiguration());

        // Apply Script Generation configurations
        builder.ApplyConfiguration(new ScriptContentEntityConfiguration());
        builder.ApplyConfiguration(new UserScriptProfileEntityConfiguration());

        // Configure Identity tables to avoid conflicts
        builder.Entity<ApplicationUser>().ToTable("AspNetUsers");
    }

    /// <summary>
    /// Override SaveChanges to automatically set audit fields on BaseEntity instances.
    /// Sets CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy based on entity state.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    /// <summary>
    /// Override SaveChangesAsync to automatically set audit fields on BaseEntity instances.
    /// Sets CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy based on entity state.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Updates audit fields for entities inheriting from BaseEntity.
    /// Called automatically before saving changes.
    /// </summary>
    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is Common.BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (Common.BaseEntity)entry.Entity;
            var now = DateTime.UtcNow;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = now;
                entity.CreatedBy = GetCurrentUserId();
            }

            entity.LastModifiedAt = now;
            entity.LastModifiedBy = GetCurrentUserId();
        }
    }

    /// <summary>
    /// Gets the current user ID for audit fields.
    /// Returns "System" if no user context is available.
    /// </summary>
    private string GetCurrentUserId()
    {
        // This will be enhanced when we add HTTP context access
        // For now, return "System" for system-generated changes
        return "System";
    }
}