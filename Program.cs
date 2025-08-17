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

        // YouTube Video Tracker Services
        builder.Services.AddScoped<IThemeService, ThemeService>();
        builder.Services.AddScoped<IMessageCenterService, MessageCenterService>();

        // Topic Feature Services
        builder.Services.AddScoped<Features.Topics.Services.ITopicService, Features.Topics.Services.TopicService>();

        // Channels Feature Services
        builder.Services.AddScoped<Features.Channels.Services.IChannelService, Features.Channels.Services.ChannelService>();
        builder.Services.AddScoped<Features.Channels.Data.IChannelRepository, Features.Channels.Data.ChannelRepository>();

        // Videos Feature Services
        builder.Services.AddScoped<Features.Videos.Services.IVideoService, Features.Videos.Services.VideoService>();
        builder.Services.AddScoped<Features.Videos.Data.IVideoRepository, Features.Videos.Data.VideoRepository>();

        // YouTube API Configuration
        builder.Services.Configure<YouTubeApiSettings>(
            builder.Configuration.GetSection("YouTube"));

        // YouTube Services
        builder.Services.AddScoped<IYouTubeApiService, YouTubeApiService>(); // Main API service (to be deprecated when all features have their own)
        builder.Services.AddScoped<Features.Channels.Services.IChannelYouTubeService, Features.Channels.Services.ChannelYouTubeService>(); // For Channels feature
        builder.Services.AddScoped<Features.Videos.Services.IVideoYouTubeService, Features.Videos.Services.VideoYouTubeService>(); // For Videos feature

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

        app.Run();
    }
}