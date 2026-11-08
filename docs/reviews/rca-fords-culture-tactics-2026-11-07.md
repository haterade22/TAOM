# RCA: Fords of War, culture tactics (/deep-review, 2026-11-07)

## Top-line

The change: a faction's `tactics` block in `factions.json` sets the arrangement its lanes march and close in, the range at which
they close on the enemy as one formation, and the range at which they charge (Mike, 2026-11-07: Isengard and the dwarves should
"advance as a formation, get close and then attack"). Worktree `E:/repos/taom-fords-tactics`, branch `feat/fords-culture-tactics`,
base `6791dbc75`.

- **Tier 1** (engine calls from a mission tick: `Formation.SetArrangementOrder`). First wave: Lens 5 Data flow and Lens 2 Engine
  compatibility at `max`, Codex at `ultra` (the output header names gpt-6-astra). No CRITICAL or HIGH, so no second wave: Lens 1
  Standards, 3 Efficiency, 4 Completeness and 6 Design are SKIPPED BY TIER, Lens 7 XML and the Tooling lens are NOT IN SCOPE (no
  XML, no script; `factions.json` is read by C#), and Step 4 is N/A.
- **Result:** 0 CRITICAL, 0 HIGH, 8 MEDIUM, 7 LOW across the first wave and the fix re-review, 0 API incompatibilities. Every
  confirmed finding is fixed or documented below.
- **Proof:** after the last fix, the tactics tests (33) and the FordsOfWar, gate and architecture filter: 2,500 passed, 0
  failed, 3 skipped; `lint_docs.py --fail-on-drift` exit 0. Nothing ran in game: round 4 of the round robin measures the tactics.

## Findings, first wave

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | A shield wall switches every unit to the engine's defensive move behaviour, whose ranged weight is 0, so a wall from 40 to 45 m could cost the lane's archers their shots for the whole approach. (Lens 2 F1) | Engine assumption | The arrangement was chosen by its look (shields to the front), not by the behaviour set the engine picks for it. | Walls form only when the lead is within 20 m; a shipped-data test pins `CloseAt <= 20` for every ShieldWall or Square faction. See R4 for why the reason is speed, not arrows. Lesson in `adapters-taleworlds-api.md`. |
| 2 | MED | The Close target was the lane path vertex nearest the enemy, so a lane could stall beside an enemy off the path. (Codex F1) | Logic | The vertex was chosen to keep orders stable, and no test put an enemy off the path. | The target is the enemy's own position (see R1 for how it stays stable). Test: an enemy 20 m beside the path is the target. |
| 3 | MED | No hysteresis at the line between Close and Charge: a melee whose nearest enemy fell flipped back into the wall each time. (Lens 5 T9, Lens 2 F2) | Missing hysteresis | A new threshold on a value that jumps (the nearest enemy) was added without a second threshold. | A charge latch: a charging lane keeps charging until no enemy is within its close range. Test: the Charge, Charge, March, Close sequence. Same lesson. |
| 4 | MED | Close came before the tower in reach, so a lead carried level with or past its tower got a Move back toward an enemy, which check 9 `bypass` fails. (Lens 5 T7) | Rule order | The new branch went in beside the charge without rereading what the branches after it guard. | Close stands aside when the lead or the target is level with or past the next enemy tower (`LevelOrPast`, shared with `TowerInReach`). Tests on both sides. |
| 5 | MED | Close came before Seize and pulled a seizing lane off its objective. (Lens 5 T8) | Rule order | As 4. | Under Seize, Close returns the seize command, or nothing when that command is a camp charge. Test. |
| 6 | LOW | A block that named only `close` kept the wall on every other branch. (Codex F2, Lens 5 T4, Lens 2 F5) | Logic | The fallback was written for the Close branch only. | `ArrangementFor` gives Line on the other branches when only `close` is named. Test. |
| 7 | LOW | The reader took "2" and "Loose, ShieldWall" as arrangements (`Enum.TryParse`). (Codex F3) | Input validation | `Enum.IsDefined` was taken as enough. | Exact names only. Test. |
| 8 | LOW | Doc claims: the 0.3 speed applies only while the formation keeps its places; the front rank is sorted, not filled in arrival order; Rohan does not charge in a wedge; the director comment named a re-slot that does not happen; the engage range lines and the march index reset list did not name the tactics. (Codex F4, F5; Lens 2 F3, F4, F6, F7; Lens 5 T13) | Doc accuracy | Written before the engine paths were read. | Rewritten from the engine lines the lenses quoted. I also told Mike the wrong "arrival order" claim. |

## Fix re-review

Codex at `high` (gpt-6-astra, prompt `docs/reviews/codex-rereview-fords-culture-tactics-2026-11-07.prompt.md`), Lens 5 and Lens 2
at `high` read the fix diff. They confirm findings 3 to 7 fixed: the mirrored tower guard, the Seize case, the Line fallback and the
exact names. Lens 5 traced 14 flows with no gap; a faction with no tactic behaves as before (`CloseAt` equals `ChargeAt`).

| # | Sev | Finding | Fix |
|---|---|---|---|
| R1 | MED | The fix snapped the target to a 5 m grid but kept the enemy's height, so on a slope the command changed every frame and the director sent a new Move each frame. (Codex 2, Lens 5 F1) | The lane keeps its Close target until the enemy is more than 5 m from it (`CloseRetargetDistance`). The adapter rebuilds the height from the ground anyway. Test: a 2 m step and a 2.5 m height change keep the target. |
| R2 | MED | A snapped point off the navmesh made the director fall back to the nearest waypoint, record the request as applied, and stall. (Codex 1) | The target is the enemy's own position, ground it walked on, which the navmesh check accepts. Residual: at the keep, an enemy on another lane's barred faces passes the scene-wide check (Lens 2, UNVERIFIED in game). |
| R3 | MED | With a 1 m charge range a lead could reach a snapped target 3.5 m from the enemy and never charge. (Codex 3) | The retarget distance is the smaller of 5 m and the charge range, so a lead on the target is within charge range. Test with a 1 m charge range. |
| R4 | LOW | The doc's "a 20 m wall costs no arrows" was not supported: the normal move behaviour gives ranged only 0.02 to 0.04 at every distance, so the 20 m line does not save arrows. (Codex 5, Lens 2) | The 20 m limit stays, for its real reason: a wall walks at about 0.3 of top speed. The docs and the test message say so, and whether a wall changes how archers shoot is left to a game run. Rivendell's and Mirkwood's intent no longer says "shoot". |
| R5 | LOW | The doc said the engine sorts shield bearers forward every 0.5 s in a wall; the sort returns at once when the interval is 0, as in a shield wall. (Codex 4, Lens 2 F3) | The doc says the sort runs while the lane marches with space between its units and stops once the wall forms. |
| R6 | LOW | Close came before an attack-range tower: a lead within 20 m of its tower turned to close on an enemy 40 m back. (Lens 5 F3) | Close stands aside when the lead is within `towers.attackRange` of the next enemy tower. Test. |
| R7 | LOW | Doc and comment drift: "Tower in reach" ignored the latch; the class summary did not name it; the director comment named a retry path that practically never runs and left out the defensiveness pass and which arrangements rebuild. (Lens 5 F4, Lens 2) | Rewritten. |

Accepted, not changed: at the keep, a lane may close on a neighbouring lane's enemy up to its close range (55 m for Mirkwood); the
doc already says the lanes meet at the keep. Under the `anyLane` case a refused target can fall back to a waypoint past the tower
(Lens 2, domain note); the target is now the enemy's own ground, so a refusal needs an enemy on another lane's barred faces.

**Process note:** I wrote the R1, R3 and R6 tests before the code, but did not run them red before the fix went in. One of them
then failed for a wrong test (an enemy outside the close range), which a red run would have shown first.

FOLLOW-UP: no issue: Mike waived issues for Fords of War, 2026-10-31. A `/game-run` or round 4 reads `branch=Close` and `arr=`
in the `[LaneState]` lines, and whether archers shoot while their lane closes.
