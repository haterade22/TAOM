# Story Missions

## Overview

Story Missions are one-shot battles that open inside a loaded campaign and run on the Long Defeat's
run machine ([long-defeat.md](long-defeat.md)). Slice 1 is built: a dev console command opens a
defence of one of seven fortresses, and the mission writes nothing to the campaign. The mission can
play its own music track, and vanilla music stays quiet for that mission only.

## Why This Exists

- **Vanilla behavior:** a campaign battle needs a map event and changes the campaign (casualties,
  prisoners, renown). Custom Battle runs outside any campaign.
- **TAOM requirement:** set-piece battles from the War of the Ring (Helm's Deep, the White City,
  Osgiliath) that a campaign can offer to the player, with authored waves and their own music.
- **Without this feature:** the only way to play a scripted defence is the Long Defeat from the main
  menu, with no campaign around it.

## Architecture

### Design Challenge

A mission that opens from the campaign map must not look like a campaign battle to other features,
and it must not change the campaign. The engine also needs a mission screen with a working order
bar and a player who can take a soldier after death.

### Solution Approach

- **Entry:** `taom.story_open <mission> [auto]` (`StoryMissionCheats`) opens the menu
  `taom_story_dev` (`StoryDevMenuBehavior`). Its "Open the mission" option is a
  `LeaveType.Mission` option, so the mission opens from a menu click, never from a console call or
  a campaign tick. When the mission ends, the engine returns to this menu.
- **Refusals** (`StoryDevMenuBehavior.Open`): a co-op session, enlistment, an encounter or a map
  event, any other open menu, an empty catalog and an unknown id. Each refusal prints one line in the
  console.
- **Launch** (`StoryMissionLauncher`): `MissionState.OpenNew` with the Long Defeat's siege behavior
  list and no map event, as Host Review opens its mission. Each launch gets its own
  `LongDefeatSession`. It never assigns `Game.PlayerTroop`, which in a campaign is the player's hero.
  The launcher refuses the attacking side, a missing culture and a missing troop, and logs why.
- **Run plan** (`StoryPlanBuilder`): the row's garrison, waves and rules only. There is no shop, gold,
  fortification, affix, boon or class, and the run stops after the authored waves.
- **Verdict** (`StoryVerdict`, `StoryEndWatch`): the run's side-neutral end reason becomes a win or a
  loss for the player's side. A stalemate, a fault or no result decides nothing. About five seconds
  after the result, the mission ends and the player returns to the map.
- **Check lines:** the hosted run writes its Long Defeat check lines through `HostedRunOutcomeLog`,
  which renames `taom.long-defeat.*` to `taom.story-missions.hosted.*`. Thus a story run never lands
  on the rows of `tools/ingame_checks/1079.toml`, whose HUD rows would read as fails.
- **Music** (`StoryMusicCue`, `MissionMusicAdapter`, and in TAOM.Client `StoryMusicMissionView`):
  the track starts when the scene first renders, loops until the mission ends, and is released by both
  teardown hooks. `MusicPauseGateTests` gates the music pause sites.

The feature declares no Harmony patch, no GameModel and no saved data.

### Component Diagram

```
story_missions/story_missions.json
        |
  StoryMissionCatalog (validating loader, Long Defeat readers)
        |
taom.story_open -> StoryDevMenuBehavior (menu, refusals)
        |
  StoryMissionLauncher (MissionState.OpenNew, StoryPlanBuilder)
       / \
      /   \
LongDefeatMissionLogic     StoryMissionLogic (StoryEndWatch, StoryVerdict)
(HostedRunOutcomeLog)      StoryMusicMissionView (TAOM.Client, StoryMusicCue)
```

## Configuration

### Config File: `Main/_Module/ModuleData/story_missions/story_missions.json`

One row per mission under `missions`. `StoryMissionCatalog` reads the file once per process. A row
without a valid id, scene, side, culture, garrison or roster is skipped with one warning. The Long
Defeat readers own the numbers (rules, keep, muster points, waves), so a story row and a fortress row
mean the same thing by the same key.

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | The id that `taom.story_open` takes |
| `name` | string | `{=KEY}English` display name |
| `scene` | string | The scene the mission opens |
| `side` | string | `Defend` (the launcher refuses the attacking side) |
| `defenderCulture`, `attackerCulture` | string | Culture ids of the two sides |
| `music` | string | Optional: an `.ogg` path under the module's `ModuleSounds` folder |
| `rules` | object | Long Defeat rule numbers, for example `preparationSeconds` |
| `garrison` | array | The player's troops: `troop`, `class`, `count` |
| `muster`, `keep` | object | Optional recorded points of the scene |
| `roster.waves` | array | Each wave: `count` and a weighted `mix` of troops |

### Current Values

All seven rows are Defend missions with one wave. Only Helm's Deep names a music track
(`StoryMusic/isengard_vs_rohan.ogg`). Four rows use stand-in scenes after one-wave probes on
2026-11-10.

| Id | Scene | Defender, attacker | Note |
|----|-------|--------------------|------|
| `helms_deep_defend` | `taom_rohan_castle_helms_deep_forceatmo` | `vlandia`, `isengard` | Has muster points |
| `minas_tirith_defend` | `taom_gondor_town_minas_tirith_forceatmo` | `gondor`, `mordor` | |
| `osgiliath_defend` | `taom_gondor_town_osgiliath_e_forceatmo` | `gondor`, `mordor` | |
| `pelargir_defend` | `taom_gondor_town_dolamroth_forceatmo` | `gondor`, `umbar` | Stand-in: the Pelargir scene took over 17 minutes to load |
| `dale_defend` | `sturgia_town_g` | `sturgia`, `khuzait` | Stand-in scene |
| `lorien_defend` | `battania_town_a` | `lothlorien`, `dolguldur` | Stand-in scene |
| `north_defend` | `empire_town_c` | `arthedain`, `gundabad` | Stand-in scene |

`vlandia` is Rohan and `sturgia` is Dale in TAOM ([id cheatsheet](../modding/id-cheatsheet.md)).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/StoryMissions/StoryMissionsModule.cs` | Feature module: the catalog, the launcher and the dev menu behavior |
| `Main/Features/StoryMissions/Config/StoryMissionCatalog.cs` | Validating loader for the JSON |
| `Main/Features/StoryMissions/Domain/` | `StoryMissionDefinition`, `StoryPlanBuilder`, `StoryVerdict` |
| `Main/Features/StoryMissions/Hooks/StoryDevMenuBehavior.cs` | The dev menu and its refusals |
| `Main/Features/StoryMissions/Hooks/StoryMissionLauncher.cs` | Opens the mission |
| `Main/Features/StoryMissions/Hooks/StoryMissionLogic.cs` | The mission logic that feeds `StoryEndWatch` |
| `Main/Features/StoryMissions/StoryEndWatch.cs` | The end cue and the `taom.story-missions.ended` check line |
| `Main/Features/StoryMissions/HostedRunOutcomeLog.cs` | Renames the hosted run's check lines |
| `Main/Features/StoryMissions/StoryMusicCue.cs` | The mission's own track |
| `Main/Adapters/IMissionMusicAdapter.cs`, `MissionMusicAdapter.cs` | Engine music calls |
| `Client/Features/StoryMissions/StoryMissionViews.cs`, `StoryMusicMissionView.cs` | The mission's view list (TAOM.Client, ADR-013) |
| `Main/Features/DevConsole/Cheats/StoryMissionCheats.cs` | `taom.story_open` |
| `Main/_Module/ModuleData/story_missions/story_missions.json` | The missions |
| `tools/ingame_checks/1105.toml` | The in-game check manifest |

## Dependencies

- The Long Defeat (`Main/Features/LongDefeat/`): the run machine, its config readers and
  `LongDefeatMissionLogic`.
- `IGameMenuAdapter`, `IEncounterAdapter` (Adapters): the menu and the encounter state.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): the co-op refusal. The co-op row
  is in [coop-interop.md](coop-interop.md).
- `IEnlistmentStateQuery` (Enlistment): the enlistment refusal.
- `IMissionMusicAdapter` (Adapters): the engine's music calls.

## Tests

`TAOM.Tests/Features/StoryMissions/`:

- `StoryMissionCatalogTests`, `ShippedStoryMissionsTests`: the loader's validation and the shipped rows.
- `StoryPlanBuilderTests`, `StoryVerdictTests`, `StoryEndWatchTests`: the run plan, the verdict and the end check line.
- `StoryMissionLauncherTests`, `StoryMissionsWiringTests`, `StoryMissionWiringPinTests`: the launch refusals and the wiring.
- `HostedRunOutcomeLogTests`: the check line rename.
- `StoryMusicCueTests`, and `TAOM.Tests/Infrastructure/MusicPauseGateTests.cs`: the music cue and the pause sites.

## How to verify in game

**Preconditions:** a loaded TAOM campaign, single player, on the campaign map with no menu open, not
enlisted. No restart and no MCM setting are needed.

1. Run `taom.story_open helms_deep_defend` (add `auto` to let the bot play the lord), then choose
   "Open the mission". The line `[Check] id=taom.story-missions.hosted.boot result=pass` proves the
   hosted run booted.
2. Let the wave end by a win or a loss. The line `[Check] id=taom.story-missions.ended result=pass`
   with `verdict=Won` or `verdict=Lost` proves the end, and the player returns to the map.
3. The `taom.story-missions.music.requested` and `taom.story-missions.music.stopped` lines prove that
   the engine accepted the track and that it looped to the end. Only a person can confirm the sound.

`python tools/ingame_verdict.py` reads these rows from `tools/ingame_checks/1105.toml`. The proof is
owed, and Slice 2 waits on it.

## How to add a story mission

1. Add a row to `story_missions.json` with a Defend side, two culture ids, a garrison and at least
   one wave.
2. Check every troop id and culture id with `python tools/validate_moduledata.py`.
3. Run `taom.story_open <id>` once to time the scene load. A load of many minutes needs a stand-in.
4. No code changes are needed.

## Not built yet

Slices 2 to 5 of #1105 are not built: field SAVE and KILL missions, the Breaking end to end,
Helm's Deep's DEFEND choice, and the ATTACK side. No campaign event offers a mission yet, and there is
no MCM setting.

## Changelog

- 2026-11-12: this doc, from the code on trunk.
- 2026-11-10: Slice 1 (the campaign host and `taom.story_open`), the mission's own music, and seven
  Defend rows; four rows on stand-in scenes after one-wave probes. Reviews:
  [slice 1 RCA](../reviews/rca-story-missions-slice1-2026-11-10.md),
  [music RCA](../reviews/rca-story-mission-music-2026-11-10.md). Codex pre-review waived by Mike on
  2026-11-11.

## GitHub Issue

- **Issue:** #1105
- **Status:** Open (Slice 1 built; in-game proof owed)
