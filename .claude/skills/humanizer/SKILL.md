---
name: humanizer
description: Use when cleaning AI-writing tells out of a commit body, CHANGELOG entry, issue/PR, or doc. Strips slop and long dashes; keeps TAOM's boldface house style.
allowed-tools:
  - Read
  - Write
  - Edit
  - Grep
  - Glob
  - AskUserQuestion
---

# /humanizer — Strip AI-Writing Tells From Prose

Rewrite a piece of prose so it reads as deliberate human writing instead of generic LLM output. Ported from [blader/humanizer](https://github.com/blader/humanizer) (MIT), whose pattern list derives from Wikipedia's [Signs of AI writing](https://en.wikipedia.org/wiki/Wikipedia:Signs_of_AI_writing). Adapted for TAOM: the boldface / inline-header patterns are **carved out** because TAOM uses them deliberately (see the carve-out below). The em-dash carve-out was **reversed on 2026-08-11**, so the upstream's dash handling now applies in full.

The everyday, always-on form of this discipline is `.claude/rules/output-style.md` Part 2 (an always-load rule that keeps the worst tells out of new prose as you write it). This skill is the **deep-clean tool** — invoke it to pass an already-written artifact through the full pattern list.

## Core insight

> "LLMs use statistical algorithms to guess what should come next. The result tends toward the most statistically likely result that applies to the widest variety of cases."

That bias is the source of every pattern below: text drifts toward generic, broadly-applicable phrasing. The fix is almost always **more specific and more concrete**, not more "polished."

The diagnostic strength is in **clusters**, not isolated instances. One formal word is not AI slop. A paragraph stacking significance inflation + vague attribution + rule-of-three + a generic conclusion is. **The long dashes are the one exception**: a single em or en dash is enough on its own, which is why it gets a hard rule rather than a cluster judgement.

## When to use

- Cleaning a feature doc, RCA, or README before it ships
- Tightening a long GitHub issue / PR body
- A commit body or CHANGELOG narrative that reads like marketing copy
- The user hands you a block of AI-generated text and asks to humanize it
- Voice-matching: the user provides a writing sample and wants the rewrite in their voice

For day-to-day writing, you don't need to invoke this — `output-style.md` Part 2 already governs new prose. Reach for the skill when you want a thorough, pattern-by-pattern pass over a finished artifact.

## TAOM house-style carve-out (READ FIRST)

TAOM's knowledge base uses several "AI tells" as **deliberate semantic markers**. Do **NOT** strip these, and do **NOT** mass-rewrite existing docs to remove them:

| Upstream pattern | TAOM disposition | Why |
|---|---|---|
| #14 em/en dashes | **STRIP** | Carve-out **reversed 2026-08-11** on user standing instruction: the long dashes are the loudest AI tell, so produced prose does not use them. 40,476 predate the rule (CHANGELOG 1,604 / docs 37,448 / `.claude` 1,424, counted that day) and are left alone. See `.claude/rules/output-style.md` Part 2. |
| #15 boldface | **KEEP** | Status / finding / constraint markers (`**Status: BUILT**`, `**Correct fix:**`). Semantic, not ornamental. |
| #16 inline-`**Label:**` lists | **KEEP** | The data-dense bullet format TAOM docs rely on. |
| markdown tables, backticked paths/code | **KEEP** | Core to every TAOM doc. |
| #17 Title Case headings | n/a | TAOM already uses sentence case, so this rule is a no-op here. |

Everything else in the list below applies. The skill removes slop **without** touching TAOM's visual-hierarchy conventions.

**Dash exemptions** (same four as the rule): fenced blocks and inline code spans; URLs and link targets; text quoted verbatim from outside TAOM, which a rewrite would falsify; and existing prose you are not otherwise touching.

## Workflow

1. **Identify** the patterns present (scan against the table). Note clusters, not lone instances.
2. **Rewrite, don't delete** — replace each AI construction with a concrete, natural alternative. Preserve the original scope, depth, and every load-bearing fact.
3. **Preserve meaning** — the rewrite must say everything the original said. Humanizing is not summarizing.
4. **Match the voice** — fit the artifact's register (a commit body, an RCA, and a README have different voices). Add personality only where the content wants it; a build-error note doesn't.

## Voice calibration

When the user provides a writing sample (inline or a file path):

- Note sentence-length rhythm, vocabulary level, how paragraphs open, punctuation habits, recurring phrasings, and how they transition between ideas.
- Match the rewrite to those patterns instead of producing generic "clean" output.

Without a sample, default to natural, varied, opinionated prose appropriate to the artifact type.

## Audit pass (the second rewrite)

After the first-draft rewrite:

1. Ask yourself: **"What makes the text below still read as obviously AI-generated?"** Answer briefly — list the remaining tells.
2. Produce a final version that addresses each one.
3. Present: the draft, the bullets of remaining tells, and the final rewrite. Offer a short summary of what changed if it's a large edit.

(The upstream's "zero em/en dashes" hard constraint applies in full: the final version must contain neither character outside the four exemptions above. TAOM dropped this constraint until 2026-08-11; it is back.)

## What NOT to flag (over-editing protection)

These are not AI tells. Leave them alone:

- Perfect grammar or consistent style on their own
- Mixed casual / formal registers
- Formal or academic vocabulary used correctly
- A single transition word, or one short emphatic sentence (a lone em-dash used to be listed here; as of 2026-08-11 it is always flagged)
- An unsourced claim that has no other clustering tells

**Preserve these human signals where they exist:** specific hard-to-fabricate detail; mixed feelings and unresolved tension; dated references tied to an era or subculture; genuine asides and self-corrections; varied sentence length.

## The 33 patterns

### Content

| # | Pattern | Before → After |
|---|---------|----------------|
| 1 | Significance inflation | "marking a pivotal moment in the evolution of…" → "was established in 1989 to collect regional statistics" |
| 2 | Notability name-dropping | "cited in NYT, BBC, FT, and The Hindu" → "In a 2024 NYT interview, she argued…" |
| 3 | Superficial -ing analyses | "symbolizing… reflecting… showcasing…" → remove or expand with actual sources |
| 4 | Promotional language | "nestled within the breathtaking region" → "is a town in the Gonder region" |
| 5 | Vague attributions | "Experts believe it plays a crucial role" → "according to a 2019 survey by…" |
| 6 | Formulaic challenges | "Despite challenges… continues to thrive" → specific facts about the actual challenges |

### Language

| # | Pattern | Before → After |
|---|---------|----------------|
| 7 | AI vocabulary | "testament… landscape… showcasing… additionally" → "also… remain common" |
| 8 | Copula avoidance | "serves as… features… boasts" → "is… has" |
| 9 | Negative parallelisms / tailing negations | "It's not just X, it's Y", "…, no guessing" → state the point directly |
| 10 | Rule of three | "innovation, inspiration, and insights" → use the natural number of items |
| 11 | Synonym cycling | "protagonist… main character… central figure… hero" → "protagonist" (repeat when clearest) |
| 12 | False ranges | "from the Big Bang to dark matter" → list the topics directly |
| 13 | Passive voice / subjectless fragments | "No configuration file needed" → name the actor when it aids clarity |

### Style

| # | Pattern | Before → After |
|---|---------|----------------|
| 14 | **Em/en dashes** | `the guard fires early — before state init` → comma, colon, semicolon, parentheses, or a new sentence. Hyphens (`--RunTests`, `v1.4.8`) stay legal. |
| 15 | **Boldface overuse** | **TAOM EXCEPTION — KEEP.** Semantic status/finding markers. |
| 16 | **Inline-header lists** (`**Label:**`) | **TAOM EXCEPTION — KEEP.** Data-dense format. |
| 17 | Title Case headings | "Strategic Negotiations And Partnerships" → "Strategic negotiations and partnerships" (TAOM already does this) |
| 18 | Emojis | "🚀 Launch Phase: 💡 Key Insight:" → remove |
| 19 | Curly quotes | smart quotes → straight quotes |
| 26 | Hyphenated word pairs | "cross-functional, data-driven, client-facing" → drop hyphens on common pairs |
| 27 | Persuasive authority tropes | "At its core, what matters is…" → state the point directly |
| 28 | Signposting announcements | "Let's dive in", "Here's what you need to know" → start with the content |
| 29 | Fragmented headers | "## Performance" + "Speed matters." → let the heading do the work |
| 30 | Diff-anchored writing | "This function was added to replace…" → describe what it does, not what changed |
| 31 | Manufactured punchlines / staccato drama | "It had no preference. No prior. No nostalgia." → varied sentence lengths, concrete claims |
| 32 | Aphorism formulas | "Symmetry is the language of trust" → replace the formula with the actual claim |
| 33 | Conversational rhetorical openers | "Honestly? It depends…" → remove the fake-candid setup |

### Communication

| # | Pattern | Before → After |
|---|---------|----------------|
| 20 | Chatbot artifacts | "I hope this helps! Let me know if…" / offer-to-continue closers → remove |
| 21 | Cutoff / speculative gap-filling | "While details are limited in available sources…", "maintains a low profile" → find sources or remove |
| 22 | Sycophantic tone | "Great question! You're absolutely right!" → respond directly (see `output-style.md` Part 1 Rule 1) |

### Filler and hedging

| # | Pattern | Before → After |
|---|---------|----------------|
| 23 | Filler phrases | "In order to", "Due to the fact that" → "To", "Because" |
| 24 | Excessive hedging | "could potentially possibly" → "may" |
| 25 | Generic conclusions | "The future looks bright" → specific plans or facts |

## Relationship to other rules

- `.claude/rules/output-style.md` — the always-load companion (merged 2026-08-05 from `response-style.md` + `ai-prose-style.md`): Part 1 owns **reply openings + confidence tags + the anti-sycophancy reflex** (pattern #22); Part 2 is the high-value subset of this list applied automatically to new prose. This skill is the on-demand deep-clean.
- `.claude/rules/evidence-over-claims.md` §C — never invent the "facts" you write into a humanized doc.

## Source

Ported from [blader/humanizer](https://github.com/blader/humanizer) (MIT license), version 2.8.0, whose 33-pattern catalogue is drawn from Wikipedia's [Signs of AI writing](https://en.wikipedia.org/wiki/Wikipedia:Signs_of_AI_writing) (WikiProject AI Cleanup). TAOM adaptations: dropped upstream `version:`/`compatibility:`/`license:` frontmatter (not Claude Code fields); carved out boldface (#15) and inline-headers (#16) as deliberate TAOM conventions; pointed sycophancy (#22) at `output-style.md` Part 1 to avoid duplication.

**Reversed 2026-08-11:** em-dashes (#14) were carved out too, and the audit pass had the upstream's "zero em-dashes" constraint removed. Both are back on user standing instruction, so TAOM now matches the upstream on dashes and diverges only on boldface and inline headers.
