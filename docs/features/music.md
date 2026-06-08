# Music Integration

## Overview

Replaces Bannerlord's vanilla music system with a TAOM-owned playback engine that plays culture-specific OGG tracks across six contexts: world map, town wandering, tavern, battle, siege, and character creation. Vanilla musician groups are suppressed in taverns so they don't fight the TAOM tracks; character-creation ambient is suppressed when a culture-specific CC track is playing.

## Why This Exists

- **Vanilla behavior:** All Bannerlord factions share the same small track pool regardless of culture. Musician groups in taverns play a hard-coded `SettlementMusicData` list the game controls.
- **TAOM requirement:** Each of TAOM's 17 Middle-earth cultures needs its own music identity (Gondor orchestral vs. Mordor industrial vs. Dwarf drums, etc.) across all six gameplay contexts.
- **Without this feature:** Every campaign context plays identical vanilla tracks; the 476 culture-specific OGG files in `ModuleSounds/taom/` are inert dead weight.

## Architecture

### Design Challenge

TaleWorlds' music engine exposes only low-level `TaleWorlds.Engine.Music.*` statics (channel allocation, clip load/unload, play/stop). There is no `IMusicService` abstraction, no campaign event for "culture changed", and the musician group behavior is a private `SandBox.Objects.Usables.MusicianGroup` that the engine constructs and drives. The CC screen drives music through `CharacterCreationScreen.OnFrameTick`, which must be patched.

### Solution Approach

TAOM owns the full music routing pipeline:

1. **Context sources** — `TaleWorldsMusicCampaignContextSource` and `TaleWorldsMusicMissionContextSource` poll `Campaign.Current.MainParty.MapEvent`, `Settlement.CurrentSettlement`, and `Mission.Current` on each tick to produce a `MusicRouteSnapshot` (active bucket + culture).
2. **Transition resolver** — `MusicTransitionResolver` merges mission and campaign snapshots into a single `MusicRouteDecision` (mission wins over campaign).
3. **Track index** — `MusicTrackIndex` is loaded once from `taom_music_module_sounds.xml` at startup; tracks are keyed `MusicBucket:cultureId`.
4. **Playback service** — `MusicPlaybackService` manages a single active channel: allocate, load, play, rotate, stop. Includes start-grace window (3 s), lost-channel back-off (1 s for campaign context only), and no-free-channel retry (1 s).
5. **No-repeat shuffle** — `NoRepeatShufflePicker` prevents the same track from playing twice in a row using a per-route history.
6. **Suppressions** — `MusicianGroupSuppressionPatches` (`Patch46_Music`) block `MusicianGroup.SetPlayList/CheckNewTrackStart/CheckTrackEnd/SetupInstruments` when TAOM's tavern context is active; `CharacterCreationAmbientSuppressor` calls `CharacterCreationScreen.StopSound()` once a CC track is confirmed playing.

### Component Diagram

```
taom_music_module_sounds.xml  (476 tracks: bucket × culture × index)
         |
   MusicTrackIndex.LoadFromModuleRoot()
         |
   MusicTransitionResolver
     /            \
CampaignContext  MissionContext
(World/Town/Tavern) (Battle/Siege)
     |                  |
     IMusicCampaignContextAdapter   IMusicMissionContextAdapter
     (wraps Campaign.Current.*)     (wraps Mission.Current.*)
         \            /
        MusicRouteDecision
              |
       MusicPlaybackService
              |
       IMusicEngineAdapter
       (wraps TaleWorlds.Engine.Music.*)
```

### Entry points

| Hook | Event | Role |
|------|-------|------|
| `MusicCampaignBehavior` | `TickEvent` (campaign tick) | Drives campaign music; paused while mission active |
| `MusicMissionBehavior.OnMissionTick` | Per mission frame | Drives mission/battle music |
| `CharacterCreationScreen_OnFrameTick_MusicPatch` | CC screen tick | Drives CC music; suppresses vanilla ambient once |
| `MusicianGroup_SetPlayList_Patch` | Musician group start | Suppresses vanilla tavern; exits tavern context on empty list |
| `CharacterCreationCultureVM_ExecuteSelectCulture_MusicPatch` | Vanilla CC culture select | Signals culture to CC context service |
| `CharacterCreationContent_SetSelectedCulture_Patch` (Patch29) | CC confirm | `ConfirmCulture` signal to CC context service |
| `CustomBattleSideVM_OnCultureSelection_Patch` (Patch19) | Custom battle culture | Signals player culture to custom-battle context |

## Configuration

No config file — all settings are compile-time defaults in `MusicSettingsSnapshot.Default`. MCM integration is not yet implemented; the `MusicSettingsProvider` returns the default snapshot on every call.

| Setting | Default | Notes |
|---------|---------|-------|
| `MusicEnabled` | `true` | Master toggle |
| `CampaignContextEnabled` | `true` | World/Town/Tavern playback |
| `MissionContextEnabled` | `true` | Battle/Siege playback |
| `RouteSettings.{Bucket}Enabled` | all `true` | Per-bucket enable |
| `UseNoRepeatShuffle` | `true` | Prevents back-to-back repeats |
| `NoRepeatHistorySize` | `8` | Tracks remembered per route |
| `MasterVolume` | `1.0` | Applied to every GetBucketVolume call |
| Per-bucket volumes | `1.0` | WorldVolume, TownVolume, TavernVolume, BattleVolume, SiegeVolume |
| Rotation intervals | `180 s` | World/Town/Battle/CC before shuffling to next track |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Music/MusicPlaybackService.cs` | Core channel-management and routing logic |
| `Main/Features/Music/MusicTrackIndex.cs` | Track registry, loaded from `project.mbproj` |
| `Main/Features/Music/MusicTransitionResolver.cs` | Merges mission + campaign snapshots → route decision |
| `Main/Features/Music/NoRepeatShufflePicker.cs` | Per-route no-repeat shuffle/round-robin |
| `Main/Features/Music/MusicRotationPolicy.cs` | Timer-based track rotation |
| `Main/Features/Music/MusicSettingsSnapshot.cs` | Immutable settings + per-bucket volume |
| `Main/Features/Music/MusicIoC.cs` | DryIoc registrations for all 15 services + adapters |
| `Main/Features/Music/Hooks/MusicCampaignBehavior.cs` | Campaign tick → playback |
| `Main/Features/Music/Hooks/MusicMissionBehavior.cs` | Mission tick + end → playback |
| `Main/Features/Music/Hooks/MusicianGroupSuppressionPatches.cs` | Tavern suppression patches |
| `Main/Features/Music/Hooks/CharacterCreationMusicScreenPatches.cs` | CC screen tick + finalize |
| `Main/Features/Music/Hooks/CharacterCreationMusicScreenPatchHelper.cs` | CC tick logic (ambient suppression, enter/exit) |
| `Main/Adapters/MusicEngineAdapter.cs` | Wraps `TaleWorlds.Engine.Music.*` statics |
| `Main/Adapters/MusicCampaignContextAdapter.cs` | Wraps `Campaign.Current.*` |
| `Main/Adapters/MusicMissionContextAdapter.cs` | Wraps `Mission.Current.*` |
| `Main/Adapters/MusicianGroupSuppressionAdapter.cs` | Reflects `MusicianGroup._trackEvent` → `SoundEvent.Stop/Release` |
| `Main/_Module/ModuleData/taom_music_module_sounds.xml` | 476 sound definitions, auto-generated |
| `Main/_Module/ModuleSounds/taom/` | 476 OGG audio files by context/culture |

## Dependencies

- `IMusicEngineAdapter` — wraps `TaleWorlds.Engine.Music` low-level statics
- `IMusicCampaignContextAdapter` — wraps `Campaign.Current.MainParty.*`, `Settlement.CurrentSettlement`
- `IMusicMissionContextAdapter` — wraps `Mission.Current.*`
- `IMusicianGroupSuppressionAdapter` — reflects private `MusicianGroup._trackEvent` field
- `IMusicTavernContextSource` — owned by TAOM; set by suppression patch, cleared on mission end or empty SetPlayList
- `ICharacterCreationMusicContextService` — receives culture signals from CC screen and FactionMap
- `ICustomBattleMusicContextService` — receives player culture from Custom Battle VM

## Tests

| File | Coverage |
|------|---------|
| `TAOM.Tests/Features/Music/MusicPlaybackServiceTests.cs` | 15 tests — channel lifecycle, rotation, back-off, track failure recovery |
| `TAOM.Tests/Features/Music/MusicTransitionResolverTests.cs` | 8 tests — mission vs campaign priority, empty snapshots |
| `TAOM.Tests/Features/Music/MusicTrackIndexTests.cs` | 5 tests — load from XML, bucket/culture lookup |
| `TAOM.Tests/Features/Music/NoRepeatShufflePickerTests.cs` | 6 tests — history, round-robin, signature invalidation |
| `TAOM.Tests/Features/Music/MusicRotationPolicyTests.cs` | 5 tests — timer scheduling, per-bucket intervals |
| `TAOM.Tests/Features/Music/CharacterCreationMusicContextServiceTests.cs` | 9 tests — enter/exit, SelectCulture, ConfirmCulture |
| `TAOM.Tests/Features/Music/MusicianGroupSuppressionServiceTests.cs` | 4 tests — suppress gate, settings checks |
| `TAOM.Tests/Features/Music/MusicSettingsProviderTests.cs` | 2 tests — defaults, numeric clamping |
| `TAOM.Tests/Features/Music/MusicRuntimeWiringTests.cs` | Wiring / IoC registration verification |
| `TAOM.Tests/Features/Music/MusicManifestTests.cs` | Verifies all cultures in XML exist as audio folders |

## How to Add a New Culture's Music Tracks

1. Create audio files under `Main/_Module/ModuleSounds/taom/<context>/<culture_id>/` for each context the culture supports.
2. Re-run `tools/replace_taom_music_assets.ps1 -Apply` — it regenerates `taom_music_module_sounds.xml` from the folder contents.
3. No C# changes needed — `MusicTrackIndex.TryParseTrack` derives the culture key from the folder name, which must match the culture's `StringId` in `taom_spcultures.xml`.

## How to Add a New Music Context (Bucket)

1. Add a value to `MusicBucket` enum.
2. Add folder handling to `MusicTrackIndex.TryParseBucket`.
3. Add a `GetBucketVolume` case in `MusicSettingsSnapshot`.
4. Add a `IsBucketEnabled` case in `MusicRouteSettings`.
5. Add a `ShouldRotate` case in `MusicRotationPolicy`.
6. Wire a snapshot source that yields `IsActive=true` for the new context in `MusicTransitionResolver.BuildCampaignOrder` or `BuildMissionOrder`.

## Known Limitations

- **MCM not wired** — `MusicSettingsProvider` always returns compile-time defaults. No in-game settings UI yet.
- **No audio fades** — tracks cut instantly on context change. Fade-in/out logic is not implemented.
- **CharacterCreation bucket uses TownEnabled/TownVolume** — disabling Town music also disables CC music. No dedicated CC enable toggle.
- **Vanilla psai suppressed globally** — A `Patch46_Music` Prefix on `MBMusicManager.StartTheme` prevents psai (the vanilla adaptive music system) from acquiring `TaleWorlds.Engine.Music` channels while TAOM music is enabled. This also suppresses vanilla battle music handlers; for the rare case where TAOM has no track for a specific culture during a mission, the result is silence rather than vanilla fallback. 17 cultures have battle tracks, so this is uncommon in practice.
- **No runtime enable/disable toggle** — `MusicSettingsProvider` returns compile-time defaults (`MusicEnabled = true` always). The MCM settings UI is not wired yet — disabling TAOM music requires removing the mod or editing settings before game launch. Once the MCM toggle is added, psai will automatically resume when `IsTaomMusicActive()` returns false.

## GitHub Issue

- **Issue:** [#275 — feat(music): culture-specific music integration — 17 cultures, 476 tracks, 6 contexts](https://github.com/haterade22/TAOM/issues/275)
- **Status:** Open
