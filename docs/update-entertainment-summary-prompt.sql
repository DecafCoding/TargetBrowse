-- SQL Script to Update Entertainment Summary Prompt
-- This script updates the Entertainment Summary prompt to use JSON format with simplified HTML

-- First, deactivate the current version of the prompt
UPDATE Prompts
SET IsActive = 0
WHERE Name = 'Entertainment Summary' AND IsActive = 1;

-- Insert new version of the prompt (adjust ModelId as needed)
-- You may need to update the ModelId to match your database
DECLARE @ModelId UNIQUEIDENTIFIER;
SELECT @ModelId = Id FROM Models WHERE Name = 'gpt-4o-mini' AND IsActive = 1;

INSERT INTO Prompts (
    Id,
    Name,
    Version,
    SystemPrompt,
    UserPromptTemplate,
    Temperature,
    MaxTokens,
    TopP,
    IsActive,
    ModelId,
    CreatedAt,
    CreatedBy,
    LastModifiedAt,
    LastModifiedBy
)
VALUES (
    NEWID(),
    'Entertainment Summary',
    '2.0',
    'You are an AI assistant that creates video summaries in JSON format.',
    'Instructions: Identify the format and main theme of the video (e.g., challenge, reaction, performance). Organize the summary into key moments or segments, highlighting fun or notable events. For each moment/segment, include:
- Title / Description of the Moment
- What happens — key actions, interactions, or events
- Emotions, reactions, or humor expressed
- Notable quotes or funny lines (optional)

Conclude with a final overview, summarizing the overall tone, energy, or entertainment value.

**IMPORTANT: You MUST respond with valid JSON only. Do not include any text outside the JSON structure.**

Your response must be a JSON object with exactly two fields:
1. `short_summary`: A single paragraph (2-3 sentences) summarizing the video format and overall tone
2. `long_summary`: A detailed HTML summary using ONLY `<h1>` through `<h6>` and `<p>` tags

Output Example (JSON):

{
  "short_summary": "This is an energetic challenge video where contestants compete in a series of physical and mental tasks. The video features lots of humor, unexpected twists, and genuine reactions from participants as they navigate through increasingly difficult obstacles.",
  "long_summary": "<h1>Entertainment Video Summary: [Video Title]</h1><p>[2-3 sentence summary of the video format and overall tone]</p><h2>Moment 1: [Title / Description]</h2><p><strong>What Happens:</strong> [Describe the key action, event, or performance]</p><p><strong>Emotions / Reactions:</strong> [Describe the humor, excitement, or reactions]</p><p><strong>Notable Quotes / Lines:</strong> \"[Optional funny or memorable quote]\"</p><h2>Moment 2: [Title / Description]</h2><p><strong>What Happens:</strong> [Describe action/event]</p><p><strong>Emotions / Reactions:</strong> [Optional]</p><p><strong>Notable Quotes / Lines:</strong> [Optional]</p><h2>Final Overview / Entertainment Value</h2><p>[Summarize the overall energy, tone, highlights, or entertainment impact of the video]</p>"
}

Full Transcript: [Paste-full-transcript-here]',
    0.7,
    2000,
    NULL,
    1,
    @ModelId,
    GETUTCDATE(),
    'System',
    GETUTCDATE(),
    'System'
);

-- Verify the update
SELECT
    Name,
    Version,
    IsActive,
    Temperature,
    MaxTokens,
    CreatedAt
FROM Prompts
WHERE Name = 'Entertainment Summary'
ORDER BY Version DESC;
