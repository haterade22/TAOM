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
dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= 2>&1
```

Both flags, on this and on Step 2: without them the build copies into the game install, and fails
while Bannerlord is running (AGENTS.md "Commands"). Never `./build.ps1` here.

If it fails, report errors and **STOP** (no point running tests on broken build).

## Step 2: Test Suite

```bash
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= 2>&1
```

No `--no-build`: Step 1 builds `Main` only, so skipping the build here would test a stale test DLL.

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

## Step 5: CHANGELOG.md untouched

Only `/release` writes `CHANGELOG.md` (generated from commit bodies). Check that this work did
not edit it by hand:

```bash
git diff --name-only HEAD -- CHANGELOG.md
```

If it prints `CHANGELOG.md` outside a `/release` run, flag it: the entry belongs in the commit body.

## Output Format

Produce a concise verification report:

```
VERIFICATION REPORT
===================
Build:      [PASS/FAIL]
Tests:      [X/Y passed, Z failed]
Uncommitted: [X files modified, Y staged, Z untracked]
TODOs:      [X in Main/]
CHANGELOG:  [Untouched/HAND-EDITED]

Ready for commit: [YES/NO]

Issues:
1. ...
2. ...
```

## Important

This is a READ-ONLY assessment followed by build/test execution. Do not make any code changes. Only analyze and report.
