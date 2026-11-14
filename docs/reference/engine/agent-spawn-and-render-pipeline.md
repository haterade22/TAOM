# Bannerlord agent spawn → render pipeline (Phase 1)

> **One process, traced end-to-end from the decompile** (`E:\Decompiled_Bannerlord\MountAndBlade\…\Mission.cs` +
> `Agent.cs` + `IMBAgent.cs`, v1.4.5). This is the pipeline that turns a troop/creature into a rendered, fighting
> agent — and it's the exact path the **giant spider AccessViolates in**. Documented so no future session
> re-traces it. Part of the phased engine study (depth-first, one process at a time). Companions:
> [adod-beasts-architecture-and-taom-port.md](../adod-beasts-architecture-and-taom-port.md) (the wolf uses this
> pipeline), [spider.md](../../features/spider.md) + its RCA, [bannerlord-engine-and-toolchain.md](../bannerlord-engine-and-toolchain.md).

## WHAT it is

Every in-battle agent (human troop, horse, wolf, elephant, spider) is created by one of two public entry points on
`Mission`, both of which funnel through a shared **create → build → preload-render** chain. The chain reads the
agent's **`Monster`** for its skeleton/animation/physics rig, adds its meshes, and hands the result to the **native
engine** to upload for rendering. Understanding it explains creature spawning, the humanoid-skin-vs-creature
distinction, and the spider's render crash.

## HOW it works — the two entry points + the shared chain

### Entry point A — `Mission.SpawnAgent(AgentBuildData)` — humanoid troops (Mission.cs:4074)
The roster/character path. `SpawnTroop` (Mission.cs:4418) builds an `AgentBuildData` from a `BasicCharacterObject`
(team, banner, colors, formation, mount key, controller) and calls `SpawnAgent`. Creation type =
`FromRoster`/`FromCharacterObj`. If the troop spawns with a horse, `SpawnAgent` internally also builds the mount via
`CreateHorseAgentFromRosterElements` (Mission.cs:4275). **TAOM:** this is where `Patch23_BannerColorPersistence` runs. The June 2026 spider spawn intercept (`Patch45`, a
`Mission.SpawnAgent` prefix) was deleted on 2026-06-10; the spider is now a ridden mount spawned by the vanilla cavalry path.

### Entry point B — `Mission.SpawnMonster(mount, harness, pos, dir)` — riderless mounts/creatures (Mission.cs:4394/4399)
The "free creature" path. Steps (Mission.cs:4399-4416):
1. `CreateHorseAgentFromRosterElements(mount, harness, …)` (Mission.cs:4525) →
   - reads `mount.Item.HorseComponent` (Mission.cs:4527);
   - **`CreateAgent(horseComponent.Monster, isFemale:false, …, Agent.CreationType.FromHorseObj, …)`** (Mission.cs:4528) — the agent is created from the **Monster** with creation type **`FromHorseObj`**;
   - `SetInitialFrame`, health, **`SetMountInitialValues(mount name, mountKey)`** (Mission.cs:4533).
2. Build a spawn `Equipment` with the mount item at `EquipmentIndex.ArmorItemEndSlot` + harness at `HorseHarness`; `agent.InitializeSpawnEquipment(...)` (Mission.cs:4402-4407).
3. **`BuildAgent(agent, null)`** (Mission.cs:4414).

**This is the path the working ADOD_Beasts wolf uses (public `SpawnMonster`).** In June 2026 TAOM's detached spider instead
*reflected* `CreateAgent`+`BuildAgent` directly (`SpiderDetachedAgentSpawner`, since deleted), a hand-rolled
copy of this exact chain. **The chain is decompiled-identical; the reflection was not the bug** (see §"the spider AV").

### `CreateAgent` — the Monster → agent step (Mission.cs:4040)
```
AnimationSystemData anim = monster.FillAnimationSystemData(stepSize, false, isFemale);  // skeleton + action set
AgentCapsuleData caps   = monster.FillCapsuleData();                                     // physics capsule
AgentSpawnData spawn    = monster.FillSpawnData(null);
AgentCreationResult r   = CreateAgentInternal(monster.Flags, …, ref spawn, ref caps, ref anim, …);  // NATIVE
Agent agent = new Agent(this, r, creationType, monster, …);
agent.Character = characterObject;   // null for FromHorseObj creatures
```
**The `Monster` is the source of the rig** — skeleton, action set (animations), capsule. `CreateAgentInternal` is
native. `creationType` is carried on the agent and decides the visual build (next).

### `BuildAgent` — build → equip → **preload-render** (Mission.cs:4007)
```
agent.Build(agentBuildData);                                   // native build
// scale from mount item BodyLength (Mission.cs:4014-4020)
agent.EquipItemsFromSpawnEquipment(…);                         // ← adds meshes (see below)
agent.InitializeAgentRecord();
agent.AgentVisuals.BatchLastLodMeshes();
agent.PreloadForRendering();                                   // ← Mission.cs:4025 — the NATIVE GPU preload
// set initial action channel; InitializeComponents; add to _activeAgents/_allAgents
```

### `EquipItemsFromSpawnEquipment` — the creation-type SWITCH (Agent.cs:4529) ⭐
```
switch (_creationType) {
  case FromRoster:
  case FromCharacterObj:
     for each weapon slot → WeaponEquipped(...)   // equip weapons
     AddSkinMeshes(...)                           // ← humanoid skin (Agent.cs:4560)
     break;
  // NO case for FromHorseObj → it equips NOTHING here and SKIPS AddSkinMeshes
}
UpdateAgentProperties();
```
**This is the load-bearing fact:** a **`FromHorseObj`** agent gets **no weapons and no `AddSkinMeshes`**. Its only
visual is the **mount mesh** (loaded natively from the mount item's `HorseComponent` mesh during `CreateAgent`/
`Build`/`SetMountInitialValues`).

### `AddSkinMeshes` — the humanoid skin build (Agent.cs:5405)
Builds the human face/body/hair/beard from `Character.Race` + `BodyProperties` + the equipment skin mask
(`SpawnEquipment.GetSkinMeshesMask()`). **Applying this to a creature skeleton is the original swap-approach crash**
— which is precisely why creatures use `FromHorseObj` to skip it.

### `PreloadForRendering` — the native boundary (Agent.cs:4923) ⭐
`PreloadForRendering()` → `PreloadForRenderingAux()` (Agent.cs:5189) → `MBAPI.IMBAgent.PreloadForRendering(GetPtr())`
→ native **`[EngineMethod("preload_for_rendering")]`** (IMBAgent.cs:533). **Pure native call — the GPU upload of the
agent's skinned meshes happens in `TaleWorlds.Native.dll`. This is where the bone-palette overflow AVs; nothing in
managed code can see or guard inside it.**

## The `Agent.CreationType` matrix (why it matters)

| CreationType | Used by | Weapons equipped? | `AddSkinMeshes` (humanoid skin)? |
|---|---|---|---|
| `FromRoster` / `FromCharacterObj` | `SpawnAgent`/`SpawnTroop` (human troops) | yes | **yes** |
| `FromHorseObj` | `SpawnMonster`/`CreateHorseAgentFromRosterElements` (mounts + riderless creatures) | no | **no** (skipped — visual is the mount mesh) |

So: humans = skin + weapons; mounts/creatures = mount mesh only, no skin. Picking `FromHorseObj` for a custom
creature is the engine-supported way to render a non-humanoid body with no humanoid skin contamination.

## WHY the spider AccessViolates here (the payoff)

1. The spider spawns `FromHorseObj` (correctly — to skip `AddSkinMeshes` / the humanoid skin). Confirmed: that path
   adds no skin; the spider's visual is its **mount mesh** (the `spider_mount_a` item's mesh: the June single mesh at
   the time of this trace, KEYforce's whole `sk_spider_forest_a1` since 2026-09-29).
2. The AV is in **native `preload_for_rendering`** (Agent.cs:4923→5189→IMBAgent.cs:533) — the GPU upload of that
   mesh's skinned vertices. **CORRECTION (2026-06-13): the original "per-mesh bone palette, ~40 bones/draw" cause is
   FALSE.** No such per-mesh cap exists — the elephant renders as ONE mesh skinned to 59 active bones (chariot 54).
   The only bone cap is `Skeleton.MaxBoneCount = 64` (skeleton-total, not per-mesh; author ≤63). The spider's
   `preload` AV is real but its **true cause is unestablished** — do not attribute it to a per-mesh bone count.
   See the [lesson](../../reviews/lessons/animation-skeleton.md).
3. **The spawn *path* is not the cause.** ADOD_Beasts's wolf uses the public `SpawnMonster` chain above and renders fine;
   the June 2026 detached spider reflected the *same* `CreateAgent`+`BuildAgent` chain. Both reach the same native `preload`. The fix lives in
   the mesh asset, not the spawn code — but NOT "re-author to ≤40 bones" (refuted above); keep the body in one mesh ≤63
   bones. The RCA's recommended cheapest experiment (the wolf's public `SpawnMonster` + single un-split
   mesh) follows directly from this trace.
4. **How it ended.** The spider became a ridden mount on 2026-06-11 (its tableau and mission AVs were a missing
   `quad_movement` clip tag, [spider.md](../../features/spider.md)), shipped on L/R split halves, and on 2026-09-29
   KEYforce's whole meshes on the same skeleton loaded and rendered in battle with no `preload` AV. So the June AV
   belonged to that mesh or its package; its cause stays unestablished.

## TAOM relevance map

- `SpawnAgent` (Entry A): every human troop, and the mounts a cavalry troop brings (the spider is one); `Patch23`.
- `SpawnMonster` (Entry B): riderless creatures (the ADOD_Beasts wolf; TAOM's creature bandits, `Main/Features/CreatureBandits`).
- `EquipItemsFromSpawnEquipment` switch — the reason a creature must be `FromHorseObj` (no humanoid skin).
- `PreloadForRendering` (native) — the render AV; mesh-side fix only.
- Mount-item mesh — set via the `HorseComponent` on the item at `ArmorItemEndSlot` (the spider/elephant mount item).

## The native boundary (what this trace can't see)

`CreateAgentInternal`, `agent.Build`, `AddSkinMeshes`→`AgentVisuals.AddSkinMeshes`, and `PreloadForRendering` all
cross into `TaleWorlds.Native.dll` (the bone-palette cap, the actual GPU mesh upload, the AV). The managed pipeline
*orchestrates* (pick Monster, pick creation type, equip, then call preload); the *rendering* is native — so the
spider's fix lives in the **mesh asset** (tpac), not in C#.

## After spawn: pose, LOD and shadows each frame (v1.5.3) [yotthani]

Read by yotthani from the v1.5.3 client in Ghidra and a 45 s Very Sleepy profile of a 500 against 500
battle. Every bullet names its MithrilForge source and its v1.5.4 status.

**Pose (animation LOD).** (MithrilForge `docs/perf-audit/profile_s3d_functions.md` section 4 and
`docs/engine/offscreen-pose.md`, decompile and profile v1.5.3; v1.5.4: the option string
`animation_sampling_quality` is still present (native_decompile `--string`), the rest not re-checked.)
- **A skeleton is posed only while its agent is visible** in at least one view, shadow views included; a rider
  and its mount count as one. A non-visible agent only advances its animation time and action state, plus a
  timer-based forced update (`do_timer_based_skeleton_forced_updates`, on by default) when its timer runs out.
  Between those updates `Skeleton.GetBoneEntitialFrame` returns the same frame tick after tick while
  `GetCurrentActionProgress` keeps running.
- **The rate falls with camera distance** under the `animation_sampling_quality` option (default 1): at 1,
  144 / 108 / 45 / 29 / 21 Hz at 0 / 10 / 30 / 50 / 100 m with a floor of 18 Hz; at 0, 60 / 46 / 21 / 15 / 11 Hz
  with a floor of 10 Hz; at 2, every visible agent every frame.
- **`MBAgent.GetVisualPosition` forces a pose evaluation**, and vanilla's melee sweep forces the attacker's pose
  before it reads the blade, so vanilla melee is unaffected.
- **The lever for a mod that reads bones itself** is `Skeleton.ForceUpdateBoneFrames()`: the same evaluation the
  sweep forces, and a no-op when the skeleton was posed this frame. Calling it only when a bone frame equals the
  previous tick's keeps the cost to off-screen agents. TAOM's open case is #739 (warg bites, elephant howdah).

**Agent LOD.** (MithrilForge `docs/perf-audit/agent-render-items.md` summary, sections 2.2, 3 and 4, decompile and profile
v1.5.3; v1.5.4: `SetAgentLodMakeZeroOrMax` confirmed via native_decompile, the rest not re-checked.)
- **One LOD per agent, from its distance to the main camera**, not per view, with thresholds set by the
  `character_detail` option (at 2: LOD 1 to 5 start at 8.3, 13.8, 19.3, 28.3 and 37.3 m).
- **A lower LOD saves triangles and skinning, not render items:** every part stays in the list with that LOD's
  mesh, so the item count follows the number of parts and materials.
- **Never call `MBAgentVisuals.SetAgentLodZeroOrMax(false)`.** The native setter stores `(makeZero ^ 1) << 3`, so
  `false` writes 8, one past the eight LOD slots (0 to 7) the renderer indexes. Vanilla calls it only with `true`.
- **`GameEntity.SetEnforcedMaximumLodLevel` does not reach agents:** only the LOD path of ordinary entities reads
  it; the agent renderer does not.
- **Agent render items cost CPU, not frame time** on the profiled battle: they start in `PreDisplay`, take about
  2 ms of wall time per frame on about 13 workers, and the main thread sleeps on them for about 0.13 ms per frame.
  Halving them would save up to about 9 % of busy CPU and no frame time there (the frame-time half is upstream's assumption).

**Distant shadows.** (MithrilForge `docs/perf-audit/agent-render-items.md` section 3.5, decompile v1.5.3;
v1.5.4: the skeleton-name test confirmed via native_decompile `--string human_shadow_mesh`.)
- Full meshes go only into shadow cascades 0 and 1. Cascades 2 and 3 draw one stand-in mesh per agent, chosen by
  skeleton name: `human_shadow_mesh` when the name contains "human", `horse_shadow_mesh.lod3` when it contains
  "horse", otherwise none. With the cascade multiplier at 1.0 the switch is at 10 m times an engine factor yotthani did not pin down.
- [unverified in game] So a creature on its own skeleton may cast no far shadow at all, and a mount on
  `horse_skeleton` may cast a horse silhouette.

## Agent-less figures [yotthani]

A body with a skeleton and meshes but no `Agent` can be built and animated by hand. Everything below is code reading of yotthani, `yotthani/bannerlord` `HoN/PerfProbe/Integration/CrowdProxy.cs`@784fef93 (and `CrowdLab.cs` at the same commit) plus the dated notes in its comments, from yotthani's crowd lab (a separate proxy product that TAOM has not adopted). **Engine version:** not stated in the source (the module's `SubModule.xml` declares Native v1.5.3; the notes are dated 10 to 12 October 2026). **Mod list:** not stated. Each note is an observation by yotthani in game, not re-measured by TAOM.

| Fact | Evidence |
|---|---|
| `CreateAgentSkeleton(this GameEntity, skeletonName, isHumanoid: true, actionSet, usage, monster)` works on an empty entity made with `GameEntity.CreateEmpty(scene, false, false, false)`; no agent is needed. [TAOM-verified, v1.5.7] The two skeleton builders and `FillAnimationSystemData` are extension methods; their classes and signatures are in [animation-binding-and-playback.md](animation-binding-and-playback.md), paragraph "Step size and gait speed". `MBAgentVisuals` is `TaleWorlds.MountAndBlade.MBAgentVisuals`. `CreateEmpty` is `GameEntity.CreateEmpty(Scene, bool isModifiableFromEditor = true, bool createPhysics = true, bool callScriptCallbacks = true)`. `CreateAgentSkeleton` uses step size 1.0 (same doc). Every other call in the lab's chain also exists on v1.5.7: `MBAgentVisuals.CreateAgentVisuals(Scene, string, Vec3)`, `SetEntity`, `SetSkeleton`, `SetVisible`, `SetAgentLocalSpeed(Vec2)`, `Tick(MBAgentVisuals, float, bool, float)`, `Reset()`, `Agent.AgentVisuals` and `Agent.SetRenderCheckEnabled(bool)` (decompile of the installed v1.5.7 DLLs; vanilla's `AnimationPoint` builds a skeleton the same way, and `MobilePartyVisual` ticks the campaign map's visuals through `AgentVisuals.Tick`) | `CrowdProxy.cs` 908-911, 971 |
| A mesh added to the skeleton after the body was handed over to the engine's visuals object was not drawn ("weapons fought in the air near the player, no bodies"), so every mesh goes on first | comment at 920-921, seen in game 10.10.2026 |
| The engine's own way to move a body that is no agent is an `MBAgentVisuals` made with `CreateAgentVisuals`, then `SetEntity` and `SetSkeleton`, told the local speed and ticked (the campaign map's parties, the editor's `CharacterDebugSpawner`) | 949-955 and 1137-1145 |
| The lab animates such a skeleton itself; whether the engine would is not shown (one of the lab's test modes leaves a raw clip "to the engine", `CrowdLab.cs` 251, and its result is not recorded). The lab sets the action progress itself (a walk clip "may be driven by movement, which a plain skeleton does not have", a hedge in `CrowdLab.cs` 250-251), ticks the action channels while one action blends into the next (`CrowdProxy.cs` 1130-1136), and calls `TickAnimations` or `TickAnimationsAndForceUpdate` (1146-1147). A figure not ticked within 0.35 s of an action change stayed between the two actions | 1130-1148 |
| A mesh cannot be added to a body the visuals object already holds, so adding one (a head) means giving the whole figure up with `Visuals.Reset()` and building it anew | comment at 966-967, code 968 |
| Give a body held by a visuals object up through `Reset()`, never by removing its entity: removing the entity under it crashed the game some time later (10.10.2026, 23:45). The lab's teardown also detaches borrowed skeletons before removing entities, so an entity does not take a master's skeleton with it | `CrowdProxy.cs` 1776-1779; `CrowdLab.cs` 835-837 |
| Five extra draw calls for the game's own weapon meshes cost 1.8 ms a frame with 2,000 figures (measured 10.10.2026). The figure is a proxy; this is not an agent cost | `CrowdProxy.cs` 683-685 |

**A figure mirrors one real agent, so the method cannot pass the agent cap.** In `CrowdProxy` every figure holds its
real `Agent` (`Figure.Agent`, `CrowdProxy.cs` 92), the selection walks `Mission.Agents` (1674), and the figure is dropped
the moment `!a.IsActive()` (1772-1784). The real soldier keeps its movement, AI, hits and death; the figure only changes
how it is drawn (class summary, 13-17; "the soldier fights for real in the engine", 665-669). The 2,040 slots
(`mission-frame-threads-and-native-costs.md` section 3) therefore stay the ceiling. The only 10,000 drawn in yotthani's
material is the lab's flip-book test, K still pose meshes per figure ("10000 figures +2.9 ms at 5120x2160", `docs/engine/crowd-rendering.md`@ea7a7bfb
line 58, measured, one PC); `CrowdLab.cs` clamps its stage count to 20,000 (line 70). TAOM's reading, from a search of
`yotthani/bannerlord`@c45d8374 for in-game code (not re-run by TAOM): no pushed code gives an agent-less unit its own
position, hit or death. `CrowdProxy.cs`@c45d8374 is byte-identical to the @784fef93 cited above, so those line numbers hold.

**Cost per 1000 figures** (yotthani, `yotthani/MithrilForge` `docs/engine/crowd-rendering.md`@ea7a7bfb lines 11-13, 20-26, 28-42;
measured in game 1.5.5 with TAOM 2.0.34 on one PC: Core Ultra 7 265KF, RTX 5070 Ti, 2560x1080, no v-sync; the run logs are not
in that repo; yotthani estimates a typical PC at 55 to 70 % of these frame rates, "assumption, not measured"):

| What is drawn | Cost per 1000 | Skeleton-buffer entries |
|---|---|---|
| Engine soldier, fully armoured (through `Mission.AgentVisualCreator`) | 8.5 ms | 28 each |
| The same, naked | 4.5 ms | none |
| Armour alone / weapons alone | about 3.3 ms / about 0.8 ms | |
| Baked one-mesh figure on a plain skeleton | 0.3 to 0.4 ms | none |
| "Plain" figure (the game's own meshes on a plain skeleton) | as much as the engine soldier | |

A hidden agent (`SetRenderCheckEnabled(false)` plus `AgentVisuals.SetVisible(false)`) takes no skeleton-buffer entry;
in every figure mode the buffer stayed at 40 to 100 of 2,340 where real soldiers filled it (lines 41-42). Entries per unit
type, and the 2,000-soldier split between simulation and drawing, are in
[mission-frame-threads-and-native-costs.md](mission-frame-threads-and-native-costs.md) sections 9 and 10. Gains in whole
battles: 1000 v 1000 uruks 61-64 to 108-117 fps and 700 v 700 (Isengard against Erebor) 87 to 128-130 fps with the
whole army in view; in a ground-level melee nothing, because the engine poses only the 500 to 1,100 skeletons it draws.

**Tried and unusable** (same file, lines 44-60, same PC; measured, except the missing instancing API, which is a code reading): no engine API takes a mesh and a list of positions
(no instancing); `entity.Skeleton = otherSkeleton` is accepted and does not crash, but the sharing entities are not drawn;
`Mesh.MorphTime` costs about 70 us a call (1000 figures at 10 steps a second add 70 ms, and 10,000 meshes with their own
morph time hang the game); merging at run time with `Mesh.AddMesh(mesh, frame)` took 67 s for 200 figures of 84 meshes and
drew no frame afterwards; forcing a coarser LOD or leaving out weapons does not make a plain figure cheaper.

**Small facts from the lab** (same file, measured unless noted): `MBAgentVisuals.Tick(null, dt, moving, speed)` and no
ticking give the same forward pace, while `Tick(null, dt, false, 0)` steps at a constant 1.07 s whatever the speed
(line 327). Team colour is `Mesh.Color` and `Mesh.Color2` per mesh plus the material flag
`use_double_colormap_with_mask_texture`; `MetaMesh.SetFactor1/2` has no effect (103-106). `Agent.ActionCodeType` `Idle`
(35) and `Guard` (36) are not "Other" (280). Bone frames read from managed code are not exact per frame, and real skeletons
beyond about 20 m are updated less often (336-338; it agrees with the pose rule at the top of this page).

**Dated crash, freeze and fault notes** (yotthani, comments in `CrowdProxy.cs`@c45d8374 and `CrowdLab.cs`, seen in game in
October 2026; engine version and mod list not stated; observations, none re-run by TAOM):

| Note | Where |
|---|---|
| A figure built while the armies were still deployed hung the game (12.10.2026, 14:11); nothing runs before `Mission.IsDeploymentFinished`, and the first mode starts 5 s after the battle begins | `CrowdProxy.cs` 223-224, 260 |
| A faster start (0.3 s, three steps at once) froze the game twice with 2,000 soldiers and a full skeleton buffer (12.10.2026) | 256-259 |
| 1,700 figures with a head froze the game three times, some seconds after they stood there; 1,000 did not (12.10.2026) | 931-933 |
| Until a figure's meshes had loaded it showed as a head with a weapon and a shield (12.10.2026); the real soldier stays drawn while it waits; the figure's visuals object still drew its body over the soldier and helmets flickered (12.10.2026), so the figure is hidden through that object too, every frame it waits | 1724-1731, 1833-1836 |
| A walk action does not run by itself on a skeleton without an agent (measured 09.10.2026), and walking is not an action (`act_none`), so the figures floated (seen 09.10.2026) | `CrowdProxy.cs`@c45d8374 667-669, 1053-1056 |
| A formation that sets off does so in one frame and the whole line lifted the same foot (12.10.2026); a start phase of `(a.Index * 0.618) % 1` fixes it | 1035-1049 |
| Within one frame the engine gives no new pose (clip stride probe 0.00 m, 10.10.2026) | 1490-1491 |
| Faults going backward were 18 to 36 % of all backward movement (measured 10.10.2026) | 1066-1067, 1077-1078 |
| Weapons re-checked three times a second showed up to a third of a second off, so they are re-checked every frame during an equip or unequip action | 996-1002 |

The gait step size is a separate finding and lives in [animation binding](animation-binding-and-playback.md) under `FillAnimationSystemData`.

**TAOM's nearest code.** `Main/Features/HeroRace/CharacterSpawnerService.cs` (about 261-290) builds a mount for the character-screen tableau with `CreateAgentSkeleton`, `SetAgentActionChannel` at an explicit progress and one `TickAnimations(0.01f, ...)`. That is a posed still, not a running animation, so the no-self-tick fact above does not affect it. It does not show that TAOM's horse animates over time.

## Evidence (file:line, v1.4.5 shipping decompile)
- `Mission.cs`: `SpawnAgent`:4074, `SpawnTroop`:4418, `SpawnMonster`:4394/4399-4416, `CreateHorseAgentFromRosterElements`:4525-4534 (CreateAgent FromHorseObj:4528, SetMountInitialValues:4533), `CreateAgent`:4040-4054, `BuildAgent`:4007-4038 (PreloadForRendering:4025).
- `Agent.cs`: `EquipItemsFromSpawnEquipment`:4529-4567 (switch FromRoster/FromCharacterObj→AddSkinMeshes:4560; FromHorseObj falls through), `AddSkinMeshes`:5405-5411, `PreloadForRendering`:4923 → `PreloadForRenderingAux`:5189 (`MBAPI.IMBAgent.PreloadForRendering`).
- `IMBAgent.cs`:533 — `[EngineMethod("preload_for_rendering")]` (native).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/banner-bearers.md](../../features/banner-bearers.md)
- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)
- [docs/reference/engine/animation-binding-and-playback.md](./animation-binding-and-playback.md)
- [docs/reference/engine/monster-model.md](./monster-model.md)

<!-- backlinks-end -->
