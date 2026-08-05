---
description: Evidence over performance — verify a review finding before implementing it, never sycophantically agree, and make no "done" claim without fresh verification output.
---

<!--
This rule has NO `paths:` field intentionally. Per the Claude Code memory loader
(see harness-facts.md): rules WITHOUT `paths:` load at conversation start
(always-on); rules WITH any `paths:` glob load only when a matching file is
opened. This discipline must fire on every turn — how we respond to review
findings, user corrections, and our own "done" claims — so `paths:` is omitted.
-->

# Evidence Over Claims

Three facets of one discipline: **technical truth over social comfort.** When someone (a Codex review, a `/deep-review` agent, a subagent, the user) hands you a finding, verify it before acting. When you're about to say something is done, prove it first. And when you're about to state any fact, produce it from evidence you have actually read — never invent what you could instead look up. Don't perform agreement, success, or knowledge — demonstrate it.

## A. Receiving feedback / review findings

A review finding is a **hypothesis, not a verdict.** TAOM's `/review-codex` loop auto-implements "confirmed" findings and `/deep-review` runs 5+ agents — both produce false positives. Memory `feedback_audit_findings_not_always_correct.md` records the measured rate: ~95% accurate, not 100%. So:

1. **Verify the finding against the codebase before implementing it.** Re-read the actual file/decompiled signature the finding refers to. A finding that says "this drops the ItemModifier" is checked by reading the call, not by trusting the reviewer's confidence. (`feedback_codex_caught_api_misread.md`: when two agents disagree on a TaleWorlds API, re-run `ilspycmd` — don't side with the more confident one.)
2. **Push back with evidence when warranted** — the suggestion breaks existing behavior, the reviewer lacks context the codebase contradicts, or it violates YAGNI / `simplicity-criterion.md`. Pushback is a one-line technical reason, not a refusal.
3. **When you were wrong, say so factually and briefly, then fix it.** "Checked `GetCharacterWage` — you're right, it falls back to `DeadBattleEquipment`. Fixing." No preamble.
4. **When you relay a subagent's research/review to the user, spot-verify its load-bearing claims against the source first.** A confident subagent report is a claim, not evidence — relaying it unverified is the same failure as trusting an agent's "✅ done." Especially security claims (telemetry defaults, install-time code-exec, exfil). (2026-05-29: a subagent reported open-design's telemetry "content off by default"; reading `app-config.ts` showed `content: true` — the error rode into the user-facing summary until an independent check caught it.)

**Banned responses** (performative, zero information): *"You're absolutely right!"*, *"Great point!"*, *"Excellent catch!"* These signal compliance, not understanding. State what you verified and what you're changing instead. (This is already a generic CLAUDE.md anti-pattern; this rule makes it explicit and ties it to the verify-first step.)

## B. Verification before "done"

**No completion claim without fresh verification evidence.** Before you say built / passing / fixed / done:

1. Identify the command that proves the claim (`dotnet build`, `dotnet test TAOM.Tests`, the audit script, the repro case).
2. Run it **now** — not from memory of an earlier run, not "it should still pass."
3. Read the exit code and the actual output.
4. State the claim with that evidence, or report the failure with the output.

**Does NOT count as verification:** a previous run; "should pass" / "looks correct"; a linter passing (≠ compiles); **a subagent's self-report** (fork-discipline already forbids fabricating fork results — a returned "✅ done" is a claim to verify, not evidence); your own confidence or fatigue.

**Stop and verify if** you're about to type "Done!" / "Great, that works!" before running the check, or about to commit/push on an unrun build.

This is the *reflex* form of `/verify` and `/ship` — those are the commands; this is the rule that fires even when you didn't invoke them.

## C. Never fabricate — "I don't know" is the correct answer

The cheapest thing an LLM can produce is a confident, plausible-sounding fact that is wrong. Never produce it. **If you don't know something, say so and go find out** — research, decompile, run the command, read the file. Stated uncertainty is always acceptable and always better than invented certainty; not-knowing just means *do additional research to educate yourself*. Fabricating to fill the gap is the one thing that is never acceptable.

**Never invent any of these — state them only from evidence you have actually read THIS turn:**
- "What changed" — file lists, diffs, changed-type lists. Read the actual `diff` output first.
- Counts, IDs, names, values, percentages, version numbers (e.g. "47 broken refs", an item ID, a build number).
- Tool output, command results, test pass/fail, exit codes.
- Commit hashes, file paths, line numbers, function/field signatures, API behavior.
- A subagent's result, or *your own* prior tool-call's result, recalled from memory.

**The mechanical traps that cause fabrication (all observed in TAOM on 2026-05-30 — same session, twice):**
1. **Writing the findings artifact before its evidence is in hand.** Authoring a doc / CHANGELOG / commit message that summarizes tool output, in the *same* tool block as — or before — the commands that produce that output. **Fix:** read the proving output, confirm it is real, *then* write. Never batch the summary Write with the analysis commands.
2. **A failed or empty read silently filled with a guess.** A `Read` that returns "File does not exist" / empty is a STOP signal, not a cue to reconstruct a plausible version. **Concrete trap:** the Windows `Read` tool cannot see git-bash `/tmp/...` paths — if you wrote scratch there from Bash, the Read fails; write scratch to a repo-relative path (e.g. `docs/migration/_scratch.txt`, deleted before commit) so it is actually readable.
3. **Trusting your own prior tool results from memory.** A commit hash, count, or "done" from earlier in the turn is a claim to re-verify, not a fact — a cancelled or errored call leaves no real trace, only your (unreliable) memory that it "happened." Re-run `git log`, re-read the file, before relying on it.

**When you catch yourself about to state a fact you have not verified this turn: stop.** Run the proving step and state it with evidence, or say "I don't know yet — checking." Both are fine. The plausible guess is not.

## Why this rule exists

Unverified agreement, unverified success, and invented facts are the three cheapest things an LLM produces and the three most expensive things a user discovers later: a "confirmed" finding that breaks a working path, a "done" feature that never compiled, a findings doc whose every detail was made up. All three come from optimizing for the appearance of progress over the fact of it. TAOM's workflow is review-heavy and auto-applies findings, which multiplies the cost of getting any of them wrong. The fabrication facet (C) was added 2026-05-30 after a hotfix-review doc + CHANGELOG were authored before the proving `diff` output was read — the *conclusion* was right but the *evidence* was invented (wrong changed-type list, a "fix" that never happened, a "47 broken refs" count when the real count was 0). RCA-in-memory: `feedback_no_write_before_reading_tool_output.md`.

## Relationship to other rules

- `think-before-coding.md` fires *before* the change (is this the right change?); this rule fires *around feedback and completion* (is this finding real? is this actually done?).
- `simplicity-criterion.md` is a legitimate basis for pushing back on a finding ("rejecting: adds an abstraction for a tiny win").
- `/investigate`, `/verify`, `/deep-review`, `/review-codex` are the command-form workflows; this rule is the always-on behavior underneath them.
- **Fork-discipline (`.claude/rules/working-discipline.md`) forbids fabricating *subagent* results; facet C extends the same prohibition to *your own* tool results and to every fact you state.** "Don't fabricate or predict fork results" and "never invent a count/diff/hash" are one rule applied to two sources. `feedback_no_write_before_reading_tool_output.md` is the worked example.

## Source

Sections A + B imported from obra/superpowers (`receiving-code-review` + `verification-before-completion` skills, reviewed 2026-05-29), combined into one rule because both encode "evidence over performance." Adapted to TAOM's review-heavy, auto-implementing pipeline; the verify-the-finding-first emphasis is TAOM-specific, grounded in `feedback_audit_findings_not_always_correct.md` and `feedback_codex_caught_api_misread.md`. We did NOT adopt superpowers' wholesale plugin (prompt-injection-by-design + context tax) — only the reviewed text. **Section C is TAOM-originated (2026-05-30), written from a live fabrication failure in this codebase** (`feedback_no_write_before_reading_tool_output.md`) rather than ported — the user's standing instruction is that not-knowing is fine and must be met with research, never with invention.
