-- SQL Script to Update Showcase Summary Prompt
-- This script updates the Showcase Summary prompt to use JSON format with simplified HTML
-- ModelId: 8E188F01-AE9E-4DCE-8CDD-2F698658AB1A

-- First, deactivate the current version of the prompt
UPDATE Prompts
SET IsActive = 0
WHERE Name = 'Showcase Summary' AND IsActive = 1;

-- Insert new version of the prompt
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
    'Showcase Summary',
    '2.0',
    'You are an AI assistant that creates showcase summaries in JSON format.',
    'Instructions: Identify the main subject being showcased (project, process, or event). Organize the summary into phases or segments, highlighting changes, milestones, or progressions.

For each segment, include:
- Phase/Segment Title
- Description of what is shown
- Key highlights, visuals, or features

Conclude with a final overview, summarizing the overall result, impact, or outcome.

Your response must be in JSON format with two fields:
1. `short_summary`: A single paragraph overview of the showcased subject, including what is being presented and the overall outcome
2. `long_summary`: A detailed summary organized into phases/segments

For the long summary, use simple HTML with only `<h1>` through `<h6>` and `<p>` tags. Keep the output concise and structured.

**IMPORTANT: You MUST respond with valid JSON only. Do not include any text outside the JSON structure.**

Output Example (JSON):

{
  "short_summary": "A single paragraph describing the showcased subject, its main purpose or goal, and the overall result or outcome achieved.",
  "long_summary": "<h1>Showcase Summary: [Video Title]</h1><p>Overview: A 2-3 sentence summary of the showcased subject and its purpose.</p><h2>Phase 1: [Segment Title]</h2><p><strong>Description:</strong> What is shown in this phase</p><p><strong>Key Highlights:</strong> Notable visuals, events, or features</p><h2>Phase 2: [Segment Title]</h2><p><strong>Description:</strong> Description of this phase</p><p><strong>Key Highlights:</strong> Notable features or changes</p><h2>Final Overview</h2><p>Summarize the overall result, impact, or outcome of the showcased process or event.</p>"
}

Transcript: [Paste-full-transcript-here]',
    1.0,
    10000,
    0.9,
    1,
    '8E188F01-AE9E-4DCE-8CDD-2F698658AB1A',
    GETUTCDATE(),
    'System',
    GETUTCDATE(),
    'System'
);

-- Verify the update
SELECT
    Id,
    Name,
    Version,
    IsActive,
    Temperature,
    MaxTokens,
    TopP,
    ModelId,
    CreatedAt
FROM Prompts
WHERE Name = 'Showcase Summary'
ORDER BY Version DESC;
