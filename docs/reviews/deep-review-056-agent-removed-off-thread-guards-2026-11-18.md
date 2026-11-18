# Deep review: plan 056, off-thread agent removals in MissionDiagnosticBehavior (2026-11-18)

```
DEEP REVIEW REPORT
===================
Feature: Missions: defer MissionDiagnosticBehavior's off-thread OnAgentRemoved and OnAgentDeleted
         (improve plan 056, #607)
Date: 2026-11-18
Range: c63dbb0ce..a531bea73 (branch improve/056-agent-removed-off-thread-guards), then the review
       follow-up commit on top of it

Scope:   C# (Main/Features/MissionDiagnostic, 2 files), tests (3 files), docs/features/mission-diagnostic.md.
         No XML, no hooks, no validators.
Blast radius: PendingAgentReads<T> has one production user (MissionDiagnosticBehavior); the census of
         the 16 OnAgentRemoved overrides in Main/ (git grep, below) stands in for the caller sweep.
         graphify was not run (the lead did not refresh the graph; the lenses used git grep).
Waves:   one wave: Agent 2 (Engine compatibility) and Agent 5 (Data flow). Codex not run.

STANDARDS:     SKIPPED BY TIER
COMPATIBILITY: PASS: 0 incompatible, 1 unverified doc claim (judged not a defect, below), 1 LOW (confirmed, fixed)
EFFICIENCY:    SKIPPED BY TIER
COMPLETENESS:  SKIPPED BY TIER
DATA FLOW:     PASS: 0 gaps, 2 LOW inconsistencies (trace 6 is the Agent 2 LOW, fixed; trace 7 is not a
               defect of this change), 1 doc nuance (confirmed, fixed)
DESIGN:        SKIPPED BY TIER
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## DETAILS

### Agent 1: Standards

SKIPPED BY TIER (deep-review tier 1 rule).

### Agent 2: Engine compatibility

Nine API usages verified against the installed v1.5.7 DLLs (`taom-src` cache v1.5.7-202611101605):
`MissionBehavior.OnAgentRemoved` and `OnAgentDeleted` are reached from the `[MBCallback]` methods
`Mission.OnAgentRemoved` and `Mission.OnAgentDeleted`, `OnMissionTick` runs from `Mission.OnTick`, and
`Agent.GetHashCode` returns the readonly `_creationIndex` with no `Equals` override, so a parked removal
of a deleted agent reads no native memory. The lead re-checked one load-bearing fact for the new test:
`Agent.ActionSet` is `new MBActionSet(MBAPI.IMBAgent.GetActionSetNo(GetPtr()))` (TaleWorlds.MountAndBlade.Agent.cs:722),
so on a bare agent in the test host it throws a managed `NullReferenceException` (MBAPI.IMBAgent is
null), which `ReadPendingAgents` catches.

- **A1, LOW, CONFIRMED and fixed.** After a failed read, `_readingStopped` is true and
  `ReadPendingAgents` returns before `Drain` (MissionDiagnosticBehavior.cs:80 at the fix), so nothing
  drained the deferral queue again, while `OnAgentRemoved` and `OnAgentDeleted` still parked every
  off-thread drop until `OnEndMissionInternal` cleared it. Re-read in the worktree: the latch at :88
  stops `OnAgentCreated` (:66) and the drain (:80), but not the two producers. The test
  `AfterAReadFault_AnOffThreadDrop_IsNotParked` failed first (`Expected:<0>. Actual:<2>.` off-thread
  calls noted) and passes after the fix. The plan had recorded the pile-up as accepted
  (plans/056-agent-removed-off-thread-guards.md:952-953); the lead fixed it because the fix uses the
  latch that already exists and closes it on the last two paths it did not reach.
- **A2, UNVERIFIED claim, FALSE POSITIVE as a defect.** "Native raises `OnAgentRemoved` and
  `OnAgentDeleted` on the thread it chooses" claims no observed off-thread `OnAgentDeleted`: it says
  native picks the thread, which the two `[MBCallback]` entry points prove. The guard costs nothing on
  the main thread. Only #634's log caught `OnAgentRemoved` off the main thread; no log has caught
  `OnAgentDeleted` there, and the text does not say one has.
- **Census (orchestrator note):** confirmed below under Agent 5.

### Agent 3: Efficiency

SKIPPED BY TIER.

### Agent 4: Completeness

SKIPPED BY TIER.

### Agent 5: Data flow

Ten flows traced. The off-thread path touches only the `ConcurrentQueue`, the volatile thread id and
the `ConcurrentDictionary` of reported sites; `_order` and `_members` are written only on the main
thread; parked removals run before the batch snapshot (PendingAgentReads.cs:53-55).

- **Trace 6, LOW:** the same pile-up as A1. CONFIRMED and fixed with A1.
- **Trace 7, LOW, FALSE POSITIVE for this change.** A drop parked between `_deferred.Drain()` and
  `_order.ToArray()` lands one tick late, so that tick can still read the agent. The window existed in
  the base and was wider there (the base also mutated the `HashSet` from two threads), and a read that
  throws is caught at MissionDiagnosticBehavior.cs:85-91. Closing it needs a lock on the main-thread
  read path, which the change does not call for. Not introduced here.
- **Trace 8:** `OnAgentCreated` adds inline with no thread check. The plan states the assumption
  (plan :947-951) and the lead did not verify it. Listed under FOLLOW-UP.
- **Trace 10, LOW, CONFIRMED and fixed (doc).** "the first one per callback writes one WARNING line"
  reads as once per mission; `_reportedSites` is static and only `ResetForTests` clears it
  (MissionThreadGuard.cs:22, :62-67), so it is once per game session. The doc now says "in a game
  session".
- **Census check.** `git grep -n "override void OnAgentRemoved" -- Main` prints 16 lines in the
  worktree, matching the plan's table. The lead closed the four sub-claims the lenses left open:
  `HeroKillTally` uses `Interlocked.Add` and `Interlocked.Exchange` (HeroKillTally.cs:18, :22);
  `HeroAuraLedger` is a `ConcurrentDictionary` and `Remove` is `TryRemove` (HeroAuraLedger.cs:16, :46);
  `EnlistmentMeritMissionBehavior._killed` is a `ConcurrentQueue<string>` (:41);
  `CreatureScoreboardBridge` lives at `Hooks/CreatureScoreboardBridge.cs` and keeps a
  `ConcurrentDictionary` (:26), and `CreatureBanditDiag.Events` is a `ConcurrentQueue` (:36). The
  plan's claim holds: no other `OnAgentRemoved` override writes TAOM shared state unguarded, apart from
  the `FordsOfWarMissionLogic.Fault` counter the plan already names as harmless.

### Agent 6: Design and elegance

SKIPPED BY TIER.

### Agent 7: XML and ModuleData integrity

NOT IN SCOPE.

### Tooling correctness

NOT IN SCOPE.

## ACTION ITEMS

1. Done: A1 and trace 6, skip the drop once census reads stop (MissionDiagnosticBehavior.cs:71-76),
   test first (MissionDiagnosticOffThreadTests.cs:93-105).
2. Done: trace 10, the doc names the once-per-session report (docs/features/mission-diagnostic.md:26).
3. Owed by the orchestrator: the fix-loop re-review. The fix changes runtime behaviour in a tier 1
   area (mission threading), so the deep-review fix-loop rule asks for one Codex pass at `high` plus
   Lens 2 on the fix diff. Codex is out of usage; the lead cannot spawn a lens.

## Suite evidence

- Before the fix, at a531bea73: `Failed!  - Failed:     3, Passed: 42918, Skipped:     9, Total: 42930`.
  The three failures are language-data tests (`EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems`, `EveryKey_IsSeededInAllTwelveLanguageFiles`): missing
  `TAOM_LD_*` and `taom_aso_*` rows and the word "default" in three translations. This diff touches no
  string or language file.
- After the fix: `Failed!  - Failed:     3, Passed: 42919, Skipped:     9, Total: 42931`, the same
  three failures, plus the new test passing. A first run at 153 class lines failed
  `EntryPoints_StayUnder150Lines_ExceptTheShrinkOnlyBaseline`; the fix was compacted to pass the gate.
- RefAsm no-game unit step, filtered to MissionDiagnostic:
  `Passed!  - Failed:     0, Passed:    27, Skipped:     0, Total:    27` (the new test is in a
  `RequiresGame` class and is left out of that step).
- Not covered: no game run (tier 1, as the orchestrator ruled).

## IMPROVEMENTS (Step 4)

APPLIED:     none. Agents 3 and 6 were skipped by tier, so no APPLY-scoped fix or KEEP proposal exists.
NOT APPLIED: none.
FOLLOW-UP:   no issue filed (an /improve lead files none; the orchestrator decides at wrap-up):
             - the 5 `OnAgentDeleted` overrides nobody checked against the off-thread question:
               BehaviorTreeMissionLogic.cs:318, CareerPerkMissionBehavior.cs:233,
               CreatureBanditDiagnosticsBehavior.cs:64, LongDefeatMissionLogic.cs:68,
               MountDespawnMissionBehavior.cs:86, plus the `_adapterFactory.Evict` call on
               AdvancedCombatBehavior.cs:72 (plan :936-942 calls this census UNVERIFIED).
             - `OnAgentCreated` adding inline assumes the engine raises it on the main thread
               (plan :947-951); a log that catches it off the thread needs the same deferral.
             - plans/056-agent-removed-off-thread-guards.md:952-953 still describes the pile-up this
               review removed; the plan is a handoff record and was not edited.

VERDICT: READY FOR COMMIT

## CODEX REVIEW

Codex not run for this item (out of usage; a paid call needs the maintainer's word). The Phase 3d
assessment therefore has no rows.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---------------|--------------|--------|--------|
| (none) | not run | | | |

- Confirmed bugs from Codex: none (not run).
- False positives: none (not run).
- Design questions: none.
- Things Codex missed: not applicable. The lenses found A1, trace 6 and trace 10.

### Codex lessons (pending)

None. No Codex run, so no new false-positive pattern or missed category to record.

## Records

- RCA: docs/reviews/rca-agent-removed-off-thread-guards-2026-11-18.md
- Lesson: docs/reviews/lessons/state-lifecycle-save.md, "A latch that stops a queue's drain must also stop its producers"
