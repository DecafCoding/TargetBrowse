-- SQL Script to Update Review Summary Prompt
-- This script updates the Review Summary prompt to use JSON format with simplified HTML
-- ModelId: 8E188F01-AE9E-4DCE-8CDD-2F698658AB1A

-- First, deactivate the current version of the prompt
UPDATE Prompts
SET IsActive = 0
WHERE Name = 'Review Summary' AND IsActive = 1;

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
    'Review Summary',
    '2.0',
    'You are an AI assistant that creates product review summaries in JSON format.',
    'Instructions: Identify the main subject being reviewed. Include its name, type, and purpose if mentioned. Organize the summary into sections reflecting evaluation points:

- Pros — advantages, strengths, or positive features highlighted
- Cons — drawbacks, weaknesses, or limitations mentioned
- Comparisons — comparisons to similar products or alternatives, if any
- Ratings or scores — numeric or qualitative assessments provided

Conclude with a final verdict: Overall opinion or recommendation (e.g., "worth buying," "not recommended"). Any final advice, tips, or actionable insights shared.

Your response must be in JSON format with two fields:
1. `short_summary`: A single paragraph summary of the product being reviewed and the overall verdict
2. `long_summary`: A detailed summary organized into sections

For the long summary, use simple HTML with only `<h1>` through `<h6>` and `<p>` tags. Keep the output concise and structured.

**IMPORTANT: You MUST respond with valid JSON only. Do not include any text outside the JSON structure.**

Output Example (JSON):

{
  "short_summary": "A single paragraph summary covering the product name, type, key pros and cons, and the reviewer''s overall recommendation or verdict.",
  "long_summary": "<h1>Product Review: [Product Name]</h1><p>Type: [Product Type] | Purpose: [Product Purpose]</p><h2>Pros</h2><p>[First Pro]</p><p>[Second Pro]</p><h2>Cons</h2><p>[First Con]</p><p>[Second Con]</p><h2>Comparisons</h2><p>[Comparison to similar products, if any]</p><h2>Ratings</h2><p>Numeric Score: [Score] | Qualitative Assessment: [Assessment]</p><h2>Final Verdict</h2><p>[Overall opinion, recommendation, and key takeaways]</p>"
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
WHERE Name = 'Review Summary'
ORDER BY Version DESC;
