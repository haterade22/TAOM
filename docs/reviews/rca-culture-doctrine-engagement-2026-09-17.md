# RCA: culture doctrines, the engagement slice after the first A/B (#608)

**Date:** 2026-09-17
**Scope:** the uncommitted slice written from the first Custom Battle A/B (Erebor v Mordor,
07:17): the foot-first target rule (`TargetSelection`, `EnemyScan`, `BehaviorFootCharge`), the
distance-gated cavalry brace, the two high-ground gates (`HighGroundRace.WorthGoing`), the
`engagement` block, and the F6 popup rows. Six review agents: standards, engine fidelity on the
installed v1.5.3, performance, completeness, data flow, and an adversarial battle-logic pass.
**Trigger:** Mike ("Ensure you conduct a deep review"), after two general observations from the
battle: the wall spent its opening minute walking to a far hill with the horse on it, and the
foot chased every passing eored.

## Top-line

Standards, performance and completeness came back clean and the engine pass verified 21
members. The defects were in the first cut of the two rules, and they share one shape: a rule
written to the number the observation suggested (40 m, "any enemy", "the closest formation
list") rather than to the engine fact behind it (a square takes 8 to 12 s to form; horse are
the brace's business, not the march's; vanilla walks ten formation slots). All found by reading
the engine beside the code; none would have shown as an exception, all would have shown as
"why did the AI just do that" in the next battle.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | HIGH | The brace fired at 40 m (3 to 4 s at horse speed, `SandboxAgentStatCalculateModel.cs:1266-1271`) and released 3 s after the last signal, under the 8 to 12 s a line needs to become a square (the slice's own `RaceTunables`); a cycling eored could flip the wall Square and back every 3.5 s, each flip re-forming every agent (`Formation.cs:744-766`). | A distance rule written without its time | The observation was "within X distance"; X was set from the picture of a charge, not from the form-up time the square needs. | Default 100 m (the form-up allowance at horse speed; the 15 s ETA horizon keeps a walking formation out), release at 1.5X (`TargetSelection.ReleaseFactor`), pinned by `CavalryThreat_BracedWall_ReleasesBeyondOneAndAHalfTimesTheDistance`. Lesson below. |
| 2 | MED | Horse archers triggered the square: `IsRangedCavalryFormation` counted as a threat, and they wheel inside range forever without charging; a square under arrows is the wrong stance. | Class read too wide | "Horse" was one bucket for target and threat alike. | Threat = `IsCavalryFormation` only; ranged cavalry stays in the never-chased target class. |
| 3 | MED | The scan and the race walked `FormationsIncludingEmpty` (8 slots) while vanilla's own `CacheClosestEnemyFormation` walks `FormationsIncludingSpecialAndEmpty` (10, `Formation.cs:1496-1519`); a lord's bodyguard formation (`GeneralsAndCaptainsAssignmentLogic`, field battles too) was invisible as target and as threat. | Wrong engine list | `FormationsIncludingEmpty` is what the tactic base uses for its OWN formations, and the name reads as "all of them". | Both walks use the ten-slot list; the doc names the engine's own walk as the reference. Lesson below. |
| 4 | MED | A melee cavalry formation sitting in the melee has an average-position velocity near zero, so `IsInbound` (speed > 2 m/s) dropped it and the wall un-braced with horse among the files. | Velocity test where a distance test was needed | The inbound test was lifted from the engine's charge query, which never has to answer "are they here already". | `CavalryThreat.IsOnUs` (15 m, velocity or not), `IsOnUs_InsideContactDistance_WhateverTheVelocity`. |
| 5 | MED | No minimum size: one surviving rider re-formed a 150-man wall into a square; vanilla filters through `ClosestSignificantlyLargeEnemyFormation`. | Missing significance | The engine query the brace replaced carried the filter in its name; the replacement kept the geometry and dropped the filter. | `CavalryThreat.IsSignificant` (5 riders and a tenth of ours, integer arithmetic: `0.1f * 150` is not 15), tested. |
| 6 | MED | `holdWhenEnemyWithinMetres` read `CachedClosestEnemyFormation` of any class and `Holding` is terminal, so a scout eored passing at 45 m locked the wall where it stood for the battle. | Gate on the wrong actor | "Any enemy on us ends the march" was written for the A/B picture (horse on a marching wall) without asking who ends a march (foot) and who the brace already answers (horse). | `EnemyScan.ClosestFootDistance`; `WorthGoing` takes the foot distance; the brace handles horse mid-march. |
| 7 | MED | `SetDefaultBehaviorWeights` arms vanilla `BehaviorCharge` at 1 on every apply (`TacticComponent.cs:581-587`) and it charges the closest formation of any class; a FootCharge row left it armed, so the counter-charge that was meant to go through FootCharge could go through the default row and chase horse. | The default row under the plan | The plan tests pin the ROWS; the applier's reset-then-default sequence sits under them. | The FootCharge case calls `DisarmVanillaCharge` (weight 0); `BehaviorWeightApplier_FootChargeRow_DisarmsTheEngineDefaultCharge` reads the IL. Engage rows raised to 1 so FootCharge can win (1.44 close against a target busy elsewhere, over the 1.2x hold on the active behaviour, `FormationAI.cs:182`). |
| 8 | MED | `HighGroundCloseToForeseenBattleGround` searches a square whose half-side is half the distance to the enemy (`FormationQuerySystem.cs:637-643`), so at deployment range the 60 m cap rejected the point almost always and a knoll 30 m off was never chosen. | Gate on a point chosen elsewhere | The cap was written against the engine's point instead of choosing the point inside the cap. | `HighGroundOf` runs the engine's own search (`Mission.FindPositionWithBiggestSlopeTowardsDirectionInSquare`, public) on a square that fits the cap. |
| 9 | LOW | `OnBehaviorActivatedAux` ran `Plan()` before `Activate()`, so the first order of a re-activation used the previous activation's brace point and target. | Lifecycle order | The base copied vanilla's order (orders first, aux after) without noticing our `Activate` is the reset. | `Activate()` first. |
| 10 | LOW | `HasBattleBeenJoined` recognised `BehaviorCharge`/`BehaviorTacticalCharge` as charging, not `BehaviorFootCharge`. | New type outside an `is` chain | Type checks in the tactic base were not on the "add a behaviour" list. | Added; the "How to add a behaviour" list now names the joined test. |
| 11 | LOW | `EnemyScan` walked the enemy twice (index then formation); the wing scanned in both `Plan` and `OnActiveTick`. | Cost | Microseconds, but a second walk for nothing. | One walk (the target only ever moves to the candidate just considered; `Target_OnlyEverMovesToTheCandidateJustConsidered` pins it); the wing caches its target per active tick. |
| 12 | LOW | `BehaviorBracedDefend.Plan` faced an emptied target its siblings gate with `CountOfUnits > 0`. | Inconsistent guard | Sibling copy without the guard. | Guarded. |
| 13 | DESIGN | Archers counted as "nearest foot", so a retreating archer block was chased ahead of the infantry beside it. | Bucket too coarse | The first rule had two classes. | Three classes; infantry preferred within 1.5 times the archers' distance. |

## What the reviews confirmed

- Standards: 23 files, 0 violations; every pure core engine-free; the config block validated
  through `FiniteFloatValidator` with one test per rule.
- Engine: 21 members verified on the installed v1.5.3; the "target empties mid-order" window
  is closed by the engine itself (`Formation.Tick` substitutes `Charge` for an inapplicable
  `ChargeToTarget` on the next tick, `MovementOrder.cs:817`, `Formation.cs:2440-2471`);
  `ChargeWeight.Foot` matches `BehaviorTacticalCharge.CalculateAIWeight`'s infantry branch term
  for term, and `GetClassWeightedFactor(1,1,0.5,0.5)` is exactly 0.5 for a pure cavalry target;
  the writer and reader of a behaviour's `Engagement` are always the same `Team.Tick` call
  (`Team.cs:585-623`), whichever thread runs it.
- Performance: no allocation on any tick; the scan is field reads and 5 s caches.
- Data flow: the JSON block reaches every behaviour (every `Ensure` site is the 3-arg overload);
  all seven engine-float gates fail toward the safe branch; `Team.Reset` rebuilds every
  `FormationAI`, so no stale target survives a scene reset.

## Why each agent missed what the others found

Standards, performance and completeness are not asked battle questions. The engine agent found
the list gap (it compares against the engine's own walk) but not the timing (no engine constant
gives the form-up time). The data-flow agent found the default `BehaviorCharge` row (it traces
what the applier arms) but judged the row weights against the active behaviour without the 1.2x
hold. The logic agent found the timing, the horse archers, the melee release, the significance,
the lock, the hold factor and the search square, because it was asked to make the AI do
something stupid and read the engine for each number. The tailored battle-logic pass is the one
that turns "the numbers look reasonable" into "here is the battle where they are wrong".

## Lessons appended

`docs/reviews/lessons/adapters-taleworlds-api.md`: the two formation lists; a distance rule
carries a time. `docs/reviews/lessons/testing-qa.md`: a rule written from an observation is
tested against the engine fact behind it, and the default rows under a plan are part of the plan.
