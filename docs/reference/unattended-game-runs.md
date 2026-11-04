# Unattended game runs

A Claude session can start Bannerlord by itself, let TAOM play a scenario with nobody at the keyboard, and judge the result from
the logs the game wrote. This page is the standing runbook: the permission and its gate, how the launch works, which game-side
files it touches, how to read a run, and what it costs. The skill that follows it is [`/game-run`](../../.claude/skills/game-run/SKILL.md).

## What it is for

In-game evidence without a person. A check that reads `NOT_SEEN` or `NOT REACHED` in [in-game verdicts](../features/ingame-verdicts.md),
or a change to mission or engine-facing code, owes a real game. Today that game is one Fords of War match, driven from the main
menu to the self-check report ([fords-of-war.md](../features/fords-of-war.md), "Unattended runs (autorun)").

## The standing permission

The maintainer decided on 2026-11-01: a session may start the game by itself, without asking, when **both** hold:

1. No `Bannerlord.exe` is running.
2. The PC has had no keyboard or mouse input for 10 minutes.

Otherwise the session asks once. Every run is reported afterwards: the run folder, the verdicts, and what was deployed.

`python tools/run_fords_autorun.py` enforces both conditions in its preflight. It refuses while any `Bannerlord.exe` or the game
folder's `Watchdog.exe` runs, and while `--require-idle-minutes` (default 10) of input silence has not passed. The idle age is
`GetLastInputInfo` against `GetTickCount` (32-bit ticks, so the difference wraps correctly); an age it cannot read refuses too.
`--require-idle-minutes 0` switches the gate off, and a session passes it only after the maintainer has said yes for that run or
that session. An open Modding Kit also refuses, with no override. The Kit and the game share one shader cache, and an engine that starts with another module list deletes it. On 2026-11-03 a match started beside the Kit and the Kit crashed 14 s later (`+0x14C7F7` in its `Win64_Shipping_wEditor` DLL), and the Kit started beside a match and the game crashed about 3 minutes later on its next missing shader (`+0x1FF7FA`). Two crashes with this timing, not yet a proof; 20 matches with the Kit closed had no crash. `--dry-run` prints the idle state without starting anything. The launcher answers the game's dialogs with posted
messages, which are not user input, so a run does not reset the clock it is gated on.

`--keep-on-crash <seconds>` makes the launcher wait after a crash, because the Watchdog holds the process and asks a person about a dump
in a window no script can press. Killing the game then loses the dump, so with a value above 0 the launcher waits up to that long for the
game to end. The default 0 does not wait: the launcher takes its own dump with procdump if that is on the PATH (below), then stops the
game, so the dump the dialog would have written is lost. Pass it whenever someone may be near the PC.

`--speed 10` runs the match at that speed from the first frame (1, 2, 4 or 10). `--live-seconds` stays in mission seconds, so a 900-second match at 10x takes about 90 seconds after the load. Use 4x for balance runs and 10x only for smoke and crash runs: 10x changes the match, as [balance-log.md](../features/fords-of-war/balance-log.md) measures.

`--control-check 240` starts the scripted soldier control at that mission second: it takes the front soldier and hands it back, takes one and waits for its fall, then takes one again. Read the `[FordsOfWar][Control]` lines and the `[Speed]` lines in the run's log. Give `--live-seconds` about 300 seconds more than the start, and put `--screenshots` inside the holds to see the camera.

The launcher does not judge the `[FordsOfWar][Control]` lines: its exit code covers the match, the report and the crash scan, so a session reads them. Each take writes a `took` line, each hand-back a `returned` line and each fall a `fell` line. Ten seconds after a return a `rejoin` line gives `moved`, `ai`, `brain` and `formation` for the returned soldier, which says whether its AI drives it again. A soldier that outlives the 180-second hold of the second round is handed back, so it writes `returned`, not `fell`. While a soldier is held the match runs at 1x, so above 1x the speed changes at each take and `tools/fords_match_review.py` calls the run not a clean sample: do not use a control-check run as a balance sample.

`--screenshots 8,45,65` saves a picture of the screen at those live seconds, plus one during the countdown, through the engine's
`Utilities.TakeScreenshot`. The picture holds the UI, so a session can read the panel itself. The engine writes 24-bit BMP data whatever the file is called, so the launcher
turns each `hud_*.bmp` into a PNG in the run folder when the match ends. Static review cannot see a Gauntlet layout: use this for any change to a prefab.

## Why not headless

Single player cannot run without the client. A dedicated server does not load `TAOM.Client`, and the Fords start needs the client
game manager (`IFordsOfWarGameManagerFactory`, which `TaomClientComposition` replaces in `TAOM.Client`). So a run opens a real window, takes the GPU and the
screen, and needs a desktop session.

## The launch recipe

| Step | What happens |
|---|---|
| Module list | Read from `Main/Properties/launchSettings.json`, profile `Bannerlord`, `$(ModuleId)` read as `TAOM`: the same list as the maintainer's F5 session |
| Process | `bin/Win64_Shipping_Client/Bannerlord.exe /singleplayer _MODULES_*...*_MODULES_`, started directly from that folder with no shell, so the environment reaches the game |
| Arming | Environment variable `TAOM_FORDS_AUTORUN` holds the absolute path of `arm.txt` in the run folder |
| Watching | Polls every 5 s for the done file or the exit; scans `rgl_log_<pid>.txt` and `watchdog_log_<pid>.txt` every 30 s for a crash marker; fails a status heartbeat that stalls 10 minutes |
| Ending | The game quits itself; the launcher waits 120 s, then kills only the process it started (and a `Watchdog.exe` whose command line holds `-p <pid>`) |
| Collecting | Copies the TAOM log, the Fords JSON report and the two engine logs into the run folder before TAOM prunes them |

A boot to the main menu takes about 20 to 80 s, longer when the shader cache is rebuilt. Defaults: 600 mission seconds, a wall limit of
2400 s, and the launcher gives up 15 minutes after the wall limit.

## Game-side files it touches

| File | What the launcher does | Why it matters |
|---|---|---|
| `<MyDocuments>/Mount and Blade II Bannerlord/Configs/engine_config.txt` | Rewrites `safely_exited = 0` to `1` before the start and after any kill (bytes and CRLF otherwise untouched) | A killed game leaves `0`, and the next start stops at a Safe Mode window. `MyDocuments` is OneDrive-redirected on this PC, so the launcher asks PowerShell for the real folder |
| `Configs/BannerlordConfig.txt` | Reads only; logs `StopGameOnFocusLost`. An armed run ignores it: `FordsCommandView` declines the Escape menu that a lost focus opens, and that menu would pause the mission until someone closed it. The value itself stays untouched, because the engine saves the config when the game closes | In the runs so far mission time ran at ratio 1.00 to wall time although it is `True`; the `[Autorun] clock` lines show the ratio on every run |
| `C:/ProgramData/Mount and Blade II Bannerlord/Shaders/CoreShaders/D3D11/shader_mapping.bin` | Reads only; logs whether its module list matches | A cache built for another module list makes the engine show a blocking "Mod change detected" notice before the graphics device starts. The first launch stalled 10 minutes on it. The launcher posts `WM_COMMAND` for that dialog's OK button; its OK only lets the engine rebuild the cache |
| `Modules/TAOM` in the game folder | Not touched by the launcher; whatever is deployed there is what runs | `build.ps1` from a checkout deploys that checkout and replaces the maintainer's own deploy; his next F5 redeploys trunk. Check the `[BuildStamp]` line in the run's log |
| `bin/Win64_Shipping_Client/Logs/taom_debug_*.log` and `fords_report_*_run1.json` | Copies the newest | TAOM prunes about 30 logs and 20 reports |
| `C:/ProgramData/Mount and Blade II Bannerlord/logs/rgl_log_<pid>.txt`, `watchdog_log_<pid>.txt` | Scans and copies | The only native evidence of a crash in an unattended run |

To test other numbers (a different cap, a different wave), edit only the **deployed** ModuleData file, back it up first, and restore it
after the run. Never edit the repo copy for a throwaway test.

## The in-game driver contract

Only the environment variable arms the driver; there is no file fallback, and an unarmed session costs one bool and one null check
per application tick. The full key list and the wiring are in [fords-of-war.md](../features/fords-of-war.md); the shape is:

| Part | Content |
|---|---|
| Env var | `TAOM_FORDS_AUTORUN` = absolute path of a UTF-8 `key=value` arm file (LF) |
| Arm keys | `out` (required, absolute folder), `quit`, `west`, `east`, `ai`, `west_ai`, `speed`, `control_check`, `screenshots`, `scene`, `live_seconds`, `live_wait_seconds`, `wall_limit_seconds` |
| Phases | Wait until the main menu has been up for 4 s (hiding any inquiry), start the match, wait for live, end it after `live_seconds` of mission time or at the wall limit, wait for the final=1 report pass, write the done file, quit |
| Status | `fords_autorun_status.txt` rewritten every 5 s |
| Done file | `fords_autorun_done.txt`: `reason`, `run`, `report`, `log`, `live_seconds`, `wall_seconds`, `final_passes`, `final_total`, `started`, `ended` |
| Reasons | `final` is the only success. Failures: `start_refused`, `start_failed`, `never_live`, `wall_limit`, `final_timeout`, `exit_stalled`, or two joined with `+` |

## Judging a run

The launcher exits 0 only when all of these hold; the run folder's `runner_log.txt` and `runner_status.txt` name the one that failed.

| Evidence | Pass looks like |
|---|---|
| `fords_autorun_done.txt` | `reason=final` |
| `fords_report_*_run1.json` | `"final": true` and no verdict row with `Result` FAIL |
| TAOM log | No `[ERROR] [CrashReport]` line from the done file's `started` time to the end of the log (a swallowed engine exception prints that block even when every in-game check passes; the driver writes `ended` before it quits, so a quit-time capture comes later and still counts) |
| Exit | The game left by itself within 120 s of the done file with code 0 or `4294967295` (-1, normal after the game's own `QuitGame`); any other code, for example `0xC0000005`, fails the run |
| Engine logs | No `Unhandled Exception Code` in `rgl_log`, no `Crash occurred` in `watchdog_log` |

The launcher prints the TAOM log's `[Shutdown] clean module unload` line for the reader and does not judge it: a run passes without it.

The `[BuildStamp]` line in the copied log names the build that ran; quote it in the evidence. A PASS from a stale deploy proves nothing
about the change under test.

For a balance reading rather than a pass or fail, run `python tools/fords_match_review.py <run folder>` and record the result in [balance-log.md](../features/fords-of-war/balance-log.md).

## Costs and risks

- **Screen and GPU.** The game window opens on the desktop and runs for minutes (a boot plus the live time, up to the wall limit). The
  idle gate is why a person is not in the middle of something else.
- **The deploy replaces the maintainer's.** Building from a worktree changes what `Modules/TAOM` holds. Say which build you deployed in the report.
- **Logs are pruned.** The launcher copies them out; the folder under `E:/Temp/fords-runs` is the record.
- **A crash dump is tried, not promised.** On a crash the launcher runs `procdump -accepteula -ma <pid>` into `crash.dmp` in the run folder
  (about 60 s, 180 s limit) before it kills the game, and says in `runner_log.txt` when procdump is missing, times out or leaves no dump. A dump counts as written when `crash.dmp` exists and is not empty, because procdump v12.01 exits 0xFFFFFFFE for success and for a missing pid alike; its own output is in `procdump.log` beside it. The Watchdog
  may block the attach (`native-crash-triage` says so), and only a real crash settles it. A value of `--keep-on-crash` above 0 waits for a person to answer the Watchdog dialog first.
- **A killed game leaves `safely_exited = 0`.** The launcher resets it; if a session aborts the launcher by hand, check the value before the next start.
- **One game at a time.** The preflight refuses while a game runs, including the maintainer's.

## What ArmorySim taught

ArmorySim 0.2 is the maintainer's standalone tool and stays standalone: nothing was integrated, only the techniques were read.

| Technique | ArmorySim | TAOM launcher and driver |
|---|---|---|
| Launch | `Bannerlord.exe` directly with `/singleplayer _MODULES_*...*_MODULES_`, no shell, from `bin/Win64_Shipping_Client` | Same; the module list is the F5 profile's |
| Arming | Environment variables name an arm file and an output folder | One variable, `TAOM_FORDS_AUTORUN`, naming the arm file; the folder is the arm file's `out` |
| Menu detection | Polls `GameStateManager` for `InitialState`, acts after 4 s | Same idea: `ActiveState is InitialState` with no `Game.Current`, settled for 4 s, polled from `ClientSubModule.OnApplicationTick` |
| Inquiries | Hides them while working | `HideAnyInquiry` while waiting at the menu |
| Ending | Done file, then `Utilities.QuitGame` | Done file, then `Utilities.QuitGame` when `quit=1` |
| Safe Mode window | Resets `safely_exited` in `engine_config.txt` | Same, before the start and after any kill |
| Shader cache | Guards the module list in `shader_mapping.bin` (its probe module changes the list) | Reads and logs it; the list is the F5 list, so it stays valid, and the notice is answered if it appears |
| Crash detection | Scans `rgl_log_<pid>` and `watchdog_log_<pid>` | Same markers, plus the TAOM log's `[CrashReport]` block |
| Stopping | Kills its own process tree after a creation-time check | Kills the process it started through its handle, plus a `-p <pid>` watchdog |

## Scope today and what is planned

Today the in-game driver plays only a Fords of War match, so `/game-run` serves Fords checks and any change a Fords match exercises
(mission start, agents, formations, destructibles). A general scenario driver, one that arms other in-game checks the same way, is planned
separately and is not built; until it exists, a check outside Fords still needs a person to play it.
The Long Defeat (#1079) has its own wrapper, `tools/run_long_defeat.py`, under this same permission and gate. It reuses this runner's
preflight, idle gate, launch, dialog handling and crash scan, arms the game with `TAOM_LONGDEFEAT_AUTORUN`, and presses Cancel on the
Helm's Deep terrain shader dialog. `/game-run` names it for changes under `LongDefeat`. Exit 0 needs the `[Check]` lines of the plan, so
read the run folder before rerunning.
