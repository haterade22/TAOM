# RCA: the crew hold that stopped every bow (#1482)

The first fix for #1482 (`f8265b6dc`, on trunk as `9d62beffb` on 2026-11-19) held every crew archer of the mumakil and the war elephant on its seat, and no archer shot. The maintainer saw it in his first battle on the trunk build. This file records the investigation and why the build, the review and the measuring battle all missed it. The fix is `d5d674663`.

## What happened

| Time (2026-11-19) | Event |
|---|---|
| 08:53 | Measuring battle 1, a different hold on each seat. The one seat with the position lock and `SetTargetZ` (`main_2`) stays within 0.03 m with no teleport. Its restart counter rises at the nest's rate, and that is read as "shoots like the nest" |
| 10:42 | Measuring battle 2, lock against lock with attachment. The attachment removes the lag at speed. The log has 0 arrow hits on the enemy in 68 seconds, and nobody counts them |
| 11:34 | The fix is committed: attachment, `SetTargetPosition` and `SetTargetZ` on every seat. The commit body says the archers "completed their reloads for the whole battle" |
| about 11:45 | Pushed to trunk on the maintainer's word |
| 17:08 | The maintainer's battle on the trunk build: the archers do not move on the platform, and he sees no shot |
| 17:22 | Measuring battle 4, with a shot counter and the movement mode for each seat: `SetTargetZ` is the cause |

## Evidence

Battle 4, one mumakil against 101 Rohan recruits, 3 minutes 10 seconds at 1x, `taom_debug_2026-11-19_17-21-29.log`. A shot is one `OnAgentShootMissile` call for that archer.

| Seat | Hold | Shots | `Agent.MovementMode` | Largest distance from seat | Rescue teleports |
|---|---|---|---|---|---|
| main_4, upper_1 | attachment, position lock, `SetTargetZ` (the first fix) | 0, 0 | NoPhysics | 0.12 m | 0 |
| main_0, main_1, upper_0 | attachment, position lock | 44, 43, 47 | Land | 0.12 m | 0 |
| main_2 | attachment, position lock, scripted position | 48 | Land | 0.12 m | 0 |
| main_3 | the old design plus the attachment | 0 | Land | 0.78 m | 448 |
| nest_0 | the old design | 48 | Land | 0.13 m | 0 |

Battle 1 said the same, once its hits were mapped to seats. Crew agents take consecutive indices from 165 (three mumakil crews of eight in seat order, then the six howdah archers; the howdah seat with `shots=0` is the one index in its range with no hit). The three nests made 17 of the mumakil crews' 21 hits, and the three locked seats made none.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | `Agent.SetTargetZ` every tick put each crew archer in `AgentMovementMode.NoPhysics`, and the two seats measured in that mode loosed no arrow in three minutes while their neighbours loosed 43 to 47 | Side effect of a copied vanilla call | The hold was copied whole from `ClimbingMachine`, whose user climbs a net and never fights. The native read said "gravity-excluded mode" and nobody asked what else that mode switches off | `SetTargetZ` removed from both seats. Gate `HowdahSeatReleaseTests.NoSeat_PutsItsArcherInTheNoPhysicsMovementMode`. The status line prints `mode=` |
| 2 | HIGH | "The archers reload and shoot" was concluded from the restart counter and a highest progress of 1.00, and written into the code comments, the feature doc, the lesson, the commit body and the issue | A proxy read as the outcome | The counter rises for an aborted draw too, and the highest progress was a lifetime maximum. A doc sentence said that a high count with `max` 1.00 is a busy archer. The mumakil line had no shot count; the howdah's origin had one since September | `MumakilCrewAgentOrigin.ShotsFired`, counted in `MumakilMissionBehavior.OnAgentShootMissile`; `shots=` and `tgt=` on the mumakil line; the doc table now says that `re` never proves shooting |
| 3 | MED | The pass rule of the measuring battles named the hold only (distance, rescues, no hang) | Test of half the requirement | The reported problem was "pushed around, hard to shoot", and the plan turned it into "zero correction teleports" | The checks `taom.crew-seats.mumakil-shot` and `howdah-shot` beside the held checks in `tools/ingame_checks/1482.toml` |
| 4 | MED | The deep review of the first fix passed it: 33 engine calls verified, 0 incompatible | Review against the wrong model | Both lenses compared each call with vanilla's use, and vanilla's user of these calls does not attack. The brief asked whether the calls are safe, never whether the archer can still shoot | The lesson: for a copied call pattern, read the state each call leaves and ask whether vanilla's user ever does what ours must |

## Why each agent missed these

- The builder (this session) had the 10:42 log with 0 hits on the enemy and did not count hits, because the restart counter already "answered" the question. It also wrote the claim into five places before any direct proof existed.
- The Engine compatibility lens flagged "SetTargetZ holds the height" as an unverified claim and read the native write of movement mode 8. It rated the mode as equal to vanilla's, which was true and beside the point.
- The Data flow lens traced every release path of the new state. The bow is not on any of those paths.

## Still open

- The howdah. Without `SetTargetZ` its floor body alone must hold the archers at 3.2 m, with no navmesh under them. No battle on 2026-11-19 had a howdah elephant after the first one, so this hold is not measured there. The checks `taom.crew-seats.howdah-held` and `howdah-shot` read it. FOLLOW-UP #1482: the issue stays open with `triage-needs-ingame`.
- The engine code that stops the shot is not read. `SetTargetZ` writes two things, the NoPhysics mode byte and a height target, and the measurement cannot say which one stops the bow. No managed code on the ranged attack path reads the movement mode, so the gate is native. The hold needs neither write, so the fix does not depend on the answer.
- Why the nest archer never walks under the old design while the main-deck archers do is not known. It no longer matters for the hold.

## Debug report

```
Symptom:             archers stay on the platform and do not shoot (maintainer, trunk 9d62beffb)
Root cause:          Agent.SetTargetZ puts the archer in AgentMovementMode.NoPhysics; the archers measured in that
                     mode loosed no arrow (the engine code that stops the shot is not read)
Phase that found it: Phase 4, one battle with a hold per seat, a shot counter and the movement mode per seat
Fix:                 SetTargetZ and the SetExcludedFromGravity reset removed from TaomMumakilStandingPoint and
                     TaomHowdahStandingPoint; the hold is SetForceAttachedEntity plus SetTargetPosition
Evidence:            the table above; the gate test failed before the fix and passes after it
Regression test:     HowdahSeatReleaseTests.NoSeat_PutsItsArcherInTheNoPhysicsMovementMode (IL gate, both seats)
Siblings:            clean: no other TAOM caller of SetTargetZ or SetExcludedFromGravity (Main searched)
Related:             REVIEW-LOG entries of 2026-11-19 for #1482; lessons/adapters-taleworlds-api.md
Status:              DONE_WITH_CONCERNS: the final build on the mumakil and the howdah are not yet played
```
