# Plan review 028, round 1 (cold)

Plan: `plans/028-mission-tick-profiler.md` (1098 lines). Code read at `dffdf879` with `git show`
from the worktree (worktree HEAD is `e9cd8b39`; `git diff --stat dffdf879..HEAD` touches only
`docs/reference/bannerlord-engine-and-toolchain.md`, `docs/reference/engine/mission-frame-threads-and-native-costs.md`
and `docs/reference/provenance-register.md`, none in this plan's scope). No earlier
`plan-review-028*.md` existed, so there are no prior blocking items to re-check.

## Verdict

Executable by a weak model: yes, after one contract fix. The engine facts, the settings
procedure, the PatchShield walk and the SubModule anchors all check out against the code and the
installed engine. One blocking item (status lines carry the data tag plan 029 parses); the rest are
excerpt slips and gaps.

## Blocking

1. **Status lines use the `[TickProfile]` data tag, which plan 029's parser counts as malformed.**
   Plan lines 839 (`[TickProfile] off: 'Enable Tick Profiler' is off at game start ...`), 842-844 (the
   install INFO line, and line 1066 expects it to start `[TickProfile] install:`), 901 (the per-mission
   WARNING `[TickProfile] the profiler is on in MCM but ...`) and the unspecified ERROR lines of
   Steps 8 and 9. Plan 029's segmenter (`plans/029-perf-runs-parser.md:1194-1205`) sends every line
   containing `[TickProfile]` to `parse_tick_profile`; a line without `t=`/`frames=` returns None and
   increments `malformed` (or `orphan_malformed` before any mission). The `off:` line is written once
   per process for EVERY player (the toggle is off by default), so every log 029 reads reports at least
   one malformed line, and a profiled run reports two or more. Fix: give status lines a different tag
   (for example `[TickProfiler]`, which does not contain the substring `[TickProfile]`) and state the
   exact install-line text in Step 8 so the after-merge check at line 1066 matches it; or add the
   status prefixes to 029's ignore list as a coordinated change. Either way, write the chosen texts
   into the contract section (lines 387-399).

## Non-blocking

1. **Stale general rules after the allowlist gains a live entry** (quality bar: "A new permitted case
   amends every general rule"). Step 2 amends only `SettingRequireRestartPostureTests.cs:23-24`, but
   lines 25-27 of the same summary still say "The allowlist holds settings whose consumer is parked
   (commented out in SubModule.cs)", and `docs/features/mcm.md:94-95` says "One is allowlisted by
   `Class.Property` with a reason: `TaomSettings.EnableNativeSkinFixes`". Step 10 (line 962) only
   appends after line 102. Add both amendments and a stale-claim grep (`git grep -n "One is allowlisted"`
   and `git grep -n "whose consumer is parked"`).
2. **`wallMs` is never defined** (lines 387-398, Step 5 lines 680-681). The pinned sample implies the
   sum of closed frames' `frameMs` (12.5 + 812.4 + 40.1 + 95 + 4040 = 5000), and plan 029 divides by it
   (`plans/029-perf-runs-parser.md:1263`). Say so in Design and give `CloseFrame_SumsPhasesIntoTheWindow`
   that oracle.
3. **Window `top=` and the phase totals cover different frame sets.** `BehaviourTickTable.Record`
   updates window arrays immediately (line 629), but phase totals fold in only at `CloseFrame`, and
   `TakeWindow` runs inside the profiler behaviour's own `OnMissionTick` (mid-frame). So `top=` includes
   the unclosed frame's calls made before the profiler ticked; phase totals do not. Either state it in
   the doc section or fold behaviour window totals at `CloseFrame` too.
4. **Install line text unspecified** (Step 8 lines 842-844) while the maintainer's check (line 1066)
   expects `[TickProfile] install:` with `sites 2/2`. Give the exact format string (see Blocking 1).
5. **The cost claims are overstated.** Lines 41-42 and 431-433 say players pay nothing when off, but
   with the profiler off every player still gets: one `[PerfContext]` per mission (a `MemorySampleReader`
   call, about 84 us, plus four engine option reads), one `off:` line per process, and the three
   `ExcludedTargetMethods` entries, which remove PatchShield's rescue from every owner's patches on
   `Mission.OnTick`, `Mission.OnPreTick` and `Mission.TickAgentsAndTeamsImp` (Patch35's existing
   postfix on `OnTick` included) whether or not the profiler is installed. The exclusion follows the
   house rule (`docs/reviews/lessons/harmony-il.md:683`, "per frame"), so keep it, but name the trade-off
   in Design and in the commit body.
6. **Step 11 item 7** (line 982): `git diff dffdf879 -- Main/SubModule.cs` shows "exactly the two
   insertions" only if nothing else moved SubModule.cs. Plan 030 edits it too (`plans/030-mission-diagnostics-diet.md:518`,
   `:1509`), and lines 11-14 call 030 expected drift. Diff against the commit the executor started from.
7. **RefAsm unit step missing from Commands.** `TickProfilerTranspilerTests` has no category and reads
   `typeof(Mission).GetMethod("WaitTickCompletion", NonPublic | Instance)`, so hosted CI runs it on BUTR
   reference assemblies. I checked that `TaleWorlds.MountAndBlade.dll` in
   `bannerlord.referenceassemblies.core 1.5.3.122374-beta` still holds the names `WaitTickCompletion`,
   `OnPreTick`, `TickAgentsAndTeamsImp` and the private `AgentTickMT` (string probe of the metadata),
   so it should pass, but dispatch rule 7 asks for the RefAsm step from `.ai/verification.md`; list it
   in the table and in Step 11.
8. **A test that never fails first.** `PatchCategory_IsAppliedOnlyThroughTheInstaller` (line 874) passes
   before and after the change (line 875 admits only the first two fail). Either drop it in favour of the
   done-criteria grep at line 1013 or say why it is a guard rather than a TDD test.
9. **`MissionTickProfilerBehavior` has no test.** Step 9's only RED is the source pin; the behaviour's
   own logic (first-tick `[PerfContext]`, the on-but-not-installed WARNING once per mission, emission
   only when measuring, self-disable on fault) is not engine-bound beyond `Mission` reads. ADR-008 asks
   80% for hooks; either add a `RequiresGame` test modelled on `FeatureModuleHooksTests`, or name the
   behaviour in the `Not-tested:` trailer (line 523 and line 1001 list only `PerfContextReader`).
10. **`AgentTick_EnterExitOnAnotherThread_IsCountedAsOffMain`** (line 768): the hooks expose no
    off-main/on-main split, so the plan should say how to observe it (for example a hitch line logged
    with threshold 1 ms whose `otherMs` was not reduced by the agent tick).
11. **Shared static profiler across missions.** `EndMission()` is unconditional (line 667, 906). If a
    new mission's `OnCreated` ever runs before the previous mission's `OnEndMission`, the new mission
    stops measuring. UNVERIFIED whether Bannerlord ever orders it that way; a mission token on
    `EndMission` would remove the question.
12. **"Same `t`" is near-certain, not guaranteed** (lines 381-383, 1068). Each behaviour samples its own
    timestamp at a different point of the frame, so a 5 s boundary can occasionally fall between them.
    Soften the in-game check to "matches the `[MissionPerf]` line beside it, give or take one frame".
13. **Line 707 says `PerfContext` holds "17 values"**; the line has 16 keys (build, jitOptimized, clr,
    serverGC, latency, missionInProcess, scene, agents, textureQuality, shadowQuality, particleDetail,
    ragdolls, memLoad, availPhysMB, tickProfiler, diag).
14. **Lines 399 and 1057 say plan 029 "pins the same" literals.** It does not: 029 pins its own
    (`plans/029-perf-runs-parser.md:422-435`) and adds one test copying 028's literals once 028 has
    landed (`:363-382`). Reword the orchestrator step to "confirm 029's copy test holds 028's three
    literals character for character".
15. **Drift-check list omits files whose excerpts the plan relies on but does not edit:**
    `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs` (registration), `Main/Features/BattleCorpses/BattleCorpsesModule.cs`
    (adapter registration), `Main/Features/BattleLoadDiagnostics/MemorySampleReader.cs` and
    `Domain/MemorySample.cs`. Optional.
16. **Step 2 provider snippet** (lines 574-575) writes `Instance?.TickProfilerTopN`; it must be
    `BattleLoadDiagnosticsSettings.Instance?...` to compile. Trivial, but a literal copier will hit CS0103.
17. **Test count in Step 11** ("Passed equal to 12345 plus the number of new tests"): Step 2 lists
    inputs per test name (0, 21, -5 and so on). If the executor uses `[DataRow]`, each row is a separate
    result in the totals. Say whether these are single methods with several asserts (the existing
    `ValidateSampleIntervalSeconds_RangeEdges_ReturnRaw` pattern) so the expected total is computable.

## Excerpt check (plan line, claim, what the code at `dffdf879` shows)

Mismatches:

1. Line 53: heartbeat clock "lines 193, 201-202, 256-266". The file is 119 lines; `OnCreated` is
   line 44, the two timestamp lines are 52-53, `Reset()` is 107-118. The excerpt text itself matches.
2. Lines 104-105: provider "lines 53-60". The comment starts at line 54; the method is 56-60.
3. Line 153: `internal static bool TryRead(out MemorySample sample)`. It is `public static bool TryRead`
   (line 64) on `internal static class MemorySampleReader` (line 12).
4. Lines 171-172: "`Patch35_Mission_OnTick`, applied at `SubModule.cs:1816`". Line 1816 is
   `TryPatchCategory("Patch35_CompanionTactics");`; the class carries that category
   (`Patch35_Mission_OnTick.cs:16`).
5. Line 190: "`SubModule.cs:1990-1993` already reads `BattleLoadDiagnosticsSettings.Instance`". The call
   spans 1988-1993; the read is line 1991.
6. Lines 127-129: the doc test "requires" `**<total>**` in coop-interop.md and ` <total> MCM settings`
   in bannerlord-together-compat.md. It accepts either form in each doc (`SettingsFingerprintTests.cs:242-245`).
7. Lines 399 and 1057: plan 029 "pins the same" literals (see non-blocking 14).

Verified as written: `Patch91_MissionTickStallProbes.cs:15-24` (and `MissionTickStallProbe.cs:31`
uses `DateTime.UtcNow`); `BattleLoadDiagnosticsSettings.cs:34, 58-61`; interface 31 lines, provider 61
lines; `BattleLoadDiagnosticsIoC.cs:10`; `IoC.cs:178`; `CoopSettingsRelevance.cs:70-72`;
`SettingsFingerprintTests.cs:205-252` (line 211 `reflected: 9`); `SettingRequireRestartPostureTests.cs:23-30, 38-41`;
`docs/features/coop-interop.md:310-316`; `bannerlord-together-compat.md:291`; `mcm.md:100-102`;
`feature-map.md:72`; `mission-perf-heartbeat.md:38`; registry `## Patch91_MissionTickStall` (1051),
`## Patch96_TournamentRewards` (1101) then the backlinks marker (1107); `IGraphicsOptionsAdapter.cs`,
`GraphicsOptionsAdapter.cs:17-23, 38`; `BattleCorpsesModule.cs:30`; `PatchShieldPolicy.cs:134-148`;
`Dependencies/SubModule.cs:293` (pass 2, unguarded inside `OnGameInitializationFinished`);
`SubModule.cs:22, 907, 1565-1566, 1916-1923, 2110-2113`, `OnMissionBehaviorInitialize` at 2002;
`RepoPaths.ReadSource(..., stripComments)`; `Patch97` free at `dffdf879` and in plans 029-035. Settings
arithmetic: 320 + 12 + 1 + 7 = 340, and 340 - 217 = 123.

Engine facts re-derived this round: `taom-src` v1.5.3 `Mission.cs` lines 1074, 1338, 1392, 3536, 3546,
3601, 3617 (`tickCompleted = true` before `AfterAsyncTickTick`), 3652, 3748-3791;
`MissionState.cs:133-219`; `MissionScreen.cs:567`/`601`; `NativeOptions.cs:274` and the four enum
members; no `OnTick`/`OnPreTick`/`TickAgentsAndTeamsImp` overloads (so `TargetOf` without argument
types is unambiguous). IL of the installed `TaleWorlds.MountAndBlade.dll` (reflection-only load, token
scan): `OnPreTick` 55 bytes with `call Mission::WaitTickCompletion` at IL_0001 and
`callvirt MissionBehavior::OnPreMissionTick` at IL_0023; `OnTick` 1282 bytes with
`callvirt OnPreDisplayMissionTick` at IL_03e1 and `callvirt OnMissionTick` at IL_0435;
`WaitTickCompletion` 17 bytes. `System.GC.GetAllocatedBytesForCurrentThread` resolves on this
machine's desktop CLR (Windows PowerShell reflection).

UNVERIFIED: the baseline totals (dotnet not run; they match `plans/_audit/2026-10-02-perf/baseline.md:7-9`);
the `graphify_taom.py affected` outputs (not re-run; a refresh writes the graph); whether native
`Mission.IdleTick` raises `OnPreTick` (the plan already says so).

## Checklist items

- TDD order: RED before GREEN in Steps 2 to 9; see non-blocking 8 and 9.
- Issue line: "filed by the orchestrator before execution" (line 30) and orchestrator step (1053). OK.
- Binding ADRs: 002, 003, 004, 005, 007, 008 named with one line each (272-281). OK.
- Protected files: none edited; listed out of scope (495-496). No Step 0 needed. OK.
- Single-owner: `SubModule.cs` exactly two edits with content anchors and a pin test; `IoC.cs` and
  `TAOM.csproj` out of scope with a STOP. OK.
- STOP conditions (1028-1049): specific to this plan's risks. OK.
- Done criteria (1005-1026): machine-checkable; I ran the dash grep form at line 1021 and it works and
  is clean at `dffdf879` for its paths.
- Planned-at `dffdf879` and drift paths consistent with Scope. OK (see non-blocking 15).
- Non-deploying commands with `-p:ModuleId=` on build and test. OK.
- No worktree path, branch name or CHANGELOG step. OK.
- No em or en dash in the plan (scanned for U+2013 and U+2014); no secret values. OK.
