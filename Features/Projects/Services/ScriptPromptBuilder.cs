namespace TargetBrowse.Features.Projects.Services
{
    /// <summary>
    /// Formats prompt templates for script generation phases.
    /// Templates are stored in the database; this class fills named placeholders with runtime data.
    /// </summary>
    public static class ScriptPromptBuilder
    {
        /// <summary>
        /// Formats the analysis prompt template with video summary data.
        /// Replaces: {video_count}, {video_list}
        /// </summary>
        public static string FormatAnalysisPrompt(string template, List<VideoSummaryData> videoSummaries)
        {
            var videoList = string.Join("\n\n", videoSummaries.Select((v, i) =>
                $"Video {i + 1}:\nTitle: {v.Title}\nSummary: {v.Summary}"));

            return template
                .Replace("{video_count}", videoSummaries.Count.ToString())
                .Replace("{video_list}", videoList);
        }

        /// <summary>
        /// Formats the outline prompt template with analysis and profile data.
        /// Replaces: {target_length}, {analysis_json}, 8 profile fields, {custom_instructions_line}
        /// </summary>
        public static string FormatOutlinePrompt(
            string template,
            string analysisJson,
            UserProfileData userProfile,
            int targetLengthMinutes)
        {
            var customInstructionsLine = string.IsNullOrEmpty(userProfile.CustomInstructions)
                ? ""
                : $"- Custom Instructions: {userProfile.CustomInstructions}";

            return template
                .Replace("{target_length}", targetLengthMinutes.ToString())
                .Replace("{analysis_json}", analysisJson)
                .Replace("{tone}", userProfile.Tone)
                .Replace("{pacing}", userProfile.Pacing)
                .Replace("{complexity}", userProfile.Complexity)
                .Replace("{structure_style}", userProfile.StructureStyle)
                .Replace("{hook_strategy}", userProfile.HookStrategy)
                .Replace("{audience_relationship}", userProfile.AudienceRelationship)
                .Replace("{information_density}", userProfile.InformationDensity)
                .Replace("{rhetorical_style}", userProfile.RhetoricalStyle)
                .Replace("{custom_instructions_line}", customInstructionsLine);
        }

        /// <summary>
        /// Formats the script prompt template with outline, profile, and transcript data.
        /// Replaces: {outline_json}, 8 profile fields, {custom_instructions_line}, {transcript_section}
        /// </summary>
        public static string FormatScriptPrompt(
            string template,
            string outlineJson,
            UserProfileData userProfile,
            List<VideoTranscriptData> videoTranscripts)
        {
            var transcriptSection = string.Join("\n\n", videoTranscripts.Select((v, i) =>
                $"Video {i + 1} Transcript (for reference):\nTitle: {v.Title}\n{v.Transcript.Substring(0, Math.Min(5000, v.Transcript.Length))}..."));

            var customInstructionsLine = string.IsNullOrEmpty(userProfile.CustomInstructions)
                ? ""
                : $"- Custom Instructions: {userProfile.CustomInstructions}";

            return template
                .Replace("{outline_json}", outlineJson)
                .Replace("{tone}", userProfile.Tone)
                .Replace("{pacing}", userProfile.Pacing)
                .Replace("{complexity}", userProfile.Complexity)
                .Replace("{structure_style}", userProfile.StructureStyle)
                .Replace("{hook_strategy}", userProfile.HookStrategy)
                .Replace("{audience_relationship}", userProfile.AudienceRelationship)
                .Replace("{information_density}", userProfile.InformationDensity)
                .Replace("{rhetorical_style}", userProfile.RhetoricalStyle)
                .Replace("{custom_instructions_line}", customInstructionsLine)
                .Replace("{transcript_section}", transcriptSection);
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
        public string Tone { get; set; } = "Insider Conversational";
        public string Pacing { get; set; } = "Build-Release";
        public string Complexity { get; set; } = "Layered Progressive";
        public string StructureStyle { get; set; } = "Enumerated Scaffolding";
        public string HookStrategy { get; set; } = "Insider Secret";
        public string AudienceRelationship { get; set; } = "Insider-Outsider";
        public string InformationDensity { get; set; } = "High Density";
        public string RhetoricalStyle { get; set; } = "Extended Metaphors";
        public string? CustomInstructions { get; set; }
    }
}
