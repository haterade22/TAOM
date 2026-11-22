TAOM is a Lord of the Rings total conversion for Mount & Blade II: Bannerlord (.NET Framework 4.7.2). You are reviewing an uncommitted change for issue #1079 (The Long Defeat, Phase 4, Slice 4, with the fix for #1143) in this working tree, a sibling worktree on branch `feat/ld-phase4` at HEAD `479e731f8`. This is a read-only review: do not edit, stage, build or commit anything.

## Engine version

The installed game is Bannerlord v1.5.7 and the worktree pins v1.5.7. Read engine code from the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file (one type per call), for example `TaleWorlds.MountAndBlade.Mission`, `TaleWorlds.MountAndBlade.Agent`, `TaleWorlds.MountAndBlade.Team`, `TaleWorlds.MountAndBlade.Formation`.

## The request and the maintainer's decisions

The Long Defeat is a wave survival mode on siege maps. Phase 4's death rule (plan: `docs/features/long-defeat/phase4-plan.md`, read the "Re-baseline, 2026-11-21" section first, then D40, D41, D44a, D47, D48, D49): when the lord falls in a wave the player may take over a soldier; at the next cleared wave he rises in a body of his own at the keep, and a taken soldier is handed back to the AI. The pure half (commit `0e68b32d1`, already reviewed) orders these through `RunEffects`. This change is the engine half. Decisions that stand:

1. The lord is spawned first and a taken soldier is handed back (`Controller = AI`) only after that spawn produced a live body.
2. The unattended bot takes the soldier nearest to where the lord's last live body stood, through the engine's own `Mission.CanTakeControlOfAgent` and `Mission.TakeControlOfAgent`.
3. The general's seat while the lord is down goes to a standing shop hero, else to nobody, never to a dead body.
4. The `lord_falls` test option fells the lord's own body with a blow owned by a standing enemy, never through `KillAgentCheat`.
5. Whether a death was the forced one is a fact the field reports for the body it struck, not an inference from timing.
6. No in-game run exists yet for any of this; the in-game spike follows this review. Do not report "unproven in game" as a finding; do report anything the engine source contradicts.

Do not re-litigate these six.

## Implementer's design choices (open to challenge)

- `Main/Adapters/ILongDefeatLordAdapter.cs` is a new narrow interface, implemented by `Main/Adapters/LongDefeatBattlefieldAdapter.Lord.cs`; `LongDefeatRunHost` hands the director `field as ILongDefeatLordAdapter`, so EVERY real run now has it: a solo run, a co-op host (`Main/Features/LongDefeat/Coop/`) and a story mission that hosts the run inside a campaign (`Main/Features/StoryMissions/`, where the player may be the attacker). A director with no adapter (tests) keeps the old respawn.
- `SpawnPlayer`'s foot respawn (`LongDefeatBattlefieldAdapter.Player.cs`) now passes the rise place to `Spawn` with the reinforcement flag still forced.
- The rise place is the recorded keep, else the scene's keep frame (`KeepFromScene`), the home point for a mounted class, and no place at all (label `respawn`) when the player attacks.
- `LongDefeatLordWatch` gained `MarkForced` / `IsForced` / `ClearForced` (a volatile object reference); `Classify` reports `LdRemoval.Forced`; `FellLord` marks the body, strikes, and withdraws the mark when `Live(lord)` is still true right after `RegisterBlow`.
- `Fell` and `FellLord` share one blow builder, `StrikeFelled`.
- The last position of the main agent is kept as three floats, written in `ReadLord` on every tick the lord is live.
- New checks `rise` and `general-down` (`Diagnostics/LongDefeatLordChecks.cs`), judged one tick after the event; `player-death` gained `via=rise` and `by=`; `fort-points` gained a `rise=` clause.
- `tools/run_long_defeat.py`: `LORD_FALLS_DELIVERED = True`, and the owed rules `owes_rise`, `owes_general_down`, `owes_player_death`; `tools/ingame_checks/1079.toml` has the two new rows.
- #1143 (same tree, separate concern): `Domain/CareProof.cs`; the unattended bot buys field care only when `share * (1 - mean health)` is above 1.25 times the check's provable rise; the director passes `read.DefenderHealthMean` in the tick's `RunSnapshot`.

## What to look for, in order of the orchestrator's doubt

1. **The body swap against the engine.** Read `Agent.Controller`'s setter, `Mission.MainAgent`'s setter and its `OnMainAgentChanged` subscribers, `Mission.SpawnAgent` / `SpawnTroopWithAgentBuildData`, and vanilla's own swap (Mission.cs around 4736-4751, which sets the old main agent to AI BEFORE the new one to Player). For a few frames this code has two `Player`-controlled bodies, the taken soldier and the new lord. Does anything the engine does in that window break: the camera and `MissionScreen`, `Formation.OnAgentControllerChanged` and `HasPlayerControlledTroop`, the order controller owner, `Team.PlayerOrderController`, spectator views, `MissionMainAgentController`? Is the reverse order (hand back first) safer, and what would it cost given `CanTakeControlOfAgent` needs `MainAgent == null`?
2. **The spawn with a place.** `Spawn` builds `GetAgentBuildDataToSpawnTroop(..., initialPosition, initialDirection, formationIndex)` and then forces `IsReinforcement(true)`. With an initial position AND a formation AND the reinforcement flag, where does `Mission.SpawnAgent` put the agent in v1.5.7? Does the forced flag override the position? Compare with the mounted path, which already passes a place.
3. **`FellLord`.** Does `Agent.RegisterBlow` with this blow (blunt, `DamageCalculated = true`, 2000 on a body clamped to at most 1000 health) kill synchronously, so `Live(lord)` is false on the next line, for a player-controlled main agent and for an AI-driven one? Can a player's body be exempt (invulnerability flags, `Agent.Invulnerable`, mission difficulty)? Who gets the kill and what does the morale model read from the affector (`CustomBattleMoraleModel`)? Is the mark cleared on every path, and can a stale mark flag a later death?
4. **`TakeOverForBot`.** Under autoplay the dead lord's adopted body had `Controller = AI`; is `Mission.MainAgent` really null after his removal (Mission.cs `OnAgentRemoved`)? What does `TakeControlOfAgent` do to fast forward, and does the Long Defeat speed adapter restore it (`Main/Adapters/LongDefeatMissionSpeed.cs`)? A taken soldier who is in the towers' posts formation or the heroes' formation: what breaks?
5. **Every real run now takes the new path.** Trace a co-op host and a follower, and a story mission as defender and as attacker: the rise place, `PlayerSideTeam`, `Side(!PlayerAttacks)`, the general's seat on the attackers' team, and whether a campaign hero is spawned twice or a campaign-side death is booked.
6. **The checks cannot lie.** For `rise`, `general-down` and the extended `player-death`: a state where the check writes a pass and the behaviour is broken, or where a correct run writes a fail (timing of the one-tick delay, a run that ends on that tick, a take-over that beats the read). The log sanitiser glues bare words in a check's detail to the value before them, so every word must be `name=value`.
7. **The wrapper.** `owes_rise`, `owes_general_down`, `owes_player_death` and the two manifest rows: a report and log this build writes that the judge refuses, and a broken run it accepts. `--lord-falls` is now accepted: with `--lose`, `--deep`, `--class cavalry`, `--start-wave`.
8. **Threads and handles.** Any new engine call reachable from a removal or hit callback; any `Agent` held across frames without the `Live` gate; anything keyed on `Agent.Index`.
9. **#1143.** A bot run that now never buys care although the proof arm (`--waves 2 --garrison-scale 1.3 --start-gold 5000`) expects it; the mean read on the tick of the buy against the mean at the heal.
10. Tests that pass with the behaviour broken, and behaviour with no test.

## Files in scope

Show a modified file's change with `git diff HEAD -- <path>`; read a new file whole (`git ls-files --others --exclude-standard`).

New: `Main/Adapters/ILongDefeatLordAdapter.cs`, `Main/Adapters/LongDefeatBattlefieldAdapter.Lord.cs`, `Main/Features/LongDefeat/LongDefeatDirector.Lord.cs`, `Main/Features/LongDefeat/Domain/LdLord.cs`, `Main/Features/LongDefeat/Domain/CareProof.cs`, `Main/Features/LongDefeat/Diagnostics/LongDefeatLordChecks.cs`, and under `TAOM.Tests/Features/LongDefeat/`: `FakeLongDefeatField.Lord.cs`, `LongDefeatDirectorLordTests.cs`, `LongDefeatLordPinTests.cs`, `Diagnostics/LongDefeatLordChecksTests.cs`.

Modified: `Main/Adapters/LongDefeatBattlefieldAdapter.cs`, `.Player.cs`, `.Diagnostics.cs`, `LongDefeatLordWatch.cs`; `Main/Features/LongDefeat/LongDefeatDirector.cs`, `.Effects.cs`, `.Gates.cs`, `.Player.cs`, `LongDefeatRunHost.cs`; `Main/Features/LongDefeat/Diagnostics/LongDefeatChecks.cs`, `LongDefeatFortChecks.cs`, `LongDefeatGateChecks.cs`; `Main/Features/LongDefeat/Domain/FieldFacts.cs`, `LordLife.cs`, `SurvivalRun.Lord.cs`, `SurvivalRun.cs`, `SurvivalRun.Fort.cs`, `RunSnapshot.cs`, `RunView.cs`, `ShopBot.cs`; the test files `git diff --stat HEAD` lists; `tools/ingame_checks/1079.toml`, `tools/run_long_defeat.py`, `tools/tests/test_run_long_defeat.py`.

## Output

List findings most severe first. For each: a severity (P1 a defect that breaks play, a co-op run or a campaign; P2 a defect a game run of this mode will meet; P3 minor), the file and line, the concrete state that shows it, and the engine or TAOM line you read (quote it). Say UNVERIFIED for any claim you did not read the source for. End with the list of things you checked and found correct.
