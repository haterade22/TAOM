# Scenario Autorun (unattended campaign runs)

## Overview

The scenario autorun starts a new sandbox campaign and lets the AI play it with nobody at the keyboard. It skips character creation,
keeps the player's party out of the world, holds time on fast forward until the wanted number of days has passed, then writes a done
file, ends the campaign and quits the game. `tools/run_scenario.py` launches the game, waits for the done file and judges the run.

## Why This Exists

- **Vanilla behavior:** a campaign needs a person at every step: the menu, character creation, the map, every popup.
- **TAOM requirement:** balance work (#1283) needs an AI campaign run to day 150 and read through `analyze_war_ledger.py`. A person
  playing that run spends hours and keeps the party out of the wars by hand.
- **Without this feature:** every balance check is a manual playthrough, so few are done.

The design is #1075's plan (Mike's decisions of 2026-11-01). It is built clean room from the TaleWorlds decompile and the TAOM repo
only: ArmorySim stays a standalone tool (the 2026-10-18 rule in `.claude/skills/upstream-review/SKILL.md`).

## Architecture

### Design Challenge

The run crosses four game states (main menu, intro video, character creation, map), each with its own screen code, and the engine stops
time for popups, focus loss and some menus. The driver must also never arm in a player's session and never run on a dedicated server.

### Solution Approach

- `ScenarioPlan` reads the arm file named by the environment variable `TAOM_AUTORUN`. No file fallback: a stray file never arms a
  session. A second autorun variable (`TAOM_FORDS_AUTORUN`, `TAOM_LONGDEFEAT_AUTORUN`, `TAOM_COMMAND_TRIAL`) leaves the plan unarmed.
- After the done file, the driver waits up to 60 s for the main menu and then quits; a wait that runs out logs `exit_stalled`. The
  launcher kills the game 120 s after it sees the done file, so the game quits first.
- `ScenarioAutorunDriver` is a pure state machine ticked with wall seconds from the client's application tick. Phases: `WaitMenu`,
  `Creation`, `WaitMap`, `Running`, `Exiting`, `Done`. The decisions are in `ScenarioRules`; every wall and day gate fails on NaN.
- `ICampaignAutorunHost` holds every engine call. Core registers `NullCampaignAutorunHost`; TAOM.Client replaces it with
  `CampaignAutorunHost` (ADR-013), so a dedicated server never runs the driver.
- Every exit path writes `autorun_done.txt` once and one `[Check]` line, then ends the campaign. A throw finishes the run as
  `driver_error`.

Two choices differ from the #1075 plan. The Fords pieces are not extracted into a shared base, and there is no `_game_launch.py`:
`run_scenario.py` reuses `run_fords_autorun.py` by swapping names, as `run_long_defeat.py` does. The extraction is a follow-up on #1075.

### Component Diagram

```
tools/run_scenario.py  --(arm file, TAOM_AUTORUN)-->  ScenarioPlan
        ^                                                 |
        | autorun_done.txt, autorun_status.txt    ScenarioAutorunDriver (Main, pure)
        |                                                 |
        +---------------------------------------  ICampaignAutorunHost
                                                         |
                                     CampaignAutorunHost (TAOM.Client, engine calls)
```

## Configuration

### Arm file (written by `run_scenario.py`)

UTF-8 `key=value` lines. An unknown key or a bad value is a warning and keeps the default.

| Key | Type | Description |
|-----|------|-------------|
| `kind` | string | Must be `campaign-new`, or the plan is unarmed |
| `out` | path | Absolute folder for the done and status files; required |
| `scenario` | string | A free label, echoed in the log and the done file (default `-`) |
| `culture` | string | The player's culture id (default `empire_w`, Gondor); checked at the culture stage |
| `days` | int | Campaign days to run, 1 to 1000 (default 3) |
| `speed` | float | Fast forward multiplier, above 0, at most 30 (default 15) |
| `stall_seconds` | number | Wall seconds with no new day before the run stops as `stalled` (default 600) |
| `wall_limit_seconds` | number | Wall seconds from the campaign start before the run stops as `wall_limit` (default 3600) |
| `quit` | flag | Quit the game after the run |

### Done reasons

`ok`, `start_refused`, `creation_failed`, `creation_timeout`, `map_timeout`, `stalled`, `wall_limit`, `driver_error`.

## Engine facts

All from the v1.5.7 decompile (`pwsh tools/taom-src.ps1 path <Type>`).

- **Start:** TAOM's start options provider makes the menu button open an options screen, so the host starts the campaign itself:
  `MBGameManager.StartNewGame(new SandBoxGameManager(...))` with `AdvancedStartOptionsManager.CreateCampaignStartOptions().GetChangedOptions()`,
  as `SandBoxViewSubModule.StartGame` does (lines 278 to 287).
- **Intro video:** Patch58 already skips it; `VideoPlaybackState.OnVideoFinished()` is the fallback.
- **Character creation:** `CharacterCreationManager.RemoveStage<T>()` is public. The host sets the culture with `SetSelectedCulture`,
  removes the six later stages and calls `NextStage()`; at the end of the stage list `NextStage` runs `ApplyFinalEffects()` and
  `FinalizeCharacterCreationState()`, so TAOM's finalize handlers still run.
- **Main party:** `IgnoreByOtherPartiesTill(CampaignTime.Never)` and `SetMoveModeHold()`, the engine's own
  `set_main_party_attackable 0` cheat.
- **Time:** `CampaignTimeControlMode.UnstoppableFastForward` (the stoppable mode stops on Hold). `SpeedUpMultiplier` has no setter cap.
- **What stops time:** `TimeControlModeLock`, and the escape menu that focus loss opens (`MapScreen` asks its map views on focus
  loss). The host sets `BannerlordConfig.StopGameOnFocusLost = false` in memory when it starts the campaign. The engine saves that
  config on shutdown: `Module.FinalizeModule` calls `Game.Current.OnFinalize()` (which ends in `Game.Destroy` and every submodule's
  `OnGameEnd`) and then `BannerlordConfig.Save()` (Module.cs lines 1348 to 1362). So `ClientSubModule.OnGameEnd` puts the player's
  value back at every game end, before that save, whoever ended the game. A crash or a kill does not save. MCM is never written.
- **Autosave:** the host replaces `SandBoxManager.SandBoxSaveManager` with an `ISaveManager` that disables autosave.
- **Popups:** `InformationManager.HideInquiry()` and `MBInformationManager.HideSceneNotification()`. No API gives an open inquiry's title,
  so the host listens to `OnShowInquiry` and `OnShowTextInquiry` and logs, for the first 20 hides, the newest title shown. With two
  inquiries queued, that title can be the queued one's, and a multi-selection inquiry logs an empty or old title. The titles are for
  the log only.
- **Day count:** `CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow`.
- **Exit:** `MBGameManager.EndGame()`, then `TaleWorlds.Engine.Utilities.QuitGame()`.

UNVERIFIED until the first run: campaign days per wall minute at speed 15; that removing stages leaves TAOM's career and Patch78
handlers whole; that `NextStage` is safe with the Gauntlet view open; that the native quit runs managed shutdown; which popups
`CampStartInCamp` and `KingdomJoinOfferService` open.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Autorun/AutorunModule.cs` | Feature module: registers the plan, the null host and the driver |
| `Main/Features/Autorun/Scenario/ScenarioPlan.cs`, `ScenarioArmFile.cs` | Arm variable and arm file parse |
| `Main/Features/Autorun/Scenario/ScenarioAutorunDriver.cs` | The state machine |
| `Main/Features/Autorun/Scenario/ScenarioRules.cs` | Pure decisions and limits |
| `Main/Features/Autorun/Scenario/ScenarioAutorunDone.cs`, `ScenarioOutcomes.cs` | Done and status file text, the `[Check]` line |
| `Main/Features/Autorun/Scenario/ICampaignAutorunHost.cs`, `NullCampaignAutorunHost.cs` | Engine seam and its core null |
| `Client/Features/Autorun/CampaignAutorunHost.cs` | The engine calls (TAOM.Client) |
| `Client/ClientSubModule.cs`, `Client/TaomClientComposition.cs` | Tick and registration |
| `tools/run_scenario.py` | Launcher and judge |
| `tools/ingame_checks/1075.toml` | Manifest for `taom.autorun.campaign-new.done` |

## Tests

- `TAOM.Tests/Features/Autorun/Scenario/`: the plan and arm file, the rules, the driver against `FakeCampaignAutorunHost`, the done
  text and outcomes, and the client wiring (`ScenarioClientWiringTests`, `RequiresGame`).
- `tools/tests/test_run_scenario.py`: the launcher's arguments, arm file and judge.
- Co-op: the feature adds no campaign behavior; a dedicated server keeps the null host.

## How to verify in game

**Preconditions:** no game running, the idle gate passes, the checkout is deployed by the runner. A run starts the game unattended,
so it needs Mike's word.

1. Run `python tools/run_scenario.py --days 3`. The TAOM log shows
   `[Check] id=taom.autorun.campaign-new.done result=pass reason=ok`, and the run folder under `E:\Temp\scenario-runs\` holds
   `autorun_done.txt` with `reason=ok`.
2. Measure the days per minute from the log: the `[Autorun][Scenario] phase WaitMap -> Running wall=` line and the `phase Running ->
   Exiting wall=` line give the running seconds, and `days_run` in the done file gives the days. (`wall_seconds` in the done file also
   counts boot, the menu, creation and the map load.) Then set a wall limit for the long run.
3. Run `python tools/run_scenario.py --days 150`. The run folder holds `ledger.txt` from `analyze_war_ledger.py`.

## Changelog

- 2026-11-15: first slice, `campaign-new` only (#1075, for #1283).

## GitHub Issue

- **Issue:** #1075: Unattended scenario runs: start a sandbox campaign, run days and save with nobody at the keyboard
- **Status:** Open
