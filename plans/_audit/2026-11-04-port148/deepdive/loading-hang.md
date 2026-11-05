# Deep-dive: infinite loading screens and first-battle stalls (1.4.x line on Bannerlord v1.4.8)

Run id: 2026-11-04-port148. Lens: loading hangs and first-battle stalls (shaders, missing bodies and meshes,
corpses, ragdoll). Read-only investigation. Refs: working tree = c5b84fb41 (local bannerlord-1.4.5), drifted files
read from origin/bannerlord-1.4.5 (1b00d1acd), leads and fixes from origin/bannerlord-1.5.x (bf3299143). Engine
behaviour from the v1.4.8 dump at C:\Decompiled_Bannerlord and taom-src on the installed v1.4.8 DLLs.

Findings are appended as they are confirmed. A summary section closes the file.

## Findings (appended in investigation order; ranked in the summary)

### LOADING-HANG-01: the first-battle "freeze" is a cold shader compile that blocks the main thread past FinishMissionLoading; players kill a working load and TAOM shows and logs nothing

**Severity P0 for player impact. Confidence: high on the mechanism, medium on "finite, not wedged".**

Evidence (player logs are E3 and E4 from the brief; line numbers are in those files):
- E3 lines 217-223: `FinishMissionLoadingDone` at +7909 ms, `WaitingForRender waitedMs=1 shaders=2720`, then
  `waitedMs=2259 shaders=2697`, then only `[MemSample]` lines every 30 s until the log ends at 20:04:22. The
  loading window opened at `MissionInitialize` 20:02:46, so the player waited about 96 s before killing it.
- E4 lines 2124-2153 (first tournament, `arena_aserai_a`, 559 agents): `WaitingForRender waitedMs=1
  shaders=1569` at 20:21:20, the NEXT marker is `waitedMs=68290 shaders=0` at 20:22:29, then
  `waitedMs=69297`, then `BattlePlayable` at +157939. The load finished after 68 s.
- E4 lines 5137-5164 (second tournament, `arena_empire_a`): `WaitingForRender waitedMs=1 shaders=806` at
  20:48:25, then only `[MemSample]` until the log ends at 20:51:51 (206 s). Killed again.
- E4 lines 2859-2862 (field battle `battle_terrain_biome_107`, 224 v 64): `shaders=1`, `BattlePlayable` 0.7 s
  later. A warm load on the same machine in the same session.
- The marker is a postfix on every `MissionState.OnTick` frame
  (`Main/Features/BattleLoadDiagnostics/Hooks/MissionState_OnTick_RenderWait_Patch.cs:53-76`, working tree)
  throttled to one line per second (`BattleLoadDiagnosticsService.cs:37` `RenderWaitEmitIntervalMs = 1000`,
  `:285-301`). On v1.4.8 `Module.OnApplicationTick` ticks the game state every frame with no loading-window
  gate (`C:\Decompiled_Bannerlord\_categories_v1.4.8\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Module.cs:463-527`).
  So a 68 s gap between markers means no managed frame ran for 68 s: the main thread was blocked, not merely
  skipping `TickMission`. The 1.5.x doc `docs/features/shader-compile-notice.md` (origin/bannerlord-1.5.x)
  says the same of the startup compile: "The engine's main thread is blocked while it compiles".
- Engine gate (v1.4.8 dump): `MissionState.cs:110-113` ticks the mission only when `Handler.RenderIsReady()`;
  `MissionScreen.RenderIsReady` returns `SceneView.ReadyToRender()`
  (`_modules_build\Native__TaleWorlds.MountAndBlade.View.cs:15964-15970`, `:16585-16588`);
  `FinishMissionLoading` ends with `Scene.ResumeLoadingRenderings()` (`MissionState.cs:349`). The loading
  window closes only later, in `MissionScreen.OnFrameTick`, after ReadyToRender plus 15 frames,
  `AreViewsReady()` and `!MBAnimation.IsAnyAnimationLoadingFromDisk()` (View.cs:16468-16488; the close is
  `OnSceneRenderingStarted`, :13189-13197).
- The scenes are not the source: `battle_terrain_031` (SandBoxCore, 923,160-byte
  `compressed_shader_cache.sack`), `arena_aserai_a` and `arena_empire_a` (SandBox) and
  `battle_terrain_biome_107` (SandBoxCore) all ship their own v1.4.8 sacks (installed game, read-only). The
  pending shaders are character, equipment and prop materials.
- Compile rates seen on 1.4.8: E4 tournament 1 drained 1569 in at most 68.3 s (23/s or more); bundle b18f3441
  (2026-09-04, public 1.4.8) compiled 818 in 290 s (2.8/s; `docs/features/shader-precompilation.md` park
  history, working tree); E3 drained 23 in 2.26 s (about 10/s), so its 2697 needed about 4.5 min at that rate
  and 16 min at the b18f3441 rate. The 2026-10-18 public-1.4.8 bundle showed the E3 shape exactly: 3128 then
  3066, then about 290 s of silence (lesson added by 89374fc4f, origin/bannerlord-1.5.x
  `docs/reviews/lessons/state-lifecycle-save.md`).
- A warm cache compiles nothing: E2 (another player, 4.5 h, about 20 battles) has zero `compile_shader` lines
  and only `Missing shader from sack: pbr_terrain*` misses (3086 + 978 + 26 + 21 lines).
- v1.4.8 native strings (installed `TaleWorlds.Native.dll`, 14,185,944 bytes, read-only): "A change in your
  active mods was detected. Deleting the runtime shader cache to avoid rendering issues." (offset 0xaf30f0),
  `compile_shader: %s, %s, %s, %d, %d, %d, %d.` (0xae6e50), `SHADER COMPILATION IN PROGRESS` (0xae6430),
  `shader_mapping.bin`, `CoreShaders/D3D11/`, and the import name `XINPUT9_1_0.dll` with `XInputGetState`.
- The watchdog cannot see it: `StallWatchdogSeconds` defaults to 300
  (`BattleLoadDiagnosticsSettings.cs:36`); E3 was killed at about 96 s and E4 tournament 2 at about 208 s, and
  neither log holds a `WATCHDOG` line. Past the threshold it would defer anyway while the count moves
  (`BattleLoadStallWatchdog.cs:115-127`).

Mechanism: once `FinishMissionLoading` resumes scene rendering, every material and shader-flag combination
that is in neither a sack nor the local runtime cache (`C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11`)
is compiled before `SceneView.ReadyToRender()` turns true, and during that work the main thread runs no
managed frame. The loading screen looks frozen. TAOM's troop and Armory material count makes the queue
thousands long on a cold cache. The 1.4.x shader-precompilation doc records how the cache goes cold on 1.4.8:
a module-list change (v1.4.8 deletes the runtime cache; the string above), a game update, a fresh install.
E3 and E4 both reached the main menu 17 s after TAOM loaded (E3 lines 1-16, E4 lines 1-16), so neither start
paid a startup rebuild and the module list did not change between them; their misses are in-mission ones
(new troops, townsfolk, tournament participants, the player's career mount).

Can it stall forever? Not shown. E4 tournament 1 finished. E4 tournament 2 (806 shaders, silent 206 s) is
either a much slower compile (3.9/s or less) or a wedge; system commit rose from 24,700 to 26,625 MB of
31,681 and available RAM fell to 1,929 MB during it (E4 lines 5158-5164), which would slow any compile. The
game's own `privMB` stayed flat (10,779 to 10,823 MB), so another process took that commit, perhaps whatever
the player opened while waiting.
UNVERIFIED: the player's `rgl_log_<pid>.txt` (timestamped `compile_shader` lines) separates the two; a hang
dump of the render thread is the second step.

Is TAOM's probe the blocker? The postfix calls native `Utilities.GetNumberOfShaderCompilationsInProgress()` on
each render-wait frame. The b18f3441 stall of 2026-09-04 (305 s) predates the probe, and the 1.5.x notice doc
reports the main thread blocked during the startup compile, where no probe runs, so the probe is not needed
for the stall. Whether that native read contends with the compiler is UNVERIFIED; the fix below avoids engine
calls from the waiting path anyway.

A loop that makes it "every first battle": E3's stale marker (LOADING-HANG-04) shows the previous session
died on the same scene and fight. If compiled shaders persist only in batches (this laptop's cache: 341 of
364 `.sacx` files share the second 2026-08-13 19:11:53), a game killed mid-compile may lose them and the retry
pays the same compile. UNVERIFIED: kill the game during a render wait, then compare the `.sacx` count in
`CoreShaders\D3D11` before and after.

Fix direction (ranked):
1. Tell the player. A background thread, started at `FinishMissionLoadingDone` and stopped at
   `BattlePlayable`, counts `compile_shader:` lines in `%ProgramData%\Mount and Blade II Bannerlord\logs\rgl_log_<pid>.txt`
   (the 1.5.x notice's method; no engine call off the main thread) and shows "Compiling shaders: N, the game
   is not frozen" in an OS window owned by the game window. 82645b53f covers only the startup compile and its
   window lives in TAOM.Client on 1.5.x; 1.4.x has no Client project, so the window host needs a decision.
2. Log it. The same thread writes a heartbeat (rgl compile count, elapsed) to `taom_debug.log` every 10 to
   15 s during the render wait, so a killed session still says whether the queue drained. Add an early,
   bundle-free `STILL LOADING` marker at 60 s. Port 89374fc4f and 1581d4d0a (triage verdicts).
3. Prevent the module-list wipe: port ShaderCacheKeeper (372773d1a). v1.4.8 has the same wipe string and the
   same `XINPUT9_1_0.dll` import, but the v1.4.8 `shader_mapping.bin` header layout is UNVERIFIED (this
   laptop's file was written by build 77991, format word 0x5ef, not by 119303). Read a v1.4.8-written header
   on the desktop first. It does not help E3/E4, whose module list did not change.
4. Prepay: the existing Pre-compile Shaders walk (#560) covers troop battle equipment, but its cold 1.4.8 run
   is still owed and it takes 20 to 70 min. Shipping module sacks in the v1.4.8 format (#448) is the durable
   fix; this laptop's TAOM, TAOM_Map and Armory `Shaders\D3D11` folders hold no sack, only
   `shader_mapping.bin` and compile reports (Armory report: "Total variants: 13262228").

1.5.x commits: 372773d1a (keeper), 82645b53f (startup notice only), 89374fc4f and 1581d4d0a (triage). Nothing
on 1.5.x addresses the in-battle render-wait compile itself.

### LOADING-HANG-02: a missing collision body spins the main thread forever right after the loading screen drops; 1.4.x has no guard, and TAOM's diagnostics go blind to it

**Severity P1. Confidence: high on the mechanism (v1.4.8 verified), unknown on whether the players' Armory carries a trigger.**

Evidence:
- v1.4.8 dump `_modules_build\Native__TaleWorlds.MountAndBlade.View.cs:3355-3371`: `WaitForMeshesToBeLoaded`
  is `do { num = 0; foreach mesh num += CheckResources(); foreach body name num += GetFromResource(name, true)
  == null ? 1 : 0; Thread.Sleep(1); } while (num != 0);` with no exit. Body names come from every preloaded
  item's `CollisionBodyName`, `BodyName` and `HolsterBodyName` (:3457-3459), added only if not whitespace
  (:3527-3533), never checked for existence. Signatures confirmed with taom-src on the installed DLLs:
  `PhysicsShape.GetFromResource(string bodyName, bool mayReturnNull = false)`
  (`C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.Engine.PhysicsShape.cs:10`) and the private
  `HashSet<string> _uniqueDynamicPhysicsShapeName` (`...PreloadHelper.cs:14`).
- Callers on v1.4.8: `SandBox__SandBox.View.cs:4978` (`MissionPreloadView`, campaign battles and sieges;
  it preloads every troop of every involved party, :4956-4967), `:6628` (tournaments and arena practice,
  every possible participant plus the player, :6608-6622), `Native__TaleWorlds.MountAndBlade.View.cs:26761`
  (custom battle), `SandBox__SandBox.GauntletUI.cs:1575` (education screen), plus multiplayer and NavalDLC.
  All run from `OnSceneRenderingStarted`, which `MissionScreen` calls just after
  `LoadingWindow.DisableGlobalLoadingWindow()` (View.cs:13189-13197).
- Ordering against TAOM's diagnostics: the mission ticks from the first ReadyToRender frame, and
  `Mission.OnTick` from the fourth (`MissionState.cs:212-219`); `OnSceneRenderingStarted` waits for the
  15-frame countdown (View.cs:16474-16488). `BattleLoadPhaseBehavior.OnMissionTick`
  (`Main/Features/BattleLoadDiagnostics/Hooks/BattleLoadPhaseBehavior.cs:22-38`, working tree) logs
  `BattlePlayable`, closes the watchdog window and deletes the in-flight marker before the spin starts. So
  this hang leaves `BattlePlayable` as the last line, no watchdog line, no bundle and no next-session notice.
- History: two Armory `body_name` typos hung siege loads on 1.4.x (#352, 956b123a5, 2026-07-16); on 1.5.x the
  2026-09-11 art drop renamed the elven bows and every elf start hung on the Rivendell tournament (#599), and
  three Rhun longbows that borrowed the elven bow's body hung a player with every desk gate green (#633;
  origin/bannerlord-1.5.x `docs/features/preload-body-guard.md`).
- Trigger data on the 1.4.x line: only the Armory names collision bodies. The repo's `Main/_Module/ModuleData`
  has no `body_name`, nor do the installed TAOM and TAOM_Map ModuleData. On this laptop
  `tools/validate_mesh_refs.py --game <laptop game> --items <Armory ModuleData> --tpac-modules LOTRLOME_Armory
  Native SandBoxCore SandBox TAOM_Map --no-rgl-log` (read-only) found 334 unique collision bodies referenced,
  0 missing, and 3,871 of 3,874 visual meshes present (the 3 are the known-dead troll meshes). But this
  laptop's Armory is v2.0.26, while the players run the 2026-09-29 Armory publish made on the 1.5.x line
  (v2.0.29.5 in E3 line 26; 92a30ba42 body: "That Armory retired 159 item, piece and monster ids"). The
  check above therefore says nothing about the players' copy. UNVERIFIED: run the same command against the
  public channel's Armory folder on the desktop.
- E3 and E4 do not match this shape: both stall before `BattlePlayable`. This is not the Harad player's
  hang.

Mechanism: one `body_name` that no loaded `.tpac` ships, on any item any preloaded character carries, makes
`GetFromResource` return null forever and the loop never exits; the game freezes on the first frame after
the loading screen. #633 shows the leading way it reaches a player whose data passed every gate: an install
whose XML and packs come from different publishes.

Fix direction: port fb8d73145 (Patch90 prefix, bounded 5 s drain of unresolvable body names, logs
`[PreloadGuard]` at ERROR, returns true so vanilla's loop runs). Every engine member it touches exists on
v1.4.8 with the same signature (above); drop or adapt the 1.5.x `[ClientOnly]` attribute. Port the desk
gates with it: 05bd0a730 (`tools/audit_armory_refs.py`, #599), the `MISSING_COLLISION_BODY` code in
`validate_moduledata.py` (ef6afeb67, 332f2ac71) and `COLLISION_BODY_BORROWED` (#633), and run them against
each public Armory publish before release. Note the guard drains body names only; a mesh whose
`CheckResources()` never reaches zero hangs the same loop and stays unguarded on both lines.

1.5.x commits: fb8d73145 (guard), 05bd0a730, ef6afeb67, 332f2ac71 (gates).

### LOADING-HANG-03: any exception inside Mission.AfterStart becomes an infinite loading screen on 1.4.x, because TAOM's own crash capture swallows it and the engine reloads the mission every frame

**Severity P1. Confidence: high (engine path and TAOM swallow both read this session); no player log yet shows it.**

Evidence:
- v1.4.8 `Mission.AfterStart` (taom-src `C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.MountAndBlade.Mission.cs:3799-3836`)
  runs every submodule's `OnBeforeMissionBehaviorInitialize`, every behaviour's `OnBehaviorInitialize`, every
  submodule's `OnMissionBehaviorInitialize`, every `EarlyStart`, the spawn-path and deployment-plan
  initialisers, every `AfterStart`, every mission object's `AfterMissionStart`, the weather model, and only then
  `CurrentState = State.Continuing`.
- Its caller `MissionState.FinishMissionLoading` sets `_missionInitializing = false` first (dump
  `MissionState.cs:333-350`). On the next frame `OnTick` still sees `Initializing` (:89-96) and `TickLoading`
  calls `LoadMission()` again because `_missionInitializing` is false (:221-233), which re-runs
  `ClearOldResourcesAndObjects` and `Mission.Initialize()` (:235-244) and then `AfterStart`: the same throw.
- The frame survives only if something swallows the throw. On 1.4.x TAOM does:
  `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs:21-26, 36-90` attaches a finalizer to every static
  method of every `*CallbacksGenerated` type in the three AutoGenerated assemblies (E3 line 4: "attached 247
  Finalizer(s)"), and the finalizer returns `CrashReportPatchHelper.HandleAndSwallow` (:119-123), which returns
  null, "swallow, game keeps ticking" (`CrashReportPatchHelper.cs:29-53`, default `EnableCrashCapture = true`,
  `CrashReportSettings.cs:22`). The application tick enters managed code through one of those shims,
  `LibraryCallbacksGenerated.Managed_ApplicationTick` (v1.4.8 dump
  `_shipping_build\TaleWorlds.DotNet.AutoGenerated.cs:79, 243-247`). After the first bundle each repeat logs
  one ERROR line, "bundle suppressed" (`CrashReportService.cs:94-114`), so the log also grows every frame.
- TAOM code runs inside that window: `SubModule.OnMissionBehaviorInitialize` (`Main/SubModule.cs:1757-1911`)
  resolves services with `IoC.Resolve` and constructs and adds about 25 behaviours inline with no per-behaviour
  guard; E3 lines 186-216 show its `TaomBehaviorsBegin..Done` bracket nested inside
  `MissionAfterStartBegin..Done`. A throw there also skips the later `BattleLoadPhaseBehavior` registration
  (`SubModule.cs:1888-1890`), so the watchdog window never closes either. Vanilla behaviours that read TAOM
  data (cultures, troops, banners, equipment) in `AfterStart` are the other source. This is the shape behind
  the maintainer's "wrong XML data" loading screens.

Mechanism: a throw in any of the six start call groups leaves the mission in `Initializing`; TAOM's
Native2Managed finalizer swallows it; the next frame reloads the mission and throws again, for ever. Without
the swallow the throw would surface as a crash report. What a player log would show: repeating
`LoadMissionBegin` / `MissionInitialize` / `FinishMissionLoadingBegin` / `MissionAfterStartBegin` phases and a
`[CrashReport] ... occurrence #N - bundle suppressed` line per frame. Neither E3 nor E4 has it.

Fix direction: port the mission-start guard from ecaa67d26 (Patch103: a transpiler swapping the six call
sites in `Mission.AfterStart` for try/filter helpers that log the owner and assembly, show one message and let
the mission start). The six sites exist exactly once each in the v1.4.8 body (read above), but the 1.5.x
patch reuses `TickProfilerTranspiler.Rewrite` and the 1.5.x feature-module runner, neither on 1.4.x, so it is
an adaptation, not a cherry-pick. Smaller first step with most of the value: make the TAOM wiring itself
throw-proof (per-behaviour try/catch in `AddTaomBehavior`, and register `BattleLoadPhaseBehavior` in a
`finally`, which ecaa67d26 also did on 1.5.x).

1.5.x commits: ecaa67d26 (guard plus the `finally` closer).

### LOADING-HANG-04: E3's stale in-flight marker means the same fight on the same vanilla scene died twice; the scene is not special, and the marker cannot tell a slow compile from a hang

**Severity P2 (diagnostic). Confidence: high on the mechanics; the gap between E3 and E4 is unexplained.**

Evidence:
- Writer: `Mission_Initialize_BattleLoad_Patch.cs:36` calls `BattleLoadStallMarker.MarkInflight(scene)`, which
  writes `Logs/battle-load-inflight.marker` with `scene=`, `utc=` and `log=` lines
  (`BattleLoadStallMarker.cs:18, 38-59, 107-112`). E3 line 182 shows it for `battle_terrain_031`.
- Clear: the first `OnMissionTick` and `OnEndMissionInternal` (`BattleLoadPhaseBehavior.cs:36-37, 44-45`).
- Check: every main menu calls `TryConsumeStaleMarker` (`SubModule.cs:551-562`), which logs the WARNING, deletes
  the file and shows `StallReportNotifier`'s inquiry (`StallReportNotifier.cs:20-56`).
- E3 line 17: marker for `battle_terrain_031`, written 2026-11-01T22:52:21.97Z, prior log
  `taom_debug_2026-11-01_19-34-01.log`. E3 lines 177-178 then select the same scene (`mapIndex=109`) for the
  same fight (`encountered='Gondor Soldiers'`, attacker 220 v 79). The marker time falls inside the 19:34
  session only if local time is UTC-3 (an inference). So the player retried one fight and the load died in the
  same window both times.
- The scene: vanilla SandBoxCore `battle_terrain_031` with its own `compressed_shader_cache.sack` (923,160 bytes)
  and terrain header (installed game). TAOM's `Main/_Module/ModuleData/sp_battle_scenes.xml:164-174` keeps
  vanilla's entry unchanged (`map_indices="109, 49, 21, 126, 105"`, same as
  `Modules\SandBox\ModuleData\sp_battle_scenes.xml:162-171`). Scene choice follows the map position, so the
  same spot always gives the same scene. Nothing marks it as special; the load died after the scene loaded
  (LOADING-HANG-01).
- E4 never logs a stale marker, although E3 died with its marker written (E3 line 182, no `BattlePlayable`).
  Either a session between 20:04 and 20:16 consumed it (its log is not in the evidence) or E3's load finished
  after its last logged line. UNVERIFIED: ask for any `taom_debug_2026-11-01_20-0*.log`.
- The notice says "the game was closed while still loading" and asks for the log (`StallReportNotifier.cs:46-55`,
  body hard-coded English). It never says the load may have been working.

Mechanism: the marker records only "the first mission tick never came". It cannot separate a kill during a
long shader compile, a true hang and a crash. For the Harad player it recorded a kill during the render wait.

Fix direction: when the consumed marker's prior log ends in `WaitingForRender` with `shaders>0`, say so in
the notice ("the game was still compiling shaders; the first battle after an update or a mod-list change can
take several minutes") and log the last phase and shader count with the WARNING. Pair with LOADING-HANG-01's
heartbeat so the prior log holds a drain rate. Localize the body.

1.5.x commits: none change the marker; 89374fc4f and 1581d4d0a improve the triage tool that reads these logs.

### LOADING-HANG-05: E4's "111 s town load" is 107.5 s of menu time; the tournament loaded in 3.8 s, its ~534 agents are vanilla's arena audience, and per-agent logging costs well under a second

**Severity P3. Confidence: high (every number read from E4 and the code this session).**

Evidence (E4 line numbers):
- The `t=` clock starts at `EncounterStart` (line 3390, 20:46:34, entering the town). `MissionOpenNew
  mission='TournamentFight' scene='arena_empire_a'` comes at +107514 ms (line 3420, 20:48:22). From there:
  `MissionOpenNewDone` +108218, `LoadMissionBegin` +108455, `MissionInitialize` +108765,
  `FinishMissionLoadingBegin polls=9 waitMs=449` +109214, `MissionAfterStartBegin` +109594, `TaomBehaviorsDone`
  +109595, per-agent lines up to `AgentBuildDone agent#533` +110316 (line 5135), `MissionAfterStartDone`
  +110370, `FinishMissionLoadingDone` +111326 (line 5137), `WaitingForRender shaders=806` +111327, then the
  stall of LOADING-HANG-01. Mission load proper: 3.8 s. Agent build: 0.72 s.
- Same shape for the first tournament: `EncounterStart` 20:19:52 (line 352), `MissionOpenNew` +83949 (line 381),
  `FinishMissionLoadingDone` +88358 (4.4 s of load), then 68.3 s of render wait, `BattlePlayable agents=559`.
- The agents are the crowd: `Townsman`, `Trader`, `Musician`, `Townswoman` with TAOM templates
  (`townsman_gondor`, `merchant_gondor`, `musician_gondor`, `townswoman_harad`, lines 1943-2047 and 5031-5135).
  Vanilla `MissionAudienceHandler.EarlyStart` spawns them at the arena scene's audience points
  (v1.4.8 dump `_modules_build\SandBox__SandBox.View.cs:1969-2030`), constructed for tournaments with density
  `0.4 + random * 0.6` (:6839, :6863, :6899). The count is vanilla's density on vanilla arena scenes, not a TAOM
  effect. TAOM's share is the townsfolk outfits, which add Armory materials to a cold shader queue.
- Per-agent logging: three INFO lines per agent through `Emit` (`BattleLoadDiagnosticsService.cs:563-572`),
  gated on the loading window (`Mission_BuildAgent_BattleLoad_Patch.cs:41`,
  `Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:42, 86`). INFO drains synchronously with a
  `Flush()` per line on the calling thread (`Main/Core/Logging/FileLogger.cs:8-11, 84-122`). The whole
  `AfterStart`, spawning included, took 0.78 s for 1,603 log lines (seq 35 to 1638), so logging costs less
  than that, likely tens of milliseconds. Not a hang factor; it is log volume (the two tournaments' agent
  blocks, lines 418-2122 and 3455-5135, are about 3,400 of E4's 5,164 lines).

Mechanism: the brief's "town mission took 111 s to FinishMissionLoadingDone" reads the phase clock from the
encounter, not from the mission. The real load was fast; the session then froze in the render wait.

Fix direction: none needed for load time. Make the triage tool and future readers measure load from
`MissionOpenNew`, or add a `mission+Nms` token beside `t=`. Optionally move per-agent lines to DEBUG (async)
and keep one INFO summary per load (agent count, build ms).

1.5.x commits: none for this. Plan 040 (346f3e093) stamps game-load phases (LoadXML per id, patch
categories), not mission loads.

### LOADING-HANG-06: the unchecked per-frame skeleton reservation that freezes a crowded battle for good is byte-identical on v1.4.8; the 1.5.x guard's patterns all resolve here

**Severity P1 for large battles and sieges (freeze, not load). Confidence: high that the code is the same; the
trigger threshold on TAOM content is unmeasured.**

Evidence:
- 1.5.x mechanism (ecaa67d26, origin/bannerlord-1.5.x `docs/features/skeleton-buffer-guard.md`, read from
  v1.5.4): each drawn skeleton reserves entries in two per-frame pools (65,536 and 262,144 entries) with a
  locked add and no bounds check; one reservation past the end overlaps the buffer's "block ready" bytes, the
  threads waiting on them spin for ever, the battle stands still, nothing is logged and the game does not
  crash. About 2,340 skeletons in view fill pool 1 (28 entries for a vanilla human); pool 2 takes a reservation
  from every skeleton drawn, mounts included.
- v1.4.8 check this session (installed `TaleWorlds.Native.dll`, read-only byte scan of the four patterns in
  origin/bannerlord-1.5.x `Main/Features/SkeletonBuffer/SkeletonBufferSignature.cs`): `WatchPattern` 1 match
  (file offset 0x87a81), `Pool2Pattern` 1 (0x87875), pool-1 `GuardPattern` 1 (0x87ab4), `Pool2GuardPattern` 1
  (0x88cf0), all inside `.text` (raw 0x400 to 0xa2ba00, RVA = offset + 0xC00). The pool-2 link check also
  passes: a call 0x29 bytes after the pool-2 load (0x8789e) targets 0x88cd0, and the site is 0x20 into it, the
  same two distances the 1.5.x code records for v1.5.4. The structure offsets in the patterns (0x9D0, 0xC28,
  0x250, 0x128) match too, so the pool layout is the same.
- TAOM adds skeletons: every horse, warg, spider and creature mount fills pool 2; the 1.5.x doc adds that a mumak
  brings eight crew (crew is 1.5.3 content; the 1.4.x public build has no howdah crew, 92a30ba42 body).
- Not E3 or E4: 299 and 559 agents are far below the limit.

Mechanism: a battle with enough skeletons in view (large battle size, sieges with melee near the camera, many
mounts) overflows a pool in one frame and the render-side threads deadlock. It can happen on the first frames
after the loading screen of a big battle, which reads as a load hang, or mid-battle, which matches the
"suddenly freezes and stays that way" report shape. Audio runs on its own thread, so already-playing sounds can
continue (an inference, not measured).

Fix direction: port the guard and watch from ecaa67d26 (`Main/Features/SkeletonBuffer/*`, byte-verified 13-byte
in-memory rewrite at the first main menu, refusing to write unless each pattern matches exactly once and the
site bytes equal the engine's). The patterns need no change for v1.4.8 by the scan above; the module and MCM
wiring must be adapted to 1.4.x (no feature-module runner). The watch alone (peak fill per mission in the log)
is a low-risk first step that would confirm or clear this mechanism from player logs.

1.5.x commits: ecaa67d26 (guard and watch).

### LOADING-HANG-07: corpses and ragdolls are a mid-battle frame cost on 1.4.8, not a load-hang path; the per-battle corpse cap ports cleanly

**Severity P2 (freeze lens). Confidence: high that no load path exists (nothing dies during a load); the native
corpse figures are from v1.5.3 and unverified on v1.4.8.**

Evidence:
- c17541f2c (origin/bannerlord-1.5.x, #701): players on 1.4.8 and 2.0.x reported mid-battle freezes that
  stopped when they lowered Number of Ragdolls and Number of Corpses. The fix calls
  `Mission.SetMissionCorpseFadeOutTimeInSeconds(60)` and `Mission.SetOverrideCorpseCount(min(25, player cap))`
  once the battle runs (`Hooks/BattleCorpseMissionBehavior.cs`), offers to lower the two options at the main
  menu, and logs one `[BattleSettings]` line per battle.
- Both setters exist on v1.4.8 and only forward to native (taom-src
  `C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.MountAndBlade.Mission.cs:1723-1731`). The native facts the 1.5.x
  doc relies on (3,600 s default fade, the cap table 0/25/75/125/250/1021, the per-frame corpse-list walk in
  the mission tick, the reset per mission) were read from the v1.5.3 `TaleWorlds.Native.dll`
  (origin/bannerlord-1.5.x `docs/features/battle-corpses.md`). UNVERIFIED on v1.4.8: the 1.5.x doc names the
  native functions, so `tools/native_decompile.py` on the v1.4.8 DLL (or a pattern scan as in LOADING-HANG-06)
  would confirm them.
- 1.4.x already has part of this: `Main/Features/MountDespawn/` fades killed mounts after 5 s (on in E3's
  behaviour list, line 200), verified against v1.4.8 in `docs/features/mount-despawn.md`. Troop corpses are left
  to the player's option, so a player on Unlimited keeps up to the native cap.
- Ragdoll cost on custom rigs: the 1.5.x doc gives elephant 60 ragdoll bodies, the mumak that rig at 3x scale,
  spider 62, against a human's 26 (Armory skeletons on 1.5.x; the public 1.4.8 build ships the 2026-09-29
  Armory publish, so likely the same rigs; UNVERIFIED on the players' copy). 4e4a0c9e7 (2026-08-31, already on
  1.4.x) restored the warg's 49 typed bodies and 48 ragdoll constraints in the Armory tpac; it is an asset fix
  in an unversioned module, so a reinstall from an older Armory would undo it. v1.4.8's own changelog row
  "Fixed ragdoll jittering" and the rein fix touch the native path that TAOM's rideable creatures without
  rein attributes feed (`docs/migration/v1.4.8-impact.md:67`, still UNVERIFIED in game).
- Load path: no agent dies before `BattlePlayable`, so neither corpses nor ragdolls exist during a load. The
  maintainer's "infinite loading screen" cases blamed on ragdoll and corpses are most likely mid-battle freezes
  (or the skeleton-pool overflow of LOADING-HANG-06, which mounts and creature rigs also feed).

Mechanism: bodies accumulate until the cap; the 1.5.x reading says the mission tick walks the list every frame,
and each death can start a full ragdoll. The 1.5.x doc itself calls "this is what freezes the game" a
hypothesis.

Fix direction: port c17541f2c's per-battle cap and fade (two verified setters, the shared mission gate already
exists on 1.4.x as `MountDespawnMissionGate`), and its `[BattleSettings]` line, which makes freeze reports
self-describing. The main-menu settings offer is optional. Verify the native defaults on v1.4.8 first.

1.5.x commits: c17541f2c (#701).

### LOADING-HANG-08: the other 1.5.x robustness leads are not load hangs on 1.4.x today; two are worth keeping as release gates

**Severity P3. Confidence: high on presence and applicability (read this session).**

- **c94f797e2 (missing creature action set).** Diagnostic accuracy only: the 1.5.x check compares the returned
  set's name because "v1.5.6 returns set 0 for a missing id". On 1.4.x the Elk, Animalia and troll-brute
  features do not exist and `Main/Features/WarRam/WarRamMissionBehavior.cs` has no action-set lookup at all (grep
  for `ActionSet|IsValid|GetActionSet` finds nothing). v1.4.8 `MBActionSet.GetActionSet(string)` passes the
  native index straight to `GetActionSetWithIndex` (taom-src `...MBActionSet.cs:70-73`), so what a missing id
  returns is native and UNVERIFIED. No hang mechanism. Not needed for load robustness.
- **cd06da904 (bandit clans and dens that load apart, Patch125).** A crash class, not a hang. Both vanilla
  paths exist on v1.4.8: `GetInfestedHideoutCount` indexes `_hideouts[banditFaction.Culture]` unguarded (taom-src
  `...BanditSpawnCampaignBehavior.cs:490-501`), reached from the new-game stage (:85-97, :436) and the nightly
  clan tick (:240-262), filtered only by `IsBanditFaction` (`CanHaveSettlement`); and the Gold Rush incident's
  `IncidentEffect.InfestNearbyHideout` exists (v1.4.8 dump `_shipping_build\TaleWorlds.CampaignSystem.cs:80483`,
  used at :192173). No trigger in 1.4.x data on this laptop: `Main/_Module/ModuleData/spclans.xslt:27-31`
  strips the five vanilla bandit clans, and all eight remaining TAOM bandit cultures have hideouts in the
  installed TAOM_Map (v2.0.27; a read-only scan this session). The trigger is a TAOM and TAOM_Map version skew,
  which the public channel makes possible. Worth porting as a release check (each channel ships each bandit
  clan with its dens), and the guard is cheap. Whether a swallowed throw in the new-game stage loops like
  LOADING-HANG-03 depends on the campaign loading step machine, not read: UNVERIFIED.
- **103636439 (dead weapon preview prefab).** `Main/_Module/GUI/PreFabs/PreviewPanel.xml` and `EventXml.xml` are
  on 1.4.x; nothing in `Main` references `PreviewPanel`. Harmless at load. The brush test it adds
  (`PrefabExtensionBrushTests`) is a cheap data gate, not a hang fix.
- **84f0459bc and d99398f12 (bow shader sources).** On 1.5.x the 2026-10-15 Armory update gave bows materials
  that use custom `taom_*` shaders; the engine compiles only from `<game>\Shaders\Sources`, so without the
  sources those bows never rendered and spun a loading indicator (#916). On 1.4.x: this laptop's Armory and
  TAOM_Map compile reports reference no `taom_*` shader (0 rows; the Armory's top shaders are `pbr_metallic`
  48,956, `hair_shader_aniso` 386, `quiver_deformer` 133, `bow_deformer` 56), and the public 1.4.8 Armory is the
  2026-09-29 publish, before that update. Port the `tools/package_release.py` check with the next 1.4.8 Armory
  publish that follows the 1.5.x Armory.
- **Plan 012 (per-frame loading-window trace).** Not applicable: it fixes 1.5.x's `MapLoadDiagnostics`
  (`Patch89`), which 1.4.x does not have.
- **Plan 036 (on-demand animation clips against a 12 MiB budget).** The loading window also waits for
  `!MBAnimation.IsAnyAnimationLoadingFromDisk()` (View.cs:16483). Clip thrash would cause hitches; a window that
  never closes because a clip is always loading is not shown anywhere. UNVERIFIED; the 1.5.x probe (a9f10688f)
  is the instrument if it is ever suspected.

## Summary

### The Harad player's freeze, in one paragraph

E3 and E4 are the same machine failing the same way three times: each mission loaded normally (measured from
`MissionOpenNew`: E3 in 3.1 s, E4's tournaments in 4.4 s and 3.8 s), reached `FinishMissionLoadingDone`, and then sat at the engine's
`SceneView.ReadyToRender()` gate while it compiled 2,720, 1,569 and 806 shaders. During that wait the main
thread ran no managed frame (68 s with no 1 Hz marker in E4), so the screen looks frozen. One of the three
finished after 68 s; the player killed the other two after about 96 s and 208 s. The previous session died on
the same fight (E3's stale marker). The field battle later in E4 cleared the render wait in 0.7 s
(`shaders=1`), once its shaders were cached. TAOM's own watchdog waits 300 s, so it never spoke. Nothing in the 1.4.x or 1.5.x code shows the
player that the game is working during an in-battle compile. That is LOADING-HANG-01.

### Ranking: load-hang and stall mechanisms whose triggers exist in 1.4.x code or data

| Rank | Id | Mechanism | Trigger present on 1.4.x? | Seen in evidence | 1.5.x commits to port |
|---|---|---|---|---|---|
| 1 | 01 | Cold shader compile blocks the main thread at the render wait; players kill working loads | Yes: TAOM and Armory material volume, no module sacks on this laptop, v1.4.8 wipes the cache on any mod-list change | E3, E4 (twice), bundles of 2026-09-04 and 2026-10-18 | 372773d1a (after a v1.4.8 header check), 82645b53f (extend to battles), 89374fc4f, 1581d4d0a |
| 2 | 03 | A throw in `Mission.AfterStart` is swallowed by TAOM's Native2Managed finalizer and the mission reloads every frame | Yes: about 25 behaviours wired inline with no guard; any vanilla behaviour reading bad TAOM data | No | ecaa67d26 (guard, `finally` closer) |
| 3 | 02 | Missing collision body spins `PreloadHelper.WaitForMeshesToBeLoaded` forever after the loading screen drops | Only through Armory data; laptop Armory v2.0.26 clean, players' 2026-09-29 Armory unchecked | No (its last line would be `BattlePlayable`) | fb8d73145, 05bd0a730, ef6afeb67, 332f2ac71 |
| 4 | 06 | Per-frame skeleton pool overflow deadlocks a crowded battle | Yes: code byte-identical on v1.4.8; TAOM mounts and creatures add skeletons | No (299 and 559 agents are far below) | ecaa67d26 (guard and watch) |
| 5 | 07 | Corpse and ragdoll accumulation: mid-battle frame cost, not a load path | Yes for troop corpses (mounts already fade on 1.4.x) | The #701 player reports | c17541f2c |
| 6 | 04 | Stale marker cannot tell a kill during a compile from a hang | Diagnostic gap | E3 | none |
| 7 | 05 | "111 s town load" is menu time; per-agent logging is cheap | Not a defect | E4 | none |
| 8 | 08 | c94f797e2, cd06da904, 103636439, 84f0459bc, plan 012, plan 036 | No current trigger | No | cd06da904 and 84f0459bc as release gates only |

### Answers to the seven questions

1. **Battle load pipeline.** `WaitingForRender` waits on native `SceneView.ReadyToRender()` (`MissionState.cs:110-113`);
   the compile flood starts at `Scene.ResumeLoadingRenderings()`, the last line of `FinishMissionLoading`. The
   count is read on the main thread once per frame, so it drains in the log only while frames still run (E3:
   23 shaders in 2.26 s), then the main thread blocks and the log goes silent; the count is not "frozen", it is
   unobserved. Cold cache: first battle after install, a game update or any mod-list change pays a full compile
   of everything that battle shows. Memory pressure slows it (E3/E4 at 80 to 88 percent memory load). A forever
   stall is not shown: one of three finished; the other two are unresolved without the player's `rgl_log`
   (LOADING-HANG-01).
2. **PreloadBodyGuard.** Same do/while with no exit on v1.4.8 (`View.cs:3355-3371`, signatures confirmed with
   taom-src); triggers live only in Armory `body_name`, `holster_body_name` and collision attributes; the
   laptop's Armory has none missing, the players' copy is unchecked; the hang would show `BattlePlayable` as the
   last line, so it is not E3/E4 (LOADING-HANG-02).
3. **Corpses and ragdoll.** A per-frame battle cost with no load-time path; the two corpse setters exist on
   v1.4.8; native defaults unverified; 4e4a0c9e7 is already on 1.4.x but lives in the unversioned Armory
   (LOADING-HANG-07). The skeleton pool overflow (LOADING-HANG-06) is the freeze that creature rigs and mounts
   feed.
4. **Stale marker.** Written at `Mission.Initialize`, cleared at the first mission tick, consumed at every main
   menu; `battle_terrain_031` is a vanilla scene with its own sack, picked by map position, so the retried
   fight replayed the same load (LOADING-HANG-04).
5. **533 agents, 111 s.** 107.5 s of the 111.3 s are menu time before `MissionOpenNew`; the mission loaded in
   3.8 s; the agents are vanilla's arena audience; per-agent INFO logging costs well under a second
   (LOADING-HANG-05).
6. **Other leads.** ecaa67d26 is the important one (LOADING-HANG-03 and -06). c94f797e2 does not apply,
   cd06da904's code paths exist on v1.4.8 with no current trigger, 103636439 is harmless (LOADING-HANG-08).
7. **Ranking.** The table above.

### Port recommendations from this lens, in order

1. Battle render-wait notice plus a background heartbeat, both reading `compile_shader` lines from the engine
   log (new work modelled on 82645b53f; nothing on 1.5.x does this for battles).
2. fb8d73145 (Patch90) and the Armory body gates; run the gates against each public Armory publish.
3. ecaa67d26's mission-start guard, or at least a throw-proof `AddTaomBehavior` and a `finally` closer.
4. ecaa67d26's skeleton buffer watch first, then the guard.
5. c17541f2c (corpse cap and `[BattleSettings]` line), after confirming the native defaults on v1.4.8.
6. 89374fc4f and 1581d4d0a (triage tool), and a stale-marker notice that recognises a render-wait kill.
7. 372773d1a (ShaderCacheKeeper) once a v1.4.8-written `shader_mapping.bin` header is read.

### What would settle the open points

- The Harad player's `rgl_log_<pid>.txt` for the 20:00 and 20:16 sessions (compile rate, failures, wedge or
  not), and any TAOM log between 20:04 and 20:16.
- A kill-during-compile test: count `.sacx` files in `C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11`
  before and after.
- A v1.4.8-written `shader_mapping.bin` header (desktop) for the keeper port, and #448's question about module
  sacks.
- `tools/validate_mesh_refs.py` Tier C against the public channel's Armory folder.
- A native read of the v1.4.8 corpse defaults.


