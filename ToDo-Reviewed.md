# TargetBrowse — ToDo Review

Items from `ToDo.md` organized by category, followed by new suggestions based on codebase review and competitive research.

---

# Part 1: Existing ToDo Items

## Bugs

- [ ] **Video title special characters** — Titles with HTML entities display incorrectly (e.g., `&#39;` instead of apostrophe). Need to use `HtmlDecode` on display.

---

## UI / Styling

### Video Titles (Cross-Feature)
- [ ] **Link video titles to watch page** on Topics/{id}, Channel-Videos, and Video-Library pages. Remove any icons appended to the title.
- [ ] **HTML-decode video titles** — Use `@((MarkupString)WebUtility.HtmlDecode(...))` for display.
- [ ] **Remove "Watch" button from Video-Library cards** — Redundant once title links to watch page.
- [ ] **Remove "Play" button** — Link thumbnail and title to watch page instead.

### Form Consistency
- [ ] **Left-side form uniformity** — Standardize form sizing (sm vs default) across pages.

### Buttons & Icons
- [ ] **Standardize button styles across features:**
  - **Save to Library** — Blue solid, circle-plus icon, "Library" text
  - **Watch on YouTube** — "YouTube" text, `box-arrow-up-right` icon
  - **Skip** — `eye-slash` or `hand-thumbs-down` icon
  - **Mark Watched** — (define icon)
  - **Rating** — Choose from: fire, battery, flask, gem, lightning, rocket
  - **Suggestions:** Approve (solid green), Pass (pink outline), Play (blue outline)
  - **Video-Library:** Mark Watched (green outline), Skip (red outline), Watch (gray outline), Add to Project (blue outline)
  - **Topics/{id}/Videos:** Save to Library (solid blue, full width)
  - **Video-Search:** Save to Library (solid blue, full width), Watch on YouTube (outline gray, full width)

### Component Cleanup
- [ ] **ProjectGuideDisplay** — Remove inline CSS from the `.razor` file.

---

## Feature Enhancements

### Suggestions
- [ ] **Filter/sort not functioning** — Fix broken filter and sort on the Suggestions page.
- [ ] **"High Priority" filter placement** — Move the value in the filter dropdown (incomplete note).
- [ ] **Filter by individual topic** — Add ability to filter suggestions down to a single topic.
- [ ] **Show count** — Display "Showing X of Y" for suggestions total.

### Suggestions — Logic
- [ ] **Prevent duplicate re-suggestions** — New suggestions should only occur if the reason differs (e.g., a new topic was added).
- [ ] **Track approved suggestions by topic** — Track which topic-based suggestions users approve most.
- [ ] **Smart topic suggestions** — Suggest new topics based on approved suggestion patterns.

### Video Search
- [ ] **Change search box placeholder** to "Enter search term".
- [ ] **4 cards per row** instead of 3.

### Video Library
- [ ] **Move Search button** to the right side of the search box. Make it solid blue primary.

### Topics
- [ ] **Add "Check Frequency"** to the Topics table.

### Topic Onboarding
- [ ] **Add all found videos to video table** during onboarding.
- [ ] **Start with 100 videos**, narrow down to 50 most relevant.

### Watch Page
- [ ] **Transcript retrieval progress** — Show a spinner on the Transcript button while retrieving. Consider showing steps or a "Check" count.

### Summarizing Videos
- [ ] **Short summary should answer the title first** — Address clickbait or "this" references before summarizing.

### Channel Videos
- [ ] **Clarify data loading behavior** — Verify: does the page pull from DB first, then check API if `LastCheckedDate` is stale?

### FormatHelper
- [ ] **Singular/plural formatting** — Modify `FormatDateDisplay` to use proper singular and plural for days, weeks, months, and years.

### Scoring
- [ ] **Adjust scoring weights** — Move 1 point from Channels to Topics (Channels: 5, Topics: 3).
- [ ] **Remove Maximum Pending Suggestions** limit.

---

## Future Features

- [ ] **YT-026: AI-Powered Thumbnail Analysis** — Use thumbnail analysis to enhance suggestion quality.

---

## Code Cleanup / Refactoring

- [ ] **Move FormatHelper** from Services root to `Services/Utilities/`.
- [ ] **Shared Services folder cleanup:**
  - Group data-based services by entity type.
  - Create an "External Services" area.
  - Review `RatingServiceBase` organization (many abstract classes).
- [ ] **Constants to AppSettings** — Move constants from `SuggestionRepository` and `SuggestionService` into `AppSettings`.

---
---

# Part 2: New Suggestions

Based on a full codebase review and competitive analysis against tools like Eightify, Feedly, Castmagic, TubeBuddy, vidIQ, and Descript.

## Competitive Landscape Summary

TargetBrowse occupies a unique position. No single competitor combines channel tracking, topic-based discovery, AI suggestions, video summarization, AND a 5-phase script generation pipeline. Creators today use 3-5 separate tools to do what TargetBrowse could do in one place. The closest comparisons:

| Area | Main Competitors | TargetBrowse Advantage |
|---|---|---|
| Video summaries | Eightify, NoteGPT, YouTube Summary w/ ChatGPT | Integrated into a curation workflow, not standalone |
| Content curation | Feedly (Leo AI), Notion (manual) | YouTube-specialized with scoring algorithm |
| Script generation | Castmagic, Descript | 5-phase pipeline with 8 style dimensions — far deeper |
| Channel tools | TubeBuddy, vidIQ | Focused on researching OTHER creators, not your own channel |
| Discovery | YouTube's algorithm | Intentional, user-controlled topic+channel intersection |

---

## MVP Suggestions (Polish What Exists)

These are changes that strengthen the current feature set, fix friction points, and make the app feel complete. No new major features — just finishing and tightening.

### Critical Path Fixes
- [ ] **Fix suggestion filter/sort** — This is a core workflow. Broken filter/sort on the main Suggestions page undermines the app's primary value proposition.
- [ ] **Fix HTML-encoded video titles everywhere** — This is a display bug across multiple pages. Single fix via a shared helper or component.

### Onboarding & First-Run Experience
- [ ] **Guided first-run flow** — New users see Topics, Channels, and Suggestions pages with no content and little guidance. Consider a step-by-step onboarding: (1) Add 2-3 topics, (2) Track 3-5 channels, (3) Generate first suggestions. The app already has `TopicOnboardingService` — extend this into a visible walkthrough.
- [ ] **Empty state messages on all pages** — Every list page (Library, Suggestions, Projects, Channels, Topics) should show a helpful message with a call-to-action when empty, not just a blank space.

### Watch Page Improvements
- [ ] **Keyboard shortcuts on Watch page** — Simple shortcuts (e.g., `M` = mark watched, `R` = rate, `P` = add to project) would speed up the core review workflow significantly.
- [ ] **"Add to Project" quick action** — Currently available but could be more prominent. When a user is actively working on a project, make it one-click from the Watch page.

### Summary Quality
- [ ] **Summary prompt refinement** — The "short summary should answer the title first" item from the ToDo is a good instinct. Consider also adding: key takeaway, target audience, and a "worth watching if..." line. This makes summaries more actionable for the curation decision.

### Video Library UX
- [ ] **Bulk actions in Video Library** — Select multiple videos to: add to project, mark watched, remove from library. This is a standard expectation for list management views.
- [ ] **"Unsorted" filter** — Videos in library but not in any project. Helps users find videos they saved but haven't organized yet.

### Project Workflow
- [ ] **Project status indicator on project cards** — Show a clear visual for: "Needs more videos", "Ready for guide", "Guide generated", "Script in progress", "Script complete". Users should see at a glance where each project stands.
- [ ] **"Add from library" flow for projects** — When adding videos to a project, allow browsing/searching the user's library directly (not just from Watch page or Video Search).

### Suggestion System
- [ ] **"Why this suggestion?" explainer** — When showing a suggestion, display the scoring breakdown: "From channel X (rated 4 stars) + matches your topic Y". This builds trust in the algorithm and helps users refine their topics/ratings.
- [ ] **Suggestion source stats** — On the Suggestions page, show a small summary: "12 from channels, 8 from topics, 3 from both". Gives users a sense of where their content is coming from.

### General Polish
- [ ] **Consistent loading states** — Ensure every page and action has a spinner or skeleton loader. Some pages may show a flash of empty content before data loads.
- [ ] **Toast notifications** — Replace or supplement the MessageSidebar with brief toast notifications for quick actions (approved, rated, added to library). The sidebar is good for persistent info but toasts are better for quick confirmations.
- [ ] **Mobile responsiveness audit** — Verify all pages work well on tablet/mobile. Video cards, modals, and forms are the typical problem areas in Blazor Server apps.

---

## Mature Product Suggestions (Growth & Differentiation)

These are features that would move TargetBrowse from a personal tool to a compelling product. Prioritize based on your goals (personal use vs. public product).

### High Impact — Differentiation

- [ ] **Browser extension companion** — A lightweight Chrome/Edge extension with one button: "Save to TargetBrowse". While browsing YouTube, users click it to add a video to their library or a specific project. This bridges the biggest friction gap vs. competitors (Eightify etc. live inside YouTube; TargetBrowse requires navigating to a separate app). This is probably the single highest-impact growth feature.

- [ ] **Script export formats** — Export final scripts as: plain text, Markdown, PDF, teleprompter format (large text, auto-scroll), or directly into Google Docs. Creators need scripts in different formats for different recording setups.

- [ ] **Script versioning and editing** — Allow users to edit the generated script in-app with a simple rich text editor. Save versions so they can compare AI-generated vs. their edited version. Track what the AI wrote vs. what the creator changed (useful for refining the script profile over time).

- [ ] **"Research mode" for projects** — A focused view that shows: the project's videos side-by-side with their summaries, the analysis results, and a scratchpad for the user's own notes. Think of it as a research desk for video creation. This positions TargetBrowse as the "Notion killer for video research."

### Medium Impact — Engagement & Retention

- [ ] **Dashboard with analytics** — The current home page could show: topics checked recently, channels with new videos, suggestions waiting, projects in progress, daily AI calls remaining, and a "what's new" feed. This gives users a reason to come back daily.

- [ ] **Scheduled suggestion generation** — Currently manual-only. Allow users to opt into automatic weekly suggestion generation (e.g., every Monday morning). Use the existing `QuotaResetBackgroundService` infrastructure. This transforms the app from "pull" to "push" — users get notified when new suggestions are ready.

- [ ] **Email digest** — Weekly email: "You have 12 new suggestions, 3 expiring soon. Your project 'AI Ethics' needs 1 more video for script generation." Low-effort feature that drives re-engagement.

- [ ] **Video comparison view** — When two videos in a project have conflicting claims (detected in Phase 2 analysis), let the user view them side-by-side with the AI's conflict summary. This is already partially built into the analysis JSON but not surfaced as a UI feature.

- [ ] **Script profile A/B testing** — Let users generate the same script with two different profiles and compare them side-by-side. "How would this sound with 'Authoritative Expert' vs. 'Enthusiastic Guide'?" This showcases the depth of the style system.

### Lower Impact — Nice to Have

- [ ] **Bring your own API key** — Let power users provide their own YouTube Data API key and/or OpenAI API key to bypass daily limits. This is common in developer-oriented tools and reduces your API costs.

- [ ] **Collaborative projects** — Share a project with another user. Both can add videos, view the analysis, and generate scripts. Useful for content teams. (Significant implementation effort — lower priority unless targeting teams.)

- [ ] **Topic discovery from library** — Analyze a user's existing library and ratings to suggest new topics they might want to track. "You've rated 5 videos about 'Rust programming' highly — would you like to add it as a topic?" This closes the feedback loop between ratings and topics.

- [ ] **Channel discovery from topics** — When a topic search returns videos, surface the channels that consistently produce good topic-matched content. "This channel has 8 videos matching your 'Machine Learning' topic — would you like to track it?" Turns topic search into a channel discovery tool.

- [ ] **Export/import project data** — Export a project (videos, analysis, outline, script) as a portable package. Useful for backup, sharing, or moving between accounts.

- [ ] **Content calendar integration** — If a user is generating scripts regularly, show a simple calendar view of: scripts generated, scripts in progress, suggested next generation dates based on their publishing cadence.

---

## Positioning Recommendations

**If building for personal use:** Focus on the MVP section. Fix the bugs, polish the onboarding, and tighten the core loop of: discover (topics/channels) → evaluate (suggestions/summaries) → create (projects/scripts). The app is already impressively functional.

**If building for public release:** Lead with video summarization or topic-based discovery as the entry feature — these have the broadest appeal. Gate the script generation pipeline as a premium feature — it's the deepest differentiator. A browser extension is the single most impactful feature for growth. Consider the tagline: *"Your personal video research desk"* or *"From YouTube rabbit hole to finished script."*
