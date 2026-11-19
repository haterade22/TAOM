# Cold review: plan 058, round 1

Plan: `plans/058-isolate-mission-behaviour-registrations.md`. Code read from the run worktree at
`c6a82aa04` (`git rev-parse --short HEAD` printed `c6a82aa04`; `git diff --stat c6a82aa04..HEAD` over
the in-scope paths printed nothing). No earlier `plan-review-058*.md` existed in this folder, so there
are no earlier blocking items to re-check. dotnet was not run (read-only role); the plan's code was
checked by a Python simulation (`sim.py` in the reviewer's scratch folder) that applies Steps 5a to 5f
to the base `Main/SubModule.cs` and runs the plan's regexes and pins against both trees.

## Verdict

Three blocking items. Two are verification commands that cannot pass as written, the third is a false
claim about today's behaviour that the plan turns into a changelog sentence and that hides a
player-visible change. The rest of the plan is precise: every excerpt matches the code, and every
simulated count, regex and mutation gives the result the plan predicts.

## Blocking

1. **Done criterion `git grep -n "AddTaomBehavior(new" -- Main TAOM.Tests` returns nothing (plan line
   978) cannot pass after the commit.** The plan's own new test file holds that text: Step 2,
   `Pin_FailsWhenACallPassesABuiltBehavior` (plan line 528), replaces with
   `"AddTaomBehavior(new BehaviorTreeMissionLogic());"`. Once the file is committed (Step 7), `git grep`
   reads it and prints `TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs:<n>`. A weak
   executor either stops at the last gate or "fixes" the mutation test. Fix: exclude the file
   (`git grep -n "AddTaomBehavior(new" -- Main TAOM.Tests ':!TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs'`
   returns nothing), or expect exactly that one line. Lines 989 to 990 need the same note.

2. **Step 7.5 (plan line 926) says "two of them `??` new files before staging"; the plan creates
   three.** Scope (lines 343 to 346) marks `Main/Composition/MissionBehaviorBuild.cs`,
   `TAOM.Tests/Composition/MissionBehaviorBuildTests.cs` and
   `TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs` as new; both `Composition`
   folders already exist, so `git status --porcelain` lists three `??` lines. The stated count does not
   equal the list beside it (the template's "a count the plan states equals the list" check). Fix: "three".

3. **"The battle starts silently" (plan lines 37 to 38) and the commit body draft's "silently missing
   from the battle" (plan lines 932 to 936) are false, and the plan removes a player-visible notice
   without saying so.** Today a throw out of `AddTaomMissionBehaviors` reaches
   `MissionStartGuardCalls.OnMissionBehaviorInitialize` (`Main/Features/MissionStartGuard/Hooks/MissionStartGuardCalls.cs:35-39`),
   whose `Note` calls `MissionStartGuardService.Report` (`Main/Features/MissionStartGuard/MissionStartGuardService.cs:83-92`):
   it logs an error line and calls `ShowNoticeIfAllowed` (`:154-162`), which shows the player a
   notice (up to `MaxNoticesPerMission = 3`, `:31`; not on a dedicated server). The toggle
   `SurviveMissionStartFailures` defaults to true (`Main/Features/CrashReport/CrashReportSettings.cs:43`;
   its hint at `:42` says "log it, show a short message"). After this plan, a build failure is caught
   inside `MissionBehaviorBuild.Isolated` and writes only a `[MissionWiring]` log line: the guard's
   error line, its notice and its per-mission summary no longer fire for this failure class. The
   Maintenance notes (lines 1047 to 1050) offer "a player-facing chat line" as a follow-up, as if none
   exists today. The executor would commit a release note that misstates the old behaviour, and the
   maintainer would approve a loss of a player notice he was never told about
   (`think-before-coding.md`: never drop part of a behaviour silently). Fix: correct "Why this
   matters" and the commit body to say the guard logs and shows a short message and then the battle
   starts without the later behaviours; then either (a) state the trade-off (a `Constraint:` or
   `Rejected:` trailer, a line in "Why this matters", a review probe) and keep it, or (b) also report
   the caught build exception through the guard service so the notice survives (a design change that
   needs its own test). Re-derive the replacement sentence from the lines cited here.

## Non-blocking

- **Step 7.3 (plan lines 921 to 923) gives no count.** "Failed: 0" also holds when the filter matches
  nothing (dotnet prints "No test matches"), and the step claims "this filtered run proves they ran".
  Give the expected Passed count or at least "Passed greater than 0, Failed: 0". The five classes
  exist (`EntryPointLineCountGateTests` 11 `[TestMethod]`, `BattleLoadCloserWiringTests` 4,
  `SubModuleServerGuardTests` 5 methods with 10 `DataRow`s, `SubModuleMissionServerGuardTests` 2,
  `SubModuleMissionViewWiringTests` 1; `SubModuleServerGuardTests` and others carry
  `[TestCategory("RequiresGame")]` tests).
- **Step 5's warning check (plan lines 839 to 841) is UNVERIFIED.** I could not compile. Two risks the
  plan does not cover: a warning the base already has inside `AddTaomMissionBehaviors` (the check would
  fail on a pre-existing warning), and a nullable warning from inferring `T` in the two block lambdas
  that return `null` (CS8631 or CS8619 if the compiler infers `X?`). My reading of the nullable
  inference rules says `T` is inferred as the non-nullable type, but the plan's STOP list (line 1000)
  names only CS0411 and errors. Add: "a warning the base build also shows is not yours" and "a nullable
  warning at 5d or 5e is a STOP".
- **Step 6.1's inserted snippet (plan lines 869 to 873)** is indented 13 spaces by the markdown list;
  the surrounding method body uses 8. Say "indent to match line 117".
- **Step 6.8 (plan line 897)** writes `Features.SiegePropDiagnostics.Hooks...` where today's comment
  (`TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs:11`) has
  `SiegePropDiagnostics.Hooks...` without `Features.`. Harmless (it matches the real call), but the
  step says "Each change below swaps the old call text for the new one and nothing else" (line 855).
- **Done criterion "`git status --porcelain` is empty after the commit" (plan line 982)** assumes no
  orchestrator file is dirty in the executor's worktree. In this run the plans and `plans/_audit/`
  are untracked in the run worktree until the plans commit (PROGRESS.md, phase 6). If an executor runs
  in a worktree where the orchestrator writes `PROGRESS.md`, the criterion fails. Consider "lists no
  path outside `plans/`".
- **ModuleRunner paraphrase (plan lines 54 to 56)** omits that `Run` rethrows for a save-owning
  module when `failClosed` is true (`Main/Composition/ModuleRunner.cs:63-91`). The mission-start call
  passes `failClosed: false` (`Main/Composition/FeatureModuleHooks.cs:119`), so the claim holds where
  the plan uses it.

## What I checked, with evidence

### Excerpts against the code at c6a82aa04

Every quoted block matches, line numbers included:
- `Main/SubModule.cs:2175` `OnMissionBehaviorInitialize`; `:2203-2216` the try/finally; `:2219-2221`
  the method comment; `:2222` the declaration; `:2246`, `:2256`, `:2261` the preamble lines;
  `:2263-2280` the local function; `:2288-2289` the shader guard; `:2291-2295` the tick-order comment;
  `:2296-2311` the one-line registrations; `:2320-2324` Enlistment merit; `:2367-2369` the hand-off;
  `:2371-2380` MissionDiagnostic (five comment lines, then five code lines); `:2385-2401` the tail.
- `Main/SubModule.cs:22` is `using TAOM.Composition;`; the file has no `using System;` and writes
  `catch (System.Exception ex)` (line 209 and others). CRLF (`file` says so).
- `Main/Composition/FeatureModuleHooks.cs:110` is the quoted doc comment, `:111` the two-argument
  overload, `:119-126` the per-module build then hand-over.
- `Main/Features/MissionStartGuard/Hooks/MissionStartGuardCalls.cs:35-39`, `Main/IoC.cs:280-283`,
  `TAOM.Tests/Features/MountDespawn/MountDespawnWiringTests.cs:29`,
  `TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs:40-41`: as quoted.
- Engine fact: `pwsh tools/taom-src.ps1 path Mission` resolved the v1.5.7 cache;
  `TaleWorlds.MountAndBlade.Mission.cs:4680-4694` is `AddMissionBehavior` exactly as quoted
  (`MissionBehaviors.Add` before `OnCreated`).
- Pin table (plan lines 265 to 276): every file:line and pinned text matches `git grep`.
- Blast radius: `AddTaomMissionBehaviors` appears at `Main/SubModule.cs:2205`, `:2222` and in the named
  tests only; `FeatureModuleHooksTests.cs:98` calls the four-argument overload.
- Issue #785: `gh issue view 785` gives "fix: isolate TAOM's hand-wired mission behaviour
  registrations | OPEN | Harness and tools". `Main/_Module/SubModule.xml:6` is `v2.0.40`; the
  subject is 68 characters.
- Baseline totals: PROGRESS.md phase 0 records 42943 passed, 3 failed, 9 skipped (not re-run here).

### Simulation of Steps 2, 5 and 6 (Python, base `Main/SubModule.cs`, CRLF normalised, comments blanked like `RepoPaths.StripComments`)

- Every Step 5 old string occurs exactly once; 5f's `AddTaomBehavior(new ` occurs 28 times after 5d
  and 5e.
- Post-edit line counts: `AddTaomBehavior(new` 0, `AddTaomBehavior(() =>` 30,
  `AddTaomBehavior(() => new ` 28, `AddBuiltTaomBehavior` 3 in SubModule (4 with 5g). Base:
  `AddTaomBehavior(new` 30.
- Step 2 pins on the base: pin 1 fails on `AddTaomBehavior(MissionBehavior behavior)`, pin 2 on
  `IoC.Resolve<`, pin 3 on "factory form gone", both mutations find nothing to change: 5 failures as
  the plan says. On the post-edit source the three pins pass and both mutations make their pin fail.
- Step 6 role regex: 30 matches post-edit, the same 30 names in the same order as the old regex on the
  base; `variableAdds` 0; `builtAdds` 1 (`AddBuiltTaomBehavior(behavior)`); the Warg mutation gives 29.
- `CoopMissionAddFilterPinTests`: after the rename, the add statement ends 876 characters after the
  anchor (inside the 900 window); the filter precedes the add; one `coopSessionActive` declaration,
  before the anchor.
- A scan of every string literal of 12 or more characters under `TAOM.Tests` that occurs in the base
  `SubModule.cs` and not in the post-edit one lists exactly the seven pins Step 5 names (plus the
  regex-based role-table count): no other test breaks on text.
- The eight Step 6 filter names each match one test method; `MissionBehaviorBuildTests` and
  `SubModuleMissionBehaviorIsolationTests` match no existing class.

### Other gates the change reaches

- `Directory.Build.props`: `LangVersion` 10.0, `Nullable` enable; MSTest 3.1.1 (has
  `Assert.ThrowsException`, used 164 times in the suite); `Main/TAOM.csproj` is SDK-style with
  globbing and has `InternalsVisibleTo` for the tests, so the new `internal` class needs no csproj edit.
- `SilentCatchScan` selects only `Hooks` folders and Harmony patch files, so `MissionBehaviorBuild.cs`
  is not scanned. The IL gates (`IlCallScanner`) resolve tokens with the generic type and method
  context, so the new generic local function and its lambda's generic closure class are readable.
  `FeatureModulesTests.AssertNotHandWiredToo` matches `new X(` anywhere and is unaffected.
- No `[MissionWiring]` prefix exists in the repo today; no log-prefix registry gate was found
  (search of `TAOM.Tests`, `tools/tests`, `docs/reference`).

### Template checks

- TDD order holds (pins, behaviour tests, helper, SubModule edit, pin updates). ADR-002, 003 to 005,
  007, 008, 013 and the cited lessons and rules exist under those names. No protected file; Step 0
  "None" is right. `Main/SubModule.cs` edits are listed exactly. Drift-check paths equal the 13 Scope
  paths. Commands carry `-p:DisableModuleCopy=true -p:ModuleId=`. No worktree path, branch name or
  CHANGELOG step. No em or en dash (Python scan for U+2014 and U+2013 found none); no secret values;
  no local absolute path.
