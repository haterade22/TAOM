# Deep review: plan 028, mission tick profiler (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: Mission tick profiler (plan 028): per-behaviour ms and allocation, engine phases,
         [Hitch], [PerfContext], [TickSummary]
Date: 2026-10-02

Scope:   C# (Main/Features/MissionPerf, BattleLoadDiagnostics settings, Patch91 bracket,
         GraphicsOptionsAdapter, SubModule wiring, TAOM.Dependencies PatchShieldPolicy), tests, docs.
         Branch perf/028-mission-tick-profiler, diff 0d1e91f0..b3bac1a9.
Blast radius: UNCHECKED by the lead (graphify not run in this delegated step); the lenses traced the
         callers by hand: the only callers of the new types are Patch97, Patch91's bracket and the two
         SubModule lines.
Waves:   lenses 1 (Standards), 2 (Engine), 3 (Efficiency), 4 (Completeness), 5 (Data flow),
         6 (Design) ran before this lead step; lens 7 (XML) NOT IN SCOPE (no XML in the diff).

STANDARDS:     FAIL: 0 critical, 0 high, 1 medium, 5 low (no ADVERSARIAL ESCALATION: no CRITICAL)
COMPATIBILITY: PASS: 0 incompatible, 4 unverified (native), 21 verified; 2 medium and 2 low text
               and logging findings
EFFICIENCY:    FAIL: 1 issue medium, 2 low, 2 follow-up
COMPLETENESS:  INCOMPLETE: hooks under-tested (M1), PatchShield skip unlogged (M2), issue not
               filed (deferred by D4), doc and draft gaps
DATA FLOW:     FAIL: 29 flows traced, 6 gaps, 7 inconsistencies
DESIGN:        8 KEEP proposals (6 apply, 2 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE (no hook, validator or CI step changed)
```

## Details

Every finding below was re-read against the code in the worktree before it was classified. Lens
numbering in brackets: L1 Standards, L2 Engine, L3 Efficiency, L4 Completeness, L5 Data flow,
L6 Design.

### Confirmed and fixed

| # | Sev | Finding | Lenses | Fix | Proof |
|---|---|---|---|---|---|
| R1 | MED | `[Hitch]` is an INFO line (synchronous flush on the main thread) for every frame at or above the threshold, with no per-mission bound: at the 50 ms floor in a battle under 20 fps it is a per-frame INFO line for the whole battle, against D6 rule 5 | L1, L2 F2, L3 F1, L5 F2 (L4 and L6 judged it bounded) | `MissionTickProfiler.MaxHitchLinesPerMission = 100`: the first 100 hitch frames of a mission come back from `CloseFrame` in full; later ones are counted (`hitches=`, `worstHitchMs`, `worstHitchT` unchanged) and one `[TickProfiler] hitch line cap reached` line marks the cut. `FrameTop` is no longer allocated past the cap | `CloseFrame_PastTheHitchLineCap_CountsEveryHitchButReturnsOnlyTheFirstCap`, `BeginMission_ResetsTheHitchLineCap`, `OnFrameBoundary_PastTheHitchLineCap_WritesTheCapLineOnceAndNoMoreHitchLines` (RED: missing members, then the hook test failed on behaviour) |
| R2 | MED | The three new `ExcludedTargetMethods` entries apply to every player, and PatchShield skipped them silently: only a mixed `skipped` count reached diag.log (D6 rule 3) | L1, L2 F4, L3 FU1, L4 M2, L5 F1, L6 P3 | `PatchShieldPolicy.FormatHotMethodSkip` plus `PatchShield.LogHotMethodSkip`: one diag.log line per patched method skipped by name, with its owners and what is given up, once per process (the method is recorded as seen first). It also covers the seven older entries | `FormatHotMethodSkip_NamesTheTargetItsOwnersAndWhatIsGivenUp`, `FormatHotMethodSkip_NoOwnersKnown_SaysUnknown` (RED: missing member) |
| R3 | MED | The trade-off text was wrong both ways: "no per-call PatchShield finalizer runs inside a frame" is false (`Mission.Tick`, `MissionState.TickMissionAux`, `MissionState.OnTick` still carry one per frame), "PatchShield no longer rescues a throwing patch of any owner" overstated the loss (only the missing-API trinity was ever swallowed, and from a process's second game start a throw from `Mission.OnTick` is still swallowed one frame up; in its first game it reaches Patch37 on `Module.OnApplicationTick`, see convergence round 1 C1), and the policy comment tied all three entries to Patch97 only | L2 F1, L1, L3 F2, L5 F3, L6 P2 | Policy comment, feature doc "PatchShield" section, registry entry rewritten from the engine reading; the corrected sentence is in the follow-up commit body (b3bac1a9 is not amended) | `lint_docs.py --fail-on-drift --dash-base 0d1e91f0` exit 0; the comment and docs cite Mission.cs `tickCompleted` writes |
| R4 | MED | `MissionTickProfilerInstaller.InstallIfEnabled` (the off-path guarantee, the `Installed` rule), `MissionTickProfilerBehavior`, `PerfContextReader`, `TimedWaitTickCompletion` and every fault path had no test; the plan's "needs a live Mission" was a draft | L4 M1, L1, L6 P5 | Installer latch removed (its caller's once-per-process guard is pinned by `MissionTickProfilerWiringTests`); `MissionTickProfilerInstallerTests` (6), `MissionTickProfilerBehaviorTests` (7, null mission), hooks tests for the timed wait, a logger fault, the null-profiler path and the no-frame summary | The installer tests failed against the latched installer (only the first test in a run could install), then passed |
| R5 | LOW | Missions that do not measure did not say why: installed but switched off (no line), switched on after game start (a warning pointing at lines that were never written), measured but no frame closed (no summary, no line) | L1, L4 L1a/L1d, L6 P1, L2 | `BuildMissionOffLine`, `RestartNeededLine`, `NotInstalledLine` reworded for the failed install only, `BuildNoFramesLine`; `WriteSummary` runs only for a measured mission | `MissionTickProfilerBehaviorTests` (four status cases), `WriteSummary_NoFrameClosed_LogsWhyThereIsNoSummary` |
| R6 | LOW | Reason lines named the wrong consequence: a transpiler fault said "measuring stopped" (the method is left vanilla), a site or helper mismatch said "records nothing for this method" (OnPreTick: measuring continues with wait and pre-tick in `otherMs`; OnTick: no mission is measured) | L1, L4 L3, L5 F6c, L3 F3 | `BuildRewriteFault`; one `LeftVanilla(method)` consequence used by the three builders; `WaitUnboundLine` names where the wait lands | `StatusLines_MatchTheirPinnedLiterals` |
| R7 | LOW | The per-mission header omitted the live call-site counts, so a mission could say "measuring" after a re-patch dropped a site | L1, L4 L1b, L2 F8 | Header carries `sites Mission.OnTick a/2 Mission.OnPreTick b/c` and the hitch-line cap | `OnCreated_InstalledAndOn_WritesTheMissionHeaderAndStartsMeasuring` |
| R8 | LOW | Measuring starts at `OnCreated` but the header was written at the first `OnMissionTick` (the engine's fourth tick), so up to three `[Hitch]` lines could precede it (D6 rule 1) | L5 F5 | Header and the not-measuring reasons written from `OnCreated`; `[PerfContext]` stays at the first tick (agent count) | `MissionTickProfilerBehaviorTests` |
| R9 | LOW | `[PerfContext] diag=` listed `stallWatchdog`, `stallBundle` and `exitSampler` as on when the master toggle was off and none of them could run (`BattleLoadStallWatchdog.cs:135`, `ExitStallSampler.cs:82`) | L5 F4 | `DiagTokens` folds the master and the watchdog gates | `DiagTokens_MasterOff_OmitsTheThreeDiagnosticsItGates`, `DiagTokens_WatchdogOff_OmitsItsBundle` (RED at run time, then green) |
| R10 | LOW | Only the install status line was pinned literally (D6 rule 6) | L1 | Every status line pinned | `StatusLines_MatchTheirPinnedLiterals` (16 literals) |
| R11 | LOW | The hint, the interface comment, the allowlist reason, mcm.md and the settings table said the toggle is read once per process; it is re-read per mission, so turning it off works from the next mission | L1, L5 F8 | All five texts state both directions; the cost of "installed, then off" is in the doc | Read back; `SettingRequireRestartPostureTests` green |
| R12 | LOW | The category name was a literal in three places with nothing linking them | L1, L5 F7 | `[HarmonyPatchCategory(MissionTickProfilerInstaller.Category)]` | Build; binding gate 407 passed |
| R13 | LOW | The by-name `WaitTickCompletion` lookup was not catalogued | L2 F3 | `ReflectionSiteBindingTests` row and reflection-sites.md Category B row | Binding gate 406 to 407 |
| R14 | LOW | Doc gaps: feature-map row without `[TickSummary]`, what `otherMs` holds, phantom hitches from a menu over a town mission, the longer first window, the extra stack frame, "one null check per agent tick", the `[TickSummary]` pin's inputs | L2 F5/F6/F7/F8, L4 L3, L5 F9/F10 | Feature doc, registry, feature map | `lint_docs.py` exit 0, ai_dashes 0 |
| R15 | LOW | `BehaviourTickTable`, `BehaviourTotal` and the `Behaviours` property broke the codebase's `Behavior` spelling (one against 144) | L1 | Renamed (files moved), with the P6 rewrite | Build and the 11 table tests |

### False positives and accepted designs

| Finding | Lenses | Verdict |
|---|---|---|
| Only the first failed `[PerfContext]` read is named | L5 F6b, L4 L1d | By design: D6 rule 3 asks for one reason line; every failed field reads `na`, `-1` or `unknown` |
| `agentTickMs` depends on Patch91 and the install line does not say whether its bracket applied | L5 F6d, L6 | Accepted: Patch91's own `[PatchApply]` line reports its apply; adding a Harmony patch-info probe to the installer fails the simplicity rule |
| A per-mission cap fails simplicity | L4, L6 | Overruled by D6 rule 5 (binding) and plan 041, which turns the profiler on by default: the cap adds one constant and one bool, and changes no data literal |
| Make the exclusion conditional on the profiler | focus (1), all lenses | Rejected by every lens: TAOM.Dependencies loads first and would need a mutable cross-assembly list ordered against pass 2; two of the three entries serve Patch35 and Patch91 for every player |

### Orchestrator focus

1. **PatchShield exclusion.** Needed with the profiler off for `Mission.OnTick` (Patch35's postfix) and
   `TickAgentsAndTeamsImp` (Patch91's bracket), both applied for every player and per frame under the
   house rule (lessons/harmony-il.md 2026-09-26 and 2026-09-28). `OnPreTick` serves only Patch97 and
   costs nothing while unpatched. The loss is acceptable and narrower than first written (R3): only the
   missing-API trinity was swallowed, and on the asynchronous agent tick the old swallow turned a crash
   into a permanent `WaitTickCompletion` spin. A throw from `Mission.OnTick` is still swallowed one frame
   up by the shield on `TickMissionAux` only from a process's second game start (that rescue strips
   Patch91's own prefix there, FOLLOW-UP L1); in a process's first game it reaches Patch37's
   crash-report finalizer on `Module.OnApplicationTick` (convergence round 1 C1). It was not logged; it
   now is, once per method (R2).
2. **`[Hitch]` volume.** Bounded per second (1000/threshold), unbounded per mission; capped at 100 per
   mission with the overflow counted in `[TickSummary] hitches=` (R1).
3. **D6.** Header, periodic, summary and reason lines now cover every path the lenses traced, with the
   exceptions accepted above. The provider fallback and the silent memory read are now logged
   (convergence round 1 C3).
4. **Known failures.** Matched; see the suite totals below.

## Action items

1. Plan 029's parser counts `[Hitch]` lines (`perf_runs.py:354-365`, per L5); with the cap it must take
   the hitch count from `[TickSummary] hitches=`. The data literals are unchanged.
2. Run `/verify-bindings` (patch-targets.md does not list Patch97's targets, L4).
3. File the plan 028 issue before the branch lands (D4), correcting the draft (it still says "spawn
   work" and omits `[TickSummary]`, L4 L4).
4. The convergence pass (deep-review Step 4.6) on the fix diff is owed: this lead cannot spawn a
   reviewer. The lead read the fix diff for standards and parity and found nothing further.

## Improvements (Step 4)

APPLIED:
- `Main/Features/MissionPerf/BehaviorTickTable.cs`: P6, one `Accum` struct array per layer (frame,
  window, mission) instead of twelve parallel arrays, `Grow` resizes six arrays instead of fifteen;
  `TakeTop` renamed `WindowTop`. Behaviour-preserving: `BehaviorTickTableTests` (11),
  `MissionTickProfilerTests`, `MissionTickProfilerHooksTests` green before and after.
- `Main/Features/MissionPerf/TickProfilerTranspiler.cs`: P7, Harmony's `CodeInstruction.Calls` replaces
  the name-and-arity matcher (exact method). Proof: the 8 `TickProfilerTranspilerTests` and both
  `MissionTickProfilerBindingTests` rewrites against the installed v1.5.3 IL.
- `Main/Features/MissionPerf/MissionTickProfiler.cs`: P8, the unread `HitchThresholdMs` property
  deleted. Proof: the build.
- `Main/Features/MissionPerf/Hooks/MissionTickProfilerInstaller.cs`: P5, the redundant attempt latch
  deleted and the installer tested (R4). Proof: `MissionTickProfilerWiringTests` pins the
  once-per-process caller; `MissionTickProfilerInstallerTests`.
- P1 and P2 applied as R5 and R3; L3 F3 applied as R6.

NOT APPLIED:
- L2 F5 optional: re-stamping the frame boundary from `OnMissionStateActivated`. Behaviour-changing and
  it covers state pushes only, not the encyclopedia's disable request; the doc caveat was applied.
- L1 D6(d): the provider's two new validators fall back silently, like its two older siblings
  (`StallWatchdogSeconds`, `MemorySampleIntervalSeconds`). The provider has no logger; the MCM
  attributes clamp the UI and the per-mission header prints the effective values. Listed for Mike as a
  provider-wide decision.

FOLLOW-UP (pre-existing code, not applied, no issue filed: issue filing is the orchestrator's on Mike's word):
- L1: PatchShield's protected owner prefix is `TAOM`, TAOM's Harmony owner is `com.taom.mod`
  (`Main/SubModule.cs:204`), and `coop-modules.txt` adds no `com.taom` entry, so a rescue on a shielded
  method would strip TAOM's own patches; `submodule-lifecycle-and-harmony.md:29` claims otherwise.
  Recommend `/investigate`.
- L6 P4, L3 FU2, L5: `Mission.Tick` (Patch37), `MissionState.TickMissionAux` (Patch91),
  `MissionState.OnTick` and `MapState.OnTick` (Patch43) still carry a per-frame shield finalizer; plan
  034 puts the exclusion lists out of its scope, so the deferral has no owner.
- L2: PatchShield's per-swallow diag.log line is not deduplicated; a stale generation could be closed by
  overriding `OnMissionStateFinalized` (reachability UNVERIFIED).
- L5: `IGraphicsOptionsAdapter` is registered by `BattleCorpsesModule`, and `SubModule.cs` now resolves
  it for every mission; its registration belongs in `IoC.cs` (single-owner: recommendation only).
- L5 F6a: `MemorySampleReader.TryRead` fails without a reason, so `memLoad=na` carries none.
- `MissionTickProfilerHooks.cs` is at 149 of ADR-002's 150 lines; split it before plan 041 adds to it.
- L4: `top=` uses `Type.Name`; two behaviours with one class name in different namespaces would merge.

## Codex review

Codex not run: no paid dispatch was authorised for this item.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | | | | Codex not run |

## AGENTS.md lessons (pending)

For the orchestrator to consolidate into `.ai/review-reference.md` "Look harder here" at wrap-up:
- A diagnostic written per event (per frame, per agent, per hit) at a synchronous log level needs a
  per-run cap whose overflow is counted, not only a per-second bound from its threshold.
- A target added to a skip or exclusion list must be named in the log once, and the comment beside it
  must give every consumer's reason, not only the newest one's.

## Suite totals

- Base `b3bac1a9` before any edit: `Failed! - Failed: 1, Passed: 12423, Skipped: 2, Total: 12426`
  (EveryLanguage_DeclaresARowForEveryEnglishKey).
- After the fixes, full suite: `Failed! - Failed: 1, Passed: 12450, Skipped: 2, Total: 12453`, the
  same single known failure (EveryLanguage_DeclaresARowForEveryEnglishKey).
- Binding gate: `Passed! - Failed: 0, Passed: 407, Skipped: 0, Total: 407` (406 at the base; the new
  reflection-site row).
- Reference-assembly unit step: `Failed! - Failed: 3, Passed: 10114, Skipped: 28, Total: 10145`, exactly
  the three known failures (EveryLanguage_DeclaresARowForEveryEnglishKey,
  Patch93_HasTheSevenPatchesInItsCategory, Patch94_HasTheMapIconNoParleyAndNoJoinPatches).
- `python tools/lint_docs.py --fail-on-drift --summary --dash-base 0d1e91f0`: exit 0, ai_dashes 0
  (context_budget 10, outside this diff).

VERDICT: READY FOR COMMIT

## Convergence round 1

Convergence review of the fix diff `b3bac1a9..e64529b3`; three LOW findings, each re-read against the
code before it was classified. All three are fixed in the commit
`fix(mission-perf): v2.0.32 - convergence fixes for plan 028` (the commit that adds this section).

| # | Sev | Finding | Verdict | Fix | Proof |
|---|---|---|---|---|---|
| C1 | LOW | R3's corrected text said a missing-API throw escaping `Mission.OnTick` "is still swallowed one frame up, by the shield on `MissionState.TickMissionAux`". That shield exists only from a process's second game start: Patch91 is applied inside TAOM's once-per-process game-init block (`Main/SubModule.cs`, `TryPatchCategory("Patch91_MissionTickStall")`), after the Dependencies module's PatchShield pass 2, whose doc says TAOM's late batch is attached only at a second start. In a process's first game a foreign mod's load-time patch on `Mission.OnTick` now throws through `TickMissionAux`, `MissionState.OnTick` (Patch43, a postfix only), `GameStateManager.OnTick` and `Game.OnTick` to Patch37's `ModuleOnApplicationTickFinalizer`. From a second start the rescue at `TickMissionAux` unpatches every unprotected owner's prefix, postfix and transpiler there (`TryUnpatchOffendingPatches`), Patch91's prefix included, since `com.taom.mod` does not start with the protected prefix `TAOM` | Fixed (docs only; the exclusion decision stands) | The sentence is qualified in the feature doc's PatchShield section, the registry entry, the harmony-il lesson (with a Prevent line on shield timing), R3 and focus 1 above. The commit body carries the correction | `lint_docs.py --fail-on-drift --summary --dash-base e64529b3`: exit 0, ai_dashes 0 |
| C2 | LOW | Three `BehaviorTickTableTests` still named `TakeTop`, renamed `WindowTop` by P6 | Fixed | `WindowTop_BeforeFoldFrame_SeesNothingFromTheOpenFrame`, `WindowTop_OrdersByTotalMsDescending_ThenByName_AndTruncatesToN`, `WindowTop_NoCalls_ReturnsEmpty`; `git grep TakeTop` outside `plans/` now finds only the P6 note above | Filtered run `BehaviorTickTableTests.WindowTop_`: `Passed! - Failed: 0, Passed: 3` |
| C3 | LOW | Two fallbacks in this plan's code wrote no reason line, against the binding D6 note: (a) `PerfContextReader` wrote `memLoad=na availPhysMB=na` when `MemorySampleReader.TryRead` returned false (it never throws, so `Safe` never saw it); (b) the provider's `ValidateTickProfilerTopN` and `ValidateHitchThresholdMs` replace an out-of-range value with the default silently. The earlier dispositions (FOLLOW-UP L5 F6a as "pre-existing", NOT APPLIED L1 D6(d) as "listed for Mike") did not hold: both are new in plan 028 and no maintainer decision was recorded | Fixed | (a) `TickProfileLines.MemoryReadFailedLine`, taken as the mission's context fault line when no earlier read failed. (b) `TickProfileLines.SettingFallbackLines(rawTopN, topN, rawHitchMs, hitchMs)`: `MissionTickProfilerBehavior` writes one WARNING per replaced knob right after the measuring header, comparing the raw MCM value with the provider's effective one. The provider stays logger-free, and its two older siblings are out of this plan's scope | `StatusLines_MatchTheirPinnedLiterals` and `StatusLines_NeverContainADataTag` extended; `SettingFallbackLines_BothInRange_ReturnsNone`, `SettingFallbackLines_BothOutOfRange_NamesEachSettingItsRawValueAndTheValueUsed`, `SettingFallbackLines_OnlyThresholdOutOfRange_ReturnsOnlyItsLine`. RED: CS0117 for the missing members; GREEN: MissionPerf filter `Passed! - Failed: 0, Passed: 106` |

The FOLLOW-UP line "L5 F6a: `MemorySampleReader.TryRead` fails without a reason" and the NOT APPLIED
item "L1 D6(d)" above are superseded by C3 for the tick profiler's own use; `TryRead` itself still
returns false without naming the cause.

### Orchestrator focus, round 1

1. **PatchShield exclusion with the profiler off.** Still needed: `Mission.OnTick` carries Patch35's
   postfix and `TickAgentsAndTeamsImp` Patch91's bracket for every player, per frame. `OnPreTick` serves
   only Patch97 and costs nothing while unpatched. The loss is acceptable but larger than R3 said in a
   process's first game (C1). It is logged once per method by `PatchShield.LogHotMethodSkip`, guarded by
   the coverage record, so a method is visited once per process (R2).
2. **`[Hitch]` volume.** Bounded: `MaxHitchLinesPerMission = 100` full lines per mission, the overflow
   counted in `[TickSummary] hitches=` and `worstHitchMs=`, one cap line at the cut. The threshold's
   floor is 50 ms (default 250), enforced by the MCM attribute and the provider.
3. **D6.** Header, periodic `[TickProfile]`, `[TickSummary]`, and a reason line for every disable,
   skip and fallback the reviewers traced, now including the two in C3.
4. **Known failures.** Matched; see below.

### Suite totals, round 1

- Base `e64529b3` before any edit: `Failed! - Failed: 1, Passed: 12450, Skipped: 2, Total: 12453`
  (EveryLanguage_DeclaresARowForEveryEnglishKey).
- After the fixes, full suite: `Failed! - Failed: 1, Passed: 12453, Skipped: 2, Total: 12456`, the
  same single known failure; the three added tests are the difference.
- No test added here touches an engine type (the new tests are in `TickProfileLinesTests`, pure), so
  the reference-assembly step was not rerun; no hook, validator or CI step changed.
- `python tools/lint_docs.py --fail-on-drift --summary --dash-base e64529b3`: exit 0, ai_dashes 0
  (context_budget 10, outside this diff).

## Convergence round 2

Convergence review of the round 1 fix diff `e64529b3..7ca09cc2`; one LOW finding. The review
workflow's last round runs no fix pass, so the orchestrator re-read it against the v1.5.3 engine
source and fixed it in docs, in the commit `docs(mission-perf): v2.0.32 - first-game cost of the
OnTick exclusion` (the commit that adds this section).

| # | Sev | Finding | Verdict | Fix | Proof |
|---|---|---|---|---|---|
| C4 | LOW | C1's corrected text still understated a process's first game: a foreign mod's missing-API throw from `Mission.OnTick` does not skip only "the rest of that frame's state tick". It unwinds the whole application tick, and a postfix that throws after `Mission.OnTick` has ended the mission stops the finished battle from ever being popped | Confirmed | Feature doc "PatchShield" section and the registry's Patch97 entry now name both consequences and the second-start difference | `Module.OnApplicationTick` calls `GameManagerBase.Current.OnTick` and only then each module's `OnApplicationTick`, `JobManager.OnTick` and `AvatarServices.UpdateAvatarServices` (v1.5.3 `TaleWorlds.MountAndBlade.Module.cs`, the end of `OnApplicationTick`); `MissionState.OnTick` checks `CurrentState == Mission.State.Over` and pops only after `TickMission(realDt)` returns, and re-enters `TickMission` every frame while `MissionEnded` stays true (`TaleWorlds.MountAndBlade.MissionState.cs:97-129`); `TickMissionAux` calls `Mission.OnTick` (`:212-218`) |

**A cost for the maintainer to weigh, not a risk already accepted.** The exclusion of
`Mission.OnTick` keeps PatchShield's finalizer (one `__originalMethod` lookup and a try/catch) off a
method that runs once per frame on the main thread. The house rule it follows (lessons/harmony-il.md,
2026-09-26 and 2026-09-28) was written for per-unit and per-agent targets, where the cost multiplies
by hundreds of calls per frame on parallel workers. What the exclusion gives up is the rescue from a
foreign mod's broken patch on `Mission.OnTick`, which in a process's first game now unwinds every
frame's application tick and soft-locks battle end. `TickAgentsAndTeamsImp` is different: a swallow
there leaves `tickCompleted` false and hangs `WaitTickCompletion`, so its exclusion is a fix.
`OnPreTick` is patched only by the profiler. Keeping or narrowing the `Mission.OnTick` entry is the
maintainer's decision (the run's FOR-MIKE list), together with the protected-owner gap above
(FOLLOW-UP L1), since a rescue on `Mission.OnTick` would also strip TAOM's own Patch35 and Patch97
while `com.taom.mod` is unprotected.

Not changed: the commit body of `7ca09cc2` (history is not rewritten); the correction is carried in
this commit's body. Suite and lint for this docs-only commit are recorded in the run's PROGRESS.md.

## Fix pass 3: the Codex and Claude reviews of the D13 state (2026-10-03)

Two independent reviews of the branch at `81811ea2` were fixed in one pass, the commit that adds this
section. Every finding is a hypothesis: each was re-read against the code and the installed v1.5.3
engine (`pwsh tools/taom-src.ps1`) before it was classified.

- **Codex adversarial review** of the whole plan, `0d1e91f0..81811ea2`: 4 findings (1 P1, 1 P2, 2 P3).
- **Claude review** of the D13 change. It was run on plan 039's copy of that change (`f86342ca`, a
  sibling branch from the same base `765d3759`, not an ancestor of this branch) and compared with this
  branch's `81811ea2`, so five of its eight findings concern files and commits that are not here. The
  three that map onto this branch's own texts were fixed.

### Codex findings

| # | Sev | Verdict | Proof | Fix or reason |
|---|---|---|---|---|
| 1 | P1 | Confirmed as fact; older than the profiler, so docs only (orchestrator ruling) | `Mission.OnTick` returns before the clear when `CurrentState != Continuing` (`Mission.cs:3738-3741`), sets `tickCompleted = false` at `:3756`, runs the `OnMissionTick` loop (`:3757-3760`), then launches the agent tick (`:3784-3791`); only `TickAgentsAndTeamsImp` sets it true (`:3629`) and `WaitTickCompletion` spins on it (`:3601-3606`). Fast-forward ticks inline (`MissionState.cs:178,189,196`, `asyncAITick: false`). PatchShield's finalizer returns null for the missing-API trinity (`PatchShield.cs:261-263,306-321`); Patch37's finalizer on `Module.OnApplicationTick` swallows any exception while capture is on (`CrashReportPatchHelper.cs:36-60`). Trunk already shields `Mission.OnTick` through Patch35 | `PatchShieldPolicy.cs`, the feature doc, the registry, the `harmony-il.md` lesson and two test comments no longer say the exclusion or the shield prevents the hang; they state the three throw positions, name the PatchShield follow-up plan and the regression tests it owes (before the clear, between the clear and the launch, inside the inline agent tick). The agent-tick exclusion is described as buying only the async case, with the shim `Mission_TickAgentsAndTeams` having no catch (`ManagedCallbacks.CoreCallbacksGenerated.cs:936-940`) and native handling UNVERIFIED |
| 2 | P2 | Confirmed | `Installed` is assigned once (`MissionTickProfilerInstaller.cs:74`); `OnTickSites` is rewritten by every transpiler rerun (`Patch97_MissionTickProfiler.cs:29-33`) and read by nothing that gates; PatchShield strips by owner (`PatchShield.cs:403-409`), and the strip removes Patch97's transpiler, so nothing rewrites `OnTickSites` afterwards; the behaviour gated on `Installed` alone. RED before the fix, 24 failing of 60: `OnCreated_InstalledThenAZeroSiteRewrite_DoesNotMeasureAndWarnsOnce` ("Assert.IsFalse failed" on `Profiler.Measuring`), the four `OnCreated_InstalledThenARequiredHookIsRemoved_...` rows (same), `OnMissionTick_AHookGoesMissing...` ("Sequence contains no elements": no warning), the `HookHealthTests` removals ("CollectionAssert.AreEqual failed. (Different number of elements.)") | `HookHealth` (the pure rule) and `MissionTickProfilerHealth` (the four required hooks, resolved from the patch classes' attributes): at each mission start the site count and `Harmony.GetPatchInfo` for each required patch method, one aggregated `[TickProfiler]` warning naming what is missing, no measurement for that mission, checked again at the next; every tick of a measuring mission re-reads the site count and stops with one warning. `Installed` stays the startup fact. A patch-info strip mid-mission is read at the next mission start only: patch info is deserialized per `GetPatchInfo` call and each `Patch.PatchMethod` resolves by scanning the loaded assemblies (0.01 and 0.02 ms in a 30-assembly test host; unmeasured in the game), a price for a mission start and not for the measurement's own clock |
| 3 | P3 (reachability UNVERIFIED) | Not fixed; documented precisely | One `_agentTickStart` slot (`MissionTickProfilerHooks.cs`): an overlap would let the earlier exit consume the later stamp. `tickCompleted` is set before the `AfterAsyncTickTick` tail (`Mission.cs:3629-3633`), so tick N's tail can overlap frame N+1, but tick N+1 cannot start before the main thread has finished `OnPreTick` and the `OnTick` loops and launched it (`:3548`, `:3786`); an overlap needs a slow `AfterAsyncTickTick` subscriber (TAOM overrides none: no match in `Main`) or native starting the next tick sooner (UNVERIFIED). The fix, a per-call `__state` token, changes Patch91 for every player | "Known limits" in the feature doc and the comment on `_agentTickStart` state the assumption and when it would read low; the token waits for a measured overlap |
| 4 | P3 | Confirmed (test strength) | The wiring test checked only the position of the text `_gameInitPatchesApplied = true;`, which survives deleting `if (_gameInitPatchesApplied) return;`. RED: `InstallGuardCheck_WithTheEarlyReturnRemoved_Fails` ("Assert.ThrowsException failed. No exception thrown") against the old assertion | The pin now needs the early return, the latch and the install in that order inside `OnGameInitializationFinished` itself (no later member declaration between), with three mutation tests (guard removed, latch removed, install moved into another member) that fail it. `Main/SubModule.cs` is not touched |

### Claude findings

| # | Sev | Verdict | Proof | Fix or reason |
|---|---|---|---|---|
| C1 | MEDIUM | Rejected for this branch | `f86342ca` is plan 039's tip (`git merge-base --is-ancestor f86342ca HEAD` fails; merge-base `765d3759`). `81811ea2` is this branch's own D13 commit, so nothing here diverges from itself | The review's step 4 (the `tickCompleted` note should live on 028 so the shared Mission text is identical on 028, 039 and 041) is done through Codex 1: the hazard text now exists here. 039 and 041 still carry their own copies and need the same Mission text before the trial re-run (orchestrator's integration step) |
| C2 | LOW | Rejected | `docs/features/map-perf-profiler.md` is not on this branch (`git ls-files` finds no map profiler file) | Plan 039's doc |
| C3 | LOW | Confirmed for this branch's texts | Same engine lines as Codex 1: the inline call reaches `Mission.OnTick`, which D13 keeps shielded. The texts said "a correctness fix" for the exclusion and "reaches the native job thread" for every call | Rewritten with Codex 1 (the exclusion buys the async case only) |
| C4 | LOW | Confirmed for this branch's wording | `Mission.OnPreTick` has one `[HarmonyPatch]`, Patch97's (`Patch97_MissionTickProfiler.cs:36`); PatchShield attaches only to patched methods (`PatchShield.cs:163`). The texts said both methods "stay shielded", "go back under the shield" or "stay on" PatchShield | Texts say "not on the list" and that `OnPreTick` carries the finalizer only while the profiler is installed (wrong, superseded by convergence round 3, R3-1: any patch on it attaches the shield). The `81811ea2` commit body cannot be rewritten; this commit's body is precise |
| C5 | LOW | Confirmed | `harmony-il.md` kept "per unit, per agent or per frame" in both Prevent lines and the D13 bullet said "The rules above stand" | Both Prevent lines narrowed (per-frame main-thread targets keep the shield, D13) and the D13 bullet corrected |
| C6 | LOW | Rejected for this branch | The sentence is in `f86342ca`'s body. `81811ea2`'s body has no "or by code it calls" claim (`git show 81811ea2`) | The callee-thrown case is named in the new hazard text (a behaviour's `OnMissionTick` need not be a patch) |
| C7 | LOW | Rejected for this branch | Mission's namespace is exactly `TaleWorlds.MountAndBlade` (`Mission.cs:21`) and `PatchShieldPolicyTests.IsExcludedTargetNamespace_GameplayNamespaces_ReturnsFalse` pins that string; the match is an ordinal `StartsWith`, so any prefix that would exclude it is a prefix of that string and fails that test. The gap exists only for the longer map namespaces on 039 | None |
| C8 | LOW | Rejected for this branch | The shield's swallow line (type and message, no stack) is existing behaviour that trunk already has on `Mission.OnTick`; D13 restores it and `Mission.OnPreTick` is patched only by the default-off Patch97. The finding itself says "not in this commit" | FOLLOW-UP L8 below |

### Mutation checks

Each guard was broken in a scratch copy, the focused tests run, and the file restored byte for byte
(sha256 compared): the not-resolved branch, the per-target read cache, a throwing read, the patch kind,
the same-token fallback, an unresolvable patch method, the site check, the patch-info check, the stop,
the warning, the behaviour's start gate, its per-tick site check, its status line, its
installed-and-on condition, and the required-hook list's kind and patch class (only the binding test
guards those two, and it kills both). Sixteen mutations, each fails at least one test. A seventeenth
survived on the first run (a stop helper returning the wrong boolean), so the boolean was deleted, which
removes the branch it guarded. The wiring pin carries its own three mutation tests, because
`Main/SubModule.cs` is single-owner and was not touched.

### Suite totals, fix pass 3

- Base before the pass: not re-run (the one known failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`).
- Focused classes (`HookHealthTests`, `MissionTickProfilerBehaviorTests`, `TickProfileLinesTests`,
  `MissionTickProfilerWiringTests`, `MissionTickProfilerBindingTests`, `MissionTickProfilerInstallerTests`):
  `Passed! - Failed: 0, Passed: 77, Skipped: 0, Total: 77`.
- Full suite at the final code: `Failed! - Failed: 1, Passed: 12492, Skipped: 2, Total: 12495`, the same
  single known failure.
- `python tools/lint_docs.py --fail-on-drift --summary --dash-base 81811ea2`: exit 0, `ai_dashes` 0
  (`context_budget` 10, outside this diff).

### FOLLOW-UP added by this pass

- L7: the interrupted-tick hazard (Codex 1): the PatchShield follow-up plan owns it, with the culprit-only
  strip (L1) and the three regression tests named above. Patch37's crash capture swallows on the same
  path, so the plan has to cover it too.
- L8: PatchShield's swallow line keeps the type and the message only; the first swallow per target and
  exception type should log the stack and count the repeats (plan 034 edits the same lines).
- L9: Mission text on 039 and 041 (and the older wording in their `PatchShieldPolicy.cs` comments) must
  match this branch's before the integration trial re-run.
- L10: `MissionTickProfilerHooks.cs` (149 lines) and `MissionTickProfilerBehavior.cs` (147) sit at
  ADR-002's 150-line limit; this pass put the new logic in `HookHealth` and `MissionTickProfilerHealth`
  for that reason, and plan 041's additions need a split first.

## Convergence round 3 (2026-10-03)

Convergence review of fix pass 3's diff `81811ea2..31c0fd15`; eight LOW findings. Each was re-read against the
code, the installed v1.5.3 engine and the Harmony 2.4.2 DLL before it was classified, and all eight are fixed in
the commit `fix(mission-perf): v2.0.32 - convergence follow-ups for plan 028` (the commit that adds this
section). The two code and test changes (R3-6, R3-8) were written test first.

| # | Sev | Finding | Verdict | Fix | Proof |
|---|---|---|---|---|---|
| R3-1 | LOW | Fix pass 3 wrote that `Mission.OnPreTick` is shielded only while the profiler is installed (policy comment, feature doc, registry, `harmony-il.md`, a binding-test comment, RCA R18) | Confirmed | Each text says TAOM patches it only through Patch97 and any patch on it attaches the shield. One sentence added where the hazard is stated: a swallowed foreign prefix throw on `OnPreTick` skips the whole body, whose first call is `WaitTickCompletion`, so that frame's `OnTick` can run while the previous agent tick still runs (consequence UNVERIFIED), older than the profiler, owned by the PatchShield follow-up plan (L11) | `PatchShield.Install` shields every method `Harmony.GetAllPatchedMethods()` returns that is not TAOM-declared, excluded or a SaveShield target (`PatchShield.cs:174-217`), and `Mission` is declared in `TaleWorlds.MountAndBlade`; the feature doc already said a foreign patch on it gets the finalizer from a first game. `OnPreTick` calls `WaitTickCompletion` first (`Mission.cs:3548`) |
| R3-2 | LOW | The feature doc said the finalizer "swallows the throw and strips the patch", and the policy comment and the doc called a throw before the clear "safe" | Confirmed; one part of the finding narrowed | The strip is described as every unprotected owner's prefixes, postfixes and transpilers on the method. A throw before the clear "does not leave the flag false". Added: a throw no patch made (a behaviour's `OnPreDisplayMissionTick`) that a finalizer swallows skips the rest of `OnTick`, the agent tick launch included, on every frame it recurs | `TryUnpatchOffendingPatches` unpatches prefixes, postfixes and transpilers for every owner that is not protected (`PatchShield.cs:385-408`). The pre-display loop (`Mission.cs:3748-3751`) runs before `tickCompleted = false` (`:3756`). Narrowed: the finding says the swallow is logged on every occurrence. That holds for PatchShield's diag.log line (`PatchShield.cs:316`, not deduplicated) and not for Patch37's capture, which logs a duplicate only where `CrashBundleThrottle.IsLoggedOccurrence` is true (occurrences 1, 2, 10, 100 and so on, in `CrashReportService.HandleException`), so the texts say that |
| R3-3 | LOW | The narrowed per-frame rule in `harmony-il.md` ("excluded only where a swallow at it would skip the code that completes the frame's work") read as if `Mission.OnTick` should be excluded | Confirmed | One sentence: `Mission.OnTick` meets the test but keeps the shield, because a finalizer above it swallows the same throw; exclude only where the exclusion changes who catches it (the asynchronous agent tick) | Codex 1 above: an exception that escapes `OnTick` is swallowed by Patch37's finalizer on `Module.OnApplicationTick` with or without the shield; only the asynchronous agent tick reaches the native job thread instead |
| R3-4 | LOW | "Without running any transpiler" for PatchShield's strip, in five places | Confirmed | Each says the strip removes Patch97's transpiler, so nothing rewrites `OnTickSites` afterwards (`MissionTickProfilerHealth.cs`, the feature doc, `harmony-il.md`, the registry and Codex 2 above) | Harmony 2.4.2, decompiled from `0Harmony.dll`: `PatchProcessor.Unpatch(type, id)` ends in `PatchFunctions.UpdateWrapper`, which builds a `MethodCreator`, and `MethodCreator.CreateReplacement` adds every transpiler still registered to the `MethodCopier` (`AddTranspiler`). PatchShield makes three `Unpatch` calls per owner, so each reruns the transpilers that remain; Patch97's runs until its own owner is stripped, then never again |
| R3-5 | LOW | The feature doc's Key Files row said the health check runs "at each mission start and window" | Confirmed | "The check at each mission start, and the site count on every tick" | `MissionTickProfilerBehavior.OnCreated` calls `Problems()` and `OnMissionTick` calls `StopIfSitesLost` on every tick; nothing reads patch info per window |
| R3-6 | LOW | `HookHealth` named an unreadable patch info by the exception type alone and dropped the message | Confirmed | The fault is `Type: message`. The line builder's `Quote` already turns brackets and line breaks in it into text; a row added to the data-tag pin feeds it a message with tags and a line break | RED before the fix: `Missing_PatchInfoThrows_NamesTheHookWithTheExceptionTypeAndMessageAndDoesNotThrow` (renamed from `...AndDoesNotThrow`) and `OnCreated_PatchInfoCannotBeRead_DoesNotMeasureAndSaysWhichHooksItCouldNotCheck`, 2 of 39 failed; GREEN: 39 passed. The added data-tag row fails with `Quote` removed from `BuildHooksMissingLine` (restored byte for byte, sha256 compared) |
| R3-7 | LOW | Plan 028's maintenance note told plan 034 to keep "the three Patch97 entries" on `ExcludedTargetMethods` | Confirmed | Amended to D13: keep `Mission.TickAgentsAndTeamsImp` on the list and `Mission.OnTick` and `Mission.OnPreTick` off it, and the two binding tests that walk both directions. The plan's earlier sections still describe the original three-entry design; the maintenance note is the one a later plan reads | `ExcludedTargetMethods` holds one Mission entry, `Mission.TickAgentsAndTeamsImp` (`PatchShieldPolicy.cs`) |
| R3-8 | LOW | The stop warning's `t` survived a one-token mutation | Confirmed | `OnMissionTick_ASiteCountDropsMidMission_StopsMeasuringAtOnceAndWarnsOnce` moves `_missionStart` back 70 s by reflection after `OnCreated` and expects `t=+70s` | With `0` in place of `nowSeconds` at `MissionTickProfilerBehavior.cs:84`: the old test passed (`Passed: 1`) and the new one fails (`t=+0s` against the pattern `t=\+70s`). The file was restored byte for byte (sha256 `c49258eb...` before the mutation and after the restore, `git diff` empty) |

**Test names on 028 and 039.** Plan 039 pins the same D13 decision under different test names:
`AgentTickBracketTarget_IsOnPatchShieldsExclusionList` and `MissionTickTargets_StayUnderPatchShield`, against
this branch's `AgentTickTarget_IsOnPatchShieldsExclusionList` and
`TickAndPreTickTargets_AreNotOnPatchShieldsExclusionList`. The 039 names come from the Claude review of 039's
tip (a `git merge-tree` of the two branches, which shows four conflict hunks in
`MissionTickProfilerBindingTests.cs`), not from 039's code, which this branch does not have. Reconcile at the
merge: keep one set, and make every text that names a test (this record, 039's record, REVIEW-LOG) match the
kept names. Reconciled at the merge (integration trial 2): this branch's names are kept, plan 039's two Mission
tests are dropped, and `TickAndPreTickTargets_AreNotOnPatchShieldsExclusionList` carries plan 039's
`IsExcludedTargetNamespace` assertion.

### Suite totals, round 3

- Base before the round: not re-run (the one known failure is `EveryLanguage_DeclaresARowForEveryEnglishKey`).
- Focused classes (`HookHealthTests`, `MissionTickProfilerBehaviorTests`, `TickProfileLinesTests`,
  `MissionTickProfilerWiringTests`, `MissionTickProfilerBindingTests`, `MissionTickProfilerInstallerTests`):
  `Passed! - Failed: 0, Passed: 77, Skipped: 0, Total: 77`. `PatchShieldPolicyTests`:
  `Passed! - Failed: 0, Passed: 29, Skipped: 0, Total: 29`.
- Full suite at the final code: `Failed! - Failed: 1, Passed: 12492, Skipped: 2, Total: 12495`, the same single
  known failure. No test was added (one renamed, two changed, one row added to an existing test), so the totals
  equal fix pass 3's.
- `python tools/lint_docs.py --fail-on-drift --summary --dash-base 31c0fd15`: exit 0, `ai_dashes` 0
  (`context_budget` 10, outside this diff).

### FOLLOW-UP added by this round

- L11: a swallowed foreign prefix throw on `Mission.OnPreTick` skips the whole body, whose first call is
  `WaitTickCompletion`, so that frame's `OnTick` can run while the previous agent tick still runs
  (consequence UNVERIFIED; older than the profiler). The PatchShield follow-up plan takes it with L7.
- L12: the two D13 test-name sets on 028 and 039 (above) are reconciled at the merge.
