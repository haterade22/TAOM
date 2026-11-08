# RCA: Fords of War check 8 judged a Seize as a lane hold (/deep-review, 2026-11-07)

## Top-line

The defect: check 8 (held lanes against tower fire) failed in run 20261107-185556 with `East1 in fire of tower_west_1_0`.
East1 was on Seize, not on Hold. `LaneMarchRules.SeizeCommand` gives a lane on Seize `LaneCommand.HoldAt` at the capture
point, the same command kind a lane on Hold gets at its hold point, and `FordsFieldProbe` judged every HoldAt as a lane hold.
It measures arrival along the lane's axis, and point_0 stands 70 m off lane 1, so East1's lead looked arrived while it stood 30
to 43 m from the tower, 79 to 128 m short of the point (Lens 5, from the run's `[LaneState]` rows). The fix: the probe judges no
HoldAt whose target is exactly a capture point's or camp's position. Worktree `E:/repos/taom-fords-check8`, branch
`fix/fords-check8-seize-hold`, base `75468a0aa`.

- **Tier 1** (the probe runs in the mission tick). First wave: Lens 5 Data flow and Lens 2 Engine compatibility at `max`, Codex
  at `ultra` (gpt-6-astra). No CRITICAL or HIGH, so no second wave: Lens 1, 3, 4 and 6 are SKIPPED BY TIER, Lens 7 and the
  Tooling lens are NOT IN SCOPE, and Step 4 is N/A.
- **Result:** 0 CRITICAL, 0 HIGH, 1 MEDIUM (accepted, below), 3 LOW. Lens 2 traced the objective's position from the scene
  marker to the recorded command and found no engine step that changes it.
- **Proof:** the two Seize tests failed before the fix. After the fix and the LOW changes, the FordsOfWar and gate tests: 2,505
  passed, 0 failed, 3 skipped. Nothing ran in game.

## Findings

| # | Sev | Finding | Disposition |
|---|---|---|---|
| 1 | MED (Codex), LOW (Lens 2, Lens 5) | Exact position equality does not prove a Seize: a scene could put an objective exactly on a lane waypoint that is also a hold point, and that real Hold would no longer be judged. | Accepted. On the shipped scene every objective is 70.0 to 70.1 m from its nearest waypoint, and the fitter moves a point at most 12 m (Lens 5, from the live `scene.xscene`). A command flag or a new command kind touches every switch on the kind or the domain record and the Seize tests for a case no scene has (simplicity criterion). If `SeizeCommand` ever stops aiming at the slot's position, Seize lanes come back into check 8 and fail it loudly. |
| 2 | LOW | `LanesOf` tested `holding` again, which the stream's arrival distance already implies, and took `applied` only for that. (Lens 5) | Deleted, with no change in behaviour. |
| 3 | LOW | Nothing at `SeizeCommand` warned that the probe depends on its target. (Lens 5) | A summary note names the dependency. |
| 4 | LOW | The new tests stand outside the tower's fire range, so they test the exclusion and not the false FAIL itself. (Codex) | Accepted: check 8 reads only `HoldSettled`, which the tests assert. |

**Effect on the evidence (Lens 5):** in that run West1's held samples were all Seize holds, and so were most of East0's, so on a
comparable run check 8's `settledSamples` falls sharply. That is the fix.

**Outside this diff:** East1 sat on Seize inside tower_west_1_0's fire, well short of point_0, and no check judges a seizing
lane against tower fire now. Round 4 watches Seize lanes on lane 1 (whether point_0, 70 m off the lane, is reachable from it).
