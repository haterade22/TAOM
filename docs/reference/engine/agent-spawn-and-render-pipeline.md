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
| `GameEntity.CreateAgentSkeleton(skeletonName, isHumanoid: true, actionSet, usage, monster)` works on an empty entity made with `GameEntity.CreateEmpty(scene, false, false, false)`; no agent is needed | `CrowdProxy.cs` 908-911, 971 |
| A mesh added to the skeleton after the body was handed over to the engine's visuals object was not drawn ("weapons fought in the air near the player, no bodies"), so every mesh goes on first | comment at 920-921, seen in game 10.10.2026 |
| The engine's own way to move a body that is no agent is an `MBAgentVisuals` made with `CreateAgentVisuals`, then `SetEntity` and `SetSkeleton`, told the local speed and ticked (the campaign map's parties, the editor's `CharacterDebugSpawner`) | 949-955 and 1137-1145 |
| The lab animates such a skeleton itself; whether the engine would is not shown (one of the lab's test modes leaves a raw clip "to the engine", `CrowdLab.cs` 251, and its result is not recorded). The lab sets the action progress itself (a walk clip "may be driven by movement, which a plain skeleton does not have", a hedge in `CrowdLab.cs` 250-251), ticks the action channels while one action blends into the next (`CrowdProxy.cs` 1130-1136), and calls `TickAnimations` or `TickAnimationsAndForceUpdate` (1146-1147). A figure not ticked within 0.35 s of an action change stayed between the two actions | 1130-1148 |
| A mesh cannot be added to a body the visuals object already holds, so adding one (a head) means giving the whole figure up with `Visuals.Reset()` and building it anew | comment at 966-967, code 968 |
| Give a body held by a visuals object up through `Reset()`, never by removing its entity: removing the entity under it crashed the game some time later (10.10.2026, 23:45). The lab's teardown also detaches borrowed skeletons before removing entities, so an entity does not take a master's skeleton with it | `CrowdProxy.cs` 1776-1779; `CrowdLab.cs` 835-837 |
| Five extra draw calls for the game's own weapon meshes cost 1.8 ms a frame with 2,000 figures (measured 10.10.2026). The figure is a proxy; this is not an agent cost | `CrowdProxy.cs` 683-685 |

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
