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

        #endregion

        #region YouTube API Configuration and Services

        // YouTube API Settings Configuration
        builder.Services.Configure<YouTubeApiSettings>(
            builder.Configuration.GetSection("YouTube"));

        // YouTube Quota Manager (Singleton for global quota tracking across all features)
        builder.Services.AddSingleton<IYouTubeQuotaManager, YouTubeQuotaManager>();

        // Legacy YouTube Services (maintained for backwards compatibility)
        builder.Services.AddScoped<IYouTubeApiService, YouTubeApiService>(); // Main API service (to be deprecated when all features have their own)

        // Feature-Specific YouTube Services
        builder.Services.AddScoped<Features.Channels.Services.IChannelYouTubeService, Features.Channels.Services.ChannelYouTubeService>(); // For Channels feature
        builder.Services.AddScoped<Features.Videos.Services.IVideoYouTubeService, Features.Videos.Services.VideoYouTubeService>(); // For Videos feature

        // Enhanced Suggestion YouTube Service with HttpClient Factory (YT-010-02 Implementation)
        builder.Services.AddHttpClient<ISuggestionYouTubeService, SuggestionYouTubeService>(client =>
        {
            // Configure HTTP client for optimal YouTube API performance
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "YouTube-Video-Tracker-Suggestions/1.0");

            // Add additional headers for better API compatibility
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
        {
            // Basic HTTP client configuration
            MaxConnectionsPerServer = 5,

            // Enable compression for better performance
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
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
 * 1. Removed Advanced HTTP Client Features:
 *    - PooledConnectionLifetime and PooledConnectionIdleTimeout are not available in standard .NET
 *    - These are .NET 8+ features that may not be available in your version
 *    - The basic HttpClientHandler configuration is sufficient for most scenarios
 * 
 * 2. Removed Polly Policy Integration:
 *    - Retry and circuit breaker policies require additional NuGet packages
 *    - The enhanced SuggestionYouTubeService has built-in error handling and retry logic
 *    - Polly integration is optional and can be added later if needed
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