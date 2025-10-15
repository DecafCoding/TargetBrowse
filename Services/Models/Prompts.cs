namespace TargetBrowse.Services.Models;

/// <summary>
/// Configuration for transcript summary prompts and AI model settings
/// </summary>
public static class Prompts
{
    /// <summary>
    /// Configuration for analyzing video transcripts and generating summaries
    /// </summary>
    public static class AnalyzeTranscript
    {
        /// <summary>
        /// AI model configuration for transcript analysis
        /// </summary>
        public static class ModelConfig
        {
            public const string Model = "gpt-4o-mini";
            public const int MaxTokens = 1500;
            public const double Temperature = 0.3;

            /// <summary>
            /// System message that defines the AI's role and behavior for transcript analysis
            /// </summary>
            public const string SystemMessage = "Act as an expert content analyst who specializes in distilling complex video content into clear, actionable summaries.";
        }

        /// <summary>
        /// The main prompt template for transcript analysis
        /// </summary>
        public const string PromptTemplate = @"**Task**: Analyze the provided YouTube video transcript and create a comprehensive summary that captures the key insights and structural elements.

Summary Requirements:
**Length**: Provide a summary of 2-3 paragraphs, expanding to more paragraphs only if the content contains particularly important or complex information that warrants additional detail.

**Content Focus**:
* Summarize the main topic, key arguments, and primary takeaways
* Capture the overall value proposition or purpose of the video

**Structural Elements to Highlight**: When present in the video, specifically mention if it includes:
* **Blueprints** or step-by-step plans
* **Frameworks** or systematic approaches
* **Numbered lists** (e.g., ""5 Ways to..."", ""10 Steps for..."")
* **Checklists** or action items
* **Templates** or models
* **Methodologies** or processes
* **Case studies** or examples
* **Before/after scenarios**

**Format**:
1. **Video Topic** (1-2 sentences based on your review of the transcript)
2. **Main Summary** (2-3 paragraphs covering the core content)
3. **Structured Content** (if applicable): Brief note about any blueprints, frameworks, or lists mentioned above

Important Guidelines:
* Focus on concepts and high-level insights rather than specific details
* Don't reproduce exact steps or detailed instructions
* Mention the presence of structured content without detailing every point
* Keep the tone professional yet accessible
* If the video lacks substantial content, note this appropriately

**Example Structure**: ""This video discusses [main topic] and provides [type of guidance] for [target audience]. The presenter covers [key themes/arguments] and explains [primary value/outcome]. [Additional context about approach or unique angle].

[Second paragraph with supporting details, methodology, or additional insights if substantial content warrants it].

**Structured Elements**: The video includes a [X]-step framework/blueprint/checklist for [purpose], along with [other structured content if present].""

Final Output Format:
Return your summary as a JSON object with the following structure:

```json
{
  ""video_topic"": ""Main topic/subject of the video"",
  ""main_summary"": ""2-3 paragraph summary of the video content"",
  ""structured_content"": ""Brief description of what structured elements are present""
}
```";

        /// <summary>
        /// Gets the formatted prompt for transcript analysis
        /// </summary>
        /// <param name="transcript">The video transcript to analyze</param>
        /// <returns>Complete formatted prompt with transcript</returns>
        public static string GetFormattedPrompt(string transcript)
        {
            var promptBuilder = new System.Text.StringBuilder();
            promptBuilder.AppendLine(PromptTemplate);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Please analyze the following YouTube video transcript:");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(transcript);

            return promptBuilder.ToString();
        }
    }
}
