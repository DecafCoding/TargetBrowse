# Changes: JSON Summary Format with Simplified HTML

## Overview

This update modifies the video summary feature to return JSON responses with both a short summary and a simplified HTML long summary.

## Changes Made

### 1. Database Layer Updates

**File:** `Services/Interfaces/ISummaryDataService.cs`
- Updated `CreateSummaryAsync` signature to accept both `content` and `summary` parameters
- `content`: The detailed HTML summary (max 4000 chars)
- `summary`: The short text summary (max 1000 chars)

**File:** `Services/DataServices/SummaryDataService.cs`
- Updated implementation of `CreateSummaryAsync` to set both `Content` and `Summary` fields
- Updated logging to show both content and summary lengths

### 2. Service Layer Updates

**File:** `Services/TranscriptSummaryService.cs`
- Changed `ResponseFormat` from `"text"` to `"json_object"` for OpenAI API calls
- Added `SummaryResponse` class to parse JSON response with two fields:
  - `short_summary`: Single paragraph summary
  - `long_summary`: Simplified HTML summary
- Updated `ParseSummaryResponse` method to:
  - Parse JSON response instead of plain text
  - Extract both summary fields
  - Return `SummaryResponse` object
- Updated `CreateSummaryAsync` call to pass both summaries:
  - `LongSummary` → stored in `Content` field
  - `ShortSummary` → stored in `Summary` field

### 3. Prompt Updates

**File:** `docs/new-entertainment-summary-prompt.md`
- Created new prompt template requiring JSON output
- System Prompt: "You are an AI assistant that creates video summaries in JSON format."
- User Prompt includes:
  - Instructions for creating moment-by-moment summaries
  - JSON output format specification
  - Example output showing both fields
  - HTML restricted to `<h1>` through `<h6>` and `<p>` tags only (no Bootstrap)

**File:** `docs/update-entertainment-summary-prompt.sql`
- SQL script to update the "Entertainment Summary" prompt in database
- Deactivates old version (sets `IsActive = 0`)
- Inserts new version 2.0 with updated prompts
- Includes verification query

## How It Works

1. **Request**: TranscriptSummaryService sends prompt to OpenAI requesting JSON format
2. **Response**: OpenAI returns JSON with two fields:
   ```json
   {
     "short_summary": "A brief 2-3 sentence overview...",
     "long_summary": "<h1>Video Title</h1><p>Details...</p>..."
   }
   ```
3. **Storage**:
   - `short_summary` → `Summaries.Summary` field (1000 chars)
   - `long_summary` → `Summaries.Content` field (4000 chars)
4. **Display**: Watch page displays `Content` field as HTML (existing behavior)

## HTML Format Changes

### Before
- Complex Bootstrap layout with cards, badges, and utility classes
- Example: `<div class="card mb-3"><div class="card-header bg-danger text-white">...</div></div>`

### After
- Simple semantic HTML using only heading and paragraph tags
- Example: `<h1>Video Title</h1><p>Description...</p><h2>Moment 1</h2><p>Details...</p>`

## Database Update Required

Before deploying this change, you must update the prompt in the database:

```bash
# Connect to your database and run:
sqlcmd -S your_server -d your_database -i docs/update-entertainment-summary-prompt.sql
```

Or manually execute the SQL in `docs/update-entertainment-summary-prompt.sql`

## Testing

To test the changes:

1. Run the SQL script to update the prompt
2. Build and run the application
3. Navigate to a video's watch page
4. Click "Generate Summary" (or "Summary" if already exists)
5. Verify the summary displays with simple HTML formatting
6. Check the database to confirm both `Summary` and `Content` fields are populated

## Backward Compatibility

- Existing summaries (with old HTML format) will continue to display correctly
- The `Summary` field may be empty for old records (will be empty string)
- New summaries will have both fields populated

## Notes

- The `Summary` field (short summary) is not currently displayed in the UI
- This field can be used in the future for:
  - Video cards/thumbnails
  - Search results
  - Preview text
  - Mobile views
