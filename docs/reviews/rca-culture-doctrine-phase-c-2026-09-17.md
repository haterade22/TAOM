# RCA: culture doctrines, deep review of Phase C and D (#608)

**Date:** 2026-09-17
**Scope:** the six-agent review of the uncommitted Phase C and D slice (five TAOM formation
behaviours, seven tactics, the morale, aggression and routing tiers) and the fixes made before
the commit: standards, engine fidelity against the installed v1.5.3, performance and threads,
battle logic and tests, lifecycle and state matrix, and the systems analysis Mike asked for
("maybe we could even rewrite them entirely"), which is
[analysis-battle-ai-2026-09-17.md](analysis-battle-ai-2026-09-17.md). Nothing here shipped
toggle-on; the in-game A/B is owed.
**Trigger:** the completion workflow, at Mike's request ("implement all of these behaviours"
plus the analysis).

## Top-line

The engine contract held again: about 160 members verified on the installed DLLs, none
incompatible; no standards violation; no thread hazard; the whole decision layer is microseconds
per second. The defects were in the decision model: two gates read a number their own tactic
changes on apply; two edges were written below the engine's hysteresis; a reform point sat inside
the distance that ends a reform; a facing-dependent engine query was used as if it saw every
charge; a state machine was reset by a lifecycle call the behaviour did not own; and the one
formation routing puts a troop into was folded by every tactic that did not know about it. All
found by reading the engine beside the code; none would have shown as an exception, all would
have shown as "why did the AI just do that" in the A/B.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | HIGH | `TwoLineWall` and `Envelop` gated on `InfantryCount`, the LARGEST infantry formation. Their own splits (2/1/2/1, 3/1/2/1) halve or third it on apply, so at the next 5 s decision the gate read half or a third, returned 0, the plain tactic took the team and merged the lines back. Bands: 80 to 159 foot (wall), 60 to 179 (envelopment). | Gate reads a value the gated action changes | The snapshot's `Largest` was written for "does the team have infantry"; the count was reused for "how many foot" without asking what the tactic does to the formations it counts. The ordering test's canonical snapshot passed the pre-split total, so it could not see it; the A/B cells (300 v 300, 400 v 250) sit above the bands. | `InfantryTotal` in the snapshot (sum over the class); both gates read it; `TwoLineWall_GateReadsTheClassTotal_NotTheLargestFormationItsOwnSplitHalves` builds the post-split snapshot. Lesson below. |
| 2 | HIGH | `TwoLineWall = ShieldWall * 1.05`, `Envelop = InfantryMass * 1.1`: `MakeDecision` keeps the current tactic unless a challenger beats it by 1.5x (`TeamAIComponent.cs:301`), so once the plain tactic held the team (one dip below the gate) the variant could never return, whatever the numbers did later. | Edge below the engine's hysteresis | The edge was chosen to "prefer the variant slightly", read as a tie-break between two of our own tactics; the 1.5x was known (the ordering test uses it) but only applied to vanilla rows. | `GatedEdge = 1.6f`; `GatedVariants_BeatTheTacticTheyRefineByTheStickyFactor_AndYieldWhenTheirGateFails` pins TAOM-versus-TAOM pairs, which the ordering test never compared. |
| 3 | HIGH | `CycleChargeMachine`: the reform point stood at the stop distance (vanilla's 20 to 50 m clamp of the charge distance), and `Reforming` charged the moment the enemy was inside 30 m. A charge that began inside 30 m therefore reformed for one tick, which is the compacted mid-battle case the cycle exists for. Vanilla has the same constants, written for infantry, and never runs them for horse. | Constants lifted with their bug | The machine was copied from `BehaviorTacticalCharge` on the strength of "vanilla wrote it"; nobody checked that the two constants were consistent with each other for the formation class vanilla had disabled it for. | `MinStopDistance` 35 m, `MaxStopDistance` 60 m, and `ReformContactDistance = min(contact, stop / 2)`; a test asserts the floor exceeds the contact distance and that the first reform tick does not charge. |
| 4 | HIGH | The brace read `IsUnderCavalryChargeFromFront` only. That query evaluates one formation (the closest significant enemy) and requires the wall to face it within about 41 degrees or already be Circle/Square (`FormationQuerySystem.cs:646-663`). A horse sweeping round a flank behind an infantry screen was invisible; the wall took the charge the brace was written for in ShieldWall. | Engine query narrower than its name | The name says "from front", which was read as "the front of the charge", not "our front"; the delegate was read for the cache period and the ETA rule, not the facing clause. | `CavalryThreat` (pure): every enemy cavalry formation's velocity against us, the engine's own cosine and horizon, no facing clause; `WallStances.Braced` ORs it with the engine query. Lesson below. |
| 5 | MED | `BehaviorInfantrySkirmish.ResetBehavior` reset the machine to `Throwing`. `ResetBehaviorWeights` calls `ResetBehavior` on every behaviour (`FormationAI.cs:358-364`), and the applier calls it on every plan apply, which any formation-set change on the team triggers; a spent javelin line re-armed, contested the melee rows for 5 to 10 s, and re-committed. | State reset by a lifecycle call the code did not own | `ResetBehavior` was overridden because `BehaviorDefend` overrides it (to clear its position); the machine was reset there because it looked like "the reset hook". | `Committed` survives `ResetBehavior` and `Activate`; the doc records why. |
| 6 | MED | Any tactic other than `MumakVanguard` being current first (Charge at the first decision, a vanilla row later) ran a 1/1/2/1 consolidation that folded the routed mumakil into the regular cavalry for the rest of the battle; the vanguard tactic's weight then read 0 forever. Separately, an EMPTY HeavyCavalry formation could become a split target for ordinary cavalry (the engine takes any empty formation before it asks the predicate, `TacticComponent.cs:229-259`) and then read as "the vanguard". | Routed formation owned by one tactic, folded by the rest | The routing seam and the vanguard split were designed together; the vanilla wrappers, which share the team, were not part of the picture. | `RoutedFormationGuard`: `Formation.SetControlledByAI(true, enforceNotSplittableByAI: true)` makes `IsAIOwned` false (`Formation.cs:326-347`), which is exactly what the consolidation skips (`TacticComponent.cs:243`) and nothing else in the field reads; set at install by toggling control on an empty formation. The vanguard split falls back to 1/1/2/1 when the slot is empty. |
| 7 | LOW | `VolleyControl` never wrote a formation that was not AI-controlled, so a formation the player took back while it held fire kept the hold. | Gate too strict for the exit | "Never write a player-controlled formation" was applied to the release too. | One release write on the next tick when control was lost; the engine serialises the AI tick against the main thread, so it is a plain write. |
| 8 | LOW | Both braced walls stood on `Here(formation)` re-read every tick while braced; the average drifts as men shuffle, so the square could creep. | Anchor re-read per tick | Vanilla's `BehaviorAdvance` stores `_reformPosition` once; the copy dropped the field. | `_bracePoint` pinned when the brace begins, cleared when it ends. |
| 9 | LOW | Doc claims: 27 behaviours per formation (24, `TeamAIGeneral.cs:63-86`); `PrecalculateMovementOrder` "on every candidate" (only on a candidate beating the running maximum, `FormationAI.cs:184-197`); the rejected `TeamAIGeneral` subclass "misses transfer-populated formations" (`TransferUnitsAux` goes through `Agent.Formation`'s setter, which calls `AddUnit`, which fires the hook); "sieges untouched" (the morale and aggression tiers are per soldier and run everywhere). | Doc written from memory of the engine | Facts recalled across a session boundary instead of re-read. | Corrected in the feature doc, the applier, the base, the CHANGELOG. |
| 10 | LOW | `UpdateAgentStats`, and so the aggression post-pass, also runs on the async AI thread: a formation order change rewrites `Agent.Defensiveness` per man, whose setter calls `UpdateAgentProperties` (`Formation.cs:2838-2844`, `Agent.cs:1112-1126`). The docs said main thread. | Thread stated from the spawn path only | The obvious caller was traced; the setter's caller was not. | Docs corrected; the service already met the bar (immutable data behind a bool, no logging). |

## What the reviews confirmed

- Standards: all ten checks clean across 60 files; the two classes over 150 lines carry their
  justification.
- Engine fidelity: every member TAOM binds to exists with that signature and accessibility on the
  installed v1.5.3; `FormationAI` ticks one behaviour and cancels the old one; `AddAiBehavior` on
  the team-AI tick is on the same sequential thread as every reader; `CommonAIComponent.CanPanic`
  asks the model from the worker tick and the model's service is a lookup behind a bool;
  `CustomGame` adds its models before the submodules' `OnGameStart`, so the Custom Battle models
  are last-registered; `GetAgentTroopClass_Override` replaces the engine's body, and the rule
  reproduces it.
- Performance: no sustained per-frame cost; the worst plausible case (a mass rout, 300 agents at
  zero morale) is about 1 to 2 ms per second across the worker pool, on a call vanilla already makes.
- Threads: no HIGH; `Mission.OnPreTick`'s `WaitTickCompletion` makes every main-thread write and
  the async reads non-overlapping; `volatile` on `Failed` and `Status` is exactly sufficient.

## Lessons appended

`docs/reviews/lessons/testing-qa.md`: a gate that reads what the gated action changes; a
TAOM-versus-TAOM edge under the engine's hysteresis. `docs/reviews/lessons/adapters-taleworlds-api.md`:
an engine query named for the threat is scoped by its own facing and its one formation; a
routed formation is folded by every tactic that does not own it, and the engine's exemption is
`enforceNotSplittableByAI`; constants lifted from a disabled vanilla path carry its bugs.
