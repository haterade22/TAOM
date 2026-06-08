# Music Tavern Suppression Research - 2026-06-06

Scope: tavern suppression binding tests plus explicit Harmony patches for vanilla `SandBox.Objects.Usables.MusicianGroup`. This step does not add character-creation music, custom-battle profile hooks, or broad source-drop reflection scanning.

## Sources Checked

- `docs/research/music-integration-plan-2026-06-06.md:319-346` defines the tavern suppression path: explicit `MusicianGroup` Harmony patches, no broad scanner, thin prefixes delegating to suppression service/hook, and vanilla allow-through when TAOM music is disabled.
- `docs/research/music-integration-plan-2026-06-06.md:334-341` lists the required v1.4.5 APIs: `MusicianGroup.SetPlayList(List<SettlementMusicData>)`, `CheckNewTrackStart()`, `CheckTrackEnd()`, `SetupInstruments()`, and `SoundEvent` create/play/stop/release calls.
- Source-drop `TAOM_AudioPack/SubModule.cs:1273-1464` implements a runtime scanner that identifies a tavern controller by method names and patches many possible void methods. TAOM intentionally does not port this scanner because the integration plan requires explicit patches.
- Source-drop `TAOM_AudioPack/TaomSharedPlaybackOwner.cs:2944-3003` shows the old owner-side suppression concept: external/tavern musician suppression is an owner concern that should not suppress unrelated mission sounds.
- Decompiled installed v1.4.5 `SandBox.dll` type `SandBox.Objects.Usables.MusicianGroup` with `ilspycmd`.
- Decompiled installed v1.4.5 `TaleWorlds.Engine.dll` type `TaleWorlds.Engine.SoundEvent` with `ilspycmd`.

## Decompiled Facts

- `MusicianGroup.SetPlayList(List<SandBox.Objects.SettlementMusicData> playList)` copies the list into private `_playList`.
- `MusicianGroup.OnTick(float dt)` calls private `CheckNewTrackStart()` and private `CheckTrackEnd()`.
- `CheckNewTrackStart()` starts vanilla tavern music only when `_playList.Count > 0`, `_trackEvent == null`, the 8-second gap has elapsed, and at least one `PlayMusicPoint` has a user. It then calls `SetupInstruments()`, `StartTrack()`, and clears `_gapTimer`.
- `CheckTrackEnd()` owns the vanilla cleanup path for private `_trackEvent`: it stops the event when no musician point has a user, releases it when no longer playing, nulls `_trackEvent`, stops musicians, and starts the gap timer.
- `SetupInstruments()` indexes `_playList[_currentTrackIndex]` and mutates the `PlayMusicPoint` instrument loops.
- `StartTrack()` calls `SoundEvent.GetEventIdFromString(_playList[_currentTrackIndex].MusicPath)`, creates a `SoundEvent` with `SoundEvent.CreateEvent(eventId, Mission.Current.Scene)`, assigns it to `_trackEvent`, sets position, calls `Play()`, and passes `_trackEvent` to each musician loop.
- `SoundEvent` in v1.4.5 exposes `GetEventIdFromString(string)`, `CreateEvent(int, Scene)`, `Play()`, `Stop()`, `Release()`, and `IsPlaying()`.
- `PlayMusicPoint.StartLoop(SoundEvent)` stores the passed track event for animation-loop ticking; `PlayMusicPoint.EndLoop()` clears its own event reference and instrument. This first suppression pass does not add a new `StopMusicians()` reflection dependency because the plan's contract is sound-event suppression, not animation-state suppression.

## Decisions

- Add explicit `Patch46_Music` prefixes for only:
  - `MusicianGroup.SetPlayList(List<SettlementMusicData>)`
  - `MusicianGroup.CheckNewTrackStart()`
  - `MusicianGroup.CheckTrackEnd()`
  - `MusicianGroup.SetupInstruments()`
- Do not patch `StartTrack()` in this pass because the researched target list names `CheckNewTrackStart()` as the start gate, and that method calls `StartTrack()` internally.
- Suppression is allowed only when:
  - TAOM music is enabled.
  - The tavern route bucket is enabled.
  - `IMusicPlaybackService` reports TAOM currently owns the active `MusicBucket.Tavern` route.
- The Harmony prefixes fail open: if IoC, settings, playback state, or adapter release fails, vanilla proceeds.
- Private `_trackEvent` access is isolated in `IMusicianGroupSuppressionAdapter`; the pure suppression service receives only settings and playback state.
- `MusicianGroup._trackEvent` is added to the reflection-site catalogue and global binding test because it is a static engine reflection site outside Harmony target resolution.

## Tests Added

- `TAOM.Tests/Features/Music/MusicianGroupSuppressionBindingTests.cs`
- `TAOM.Tests/Features/Music/MusicianGroupSuppressionServiceTests.cs`

Tests also extend:

- `MusicPlaybackServiceTests` for `IsActiveBucket(MusicBucket)`.
- `MusicRuntimeWiringTests` for `Patch46_Music` and IoC registration.
- `ReflectionSiteBindingTests` and `docs/reference/taleworlds-api-snapshot/reflection-sites.md` for `MusicianGroup._trackEvent`.
