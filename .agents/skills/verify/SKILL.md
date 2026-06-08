---
name: verify
description: Run comprehensive build + test + git verification and produce a pass/fail report
argument-hint: [quick|full]
---

# Verification

Run comprehensive verification on current codebase state.

## Mode Selection

- `$ARGUMENTS` = `quick` → Build only (Step 1)
- `$ARGUMENTS` = `full` or empty → All steps

## Step 1: Build Check

```bash
dotnet build Main --no-restore 2>&1
```

If it fails, report errors and **STOP** (no point running tests on broken build).

## Step 2: Test Suite

```bash
dotnet test TAOM.Tests --no-build 2>&1
```

Report:
- Total tests
- Passed / Failed / Skipped
- Any failure details (test name + assertion message)

## Step 3: Git Status

```bash
git diff --stat
git diff --staged --stat
git ls-files --others --exclude-standard
```

Report:
- Files modified (unstaged)
- Files staged
- Untracked files

## Step 4: TODO/FIXME Scan

```bash
grep -rE 'TODO|FIXME' Main/ --include='*.cs' | wc -l
```

Report count.

## Step 5: CHANGELOG Check

Check if `CHANGELOG.md` has been modified (staged or unstaged):

```bash
git diff --name-only -- CHANGELOG.md
git diff --staged --name-only -- CHANGELOG.md
```

If C# or XML files changed but CHANGELOG not updated, flag it.

## Output Format

Produce a concise verification report:

```
VERIFICATION REPORT
===================
Build:      [PASS/FAIL]
Tests:      [X/Y passed, Z failed]
Uncommitted: [X files modified, Y staged, Z untracked]
TODOs:      [X in Main/]
CHANGELOG:  [Updated/NOT UPDATED]

Ready for commit: [YES/NO]

Issues:
1. ...
2. ...
```

## Important

This is a READ-ONLY assessment followed by build/test execution. Do not make any code changes. Only analyze and report.
