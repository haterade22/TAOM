# REPORT: TAOM engine performance and memory, 2026-10-02

Written for: Mike, deciding what TAOM changes to make in how it drives the engine, with memory and
performance as joint top priorities and large-battle frame rate first. Baseline `dffdf879` on
`bannerlord-1.5.x`; all work in this run lives on `perf/engine-performance` and `perf/NNN-*` branches.

How this was done: yotthani's engine research (MithrilForge `docs/engine/*` at `7c556554`, his
`bannerlord` repository at `2e44db7`) was read in full and every claim TAOM relies on was re-checked
on TAOM's own copy of the v1.5.3 client with TAOM's tools (`tools/native_decompile.py`,
`tools/native_sig_author.py`, the v1.5.3 managed decompile). Five read-only explorers mapped TAOM's
mission, campaign, load-time and asset surfaces and its measurement record; a critic checked them. The
verified engine facts are written up as a TAOM reference page,
[mission-frame-threads-and-native-costs.md](../../../docs/reference/engine/mission-frame-threads-and-native-costs.md).

## Summary

1. **The biggest engine-side finding is clip residency.** On the v1.5.3 binary, every animation clip at
   Loading Type 1 or 2 costs an atomic increment and decrement of one shared counter on every data
   access, blocks the sampling worker on its first use, and is evicted again whenever the engine's
   on-demand clip data passes **12 MiB** (a constant read by one function only). A blocked worker
   delays the parallel agent tick, and the next frame's main thread waits for it, so a clip load shows
   up as a frame spike. Vanilla ships 1,137 such clips that every TAOM battle plays, TAOM's hill troll
   binds 24 and the elephant 8. This is the leading hypothesis for the unexplained 0.6 to 1.1 s battle
   hitches. Two levers follow (resident clips, or a larger budget); both need one measurement first.
2. **TAOM's own per-frame code is unmeasured, and some of it is expensive by construction.** Ranked by
   likely cost: a 5 s per-agent census in every mission, a worker-thread formation patch that
   allocates, locks and reads MCM on every call, about eight MCM registry lookups per melee hit,
   PatchShield's finalizer on hot engine methods, per-frame skeleton wrappers on crewed elephants, and
   always-on diagnostics that flush the log synchronously. None of these has a frame-time number;
   plan 028 builds the instrument that gives one.
3. **Memory is dominated by content, and the levers are measured.** The campaign map costs about 4.2 GB
   on Mike's machine today, paid twice: about 2.1 GB when the loading screen reads the scene, and about
   2.15 GB of textures streamed on the first view (down from about 6.1 GB on 2026-09-12; the first-view
   cost fell by 1,331 MB, within 1 MB of the 1,332 MiB of textures the 2026-09-13 downsizing removed). The
   16K vista is still 341 MB of it. The 1.02 GB of mesh edit data next to the map's meshes is **not**
   loaded in play (closed by the engine code and the logs). Every UI sprite category is always loaded:
   435 MB of texture data pinned all session, 299 MB of it four uncompressed sheets that BC7 would cut
   by about 224 MB; 39 orphan sheets add 40 MB to every download. The 2.5 GB inventory spike in the logs
   is cheat mode's every-item list, not TAOM code. Whether the campaign map leaks slowly is open: the
   only two long map stretches on disk (16 minutes each, fast forward) show +16 and -7 MB/min, while the
   2026-09-12 audit measured 60 to 100 MB/min over five minutes at normal speed; a 30-minute soak at
   each speed is the measurement that settles it (FOR-MIKE item 3).
4. **The campaign map draws far more than vanilla**: 14.9M faces against vanilla's 5.5M (largest record
   times placements), LOD-less meshes such as `wildling_village_large` (98,508 faces placed 42 times),
   322 particle instances against 4, and 70 lights against none.
5. **Shader sacks are a policy question, not a regression** (corrected after the maintainer's answer,
   2026-10-02: he does not ship sacks because they have been problematic for players). Measured: every
   channel ships no module-level sack and 52 to 53 per-scene sacks (about 92 MB, all format `0x0783`,
   the current one, so #448's version gap is closed in practice); testing's Main_map and Edoras town
   carry none. A missing sack costs first-use compiles on a cold local cache (after an install, an
   engine update, or, since 1.4.8, any module-list change); a warm cache serves them. **Now measured
   from the engine's own log:** a cold game start compiles 1,335 `pbr_metallic` shaders in 32.5 to
   34.3 s of a roughly 53 s start (twice on 2026-10-02, the second after a session on another module
   list; a warm session on the same list compiled 46), and a campaign load spends up to about 3 s on the
   map's terrain step, which takes 0.14 s in a scene with a sack (no compile in it: how much of the 3 s
   is shader loading against terrain setup is unmeasured). **Read from the engine on 2026-10-03**
   (`evidence/load/shader-cache-native.md`): the runtime cache is dropped when the set of active module
   ids or the game build changes, never by a TAOM update alone, so a player who plays only TAOM pays the
   half minute once per install or game update, and one who switches mods pays it at every return. The
   1,335 are 40 material flag combinations of the Armory's `pbr_metallic` materials, eight of them
   compiled in 104 engine contexts (weather, quality, SSR and others) for 888 of the compiles. A
   Kit-written Armory sack on Mike's install did not serve them, and the engine's sack reader crashes
   the game on a damaged sack. The decision stays the maintainer's (FOR-MIKE item 1).
6. **Load time is half module-XML merging.** In the engine's own millisecond log, about 28 of a new
   campaign's 50 loading seconds and about 15 s of a custom battle's go to merging module XML: the engine
   recompiles the schema and every XSLT and round-trips the whole merged document for each file, so each
   of TAOM's 46 NPC files costs 0.14 to 0.29 s however small it is. Already measured offline with the
   engine's own code (`evidence/load/merge-bench.md`): a merge that keeps one document across files cuts
   the four biggest types from about 8.8 s to 2.4 to 3.1 s, with output identical character for
   character. **Plan 042 built it (2026-10-03, `19be93be`, in review):** on the live install, inside the
   test host, every merged type is identical to the engine's for both game types, and the four heaviest
   types drop from 17.5 to 2.9 s (campaign) and 12.9 to 1.2 s (custom battle). The engine path runs about
   twice as slow in the test host as in the offline prototype, so the fair ratio is the prototype's 27 to
   35 percent of the engine's time; the in-game saving is measured after merge by its `[XmlMerge]` summary
   line. `lords.xslt` (1.26 s, compile included) is the next lever after it. The rest of a new campaign's load: the map scene read about 5 s,
   new-game hero creation about 6.6 s, the map's terrain step about 3 s (no sack; terrain shaders from
   the runtime cache plus terrain setup, split unmeasured).
7. **The managed runtime is the .NET Framework CLR, not Mono** as TAOM's toolchain doc said. Main-thread
   allocation therefore costs blocking gen0 collections, and per-thread allocation can be measured
   (`GC.GetAllocatedBytesForCurrentThread`, bound by reflection).

## Verified engine facts (TAOM's binary, 2026-10-02)

| Fact | Evidence |
|---|---|
| Frame order: `OnPreTick` waits for the previous agent tick (`WaitTickCompletion`, `Thread.Sleep(1)` spin), then `OnPreMissionTick`; `OnTick` runs `OnPreDisplayMissionTick`, `OnMissionTick`, then starts the async agent tick | v1.5.3 `Mission.cs` ~3546, ~3591-3597, ~3652-3791; native job `IMBMission.TickAgentsAndTeamsAsync` RVA `0x6F6290` |
| Timer resolution is 1 ms for the process: one `timeBeginPeriod(1)` at engine start | Ghidra `0x27A80` (called by `WotsMain*`), call at `0x27DAC`; single IAT reference |
| `/maxThreadCount N` launch argument sets the job manager's worker cap | same function, store at job manager `+0xA4C` |
| Agent cap 2,040, a six-byte `return 0x7F8` | `IMBAgent.GetMaximumNumberOfAgents` `0x6E4FA0` |
| `GetNearbyAgents` returns only `IsHumanoid` agents in state `Active`, 40 per native call under one global managed lock, a spatial grid up to 15.0 m and a walk of every agent beyond | `IMBMission.GetNearbyAgentsAux` `0x6F70E0`; float `15.0` at `0xB2E010`; `AgentFlag.IsHumanoid = 0x800`; `Mission.cs:2330-2352` |
| On-demand clips: reader count with CAS loop, load start, condition-variable wait; atomic release per access; 12 MiB eviction budget | acquire `0x474140`, init `0x473090`, accessors `0x473320`/`0x4733A0`, eviction `0x21DEA0`, budget float at `0xB2E2DC` (one reference in all of `.text`), 28 atomic sites on `+0xD8` |
| `MBAnimation.IsAnyAnimationLoadingFromDisk()` reports an in-flight on-demand load | `0x6EAAE0` walks the clip records for state 1 |
| Managed runtime: .NET Framework 4.x desktop CLR; .NET Framework 4.8.1 on the desktop has `GC.GetAllocatedBytesForCurrentThread` | `Bannerlord.exe` CLR header; launcher runs the game in-process; `[MissionDiag]` CLR 4.0.30319.42000; gc1 > 0 with gc2 = 0 |

yotthani findings TAOM has not re-derived are tagged in the reference page: the Kit's texture format
by name suffix, the texture flag bits, metamesh visible distance defaulting to `FLT_MAX`, particle
min and max config bounds, mesh edit data shipping in packages, limb-ray costs.

## The battle hitches: what the existing logs already rule out

Single frames of 0.6 to 1.1 s recur every 30 to 70 s in battles (2026-10-02 logs) with `gc2=0`, with
and without trolls. Tonight's correlation of every `[MissionPerf]` window against the other log lines
in the same five seconds (`evidence/native/hitch_correlate.py` method, three sessions):

- **Not TAOM's log activity in steady state.** In the 11:38 session (7 hitch windows, 62 calm), no tag is
  over-represented in hitch windows: `[MemSample]` appears in 3 of 7 hitch windows and 29 of 62 calm ones;
  one 674 ms hitch window holds nothing but the always-present `[MapLoad]` line.
- **Silent in both logs.** The engine's own log has no line at all in the 1,090 ms hitch window
  (14:35:28 to 14:35:33, `rgl_log_56116`), and no TAOM tag is over-represented there either
  (`evidence/native/hitch_rgl_correlate.py`; the session's other hitch window, 544 ms at t=+6 s, is the
  battle start's order burst). Whatever stalls the frame reports nothing. With GC, paging, shader
  compiles and TAOM's samplers ruled out below, the leading untested candidate is a blocking on-demand
  clip load (the verified clip path above locks and loads from disk when the 12 MiB budget has evicted
  a clip); plan 036 logs the clip bytes and plan 041 samples `IsAnyAnimationLoadingFromDisk` inside the
  slow frame. Only one session has both logs with a battle in it, so this is one data point.
- **Not shader compilation, in the sessions on record.** The 1,090 ms hitch at 14:35:28 to 14:35:33 has
  no shader line near it: that session's 46 compiles ran at 14:34:38 to 14:34:44, while the custom battle
  menu rendered lord previews, before the battle opened at 14:35:15; the two cold-start sessions compiled
  only in their first minute (`evidence/load/shader-cache-native.md` section 3). A player with a cold
  cache can still compile a variant on first use mid-battle; whether that blocks the frame is not settled.
- **A log burst at battle start in elephant battles.** The 2026-09-29 spawn window that holds a 1,090 ms
  hitch also holds 1,547 `[Howdah#]`, 273 `[Howdah]` and 92 `[Elephant]` INFO lines, each a synchronous
  flush on the game thread. Correlation only; plan 028's per-phase breakdown will show whether it is the
  cause.
- **Not paging.** The 11:38 session ran at 82 to 84% system memory load, but across its
  steady-state hitches the working set does not move (869.8 ms at 11:44:54: 5,513 to 5,513 MB; 913.5 ms
  at 11:50:48: 5,763 to 5,766 MB) and about 10 GB of physical memory stays free, where a paging stall
  of that length would fault tens of megabytes back in (`evidence/native/hitch_ws.py`; the
  hitches at t=+6 and +16 s fall in the battle-start load, when private bytes are still rising).
- **Not TAOM's stack samplers.** The stall watchdog suspends the main thread for a stack only after a
  10, 20 or 40 s stall (`MissionTickStallWatchdog.cs:23-24`), and the exit sampler only on a mission
  exit; neither runs on a timer in a healthy battle.
- **Left standing as suspects:** on-demand clip loads and evictions blocking the agent tick (mechanism
  verified natively), the agent tick itself, native streaming, and script-component ticks outside
  `Mission.OnTick`. The profiler (028, amended to cover spawn work, script components and clip loading)
  and the clip probe (036) are built to decide between them in one session.

## TAOM's surfaces, ranked

### Battle frame time (first priority)

| # | Finding | Evidence | Plan |
|---|---|---|---|
| B1 | No per-feature attribution exists: `[MissionPerf]` is whole-frame only | `Main/Features/MissionPerf/` | 028 (profiler, hitch breakdown, context line), 029 (log tool) |
| B2 | On-demand clip locking, blocking and 12 MiB eviction (see Summary 1) | Verified facts above | 028 records `IsAnyAnimationLoadingFromDisk`; 036 reads the engine's loaded-clip bytes; the lever is the maintainer's |
| B3 | MissionDiagnostic census: every agent, every frame, first 5 s of every mission: a native string marshal, a localized name and an interpolated key each | `MissionDiagnosticBehavior.cs:19, :41-45, :76-91`, registered unconditionally `SubModule.cs:2128-2132` | 030 |
| B4 | MixedFormations `Patch30` on `Formation.GetOrderPositionOfUnit` (worker threads, about twice a second per AI agent plus bursts): allocation, MCM read, global lock, a formation query that recomputes off-thread without a lock | `Patch30_FormationGetOrderPositionOfUnit.cs:41, :54, :68`; `FormationLayoutService.cs:68, :79-93`; `FormationAdapter.cs:40-41` | 032 |
| B5 | About eight `TaomSettings.Instance` lookups per melee hit, and per-frame lookups in six behaviours; each is two `ConcurrentDictionary` operations plus a container walk | `CombatMechanicsSettingsProvider.cs:19-70` and the providers in plan 031; MCMv5 decompile | 031 |
| B6 | PatchShield binds `__originalMethod` on every call of 159 to 305 wrapped engine methods, several per hit, per frame or per agent. **Measured by plan 034:** 64 ns and 241 bytes of garbage per call on one thread, 1,146 ns per call with 8 threads contending, against about 5.4 ns per call and 0 bytes for the same finalizer without that binding as shipped (TAOM.Dependencies is a Debug build; 1.3 ns optimized, the figure first quoted; plan 034's review re-measured it) (the "~50 us" figure was a misread average) | `PatchShield.cs:246, :264`; diag.log shield-pass lines; plan 034's `result-1.txt` | 034 (Branch B: resolve the original on the exception path only) |
| B7 | Crewed elephants: a native `Skeleton` wrapper with a finalizer per howdah per frame, per-seat native writes, diagnostics on by default | `TaomHowdahMachine.cs:274`; `BoneCheck.cs:130-133` | 033 |
| B8 | Behaviour-tree framework: `DateTime.Now` per tree per frame, list allocations per selector entry, boxed event arguments per removal with no subscriber | `SleepTask.cs:22`, `BehaviorTreesNodes.cs:131-135`, `BehaviorTreeMissionLogic.cs:289-310` | 033 |
| B9 | SpatialGrid rebuilt every 2 s in every mission, O(N) removal per deleted agent (plan 003's fix never landed) | `AdvancedCombatBehavior.cs:16, :38-47`; merge `40615c22` | 033 |
| B10 | A write-only per-agent colour store keyed on `Agent.Index`, never evicted | `AgentColorStore.cs`; no reader anywhere | 030 |
| B11 | An empty `Mission.OnTick` postfix that still costs a Harmony detour, an MCM read and a PatchShield finalizer every frame | `Patch35_Mission_OnTick.cs:31-35` | 030 |
| B12 | Per-hit `[CareerPerks]` DEBUG lines (a pattern removed once before for log spam) | `CareerAgentStatService.cs:183-208`, added `7ede923b` | 030 |

### Memory (joint first priority)

| # | Finding | Evidence | Lever owner |
|---|---|---|---|
| M1 | Textures: on 2026-09-12, 2.3 GB of the map's 4.4 GB moved with the texture-quality slider. The 2026-09-13 downsizing has since shipped (Main_map textures 1.04 GB patreon, 1.03 GB testing, from 2.34 GB); the 16K vista (341 MB) remains a third of it | `docs/investigations/native-commit-audit-2026-08.md:649-666`; tonight's `audit_map_scene_memory.py` runs on the patreon and testing packs (`evidence/memory/`) | Content: downsize the vista (8K saves about 256 MB); code: a texture-quality advisor for 16 GB machines (extends the #701 advisor), worth re-measuring now that the base is smaller |
| M2 | Module textures keep full mip chains in memory; vanilla keeps a 512 px tail and streams | yotthani (Kit publish filter), consistent with TAOM's VMMap readings | Content: cap dimensions and mip counts |
| M3 | Session memory, re-read from the logs (`evidence/memory/campaign-memory-reread.md`): about 7.6 GB private at the main menu (6.8 GB on 2026-09-12, then on v1.4.8); campaign load +2.6 GB (now 32 to 50 s, from about 2 minutes on 2026-09-12), of which about 2.1 GB is the campaign map scene's read within 5 s of `GameInitializationFinished`; the first view of the map streams about +2.15 GB of textures and then holds flat over the two 16-minute map stretches on disk (+16 and -7 MB/min; a longer soak is owed). The campaign map therefore holds about 4.2 GB, down from about 6.1 GB on 2026-09-12. The large transients are UI screens: every logged inventory open (16 of 16) added about 2.5 GB of managed heap, and the five whose logs can show it stalled the main thread 11 to 14 s; the party screen added 0.6 to 0.75 GB; all with cheat mode on, which makes vanilla list every item (about 5,165 rows) and troop (about 1,271) in the game at about 0.5 MB per row (`InventoryScreenHelper.cs:177-188`; `evidence/memory/inventory-open-alloc.md`). A player's open without cheat mode is estimated at 95 to 150 MB and under a second (UNVERIFIED). After close the memory is held only by the closing call stack during the pop's collection, so the next collection frees it. The 2026-09-02 "8.4 GB in 37 minutes on the map" session is no longer on disk and is not reproduced | `evidence/memory/campaign-memory-reread.md`; `battle-load-diagnostics.md:420-427`; `native-commit-audit-2026-08.md:392-430` | Measurement first: one inventory and one party open with cheat mode off (FOR-MIKE item 12); about 3.9 GB of map-load regions are still unnamed |
| M4 | **Resolved, not a memory cost.** Mesh edit data ships next to the render buffers (TAOM_Map: 1.02 GB edit data against 735 MB on Main_map; vanilla Native the same way, 8.6 GB against 8.1 GB), but the shipping client reads it only for face and body generation, tableaus, explicit mesh-edit operations and mesh batching (which nothing calls), so static scenery's edit data stays on disk in play. The logs agree: the map scene read at load (+2.07 to 2.21 GB within 5 s of `GameInitializationFinished`) is smaller than the scene's render buffers plus edit data would be (2,882 MiB), and the first view's growth tracks TAOM_Map's textures to within 1 MB across the downsizing | engine reference page section 7; `evidence/memory/campaign-memory-reread.md` section 1; Ghidra: GUID `5f98413d...` at `0xAE3D50` read only by `0x68D30` and `0x181CD0`, callers traced; render buffers by `0x66700`; `evidence/native/native-mesh-editdata.txt` | None |
| M5 | On-demand clip data is held to 12 MiB by design; raising it trades a bounded amount of memory for less reload churn | verified facts above | The maintainer's decision after plan 036's measurement |
| M6 | TAOM's UI sprite sheets: all five categories are `AlwaysLoad`, so 13 sheets (434.7 MB of texture data) stay pinned all session; 298.7 MB of that is four uncompressed 4096 px sheets (`ui_loading_1`, `_2`, `ui_taom_bannericons_1`, `_2`). BC7 would save about 224 MB | `evidence/memory/ui-sprite-sheets.md`; v1.5.3 `SpriteData.cs:205-208`, `SpriteCategory.cs:56-58`, `TwoDimensionEngineResourceContext.cs:8-19` | Content (Kit): compress the four sheets, check by eye (FOR-MIKE item 13) |
| M7 | 39 orphan banner-icon sheets (`_3` to `_41`, left when the category went from 41 sheets to 2 on 2026-08-08) ship in every channel's `pack0.tpac`: never loaded, but 39.6 MB of the 180.2 MB download | same file; `git log -p` of `TAOMSpriteData.xml` | Content (Kit): delete them before the next packaging (FOR-MIKE item 13) |
| M8 | The 7.6 GB main-menu floor, partly named: the UI sprite sheets pinned all session (435 MB, M6) and the managed heap (about 130 to 140 MB, the `[BattleLoad]` heapMB readings). **Not in it: TAOM's module sounds** (437 files, 166 MiB, behind 232 `module_sounds.xml` entries). Read in the binary on 2026-10-03: the engine only registers them at startup, and FMOD opens a file when an event plays it (a programmer sound, created at `0x2378E0` and released on the event's destroy callback), decoding the whole file into memory for that play since no stream flag is passed. MP3s cost about 5.4 times their size while playing; the largest registered sound outside `LOTR/OST` decodes to 2.5 MiB. Three registered music tracks (27 to 46 MiB decoded) are played by nothing; 21 files (53.3 MiB) are not registered; one entry (`elf_horn.wav`) names a missing file. The rest of the floor is unnamed | `evidence/memory/module-sounds.md` (native facts, decoded sizes, the unregistered list) | Measurement: main-menu private bytes for a vanilla start against a TAOM start on the same machine. Content: the module-sound cleanup (FOR-MIKE 13d) |
| M9 | TAOM's 33 banner icon atlases (`banner_icons.xml`'s `taom_banners_<culture>_alpha_NN` and ornament sheets) are each 4096 x 4096 uncompressed RGBA with one mip: 64 MiB apiece, 2,112 MiB for all 33, against vanilla's 2048 x 2048 BC7 banner atlases at 4 MiB. Each loads when a banner using it is drawn. Computed from the data: the campaign's own 265 clan, kingdom and culture banner keys name 19 of the 33 (1,216 MiB at today's format if all are drawn; the other 14 serve only runtime-built banners). How many the first map view draws and how much shows in private bytes is unmeasured; a share of the map's first-view 2.15 GB is a hypothesis. Also found: five clans' banners name icon ids no module defines, so those layers draw nothing (`clan_khuzait_16` has no emblem at all) | `evidence/memory/banner-atlases.md` | Content (Kit): re-export as BC7 (19 used atlases: 304 MiB at 4096, 76 MiB at vanilla's 2048); FOR-MIKE items 13c and 13e |

### Campaign map and loading (later waves)

| # | Finding | Evidence |
|---|---|---|
| C1 | Caravan trade repeats vanilla's distance query per town per pass and reads MCM several times per score, for 302+ caravans, 78 towns, up to two passes | `CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:40-41`; vanilla `TaleWorlds.CampaignSystem.cs:178046-178160` |
| C2 | Party speed: a roster walk plus two or three detours (each with a PatchShield finalizer) on every speed recompute for 2,000 to 3,000 parties | `TaomPartySpeedModel.cs:70-80`; `PartyBaseHelper_HasFeat_Patch.cs:34-39` |
| C3 | Per-frame MCM reads on the map: about 12 in RealmBorders, 7 in FieldCommission, others | `RealmBorderService.cs:241-299`; `FieldCommissionBehavior.cs:51, :105-117` |
| C4 | Always-on campaign diagnostics that flush synchronously: the `[MapLoad]` heartbeat and SceneReady trace forever, the AutoResolve battle log per world battle, EconomyDiagnostics' gold patches | `Campaign_RealTick_MapLoad_Patch.cs:72-127`; `AutoResolveLogWriter.cs:86-99`; `SettlementComponent_ChangeGold_Patch.cs` |
| C5 | **Measured: about 28 of a new campaign's 50 loading seconds, and about 15 s of every custom battle load, are the engine's module-XML merge.** It recompiles each XSLT and the XSD per file and round-trips the whole merged document at every file, so each of TAOM's NPC files costs 0.14 to 0.29 s even at 3 KB (NPCCharacters 56 files 13.3 s, Items 142 files 9.6 s at campaign load). Also measured: the map scene read about 5 s, new-game hero creation about 6.6 s, the map's terrain shaders about 3.0 s without a sack (0.14 s in a scene with one) | `evidence/load/load-time-rgl.md` (engine rgl logs); v1.5.3 `MBObjectManager.cs:962-1005, :1057-1110` (dump); plan 042's brief |
| C6 | Map GPU load: 14.9M faces against 5.5M, LOD-less heavy meshes, 322 particles against 4, 70 lights against 0; the Mordor scenes carry 429 to 531 lights against vanilla's maximum of 305 | the map manifest and scene greps (critic) |
| C7 | PatchShield pass 2 at every game start (about 125 to 140 attaches; 1 to 3 s on player machines) | `Dependencies/SubModule.cs:274-293` |

## What is being built tonight

Each plan runs on its own branch off `perf/engine-performance`, test first, through the `/deep-review`
lenses, never merged or pushed.

| Plan | What | Why first |
|---|---|---|
| 028 | Mission tick profiler: per-behaviour ms and allocation, engine phase times, a `[Hitch]` breakdown per spike, a `[PerfContext]` line per mission; default off | It turns every other claim here into a number, and it answers the hitch question in one run |
| 029 | `tools/perf_runs.py`: one row per mission from any number of logs, A/B comparison, confounder flags (frame cap, memory pressure, dirty build) | The 2026-10-02 logs show a 116 to 117 fps plateau that would hide any CPU gain |
| 030 | Diagnostics diet, behaviour-preserving (B3, B10, B11, B12, troll and creature-bandit scans) | Cheap, safe, every mission |
| 031 | MCM reads off hot paths (B5 and the per-frame reads) | The 7feca96b pattern, proven |
| 032 | Worker-thread formation patch and wield getters (B4) | Also a thread-safety fix |
| 033 | Creature-battle allocations (B7, B8, B9) | Creature battles are TAOM's signature |
| 034 | PatchShield per-call cost: measure, then act only if material (B6) | Decide by numbers |
| 035 | Release packager reports each scene's shader sack and its format version (never refuses, D8), and each DLL's optimization state | Shows which scenes ship a sack and flags a sack out of step with the rest; a set lagging as a whole, or a module-level sack, goes unreported (FOR-MIKE 16t) |
| 036 | A read-only probe of the engine's on-demand clip bytes against the 12 MiB budget, signature-guarded (B2, M5) | Decides the clip lever |
| 037 | Campaign hot paths (C1 to C3) | Per-party and per-frame costs on the map |
| 038 | Battle equipment memory audit (offline) | Ranks the Armory assets a battle loads |
| 039 | Campaign map frame profiler | The map's counterpart of 028 |
| 040 | Load-time stamps | Times what the rgl log cannot attribute |
| 041 | Profiler extensions (spawn, script components, clip loading) and an on-by-default hitch probe | Answers the hitch question without the maintainer switching anything on |
| 042 | The module-XML merge, measured offline and replaced by a byte-identical equivalent (C5) | 15 to 28 s off every load, for every player |

## Decisions for Mike (detail in FOR-MIKE.md)

1. Shader sacks: answered (not shipped by policy, D8); no action unless players report first-session map problems.
2. Clip residency: after plan 036's numbers, resident TAOM clips (Armory data, unique keys), a larger
   engine budget (a guarded four-byte patch), both, or neither.
3. Debug versus Release: TAOM ships Debug on purpose. The profiler makes the measurement cheap; no
   change is proposed without it.
4. Texture-quality advisor for 16 GB machines (M1).
5. Content levers for the map (M1, M2, C6): the 16K vista, LODs for the heavy LOD-less meshes, lights,
   particles (the texture downsizing has already shipped).
6. Costly diagnostics: keep every one on and make each cheap without losing information (cached
   settings, aggregated or on-change lines, end summaries); no default changes (FOR-MIKE item 7).
7. Issues: drafts for each plan, to file on your word before any branch lands on a trunk.
8. The inventory and party screens with cheat mode off: one five-minute check for the player's cost
   (FOR-MIKE item 12).
9. UI sprite sheets in the Kit: delete the 39 orphan banner-icon sheets, re-save four sheets as BC7
   (M6, M7; FOR-MIKE item 13).
10. Trunk CI has been red on its last 30 runs; a small fix if you want CI meaningful again (FOR-MIKE
    item 14).

## Not covered yet

Audio beyond module sounds (FMOD Studio's own banks and voice counts; module sounds: M8); navmesh and
pathfinding cost in large custom scenes; ragdoll physics after mass deaths
(#701's owed checks); co-op overhead; GPU timing (no GPU timer exists; the profiler's `otherMs` is the
proxy); the campaign map's 3.9 GB of unnamed load-time regions; LoadXML timing per id. Each has a
measurement named in the audit files under `E:\repos\taom-perf\scratch\understand\`.
