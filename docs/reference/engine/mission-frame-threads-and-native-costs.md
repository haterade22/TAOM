# Mission frame, threads and native costs (v1.5.3)

Sibling reference, not a numbered phase. What one battle frame does on which thread, what the managed
runtime is, and what a few engine calls TAOM uses cost inside `TaleWorlds.Native.dll`. Written for the
2026-10-02 engine-performance programme (`plans/_audit/2026-10-02-perf/`), at the depth of yotthani's
Ghidra work in MithrilForge `docs/engine/` ([provenance](../provenance-register.md), comparison only).

Every claim carries a tag:

- **[TAOM-verified]**: checked on 2026-10-02 against the installed v1.5.3 client (`TaleWorlds.Native.dll`,
  14,209,376 bytes, Ghidra project key `Win64_Shipping_Client-45be32c57c451f78`) with TAOM's own tools,
  or against the v1.5.3 managed decompile. The method is named so it can be re-run.
- **[yotthani]**: a finding from yotthani's MithrilForge or `bannerlord` repositories that TAOM has not
  re-derived. Native addresses he gives for 1.5.3 are RVAs on the same binary hash.

Native offsets hold for this one binary only; re-verify after any engine bump (`/engine-bump`).

## 1. The battle frame

[TAOM-verified, v1.5.3 `Mission.cs`] One frame of a mission, in order:

| Step | Where | Thread | What runs |
|---|---|---|---|
| 1 | `Mission.OnPreTick` (`[MBCallback]`, ~3546) | main | `WaitTickCompletion()`: spins `Thread.Sleep(1)` until the previous frame's agent tick set `tickCompleted`; then every behaviour's `OnPreMissionTick`, reverse order |
| 2 | `Mission.OnTick` (~3652) | main | every behaviour's `OnPreDisplayMissionTick`; the camera; `tickCompleted = false`; every behaviour's `OnMissionTick`, reverse order; dynamic entities; spawned items |
| 3 | end of `Mission.OnTick` (~3784-3791) | main | `TickAgentsAndTeamsAsync(dt)`: posts a native job (`IMBMission.TickAgentsAndTeamsAsync`, RVA `0x6F6290`, stores the job object in a global and returns), or runs `TickAgentsAndTeamsImp` inline when the AI tick is synchronous |
| 4 | `Mission.TickAgentsAndTeamsImp` (~3617), reached from the native job through the `[MBCallback]` `TickAgentsAndTeams` | async AI thread | `TWParallel.For` over `AllAgents` running `Agent.TickParallel` on the worker pool; then a serial `Agent.Tick` loop; a serial `Team.Tick` loop; `tickCompleted = true`; then every submodule's `AfterAsyncTickTick` |

What it means for TAOM:

- **`OnMissionTick` is on the critical path twice.** Every millisecond TAOM spends there is main-thread
  frame time, and it also delays the moment step 3 starts the parallel agent tick for that frame.
- **A slow agent tick shows up as main-thread waiting.** When step 4 of frame N outlasts the native work
  between the two frames, frame N+1's `WaitTickCompletion` sleeps in 1 ms steps until it finishes.
- **Timer resolution is 1 ms for the whole process.** [TAOM-verified, Ghidra] The engine's start-up
  function (`0x27A80`, called from `WotsMain`, `WotsMainNative` and `WotsMainNativeCoreCLR`) calls
  `timeBeginPeriod(1)` once (`0x27DAC`) and never in a mission. So `Sleep(1)` in `WaitTickCompletion`
  overshoots by about 1 to 2 ms at most, not by the 15.6 ms a default Windows timer would give.
- **The engine's threads.** [TAOM-verified, Ghidra] The job manager's constructor (`0x43680`) sets its
  thread cap (`+0xA4C`) to 4,096; the start-up function parses a `/maxThreadCount N` launch argument over
  it. Its start routine (`0x43D30`) then creates max(3, min(logical cores - 1, cap)) threads: `Main`, a
  dedicated `Render` thread, and `Worker 1` onward, which run `TWParallel.For` (a 32-thread CPU gets Main,
  Render and 29 workers). Separately it creates `BackWorker` threads for background jobs: logical cores
  divided by 4, clamped to between 2 and 4. On-demand loads (section 6) share those at most four
  background threads with other streaming work. `/maxThreadCount` can only lower the count; useful as an
  A/B variable, not a lever to ship.
- Which callbacks reach which thread, and how TAOM code must treat them, is the thread table in
  [`.claude/rules/harmony-patches.md`](../../../.claude/rules/harmony-patches.md) "Which thread runs your
  target". This page does not repeat it.

## 2. The managed runtime is the .NET Framework CLR

[TAOM-verified] The Steam game runs TaleWorlds' and TAOM's managed code on the **.NET Framework 4.x
desktop CLR**, not on Mono:

- `Bannerlord.exe` is a managed executable (it has a CLR header and imports nothing native). TAOM's
  Visual Studio launch profile starts it with the `managed-framework` debug engine
  (`Main/Properties/launchSettings.json`), which debugs the desktop CLR only.
- The launcher (`TaleWorlds.MountAndBlade.Launcher.exe`, also managed) runs the game in its own process:
  `TaleWorlds.MountAndBlade.Launcher.Library.Program.Main` calls
  `TaleWorlds.Starter.Library.Program.Main` once the player presses Play (v1.5.3 launcher `Program.cs`).
- Every `[MissionDiag] OS:` line logs `CLR: 4.0.30319.42000`, and `[MissionPerf]` lines show
  `gc1` above zero while `gc2` stays zero for a whole battle: the three-generation pattern of the
  desktop GC.
- `mono-2.0-sgen.dll` is imported by `TaleWorlds.Native.dll` for the native-hosted entry
  (`Bannerlord.Native.exe` calls `WotsMainNative`), and a .NET 6 CoreCLR runtime ships in
  `bin/Win64_Shipping_Client/Microsoft.NETCore.App/` for `WotsMainNativeCoreCLR`. The rgl log's
  "Mono Loading Step" lines appear in client sessions too and are not evidence of the runtime.

Consequences for TAOM code:

- **Allocation on the main thread costs gen0 collections, which pause every managed thread.** No
  `Bannerlord.exe.config` ships, so the GC runs with its defaults (workstation, concurrent). A 1,184-agent
  battle on 2026-09-29 logged `gc0=99 gc1=9` in its first five seconds (the spawn wave) and `gc0=5 gc1=0`
  per five seconds once steady.
- **Per-thread allocation can be measured.** [TAOM-verified] The machine's .NET Framework 4.8.1
  `mscorlib` has `GC.GetAllocatedBytesForCurrentThread()`. TAOM compiles against the net472 reference
  assemblies, which do not expose it, so bind it once by reflection and treat it as absent on older
  runtimes.
- Correction: `bannerlord-engine-and-toolchain.md` said "Mono (sgen GC)" until 2026-10-02.

## 3. Agent capacity

- **2,040 agents, hard.** [TAOM-verified, Ghidra] `IMBAgent.GetMaximumNumberOfAgents` (RVA `0x6E4FA0`) is a
  six-byte function returning `0x7F8`. [yotthani] The limit is baked into `Mission`'s native layout: two
  embedded agent pools with element stride `0xC60` and 16-bit free lists; about 40 constants in 16
  functions and about 1,000 field offsets in 150 functions depend on it, so it is not patchable in
  practice. [TAOM-verified] The `0xC60` stride and a 16-bit next-index list both appear in the native
  nearby-agent walk (section 4).
- **Every mount is an agent.** [yotthani, re-checked by the 2026-10-02 audit in the v1.5.3 decompile]
  `BannerlordConfig._battleSizes` is the fixed array {200, 300, 400, 500, 600, 800, 1000} and the menu
  value is an index; `DefaultBattleMissionAgentSpawnLogic` caps soldiers at
  `MaxNumberOfAgentsForMission / 2` because a mounted soldier takes two slots, riderless horses included.
  For TAOM, wargs, elk, mumakil and horses each hold a slot, and corpses hold theirs until deleted, so a
  creature-heavy battle reaches the cap with fewer combatants than the slider suggests.

## 4. Nearby-agent queries

[TAOM-verified, Ghidra: `IMBMission.GetNearbyAgentsAux`, RVA `0x6F70E0`; managed wrapper
`Mission.GetNearbyAgentsAux`, v1.5.3 `Mission.cs:2330-2352`]

- **It returns active humanoids only.** The native filter keeps an agent when
  `(flags & 0x800) != 0` and its state is `1` and it lies inside the radius. `0x800` is
  `AgentFlag.IsHumanoid` and `1` is `AgentState.Active` (`TaleWorlds.Core`). No mount (horse, warg, elk,
  mumak, any creature on a horse skeleton), no routed, unconscious or killed agent is ever returned,
  whatever `GetNearbyAgentsAuxType` the caller passes. yotthani measured the same from the other side
  (113,601 and 143,038 limb rays): every agent the engine's limb ray met that a 4 m list lacked was a
  fallen body or a mount.
- **40 per native call, under one global lock.** The native function returns at most 40 ids and keeps
  its scan cursor in a global, so the managed wrapper calls it in a loop of 40-blocks inside
  `lock (GetNearbyAgentsAuxLock)`, a static lock. Every caller on every thread serialises on it.
- **A grid up to 15 m, everything beyond.** A radius up to `15.0` (the float at RVA `0xB2E010`) queries
  the engine's spatial grid; a larger radius walks every agent slot in the mission. A per-agent query
  with a large radius is O(N) natively each time.

TAOM consequences: code that expects mounts or creatures from these queries (trample, engage gates)
never sees them; add `agent.MountAgent` yourself when a rider's mount matters, as DualWield does
[yotthani]. Keep radii at 15 m or below on per-agent paths, and do not call the query from worker
threads expecting parallelism.

## 5. Limb rays [yotthani]

`Mission.RayCastForClosestAgentsLimbs` (RVA `0x6F2D60` in TAOM's engine-method map) costs about 65 to 78
microseconds per call with 1,600 agents, tests the limbs of only the agent whose origin is nearest among
those whose box the ray touches, and reports nothing if that agent's capsules are missed even when a body
further along is cut. `RayCastForGivenAgentsLimbs` (`0x6F2BA0`) returns a bone index the hit code
rejects, which raises an access violation in the damage call. Budget the cost per call before giving
creature contact limb rays ([scripted-melee-strikes.md](../scripted-melee-strikes.md) section 9).

## 6. Animation clip residency: on-demand clips lock, block and get evicted

[TAOM-verified, Ghidra, 2026-10-02] The whole chain, on the client binary:

| Piece | Where | What it does |
|---|---|---|
| Clip data record | initialiser `0x473090` | Stores the clip's Loading Type at `+0x194`. Type 0: the data pointer (`+0x88`) and size (`+0x80`) are set at once, no loader. Type 1: a short resident piece goes to `+0x90` and a loader callback (`+0x98`) is installed for the full data. Type 2: a loader callback is installed. Reader count `+0xD8`, state `+0xE0` (0 unloaded, 1 loading, 2 loaded), a mutex at `+0x130` and a condition variable at `+0xE8` |
| Acquire | `0x474140` | Type 0: returns the data pointer, nothing else. Type 1 or 2: marks the clip used (`0x473250`); spins while the reader count is `-1` (being evicted), then raises it with a compare-and-swap loop; if the state is 0 it swaps it to 1 and starts the loader (`0x474660`); a type 1 clip sampled inside its resident short piece returns that piece; otherwise the calling thread locks the clip's mutex and **waits on its condition variable until the data is loaded** |
| Release | the accessors, for example `0x473320` and `0x4733A0` | Every read of a type 1 or 2 clip's data ends with an atomic decrement of the reader count; type 0 skips it. Atomic operations on `+0xD8` appear at 28 sites, among them one sampler function with eight (`0x49D960`) |
| Eviction | `0x21DEA0` | Drains a queue of loaded on-demand clips, sorts them, and while the global loaded-bytes counter exceeds **12,582,912 bytes (12 MiB, the float at `0xB2E2DC`, read by this function only)** frees clips whose reader count it can swap from 0 to `-1`: data freed, state back to 0, the counter lowered |
| Probe | `IMBAnimation.IsAnyAnimationLoadingFromDisk` (`0x6EAAE0`, managed `MBAnimation.IsAnyAnimationLoadingFromDisk()`) | Walks the on-demand clip records and returns true while any is in state 1 (loading) |
| Load-time loaders | bulk `0x591390`, single `0x592400` | Read clip data at asset load ("Unable to read animation clip data for %s"); the bulk loader fans out jobs on the engine job manager and helps run them |

What it means:

- A clip at Loading Type 1 or 2 costs an atomic increment and decrement of one shared counter on every
  data access, so every sampling worker in the parallel agent tick contends on the same cache line for a
  popular clip.
- The first sample after a clip is unloaded **blocks that worker** until the clip has loaded. The
  parallel agent tick then finishes late, and the next frame's `WaitTickCompletion` (section 1) holds the
  main thread: a load shows up as a frame spike, not as a worker-only cost.
- Only 12 MiB of on-demand clip data stays resident. A battle whose working set of type 1 and 2 clips
  is larger evicts clips nobody is reading at that moment and loads them again on their next use.
- Type 0 clips do none of this. [yotthani] DualWield's 293 clips at type 2 cost 8.3 s of CPU in 45 s in a
  500 against 500 battle and 0.14 s once every clip was resident (an unpublished profile; the
  mechanism above is the TAOM-verified part).
- **[yotthani, seen in game]** Resident clip data is shared by a key of the clip's SourceAnimation guid
  plus its source window, so a resident clone that keeps its template's guid and window plays the
  template's motion. Give each resident clone its own key before setting type 0.
- TAOM consequence: 24 of the 30 bound hill-troll release and blocked clips and the elephant's 8 attack
  clips sit at Loading Type 2 ([bannerlord-animation-system-map.md](../bannerlord-animation-system-map.md)),
  and vanilla ships hundreds of type 1 and 2 clips that TAOM battles also play. Measure first: the
  profiler (plan 028) records `IsAnyAnimationLoadingFromDisk` per frame and on every hitch, beside the
  agent-tick and wait times. Two levers follow from the result, both the maintainer's decision: set type
  0 on TAOM's hot battle clips (Armory data, with unique keys), or raise the 12 MiB budget for the whole
  process (a guarded four-byte native patch).

## 7. Asset facts from the Modding Kit [yotthani]

Read by yotthani from the 1.5.3 Kit in Ghidra and checked against shipped data. Bullets marked
TAOM-verified were re-derived by TAOM; the rest are not yet.

- **Texture format follows the name suffix.** [TAOM-verified, Ghidra on the Kit's editor
  `TaleWorlds.Native.dll`, project `Win64_Shipping_wEditor-85c4d16945700701`] The image compiler
  (`rglResource_compiler::Image_compiler`, RVA `0x465570`) classifies a texture by the end of its name:
  `_atlas` (string at `0x13BC7B0`), `_d`, `_s`, `_n` (`0x13B9E9C`), `_h`, or none; an `_n` texture is
  normalised first. The compressor (`0x46A4B0`, NVIDIA Texture Tools) then picks DXT1 (DXT5 when the
  texture has alpha) for `_d`, `_s` and unsuffixed names, BC5 for `_n`, BC4 for `_h`, BC7 for `_atlas`,
  and BC6 for HDR. So an `_atlas` texture costs twice an opaque `_d` one (BC7 against DXT1), and a
  normal map not named `_n` loses BC5. Mips are generated to 1x1 (`0x468ED0`) [yotthani: Kaiser filter].
- **Texture flags** include `dont_degrade` (0x2), `dont_compress` (0x20) and `dont_delay_loading` (0x80);
  what the last two change at runtime is not reversed.
- **Module textures are not virtual-textured** (an assumption: the publish step's Granite tiler needs a
  file the Kit does not ship), so every module texture keeps its full mip chain in its package, where
  vanilla keeps only a 512 px mip tail and streams the rest.
- **Metamesh visible distance** defaults to `FLT_MAX` on a Kit import; shipped small props use 20 to 50 m,
  baskets 100 to 200, map icons 200.
- **Particle emitters** carry a min and max config that bound the Particle Detail option; an emitter is
  created only inside its bounds.
- **Mesh edit data ships, and the client reads it only on demand.** [TAOM-verified, Ghidra on the
  v1.5.3 shipping `TaleWorlds.Native.dll`] Every metamesh in a package carries two segments: render
  buffers (segment type `97f81dbb...`, the GUID at RVA `0xAE3D78`) and edit data (`5f98413d...`, at
  `0xAE3D50`). Vanilla packs both too: Native's 151 packages hold 8,567 MB of edit data against 8,107 MB
  of render buffers (decompressed sizes). The mesh loader (`0x66700`) reads only the render buffers ("Cannot read render
  buffers of mesh %s"). Edit data is read by exactly two functions: "ensure edit data" (`0x68D30`, a
  synchronous read, or a copy from a mesh sharing the geometry, or "Generating edit data from buffers,
  this may cause a performance hit" for a mesh with no package) and an asynchronous request manager
  (`0x181CD0`). Their callers are face and body generation (`produce_vertex_map_from_mesh`,
  `face_base_mesh`, `deform_keys`, skin loading), a tableau path, the managed mesh-edit operations
  (`IManagedMeshEditOperations`, the static mesh edit lock) and mesh batching (`IMetaMesh.BatchMultiMeshes`
  and `BatchMultiMeshesMultiple`, which no v1.5.3 module and no TAOM code calls). So a static scenery
  mesh's edit data stays on disk in play. TAOM's own `LockEditDataWrite` users (`BorderRenderAdapter`,
  `RoadMeshAttacher`) build new meshes and read no package data.
- **UI sprite categories marked `AlwaysLoad` stay resident; undeclared sheets never load.**
  [TAOM-verified, v1.5.3 managed decompile] `SpriteData.Load` loads every `AlwaysLoad` category at once
  (`SpriteData.cs:205-208`), a category requests sheets 1 to `SpriteSheetCount` and no others
  (`SpriteCategory.cs:56-58`), and each sheet is pinned with `SetTextureAsAlwaysValid` and
  `PreloadTexture` (`TwoDimensionEngineResourceContext.cs:8-19`). A sheet's format is whatever its Kit
  texture asset says, so an uncompressed 4096 px sheet costs 64 MB pinned (85 MB with mips).
- **The client never cooks package physics shapes**; it reads their stored cook data.

## 8. Campaign map [yotthani]

Vanilla updates every settlement nameplate each frame on worker threads (`UpdateNameplateMT` ->
`RefreshDynamicProperties(forceUpdate: false)`); only the rare forced refresh runs on the main thread. A
patch there runs per settlement per frame off the main thread. In 1.5.x a settlement's visibility is a
flag set on events, no longer recomputed every tick.

## How to re-verify

| Claim | Command |
|---|---|
| A native method's body | `python tools/native_decompile.py --engine-method IMBMission.GetNearbyAgentsAux` |
| A native function by address | `python tools/native_decompile.py --rva 0x27DAC --callers 1` |
| An import's callers | the IAT slot from `pefile`, then `python tools/native_sig_author.py xref <slot rva>` |
| A class's vtable | `python tools/native_sig_author.py vtable rglAnimation_memory_manager_task` |
| A managed body | `pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Mission` |
