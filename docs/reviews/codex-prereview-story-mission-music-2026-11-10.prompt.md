TAOM is a Lord of the Rings total-conversion mod for Mount & Blade II: Bannerlord v1.5.7 (.NET Framework 4.7.2). You review an uncommitted change in the worktree `E:/repos/taom-story-missions` (branch `feat/story-missions`, HEAD `97ef02318`). Read the change with `git diff HEAD` and `git ls-files --others --exclude-standard`. Every file in those two lists is in scope; no other file in the worktree is modified.

This is a review only. Do not edit, stage or commit anything.

## The maintainer's request (the decisions)

The maintainer asked for this track in the Helm's Deep story mission: `E:\LOTRAOMAssets\Isengard vs. Rohan Third Draft.wav`. The constraint, quoted: "But we have to review the music pipeline/workflow and ensure we are not removing all music just for this".

The bar for this review: no path in this change may remove, mute, pause or leave silenced any music outside the one story mission whose row names the track. That covers the campaign map, every other mission, the main menu, and later missions and campaigns in the same process.

## What changed (the implementing session's description: verify it, do not trust it)

- `Main/_Module/ModuleData/story_missions/story_missions.json`: the `helms_deep_defend` row gains `"music": "StoryMusic/isengard_vs_rohan.ogg"`.
- `Main/Features/StoryMissions/Config/StoryMissionCatalog.cs`, `Main/Features/StoryMissions/Domain/StoryMissionDefinition.cs`: an optional `music` field. A value that is not a relative `.ogg` path of at most 120 characters with no `..` is dropped with a warning.
- `Main/Features/StoryMissions/Hooks/StoryMissionLauncher.cs`, `Main/Features/StoryMissions/Hooks/StoryMissionLogic.cs`: the mission logic carries `Music` and `MissionKey`.
- `Main/Adapters/IMissionMusicAdapter.cs`, `Main/Adapters/MissionMusicAdapter.cs`: plays a file on `event:/music` through `SoundEvent.CreateEventFromExternalFile`, and takes vanilla's silenced music-handler slot (`DeactivateCurrentMode`, then `MBMusicManager.OnSilencedMusicHandlerInit`). It gives the slot back with `OnSilencedMusicHandlerFinalize`.
- `Main/Features/StoryMissions/StoryMusicCue.cs`: start, loop and release logic, and the `IOutcomeLog` check lines.
- `Client/Features/StoryMissions/StoryMusicMissionView.cs`, `Client/Features/StoryMissions/StoryMissionViews.cs`: a `MissionView` that the story mission's `[ViewMethod]` list adds only when the row names a track. It starts the cue in `OnSceneRenderingStarted`, ticks it in `OnMissionScreenTick`, and releases it in both `OnMissionScreenFinalize` and `OnRemoveBehavior`.
- `Main/_Module/ModuleSounds/StoryMusic/isengard_vs_rohan.ogg`: a Vorbis encode of the WAV (3,778,090 bytes, 48 kHz, stereo, 192.5 s).
- Tests: `TAOM.Tests/Features/StoryMissions/StoryMusicCueTests.cs` (new), `StoryMissionCatalogTests.cs`, `ShippedStoryMissionsTests.cs`, `StoryMissionWiringPinTests.cs`; `TAOM.Tests/Infrastructure/MusicPauseGateTests.cs` (new: no TAOM code calls `PauseMusicManagerSystem`); `TAOM.Tests/Infrastructure/MissionViewRegistrationGateTests.cs` (an allowlist entry for the new view).
- Docs and data: `docs/reference/engine/music-and-sound-events.md` (new), `docs/INDEX.md`, `docs/reference/provenance-register.md`, `tools/ingame_checks/1105.toml`.

## Evidence the session read on 2026-11-10

- Full `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: 38,400 passed, 1 failed, 9 skipped. The failure was `MissionViewRegistrationGateTests`: the new view had no allowlist entry. With the entry, that test passes. The full suite has not run again since that edit.
- `python tools/check_provenance.py`: 0 misses.
- Nothing has run in game. Nobody has heard the track in game.

## Engine lookup

Take signatures and bodies from the installed v1.5.7 DLLs: `pwsh tools/taom-src.ps1 path <Type>` prints the path of the decompiled file. Types to read: `MBMusicManager`, `CampaignMusicHandler`, `MusicSilencedMissionView`, `MusicBattleMissionView`, `SoundEvent`, `MissionScreen`, `MissionView`, `Mission`, `ModuleHelper`, `InitialState`, `MapScreen`. Native code: `python tools/native_decompile.py` (`docs/features/ghidra-native-decompile.md`). Mark as UNVERIFIED anything you cannot prove from code.

## Known suspects: CONFIRM or DISPUTE each with file:line evidence

1. **Owner-blind release.** `MBMusicManager.OnSilencedMusicHandlerFinalize()` clears the silenced slot with no check of who set it. Is there an order of events in which this change's release clears a slot that another owner set (a vanilla `MusicSilencedMissionView` in the next mission, a conversation, a tavern)? Or in which another owner's release clears this change's slot while the track still plays?
2. **A slot left set.** List every way a story mission can end: win, loss, retreat, Exit to Main Menu, a save loaded from inside the mission, an exception in another view, `MBGameManager.EndGame`. For each one, does `OnMissionScreenFinalize` or `OnRemoveBehavior` reach this view? A silenced slot that nobody releases outranks the campaign handler for the rest of the process.
3. **Campaign music after the release.** `Silence()` calls `DeactivateCurrentMode()` first. After the release, does campaign music come back with no further call? The session reads `CampaignMusicHandler` as re-activating Campaign mode when the mode is Paused. Do later field battles and sieges get vanilla battle music as before?
4. **The pause flag.** `MBMusicManager.PauseMusicManagerSystem` sets a process-wide flag. Confirm that nothing in this change, and nothing it calls, sets that flag. Is `MusicPauseGateTests` (a whole-word search outside comments over `RepoPaths.CSharpFiles()`) a sound gate?
5. **The loop.** `StoryMusicCue.Tick` calls `TryRestart` (`SoundEvent.Play()` again on the same event) when `IsStopped()` is true after at least 10 s of play. Does a stopped external-file event on `event:/music` play again on `Play()`, or has native released it? What happens at the end of the track in each case?
6. **Memory per mission.** `SoundEvent.Stop()` sets the managed id to -1, so the adapter never calls `Release()`. Does the native stop release the event and its decoded sound? If not, how much memory does each story mission keep?
7. **The path.** The view builds the path as `IPathService.ModuleRootPath + "ModuleSounds/" + music`, and `ModuleRootPath` is `ModuleHelper.GetModuleFullPath("TAOM")`. Is that path absolute or relative to the process directory? Does the native external-file open accept that form?
8. **The volume slider.** Which slider controls a sound on `event:/music`: the Music slider, which the session expects, or another one? The session marked this UNVERIFIED.
9. **Threads.** Do `OnSceneRenderingStarted`, `OnMissionScreenTick`, `OnMissionScreenFinalize` and `OnRemoveBehavior` all run on the main thread, so that the cue's flags need no lock?
10. **Server and co-op.** The view is in `TAOM.Client`. The adapter is in `TAOM` (Main), which a dedicated server also loads. Can any server path construct `MissionMusicAdapter` or touch `SoundEvent` or `MBMusicManager` through this change?
11. **The engine doc.** Does each [v1.5.7 managed] and [v1.5.7 native] claim in `docs/reference/engine/music-and-sound-events.md` match the code at the line it cites?
12. **The manifest.** Do the `pass` patterns in `tools/ingame_checks/1105.toml` match the lines that `StoryMusicCue` writes, after `OutcomeLog.SanitiseDetail` (`Main/Core/Logging/OutcomeLog.cs`:152-165)?

## What not to flag

- The track's rights. The provenance row is `UNKNOWN` and `uncleared` on purpose, until the maintainer answers.
- Story Missions Slice 1 code outside this diff, unless this change depends on it.
- Loop against play once. The maintainer has not yet confirmed the loop default; it is not a defect.
- The `docs/features/story-missions.md` feature doc, which a later slice writes.

Report P1, P2 and P3 findings with `file:line` for every claim. Say explicitly where you disagree with the session's description above.
