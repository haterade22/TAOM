---
description: Yes/No matrix for evaluating whether a change is worth keeping. Tiny gain + ugly code is rejected; deletions that hold parity always win.
---

<!-- NO paths: intentionally — always-load. See harness-facts.md "Rule loader (memory) semantics". -->

# The Simplicity Criterion

A change is judged on TWO axes simultaneously: how much it helps, and how much complexity it adds. The verdict is determined by both, not by either alone.

| Situation | Verdict | Why |
|---|---|---|
| Tiny win + added complexity (new abstraction, new helper, new flag) | **Reject** | Future readers pay forever for a benefit they won't notice. |
| Equal result + simpler code | **Keep** | Pure win. Take it. |
| Deletion that holds parity (tests still green, behavior unchanged) | **Always keep** | Deletion is the highest-leverage change. Never argue against it. |
| Improvement large enough to dominate complexity cost | **Keep**, but flag the trade-off in PR / CHANGELOG | Make the cost explicit so the next person can re-evaluate later. |
| New code "in case we need it later" | **Reject** | YAGNI. CLAUDE.md already forbids hypothetical-future plumbing. |

## How to apply

When evaluating your own change, or when reviewing inside `/deep-review` / `/review-codex`:

1. State the win in one sentence ("removes a nullable check", "saves 4ms in the hot path", "fixes a crash on save load").
2. State the cost in one sentence ("adds an interface", "adds 30 lines", "adds a config knob").
3. Match against the table. If the verdict is **Reject**, the change does not ship — even if it's "technically correct" or "more idiomatic."

> The "deletion that holds parity" test asks *is this code redundant?* — NOT the same as `/improve`'s deepening deletion test (*is this abstraction shallow?*). Don't conflate the lenses.

_Provenance (why this rule exists, relationships, sources): [docs/reference/rule-provenance.md](../../docs/reference/rule-provenance.md)._