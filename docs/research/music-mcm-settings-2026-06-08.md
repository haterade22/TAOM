# Music MCM Settings Research - 2026-06-08

Scope: safe MCM settings for the already-researched TAOM music feature. This is a research and implementation path only. It does not add broad native music suppression, external channel takeover scans, SoundEvent fallback, new campaign lifecycle hooks, or new bucket detection.

## Sources Checked

- `docs/research/music-integration-plan-2026-06-06.md:94` says the source-drop `TaomMcmSettings.cs` may be used for settings only, not for its runtime access pattern.
- `docs/research/music-integration-plan-2026-06-06.md:137-139` names the TAOM architecture target as `MusicSettings`, `IMusicSettingsProvider`, and `MusicSettingsProvider`.
- `docs/research/music-integration-plan-2026-06-06.md:31-42` confirms the installed v1.4.5 direct backend is `TaleWorlds.Engine.Music`, including `SetVolume(int index, float volume)`.
- `docs/research/music-integration-plan-2026-06-06.md:54` says public `SoundEvent.SetVolume(float)` is absent in v1.4.5, so runtime fades and volume controls must use direct `Music.SetVolume`.
- `docs/research/music-runtime-hooks-2026-06-06.md:23-24` pins campaign music to `CampaignEvents.TickEvent` and mission-active suppression so campaign and mission feeders do not race `IMusicPlaybackService`.
- `docs/research/music-runtime-hooks-2026-06-06.md:78-80` says `MusicNativeSuppressor` and `MusicExternalChannelSuppressor` are harmful in the installed runtime and must not be reintroduced.
- `docs/research/music-tavern-suppression-2026-06-06.md:33-37` allows tavern suppression only when TAOM music is enabled, the tavern bucket is enabled, TAOM owns active Tavern playback, and failures fail open to vanilla.
- `docs/research/music-character-creation-path-2026-06-06.md:105-107` says character creation vanilla ambient suppression is allowed only after TAOM character-creation playback has started or continued.
- `Main/Features/TaomSettings.cs:10-15` is the existing main TAOM MCM settings class, using `AttributeGlobalSettings<TaomSettings>` with id `TAOM`.
- `Main/Features/TaomSettings.cs:506-519` is the last current normal gameplay settings group before the map-tool button group at `Main/Features/TaomSettings.cs:531-536`; a music group can safely sit between those, for example `Audio/Music` at `GroupOrder = 45`.
- `Main/Features/Music/MusicIoC.cs:18` already registers `IMusicSettingsProvider` as a singleton.
- `Main/Features/Music/MusicSettingsProvider.cs:3-8` currently returns `MusicSettingsSnapshot.Default` only.
- `Main/Features/Music/MusicPlaybackService.cs:48-57` reads a settings snapshot on every update, gates `MusicEnabled`, then passes route settings into `MusicTransitionResolver`.
- `Main/Features/Music/MusicPlaybackService.cs:127-136` uses snapshot volume/no-repeat settings while starting tracks.
- `Main/Features/Music/MusicPlaybackService.cs:208` applies volume through the direct engine adapter.
- `Main/Features/Music/MusicRouteSettings.cs:36-52` already supports per-bucket routing for World, Town, Tavern, Battle, and Siege; CharacterCreation currently follows Town.
- `Main/Features/Music/MusicSettingsSnapshot.cs:55-87` defines safe defaults: all routes on, no-repeat on, history size 8, volumes 1.0, rotation intervals 180 seconds, logging off.
- `Main/Features/Music/MusicSettingsSnapshot.cs:135-153` maps bucket volume: World, Town, Tavern, Battle, Siege, with CharacterCreation using Town volume.
- `Main/Features/Music/MusicSettingsSnapshot.cs:156-175` clamps numeric snapshot values, including NaN/Infinity for volume/fade fields.
- `Main/Features/Music/MusicRotationPolicy.cs:21-24` only clamps rotation intervals with `ClampNonNegative`; `Main/Features/Music/MusicRotationPolicy.cs:118-121` does not reject NaN/Infinity yet.
- Decompiled `Dependencies/bin/Debug/net472/MCMv5.dll`, `MCM.Abstractions.Base.Global.AttributeGlobalSettings<T>`: it only sets `DiscoveryType` to `attributes`.
- Decompiled `Dependencies/bin/Debug/net472/MCMv5.dll`, `MCM.Abstractions.Base.Global.GlobalSettings<T>.Instance`: the getter caches the settings id by type, then calls `BaseSettingsProvider.Instance?.GetSettings(id) as T`.
- Decompiled `Dependencies/bin/Debug/net472/MCMv5.dll`, `MCM.Abstractions.BaseSettingsProvider`: `Instance` can be null and `GetSettings(string id)` returns nullable `BaseSettings`.
- `Main/Features/SettlementNameplateFade/NameplateFadeSettingsProvider.cs:10-14` documents the TAOM pattern for avoiding repeated `TaomSettings.Instance` lookups while still reading live edited property values through the cached settings reference.
- `Main/Features/CastleRecruitment/CastleRecruitmentSettingsProvider.cs:5-9` documents the safe fallback pattern when `TaomSettings.Instance` is null early or MCM fails to load.
- `Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs:22-36` documents the NaN/Infinity clamp rule for corrupted MCM/config numeric values.

## Safe MCM Surface

Add the first music settings to `Main/Features/TaomSettings.cs` under `Audio/Music`, not as a new runtime hook:

- `EnableTaomMusic`, default `true`.
- `EnableWorldMusic`, `EnableTownMusic`, `EnableTavernMusic`, `EnableBattleMusic`, `EnableSiegeMusic`, all default `true`.
- `MusicMasterVolume`, `WorldMusicVolume`, `TownMusicVolume`, `TavernMusicVolume`, `BattleMusicVolume`, `SiegeMusicVolume`, all `0.0f..1.0f`, default `1.0f`.
- `UseNoRepeatMusicShuffle`, default `true`.
- `MusicNoRepeatHistorySize`, `0..64`, default `8`.
- Rotation controls only after the first red test fixes non-finite interval handling: `EnableWorldMusicRotation`, `EnableTownMusicRotation`, `EnableBattleMusicRotation`, plus interval sliders defaulting to `180`.

Do not expose these in the first pass:

- Broad "suppress all vanilla music" toggle. Research says the native suppressor and external channel scan paths are harmful to direct TAOM playback.
- SoundEvent fallback toggle. v1.4.5 lacks public `SoundEvent.SetVolume`; fallback must stay separately researched and scene-gated.
- Immediate campaign reevaluation toggle. Runtime hook research pins campaign playback to campaign tick and mission-active suppression.
- Separate character-creation enable/volume toggle. Current resolver intentionally maps CharacterCreation to Town routing and volume; a separate CC control needs red resolver/settings tests first.

## Safe Provider Design

Implement `MusicSettingsProvider` as a pure bridge from MCM to `MusicSettingsSnapshot`:

- Keep `IMusicSettingsProvider.GetSnapshot()` as the only surface consumed by music services.
- Use a lazy cached `TaomSettings` reference after the first non-null lookup, because MCM `Instance` is a provider lookup and can be null early.
- Build a new immutable `MusicSettingsSnapshot` per call so live MCM edits apply on the next music update without mutable settings leaking into services.
- Keep defaults identical to `MusicSettingsSnapshot.Default` when MCM is absent.
- Keep all numeric validation in `MusicSettingsSnapshot` and `MusicRotationPolicy.RotationSnapshot`, then unit test it.
- Do not read `TaomSettings.Instance` in Harmony patches, campaign behaviors, mission behaviors, adapters, or playback hooks.

## Test Plan Before Runtime Publish

Red tests first:

1. `MusicRotationPolicyTests.RotationSnapshot_RejectsNonFiniteIntervals` - NaN/Infinity intervals become disabled or defaulted, never NaN.
2. `MusicSettingsProviderTests.GetSnapshot_NoMcmInstance_ReturnsDefaultSnapshot` - absent MCM keeps all current behavior.
3. `MusicSettingsProviderTests.GetSnapshot_MapsMcmMasterAndBucketToggles` - master and five bucket toggles map to `MusicRouteSettings`.
4. `MusicSettingsProviderTests.GetSnapshot_MapsMcmVolumesAndNoRepeat` - volumes and no-repeat values map to `MusicSettingsSnapshot`.
5. `MusicPlaybackServiceTests.Update_DisablingMusicStopsOwnedTrack` - master toggle still routes through the existing `Stop("music_disabled")` path.
6. `MusicianGroupSuppressionServiceTests.ShouldSuppressVanillaTavernMusic_TavernBucketDisabled_ReturnsFalse` - tavern suppression keeps the documented fail-open behavior when bucket disabled.

Then implement:

1. Add `Audio/Music` MCM properties to `TaomSettings`.
2. Add an optional settings accessor to `MusicSettingsProvider` for tests, defaulting to `() => TaomSettings.Instance`.
3. Map MCM values into `MusicSettingsSnapshot`.
4. Fix `MusicRotationPolicy.ClampNonNegative` to reject NaN/Infinity before exposing interval sliders.
5. Run focused music tests, full tests, build, publish, then retest in game.

## Next Researched Step

Add the red tests for finite-safe rotation intervals and MCM-to-`MusicSettingsSnapshot` mapping, then wire `MusicSettingsProvider` to `TaomSettings`. This keeps the change inside the already-proven settings seam and avoids touching campaign/mission/character-creation runtime hooks.
