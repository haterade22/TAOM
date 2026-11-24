# Battle Shader Notice

## Overview

When a battle load has to compile many shaders, the small window of the [Shader Compile Notice](shader-compile-notice.md)
appears over the loading screen and counts them ("Preparing this scene: 412 shaders compiled"), and the loading
screen's bottom bar says "Compiling shaders for this scene. This can take a minute or two; the game is not frozen."
Every mission load is checked, a town or a tavern too, so the texts say "scene".
Both go away when the battle is ready. A battle with a warm shader cache shows neither.

## Why This Exists

- **Vanilla behavior:** after a game update or a change to the mod list the engine throws its shader cache away, and the
  next battles compile their shaders while the loading screen shows a still picture. The game thread stops inside one
  frame for most of that time, so even the corner animation freezes.
- **TAOM requirement:** players should see that the game is working, as they do at startup.
- **Without this feature:** the load looks like a hang, Windows may mark the window "Not Responding", and players kill a
  working game.

Measured on the dev PC (taom_debug logs of 19 Oct, 31 Oct and 15 Nov 2026; 15 Nov with `rgl_log_39588.txt`): the
`WaitingForRender` stamp of [battle-load-diagnostics.md](battle-load-diagnostics.md) shows 714 to 1,787 shaders queued
on a cold load, then one or two frames with the count falling, then a gap of 15 to 110 s with no stamp until
`shaders=0`. On 15 Nov the gap was 44 s (15:09:11 to 15:09:55) and the engine wrote 706 `compile_shader` lines in it,
6 to 25 a second. Warm loads queued 0 to 20 and waited about 0.3 s.

## Character creation (2026-11-24)

The faction screen shows a 3D character for every faction and pick, so on a cold shader cache character creation
compiles many shaders too. `FactionUITicker` calls `BattleShaderNoticeService.OnScreenTick` every application tick
with "character creation is on the stack" (`IScreenShaderNotice`). While it is, the service reads the queued count
(`IShaderCompileQueueAdapter`) and starts the same window from the same 50 queued, with no loading-screen line, and
closes it when the queue is empty or character creation closes. A battle notice already up owns the window.
Owed in game: on a cold cache, open the faction screen; the window counts while it loads and closes after.

## Architecture

### Design Challenge

Nothing the game draws can move while the game thread waits, and the wait starts a frame or two after the load reaches
its render-ready check. TAOM reads the queued count (`Utilities.GetNumberOfShaderCompilationsInProgress`, one field read in
native) only on the game thread, because the window thread never calls the engine, so the count is known only
before the stop.

### Solution Approach

- **Detection.** A postfix on `MissionState.OnTick` (Patch155, the same frames Patch43's
  `MissionState_OnTick_RenderWait_Patch` measures) passes "in the render wait" (`FirstMissionTickAfterLoading` and the
  mission `Continuing`) and, on a waiting frame before the notice starts, the queued count. `BattleShaderNoticeGate`
  starts the notice once per load at 50 queued or more and stops it on the first frame that is not waiting: the frame
  the mission finally ticks, since `TickMission` clears the flag before the postfix runs.
- **The window.** `BattleShaderNoticeRunner` runs the startup notice's window host on an STA thread of its own in
  `ShaderCompileNoticeMode.Battle`: it reads `rgl_log_<pid>.txt` from its length at the start (earlier compiles are not
  counted), shows the window after one second (a queue the engine clears at once never flashes a window), leaves the
  process priority alone (the compile is what the player waits for), writes no remembered total, closes 30 s after the
  last new `compile_shader` line (a net for a mission that ends without another frame), and gives up after 15 minutes.
  The count has no "of about N": on 15 Nov the queue said 1,192 and 706 lines came before the wait ended.
- **The loading-screen line.** A postfix on the internal `LoadingWindowViewModel.Update` (called every frame from
  `GauntletDefaultLoadingWindowManager.OnLateTick`) writes the line into `DescriptionText` while the notice is up and the current mission state is still in its
  render wait. TAOM's
  `TAOMLoadingWindow.xml` shows `DescriptionText` in its bottom bar; in single player nothing else writes it. The line is
  set in the frames before the stop and stays through it, and it shows in every display mode, also where the window
  might not.
- **Texts** resolve on the game thread when the notice starts; the window thread only fills `%COUNT%`.
- **Stands down** on a dedicated server. It does not stand down for VanillaTuning: the startup notice does because
  VanillaTuning shows its own startup notice; nothing TAOM has read says VanillaTuning shows one during battles.

### Component Diagram

```
MissionState.OnTick ──> MissionState_OnTick_BattleShaderNotice_Patch (waiting?, queued count)
                              └── BattleShaderNoticeService ── BattleShaderNoticeGate (pure: Start / Stop / None)
                                     ├── IBattleShaderNoticeTextSource (TextObject, game thread)
                                     ├── IBattleShaderNoticeRunner ── STA thread ── IShaderCompileNoticeWindowHost
                                     │                                   (TAOM.Client: ShaderCompileNoticeContext, Battle mode)
                                     └── LoadingScreenLine
LoadingWindowViewModel.Update ──> LoadingWindowViewModel_Update_BattleShaderLine_Patch ──> DescriptionText
```

## Configuration

None. Log lines in `taom_debug.log`: `[BattleShaderNotice] N shaders queued for this mission: notice started` (game
thread), `[BattleShaderNotice] window shown after S s, C compiled so far`, `[BattleShaderNotice] C shaders compiled,
notice closed after S s` and, when the net closes it, `[BattleShaderNotice] no shader compiled for S s, closing`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BattleShaderNotice/BattleShaderNoticeGate.cs` | Pure: when to start and stop, the 50-shader threshold |
| `Main/Features/BattleShaderNotice/BattleShaderNoticeService.cs` | Server check, texts, window start and stop, the loading-screen line |
| `Main/Features/BattleShaderNotice/BattleShaderNoticeRunner.cs` | The window's STA thread, one run per battle load |
| `Main/Features/BattleShaderNotice/BattleShaderNoticeTextSource.cs` | Engine boundary for the three texts |
| `Main/Features/BattleShaderNotice/BattleShaderNoticeTexts.cs` | The texts and the runner and text-source interfaces |
| `Main/Features/BattleShaderNotice/Hooks/MissionState_OnTick_BattleShaderNotice_Patch.cs` | Patch155: render-wait detection |
| `Main/Features/BattleShaderNotice/Hooks/LoadingWindowViewModel_Update_BattleShaderLine_Patch.cs` | Patch155: the loading-screen line, `[ClientOnly]` |
| `Main/Features/BattleShaderNotice/BattleShaderNoticeModule.cs` | Feature module: services, statics, Patch155 at GameInit |
| `Main/Features/ShaderCompileNotice/ShaderCompileNoticeMode.cs` | Pure: the Startup and Battle settings of the shared window |
| `Client/Features/ShaderCompileNotice/ShaderCompileNoticeContext.cs` | The shared window loop, driven by the mode |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `taom_battle_shader_notice_title`, `taom_battle_shader_notice_detail`, `taom_battle_shader_loading_line` |

### Limits

- The service clears the loading-screen line on the frame the mission ticks. The writer also requires the engine's
  render wait on the current mission state (`FirstMissionTickAfterLoading` with the mission `Continuing`, the test
  the `OnTick` hook uses). Once a mission state is finalized, `OnFinalize` clears `MissionState.Current`, so nothing
  writes the line again; a line already on the window stays until the window's next enable clears it. No engine
  path that ends a mission state during its wait was found (#1308 review, lens 5 F2).
- The parked Patch21 (`LoadingScreen_ShaderProgress_Patch`) writes the same `DescriptionText`. Before it is
  re-enabled, keep the two writers apart: its shader walk loads cold missions on purpose.

## Dependencies

- `IShaderCompileNoticeWindowHost` (the startup notice's window host; TAOM.Client registers the WinForms one).
- `IDedicatedServerProvider`, `IModLogger`.
- The engine's `rgl_log_<pid>.txt` and its `compile_shader:` line, as for the startup notice.

## Tests

- `TAOM.Tests/Features/BattleShaderNotice/BattleShaderNoticeGateTests.cs`: 11 tests (threshold either side, unreadable
  count, a count that rises later, no second start, stop, nothing after the stop, the next load).
- `TAOM.Tests/Features/BattleShaderNotice/BattleShaderNoticeServiceTests.cs` (start with the texts, the line,
  warm load, stop clears the line, ordinary frames, dedicated server, texts or window failing, the start log line, the line only during the render wait).
- `TAOM.Tests/Features/ShaderCompileNotice/ShaderCompileNoticeModeTests.cs`: 13 tests (start position, show rule and
  idle close per mode, the startup mode unchanged).
- The thread, the window and the frame timing are engine and OS boundaries: in game only.

## How to verify in game

**Preconditions:** a cold shader cache: change the mod list (any module on or off), or move
`C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders` aside, then start the game (the startup notice runs
first).

1. Start a field battle from the campaign. Pass: the bottom bar of the loading screen shows the line, the window
   appears within a second or two and counts up, both are gone when the battle starts, and `taom_debug.log` has the
   `queued ... notice started` and `notice closed after` lines. A person must look for the window and the line.
2. Start a second battle in the same session. Pass: no window, no line, no `[BattleShaderNotice]` line.
3. Repeat step 1 in exclusive full screen. The window may not show there; the line must.

## Changelog

- 2026-11-15: added (owner's request: battle loads looked crashed).

## GitHub Issue

- **Issue:** not filed (the owner's standing approval covers branches and PRs, not issues; the maintainer may file or
  waive one)
- **Status:** not checked in game
