using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TargetBrowse.Migrations
{
    /// <inheritdoc />
    public partial class MoveScriptPromptsToDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Get the gpt-4o-mini model ID (created by GetOrCreateScriptPromptIdAsync or other migrations)
            // Use raw SQL to look up the model and insert prompts with correct FK
            migrationBuilder.Sql(@"
                -- Ensure gpt-4o-mini model exists
                IF NOT EXISTS (SELECT 1 FROM Models WHERE Name = 'gpt-4o-mini')
                BEGIN
                    INSERT INTO Models (Id, Name, Provider, CostPer1kInputTokens, CostPer1kOutputTokens, IsActive, CreatedAt, LastModifiedAt, IsDeleted)
                    VALUES (NEWID(), 'gpt-4o-mini', 'OpenAI', 0.000150, 0.000600, 1, GETUTCDATE(), GETUTCDATE(), 0);
                END

                -- Deactivate the old MVP placeholder prompt
                UPDATE Prompts SET IsActive = 0 WHERE Name = 'Script Analysis (MVP)';

                -- Get model ID for inserts
                DECLARE @ModelId UNIQUEIDENTIFIER;
                SELECT @ModelId = Id FROM Models WHERE Name = 'gpt-4o-mini';

                -- Insert Script Analysis prompt
                INSERT INTO Prompts (Id, Name, Version, ModelId, SystemPrompt, UserPromptTemplate, Temperature, MaxTokens, IsActive, CreatedAt, LastModifiedAt, IsDeleted)
                VALUES (
                    NEWID(),
                    'Script Analysis',
                    '1.0',
                    @ModelId,
                    'You are an expert video content analyst. Analyze video summaries to identify themes, conflicts, and create cohesive video scripts.',
                    'Analyze these {video_count} video summaries and provide a comprehensive analysis.

{video_list}

Please provide a detailed analysis in JSON format with the following structure:
{
  ""mainTopic"": ""The overarching topic covered across all videos"",
  ""subtopics"": [
    {
      ""name"": ""Subtopic name"",
      ""coveredBy"": [""video1"", ""video2""],
      ""bestSource"": ""video1"",
      ""depth"": ""comprehensive|moderate|brief""
    }
  ],
  ""conflicts"": [
    {
      ""topic"": ""Topic where videos disagree"",
      ""video1Claim"": ""What video 1 says"",
      ""video2Claim"": ""What video 2 says"",
      ""aiRecommendation"": ""Which approach to use or how to present both""
    }
  ],
  ""uniqueClaims"": [
    {
      ""source"": ""video1"",
      ""claim"": ""Something only this video mentions""
    }
  ],
  ""cohesionScore"": 85,
  ""recommendations"": [
    ""These videos work well together"",
    ""Consider finding more content on X""
  ]
}

Focus on:
1. Identifying common themes vs. unique perspectives
2. Finding contradictions or different approaches
3. Determining which video explains each concept best
4. Assessing whether these videos work well together for a cohesive script',
                    0.7,
                    2000,
                    1,
                    GETUTCDATE(),
                    GETUTCDATE(),
                    0
                );

                -- Insert Script Outline prompt
                INSERT INTO Prompts (Id, Name, Version, ModelId, SystemPrompt, UserPromptTemplate, Temperature, MaxTokens, IsActive, CreatedAt, LastModifiedAt, IsDeleted)
                VALUES (
                    NEWID(),
                    'Script Outline',
                    '1.0',
                    @ModelId,
                    'You are an expert video script outliner. Create detailed, structured outlines for video scripts based on content analysis and style preferences.',
                    'Create a detailed script outline for a {target_length}-minute video based on this analysis:

{analysis_json}

=== STYLE PROFILE ===
- Tone: {tone}
- Pacing: {pacing}
- Complexity: {complexity}
- Structure Style: {structure_style}
- Hook Strategy: {hook_strategy}
- Audience Relationship: {audience_relationship}
- Information Density: {information_density}
- Rhetorical Style: {rhetorical_style}
{custom_instructions_line}

=== HOOK STRATEGY GUIDE ===
Write the hook based on the Hook Strategy setting:
- ""Insider Secret"": Open with insider knowledge framing (e.g., ""I''m gonna let you in on a secret...""). Make the viewer feel they''re getting exclusive, behind-the-scenes insight.
- ""Provocative Question"": Open with a challenging question that reframes what the viewer thinks they know. Make them question their assumptions immediately.
- ""Bold Claim"": Open with a surprising statement or statistic that stops the viewer cold. Follow with a brief promise of what the video will prove.
- ""Shared Frustration"": Open with a common pain point the audience relates to. Name the frustration specifically so viewers feel instantly understood.

=== STRUCTURE GUIDE ===
Organize sections based on the Structure Style setting:
- ""Enumerated Scaffolding"": Use numbered items, levers, or steps as clear mental scaffolding. Each section title should feel like a list item (e.g., ""Lever #1: ..."", ""Step 2: ..."").
- ""Narrative Flow"": Arrange sections as story beats with seamless transitions. Each section should flow naturally into the next like chapters in a story.
- ""Comparative Framework"": Build sections around contrasts and side-by-side analysis. Each section should compare approaches, tools, or perspectives.
- ""Preview-Deliver-Recap"": Each section previews what''s coming, delivers the content, then reinforces the takeaway before moving on.

=== AUDIENCE RELATIONSHIP GUIDE ===
Shape the outline''s voice based on the Audience Relationship setting:
- ""Insider-Outsider"": Frame the content as secrets most people miss. The viewer is being let in on knowledge the majority overlooks.
- ""Collaborative Partner"": Frame it as a shared exploration. Use language suggesting you''re figuring this out together.
- ""Mentor-Student"": Frame it as a guided learning journey. Build progressively from foundational to advanced.
- ""Peer-to-Peer"": Frame it as equals exchanging expertise. Assume shared baseline knowledge and skip basics.

Generate a structured outline in JSON format:
{
  ""title"": ""Compelling video title shaped by the tone and audience relationship"",
  ""hook"": ""Opening hook following the Hook Strategy guide above (2-3 sentences)"",
  ""sections"": [
    {
      ""title"": ""Section title following the Structure Style guide"",
      ""keyPoints"": [
        ""Point 1"",
        ""Point 2""
      ],
      ""estimatedMinutes"": 2,
      ""sourceVideos"": ""Video titles that cover this section""
    }
  ],
  ""conclusion"": ""Closing that reinforces the audience relationship and provides a clear next step""
}

Guidelines:
1. The title should reflect both the main topic and the tone setting — make it feel native to the chosen voice
2. The hook MUST follow the Hook Strategy guide above — this is the most critical element for viewer retention
3. Organize sections following the Structure Style guide — this determines the entire flow of the video
4. Each section should have 3-5 key points calibrated to the Information Density setting
5. Include which source videos are referenced in each section
6. Distribute time to hit approximately {target_length} minutes total
7. The conclusion should match the Audience Relationship — an insider wrap-up feels different from a mentor''s summary
8. All choices should feel cohesive: tone, structure, and audience relationship should reinforce each other',
                    0.7,
                    2000,
                    1,
                    GETUTCDATE(),
                    GETUTCDATE(),
                    0
                );

                -- Insert Script Generation prompt
                INSERT INTO Prompts (Id, Name, Version, ModelId, SystemPrompt, UserPromptTemplate, Temperature, MaxTokens, IsActive, CreatedAt, LastModifiedAt, IsDeleted)
                VALUES (
                    NEWID(),
                    'Script Generation',
                    '1.0',
                    @ModelId,
                    'You are an expert video script writer. Generate engaging, well-structured video scripts based on outlines and source transcripts.',
                    'Generate a complete video script based on this outline:

{outline_json}

=== FULL STYLE PROFILE ===
- Tone: {tone}
- Pacing: {pacing}
- Complexity: {complexity}
- Structure Style: {structure_style}
- Hook Strategy: {hook_strategy}
- Audience Relationship: {audience_relationship}
- Information Density: {information_density}
- Rhetorical Style: {rhetorical_style}
{custom_instructions_line}

=== TONE INSTRUCTIONS ===
Write the entire script in the voice defined by the Tone setting:
- ""Insider Conversational"": Confident and direct. Use ""you"" constantly to maintain intimacy. Balance expertise with accessibility — never condescending but clearly informed. Use contractions naturally (""it''s"", ""you''re"", ""don''t""). Share knowledge as insider secrets. Example: ""I''m gonna let you in on a secret that most people completely miss...""
- ""Informed Skeptic"": Critical of defaults without being cynical. Question common assumptions and explain why they fall short. Honest about limitations while offering better alternatives. Example: ""Everyone tells you to do X, but here''s why that advice is actually holding you back...""
- ""Enthusiastic Guide"": Energetic and encouraging. Celebrate discoveries and breakthroughs. Use exclamation points sparingly but genuinely. Show authentic excitement about the subject. Example: ""This is the part that changed everything for me, and I think it''s going to click for you too...""
- ""Authoritative Expert"": Commanding and precise. Lead with data, evidence, and specifics. State conclusions with confidence. Minimal hedging. Example: ""The data is clear on this: three factors determine the outcome, and most people only optimize for one...""

=== PACING INSTRUCTIONS ===
Control the rhythm and speed of the script based on the Pacing setting:
- ""Build-Release"": Create tension by setting up a problem or misconception, then deliver the solution or insight. Vary speed — slow down for technical explanations, accelerate through familiar territory. Use strategic pauses for emphasis. Pattern: tension → insight → breathing room → next tension.
- ""Rapid-Fire"": Quick micro-transitions between related points without dwelling. High sentence variety — mix short punchy statements with longer analytical ones. Move fast but never sacrifice clarity. Every sentence carries information or reinforcement. Minimal filler.
- ""Steady Cruise"": Consistent moderate pace throughout. Give each point equal breathing room. Smooth, predictable transitions. Comfortable cadence that lets complex ideas land without rushing.
- ""Deliberate Deep-Dive"": Slow and thorough exploration of each point. Extended explanations with multiple examples per concept. Pause to let important ideas sink in. Prioritize depth over breadth.

=== COMPLEXITY INSTRUCTIONS ===
Manage how deeply concepts are explained based on the Complexity setting:
- ""Layered Progressive"": Start each concept with a metaphor or analogy, move to the actual mechanism, then show practical application. Introduce ideas in digestible chunks before adding nuance. Example: Start with ""Think of it like a restaurant menu..."" then explain the actual system, then show how to use it.
- ""Assumes Competence"": Skip foundational explanations. Speak to an audience that already understands the basics. Use technical terms without over-explaining but provide enough context for the specific insight. Don''t talk down but still educate on the new material.
- ""Ground-Up"": Build from absolute fundamentals. Define terms before using them. Use step-by-step explanations. Never assume prior knowledge. Include ""if you''re wondering what X means..."" clarifications.
- ""Technical Immersion"": Full technical depth. Use domain-specific jargon freely. Expect the audience to keep up. Focus on precision and specificity over accessibility. Reference papers, specifications, and technical details.

=== STRUCTURE INSTRUCTIONS ===
Organize the script''s flow based on the Structure Style setting:
- ""Enumerated Scaffolding"": Use numbered items, levers, steps, or rules as the backbone. Each major point gets a clear label (""Lever number one..."", ""The second thing you need to know...""). Create mental scaffolding the viewer can hold onto. Transitions reference the numbering: ""Now that we''ve covered lever two, lever three is where it gets interesting.""
- ""Narrative Flow"": Tell it as a continuous story. Each section flows into the next through cause-and-effect or natural progression. No visible structure — just seamless movement from one idea to the next. Use phrases like ""and that leads us to..."" or ""which is exactly why..."".
- ""Comparative Framework"": Build around contrasts. Constantly compare approaches, tools, or perspectives side by side. Use ""unlike X, Y does..."" and ""where A falls short, B excels because..."". Each section should feel like a fair evaluation.
- ""Preview-Deliver-Recap"": Tell viewers what''s coming (""In the next section, we''ll cover...""), deliver the content, then reinforce (""So what we just learned is...""). Every major idea gets triple exposure: preview, explanation, summary.

=== HOOK INSTRUCTIONS ===
The opening must follow the Hook Strategy setting from the outline. Execute it with full commitment — the first 10 seconds determine whether viewers stay.

=== AUDIENCE RELATIONSHIP INSTRUCTIONS ===
Maintain the relationship dynamic throughout the entire script based on the Audience Relationship setting:
- ""Insider-Outsider"": The viewer is learning secrets most people miss. Create an us-vs-them dynamic with ""most people"" or ""the default approach."" Make the viewer feel like they''re part of an exclusive group now. Anticipate objections: ""And if you''re thinking X, here''s why that''s actually..."".
- ""Collaborative Partner"": Use ""we"" language. Frame discoveries as shared: ""Let''s figure this out together."" Acknowledge when things are tricky: ""This is the part that trips everyone up, myself included."" The choice belongs to the viewer: ""The choice is yours"" vs ""you must do this.""
- ""Mentor-Student"": Guide with warmth and patience. Use encouraging language: ""You''re going to get this."" Build confidence progressively. Celebrate small wins along the way: ""See how that works? You just did something most people never figure out.""
- ""Peer-to-Peer"": Speak as equals with shared expertise. Reference common experiences: ""You''ve probably run into this already."" Skip pleasantries and get straight to the insight. Respect the viewer''s time and intelligence.

=== INFORMATION DENSITY INSTRUCTIONS ===
Control how much detail goes into each paragraph based on the Information Density setting:
- ""High Density"": Pack multiple specific details per paragraph. Include actual settings names, feature names, real examples, and proof points. Scatter evidence throughout — mention papers, statistics, concrete cases. No unnecessary backstory. Get to the point quickly.
- ""Balanced"": Mix detailed passages with breathing room. Alternate between dense informational paragraphs and lighter transitional ones. Include examples but don''t overwhelm.
- ""Focused Essentials"": Key points only. Strip away tangents and nice-to-knows. Each paragraph makes one clear point with one supporting example. Prioritize clarity over comprehensiveness.
- ""Story-Rich"": Fewer facts, more extended examples and narratives. Develop each example fully — set the scene, show the problem, reveal the solution. Let stories carry the teaching.

=== RHETORICAL STYLE INSTRUCTIONS ===
Apply rhetorical devices based on the Rhetorical Style setting:
- ""Extended Metaphors"": Introduce a metaphor early and carry it through multiple sections. Let the analogy do the heavy lifting for complex concepts. Return to it for callbacks: ""Remember our restaurant analogy? This is like the kitchen finally getting your actual order."" Maintain the metaphor across paragraphs for cohesion.
- ""Direct and Punchy"": Short, declarative statements. Active voice dominates (""The model learns"" not ""learning is performed by the model""). Minimal filler words. Every sentence earns its place. Strategic repetition of key phrases for emphasis.
- ""Socratic"": Guide through rhetorical questions. ""You catch the keyword, right?"" ""So what happens when you actually try this?"" Let the viewer arrive at conclusions by following your questions. Don''t just state — ask, then reveal.
- ""Parenthetical Asides"": Add conversational interjections that build personality. Use asides to acknowledge the viewer''s probable thoughts: ""(by the way, if you''re wondering why this matters, hang on — it''s coming)"". Break the fourth wall occasionally. These create intimacy and authenticity.

=== LANGUAGE STYLE (APPLY TO ALL SCRIPTS) ===
- Use sentence variety: mix short punchy statements with longer analytical sentences
- Active voice dominates over passive voice
- Use conversational contractions when the tone allows it
- Strategic repetition of key phrases for emphasis and reinforcement
- Every sentence should carry information or reinforcement — cut filler
- Concrete examples over abstractions: don''t just say ""be specific,"" show the difference

Reference transcripts (use these for specific details and examples):
{transcript_section}

Generate the script in JSON format:
{
  ""scriptText"": ""Full narration text with section breaks clearly marked"",
  ""wordCount"": 2150,
  ""estimatedDurationSeconds"": 860,
  ""internalNotes"": {
    ""section1"": {
      ""sources"": [""video1"", ""video2""],
      ""conflictsResolved"": [""Chose video 1''s approach because...""],
      ""uniqueClaims"": [""Only video 1 mentioned this...""]
    }
  }
}

Script formatting rules:
1. Mark section breaks clearly (e.g., ""[SECTION: Introduction]"")
2. Target speaking rate: ~150 words per minute
3. When videos conflict, either choose the better approach or present both perspectives — match this to the tone
4. Include specific examples and explanations drawn from the source transcripts
5. Internal notes are for transparency — don''t include them in the scriptText
6. The script must feel like a single cohesive voice throughout — all style settings should work together, not fight each other',
                    0.7,
                    8000,
                    1,
                    GETUTCDATE(),
                    GETUTCDATE(),
                    0
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Remove the 3 new prompts
                DELETE FROM Prompts WHERE Name IN ('Script Analysis', 'Script Outline', 'Script Generation') AND Version = '1.0';

                -- Reactivate the old MVP prompt
                UPDATE Prompts SET IsActive = 1 WHERE Name = 'Script Analysis (MVP)';
            ");
        }
    }
}
