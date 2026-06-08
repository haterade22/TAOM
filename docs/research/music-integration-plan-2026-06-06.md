# TAOM Music Integration Plan (v1.4.5)

Date: 2026-06-06
Status: planned, no runtime code ported yet
Source drop: `_handoff/MusicSystem_SourceDrop`
Target module: `Main/_Module` and `Main/TAOM.csproj`

## Ground Truth

The source drop is a standalone module named `TAOM_AudioPack`. Its `SubModule.xml` declares Bannerlord `v1.4.15`, module id `TAOM_AudioPack`, and a hard dependency on `TAOM` plus `TAOM.Dependencies`. TAOM's current target is Bannerlord `v1.4.5`, and `Main/_Module/SubModule.xml` declares TAOM version `v2.0.5` with Native/SandBoxCore/Sandbox/CustomBattle dependencies.

This integration should not ship as a second standalone module unless that is deliberately chosen later. The safer target is to integrate the music system into `Main/TAOM.dll`, register it through TAOM IoC, and copy the data/assets into `Main/_Module`.

The source drop contains 598 music registrations and 598 OGG files under `ModuleSounds/taom`. Its buckets are:

- `battle_music`: 146 tracks
- `character_creation`: 85 tracks
- `siege_music`: 85 tracks
- `tavern_wander`: 85 tracks
- `town_wander`: 110 tracks
- `worldmap`: 87 tracks

`Main/_Module/ModuleData/module_sounds.xml` currently has no `taom/` music registrations. `Main/_Module/ModuleSounds` already contains other audio assets, but not this music tree.

The source drop also includes 40 tiny 3901-byte OGG placeholder/silence files. Those must be tracked as intentional placeholders or replaced before final content QA.

## Decompiled v1.4.5 API Evidence

These APIs were checked against the installed v1.4.5 DLLs under `A:/SteamLibrary/steamapps/common/Mount & Blade II Bannerlord`.

`TaleWorlds.Engine.Music` exists in v1.4.5 and supports the direct OGG backend:

- `GetFreeMusicChannelIndex()`
- `LoadClip(int index, string pathToClip)`
- `UnloadClip(int index)`
- `IsClipLoaded(int index)`
- `PlayMusic(int index)`
- `PlayDelayed(int index, int deltaMilliseconds)`
- `IsMusicPlaying(int index)`
- `PauseMusic(int index)`
- `StopMusic(int index)`
- `SetVolume(int index, float volume)`

`TaleWorlds.Engine.SoundEvent` exists and supports fallback/event playback:

- `GetEventIdFromString(string name)`
- `PlaySound2D(int soundCodeId)`
- `PlaySound2D(string eventPath)`
- `CreateEventFromString(string eventId, Scene scene)`, scene-null safe
- `CreateEventFromExternalFile(string programmerEventName, string soundFilePath, Scene scene, bool is3d, bool isBlocking)`, scene-null safe
- `CreateEvent(int soundCodeId, Scene scene)`, not scene-null safe because it dereferences `scene.Pointer`
- `Play()`, `Stop()`, `Release()`

There is no public `SoundEvent.SetVolume(float)` in the checked v1.4.5 API. Runtime fades and volume control must use `Music.SetVolume` on the direct music backend. The `SoundEvent` path is fallback-only and must be scene-gated.

`TaleWorlds.MountAndBlade.MissionBehavior` has:

- `Mission` property
- `BehaviorType`
- `OnBehaviorInitialize()`
- `OnMissionTick(float dt)`
- `OnEndMission()`
- `OnRemoveBehavior()`

`Mission.AddMissionBehavior` sets `missionBehavior.Mission = this`, then routes by `MissionBehaviorType`. `MissionBehaviorType.Other` goes to `_otherMissionBehaviors`; `Logic` is for `MissionLogic`. The music mission behavior must return `MissionBehaviorType.Other`.

`CampaignEvents` in v1.4.5 exposes the source-drop events:

- `OnSessionLaunchedEvent`: `CampaignGameStarter`
- `SettlementEntered`: `MobileParty, Settlement, Hero`
- `OnSettlementLeftEvent`: `MobileParty, Settlement`
- `BattleStarted`: `PartyBase, PartyBase, object, bool`
- `OnMissionStartedEvent`: `IMission`
- `OnMissionEndedEvent`: `IMission`
- `TickEvent`: `float`

`Campaign.MapSceneWrapper` returns `TaleWorlds.CampaignSystem.Map.IMapScene`. The public interface does not expose a `Scene` property. Campaign music therefore should use the direct `Music.LoadClip` backend and only use a `Scene` for `SoundEvent` fallback when an adapter can prove one is available.

`SandBox.Objects.Usables.MusicianGroup` exists in v1.4.5. Its vanilla tavern path uses `_playList`, `_trackEvent`, `CheckNewTrackStart()`, `CheckTrackEnd()`, `SetupInstruments()`, and `StartTrack()`. `StartTrack()` resolves the current settlement music path with `SoundEvent.GetEventIdFromString`, creates the event with `Mission.Current.Scene`, then calls `Play()` and starts musician loops. This confirms that tavern suppression can be implemented with explicit Harmony targets instead of the source drop's broad reflection scanner.

## Files To Port

Port these source-drop files by refactoring them into TAOM architecture, not by copying them wholesale:

- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomCampaignMusic.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomMissionMusicRouterBehavior.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSharedPlaybackOwner.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/MusicBucket.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/MusicState.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/MusicRotationPolicy.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/MusicTransitionResolver.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/NoRepeatShufflePicker.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/MusicDiagnostics.cs`, only if diagnostics are retained
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomMcmSettings.cs`, settings only, not the runtime access pattern
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/SubModule.cs`, research reference only for lifecycle and Harmony intent

Port these data/assets:

- `_handoff/MusicSystem_SourceDrop/ModuleSounds/taom/**/*.ogg` -> `Main/_Module/ModuleSounds/taom/**/*.ogg`
- `_handoff/MusicSystem_SourceDrop/ModuleData/module_sounds.xml` taom entries -> imported as `Main/_Module/ModuleData/taom_music_module_sounds.xml` and registered in `project.mbproj`
- `_handoff/MusicSystem_SourceDrop/Tools/config.json` -> adapt as TAOM music generator config
- `_handoff/MusicSystem_SourceDrop/Tools/generate_module_sounds.py` -> adapt so it targets `Main/_Module`, not the standalone source module

Do not port these into runtime:

- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/SubModule.cs` as a whole class
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/Logger.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/LoopMusicController.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomWorldMusicBehavior.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomTownMusicBehavior.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomTavernMusicBehavior.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSiegeMusicBehavior.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomCombatMusicBehavior.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomCharacterCreationMusicBehavior.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomCharacterCreationMusicController.cs`, until the v1.4.5 character-creation VM path is decompiled
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/OptionalAudioCoreBridge.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSharedPlaybackOwner.NativeDirector.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/MusicSuiteIntegration.cs`
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/SoundNameIndex.cs`
- `_handoff/MusicSystem_SourceDrop/bin`, `obj`, `.vs`, and `release`

The skipped behavior files are inert wrappers or placeholders in the source drop. The native/core/suite bridge files are explicitly outside the current handoff contract because `TAOM_AudioCore` is not a dependency and the hidden native/suite flags are forced off.

## TAOM Architecture Target

The port must follow TAOM's boundary rule:

`CampaignBehavior / MissionBehavior / HarmonyPatch -> hook/service interface -> service -> adapter -> TaleWorlds engine`

Services must not accept sealed TaleWorlds types. All `Campaign`, `Mission`, `Scene`, `Settlement`, `PartyBase`, `Hero`, `MobileParty`, `SoundEvent`, and `Music` interaction belongs in adapters or entry points.

### New Feature Files

Create this feature area:

- `Main/Features/Music/MusicIoC.cs`
- `Main/Features/Music/MusicSettings.cs`
- `Main/Features/Music/IMusicSettingsProvider.cs`
- `Main/Features/Music/MusicSettingsProvider.cs`
- `Main/Features/Music/IMusicService.cs`
- `Main/Features/Music/MusicService.cs`
- `Main/Features/Music/MusicBucket.cs`
- `Main/Features/Music/MusicState.cs`
- `Main/Features/Music/MusicContextSnapshot.cs`
- `Main/Features/Music/MusicRotationPolicy.cs`
- `Main/Features/Music/MusicTransitionResolver.cs`
- `Main/Features/Music/NoRepeatShufflePicker.cs`
- `Main/Features/Music/IMusicTrackIndex.cs`
- `Main/Features/Music/MusicTrackIndex.cs`
- `Main/Features/Music/IMusicDiagnosticsRecorder.cs`
- `Main/Features/Music/MusicDiagnosticsRecorder.cs`

Create thin runtime entry points:

- `Main/Features/Music/Hooks/MusicCampaignBehavior.cs`
- `Main/Features/Music/Hooks/MusicMissionBehavior.cs`
- `Main/Features/Music/Hooks/MusicianGroupSetPlayListPatch.cs`
- `Main/Features/Music/Hooks/MusicianGroupCheckNewTrackStartPatch.cs`
- `Main/Features/Music/Hooks/MusicianGroupCheckTrackEndPatch.cs`
- `Main/Features/Music/Hooks/MusicianGroupSetupInstrumentsPatch.cs`

Character creation music must not reuse the source drop's broad reflection scanner. It needs a separate researched hook against the actual v1.4.5 character-creation culture-selection path. Candidate TAOM-owned files to inspect before editing are:

- `Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs`
- `Main/Features/FactionMap/ICultureSettingService.cs`
- `Main/Features/FactionMap/CultureSettingService.cs`

Only add a character-creation music hook after decompiling the exact v1.4.5 VM/state path and proving the selected culture signal is available at the TAOM boundary.

### New Adapter Files

Create these adapters so services remain free of sealed engine types:

- `Main/Adapters/IMusicEngineAdapter.cs`
- `Main/Adapters/MusicEngineAdapter.cs`
- `Main/Adapters/ISoundEventMusicAdapter.cs`
- `Main/Adapters/SoundEventMusicAdapter.cs`
- `Main/Adapters/ICampaignMusicContextAdapter.cs`
- `Main/Adapters/CampaignMusicContextAdapter.cs`
- `Main/Adapters/IMissionMusicContextAdapter.cs`
- `Main/Adapters/MissionMusicContextAdapter.cs`
- `Main/Adapters/IMusicianGroupSuppressionAdapter.cs`
- `Main/Adapters/MusicianGroupSuppressionAdapter.cs`

`MusicEngineAdapter` is the primary playback adapter and wraps only `TaleWorlds.Engine.Music`.

`SoundEventMusicAdapter` is fallback-only. It must reject `CreateEvent(int, Scene)` when `Scene` is null. Prefer `CreateEventFromString` only when a scene is available or the direct `Music` backend failed.

`CampaignMusicContextAdapter` may read `Campaign.Current`, `Settlement.CurrentSettlement`, campaign events, and map/menu state. It must return DTOs to the service.

`MissionMusicContextAdapter` may read `Mission`, `Mission.Scene`, `Mission.Mode`, scene id, and mission culture signals. It must return DTOs to the service.

### Existing Files To Touch

- `Main/IoC.cs`: add `MusicIoC.RegisterMusicFeature(container)` and adapter registrations.
- `Main/SubModule.cs`: add `MusicCampaignBehavior` in `OnGameStart` and `MusicMissionBehavior` in `OnMissionBehaviorInitialize`.
- `Main/_Module/SubModule.xml`: add or verify the explicit `TAOM.Dependencies` load/dependency edge before shipping this feature.
- `Main/_Module/ModuleData/taom_music_module_sounds.xml`: imported `taom/` music registrations.
- `Main/_Module/ModuleData/project.mbproj`: registers both `ModuleData/module_sounds.xml` and `ModuleData/taom_music_module_sounds.xml` as `module_sound`.
- `Main/_Module/ModuleSounds/taom/**`: add the copied OGG tree.
- `tools/music/generate_module_sounds.py`: add the adapted generator.
- `tools/music/config.json`: add the adapted generator config.
- `docs/features/music.md`: add runtime behavior, settings, known placeholders, and content-source notes.
- `CHANGELOG.md`: add the integration entry after implementation and verification.

## Runtime Paths

### 1. Asset And Manifest Path

Source:

- `Main/_Module/ModuleSounds/taom/<bucket>/<culture>/<track>.ogg`
- `Main/_Module/ModuleData/module_sounds.xml`

Runtime:

- `MusicTrackIndex` parses TAOM's merged `module_sounds.xml`.
- It indexes only `Sound` entries whose file path starts with `taom/`.
- It validates bucket, culture, event id, and absolute OGG path.
- It must not scan source-drop paths at runtime.

Tests:

- Assert every `taom/` XML entry has an OGG file.
- Assert every OGG file has a `taom_...` XML entry.
- Assert bucket counts match the source manifest unless intentionally changed.
- Assert placeholder silence files are counted and documented.

### 2. Campaign Music Path

Source-drop reference:

- `TaomCampaignMusic.cs`

TAOM target:

- `MusicCampaignBehavior : CampaignBehaviorBase`
- Register through `CampaignGameStarter.AddBehavior` in `Main/SubModule.cs`.
- Register non-serialized listeners on the v1.4.5 `CampaignEvents` listed above.
- Build DTO snapshots and delegate to `IMusicService`.

Boundary:

- The behavior may receive sealed event arguments.
- It must adapt them immediately through `ICampaignMusicContextAdapter`.
- `MusicService` receives only DTOs, enums, ids, and strings.

API dependencies:

- `CampaignBehaviorBase.RegisterEvents`
- `CampaignEvents.OnSessionLaunchedEvent`
- `CampaignEvents.SettlementEntered`
- `CampaignEvents.OnSettlementLeftEvent`
- `CampaignEvents.BattleStarted`
- `CampaignEvents.OnMissionStartedEvent`
- `CampaignEvents.OnMissionEndedEvent`
- `CampaignEvents.TickEvent`
- `Campaign.MapSceneWrapper`, with no assumption that it exposes `Scene`

### 3. Mission Music Path

Source-drop reference:

- `TaomMissionMusicRouterBehavior.cs`

TAOM target:

- `MusicMissionBehavior : MissionBehavior`
- Register through `Main/SubModule.cs` in `OnMissionBehaviorInitialize`.
- Use `MissionBehaviorType.Other`.
- On each mission tick, adapt the current mission context and delegate to `IMusicService`.
- Clear mission snapshot state on `OnEndMission` and `OnRemoveBehavior`.

Boundary:

- The behavior can touch `Mission` because it is the entry point.
- All deeper logic must use DTOs and adapters.

API dependencies:

- `MBSubModuleBase.OnMissionBehaviorInitialize(Mission mission)`
- `Mission.AddMissionBehavior(MissionBehavior behavior)`
- `MissionBehavior.Mission`
- `MissionBehavior.OnMissionTick(float dt)`
- `MissionBehavior.OnEndMission()`
- `MissionBehavior.OnRemoveBehavior()`
- `MissionBehaviorType.Other`
- `Mission.Scene`
- `Mission.Mode`

### 4. Playback Path

Source-drop reference:

- `TaomSharedPlaybackOwner.cs`

TAOM target:

- `MusicService` owns state decisions, bucket priority, no-repeat policy, fade timing, and diagnostics.
- `MusicEngineAdapter` owns all calls to `TaleWorlds.Engine.Music`.
- `SoundEventMusicAdapter` is fallback-only.

Primary backend:

- Allocate channel with `Music.GetFreeMusicChannelIndex()`.
- Load OGG with `Music.LoadClip(channel, absolutePath)`.
- Check load with `Music.IsClipLoaded(channel)`.
- Play with `Music.PlayMusic(channel)` or `Music.PlayDelayed(channel, milliseconds)`.
- Fade with `Music.SetVolume(channel, value)`.
- Stop/unload with `Music.StopMusic(channel)` and `Music.UnloadClip(channel)`.

Fallback backend:

- Resolve event id with `SoundEvent.GetEventIdFromString(name)`.
- Use `CreateEventFromString` or scene-gated `CreateEvent`.
- Play/stop/release through `SoundEvent`.
- Do not promise fade support on fallback because v1.4.5 does not expose public `SoundEvent.SetVolume`.

### 5. Tavern Suppression Path

Source-drop reference:

- `SubModule.cs` `TryInstallVanillaTavernMusicSuppressorPatch`
- `TaomSharedPlaybackOwner.ExternalMusicSuppressor`

TAOM target:

- Explicit Harmony patches against `SandBox.Objects.Usables.MusicianGroup`.
- No broad reflection scanner.
- Prefixes should be thin and delegate to a suppression service/hook.

API dependencies:

- `MusicianGroup.SetPlayList(List<SettlementMusicData>)`
- `MusicianGroup.CheckNewTrackStart()`
- `MusicianGroup.CheckTrackEnd()`
- `MusicianGroup.SetupInstruments()`
- `SoundEvent.GetEventIdFromString`
- `SoundEvent.CreateEvent(int, Mission.Current.Scene)`
- `SoundEvent.Play()`
- `SoundEvent.Stop()`
- `SoundEvent.Release()`

Patch behavior:

- When TAOM music is disabled, return true and allow vanilla.
- When TAOM music is enabled and current bucket/context owns tavern music, block vanilla track start and release any existing vanilla tavern event.
- Do not suppress unrelated mission sound effects.

### 6. Character Creation Music Path

Source-drop reference:

- `TaomCharacterCreationMusicController.cs`
- `SubModule.cs` character-creation reflection patch installer

TAOM target:

- Not part of the first runtime port until the v1.4.5 character creation VM and TAOM hook path are decompiled.
- Prefer a TAOM-owned selected-culture signal from the existing CharacterCreation/FactionMap feature over a broad reflection scan.

Required research before implementation:

- Decompile the v1.4.5 character creation culture-selection VM/state path.
- Inspect `Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs`.
- Inspect `Main/Features/FactionMap/CultureSettingService.cs`.
- Prove where selected culture changes are observable without duplicating source-drop scanner behavior.

## Version Fixes For v1.4.5

- Replace all standalone module identity with TAOM feature identity. Do not import `TAOM_AudioPack` ids or `v1.4.15` module metadata.
- Keep `net472`.
- Use TAOM's existing Bannerlord reference layout from `Main/TAOM.csproj`.
- Register through TAOM `IoC.cs`; do not use a static source-drop singleton as the service boundary.
- Add or verify `TAOM.Dependencies` dependency/load edge in `Main/_Module/SubModule.xml` before relying on MCM/Harmony dependency behavior.
- Replace source-drop broad reflection patches with explicit v1.4.5 decompiled target methods.
- Use `Music.SetVolume` for fades; do not port SoundEvent fade probing as a feature promise.
- Do not enable `TAOM_AudioCore`, native, suite, or future flags. They are out of contract for this integration.
- Update source-drop docs/release notes from `v1.4.15` to TAOM `v1.4.5` only after build and smoke verification.

## Culture Coverage Rules

The source drop covers these culture folders:

- `gondor`
- `vlandia`
- `sturgia`
- `battania`
- `umbar`
- `empire`
- `khuzait`
- `aserai`
- `mirkwood`
- `rivendell`
- `lothlorien`
- `erebor`
- `mordor`
- `dolguldur`
- `isengard`
- `gundabad`
- `neutral_culture`

TAOM's playable/campaign culture domain must be enumerated from source-of-truth data, not from existing music folders. At minimum, verify coverage against:

- `Main/_Module/ModuleData/taom_spcultures.xml`
- `Main/_Module/ModuleData/spcultures.xslt`
- `Main/_Module/ModuleData/charactercreation/cultures.json`
- any kingdom/culture mapping used by the mission/campaign adapters

Known coverage gaps from the current source drop:

- `shaghana`, `abanissa`, `goblin`, and `mistymountainorcs` exist in character-creation culture data but not in the music source-drop culture folders.
- `dunland_raiders`, `erebor_warriors`, `gondor_soldiers`, `gundabad_raiders`, `harad_raiders`, `mirkwood_stalkers`, `rhun_raiders`, and `umbar_corsairs` exist in `taom_spcultures.xml` but not in the music source-drop culture folders.

The implementation must either add culture pools for these ids or explicitly test and document their fallback to `neutral_culture`.

Tests must enumerate the full culture domain and assert each culture resolves to either a concrete music pool or a documented fallback. Do not define the gate as "has a folder" without a full-domain coverage test.

## Test Plan

Add focused tests before runtime porting:

- `TAOM.Tests/Features/Music/MusicManifestTests.cs`
- `TAOM.Tests/Features/Music/MusicCultureCoverageTests.cs`
- `TAOM.Tests/Features/Music/MusicTrackIndexTests.cs`
- `TAOM.Tests/Features/Music/NoRepeatShufflePickerTests.cs`
- `TAOM.Tests/Features/Music/MusicRotationPolicyTests.cs`
- `TAOM.Tests/Features/Music/MusicTransitionResolverTests.cs`
- `TAOM.Tests/Features/Music/MusicServiceRoutingTests.cs`
- `TAOM.Tests/Features/Music/MusicSettingsProviderTests.cs`
- `TAOM.Tests/Features/Music/MusicianGroupSuppressionBindingTests.cs`

Test requirements:

- Services use NSubstitute mocks for adapters.
- No service test constructs sealed TaleWorlds types.
- Manifest tests compare XML entries to real OGG files.
- Culture tests enumerate the full source-of-truth domain.
- Binding tests prove explicit Harmony target methods exist on v1.4.5 `MusicianGroup`.
- Mission behavior registration tests should assert `MissionBehaviorType.Other`.
- Campaign event tests should assert every registered event delegates to the service through DTOs.

## Implementation Phases

1. Data import and manifest tests.
   - Copy OGG tree.
   - Merge `taom/` XML entries.
   - Add generator config/tool.
   - Add manifest and culture coverage tests.

2. Pure domain port.
   - Port bucket/state/rotation/transition/no-repeat logic.
   - Strip static singleton access.
   - Add service tests.

3. Adapter port.
   - Add `MusicEngineAdapter`.
   - Add fallback `SoundEventMusicAdapter`.
   - Add campaign and mission context adapters.
   - Register through `MusicIoC`.

4. Runtime campaign and mission integration.
   - Add campaign behavior.
   - Add mission behavior.
   - Register both in `Main/SubModule.cs`.
   - Keep character creation disabled until its exact VM path is researched.

5. Tavern suppression.
   - Add explicit `MusicianGroup` Harmony patches.
   - Bind against decompiled v1.4.5 method names.
   - Add binding tests.

6. Character creation, only after additional research.
   - Decompile selected-culture path.
   - Reuse TAOM-owned hook/service if possible.
   - Avoid broad source-drop reflection scanner.

7. Verification.
   - Build TAOM.
   - Run music tests and full test suite.
   - Run in-game smoke with TAOM and TAOM.Dependencies enabled.
   - Verify logs show track index load, direct Music backend playback, mission/campaign bucket transitions, and no vanilla tavern track overlap.

## No-Go Gates

Do not ship if any of these are true:

- `TaomSharedPlaybackOwner` is copied as a static singleton without service/adapters.
- A service accepts `Campaign`, `Mission`, `Scene`, `Settlement`, `Hero`, `PartyBase`, `SoundEvent`, or another sealed TaleWorlds type.
- The port uses source-drop broad reflection scanners where explicit v1.4.5 targets are known.
- `SoundEvent.CreateEvent(int, Scene)` can run with a null scene.
- Culture routing is tested only against existing music folders.
- `TAOM_AudioCore`, native director, or suite bridge code is active.
- `Main/_Module/SubModule.xml` load/dependency ordering is left unverified.
- The 40 placeholder OGG files are silently shipped without documentation or replacement decision.
