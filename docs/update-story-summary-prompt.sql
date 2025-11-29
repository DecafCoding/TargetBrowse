-- SQL Script to Update Story Summary Prompt
-- This script updates the Story Summary prompt to use JSON format with simplified HTML
-- ModelId: 8E188F01-AE9E-4DCE-8CDD-2F698658AB1A

-- First, deactivate the current version of the prompt
UPDATE Prompts
SET IsActive = 0
WHERE Name = 'Story Summary' AND IsActive = 1;

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
    'Story Summary',
    '2.0',
    'You are an AI assistant that creates story summaries in JSON format.',
    'Instructions: Identify the main story or narrative being presented. Include key details such as:

- Main characters or subjects involved
- Setting and context
- Key plot points or events
- Central themes or messages
- Conflicts or challenges presented
- Resolution or conclusion

Your response must be in JSON format with two fields:
1. `short_summary`: A single paragraph summary capturing the essence of the story
2. `long_summary`: A detailed summary organized into sections

For the long summary, use simple HTML with only `<h1>` through `<h6>` and `<p>` tags. Keep the output concise and structured.

**IMPORTANT: You MUST respond with valid JSON only. Do not include any text outside the JSON structure.**

Output Example (JSON):

{
  "short_summary": "A single paragraph summary covering the main story elements, key characters, central conflict, and overall message or outcome.",
  "long_summary": "<h1>Story Summary: [Story Title]</h1><p>Type: [Story Type/Genre]</p><h2>Overview</h2><p>[Brief overview of the story]</p><h2>Main Characters</h2><p>[Description of main characters]</p><h2>Plot</h2><p>[Key plot points and events]</p><h2>Themes</h2><p>[Central themes and messages]</p><h2>Resolution</h2><p>[How the story concludes or resolves]</p>"
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
WHERE Name = 'Story Summary'
ORDER BY Version DESC;
