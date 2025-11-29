# Educational Summary Prompt Update

## Overview
This document captures the changes made to the Educational Summary prompt to simplify the HTML output and switch to a JSON response format with both short and long summaries.

## Key Changes
- Simplified HTML to use only `<h>` and `<p>` tags (removed Bootstrap classes)
- Changed response format to JSON with two fields: `short_summary` and `long_summary`
- Removed all references to quotes
- Name: Educational Summary
- ModelId: 8E188F01-AE9E-4DCE-8CDD-2F698658AB1A

---

## OLD PROMPT

**Instructions:**
Identify the core topic or question the video addresses. Organize the summary into sections based on concepts, subtopics, or key points. For each section, include: Section Title / Concept, Explanation / Key Details — summarize the main ideas clearly, Examples, evidence, or supporting data (if mentioned), Notable quotes or insights (optional). Conclude with a final overview, summarizing the main takeaways or implications of the content. Output only HTML, using Bootstrap 5 classes for cards, headings, and lists. Do not include extra text outside the HTML.

**Output Example (Bootstrap HTML):**
```html
<div class="container my-4">
  <div class="card mb-3">
    <div class="card-header bg-secondary text-white">
      <h2 class="mb-0">Educational Video Summary: [Video Title]</h2>
      <small class="text-light">Overview: [2–3 sentence summary of the topic and purpose]</small>
    </div>
    <div class="card-body">
      <h4>Section 1: [Concept / Subtopic Title]</h4>
      <div class="card mb-3">
        <div class="card-body">
          <p><strong>Explanation:</strong> [Summarize the key details of this concept]</p>
          <p><strong>Examples / Evidence:</strong> [Optional examples or data]</p>
          <p><strong>Notable Quotes / Insights:</strong> "[Optional quote]"</p>
        </div>
      </div>

      <h4>Section 2: [Concept / Subtopic Title]</h4>
      <div class="card mb-3">
        <div class="card-body">
          <p><strong>Explanation:</strong> [Summarize key details]</p>
          <p><strong>Examples / Evidence:</strong> [Optional]</p>
          <p><strong>Notable Quotes / Insights:</strong> "[Optional quote]"</p>
        </div>
      </div>

      <!-- Continue for additional sections -->

      <h4>Final Overview / Takeaways</h4>
      <p>[Summarize the overall conclusions, insights, or implications of the video]</p>
    </div>
  </div>
</div>

Full Transcript: [Paste-full-transcript-here]
```

---

## NEW PROMPT

**Instructions:**
Identify the core topic or question the video addresses. Organize the summary into sections based on concepts, subtopics, or key points.

Your response must be in JSON format with two fields:
1. `short_summary`: A single paragraph summary of the video's main topic and key takeaways
2. `long_summary`: A detailed summary organized into sections

For the long summary, use simple HTML with only `<h>` and `<p>` tags. For each section, include:
- Section Title / Concept
- Explanation / Key Details — summarize the main ideas clearly
- Examples, evidence, or supporting data (if mentioned)

Conclude with a final overview, summarizing the main takeaways or implications of the content.

**Output Format:**
```json
{
  "short_summary": "A single paragraph summary of the video covering the main topic and key points.",
  "long_summary": "<h2>Educational Video Summary: [Video Title]</h2><p>Overview: [2-3 sentence summary of the topic and purpose]</p><h3>Section 1: [Concept/Subtopic Title]</h3><p><strong>Explanation:</strong> [Summarize the key details of this concept]</p><p><strong>Examples/Evidence:</strong> [Optional examples or data]</p><h3>Section 2: [Concept/Subtopic Title]</h3><p><strong>Explanation:</strong> [Summarize key details]</p><p><strong>Examples/Evidence:</strong> [Optional]</p><h3>Final Overview</h3><p>[Summarize the overall conclusions, insights, or implications of the video]</p>"
}
```

**Full Transcript:** [Paste-full-transcript-here]

---

## Database Configuration

To implement this prompt in the database:

1. **Name:** Educational Summary
2. **ModelId:** 8E188F01-AE9E-4DCE-8CDD-2F698658AB1A
3. **System Prompt:** Standard educational video summarization prompt
4. **User Prompt Template:** Use the "NEW PROMPT" content above
5. **Response Format:** JSON object

## Implementation Notes

The `TranscriptSummaryService.cs` already supports:
- JSON response format (line 280)
- Parsing of `short_summary` and `long_summary` fields (lines 317-369)
- Storing both summaries in the database (lines 164-168)

No code changes are required; only the prompt configuration needs to be updated in the database.
