# Shader Compile Notice

## Overview

While the engine rebuilds its runtime shader cache before the main menu, a small window over the game says so and
counts the shaders ("Compiling shaders: 412 of about 1336"), and the game runs at BelowNormal priority so the rest of
the computer stays usable. It closes at the main menu. The idea is yotthani's (VanillaTuning's
`shader-compile-notice`); the code is TAOM's.

## Why This Exists

- **Vanilla behavior:** after a module-list change, a game update or a fresh install the engine compiles about 1,340
  shaders before the main menu, every core busy, with the loading symbol frozen: 32.5 to 42.5 s on the dev machine
  (2026-10-02 perf audit, FOR-MIKE item 1), minutes on a small CPU. The game looks hung.
- **TAOM requirement:** players should know the game is working, not frozen, and keep a usable PC meanwhile.
- **Without this feature:** players kill a working game, or report a hang.

[ShaderCacheKeeper](shader-cache-keeper.md) avoids the rebuild when no module with shader content changed; this
notice covers the rebuilds that still happen.

## Architecture

### Design Challenge

The engine's main thread is blocked while it compiles, so nothing can be drawn inside the game, and the engine offers
no compile progress to managed code.

### Solution Approach

- **Timing.** `SubModule.OnSubModuleLoad` starts the notice right after the logger exists. In the cold start of
  2026-10-06 14:20 (`rgl_log_67284`), `TAOM.dll` loaded at 14:20:47.5 and the first `compile_shader` came at
  14:20:53.4, so the notice is up before the compile begins. It stops at the first
  `OnBeforeInitialModuleScreenSetAsRoot`; a 30-minute cap stops it if that never comes.
- **Progress.** The engine writes one `compile_shader:` line per shader to
  `%ProgramData%\Mount and Blade II Bannerlord\logs\rgl_log_<pid>.txt`. A thread of its own reads the new end of that
  file every 300 ms and counts the lines (`ShaderCompileProgress.Feed`, a marker split between two reads counted
  once). The window appears from 10 compiles on: a handful compile on many warm starts.
- **Expected total.** A count above 200 at close is written to
  `%ProgramData%\Mount and Blade II Bannerlord\Shaders\taom_shader_compile_total.txt` and read at the next start for
  "of about N"; without it, or once the count passes it, the title shows the count alone.
- **The window.** WinForms on its own STA thread (`ApplicationContext` with a WinForms timer): borderless, never
  activated (`WS_EX_NOACTIVATE`), out of the task bar (`WS_EX_TOOLWINDOW`), owned by the game's window so it lies over
  the game but not over programs in front of it, sized from its own texts, centred at 62 percent of the game window's
  height (the loading symbol sits in the middle).
- **Priority.** While the window is up the process runs at BelowNormal, restored at close; a priority that is not
  Normal at that moment was set on purpose by someone and is left alone.
- **Texts.** Resolved on the game thread before the thread starts (the engine loads every module's language files
  before any `OnSubModuleLoad`: `Module.Initialize`, v1.5.4 lines 277 and 282); the notice thread only fills the
  `%COUNT%` and `%TOTAL%` placeholders and never touches the engine.
- **Stands down** on a dedicated server (`IDedicatedServerProvider`) and when yotthani's VanillaTuning module is
  active, which shows the same notice.

### Component Diagram

```
SubModule.OnSubModuleLoad ──> ShaderCompileNoticeEntry.Start   (game thread: texts, module list, server check)
                                    └── ShaderCompileNotice.Start ──> STA thread: ApplicationContext + timer
                                              ├── reads rgl_log_<pid>.txt ──> ShaderCompileProgress (pure, tested)
                                              └── ShaderCompileNoticeWindow (WinForms), process priority
SubModule.OnBeforeInitialModuleScreenSetAsRoot ──> ShaderCompileNoticeEntry.Stop
```

## Configuration

None. Log lines: `[ShaderNotice] the engine is compiling its shaders: notice shown, expected about N` and
`[ShaderNotice] N shaders compiled, notice closed after S s` in `taom_debug.log`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ShaderCompileNotice/ShaderCompileProgress.cs` | Pure: counting, show threshold, title, remembered total, `ShouldStart` |
| `Main/Features/ShaderCompileNotice/ShaderCompileNoticeTexts.cs` | The resolved texts and their placeholders |
| `Main/Features/ShaderCompileNotice/ShaderCompileNotice.cs` | Boundary: the thread, the log reading, the priority |
| `Main/Features/ShaderCompileNotice/ShaderCompileNoticeWindow.cs` | The window |
| `Main/Features/ShaderCompileNotice/ShaderCompileNoticeEntry.cs` | Engine boundary: texts, module list, start and stop |
| `Main/SubModule.cs` | Start in `OnSubModuleLoad`, stop in `OnBeforeInitialModuleScreenSetAsRoot` |
| `Main/TAOM.csproj` | `System.Windows.Forms` and `System.Drawing` references (the game bin ships neither) |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `taom_shader_notice_title_total`, `taom_shader_notice_title_count`, `taom_shader_notice_detail` |

## Dependencies

- `IModLogger` (thread-safe `FileLogger`), `IDedicatedServerProvider`.
- The engine's `rgl_log_<pid>.txt` and its `compile_shader:` line (read 2026-10-06, v1.5.4).

## Tests

- `TAOM.Tests/Features/ShaderCompileNotice/ShaderCompileProgressTests.cs`: 16 tests (counting in one chunk, a marker
  split across chunks, no recount from the carried tail, empty input, the show threshold either side, the remember
  floor, the three title cases, the remembered-total parse, and `ShouldStart` for a player, a server, an active
  VanillaTuning and no module list).
- The thread, window and priority are engine and OS boundaries: in-game only.

## Verification owed

In game, after a module-list change (or with `C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders`
emptied): the window appears over the loading screen within a few seconds, counts up, does not take the focus, sits
under any program in front of the game, closes at the main menu; Task Manager shows Below normal while it is up and
Normal after; `taom_debug.log` has both `[ShaderNotice]` lines; the second cold start shows "of about N". Also check
exclusive fullscreen, where the game window owns the display. Translations: the three keys are registered in English
only; the translator run (`/localize`, paid) is owed.

## Changelog

- 2026-10-06: added. Idea and measurements from yotthani's VanillaTuning `ShaderCompileNotice` (no licence; read,
  then written anew for TAOM).

## GitHub Issue

- **Issue:** [#752, feat: show shader compile progress while the game rebuilds its shaders](https://github.com/haterade22/TAOM/issues/752)
- **Status:** Closed with `triage-needs-ingame` (the in-game check above is owed)
