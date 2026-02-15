# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
dotnet build
dotnet run
dotnet watch                    # Hot reload during development
```

### Entity Framework Migrations

```bash
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

After generating a migration, review it and update any `defaultValue` fields that default to empty string for required columns.

### No Test Project

This project does not currently have a test project.

## Project Overview

TargetBrowse is a **Blazor Server** (.NET 9.0) application for personalized YouTube content curation. Users track channels, define topics, get AI-powered video suggestions and summaries, and generate scripts from video content. It integrates with YouTube Data API v3, OpenAI API, and Apify (transcript retrieval).

## Architecture

**Vertical Slice Architecture** with shared services:

- **`Features/`** — Each feature is a self-contained vertical slice with its own `Data/`, `Models/`, `Services/`, `Components/`, and `Pages/` subfolders. Features: Channels, ChannelVideos, Projects, RatingHistory, Suggestions, Topics, TopicVideos, Videos, Watch.
- **`Services/`** — Shared cross-cutting services used by multiple features: AI prompts (`Services/AI/`), YouTube API integration (`Services/YouTube/`), data services (`Services/DataServices/`), transcript/summary services, validation, and utilities.
- **`Data/`** — EF Core `ApplicationDbContext`, entity classes (`Data/Entities/`), base repository, and entity configurations (`Data/Configurations/`).
- **`Components/`** — Blazor layout, shared components, account/identity pages, and the app shell (`App.razor`, `Routes.razor`).
- **`Migrations/`** — EF Core migration files.

### Key Patterns

- **Entity → Model → Service → Razor** data flow pattern
- All Razor components use **separate code-behind files** (`.razor` + `.razor.cs`)
- Feature services are registered with full namespace paths in `Program.cs` (e.g., `Features.Channels.Services.IChannelService`)
- Each feature that needs YouTube access has its own specialized YouTube service, all backed by `SharedYouTubeService` and a singleton `YouTubeQuotaManager`
- Database access uses both scoped `DbContext` and `DbContextFactory` (for concurrent operations)
- API keys stored via **User Secrets** in development

### Script Generation Pipeline

The Projects feature includes a 5-phase script generation pipeline:
1. **P2 - Analysis** — AI analyzes video content
2. **P3 - Configure** — User configures script preferences via `UserScriptProfile` (8 dropdown categories + custom instructions)
3. **P4 - Outline** — AI generates script outline
4. **P5 - Script** — AI generates full script
5. **Final Display** — Script presented to user

Key files: `Services/AI/ScriptPromptBuilder.cs`, `Features/Projects/Services/ScriptGenerationService.cs`

## Razor File Conventions

- `.razor` files with code-behind (`.razor.cs`) use **single `@`** for directives (`@bind-Value`, `@(() => ...)`)
- Do NOT use `@@` escaping in these files — the `@@` convention applies only to inline `@` symbols in text content, not to Razor directives
- Use `EditForm` with `DataAnnotationsValidator` for forms (see parent `CLAUDE.md` for pattern)
- Use Bootstrap 5 classes for UI; prefer Blazor built-in components over custom CSS

## Database

SQL Server LocalDB. Query via:

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -d "TargetBrowse" -Q "SELECT ..."
```

## Configuration

- `appsettings.json` contains YouTube API settings (`YouTube` section) and project settings (`ProjectSettings` section)
- Sensitive keys (YouTube API, OpenAI) go in User Secrets
- Connection string: `DefaultConnection` pointing to SQL Server LocalDB

## Service Registration

All services are registered in `Program.cs`. Feature services use fully qualified namespaces. Shared services use interface-based DI. The YouTube quota manager is a **singleton** for global quota tracking; most other services are **scoped**.
