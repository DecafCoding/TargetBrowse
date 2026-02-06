namespace TargetBrowse.Services.AI
{
    /// <summary>
    /// Helper class for building prompts for script generation phases.
    /// Provides methods to construct prompts for analysis, outline, and script generation.
    /// </summary>
    public static class ScriptPromptBuilder
    {
        /// <summary>
        /// Builds the prompt for analyzing video content (Phase 2).
        /// Takes video summaries and identifies themes, conflicts, and cohesion.
        /// </summary>
        /// <param name="videoSummaries">List of video summaries with metadata</param>
        /// <returns>Formatted prompt string for analysis</returns>
        public static string BuildAnalysisPrompt(List<VideoSummaryData> videoSummaries)
        {
            var videoList = string.Join("\n\n", videoSummaries.Select((v, i) =>
                $"Video {i + 1}:\nTitle: {v.Title}\nSummary: {v.Summary}"));

            return $@"Analyze these {videoSummaries.Count} video summaries and provide a comprehensive analysis.

{videoList}

Please provide a detailed analysis in JSON format with the following structure:
{{
  ""mainTopic"": ""The overarching topic covered across all videos"",
  ""subtopics"": [
    {{
      ""name"": ""Subtopic name"",
      ""coveredBy"": [""video1"", ""video2""],
      ""bestSource"": ""video1"",
      ""depth"": ""comprehensive|moderate|brief""
    }}
  ],
  ""conflicts"": [
    {{
      ""topic"": ""Topic where videos disagree"",
      ""video1Claim"": ""What video 1 says"",
      ""video2Claim"": ""What video 2 says"",
      ""aiRecommendation"": ""Which approach to use or how to present both""
    }}
  ],
  ""uniqueClaims"": [
    {{
      ""source"": ""video1"",
      ""claim"": ""Something only this video mentions""
    }}
  ],
  ""cohesionScore"": 85,
  ""recommendations"": [
    ""These videos work well together"",
    ""Consider finding more content on X""
  ]
}}

Focus on:
1. Identifying common themes vs. unique perspectives
2. Finding contradictions or different approaches
3. Determining which video explains each concept best
4. Assessing whether these videos work well together for a cohesive script";
        }

        /// <summary>
        /// Builds the prompt for generating a script outline (Phase 4).
        /// Takes analysis results, user profile, and target length.
        /// </summary>
        /// <param name="analysisJson">JSON result from analysis phase</param>
        /// <param name="userProfile">User's script style preferences</param>
        /// <param name="targetLengthMinutes">Desired script length</param>
        /// <returns>Formatted prompt string for outline generation</returns>
        public static string BuildOutlinePrompt(
            string analysisJson,
            UserProfileData userProfile,
            int targetLengthMinutes)
        {
            return $@"Create a detailed script outline for a {targetLengthMinutes}-minute video based on this analysis:

{analysisJson}

User's style preferences:
- Tone: {userProfile.Tone}
- Pacing: {userProfile.Pacing}
- Complexity: {userProfile.Complexity}
{(string.IsNullOrEmpty(userProfile.CustomInstructions) ? "" : $"- Custom instructions: {userProfile.CustomInstructions}")}

Generate a structured outline in JSON format:
{{
  ""sections"": [
    {{
      ""title"": ""Section title"",
      ""estimatedMinutes"": 2,
      ""keyPoints"": [
        ""Point 1 (source: all videos agree)"",
        ""Point 2 (source: video 2 best explanation)""
      ],
      ""sourceNotes"": ""Which videos this section draws from"",
      ""conflictsToAddress"": ""Any conflicts that need to be handled in this section""
    }}
  ],
  ""totalEstimatedMinutes"": {targetLengthMinutes},
  ""structureRationale"": ""Why this structure was chosen""
}}

Guidelines:
1. Include introduction (hook, preview) and conclusion (recap, call-to-action)
2. Organize content logically from fundamentals to advanced
3. Note which video provides the best explanation for each point
4. Flag any conflicts that will be addressed in the script
5. Distribute time appropriately across sections to hit {targetLengthMinutes} minutes
6. Match the user's preferred tone and complexity level";
        }

        /// <summary>
        /// Builds the prompt for generating the final script (Phase 5).
        /// Takes outline, user profile, and original transcripts for detail.
        /// </summary>
        /// <param name="outlineJson">JSON outline from Phase 4</param>
        /// <param name="userProfile">User's script style preferences</param>
        /// <param name="videoTranscripts">Full transcripts for detailed reference</param>
        /// <returns>Formatted prompt string for script generation</returns>
        public static string BuildScriptPrompt(
            string outlineJson,
            UserProfileData userProfile,
            List<VideoTranscriptData> videoTranscripts)
        {
            var transcriptSection = string.Join("\n\n", videoTranscripts.Select((v, i) =>
                $"Video {i + 1} Transcript (for reference):\nTitle: {v.Title}\n{v.Transcript.Substring(0, Math.Min(5000, v.Transcript.Length))}..."));

            return $@"Generate a complete video script based on this outline:

{outlineJson}

User's style preferences:
- Tone: {userProfile.Tone}
- Pacing: {userProfile.Pacing}
- Complexity: {userProfile.Complexity}
{(string.IsNullOrEmpty(userProfile.CustomInstructions) ? "" : $"- Custom instructions: {userProfile.CustomInstructions}")}

Reference transcripts (use these for specific details and examples):
{transcriptSection}

Generate the script in JSON format:
{{
  ""scriptText"": ""Full narration text with section breaks clearly marked"",
  ""wordCount"": 2150,
  ""estimatedDurationSeconds"": 860,
  ""internalNotes"": {{
    ""section1"": {{
      ""sources"": [""video1"", ""video2""],
      ""conflictsResolved"": [""Chose video 1's approach because...""],
      ""uniqueClaims"": [""Only video 1 mentioned this...""]
    }}
  }}
}}

Script writing guidelines:
1. Write in a natural, conversational style matching the user's tone preference
2. Use smooth transitions between sections
3. Include specific examples and explanations from the source videos
4. When videos conflict, either choose the better approach or present both perspectives
5. Mark section breaks clearly (e.g., ""[SECTION: Introduction]"")
6. Target speaking rate: ~150 words per minute
7. Make it engaging with hooks, examples, and clear explanations
8. Keep complexity level appropriate for the user's target audience
9. Internal notes are for transparency - don't include them in the scriptText";
        }
    }

    /// <summary>
    /// Data structure for video summary information
    /// </summary>
    public class VideoSummaryData
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string VideoId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data structure for video transcript information
    /// </summary>
    public class VideoTranscriptData
    {
        public string Title { get; set; } = string.Empty;
        public string Transcript { get; set; } = string.Empty;
        public string VideoId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data structure for user profile preferences
    /// </summary>
    public class UserProfileData
    {
        public string Tone { get; set; } = "Casual";
        public string Pacing { get; set; } = "Moderate";
        public string Complexity { get; set; } = "Intermediate";
        public string? CustomInstructions { get; set; }
    }
}
