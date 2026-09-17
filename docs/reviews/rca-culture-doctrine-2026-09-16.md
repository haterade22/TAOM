# RCA: culture doctrines, deep review of the Phase A + B slice (#608)

**Date:** 2026-09-16
**Scope:** the seven-agent deep review of the uncommitted culture-doctrine slice (five core
passes plus the two tailored ones Mike asked for, thread safety and extensibility), and the fixes
made before the Codex pass. Nothing here shipped; the toggle is off until the in-game A/B.
**Trigger:** the completion workflow, run at Mike's request for "specific, tailored deep reviews
and a broad, large codex review".

## Top-line

The architecture survived: no incompatible engine member (88 verified against the installed
v1.5.3 DLLs), no thread hazard, no standards violation, no empty-list path, no NaN gate with the
wrong polarity. What the review found was in the numbers and the instruments around the
architecture. The shipped multipliers made two of the four doctrine tactics unreachable or
losing in their own nominal case (Rohan's `FrontalCavalryCharge*2.0` used the same formula as
`CavalryDominance*1.3`; the Elven `DefensiveEngagement*1.5` beat the ring whenever the infantry
stood near the high ground), and no test could have said so because the weight tests checked
monotonicity, never the competition. The debug status line, the A/B's instrument, was gated behind
the toggle it was meant to measure against. The ally-team partition used a convenience API
(`SupportsAllyTeamOnPlayerSide`, which returns the first qualifying party) where the engine
partitions per troop. And `ClearTacticOptions` swept away the one tactic another vanilla behavior
adds (the caravan `DefensiveLine`), which nothing in the slice knew existed because the research
had grepped `MissionCombatantsLogic` and not the whole dump for `AddTacticOption`.

Twelve findings, all fixed in-session; full suite 9,534 green afterwards.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | HIGH | `MissionPerfHeartbeatBehavior.OnMissionTick` read `BattleLoadDiagnosticsSettings.Instance` every frame; MCM's `Instance` is a linear scan over every registered settings container. | Hot-path settings read | The sibling logic in the same slice reads its toggle once at `EarlyStart`; the heartbeat copied `[MemSample]`'s shape (a 30 s timer) without noticing it runs per frame. | Toggle re-read at 1 Hz; the frame clock resets while off so the first sample after re-enable is not the whole off period. Perf agent rule already covers it; the miss was mine. |
| 2 | MED | The `[Doctrine]` status line was set only at the end of `Apply()`, so with `EnableCultureDoctrine` off there was no 5 s line at all; the A/B protocol's off arm had no instrument. | Instrument gated behind the feature it measures | The status flag was assigned where it was convenient (after the swap) rather than where the protocol needed it (in every field battle). The protocol was written after the code. | `_status = IsDebug` is set before the toggle check. Lesson: an A/B's instrument is wired and tested in the OFF arm first. |
| 3 | MED | `TeamCombatantSelector` gave the ally team exactly the combatant `SupportsAllyTeamOnPlayerSide` returns (the FIRST qualifying party) and the player team the rest, and the ally team registered with that one party's Tactics skill. The engine places every troop by `!IsUnderPlayersCommand && !IsInSameArmyAsPlayer` (`Mission.GetAgentTeam`, `Mission.cs:5233-5240`) and gates every team on the side maximum (`MissionCombatantsLogic.cs:169`). | Engine rule replaced by a convenience API | The public accessor looked like the engine's partition and was used as one. The design critique had proposed it; the data-flow agent opened `GetAgentTeam`. | The selector applies the engine's predicate per combatant (with the army test at the boundary) and returns the side-wide skill; `TeamCombatantSelectorTests` pins both. Lesson: mirror the engine's partition rule, not a helper that happens to be public. |
| 4 | MED | Shipped multipliers: Rohan `FrontalCavalryCharge*2.0` (= 2.0f) above `CavalryDominance` (= 1.3f, identical formula); Elven `DefensiveEngagement*1.5` above `ArcherRing` in the nominal case; Isengard `FullScaleAttack*1.0` tied `InfantryMass` at the 1.5x bar; Erebor's defensive rows above the wall on a scene with no entities; goblin and Misty Mountain `HoldChokePoint*0.8` above `InfantryMass`. | Data authored without the competing weights | The weight tests asserted monotonicity and zero cases. The vanilla formulas the TAOM weights compete with were read for the design but never computed against the shipped rows; the author tuned by feel. | `VanillaTacticWeightReference` (test-side, nine formulas at nominal scores with line citations) and `ShippedDoctrineOrderingTests`: every shipped TAOM row beats every vanilla row it shares a list with by `MakeDecision`'s 1.5x sticky factor at the culture's canonical army. Multipliers retuned to pass it. |
| 5 | LOW | `ClearTacticOptions` discarded the `TacticDefensiveLine` that vanilla's `MissionCaravanOrVillagerTacticsHandler.EarlyStart` adds for caravan and villager sides regardless of skill. | Replaced list had a second writer | The research grepped `AddTacticOption` in `MissionCombatantsLogic` and the `TeamAIComponent` surface; the SandBox module's own `MissionLogic` was found only by the engine-fidelity agent grepping the whole dump. | `CaravanTacticsRule` mirrors the handler's condition and `TacticRoster.Build` takes ensured rows. Lesson: before replacing an engine list wholesale, grep the WHOLE dump for its writers. |
| 6 | LOW | The 1/1/1/1 branch wrote `_mainInfantry.AI.Side = Middle`, which `TacticFrontalCavalryCharge.ManageFormationCounts` (its named template) does not. | Template deviation | Copied the line from the 1/1/2/1 path by habit. | Removed. |
| 7 | LOW | Comments and the feature doc said the tactics run "on the async AI thread". The first `MakeDecision` and the first `TickOccasionally` run on the MAIN thread from `DeploymentMissionController.SetupAIOfEnemyTeam`, as does fast-forward; and `OnMissionTick` never overlaps the async tick (`Mission.OnPreTick` -> `WaitTickCompletion`), so the "torn read" caveat on the status line was overstated. | Thread model stated from the rule table, not re-derived | `harmony-patches.md`'s table is correct for the steady state; the deployment path is a different caller of the same code. | Comments and doc corrected to "the team-AI tick, usually async, sometimes main, never concurrent with `OnMissionTick`". The rules (no IoC, no logger, wrapped bodies) stand because the steady-state thread IS async. |
| 8 | LOW | `DoctrineWeights.ArcherRing`'s comment claimed the insurmountable score was folded in; 3 = 2 x 1.5 (DefendersAdvantage) at score 1. | Comment | Written from memory of the vanilla formula. | Fixed. |
| 9 | LOW | `TacticFactory.Create` returned null and the logic logged `skipped=` for an unmapped id; `BehaviorWeightApplier` had no `default`, so an unmapped `BehaviorKind` was a silent no-op. `DoctrinePlansTests.EveryPlan_NamesOnlyRegisteredBehaviours` compared the enum with itself. | Silent switch defaults, tautological test | The plans test was written to pin "registered behaviours" but had only TAOM's own enum to compare against. | Both switches throw on an unmapped member (the factory throw lands before the team's list is touched, the applier's in the failed-tactic latch); `DoctrineSwitchInvariantTests` reads both bodies as IL and pins every enum member to its case, and every applier target to a type `TeamAIGeneral` registers; the tautology is deleted. |
| 10 | LOW | `ConsoleCommandBindingTests` failed in a filtered run: its attribute scan hit `TaomStartOptionsProvider.AddStartOptions` (SandBox attribute) before any class had loaded the game folders, and .NET caches the failed bind for the process. | Order-dependent test | The test relied on some other class calling `GameAssemblies.EnsureLoaded()` first, which the full suite happened to do. | The test loads the game folders in its own `ClassInitialize`. |
| 11 | INFO | The design doc said the doctrine keys on "the culture fielding the most troops"; `IBattleCombatant.BasicCulture` is `PartyBase.MapFaction.Culture`, the party's faction culture, so a Dunlending clan sworn to Isengard fights as Isengard and recruited troops of another culture do not count. | Doc precision | Read the interface, not its campaign implementation. | Doc corrected; the agent-level read is Phase E. |
| 12 | INFO | A null `RingPosition` (no main infantry) leaves `BehaviorDefensiveRing` at weight 0, so the ring row is inert rather than wrong. | Comment | Not stated. | Comment added; the weight function already reports 0 for that case. |

## After the deep review: the Codex pass

The Codex adversarial review (gpt-6-astra, ultra, prompt
`codex-adversarial-culture-doctrine-2026-09-16.prompt.md`) hit the ChatGPT usage limit after
about 110k tokens and produced no report. Three interim observations survived in the transcript
and were verified and applied: `TacticCharge`'s casualty term sums losses on both sides (the
handover to a charge tracks total battle attrition; doc corrected); `TacticDefensiveLine`,
`TacticDefensiveRing` and `TacticHoldChokePoint` override `ResetTacticalPositions` to return true
(the base comment had said every vanilla field tactic returns false; corrected); the engine
places a circle's centre half a diameter behind its order position, which the vanilla
`DefensiveRing` + `FireFromInfantryCover` pairing shares, so the ring's archer offset is parity,
not a defect. Codex was mid-way through the one open question, whether the wall's `DefensePosition`
re-read at the Engage apply could walk the wall into the enemy, when it stopped; the wall now
follows `BehaviorHoldHighGround`'s lock rule (re-read only while the closest enemy is beyond
`max(0.8 * archers' missile range, 30 m)`), which closes it. Re-dispatch owed when credits allow.

## Root-cause pattern

Two of the four MED findings and the HIGH share one shape: the code that decides was right and
the code that measures or tunes it was written afterwards and by feel. The multipliers had no
competitor computed, the status line had no off arm, the heartbeat had no cost model for its own
gate. The fix in each case was to make the measurement machine-readable before the value: the
ordering test now encodes the A/B's "preferred set", the status line runs in both arms, the
heartbeat's gate is costed.

The other pattern is replacing an engine mechanism with the nearest public helper: the ally
partition (#3) and the tactic list (#5). Both times the helper was real and public and did
something adjacent to what the engine does internally. The rule that would have caught both is
the one the review agents applied: open the engine's own consumer (`GetAgentTeam`, every
`AddTacticOption` writer in the dump), not the accessor that resembles it.

## Why each agent missed what the others found

- Standards: nothing to find; its one note (an ADR-002 rationale line on `TaomTacticBase`) was added.
- Engine fidelity found #5 and #7 and #12 by decompiling callers; it did not evaluate numbers (#4)
  because it verifies signatures and firing sets, not values.
- Performance found #1 (MCM `Instance` cost, decompiled); it could not see #2 (a diagnostic's
  reachability is a data-flow question).
- Completeness flagged the untested selector and the missing switch pins (#9's test half); it does
  not read engine callers, so #3 and #5 were outside it.
- Data flow found #2, #3, #7's serialisation half, #11; it traced the engine's `GetAgentTeam`, which
  is the trace the author skipped.
- Thread safety confirmed the model and found the deployment-time main-thread path (#7) and the
  1/1/1/1 deviation (#6); by design it does not judge multipliers.
- Extensibility found #4 by computing the competition, and #9 by asking what a new enum member
  would do; it is the only pass that ran the numbers.

## Feedback memories to codify

One lesson each, appended to `docs/reviews/lessons/testing-qa.md` (a weight that competes in an
engine `MaxBy` gets an ordering test against a test-side reference of its competitors before the
data is tuned; an A/B instrument is wired in the OFF arm) and
`docs/reviews/lessons/adapters-taleworlds-api.md` (grep the whole dump for every writer of an
engine list before replacing it; mirror the engine's own partition rule rather than the nearest
public helper). No new harness memory: the review prompts that found these already exist.
