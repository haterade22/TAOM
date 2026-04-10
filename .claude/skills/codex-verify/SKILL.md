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
3. Harmony patch targets — verify method signatures exist in Bannerlord v1.4.0
4. Test coverage gaps — services without corresponding test files
5. GameModel override correctness — inheriting from Default* base, calling base.Method()

Rate each finding: CRITICAL / HIGH / MEDIUM / LOW
Format: [SEVERITY] file.cs:line — description — remediation
```

## Step 3: Monitor Background Job

Use the Monitor tool to stream Codex progress. This auto-notifies when the job completes — no manual polling needed:

```
Monitor the Codex background process output for completion signals.
When "Review complete" or similar appears, proceed to Step 4.
```

Alternatively, continue other work and check manually:

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

## Important

- This is independent verification — Codex has its own understanding via AGENTS.md
- If Codex and Claude disagree on a finding, that disagreement is valuable signal — flag it
- Codex does NOT run builds or tests — it does static analysis and API signature verification
- If `/codex:review` fails (plugin not installed, Codex not authenticated), inform the user and suggest running `/codex:setup`
