# Music and sound events

Sibling reference, not a numbered phase. How vanilla music runs, how one mission can play its own track without
touching music anywhere else, and what the engine does with a module sound. Written for the Story Missions track
(#1105) on 2026-11-10, against the installed v1.5.7 client.

Every claim carries a tag:

- **[v1.5.7 managed]**: read in the v1.5.7 decompile (`pwsh tools/taom-src.ps1 path <Type>`), with its line.
- **[v1.5.7 native]**: read in a Ghidra decompile of the v1.5.7 `TaleWorlds.Native.dll` (re-run with
  `tools/native_decompile.py`, [doc](../../features/ghidra-native-decompile.md)). Offsets hold for that binary only.
- **[read from code]**: follows from the code, never seen or heard in game.
- **[UNVERIFIED]**: not proven; the line says what would prove it.

## 1. Who plays vanilla music

[v1.5.7 managed] Vanilla music is psai, driven by `MBMusicManager`. It has three handler slots, campaign, battle
and silenced, and the active handler is `_battleMusicHandler ?? _silencedMusicHandler ?? _campaignMusicHandler`
(`MBMusicManager.cs`:430-433). Each `On*MusicHandlerInit` sets its slot and makes it active at once; each
`On*MusicHandlerFinalize` nulls its slot with no owner check and recomputes the active handler (:394-428). So a
silenced slot that nobody releases keeps outranking the campaign handler. A new map screen's
`OnCampaignMusicHandlerInit` makes the campaign handler active directly (:394-398) but leaves the slot set, so the
next recompute (any finalize, such as the end of a battle with a battle music view, :412-416) makes the slot active
again (:430-433). Only `OnSilencedMusicHandlerFinalize` clears it (:424-428). `MBMusicManager` lives for the whole
process, so a leftover slot survives a save load.

[v1.5.7 managed] `ViewSubModule` calls `MBMusicManager.Update` from both `OnApplicationTick` and
`AfterAsyncTickTick`, and the second runs on the async AI thread
([frame threads](mission-frame-threads-and-native-costs.md)). `Update` returns early when it already ran in this
engine frame (`MBMusicManager.cs`:545-551); the check takes no lock, so two overlapping callers can both run it. So a handler's `OnUpdated` can run off the main thread: keep it
free of game state, as vanilla's silenced view and TAOM's empty handler do.

- **Campaign:** `CampaignMusicHandler`, which the map screen creates and drops only in `MapScreen.OnFinalize`.
  When the mode is Paused it calls `ActivateCampaignMode`, and it starts a theme after a rest of 30 to 120 s
  (`CampaignMusicHandler.cs`:37-63).
- **Battle:** `MusicBattleMissionView`, `MusicTournamentMissionView`, `MusicArenaPracticeMissionView`,
  `MusicStealthMissionView`.
- **Silenced:** `MusicSilencedMissionView`, in SandBox's Indoor, PrisonBreak, ArenaDuelMission and HideoutBattle
  mission lists. Its `OnUpdated` is empty.

[read from code] A mission pushed over the map with no music view (every TAOM-owned mission list on 2026-11-10:
Long Defeat, Host Review, Fords of War, and a story mission whose row names no track) leaves the campaign handler
active, so campaign themes play in the battle.

## 2. The pause flag is process-wide

[v1.5.7 managed] `PauseMusicManagerSystem` and `UnpauseMusicManagerSystem` set one `bool _systemPaused`
(`MBMusicManager.cs`:332, 499-518). It is not a counter. While it is set, `Update` skips the mode switch and the
active handler's `OnUpdated` (:556-580), so no handler starts a theme; a theme that already plays runs to its end,
because psai still updates (:582). Only `InitialState.OnActivate` (the main menu) and
`VideoPlaybackState.OnVideoFinished` clear the flag, so a pause set from a mission leaves the campaign map with no
new music until the player next reaches the main menu. **Never call `PauseMusicManagerSystem` from TAOM code**; the gate is
`MusicPauseGateTests`.

## 3. Silencing one mission

[v1.5.7 managed] `MusicSilencedMissionView` calls `DeactivateCurrentMode()` (psai stops with a 3 s fade and the
mode becomes Paused) and `OnSilencedMusicHandlerInit(this)` in `OnBehaviorInitialize`, and
`OnSilencedMusicHandlerFinalize()` in `OnMissionScreenFinalize`. Two limits:

- `OnMissionScreenFinalize` comes from `IMissionListener.OnEndMission`, which `Mission.EndMission` reaches; the win,
  the loss, a retreat and Exit to Main Menu all go that way. A teardown that finalizes the mission state without
  `Mission.EndMission` (`GameStateManager.CleanStates` at shutdown, or a mod that loads a save mid-mission; vanilla's
  mission escape menu has no Load) reaches only `OnRemoveBehavior`
  ([lifecycle](mission-and-missionbehavior-lifecycle.md)), so the vanilla view can leave its slot set. Release the
  slot from both hooks through one method that is safe to call twice.
- `OnBehaviorInitialize` runs only for a behavior the mission's own list adds; `AddTaomBehavior` never calls it.

When the slot is released, the campaign handler becomes active again, sees Paused and restarts campaign music by
itself. TAOM's `MissionMusicAdapter` follows this pattern (#1105).

## 4. Module sounds cannot carry music

[v1.5.7 native] The `module_sounds.xml` parser (`FUN_1802330b0`, RVA 0x2330B0) accepts exactly 20 category names,
from `mission_ambient_bed` to `campaign_bed`, and none is `music`. Any other name logs "Ignoring module sound.
Name:%s! Invalid category given." and the sound is dropped. Native's own `module_sounds.xml` header still lists
`music`; that header is out of date for v1.5.7.

## 5. Playing a track

[v1.5.7 managed] `SoundEvent.CreateEventFromExternalFile(programmerEventName, soundFilePath, scene, is3d,
isBlocking)` (`SoundEvent.cs`:185) makes an event that plays a file. TAOM plays it on `event:/music`, the event
psai's own channels are made on (native, with no scene, 2D, not blocking), so it most likely follows the same Music
fader as vanilla music [UNVERIFIED].

- [UNVERIFIED] Which volume slider `event:/music` follows: the routing is bank data. The #1105 manifest has the
  in-game row.
- [v1.5.7 managed] `Play()` returns the start result only (`SoundEvent.cs`:77-80). The file is opened later in the
  sound callback, and a file that does not decode reports nothing to managed code, so a pass on `Play()` is not
  proof that anyone hears the track. [UNVERIFIED] How long an event with no sound runs before it reports stopped:
  that depends on the sound bank, and TAOM's 10 s floor for a replay assumes it ends at once.
- [v1.5.7 native] The CREATE_PROGRAMMER_SOUND callback creates the sound with no stream flag, and the
  DESTROY_PROGRAMMER_SOUND callback frees it, so the sound lives for one play. [UNVERIFIED] That the whole file is
  decoded into memory (FMOD's documented default, not read in the binary), and whether to 16-bit or float: 37 or
  74 MB for a 3 minute 48 kHz stereo track.
- [v1.5.7 native] An event that ends by itself is released at the next native sound update: the STOPPED callback
  queues its id, and the drain releases the FMOD instance and removes the id. After that `IsValid` is false and
  `Play()` on the same object fails, so a replay needs a new event, as psai's own channels make one. Ids come from
  a counter, so an old object never finds another event's id. A `Stop()` before the drain finds the event stopped
  and queues its id a second time (`FUN_180477790`); the release looks the id up and does nothing when it is gone
  (`FUN_180237200`), so the second entry is harmless. The drain releases each id through slot +0x160 of
  `rglFMOD_Sound_manager::vftable` (RVA 0xAFD3D8), which holds `FUN_180237200`. A replay can start while the old
  event still fades out; each event's callback reads its own holder, so the old event never finds the new id.
- [v1.5.7 managed] `Stop()` sets the id to -1 (`SoundEvent.cs`:112-119), so `Release()` after it does nothing,
  and `Release()` on a playing event stops it and then releases id -1 (:152-163). Native `ReleaseEvent` only logs.
  Stop, then drop the reference. [v1.5.7 native] `Stop()` also frees an event that was created but never started:
  the native stop queues the id of an event in state 2, and the drain releases it. [UNVERIFIED] That an event that
  never started reads state 2 (FMOD's documented starting state, not read in the binary).
- Ship OGG Vorbis, not WAV: Native's `module_sounds.xml` header names `.ogg` and `.wav` as the supported formats,
  and the #1105 track is 3.8 MB as OGG against 73.9 MB as a float WAV.

## Where TAOM uses it

- `Client/Features/StoryMissions/StoryMusicMissionView.cs` and `Main/Features/StoryMissions/StoryMusicCue.cs`: a
  story mission's own track, which silences vanilla music for that mission only and gives it back as soon as the
  track can no longer play (an early end, a refused replay or a fault), not only at the end of the mission.
- `Main/Features/FactionUI/Hooks/MBMusicManagerActivateMenuModePatch.cs`: keeps the menu theme off the main menu
  while the menu video plays ([faction UI](../../features/faction-ui.md)).
