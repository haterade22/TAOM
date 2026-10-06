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
- **The native `Mission.Tick` runs before the managed `Mission.OnTick`**: `MissionState.TickMissionAux` calls
  it first. Inside it, in order: the managed `OnPreTick` callback, the agent jobs (combat tick, AI, movement,
  the melee sweep), then the scene tick, which starts the particle simulation task, runs
  `ManagedScriptHolder.TickComponents` and the native scene objects on the main thread, and then **waits for
  the particle task**. In the profiled 500 against 500 battle that wait was 5.9 s of 45 s (about 2.7 ms a
  frame). (yotthani, MithrilForge `docs/perf-audit/particle-sim-wait.md` and
  `docs/perf-audit/profile_s3d_functions.md` section 3, decompile and profile v1.5.3; v1.5.4: not re-checked.)
- **Particle simulation has no visibility or distance gate.** Every active emitter is stepped every step;
  visibility, camera distance (emitter LOD) and the particle quality option gate only emission. Emitters are
  split across workers in chunks of 16, so one emitter holding most particles runs on one thread. The step is a
  fixed 1/60 s and the step count per frame is uncapped, so a hitch is followed by a particle-heavy frame (a
  0.5 s hitch means 30 steps at once; no cap in native code, and whether C# caps the frame time first is not
checked). (yotthani, MithrilForge `docs/perf-audit/particle-sim-wait.md`,
  decompile v1.5.3; v1.5.4: not re-checked.)
- **Later in the frame the main thread waits on particle render data**, which grows with the particle count
  (roughly 0.06 ms per 1,000 particles in one Black Gate run, an estimate from one battle). On the Black Gate
  ash that was about 2.7 ms of simulation plus about 7 ms of render data per frame. TAOM's case is #738.
  (yotthani, MithrilForge `docs/perf-audit/agent-render-items.md` sections 0 and 1.5, profile v1.5.3; v1.5.4:
  not re-checked.)
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

`Mission.RayCastForClosestAgentsLimbs` (RVA `0x6F2D60` on v1.5.3, `0x6F2FF0` on v1.5.4 in TAOM's engine-method
map) costs about 65 to 78
microseconds per call with 1,600 agents, tests the limbs of only the agent whose origin is nearest among
those whose box the ray touches, and reports nothing if that agent's capsules are missed even when a body
further along is cut. Its agent loop has no early exit: every agent with visuals pays a root-bone
spinlock, a matrix inverse and a box test (about 50 ns each), whatever the ray's length, thickness or
excluded index, and no dead, mount or team filter applies. Of the chosen agent's capsules (at most 64)
the first hit in list order wins, not the nearest. Vanilla's only caller is
`MissionMainAgentInteractionComponent.FocusTick`, once per frame, to focus fallen bodies. (yotthani,
MithrilForge `docs/perf-audit/limb-ray-given-agent.md`, decompile v1.5.3; v1.5.4: the filter, the
first-hit rule and the 64-capsule buffer confirmed via native_decompile, the 50 ns and the caller not
re-checked.)

`RayCastForGivenAgentsLimbs` (`0x6F2BA0`; `0x6F2E30` on v1.5.4) is broken in the engine: its native
wrapper never calls the capsule builder (`0x681240`) that the closest variant calls, and reads the
capsule count from uninitialised stack, so its hit, distance and bone index are stack garbage. It never
null-checks the agent's visuals (`agent+0x880`) either, and vanilla never calls it. A garbage bone then
reaches `MBAgentVisuals.GetBoneTypeData`, which indexes `bone * 0x1B0` with no range check and reads
address 0 when the byte it finds is no known bone type: the likely site of the access violation in the damage
call (upstream did not isolate it, since DualWield catches every site in one handler).
(yotthani, MithrilForge `docs/perf-audit/limb-ray-given-agent.md`, decompile v1.5.3; v1.5.4: confirmed
via native_decompile, except "vanilla never calls it", not re-checked.) Budget the cost per call before
giving creature contact limb rays ([scripted-melee-strikes.md](../scripted-melee-strikes.md) section 9).

## 6. Animation clip residency: on-demand clips lock, block and get evicted

[TAOM-verified, Ghidra, 2026-10-02] The whole chain, on the client binary:

| Piece | Where | What it does |
|---|---|---|
| Clip data record | initialiser `0x473090` | Stores the clip's Loading Type at `+0x194`. Type 0: the data pointer (`+0x88`) and size (`+0x80`) are set at once, no loader. Type 1: a short resident piece goes to `+0x90` and a loader callback (`+0x98`) is installed for the full data. Type 2: a loader callback is installed. Reader count `+0xD8`, state `+0xE0` (0 unloaded, 1 loading, 2 loaded; the initialiser stores 0 in both, so an on-demand clip starts unloaded), a mutex at `+0x130` and a condition variable at `+0xE8` |
| Acquire | `0x474140` | Type 0: returns the data pointer, nothing else. Type 1 or 2: marks the clip used (`0x473250` writes a `QueryPerformanceCounter` timestamp to `+0x84`); spins while the reader count is `-1` (being evicted), then raises it with a compare-and-swap loop; if the state is 0 it swaps it to 1 and starts the loader (`0x474660`); a type 1 clip sampled inside its resident short piece returns that piece; otherwise the calling thread locks the clip's mutex and **waits on its condition variable until the data is loaded**. The one-bone sampler (`0x473630`) always calls it with header-only off, so the pair is paid per bone sample, per clip, per agent (yotthani, MithrilForge `docs/perf-audit/clip-reader-lock.md`, decompile v1.5.3; v1.5.4: not re-checked) |
| Release | the accessors, for example `0x473320` and `0x4733A0` | Every read of a type 1 or 2 clip's data ends with an atomic decrement of the reader count; type 0 skips it. Atomic operations on `+0xD8` appear at 28 sites, among them one sampler function with eight (`0x49D960`). **Conflict, unresolved:** yotthani lists `0x473320` and `0x4733A0` as header-only reads (the bone count) that take no lock, which contradicts this row; his own address table adds "except mode 2" for them (MithrilForge `docs/perf-audit/clip-reader-lock.md`, decompile v1.5.3; v1.5.4: not re-checked) |
| Eviction | `0x21DEA0` | Drains a queue of loaded on-demand clips and sorts them. It measures the excess of the global loaded-bytes counter over **12,582,912 bytes (12 MiB, the float at `0xB2E2DC`, read by this function only)** once, at its start, then walks the sorted list from its end and frees clips in state 2 (loaded) whose reader count it can swap from 0 to `-1` (data freed, state back to 0, the counter lowered), until the bytes freed reach that excess or the list runs out. It never re-reads the counter, so clips in use and loads that land during the pass can leave the total above 12 MiB. Every return clears the pass flag `0xDABE44` |
| Probe | `IMBAnimation.IsAnyAnimationLoadingFromDisk` (`0x6EAAE0`, managed `MBAnimation.IsAnyAnimationLoadingFromDisk()`) | Walks the record pointer list at `0xDB00C8` from its start and returns true at the first record in state 1 (loading); when none is loading it reads every entry. It takes no lock and checks no thread: it takes the count once at entry from the list's end and begin pointers (`0xDB00D0`, `0xDB00C8`) and re-reads the begin pointer for every entry. Whether the engine can grow that list or free a record while a mission thread calls this was not traced (**UNVERIFIED**) |
| Load-time loaders | bulk `0x591390`, single `0x592400` | Read clip data at asset load ("Unable to read animation clip data for %s"); the bulk loader fans out jobs on the engine job manager and helps run them |

What it means:

- A clip at Loading Type 1 or 2 costs an atomic increment and decrement of one shared counter on every
  data access, which means per bone sample, per clip, per agent, so every sampling worker in the parallel
  agent tick contends on the same cache line for a popular clip. Each acquire also writes the timestamp at
  `+0x84`, in the same 64-byte line (`+0x80` to `+0xBF`) as the data pointer at `+0x88`, so every
  sampling worker invalidates the line the others read that pointer from; this cache-line cost is an
  inference, not measured (yotthani, MithrilForge `docs/perf-audit/clip-reader-lock.md`, decompile
  v1.5.3; v1.5.4: not re-checked).
- The first sample after a clip is unloaded **blocks that worker** until the clip has loaded. The
  parallel agent tick then finishes late, and the next frame's `WaitTickCompletion` (section 1) holds the
  main thread: a load shows up as a frame spike, not as a worker-only cost.
- On-demand clip data can grow to 15 MiB before a load sets the pass flag (below), and a pass frees
  only the excess over 12 MiB that it measured at its start. A battle whose working set of type 1 and 2
  clips is larger evicts clips nobody is reading at that moment and loads them again on their next use.
- Type 0 clips do none of this. [yotthani] In one profile of a 500 against 500 DualWield battle the
  acquire function took 8.3 s of exclusive CPU (2.6 %). That is the whole function over every clip in
  that profile; that most of it comes from DualWield's 292 mirror-pack clips, all at type 2, is very
  likely, not measured per clip (MithrilForge `docs/perf-audit/profile_s3d_functions.md` and
  `docs/perf-audit/clip-reader-lock.md`, decompile v1.5.3; v1.5.4: not re-checked). A second 45 s
  profile with a type 0 test pack, on another battle with the same troops, measured 0.14 s; its own
  author reads the two battles' totals as a direction only (MithrilForge `docs/engine/perf.md`).
- **[yotthani, decompile-verified and seen in game]** Resident clip data is shared through a cache
  (`0x5927B0`, table `0xDABE50`) keyed on `AnimationGUID ^ min(Source1, end) ^ (mode == 1)`, so a
  resident clone that keeps its template's guid and window plays the template's motion. The mode bit
  keeps type 0 and type 1 clips from ever sharing data; type 2 bypasses the cache (MithrilForge
  `docs/perf-audit/clip-reader-lock.md`, decompile v1.5.3; v1.5.4: not re-checked). Give each resident
  clone its own key before setting type 0.
- TAOM consequence: 24 of the 30 bound hill-troll release and blocked clips and the elephant's 8 attack
  clips sit at Loading Type 2 ([bannerlord-animation-system-map.md](../bannerlord-animation-system-map.md)),
  and vanilla ships hundreds of type 1 and 2 clips that TAOM battles also play: by name group, type 2
  holds 63 `release`, 102 `quick`, 44 `blocked` and 97 `conversation` clips, while every `run`, `crouch`,
  `defend`, `ready` and `strike` clip is type 0 (`walk` has one at type 2) (yotthani, MithrilForge
  `docs/perf-audit/clip-reader-lock.md`, package census v1.5.3; v1.5.4: not re-checked). Measure first: plan 028's
  profiler records the wait and agent-tick times of every hitch, and plan 041 adds an
  `IsAnyAnimationLoadingFromDisk` sample per frame, flagged on each hitch's `[HitchDetail]` line and taken
  in a prefix on `Mission.OnPreTick` before its `WaitTickCompletion`, so it can see a load still blocking
  the previous agent tick. Three levers follow from the result, all the maintainer's decision: set type 0
  on TAOM's hot battle clips (Armory data, with unique keys), raise the 12 MiB budget for the whole
  process (a guarded four-byte native patch), or patch the native sampler to count readers once per clip
  (in `0x472660`) instead of once per bone, the only lever that reaches vanilla's own type 1 and 2 clips
  (yotthani, MithrilForge `docs/perf-audit/clip-reader-lock.md`, decompile v1.5.3; v1.5.4: not
  re-checked).
- **[TAOM-verified, Ghidra and a disp32 scan, 2026-10-02]** Among rip-relative references, the counter
  at `0xDABE40` is written only by `lock xadd` at `0x591319` (in `0x5911A0`, adding a clip's size field
  `+0x80` after a load) and at `0x21E0EF` (the eviction pass, adding the negated freed bytes), and read
  at `0x21E00F` (the eviction pass) and `0x830A3` (in `0x82BE0`, as a float). After its add, `0x5911A0`
  compares the total with `0xF00000` (15 MiB, at `0x591327`) and above it sets a once-flag
  (`0xDABE44`) with `lock cmpxchg` and hands an object to `0x44400`; that this schedules the eviction
  pass is an inference, not traced, though the pass clears the same flag at every return. The
  `[AnimMem]` probe logs that counter against the budget once a second, locating both by signature,
  never by offset ([mission-perf-heartbeat.md](../../features/mission-perf-heartbeat.md)). That is an
  occupancy snapshot, not a timer of loads. It shows pressure on the budget. It does not show a clip's
  first load, which blocks the worker that samples it whatever the total (the Acquire row: a clip in
  state 0 is loaded on its first use), and its loading flag is read in `OnMissionTick`, after the
  previous parallel agent tick has finished (section 1), so a load that delayed that tick has already
  ended. A low total therefore does not rule out setting type 0 on hot clips.

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
  created only inside its bounds. That and the other gates act on creation and emission only: a created
  emitter is simulated every step, seen or not (section 1; yotthani, MithrilForge
  `docs/perf-audit/particle-sim-wait.md` section 5, decompile v1.5.3; v1.5.4: not re-checked).
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

## 9. What an agent read costs [yotthani]

(MithrilForge `docs/perf-audit/dw-left-strike-cost.md` and `docs/perf-audit/profile_s3d_functions.md`, v1.5.3
managed decompile and profile; v1.5.4: not re-checked.)

- **Plain pointer reads, no engine call:** `Agent.Position`, `IsActive()`, `State`, `IsHuman`, `Controller`,
  `GetPrimaryWieldedItemIndex` and `GetOffhandWieldedItemIndex`, `WieldedWeapon`, `GetCurrentAction(channel)`,
  `MountAgent`, `Health` and `Index` read through pointers cached at agent creation (`AgentHelper`).
- **Engine calls:** `GetCurrentActionType`, `GetCurrentActionProgress`, `LookDirection`, `Frame`, `Velocity`
  (two calls), `IsEnemyOf`, `GetEyeGlobalPosition`, `MBAgentVisuals.GetSkeleton`, `Skeleton.GetBoneEntitialFrame`,
  `Mission.GetNearbyAgents` (section 4) and the raycasts (section 5).
- **Costs, derived from one measurement line, not timed per call:** a simple engine call about 0.1 to 0.4 µs;
  `GetSkeleton` about 1.5 to 2 µs, because each call builds a new `Skeleton` wrapper with a finalizer, a lock and
  a `GCHandle`. One melee hit through vanilla's `MeleeHitCallback` chain costs about 128 µs, most of it vanilla's
  own hit handling and every mod's `OnAgentHit` (that split is estimated).
- **`Mission.FindAgentWithIndex` is O(1)** natively (an index check and a handle read) and drew 0 samples in the
  profile.
- **Where busy CPU goes** in that 500 against 500 battle (82 % of it native): about 60 % presentation (agent render items 18.6 %,
  render commands 15.2 %, skeleton and animation 15.1 %, particles 8.0 %, culling 2.8 %), against agent AI 4.2 %,
  movement and physics 6.4 % and the melee sweep 1.2 %. These are CPU shares, not frame-time shares.

The reverse case, a perf change costed by call names instead of bodies, is the lesson "A perf commit's win names
the cost it removes" in [adapters-taleworlds-api.md](../../reviews/lessons/adapters-taleworlds-api.md).

## How to re-verify

| Claim | Command |
|---|---|
| A native method's body | `python tools/native_decompile.py --engine-method IMBMission.GetNearbyAgentsAux` |
| A native function by address | `python tools/native_decompile.py --rva 0x27DAC --callers 1` |
| An import's callers | the IAT slot from `pefile`, then `python tools/native_sig_author.py xref <slot rva>` |
| A class's vtable | `python tools/native_sig_author.py vtable rglAnimation_memory_manager_task` |
| A managed body | `pwsh tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Mission` |
