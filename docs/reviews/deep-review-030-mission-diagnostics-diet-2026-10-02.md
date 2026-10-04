# Deep review: plan 030, mission diagnostics diet (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: plan 030, mission diagnostics diet, behaviour-preserving: census keys, per-hit career
         lines, troll and creature-bandit gates, two dead patches
         (branch perf/030-mission-diagnostics-diet, 0d1e91f0..494eec47)
Date: 2026-10-02

Scope:   C# (MissionDiagnostic, CareerSystem, TrollBruteForce, CreatureBandits diagnostics,
         BannerColorPersistence, CompanionTactics, SubModule.cs), tests, feature docs, the Harmony
         registry and patch-target snapshot. No XML/XSLT, no scripts, no harness files.
Blast radius: the deleted IAgentColorStore, AgentColorStore, AgentColorStoreCleanupBehavior,
         Agent_EquipItemsFromSpawnEquipment_Patch and Patch35_Mission_OnTick swept for readers by
         four lenses and the lead (code, reflection strings, XML, tests, tools, docs): none left
         outside dated history. graphify not run by the lead (deletions swept by grep instead).
Waves:   Wave 1: lenses 1 to 6. Lens 7 (XML) and tooling: NOT IN SCOPE.
         Codex adversarial: not run in the first pass; run 2026-10-03 on tip 37dab128 (see "Codex round").
         Review lead (this report): verification, fixes, Step 4, RCA.

STANDARDS:     PASS on all ten checks; 1 MEDIUM, 4 LOW, all fixed (CRITICAL: 0, no escalation)
COMPATIBILITY: PASS, 18 verified, 0 incompatible, 3 unverified; 1 wrong claim in a new log line
               (fixed), 3 scope notes
EFFICIENCY:    PASS, 0 H, 0 M, 4 L (none a cost defect; 2 fixed, 2 accepted notes)
COMPLETENESS:  INCOMPLETE: no GitHub issue (drafted, needs Mike); 3 LOW and 1 NIT fixed
DATA FLOW:     PASS after fixes, 14 flows, 3 gaps, 2 inconsistencies (all fixed or documented)
DESIGN:        6 KEEP proposals (6 apply, 0 follow-up): 6 applied
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## DETAILS

Every finding below was re-read in the worktree before it was classified: the source at `494eec47`,
the base with `git show 0d1e91f0:<path>`, and the lenses' engine reads (taom-src v1.5.3 from the installed
DLLs) where the verdict turned on them. Finding ids R1 to R11 are the RCA's,
`docs/reviews/rca-mission-diagnostics-diet-2026-10-02.md`.

### Agent 1: Standards

| Finding | Verdict | Action |
|---|---|---|
| MEDIUM `no-creatures` line false after a declined spawn | CONFIRMED (R1): `NoteDeclined` writes through `TakeLine(0, "declined")` with no `Reserve` (`CreatureBanditDiag.cs:271-279`); `WriteSummary` prints on `SpawnsDeclined != 0` (`CreatureBanditDiagTicker.cs:572`) | Fixed: condition is the summary's complement; text drops the absolute clause; two tests red first |
| LOW stale Patch23 EquipItems claims | CONFIRMED (R7) | Fixed: comment line deleted, doc sentence rewritten (Agent 6 P5, P6) |
| LOW NaN polarity at `MissionDiagnosticBehavior.cs:49` | CONFIRMED (R3) | Fixed: `!(x > 0f)`; `OnMissionTick_NaNFrameTime_ClosesTheCensusOnce` red first |
| LOW career tally written only at teardown | CONFIRMED (R5): `ResetDiagnostics` has one caller, `CareerPerkMissionBehavior.OnEndMission`; `FileLogger.ProcessQueue` drains DEBUG every 50 ms | Documented (doc and field comment); a result-screen snapshot is NEEDS MIKE |
| LOW NaN damage sums | CONFIRMED (R4): the new test printed `damage NaN -> NaN` on the plan's code | Fixed: non-finite hits counted and kept out of the sums; test red first |
| Follow-ups (RequiresGame class for the D6 format tests, sort tie-breakers untested, pre-existing ADR-007 reads in `MissionDiagnosticService`, `IoC.Resolve` in the troll behavior's constructor, `/verify-bindings` after merge) | Pre-existing or owed after merge | FOLLOW-UP |

### Agent 2: Engine compatibility

| Finding | Verdict | Action |
|---|---|---|
| 1 `no-creatures` wrong; "every engine callback" true only of the seven agent callbacks | CONFIRMED (R1, R2) | Fixed with R1; "agent callback" and "mission" in code, behavior comment and `creature-bandits.md` |
| 2 four new INFO lines in every mission (census open/closed, troll latch, `no-creatures`) | Not a defect: additions, no information lost | NEEDS MIKE (simplicity trade-off) |
| 3 career summary teardown-only | CONFIRMED (R5) | As above |
| 4 `494eec47` body says "no log line changes" | CONFIRMED (R9): `LogTaomBehaviorAdded` and `LogTaomBehaviorsDone` (`BattleLoadDiagnosticsService.cs:446-456`) | Recorded in this follow-up's commit body; history not rewritten |
| 18 API reads verified; 3 UNVERIFIED (damage-model threading, native action-set table rebuild, co-op consumers of deleted public types) | Not re-run by the lead | Carried as UNVERIFIED |
| Follow-up: `MissionDiagnosticBehavior.OnEndMissionInternal` does not call `base` | Pre-existing | FOLLOW-UP |

### Agent 3: Efficiency

| Finding | Verdict | Action |
|---|---|---|
| 1 `no-creatures` false (APPLY) | CONFIRMED (R1) | Fixed (its sketch, plus `SpawnsAttempted`, mirrors `WriteSummary` exactly) |
| 2 stale Patch23 references (APPLY) | CONFIRMED (R7) | Fixed |
| 3 career aggregate clean-end only (accept) | CONFIRMED (R5) | Documented |
| 4 troll trackers' first pass shifts by up to 0.5 s in late-spawn missions | CONFIRMED as described; line content unchanged | Accepted note, no fix proposed; listed for Mike |
| Every item removes per-frame, per-agent or per-hit work; no hot-path cost added | Agreed | None |

### Agent 4: Completeness

| Finding | Verdict | Action |
|---|---|---|
| GitHub issue not filed (draft in `plans/_audit/2026-10-02-perf/issue-drafts.md:34-44`) | CONFIRMED | NEEDS MIKE (filed on the maintainer's word; filed since as #712) |
| L1 `no-creatures` contradiction | CONFIRMED (R1) | Fixed |
| L2 three stale descriptions (comment, `battle-load-diagnostics.md:46`, `companion-tactics.md:57` "5 Harmony patches") | CONFIRMED (R7, R8): `FormationPresets/Hooks/` holds 4 `Patch35_*` files at the tip, 5 at the base | Fixed |
| L3 test gaps (first-hit DEBUG literal, per-creature cap branch, declined-only case) | CONFIRMED (R11) | Fixed: two literal pins (green on the plan's code), one cap test, R1's tests |
| NIT `ResetForNewMission` comment | CONFIRMED (R10) | Fixed |
| Sub-claim "the DEBUG lane it replaces was not durable either" | Partly wrong: `FileLogger.ProcessQueue` drains DEBUG every 50 ms, so the per-hit lines were nearly durable | Folded into R5 |
| RefAsm run for the plan's untagged new tests | UNVERIFIED (not re-run: the lead added only `RequiresGame` tests) | Listed for the orchestrator |
| Follow-ups (career tests' class tag, `CareerPerkConsumerMap` console text) | Pre-existing or out of scope | FOLLOW-UP |

### Agent 5: Data flow

| Finding | Verdict | Action |
|---|---|---|
| MEDIUM `no-creatures` (hideouts and other non-field missions reach it) | CONFIRMED (R1) | Fixed |
| LOW stale Patch23 references | CONFIRMED (R7) | Fixed |
| LOW career aggregate after a clean teardown only | CONFIRMED (R5) | Documented |
| LOW ally-buff subject described wrongly (key is `victimHeroId ?? troopLeaderHeroId`) | CONFIRMED (R6): `CareerAgentStatService.cs:379`, leader from `TaomAgentApplyDamageModel.GetVictimTroopLeaderHeroId` | Fixed: comment and two doc passages; a leader-keyed ally-buff line pinned |
| LOW census close NaN polarity | CONFIRMED (R3) | Fixed |
| Follow-ups (`TrollClipTrace` strip recipe vs `TrollPresence` strings, the "i.e. one" comment at `CareerAgentStatService.cs:29`, dead `?? $"id={raceId}"`) | Pre-existing | FOLLOW-UP |

### Agent 6: Design and elegance

| Proposal | Verdict | Action |
|---|---|---|
| P1 census counters duplicate the sets' counts | KEEP, PRESERVING: `_censusLines++` only after `_seenActionSets.Add` succeeds, `_censusNewKeys++` only after `_seenActionSetKeys.Add`, all cleared together | Applied |
| P2 seven ternaries in the diagnostic multipliers | KEEP, PRESERVING: an unset term is `0f` and `1f +/- 0f` is exactly `1f` | Applied |
| P3 `no-creatures` text false | KEEP, applied as the R1 fix (text and condition) | Applied |
| P4 `_loggedHits` names a tally of every hit | KEEP, PRESERVING | Applied as `_hitTallies` |
| P5, P6 stale Patch23 comment and doc | KEEP, PRESERVING | Applied |

### Orchestrator focus: is every changed log path's information still in taom_debug.log?

| Path | Where it is now | Verdict |
|---|---|---|
| A `[MissionDiag] ActionSet` | Same text and first agent (`MissionDiagnosticService.LogActionSetSeen`); one integer key maps to one string key (`MBActionSet.GetHashCode()` returns `Index`; `RaceManager.GetRaceNameFromId` caches per id). Census open/closed lines added | Kept; closed line now also written after a NaN frame (R3) |
| B `[CareerPerks] hit amp/reduction` | First hit per (direction, subject, mask, terms) in full at DEBUG, pinned literally; every hit counted and summarized at INFO at mission teardown | Kept as an aggregate; non-finite hits counted apart (R4); teardown-only limit documented (R5) |
| C `[TrollSpacing]`, `[TrollClips]` | Unchanged writers; a troll-less mission wrote neither before; latch lines added | Kept (first pass may move up to 0.5 s earlier) |
| D `[CreatureBandits][diag]` | 22 writers, same text, kind, level and budget; refusals still counted | Kept; the new reason line is now true in every mission (R1) |
| E, F deleted members | Wrote nothing | Nothing to keep; BattleLoad's behavior count drops by one (R9) |

**Deleted members, remaining readers:** `git grep -nE "AgentColorStore|IAgentColorStore|TryGetColors|AgentColorStoreCleanupBehavior|Agent_EquipItemsFromSpawnEquipment_Patch\b|Patch35_Mission_OnTick|colorStore"` at `494eec47`, history folders excluded, finds only the dated line `banner-color-persistence.md:163`. `git grep -n Patch23 -- Main docs/features docs/reference` found the two stale EquipItems claims (fixed) and no other.

**`Main/SubModule.cs`:** `git diff 0d1e91f0 494eec47 -- Main/SubModule.cs` shows two hunks: the store resolve and two `Initialize` calls became one two-argument call (:632-640), and the cleanup behavior's three lines and blank line went (:2112-2121). Exactly plan Step 13.5; this follow-up does not touch it.

## ACTION ITEMS

1. R1 (MEDIUM): fixed.
2. R3, R4: fixed, red first.
3. R5, R6, R7, R8, R10: fixed in docs and comments.
4. R9: recorded in the follow-up commit body.
5. R11: tests added.
6. Owed: the GitHub issue (filed since as #712), the convergence pass on the fix diff (one `deep-reviewer`, which the lead cannot spawn), the RefAsm step for the plan's untagged tests, `/verify-bindings` after merge.

## IMPROVEMENTS (Step 4)

APPLIED:
- `MissionDiagnosticService.cs`: P1, `_censusNewKeys` and `_censusLines` deleted; the summary reads the two sets' counts. Proof: `LogActionSetCensusClosed_ReportsChecksNewKeysAndLines` and `ResetForNewMission_ZeroesTheCensusTotals`, green before and after.
- `CareerAgentStatService.cs`: P2, both diagnostic multipliers are plain products. Proof: the five summary tests with literal multipliers (`_AfterManyHits_`, `_BuffChangedMidBattle_`, `_AllyBuffOnlyHits_`, `_TwoCombinations_`, `_NonFiniteDamage_`), green after.
- `CareerAgentStatService.cs`: P4, `_loggedHits` renamed `_hitTallies`. Proof: `CareerAgentStatServiceTests`.
- P3, P5, P6: applied as the R1 and R7 fixes.

NOT APPLIED:
- Agent 3 finding 4 (troll first-pass phase): no change proposed; behaviour-changing in timing only (under 0.5 s), accepted pending Mike.
- Agent 2 finding 2 (four new INFO lines per mission): a product trade-off, not a defect; Mike's call.
- A result-screen snapshot of the career tally (R5): a design change (`CareerPerkMissionBehavior` is `BehaviorType.Other` and never receives `OnMissionResultReady`); Mike's call.

FOLLOW-UP (pre-existing code, no issue filed: issues are filed on the maintainer's word):
- `CareerAgentStatServiceTests` is `RequiresGame` at class level, so hosted CI never runs the tally tests; the tally needs no engine type.
- `MissionDiagnosticService` reads `Campaign`, `ModuleHelper` and `MissionBehavior` (ADR-007).
- `TrollBruteForceMissionBehavior`'s constructor calls `IoC.Resolve` although `SubModule` builds it with `new`.
- `MissionDiagnosticBehavior.OnEndMissionInternal` does not call `base`.
- `CareerAgentStatService.cs:29` "one entry per career hero, i.e. one" contradicted by the log.
- `MissionDiagnosticBehavior.cs:95` `?? $"id={raceId}"` is dead (`GetRaceNameFromId` never returns null).
- `CareerPerkConsumerMap.cs:20-30` console text does not mention the INFO summary.
- `TrollClipTrace.cs` strip recipe should name `TrollPresence`'s strings and tests.
- `HitSummaryLines` sort tie-breakers untested.
- `/verify-bindings` after 028 and 030 merge, rather than trusting the hand-edited 266.

## Verification

- Base (branch tip `494eec47`, before any edit): `Failed!  - Failed:     1, Passed: 12374, Skipped:     2, Total: 12377`; the failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey`.
- RED: the six new or changed assertions failed on the plan's code (`Failed: 6, Passed: 92` on the four touched classes), the two literal pins passed.
- GREEN, filtered (the eight classes the lenses named plus `HarmonyPatchBindingTests` and `MissionBehaviorLifecycleTests`): `Passed!  - Failed:     0, Passed:   142`.
- Final full suite: `Failed!  - Failed:     1, Passed: 12383, Skipped:     2, Total: 12386`; 12374 + 9 new tests, the same single known failure.
- Gate sweep: no hook, validator or CI step changed, so none was run.

## CODEX REVIEW

Codex not run in the first pass: no adversarial dispatch was made for plan 030, so there was no Phase 3d assessment.
It ran on 2026-10-03; its findings and their dispositions are in "Codex round" at the end of this report.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| (none) | n/a | n/a | n/a | Codex not run |

**AGENTS.md lessons (pending):** none from Codex. For the orchestrator's consolidation, the Claude-side
lessons a later Codex review of diagnostics work should look for: a reason line whose condition is not
the complement of its summary's (R1), an aggregate that sums engine floats unchecked (R4), and a second
check on an engine float written with the opposite comparison instead of the negated requirement (R3).

VERDICT: READY FOR COMMIT

## Convergence round 1

The convergence reviewer read `494eec47..b812bb82` (the review follow-ups) and raised four LOW findings.
Each was checked against the code at `b812bb82`; all four were confirmed and are fixed in the commit
`fix(diagnostics): v2.0.32 - convergence fixes for plan 030`, which follows `b812bb82` on the branch. All
four are comment and documentation corrections; no runtime line changed.

| # | Severity | Finding | Verdict | Resolution |
|---|---|---|---|---|
| C1 | LOW | The `_hitTallies` comment in `CareerAgentStatService.cs` said every tally subject is the victim's hero or party-leader id; the amplification path keys on the attacker (`attackerHeroId ?? attackerTroopLeaderHeroId`), and only a reduction victim can have the null subject | Fixed | The comment now names both paths: the victim's hero id, else its party leader's, for reduction; the attacker's hero id, else its troop's party leader's, for amplification; the null subject only for a reduction victim known by `Agent.Index` alone |
| C2 | LOW | Two test comments still said "a battle with no creature skips every engine callback" (`CreatureBanditDiagTests.cs`) and "engine callbacks" (`CreatureDiagLedgerTests.cs`); R1 and R2 had made both halves false, since `NoteMissionEnd` also stays silent after an attempted or declined spawn and only the agent callbacks exit on the gate | Fixed | Both read "agent callback(s)" and "mission"; the `no-creatures` comment names the attempted or declined condition |
| C3 | LOW | `mission-diagnostic.md` did not list `MissionDiagnosticBehaviorTests.cs` or the NaN frame time among the census summary's closes; `creature-bandits.md` did not list the per-creature cap test | Fixed | Tests section names the behavior tests (`RequiresGame`: window runs out, shorter mission, NaN frame time); the summary's "written when" list adds a non-finite frame time; the `CreatureBanditDiagTests` entry adds the per-creature cap refusing without a WARNING and counting `suppressedLines` |
| C4 | LOW | The RCA said `lessons/gamemodels-services.md` counts the NaN-gate class five times (the rule says six, the lessons file records more); the REVIEW-LOG entry said three regression tests were proven RED and named four | Fixed | The RCA cites the rule's count of six and lists the later instances (Enlistment, SettlementFood #546, the 2026-09-30 index bounds check); the REVIEW-LOG entry says four |

**Orchestrator focus, rechecked.** `Main/SubModule.cs` differs from `0d1e91f0` by exactly the four edits plan
030 Step 13.5 lists (the `IAgentColorStore` resolve and the `EquipItems` initialise removed, the
`Mission_SpawnAgent_Patch.Initialize` call shortened, the cleanup behavior registration removed).
`git grep` for `AgentColorStore`, `Agent_EquipItemsFromSpawnEquipment_Patch` and
`EquipItemsFromSpawnEquipment` outside `CHANGELOG.md`, the changelog archive, `plans/` and `docs/reviews/`
finds no code, test, XML or reflection reader of the deleted members. The remaining hits are the separate
`Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch` (a different, live patch), engine reference docs, the
dated history line in `banner-color-persistence.md`, and two dated audit snapshots
(`docs/audits/cluster-harmony-patches.md`, `docs/audits/triage-results-D.md`) that record the code as it was
when audited; they are left as historical records.

**Verification.** Full suite before and after the fixes, identical:
`Failed!  - Failed:     1, Passed: 12383, Skipped:     2, Total: 12386`, the one failure the known
`EveryLanguage_DeclaresARowForEveryEnglishKey`. No gate (hook, validator or CI step) changed, so no gate
sweep was run.

## Convergence round 2

The convergence reviewer read `b812bb82..8fee9625` and raised one LOW finding. The review workflow's
last round runs no fix pass, so the orchestrator checked it against the records and closed it in the
commit `docs(diagnostics): v2.0.32 - record plan 030's convergence rounds`.

| # | Severity | Finding | Verdict | Resolution |
|---|---|---|---|---|
| C5 | LOW | Round 1 was recorded only in this report: the RCA had no convergence section, so C1 to C4 had no Why-missed or Preventive-action entry (deep-review Phase 3e applies to every confirmed finding, LOW included), and the plan's REVIEW-LOG entry still listed the convergence pass as owed and did not mention it | Confirmed | `rca-mission-diagnostics-diet-2026-10-02.md` gains "Convergence rounds" (C1 to C5); the REVIEW-LOG entry's heading, its **Claude** line and its **Owed** line now record both passes |

No runtime line changed in round 2.

## Codex round (2026-10-03)

Codex read `0d1e91f0..37dab128`, the branch tip after both convergence rounds, and raised no P1, one P2 and three P3
observations. Each was re-read against the code and the installed v1.5.3 decompile (`taom-src`) before it was
classified. The fixes are in `fix(diagnostics): v2.0.32 - review and Codex follow-ups for plan 030` (issue #712).

| # | Codex | Verdict | Resolution |
|---|---|---|---|
| 1 | P2: the career tally is lost on an abnormal mission exit | CONFIRMED, narrowed by the orchestrator's ruling to "written on every way a mission can end; only a crash may lose it" | `ResetDiagnostics` had one caller, `CareerPerkMissionBehavior.OnEndMission`, which runs only through `Mission.EndMission` (the sole caller of `OnEndMissionInternal` is `EndMissionInternal`, `Mission.cs:4654-4657`). `GameStateManager.CleanStates` finalises a mission state with no `EndMission` first (the application shutting down through `Game.OnFinalize`, if a window close reaches it, unverified; or a mod that loads a save mid-mission through `SavedGameVM.StartGame`, since vanilla opens the load screen only from the map and the main menu), so the callbacks that ran were `OnMissionStateDeactivated` (when the state was active), every behavior's `OnMissionStateFinalized` and `OnRemoveBehavior`, and the behavior overrode none of them: the counts were lost and, the stat service being a singleton, the dead mission's tally rode into the next one, which then logged no first hit for the combination and summed both missions. `OnRemoveBehavior` now writes the summary too (nothing to write after a normal end). `CareerPerkMissionTeardownTests`: 3 red first (`Failed: 3, Passed: 4, Total: 7`); removing the try/catch fails the 2 containment tests. No periodic snapshot was added, so a crash or a process kill still loses the counts |
| 1b | P2, second half: the aggregates do not preserve every piece of old information | CONFIRMED | Checked in `TallyHit` and `HitSummaryLines`: damage 10, 20 and 70 against 10, 40 and 50 under one multiplier give the same first-hit line and the same summary. `career-system.md` now says what the summary keeps and what it drops. The plan's amendment wording ("so nothing the per-hit lines showed is lost") is the orchestrator's file and is not edited here |
| 2 | P3: the troll gate shifts the spacing tracker's refresh phase | CONFIRMED (observation) | `TrollFormationSpacingTracker.Tick` throttles itself on `_nextRefresh`, which the gate now starts at the first troll tick. The first scan after a late first troll comes up to 0.5 s sooner, never later; every later scan keeps the new phase, so a later re-space can land up to about 0.5 s sooner or later, and real formations re-space and teleport on that clock. Documented in `troll-brute-force.md` and on `TrollPresence`; no code change (ruling: document). Advancing the deadline while the latch is closed was not done: it would only delay the first response by under half a second. The plan's "changes nothing once a troll exists" is too strong; the plan file is the orchestrator's and is not edited here |
| 3 | P3: a refused creature event still formats its thread label | CONFIRMED | `WriteEvent` built `I(e.ThreadId) + "/main"` before the budget check, 88 bytes per refused event. The label is now built inside each granted branch (`ThreadLabel`). `WriteEvent_EveryKindRefusedByTheBudget_AllocatesNothing` red first (`Expected:<0>. Actual:<88000>` over 1,000 refused events); hoisting the label out of one kind's branch reads `Actual:<8800>` |
| 4 | P3: the tests prove service behavior, not engine integration | CONFIRMED (a coverage limit, not a fabricated pass) | The composition check is a source-order assertion (`AssertOnceBetween` over `SubModule.cs` text) and the census behavior tests run with no mission, as Codex says. Its concrete follow-up is done: a new term set for the same subject and mask, and hits from eight threads at once. Dropping the term bits from the key fails the first; removing the lock from `TallyHit` fails the second, 3 runs in 3. The engine loop, the Harmony application and the battle tint stay UNVERIFIED until an in-game run |

**Noted, not changed.** `MissionDiagnosticBehavior` writes the census "closed" line from `OnEndMissionInternal` only, so a
mission aborted inside the 5 s window (the application shutting down, or a mod loading a save) leaves the "open" header unpaired.
No finding raised it and no counts are lost beyond that line, so it is left for the maintainer.

**The `Mission.OnTick` completion hazard** (an exception inside `OnTick` before it launches the agent tick leaves the next
`WaitTickCompletion` waiting) is pre-existing and goes to the separate PatchShield follow-up plan. Nothing this branch
added claims otherwise: the only `Mission.OnTick` text it changed is the removal of the deleted Patch35 postfix.

**Verification.** Full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
`Failed!  - Failed:     1, Passed: 12394, Skipped:     2, Total: 12397`. That is the recorded 12386 plus the 11 tests
added here (7 teardown, 2 creature writer, 2 stat service), and the one failure is the known
`EveryLanguage_DeclaresARowForEveryEnglishKey`.

## Convergence round 3 (the Codex round's fixes)

The convergence reviewer read `37dab128..862756e7` (`fix(diagnostics): v2.0.32 - review and Codex follow-ups for plan
030`) and raised three LOW findings, all in the prose that commit wrote. Each was re-read against the code and the
installed v1.5.3 engine (`taom-src` for `Mission.cs`, `MissionState.cs`, `MBGameManager.cs`, `GameStateManager.cs`,
`Game.cs`, `Module.cs` and `SavedGameVM.cs`, plus an `ilspycmd` decompile of `SandBox.View`, `SandBox.GauntletUI`,
`SandBox` and the two `TaleWorlds.MountAndBlade` view assemblies for the menu paths). All three hold. No runtime line
changed: the fixes are in a code comment, a test summary, the feature and engine docs and the review records (issue
#712).

| # | Severity | Finding | Verdict | Resolution |
|---|---|---|---|---|
| C6 | LOW | The troll timing text (`TrollPresence.cs`, `troll-brute-force.md`, the `862756e7` body) said a troll built after the first tick is counted on the next tick, up to 0.5 s sooner, "never later". That holds for the first scan after the latch opens only. Every later scan runs on a 0.5 s phase anchored at that tick, and the first scan can change nothing (`FormationUnitDiameter` returns null below the 0.1 troll share), so a re-space that falls due later can come up to about 0.5 s later than under the old clock | Confirmed | Re-read `TrollFormationSpacingTracker.Tick` (the self-throttle, lines 42-43), `TrollBruteForceMissionBehavior.OnMissionTick` (the tracker runs only while `_presence.Seen`; at `0d1e91f0` it ran every tick) and `TrollBruteForceService.FormationUnitDiameter` (line 41). A scratch simulation of the throttle under both call patterns (not committed): first troll built at 1.20 s, old scans at 1.00, 1.50 and 2.00 s, new scans at 1.21, 1.71 and 2.21 s. The first troll is counted 0.29 s sooner (1.21 against 1.50); a formation that reaches the share at 1.75 s is re-spaced 0.21 s later (2.21 against 2.00). Over every later event time and every first-troll build time the shift runs from -0.49 s to +0.49 s, and a troll present on the first tick keeps the old scans exactly. `TrollPresence.cs` and `troll-brute-force.md` now scope "never later" to the first scan, say that every later re-space can land about 0.5 s sooner or later, and give the worked case. The same claim is corrected in this report (Codex round, row 2), RCA X3 and `lessons/misc.md`. The `862756e7` body cannot be edited, so this commit's body carries the correction; the plan's squash message (D10) is the orchestrator's file |
| C7 | LOW | The fix named "a save being loaded mid-battle" as an exit that reaches `OnRemoveBehavior` without `OnEndMission` (`CareerPerkMissionBehavior.cs`, `CareerPerkMissionTeardownTests.cs`, `career-system.md`, the lifecycle doc, `lessons/misc.md`, this report, RCA X1, the REVIEW-LOG entry and the `862756e7` Not-tested trailer). Vanilla offers no way to load a save inside a mission, so in vanilla the only exit the new write adds is the shutdown finalize, and whether a mid-battle window close reaches it is itself unverified | Confirmed | The mission escape menu (`MissionGauntletSingleplayerEscapeMenu.GetEscapeMenuItems`) lists Return to the Game, Options, Re-enable Battle UI, Cheat Menu, Photo Mode and Exit to Main Menu; Exit to Main Menu calls `MBGameManager.EndGame`, which calls `EndMission` on a live mission state (`MBGameManager.cs:202-204`). `SandBox.View` builds its mission escape menus through `ViewCreator.CreateMissionSingleplayerEscapeMenu` (33 call sites). The Load item is functional only in `MapScreen.GetEscapeMenuItems`; the character-creation and education menus show it disabled with an empty action. `SandBoxViewCreator.CreateSaveLoadScreen` has two callers, the main menu's Saved Games option and `MapScreen.OpenSaveLoad`, so `SavedGameVM.StartGame` and its `CleanStates(0)` run from the map or the main menu with no mission live. `Main` calls no save loader (its hits are diagnostic patches on `TryLoadSave` and `CleanStates`, and the parked ShaderPrecompilation runner, which starts a new game). Every place now reads "the application shutting down (if a window close reaches `CoreManaged.Finalize`, unverified), or a mod that loads a save mid-mission". The lifecycle doc says `SavedGameVM.StartGame` is reached from the map's Load or the main menu, never with a mission live, and that if the window close does not reach `CoreManaged.Finalize` the `OnRemoveBehavior` write is defensive and covers no vanilla exit. The code is unchanged: the write stays as the cheap defensive one the ruling keeps. Whether the window close reaches `CoreManaged.Finalize` stays UNVERIFIED (native); the managed chain from there is `CoreManaged.Finalize`, `Module.FinalizeCurrentModule`, `Game.OnFinalize`, `GameStateManager.CleanStates` |
| C8 | LOW | The lifecycle doc's "Teardown paths" section cited `CheckMissionEnd, 4885-4888` as the route to `EndMissionInternal`. Those lines are the `else if` branch, which only a network client reaches | Confirmed | `Mission.cs:4638-4641`: `EndMission` sets `_missionEndTime` and `NextCheckTimeEndMission` to -1 and `CurrentState` to `EndingNextFrame`. In `CheckMissionEnd` (4850), `!GameNetwork.IsClient && currentTime > NextCheckTimeEndMission` is true for a non-client, the `Continuing` block (4854-4875) is skipped and `currentTime > _missionEndTime` at 4876-4878 calls `EndMissionInternal`. The `else if` at 4885-4888 runs only when the first condition fails, which for a non-client leaves its own `currentTime > NextCheckTimeEndMission` false, so it is the client route. The doc now cites 4876-4878 for single player and a host and 4885-4888 for a client. The section's other cites (`RetreatMission` 2869, `SurrenderMission` 2885, `OnEndMissionResult` 4767, `MissionState.cs:49` and `105-107`, `Mission.cs:2216` and `4714`, `Game.cs:419`, `GameStateManager.cs:345-366`, `MBGameManager.cs:204`, `OnExitToMainMenu` :220-225) were re-read and hold |

**Why these were missed.** C6: the claim was proven for the first scan after the latch and written for every scan; the
check that bounds it is a simulation of the throttle under both call patterns, which gives the shift in both
directions. C7: the exit list came from the engine side (what calls `CleanStates`), and nobody asked which screen can
reach `SavedGameVM.StartGame` while a mission is live; the check is to read each exit's trigger from the UI side too.
C8: the cite was taken from the `else if` that follows the branch that runs; a cite for a branch states the condition
that reaches it. The RCA's convergence table (C6 to C8), the REVIEW-LOG entry's convergence summary and the plan's
squash message are the orchestrator's records, as for round 2.

**Verification.** Full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
`Failed!  - Failed:     1, Passed: 12394, Skipped:     2, Total: 12397`, the totals recorded after the Codex fixes
(the only test file touched changed a summary comment), and the one failure is the known
`EveryLanguage_DeclaresARowForEveryEnglishKey`. `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`
built with 0 errors and the 2 existing analyzer warnings, in files this branch does not touch.
`python tools/lint_docs.py --fail-on-drift` exits 0 with no dash in newly written prose. No gate (hook, validator or CI
step) changed, so no gate sweep was run.
