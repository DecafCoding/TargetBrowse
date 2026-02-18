# Script Generation Prompt (Revised)

You are writing words that will come out of someone's mouth on camera. Not an article. Not a blog post. Not a script that "sounds conversational." Actual speech — the kind where someone loses their train of thought for half a second, catches it, and keeps going. The kind where a sentence fragment hits harder than a complete one. The kind where "Right?" does more work than a full paragraph of explanation.

Here's your test for every single line: read it out loud. If it sounds like someone wrote it, rewrite it. If it sounds like someone said it, keep it.

---

## The Three Things That Matter Most

These are in priority order. When they conflict, the higher one wins.

**1. It has to sound like a person talking.**

Not a person reading. Not a person performing. A person thinking out loud to a camera, explaining something they genuinely understand and care about. That means:

- Sentence fragments that land: "Papa John's, Pizza Hut, you get the idea."
- Self-corrections mid-thought: "It's not just good — it's actually the single biggest lever you have."
- Filler that creates rhythm, not noise: "Look," "Right?" "You know," "And here's the thing."
- Opinions dropped in like they're obvious: "I can tell in context that it's still not a very good memory implementation, it misses stuff I would consider obvious."
- Moments where the speaker talks TO the viewer: "You are being averaged out into a median AI user and I'm interested in you understanding the levers."

What this does NOT mean:
- Sprinkling "you know" and "right?" onto polished sentences. That's decoration, not voice.
- Writing clean prose and then roughing it up. Start messy. Start like you're actually explaining this to someone sitting across from you.

**2. Every claim needs a name, a number, or a specific feature.**

Never say "AI platforms offer customization options." Say "ChatGPT has eight different personalities. Claude has different style summaries. We're going to get into that."

Never say "power users get dramatically more done." Say "Boris Churnney runs five Claude instances in parallel and another five to ten on claude.ai and ships roughly 100 PRs a week."

Never say "memory helps personalize your experience." Say "Claude's memory is project-scoped at default. So every project has a very separate memory space and your startup discussions don't bleed into your vacation planning."

The specifics should arrive mid-conversation, dropped in as if the speaker just knows this stuff — not organized into comparison tables or feature lists. They come up when they're relevant and they come up naturally.

**3. When you use a metaphor, commit to it.**

Don't introduce an analogy, use it once, and move on. That's decoration. A real extended metaphor becomes the structure of the explanation.

Look at how this works in practice. The pizza restaurant metaphor isn't mentioned and then abandoned — it IS the argument:
- First: "Imagine a restaurant that wants to create one dish to satisfy the widest possible range of customers."
- Then it extends: "The chef studies what most diners order. They analyze which flavors get consistent approval across different demographics and they just optimize for the middle."
- Then it gets specific: "It's edible. It's competent. It's technically fine. You can make the cheese look nice on an ad, but not your preference, right?"
- Then it connects: "This is exactly what AI does with answers. It's like the Pizza Hut approach."
- Then it comes back later when needed for a new point.

The metaphor carries the argument across paragraphs. It's not one line — it's the lens the viewer uses to understand the concept.

---

## How Sections Connect

Never use these:
- "Moving on to..."
- "Let's talk about..."
- "Now let's move to..."
- "The next thing is..."
- "The first/second/third step is..."

These are slide deck transitions. They break the feeling of conversation.

Instead, the end of each section should create a gap that the next section fills. The viewer should feel WHY you're moving to the next topic before you get there.

Good: "So that's lever one. That's memory. What about lever two?" — casual, conversational, the numbering is embedded in speech, not announced.

Good: "And that fixes the personalization problem. But it doesn't fix everything..." — tension pulls the viewer forward.

Good: "But memory without clear instructions is like having a great chef who doesn't know what you ordered." — the metaphor does the transition work.

Bad: "Now that we've covered memory, let's discuss instructions." — nobody talks like this.

---

## Making the Viewer Feel the Stakes

Don't describe problems at arm's length. Make the viewer feel them personally.

Bad: "Many users find that default AI settings produce suboptimal results."
Good: "Nobody gets 10x results from default vanilla ChatGPT, vanilla Claude, vanilla Gemini. It just isn't how it works."

Bad: "The training process can lead to generic outputs."
Good: "You are being averaged out into a median AI user."

Bad: "There are misconceptions about AI capabilities."
Good: "Most people experience this and they think to themselves, well, the AI is just okay, right? It's probably the AI's issue. They don't realize there's a mechanical reason and they don't realize that it's fixable."

The heaviest emotional weight goes in the first 60 seconds and at the top of each new section. Then you release into the tactical, specific content. This creates a rhythm: tension, then information. Tension, then information.

---

## How Information Density Should Flow

Don't maintain the same level of detail throughout. That makes everything feel flat.

The shape should be:
- **Opening:** Loose, emotional, hook-driven. Short punchy statements. Establish the problem viscerally. Not a lot of technical detail yet.
- **Early sections:** Introduce the core metaphor. Explain the WHY — the mechanical reason things are broken. Start getting specific but keep the rhythm conversational.
- **Middle sections:** Maximum density. This is where you pack in platform-specific features, actual setting names, real workflows, concrete examples. The viewer is engaged now — give them the goods. Rattle off specifics the way someone who knows this stuff cold would.
- **Late sections:** Still dense but shift toward "here's what to actually do." Actionable, specific, practical.
- **Conclusion:** Pull back. Shorter. Punchier. Forward-looking. Motivational but earned — it works because you've delivered the substance already.

---

## Structure and Length

**Target word count: {wordCount} words** (approximately {estimatedDurationSeconds} seconds at ~150 words per minute).

The introduction/hook needs 150-250 words. The conclusion needs 150-200 words. Everything in between should be as long as it needs to be to cover the material with real specificity — aim for at least 400 words per major section.

If a section feels thin, it needs more specific examples, more metaphor development, or more platform-by-platform detail. Never pad with generic statements or motivational filler.

---

## Style Profile

These parameters work together as a single voice. They're listed in priority order — when two conflict, go with the higher one.

- Tone: {tone}
- Pacing: {pacing}
- Complexity: {complexity}
- Structure Style: {structure_style}
- Hook Strategy: {hook_strategy}
- Audience Relationship: {audience_relationship}
- Information Density: {information_density}
- Rhetorical Style: {rhetorical_style}
{custom_instructions_line}

### Tone
- **"Insider Conversational"**: Confident and direct. Use "you" constantly. Balance expertise with accessibility — never condescending but clearly informed. Contractions everywhere. Share knowledge like insider secrets. "I'm gonna let you in on a secret that most people completely miss..."
- **"Informed Skeptic"**: Critical of defaults without being cynical. Question common assumptions. Honest about limitations while offering better alternatives. "Everyone tells you to do X, but here's why that advice is actually holding you back..."
- **"Enthusiastic Guide"**: Energetic and encouraging. Celebrate discoveries and breakthroughs. Sparingly use exclamation points. Show authentic excitement. "This is the part that changed everything for me, and I think it's going to click for you too..."
- **"Authoritative Expert"**: Commanding and precise. Lead with data, evidence, specifics. State conclusions with confidence. Minimal hedging. "The data is clear on this: three factors determine the outcome, and most people only optimize for one..."

### Pacing
- **"Build-Release"**: Set up tension, deliver the insight. Vary speed — slow for technical, fast through familiar territory. Pattern: tension → insight → breathing room → next tension.
- **"Rapid-Fire"**: Quick transitions, high sentence variety. Mix short punchy statements with longer analytical ones. Every sentence carries information. Minimal filler.
- **"Steady Cruise"**: Consistent moderate pace. Equal breathing room per point. Smooth transitions.
- **"Deliberate Deep-Dive"**: Slow, thorough. Multiple examples per concept. Let important ideas sink in.

### Complexity
- **"Layered Progressive"**: Start each concept with metaphor, move to mechanism, then application. Digestible chunks before nuance.
- **"Assumes Competence"**: Skip basics. Use technical terms without over-explaining. Don't talk down but still educate on the new material.
- **"Ground-Up"**: From fundamentals. Define terms. Step-by-step. Never assume prior knowledge.
- **"Technical Immersion"**: Full technical depth. Domain jargon. Expect the audience to keep up.

### Structure Style
- **"Enumerated Scaffolding"**: Numbered items as the backbone — but spoken naturally. "Lever number one is memory" not "The first category we will examine is memory." The numbers give the viewer something to hold onto without making it sound like a presentation.
- **"Narrative Flow"**: Continuous story. Cause-and-effect progression. No visible structure.
- **"Comparative Framework"**: Built around contrasts. Side-by-side comparisons throughout.
- **"Preview-Deliver-Recap"**: Tell them what's coming, deliver it, reinforce it.

### Hook Strategy
Execute the hook from the outline with full commitment. The first 10 seconds determine whether viewers stay.

### Audience Relationship
- **"Insider-Outsider"**: Secrets most people miss. Us-vs-them with "most people." Viewer is part of an exclusive group.
- **"Collaborative Partner"**: "We" language. Shared discoveries. The choice belongs to the viewer.
- **"Mentor-Student"**: Warm guidance. Build confidence progressively.
- **"Peer-to-Peer"**: Equals with shared expertise. Skip pleasantries. Respect their time.

### Information Density
- **"High Density"**: Multiple specific details per paragraph. Settings names, feature names, real examples. No unnecessary backstory.
- **"Balanced"**: Mix detailed passages with breathing room.
- **"Focused Essentials"**: Key points only. One clear point per paragraph, one supporting example.
- **"Story-Rich"**: Fewer facts, more extended examples and narratives.

### Rhetorical Style
- **"Extended Metaphors"**: Introduce a metaphor early and carry it through multiple sections. The analogy IS the explanation, not decoration on top of it. Return to it for callbacks that feel natural, not forced.
- **"Direct and Punchy"**: Short, declarative. Active voice. Every sentence earns its place.
- **"Socratic"**: Guide through rhetorical questions. Let the viewer arrive at conclusions.
- **"Parenthetical Asides"**: Conversational interjections that acknowledge the viewer's thoughts. Break the fourth wall.

### Language (apply to all scripts)
- Mix short punchy statements with longer analytical sentences
- Active voice dominates
- Contractions always
- Strategic repetition of key phrases
- Every sentence carries information or reinforcement — cut filler
- Show the difference, don't just say "be specific"

---

## Outline to Execute

{outline_json}

---

## Reference Transcripts

These are your source material. Draw concrete facts, specific examples, platform details, and proof points directly from them. When a transcript names a specific feature, setting, or number — use it. Never generalize what the source makes specific. Never clean up a quote that works better messy.

{transcript_section}

---

## Output

Write the script as plain text. Mark section transitions with a simple line break and a brief label like:

```
[Introduction]

Script text here...

[Memory]

Script text here...
```

That's it. No JSON. No metadata. No word count tracking. No metaphor tracker. Just write the script the way someone would actually say it, from top to bottom. The section labels are just for your own organization — they won't be read aloud.

After you finish, read the whole thing back to yourself. Every sentence that sounds like writing instead of speech, fix it. Every claim that's vague where the source material had a specific detail, fix it. Every metaphor that appears once and vanishes, either extend it or cut it.
