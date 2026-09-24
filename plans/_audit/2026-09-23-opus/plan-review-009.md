# Cold review: plan 009 (guarded PatchCategory apply)

Reviewer: cold read, reviewer role, no fixes. Plan file: `plans/009-guarded-patch-category-apply.md`
(untracked, read from the main tree). Template: `.claude/skills/improve/references/plan-template.md`.
Evidence was read this turn from `git show b2e387db:<path>`, the v1.5.3 `~/.taom-src` cache, and
`ilspycmd` on `~/.nuget/packages/lib.harmony/2.4.2/lib/net472/0Harmony.dll`. I did not run the
build or the test suite, so the baseline test counts at plan line 266 are **not re-verified**.

## Verdict

The plan is unusually complete. Its excerpts match `b2e387db` except for two stale line labels, and
every grep count I could simulate holds. One environment hazard can stop the final commit and push a
weak executor out of scope. With that fixed, the plan is executable by a weak model.

## Blocking

1. **The commit 2 staging set trips the `check-changelog-changed.sh` PreToolUse hook (plan lines
   283, 298, 769; no STOP bullet covers it).** Commit 2 stages `.claude/rules/harmony-patches.md`
   without `CHANGELOG.md`. The hook denies any `git commit` whose staged set touches `.claude/*`
   unless `CHANGELOG.md` is also staged. Its deny reason tells the agent to "Add a CHANGELOG entry
   under today's date and re-stage", but plan line 283 puts `CHANGELOG.md` out of scope. The hook
   runs `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"` before it reads the index. It therefore fires whenever
   the executor's project dir is the worktree, for example a worktree-isolated spawn or a session
   opened in `E:/repos/wt-plan-009`. A weak executor then either edits CHANGELOG (a scope breach)
   or loops. **Fix, pick one:** (a) move the `harmony-patches.md` edit to the orchestrator, (b) allow
   a CHANGELOG entry in the worktree for commit 2, or (c) add a STOP bullet: "a hook denies a commit:
   stop and report its message; do not edit CHANGELOG.md".

## Current-state excerpt mismatches (b2e387db)

- **Plan line 197:** the example C site is labelled `// 1557-1564`. At `b2e387db` the Patch68 block is
  lines **557-564**: `try` is at 557, `_harmony.PatchCategory("Patch68_EconomyDiagnostics")` at 559
  and the catch's closing brace at 564. The code text itself matches.
- **Plan lines 275 and 727:** the Patch65 doc comment is described as "lines 16-20". The quoted span
  (from `rethrows it as a <c>HarmonyException</c>. Since <c>SubModule</c>'s` through `that cannot
  happen.)`) is **lines 16-21** of
  `TAOM.Tests/Features/LordSpawnGuard/Patch65LandlessCultureSpawnGuardBindingTests.cs`. Line 21 is
  `///    that cannot happen.)`. An executor who trusts the line numbers leaves that line dangling.

Everything else checked matches. I checked:
- `SubModule.cs`: 2,148 lines, the field at 102, `new Harmony` at 194, the comment at 187-193, the
  Patch37 block at 194-210, the preview loop at 1478-1501, Patch77 at 1517-1527, the game-init flag at
  1464-1465, the mission flag at 1919-1923, the green message at 626 and `StampSaveLoadPhase` at 857.
- The call sites: all 86 grep lines (84 live plus the two commented, 1753 and 1760) and the 13/50/1
  split of the 64 bare sites. The 20 guarded sites, including the `Initialize` counts per K site.
- The nine test lines in Step 7 and `UncapturableHeroesWiringTests.cs:92`. The doc lines
  `crash-report.md:284`, `submodule-lifecycle-and-harmony.md:20` and `:30`, `harmony-patches.md:61`
  (15,043 B) and `harmony-il.md:172`.
- The `Lib.Harmony 2.4.2` lines (Dependencies:70, Main:110, Tests:19) and Patch37's targets (36-104, and
  113 for `MBSubModuleBase.OnSubModuleLoad`).
- The engine excerpts in v1.5.3: `Module.cs:201-223` and `MBGameManager.cs:110-115`. The Harmony
  facts: `PatchCategory(string)` uses the stack frame, `PatchWithAttributes` throws "Undefined target
  method", and `ReportException` wraps it as a `HarmonyException`.
- The drift check prints nothing for `b2e387db..HEAD` (HEAD `4b5662b2`).

Simulated gates (a Python script over `git show b2e387db` blobs):
- The source-gate regex, after comment stripping, finds exactly **84** hits in `Main/**/*.cs`, all in
  `SubModule.cs`. This matches Step 2's expected failure message.
- The Step 4 byte regex `_harmony\.PatchCategory\((?!typeof)` finds **86**.
- The baseline counts are: `TryPatchCategory(` 0, `ReportPatchFailures(` 0, `DisableForSession` 1,
  `applied OK` 1, both flags 1. Each of the nine collapsed-catch substrings occurs exactly once, in
  the block being collapsed.
- So the expected values at Steps 3, 4, 5 and 6 and in the Done criteria (87 / 1 / 87 / 2 / 5 / 0)
  are arithmetically consistent.
- No test outside the nine listed pins the old spelling, a catch message, or the preview FAILED
  text. `HarmonyPatchBindingTests` scans only `typeof(TAOM.IoC).Assembly`, so the test-assembly
  probe class cannot trip it.

## Non-blocking

1. **Plan lines 7, 277, 804: `plans/README.md` has no row for plan 009.** Neither the committed copy
   (which the worktree gets) nor the main tree's modified copy has one; `grep 009` finds nothing. The
   Done criterion "status row updated" is neither achievable as worded nor machine-checkable. The
   plan also does not say which copy to edit: the worktree's, or the main tree's untracked-plans
   area. Say "add a row" or "the orchestrator maintains the index".
2. **Plan line 813 (fallback path):** deleting the two real-Harmony tests leaves 7 tests, but Steps 2
   ("8 passed, 1 failed"), 4 ("9 passed") and 7 and 9 ("Step 0 total + 9"), and the Done criteria
   ("the 9 ... pass") are not adjusted. Add "(7 / +7 if you took the fallback)".
3. **The real-Harmony premise has a likely specific failure mode (plan lines 437-460).** Harmony
   2.4.2's `BuildCategoryCache` calls `HarmonyMethodExtensions.GetFromType(type)`, which reads
   `GetCustomAttributes(true)`, on every type in `TAOM.Tests`. The comment in
   `EconomyDiagnosticsPatchDiscoveryTests.cs:52-57` documents `FileNotFoundException` when attributes
   are read across TAOM types that reference `TaleWorlds.MountAndBlade.View`. The fallback covers
   this, but naming "FileNotFoundException / TypeLoadException from BuildCategoryCache" in the STOP
   bullet would let a weak executor recognise it at once.
4. **Shell:** the primary shell is PowerShell, but every verify uses `grep -c`, `grep -cF` and BRE
   `\|`. Plan lines 256-264 should say "run these in the Bash tool (Git Bash)". Git Bash has GNU
   grep 3.0, so the patterns work there.
5. **Plan line 751:** the replacement bullet for `crash-report.md` is an inline code span that
   contains backticks. The literal's start and end are ambiguous; use a fenced block.
6. **Plan line 45 and the Why section:** `Main/TAOM.csproj:110` references Harmony with
   `IncludeAssets="compile"`, so in game the runtime Harmony is the Bannerlord.Harmony module, not
   necessarily 2.4.2. The helper does not depend on the version, so no action is needed. The claim
   "Harmony 2.4.2 has no catch" is proven only for the compile and test version; the maintenance
   notes could say so.
7. **Plan line 37 vs CLAUDE.md "Subagents":** CLAUDE.md tells subagents that `SubModule.cs` is
   single-owner ("recommend, don't edit"). The plan overrides that correctly for a worktree branch.
   Quoting the CLAUDE.md line and saying "the orchestrator's dispatch of this plan is the
   authorization" would stop a cautious executor from halting there.
8. **Plan line 291:** `git worktree add ... -b plan/009-guarded-patch-apply` fails if the branch or
   the directory already exists, for example on a retry. Add a STOP bullet or "if it exists, stop and
   report".
9. **Plan line 767:** `git status --porcelain` → "only in-scope paths". This assumes the build and test
   write only gitignored output in the worktree. That is plausible, since the baseline was measured in
   a clean worktree, but it is not stated.

## Checklist (template "Quality bar" and TAOM additions)

| Item | Result |
|---|---|
| Self-contained (no advisor-session knowledge needed) | Yes, apart from blocking item 1 and non-blocking item 1 |
| Every step ends in a command with an expected result | Yes (Steps 0 to 9 each have a Verify with counts or exit codes) |
| TDD order for C# | Yes: Step 1 RED (CS0246), Step 2 GREEN, and the gate goes red then green at Step 4 |
| Issue-first note | Yes (line 25, orchestrator) |
| Binding ADRs named with one-line summaries | Yes: ADR-002, 007 and 008, 003/004/005, and `csharp-architecture.md` (lines 238-241) |
| Single-owner files handled | `SubModule.cs` is explicitly in scope with an enumerated edit list; `IoC.cs` and the csproj are out with a STOP |
| STOP conditions specific | Yes, except that a hook-denied commit is not covered (blocking item 1) |
| Done criteria machine-checkable | Yes, except for the `plans/README.md` row |
| Planned-at SHA and drift paths consistent with Scope | Yes: `b2e387db`, and the drift list covers every in-scope file except `plans/README.md` (acceptable) |
| Non-deploying commands with `-p:ModuleId=` | Yes (lines 260-262, 793-794); `./build.ps1` is forbidden |
| No em or en dash in prose | Yes; the two hits (lines 177 and 227) are verbatim code excerpts |
| No secret values | None found |
