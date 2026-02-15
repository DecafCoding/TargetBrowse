Analyze the following video transcript and extract the informational substance. Strip out everything related to the speaker''s delivery style, voice, pacing, and craft. Your job is to pull out WHAT the speaker is saying — not HOW they say it.

Do NOT summarize. Do NOT generalize. Preserve every specific fact, feature name, example, data point, and argument at full resolution. If the speaker names a specific platform feature, capture the feature name and how it works. If they give a concrete example, capture the full example. If they cite a number, capture the number and its context.

Keep metaphors and analogies — these carry meaning and help the script writer understand the concepts being explained.

---

## What to Extract

### Main Topics and Subtopics
- What are the major topics covered?
- What subtopics fall under each?
- How do the topics relate to each other (what argument is being built)?

### Platform-Specific Features and Settings
For each platform mentioned (ChatGPT, Claude, Gemini, etc.), list:
- Every specific feature name mentioned (e.g., "project-scoped memory," "custom instructions," "style controls")
- How the feature works (as described in the transcript)
- Any specific settings, menus, or UI elements referenced
- Any limitations or quirks mentioned
- Any comparisons made between platforms on the same feature

### Concrete Examples
- Every specific example the speaker gives (e.g., "Remember that I prefer one-sentence answers to factual questions")
- Every "do this, not that" comparison with both sides preserved
- Every workflow or step-by-step process described
- Every real-world scenario used to illustrate a point

### People, Data, and Evidence
- Names mentioned and their context (e.g., "Boris Churnney, who created Claude Code, ships roughly 100 PRs a week")
- Any statistics, numbers, or data points cited
- Any papers, articles, or external sources referenced
- Any specific claims about how AI systems work (e.g., RLHF training process details)

### Key Arguments and Conclusions
- What is the speaker''s core thesis? State it as a specific claim.
- What supporting arguments does the speaker make? List each as a concrete position, not a topic label.
- What counterarguments or limitations does the speaker acknowledge?
- What specific advice or calls to action does the speaker give?

### Metaphors and Analogies (preserved for context)
- List every metaphor and analogy with the full context of how it was used
- Note which concept each metaphor was explaining
- If a metaphor recurs, note each usage

---

## What to Leave Out

- Speaker''s tone, delivery style, and speech patterns
- Transition phrases and structural scaffolding
- Emotional appeals and tension-building techniques
- Rhetorical questions used for effect (unless they contain a factual claim)
- Filler, repetition, and conversational asides that don''t carry information
- Sponsor segments and calls to subscribe/follow

---

## Output Format

```json
{
  "videoTitle": "Title of the video",
  "coreThesis": "The speaker''s main argument in one specific sentence",
  "topicStructure": [
    {
      "topic": "Main topic name",
      "subtopics": ["subtopic 1", "subtopic 2"],
      "relationship": "How this topic connects to the next"
    }
  ],
  "platformFeatures": {
    "ChatGPT": [
      {
        "feature": "specific feature name",
        "howItWorks": "description as given in transcript",
        "settings": "any UI elements or settings mentioned",
        "limitations": "any caveats mentioned"
      }
    ],
    "Claude": [],
    "Gemini": [],
    "Other": []
  },
  "concreteExamples": [
    {
      "example": "the full example as described",
      "illustrates": "what concept it was demonstrating",
      "timestamp": "00:00:00"
    }
  ],
  "doThisNotThat": [
    {
      "bad": "the ineffective approach",
      "good": "the effective approach",
      "why": "the reasoning given"
    }
  ],
  "workflows": [
    {
      "description": "what the workflow accomplishes",
      "steps": ["step 1", "step 2"]
    }
  ],
  "peopleAndEvidence": {
    "people": [{"name": "", "context": ""}],
    "statistics": [{"stat": "", "source": "", "context": ""}],
    "externalSources": [{"source": "", "context": ""}],
    "technicalClaims": [{"claim": "", "detail": ""}]
  },
  "arguments": {
    "supportingArguments": ["specific claim 1", "specific claim 2"],
    "limitations": ["acknowledged limitation 1"],
    "callsToAction": ["specific advice 1"]
  },
  "metaphors": [
    {
      "metaphor": "the analogy or metaphor used",
      "explains": "what concept it illustrates",
      "recurrences": ["how it was used again later, if applicable"]
    }
  ]
}
```

---

## Transcript to Analyze

{transcript}
