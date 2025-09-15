# YouTube Video Tracker

A personalized YouTube curation platform that learns from your viewing preferences, ratings, and interests to suggest truly relevant content. Unlike YouTube's algorithm, users have full control over their content discovery through explicit ratings, topic preferences, and channel management, with AI-powered summaries to help decide what's worth watching.

## Problem Statement

In my opinion YouTube's recommendation algorithm fails at several points by:

- Over emphasizes the topic of the most recent video watched
- Constantly recommends games and shorts with no way to turn off permanantly
- Recommends video you have already watched
- Recommends partially watched videos
- Pulls recommendations from you "Watch Later" list

This causes you to waste time browsing through irrelevant suggestions or miss valuable content from channels they care about. This application provides:

- Track and organize valuable content for future reference
- Build a personalized rating system that improves over time
- Get concise summaries before committing time to watch
- Maintain focus on specific learning topics without algorithmic distractions

## Features

### Core Features (MVP)
- **User Authentication** - Secure account creation and session management
- **Topic Management** - Define up to 10 learning topics for personalized content discovery
- **Channel Tracking** - Follow up to 50 YouTube channels for new video notifications
- **Video Library** - Save and organize specific videos of interest
- **Rating System** - Rate channels and videos (1-5 stars) with explanatory notes
- **Smart Suggestions** - Manual suggestion generation combining topic searches and channel updates
- **AI Summaries** - Get 1-2 paragraph summaries of video content (10 daily limit)
- **Content Decisions** - Mark videos as "watched" or "skip" based on summaries

### Intelligent Features
- **Unified Scoring Algorithm** - Combines channel ratings (60%), topic relevance (25%), and recency (15%)
- **Dual-Source Detection** - Identifies videos found through both channel tracking AND topic searches
- **Smart Filtering** - Automatically excludes content from 1-star rated channels
- **Source Transparency** - Clear indicators showing why each video was suggested

## Technology Stack

- **Framework**: ASP.NET Core 8.0 with Blazor Server
- **Database**: SQL Server LocalDB with Entity Framework Core
- **Authentication**: ASP.NET Core Identity
- **UI Framework**: Bootstrap 5 with responsive design
- **APIs**: 
  - YouTube Data API v3 (content discovery)
  - OpenAI API (video summarization with gpt-4o-mini)
  - Apify Integration (transcript retrieval)

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/)
- SQL Server LocalDB (included with Visual Studio)
- YouTube Data API v3 Key
- OpenAI API Key
- Apify Account (for transcript services)

## Usage Guide

### Getting Started
1. **Create Account** - Register with email and password
2. **Add Topics** - Define up to 10 learning interests (e.g., "Machine Learning", "Cooking")
3. **Track Channels** - Add YouTube channels you want to follow (up to 50)
4. **Rate Content** - Rate channels and videos to improve suggestions

### Generating Suggestions
1. Click **"Get New Suggestions"** button on the dashboard
2. System searches your tracked channels for new videos since last check
3. System searches YouTube for videos matching your topics from the last 7 days
4. View suggestions with clear source indicators:
   - **Channel Update** - New video from tracked channel
   - **Topic Match** - Video found via topic search
   - **Channel + Topic** - Video found via both sources (highest priority)

### Using AI Summaries
1. **Approve** interesting suggestions to add them to your library
2. Click **"Generate Summary"** on approved videos
3. Read 1-2 paragraph summary revealing actual content beyond clickbait
4. Mark as **"Watch"** or **"Skip"** based on summary

### Rating System
- Rate channels and videos 1-5 stars with explanatory notes
- 1-star channels are automatically excluded from future suggestions
- Higher-rated channels get priority in suggestion scoring

## Project Structure

```
YouTubeTracker/
├── YouTubeTracker.Web/                 # Main Blazor Server Application
│   ├── Features/                       # Feature-based organization
│   │   ├── Authentication/             # User management
│   │   ├── Topics/                     # Topic management
│   │   ├── Channels/                   # Channel tracking
│   │   ├── Videos/                     # Video library
│   │   ├── Ratings/                    # Rating system
│   │   ├── Suggestions/                # Suggestion engine
│   │   └── Summaries/                  # AI summarization
│   ├── Shared/
│   │   ├── Services/                   # Cross-cutting services
│   │   ├── Components/                 # Reusable UI components
│   │   └── Models/                     # Shared data models
│   ├── Data/
│   │   ├── ApplicationDbContext.cs     # EF Core context
│   │   ├── Entities/                   # Database entities
│   │   └── Migrations/                 # Database migrations
│   └── appsettings.json                # Configuration
└── YouTubeTracker.Shared/              # Shared library
    ├── Prompts/                        # AI prompt templates
    ├── Integration/                    # External API classes
    └── Models/                         # Shared models
```

## API Limits & Quotas

### Daily Limits (Per User)
- **Video Summaries**: 10 per day (resets at midnight UTC)
- **Tracked Channels**: 50 maximum
- **Topics**: 10 maximum
- **Pending Suggestions**: 100 maximum (auto-cleanup after 30 days)

### API Rate Limiting
- YouTube Data API v3: Managed through application-level quotas
- OpenAI API: Integrated with summary daily limits
- Suggestions are generated manually by users (no background processing)

## Key Features Explained

### Unified Scoring Algorithm
Each suggested video receives a score based on:
- **Channel Rating (60%)**: Your explicit 1-5 star rating of the channel
- **Topic Relevance (25%)**: How well the video title matches your topics
- **Recency (15%)**: Newer videos get higher scores
- **Dual-Source Bonus**: +1.0 bonus for videos found via both channels AND topics

### Smart Deduplication
The system intelligently identifies when the same video is discovered through multiple sources (both channel tracking and topic searches), giving these videos priority as they represent the highest-quality recommendations.

### Message Center
Centralized notification system for:
- Success confirmations for user actions
- Error messages for failed operations
- API limit notifications
- Daily limit warnings

## Development

### Architecture
The application follows **Vertical Slice Architecture** principles:
- Features are organized by business capability
- Each feature contains its own models, services, and UI components
- Shared services handle cross-cutting concerns
- Clean separation between data access, business logic, and presentation

### Key Services
- **ISuggestionService**: Core suggestion generation with unified scoring
- **IYouTubeService**: YouTube API integration for content discovery
- **ISummaryService**: OpenAI integration for video summarization
- **IMessageCenterService**: User feedback and notification system
- **IRatingService**: Content rating and preference management

### Testing Strategy
- Unit tests for critical business logic
- Integration tests for API services
- Component tests for UI interactions
- End-to-end tests for core user workflows

## Roadmap

### Planned Enhancements
- **AI-Powered Thumbnail Analysis** (YT-026) - Analyze video thumbnails to improve suggestion accuracy
- **Advanced Search & Filtering** - Enhanced content discovery capabilities
- **Mobile App** - Native mobile application
- **Advanced Analytics** - User behavior insights and recommendation analytics
- **Premium Features** - Unlimited tracking, priority processing, advanced analytics

### Future Integrations
- Additional video platforms beyond YouTube
- Social features for sharing recommendations
- Integration with learning management systems
- Browser extension for seamless YouTube integration

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 External Resources

- [YouTube Data API v3 Documentation](https://developers.google.com/youtube/v3)
- [OpenAI API Documentation](https://platform.openai.com/docs)
- [ASP.NET Core Blazor Documentation](https://docs.microsoft.com/en-us/aspnet/core/blazor/)
- [Entity Framework Core Documentation](https://docs.microsoft.com/en-us/ef/core/)
