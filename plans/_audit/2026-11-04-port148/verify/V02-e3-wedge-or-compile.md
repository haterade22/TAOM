# V02: E3 Harad first-battle freeze, wedge or compile? (adversarial verification)

Run 2026-11-04-port148. Read-only. Refs: working tree c5b84fb41 (equals origin/bannerlord-1.4.5 and the public
build for every BattleLoadDiagnostics file; none is in SP\diff_localHEAD_vs_origin145.txt and the three public-only
commits do not touch them), public build data at 8df90444c, 1.5.x at origin/bannerlord-1.5.x. Engine: installed
v1.4.8 via `tools/taom-src.ps1` (cache `C:\Users\mikew\.taom-src\v1.4.8\`) and the dump
`C:\Decompiled_Bannerlord\`. E3 = `taom_debug_2026-11-01_20-00-00.log`, E4 = `taom_debug_2026-11-01_20-16-56.log`
(scratchpad evidence copies; line numbers are file lines).

## Verdict

**UNVERIFIABLE.** Neither "E3 is most likely a slow cold compile the player killed" (loading-hang, freeze-threading,
runtime-perf) nor "E3 is a wedge like #612" is established by the evidence in hand. The deciding fact: in E4 the same
player's 1 Hz render-wait stream went silent for 68.3 s during a load that then completed (shaders 1569 to 0, then
BattlePlayable). Silence alone cannot tell a compile from a wedge. E3's own duration fits a compile at E4's rate, the
#612 cure (Warg to horse) does not transfer to E3 (no TAOM creature mount is evidenced in E3), and E4 ends in a
second, unrecorded stall of the same shape with only 806 pending shaders. The questions that would decide it
(rgl_log compile activity during the silence, a hang dump, the hero's mount) have no answer in these logs.

## 1. Cadence of the stream, and E4's 68 s silence

How the line is produced (working tree):
- A Harmony postfix on `MissionState.OnTick`
  (`Main/Features/BattleLoadDiagnostics/Hooks/MissionState_OnTick_RenderWait_Patch.cs:52-76`) runs only while
  `FirstMissionTickAfterLoading` is true, the loading window is open and the mission is `Continuing` (:58-64). It
  reads `Utilities.GetNumberOfShaderCompilationsInProgress()` on the main thread and publishes it (:67-73).
- `BattleLoadDiagnosticsService.NoteWaitingForRender` emits on the first such frame at least 1000 ms after the
  previous line (`BattleLoadDiagnosticsService.cs:37`, :285-301, :306-307). A gap of N seconds therefore means no
  `MissionState.OnTick` completed for about N seconds.
- `[MemSample]` comes from a thread-pool `Timer` (`MemoryPressureSampler.cs:38-54`), so it keeps writing while the
  game thread is stopped. It proves the process and thread pool were alive, nothing about the main thread.

E3 (field battle, `battle_terrain_031`, 220 v 79 Gondor Soldiers, line 178):
- line 217 `FinishMissionLoadingDone` +7909 ms; line 218 `WaitingForRender waitedMs=1 shaders=2720`; line 219
  `waitedMs=2259 shaders=2697` (already 1.26 s late against the 1 Hz cadence); then lines 220-223 are `[MemSample]`
  only (20:02:52 to 20:04:22), `heapMB=466` flat, privMB 9590 to 9681, availPhysMB 2773 to 2847, memLoad 82-83%.
- Silence: 92 s from the last sample (20:02:50) to the last line. The next `[MemSample]` was due about 20:04:52, so
  the process ended, or the copy was taken, between 92 and 122 s after the last sample (96 to 126 s after the window
  opened at Mission.Initialize, line 183). "Killed" is an inference; the log does not say how it ended.

E4 tournament 1 (`TournamentFight`, `arena_aserai_a`, 559 agents; line 381):
- line 2124 `FinishMissionLoadingDone` +88358 ms; line 2125 `WaitingForRender waitedMs=1 shaders=1569` (20:21:20).
- lines 2126-2128: three `[TableauDiag]` lines (an elf facegen tableau spawn); lines 2129-2130: `[MemSample]` at
  20:21:49 and 20:22:19. Nothing from the render-wait stream.
- line 2131 `waitedMs=68290 shaders=0` (20:22:29); line 2151 `waitedMs=69297 shaders=0`; line 2153 `BattlePlayable
  agents=559`; the mission then played until 20:35:32 (line 2300).
- So yes: the stream was silent for 68.3 s across a load that then completed, with the count falling from 1569 to 0.
  **Silence alone cannot discriminate a slow compile from a wedge.**

E4 tournament 2 (`TournamentFight`, `arena_empire_a`, "Methir"; line 3420), not recorded in brief.md:
- line 5137 `FinishMissionLoadingDone`, line 5138 `WaitingForRender waitedMs=1 shaders=806` (20:48:25), line 5139 a
  `[TableauDiag]` census (20:48:26), then lines 5158-5164 are `[MemSample]` only until 20:51:51. No BattlePlayable.
  Silent at least 206 s. Its 534 built agents (`AgentBuildDone` lines 3420-5137) are all `monster='human'` audience
  (`townswoman_gondor` 120, `townsman_gondor` 103, and seven more `*_gondor` kinds).
- brief.md item 6 calls this a slow town load. The +111326 ms counts from `EncounterStart` (line 3390) and includes
  20:46:39 to 20:48:15 in the inventory screen (lines 3391, 3418); the mission load itself ran +107514 to +111326 ms,
  about 3.8 s. The real event is the unresolved render-wait stall.

Rate arithmetic (all from the logs above): tournament 1 cleared 1569 in at most 68.3 s, so at least 23/s. At that rate
E3's 2697 needs about 117 s, inside the 92 to 122 s window E3 had: no discrimination. Tournament 2's 806 would need
about 35 s, but it stayed silent 206 s or more (about 6 times longer); the loading-hang lens computed the same (3.9/s
or less, or a wedge). During it sysCommit rose 24700 to 26625 MB and availPhysMB fell to 1929 while privMB stayed
10748 to 10826 (lines 5161-5163), so another process took the commit. Bundle b18f3441 (2026-09-04, public 1.4.8)
shows how slow a real compile can be: 818 `compile_shader` lines in a 305 s stall
(`docs/features/shader-precompilation.md:5`, `MissionState_OnTick_RenderWait_Patch.cs:10-13`).

## 2. What a 1.4.x log can and cannot tell

Engine order, verified on the installed v1.4.8 (taom-src):
- `MissionState.cs:110`: `TickMission` runs only when `Handler.RenderIsReady()` (MissionScreen ->
  `SceneView.ReadyToRender()`); `:208` clears `FirstMissionTickAfterLoading` at the end of the first `TickMission`;
  `:215` calls `Mission.OnTick` (every behavior's `OnMissionTick`) only when `_missionTickCount > 2`, i.e. from the
  4th `TickMission`; `:340` two warm-up `Mission.Tick(0.001f)` and `:349` `Scene.ResumeLoadingRenderings()` close
  `FinishMissionLoading`.
- `BattleLoadPhaseBehavior.OnMissionTick` logs BattlePlayable, closes the window and clears the inflight marker
  (`BattleLoadPhaseBehavior.cs:22-38`): that is the 4th `TickMission`.
- `MissionScreen.cs:163` `_loadingScreenFramesLeft = 15`; `:4011` `BeforeMissionTick` (called from `TickMission`)
  sets `_isRenderingStarted` (:4051) and after the 15-frame countdown calls `OnSceneRenderingStarted` (:4065), which
  drops the loading window (:771) and calls every view's `OnSceneRenderingStarted` (:775).
- Consequence: TAOM logs nothing on `TickMission` 1 to 3. "Last line WaitingForRender, no BattlePlayable" means the
  main thread stopped either inside a render-wait frame (gate closed) or within the first three `TickMission`s. The
  log cannot say which.

The 1.5.x commits: 89374fc4f (frozen count when the stream went silent 30 s or more before the watchdog line,
`RENDER_WAIT_SILENCE_THRESHOLD_S = 30` at `tools/triage_battle_load.py:231` on 1.5.x), 0b74cc2dd (lesson outcome:
Warg to horse), 1581d4d0a (only this load's watchdog, end-of-log anchor, `stuck_at_zero`). The tool's current behavior
also depends on three commits whose subjects hide it: d99398f12 (a post-load freeze check: flat-heapMB `[MemSample]`
tail after the last BattlePlayable), b9eabd712 and 76a395729 (the end-of-log silence anchor). bdf7d515c changes a
string to `[PatchApply] Patch43_BattleLoadDiagnostics FAILED`, but 1.4.x still logs `Patch43 diagnostics failed to
apply` (`Main/SubModule.cs:1675`).

Tool runs this session (read-only, stdout only; the 1.5.x version executed from `git show` in memory):
- 1.4.x `tools/triage_battle_load.py` on E3: "a COLD SHADER CACHE, i.e. a slow load rather than a hang ... DRAINING"
  (its first-vs-last rule, `tools/triage_battle_load.py:947`). On E4 it says the same of the 806 load, while its
  own detail line says a frozen non-zero count is a wedge.
- 1.5.x tool on E3: "the 1 Hz render-wait stream went silent 92s before the log ended. The log alone cannot separate
  a slow cold compile from a main-thread wedge." On E4: same wording, 206 s.

A 1.4.x log CAN tell:
- the last phase reached, and whether BattlePlayable came before the silence (pre- or post-render-gate);
- whether render-wait frames were running (about one line a second) and the pending count at each frame;
- that the process and thread pool lived on (`[MemSample]`), with memory state;
- that no exception was captured at the native boundary: `CrashReportService` logs every capture, throttled or not
  (`CrashReportService.cs:107-128`);
- that the previous session's load never reached its first mission tick, and its scene (stale marker,
  `BattleLoadStallMarker.cs:74-97`);
- after 300 s, a `WATCHDOG STILL LOADING` (or `DEFERRED`) line and a bundle (`BattleLoadDiagnosticsSettings.cs:29-36`).

A 1.4.x log CANNOT tell:
- what the main thread is doing: no stack capture in the load window; `ExitStallSampler` arms only for mission exit
  (`ExitStallSampler.cs:84-99`);
- whether compiles continue during a silence: the count is read and published only on the main thread
  (`BattleLoadRenderWaitProbe.cs:16-19`, :43-63), so a blocked main thread freezes the watchdog's input;
- hence the watchdog does not discriminate either. After 300 s `Decide` sees a count unchanged for 60 s or more and
  returns `FireWedge` (`BattleLoadStallWatchdog.cs:115-127`, :162-165) for a main-thread-blocking compile and a wedge
  alike. Its `Defer` branch works only while frames run. It fires at 300 to 305 s (5 s poll, :22), not "300 s plus
  60 s": the 60 s rule is already met when the threshold arrives;
- what anyone rode: there is no mount census before the gate (MissionDiag's action-set census runs in the first
  mission ticks);
- whether an earlier stalled load recovered: the marker is cleared on recovery or consumed by the next session
  (`BattleLoadStallMarker.cs:68-97`), and E4 prints no stale warning for E3's marker (E4 line 16 onward), which fits
  an intermediate session or a late recovery equally;
- which `rgl_log_<pid>.txt` belongs to the session: TAOM logs no process id (none in E3 or E4).

## 3. The wedge hypotheses

(a) **Skeleton-buffer pool overflow: refuted for E3 and for E4's last load.**
- In a field battle no agent exists before the gate: E4's field battle logged `BattlePlayable agents=0` (line 2862),
  and behaviors (spawn logic included) tick only from the 4th `TickMission` (`MissionState.cs:215`). E3 froze before
  BattlePlayable, so no troop or mount skeleton existed to draw.
- Even after spawning: 299 humans plus at most about 80 Harad mounts plus Gondor horses is under 500 skeletons; the
  1.5.x doc puts pool 1 at about 2,340 human skeletons (65,536 entries, 28 per human; pool 2 is 262,144 entries with
  one reservation per drawn skeleton, horses and wargs included). E4's last load had 534 humans and no mounts.
- The pools' presence in v1.4.8's native code is UNVERIFIED (the doc's RVAs are v1.5.4), and not needed here.

(b) **Creature-mount spins: no evidence in E3.**
- E3's roster (the 220 are listed in E4 lines 161-178 and E4 line 283; E4 loaded the `save003` E3 wrote at lines
  163-164): mounted lines in the public build's `troops_harad.xml` (8df90444c) are `harad_camelscout` (`Item.camel`,
  `Item.camel_saddle`) and `harad_sandblade`, `harad_horsearcher`, `harad_serpentguard`, `harad_serpenthorsearcher`,
  `harad_noble` (`Item.t2_khuzait_horse`, `Item.steppe_half_barding`). No mumak, elephant, warg, spider or ram.
- The hero `lord_3_3` (Marzuk, `lords.xslt:9690-9694` at 8df90444c) uses `harad_bat_template_medium_c`
  (`equipmentsets/taom_equipment_sets_harad.xml:33`): `Item.charger` and `Item.chain_horse_harness`. The player may
  have re-equipped him; E4 shows many inventory visits. UNVERIFIED what he rode in E3.
- `camel`, `camel_saddle`, `t2_khuzait_horse` and `charger` are vanilla `SandBoxCore/ModuleData/items/horses_and_others.xml`
  items; no installed Armory, TAOM_Map or TAOM XSLT, and no XSLT at 8df90444c, touches them.
- E4's field battle with the same party: action sets `as_human_warrior`, `as_horse`, `as_camel` only (lines
  2976-2978), 0 creature trees attached or late-attached (lines 2965-2979, 2993-2996).
- No mount agent exists before the gate, so mount attach, IK, action sets and creature trees cannot run in E3's
  window. TAOM's only `OnPreMissionTick` override returns at once without a mounted main agent
  (`AutonomousMovementPlayerController.cs:190-193`); TAOM has no `OnRenderingStarted`/`OnSceneRenderingStarted`
  overrides.
- The one way a mount touches the window is its ITEM in the preload: `MissionPreloadView` preloads all 12 equipment
  slots of every troop of every involved party (`SandBox__SandBox.View.cs:4946-4972`), registering mount meshes
  (`Native__TaleWorlds.MountAndBlade.View.cs:3433-3438`) and calling `PreloadShaders` per unique metamesh
  (:3486-3497). It runs on the first `OnPreMissionTick` (native `Mission.OnPreTick`, `Mission.cs:3530-3535`).
  Whether that is the warm-up `Mission.Tick(0.001f)` or `TickMission` 1 is native and UNVERIFIED; TAOM's docs say
  "before the first frame" (`docs/features/shader-precompilation.md:33`, `TaomShaderGameManager.cs:18`).

(c) **Managed exception swallowed in the render-ready path: excluded for E3.** A per-frame exception in
`MissionState.OnTick` would stop the postfix while frames run, but every capture is logged
(`CrashReportService.cs:107-128`) and E3 has no `[CrashReport]` line after startup (line 4 shows capture enabled).
The window closes only at `BattleLoadPhaseBehavior.cs:36` and :44 (BattlePlayable, mission end), neither of which
E3 logged, so the silence is not a closed window.

(d) **Other evidence.**
- Missing collision body (#352, #599, #633 class): `PreloadHelper.WaitForMeshesToBeLoaded` is an unbounded
  `Thread.Sleep(1)` loop (taom-src `TaleWorlds.MountAndBlade.View.PreloadHelper.cs:58-74`) called from
  `OnSceneRenderingStarted`, i.e. about 11 frames AFTER BattlePlayable. Real on v1.4.8, fits the generic "frozen,
  sounds continue, Task Manager normal" report, does not fit E3. The loading-hang lens says so (LOADING-HANG-02);
  xml-data XML-DATA-01 says it "matches the shape of E3", which the engine order refutes.
- Async-tick handshake: `TickAgentsAndTeamsImp` sets `tickCompleted = true` with no try/finally (`Mission.cs:3613`),
  and the next `OnPreTick` spins in `WaitTickCompletion` (:3585-3587) forever if it was skipped. A mid-battle freeze
  mechanism (it needs `Mission.OnTick`, :3740), not E3.
- The same scene failed twice: E3 line 17 reports the 19:34 session's `battle_terrain_031` load never reached
  playable (marker 22:52:21Z). Only the scene is recorded, not the fight.
- Correlation, a LEAD only: the three unresolved stalls preload Gondor content (E3 and its 19:34 retry vs Gondor
  Soldiers; tournament 2 in a Gondor town), and the two completed loads are Harad (tournament 1, the biome_107 field
  battle at `shaders=1`). The public build re-pointed the Gondor tree to new Armory ids (92a30ba42, 3849 lines of
  `troops_gondor.xml`). A cold cache for new Gondor art explains it as well as a broken asset does.
- The #612 outcome is a player-reported cure, not a proven mechanism (the lesson itself says the log cannot separate
  the two). The bundle suffix `b18f3441` is the crash signature (`taom_crash_<timestamp>_<sig>.zip`,
  `CrashReportSettings.cs:38`), so the 2026-09-04 slow compile and the 2026-10-18 wedge need not be one player.
- Memory: 16 GB machine (E3 line 20), privMB about 9.6 GB against wsMB about 6.3 GB, memLoad 82-83%; paging can slow
  a compile by an unknown amount.

## 4. What a player can do today (only what the evidence supports)

1. Read the end of `taom_debug_*.log`. Last BattleLoad line `WaitingForRender` and no BattlePlayable: the game is
   stuck on the loading screen, compiling or wedged. BattlePlayable present and the game froze as the loading
   screen dropped: suspect a missing collision body; `rgl_log_errors_<pid>.txt` names it as `get_object failed for
   body:` (string present in the installed v1.4.8 `TaleWorlds.Native.dll` near byte 11434880).
2. To tell compile from wedge while stuck: open the newest `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_<pid>.txt`.
   New `compile_shader:` lines with fresh timestamps mean it is compiling, so wait. If the timestamps stop for several
   minutes, it is not compiling. The format string is in the v1.4.8 native DLL (byte 11431504, 0xAE6E50); E1/E2
   (another player, warm cache) contain none, so their absence is normal on a warm cache.
3. Wait at least 5 minutes on a first battle after a game update or any mod-list change (v1.4.8 deletes the runtime
   shader cache when the active mods change: string in the native DLL near byte 11481325). Observed compile stalls run
   68 s (E4) to about 305 s (b18f3441).
4. For a report: lower MCM "TAOM Battle Load Diagnostics > Stall Threshold (seconds)" (10 to 600, default 300) to
   about 120 so the watchdog writes a bundle before giving up. Its `STILL LOADING` wording does not mean "wedged".
   Better still, while frozen: Task Manager > Details > Bannerlord.exe > Create dump file, and send it with the
   taom_debug and rgl logs.
5. If the hero rides a TAOM creature mount (Warg, mumak and so on), swapping it for a horse is a cheap test backed by
   one report (#612). The E3 player's evidence shows no creature mount, so do not present it to them as the fix.
6. Do not expect battle size, corpse or ragdoll settings to help a stall before BattlePlayable: no agent exists yet,
   and the preload covers every troop in every involved party whatever the battle size
   (`SandBox__SandBox.View.cs:4955-4967`).
7. Optional warm-up: the main-menu "Pre-compile Shaders" walk (#560) prepays character shaders (20 to 70 min). Its
   cold run on 1.4.8 is still owed. If the first battle still freezes after a full walk, a cold compile is unlikely.

## 5. Diagnostics and guards for the plans

1. Port the triage tool's 1.5.x END STATE (not the five commits one by one), keep the 1.4.x Patch43 string, add E3
   and E4 as fixtures, and add two rules: report whether BattlePlayable preceded the silence, and when the same log
   holds a completed blocked render wait (E4: 68 s for 1569), print the stalled load's silence against that rate as a
   labelled heuristic. Never word silence as "wedge".
2. A render-wait stall sampler: reuse `ExitStallSampler`'s capture (or f75288ebf's `ThreadStackCapture`) on the load
   window. Heartbeat = last `NoteWaitingForRender` or `NoteLoadingPoll`; when older than 10 s, log `[RenderWaitStall]`
   at +10/+20/+40/+80/+160 s with the main thread's managed stack (empty stack = native), the last count and its age.
   f75288ebf's Patch91 probes `Mission.TickAgentsAndTeamsImp` and `MissionState.TickMissionAux`
   (`Patch91_MissionTickStallProbes.cs:15`, :26), so it is blind here.
3. An off-main-thread compile signal: tail the session's rgl_log from a background thread and count `compile_shader:`
   lines per interval, the 1.5.x ShaderCompileNotice method (its doc: read every 300 ms on its own thread). Never call
   `GetNumberOfShaderCompilationsInProgress` off the main thread.
4. Feed (3) to the watchdog: when the heartbeat is stale, print "main thread silent N s, rgl compiles in last 60 s:
   K" instead of a count frozen by the blocked thread, and fire as a wedge only when K stays 0.
5. A "recovered" line when the stream resumes after a gap of 10 s or more (E4's 68 s would have been measured).
6. A mount and creature census at `MissionOpenNew`: the main hero's Horse and HorseHarness ids and Monster, distinct
   mount items per side with counts, any non-vanilla Monster flagged.
7. Preload timing markers on the first `MissionPreloadView`/`ArenaPreloadView.OnPreMissionTick` and on
   `PreloadHelper.PreloadCharacters`, plus one line on the first `TickMission`, to close the `TickMission` 1 to 3
   blind window and settle where the preload runs.
8. Log the process id and the resolved rgl path at start, and fix `LogTailCollector.FindLatestRglLog`
   (`Main/Features/CrashReport/Collectors/LogTailCollector.cs:65-70`; same at :81 on 1.5.x): `rgl_log_*.txt` also
   matches `rgl_log_errors_*.txt`, so a stall bundle can carry the errors log instead of the full one.
9. Carry the last phase, last count, heartbeat age and hero mount in the inflight marker.
10. Port Patch90 PreloadBodyGuard (fb8d73145) for the post-BattlePlayable class; it does not touch E3's shape.
11. A player-facing notice that can draw while the main thread is blocked (WinForms on its own STA thread, as the
    1.5.x boot notice does; its host lives in `Client/` on 1.5.x, so 1.4.x needs a decision).

## Corrections to the lenses and briefs

- brief2: "the E3 player killed the game at about 92 s" is an inference; the process ended, or the log was copied,
  92 to 122 s after the last sample. "300 s plus 60 s unchanged" is wrong: the watchdog fires at 300 to 305 s.
- brief2: "It was a wedge, not a slow compile" (#612) rests on a mount-swap cure, not on rgl or dump evidence.
- brief.md item 6: E4's last load was not slow (about 3.8 s of loading); it never reached playable (206 s silent).
- loading-hang LOADING-HANG-01 title ("players kill a working load"): not established for E3; its body is properly
  hedged. freeze-threading FREEZE-THREADING-02 and runtime-perf RUNTIME-PERF-01 lean compile without weighing #612 or
  E4's last load.
- xml-data XML-DATA-01: the missing-body wait cannot produce E3's shape (it starts about 11 frames after BattlePlayable).
- All lenses: E4's stream silence during a completed load is the counter-example to any "silence means X" rule.

## Open questions

- What did Marzuk ride in the frozen battle, and did the player change it before the 20:40 battle that loaded?
- Was there a session between 20:04 and 20:16 (it would hold the stale warning for E3's marker), or did E3 recover?
- The player's `rgl_log_<pid>.txt` for 20:00-20:05 and 20:48-20:52: do `compile_shader` lines continue in the silence?
- Does the preload run in the warm-up tick or on `TickMission` 1 (native)?
- Does a Gondor-content load on a cold cache with the public Armory complete?

## Recommended skills (not run here)

`/investigate` for E3 once the rgl log or a dump arrives; `/native-crash-triage` (Phase 2b, hang dump);
`/research` on preload timing; `/issue` for the triage port, the rgl-selection bug and the sampler.
