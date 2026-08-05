# Rule Provenance — `.claude/rules/` always-load set

> Extracted from the always-load rules 2026-08-05 (eager-context diet round 2). The rules keep
> their operative text + a one-line pointer here; this file holds each rule's "Why this rule
> exists" / "Relationship to other rules" / "Source" sections verbatim. These are import history
> and cross-rule commentary — read when auditing or editing a rule, not needed on every turn.

## evidence-over-claims.md

### Why this rule exists

Unverified agreement, unverified success, and invented facts are the three cheapest things an LLM produces and the three most expensive things a user discovers later: a "confirmed" finding that breaks a working path, a "done" feature that never compiled, a findings doc whose every detail was made up. All three come from optimizing for the appearance of progress over the fact of it. TAOM's workflow is review-heavy and auto-applies findings, which multiplies the cost of getting any of them wrong. The fabrication facet (C) was added 2026-05-30 after a hotfix-review doc + CHANGELOG were authored before the proving `diff` output was read — the *conclusion* was right but the *evidence* was invented (wrong changed-type list, a "fix" that never happened, a "47 broken refs" count when the real count was 0). RCA-in-memory: `feedback_no_write_before_reading_tool_output.md`.

### Relationship to other rules

- `think-before-coding.md` fires *before* the change (is this the right change?); this rule fires *around feedback and completion* (is this finding real? is this actually done?).
- `simplicity-criterion.md` is a legitimate basis for pushing back on a finding ("rejecting: adds an abstraction for a tiny win").
- `/investigate`, `/verify`, `/deep-review`, `/review-codex` are the command-form workflows; this rule is the always-on behavior underneath them.
- **Fork-discipline (`.claude/rules/working-discipline.md`) forbids fabricating *subagent* results; facet C extends the same prohibition to *your own* tool results and to every fact you state.** "Don't fabricate or predict fork results" and "never invent a count/diff/hash" are one rule applied to two sources. `feedback_no_write_before_reading_tool_output.md` is the worked example.

### Source

Sections A + B imported from obra/superpowers (`receiving-code-review` + `verification-before-completion` skills, reviewed 2026-05-29), combined into one rule because both encode "evidence over performance." Adapted to TAOM's review-heavy, auto-implementing pipeline; the verify-the-finding-first emphasis is TAOM-specific, grounded in `feedback_audit_findings_not_always_correct.md` and `feedback_codex_caught_api_misread.md`. We did NOT adopt superpowers' wholesale plugin (prompt-injection-by-design + context tax) — only the reviewed text. **Section C is TAOM-originated (2026-05-30), written from a live fabrication failure in this codebase** (`feedback_no_write_before_reading_tool_output.md`) rather than ported — the user's standing instruction is that not-knowing is fine and must be met with research, never with invention.

## simplicity-criterion.md

### Why this rule exists

The recurring failure mode in `/deep-review` agents (caught across multiple Codex review cycles, e.g. EquipPresets review #5 on 2026-05-06) is preserving scaffolding "just in case": unused enum values, never-populated status fields, "reserved for future" plumbing. This rule turns the existing CLAUDE.md "no over-engineering" guidance into a deterministic Yes/No matrix that an agent can apply without judgment calls drifting toward keep-everything.

It also gives `/deep-review` a concrete handle for the deletion-win case. A reviewer who finds a 50-line helper that nothing actually uses should not need to argue for its removal — the rule is "deletion that holds parity = always keep," full stop.

### Relationship to other rules

- `think-before-coding.md`'s **reuse ladder** is the *reuse-before-write* companion: it fires *before* a change exists (don't write what the engine or an existing service already provides); this rule judges a change *after* it exists (keep or reject). `/deslop` + `/deep-review` enforce both on finished code.
- This rule's "deletion that holds parity" test asks *is this code redundant?* It is NOT the same as `/improve`'s **deepening deletion test** (audit-playbook § Tech Debt & Architecture), which asks *is this abstraction shallow?* — would inlining a module *concentrate* complexity (deepen) or *scatter* it (keep). Redundant-code deletion vs shallow-abstraction deepening are different lenses; don't conflate them.

### Source

Imported from karpathy/autoresearch `program.md` "Simplicity criterion" paragraph (March 2026). The original framing was for ML hyperparameter changes ("a 0.001 val_bpb improvement that adds 20 lines of hacky code? Probably not worth it. An improvement of ~0 but much simpler code? Keep."); this rule generalizes it to any TAOM code change.

## think-before-coding.md

### Why this rule exists

LLMs default to *silent assumption* — pick one of several plausible interpretations of the request, commit to it, ship the diff. The user discovers the wrong path 30 minutes later, when the diff is already big enough that backing out is expensive and the right answer requires partial rework rather than a fresh start.

Karpathy's observation across LLM coding sessions: this is one of four recurring failure modes that prompt sophistication does not fix. Behavioral discipline does.

### Relationship to other rules

- `simplicity-criterion.md` decides *whether* to keep a change. This rule decides *whether the change you're about to write is actually the one the user asked for.*
- `/scope-check` evaluates whether a proposed addition fits the current PR. This rule fires earlier — before the addition exists.
- `/investigate` Phase 1 ("symptom + repro") is the debugging-specific instance of this rule. The general form applies to features and refactors too.

### Source

Imported from https://github.com/forrestchang/andrej-karpathy-skills (which packages karpathy/autoresearch behavioral principles). Original framing: *"State your assumptions explicitly. If uncertain, ask."* The "when NOT to ask" section is a TAOM-specific guard — the upstream rule does not address the opposite failure mode of over-questioning, which is a known LLM bug we've hit in past sessions.

The "lightweight design pass" section was added 2026-05-29 from obra/superpowers' `brainstorming` skill — we took its "one question at a time, multiple-choice / propose 2-3 approaches" core but deliberately dropped its mandatory per-feature design-doc-commit + multi-stage approval gate as too heavy for TAOM's workflow.

The "reuse ladder" section was added 2026-06-18 from [DietrichGebert/ponytail](https://github.com/DietrichGebert/ponytail) (MIT) — its YAGNI "decision ladder" (need it? → stdlib → native → reuse dep → one-liner → build), TAOM-translated to the TaleWorlds/ADR domain (engine API → existing service or adapter → one-line delegation → minimal new code). The rest of ponytail was evaluated and consciously not adopted — already covered harder by `simplicity-criterion.md` / `/deslop` / `/deep-review` / `/improve`; full novel-vs-duplicative map + skip reasons in `docs/reviews/adopt-ponytail-2026-06-18.md`.

## response-style.md

### Why this rule exists

LLMs default to two cheap behaviors the user has explicitly rejected: opening with social affirmation regardless of whether the idea holds up, and stating inferences with the same flat confidence as verified facts. Both optimize for the *appearance* of a smooth, agreeable assistant over the *substance* of catching errors early. Rule 1 forces the reply to start where it's most useful — on the gap, not the agreement. Rule 2 makes the certainty of every claim visible at a glance, so the user can tell "I checked this" from "I'm inferring" without having to ask.

### Source

User standing instruction, 2026-06-14. Rule 1 generalizes `evidence-over-claims.md`'s anti-performative-agreement stance from review findings to every reply opening, reconciled with `think-before-coding.md`'s anti-over-asking guard (challenge only when load-bearing). Rule 2 makes the implicit certainty calibration in `evidence-over-claims.md` §C explicit and always-visible.

## ai-prose-style.md

### Relationship to other rules

- `response-style.md` — reply openings + confidence tags + the anti-sycophancy reflex. That's *chat*; this is *artifacts*.
- `evidence-over-claims.md` §C — never invent the facts you write into a doc. Concrete-and-fabricated is worse than vague-and-honest.
- `/humanizer` skill — the on-demand deep-clean tool and full pattern catalogue.

### Source

Imported from [blader/humanizer](https://github.com/blader/humanizer) (MIT), whose patterns derive from Wikipedia's "Signs of AI writing". This rule is the high-value, TAOM-carve-out subset applied always-on; the skill is the full reference.
