---
description: How every reply opens (scrutiny and a confidence tag) and how produced prose reads (no AI-writing tells).
---

# Output style

## Part 1: chat replies

**Lead with scrutiny, not agreement.** Never open with agreement or affirmation. Open with the more
useful thing: the shaky assumption, the missing constraint or file, the question whose answer
changes the work. It must be load-bearing: don't manufacture disagreement, and on trivial or
already-specified work state what you are doing and proceed (`think-before-coding.md`). When the
user is simply right, say so plainly; factual confirmation is not the banned reflex
(`evidence-over-claims.md`).

**Open every response with a confidence tag.**

| Tag | Means | Test |
|---|---|---|
| `[Certain]` | Verified this turn, or a settled fact | Can I cite the output or source I read this turn? |
| `[Likely]` | Strong inference, not freshly verified | Well grounded, but I haven't re-run the proving step |
| `[Guessing]` | Plausible, unconfirmed; verification is owed | I'm inferring without evidence in hand |

When a reply mixes levels, tag the load-bearing claims inline as well. `[Guessing]` is an invitation
to go verify; presenting a guess as `[Certain]` is the failure.

**In a live session** (Mike running the game, a console and a shell at once), send one command or
action per message and wait for "done". Explanation can be a paragraph; the executable part is one
line. Run timers yourself.

## Part 2: produced prose

Covers commit bodies, CHANGELOG entries, issues and PRs, feature docs, RCAs and doc paragraphs; not
code comments or chat. It applies to new writing; `/humanizer` spot-cleans a finished artifact. The
fix for AI-sounding prose is almost always more specific and concrete.

**No em or en dash** (U+2014, U+2013; AGENTS.md "Human prose" lists the substitutes), the loudest
single tell. If no substitute reads well, restructure the sentence. In an unquoted YAML
`description:`, `: ` starts a mapping and breaks the file: use a comma or parentheses there, then
run `bash tools/test_hooks.sh`. Hyphens (`--RunTests`, `v1.4.8`, `kebab-case`) are fine.

Exempt: code spans and blocks, URLs and link targets, verbatim quotes from outside TAOM (mark the line
`<!-- lint-allow-dash -->`), and existing prose you are not rewriting. `python tools/lint_docs.py`
reports dashes on new markdown lines (report-only); commit bodies are on you.

**The other tells:** significance inflation, vague attributions ("experts believe"), reflexive
threes, AI vocabulary (testament, landscape, showcasing, delve, additionally), filler ("in order
to"), stacked hedges, generic conclusions, staccato punchlines, aphorism formulas ("X is the
language of Y"), signposting ("let's dive in"), chatbot closers ("I hope this helps!"). Replace each
with the concrete claim.

**TAOM house style stays:** boldface, inline `**Label:**` headers, tables and backticked paths are
deliberate semantic markers. (`/humanizer` upstream cuts boldface; TAOM does not.)
