# Deep review: plan 040, load-time stamps (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: Load-time stamps (plan 040): patch groups and TAOM's load hooks, every module XML load
         per type (Patch100_LoadTimeStamps_LoadXml), each campaign handler of a load
         (Patch100_LoadTimeStamps_Lifecycle)
Date: 2026-10-02

Scope:   C# (Main, Dependencies/Foundation, TAOM.Tests), docs. Branch perf/040-load-time-stamps,
         diff 67fa6c1a..a07546a7 (3 commits, 52 files); worktree wt-040.
Blast radius: not re-run by the review lead (no graphify call in this pass); the lenses traced every
         caller of the changed types by hand (Agent 5 T3 to T19). UNCHECKED by graphify.
Waves:   one wave: Agents 1 (Standards), 2 (Engine compatibility), 3 (Efficiency), 4 (Completeness),
         5 (Data flow), 6 (Design). Agent 7 (XML) NOT IN SCOPE (no ModuleData or XSLT in the diff).
         Tooling lens NOT IN SCOPE (no hook, tool or CI change).

STANDARDS:     PASS: checks 1 to 10 clean; 2 LOW doc findings (fixed)
COMPATIBILITY: PASS: 27 API usages verified, 0 incompatible, 0 unverified at API level;
               1 WRONG doc claim (fixed)
EFFICIENCY:    PASS: 0 high, 0 medium, 2 low (F1 applied; F2 a design question for Mike)
COMPLETENESS:  INCOMPLETE: the GitHub issue is not filed (DECISIONS D4 defers it; merge blocker);
               everything else complete after the fixes below
DATA FLOW:     PASS after fixes: 19 flows traced, 3 gaps and 5 inconsistencies at review, all fixed
               or accounted for below
DESIGN:        4 KEEP proposals (1 applied, 3 not applied: 2 behaviour-changing, 1 premise only
               half true)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Details

The six lens reports are the review packet's text (plan 040 review, Agents 1 to 6). The review lead
re-read every cited line in the worktree before classifying a finding; the classification and the
evidence for each are below.

### Findings, verified

| # | Sev | Finding | Lenses | Verdict | Evidence read | Action |
|---|---|---|---|---|---|---|
| 1 | LOW | The ten new `reflection-sites.md` rows sat after a blank line (:88), outside the Category B table, and their generic-arity names broke the single-backtick spans | 1 F1, 2 F4, 4 L1, 5 F6 | CONFIRMED | `reflection-sites.md:84-100` at a07546a7 | Fixed: the rows joined to the table, double-backtick spans for the ten rows and the status line |
| 2 | LOW | The `feature-map.md:86` LoadTimeStamps row described stage A only | 1 F2, 3, 4 L2, 5 F5, 6 | CONFIRMED | `feature-map.md:86` | Fixed: the row names the always-on `[LoadXml]` lines and the toggled `[Lifecycle]` timing |
| 3 | LOW | The C3 dispatch `ms` started before the listener swap and was read after every C1 and C2 line was written, while the feature doc, the registry and the stage C commit body called `ms - listeners_ms` "the IssueManager and QuestManager receivers" | 1 F5, 2 F1, 3 F1, 5 F1 | CONFIRMED | `LifecycleTimingService.cs:64, 79-90, 171`; v1.5.3 `QuestManager.cs:129, 179` (the only receiver overriding any of the five), `IssueManager` overrides none | Fixed red-first: `Start` moves past the swap, the end tick is read before any line (`End_DispatchMs_ExcludesTheStampsOwnSwapAndLines`, RED `ms=235.00`, GREEN `ms=30.00`); both docs corrected |
| 4 | LOW | `LogOffOnce` wrote C4 ("per-handler timing off for this session; dispatch totals are still written") for a gate or clock fault in `Begin`, a fault in `End` and a restore fault, none of which turns anything off, and the shared once-per-process latch then hid a later real wrap failure | 1 F3, 2 F2, 3, 4 L4, 5 F2, 6 P3 | CONFIRMED | `LifecycleTimingService.cs:66-69, 119-122, 185-188, 195-206` | Fixed red-first: a separate once-only C5 `[Lifecycle] stamp fault` WARNING with its own latch, worded like X3; C4 keeps the binding and wrap cases (four new tests, each RED first) |
| 5 | LOW | A toggle read that throws returned false with no line, against D6 and the gate's own summary | 1 F4, 2 F3, 4 L5, 5 F3 | CONFIRMED | `LoadStampDetailGate.cs:32-39` | Fixed red-first: H3, a WARNING naming the exception whenever the failure turns the detail off |
| 6 | LOW | The per-category P1 lines are decided once per process (`WriteHeldCategoryLines` sits behind `_gameInitPatchesApplied` and the first-mission block), but H1 and the configuration row said the toggle is read live with no restart | 5 F4 | CONFIRMED | `SubModule.cs:1581-1582, 1972-1973, 2037-2038` | Fixed: H1's text (its literal pin updated first) and the configuration row |
| 7 | LOW | A loaded save's last fan-out, `OnGameLoadFinished`, is neither timed nor in the "Not timed" list | 5 F7 | CONFIRMED | v1.5.3 `CampaignEventDispatcher.cs:1098`; `SandBoxGameManager.cs:177` | Fixed: added to "Not timed" (a sixth dispatch waits for field data) |
| 8 | LOW | The caution covered only the toggle-on wrapper frame; the seven always-on targets put a TAOM-patched frame in every load-time crash stack | 5 F8 | CONFIRMED | `HarmonyCorrelationCollector.cs:59` | Fixed: the feature doc's caution and the registry say so |
| 9 | INFO | X1 `result=ok` means `LoadXML` returned; object-creation faults are swallowed inside it | 1 nit, 2 F5 | CONFIRMED | lens citations of `MBObjectManager.cs:786-797` | Fixed: the X1 row says so |
| 10 | LOW | The feature doc had no Performance section; the always-on cost was not stated | 4 L3 | CONFIRMED | `docs/features/TEMPLATE.md`, `load-time-stamps.md` | Fixed: a Performance section with counts, no unmeasured times |
| 11 | NIT | A failed `[LoadXml]` line write dropped its call from the summary (the aggregate was added after the log call) | 4 N1 | CONFIRMED | `LoadXmlStampService.cs:88-113` | Fixed red-first (`End_WhenTheCallLineFails_TheSummaryStillCountsTheCall`) |
| 12 | NIT | `StopwatchStampClock.Instance`'s summary said the applier is built before the container exists; `IoC.Configure` runs at `SubModule.cs:120`, the applier at :206 | 4 N1 | CONFIRMED | `SubModule.cs:120, 206` | Fixed: "built outside the container" |
| 13 | NIT | Unused `using System;` in `HookTimer.cs`; "How to read" did not say the `OnGameStart` hook lines land among the `[LoadXml]` lines; no C0 binding-missing example | 1 nits | CONFIRMED | `HookTimer.cs:1`; v1.5.3 `Campaign.cs:1410, 1429` | Fixed |
| 14 | LOW | Test gaps: HookTimer's never-throws contract, no container test for the module, no shape test for the merge finalizer behind a skipping prefix, two adapter branches, several catch branches | 4 L6 | CONFIRMED (coverage) | the test files | Partly fixed: `MarkAndEnd_WhenTheLoggerThrows_NeverThrow`, `LoadTimeStampsModuleTests`, `MergeStampPatchShapeTests` (each proven to fail against a mutated copy: a rethrowing catch, a transient registration, the stamp applied as a prefix); the restore and End catches are now covered by finding 4's tests. Left as follow-up: the adapter's "still holds our wrapper" branch and mid-walk undo (L6d), and the `Begin` gate or clock catch |
| 15 | LOW | No GitHub issue | 4 | CONFIRMED, NEEDS MIKE | `DECISIONS.md` D4 | Not fixed here: filing is the maintainer's; it blocks the merge |
| 16 | LOW | A failed category's apply time includes its synchronous ERROR write (`PatchCategoryApplier.cs:63, 69`) | 2 F1 (aside) | FALSE POSITIVE | Agent 3 checked the same and called it definitional; the doc says the failed category's time is in the phase total | No change |

The executor's deviations (focus 4) hold: the `PatchCategoryLine` rename is forced by the gate's
`\.PatchCategory\s*\(` regex (`PatchCategoryApplierTests.cs:29-30`); the seven extra tests match the
code; the corrected SandBox reason is right (three lenses traced `SandBoxSubModule.cs:113-127` and
`MBGameManager.cs:110-115`); and the End-fault warning was finding 4.

Focus answers, consolidated: (1) every listener runs once, in order, with its arguments; the wrapper
has no catch; a reflection failure restores everything; nothing stays swapped; the dispatch runs on
the main thread, and the refactor below keeps that assumption explicit in `ListenerTiming`'s
summary. (2) All finalizers and prefixes are `void` and forward to swallowing wrappers; the seven
PatchShield strings match each target's `FullName.Name`; the merge finalizer stays correct when
another prefix skips the original, now pinned by `MergeStampPatchShapeTests`. A third-party prefix
that skips `LoadXML` itself would also skip TAOM's `LoadXML` prefix (its `string` parameters count as
affecting the original in Harmony 2.4.2), losing that call's line safely. (3) Toggle off, per process:
seven `Harmony.Patch` calls (the only material item, size unmeasured), about 100 timed categories, a
few header and total lines; per `LoadXML`: a thread-local push and pop and one INFO line; per
dispatch and hook: one toggle read. (6) `SubModule.cs`: 24 inserted lines, exactly Step 7 and the
Stage B summary call plus three one-line comments; this review does not touch the file.

## Action items

1. File the plan 040 issue (D4) with `triage-needs-ingame` and the plan's "After merge" checklist;
   it blocks the merge.
2. Decide the NEEDS MIKE items below.
3. Run `/verify-bindings` so `patch-targets.md` lists the seven new targets.
4. In-game check: a new campaign and a save load with the toggle on and off; if every `[LoadXml]`
   line shows `merge_ms=none`, the merge finalizer is not firing.
5. Run the convergence lens (Step 4.6) on this commit: the review lead cannot spawn agents.

## Improvements (Step 4)

APPLIED:
- `Main/Features/LoadTimeStamps/LifecycleTimingService.cs` (Agent 3 F1, APPLY): the dispatch is
  timed from the end of the swap to an end tick read before any line.
  `End_DispatchMs_ExcludesTheStampsOwnSwapAndLines` RED then GREEN.
- `Main/Features/LoadTimeStamps/Domain/ListenerTiming.cs`, `Main/Adapters/CampaignListenerAdapter.cs`,
  `ICampaignListenerAdapter.cs`, `LifecycleDispatchScope.cs`, `LifecycleTimingService.cs` (Agent 6
  P2, PRESERVING): each listener's timing records its own calls; the index-addressed
  `Action<int, long, int>`, `EventRecord`'s lock, the growable stats array, `Grow`, `StatsOf`,
  `ListenerStats` and `ListenerInfo` are gone. Characterisation: `LifecycleTimingServiceTests` and
  `CampaignListenerAdapterTests` green before (108 of 108 in the LoadTimeStamps filter) and after
  with the same oracles read off the timings (111 of 111 with the three new `ListenerTimingTests`).

NOT APPLIED:
- Agent 6 P1 (`PatchCategoryApplier.cs:33, 85, 99-110`; `SubModule.cs:1973, 2038`): write every
  per-category line at DEBUG for every player and delete the hold-and-replay. Behaviour-changing; it
  reverses plan decisions 1 and 3 and edits the single-owner `SubModule.cs`. NEEDS MIKE.
- Agent 6 P3 (`LifecycleTimingService.cs`): any fault in the per-handler machinery turns it off for
  the session. Behaviour-changing. The defect it targeted (a false C4 line) is fixed by the separate
  C5 line instead; whether a restore fault should also stop further swaps is NEEDS MIKE.
- Agent 6 P4 (test helper copies): only one of each pair is new in this change. The brace walker in
  `PatchCategoryApplierTests.cs` dates from `bdf7d515` and `CreatureBanditsWiringTests` predates the
  branch, so de-duplicating means editing pre-existing code: FOLLOW-UP.
- Agent 3 F2 and Agent 1 F6: option A (toggle off, write the dispatch total without a swap) is
  behaviour-changing and amends plan decision 1: NEEDS MIKE. Option B (apply the Lifecycle category
  only when the toggle is on) stays deferred by the plan; Agent 3 judges it a tiny win with added
  complexity.

FOLLOW-UP (pre-existing code or outside this change; no issue filed, issues are the maintainer's):
- `reflection-sites.md` rows before this branch (for example :54-68) use single-backtick spans around
  generic-arity names, which break the same way; the file has two "## Referenced by" headings.
- Brace walkers and target resolvers duplicated across `PatchCategoryApplierTests`,
  `CreatureBanditsWiringTests`, `IoCRegistrationDisciplineTests` and `HeroRaceWiringTests`.
- `[HarmonyPriority(Priority.First)]` on the `LoadXML` prefix would keep the X1 line behind another
  owner's skipping prefix (Agent 5); optional, it changes patch order.
- With the toggle on, the wrapper frame sits in the crash signature's top five frames, so a crash
  dedups under a different signature for toggle-on players (Agent 5).
- Tests for the adapter's "still holds our wrapper" branch and its mid-walk undo (L6d).

VERDICT: READY FOR COMMIT (every confirmed defect fixed in this commit; the issue, the NEEDS MIKE
decisions and the convergence pass are owed before merge)

## Verification

- Base (branch head `a07546a7`, before any edit): `Failed!  - Failed:     1, Passed: 12465, Skipped:     2, Total: 12468`;
  the one failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey`.
- RED (LoadTimeStamps filter, the new tests against the old code): `Failed!  - Failed:     8, Passed:   100, Skipped:     0, Total:   108`.
- Final full suite: `Failed!  - Failed:     1, Passed: 12480, Skipped:     2, Total: 12483`, the same
  known failure and nothing new.
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: succeeded, 2 warnings, both
  pre-existing BUTR analyzer warnings in other features.
- `python -B tools/lint_docs.py --summary --fail-on-drift`: exit 0 (10 pre-existing context_budget
  items, 0 dead links, 0 dashes).
- The RefAsm unit step was not run: no new test touches an engine type outside `RequiresGame`.
- No hook, validator or CI step changed, so no hook suite run and no gate sweep.

## CODEX REVIEW

Codex not run: the orchestrator dispatched no Codex review for this item, so there is no Phase 3a to
3c input.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | | | | Codex not run |

- **Confirmed bugs:** none from Codex.
- **False positives:** none.
- **Design questions:** the NEEDS MIKE items above came from the Claude lenses.
- **Things Codex missed:** not applicable.

### AGENTS.md lessons (pending)

For the orchestrator to consolidate into `.ai/review-reference.md` "Look harder here":

- In a timing stamp, check where each clock read sits against the stamp's own work (setup, reflection
  swaps, synchronous log writes): a window that contains its own logging misstates what it measures.
- In a once-per-process reason line, check that every path writing it does what the line says, and
  that unrelated faults do not share its latch.
- A `catch` that returns the "off" value is a disable path: check that it logs its reason.

## Convergence round 1

Six LOW findings from the convergence reviewer on `a07546a7..f1267a98`, each re-verified against the
code (and, for findings 4 and 5, the v1.5.3 `taom-src` cache) before any edit. Every fix is in the
commit that adds this section (subject "convergence fixes for plan 040").

1. **Begin's start tick and the failure paths** (`LifecycleTimingService.cs` Begin): FIXED. Confirmed:
   the post-swap clock read sat inside the wrap try, so a clock fault restored the swap, switched
   per-handler timing off with C4 and left `HasListeners` true (`listeners_ms=0.00`); the binding and
   wrap-failure paths kept the constructor tick, so their C4 write and partial swap were inside `ms`.
   Now the wrap try ends at `HasListeners = true`, and the start tick is read once after both
   branches in its own guard: a fault there is a C5 stamp fault that keeps the swap and the first
   tick. RED first: `Begin_ClockFaultAfterTheSwap_WarnsAStampFault_KeepsTheSwap_AndTimingStaysOn`,
   `Begin_BindingProblem_DispatchMs_LeavesOutTheTimingOffWarning` (old code: `ms=140.00` for 40) and
   `Begin_WrapFailure_DispatchMs_LeavesOutThePartialSwapAndTheWarning` (old code: `ms=112.00` for 7).
   `FakeStampClock` gained `ThrowOnRead`.
2. **The restore fault's reason line** (`RestoreAll`, C5): FIXED. Confirmed: `RestoreListeners`
   drops the event's swaps before restoring them (`CampaignListenerAdapter.cs:65-69`) and nothing
   retries, so a failed restore leaves the wrapper in the record for the session, while C5 said
   lines may be missing. New C6 line `LifecycleRestoreFault` with its own once-only latch, a literal
   pin in `LoadTimeStampLinesTests`, a Log lines row, one clause in the Caution, and C5's text and
   comments no longer list a restore. RED first: the renamed
   `End_RestoreFault_WarnsARestoreFaultOnce_AndStillWritesTheDispatchLine` and
   `End_RestoreFault_NeverHidesALaterStampFault` (red because, at `f1267a98`, its clock fault sat inside the wrap try and wrote C4, so it failed on the C5 and C6 texts; corrected after convergence round 2). Whether a
   restore fault should also stop further swaps stays NEEDS MIKE (P3).
3. **The gate's recovered off read** (`LoadStampDetailGate`): FIXED, slightly wider than proposed.
   Confirmed: `_lastLogged` held only the value, so H3 followed by a readable `false` wrote nothing.
   The gate now remembers the logged state (on, off or unreadable), which also logs H3 when a read
   throws after a readable off (the old code was silent there too: the value stayed `false`). RED
   first: the corrected oracle of `Enabled_ProviderThrows_LogsWhyOnce_AndARecoveredReadLogsTheNewValue`
   (WARN H3, INFO off, INFO on) and the new `Enabled_ProviderThrowsAfterAnOffRead_LogsWhyOnce`. The
   Key Files row, the toggle paragraph and the H2 and H3 rows say so.
4. **Dispatches per load** (feature doc, Performance): FIXED. Confirmed in v1.5.3 `Campaign.cs`: a
   new campaign runs `OnNewGameCreated` (:1607, from :1709) and `OnSessionStart` (:1710), which
   dispatches `OnSessionStart` and `OnAfterSessionStart` (:754-755); a save load runs
   `OnGameEarlyLoaded` and `OnGameLoaded` (:715-716, from :1685) then the same two (:1686). Now
   "three per new campaign, four per save load".
5. **Where the OnGameStart lines fall** (feature doc, How to read a load): FIXED. Confirmed:
   `GameManager.OnGameStart` runs at `Campaign.cs:1410`; the first campaign `LoadXML` is
   `Game.LoadBasicFiles` (`Game.cs:437-445`) through `InitializeDefaultCampaignObjects` (:1489),
   reached at :1417 (save) or through `OnNewCampaignStart` (:1426, :1545); `Game.Initialize` before it
   loads only the game texts, outside `LoadXML`. The sentence now says "before".
6. **Registry, LoadXml section**: FIXED. It now says that, toggle on or off, a crash thrown through an
   XML load shows `com.taom.mod`'s observe-only replacement frame, and that the exception is the
   engine's.

False positives: none. Not fixed: none.

### Convergence round 1 verification

- Base (branch head `f1267a98`, before any edit): `Failed!  - Failed:     1, Passed: 12480, Skipped:     2, Total: 12483`;
  the one failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey`.
- RED (LoadTimeStamps filter, the new and corrected tests against the old code, with a stub
  `LifecycleRestoreFault`): `Failed!  - Failed:     8, Passed:   112, Skipped:     0, Total:   120`.
- GREEN (same filter): `Passed!  - Failed:     0, Passed:   120, Skipped:     0, Total:   120`.
- Final full suite: `Failed!  - Failed:     1, Passed: 12486, Skipped:     2, Total: 12489`, the same
  known failure and nothing new.
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: succeeded, 2 warnings, both
  pre-existing BUTR analyzer warnings in other features (`TeamTacticProbe.cs`, `Patch88_...`).
- `python -B tools/lint_docs.py --summary --fail-on-drift`: exit 0 (10 pre-existing context_budget
  items, 0 dead links, 0 dashes).
- No new test touches an engine type outside `RequiresGame`, so no RefAsm step; no hook, validator
  or CI step changed, so no hook suite run and no gate sweep.

## Convergence round 2

The convergence reviewer read `f1267a98..e6dcb83f` and raised two LOW findings. The review workflow's
last round runs no fix pass, so the orchestrator checked both and closed them in the commit
`docs(load-stamps): v2.0.32 - record plan 040's convergence rounds`.

| # | Sev | Finding | Verdict | Resolution |
|---|---|---|---|---|
| R2-1 | LOW | Round 1 moved restore faults to their own C6 latch, so `Begin_WrapFailsAfterAStampFault_StillWarnsThatTimingIsOff`, which made its fault with `ThrowOnRestore`, now exercised C6 then C4: its name was false and nothing pinned C5 against C4, so the shared-latch defect could return unseen | Confirmed | The test now makes a real stamp fault (the first dispatch's end-tick clock read throws) and asserts the WARN lines are exactly [C5, C4]; the restore variant keeps its body under the honest name `Begin_WrapFailsAfterARestoreFault_StillWarnsThatTimingIsOff`. Proven: green (`Passed: 24`), red with C5 latched on the timing-off flag (`Failed: 1, Passed: 23`, that test), file restored byte for byte. The round-1 parenthetical about why `End_RestoreFault_NeverHidesALaterStampFault` was red is corrected |
| R2-2 | LOW | Three comments described the old behaviour: `LifecycleDispatchScope.Start`, and the H2 and H3 comments in `LoadTimeStampLines` | Confirmed against `LifecycleTimingService` Begin and `LoadStampDetailGate` | All three rewritten as the reviewer proposed |
