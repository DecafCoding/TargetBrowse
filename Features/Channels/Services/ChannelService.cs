using TargetBrowse.Features.Channels.Models;
using TargetBrowse.Services;

namespace TargetBrowse.Features.Channels.Services;

/// <summary>
/// Dummy implementation of channel service for UI testing and review.
/// Provides realistic mock data and simulated delays to demonstrate UI behavior.
/// This will be replaced with real implementation in the next phase.
/// </summary>
public class ChannelService : IChannelService
{
    private readonly IMessageCenterService _messageCenterService;
    private readonly ILogger<ChannelService> _logger;

    // Mock data stores for demonstration
    private static readonly List<ChannelDisplayModel> _mockSearchDatabase = new();
    private static readonly Dictionary<string, List<ChannelDisplayModel>> _userTrackedChannels = new();

    static ChannelService()
    {
        InitializeMockData();
    }

    public ChannelService(IMessageCenterService messageCenterService, ILogger<ChannelService> logger)
    {
        _messageCenterService = messageCenterService;
        _logger = logger;
    }

    /// <summary>
    /// Simulates YouTube channel search with realistic results and delays.
    /// </summary>
    public async Task<List<ChannelDisplayModel>> SearchChannelsAsync(string searchQuery)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return new List<ChannelDisplayModel>();

            // Simulate API delay
            await Task.Delay(Random.Shared.Next(800, 2000));

            // Filter mock data based on search query
            var results = _mockSearchDatabase
                .Where(c => c.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                           c.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            // If no matches, generate dynamic results based on search term
            if (!results.Any())
            {
                results = GenerateDynamicSearchResults(searchQuery);
            }

            _logger.LogInformation("Mock search for '{SearchQuery}' returned {ResultCount} channels", searchQuery, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during mock channel search for query: {SearchQuery}", searchQuery);
            await _messageCenterService.ShowErrorAsync("Search failed. Please try again.");
            return new List<ChannelDisplayModel>();
        }
    }

    /// <summary>
    /// Simulates adding a channel to user's tracking list with validation.
    /// </summary>
    public async Task<bool> AddChannelToTrackingAsync(string userId, AddChannelModel channelModel)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                await _messageCenterService.ShowErrorAsync("User authentication required.");
                return false;
            }

            if (!channelModel.IsValid())
            {
                await _messageCenterService.ShowErrorAsync("Invalid channel data.");
                return false;
            }

            // Simulate processing delay
            await Task.Delay(Random.Shared.Next(500, 1200));

            // Check current tracking count
            var currentCount = await GetTrackedChannelCountAsync(userId);
            if (currentCount >= 50)
            {
                await _messageCenterService.ShowWarningAsync("You have reached the maximum limit of 50 tracked channels. Remove some channels before adding new ones.");
                return false;
            }

            // Check for duplicates
            if (await IsChannelTrackedAsync(userId, channelModel.YouTubeChannelId))
            {
                await _messageCenterService.ShowWarningAsync($"Channel '{channelModel.Name}' is already in your tracking list.");
                return false;
            }

            // Add to mock tracking list
            if (!_userTrackedChannels.ContainsKey(userId))
            {
                _userTrackedChannels[userId] = new List<ChannelDisplayModel>();
            }

            var trackedChannel = new ChannelDisplayModel
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = channelModel.YouTubeChannelId,
                Name = channelModel.Name,
                Description = channelModel.Description ?? "No description available",
                ThumbnailUrl = channelModel.ThumbnailUrl,
                SubscriberCount = channelModel.SubscriberCount,
                VideoCount = channelModel.VideoCount,
                PublishedAt = channelModel.PublishedAt,
                TrackedSince = DateTime.UtcNow,
                IsTracked = true
            };

            _userTrackedChannels[userId].Add(trackedChannel);

            await _messageCenterService.ShowSuccessAsync($"Channel '{channelModel.Name}' added to your tracking list!");
            _logger.LogInformation("User {UserId} added channel: {ChannelName}", userId, channelModel.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding channel {ChannelName} for user {UserId}", channelModel.Name, userId);
            await _messageCenterService.ShowErrorAsync("Failed to add channel. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Simulates removing a channel from user's tracking list.
    /// </summary>
    public async Task<bool> RemoveChannelFromTrackingAsync(string userId, Guid channelId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                await _messageCenterService.ShowErrorAsync("User authentication required.");
                return false;
            }

            // Simulate processing delay
            await Task.Delay(Random.Shared.Next(300, 800));

            if (!_userTrackedChannels.ContainsKey(userId))
            {
                await _messageCenterService.ShowWarningAsync("Channel not found in your tracking list.");
                return false;
            }

            var channel = _userTrackedChannels[userId].FirstOrDefault(c => c.Id == channelId);
            if (channel == null)
            {
                await _messageCenterService.ShowWarningAsync("Channel not found in your tracking list.");
                return false;
            }

            _userTrackedChannels[userId].Remove(channel);

            await _messageCenterService.ShowSuccessAsync($"Channel '{channel.Name}' removed from your tracking list.");
            _logger.LogInformation("User {UserId} removed channel: {ChannelName}", userId, channel.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing channel {ChannelId} for user {UserId}", channelId, userId);
            await _messageCenterService.ShowErrorAsync("Failed to remove channel. Please try again.");
            return false;
        }
    }

    /// <summary>
    /// Gets all tracked channels for a user.
    /// </summary>
    public async Task<List<ChannelDisplayModel>> GetTrackedChannelsAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
                return new List<ChannelDisplayModel>();

            // Simulate loading delay
            await Task.Delay(Random.Shared.Next(200, 600));

            if (!_userTrackedChannels.ContainsKey(userId))
            {
                // Initialize with some sample data for demo
                _userTrackedChannels[userId] = GenerateInitialTrackedChannels();
            }

            var channels = _userTrackedChannels[userId]
                .OrderByDescending(c => c.TrackedSince)
                .ToList();

            _logger.LogDebug("Retrieved {ChannelCount} tracked channels for user {UserId}", channels.Count, userId);
            return channels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tracked channels for user {UserId}", userId);
            await _messageCenterService.ShowErrorAsync("Failed to load tracked channels. Please refresh the page.");
            return new List<ChannelDisplayModel>();
        }
    }

    /// <summary>
    /// Gets the count of tracked channels for a user.
    /// </summary>
    public async Task<int> GetTrackedChannelCountAsync(string userId)
    {
        try
        {
            await Task.Delay(50); // Minimal delay for count queries

            if (!_userTrackedChannels.ContainsKey(userId))
                return 0;

            return _userTrackedChannels[userId].Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracked channel count for user {UserId}", userId);
            return 0;
        }
    }

    /// <summary>
    /// Checks if a channel is already tracked by the user.
    /// </summary>
    public async Task<bool> IsChannelTrackedAsync(string userId, string youTubeChannelId)
    {
        try
        {
            await Task.Delay(50); // Minimal delay for existence checks

            if (!_userTrackedChannels.ContainsKey(userId))
                return false;

            return _userTrackedChannels[userId]
                .Any(c => c.YouTubeChannelId.Equals(youTubeChannelId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if channel {ChannelId} is tracked for user {UserId}", youTubeChannelId, userId);
            return false;
        }
    }

    /// <summary>
    /// Gets detailed information about a specific YouTube channel.
    /// </summary>
    public async Task<ChannelDisplayModel?> GetChannelDetailsAsync(string youTubeChannelId)
    {
        try
        {
            // Simulate API delay
            await Task.Delay(Random.Shared.Next(300, 800));

            var channel = _mockSearchDatabase
                .FirstOrDefault(c => c.YouTubeChannelId.Equals(youTubeChannelId, StringComparison.OrdinalIgnoreCase));

            if (channel != null)
            {
                _logger.LogDebug("Retrieved details for channel {ChannelId}", youTubeChannelId);
            }

            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel details for {ChannelId}", youTubeChannelId);
            return null;
        }
    }

    /// <summary>
    /// Initializes the mock search database with realistic channel data.
    /// </summary>
    private static void InitializeMockData()
    {
        _mockSearchDatabase.AddRange(new[]
        {
            new ChannelDisplayModel
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UC_x5XG1OV2P6uZZ5FSM9Ttw",
                Name = "Google for Developers",
                Description = "Google for Developers is where you can discover all the ways you can use Google developer tools, platforms, and APIs to create amazing experiences for users and grow your business.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/4285F4/FFFFFF?text=G",
                SubscriberCount = 2340000,
                VideoCount = 4500,
                PublishedAt = DateTime.UtcNow.AddYears(-8)
            },
            new ChannelDisplayModel
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UCRPMAPhquse1Ah79DI90rNg",
                Name = "Microsoft Visual Studio",
                Description = "The official channel for Microsoft Visual Studio. Tips, tricks, and tutorials for developers using Visual Studio and .NET technologies.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/5C2D91/FFFFFF?text=VS",
                SubscriberCount = 890000,
                VideoCount = 2100,
                PublishedAt = DateTime.UtcNow.AddYears(-6)
            },
            new ChannelDisplayModel
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UC0vBXGSyV14uvJ4hECDOl0Q",
                Name = "Traversy Media",
                Description = "Web development and programming tutorials. HTML, CSS, JavaScript, React, Node.js and more. Practical projects and tips for developers.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/FF6B35/FFFFFF?text=TM",
                SubscriberCount = 2100000,
                VideoCount = 1200,
                PublishedAt = DateTime.UtcNow.AddYears(-12)
            },
            new ChannelDisplayModel
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UCWv7vMbMWH4-V0ZXdmDpPBA",
                Name = "Programming with Mosh",
                Description = "Code with Mosh is a platform for learning programming and software engineering skills. Clear, concise tutorials for modern developers.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/1DB954/FFFFFF?text=M",
                SubscriberCount = 3200000,
                VideoCount = 850,
                PublishedAt = DateTime.UtcNow.AddYears(-9)
            },
            new ChannelDisplayModel
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UCMXOVXQVkjH-23FBjKDbMzA",
                Name = "TED",
                Description = "The TED Talks channel features the best talks and performances from the TED Conference, where the world's leading thinkers give the talk of their lives.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/E62B1E/FFFFFF?text=TED",
                SubscriberCount = 22500000,
                VideoCount = 5200,
                PublishedAt = DateTime.UtcNow.AddYears(-15)
            },
            new ChannelDisplayModel
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UCJbPGzawDH1njbqV-D5HqKw",
                Name = "thenewboston",
                Description = "Educational videos on programming, web development, and computer science. Thousands of free tutorials to help you learn to code.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/2E7D32/FFFFFF?text=TNB",
                SubscriberCount = 2700000,
                VideoCount = 4300,
                PublishedAt = DateTime.UtcNow.AddYears(-13)
            }
        });
    }

    /// <summary>
    /// Generates dynamic search results when no predefined matches are found.
    /// </summary>
    private static List<ChannelDisplayModel> GenerateDynamicSearchResults(string searchQuery)
    {
        var results = new List<ChannelDisplayModel>();
        var random = new Random();
        var resultCount = random.Next(2, 6);

        for (int i = 0; i < resultCount; i++)
        {
            var subscriberCount = (ulong)random.Next(1000, 5_000_000);
            var videoCount = (ulong)random.Next(50, 2000);
            var channelAge = random.Next(1, 10);

            results.Add(new ChannelDisplayModel
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = $"UC{Guid.NewGuid().ToString("N")[..22]}",
                Name = $"{searchQuery} {GetChannelSuffix(i)}",
                Description = $"This channel focuses on {searchQuery.ToLower()} content and provides educational videos, tutorials, and insights for viewers interested in {searchQuery.ToLower()}. Regular uploads with high-quality content.",
                ThumbnailUrl = $"https://via.placeholder.com/88x88/{GetRandomColor()}/FFFFFF?text={searchQuery.Take(2).ToArray()[0]}",
                SubscriberCount = subscriberCount,
                VideoCount = videoCount,
                PublishedAt = DateTime.UtcNow.AddYears(-channelAge),
                IsTracked = false
            });
        }

        return results;
    }

    /// <summary>
    /// Generates initial tracked channels for new users to demonstrate the UI.
    /// </summary>
    private static List<ChannelDisplayModel> GenerateInitialTrackedChannels()
    {
        return new List<ChannelDisplayModel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UC_x5XG1OV2P6uZZ5FSM9Ttw",
                Name = "Google for Developers",
                Description = "Google for Developers is where you can discover all the ways you can use Google developer tools, platforms, and APIs.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/4285F4/FFFFFF?text=G",
                SubscriberCount = 2340000,
                VideoCount = 4500,
                PublishedAt = DateTime.UtcNow.AddYears(-8),
                TrackedSince = DateTime.UtcNow.AddDays(-45),
                IsTracked = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UCRPMAPhquse1Ah79DI90rNg",
                Name = "Microsoft Visual Studio",
                Description = "The official channel for Microsoft Visual Studio. Tips, tricks, and tutorials for developers.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/5C2D91/FFFFFF?text=VS",
                SubscriberCount = 890000,
                VideoCount = 2100,
                PublishedAt = DateTime.UtcNow.AddYears(-6),
                TrackedSince = DateTime.UtcNow.AddDays(-23),
                IsTracked = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UC0vBXGSyV14uvJ4hECDOl0Q",
                Name = "Traversy Media",
                Description = "Web development and programming tutorials. HTML, CSS, JavaScript, React, Node.js and more.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/FF6B35/FFFFFF?text=TM",
                SubscriberCount = 2100000,
                VideoCount = 1200,
                PublishedAt = DateTime.UtcNow.AddYears(-12),
                TrackedSince = DateTime.UtcNow.AddDays(-12),
                IsTracked = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                YouTubeChannelId = "UCWv7vMbMWH4-V0ZXdmDpPBA",
                Name = "Programming with Mosh",
                Description = "Code with Mosh is a platform for learning programming and software engineering skills.",
                ThumbnailUrl = "https://via.placeholder.com/88x88/1DB954/FFFFFF?text=M",
                SubscriberCount = 3200000,
                VideoCount = 850,
                PublishedAt = DateTime.UtcNow.AddYears(-9),
                TrackedSince = DateTime.UtcNow.AddDays(-5),
                IsTracked = true
            }
        };
    }

    /// <summary>
    /// Helper method to get channel name suffixes for generated results.
    /// </summary>
    private static string GetChannelSuffix(int index)
    {
        var suffixes = new[] { "Official", "Academy", "Tutorials", "Hub", "Channel", "TV", "Studio", "Learning" };
        return suffixes[index % suffixes.Length];
    }

    /// <summary>
    /// Helper method to get random colors for placeholder thumbnails.
    /// </summary>
    private static string GetRandomColor()
    {
        var colors = new[] { "0066CC", "E91E63", "9C27B0", "673AB7", "3F51B5", "2196F3", "03A9F4", "00BCD4", "009688", "4CAF50", "8BC34A", "CDDC39", "FF9800", "FF5722" };
        return colors[Random.Shared.Next(colors.Length)];
    }
}