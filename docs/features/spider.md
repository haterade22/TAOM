# Spider

> **Status (2026-06-04): WORK IN PROGRESS — detached non-humanoid combatant.** The giant spider is a recruitable Dol Guldur troop that, at battle spawn, is intercepted on `Mission.SpawnAgent` and built as a **detached non-humanoid agent** via the engine's free-mount (`FromHorseObj`) path — the only way to put a non-humanoid body on the battlefield without the humanoid-skin build that crashes. The earlier in-place `AgentBuildData.Monster`-swap design (which let vanilla `SpawnAgent` build a humanoid skin on the spider skeleton → native AccessViolation) is **superseded**.
>
> **Two things still gate a fully-shipping spider** (both tracked below + in the RCA): (1) the real 62-bone spider mesh AccessViolations at render and needs a **Modding-Kit mesh-split**; (2) **formation membership** (advancing with the army) is implemented but gated off pending an in-game test of the native weapon-state init. As a committed, non-crashing checkpoint the feature ships behind a **warg render stand-in** (`SpiderConfig` points at the warg monster/mesh, which renders cleanly and exercises the entire spawn code path).
>
> Crash-fix journey, decompiled evidence, and the next-session plan: [`docs/reviews/rca-spider-troop-2026-06-04.md`](../reviews/rca-spider-troop-2026-06-04.md). Skeleton / mesh / animation authoring (the in-Blender + Modding-Kit side): [`spider-skeleton-animation-pipeline.md`](spider-skeleton-animation-pipeline.md).

## Overview

Giant spiders are a recruitable troop of Dol Guldur. Bannerlord cannot host a non-humanoid creature as an ordinary `NPCCharacter` (the `race=` field resolves against a humanoid-only `skins.xml`), so the troop uses a **humanoid anchor** (`taom_spider_creature`, `race="dg_uruk"`) for everything the troop system needs — recruitment, party roster, UI. At battle spawn a Harmony **prefix on `Mission.SpawnAgent` returns `false`** for that one troop and **builds the agent itself** as a detached spider via reflected private engine methods, mirroring how the engine spawns a riderless mount. The recruit shows a humanoid silhouette in the party roster but spawns and fights as a giant spider in battle.

## Why This Exists

LOTR combat needs Mirkwood / Dol-Guldur giant spiders, but the engine offers no direct way to make a non-humanoid creature a recruitable, battle-spawning combatant. Three approaches were tried:

1. **Rideable mount** (spider as a `HorseItem` ridden by an uruk) — abandoned: a mounted creature gets a campaign-map party icon whose build calls `Skeleton.ForceUpdateBoneFrames()` → `AccessViolationException` on entering the open world (a hard crash that exists ONLY for mounts). See memory `feedback_nonhumanoid_creature_troop_not_mount`.
2. **In-place Monster swap** (`Mission.SpawnAgent` prefix returns `true` after `agentBuildData.Monster(spider).NoHorses(true).NoWeapons(true)`) — **superseded**: vanilla `SpawnAgent` then runs its normal humanoid build (`EquipItemsFromSpawnEquipment` → `AddSkinMeshes`) on the spider skeleton, applying a `dg_uruk` skin onto non-matching bones → native AccessViolation during the agent-visual build.
3. **Detached non-humanoid combatant** (current) — the prefix returns `false` and reproduces the engine's **free-mount** build (`CreationType.FromHorseObj`), which skips `AddSkinMeshes` entirely. The body mesh comes from a mesh-only `Horse` item; the Monster supplies the skeleton + `as_spider` animations. No humanoid skin is ever built.

## Architecture

### Design challenge

`FromHorseObj` is the engine's *mount* recipe: it skips the humanoid skin (good) but also skips weapon/wield setup and leaves the agent out of all formations (a riderless horse is not an army combatant). Making that same construction fight as a **troop** means re-adding, by hand and in the right order, the pieces a normal troop spawn would have gotten — position, team, casualty origin, a body mesh, an (empty) weapon state, and formation membership — while never triggering the skin build.

### Solution — the detached spawn pipeline

```
Dol Guldur notable volunteers (VolunteerRecruitmentService, weight 1)
                    │ player/AI recruits "Giant Spider"
                    ▼
        party roster holds taom_spider_creature (humanoid anchor)
                    │ battle deploy → MissionBattleSideSpawnContext.SpawnTroops
                    │              → Mission.SpawnAgent(agentBuildData) per troop
                    ▼
   Patch45_SpiderTroopSpawn  (Prefix on Mission.SpawnAgent)
        if IsSpiderTroop(agentBuildData.AgentCharacter):
            __result = SpiderDetachedAgentSpawner.TrySpawnDetachedSpider(...)
            return false        ← skip the vanilla humanoid build
        else return true        ← every other troop unaffected
                    ▼
   SpiderDetachedAgentSpawner.TrySpawnDetachedSpider  (boundary glue, reflected privates)
     1. compute formation-slot frame  (public Mission.GetTroopSpawnFrameWithIndex)
     2. CreateAgent(FromHorseObj, character)            ← no humanoid skin
     3. SetInitialFrame(slot pos/dir)  +  SetMountInitialValues(name, mountKey)
     4. SetTeam + Origin (casualty/XP)  +  InitializeSpawnEquipment(mesh item @ Horse slot)
     5. BuildAgent(agent, null)                          ← free-mount build (Formation null, AI)
     6. EnsureMissionEquipment  → InitializeMissionEquipment(null,null)  (empty MissionEquipment)
     7. [GATED OFF] InitializeNativeWeaponState + AttachToFormation
     8. NotifyAgentBuilt → OnAgentBuild fires           ← attaches the BT, casualty trackers
                    ▼
   SpiderMissionBehavior attaches SpiderTree BT to spider-bodied agents
        SpiderAttackService (CustomAttack via fang bones; bone-collision through the
        shared IBoneCollisionService singleton that AdvancedCombatBehavior ticks)
```

### The three Harmony patches (all in `Patch45_SpiderTroopSpawn`, applied at `SubModule.cs:539`)

| Patch class | Target | Role |
|---|---|---|
| `Mission_SpawnAgent_SpiderSwap_Patch` | `Mission.SpawnAgent` (Prefix) | For the spider troop: build the detached agent + `return false`; else `return true` (fail-open → vanilla humanoid anchor, never a crash). Co-exists with `Patch23_BannerColorPersistence` on the same method. |
| `Agent_WieldInitialWeapons_SpiderSkip_Patch` | `Agent.WieldInitialWeapons` (Prefix) | Skip wielding for the spider. Vanilla `SpawnTroops` calls `WieldInitialWeapons()` on every spawned agent; its first line (`GetPrimaryWieldedItemIndex`) derefs the spider's uninitialized native wield pointer → NRE. The spider bites via its Monster, never wields — skipping is correct. |
| (`InitializeNativeWeaponState`, in the spawner) | `Agent.RemoveEquippedWeapon` ×5 | **Gated off** (`SpiderConfig.EnableFormationMembership`). The investigated *root* fix for the native weapon state — see "Formation membership" below. |

- **Patch45 is thin + fail-safe:** the spawner never throws out (try/catch around every reflected invoke; `TargetInvocationException` unwrapped + logged once per error class). Any null/binding/asset failure → returns `null` → the prefix returns `true` → vanilla spawns the harmless humanoid anchor.
- **The decision is unit-testable, the spawn is boundary glue:** `ISpiderTroopSpawnService.IsSpiderTroop(string)` is a pure, mocked decision; `SpiderDetachedAgentSpawner` is engine-coupled glue (reflection + `MBObjectManager` + sealed types) invoked by the patch, verified in-game rather than unit-tested (ADR-002/007 boundary).

## The crash-fix journey (2026-06-04)

The detached redesign surfaced five distinct crash layers, each fixed in turn (full evidence + stacks in the RCA):

1. **DivideByZero (native `CreateAgent`)** — the spider animation files were registered in `LOTRLOME_Armory/ModuleData/project.mbproj` under *custom* `soln_spider_*` ids the runtime silently ignores. **Fix:** move them to top-level files under the engine's *recognized* ids (`soln_action_types`, `soln_monster_usage_sets`, `soln_action_sets`).
2. **AccessViolation — missing mount-key + NaN direction** — `BuildAgent → PreloadForRendering` AV'd because the agent's render data was uninitialized and a `(0,0)` deploy direction normalized to a NaN frame. **Fix:** reflected `Agent.SetMountInitialValues(name, MountCreationKey)` + a `Vec2.Forward` direction guard.
3. **AccessViolation — the spider mesh** — `sk_spider_forest_c` is a single **62-bone** mesh that overflows the native per-mesh bone palette → AV in `PreloadForRendering`. **Not yet fixed** (needs a Modding-Kit mesh-split). Confirmed via the warg stand-in: the warg mesh renders cleanly through the exact same code path.
4. **NRE — `WieldInitialWeapons`** — vanilla `SpawnTroops` calls `agent.WieldInitialWeapons()` post-spawn; it derefs the uninitialized native wield pointer (`0xee0`). **Fix:** `Agent_WieldInitialWeapons_SpiderSkip_Patch`.
5. **NRE → native AV — formation membership** — adding the agent to a formation triggers engine classification: `IsInfantry → IsRangedCached → Equipment.Contains…()` (NRE on the null `MissionEquipment`), then `BehaviorSkirmish → MaximumMissileRange → GetMissileRange()` (native AV on the uninitialized weapon struct). **Partial fix:** `EnsureMissionEquipment` (managed, shipped on) resolves the NRE; the native AV is resolved by `InitializeNativeWeaponState` (gated off, see below).

## Current checkpoint state — what works / what's gated

| Layer | State |
|---|---|
| Spawn as detached non-humanoid agent | ✅ working (8 agents spawn, get BTs, correct `as_warg`/`as_spider` action set) |
| Render | ✅ with the **warg stand-in**; ❌ real spider mesh AVs until the mesh-split |
| Deployment-zone positioning | ✅ `Mission.GetTroopSpawnFrameWithIndex` (vanilla-exact slot frame) |
| Empty `MissionEquipment` | ✅ `InitializeMissionEquipment(null,null)` — weapon-cache queries return `false` not NRE |
| `WieldInitialWeapons` NRE | ✅ skipped for the spider |
| Formation membership (advance with army, commandable) | ⏸ **gated off** (`SpiderConfig.EnableFormationMembership = false`) — detached spiders are positioned but **passive** (the BT bites adjacent targets only; no move-to-enemy node) |
| Native weapon-state init (root fix for the formation/combat native AVs) | ⏸ implemented, gated with the membership it enables, **unverified in-game** |

## Recruitment

`taom_spider_creature` is added at **weight 1** to the four Dol Guldur settlement pools (`town_DG1`, `castle_DG1`–`castle_DG3`) and the `dolguldur` culture fallback in `VolunteerRecruitmentService`. It is deliberately **absent from the clan-path pool**. Because settlement pools feed both player and AI recruitment, AI Dol Guldur lords may occasionally field a spider too (thematic, rare). `level="21"` maps to Tier 4, ≤ `MaxVolunteerTier` (6), so the spider is genuinely offerable as a volunteer.

## Configuration

| File / Class | Purpose |
|---|---|
| `Main/Features/Spider/SpiderConfig.cs` | `SpiderMonsterId` / `SpiderMountItemId` (**currently the warg stand-in** — real values `"spider"` / `"spider_mount_a"`); `SpiderCharacterId="taom_spider_creature"`; `EnableFormationMembership=false`; combat tuning + fang bone indices (placeholders 23/37/43) |
| `Main/_Module/ModuleData/characters/spider_creature.xml` | The recruitable anchor `<NPCCharacter id="taom_spider_creature">` — `race="dg_uruk"`, `level="21"`, `occupation="Wanderer"` (keeps it out of the Custom-Battle picker), `is_basic_troop="false"`, `hidden_in_encyclopedia="true"` |
| `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | Dol Guldur settlement + culture pools include the spider (weight 1) |
| `LOTRLOME_Armory/.../LOTRAOM_horses.xml` → `spider_mount_a` | Mesh-only `Horse` item (`mesh="sk_spider_forest_c"`, `monster="Monster.spider"`, `is_mountable="false"`, `is_merchandise="false"`, culture-less) — the spider body mesh + Monster for the detached agent. Never rideable/rostered. |
| `LOTRLOME_Armory/.../Monsters/LOTR/lotr_monster_spider.xml` | `<Monster id="spider" … Mountable="false" IsHumanoid="false">` (62 bones) |
| `LOTRLOME_Armory/ModuleData/project.mbproj` | Registers the spider anim files under **recognized** ids (`soln_action_sets`/`_types`/`_monster_usage_sets`) so the runtime loads them — see journey #1 |

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/Spider/Hooks/SpiderDetachedAgentSpawner.cs` | Boundary glue — reflected `Mission.CreateAgent` + `Mission.BuildAgent` + `Agent.SetMountInitialValues`; the full detached spawn sequence; `EnsureMissionEquipment` / `InitializeNativeWeaponState` / `AttachToFormation` |
| `Main/Features/Spider/Hooks/Mission_SpawnAgent_SpiderSwap_Patch.cs` | `Patch45_SpiderTroopSpawn` prefix — detached spawn + `return false`, else `return true` |
| `Main/Features/Spider/Hooks/Agent_WieldInitialWeapons_SpiderSkip_Patch.cs` | Skip `WieldInitialWeapons` for the spider |
| `Main/Features/Spider/ISpiderTroopSpawnService.cs` / `SpiderTroopSpawnService.cs` | Pure `IsSpiderTroop(string)` decision (the swap logic is gone) |
| `Main/Features/Spider/SpiderMissionBehavior.cs` | `MissionLogic` — attaches `SpiderTree` BT to spider-bodied agents; owns `SpatialGrid`/bone-collision only when no other combat behavior already does |
| `Main/Features/Spider/SpiderBehaviorTree.cs` + `BehaviorTreeElements/*` | BT: no-enemy-near → sleep, else → `SpiderAttackTask` (bite-in-place; **no movement node**) |
| `Main/Features/Spider/ISpiderAttackService.cs` / `SpiderAttackService.cs` | Bite damage calc + `CustomAttack` |
| `Main/IoC.cs` / `Main/SubModule.cs` | Feature registration + Patch45 `Initialize` + `PatchCategory("Patch45_SpiderTroopSpawn")` (`SubModule.cs:539`) + `AddMissionBehavior` |

## Dependencies

- **`LOTRLOME_Armory`** — provides the spider Monster, `as_spider` action set, skeleton, mesh, animations, and the `spider_mount_a` mesh item. Resolved by a **runtime** `MBObjectManager` lookup at spawn (all modules loaded by battle time), and the spawner fail-opens if absent, so load order is not load-bearing for the spider specifically.
- **`Alliance.Wargs`** — provides the `warg` Monster + `warg_brown` item + `warg_low` mesh used by the current render stand-in.
- **`Main/Features/AdvancedCombat`** — `IBoneCollisionService` (singleton, ticked by `AdvancedCombatBehavior`), `SpatialGrid`, `CustomAttack`.
- **`BehaviorTrees` + `BehaviorTreeWrapper`** — BT framework (inlined into `TAOM.dll`).

## Tests

- `TAOM.Tests/Features/Spider/SpiderTroopSpawnServiceTests.cs` — `IsSpiderTroop` decision (spider id, non-spider, null, ctor smoke).
- `TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs` — bite damage formula + skip-guard exhaustion.
- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` — spider pool coverage (4 settlements + culture return the spider on max roll at weight 1; clan-path pool excludes it).
- `SpiderDetachedAgentSpawner` (reflection + engine build), the BT nodes, and `SpiderMissionBehavior` are engine-coupled → in-game smoke test. Full suite: **3030 pass / 2 skip**.

## How-To

### Resume / test in-game (warg stand-in)

1. Close Bannerlord, `./build.ps1 -RunTests` (deploy needs the game closed; confirm green).
2. Launch with TAOM + LOTRLOME_Armory + Alliance.Wargs enabled.
3. Recruit a "Giant Spider" at a Dol Guldur fief (rare at weight 1) and take it to battle.
4. Grep the new `rgl_log` / `taom_debug` for `[Spider][diag]` lines — they dump the intercepted build data, the chosen spawn frame, post-build agent state, and (if enabled) formation attach. With the stand-in you should see warg-bodied agents spawn + render in the deployment zone with **no crash**.

### Re-enable formation membership (next session, needs in-game verification)

1. Set `SpiderConfig.EnableFormationMembership = true`. This runs `InitializeNativeWeaponState` (native `WeaponEquipped(Invalid)` for all 5 weapon slots via `RemoveEquippedWeapon`, no skin build) **then** `AttachToFormation`.
2. Test in-game. If the formation `GetMissileRange` native AV is gone, membership works (spiders advance with the army). If it persists, the fallback is a `Agent.GetMissileRange` prefix returning `0` for the spider (the formation-query surface is a single bounded method — HIGH confidence, see RCA).
3. If membership stays off by design, the spiders need a **BT movement node** (model on `WargAiControlledGetToEnemy`, but target the spider itself, not a non-existent `RiderAgent`) — otherwise they are passive bite-traps.

### Return to the real spider (after the mesh-split)

Split `sk_spider_forest_c` into base + `<AdditionalMeshes>` sub-meshes each ≤ ~40 bones (Modding-Kit), then set `SpiderConfig.SpiderMonsterId = "spider"` and `SpiderMountItemId = "spider_mount_a"`.

### Strip the diagnostic logging

All temporary instrumentation is tagged `[Spider][diag]` — grep `Main/Features/Spider/` for that tag and remove once the spider ships.

## Known Issues / Next Steps

1. **Spider mesh-split (asset, blocking real spider)** — `sk_spider_forest_c` (62-bone single mesh) AVs at `PreloadForRendering`. Split into sub-meshes each ≤ ~40 bones; the 62-bone *skeleton* is fine (`Skeleton.MaxBoneCount=64`), the limit is per-*mesh*.
2. **Formation membership (gated, needs test)** — `InitializeNativeWeaponState` is the investigated root fix; verify in-game, or fall back to the `GetMissileRange→0` prefix.
3. **Detached spiders are passive** — the `SpiderTree` BT only bites adjacent targets; advancing requires formation movement orders (path #2) or a new BT movement node.
4. **Fang bone indices are placeholders** (23/37/43, copied from the warg) — bites may land off-target until a runtime bone dump resolves `joint5_l/r` / `joint12_m` on `as_spider`.
5. **Humanoid roster silhouette** — the recruit shows the `dg_uruk` anchor in the party roster (visual only swaps at battle spawn). Accepted tradeoff.
6. **Warg stand-in is committed** — revert to the real spider once the mesh-split lands (see How-To).

## History

- **2026-06-04 (pm)** — redesigned to the **detached non-humanoid combatant** (`FromHorseObj`) approach; fixed 5 crash layers; shipped a non-crashing checkpoint behind the warg stand-in with formation membership gated off. This doc.
- **2026-06-04 (am)** — re-enabled as a recruitable troop via the in-place `Mission.SpawnAgent` `Monster`-swap (superseded the same day — the swap built a humanoid skin on the spider skeleton → AV).
- **2026-05-14** — feature disabled in-place (rideable-mount + scatter design).
- **2026-04-23** — original spider Monster + skeleton + animations from `LOTRLOME_Armory`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
