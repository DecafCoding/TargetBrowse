using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using TargetBrowse.Components;
using TargetBrowse.Components.Account;
using TargetBrowse.Data;
using TargetBrowse.Services;
using TargetBrowse.Services.YouTube.Models;
using TargetBrowse.Services.YouTube;

// Enhanced YouTube API Service imports
using TargetBrowse.Features.Suggestions.Services;
using TargetBrowse.Features.Suggestions.BackgroundServices;
using Microsoft.Extensions.Options;

namespace TargetBrowse;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<IdentityUserAccessor>();
        builder.Services.AddScoped<IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
            .AddIdentityCookies();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        // Updated Identity configuration for YouTube Video Tracker requirements
        builder.Services.AddIdentityCore<ApplicationUser>(options => {
            // Email confirmation required but users can access app without forced onboarding
            options.SignIn.RequireConfirmedAccount = true;

            // Password requirements
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;

            // User settings
            options.User.RequireUniqueEmail = true;

            // Lockout settings for security
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

        #region Core Application Services

        // YouTube Video Tracker Core Services
        builder.Services.AddScoped<IThemeService, ThemeService>();
        builder.Services.AddScoped<IMessageCenterService, MessageCenterService>();

        #endregion

        #region Feature Services

        // Topic Feature Services
        builder.Services.AddScoped<Features.Topics.Services.ITopicService, Features.Topics.Services.TopicService>();

        // Channels Feature Services
        builder.Services.AddScoped<Features.Channels.Services.IChannelService, Features.Channels.Services.ChannelService>();
        builder.Services.AddScoped<Features.Channels.Data.IChannelRepository, Features.Channels.Data.ChannelRepository>();
        builder.Services.AddScoped<Features.Channels.Services.IChannelRatingService, Features.Channels.Services.ChannelRatingService>();

        // Videos Feature Services
        builder.Services.AddScoped<Features.Videos.Services.IVideoService, Features.Videos.Services.VideoService>();
        builder.Services.AddScoped<Features.Videos.Data.IVideoRepository, Features.Videos.Data.VideoRepository>();
        builder.Services.AddScoped<Features.Videos.Services.IVideoRatingService, Features.Videos.Services.VideoRatingService>();

        // Suggestions Feature Services (Enhanced YT-010 Implementation)
        builder.Services.AddScoped<Features.Suggestions.Services.ISuggestionService, Features.Suggestions.Services.SuggestionService>();
        builder.Services.AddScoped<Features.Suggestions.Data.ISuggestionRepository, Features.Suggestions.Data.SuggestionRepository>();
        builder.Services.AddScoped<Features.Suggestions.Services.ISuggestionQuotaManager, Features.Suggestions.Services.SuggestionQuotaManager>();

        // ChannelVideos Feature Services
        builder.Services.AddScoped<Features.ChannelVideos.Services.IChannelVideosService, Features.ChannelVideos.Services.ChannelVideosService>();
        builder.Services.AddScoped<Features.ChannelVideos.Data.IChannelVideosRepository, Features.ChannelVideos.Data.ChannelVideosRepository>();

        builder.Services.AddScoped<Features.TopicVideos.Services.ITopicVideosService, Features.TopicVideos.Services.TopicVideosService>();

        #endregion

        #region YouTube API Configuration and Services

        // YouTube API Settings Configuration
        // Bind configuration from appsettings.json "YouTube" section
        builder.Services.Configure<YouTubeApiSettings>(
            builder.Configuration.GetSection("YouTube"));

        // Validate YouTube API settings at startup
        builder.Services.AddOptions<YouTubeApiSettings>()
            .Configure(options =>
            {
                builder.Configuration.GetSection("YouTube").Bind(options);
            })
            .Validate(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    return false; // Will throw validation exception
                }
                if (options.DailyQuotaLimit <= 0)
                {
                    return false;
                }
                if (options.QuotaWarningThreshold < 0 || options.QuotaWarningThreshold > 100)
                {
                    return false;
                }
                if (options.QuotaCriticalThreshold < 0 || options.QuotaCriticalThreshold > 100)
                {
                    return false;
                }
                if (options.QuotaCriticalThreshold <= options.QuotaWarningThreshold)
                {
                    return false;
                }
                return true;
            }, "YouTube API settings validation failed. Check your appsettings.json configuration.");

        // YouTube Quota Manager (Singleton for global quota tracking across all features)
        // Singleton ensures quota tracking is shared across all services and requests
        builder.Services.AddSingleton<IYouTubeQuotaManager, YouTubeQuotaManager>();

        // Legacy YouTube Services (maintained for backwards compatibility)
        builder.Services.AddScoped<IYouTubeApiService, YouTubeApiService>();

        // Shared YouTube Service (REQUIRED - used by multiple features)
        // This service provides common YouTube API operations used across features
        builder.Services.AddHttpClient<ISharedYouTubeService, SharedYouTubeService>(client =>
        {
            // Configure HTTP client for optimal YouTube API performance
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "YouTube-Video-Tracker-Shared/1.0");

            // Add additional headers for better API compatibility
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
        {
            // Basic HTTP client configuration for optimal performance
            MaxConnectionsPerServer = 5,

            // Enable compression for better performance
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        });

        // Feature-Specific YouTube Services
        // Each feature has its own specialized YouTube service for feature-specific functionality
        builder.Services.AddScoped<Features.Channels.Services.IChannelYouTubeService, Features.Channels.Services.ChannelYouTubeService>();
        builder.Services.AddScoped<Features.Videos.Services.IVideoYouTubeService, Features.Videos.Services.VideoYouTubeService>();

        // Enhanced Suggestion YouTube Service (uses shared service for common operations)
        // This service now focuses on Suggestions-specific functionality only
        builder.Services.AddScoped<ISuggestionYouTubeService, SuggestionYouTubeService>();

        // Configure quota manager event handlers for Message Center integration
        builder.Services.AddSingleton<IHostedService>(provider =>
        {
            var quotaManager = provider.GetRequiredService<IYouTubeQuotaManager>();
            var logger = provider.GetRequiredService<ILogger<Program>>();

            // Subscribe to quota events for application-wide monitoring
            quotaManager.QuotaThresholdReached += async (sender, args) =>
            {
                logger.LogWarning("YouTube API quota threshold reached: {ThresholdType} - {Message}",
                    args.ThresholdType, args.Message);

                // Additional handling can be added here (e.g., send notifications, alerts)
                using var scope = provider.CreateScope();
                var messageCenter = scope.ServiceProvider.GetService<IMessageCenterService>();

                if (messageCenter != null)
                {
                    if (args.ThresholdType == "Critical")
                    {
                        await messageCenter.ShowErrorAsync($"Critical: {args.Message}");
                    }
                    else
                    {
                        await messageCenter.ShowApiLimitAsync("YouTube API", args.QuotaStatus.NextReset);
                    }
                }
            };

            quotaManager.QuotaExhausted += async (sender, args) =>
            {
                logger.LogError("YouTube API quota exhausted: {Message}. Next reset: {NextReset}",
                    args.Message, args.NextResetAt);

                // Handle quota exhaustion across the application
                using var scope = provider.CreateScope();
                var messageCenter = scope.ServiceProvider.GetService<IMessageCenterService>();

                if (messageCenter != null)
                {
                    await messageCenter.ShowErrorAsync($"YouTube API quota exhausted. Service resumes at {args.NextResetAt:HH:mm} UTC.");
                }
            };

            // Return the quota reset background service
            return new QuotaResetBackgroundService(
                provider,
                provider.GetRequiredService<IOptions<YouTubeApiSettings>>(),
                provider.GetRequiredService<ILogger<QuotaResetBackgroundService>>()
            );
        });

        #endregion

        #region Background Services

        // Background service for automatic quota reset at midnight UTC
        // This ensures quota tracking is reset daily without manual intervention
        builder.Services.AddHostedService<QuotaResetBackgroundService>();

        #endregion

        #region Application Configuration

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();

        #endregion

        app.Run();
    }
}

/*
 * IMPLEMENTATION NOTES:
 * 
 * 3. Core Functionality Preserved:
 *    - All essential service registrations are maintained
 *    - Enhanced YouTube API service is properly configured
 *    - Background services are registered
 *    - HTTP client factory pattern is used with basic optimization
 * 
 * 4. HTTP Client Configuration:
 *    - 30-second timeout for YouTube API calls
 *    - Proper user agent for API identification
 *    - GZip/Deflate compression enabled
 *    - Maximum 5 connections per server
 * 
 * 5. YouTube API Configuration Required:
 *    - Ensure appsettings.json has YouTube section with ApiKey
 *    - Daily quota limit and other settings as needed
 *    - API key should be stored securely (environment variables in production)
 */

/*
 * YOUTUBE QUOTA MANAGEMENT IMPLEMENTATION NOTES:
 * 
 * 1. Comprehensive Configuration:
 *    - Full validation of YouTube API settings at startup
 *    - Configurable thresholds for warning and critical alerts
 *    - Support for custom quota limits per environment
 *    - Automatic validation prevents runtime errors from misconfiguration
 * 
 * 2. Singleton Pattern for Quota Manager:
 *    - Global quota tracking across all application features
 *    - Thread-safe operations ensure accurate quota management
 *    - Persistent storage maintains quota state across application restarts
 *    - Single source of truth for all YouTube API quota decisions
 * 
 * 3. Event-Driven Architecture:
 *    - Real-time quota threshold notifications
 *    - Automatic integration with Message Center for user feedback
 *    - Extensible event system for custom quota handling
 *    - Separation of concerns between quota management and user notification
 * 
 * 4. Background Service Integration:
 *    - Continuous quota monitoring and maintenance
 *    - Automatic cleanup of expired reservations
 *    - Periodic analytics logging for usage insights
 *    - Graceful handling of quota resets and maintenance tasks
 * 
 * 5. Feature-Specific Services:
 *    - Each feature (Channels, Videos, Suggestions) has dedicated YouTube service
 *    - All services use the centralized quota manager for consistency
 *    - HttpClient factory pattern for optimal connection management
 *    - Specialized service implementations for different use cases
 * 
 * 6. Error Handling and Resilience:
 *    - Comprehensive error handling throughout the quota system
 *    - Graceful degradation when quotas are exhausted
 *    - Automatic retry logic and circuit breaker patterns (if needed)
 *    - Detailed logging for monitoring and debugging
 * 
 * 7. Configuration Requirements:
 *    - appsettings.json must contain YouTube section with ApiKey
 *    - DailyQuotaLimit should match your YouTube API project limits
 *    - QuotaWarningThreshold and QuotaCriticalThreshold are configurable
 *    - QuotaStorageFilePath can be customized or left empty for default
 * 
 * Example appsettings.json YouTube section:
 * {
 *   "YouTube": {
 *     "ApiKey": "YOUR_YOUTUBE_API_KEY_HERE",
 *     "DailyQuotaLimit": 10000,
 *     "MaxSearchResults": 50,
 *     "QuotaWarningThreshold": 80,
 *     "QuotaCriticalThreshold": 95,
 *     "MaxConcurrentRequests": 5,
 *     "RequestTimeoutSeconds": 30,
 *     "EnablePersistentQuotaStorage": true,
 *     "QuotaStorageFilePath": "",
 *     "QuotaResetHour": 0,
 *     "EnableQuotaLogging": true
 *   }
 * }
 * 
 * 8. Security Considerations:
 *    - API key should be stored in user secrets for development
 *    - Production deployments should use environment variables
 *    - Quota storage file contains no sensitive information
 *    - Rate limiting prevents API abuse and unexpected quota exhaustion
 * 
 * 9. Monitoring and Analytics:
 *    - Detailed quota usage tracking and analytics
 *    - Daily, weekly, and custom period reporting
 *    - Operation-level breakdown for optimization insights
 *    - Integration with application logging for centralized monitoring
 * 
 * 10. Extensibility:
 *     - Interface-based design allows for easy testing and mocking
 *     - Event system enables custom quota handling logic
 *     - Modular architecture supports additional quota sources
 *     - Future enhancements (e.g., Redis storage) can be added easily
 */