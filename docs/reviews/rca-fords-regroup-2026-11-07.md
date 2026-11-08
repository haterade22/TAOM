# RCA: Fords of War, the AI regroups under its own tower (/deep-review, 2026-11-07)

## Top-line

The change: the Fords of War AI sends a lane that is badly outmatched near its lead back to Fall back, where it fights under
its own tower, and returns it to Push once the odds ease. Mike asked for a smarter AI overnight (2026-11-07: "The current AI
behavior is pretty dumb, just attacking the first thing it sees/closest enemy"). Worktree `E:/repos/taom-fords-trial`,
base `6791dbc75`, reviewed as the unstaged diff.

- **Tier 1** (a mission tick: the AI commander and the lane rules run every tick of the match). First wave: Lens 5 Data flow
  and Lens 2 Engine compatibility at `max`, Codex at `ultra` (gpt-6-astra). No CRITICAL or HIGH, so no second wave: Lens 1
  Standards, 3 Efficiency, 4 Completeness and 6 Design are SKIPPED BY TIER, Lens 7 XML and the Tooling lens are NOT IN SCOPE
  (no XML, no script), and Step 4 is N/A.
- **Result:** 0 CRITICAL, 0 HIGH, 10 MEDIUM, 7 LOW over four rounds (the first wave, a Codex re-review at `high`, a Lens 5
  re-review, a final Lens 5 check at `high`). The first design compared whole-lane headcounts, and most findings follow
  from that. The design was reworked twice. Every confirmed finding is fixed or documented below.
- **Proof:** the filtered tests after the last fix (FordsOfWar and the architecture gates): 2,551 passed, 0 failed, 3
  skipped. The last fix was RED first (1 failed: the lane with a few pursuers was released). Nothing ran in game: round 4 of
  the round robin measures the rule.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The trigger and the release compared whole-lane headcounts. A lane counts units still at its base, so a lane retreated from enemies that were not there, and the elves (40 per lane against 90 to 123) would retreat all match. (Codex, Lens 5) | Wrong input | The view already had `OwnLive` and `EnemyLive`, so the rule used them. | `FordsMatch.PressureOf` sums `UnitRecord.Value` of the units within 40 m (`PressureRadius`) of the lane's lead, from the new `LaneSnapshot.UnitKeys`. Entry needs at least `RegroupMinEnemy` (10) enemies there. |
| 2 | MED | With every lane on Fall back, no lane could be the assault, `Defend` bought nothing and `BuildAssault` returned: the AI stopped buying. (Codex, Lens 5) | Starved path | Before this change the AI never ordered Fall back, so `PickAssaultLane` returning null was rare. | With no assault lane and no lane under threat, `BuildAssault` takes the lane with the most room and runs the normal cycle dispatch. |
| 3 | MED | The first fix for 2 called `BuySparePack`, which skips `up:` and `beast:` entries of the cycle. (Codex re-review, Lens 5 re-review) | Fix defect | The fix reused the nearest helper without reading what it skips. | The same dispatch as a real assault lane; `AiBuyCycleTests` cover an upgrade, a beast and full lanes. |
| 4 | MED | A Fall back lane at the edge of its tower's fire range could switch Charge and Fall back every tick, and the director dedups identical commands only. (Codex, Lens 2) | Hysteresis | `_charging` widened the enemy distance but not the tower distance. | `FightsAtOwnTower` adds `SiegeStayMargin` while the lane charges. |
| 5 | MED | The tower test measured the lane's lead, not the enemy: with the lead at 44 m and the enemy at 54 m of a 45 m fire range, the lane charged outside the tower's cover. (Codex) | Wrong input | The lead was the position at hand. | The test reads the nearest enemy's position. |
| 6 | MED | A Seize exit ran before the lane left `_regrouping`, so the AI could later release a person's Fall back. (Codex) | State | The bookkeeping sat below an early `continue`. | The lane leaves `_regrouping` on any visit off Fall back, before the Seize exit; a test runs the sequence. |
| 7 | MED | With no enemy within 40 m the enemy value is 0, so the release fired anywhere, before the lane reached its tower. A test masked it with an input `PressureOf` cannot produce. (Codex re-review, Lens 5 re-review) | Wrong condition | The release tested only the ratio. | The release needs contact, or the lead at its own tower with no enemy near, or an empty lane. |
| 8 | MED | A hero's price (800 to 2400) dominated the value sums, so one hero forced or blocked a regroup. (Lens 5 re-review) | Balance input | `UnitRecord.Value` is the unit's gold value, which the kill bounty reads. | `PressureOf` counts a hero as `HeroPressureValue` (250); the record keeps its price. |
| 9 | MED | Own units of other lanes near the lead did not count, though enemy units of any lane did. | Asymmetry | The own side was summed from the lane's own list. | Both sides sum every lane's units within the radius. |
| 10 | MED | One enemy within 40 m counted as contact for the release, though the entry needs 10. On a retreat the lead is the rear guard, so the first riders to catch up released the lane away from its tower. (Lens 5 final check) | Asymmetry | The contact test of fix 7 was `NearEnemyCount > 0`. | Contact needs `RegroupMinEnemy` enemies near, the same as the entry; a test feeds 9 pursuers away from the tower. |
| 11 | LOW | The log line had no side. (Lens 5) | Diagnostics | | `side=` added. |
| 12 | LOW | The release removed the lane from `_regrouping` before the match accepted the Push. (Lens 5) | State | | The lane leaves the set only on a later visit off Fall back; a test asks again after a refusal. |
| 13 | LOW | `Decide_AFallBackTheAiDidNotOrder_IsNeverReleased` passed with its guard deleted. (Lens 5 final check) | Weak oracle | Its inputs failed the release on their own. | Inputs that pass the release, so only the guard holds the lane. |
| 14 | LOW | Three tests fed value with a count of 0, which `PressureOf` cannot produce, and one test name said the wrong method. (Lens 5 final check) | Test inputs | | Inputs made consistent; renamed `..._StillBuysTheNextCycleEntry`. |
| 15 | LOW | The docs said the retreat buy takes an upgrade or beast first; it follows the cycle in order, and only while no lane is under threat. (Lens 5 final check) | Doc accuracy | Written from the intent. | Rewritten from the code. |
| 16 | LOW | A hero bought while every lane is on Fall back goes to lane 0, the older fallback, while the pack goes to the lane with the most room. (Lens 5 final check) | Inconsistency | Older code. | Accepted and documented in the balance log: the hero spawns at the base and joins the regroup. |
| 17 | LOW | An enemy sampled before its death still counts in `NearEnemyCount` until the next snapshot. (Codex re-review) | Timing | | Accepted: one snapshot interval. |

## Constants

`FallBackRatio` 1.6, `ReturnRatio` 1.1, `RegroupMinEnemy` 10, `PressureRadius` 40 m and `HeroPressureValue` 250 are the
orchestrator's calls, not Mike's. The balance log names them and the undo: set `FallBackRatio` so high that the rule never
fires (1000) to turn off the AI half, and remove the `FightsAtOwnTower` test in `LaneMarchRules.Evaluate` to turn off the
tower fight.
