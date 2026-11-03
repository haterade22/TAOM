---
name: harness-retro
description: Use on the HARNESS RETRO DUE banner, or when asked what the harness keeps getting wrong or whether a rule, hook or skill change helped.
---

# Harness retro

Measure TAOM's own harness from the session transcripts, give each recent harness change a
verdict, and propose at most five evidence-backed edits. The full procedure, the verdict table and
the pitfalls are in **[docs/ai-includes/harness-retro.md](../../../docs/ai-includes/harness-retro.md)**:
read it first.

## Phases

1. **Evidence:** `python tools/harness_retro.py`, then `python tools/analyze_reviews.py --summary --no-plot`. Exit 3 UNCHECKED means stop. Section 9 is tool output size by tool, shell family and file read, with growth against the previous run; an `output growth: UNCHECKED` line only means no earlier run. Section 10 also splits tokens by agent type and model, with the costliest transcripts, model overrides and `token growth:` against the previous run. Section 11 prices the usage at list rates (a proxy for relative size): by agent, by main-session context size, cache rebuilds by idle gap, and review runs. Compare it with the last retro's numbers by hand.
2. **Changes:** `git log --since <last retro>` over the harness paths, plus the window's RCA preventive actions.
3. **Verdicts:** helped, hurt, no observed effect, confounded or too early, one row per change older than seven days.
4. **Proposals:** at most five, each naming its session moment; every section 9 growth row, section 10 token growth row and model override is a candidate; cheapest form first; no change is a valid result.
5. **Approval:** one proposal at a time (what, pros, cons, recommendation); nothing is edited before Mike approves.
6. **Record:** `docs/reviews/harness-retro-<date>.md`, aggregates and session ids only, never transcript text; always section 9's top families and growth table.
