# Codex pre-review: The Long Defeat kill feed (#1159)

You are an independent reviewer. Read only; change no file. Report findings with file:line evidence, severity (CRITICAL, HIGH, MEDIUM, LOW) and a failure scenario. Missing engine evidence stays UNVERIFIED.

## Scope

The uncommitted change in this worktree (`git diff` against HEAD `eeb664f4a`, branch `fix/ld-kill-feed`):

- `Client/Features/LongDefeat/LongDefeatMissionViews.cs`: adds `ViewCreator.CreateSingleplayerMissionKillNotificationUIHandler()` to the `[ViewMethod("TaomLongDefeat")]` view list, and a doc comment.
- `TAOM.Tests/Features/LongDefeat/Client/LongDefeatHudViewTests.cs`: a source test that pins the line.
- `docs/features/long-defeat.md`: one sentence, two changelog rows, two issue rows.

Intent: players asked for the kill feed in The Long Defeat. Every vanilla Custom Battle view list adds this call; the Long Defeat list was built by hand and left it out.

## Questions

1. Does the vanilla handler (`MissionSingleplayerKillNotificationUIHandler`, overridden by `MissionGauntletKillNotificationSingleplayerUIHandler`, base `MissionBattleUIBaseView`) need any mission behavior, team, campaign object or deployment state that the Long Defeat mission lacks? The mission is a `CustomGame` siege with no `BattleEndLogic`, no vanilla spawn logic and no deployment phase; soldiers spawn in waves after the start; the player can take over another soldier after death; a run can last over an hour with several hundred kills per wave.
2. Does any TAOM code (Harmony patch, `[DefaultView]`, `[OverrideView]`, GUI prefab override of `SingleplayerKillfeed`, `CombatLogManager` handler) interact with this handler or its VM in a way that differs in this mission?
3. Does adding a Gauntlet layer at `ViewOrderPriority` 17 conflict with the Long Defeat HUD and shop layers (`Client/Features/LongDefeat/LongDefeatHudView.cs`) for input or focus, including the unattended autorun path?
4. Is the doc text accurate about the options (`BannerlordConfig.KillFeedVisualType`, `ReportPersonalDamage`, `HideBattleUI`)?

Engine source: the installed DLLs are the truth (`pwsh tools/taom-src.ps1 path <Type>`); the dump at `E:\Decompiled_Bannerlord\_modules_build` and `_shipping_build` is v1.5.7.
