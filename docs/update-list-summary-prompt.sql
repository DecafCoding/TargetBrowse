-- SQL Script to Update Educational Summary Prompt for List-Based Videos
-- This script updates the Educational Summary prompt to handle list-based content
-- ModelId: 8E188F01-AE9E-4DCE-8CDD-2F698658AB1A
-- Temperature: 1, TopP: 0.9, Version: 2

-- First, mark the old prompt as deleted
UPDATE Prompts
SET IsDeleted = 1,
    IsActive = 0
WHERE Name = 'Educational Summary' AND IsActive = 1;

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
    IsDeleted,
    ModelId,
    CreatedAt,
    CreatedBy,
    LastModifiedAt,
    LastModifiedBy
)
VALUES (
    NEWID(),
    'Educational Summary',
    2,
    'You are an AI assistant that creates summaries for list-based educational videos in JSON format.',
    'Instructions: Identify the list structure. Determine whether the video uses numbers (e.g., "Top 5 Reasons…") or bullets (e.g., "Here are several tips…"). If the number of list items is not explicit, infer it logically from the transcript.

Your response must be in JSON format with two fields:
1. `short_summary`: A single paragraph summary of the video''s main topic and the key list items covered
2. `long_summary`: A detailed summary organized by list items

For the long summary, use simple HTML with only `<h1>` through `<h6>` and `<p>` tags. Organize the summary clearly:
- Present each point as its own numbered or bulleted section
- For each item, include:
  - Main point or heading — what the speaker is discussing
  - Supporting explanation or reasoning — why it matters or what it means
  - Examples, anecdotes, or evidence mentioned (if any)
- Conclude with a brief overview summarizing the overall takeaway or theme connecting the list items
- Mention any final advice, conclusions, or closing thoughts offered by the speaker

Keep your writing concise, structured, and easy to parse programmatically.

**IMPORTANT: You MUST respond with valid JSON only. Do not include any text outside the JSON structure.**

Output Example (JSON):

{
  "short_summary": "A single paragraph summary describing the video topic, the type of list presented (numbered/bulleted), and the main points covered in the list.",
  "long_summary": "<h1>List Video Summary: [Video Title]</h1><p>Overview: [Brief 2-3 sentence summary of the video]</p><h2>List of Points</h2><h3>1. [Title of first point]</h3><p><strong>Explanation:</strong> [Summary of explanation, reasoning, or context]</p><p><strong>Examples/Evidence:</strong> [Optional examples or evidence]</p><h3>2. [Title of second point]</h3><p><strong>Explanation:</strong> [Summary]</p><p><strong>Examples/Evidence:</strong> [Optional]</p><h2>Final Takeaway</h2><p>[Brief summary of the overall insight, connection between points, or closing statement]</p>"
}

Transcript: [Paste-full-transcript-here]',
    1.0,
    2000,
    0.9,
    1,
    0,
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
    IsDeleted,
    Temperature,
    TopP,
    MaxTokens,
    ModelId,
    CreatedAt
FROM Prompts
WHERE Name = 'Educational Summary'
ORDER BY Version DESC;
