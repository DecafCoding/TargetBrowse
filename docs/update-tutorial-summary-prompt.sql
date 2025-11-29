-- SQL Script to Update Tutorial Summary Prompt
-- This script updates the Tutorial Summary prompt to use JSON format with simplified HTML
-- ModelId: 8E188F01-AE9E-4DCE-8CDD-2F698658AB1A

-- First, deactivate the current version of the prompt
UPDATE Prompts
SET IsActive = 0
WHERE Name = 'Tutorial Summary' AND IsActive = 1;

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
    'Tutorial Summary',
    '2.0',
    'You are an AI assistant that creates tutorial summaries in JSON format.',
    'Instructions: Organize the summary as a step-by-step guide. Identify each step clearly in the order presented in the video. If steps are implied rather than explicitly numbered, infer the logical sequence.

For each step, include:
- Step title or goal — what the user is trying to achieve in this step
- Actions or instructions — detailed description of what to do
- Tools, materials, or resources needed (if mentioned)
- Tips, warnings, or best practices mentioned

Conclude with a final overview:
- Summary of final results or outcome after completing the tutorial
- Any common mistakes, troubleshooting advice, or key takeaways shared

Your response must be in JSON format with two fields:
1. `short_summary`: A single paragraph summary capturing the essence of the tutorial
2. `long_summary`: A detailed step-by-step guide

For the long summary, use simple HTML with only `<h1>` through `<h6>` and `<p>` tags. Keep the output concise and structured.

**IMPORTANT: You MUST respond with valid JSON only. Do not include any text outside the JSON structure.**

Output Example (JSON):

{
  "short_summary": "A single paragraph summary covering the tutorial topic, key steps, and expected outcome.",
  "long_summary": "<h1>Tutorial: [Tutorial Title]</h1><p>Purpose: [Brief Purpose or Goal]</p><h2>Steps</h2><h3>Step 1: [Step Title or Goal]</h3><p><strong>Instructions:</strong> [Detailed instructions for this step]</p><p><strong>Tools/Resources:</strong> [Tools, materials, or resources needed]</p><p><strong>Tips/Warnings:</strong> [Optional tips or warnings]</p><h3>Step 2: [Step Title or Goal]</h3><p><strong>Instructions:</strong> [Detailed instructions]</p><p><strong>Tools/Resources:</strong> [Optional]</p><p><strong>Tips/Warnings:</strong> [Optional]</p><h2>Final Overview</h2><p>[Summary of final results, key takeaways, and troubleshooting advice]</p>"
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
WHERE Name = 'Tutorial Summary'
ORDER BY Version DESC;
