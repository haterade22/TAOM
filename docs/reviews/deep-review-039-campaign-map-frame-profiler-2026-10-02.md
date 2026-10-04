# Deep review: plan 039, campaign map frame profiler (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: Campaign map frame profiler (plan 039, Patch101, off by default): map frame phases,
         TAOM's per-frame map code and the CampaignEvents.Tick listeners, PatchShield option A
Date: 2026-10-02

Scope:   C# (Main/Features/MapPerf, BattleLoadDiagnostics settings and provider, CoopSettingsRelevance,
         two SubModule insertions, TAOM.Dependencies PatchShieldPolicy), tests, docs.
         Branch perf/039-campaign-map-frame-profiler, diff c4ee9373..a3c219d4 (one commit).
Blast radius: graphify affected run by the lead on MapSessionHooks, MapFrameProfilerInstaller and
         MapProfileLines after the fixes: every caller is in Main/Features/MapPerf, its tests, or the two
         SubModule lines, whose signatures did not change. MapFrameProfiler.EndSession(int), removed by
         P4, had no caller outside MapSessionHooks and its own tests (grep).
Waves:   lenses 1 (Standards), 2 (Engine), 3 (Efficiency), 4 (Completeness), 5 (Data flow),
         6 (Design) ran before this lead step; lens 7 (XML) NOT IN SCOPE (no XML in the diff).

STANDARDS:     FAIL: 0 critical, 0 high, 1 medium, 5 low, 1 nit
COMPATIBILITY: PASS: 13 verified, 0 incompatible, 4 unverified; 2 medium, 9 low (text, gate, logging)
EFFICIENCY:    PASS on the hot paths: 0 high, 0 medium, 3 low (1 fault path, 2 documentation)
COMPLETENESS:  INCOMPLETE: issue not filed (held by DECISIONS D4), the Invoke IL unpinned, the production
               patched check untested, the option A text wrong in places
DATA FLOW:     PASS on code: 31 flows traced, 0 gaps, 9 inconsistencies (2 medium, 7 low)
DESIGN:        7 KEEP proposals (5 apply, 2 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE (no hook, validator or CI step changed)
```

## Details

Every finding was re-read against the worktree, the installed v1.5.3 decompile (taom-src cache) or the
shipped Lib.Harmony 2.4.2 decompile before it was classified. Lens numbering: L1 Standards, L2 Engine,
L3 Efficiency, L4 Completeness, L5 Data flow, L6 Design.

**The finalizer order the executor left UNVERIFIED is verified.** Patch37's finalizers on
`Module.OnApplicationTick` and `ScreenManager.Tick` carry `[HarmonyPriority(800)]`
(`Patch37_CrashReport.cs:51`, `:60`); PatchShield attaches `new HarmonyMethod(finalizer)` with no priority
(`PatchShield.cs:224-225`), which the `Patch` constructor files as 400 (`priority == -1 ? 400 : priority`);
`PatchInfoSerialization.PriorityComparer` returns `-priority.CompareTo(other)`, so the higher priority sorts
first; `MethodCreator.AddFinalizers` emits them in that order and stores each value-returning finalizer's
result into the one shared exception local. Patch37 runs first. Four lenses reached the same result
independently; the lead re-read the comparer and `AddFinalizers` in the Harmony decompile.

### Confirmed and fixed

| # | Sev | Finding | Lenses | Fix | Proof |
|---|---|---|---|---|---|
| R1 | MED | The option A text (feature doc, policy comment, registry, commit body) described only the crash-capture-on path and called the finalizer order UNVERIFIED. With capture off, on re-entry or with the service unresolved, `HandleAndSwallow` hands the exception back (`CrashReportPatchHelper.cs:36-59`) and PatchShield's own finalizer on `Module.OnApplicationTick` or `ScreenManager.Tick` swallows it, logs one line per throw and strips that outer method's non-protected patches (TAOM's `CrashReportApplicationTickTrigger` among them), never the culprit | L1 F1, L2 LOW-1, L3 3, L4 F2, L5 23, L6 | Verified order and both capture branches written into the feature doc, the `PatchShieldPolicy` comment and the four registry paragraphs | Re-read of `PatchShield.cs:296-321`, `CrashReportPatchHelper.cs`; `lint_docs.py` exit 0 |
| R2 | MED | An escape from `MapScreen.OnFrameTick` also skips `MapScreen.OnPostFrameTick`, the only starter of `CampaignLateAITickTask` (`Campaign.LateAITick`, `PartiesThink`): while the throw recurs, parties stop re-planning while campaign time passes. The decision text said only "the UI layers stop ticking" | L2 MED-2, L5 24, L6 | Added to the feature doc, registry and policy comment | `SandBox.View.Map.MapScreen.cs:1373-1385`, `SandBox.MapScene.cs:246-250`, `Campaign.cs:961-970` |
| R3 | MED | The walk replaces all of `MbEvent<float>.Invoke`, but only `InvokeList`'s IL was pinned: a second list, guard or handler added to `Invoke` would pass every gate | L2 MED-1, L4 F4 | `MbEventInvoke_OnlyCallsInvokeListOnTheNonSerializedList_AndHasNoHandler` (`RequiresGameIL`): one field read (`_nonSerializedListenerList`), one call (`InvokeList`), no branch, no handler | Passes on v1.5.3; pointed at `MbEvent<float>.ClearListeners` in a scratch run it failed ("Invoke must only call InvokeList"), then restored |
| R4 | LOW | "Gained: none runs inside realTickMs, campaignTickMs, tickEventMs or mapStateMs" was wrong: Harmony emits finalizers after every postfix, so a finalizer never sat in its own bracket; only RealTick's moves out of `mapStateMs`, and Patch89's `SceneView` patches keep one inside `mapStateMs` every frame. A repeat of the 2026-10-02 harmony-il lesson | L2 LOW-2, L6 | "Gained" rewritten; registry per-frame list extended | `MethodCreator.cs` (postfixes, then `AddFinalizers`); `MapScreen.cs:833`, `:964`, `:1045` |
| R5 | LOW | "A save" was listed as a gap; `MapState.OnTick` runs `SaveTick` and returns while saving, no loading window is raised, so save frames close normally | L2 LOW-3, L5 27, L6 | Frame model corrected (the save UI's top-screen question marked UNVERIFIED) | `MapState.cs:144-150` |
| R6 | LOW (L4: MED) | "Campaign time stops" holds only for a throw before `TickMapTime`; after it, or from a RealTick postfix, the clock and movement advance and only `Campaign.Tick` is skipped; a `MapState.OnTick` postfix throws after the campaign tick | L2 LOW-4, L4 F1 | Given-up items 1 and 2 restated per throw position | `Campaign.cs:905-909` |
| R7 | LOW | "Nothing else patches Campaign.Tick/CampaignEvents.Tick" holds for TAOM only; the exclusion is by name for every owner. `MapState.OnTick` is shielded from the first start when another mod patched it at load; the registry's Patch43 sentence lacked the second-start qualifier | L1, L2 LOW-5, L4 F7, L5 30, L6 | Qualified in doc, registry and policy comment | `PatchShield.cs:198-204` |
| R8 | LOW | A failed install at the first game init logged only the INFO install line; the warning that nothing is measured came only at a later game init (D6) | L1 F4a, L2 LOW-6, L3, L6 | The installer logs `NotInstalledLine` right after a failed install line | `OnGameInitialized_FirstInstallFails_WarnsNothingIsMeasured` (RED: no matching call, then green) |
| R9 | LOW | The install fault said "measuring stopped for this campaign session"; an install fault means nothing is measured in the process | L1 F4b, L5 28 | `MapProfileLines.BuildInstallFault`, pinned literally | `OnGameInitialized_ApplyThrows_LogsOneFault` (RED), `StatusLines_MatchTheirPinnedLiterals` |
| R10 | LOW | A fault before `_session` was stored (a provider read in `OpenSession`, or the boundary's engine reads) left no session, so every later frame re-opened and threw again; the once-per-session log guard keyed on a session number that never advanced, so the storm was silent and a later campaign's same fault logged nothing | L1 F5, L3 1, L6 P3, L4 F6 | `Fault` takes the campaign and makes it the session, so `IsSettled` returns early from the next boundary; `_faultLoggedSession` deleted (no repeat call remains) | `Step_SessionOpenThrows_FaultsOnceAndDoesNotRetryTheSameCampaign` (RED: the getter read 3 times), `Step_SessionOpenThrowsForTwoCampaigns_LogsEachFault` (RED: 1 of 2 errors) |
| R11 | LOW | A fault dropped every frame that had already closed in the session: no `[MapProfileSummary]` (D6: per-session summary, never drop) | L5 29 | `Fault` writes the summary with `reason=fault` before it stops measuring | `Step_FaultAfterClosedFrames_WritesTheSummaryWithReasonFault` (RED: 0 summaries) |
| R12 | LOW | Untested production paths: `EndAppTick` (the only feed of the continuity count; the test helper called `AddAppTick` directly), the measuring `BeginPhase`/`EndPhase` path, deviation 4's early return, the installer's production `IsPatchedByThisProfiler`, and two walker shapes (removing the successor, the head removing itself) | L1 F2, L4 F3, L4 F5 | `CloseFrame` drives `EndAppTick(BeginPhase())`; the early return extracted as `IsSettled` with two tests; `EndPhase_Measuring_AddsThePhaseToTheClosedFrame`; `IsPatchedByThisProfiler_RealPatch101PrefixOnly_True` patches test methods with the real Patch101 prefix and a foreign one; two walker differential tests | Characterisation, green before and after; one existing assertion (`RecordView`) now includes `appTickMs`, which the real helper records |
| R13 | LOW | `taomMs` was called "TAOM's own share"; it leaves out TAOM's patches inside the brackets, its hourly and daily listeners and its models | L5 25 | "A floor on TAOM's share", with what it leaves out | Doc |
| R14 | LOW | `realTickMs` starts with `WaitAsyncTasks`, the wait for the previous frame's party AI task | L5 26, L2 note | Field row | `Campaign.cs:905-907` |
| R15 | LOW | Where the profiler's own cost lands was incomplete: the walk's bookkeeping is in `tickEventMs`; the window line's flushed write lands in the next window's `otherMs`, `maxFrameMs` and `allocKB` | L3 2 | `otherMs` row | `TickEventListenerWalker.cs:83-96`, `MapSessionHooks.Step` |
| R16 | LOW | Cost said three boundary reads (five) and "one reference compare" when not measuring (also a `WeakReference.Target` read) | L6, L1 | Cost section | `MapSessionHooks.OnFrameBoundary` |
| R17 | LOW | Unstated: while measuring, another mod's patch on `Invoke` or `InvokeList` is bypassed for TickEvent; once installed, listener exceptions carry TAOM frames | L2 LOW-9, L5 31 | Two "not covered" lines (exposure UNVERIFIED) | Doc |
| R18 | LOW | The public `Invoke` swap target was the literal `"Invoke"` | L2 LOW-7 | `nameof(MbEvent<float>.Invoke)` | `CampaignEventsTickRewrite_FindsTheOneInvoke_InInstalledEngine` |
| R19 | LOW | The four reflection-site rows and DataRows carried no line numbers | L2 LOW-8 | `TickEventListenerWalker.cs:47`, `:51`, `:54`, `:57` | `ReflectionSiteBindingTests` green |

### False positives and accepted designs

| Finding | Lenses | Verdict |
|---|---|---|
| `MapSessionHooks.cs` at 149 lines | L1 F6 | Not a violation; after the fixes it is 147 lines. The pure-class split stays a follow-up idea |
| `ReadPartyCount` and `ReadRawTopN` swallow silently | L1 F4c, L4 F6 | FALSE POSITIVE against D6's text ("an explicit reason line whenever anything disables itself"): nothing disables; `parties=na` is the documented per-field marker, and a failed raw top-N read means no fallback was detected and the provider's value stands |
| `MapViewTargets` can return a partial list silently | L1 F4c, L4 F6 | FALSE POSITIVE: the install line prints found and patched view counts, and `MapViewTargets_AreTheTaomMapViewPerFrameOverrides` pins the three expected views; a `ReflectionTypeLoadException` on TAOM's own assembly would break far more than the profiler |
| The executor's "7 comparison tests" | L2, L3, L4 | Accurate count of the class, overstated description: 4 of the 7 compare against vanilla `Invoke`. Two more differential tests added (R12) |
| Naming nits (one-class file, `ProbeStamp` placement, two-part test names) | L1 NIT | No change; the plan prescribed them and plan 028 has the same shape |
| GitHub issue not filed | L4 | Held by DECISIONS D4; "Refs: plan 039" stands in |

### Orchestrator probes, answered

1. **Cost.** Off: no patch exists (`MapFrameProfilerInstaller.cs:46-51`); per game init four resolves and one toggle read, per game end one null check; nothing per frame or per party. Option A removes PatchShield's finalizer for every player from `Campaign.RealTick` and `MapScreen.OnFrameTick` (first start) and `MapState.OnTick` (second start). On but not measuring: deviation 4 verified, now as `IsSettled` (a flag, a `WeakReference.Target` read and a compare before any other engine read), with two tests.
2. **Listener swap.** Matches v1.5.3 `MbEvent<T>` statement for statement (head read once, `Next` read after the call, nothing caught, `RecordEntry` in `finally` cannot throw). Main thread only (`Campaign.Tick` from `MapState.OnMapModeTick`). No state to restore. Six differential tests now compare against vanilla `Invoke`, and `Invoke`'s own IL is pinned (R3).
3. **PatchShield option A, per target** (input to FOR-MIKE 16a; the list is unchanged):

| Target | Shielded today from | Swallowed there (option B) | Escaping (option A) |
|---|---|---|---|
| `Campaign.RealTick` | 1st game start (Patch89) | rest of RealTick skipped; views, `Campaign.Tick`, save tick and the app tick run; Patch89 (and Patch101) stripped once | rest of `MapState.OnTick` (the campaign tick; the clock has still advanced when the throw comes after `TickMapTime`), `Game.OnTick` handlers, `AfterTick`, save completion, every module's app tick, `JobManager`, `AvatarServices`; every frame it recurs |
| `MapState.OnTick` | 2nd start (Patch43), or 1st when another mod patched it at load | rest of OnTick skipped, app tick runs; Patch43 and Patch101 stripped | as above from `GameStateManager.OnTick` outward |
| `MapScreen.OnFrameTick` | 1st start (Patch89 trace, Patch36) | rest of OnFrameTick skipped, UI layers tick; Patch36's F6 hub stripped for the process | predecessor `IdleTick`, every layer tick, `LateUpdate`, `LateTick`, `PostFrameTick`, and so the party AI task |
| `Campaign.Tick`, `CampaignEvents.Tick` | profiler on only (no other TAOM patch) | profiler on: rest skipped, Patch101 stripped | profiler off: unchanged for TAOM; another mod's patch loses the shield |

   At the outer method: Patch37 first; capture on (default) it reports, throttled, and swallows; capture off it hands back and PatchShield swallows and strips the outer method's non-protected patches. No CTD path while PatchShield is installed. The executor's UNVERIFIED order claim is now VERIFIED (above).
4. **Deviations.** Top-N warning: kept (placement follow-up, L1 F3). Two extra tests: kept. `MapProfilerTargets` split: correct. Once-per-session fault logging: replaced by R10's fix. `parties=na`: kept. SandBox.View types by string and `ClassInitialize` loading: required. Dates 2026-10-03: the commit date. `TryBind` early return: harmless.
5. **D6.** Header, periodic lines, summary, every status line pinned; R8, R9 and R11 closed the gaps.
6. **SubModule.cs.** Exactly two pure insertions (+18, 0 deleted): the installer after `ApplyGating` and before the guard, `EndSession("gameEnd")` at the end of `OnGameEnd`. Not edited by this review.
7. **Known failures.** Only `EveryLanguage_DeclaresARowForEveryEnglishKey`, at base and at the end.

## Action items

All confirmed findings are fixed in this commit. Owed by the orchestrator:

1. A convergence pass (Step 4.6) on the review commit; this lead cannot spawn a reviewer.
2. FOR-MIKE 16a: option A or B, with the per-target table above and the feature doc's corrected text.
   Answered 2026-10-03: option B (decision D13), see "Decision" at the end.
3. `/verify-bindings` for the API snapshot.

## Improvements (Step 4)

```
APPLIED:
  - P2 (L6): MapFrameProfilerBindingTests.Patch101Bodies_CompileAgainstInstalledEngine (RequiresGameIL):
    RuntimeHelpers.PrepareMethod on every method of the Patch101 hooks namespace, which surfaces a member
    the installed engine lacks before a body's own try can run. Green on v1.5.3; a failing run needs an
    engine that lacks a referenced member, so its RED is UNVERIFIED (a scratch build against a doctored
    assembly was not attempted).
  - P3 (L6) and L3 issue 1: applied as R10 (fault path only; red first).
  - P4 (L6): MapFrameProfiler.EndSession(int) became StopMeasuring(); the unreachable stale-session branch
    and its test deleted. MapFrameProfilerTests.StopMeasuring_OpenSession_StopsMeasuring and the hooks'
    session and fault tests green before and after.
  - P5 (L6): the redundant prefix assertion in Step_AnotherCampaign_WritesThePreviousSummaryThenOpensTheNext
    deleted.
  - L3 issues 2 and 3: applied as R15 and R1.
NOT APPLIED:
  - P1 (L6), MapSessionHooks.cs:47-49: classify speed from Campaign.GetSimplifiedTimeControlMode().
    Behaviour-CHANGING for profiler users (a waiting main party in a Stoppable mode prints Stop); needs the
    maintainer's word.
  - L1 F3, MapSessionHooks.cs RawTopN seam: move the top-N fallback report into the validating provider (so
    Patch97 gets it too). The provider is outside the plan's file scope; listed as a follow-up.
FOLLOW-UP (pre-existing code; no issue filed, DECISIONS D4 holds issue filing for this run):
  - BattleLoadDiagnosticsSettingsProvider.cs:79-89: TickProfilerTopN and HitchThresholdMs fall back silently.
  - PatchShield.cs:316, :320: one diag line per swallowed throw, unthrottled; the strip lands on the method
    that caught, never the culprit; owner com.taom.mod matches no protected prefix (PatchShieldPolicy.cs:238-249).
  - Patch37_CrashReport.cs:23-26: says finalizers run "first runs last"; Harmony 2.4.2 runs highest
    priority first.
  - harmony-il.md:379 says TAOM's patches are protected from the strip; they are not.
  - Patch89, Patch36 and Patch43 postfix bodies reference engine members directly, so a JIT-time member
    failure escapes their own try (now unshielded on these targets).
  - PatchShieldPolicy.FormatHotMethodSkip says "from a patch on it"; for these targets it also covers the
    body and its callees.
  - P6 (L6): share AppendTop, Num, Seconds, Int, Kb and Quote with plan 028's TickProfileLines after both merge.
  - P7 (L6): one shared TargetOf test helper for the three PatchShield walks.
```

**Behaviour note on R10.** A fault while closing the previous campaign's session (its summary write
throwing) now also leaves the new campaign unmeasured, because the fault records the current campaign;
before, the next frame opened the new campaign normally. The summary class comment states the rule: a
fault stops measuring the campaign current at the fault.

## Verification

- Base (a3c219d4, before any edit): `Failed!  - Failed:     1, Passed: 12538, Skipped:     2, Total: 12541`.
- RED run of the new tests (`FullyQualifiedName~TAOM.Tests.Features.MapPerf`, API added, behaviour not yet):
  `Failed!  - Failed:     6, Passed:    83, Skipped:     0, Total:    89` (the five behaviour tests above, and
  `RecordView` whose assertion was then corrected for the real app-tick helper).
- After the fixes, MapPerf filter: `Passed!  - Failed:     0, Passed:    89, Skipped:     0, Total:    89`.
- Walker filter after the two differential tests: `Passed!  - Failed:     0, Passed:     9, Skipped:     0, Total:     9`.
- Final full suite: `Failed!  - Failed:     1, Passed: 12549, Skipped:     2, Total: 12552`, the one failure
  `EveryLanguage_DeclaresARowForEveryEnglishKey` as at base (12 new tests, 1 deleted, so 11 more than base).
- `python tools/lint_docs.py --fail-on-drift --summary --dash-base c4ee9373`: exit 0, `ai_dashes: 0`; the
  10 `context_budget` findings predate the change.
- Reference-assembly steps: not run. Every new test that touches an engine type is in a class tagged
  `RequiresGame` or carries `RequiresGameIL`, which both CI filters exclude
  (`.github/workflows/csharp.yml:67`, `:83`); the two uncategorised test classes changed
  (`MapProfileLinesTests`, `MapFrameProfilerTests`) touch no engine type.
- Hook suite: not run (no hook or `tools/test_hooks.sh` change).

## CODEX REVIEW

Codex not run for this item (no paid dispatch authorised).

| Codex finding | Verdict | Action |
|---|---|---|
| none (not run) | n/a | n/a |

**AGENTS.md lessons (pending):** none (no Codex findings).

## VERDICT

READY FOR COMMIT: every confirmed finding is fixed and tested where testable, Step 4 applied with P1 and
the provider placement left for the maintainer, and the final full suite matches the base apart from the
added tests. Convergence pass owed (orchestrator).

## Convergence round 1

Scope: `a3c219d4..69ed4a36`. Both findings were checked against the v1.5.3 taom-src cache and the
branch's code; both are documentation errors, fixed in the convergence commit (its hash is the branch
HEAD that follows this record). No runtime behaviour changed.

| # | Severity | Finding | Verdict | Action |
|---|---|---|---|---|
| C1 | LOW | The PatchShield option A text (feature doc, "Given up" items 1 and 2) said a throw in `Campaign.RealTick` after `TickMapTime` still moves parties, and left `SaveHandler.CampaignTick` reading as if it ran | Confirmed. `Campaign.RealTick` runs `TickMapTime`, then every `CampaignEntityComponent.OnTick`, then (first frame) `InitializeDataCache`, then `_tickData.RealTick`, which is the movement step (`CampaignTickCacheDataStore.RealTick`, the `TWParallel.For` passes over moving parties). `MapState.OnTick` calls `SaveHandler.CampaignTick()` after `OnMapModeTick`, so a throw out of it skips that call | Fixed. Item 1 lists `SaveHandler.CampaignTick` among the unwound calls; item 2 says the clock advances after `TickMapTime` and parties move only when the throw comes after `_tickData.RealTick` (a `RealTick` postfix such as Patch89's heartbeat), `Campaign.Tick` skipped either way |
| C2 | LOW | The status-line row and two `MapSessionHooks` comments promised a `reason=fault` summary after any fault, including one in the summary write itself, where the retry runs the same code and throws again | Confirmed. `FileLogger.LogInfo`/`LogError` only enqueue and `Drain` catches everything, so in production a summary write throws only from `Summarize`, `BuildSummary` or `BuildNoFramesLine`; `Summarize` resets nothing and `StopMeasuring` has not run, so `Fault`'s `WriteSummary(profiler, "fault")` repeats the throw, which its catch swallows | Fixed in the doc row and both comments: such a summary is lost. The optional change (skip the retry from `EndSession`) was not made: it adds a branch to save one swallowed throw on a path that only a profiler bug reaches (`simplicity-criterion.md`, tiny win plus complexity) |

**Verification.** Full suite before the edits and after them, both
`Failed!  - Failed:     1, Passed: 12549, Skipped:     2, Total: 12552`, the one failure
`EveryLanguage_DeclaresARowForEveryEnglishKey` as at base. No test was added: both fixes are prose (doc
text and code comments) that no test reads. No gate changed, so no differential sweep.

## Convergence round 2

The convergence reviewer read `69ed4a36..5714149b` and raised one LOW finding: that commit's body says
party movement is the last step of `Campaign.RealTick`, while `SiegeEventManager.Tick` follows it (the
feature doc's "near the end" is right). Agents may not amend, so the orchestrator put the correction in
the body of `docs(map-perf): v2.0.32 - record plan 039's convergence rounds`; a history rewrite at merge can
reword the sentence instead. Full suite at `5714149b`: `Failed! - Failed: 1, Passed: 12549, Skipped: 2,
Total: 12552`, the known `EveryLanguage_DeclaresARowForEveryEnglishKey` only.

## Decision (2026-10-03)

The maintainer answered FOR-MIKE 16a with **option B** (decision D13). `MapState.OnTick`, `Campaign.RealTick` and
`MapScreen.OnFrameTick`, which Patch43, Patch89 and Patch36 patch for every player, went back under PatchShield;
`Campaign.Tick` and `CampaignEvents.Tick` stay excluded as built. The same decision narrowed plan 028's list:
`Mission.OnTick` and `Mission.OnPreTick` came off it, `Mission.TickAgentsAndTeamsImp` stays.

Everything above records the review of the first build, which took option A. The "Escaping (option A)" column of the
per-target table in probe 3 is still the analysis of the path not taken; "Swallowed there (option B)" is now what
the three shared methods do. Findings R1, R2, R4, R6 and C1 corrected option A text that no longer ships: the
feature doc's PatchShield section, the registry and the `PatchShieldPolicy` comment now carry the option B text, with
a per-target table in the feature doc (the rule of the 2026-10-02 harmony-il lesson). R7 still holds for the two
excluded methods (the exclusion is by name for every owner). The Patch36, Patch43 and Patch89 registry paragraphs that
option A added are removed again.

Tests first: `SharedMapTargets_StayUnderPatchShield` (the three shared patch classes' real targets must not be on
`ExcludedTargetMethods`) and its Mission twin `MissionTickTargets_StayUnderPatchShield` failed against the first
build (`Assert.IsFalse failed. TaleWorlds.CampaignSystem.GameState.MapState.OnTick must stay under PatchShield` and
`... TaleWorlds.MountAndBlade.Mission.OnTick must stay under PatchShield`; `Failed: 2, Passed: 4, Total: 6` for the four
pinning tests and the two skip-line tests), then passed after the list change. The old walks were renamed to say
what they now pin: `ProfilerOnlyTargets_AreOnPatchShieldsExclusionList` and
`AgentTickBracketTarget_IsOnPatchShieldsExclusionList`. At the merge with plan 028 the two Mission walks became
028's `AgentTickTarget_IsOnPatchShieldsExclusionList` and `TickAndPreTickTargets_AreNotOnPatchShieldsExclusionList`
(the same pins, plus the namespace assertion below); the Mission names in this record are this branch's.

## Review follow-up pass (2026-10-03)

Scope: the branch tip `f86342ca`. Inputs: a Claude review of plan 028's own D13 commit (`81811ea2`, issue #710), whose
Mission-side findings read on this branch too, and the Codex adversarial review of `c4ee9373..f86342ca`. Every finding
was re-read against the code and the installed v1.5.3 decompile before it was classified. The review's issue is #721.

| # | Source | Sev | Finding | Verdict | Action | Proof |
|---|---|---|---|---|---|---|
| F1 | Codex P2 (observation 1, S3) | P2 | A waiting main party in a Stoppable mode advances no campaign time, yet the raw mode put the frame in `Play` or `FF`, so waiting frames mixed into the speed buckets (the first review's P1, held for the maintainer) | CONFIRMED, decided (FOR-MIKE 16r) | The speed class reads `Campaign.GetSimplifiedTimeControlMode()` through `ITimeControlAdapter.SimplifiedTimeControlMode`; `MapSessionHooks.Step` reads it after `OpenSession` | `Campaign.cs:821-847` and `:860-895` read this turn; RED `Failed: 2, Passed: 94, Total: 96` (a waiting frame printed `speed=FF` and `byspeed=Play:1/10.00`), then `Passed: 96, Total: 96` |
| F2 | Codex P2 (observation 2) | P2 | PatchShield's owner-wide strip removes Patch101's pair from `Campaign.RealTick`, `MapScreen.OnFrameTick` or `MapState.OnTick`; the installer's check ran once, so later windows read zeros that look measured | CONFIRMED | `MapProfilerTargets.UnpatchedCore` (the install line's `missing`, now shared) is handed to `MapSessionHooks.LostHooks` and run at each session start (toggle on) and window start; a lost hook gives one aggregated `[MapProfiler]` warning, a `reason=hooksLost` summary and a stop; the window line before it is kept | `PatchShield.cs:288-322` and `:405-407` read this turn; `IsPatchedByThisProfiler_AfterThePatchShieldStripCalls_False` shows the check sees the strip's three `Unpatch` calls; RED `Failed: 9, Passed: 96, Total: 105`, then `Passed: 105, Total: 105`; eleven mutations killed (listed below) |
| F3 | Codex P3 (observation 3) | P3 | The record said the reference-assembly steps were not run, so full-branch RefAsm safety was unverified | CONFIRMED as an evidence gap | Ran the CI's build, unit step and binding gate on reference assemblies (below) | `Failed: 3, Passed: 10162, Skipped: 28, Total: 10193`; `Passed: 378, Total: 378` |
| F4 | Codex P3 (observation 4) | P3 | "The only cost every player pays" was too strong: `SubModule.cs:1569` resolves four services and builds a delegate on every game init, and the two `PatchShieldPolicy` exclusions are unconditional | CONFIRMED | The status-table row and the "Cost" section say what runs with the toggle off, and that another mod's patch on the two excluded methods loses the shield; the policy comment says the same | `SubModule.cs:1569-1575`, `PatchShield.cs:198-204` read this turn |
| F5 | Claude MEDIUM | MED | The D13 Mission text called the `TickAgentsAndTeamsImp` exclusion a fix and said an escape "reaches the native job thread"; in fast-forward the agent tick runs inline inside `Mission.OnTick`, whose restored shield swallows the escape with `tickCompleted` still false, and a swallow after `tickCompleted = false` on `Mission.OnTick` hangs the next `WaitTickCompletion` | CONFIRMED (text); the hazard itself predates D13 and the profiler and goes to a separate follow-up plan by ruling (its swallowers and scope were corrected in the convergence round below: Patch37's crash capture swallows too) | Feature doc, registry, policy comment and lesson now say: the fix covers the asynchronous tick only, the inline path and every throw after `Mission.cs:3756` hang the next wait, and the shield strips Patch35 and Patch97 whoever threw | `Mission.cs:3548`, `:3603-3606`, `:3629` (the only `tickCompleted = true`), `:3756`, `:3784-3791`; `MissionState.cs:171-196` (`asyncAITick: false` while `IsFastForward`); `PatchShield.cs:306-321` |
| F6 | Claude LOW | LOW | Plan 028's D13 commit and this branch's `f86342ca` both implement the Mission half of D13 from `765d3759`, so the integration re-run will conflict | Not changed (ruling: note only, the merge resolves it) | None here. This branch carries the Mission half (`MissionTickProfilerBindingTests.AgentTickBracketTarget_IsOnPatchShieldsExclusionList` and `MissionTickTargets_StayUnderPatchShield`); `81811ea2` is not an ancestor of this tip and its merge-base with it is `765d3759` | `git merge-base --is-ancestor`, read-only |
| F7 | Claude LOW | LOW | The 2026-09-26 and 2026-09-28 `harmony-il.md` rules still said a per-frame target goes on `ExcludedTargetMethods`, against D13 | CONFIRMED | Both Prevent bullets narrowed: per-unit, per-agent and parallel-tick targets go on the list; a once-per-frame main-thread target stays shielded unless a swallow there is unsafe | `harmony-il.md` |

**Behaviour changes.** F1 changes what `speed=` and `byspeed=` print for waiting frames (the first review held it for the
maintainer, now decided). F2 adds one warning and a stop where a stripped hook used to leave the profiler running on
zeros. `MapSessionHooks.Step` lost its `mode` and `multiplier` parameters (it reads the adapter itself); the frame
boundary's engine reads moved to `MapFrameBoundary`, because the hooks had to stay under 150 lines (ADR-002, plan 039's
size check): `MapSessionHooks.cs` is 148 lines. `ITimeControlAdapter` gained one read-only member; its one
implementer is `TimeControlAdapter` and every other user fakes it with NSubstitute.

**Not covered, owed.** A strip on `MapState.OnTick` takes the frame boundary that runs the check, so no window or
session start follows and the lines just stop (the convergence round below added a look at the session end, which names
it). The Codex fix suggested running the check from a hook the shield cannot reach (TAOM's own
`SubModule.OnApplicationTick`); the decision for this pass was the session and window start, so that variant is not
built and the gap is written into the feature doc. The `Mission.OnTick` completion hazard goes to its own follow-up
plan (its scope is corrected in the convergence round below).

**Mutation checks** (each on the live file, restored byte for byte, the MapPerf filter run): the window check removed
(2 tests fail), the session start ignoring lost hooks (2), the session start checking with the toggle off (1), the check
run at every boundary (1), no `reason=hooksLost` summary (1), the no-adapter fallback changed from -1 to 0 (1), the
installer not handing over the check (1), the warning written at INFO (2), the speed read from the raw mode (2),
the unpatched predicate inverted (9, the install decisions share it), the warning's names joined with a semicolon (1).

**Verification.** Base MapPerf filter at the tip: `Passed: 92, Total: 92`. Full suite after the code:
`Failed!  - Failed:     1, Passed: 12564, Skipped:     2, Total: 12567`, the known
`EveryLanguage_DeclaresARowForEveryEnglishKey` only (13 new tests over the commit message's `Passed: 12551`).
Reference-assembly build (`-p:TaomGameRefs=RefAsm`, `BANNERLORD_GAME_DIR` unset, Debug): 0 errors. Unit step:
`Failed!  - Failed:     3, Passed: 10162, Skipped:    28, Total: 10193` (CI's own count: total 10193, executed 10165),
the three failures the plan's known RefAsm set (`EveryLanguage_DeclaresARowForEveryEnglishKey`, and
`Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`, a
`FileNotFoundException` for `TaleWorlds.MountAndBlade.View`), none in `TAOM.Tests/Features/MapPerf`. Binding gate:
`Passed!  - Failed:     0, Passed:   378, Skipped:     0, Total:   378`; its base set was not run here, so there is no
base to compare, and no failure to attribute. The new tests all sit in `RequiresGame` classes (both CI filters skip
them) except the two `MapProfileLinesTests` pins, which the unit step ran. Python suite (`python -B -m unittest
discover -s tools/tests -t .`): `Ran 2962 tests`, `FAILED (failures=3, skipped=8)`, the three known
(`test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`,
`test_default_is_on_the_e_drive`); this pass touches no `tools/` file. `python tools/check_doc_graph_ratchet.py` fails
the same way on an export of the branch tip without this pass (orphans 155 against a baseline of 26, components 161
against 29), so it says nothing about this change.

## Convergence round of the follow-up pass (2026-10-03)

Scope: the follow-up commit `7ac9cab7`. Inputs: the convergence review of that commit, and
`claude-review-TRUE-039.json`, the Claude review of this branch's `f86342ca`, which the follow-up pass had not answered:
it answered `claude-review.json`, the review of plan 028's `81811ea2` in wt-028, and the first lines of each file name the
range and the worktree. Every finding was re-read against the code and the installed v1.5.3 decompile before it was
classified. Issue #721.

| # | Source | Sev | Finding | Verdict | Action | Proof |
|---|---|---|---|---|---|---|
| G1 | Convergence MEDIUM | MED | The `tickCompleted` hazard was attributed to PatchShield alone ("the shield's, not the profiler's", "a separate PatchShield follow-up plan"). Patch37's crash capture on `Module.OnApplicationTick` swallows any exception while capture is on, so an ordinary exception after `Mission.cs:3756` (in any game) and a missing-API throw in a process's first game freeze the next frame too, and a PatchShield plan built on the culprit-only strip changes neither path | CONFIRMED | The feature doc carries plan 028's paragraph from `31c0fd15` (identical after whitespace; only its dangling citation of 028's own review is dropped) plus the two cases, the no-catch evidence and the follow-up's scope: a completion-aware recovery on `Mission.OnTick`, for example a TAOM finalizer that completes the tick when the body unwinds after `:3756`. The registry, the policy comment and the lesson say the same, and the sentence about "the first build, one frame up" names the right catchers | `Patch37_CrashReport.cs:47-53` (priority 800, `HandleAndSwallow`), `CrashReportPatchHelper.cs:36-59` (`return null`, no type filter), `SubModule.cs:215-221` (applied unconditionally), `CrashReportService.cs:98-154` (no exit, no mission end); decompile `Mission.cs:3652-3792` (no try or catch), `MissionState.cs` (none), `GameStateManager.cs:196-210`, `Game.cs:332-351` (its only `try` wraps the game handlers), `GameManagerBase.cs:104-113`, `Module.cs:527-540`; `Mission.cs:3601-3606`, `:3629`, `:3756`. The native half (the next `OnPreTick` reaches `WaitTickCompletion`) stays UNVERIFIED |
| G2 | Convergence LOW | LOW | The policy comment said the profiler finds a strip "at its next window or session start", untrue on `MapState.OnTick`; the agent-tick test comment still called the exclusion a fix with no asynchronous qualifier | CONFIRMED | Both comments reworded | `Patch101_MapFrameProfiler.cs:37` is the only caller of `MapFrameBoundary.OnFrameBoundary`, and `MapFrameBoundary.cs:33` the only production caller of `MapSessionHooks.Step` (grep); `PatchShield.cs:405-407` |
| G3 | Convergence LOW | LOW | After a `MapState.OnTick` strip nothing clears `Measuring`, so the other five hooks and the views keep paying the full measuring cost with nothing in the output to say so, and `EndSession("gameEnd")` wrote a plain summary of the frames closed before the strip. `EndSession` and the `newCampaign` close wrote their summary without the hook check, so a `Campaign.RealTick` or `MapScreen.OnFrameTick` strip after the last window start put up to one window of zero-phase frames into it unflagged, and on a new campaign the warning that followed named the new session | CONFIRMED | Built: before a measuring session's summary (`gameEnd`, `newCampaign`) `MapSessionHooks.WarnIfHooksLost` runs the same check, and a lost hook gives the warning for that session and a `reason=hooksLost` summary. One look per session end. The docs say what remains: on `MapState.OnTick` the in-session cost continues until that look | `StopMeasuring` is called only from `WriteSummary` and `Fault`, reached only from `Step`, `EndSession` and `MapFrameBoundary` (grep); `SubModule.cs:851`. RED `Failed: 6, Passed: 107, Total: 113`, then `Passed: 113, Total: 113`; seven mutations killed (below) |
| G4 | Convergence LOW | LOW | RCA row F4's preventive action cited a `misc.md` sentence that did not exist | CONFIRMED | The `misc.md` lesson now covers the lessons, with the RCA row as a source | grep for "across the lessons" and "lessons too" under `docs/reviews/lessons` found nothing; `git diff --stat f86342ca..7ac9cab7` lists no `misc.md` |
| G5 | `claude-review-TRUE-039.json` #2, #4, #7 | LOW | The "Gained" line paired each removed finalizer with the wrong bracket; `Mission.OnPreTick` was called shared and previously shielded; the `*_StayUnderPatchShield` walks ignored the namespace exclusions | CONFIRMED | "Gained" rewritten: a `Campaign.Tick` finalizer would run after Patch101's postfix, inside `mapStateMs`, a `CampaignEvents.Tick` one inside `campaignTickMs` and never `tickEventMs`, and shielded callees such as Patch65's `SpawnLordParty` still run theirs inside `campaignTickMs`. The lesson, the REVIEW-LOG line and the test comment say `Mission.OnPreTick` is shielded only while the profiler patches it. Both walks also assert `IsExcludedTargetNamespace` is false | `Campaign.cs:974-1020`, `CampaignEvents.cs:2083-2086`, `CampaignPeriodicEventManager.cs:358-363` and `:397-406`, `HeroSpawnCampaignBehavior.cs:22`, `:194`, `:246`; `Patch97_MissionTickProfiler.cs:36` is the only `[HarmonyPatch]` on `Mission.OnPreTick`; `PatchShield.cs:59-60`. With `"SandBox.View"` or `"TaleWorlds.CampaignSystem.GameState"` added to `ExcludedTargetNamespacePrefixes` and the new assertion neutralised, the MapPerf, MissionPerf and PatchShieldPolicy tests all passed (`Passed: 248, Total: 248`), so the gap was real |
| G6 | Convergence LOW | LOW | `TimeControlAdapter.SimplifiedTimeControlMode` was pinned by no test (every speed test fakes the adapter), so the raw-mode bug could return with the suite green | CONFIRMED | `TimeControlAdapterBindingTests` (`BindingVerification`) reads the getter's IL and asserts a call to `Campaign.GetSimplifiedTimeControlMode` and none to the raw getter; the adapter leaves the "not testable offline" list | no test named the concrete adapter (grep); mutations below |

The other five findings of `claude-review-TRUE-039.json`: #1 (the duplicated D13 commit) stays under the ruling "note
only, the merge resolves it", and G1 narrows what the merge has to reconcile; #3 (the `TickAgentsAndTeamsImp` exclusion
covers the asynchronous tick only) and #5 (the stale Prevent rule) were fixed in `7ac9cab7` (F5 and F7 above); #6 (the
`f86342ca` body's "What a player sees" paragraph) lives in the commit message, which the orchestrator rewrites at the
squash; #8 (PatchShield's swallow line keeps only the type and message) is existing PatchShield behaviour that the review
itself assigned to plan 034 or the culprit-only-strip plan.

**Behaviour changes.** G3 adds, at the end of a measuring session, one look at the six core hooks (six patch-info reads)
and, when one is lost, one warning and a `reason=hooksLost` summary in place of the `gameEnd` or `newCampaign` one.
Nothing changes with the profiler off or with the hooks intact. `MapFrameBoundary` took over `ReadPartyCount` and
`ReadRawTopN` (two engine reads), so `MapSessionHooks.cs` stays at 148 lines (ADR-002, plan 039's size check).

**Mutation checks** (each on the live file, restored byte for byte and compared against a copy taken before the run):
the session-end look removed from `EndSession` (3 tests fail), removed from the `newCampaign` close (3), not gated on
`Measuring` (8, five of them existing tests), a lost hook leaving the summary's reason as it was (1), the warning naming
the wrong session (3), the window check writing no summary (2), the window check removed (3). `"SandBox.View"` or
`"TaleWorlds.CampaignSystem.GameState"` added to the namespace exclusions: the map walk fails; with its new assertion
neutralised, nothing fails. `"TaleWorlds.MountAndBlade"` added: the Mission walk fails; neutralised, it passes. The
adapter reading the raw `TimeControlMode`: the new pin fails and every other test in the TimeAcceleration and MapPerf
filters passes (`Passed: 176, Total: 176`); the adapter returning a constant `Stop`: the pin fails.

**Verification.** The eight new session-end tests against the unchanged production code: the MapPerf filter read
`Failed!  - Failed:     6, Passed:   107, Skipped:     0, Total:   113` (the other two are guards that hold until a
wrong check exists), then `Passed!  - Failed:     0, Passed:   113, Skipped:     0, Total:   113`. The adapter pin and
the namespace assertions pin behaviour that already held, so their RED is the mutations above. Full suite: `Failed!  - Failed:     1, Passed:
12573, Skipped:     2, Total: 12576`, the known `EveryLanguage_DeclaresARowForEveryEnglishKey` only (the previous
pass's `Passed: 12564`, plus 9). Reference assemblies (Debug, `BANNERLORD_GAME_DIR` unset): build 0 errors; unit step
`Failed!  - Failed:     3, Passed: 10162, Skipped:    28, Total: 10193`, the same three known failures as before
(`EveryLanguage_DeclaresARowForEveryEnglishKey`, `Patch93_HasTheSevenPatchesInItsCategory`,
`Patch94_HasTheMapIconNoParleyAndNoJoinPatches`); binding gate `Passed!  - Failed:     0, Passed:   379, Skipped:     0,
Total:   379` (378 before; the adapter pin has no `RequiresGameIL` tag, so it runs there). Python suite (`python -B -m
unittest discover -s tools/tests -t .`): `Ran 2962 tests`, `FAILED (failures=3, skipped=8)`, the same three known; this
pass touches no `tools/` file. `python tools/lint_docs.py --fail-on-drift` exits 0, and every file under
`Main/Features/MapPerf/Hooks/` is under 150 lines (`MapSessionHooks.cs` 148).

## Top-up pass (2026-10-03)

Scope: the branch tip `c6940d65`. Input: `claude-review-TRUE-039.json` once more, all eight findings. The passes above
answered it from two places (the G5 row and the paragraph after G6); this pass re-derived every verdict from the files,
the installed v1.5.3 decompile and the commits `7ac9cab7` and `c6940d65`, and finished what two of the fixes had left.
Docs only: no code and no test changed.

| # | Sev | Finding | Verdict | Proof and action |
|---|---|---|---|---|
| 1 | MED | `f86342ca` writes its own copy of plan 028's D13 commit `81811ea2`, so the integration re-run conflicts | Not changed (ruling: note only, the merge resolves it) | A read-only `git merge-tree` of plan 028's tip `d952df94` and this tip (merge-base `765d3759`) shows 8 files with 18 conflict hunks: `mission-perf-heartbeat.md` 5, `MissionTickProfilerBindingTests.cs` 4, `harmony-il.md` 3, `PatchShieldPolicy.cs` 2, and one each in the 028 RCA, the 028 review record, REVIEW-LOG and the registry. 028 holds the no-game pin `IsExcludedTargetMethod_MissionOnTickAndOnPreTick_ReturnsFalse`, which this branch lacks; this branch holds the namespace assertions in its two walks, which 028's walk lacks. 028's own record says its test names are "reconciled at the merge" (L12). One pair of Mission test names and one D13 paragraph per lesson must survive the merge |
| 2 | LOW | The "Gained" line paired each removed finalizer with the wrong bracket and overstated the gain | Already fixed (`c6940d65`) | `map-perf-profiler.md:324-329` says a `Campaign.Tick` finalizer would run after Patch101's postfix, inside `mapStateMs`, a `CampaignEvents.Tick` one inside `campaignTickMs` and never `tickEventMs`, and that shielded callees such as Patch65's `SpawnLordParty` still run theirs inside `campaignTickMs`. Re-read this pass: `CampaignEvents.cs:2083-2086` (the body is one `Invoke`), `Campaign.cs:974-992`, `CampaignPeriodicEventManager.cs:358-363` and `:397-405`, `HeroSpawnCampaignBehavior.cs:22`, `:144`, `:194`, `:246`, `Patch65_LandlessCultureSpawnGuard.cs:40`. `git log -S"never exists"` on the doc names only `c6940d65` |
| 3 | LOW | The `TickAgentsAndTeamsImp` exclusion was presented as the fix for the `WaitTickCompletion` hang on every call | Already fixed (`7ac9cab7`), one copy left; reworded now | The table row (`mission-perf-heartbeat.md:246`), the paragraph (`:224-232`), the policy comment (`PatchShieldPolicy.cs:148-155`) and the test comment (`MissionTickProfilerBindingTests.cs:74-77`) all scope the fix to the asynchronous tick and say the inline call escapes into `Mission.OnTick`. Engine re-read: `Mission.cs:3784-3791` (asynchronous or inline), `MissionState.cs:171-196` (`asyncAITick: false` in fast-forward) and `:201` (`true` otherwise); `Native2ManagedTargets.cs` allowlists 16 shims and none is `TickAgentsAndTeams`, so no TAOM finalizer on that shim swallows an asynchronous escape. The copy left was this branch's "Decision (2026-10-03)" section of the 028 review record ("its exclusion is a fix, as noted above"), now scoped to the asynchronous tick |
| 4 | LOW | `Mission.OnPreTick` was counted among the once-per-frame targets other TAOM patches use, and called shielded before the profilers | Already fixed (`c6940d65`) | The Update under the 2026-09-26 lesson in `harmony-il.md` names the four shared targets and says `Mission.OnPreTick`, which only the default-off Patch97 patches, is shielded only while the profiler patches it; the 028 entry in REVIEW-LOG says the same; the walk's comment limits "every player" to `Mission.OnTick`. `Patch97_MissionTickProfiler.cs:36` is the only `[HarmonyPatch]` on `Mission.OnPreTick` (grep over `Main` and `Dependencies`). The `f86342ca` body cannot be amended: `c6940d65`'s body corrects it, and this commit's body carries the wording for the squash |
| 5 | LOW | The standing Prevent rule still sent per-frame targets to the exclusion list | Already fixed (`7ac9cab7`), two parts left; added now | Both Prevent bullets were narrowed to per-unit and per-agent targets plus the unsafe-swallow case. Left out: the second exception D13 kept (`Campaign.Tick` and `CampaignEvents.Tick`, which only the default-off profiler patches) and the reverse pin. The 2026-09-26 bullet now names both |
| 6 | LOW | The `f86342ca` body says a callee-thrown exception is swallowed and "the offending patch is stripped" | Confirmed, wording only | The strip removes every non-protected owner's patches on the method (`PatchShield.cs:366-420`), so with no patch at fault TAOM's own patches go, and the cause is swallowed again every frame, each time skipping the rest of the method. History is not rewritten: this commit's body carries the corrected paragraph, and the squash body takes it |
| 7 | LOW | The two `StayUnderPatchShield` walks checked the method list only, so a namespace prefix would unshield `MapScreen.OnFrameTick` or `MapState.OnTick` with every test green | Already fixed (`c6940d65`) | Both walks also assert `IsExcludedTargetNamespace` is false (`MapFrameProfilerBindingTests.cs:212`, `MissionTickProfilerBindingTests.cs:109`). Three mutations run this pass (below) each fail a walk, and nothing else but `IsExcludedTargetNamespace_GameplayNamespaces_ReturnsFalse` for the Mission one |
| 8 | LOW | PatchShield's swallow line keeps the type and message and no stack, where Patch37's crash capture used to record frames on these methods | Rejected for this branch | `PatchShield.cs:316` writes `swallowed {type} from a patch on {owner}.{name}: {message}` and `DiagLog.LogCaught` (`DiagLog.cs:30-36`) writes type and message only. The same line is on trunk (`bannerlord-1.5.x` at `dffdf879`, `PatchShield.cs:301`), whose `ExcludedTargetMethods` names none of the five, so D13 restores trunk's behaviour and nothing regresses against it. The review's own fix reads "Not in this commit". For plan 034 or the culprit-only-strip plan: on the first swallow per target and exception type, log the stack (with `TAOM.ThrowSite` when present), count the repeats, and drop "from a patch" when the throwing frame is not a patch method |

**Behaviour changes.** None. The fixes are one sentence in the 028 review record and one lesson bullet; this section and
the REVIEW-LOG line record them.

**Mutation checks** (finding 7's guards; each on the live `PatchShieldPolicy.cs`, restored byte for byte and compared with
`cmp` against a copy taken first; filter: the two binding classes, `PatchShieldPolicyTests`, `MapPerf` and `MissionPerf`,
`Passed: 248, Total: 248` unmutated). `"SandBox.View"` added to `ExcludedTargetNamespacePrefixes`: 1 fails,
`SharedMapTargets_StayUnderPatchShield`, naming `SandBox.View.Map` and `MapScreen.OnFrameTick`.
`"TaleWorlds.CampaignSystem.GameState"`: 1 fails, the same test, naming `MapState.OnTick`. `"TaleWorlds.MountAndBlade"`:
2 fail, `MissionTickTargets_StayUnderPatchShield` and `IsExcludedTargetNamespace_GameplayNamespaces_ReturnsFalse`.

**Verification.** Full suite after the edits: `Failed!  - Failed:     1, Passed: 12573, Skipped:     2, Total: 12576`, the
known `EveryLanguage_DeclaresARowForEveryEnglishKey` only (the same totals as the previous pass, since no code changed).
`python tools/lint_docs.py --fail-on-drift --summary --dash-base HEAD` exits 0 with `ai_dashes: 0`. The lesson behind the
two incomplete fixes already exists: `misc.md`, "A claim found wrong is wrong everywhere it was written".

## Residual pass (2026-10-04)

Scope: the branch tip `158fea98`. Input: the four LOW findings a residual review left after the two fix rounds, written
against `c6940d65`. The top-up pass changed only the 028 review record, this report, REVIEW-LOG and one lesson bullet, so
every cited line was re-read at the tip. Each claim was checked against the code first, and the patch-info cost against
a decompile of the Lib.Harmony 2.4.2 the project references. Docs and comments only: no test logic and no behaviour
changed. Issue #721.

| # | Sev | Finding | Verdict | Proof and action |
|---|---|---|---|---|
| 1 | LOW | The `tickCompleted` hazard text names two catchers, PatchShield's finalizer on `Mission.OnTick` and Patch37's crash capture; a third, PatchShield's own finalizer on `Module.OnApplicationTick`, swallows a missing-API throw when Patch37 hands it back | CONFIRMED | `CrashReportPatchHelper.cs:40`, `:46-47`, `:55` and `:59` return `HandBack` (re-entry, capture off, no service, service throws), which is `RethrowStackPreserver.PreserveForRethrow`, "Never a wrapper" (`RethrowStackPreserver.cs:61`), so the next finalizer sees the same exception. `Main/SubModule.cs:217` applies Patch37 in `OnSubModuleLoad`; `Dependencies/SubModule.cs:293` runs PatchShield pass 2 at every game init, the first included; `PatchShield.cs:55-63` and `:174-228` attach to every non-TAOM patched method on neither exclusion list (`Module.OnApplicationTick` is on neither, and `SaveShield`'s targets do not include it); `ShouldSwallow` (`:288-322`) swallows the three exception types on any shielded method. Fixed in the feature doc (the "first build" sentence, "Who swallows", "Two cases", and a crash-capture-off case in the follow-up's regression tests), the policy comment (and the short form of it that a test comment repeats), the Patch97 registry paragraph and both `harmony-il.md` updates. Plan 028's `31c0fd15` paragraph, which the feature doc copies, lives in wt-028 and is not touched here: the same edit is owed there so the shared text stays identical for the merge |
| 2 | LOW | RCA rows G2 and G5 cite lesson text that does not exist | CONFIRMED | The `misc.md` grep list named `Main/`, ModuleData comments, `TaomSettings.cs` and the lessons, not `Dependencies/` or tests, and G2's two comments are in `PatchShieldPolicy.cs` and `MissionTickProfilerBindingTests.cs` (`git show c6940d65`); a grep of `docs/reviews/lessons` for the scope-line wording found no G5 lesson. `misc.md` is extended and its Source names row G2; `testing-qa.md` carries the G5 lesson; both RCA rows point at them |
| 3 | LOW | The hook check runs inside a measured frame and the docs do not say so | CONFIRMED (docs); the behaviour option not taken | `MapFrameBoundary.cs:33` takes the timestamp and the allocation count before `Step`, and `MapFrameProfiler.Boundary` stores them as the open frame's start (`:182-183`). `MapSessionHooks.Step` then writes the window line and calls `WarnIfHooksLost` (`:57-60`), and the campaign-change looks sit at `:49` and `:88`, all before the MapState prefix's `BeginPhase` (`Patch101_MapFrameProfiler.cs:37-38`), so their time lands in `otherMs` and `maxFrameMs` of the frame the boundary opened and their allocation in `allocKB`, and a collection they cause in the next window's `gc` deltas (`TakeWindow` stores the counts before the line is written, `:56` and `MapFrameProfiler.cs:211-213`). One look is six `Harmony.GetPatchInfo` calls: `HarmonySharedState.GetPatchInfo` ends in `PatchInfoSerialization.Deserialize`, a `BinaryFormatter`, and the `Patch.PatchMethod` getter calls `AccessTools.GetMethodByModuleAndToken`, which walks `AppDomain.GetAssemblies()` and every loaded module. Plan 028's `31c0fd15` body says the same of its own check, which it placed at a mission start. The `otherMs` row, the Cost section and the `harmony-il.md` Prevent bullet now say where the cost lands and mark it UNVERIFIED. Re-basing the open frame after the window work, for clean windows, is a behaviour change with its own `MapFrameProfilerTests` case: not taken, the maintainer's call |
| 4 | LOW | The squash message, D13's follow-up record and FOR-MIKE 16m defer three items to records outside the branch | Not changed here | The records live under the program's `plans/_audit`, which this pass does not edit; the orchestrator redrafts the squash message. For that redraft: option B (only `Campaign.Tick` and `CampaignEvents.Tick` excluded), waiting Stoppable frames read `Stop`, and the hook check at session start, window start and measuring session end with one warning and `reason=hooksLost`; the follow-up is a completion-aware recovery on `Mission.OnTick` with the regression tests the feature doc now lists, and TRUE-039 #8 (log the stack with `TAOM.ThrowSite` on the first swallow per target and exception type, aggregate the repeats) |

**Behaviour changes.** None. The source edits are two comments, in `PatchShieldPolicy.cs` and in
`MissionTickProfilerBindingTests.cs`: the policy comment's hazard paragraph and its short form beside the agent-tick
entry, and the test comment that restated the short form.

**Mutation checks.** None: no test logic changed, and findings 1 to 3 are documentation and comment text.

**Verification.** `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: `Build succeeded.`, 0 errors, two
analyzer warnings (BHA0001, BHA0006) in files this pass does not touch. Full suite:
`Failed!  - Failed:     1, Passed: 12573, Skipped:     2, Total: 12576, Duration: 28 s - TAOM.Tests.dll (net472)`, the
known `EveryLanguage_DeclaresARowForEveryEnglishKey` only (the same totals as the previous pass, since no test logic
changed).
`python -B tools/lint_docs.py --fail-on-drift --summary --dash-base HEAD` exits 0 with `ai_dashes: 0`.
