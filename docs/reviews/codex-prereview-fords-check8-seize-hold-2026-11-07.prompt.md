# Codex pre-review: Fords of War check 8, a Seize hold is not a lane hold (2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). Review only: edit, create or delete nothing; do not
stage, commit or stash; do not run `dotnet build`, `dotnet test` or `build.ps1` (the full suite runs elsewhere on this machine).
The orchestrator ran the FordsOfWar and gate tests after the change: 2,505 passed, 0 failed, 3 skipped. The two new Seize tests
failed before the probe change.

## Scope

Worktree `E:/repos/taom-fords-check8` (branch `fix/fords-check8-seize-hold`). **The change under review is the unstaged diff:
`git diff`.** Three files: `Main/Features/FordsOfWar/FordsFieldProbe.cs`, `TAOM.Tests/Features/FordsOfWar/FordsFieldProbeTests.cs`,
`docs/features/fords-of-war.md`.

## The defect

Check 8 (the run report's hold check) judged a lane on Seize as a held lane. `LaneMarchRules.SeizeCommand` gives
`LaneCommand.HoldAt(seize.Position)` for a capture point, the same command kind a lane on Hold gets at its hold point. The probe
measures "settled" along the lane's axis (`AlongAxis`), so with an objective off the lane, units that stood beside an enemy tower
looked as if they had reached the hold point, and check 8 failed with `East1 in fire of tower_west_1_0` (run 20261107-185556).

## The change

`FordsFieldProbe.Sample` skips a HoldAt whose target equals (exact `Vec3f` equality) a layout capture point or camp position.
The claims behind exact equality, to check, not to trust: `SeizeRules` builds `SeizeTarget` from the layout slots' own positions;
`FordsMissionDirector.ApplyCommand` stores the rules' command in `_applied`, not the fallback waypoint `Send` may move it to.

## Questions (file:line evidence)

1. Is exact equality sound in every path from the layout to `_applied` (map loading, any copy or transform of positions, a
   fallback waypoint, a camp that is cleared)? Could a lane hold point ever equal an objective position?
2. Does anything else read `applied` HoldAt entries or `HoldSettled` for a lane on Seize (check 8's other numbers, the decision
   trace, other checks), and does the change leave them right?
3. Is there a simpler or more robust design (a separate command kind for Seize, or the lane's order passed to the probe)?
4. Do the tests prove the behaviour, and is any case missing?

## Output

Per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), location, failure scenario, evidence. Then a one-line verdict per question.
