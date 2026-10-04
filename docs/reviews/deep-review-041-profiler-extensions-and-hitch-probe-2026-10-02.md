# Deep review: plan 041, hitch probe and profiler extensions (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: Hitch probe on by default (Patch98): whole-method prefix and finalizer pairs on five engine
         methods, spawn, script and clip-loading attribution, [HitchDetail], [SpawnProfile],
         [ScriptProfile], [AnimLoad], [TickSummaryExtra]
Date: 2026-10-02

Scope:   C# (Main/Features/MissionPerf, BattleLoadDiagnostics settings, the animation-loading
         adapter, CoopSettingsRelevance, TAOM.Dependencies PatchShieldPolicy, two SubModule comments),
         tests, docs. Branch perf/041-profiler-extensions-and-hitch-probe, diff 1639147d..56b2b327
         (the dispatch's full hash 56b2b32708a9... does not exist; its prefix resolves to HEAD
         56b2b32732150459b8e727fe48fafada71886882, which every lens and this lead reviewed).
Blast radius: UNCHECKED by the lead (graphify not run in this delegated step); the lenses traced the
         callers by hand: the new types are reached only from Patch97, Patch98, Patch91's bracket,
         MissionTickProfilerBehavior and the installer SubModule already called.
Waves:   lenses 1 (Standards), 2 (Engine), 3 (Efficiency), 4 (Completeness), 5 (Data flow),
         6 (Design) ran before this lead step; lens 7 (XML) NOT IN SCOPE (no XML in the diff).

STANDARDS:     FAIL: 0 critical, 0 high, 2 medium, 9 low
COMPATIBILITY: PASS: 0 incompatible, 13 verified, 4 unverified (native cost, native concurrency,
               TickComponents' thread, JIT inlining); 3 low findings and 1 nit
EFFICIENCY:    FAIL: 1 medium (cost UNVERIFIED), 1 low, 2 info, 1 follow-up
COMPLETENESS:  INCOMPLETE: issue not filed, installer tests passing through a swallowed assertion,
               plan-041 branches untested, 6 low
DATA FLOW:     FAIL: 24 flows traced, 3 gaps, 4 inconsistencies, 2 info, 1 unverified
DESIGN:        10 KEEP proposals (9 apply, 1 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE (no hook, validator or CI step changed)
```

## Details

Every finding below was re-read against the code in the worktree before it was classified. Lens
numbering: L1 Standards, L2 Engine, L3 Efficiency, L4 Completeness, L5 Data flow, L6 Design. The fixes
are in the commit `fix(mission-perf): v2.0.32 - review follow-ups for plan 041`.

### Confirmed and fixed

| # | Sev | Finding | Lenses | Fix | Proof |
|---|---|---|---|---|---|
| R1 | MED | `HitchProbeInstaller.Install`'s catch cleared `ProbeInstalled` but not `MissionTickProfilerHooks.Installed`, which only the skipped line 48 folds, so an exception before it left full-mode behaviour timing on with no frame boundary (`frames=0` windows, header `mode full`) and broke the invariant the measuring formula rests on | L1 2, L2 F4, L3, L4 M1, L5 F5, L6 | The catch also sets `Installed = false` | `HitchProbeInstallerTests.Install_ProbeInstallThrows_LeavesNeitherCategoryCountedAsInstalled` (RED: `Assert.IsFalse failed. Without Patch98 there is no frame boundary...`) |
| R2 | MED | Plan 028's `MissionTickProfilerInstallerTests` fake asserted the category was Patch97's; since plan 041 the installer also applies Patch98, the assertion threw inside `HitchProbeInstaller.Install`, its catch swallowed `AssertFailedException`, and the two "is installed" cases passed only because line 48 never ran. Both plan 028 classes also reset none of plan 041's statics (`HitchProbeHooks.Sampler`, `MissionAttributionHooks.ScriptTickCall`, `HitchProbeInstaller.ProfilerOffAtGameStart`) | L1 2, L4 M1 | The fake answers Patch98's category; both classes reset all four `ResetForTests`; `AllSitesSwapped` asserts no `LogError`; the "frame boundary is a prefix" message names Patch98 | With R1 fixed and the old fake, the two "is installed" cases would fail; with the new fake they pass and log no error |
| R3 | MED | Plan 028's not-timing reason lines stated a false consequence while the probe measured the mission (D6): `RestartNeededLine` ("no patches are installed and nothing is measured") on the default path when a player turns the profiler on mid-session, `NotInstalledLine` and `BuildMissionOffLine` likewise; `ProfilerNeedsRestart` sent "profiler off at game start, Patch98 failed" to `NotInstalledLine`, blaming an install that never ran; the `LeftVanilla` consequences said "no mission is measured", "waitTickMs reads 0" and "the profiler records nothing for it" although the probe measures, times the wait and keeps its totals | L1 3, L5 F1, L4 L1 | `HitchProbeLines.ProfilerNotTimingLine(restartNeeded, probeMeasuring)` and `BuildProfilerOffLine(mission, probeMeasuring)` keep plan 028's lines when nothing measures and say "the hitch probe still measures this mission" when it does; `ProfilerNeedsRestart => Profiler == null \|\| ProfilerOffAtGameStart`; `RestartNeededLine` says "its patches"; `LeftVanilla` names what the probe keeps | `MissionTickProfilerBehaviorProbeTests` (3, RED: the old restart line), `ProbeAwareStatusLines_MatchThePinnedLiterals`, `StatusLines_MatchTheirPinnedLiterals`, `ProfilerNeedsRestart_ProfilerOffAtGameStartAndPatch98Failed_IsTrue` (RED) |
| R4 | MED | The logic moved into `MissionTickProfilerHooks.Probe.cs` (`ConfigureProbeMission`, `OnMissionFirstTick`, `WriteSummaryExtra`, `[HitchDetail]`), the restart members and the default-on mission flow had no test; changing `_measuring` back to `_behaviourTiming` alone would have switched the probe off for every player with every test green. The commit's Not-tested trailer named only the behaviour, which plan 028's tests already drive unattached | L1 1, L4 M2, L5 F2 | `HitchProbeMissionFlowTests` (12, no category), `MissionTickProfilerBehaviorProbeTests` (3, RequiresGame), a pre-tick fault test, the installer fault test | Nine of the new cases were RED against the unfixed code (see R5 to R10) |
| R5 | LOW | The mission header said `script attribution on` when the `ScriptComponentBehavior.OnTick` delegate did not bind: `ScriptExpected` drops to 4, the four `TWParallel.For` swaps satisfy it, and no `TimedScriptTick` swap exists | L1 4, L6 D2 | `ScriptAttribution` also needs `MissionAttributionHooks.ScriptTickCall != null` | `ConfigureProbeMission_ScriptDelegateUnbound_TimesTheBlocksButAttributesNoComponent` (RED) |
| R6 | LOW | One attribution-helper fault latches every helper off, but the line said only "`<part>` hook failed, its timing is off", and later missions kept `SpawnAttribution`, `ScriptAttribution` and `ScriptBlockTiming` on, so their headers said `on` and the block columns read `0.00` instead of `na` | L1 5, L5 F4 | `HitchProbeLines.BuildAttributionFault` names all per-type attribution; the latch is `MissionAttributionHooks.Off`, and `ConfigureProbeMission` folds it into the three flags | `ConfigureProbeMission_AfterAnAttributionFault_AttributesAndTimesNothingByType` (RED), the new pin |
| R7 | LOW | A pre-tick bracket fault stopped sampling (the sample is taken inside it) but left `profiler.AnimSampling` on and the sampler enabled, so `animLoading=0` and `[AnimLoad] loadingFrames=0` were false negatives for the rest of the process | L5 F4 | The pre-tick catches drop the sampler and the flag; the part reads `pre-tick (with the anim-loading sample, which samples there)` | `PreTickFault_TurnsTheClipSampleOff_SoLaterMissionsReadNaNotZero` (RED) |
| R8 | LOW | `[TickSummaryExtra] frames=0` followed the line saying the mission has no summary | L5 F6 | `WriteSummaryExtra` writes only when a frame closed | `WriteSummaryExtra_NoFrameClosed_WritesNothing` (RED) |
| R9 | LOW | Patch98 installed, the profiler not, both toggles switched off in MCM: missions stopped measuring with no reason line (D6) | L5 F7, L4 L1 | `BuildProbeMissionOffLine` at the first tick in exactly that case | `OnMissionFirstTick_BothTogglesOffWithTheProbeInstalled_SaysWhyItIsNotMeasuring` (RED), `..._BothTogglesOffNothingInstalled_WritesNothing` |
| R10 | LOW | `ProbeNotInstalledLine` could not tell "off at game start, restart" from "install failed" | L5 F3 | The line names both causes | `OnMissionFirstTick_ProbeOnButNotInstalled_WarnsWithBothPossibleCauses` (RED) |
| R11 | LOW | The two hints, the allowlist reason and mcm.md said a change needs a restart (turning either toggle off applies from the next mission, `MissionTickProfilerBehavior.OnCreated` re-reads both); the probe's hint said "every battle frame" (every mission is measured) and "a few microseconds per frame" (the native call's real cost is UNVERIFIED). A repeat of review 028's R11 | L1 10, L5 F3, L4 L2 | Both directions stated; "every mission frame"; "Its measured cost is written to the TAOM debug log" | `SettingRequireRestartPostureTests` green; read back |
| R12 | LOW | "No per-call shield finalizer sits inside the measurement" (feature doc, registry, `HitchProbeBindingTests` summary, commit body): `Mission.SpawnAgent` calls `Agent.EquipItemsFromSpawnEquipment`, which Patch23 and Patch43 patch and PatchShield still shields. The policy comment still said "OnPreTick carries Patch97 only" and did not name Patch97's attribution transpilers. A repeat of review 028's R3 | L1 6, L6 D9 | All four texts say the five methods themselves carry no shield finalizer and a shielded callee keeps its own; the comment names every consumer and the wait's correctness reason. `56b2b327` is not amended; the fix commit body carries the corrected sentence | Read back against `Agent_EquipItemsFromSpawnEquipment_Patch.cs:8-9` and `PatchShieldPolicy.cs` |
| R13 | LOW | Doc drift: "a failed Patch98 leaves Patch97 uninstalled" (its transpilers stay, inert); the registry's "a site not found exactly once"; "run once per frame on the main thread" for `TickComponents`; "0.43 us" presented as the probe's cost while the benchmark stubs the native call | L2 F6, L4 L4, L1 follow-up, L3 E2 | Reworded; the cost paragraph separates the managed brackets from the native call, says its median is warm-cache and its in-place cost and concurrency are UNVERIFIED | `lint_docs.py` below |
| R14 | LOW | The by-name `ScriptComponentBehavior.OnTick` lookup had no reflection-site row, and the `WaitTickCompletion` row cited `:29,52` (now `:31,54`) | L2 F3, L4 L6 | Row added to `ReflectionSiteBindingTests` and `reflection-sites.md`; locations corrected | Binding gate 411 to 412, both rows `Passed` |
| R15 | LOW | The benchmark's unpatched arm ran with the measuring profiler installed, so the agent-tick pair's measuring work (new for every default player: before plan 041 the profiler was null) cancelled out of the figure | L3 E2 | The unpatched arm runs with no profiler | `TAOM_RUN_BENCHMARKS=1`: 0.477 and 0.470 us per simulated frame (was 0.425 and 0.429), under the 50 us target |
| R16 | LOW | `SpawnBracket_Nested_TimesOnlyTheOutermost_AndCountsBoth` bounded two `Thread.Sleep(10)` at a fixed 40 ms on the hosted runner; at a 15.6 ms timer tick they take about 31 ms | L4 L5 | Bounded by the test's own stopwatch around the outer call, which a double count would exceed | Filtered run green |
| R17 | LOW | A probe-mode pre-tick, on-tick or wait fault moves that time into another column, and nothing said so | L5 F4 | The feature doc's fault row says where each part's time lands | Read back |
| R18 | LOW | Patch98 stays correct only while the `WaitTickCompletion` class applies before the `OnPreTick` class (Harmony stops inlining a method only when it detours it); nothing pinned the order | L2 F5 | One comment in `Patch98_HitchProbe.cs`; `Patch98_WaitTickCompletionClass_IsDeclaredBeforeTheOnPreTickClass` | Filtered run green |
| R19 | LOW | `ProbeTotals.cs` held no type of that name and also held the unrelated `ProbeDelegates` binder | L1 7 | `ProbeLineData.cs` (the three line-data classes) and `ProbeDelegates.cs` | Build; `OpenDelegateDispatchTests` green |

### False positives and accepted designs

| Finding | Lenses | Verdict |
|---|---|---|
| `AnimationLoadingAdapter` is built with `new`, not resolved from IoC | L1 8 | Accepted, as L1 recommends: the hooks are static and outside the container like the profiler's logger, the adapter is stateless, `HitchProbeInstaller.cs:13-15` says why, and registering it would add plumbing through the single-owner `SubModule.cs` |
| `MissionTickProfilerInstaller` passes the profiler toggle, not its install result, so a failed Patch97 with the probe off still installs Patch98 | L5 F5 | Not a defect: every mission then reports the failed install correctly (`NotInstalledLine`, traced), the brackets return on their first null or measuring check, and the installed Patch98 lets the probe toggle turn on in session |
| The frame during which sampling switches on counts as a sampled frame | L5 F4 | Not a defect: `[AnimLoad] frames=` is every window frame by contract and no line reports a sampled-frame count; that one frame reads not-loading, as every frame before sampling turned on does |

### Orchestrator focus

1. **Default-on cost and safety.** The benchmark covers the real Patch98 prefix and finalizer pairs
   attached by Harmony (with the finalizer's try/catch wrapper), `CloseFrame` with the allocation and
   three GC reads, the sampler's managed path, the `[ThreadStatic]` script bracket, the spawn depth
   counter and, after R15, the agent-tick pair's new measuring work: 0.477 and 0.470 us per frame. It
   does not cover the native clip-loading call (a stub; the runtime gate allows a median up to 20 us,
   measured warm from 32 back-to-back calls), more than one `TickComponents` call per frame, hitch
   frames, or cold caches. The open-delegate calls are full mode only, so they are not part of the
   default cost. No hook can throw into the engine: every statement before a `try` is a static,
   volatile or `[ThreadStatic]` read or a bool write, every body is in a try/catch whose fault logger
   swallows its own errors, and the finalizers are `void`. The one crash path left is native (below).
   Per-frame INFO lines: only `[Hitch]` and `[HitchDetail]`, for the first 100 slow frames of a
   mission; every other line is per process, per mission or per 5 s window.
2. **PatchShield exclusions (FOR-MIKE 16a, list unchanged).** `WaitTickCompletion`: keep; once per
   frame, only Patch98 patches it, and a swallow would skip the wait loop so `OnPreMissionTick` overlaps
   the running agent tick, which makes the exclusion a correctness choice as much as a cost one.
   `TickComponents`: neutral; once per ticking scene per frame on native's thread, no other in-tree patch
   on the method, the shield's tax small either way. `SpawnAgent`: the weakest case; per spawned agent,
   about 84 us and 316 KB per 1,313-agent wave before plan 034 and about 2 us after (plan 034's figures,
   not measured here), against giving up the missing-API swallow and strip for Patch23 and any other
   mod's patch for every player, the toggles off included. A swallow there returns a null `Agent` to
   vanilla callers, so the rescue's value is itself limited. Decision for Mike.
3. **The executor's deviations.** The measuring formula equals the plan's on every path now that R1
   restores `Installed` implies `ProbeInstalled`. `[HitchDetail]` gated on `ProbeInstalled` was always
   true in production and existed only to keep plan 028's single-line assertions; removed (D1).
   `ScriptBlockTiming`: right, the block totals are `Interlocked` and valid from any thread (R6 adds the
   fault fold). The restart members: right intent, wrong branch and wrong line (R3). The split try/catch:
   correct, `applied` stays false until the apply returns. The `Probe.cs` partial: no behaviour change,
   but its logic had no test (R4). Plan 028's tests: R2.
4. **TickComponents' thread.** Safe on any thread: `[ThreadStatic]` open flag and stamp, `Interlocked`
   totals, volatile `_scriptOff` and `ScriptOffMain`, a compare-and-swap once-guard on the thread line,
   per-type tables touched only after a per-call main-thread check, and a locked `FileLogger`. Caveat
   (full mode only): `ScriptOffMain` is decided by the first measured call from any scene. The real
   thread is UNVERIFIED until a player log shows the line.
5. **D6.** Header, periodic lines, summary and one reason line per disable, skip or fallback, now
   including R3, R6, R7, R8, R9 and R10. `[HitchDetail]` is built only inside `CloseFrame`'s cap branch,
   so it follows `[Hitch]`'s 100 per mission.
6. **Known failures.** Matched; see the suite totals.

### UNVERIFIED (no lens could close these)

- The per-frame cost of `MBAnimation.IsAnyAnimationLoadingFromDisk` in place (L2 F1, L3 E1): it walks
  the whole clip table (at least the 6,177 vanilla clips, about 395 KB of records) with no lock, once per
  frame, between two frames of other work, while the previous frame's agent-tick workers may update the
  same records; the 20 us gate is decided on a warm median.
- Its native safety (L2 F2, L3 E4, L5, L6): no writer of the clip vector was found by xref, but a writer
  through a stored pointer was not ruled out; a native access violation is not catchable by `Sample()`.
- Whether the JIT would inline `WaitTickCompletion` (the runtime self-check reports it).

## Action items

1. Mike: FOR-MIKE 16a, the three exclusions, per the table in focus 2.
2. Mike: before the probe ships on by default, `/research` on the clip vector's writers and an in-place
   timing of the sample (E1 below, behaviour-changing).
3. File the plan 041 issue (the draft is in the run's `issue-drafts.md`); filing needs Mike's word.
4. The convergence pass (deep-review Step 4.6) on the fix diff is owed: this lead cannot spawn a
   reviewer. The lead read the fix diff for parity and found nothing further.
5. After merge: `/verify-bindings` (patch-targets.md lists neither Patch97 nor Patch98).

## Improvements (Step 4)

APPLIED (behaviour-preserving, tests green before and after):
- `MissionTickProfilerHooks.Probe.cs` `LogHitch`: D1, the `ProbeInstalled` guard on `[HitchDetail]`
  removed (only Patch98's prefix closes frames); plan 028's three hitch tests select the `[Hitch]` line
  by tag. Proof: `OnFrameBoundary_Hitch_WritesHitchDetailRightAfterTheHitchWithTheSameT`.
- `HitchProbeLines.BuildSpawnProfile`, `BuildScriptProfile`, `ProbeWindowWriter.WriteExtras`: D3, one
  attribution rule (the window's empty top) instead of two. Proof: the pinned literals and the three
  `WriteExtras` cases.
- `ProbeWindowWriter.WriteMissionHeader`, `WriteMissionEnd`: D4, deleted; the two callers write the line.
  Proof: their real callers are now tested (`HitchProbeMissionFlowTests`).
- `HitchProbeLines`: D5, its copies of `Num`, `Seconds`, `Int`, `Quote` and `CountOrNa` deleted in favour
  of `TickProfileLines`' (now internal). Proof: every pin in `HitchProbeLinesTests` (the comma-culture
  case included) and `TickProfileLinesTests`.
- `CallSwap.Helper`: D6, deleted (one source of truth, `Helpers`). Proof: `TickProfilerTranspilerTests`,
  `TickProfilerTranspilerOccurrenceTests`, `HitchProbeBindingTests`.
- `MissionTickProfiler.TakeMissionExtras` renamed `SummarizeExtras` (D7) and `BehaviourTiming` spelled
  `BehaviorTiming` (D8). Proof: the build and the MissionPerf tests.
- `PatchShieldPolicy.cs` comment: D9 (with R12).

NOT APPLIED:
- L3 E1, `AnimLoadingSampler`: decide the sampling gate on frame-spaced in-place costs and log them.
  Behaviour-changing (sampling may turn off where it now turns on); for Mike, with the native safety
  question.
- L3 E2 text part: drop "target 0.50%" from the `probe install` line or say what it excludes. A pinned
  literal change; the line already says "bookkeeping", and the docs now state what the figure excludes.
- L2 F7: `new Type[0]` on the `WaitTickCompletion` attribute. A nit with no overload today; the binding
  gate fails loudly on an ambiguous match after an engine bump.

FOLLOW-UP (pre-existing code or conditional; no issue filed, issue filing is the orchestrator's on Mike's word):
- L6 D10: once a player log shows the wait bracket runs, delete plan 028's wait swap and `WaitSwapActive`
  (about 60 lines), leaving one wait recorder.
- L2: `_measuring` has one closer, `OnEndMission`; a teardown through `OnMissionStateFinalized` leaves it
  on until the next mission (plan 028's design, now on by default; reachability UNVERIFIED).
- L5: Patch23's postfix keys on `Agent.Index` (`Mission_SpawnAgent_Patch.cs:77`), which the trap index
  forbids.
- L5, L3: `docs/reference/engine/mission-frame-threads-and-native-costs.md` credits plan 028 with the
  per-frame clip sample (:162) and says the walk covers on-demand records only (:140); it covers the
  whole clip table.
- L5: `.claude/rules/harmony-patches.md` has no thread row for `ManagedScriptHolder.TickComponents`;
  add one from the first player log's thread line.
- L1: `MissionTickProfilerBehavior.cs:128` reads `BattleLoadDiagnosticsSettings.Instance` directly
  (plan 028).
- L1 9 (for Mike): ADR-002 says an entry-point class stays under 150 lines; `HitchProbeHooks` is 284
  lines over three partial files and `MissionTickProfilerHooks` 251 over two. Whether a partial
  split satisfies the ADR is Mike's call; three files sit at 148 or 149 lines.

## Codex review

Codex not run: no paid dispatch was authorised for this item.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | | | | Codex not run |

## AGENTS.md lessons (pending)

For the orchestrator to consolidate into `.ai/review-reference.md` "Look harder here" at wrap-up:
- When a second measurer joins a feature, every "not measuring" reason line of the first must be
  re-read: it may now be false whenever the second one measures.
- A test fake that asserts inside a call production wraps in a catch passes on the error path; check the
  fake's assertion against every caller of the delegate it stands in for.
- A cost figure from a benchmark names what the benchmark stubbed, and its baseline arm runs in the
  state players had before the change.

## Suite totals

- Base `56b2b327` before any edit: `Failed! - Failed: 1, Passed: 12536, Skipped: 3, Total: 12540`
  (EveryLanguage_DeclaresARowForEveryEnglishKey).
- RED run of the new and edited tests against the unfixed code: `Failed! - Failed: 13, Passed: 65,
  Skipped: 0, Total: 78`, every failure one of the cases named above.
- After the fixes, full suite: `Failed! - Failed: 1, Passed: 12554, Skipped: 3, Total: 12558`, the same
  single known failure (EveryLanguage_DeclaresARowForEveryEnglishKey).
- `python tools/lint_docs.py --fail-on-drift --summary --dash-base 1639147d`: exit 0, ai_dashes 0
  (context_budget 10, outside this diff).
- Binding gate (`TestCategory=BindingVerification`, installed v1.5.3): `Passed! - Failed: 0, Passed: 412,
  Skipped: 0, Total: 412` (411 at the base; the new reflection-site row).
- Reference-assembly unit step (CI's filter, `BANNERLORD_*` unset): `Failed! - Failed: 3, Passed: 10205,
  Skipped: 29, Total: 10237`, exactly the three known failures (EveryLanguage_DeclaresARowForEveryEnglishKey,
  Patch93_HasTheSevenPatchesInItsCategory, Patch94_HasTheMapIconNoParleyAndNoJoinPatches); the six touched
  no-game classes alone: `Passed! - Failed: 0, Passed: 56, Skipped: 0, Total: 56`.
- Benchmark (`TAOM_RUN_BENCHMARKS=1`): 0.477 and 0.470 us per simulated frame, both passed.

VERDICT: READY FOR COMMIT

## Convergence round 1

Reviewed range `56b2b327..0d030997` (the review follow-ups). Three LOW findings, each re-checked
against the code before the fix; all three fixed in the commit
`fix(mission-perf): v2.0.32 - convergence fixes for plan 041`.

1. **Fixed.** The "Enable Hitch Probe" hint and `docs/features/mcm.md` said turning the probe off stops
   measuring from the next mission. Confirmed false while "Enable Tick Profiler" is on:
   `MissionTickProfilerBehavior.cs:61-62` sets `_measuring = _behaviorTiming || (ProbeInstalled &&
   _probeOn)`, every Patch98 bracket gates only on `profiler.Measuring`, and
   `MissionTickProfilerHooks.Probe.cs:40` keeps `AnimSampling` on whenever the mission measures. The
   hint now says the probe toggle stops measuring while the profiler is off, mcm.md says the
   profiler's full mode keeps every probe bracket, and the restart allowlist reason in
   `SettingRequireRestartPostureTests` carries the same condition. The same stale claim sat in the
   profiler toggle's summary in `IBattleLoadDiagnosticsSettingsProvider.cs` ("turning it off stops
   measuring"); it now says the profiler toggle stops behaviour timing while the probe, when
   installed and on, still measures.
2. **Fixed.** The `HitchProbeInstaller.ProfilerOffAtGameStart` summary described the pre-R3 meaning
   (Patch98 installed for the probe alone). Confirmed: the flag is set before the Patch98 apply and
   `ProfilerNeedsRestart` no longer reads `ProbeInstalled`. The summary now says the profiler was off
   at game start so its install never ran, whether or not Patch98 then applied.
3. **Fixed.** Three test names in `MissionTickProfilerProbeTests` still named the pre-rename members.
   Renamed to `SummarizeExtras_SumsEveryClosedFrame_AndCountsHitchesWithAnimLoading`,
   `SummarizeExtras_TopsComeFromTheSpawnAndScriptTables` and
   `BeginMission_WithoutBehaviorTiming_DefaultsToFullMode`; `git grep -e TakeMissionExtras -e
   BehaviourTiming` outside `plans/` and `docs/reviews/` now returns nothing.

No runtime behaviour changed (comments, a hint string, a doc paragraph, an allowlist reason and test
names), so no test could fail first; the renamed tests and the posture tests ran by name:
`Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5`.

Suite totals for this round:

- Before any edit (`0d030997`): `Failed! - Failed: 1, Passed: 12554, Skipped: 3, Total: 12558`
  (EveryLanguage_DeclaresARowForEveryEnglishKey, known at the base).
- After the fixes: `Failed! - Failed: 1, Passed: 12554, Skipped: 3, Total: 12558`, the same single
  known failure.
- `python tools/lint_docs.py --fail-on-drift --summary --dash-base 1639147d`: exit 0, ai_dashes 0.
- No gate (hook, validator or CI step) changed, so no differential sweep.

## Convergence round 2

The convergence reviewer read `0d030997..2ab27cdd` and raised one LOW finding. The review workflow's
last round runs no fix pass, so the orchestrator checked it and closed it in the commit
`docs(mission-perf): v2.0.32 - record plan 041's convergence rounds`.

| # | Severity | Finding | Verdict | Resolution |
|---|---|---|---|---|
| R2-1 | LOW | Round 1's item 2 rewrote the `HitchProbeInstaller.ProfilerOffAtGameStart` summary to "the tick profiler was off at game start", which overstates: with both toggles off, `Install` returns before it sets the flag, so the flag is true only when the hitch probe was on and the profiler off | Confirmed: `HitchProbeInstaller.cs` returns at `if (!probeOn && !profilerRequested)` before `ProfilerOffAtGameStart = !profilerRequested;`. The code is right; `ProfilerNeedsRestart` covers the both-off case through `Profiler == null` | The summary now names the probe-on, profiler-off condition, says the flag stays false with both toggles off, and points at `ProfilerNeedsRestart`'s other check. Comment only; build clean, full suite at the records commit `Failed! - Failed: 1, Passed: 12554, Skipped: 3, Total: 12558` (the known EveryLanguage failure) |

No runtime line changed in round 2. The RCA gains a "Convergence rounds" section covering both rounds.

## Fix pass after the reviews of `948fe42a` (2026-10-03)

Two independent reviews read the D13 commit: a Claude lens review of `d7208235..948fe42a` (seven findings,
C1 to C7 below) and a Codex adversarial review of the whole plan (two findings and two observations, X1 to
X4). Each was re-read against the code, the installed v1.5.3 decompile and Harmony's source (2.4.2 and 2.3.3)
before any change. Rulings from the orchestrator: the `Mission.OnTick` completion hazard is pre-existing and
is not fixed here, the docs only say honestly that the re-shield does not prevent it; the native record
lifetime and the cost gate are documented, not coded.

| # | Verdict | Proof | Resolution |
|---|---|---|---|
| C1 (guard checks "some prefix outstanding on this thread", not this call's) and X1 (Codex P2, the same defect from the nested-spawn side) | CONFIRMED | `HitchProbePrefixGuardTests` against the unmodified code: the nested-strip test failed (`Expected:<1>. Actual:<0>. The nested call has no prefix: its finalizer is the one that warns, at once`: the lone nested finalizer took the outer call's count), the rerun test failed (`Expected:<0>. Actual:<1>`: Harmony 2.4.2 `MethodCreator` sets `finalized` only after the normal-path finalizers, so a later finalizer that throws reruns all of them, and the second run read as a missing prefix) | Each call carries its own `ProbeState` in Harmony's `__state` (default 0 per call, set by the prefix first, closed by the finalizer); the five counts, the thread-statics for them and `ConsumePrefix` are gone |
| C2 (later lone exits dropped uncounted; "reads 0" stated as permanent) | CONFIRMED | `ConsumePrefix` set a latch and incremented nothing; decision D6 says aggregate, never drop | Every lone exit is counted per bracket (`Interlocked`); each measured mission's end writes one WARNING with the counts, also when no frame closed; the five lines now read "that call is not measured, and while the prefix stays gone ..." |
| C3 (no test pins the per-thread counts) | CONFIRMED | On the unmodified code, deleting `[ThreadStatic]` from `_spawnEntered` left all 20 `HitchProbeHooksTests` passing | The counts no longer exist; new tests: a lone exit on one thread beside an open call on another (spawn and script, asserting the warning at once and the open call's count and time), two overlapping script threads (pins the remaining `[ThreadStatic]` open flag and start stamp: removing either fails it) |
| C4 (docs understate the rescue; kept exclusions leave a swallow one level up open) | CONFIRMED, all three parts | `PatchShield.ShouldSwallow` classifies by exception type only; v1.5.3 `Mission.cs`: `OnTick` calls `OnPreDisplayMissionTick` (:3750) and `OnMissionTick` (:3759), with `tickCompleted = false` at :3756 between them; `OnPreTick` calls `WaitTickCompletion` (its only caller, :3548) then `OnPreMissionTick`, `SpawnAgent` calls `OnAgentBuild` (:4383, :4397), `TickAgentsAndTeamsImp` runs synchronously in `OnTick` (:3790) and `MissionState` passes `asyncAITick: false` on fast-forward (:178, :189, :196); Patch23 is a prefix and postfix on `SpawnAgent`, Patch97 a transpiler, owner `com.taom.mod` | Docs only, per the ruling: the feature doc, three registry rows, the `PatchShieldPolicy` comment, the plan 028 record and the lesson now name the trigger, list what a `SpawnAgent` rescue strips, and say the re-shield does not prevent the `tickCompleted` hang |
| C5 ("skipped by another patch" is not how Harmony behaves) | CONFIRMED | Harmony 2.4.2 `MethodCreatorTools.AffectsOriginal` and 2.3.3 `MethodPatcher.PrefixAffectsOriginal` return false for `__state` and for a void prefix with no parameter; real Harmony: a prefix before ours that returns false does not skip ours, one that throws does (`PrefixBeforeOursThatReturnsFalse_...`, `PrefixBeforeOursThatThrows_...`) | The line, the comments and the docs say "it was removed, or an earlier prefix threw"; the literal pins changed first (`Failed: 2, Passed: 17, Total: 19`) |
| C6 (the 5 ns figure needs plan 034, which is not in this branch) | CONFIRMED | `git merge-base --is-ancestor 31f59582 HEAD` exits 1; HEAD's `ShieldFinalizerVoid` and `ShieldFinalizerWithResult` take `MethodBase __originalMethod`, plan 034's branch takes only `Exception`; plan 034's figures: 64 ns and 241 bytes per call as shipped, 1,146 ns with 8 threads, about 5.4 ns without the binding | The figure is conditional on plan 034's change in the `PatchShieldPolicy` comment, three test comments, the feature doc, the registry, the lesson and the plan 028 record. The `~50 us` text at `PatchShieldPolicy.cs:67-69` is plan 034's to fix |
| C7 (older lessons' per-frame Prevent never retired) | CONFIRMED | `harmony-il.md` Prevent lines of 2026-09-26 and 2026-09-28 still send per-frame targets to the list; no file under `.claude/`, `.agents/` or `docs/reference/engine/` mentions `ExcludedTargetMethods`, so no rule carries it | A dated "Later change" bullet under both lessons and a replacing sentence in the 2026-10-03 lesson |
| X2 (D13's "cost only" overlooks the completion invariant; P2 observation) | CONFIRMED as a hazard by reading, PRE-EXISTING | `tickCompleted` is cleared at `Mission.cs:3756` and set only at :3629; a swallow in `OnTick` between them leaves the next wait spinning, and `TickMissionAux` (Patch91) swallowed the same exception one level higher from a process's second game start | Not fixed here (ruling). Honest wording in the docs above; the remedy (culprit-only strip, a reproduction) belongs to the PatchShield follow-up plan |
| X3 (native record lifetime unverified; P3) | UNVERIFIED, no defect proven | Committed evidence: the walk takes no lock, returns at the first loading record, the list is a member of a larger engine object whose fill path was not traced; eviction frees a clip's data and keeps its record | Documented precisely in the feature doc's Cost section; no code change |
| X4 (cost gate is a one-time eligibility check; P3) | CONFIRMED as implemented | `AnimLoadingSampler.MeasureCost` runs once per sampler (once per process), `Sample` is not timed, only an exception turns sampling off | Documented precisely (including that the 0.5% figure covers the managed brackets only); no code change |

Not changed: the Codex report's remarks that the script bracket's open flag does not support a call nested
inside another on one thread, that the thread line looks at the first measured call only, and that duration
and count are exchanged separately at frame close are limitations under native schedules nobody has
observed, not findings; the `OnTick` hazard above; the `~50 us` comment in `PatchShieldPolicy`.

TDD evidence, in order. Real-Harmony tests against the unmodified code: `Failed: 2, Passed: 2, Total: 4`
(C1 and X1 as quoted). Literal pins changed first: `Failed: 2, Passed: 17, Total: 19`. New signatures with
the old counting logic: `Failed: 9, Passed: 217, Skipped: 1, Total: 227` (the nine new-behaviour tests, and
nothing else). After the guard, every `MissionPerf` test passes or skips.

Mutation checks on the final code (one edit each, run against the five guard test classes, each file
restored byte for byte, hashes compared): closing the call (`Closed` to `Entered`) fails the nested-rerun
test; inverting the lone-exit branch fails 15 tests; `Increment` to `Decrement` fails 5; the once-per-process
latch inverted fails 4; the take that does not zero fails 2; the enter that sets `Closed` fails 3; the
mission-end `if (missing != null)` inverted fails 2; the count line's `<= 0` to `< 0` fails its pin; the
finalizer taking the state by value fails the wiring pin and the nested-rerun test; each of the two script
`[ThreadStatic]`s removed fails the overlapping-thread test; the spawn exit ignoring the guard fails the
nested-strip test, the nested-rerun test and the direct nested lone-exit test (the nested-strip test first
passed this mutation with its fixed 18 ms lower bound, so it now compares the outer call's recorded time
with the moment the nested call ended).

Suite totals: base `948fe42a` `Failed: 1, Passed: 12564, Skipped: 3, Total: 12568` (EveryLanguage_DeclaresARowForEveryEnglishKey, the known
failure); after the fixes `Failed: 1, Passed: 12580, Skipped: 3, Total: 12584`, the same single failure,
16 new tests. `TestCategory=BindingVerification` on the installed v1.5.3: `Passed: 412, Total: 412`.
`python tools/lint_docs.py --fail-on-drift --summary --dash-base 1639147d`: exit 0, ai_dashes 0. Benchmark
(`TAOM_RUN_BENCHMARKS=1`, three runs): 0.492, 0.496 and 0.506 us per simulated frame.
