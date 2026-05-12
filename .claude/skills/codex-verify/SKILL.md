---
name: codex-verify
description: Dispatch an independent Codex verification job for the current changes, then retrieve results
argument-hint: "[feature-name or file-path]"
---

# Codex Independent Verification

Dispatch an independent Codex review via the codex-plugin-cc. Codex reviews with no shared Claude context — a genuine second opinion.

The feature or area to verify: `$ARGUMENTS` (if empty, verify all uncommitted changes).

## Step 1: Identify Files to Review

Determine what to verify:
- If `$ARGUMENTS` is provided, find files for that feature/path using `find Main/Features/ -iname "*$ARGUMENTS*"` or the literal path
- Otherwise, use `git diff --name-only` and `git ls-files --others --exclude-standard` to find all changed/new files
- Filter to only `.cs` and `.xslt` files (Codex reviews code, not XML data)

Collect the file list.

## Step 2: Dispatch Codex Review

Run the following command — Codex will pick up project rules from `AGENTS.md` automatically:

```
/codex:review --background
```

If reviewing specific files or a feature, use:

```
/codex:adversarial-review --background Review these specific files for TAOM architectural compliance:

FILES: [list the files from Step 1]

Focus on:
1. Adapter pattern violations (ADR-007) — sealed TaleWorlds types in service classes
2. Thin entry point violations (ADR-002) — entry points over 150 lines with business logic
3. Harmony patch targets — verify method signatures exist in Bannerlord v1.3.15
4. Test coverage gaps — services without corresponding test files
5. GameModel override correctness — inheriting from Default* base, calling base.Method()

Rate each finding: CRITICAL / HIGH / MEDIUM / LOW
Format: [SEVERITY] file.cs:line — description — remediation
```

## Step 3: Continue Other Work (Optional)

While Codex runs in background, continue building, researching, or writing tests. Check progress:

```
/codex:status
```

Cancel if needed:

```
/codex:cancel
```

## Step 4: Retrieve Results

```
/codex:result
```

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
- If `/codex:review` fails (plugin not installed, Codex not authenticated), inform the user and suggest running `/codex:setup`
- **DO NOT SKIP STEP 6.** The harness-facts rule + the feedback memory both label this as a BLOCKING GATE. Past sessions have shipped without it; the meta-RCA in `docs/reviews/rca-scene-scripts-cs-road-2026-05-13.md` documents that case and why this step now exists in this skill.
