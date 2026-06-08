# Music Runtime Hook Research - 2026-06-06

Scope: first thin runtime hook pass for the TAOM music system. This step wires campaign and mission runtime feeders only. It does not add Harmony patches, tavern musician suppression, SoundEvent fallback, character creation music, or custom battle profile hooks.

## Sources Checked

- `docs/research/music-integration-plan-2026-06-06.md:128-130` defines the TAOM boundary as `CampaignBehavior / MissionBehavior / HarmonyPatch -> hook/service interface -> service -> adapter -> TaleWorlds engine`, and keeps sealed TaleWorlds types in adapters or entry points.
- `docs/research/music-integration-plan-2026-06-06.md:237-246` specifies `MusicCampaignBehavior : CampaignBehaviorBase`, registration through `CampaignGameStarter.AddBehavior`, non-serialized campaign event listeners, DTO snapshot construction, and delegation to the music service.
- `docs/research/music-integration-plan-2026-06-06.md:268-285` specifies `MusicMissionBehavior : MissionBehavior`, registration through `OnMissionBehaviorInitialize`, `MissionBehaviorType.Other`, `OnMissionTick(float dt)`, `OnEndMission()`, and `OnRemoveBehavior()`.
- Decompiled `TaleWorlds.CampaignSystem.CampaignBehaviorBase` from installed v1.4.5 proves campaign behaviors must implement `RegisterEvents()` and `SyncData(IDataStore)`.
- Decompiled `TaleWorlds.CampaignSystem.CampaignEvents` from installed v1.4.5 proves:
  - `TickEvent` is `IMbEvent<float>`.
  - `OnSessionLaunchedEvent` is `IMbEvent<CampaignGameStarter>`.
  - `OnMissionStartedEvent` and `OnMissionEndedEvent` are `IMbEvent<IMission>`.
- Decompiled `TaleWorlds.MountAndBlade.MBSubModuleBase` from installed v1.4.5 proves `OnMissionBehaviorInitialize(Mission mission)` is the module-level mission behavior hook.
- Decompiled `TaleWorlds.MountAndBlade.MissionBehavior` from installed v1.4.5 proves `OnMissionTick(float dt)` is virtual, `OnEndMission()` is protected virtual and reached through `OnEndMissionInternal()`, and `OnRemoveBehavior()` is virtual.
- Decompiled `TaleWorlds.MountAndBlade.Mission.AddMissionBehavior(MissionBehavior)` from installed v1.4.5 sets `missionBehavior.Mission = this` and routes `MissionBehaviorType.Logic` into `MissionLogics` by `missionBehavior as MissionLogic`, while `MissionBehaviorType.Other` goes into `_otherMissionBehaviors`.
- Decompiled `TaleWorlds.MountAndBlade.MissionBehaviorType` from installed v1.4.5 contains only `Logic` and `Other`.

## Decisions

- `MusicCampaignBehavior` is a `CampaignBehaviorBase` registered through `CampaignGameStarter.AddBehavior(IoC.Resolve<MusicCampaignBehavior>())`.
- Campaign playback uses `CampaignEvents.TickEvent` and passes `MusicRouteSnapshot.Empty` as the mission snapshot.
- Campaign playback stops and suppresses campaign ticks while `OnMissionStartedEvent` has marked a mission active; `OnMissionEndedEvent` reopens campaign ticks. This prevents the campaign feeder and mission feeder from racing the same `IMusicPlaybackService`.
- `MusicMissionBehavior` is a `MissionBehavior` registered through `mission.AddMissionBehavior(new MusicMissionBehavior())`.
- `MusicMissionBehavior.BehaviorType` is `MissionBehaviorType.Other`, not `Logic`, because it is not a `MissionLogic`.
- Mission playback captures the campaign snapshot first and passes its culture id as the mission fallback culture, matching the existing `IMusicMissionContextAdapter.CaptureSnapshot(string fallbackCultureId)` seam.
- Runtime hooks pass only `MusicRouteSnapshot` DTOs and primitive timer values to `IMusicPlaybackService`; no service receives a TaleWorlds object.

## 2026-06-07 World Double-Start Diagnostic

Live smoke logs prove World playback was reached, but the same bucket restarted immediately before the engine reported the channel as playing:

- `A:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs\taom_debug_2026-06-07_21-36-58.log:408-411` started `bucket=World culture=aserai` twice at `21:39:02`, then continued the second track at `21:39:04`.
- `A:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs\taom_debug_2026-06-07_21-40-44.log:410-413` started `bucket=World culture=gondor` twice at `21:42:22`, then continued the second track at `21:42:24`.

Fix path: keep this in `MusicPlaybackService`, because the researched runtime boundary says campaign hooks only feed DTO snapshots and primitive timer values into the service. Regression test: `MusicPlaybackServiceTests.Update_DoesNotRestartOwnedChannelDuringStartGraceWhenEngineReportsNotPlayingYet`.

## 2026-06-07 Native Music Suppression Diagnostic

Live smoke log `A:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs\taom_debug_2026-06-07_22-00-37.log` proves TAOM playback is firing, but native/external music is still present:

- `:416`, `:661`, and `:1168` start `bucket=World` from the campaign feeder.
- `:651` starts `bucket=Tavern`; `:1158` starts `bucket=Town`; `:1321` starts `bucket=Battle`.
- `:419` and `:1320` log `[TAOM][OWNER][TAKEOVER] stopped_external_music_channels=1`, proving non-TAOM music can still appear after TAOM starts.

Installed v1.4.5 decompile identifies the native owner path:

- `SandBox.View.CampaignMusicHandler.IMusicHandler.OnUpdated(float dt)` calls `CheckMusicMode()` and `TickCampaignMusic(dt)`, then `TickCampaignMusic` calls `MBMusicManager.Current.StartThemeWithConstantIntensity(...)`.
- `TaleWorlds.MountAndBlade.View.MissionViews.Sound.MusicBattleMissionView.OnBehaviorInitialize()` calls `MBMusicManager.Current.DeactivateCurrentMode()`, `ActivateBattleMode()`, and `OnBattleMusicHandlerInit(...)`; `CheckForStarting()` later calls `MBMusicManager.Current.StartTheme(...)`.
- `MusicStealthMissionView.AfterStart()` calls `MBMusicManager.Current.StartTheme(...)`; `MusicSilencedMissionView.OnBehaviorInitialize()` calls `DeactivateCurrentMode()` and registers the silenced handler.
- `TaleWorlds.MountAndBlade.MBMusicManager.Update(float dt)` only calls `_activeMusicHandler.OnUpdated(dt)` when `_systemPaused` is false. `PauseMusicManagerSystem()` sets `_systemPaused = true`; `DeactivateCurrentMode()` routes campaign/battle modes through psai stop; `ForceStopThemeWithFadeOut()` calls psai stop directly.

Superseded fix path: an adapter-bound native suppressor over `MBMusicManager.Current.DeactivateCurrentMode()`, `ForceStopThemeWithFadeOut()`, and `PauseMusicManagerSystem()` was tested, then removed after live runtime logs showed TAOM playback starting and immediately failing to remain active.

## 2026-06-07 Native Suppression Continuation Regression

Live smoke log `A:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs\taom_debug_2026-06-07_23-23-55.log` shows the first native suppression pass was too broad:

- `:181` starts `bucket=Battle` on `channel=0`.
- `:202` reports `outcome=Continued` for the same track/channel.
- `:204` starts a different `bucket=Battle` track only 34 seconds later, even though battle rotation is configured at 180 seconds.

Root-cause hypothesis confirmed by red tests: calling `MusicNativeSuppressor.SuppressNativeMusic()` while an owned TAOM channel is continuing can stop the just-started direct music channel through the same underlying music system. Native suppression must run before TAOM starts a new track, not during owned-track continuation or start-grace continuation.

Superseded fix path: keeping the pre-start native suppressor before external-channel scan and free-channel allocation still left TAOM tracks failing to remain active in live testing.

## 2026-06-07 Suppression Owner Removal

Live smoke log `A:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs\taom_debug_2026-06-07_23-38-12.log` shows the remaining suppression owner path is unsafe in the installed runtime:

- `:157` through `:179` log `[TAOM][OWNER][TAKEOVER] external_music_channel_probe_failed` with `AccessViolationException` for channels 9-31.
- `:180` logs `[TAOM][OWNER][TAKEOVER] stopped_external_music_channels=1 owned_channel=-1`.
- `:181` starts `bucket=Battle` on `channel=0`.
- `:202` reports `outcome=Continued` for that same track/channel.
- `:204` starts a different `bucket=Battle` track only four seconds after the first start, while battle rotation remains configured at 180 seconds.

Root-cause hypothesis confirmed by red tests: both suppression-owner seams are harmful to the direct TAOM playback path. `MusicNativeSuppressor` can pause/deactivate the same underlying music system used by TAOM direct music playback, and `MusicExternalChannelSuppressor` probes invalid music channel indices in the installed runtime. Playback must start and continue through `IMusicEngineAdapter` without native manager suppression or 0..31 channel takeover scans.

Fix path: remove `IMusicNativeSuppressor`, `MusicNativeSuppressor`, `IMusicNativeManagerAdapter`, `MusicNativeManagerAdapter`, `IMusicExternalChannelSuppressor`, `MusicExternalChannelSuppressor`, and `MusicExternalChannelSuppressionResult`; remove their IoC registrations; keep `MusicPlaybackService` limited to owned-channel stop/unload and direct `LoadClip`/`PlayMusic`. Regression tests: `MusicPlaybackServiceTests.Update_StartsResolvedTrackWithoutSuppressionOwnerSideEffects`, `MusicPlaybackServiceTests.Update_AllocatesFreeChannelDirectly`, and `MusicPlaybackServiceTests.Update_ContinuesActiveOwnedTrackWithoutSuppressionOwnerSideEffects`.

## 2026-06-08 Custom Battle No-Free-Channel Backoff

Live smoke log `A:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs\taom_debug_2026-06-07_23-50-41.log` shows custom battle startup can briefly have no available direct music channel:

- `:161` logs the first `outcome=Failed bucket=Battle ... reason=no_free_music_channel`.
- `:182-305` repeats `reason=no_free_music_channel` before the engine frees a channel.
- `:306-307` starts and continues `bucket=Battle` on `channel=0`.

Fix path: keep this in `MusicPlaybackService`, because the suppression-owner removal decision above keeps TAOM playback inside the direct `IMusicEngineAdapter` path. On `no_free_music_channel`, remember the route key and skip same-route retries until the one-second retry window expires, returning `waiting_for_free_music_channel` without advancing the picker or calling `GetFreeMusicChannelIndex()` every tick. `MusicRuntimeSmokeTrace` deduplicates identical messages, so this produces one waiting marker instead of per-tick failed-start spam. Regression test: `MusicPlaybackServiceTests.Update_BacksOffNoFreeChannelRetriesForSameRouteUntilRetryWindow`.

## 2026-06-08 Custom Battle Selected-Culture Feed

Live smoke log `A:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs\taom_debug_2026-06-08_00-26-06.log` proves custom siege routing reaches TAOM but had no selected-culture feed:

- `:90` opens `mission='CustomSiegeBattle'`.
- `:172` shows `TAOM.Features.Music.Hooks.MusicMissionBehavior` attached as `BehaviorType=Other`.
- `:182` starts `bucket=Siege culture=neutral_culture`, and `:189` continues the same track.
- `:303/:311` and `:425/:431` repeat successful custom siege starts/continues with `culture=neutral_culture`.

Installed v1.4.5 decompile of `TaleWorlds.MountAndBlade.CustomBattle.dll` identifies the safe boundary:

- `CustomBattleSideVM(TextObject sideName, bool isPlayerSide, TroopTypeSelectionPopUpVM troopTypeSelectionPopUp, Action onCharacterSelected)` stores `isPlayerSide` in private `_isPlayerSide`.
- The constructor creates `FactionSelectionGroup = new CustomBattleFactionSelectionVM(OnCultureSelection)`.
- `CustomBattleFactionSelectionVM.OnFactionSelected(FactionItemVM faction)` assigns `SelectedItem`, calls `_onSelectionChanged(faction.Faction)`, then updates `SelectedFactionName`.
- `FactionItemVM.Faction` is the selected `BasicCultureObject`; its `StringId` comes from `MBObjectBase.StringId`.

Fix path: keep `BasicCultureObject` at the `CustomBattleSideVM_OnCultureSelection_Patch` boundary, register player-side VMs from the constructor's `isPlayerSide` argument, and delegate only `selectedCulture.StringId` into `ICustomBattleMusicContextService`. `MusicMissionContextAdapter` uses that selected culture only when the mission has no explicit culture and the campaign fallback is missing or `neutral_culture`; real campaign fallback culture still wins over stale custom-battle state. `MusicMissionBehavior` clears the custom-battle selected culture on mission end/remove. Regression tests: `CustomBattleMusicCultureSignalTests`, `CustomBattleMusicContextServiceTests`, `MusicMissionContextAdapterTests.CaptureSnapshot_UsesCustomBattleCultureBeforeNeutralFallbackWhenMissionHasNoCulture`, `MusicMissionContextAdapterTests.CaptureSnapshot_RealCampaignFallbackWinsOverStaleCustomBattleCulture`, and `MusicRuntimeHookTests.MissionEnd_StopsPlayback`.

## Tests Added

- `TAOM.Tests/Features/Music/MusicRuntimeHookTests.cs`
- `TAOM.Tests/Features/Music/MusicRuntimeWiringTests.cs`

These tests pin behavior type, snapshot delegation, campaign-vs-mission ownership, stop calls on mission lifecycle transitions, and SubModule/IoC wiring.
