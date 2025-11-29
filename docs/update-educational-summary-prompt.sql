-- SQL Script to Update Educational Summary Prompt
-- This script updates the Educational Summary prompt to use JSON format with simplified HTML
-- ModelId: 8E188F01-AE9E-4DCE-8CDD-2F698658AB1A

-- First, deactivate the current version of the prompt
UPDATE Prompts
SET IsActive = 0
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
    ModelId,
    CreatedAt,
    CreatedBy,
    LastModifiedAt,
    LastModifiedBy
)
VALUES (
    NEWID(),
    'Educational Summary',
    '2.0',
    'You are an AI assistant that creates educational video summaries in JSON format.',
    'Instructions: Identify the core topic or question the video addresses. Organize the summary into sections based on concepts, subtopics, or key points.

Your response must be in JSON format with two fields:
1. `short_summary`: A single paragraph summary of the video''s main topic and key takeaways
2. `long_summary`: A detailed summary organized into sections

For the long summary, use simple HTML with only `<h1>` through `<h6>` and `<p>` tags. For each section, include:
- Section Title / Concept
- Explanation / Key Details — summarize the main ideas clearly
- Examples, evidence, or supporting data (if mentioned)

Conclude with a final overview, summarizing the main takeaways or implications of the content.

**IMPORTANT: You MUST respond with valid JSON only. Do not include any text outside the JSON structure.**

Output Example (JSON):

{
  "short_summary": "A single paragraph summary of the video covering the main topic and key points discussed, highlighting the core educational value and primary takeaways from the content.",
  "long_summary": "<h1>Educational Video Summary: [Video Title]</h1><p>Overview: [2-3 sentence summary of the topic and purpose]</p><h2>Section 1: [Concept/Subtopic Title]</h2><p><strong>Explanation:</strong> [Summarize the key details of this concept]</p><p><strong>Examples/Evidence:</strong> [Optional examples or data]</p><h2>Section 2: [Concept/Subtopic Title]</h2><p><strong>Explanation:</strong> [Summarize key details]</p><p><strong>Examples/Evidence:</strong> [Optional]</p><h2>Final Overview</h2><p>[Summarize the overall conclusions, insights, or implications of the video]</p>"
}

Full Transcript: [Paste-full-transcript-here]',
    0.7,
    2000,
    NULL,
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
    ModelId,
    CreatedAt
FROM Prompts
WHERE Name = 'Educational Summary'
ORDER BY Version DESC;
