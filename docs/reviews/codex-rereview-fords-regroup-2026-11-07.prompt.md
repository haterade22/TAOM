# Codex re-review: Fords of War AI regroup, after the fixes (2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). Review only: edit, create or delete nothing; do not
stage, commit or stash; do not run `dotnet build`, `dotnet test` or `build.ps1` (the full suite runs elsewhere on this machine).
The orchestrator ran the FordsOfWar and gate tests after the fixes: 2,538 passed, 0 failed, 3 skipped.

## Scope

Worktree `E:/repos/taom-fords-trial` (detached). **The change under review is the unstaged diff only: `git diff`.** The index
holds six other, already reviewed changes. Your earlier review of this rule is
`docs/reviews/raw/codex-prereview-fords-regroup-2026-11-07.md` (four MEDIUM findings). Two Claude lenses found the same gaps plus:
whole-lane headcount ignores unit quality (Rivendell and Mirkwood lanes cap at 40 against 90 to 123, and the elves win their
fights by value), the tower test measured the lead instead of the enemy, and the log line had no side.

## What the fixes do (claims to check, not facts)

1. The regroup compares the value (`UnitRecord.Value`) of own and enemy units within `PressureRadius` (40 m) of the lane's lead,
   computed in `FordsMatch.View.cs` from new `LaneSnapshot.UnitKeys` filled by `FordsSnapshotBuilder`; new `LaneView` fields
   `NearOwnValue`, `NearEnemyValue`, `NearEnemyCount`, `AtOwnTower`. Entry: own value above 0, at least `RegroupMinEnemy` (10)
   enemies, enemy value above 1.6 times own.
2. Release: value ratio at most 1.1, or the lead at its own tower with no enemy within 40 m. The lane leaves `_regrouping` only on
   a later visit where its order is no longer FallBack.
3. `_regrouping` is cleared before the Seize early exit.
4. With no assault lane, and no lane whose Threat is above its own count, `BuySparePack` runs.
5. `FightsAtOwnTower` tests the nearest enemy's position against the anchor's fire range, plus `SiegeStayMargin` while charging.
6. The log line names the side.

## Questions

1. Is each of your four earlier findings fixed? Quote the lines.
2. Is the new data path sound: do `UnitKeys` and `UnitPositions` stay aligned in every builder path (filters, NaN positions,
   heroes, creatures, units with no lane)? Is the lookup of a key in the records correct for a unit that died this tick?
3. Can the release by "at own tower, nobody within 40 m" cause a short loop of regroup, release, push, regroup at the order
   cooldown? Is that acceptable as "regroup, gather, go again", or does it waste the lane?
4. Any new defect in the fixes, any missing test, any simpler design.

## Output

Per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), location, failure scenario, evidence. Then a one-line verdict per question.
