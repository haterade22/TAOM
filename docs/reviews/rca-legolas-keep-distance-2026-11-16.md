# RCA: Legolas keeps out of melee range (#1415)

Findings from the tier 1 review of the uncommitted worktree `feat/legolas-keep-distance` over bb2db8a7b: the step away from a melee threat (`HeroKeepDistance`, `KeepDistanceRule`), its config values, its tally and check, `tools/ingame_checks/1415.toml` and the doc. One wave ran two lenses (5 Data flow, 2 Engine compatibility). It found no CRITICAL and no HIGH, so no second wave ran. The Codex pass did not run: the maintainer said to skip it while the account has no usage.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | The keep-distance check passed on `steps >= 1`, and a step was counted when the engine call ran. The effect the feature exists for is native and unproven: that the scripted frame wins over his formation's Move order, and that the Hold state's speed cap (`HumanAIComponent.cs:622-633, :673-687`) does not slow the step. A run where he never moved would pass | A value read back from the layer that set it | The check was written for the call, the only thing the managed side sees | At each step the start and target are stored; when the hold ends, `KeepDistanceRule.Walked` counts `moved` if his body covered half the step along its line. The check passes on `moved >= 1` and fails on ten steps or ten cornered looks with no move. Lesson below |
| 2 | MED | `CanStep` re-implemented part of the engine's own gate for a scripted move and missed four conditions: detached, a scripted move already set, using a game object, in a ladder queue. An item pickup walk (Legolas keeps arrow pickup on) or a ladder queue could be overwritten | Hand-made guard beside an engine method that answers the same question | The guard was written from the obvious cases (player, mount, fleeing) | `CanStep` calls `Agent.CanBeAssignedForScriptedMovement()` after the `HumanAIComponent` null check, plus `!IsMainAgent` and `!HasMount` |
| 3 | LOW | `Usable` built a new `WorldPosition` with no navmesh face, while the engine builds the move's target from his own position plus `SetVec2` (`HumanAIComponent.cs:825-826`); the two can resolve different faces under an overhang | A test point built differently from the engine's target | The constructor looked equal | `var to = from; to.SetVec2(...)` |
| 4 | LOW | The rate limiter read `now < _nextTick`, which lets a NaN through every tick | Comparison polarity | The rule's own NaN polarity was not carried to the hook | `!(now >= _nextTick)` |
| 5 | LOW | `MaxStepBack` was 20 m, more than a run covers in the 2.5 s hold | A limit with no tie to the hold | Picked as a round number | 12 m; config text and doc updated; the range test uses 13 |
| 6 | LOW | Text that was wrong or unproven: `ArrangementOrder.cs:347` called "vanilla's test for a formation place" (it is in the unused `CreateStrategicAreas`), the query comment said it returns mounts and dead bodies (native returns active humanoids of hostile teams; the duplicate filters were deleted), "faces the threats" and "his formation takes him back" stated as fact, the checks summary named only 1361, the tallies comment said "both off", and the manifest did not say the story mission is not judged or that a cornered look starts the cooldown | Text written from names, not from the source | Each claim sat in a comment or doc no test reads | Each corrected or marked UNVERIFIED |
| 7 | LOW | The tallies tests had no case for the `KeepDistance` switch | Missing test | Added with the switch | `Of_KeepDistance_NeedsBothValues` |

Not applied:

- A guard for a timer of exactly zero, and `DisableTimedScriptedMovement` on a takeover: both are speculative; the simplicity rule asks the maintainer first.
- `hero.CanMoveDirectlyToPosition` in place of the navmesh, boundary and path tests (lens 2, cheaper option): it tests a straight line, stricter than the path rule; the one unattended run decides whether the path query costs anything.

## Root-cause pattern

Findings 1 and 2 share one habit: the managed side was trusted to describe a native effect. The call was counted as the move, and a hand list of conditions stood in for the engine's own gate.

## Why each reviewer missed these

- **The builder** wrote the check from what the hook can see and the guard from the cases the design named.
- **Lens 5** traced the tally to the verdict and saw that it read back the call. **Lens 2** read the Hold state's speed cap, the engine gate and the native target build, and found the doc claims that the source does not support.
