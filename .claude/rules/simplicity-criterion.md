---
description: Yes/No matrix for evaluating whether a change is worth keeping. Tiny gain + ugly code is rejected; deletions that hold parity always win.
---

<!--
This rule has NO `paths:` field intentionally. Per Claude Code memory loader:
  - Rules WITHOUT `paths:` load at conversation start (always-on).
  - Rules WITH `paths:` (any glob, including `**/*`) load only when a matching
    file is opened — they are conditional, not unconditional.
This rule is meant to apply universally, so `paths:` is omitted.
-->


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

## Why this rule exists

The recurring failure mode in `/deep-review` agents (caught across multiple Codex review cycles, e.g. EquipPresets review #5 on 2026-05-06) is preserving scaffolding "just in case": unused enum values, never-populated status fields, "reserved for future" plumbing. This rule turns the existing CLAUDE.md "no over-engineering" guidance into a deterministic Yes/No matrix that an agent can apply without judgment calls drifting toward keep-everything.

It also gives `/deep-review` a concrete handle for the deletion-win case. A reviewer who finds a 50-line helper that nothing actually uses should not need to argue for its removal — the rule is "deletion that holds parity = always keep," full stop.

## Source

Imported from karpathy/autoresearch `program.md` "Simplicity criterion" paragraph (March 2026). The original framing was for ML hyperparameter changes ("a 0.001 val_bpb improvement that adds 20 lines of hacky code? Probably not worth it. An improvement of ~0 but much simpler code? Keep."); this rule generalizes it to any TAOM code change.