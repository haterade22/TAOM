# Deep review: plan 058, isolate each hand-wired mission behaviour registration (#785)

```
DEEP REVIEW REPORT
===================
Feature: SubModule: isolate each hand-wired mission behaviour registration (#785), plan 058
Date: 2026-11-19

Scope:   C# (Main/SubModule.cs AddTaomMissionBehaviors, new Main/Composition/MissionBehaviorBuild.cs,
         a doc comment in Main/Composition/FeatureModuleHooks.cs) and 10 test files.
         Diff 8e836fd40..3e1b1dc9b on improve/058-isolate-mission-behaviour-registrations.
Blast radius: UNCHECKED by graphify (the lead did not run it); the change keeps every public and
         internal signature except the SubModule-local function rename, and the two lenses traced every
         caller of the renamed local function inside AddTaomMissionBehaviors.
Waves:   one wave: Agent 2 (Engine compatibility), Agent 5 (Data flow). Tier 1, mission start.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: PASS, 0 incompatible, 0 unverified (5 verified)
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     PASS, 0 gaps, 2 inconsistencies (both LOW; 1 confirmed and fixed, 1 by design)
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Details

### Agent 1, Standards

SKIPPED BY TIER (deep-review tier 1 rule).

### Agent 2, Engine compatibility

Verified against the installed v1.5.7 DLLs (taom-src cache `v1.5.7-202611101605`):

- `Mission.AddMissionBehavior` lists the behaviour before `OnCreated` (Mission.cs:4682 to 4693), and the
  engine add stays outside `MissionBehaviorBuild.Isolated`, in `AddBuiltTaomBehavior`.
- `MBSubModuleBase.OnMissionBehaviorInitialize` is still called at `Mission.AfterStart` :3825, after the
  `OnBehaviorInitialize` loop and before `EarlyStart` and `AfterStart`; each factory runs inside that
  call, so every behaviour is built and added at the same point as before.
- `Campaign.Current` is read inside the CareerPerk factory at the same moment as before.
- `CoopMissionRolePolicy.ShouldAdd(behavior.GetType(), ...)` still runs on every hand-wired and
  feature-module behaviour, through `AddBuiltTaomBehavior`.
- Generic inference names the concrete behaviour type for both null-returning factories.

Note (LOW): with "Survive Mission Start Failures" off, a build failure is now always contained, and no
comment said so. Lead verdict: FALSE POSITIVE as a defect. Plan Design item 6 and Maintenance note 7
make this the intended behaviour. The missing comment was cheap to add, so the lead added one sentence
to the `MissionBehaviorBuild` summary (see APPLIED).

### Agent 3, Efficiency

SKIPPED BY TIER.

### Agent 4, Completeness

SKIPPED BY TIER.

### Agent 5, Data flow

Nine flows traced. Registration order, every moved condition, the co-op add filter, the guard's notice
and summary, the engine add outside the isolation, the `[BattleLoad]` stamp and count, and agent-handle
capture are all connected. Two inconsistencies:

- Trace 8, OOM parity (LOW). `MissionBehaviorBuild.Isolated` caught every `Exception`, including
  `OutOfMemoryException`. The sibling guard never swallows one:
  `MissionStartGuardService.ShouldSurvive` returns false for it (MissionStartGuardService.cs:65-66), and
  the guard's own call wrapper lets it reach the engine untouched (MissionStartGuardCalls.cs:12-14).
  Before #785 an OOM thrown while building a TAOM behaviour therefore propagated; after it, the
  behaviour was dropped and the mission went on. Lead verdict: CONFIRMED, LOW. Fixed (see APPLIED).
- Trace 9, the failure logged twice (LOW). One failed build writes the `[MissionWiring]` line with the
  full exception (MissionBehaviorBuild.cs:34) and the guard's `[MissionStartGuard] ... threw in` line
  (MissionStartGuardLines.cs:19-20). Lead verdict: FALSE POSITIVE as a defect. Plan Design item 1 asks
  for the `[MissionWiring]` line with the exception, and it is the only full record when the guard
  service does not resolve, when its log write fails, or past its cap of 10 full lines per mission
  (MissionStartGuardService.cs:32, :86-89). The duplication is bounded by about 30 registrations. Kept;
  listed under NOT APPLIED.

### Agent 6, Design and elegance

SKIPPED BY TIER.

### Agent 7, XML and ModuleData integrity

NOT IN SCOPE (no XML in the diff).

### Tooling correctness

NOT IN SCOPE (no hook, validator or tool in the diff). No gate changed, so no differential sweep.

## Action items

1. Done: an `OutOfMemoryException` thrown while building a TAOM mission behaviour propagates again, as
   the mission-start guard requires.

## Improvements (Step 4)

- APPLIED: `Main/Composition/MissionBehaviorBuild.cs:29`, the catch is now
  `catch (Exception ex) when (!(ex is OutOfMemoryException))`. Test
  `MissionBehaviorBuildTests.Isolated_FactoryThrowsOutOfMemory_RethrowsWithoutLoggingOrReporting`
  failed first ("Assert.ThrowsException failed. No exception thrown. OutOfMemoryException exception was
  expected.") and passes after the fix.
- APPLIED: `Main/Composition/MissionBehaviorBuild.cs:11-13` and :18, the summary now says that the
  isolation does not read the "Survive Mission Start Failures" setting and that an
  `OutOfMemoryException` propagates. Comment only.
- NOT APPLIED: `Main/Composition/MissionBehaviorBuild.cs:34`, shortening the `[MissionWiring]` line to
  the type and message (Agent 5 trace 9). It is the fallback record when the guard cannot log; the
  simplicity criterion gives no clear win.
- FOLLOW-UP: none from the lenses. The plan's own deferral stays: the preamble of
  `AddTaomMissionBehaviors` and `TryClaimMission` are not isolated (plan Maintenance notes). No issue
  filed: an /improve lead files none, and the orchestrator decides at wrap-up.

## Verification

- Base totals in the worktree at `3e1b1dc9b`, before any edit:
  `Failed!  - Failed:     3, Passed: 42953, Skipped:     9, Total: 42965` (the three known
  language-file failures: `EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems`, `EveryKey_IsSeededInAllTwelveLanguageFiles`).
- After the fix: `Failed!  - Failed:     3, Passed: 42954, Skipped:     9, Total: 42966`, the same three
  failures and one more passing test.
- Focused run (`MissionBehaviorBuildTests`, `SubModuleMissionBehaviorIsolationTests`,
  `CoopMissionRolePolicyTests`, `CoopMissionAddFilterPinTests`): `Passed!  - Failed:     0, Passed:    20`.
- Not covered: no game run (tier 1 rule for this item); the new test uses no engine type, so the RefAsm
  no-game replay was not run; no tools or hooks changed, so the Python and hook suites were not run.

## Codex review

Codex not run for this item (no paid dispatch was authorised for it).

| # | Codex finding | Severity | Lead assessment | Action |
|---|---|---|---|---|
| - | Codex not run | - | - | - |

Codex lessons (pending): none. With no Codex run there is no false-positive pattern or missed category
to record.

VERDICT: READY FOR COMMIT
