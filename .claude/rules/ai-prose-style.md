# AI-Prose Style

When you write longform prose, make it read as deliberate human writing, not generic LLM output. LLMs drift toward the most statistically likely, broadly-applicable phrasing; the fix is almost always **more specific and concrete**, not more "polished."

## Scope

Governs prose Claude *produces*: commit bodies, CHANGELOG entries, GitHub issue / PR bodies, feature docs, RCA write-ups, doc paragraphs. It does **not** govern code comments (their own density rule applies) or chat replies (`response-style.md` owns those). Applies to **new work** — don't rewrite existing docs to comply; use `/humanizer` for opt-in spot-cleans of a finished artifact.

## The tells to avoid (write the right side)

- **Significance inflation** — "marks a pivotal moment in the evolution of…" → the concrete fact.
- **Vague attributions** — "experts believe", "studies show" → name the source or drop the claim.
- **Rule of three** — "innovation, inspiration, and insights" → use the natural number of items.
- **AI vocabulary** — testament / landscape / showcasing / "additionally" / "delve" → plain words.
- **Filler phrases** — "in order to", "due to the fact that" → "to", "because".
- **Excessive hedging** — "could potentially possibly" → "may", or state it.
- **Generic conclusions** — "the future looks bright", "exciting times ahead" → specific plans or facts.
- **Manufactured punchlines / staccato drama** — "No preference. No prior. No nostalgia." → varied sentences, concrete claims.
- **Aphorism formulas** — "X is the language of Y" → the actual claim.
- **Signposting** — "let's dive in", "here's what you need to know" → start with the content.
- **Chatbot artifacts** — "I hope this helps!", offer-to-continue closers → remove.

(Sycophancy — "great question", "you're absolutely right" — is already banned by `response-style.md` Rule 1. Don't restate it; it applies here too.)

## TAOM house-style carve-out

Em-dashes, **boldface**, inline-`**Label:**` headers, markdown tables, and backticked paths/code are **deliberate TAOM semantic markers** — keep them. The upstream humanizer cuts em-dashes and boldface; TAOM does not. (`/humanizer` carries the full carve-out + the complete 33-pattern reference for deep cleaning.)

## Relationship to other rules

- `response-style.md` — reply openings + confidence tags + the anti-sycophancy reflex. That's *chat*; this is *artifacts*.
- `evidence-over-claims.md` §C — never invent the facts you write into a doc. Concrete-and-fabricated is worse than vague-and-honest.
- `/humanizer` skill — the on-demand deep-clean tool and full pattern catalogue.

## Source

Imported from [blader/humanizer](https://github.com/blader/humanizer) (MIT), whose patterns derive from Wikipedia's "Signs of AI writing". This rule is the high-value, TAOM-carve-out subset applied always-on; the skill is the full reference.
