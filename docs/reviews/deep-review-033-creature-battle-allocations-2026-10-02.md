# Deep review: plan 033, creature-battle allocations and scans (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: plan 033, creature-battle allocations and scans: UTC clock in creature trees,
         behaviour-tree reuse per re-entry and event, spatial grid reuse, nearby-query hunt
Branch:  perf/033-creature-battle-allocations, worktree wt-033
Diff:    d50bf962..d45774a5 (98a26c69 clock, 5e5cb7a0 reuse, 42dddf17 grid, d45774a5 hunt)
Date:    2026-10-02 (review run 2026-10-03)

Scope:   C# (13 production files, 7 test files) and 3 feature docs. No XML, XSLT, hook,
         validator or CI step. The plan file and DECISIONS.md are records, not targets.
Blast radius: graphify not run. Public surfaces checked by diff instead: the review fixes
         change only private members of BehaviorTreeMissionLogic and add one internal
         class (CallbackSkipLedger); the hunt revert restores the base file byte for byte.
Waves:   lens reports from Agents 1 to 6 (Standards, Engine compatibility, Efficiency,
         Completeness, Data flow, Design). Agent 7 (XML) and Tooling not launched.
         Review lead: verification, fixes, Step 4.

STANDARDS:     FAIL: 0 CRITICAL, 3 MEDIUM, 5 LOW, 4 NIT; 2 MEDIUM fixed, 1 MEDIUM split
               (hunt half moot after the revert, grid half needs Mike), LOWs below
COMPATIBILITY: PASS: 31 verified (23 API uses, 8 engine claims), 0 incompatible,
               2 unverified (C11 callback targets, C12 native PreTick order), 2 wrong
               engine claims (C6 plan text, C7 commit 4's cost premise)
EFFICIENCY:    FAIL: 6 issues (0 high, 2 medium, 4 low); 3 applied, 3 follow-up or
               not applied
COMPLETENESS:  INCOMPLETE: FOR-MIKE items owed (orchestrator); the GitHub issue was filed
               after this report as #715; test gaps M3 and L1 fixed; stale docs fixed
DATA FLOW:     PASS after fixes: 28 flows, 4 gaps (2 in scope: grid summary needs Mike,
               hunt summary moot), 4 inconsistencies (stale docs fixed, cost model
               reverted, native route moot)
DESIGN:        6 KEEP proposals (4 apply, 2 follow-up): 4 applied, 2 not applied
XML:           NOT IN SCOPE (no XML or XSLT changed)
TOOLING:       NOT IN SCOPE (no script under tools/ or .claude/hooks/ changed)
CODEX:         Not run for the lens review. A Codex review of the final tip ran later
               (d50bf962..06307997): 0 P1 or P2, 2 P3, both confirmed and fixed (see
               "Codex follow-up" at the end)
```

## Details

Every finding below was re-read against the worktree before it was classified. Engine reads
came from the v1.5.3 `taom-src` cache: `AgentHelper` (the four `GetAgent*` readers are
`[MethodImpl(MethodImplOptions.AggressiveInlining)]` unsafe dereferences) and `Agent.cs:642`
(`IsHuman => (GetAgentFlags() & AgentFlag.IsHumanoid) != 0`), `:706`
(`Position => AgentHelper.GetAgentPosition(PositionPointer)`), both read this run. The native
`GetNearbyAgentsAux` facts are the lenses' (four of them read the same disassembly
independently); I did not re-run the decompiler.

### Findings and verdicts

| ID | Source | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| R1 | A1 #1, A2 F1/C7, A3 #1, A5 F1, A6 P1 | MEDIUM | `d45774a5`'s stated win ("one slot walk instead of up to three native reads per hostile agent") rests on a false premise: on v1.5.3 `IsHuman`, `IsActive()` and `Position` are inlined pointer loads, not native calls (read this run, above). The native walk reads the same fields for every used slot of every team, under a static lock, plus a locked `GetManagedObjectWithId` per returned agent. Before contact (the usual state at deployment end) the commit itself says it costs more. No measurement exists | CONFIRMED | Reverted in effect (Agent 6 KEEP, Agent 3 APPLY): `CreatureHuntTask.cs` and `creature-bandits.md` are back to their `d50bf962` content and `CreatureHuntNearestTests.cs` is deleted. Behaviour PRESERVING: the base scan was the commit's own oracle (3,000 layouts, same pick). This also retires A1 #7, A5 F2/F3 (hunt half), A4 L3/F4/M2b and A2 F3. Commit 4's body cannot be corrected without a rebase; this record and the follow-up commit body carry the correction |
| R2 | A1 #2, A3 #6, A4 L6, A5 F4 (A6 dissents) | MEDIUM | `BehaviorTreeMissionLogic.cs` grew from 397 to 512 lines; half of the growth is a skip and park ledger that counts and formats log lines (ADR-002: no data transformation in an entry point), and its two pinned formats sat only in a `RequiresGame` class that hosted CI never runs (a recurrence of `lessons/testing-qa.md` "A pure rule's tests go in an untagged class") | CONFIRMED | Fixed: the enum, counters, reason line and summary moved into the internal `CallbackSkipLedger` (no interface, no IoC); the logic keeps the listener counts and the dispatch and is 485 lines. `CallbackSkipLedgerTests` (4, untagged) pins both lines without the game; the dispatch tests pin them through the logic, green before and after. Agent 6's objection (one-caller abstraction) weighed: the win is the CI-run format pins plus an entry point that no longer formats; the cost is one 65-line file. The remaining 485 lines are the pre-existing debt (397 at base) |
| R3 | A1 #3, A3 #5, A4 M2, A5 F3 | MEDIUM | D6 logging: the grid has no mission-end summary (later idle spells and the mission's skip and query-rebuild totals are never logged); the hunt logged once per creature with no aggregate | Grid: NEEDS MIKE. Hunt: moot | The grid summary needs a mission-end call in `AdvancedCombatBehavior.cs`, which plan 033 lists as a STOP file (out of scope). `advanced-combat.md` already says the summary is missing and why. The hunt line left with the revert |
| R4 | A1 #3 (config header), A3 #5 | LOW | None of the three mechanisms writes a configuration header | FALSE POSITIVE | Agreed with Agent 5: D6's header is for instruments with settings; the grid's interval and the gates have none to report, and the reason lines name the mechanism in full |
| R5 | A5 trace 11 | LOW | The dispatcher's "parked" count omits component cleanups parked through `RunOnMissionThread` | FALSE POSITIVE | The summary counts this logic's callbacks ("N callbacks skipped ...; M parked off-thread"); a component's cleanup is not one of them, and the line says nothing about it |
| R6 | A1 #4 | LOW | `SpatialGrid.LogInfoToFile` resolves `IModLogger` through the service locator in a helper that is not a boundary class | NEEDS MIKE | Injecting the logger means editing `AdvancedCombatBehavior.cs` and `WargMissionBehavior.cs`, both outside plan 033's scope; bundle with R3's summary if Mike lifts the fence |
| R7 | A1 #5, A4 L2, A5 follow-up, A6 P4 | LOW | `CreatureTreeClockTests` scanned five prefixes while its commit claims "any creature-tree node": `AdvancedCombat.BaseBehaviorTree`, `CreatureBandits.BehaviorTreeElements` (which shares the spider's stamps), `TrollBruteForce.BehaviorTreeElements` and the Elephant, Elk, Mumakil, WarRam and Animalia trees were outside it | CONFIRMED | Fixed: eight prefixes added. Proven: a `DateTime.Now` read added to `CreatureHuntTask` and `BruteForceReadyDecorator` turned the rule red (`Actual:<2>`, both named), then removed |
| R8 | A1 #6, A4 M3 | MEDIUM | The early-return gates of 10 of the 14 callbacks had no test, and nothing proved the two self values TAOM's trees use (`OnSelfRemoved`: `OnWargDied`, `OnSpiderDied`; `OnSelfIsHit`: `WargTryToGoRage`) still reach a tree listener | CONFIRMED (test gap) | Fixed: four tests deliver `OnSelfRemoved` and `OnSelfIsHit` to a tree listener on the main thread and park them off-thread; `EveryCallback_OffThread_ParksExactlyWhenOneOfItsOwnValuesHasAListener` raises every callback off-thread with a listener on each of the 20 values against a hand-read oracle. Proven: removing each self value from its gate turned its two tests red; swapping `OnFocusLost`'s value turned the table red, naming both cells |
| R9 | A1 #8, A2 F2, A3 #2, A5 F6, A6 P3 | LOW | `SpatialGridDebugService._nearby` lives in a process-lifetime IoC singleton and stayed full after each overlay frame, keeping the last frame's agents, and through `Agent.Team` their finished mission, reachable into later battles (before the change the list was a local) | CONFIRMED | Fixed: `_nearby.Clear()` after the loop. `RenderDebugVisualization_EmptiesTheBufferAfterTheScan` (IL: a `List<Agent>.Clear` after the scan) failed first, then passed |
| R10 | A4 L1 | LOW | `SelectorListReuseTests` checked list identity only; deleting both `Clear()` calls kept it green while the lists grew every frame | CONFIRMED (test gap) | Fixed: content assertions. Proven: with both `Clear()` lines removed the test failed (`Expected:<1>. Actual:<2>`) |
| R11 | A5 F7, A4 L7 | LOW | The production two-map swap over one spare stack was untested with content | CONFIRMED (test gap), pure half | Fixed: `BuildCellsInto_TwoMapsOverOneSpareStack_NeverTouchesOrSharesWithThePublishedMap` (six rounds, swap as `Rebuild` does). Proven: with `cells.Clear()` removed it failed. The instance wiring (index swapped with its map, `RemoveNow` on the published pair) needs live agents: NOT APPLIED, correct by reading per three lenses. Superseded by Codex finding 2: bare agents and a position and liveness seam make it testable, and the swap could be deleted with every grid test green (see Codex follow-up) |
| R12 | A4 L4, A5 F5 | LOW | `elephant.md:366` and `bannerlord-together-compat.md:178` still named `DateTime.Now` | CONFIRMED | Fixed (identifier only; the clocks are still wall-clock, so the pause point stands) |
| R13 | A1 follow-up, A4 F3 | LOW | `warg-combat.md:153` said "Grid updates: Every 5 ticks" (stale before this branch, doubly stale after the skip rule) and a blank line split its changelog list | CONFIRMED | Fixed both |
| R14 | A2 C6, A5 F2, A4 F4 | LOW | The plan's maintenance note names `IMBAgent.GetPosition` as the method `Agent.Position` calls; the true route is `IMBMission.CreateAgent`'s `PositionPtr` | MOOT | The hunt that depended on it is reverted. The plan is a record; the orchestrator may strike the note |
| R15 | A4 M1 | MEDIUM | No GitHub issue; the draft in `issue-drafts.md` still promises the howdah skeleton fix plan 033 excluded | NEEDS MIKE | Orchestrator files it (standing rule 11 forbids me); edit the draft first, and drop the hunt line. Done after this report: filed as #715, and its summary names neither the howdah fix nor the hunt change |
| R16 | A4 L8 | LOW | FOR-MIKE lacks the plan's orchestrator items (howdah `GetSkeleton`, `GridUpdateInterval`, the plan 028 hunt measurement) | NEEDS MIKE | Orchestrator's record; the hunt measurement now applies only if commit 4 is to be re-landed |
| R17 | A4 L5 | LOW | `warg-combat.md` was committed in `5e5cb7a0` though it is not in the plan's Scope list | Recorded | The edit documents the D6 lines and is accurate; no change. Listed so the deviation is on record |
| R18 | A5 F6a, A4 F1 | LOW | Through the static `SpatialGrid.Instance`, `_agents`, the spare map and both indexes keep the finished mission's agents reachable until the next mission | FALSE POSITIVE (as a new defect) | The published `_grid` already held those agents, and through `Agent.Team` the whole mission, until the next `AdvancedCombatBehavior` replaces the instance; the new references end at the same moment. Pre-existing retention: FOLLOW-UP. Narrowed by Codex finding 1: that holds for live agents, not for an agent deleted before the mission ended (its handle in the spare map was new retention; fixed, see Codex follow-up) |

Considered and accepted, not findings: an off-thread grid reader can now see a build older than
2 s (that path is the tripwire's reported regression, and every reader in Main is on the mission
tick); a query rebuild after a skip gives a reader fresher cells than the old schedule did (never
older); off the main thread, an event raised while nothing listens is dropped where the old code
parked it, which differs only if a first listener for that value subscribes between the
asynchronous agent tick and the next drain (documented in the code and in `warg-combat.md`; TAOM
subscribes three values, `OnSelfRemoved`, `OnSelfIsHit` and `OnAgentRemoved`, all from tree
construction or decorator entry on the mission thread).

### Orchestrator focus

1. **Clock (`98a26c69`): no defect.** Five lenses traced every writer and reader of the sleep,
   wait, cooldown and rage stamps; all moved to `UtcNow` together. Stamps are per-tree blackboard
   values, nulled at build and never persisted, so no value stored under the old clock meets the
   new one; UTC has no daylight-saving step and subtraction across midnight is exact. The rule
   that guards it now covers every creature-tree namespace (R7).
2. **Reuse (`5e5cb7a0`): no defect.** Each `Selector` belongs to one node of one tree; `Prepare`
   leaves the same contents a fresh list would (now pinned, R10); event delivery only sets flags,
   so nothing re-enters `Prepare` mid-walk. `_listenerCounts[k] == actions[k].Count` holds
   (increment on Add, decrement only on a successful Remove, both cleared at mission end).
   Nothing new is keyed on `Agent.Index`: `_cellOf` keys on the Agent object (hash
   `_creationIndex`, no `Equals` override).
3. **Grid (`42dddf17`): no defect for main-thread readers.** Every reader goes through
   `GetAgentsInRadius(Vec3, float, List<Agent>)`; a skip arms a rebuild for the next main-thread
   query from the live `AllAgents`, so no later reader in the same frame sees an older build than
   the old schedule gave; deleted agents leave the published map through the index, and a rebuild
   filters `IsActive()`. The overlay buffer retention is R9 (fixed).
4. **Native claims (`d45774a5`): the equivalence held, the cost premise did not.** Four lenses
   read `IMBMission.GetNearbyAgentsAux` (RVA 0x6F70E0) on the installed v1.5.3 native DLL: 15.0f
   threshold at RVA 0xB2E010, the `0x800` humanoid flag and Active state filter, an inclusive
   horizontal test on the field `IMBMission.CreateAgent` hands `Agent.Position`, and the r minus
   0.5 m margin argument. UNVERIFIED: the two per-result callback targets (C11) and native
   `OnPreTick` ordering (C12). The commit is reverted on R1, so none of this is load-bearing now.
5. **ADR-002:** R2 (fixed). The listener-count gate belongs in the logic; the ledger does not.
6. **D6:** dispatcher compliant (reason line plus summary); grid summary needs Mike (R3); hunt
   line gone with the revert; config header not applicable (R4).
7. **Known failures:** matched exactly (totals below).

### Agent results

- **Agent 1 Standards:** findings 1 to 8 are R1, R2, R3/R4, R6, R7, R8, R1 (hunt growth, moot),
  R9. NITs: test names that do not follow MethodName_State_Expected (plan-dictated; not renamed),
  the warg changelog blank line (fixed, R13), the enum-length assumption (holds, 0 to 13, carried
  into the ledger unchanged), `Interlocked.Exchange` per skip (not applied: skip path only).
  CRITICAL: 0, so no adversarial escalation.
- **Agent 2 Engine compatibility:** F1 is R1, F2 is R9, F3 is R14. All 23 API uses verified,
  nothing incompatible.
- **Agent 3 Efficiency:** #1 R1 (applied as the revert), #2 R9 (applied), #3 FOLLOW-UP, #4 NOT
  APPLIED (CHANGING), #5 R3, #6 R2 (applied).
- **Agent 4 Completeness:** M1 R15, M2 R3/R4, M3 R8, L1 R10, L2 R7, L3 R14, L4 R12, L5 R17, L6 R2,
  L7 R11, L8 R16, N1 not applied (the "1 scheduled rebuilds" wording is pinned by
  `SpatialGridDormancyTests` and the doc; cosmetic).
- **Agent 5 Data flow:** F1 R1, F2 R14, F3 R3, F4 R2, F5 R12, F6 R9 and R18, F7 R11.
- **Agent 6 Design:** P1 revert applied (R1), P2 overlay deletion FOLLOW-UP, P3 `_nearby.Clear`
  applied (R9), P4 clock prefixes applied (R7), P5 mission clock NOT APPLIED (CHANGING), P6
  `ReferenceIdentity` applied.

## Action items

1. Mike: re-land commit 4's hunt only with a plan 028 profiler measurement before and after
   contact; until then it stays reverted (R1).
2. Mike: decide whether a follow-up may touch `AdvancedCombatBehavior.cs` for the grid's
   mission-end summary and an injected logger (R3, R6).
3. Orchestrator: file the issue from a corrected draft (R15; filed as #715), add the FOR-MIKE items
   (R16), run the Step 4 convergence pass on this fix diff (I cannot spawn a reviewer).

## Improvements (Step 4)

APPLIED:
- `CreatureHuntTask.cs`, `creature-bandits.md`, `CreatureHuntNearestTests.cs`: back to
  `d50bf962` (Agent 6 P1, Agent 3 #1; deletion holding parity). Proof: full suite green except
  the known failure; the CreatureBandits tests are unchanged.
- `SpatialGridDebugService.cs:26`: `_nearby.Clear()` after the overlay loop (Agent 3 #2, Agent 6
  P3). Proof: `RenderDebugVisualization_EmptiesTheBufferAfterTheScan`, red then green.
- `CallbackSkipLedger.cs` (new) and `BehaviorTreeMissionLogic.cs`: the skip and park ledger
  extracted (Agent 3 #6). Proof: the two dispatch format tests green before and after;
  `CallbackSkipLedgerTests` (4) run in the RefAsm step.
- `CreatureTreeClockTests.cs:19-35`: eight prefixes (Agent 6 P4). Proof: mutation red, above.
- `SpatialGridReuseTests.cs`: the private `ReferenceComparer` replaced by
  `TAOM.Core.Collections.ReferenceIdentity` (Agent 6 P6). Proof:
  `BuildCellsInto_SecondBuild_TakesItsListsFromTheSpareStack` green.

NOT APPLIED:
- All creature-tree clocks to `Mission.CurrentTime` (Agent 6 P5, Agent 3 #4): behaviour
  CHANGING (pause and slow motion); Mike's call.
- Grid mission-end summary and injected logger (Agent 3 #5, Agent 1 #4): need
  `AdvancedCombatBehavior.cs`, a plan 033 STOP file.
- `SpatialGrid` instance-wiring test (Agent 4 L7, second half): needs live `Agent` objects. Done
  after the Codex review, on bare agents through a position and liveness seam (see Codex follow-up).
- NITs: test renames, the `Volatile.Read` pre-check in `Skip`, the "1 scheduled rebuilds"
  wording (pinned).

FOLLOW-UP (pre-existing or outside the diff; no issue filed by me, standing rule 11):
- Delete the Alt-key debug overlay: `MBDebug.RenderDebugSphere` is
  `[Conditional("_RGL_KEEP_ASSERTS")]` (Agent 6's decompile of the installed `TaleWorlds.Engine.dll`,
  `MBDebug.cs:280-281`, which I read), so
  it draws nothing and only re-arms grid rebuilds while LeftAlt is held (Agent 6 P2, Agent 3 #3).
- Static `SpatialGrid.Instance` keeps the last mission's agents until the next mission (R18).
- Retired trees' constant listeners are never unsubscribed, so `_listenerCounts` never returns to
  zero once a creature tree has subscribed (Agent 5).
- `SubscriptionPossibilities.OnAgentDeleted` has no dispatch site (Agent 5; plan lists it out of
  scope).
- `GetNearbyAgentsAux` may repeat the 40th id at a page boundary (Agent 1 read it at instruction
  level; Agents 3, 4, 5 UNVERIFIED): a probe for per-entry consumers (elephant trample, DreadAura,
  SignatureStrikes), recommended for `/investigate`.
- Engine doc §4 (`mission-frame-threads-and-native-costs.md`): Agent getters are cached pointer
  reads, worth a line now that this review leaned on it.

VERDICT: READY FOR COMMIT (every confirmed defect fixed; R3, R6, R15, R16 wait on Mike or the
orchestrator)

## Verification

- Base (branch HEAD `d45774a5`, before any edit): `Failed! - Failed: 1, Passed: 12385, Skipped:
  2, Total: 12388` (`EveryLanguage_DeclaresARowForEveryEnglishKey`).
- Final: `Failed! - Failed: 1, Passed: 12385, Skipped: 2, Total: 12388`, the same single known
  failure (11 hunt tests removed; 11 added: 5 dispatch, 4 ledger, 1 debug-service IL rule and 1
  two-map reuse test; the clock and selector tests only gained prefixes and assertions).
- RefAsm unit step (CI's filter, game variables unset): `Failed! - Failed: 3, Passed: 10050,
  Skipped: 29, Total: 10082`; the three are the known `EveryLanguage`, `Patch93` and `Patch94`
  failures. The executor's final run was 10,045 passed of 10,077; the five more are the four
  ledger tests and the debug-service IL test. The deleted hunt tests were `RequiresGame`.
- `python tools/lint_docs.py --fail-on-drift --dash-base d50bf962`: exit 0, no new dashes.
- Hook suite not run: no hook, `tools/test_hooks.sh` or gate changed, so the gate sweep is empty.

## CODEX REVIEW

Codex not run: no Codex adversarial review was dispatched for this item, so Phase 3 had no review
file to verify.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | | | | Codex not run |

That was true for the lens review. A Codex review of the final tip ran afterwards; its two P3
findings are classified in "Codex follow-up" below.

AGENTS.md lessons (pending), for the orchestrator to consolidate:
- Look harder here: a perf commit's "native call" count must be read from the getter's body; on
  v1.5.3 `Agent.Position`, `IsHuman` and `IsActive()` are inlined pointer reads (R1).
- Look harder here: a test that pins object reuse must also pin contents; identity alone passes
  when the clear is deleted (R10).

## Convergence round 1

Scope: `d45774a5..7021101b`. Two LOW findings, both confirmed against the code and fixed in
the commit `fix(behavior-trees): v2.0.32 - convergence fixes for plan 033`, the one after
`7021101b` on this branch. No production code changed; both are test gaps.

| # | Severity | Finding | Verdict | Outcome |
|---|---|---|---|---|
| C1 | LOW | `SelectorListReuseTests`' `childrenWithTasks` content assertion could never fail: the tree's only selector child was an undecorated task, so `Prepare` never adds to that list, and deleting `childrenWithTasks.Clear()` (`BehaviorTreesNodes.cs:135`) alone left the test green. R10's "now pinned" and the `7021101b` body overstated it | CONFIRMED: `BTNode.Decorator` is `null` unless overridden (`BehaviorTreesNodes.cs:12`), `BTTask` does not override it, and the only add is `:142-143` | Fixed: the selector gains a second child, a selector behind a `BTEventDecorator` that never opens, so every `Prepare` files it as event-waiting; the first child still finishes the selector, so the gate's listener is never subscribed. The test now expects one event-waiting child and asserts the gated child never ran. Proven line by line: deleting only line 135 failed with `Expected:<1>. Actual:<2>. Selector.Prepare kept the previous entry's event-waiting children`; deleting only line 134 failed with `Expected:<1>. Actual:<2>. Selector.Prepare kept the previous entry's executable children` |
| C2 | LOW | The clock rule's prefixes missed the two tree classes outside an elements namespace: `CreatureBanditBehaviorTree` (`TAOM.Features.CreatureBandits`, owner of the spider-shared `DateTime?` stamps) and `TrollBruteForceBehaviorTree` (`TAOM.Features.TrollBruteForce`). The `7021101b` body, focus 1 and the REVIEW-LOG entry claimed every creature-tree namespace | CONFIRMED: the prefixes ended at `.BehaviorTreeElements.`; both classes write only `null` today and no `DateTime.Now` exists in either feature folder, so no clock was mixed | Fixed: prefixes `TAOM.Features.CreatureBandits.CreatureBanditBehaviorTree` and `TAOM.Features.TrollBruteForce.TrollBruteForceBehaviorTree`, with no trailing dot so nested closures match. All nine tree classes deriving from `BehaviorTree` under `Main/` now fall under a prefix. Proven: a `DateTime.Now` probe in each class turned the rule red, naming both (`Expected:<0>. Actual:<2>`) |

Verification: the mutations were applied to saved copies and restored byte for byte by a scratch
script, never by git. Full suite before and after: `Failed! - Failed: 1, Passed: 12385,
Skipped: 2, Total: 12388`, the known `EveryLanguage_DeclaresARowForEveryEnglishKey` only. No
hook, validator or CI step changed, so the gate sweep is empty. With C2 fixed, the earlier
claims that the rule covers every creature-tree namespace now hold.

## Convergence round 2

The convergence reviewer read `7021101b..066e129f` and found nothing. The review workflow's last
round runs no fix pass, so the orchestrator recorded both rounds in the RCA ("Convergence rounds")
and the plan's REVIEW-LOG entry, in the commit
`docs(behavior-trees): v2.0.32 - record plan 033's convergence rounds`. The full suite at
`066e129f`, run by the orchestrator: `Failed! - Failed: 1, Passed: 12385, Skipped: 2, Total: 12388`,
the known `EveryLanguage_DeclaresARowForEveryEnglishKey` only.

## Codex follow-up

Scope: `d50bf962..06307997`. Codex read the branch through git refs, ran no build or test, and found
no P1 or P2 defect and two P3 findings. Each was re-read against the code and the installed v1.5.3
engine (`taom-src`) before it was classified; the fixes are in the commit that follows `06307997`.
The Codex report is the orchestrator's `scratch/codex/final/033.md`, outside the repo.

| # | Severity | Finding | Verdict | Outcome |
|---|---|---|---|---|
| X1 | P3 | `RemoveNow` clears only the published map and its index, so an agent that sat in both of the last two builds stays in the spare map after it is deleted, and while nothing queries the grid no rebuild clears the spare | CONFIRMED: `Mission.OnAgentDeleted` drops the agent from `AllAgents` and calls `Agent.OnDelete`, which sets `_isDeleted` and clears `MissionPeer` but leaves `Mission` set (`Agent.cs:5156-5160`); `EndMissionInternal` calls `Clear()`, which nulls `Mission`, only for agents still in `AllAgents` (`Mission.cs:4662-4666`). The base emptied its only map on every removal and dropped the old map at every build | Red first: `Remove_AfterTwoBuildsThenAnIdleSpell_LeavesNeitherMapGenerationHoldingTheAgent` failed with `a map generation still holds the deleted agent`, and `Remove_AfterEachOfSeveralBuilds_DropsTheAgentFromBothMapsAndKeepsEveryAnswerRight` with `round 1: a map generation still holds agent 1`; no earlier grid test fails without the fix (first row of the table below). Fixed: `RemoveNow` removes the agent from the published and the spare pair, two indexed cell removals, still O(cell). A deleted agent's handle in the static `SpatialGrid.Instance` was new retention (the base held no deleted agent) and kept the finished mission reachable through `Agent.Mission` until the next `AdvancedCombatBehavior` replaced the grid. The fix restores parity with the base; it does not release the mission. The agents the last build left in the published map (survivors, and agents killed but not deleted) keep `Agent.Team`: `Agent.Clear()` nulls only `Mission` and the native pointers (`Agent.cs:5228-5244`), the engine's only `SetTeam(null)` call is the one for a deleted agent (`Mission.cs:3001`), and `Team.Mission` is set once in the constructor (`Team.cs:209`) and never cleared (`Team.Clear()` calls only `Reset()`, `Team.cs:328-338`). So the grid still reaches the finished mission through them until the next mission's `AdvancedCombatBehavior` replaces it: R18 as recorded, still open. What this changes in R18 is only its last clause: "the new references end at the same moment" did not hold for a deleted agent's handle |
| X2 | P3 | The grid tests do not prove a non-empty wake-up or the instance wiring: the dormancy test used empty lists and read only `BuildCount`, the reuse tests took `BuildCells` (the `BuildCellsInto` under test) as their oracle, and the two-map test made the swap itself | CONFIRMED: shown first by mutation, so the gap is measured, not argued: with the index swap deleted from `Rebuild`, with the map swap deleted, and with the wake-up rebuild moved after the answer, all 25 earlier grid tests still passed | Fixed: `SpatialGrid` reads an agent through two internal seams, `PositionOf` and `IsLive` (the engine reads by default; `InfoLog` is the same kind of seam for the log), plus a test inspector `HeldAgents()`. `SpatialGridWiringTests` (8) drives the real instance on bare agents, the pattern of `SpatialGridRemovalTests`. The reuse tests now compare against cells worked out in the test (`ExpectedCells`, a `GroupBy` on the floored coordinates), and `BuildCellsInto_ReusedContainers_MatchAFreshBuild` is renamed `..._MatchTheCellsThePositionsName` to match. The generic helpers were not wrapped in a state holder: two properties and an inspector cost less than moving the orchestration |

Every guard was proven by changing the line it covers in `SpatialGrid.cs`, running the grid tests,
and copying the saved original back, compared byte for byte after each run (never a git restore).
Failures among the 37 grid tests (25 earlier, 8 new, 4 for the debug overlay), one run per mutation:

| Mutation | Earlier tests failing | New or changed tests failing |
|---|---|---|
| `RemoveNow` back to the published pair only (X1) | none | 3 |
| `Rebuild`: the index swap deleted | none | 4 |
| `Rebuild`: the map swap deleted | none | 6 |
| Query: the wake-up rebuild removed | 2 | 3 |
| Query: the wake-up rebuild moved after the answer | none | 3 |
| Query: every query rebuilds | 1 | 1 |
| `Remove` edits the maps on the calling thread | 1 | 1 |
| `HeldAgents()` returns nothing (the vacuity guards) | none | 3 |
| `RemoveFromCells` also drops the cell's first remaining item | 1 | 2 |
| `BuildCellsInto` buckets by truncation, not `Math.Floor` | 1 | 3 (the earlier reuse tests, with `BuildCells` as oracle, passed 5 of 5) |

Codex's known-suspect table marks four items CONFIRMED in a qualified sense; none needs more than the
two fixes above.

- 12 (lifecycle, stale state): partly confirmed, and the part is X1.
- 13 (tests passing without proof): confirmed only as X2. The `Assert.Inconclusive` branches in
  `CreatureTreeClockTests` (game assemblies not loaded) and the dispatch allocation tests (probe
  not bound) report Skipped, never Passed. They ran here: those groups plus the ledger and selector
  tests finished 25 passed, 0 skipped, and the suite's two skips are the base's `WargAttack_*` pair.
- 9 (scope): the literal expansion is confirmed (the ledger and the review files are outside the
  plan's file list); the ledger is on record as R2 and `warg-combat.md` as R17, and the review
  files are this review's own output.
- S2 (a first listener missing an off-thread event raised while nothing listened): the qualified
  difference the comment in `BehaviorTreeMissionLogic.cs` and `warg-combat.md` already state.

Left alone, as Codex classed them: the overlay buffer's clear sits after the loop rather than in a
`finally` (no throwing route is known), `warg-combat.md` says `taom_debug.log` for the timestamped
`Logs/taom_debug_<timestamp>.log` (shorthand), and the plan's `IMBAgent.GetPosition` note (R14).

Verification: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` at the fix:
`Failed!  - Failed:     1, Passed: 12393, Skipped:     2, Total: 12396, Duration: 27 s - TAOM.Tests.dll (net472)`.
The one failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey`; the two skips are the base's
`WargAttack_FastWarg_InvokesRunningAttack` and `WargAttack_SlowWarg_InvokesStandingAttack`. Eight tests
were added (12388 to 12396) and none removed. The Python suite (`python -m unittest discover -s tools/tests -t .`)
ran 2962 tests with `FAILED (failures=3, skipped=8)`, the three known ones: `test_applying_every_spec_is_a_no_op`,
`test_the_committed_career_file_is_what_the_rule_derives` and `test_default_is_on_the_e_drive`. Not run: the
game; the retention needs a live mission, and the two native reads behind the seam (`Agent.Position`,
`IsActive()`) stay unrun.

## Convergence round 3

Scope: `06307997..aa2f6437`, the Codex follow-up fix. The convergence reviewer traced it statically, running no
mutation and no test, and found three LOW findings. Each was re-read against the code and the installed v1.5.3
engine (`taom-src`) before it was classified; the fixes are in the commit that follows `aa2f6437`.

| # | Severity | Finding | Verdict | Outcome |
|---|---|---|---|---|
| C3 | LOW | Two reset lines in `Rebuild`, `_queriedSinceBuild = false;` and `_skippedRebuilds = 0;` (`SpatialGrid.cs:100-101`), could each be deleted with every grid test green; and the two vacuity guards in `SpatialGridWiringTests` (W6, W7) tested membership in the union of both map generations, so W7 could not see a worker thread edit one generation | CONFIRMED, a gap that came in with `42dddf17`, not one the Codex fix introduced. Measured first on the 37 grid tests as they stood: deleting either line left 37 of 37 green; a `Remove` that edits the published generation on the calling thread before parking the rest also left 37 of 37 green; and W6, run alone against a query that no longer marks its build read, passed with a single generation holding the agent. The first line matters because without it, once anything queries, no scheduled rebuild is skipped again that mission (plan 033's saving is gone); the second because without it, after the grid's first skip, every main-thread scan rebuilds the whole grid from `AllAgents` before answering, and `NoEnemyCloseDecorator` scans on every tick of every ridden warg. Both are the commonest states of the two dormancy guards, which the skip-guard exhaustion rule in `.claude/rules/tests.md` asks to pin | Fixed: `GetAgentsInRadius_AfterAQueryRebuild_DoesNotRebuildAgain` (update, update, query, query: 2 builds) and `UpdateGrid_AfterAQueriedBuildThenAnUnqueriedOne_SkipsAgain` (update, query, update, update: 2 builds), on empty lists like their siblings; W6 and W7 now count the agent in `HeldAgents()` and expect 4 (a cell entry and an index entry in each of the two maps), so they prove both generations as their messages say. Red first, one mutation per run: see the table below. Not added: the optional wiring form (spawn after a wake-up query, query again, expect no find); it pins the line the first new test already pins, and that test kills its mutation alone |
| C4 | LOW | The coverage-gap line in `advanced-combat.md` said `SpatialGrid`'s `Agent`-typed wrappers run in `SpatialGridWiringTests`, so only the two native reads go unrun | CONFIRMED: the wiring tests execute `UpdateGrid`, the buffered `GetAgentsInRadius`, `Remove` and `ApplyPendingRemovals`. No test executes a `GetNearAliveAgentsInRange` overload, the entry point of every production scan (its hits in `SpatialGridDebugServiceTests` and `WargTickCostTests` are IL scans, plus a control fixture that is never executed), or the allocating `GetAgentsInRadius(Vec3, float)`; the two overloads that take an `Agent` also read `target.Position` outside the seam (`SpatialGrid.cs:254,265`), so they cannot run on a bare agent | Fixed in the doc: the line names what runs and what does not. Not changed in code: routing `target.Position` through `PositionOf` to cover a one-line delegation fails the simplicity criterion |
| C5 | LOW | The new `RemoveNow` comment, `advanced-combat.md` (the solution paragraph and the 2026-10-04 changelog entry) and X1's outcome above overstated the fix: the comment said a deleted agent's handle kept the mission reachable "until a query woke it", and the wording read as if the grid's hold on the finished mission were closed | CONFIRMED on both counts. No query reaches a finished mission's grid: every caller (`PeriodicallyCheckIfCanAttackAnyone`, `NoEnemyCloseDecorator`, `SpiderEngageDecorator`, `AgentAdapter`, `SpatialGridDebugService`) works on live agents inside a mission, and `AdvancedCombatBehavior`, added to every mission (`SubModule.cs:2051`), replaces `SpatialGrid.Instance` in its constructor (`AdvancedCombatBehavior.cs:22`). And the fix does not change what reaches the mission: `Agent.Clear()` nulls `Mission` and the native pointers but not `Team` (`Agent.cs:5228-5244`), the engine's only `SetTeam(null)` call is the one for a deleted agent (`Mission.cs:3001`), `Team.Mission` is set once (`Team.cs:209`) and `Team.Clear()` only resets (`Team.cs:328-338`), so the surviving and the killed-but-undeleted agents of the last build still reach the mission through `Agent.Team`. That is R18, still open; the fix restores parity with the base, which held no deleted agent | Fixed: the comment in `SpatialGrid.cs`, both places in `advanced-combat.md` and X1's outcome now say that the grid no longer holds a deleted agent's handle, that this matches the base, and that the surviving agents' `Team` still holds the mission until the next mission's `AdvancedCombatBehavior` replaces the grid (written "the next battle" at first, corrected in round 4, C6). The commit body of `aa2f6437` says "until the next battle replaced the grid" too |

Failures among the grid tests (37 before, 39 now), one run per mutation of `SpatialGrid.cs`, the saved original
copied back and compared byte for byte after each run (never a git restore):

| Mutation | Before (37 tests) | Now (39 tests) |
|---|---|---|
| `_queriedSinceBuild = false;` deleted from `Rebuild` (M1) | none | 1: `UpdateGrid_AfterAQueriedBuildThenAnUnqueriedOne_SkipsAgain`, `Expected:<2>. Actual:<3>` |
| `_skippedRebuilds = 0;` deleted from `Rebuild` (M2) | none | 1: `GetAgentsInRadius_AfterAQueryRebuild_DoesNotRebuildAgain`, `Expected:<2>. Actual:<3>` |
| `Remove` also edits the published generation on the calling thread (M3) | none | 1: `Remove_OffTheMainThread_ReachesBothMapsOnlyWhenTheMissionTickAppliesIt`, `Expected:<4>. Actual:<2>` |
| A query no longer marks its build read, `_queriedSinceBuild = true;` deleted (M4) | W6 alone: passed | 5 failing, W6 among them at its precondition (`Expected:<4>. Actual:<2>`, "both generations hold the agent before it is deleted") |

A sweep of the other lines of the two guards, run on the 37 earlier tests, found each caught by one of them: the
first-build condition dropped (7 failing), never skipping (4), an uncounted skip (5), a query that always rebuilds
(2), an off-thread rebuild (1), the live list never recorded (5), a skip that falls through to the rebuild (3). One
survives and stays unpinned: dropping the query's `_agents != null` check. Only `UpdateGrid(null)` followed by a
query reaches it, and both callers pass `Mission.Current.AllAgents`, so no test was added.

AGENTS.md lessons (pending), for the orchestrator to consolidate:
- Look harder here: a mutation sweep over the lines a fix changed leaves the rest of a state machine unproven. For
  every flag that one call sets and another clears, check that some test crosses the clear (here, a read build that
  goes idle again, and a woken grid that is read twice).
- Look harder here: before a comment, changelog or report says a fix releases an object, trace each reference path
  from the static root. For an `Agent` the engine clears `Mission` at mission end but leaves `Team`, and
  `Team.Mission` keeps the mission.

Verification: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` reports `Build succeeded`
with 0 errors. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` at the fix:
`Failed!  - Failed:     1, Passed: 12395, Skipped:     2, Total: 12398, Duration: 26 s - TAOM.Tests.dll (net472)`.
The one failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey`; the two skips are the base's
`WargAttack_FastWarg_InvokesRunningAttack` and `WargAttack_SlowWarg_InvokesStandingAttack`. Two tests were added
(12396 to 12398) and none removed. The Python suite (`python -m unittest discover -s tools/tests -t .`) ran 2962
tests with `FAILED (failures=3, skipped=8)`, the three known ones. Not run: the game; the retention needs a live
mission, and the two native reads behind the seam (`Agent.Position`, `IsActive()`) stay unrun.

## Convergence round 4

Scope: the round 3 fix, `a6b24ac9`. A review of that tip left two LOW findings, one on wording the fix itself wrote
and one on a test gap older than it. Each was re-read against the code, the first also against the installed v1.5.3
engine (`taom-src`), before it was classified; the fixes are in the commit that follows `a6b24ac9`. No creature
decision changes.

| # | Severity | Finding | Verdict | Outcome |
|---|---|---|---|---|
| C6 | LOW | C5's rewording said the grid holds the finished mission "until the next battle" replaces it: the `RemoveNow` comment, `advanced-combat.md` in two places, X1's outcome and C5's own outcome. A mission of any kind replaces the grid, a town visit included, so the text overstated how long R18's retention lasts and contradicted R18 ("until the next mission") and `advanced-combat.md` ("makes a new grid for every mission") | CONFIRMED: `SubModule.OnMissionBehaviorInitialize` has no return or mission-type gate before `AddTaomBehavior(new AdvancedCombatBehavior())` (`SubModule.cs:2051`), the constructor runs `SpatialGrid.Instance = new()` (`AdvancedCombatBehavior.cs:22`), and the only other write is the `??=` fallback in `WargMissionBehavior.cs:61`. The engine calls `OnMissionBehaviorInitialize` for every mission: `MissionState.TickLoading` calls `FinishMissionLoading` once loading ends, which calls `Mission.AfterStart` (`MissionState.cs:231`, `:345`), which calls it on every submodule (`Mission.cs:3829-3832`), and the town centre, village, arena, hideout, battle, siege and conversation missions are all opened through `MissionState.OpenNew` (`SandBoxMissions.cs`). C5's own verdict already said "added to every mission" | Fixed: the five places now say the next mission. The comment, the changelog entry, X1's outcome and C5's outcome name the next mission's `AdvancedCombatBehavior`; the solution paragraph adds that a town visit counts. The commit bodies of `aa2f6437` and `a6b24ac9` carry the same wording and are not edited |
| C7 | LOW | No grid test pins `cellOf.Clear();` in `BuildCellsInto` (`SpatialGrid.cs:147`): deleting it left all 39 grid tests green. A container's agent-to-cell index would then keep the entries of agents that a later build into it excluded (killed or unconscious, not deleted). Answers stay right, because a stale key's `List.Remove` returns false; the cost is index growth bounded by casualties, and each stale key is a strong reference to its agent | CONFIRMED, a gap that came in with `42dddf17`, like C3's two reset lines. Measured first: with the line deleted, 39 of 39 passed. The reuse test rebuilds the same 400 points, so every key is overwritten; the two-map test builds new points every round but asserted no index; W8 (`Remove_AfterEachOfSeveralBuilds_...`) reuses its containers, but every agent that leaves is deleted, and `RemoveNow` strips it from both indexes, so no stale entry can form | Fixed: `AssertIndexMatchesCells(spareMap, spareIndex)` after each build in `BuildCellsInto_TwoMapsOverOneSpareStack_NeverTouchesOrSharesWithThePublishedMap`. The points are new every round, so from round 2 the pair being filled held round 0's agents, and an index that is not cleared counts 400 entries for 200 agents. Red first, then restored: see the table below. Not changed: W8's per-round shift. The reviewer's optional variant (each deleted agent crossing a cell between its two builds) describes the test as it stands (computed: no deleted agent changes cell between the last two builds that hold it), but it has no one-token mutant to kill: a `RemoveNow` that reads the other generation's index already fails three tests either way (table below), so it would add test complexity with nothing to prove it |

Mutations of `SpatialGrid.cs`, one run each over the 39 grid tests, the saved original copied back and compared byte
for byte after each run (never a git restore):

| Mutation | Failing tests |
|---|---|
| `cellOf.Clear();` deleted from `BuildCellsInto`, before the new assertion | none (39 of 39 passed) |
| the same, with the new assertion | 1: `BuildCellsInto_TwoMapsOverOneSpareStack_NeverTouchesOrSharesWithThePublishedMap`, `Expected:<200>. Actual:<400>. every bucketed item has exactly one index entry` |
| `RemoveNow` takes the published index for the spare map (`_spareCellOf` to `_cellOf`) | 3: `Remove_AfterTwoBuildsThenAnIdleSpell_LeavesNeitherMapGenerationHoldingTheAgent`, `Remove_OffTheMainThread_ReachesBothMapsOnlyWhenTheMissionTickAppliesIt`, `Remove_AfterEachOfSeveralBuilds_DropsTheAgentFromBothMapsAndKeepsEveryAnswerRight` |
| `RemoveNow` takes the spare index for the published map (`_cellOf` to `_spareCellOf`) | the same 3 |

Verification: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` reports `Build succeeded` with
0 errors. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` at the fix:
`Failed!  - Failed:     1, Passed: 12395, Skipped:     2, Total: 12398, Duration: 26 s - TAOM.Tests.dll (net472)`.
The one failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey`; the two skips are the base's
`WargAttack_FastWarg_InvokesRunningAttack` and `WargAttack_SlowWarg_InvokesStandingAttack`. No test was added: C7 is one
assertion inside an existing test, so the total stays 12398. Not run: the Python suite (no script changed) and the game.
