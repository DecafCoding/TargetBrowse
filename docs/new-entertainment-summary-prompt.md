# New Entertainment Summary Prompt

This document contains the updated prompt for the Entertainment Summary feature.

## System Prompt

You are an AI assistant that creates video summaries in JSON format.

## User Prompt Template

Instructions: Identify the format and main theme of the video (e.g., challenge, reaction, performance). Organize the summary into key moments or segments, highlighting fun or notable events. For each moment/segment, include:
- Title / Description of the Moment
- What happens — key actions, interactions, or events
- Emotions, reactions, or humor expressed
- Notable quotes or funny lines (optional)

Conclude with a final overview, summarizing the overall tone, energy, or entertainment value.

**IMPORTANT: You MUST respond with valid JSON only. Do not include any text outside the JSON structure.**

Your response must be a JSON object with exactly two fields:
1. `short_summary`: A single paragraph (2-3 sentences) summarizing the video format and overall tone
2. `long_summary`: A detailed HTML summary using ONLY `<h1>` through `<h6>` and `<p>` tags

## Output Example (JSON)

```json
{
  "short_summary": "This is an energetic challenge video where contestants compete in a series of physical and mental tasks. The video features lots of humor, unexpected twists, and genuine reactions from participants as they navigate through increasingly difficult obstacles.",
  "long_summary": "<h1>Entertainment Video Summary: [Video Title]</h1><p>[2-3 sentence summary of the video format and overall tone]</p><h2>Moment 1: [Title / Description]</h2><p><strong>What Happens:</strong> [Describe the key action, event, or performance]</p><p><strong>Emotions / Reactions:</strong> [Describe the humor, excitement, or reactions]</p><p><strong>Notable Quotes / Lines:</strong> \"[Optional funny or memorable quote]\"</p><h2>Moment 2: [Title / Description]</h2><p><strong>What Happens:</strong> [Describe action/event]</p><p><strong>Emotions / Reactions:</strong> [Optional]</p><p><strong>Notable Quotes / Lines:</strong> [Optional]</p><h2>Final Overview / Entertainment Value</h2><p>[Summarize the overall energy, tone, highlights, or entertainment impact of the video]</p>"
}
```

## Full Transcript

[Paste-full-transcript-here]

---

## Database Update Instructions

To update the prompt in the database:

1. Identify the prompt record for "Entertainment Summary"
2. Update the SystemPrompt field to: "You are an AI assistant that creates video summaries in JSON format."
3. Update the UserPromptTemplate field with the template above (from "Instructions:" through "[Paste-full-transcript-here]")
4. Ensure the prompt is marked as IsActive = true
5. Version should be incremented (e.g., if current is "1.0", update to "2.0")

See the SQL script file for the exact UPDATE statement.
