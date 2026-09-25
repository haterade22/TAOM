# Deep review and Codex: plan 006 maintainer decisions (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 006 maintainer decisions (crash capture: entry 6 swap, bridge single exit, helper
         hand-backs, off-main verdict, ten combat callbacks), #650
Date: 2026-09-24

Scope:   C# (Main/Features/CrashReport, 4 files; tests, 5 files), docs (6 files, CHANGELOG)
         Diff 70727529..8e6b0935 on improve/006-crash-capture-boot-cost
         (commits 42624b95 and 8e6b0935, which share one subject)
Waves:   one wave: Agents 1 Standards, 2 Engine, 3 Efficiency, 4 Completeness, 5 Data Flow,
         6 Design. Agent 7 (XML) and Tooling NOT IN SCOPE (no XML, no tooling files).
         Codex adversarial (gpt-6-astra, ultra): complete ("END OF CODEX REVIEW" present).

STANDARDS:     FAIL: 6 LOW, 6 NIT (all fixed or accounted below)
COMPATIBILITY: PASS: 16 verified, 0 incompatible, 3 unverified (main-thread premise, detour on a
               delegate-target shim, tableau/thumbnail thread); 2 LOW text errors fixed
EFFICIENCY:    PASS: 1 UNVERIFIED (PatchShield per-call wrapper on 16 shims until plan 007;
               doc applied), 1 MEDIUM follow-up (pre-existing)
COMPLETENESS:  INCOMPLETE at review, COMPLETE after fixes: M1 (swallow path untested),
               M2 (probe names an allowlisted callback), L1 to L7
DATA FLOW:     FAIL at review: 16 flows, 2 gaps (T8 LOW fixed; T16 follow-up), 4 inconsistencies
               (T3 MED NEEDS MIKE; T11 UNVERIFIED, logged; T13 pre-existing; T14 LOW fixed)
DESIGN:        6 KEEP proposals (5 apply, 1 follow-up): 4 applied, 1 needs Mike, 1 follow-up
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Verification of each finding

Every finding below was re-read against the worktree at `8e6b0935` (or the installed engine and
BCL) before it was acted on. "Fixed" means changed on the branch in the review follow-up commit.

### Codex (P1: 0, P2: 1, P3: 2)

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2: a failed `ex.Data` write loses the off-main verdict; the worker capture then runs the full collectors and the inquiry | MED | Yes | `Native2ManagedPatcher.cs:107-108` swallowed the write failure; `CrashReportService.cs:164-174` read an absent mark as main-thread. `ilspycmd -t System.Exception` on the installed mscorlib shows `Data` is virtual and returns `EmptyReadOnlyDictionaryInternal` for immutable agile exceptions. RED test reproduced it (see Fixes). Fixed |
| 2 | P3: `crash-report.md:102` still says master-off returns the raw exception | LOW | Yes | Contradicted `CrashReportPatchHelper.cs:43-44` (`HandBack`) and `crash-report.md:65`. Fixed |
| 3 | P3: the owed dropped-callback probe names `OnAgentRemoved`, now allowlisted | LOW | Yes | `Native2ManagedTargets.cs:58`. Same as lens 1 LOW-3, lens 2 T2, lens 4 M2, lens 5 T14. Fixed |

- **Confirmed bugs:** 1 (P2), plus the two P3 doc defects.
- **False positives:** none.
- **Design questions:** none raised by Codex beyond the posture it explicitly accepted (swallow and
  default results, `crash-report.md:292`).
- **Things Codex missed:** the untested swallow path (it listed it as a coverage limit, Known
  Suspect 9, not a defect), the stale MCM hint (noted, not counted), the `reflection-sites.md` counts,
  the incomplete tableau caller list, the BUTR frame loss on master-off (lens 5 T3), the unmeasured
  comparison and PatchShield's per-call cost, and the review record's decision labels.
- **Known Suspects:** Codex CONFIRMED 7 (marker failure) and partly 9 (coverage); both are the
  findings above. Its DISPUTED verdicts on 2, 5, 6, 8 and 10 match what the lenses and this review
  read (all 16 shims resolve; the preserver's signature and idempotence at
  `RethrowStackPreserver.cs:63-93`; the boot id refreshes before attach at `SubModule.cs:203-204`).
  1, 3 and 4 stay UNVERIFIED (historical execution).

### Lens findings

| Finding | Lens(es) | Verdict | Action |
|---|---|---|---|
| Swallow path never tested with a reachable service; mutations survive | 4 M1, 4 L1, 1 LOW-4 | CONFIRMED (MED) | Fixed: `RecordingCrashService`; mutation-checked (below) |
| MCM hint omits the combat callbacks and names only "character tableau" | 1 LOW-1, 4 L3, 5 T8, 6 #2, Codex note | CONFIRMED (LOW) | Fixed: `CrashReportSettings.cs:31` |
| `reflection-sites.md:21`, `:113` say "six" | 1 LOW-2, 4 L4, 5 T8, 6 #5 | CONFIRMED (LOW) | Fixed: count dropped |
| Owed dropped-callback probe uses `OnAgentRemoved` | 1 LOW-3, 2 T2, 4 M2, 5 T14, Codex 3 | CONFIRMED (LOW; lens 4 said MED) | Fixed: `Agent_OnDismount`; `OnAgentRemoved` relabelled as the off-main bundle recipe |
| Off-main logic duplicated in bridge and hook; redundant `!= 0` clause | 1 LOW-5, 6 nits | CONFIRMED (LOW) | Fixed: one `AppDomainExceptionHook.IsOffMainThread(int)` |
| Review record labels D43 and D47 the wrong way round; NEEDS MIKE numbers instead of register ids | 1 LOW-6 | CONFIRMED (LOW); register rows 43 and 47 read | Fixed: relabelled; the register is cited by path (it is not on this branch, so no link) |
| `crash-report.md:102` master-off text | 4 L2, Codex 2 | CONFIRMED (LOW) | Fixed |
| Combat Risks bullet omits `ref`/`out` state and the abandoned remainder of the method | 2 T5, 4 L5, 6 #4 | CONFIRMED (LOW); `Mission.cs:5789-5790` and `:3019-3040` read in the v1.5.3 cache | Fixed |
| Tableau caller list omits `BasicCharacterTableau`, `BrightnessDemoTableau` | 2 T3 | CONFIRMED (LOW); `ilspycmd` on the installed View DLL, lines 255 and 104 | Fixed in `Native2ManagedTargets.cs` and the doc |
| "Called far more often than the per-frame screen shims" | 2 T4 | CONFIRMED (LOW, unmeasured) | Fixed: comparison dropped |
| PatchShield's per-call finalizer on every shim | 3 E1 | CONFIRMED mechanism (`PatchShield.cs:60-69` has no `ManagedCallbacks` exclusion); cost UNVERIFIED | Doc sentence applied; merge order is for Mike |
| Superseded sentence in the Changelog (`crash-report.md:326`) | 6 #5, 4 N1 | CONFIRMED (NIT) | Fixed |
| Cap test cannot fail on its own; count in the pin's name | 6 #3, 1 NIT, 4 N6 | CONFIRMED (NIT) | Applied (Step 4, preserving) |
| `s_mainThreadId` prefix | 1 NIT | CONFIRMED | Fixed: `_mainThreadId` |
| `CrashReportService.cs:120-123` comment names only the hook | 1 NIT, 4 N4, 5 T2 | CONFIRMED | Fixed |
| `CrashReportPatchHelper.cs:27-29` quotes a retired hint | 1, 4, 5 follow-ups | CONFIRMED; in a changed file, adjacent to changed lines | Fixed (comment only) |
| `crash-report.md:336` has no issue link | 4 N2 | CONFIRMED | Fixed |
| Untracked Codex prompt file | 4 N5 | CONFIRMED | Committed with this review |
| No lesson for the off-main hole | 1 follow-up, 4 L7 | CONFIRMED | Lesson in `harmony-il.md` (broadened to the side-channel pattern) |
| `#650` body and comment behind the branch | 4 L6 | CONFIRMED | NEEDS MIKE (public post) |
| Master-off hand-back clears frames before ButterLib's BEW finalizer | 5 T3 | CONFIRMED mechanism: `RethrowStackPreserver.cs:86` nulls `_stackTrace`; `ilspycmd` on the shipped ButterLib shows `BEWPatch.Enable` attaching `FinalizerMethod` at priority -1 (400) to `Managed.ApplicationTick`, `Module.OnApplicationTick`, `ScreenManager.Tick`, `Mission.Tick` (lines 67-71). BUTR's report reading `EnhancedStackTrace` is lens-reported, not re-read | NEEDS MIKE: it trades against decision D26 |
| Main-thread premise (`OnSubModuleLoad` thread is the game loop) | 2 T1, 5 T11, 4 follow-up, 6 #1 | UNVERIFIED (no managed evidence either way) | Diagnostic applied: `Subscribe()` logs the recorded id; owed launch check added to the doc. The `TWParallel.IsMainThread()` swap is NEEDS MIKE |
| Info: mark set before the capture decision with master off | 3, 5 T6 | Not a defect (nothing reads it); moot now that the mark is gone | None |
| CHANGELOG lead says six, ends at 16 | 1 NIT, 4 N1 | CONFIRMED (NIT) | Left for the merge-time date-header consolidation (earlier NEEDS MIKE 8); a follow-up paragraph was appended |
| Two commits share a subject | 1 NIT, 4 N3 | CONFIRMED | History not rewritten; the record now cites hashes |
| `AppDomainExceptionHookTests` leaves the static set to a dead thread | 1 NIT, 5 T15 | CONFIRMED, harmless | Not applied: every reader takes the id as a parameter, and the new `Finalizer` test subscribes on its own thread first |

## Fixes (defects)

**F1, the off-main verdict (Codex P2).** TDD. `HandleOrPassThrough_CaptureOnAWorkerThread_WithReadOnlyData_StillTellsTheServiceItIsOffMain`
was written against the unchanged code with a `RecordingCrashService` that read the `Data` mark at
call time and a `ReadOnlyDataException` (a `Hashtable` subclass whose setter throws). RED:
`Assert.IsTrue failed. an exception whose Data rejects writes must still take the reduced path off
the main thread`; `Failed: 1, Passed: 16, Total: 17`. Change:

- `ICrashReportService.HandleException(Exception, string, bool offMainThread = false)`; the service
  uses the parameter. `CrashReportService.IsOffMainThread(Exception)` is deleted.
- `AppDomainExceptionHook.IsOffMainThread(int mainThreadId)` is the single definition (an unset id
  never equals a real thread, so it counts as off-main). `OnUnhandled` passes it; its `Data` write and
  `OffMainThreadDataKey` are deleted.
- `CrashReportPatchHelper.HandleAndSwallow(..., bool offMainThread = false)` passes it through; the
  bridge computes it and `MarkIfOffMainThread` is deleted. Patch37 and `BattleLoadStallWatchdog`
  keep the default `false` (unchanged behaviour).
- `Subscribe()` logs `[CrashReport] main thread id N recorded at Subscribe()`, the cheap proof for
  the UNVERIFIED premise.

GREEN: the crash-report and battle-load filter passed 351 of 351.

**F2, the untested swallow path.** New tests with the recording service:
`HandleOrPassThrough_WhenTheServiceCaptures_SwallowsWithTheCallbackOrigin`,
`..._CaptureOnAWorkerThread_TellsTheServiceBeforeItCaptures`,
`..._WhenNoMainThreadWasRecorded_TellsTheServiceItIsOffMain`,
`..._WhenNativeCaptureIsOff_OnAWorkerThread_NeverReachesTheService`,
`Finalizer_ComparesAgainstTheIdTheHookRecorded`,
`HandleAndSwallow_WhenTheServiceCaptures_SwallowsAndPassesTheOrigin`,
`HandleAndSwallow_WhenTheServiceThrows_HandsBackTheOriginalWithItsThrowSite`,
`HandleAndSwallow_ReenteredFromInsideTheService_HandsBackTheInnerException` and
`IsOffMainThread_IsFalseOnlyOnTheRecordedThread`. The three old tests that asserted the `Data`
mark are replaced by these. Mutation check (each applied, the filter run, the file restored):

| Mutation | Tests failing |
|---|---|
| (a) bridge returns `PreserveForRethrow(exception, null)` | 2 |
| (b) bridge passes `false` for the verdict | 3 |
| (c) helper's swallow `return null` becomes `return HandBack(exception)` | 4 |
| (d) `Finalizer` passes 0 instead of `MainThreadId` | 1 |
| (e) toggle guard dropped (`nativeCaptureEnabled` forced true) | 1 |
| (f) helper drops the verdict on the way to the service | 3 |

Before this commit, lens 4 predicted (a) to (d) survive every test; each now fails.

**Text fixes:** `CrashReportSettings.cs:31` hint; `reflection-sites.md:21`, `:113`;
`crash-report.md` (off-main paragraph, config table row 102, Tests list, Performance, Risks tableau
list and combat bullet, Changelog, issue link, owed checks); `harmony-patch-registry.md` Patch37
target line; `Native2ManagedTargets.cs` comments; the first review record's decision tables.

## ACTION ITEMS

1. Mike: the ButterLib frame loss on master-off hand-backs (NEEDS MIKE 1 below).
2. Orchestrator: one convergence `deep-reviewer` pass on this follow-up commit's diff (Step 4.6);
   this delegate cannot spawn agents.
3. Mike: merge plan 007 no later than this branch (PatchShield per-call wrapper on the 16 shims),
   and whichever merges second corrects the "plus a PatchShield attach" wording and 007's
   `PatchShieldPolicy.cs` comment ("247").
4. First launch: read the `main thread id N recorded` line against a `MissionThreadGuard`
   "main N" line, and the `attached 16 of 16 ... in X ms` line.

## IMPROVEMENTS (Step 4)

```
APPLIED:
- TAOM.Tests/Features/CrashReport/Native2ManagedTargetsTests.cs: All_IsASmallDistinctAllowlist
  deleted, its cost text moved into the pin's AreEquivalent message, pin renamed
  All_IsExactlyTheReviewedShims (Agent 6 #3, PRESERVING, tests only). Characterisation: the pin
  was green before and after (filtered 37 of 37 before; 351 of 351 after); AreEquivalent compares
  occurrence counts, so a duplicate or a 17th entry still fails.
- docs/features/crash-report.md: combat Risks bullet gains the ref/out and abandoned-remainder text
  (Agent 6 #4, PRESERVING, docs); Performance gains the PatchShield per-call sentence and loses the
  unmeasured comparison (Agent 3 E1 APPLY, docs); superseded Changelog sentence deleted (Agent 6 #5).
- docs/reference/taleworlds-api-snapshot/reflection-sites.md: counts dropped (Agent 6 #5).
- Main/Features/CrashReport/CrashReportSettings.cs:31: hint rewritten (Agent 6 #2; text only, no
  runtime change; filed as a defect fix above).
NOT APPLIED:
- Agent 6 #1 (TWParallel.IsMainThread() instead of the boot id): CHANGING and rests on an
  UNVERIFIED native premise; needs Mike. The logged id settles the premise first.
- Agent 3 E1 merge order with plan 007: a merge decision, needs Mike.
- AppDomainExceptionHookTests static reset (NIT): harmless, see the table above.
- CHANGELOG lead consolidation: at merge, with the date header.
FOLLOW-UP (pre-existing code; no issue filed, since /issue is public and never auto-invoked):
- BattleLoadStallWatchdog.cs:189 captures from a thread-pool Timer without the off-main verdict, so
  it runs the Mission and Campaign collectors and ShowInquiry on a pool thread (lens 1, lens 5 F1,
  Agent 6 #6). With the new parameter the fix is one argument; which way (reduced bundle, or keep
  the Mission section and skip only the inquiry) is Mike's call.
- Agent 3 E2: suppressed captures pay file-info stack walks, per-frame Assembly.GetName, SHA1 and
  BUTR reflection before the throttle decides; now per hit while a combat bug recurs. PRESERVING
  fix sketched by the lens.
- Concurrent captures: _handling is [ThreadStatic], and GetDefendCollisionResults is callable from
  several threads (Mission.cs:6522), so HandleException can run in parallel (lens 2 F2, lens 5).
- TrySuspend (ButterLib Disable, and Harmony.Unpatch when BetterExceptionWindow is loaded) now runs
  on worker threads during combat captures (lens 2 F3, lens 5 F3).
- Mission_OnPreTick and Mission_TickAgentsAndTeams reach TAOM code (AutonomousMovementPlayerController,
  TaomTacticBase, the Formation patches on the AI thread) and are neither on the list nor in the
  "left out" line (lens 2 F1, lens 5 T16). Needs Mike.
- crash-report.md:94 overstates ButterLib's Disable() (it removes BEW finalizers only when the
  BetterExceptionWindow module is loaded) (lens 5 F2).
- Native2ManagedBridge lives in Native2ManagedPatcher.cs (class/file naming); feature-map.md has no
  CrashReport row; the 1.4.5 port must re-resolve all 16 names. (OnUnhandled's test gap moved out of
  FOLLOW-UP: its line was touched, so the convergence pass fixed it; see Convergence below.)

VERDICT: READY FOR COMMIT (Step 4 applied; the Step 4.6 convergence pass is owed to the orchestrator)
```

## NEEDS MIKE

1. **ButterLib loses the live frames when Enable Crash Capture is off** (lens 5 T3, MED). D26 routes
   every hand-back through `PreserveForRethrow`, which clears `_stackTrace`; ButterLib's BEW finalizer
   runs after TAOM's (priority 400 against 800) on four Patch37 targets and builds its report from the
   live frames. The plain text trace survives. Options: (a) return the raw exception on the master-off
   path only and let PatchShield preserve last; (b) accept and document (the config table now says it);
   (c) preserve the text without clearing frames, accepting duplicated frames if a later finalizer
   swallows.
2. **Main-thread reference.** Keep the boot id (now logged) or switch to `TWParallel.IsMainThread()`
   (Agent 6 #1) after one launch shows whether they agree.
3. **Plan 007 merge order** (PatchShield's per-call wrapper on the 16 shims until it lands).
4. **#650 comment** (public): the body still says "6 of 6" and predates D43, D47 and this fix.
5. **Watchdog capture thread** and **`Mission_OnPreTick` / `Mission_TickAgentsAndTeams` coverage**
   (follow-ups above).

## CODEX REVIEW

Codex adversarial review (gpt-6-astra, ultra) of `70727529..8e6b0935`, output in
`docs/reviews/raw/codex-adversarial-006-crash-capture-boot-cost-decisions-2026-09-24.md` (local,
gitignored), prompt in `docs/reviews/codex-adversarial-006-crash-capture-boot-cost-decisions-2026-09-24.prompt.md`.
Quality: engine excerpts quoted from the installed DLLs for all 16 shims, Harmony 2.4.2's
`MethodCreator` rethrow choice, the BCL's `Exception.Data`, and a config cross-reference table.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | MED | Yes | Reproduced RED with a read-only `Data` on a worker thread; fixed with an explicit parameter |
| 2 | P3 | LOW | Yes | Config table contradicted `HandBack`; fixed |
| 3 | P3 | LOW | Yes | Probe example was on the allowlist; fixed |

### Phase 3e root cause

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Off-main verdict lost when `Exception.Data` is read-only | Other: safety decision in a best-effort side channel | Copied a pattern from another feature (the hook's mark) without checking the direction of its catch | Parameter instead of mark; RED test; lesson in `harmony-il.md` |
| 2 | Config table stale after D26 | Convention inconsistency (doc) | Did not re-read every doc sentence describing the changed path | Fixed; covered by the `misc.md` list-change lesson |
| 3 | Probe example on the allowlist | Other: verification gap | Example not re-checked after the list grew | Fixed; lesson in `misc.md` |

RCA: `docs/reviews/rca-crash-capture-boot-cost-decisions-2026-09-24.md`.

## AGENTS.md lessons (pending)

Phase 3h is consolidated later for all branches. Proposed lines:

- **Bugs Codex typically misses:** a test gap it can see but files as a "coverage limit" rather than a
  defect (here the untested swallow path, which let four mutations survive); stale player-facing
  hint text outside the diff.
- **What Codex does well:** decompiling the BCL (`System.Exception.Data`) as well as the game to find
  the one input that defeats a guard, and stating which of its own observations it chose not to count.

Also pending consolidation: `docs/reviews/LESSONS-LEARNED.md` per-category counts (misc, testing-qa
and harmony-il each gained one lesson here; the index counts were already behind on this branch).

## Test evidence

- Before any change: crash-report filter (`Native2ManagedBridgeTests`, `AppDomainExceptionHookTests`,
  `Native2ManagedTargetsTests`, `CrashReportPatchHelperTests`, `RethrowStackPreserverTests`,
  `Patch37TargetShapeTests`): `Passed: 37, Total: 37`.
- RED as quoted under F1.
- After: the same filter plus `BattleLoad`: `Passed: 351, Total: 351`.
- Full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` in the worktree:
  `Failed: 2, Passed: 10271, Skipped: 2, Total: 10275`. The two failures are the known live-Armory
  tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; the branch is based before `a39a9c86`.
- `python tools/lint_docs.py --quick --fail-on-dead`: dead links 0. No em or en dash on any added line.
- Nothing run in game.

## Convergence

One `deep-reviewer` pass (Standards lens plus a behaviour-parity check) over `8e6b0935..8c84fa20`
reported five defects, all in comments, docs and tests; runtime parity held. Each was re-read
against the code before fixing. None was a false positive.

| # | Sev | Finding | Outcome |
|---|---|---|---|
| 1 | LOW | `AppDomainExceptionHook.IsOffMainThread` comment called itself the definition for "every" worker capture source, but `BattleLoadStallWatchdog` captures from a pool Timer without it | FIXED: the comment names the watchdog as the open follow-up |
| 2 | LOW | The touched `OnUnhandled` line had no test, and `offMainThread` defaults to the unsafe `false`, so dropping the argument compiled silently | FIXED: `OnUnhandled` made `internal` (InternalsVisibleTo); new `OnUnhandled_TellsTheServiceWhetherItRanOnTheSubscribingThread` asserts `false` on the subscribing thread and `true` on a worker. The FOLLOW-UP line that filed it as pre-existing is corrected |
| 3 | NIT | `Finalizer_ComparesAgainstTheIdTheHookRecorded` pinned only the id-0 direction | FIXED: the test also calls `Finalizer` on a worker and asserts off-main; its comment and the crash-report.md test line now state both directions |
| 4 | NIT | Three stale comments: the `_mainThreadId` note ("mark"), the `HandleAndSwallow` arity in `Native2ManagedPatcher.cs`, the `CrashReportPatchHelperTests` header | FIXED: reworded |
| 5 | NIT | The deep-review record pointed at a deleted `Native2ManagedPatcher.cs` comment; the crash-report.md config row said the AppDomain hook hands exceptions back when master is off (`OnUnhandled` just returns) | FIXED: repointed to `IsOffMainThread` in `AppDomainExceptionHook.cs`; the row now says the hook does nothing and limits the throw-site clause to the Patch37 finalizers |

**RED evidence (mutation):** with the verdict dropped from `OnUnhandled` and `Finalizer` passing the
current thread's id instead of `AppDomainExceptionHook.MainThreadId`, the filter
`AppDomainExceptionHookTests|Native2ManagedBridgeTests` gave `Failed: 2, Passed: 11, Total: 13`
(exactly the two new assertions). Both mutations were reverted before the fix run.

**Full suite:** `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
`Failed: 2, Passed: 10272, Skipped: 2, Total: 10276`. The two failures are the known live-Armory
tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; the branch is based before `a39a9c86`.
`python tools/lint_docs.py --quick --fail-on-dead`: dead links 0. No em or en dash on any added line.

CONVERGENCE: all five fixed; no runtime behaviour changed beyond `OnUnhandled`'s visibility.
