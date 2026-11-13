TAOM is a Lord of the Rings total-conversion mod for Mount & Blade II: Bannerlord v1.5.7 (.NET Framework 4.7.2). You re-review the fix for one HIGH finding in an uncommitted change in the worktree `E:/repos/taom-story-missions` (branch `feat/story-missions`, HEAD `97ef02318`). Read the change with `git diff HEAD` and `git ls-files --others --exclude-standard`.

This is a review only. Do not edit, stage or commit anything.

## The maintainer's bar

The maintainer asked for one track in the Helm's Deep story mission and said: "ensure we are not removing all music just for this". No path may remove, mute, pause or leave silenced any music outside the one story mission whose row names the track.

## The findings that were fixed (verify each fix; do not trust this description)

1. **HIGH, the loop.** The old `StoryMusicCue.Tick` called `TryRestart`, which was `SoundEvent.Play()` on the same event. The native sound manager releases an event that ends by itself (the STOPPED callback queues its id; the next sound update releases the FMOD instance and removes the id), so `IsValid` became false and the replay failed: every story mission longer than the 3 min 12 s track went silent. The fix: `Tick` calls `IMissionMusicAdapter.TryPlay(path)` again, which stops and drops the old event and creates a new one. `TryRestart` is gone from the interface and the adapter.
2. **MEDIUM, silence after the loop ends.** The old cue kept vanilla music silenced until the mission ended when the loop stopped (an early end, a refused replay, a fault). The fix: `EndLoop` calls `Unsilence` at once, and `Release` does not unsilence a second time. The stopped check line now carries `loop=ok|early|refused|fault` and is a fail unless `loop=ok`.
3. **LOW, a refused or faulted first play.** The adapter could hold an event that the engine created but never started. The fix: `Start` calls `Stop()` (in a try) on its refused and fault branches.
4. **LOW, the pause gate.** `TAOM.Tests/Infrastructure/MusicPauseGateTests.cs` now also scans the IL of TAOM.dll and TAOM.Client.dll for a call to `MBMusicManager.PauseMusicManagerSystem`, with a canary method that the scan must find.

Files of the fix: `Main/Features/StoryMissions/StoryMusicCue.cs`, `Main/Adapters/IMissionMusicAdapter.cs`, `Main/Adapters/MissionMusicAdapter.cs`, `TAOM.Tests/Features/StoryMissions/StoryMusicCueTests.cs`, `TAOM.Tests/Infrastructure/MusicPauseGateTests.cs`, `tools/ingame_checks/1105.toml`, `docs/reference/engine/music-and-sound-events.md`. The rest of the diff was reviewed at `ultra` already; read it only as context.

## Evidence the session read on 2026-11-10

- Filtered `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~StoryMusicCueTests|FullyQualifiedName~MusicPauseGateTests|FullyQualifiedName~StoryMission"`: 183 passed, 0 failed. The full suite has not run since the fix. Bannerlord is running a separate test on this machine: do not run the full test suite or any build that deploys.
- Native evidence (Ghidra decompile of the installed `TaleWorlds.Native.dll`): the STOPPED callback branch and the release function `FUN_180237200`; event ids come from a counter (`param_1[5]++` in `FUN_18015f960`). Re-derive with `python tools/native_decompile.py` if you dispute it.
- Nothing has run in game. Nobody has heard the track.

## Questions: CONFIRM or DISPUTE each with file:line evidence

1. Does the new `Tick` replay the track for a mission of any length? Is there a frame, between the STOPPED callback and the native release, in which `TryPlay` (its `Stop()` of the old event, then the create) misbehaves?
2. After `EndLoop`, is there any path on which `Release` or a later `Tick` silences vanilla music again, or unsilences a slot that another owner set?
3. With vanilla music given back in the middle of the mission, what plays? The story mission's view list holds no vanilla music view. Is the result the campaign handler's themes, as in a mission with no track?
4. Can a replay on every 10 s floor, or a fast loop of create and fail, cost memory or file opens? `MinimumPlaySeconds` is 10.
5. Does the IL gate's canary prove the scan (a `callvirt` on `MBMusicManager.PauseMusicManagerSystem`), and is any TAOM code shape (a delegate, a lambda, an async state machine, a generic method) outside what `IlCallScanner.FindCallers` walks?
6. Do the `pass` and `fail` patterns in `tools/ingame_checks/1105.toml` match the lines `StoryMusicCue` now writes, after `OutcomeLog.SanitiseDetail` (`Main/Core/Logging/OutcomeLog.cs`)?
7. Is every claim the fix added to `docs/reference/engine/music-and-sound-events.md` (sections 1 and 5) true at the code it cites?

## What not to flag

- The track's rights (the provenance row is `UNKNOWN` on purpose), and loop against play once (the maintainer has not answered yet).
- Story Missions Slice 1 code outside this diff, unless the fix depends on it.

Report P1, P2 and P3 findings with `file:line` for every claim. Say explicitly where you disagree with the description above.
