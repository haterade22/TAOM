# Codex re-review: Fords of War culture tactics, fix pass (2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). This is a review only:

- Do not edit, create or delete any file. Do not stage, commit or stash.
- Do not run `dotnet build`, `dotnet test` or `build.ps1`. A Bannerlord match runs on this machine and needs the CPU. The orchestrator ran the tests after the fixes: the tactics tests 29 passed, 0 failed; the Fords, gate and architecture filter 3,447 passed, 0 failed, 3 skipped.
- Read files, `git diff`, `git status`, and the installed engine's decompiled sources: `pwsh tools/taom-src.ps1 path <Type>` (cache `C:\Users\mikew\.taom-src\v1.5.6-202611041234`). Look up one type at a time.

## Context

The first review of this change is `docs/reviews/codex-prereview-fords-culture-tactics-2026-11-07.prompt.md` (your earlier pass answered it). The whole change is the uncommitted diff of this worktree (branch `feat/fords-culture-tactics`, base `6791dbc75`). The fix pass answered these findings:

| Finding | Fix |
|---|---|
| A shield wall gives its units `DefensiveArrangementMove`, ranged weight 0 | The four ShieldWall factions' `closeRange` is 20 in `factions.json`; a shipped-data test pins `CloseAt <= 20` for every ShieldWall or Square faction |
| Your F1: the vertex target can stall beside an enemy off the path; flapping at the Close/Charge line | Close moves to the enemy's position snapped to a 5 m grid (`LaneMarchRules.CloseGrid`, Z kept) |
| Flapping at the Close/Charge line | A charge latch: `_charging` per side and lane; a charging lane keeps charging while the enemy is within `CloseAt` |
| A lead level with or past the tower got a Move back from Close, which check 9 fails | `Close` returns null when the lead or the snapped target is level with or past the next enemy tower's arc (`LevelOrPast`, shared with `TowerInReach`) |
| Close outranked Seize and pulled a lane off its objective | Under Seize, `Close` returns `SeizeCommand` (the hold at the objective), or null when that command is a camp Charge |
| Your F2: a block with only `close` kept the wall on every branch | `LaneTactic.ArrangementFor` returns Line on other branches when only `close` is named |
| Your F3: the reader accepted "2" and "Loose, ShieldWall" | Exact enum names only |
| Your F4, F5 and doc items | `docs/features/fords-of-war.md` "Culture tactics", the lane-rule lines, Seize, Known limitations; `balance-log.md` round 4; the director comment |

## Questions (answer each with file:line evidence; missing engine evidence is UNVERIFIED)

1. Is each fix above correct and complete for both sides (West and East), and does any fix add a new defect? Read `LaneMarchRules.Evaluate`, `Close`, `LevelOrPast`, `SeizeCommand`, `TowerInReach`.
2. The latch: `_charging` is added on the Charge branch and removed on every other branch. Can a lane stay latched wrongly (for example across a Fall back, a siege, an empty lane that returns early before the add or remove, or an order change), and does a faction with no tactic behave exactly as before?
3. The snapped target: can a target on the 5 m grid be off the navmesh or outside the lane's bars, and what does the director or adapter do with a MoveTo point the engine refuses (read how the director handles a refused move)? Does an enemy from another lane (the keep, `anyLane`) draw a lane off its path?
4. The docs' engine claims in "Culture tactics" ("What a shield wall does in the engine"): confirm or correct each from the engine source.
5. Anything else in the diff that is wrong.

## Output

Per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), location, failure scenario, evidence. Then a one-line verdict per question.
