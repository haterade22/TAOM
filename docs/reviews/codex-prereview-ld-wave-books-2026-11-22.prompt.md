TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2, game v1.5.7). Review the uncommitted change in this working tree (branch `fix/ld-attack-wave-books`, HEAD `git rev-parse HEAD`). This is a read-only review: do not edit, stage, build or commit anything. Show the change with `git diff HEAD`. Engine source of the installed game, if you need it: `pwsh tools/taom-src.ps1 path <Type>`.

## The defect

The Long Defeat director (`Main/Features/LongDefeat/`) also hosts story missions (`Main/Features/StoryMissions/`). In a story ATTACK (#1409) the player's team is the attacker team, so the player's own soldiers are the wave whose count the wave check judges. `LongDefeatBattlefieldAdapter.Classify` (`Main/Adapters/LongDefeatBattlefieldAdapter.Diagnostics.cs`, about line 200) books the player's body on his own team as `LdRemovalKind.Player`: his fall, never a wave kill. A fallen player can take over one of his soldiers (a person with the take-over key, or the unattended bot through `TakeOverForBot`, `Main/Adapters/LongDefeatBattlefieldAdapter.Lord.cs`). In an attack that soldier is one of the wave's count. After the take-over his fall is the player's, and the attacker read and the stalemate fell skip him, so `kills + felled` is one short for each take-over. Evidence: run `E:/Temp/scenario-runs/20261122-093421`, `[Check] id=taom.story-missions.hosted.wave result=fail kills=345 felled=4 lost=66 seconds=805.7 count=350` after one bot take-over. The play and the story verdict were right; only the check's books were wrong.

## The change

- `SurvivalRun.AttackerTaken()` (new) counts a wave soldier that became the player's body; the count resets at each wave start and reaches `WaveStats.Taken` through `RunStats.CloseWave`.
- `LongDefeatDirector.AdoptIfNew` calls it at the adoption of a new main body after a death, when the adoption is a take-over (not a rise), the player attacks (`_playerAttacks`) and the body is not a held hero.
- `LongDefeatChecks.Wave` accepts `kills + felled + taken >= count` and prints ` taken=N` after `felled=` only when N is above zero, so every line of a defence is byte-identical to before.
- Tests in `TAOM.Tests/Features/LongDefeat/LongDefeatDirectorLordTests.cs`; a manifest claim in `tools/ingame_checks/1409.toml`; two docs.

## What to look for

1. **Is the count right in every order of events?** The take-over is detected at `AdoptIfNew` (the tick after the engine made the soldier the main agent). Find an order where the taken soldier is counted twice or not at all: he dies in the same tick he was taken (before the adoption); the wave clears between the take and the adoption; he is taken in one wave and the wave count resets (`_waveTaken = 0` at the wave start) before the check is written; a second fall and a second take-over in one wave; a rise (`RiseLord`) that hands the taken soldier back to AI (is he then a wave soldier again, and is a later kill of him booked as a kill, so that `kills + felled + taken` exceeds the count: is exceeding harmless everywhere `Taken` is read?); a person's take-over while the lord still lives (no death pending).
2. **Is the body always a wave soldier?** In an attack, what else can the new main body be: a hero (handled by `heldHero`), a soldier of a formation that is not part of the wave count (summoned soldiers, reinforcements the story adds, a commander), a body on a third team in co-op? Read `TakeOverForBot` and `Mission.CanTakeControlOfAgent` in the installed source.
3. **`_playerAttacks`.** Where is it set (`LongDefeatDirector.cs`, from the field's boot facts) and can it be stale or wrong at the adoption: a co-op follower, a mission where the player switches sides, the boot of a defence?
4. **Other readers of the wave's books.** Who else reads `WaveStats.Kills`, `Felled`, the run totals or the wave's clear condition and now disagrees with the check: the run report (`Diagnostics/LongDefeatRunReport.cs`, which does not write `taken`), the leaderboard replay, the Python judges (`tools/run_long_defeat.py`, the story runner), the story verdict (`StoryVerdict.cs`, `StoryEndWatch.cs`), the straggler-share half of the wave check, the `no-rout` check, gold (a taken soldier pays no bounty: right or wrong)?
5. **Manifests.** Does any `[[check]]` row in `tools/ingame_checks/*.toml` match the wave line with a pattern that the new ` taken=N` token breaks (it appears only when N is above zero)?
6. **A defence must be unchanged.** Find any path where a defence run now prints `taken=` or passes a wave it failed before.
7. **Tests that pin nothing.** Would each of the three new tests fail if `AttackerTaken` were never called, or called always?

## Output

Findings most severe first, each with a severity (P1 breaks play, a co-op run or a campaign; P2 a defect a game run will meet; P3 minor), the file and line, the concrete state that shows it, and the engine or TAOM line you read. Say UNVERIFIED for any claim you did not read the source for. End with what you checked and found correct.
