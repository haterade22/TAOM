---
name: codex-verify
description: Dispatch an independent Codex verification job directly via `codex exec` Bash call, then retrieve and present results
argument-hint: "[feature-name or file-path]"
---

# Codex Independent Verification

Dispatch an independent Codex review via the local `codex` CLI — Claude calls it directly from this skill (no terminal hand-off to the user). Codex reviews with no shared Claude context: a genuine second opinion.

The feature or area to verify: `$ARGUMENTS` (if empty, verify all uncommitted changes).

## Codex CLI invocation contract (added 2026-05-25)

Claude has direct access to Codex via the `codex` binary (`C:\Users\mikew\AppData\Roaming\npm\codex.cmd` on Windows, `codex` on POSIX). Both `/codex-verify` and `/review-codex` dispatch from Bash — the user does NOT open a separate terminal. See the matching block in `.claude/skills/review-codex/SKILL.md` for the full contract; this skill follows the same dispatch model with a smaller, less-prescriptive prompt.

**Required pre-flight (once per invocation):**
```bash
codex login status
```
Expect `Logged in using ChatGPT`. If not, stop and tell the user to `codex login` (interactive browser flow).

**Dispatch command:**
```bash
cd "<repo-root>" && codex exec - < "<prompt-file>" > "<output-file>" 2>&1
```
- Run with `run_in_background: true` on the Bash tool.
- Output path: `docs/reviews/codex-verify-{feature-or-uncommitted}-{date}.md`.
- Codex picks up project rules from `AGENTS.md` automatically.

## Step 1: Identify Files to Review

Determine what to verify:
- If `$ARGUMENTS` is provided, find files for that feature/path using `Glob` against `Main/Features/**/*$ARGUMENTS*` or the literal path
- Otherwise, use `git diff --name-only` and `git ls-files --others --exclude-standard` to find all changed/new files
- Filter to only `.cs` and `.xslt` files (Codex reviews code, not XML data)

Collect the file list.

## Step 2: Write Prompt File

Write a focused prompt to `docs/reviews/codex-verify-{target}-{date}.prompt.md`:

```
You are reviewing TAOM changes for architectural compliance. AGENTS.md describes your role and the project conventions; honour it.

FILES TO REVIEW: [list from Step 1, one per line]

Focus on (in priority order):
1. Adapter pattern violations (ADR-007) — sealed TaleWorlds types in service classes
2. Thin entry point violations (ADR-002) — entry points over 150 lines with business logic
3. Harmony patch target signatures — verify method signatures exist in installed v1.4.5 DLLs (NOT the decompiled folder, which is v1.4 and may have drifted)
4. Test coverage gaps — services without corresponding test files
5. GameModel override correctness — correct base class, base call patterns
6. Cross-file data flow — declared types/fields/methods without consumers (aspirational scaffolding)
7. Lifecycle correctness — Subscribe/Unsubscribe pairs, OnSessionLaunched/OnGameLoaded entity state matrix

Rate each finding: CRITICAL / HIGH / MEDIUM / LOW
Format: [SEVERITY] file.cs:line -- description -- remediation

Output a structured review with sections: top-line verdict, findings grouped by severity, summary table.
```

For `/review-codex` (the heavier adversarial flow), the prompt is much richer with Known Suspects + vanilla decompile targets. This skill is the lightweight version — shorter prompt, faster Codex turnaround.

## Step 3: Dispatch via Bash

1. `codex login status` -- abort with user message if not logged in.
2. Dispatch:
   ```
   Bash tool call:
     command: cd "<repo-root>" && codex exec - < "docs/reviews/codex-verify-{target}-{date}.prompt.md" > "docs/reviews/codex-verify-{target}-{date}.md" 2>&1
     run_in_background: true
     timeout: 600000
   ```
3. Tell the user: dispatched, prompt at X, output at Y, expected window 5-20 min (this is the lighter prompt — faster than `/review-codex`).
4. **Do not poll.** The harness notifies when the background job completes. Continue with other work or wait.

When the background notification arrives, proceed to Step 4 automatically.

## Step 4: Retrieve & Validate Output

Read `docs/reviews/codex-verify-{target}-{date}.md`. Check:
- File is non-empty
- Does not start with `Error:` / `panic:` / `login required`
- Contains structured findings (severity tags, file:line refs)

If validation fails, surface the exact error and suggest manual fallback (`codex exec` in a terminal with the same args).

## Step 5: Display Report

Format the Codex output as:

```
CODEX VERIFICATION REPORT
==========================
Target: [feature or files]
Model: o4-mini (reasoning: high)

[Codex findings, grouped by severity]

CRITICAL: N
HIGH: N
MEDIUM: N
LOW: N

VERDICT: CLEAN / ISSUES FOUND
```

## Step 6: Root Cause Analysis (MANDATORY — BLOCKING GATE before commit)

If Codex returned ANY confirmed finding (any severity, including LOW), Phase 3e RCA applies before the closing commit. Per `.claude/rules/harness-facts.md` and `feedback_root_cause_mandatory.md` — this is not optional, not severity-gated, not "only HIGH." Conflating severity with importance for RCA means we patch LOW symptoms but never extract the systemic lesson — and the same category of bug ships again. The recurring failure: it's already happened repeatedly (Career cooldown review #31, EditorCacheRebuild review #38, scene-scripts CS_Road 2026-05-13 — all the same NaN-gate scope miss).

**For EVERY confirmed finding (not just HIGH):**

1. Write the finding text + severity.
2. **Why missed:** what assumption, scope gap, or pattern blindness let it through? Be specific — name the rule that should have caught it, name the file/line that exhibits the pattern.
3. **Preventive action:** is there a generalizable rule, a feedback-memory entry, or a scope extension to an existing rule? Or is this a one-off?
4. If the pattern has shipped before (grep `docs/reviews/rca-*.md` and `~/.claude/projects/.../memory/feedback_*.md` for it), call that out — repeat-offender bugs need stronger preventive action than first-time bugs.

**Write the result to `docs/reviews/rca-<feature>-<YYYY-MM-DD>.md`** following the format of `docs/reviews/rca-quickactions-2026-05-06.md`:
- Top-line summary
- Findings table with columns: # | Sev | Bug | Category | Why Missed | Preventive Action
- Root-cause pattern section (if 2+ findings share a theme)
- "Why deep-review missed these" section (if deep-review ran before Codex)
- "Feedback memories to codify" section (only if there's a genuine systemic pattern; don't manufacture rules)

**This file MUST exist BEFORE the closing commit.** The commit message should reference the RCA path.

If the RCA reveals a rule that's already documented but wasn't followed (scope gap or skill body missing prompt), update the rule file or skill body in a follow-up commit. The commit graph should show: review → fixes → RCA → preventive-rule update.

## Important

- This is independent verification — Codex has its own understanding via AGENTS.md
- If Codex and Claude disagree on a finding, that disagreement is valuable signal — flag it
- Codex does NOT run builds or tests — it does static analysis and API signature verification
- If the `codex` CLI is missing or unauthenticated, surface the exact `codex login status` output and tell the user to run `codex login` (interactive browser flow). Do not silently fall back to a different verification path.
- **DO NOT SKIP STEP 6.** The harness-facts rule + the feedback memory both label this as a BLOCKING GATE. Past sessions have shipped without it; the meta-RCA in `docs/reviews/rca-scene-scripts-cs-road-2026-05-13.md` documents that case and why this step now exists in this skill.
