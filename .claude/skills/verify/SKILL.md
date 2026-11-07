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
while Bannerlord is running (.ai/verification.md). Never `./build.ps1` here.

If it fails, report errors and **STOP** (no point running tests on broken build).

## Step 1b: NuGet audit

```bash
dotnet restore TAOM.sln --force-evaluate 2>&1
```

`Directory.Build.props` makes a high or critical advisory (NU1903, NU1904) a restore error (#1020),
but NuGet skips the audit on a no-op restore, so an unchanged checkout never hears of a new
advisory. `--force-evaluate` makes this restore a full one. An NU1903 or NU1904 error is a FAIL.
NU1900 means the vulnerability data could not be fetched (offline): report the audit as
UNCHECKED, not as a pass.

## Step 2: Test Suite

```bash
dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= 2>&1
```

No `--no-build`: Step 1 builds `Main` only, so skipping the build here would test a stale test DLL.

Report:
- Total tests
- Passed / Failed / Skipped
- Any failure details (test name + assertion message)

A total under 7,000 (the WP101 baseline was 9,154) means discovery broke: not a pass.

## Step 2b: Python tool tests

```bash
python -m unittest discover -s tools/tests -t . 2>&1
```

The tests for `tools/`. Plain `unittest`, no pytest. Nothing else runs them automatically since the
GitHub CI was removed (2026-10-05). Report `Ran N tests` and the failures. No `Ran N tests` line,
or fewer than 1,200 tests, means discovery broke: not a pass.

## Step 2c: Canary

```bash
TAOM_VERIFY_CANARY=1 dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~VerifyCanaryTests 2>&1
```

`--no-build` is safe here: Step 2 just built the test DLL. This proves the runner can report a
failure without editing any source (`TAOM.Tests/Infrastructure/VerifyCanaryTests.cs`). The canary
PASSES only when the run exits non-zero AND its output contains `VERIFY_CANARY_TRIPPED`. An exit 0
or a missing token means the runner cannot report failures, so the whole verify FAILS.

## Step 2d: API snapshot reproduces

```bash
pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check 2>&1
```

Reflects over the test bin's `TAOM.dll` that Step 2 built and compares the result with
`docs/reference/taleworlds-api-snapshot/`. Exit 1 with a `DRIFT:` line means a patch target or
GameModel base changed and the snapshot was not regenerated, so `tools/engine_body_diff.py` would
miss that method at the next engine bump. Fix it with `pwsh tools/snapshot_api_surface.ps1` and
commit the result with the change. DRIFT blocks: `Ready for commit` is NO while it stands. Any other
failure (no `pwsh`, no game install) is NOT RUN, which has not passed.

## Step 2e: Data gates

```bash
python tools/data_gates.py
```

Runs the gates for the ModuleData, Armory and tool files the working tree changed, and prints PASS, FAIL or NOT RUN for each. Name any live Armory or `TAOM_Map` file edited this session on its command line. Report the counts; a FAIL blocks, and a NOT RUN (no install, SKIPPED, the dotnet rows) has not passed. No match is "no gate reads the changed files", not a pass of anything.

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

## Step 5: CHANGELOG.md untouched

Only `/release` writes `CHANGELOG.md` (generated from commit bodies). Check that this work did
not edit it by hand:

```bash
git diff --name-only HEAD -- CHANGELOG.md
```

When a review base is known (a branch or worktree), also check the branch range, which catches a hand edit that is already committed:

```bash
git diff --name-only <base> -- CHANGELOG.md
```

If it prints `CHANGELOG.md` outside a `/release` run, flag it: the entry belongs in the commit body.

## Step 5b: Gates silenced (read the hits)

```bash
git diff HEAD -U0 | grep -nE '^-\s*\[TestMethod|^\+\s*\[Ignore|^\+.*(_BY_DESIGN|_EXEMPT|exempt_troops|KNOWN_FAILURES|shellcheck disable=|noqa|NuGetAuditSuppress|ruff: noqa)'
git ls-files --others --exclude-standard | xargs -r grep -nE '\[Ignore|_BY_DESIGN|_EXEMPT|exempt_troops|KNOWN_FAILURES|shellcheck disable=|noqa|NuGetAuditSuppress'
```

With a review base known, diff against `<base>` instead of `HEAD`. Read every hit: a silenced check needs a stated reason in the entry or the commit body, and a weakened `Assert` is not caught by the pattern. List the unexplained ones under Issues. Information only; the hits are not a count to gate on.

## Step 5c: Provenance

```bash
python tools/check_provenance.py
```

Every miss goes under Issues: a register Derivation or Status outside its closed set, a shipped non-TAOM binary with no `redistributed` row, or an adoption record the register does not name. A zero does not prove intent ([provenance.md](../../rules/provenance.md)).

## Step 6: Owed in-game verdicts (information only)

```bash
python tools/ingame_verdict.py --owed --sessions 10 2>&1
```

It asks `gh` for the issues labelled `triage-needs-ingame`, reads the ten newest game sessions' logs
against each one that has a `tools/ingame_checks/<issue>.toml`, and prints a verdict per check
([ingame-verdicts.md](../../../docs/features/ingame-verdicts.md)). Report each issue's verdict,
every FAIL with its reason, the newest session's `summary` line once and each `WARNING: errors=`
line once. Give the `no manifest:` list as a count only.

This step never fails verify and never changes "Ready for commit". NOT REACHED (exit 4) is the
normal state before anyone has played the build. A FAIL (exit 1) is evidence against the logged
build named in the output; whether it touches this tree is for investigation and Mike. Exit 3 (`git`
missing or `gh` failed; the message says which) is NOT RUN; a build sha `git` cannot compare leaves
that session NOT REACHED. Ten sessions let a two-session check such as #1011 pass, and per check the
newest PASS or FAIL wins. Exit 2 is a malformed manifest, which Step 2b's `test_ingame_verdict` also
fails on. Never pass `--post` here: it comments on public issues.

## Output Format

Produce a concise verification report:

```
VERIFICATION REPORT
===================
Build:      [PASS/FAIL]
Tests:      [X/Y passed, Z failed]
Tool tests: [N ran, F failed, S skipped]
API snapshot: [REPRODUCES/DRIFT/NOT RUN]
Canary:     [TRIPPED (expected)/NOT TRIPPED (verify FAILS)]
Data gates: [P PASS, F FAIL, N NOT RUN]
Uncommitted: [X files modified, Y staged, Z untracked]
CHANGELOG:  [Untouched/HAND-EDITED]
In-game:    [I issues judged: P PASS, F FAIL, N NOT REACHED / NOT RUN (gh)] (information only)
Provenance: [N misses]

Ready for commit: [YES/NO]

Issues:
1. ...
2. ...
```

## Important

This is a READ-ONLY assessment followed by build/test execution. Do not make any code changes. Only analyze and report.
