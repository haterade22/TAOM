# Spider

## Overview

Adds giant spiders to TAOM through **two complementary paths**:

1. **Mount path (primary)** — Spider as a HorseItem equipped by the `dg_giant_spider_rider` troop. The orc/uruk rider IS the soldier; the spider is their mount. Selectable in Custom Battle Cavalry, can appear in Dol Guldur party templates (when wired). Engine-native pattern, identical to Alliance.Wargs warg riders.
2. **C# spawner path (ambient)** — Hostile rider-less spiders auto-spawn via `Mission.SpawnAgent` + `AgentBuildData.Monster()` 1 second after Custom Battle start. Used for ambient Mirkwood-style encounters where a brood appears regardless of party composition.

Both paths share the same Monster, skeleton, and animations from the shared `LOTRLOME_Armory` module.

## Why This Exists

LOTR fantasy combat needs Mirkwood/Dol-Guldur-style giant spiders. Bannerlord's troop system cannot directly host non-humanoid creatures (the `race=` field on `NPCCharacter` is resolved against a hardcoded native race list and a humanoid-only `skins.xml` schema), so spiders cannot be defined as ordinary troops the way orcs/uruks are.

- **Vanilla behavior:** Non-humanoid creatures must be either (a) equipment items ridden by humanoid troops (the warg/horse pattern) or (b) decoration agents in scenes (chickens, sheep).
- **TAOM requirement:** Both — humanoid-rider mounted spiders for player-controlled battle AND standalone ambient spider mobs for Mirkwood encounters.
- **Without this feature:** The spider Monster + skeleton + 23 animations published by Erkam in `LOTRLOME_Armory` would have no path into actual gameplay.

## Architecture

### Design Challenge

`Mission.Current.SpawnAgent(AgentBuildData)` requires a `BasicCharacterObject` anchor — there's no way to spawn an agent without one. But TAOM's troop-XML pipeline cannot bind `race="spider"` because the engine rejects non-humanoid races at load time. The fix: define a humanoid-race **anchor character** (`taom_spider_creature`, `race="dg_uruk"`) purely to satisfy the `AgentBuildData` constructor, then override the visual at spawn time via `AgentBuildData.Monster(spiderMonster)`. The anchor never appears in party templates, custom-battle pickers, or recruitment menus — it is `hidden_in_encyclopedia`, `is_basic_troop="false"`, and equipment-empty.

### Solution Approach

Mirrors `Main/Features/Warg/` — a Mission-lifecycle behavior tree that drives bite attacks via bone-collision detection. Differences from Warg:

- **No rider, no rage mode** — the spider is the primary agent, not a mount; there is no rider to coordinate with.
- **Public service interface uses `IAgentAdapter`, not `Agent`** — corrects the ADR-007 violation in `IWargAttackService` (warg service exposes sealed `Agent` types which makes it un-mockable).
- **Direct spawn via `Mission.SpawnAgent` + `AgentBuildData.Monster()`** — bypasses the troop system entirely.
- **Custom Battle gating** — `SpiderMissionBehavior.ShouldSpawnInThisMission()` checks for `CustomBattleAgentLogic` so spiders never leak into campaign battles in v1.

### Component Diagram

```
                Mission start (Custom Battle only)
                            |
                SpiderMissionBehavior (MissionLogic)
                            |
            spawn ----+----- attach BT ----+----- tick BT
                      |                    |
              ISpiderSpawnerService    SpiderBehaviorTree
                      |              (no-enemy-near -> sleep,
                      |               otherwise -> SpiderAttackTask)
                      |                                 |
        Mission.SpawnAgent(AgentBuildData)        ISpiderAttackService
        (anchor: taom_spider_creature,            (CustomAttack with fang bones,
         monster: spider Monster)                  damage calc, hit handling)
```

## Asset Pipeline (Maya → Bannerlord)

The canonical authoring path for the spider's mesh + skeleton + animations, shared by the animator who got it working on a parallel project. Follow this for any future non-humanoid creature.

### 5-step workflow

1. **Export mesh + skeleton from Maya, import into Blender.** Maya is the source-of-truth rigging tool; Blender is the cleanup + Bannerlord-export stage.
2. **Clean rot/loc/scale transforms. Remove unused bones. Fix armature name.** Three things this fixes:
    - **Transform cleanup** — eliminates non-identity rest-pose transforms that cause mesh drift when animations apply.
    - **Unused bone removal** — strips tip/claw/decorative bones that aren't keyframed. This is what fixed the spider's 72-vs-62 bone mismatch (Erkam's update on 2026-04-23 stripped `joint6_L/R`, `joint27_L/R`, `joint33_L/R`, `joint39_L/R`, `joint45_L/R`).
    - **Armature rename** — apply Bannerlord's `_notused` convention to the source-side animation skeleton (e.g. `spider_skeleton_notused`). This is the convention that lets the Modding Kit auto-create Animation Clip wrappers separate from the rendering skeleton.
3. **Import mesh + skeleton + animations from Maya** as a separate **source armature** (don't overwrite the cleaned target).
4. **Bake animations from source to target bones via the bone-retargeter**. See `tools/blender_bone_retargeter.py` below.
5. **Export to Bannerlord** as FBX. Each animation gets its own FBX with mesh/skin disabled.

### `tools/blender_bone_retargeter.py`

A Blender add-on that automates step 4. Install via `Edit → Preferences → Add-ons → Install` and enable. Adds a panel to the 3D Viewport sidebar under **Technical Art → Advanced Bone Mapping (Loc/Rot)**.

| Operator | What it does |
|---|---|
| **Scan Target Bones** | Iterates the target armature's bones; auto-pairs each with a same-named source bone where one exists. The pairing list is editable per-row for bones that need manual mapping (e.g. `Spine1_M` → `spine1_m` if case differs). |
| **Apply Loc/Rot Constraints** | For each mapped pair, adds `COPY_LOCATION` + `COPY_ROTATION` pose constraints on the target bone, pointing at the source bone. Constraints are prefixed `Temp_Map_` so they can be cleared later. |
| **Bake Animation** | Runs `bpy.ops.nla.bake(visual_keying=True, clear_constraints=True, bake_types={'POSE'})` — converts the constrained pose into raw keyframes on the target armature, then removes the temporary constraints. Optionally deletes the source armature after baking. |

The result: animations originally authored against the Maya skeleton are baked as keyframes on the cleaned target skeleton, ready for Bannerlord FBX export.

### What this workflow solves vs what it doesn't

| Concern | Solved by workflow? |
|---|---|
| `_notused` convention missing on animation FBX source skeleton | ✅ Step 2 renames the armature |
| Bone-count mismatch between mesh skeleton and animation skeleton | ✅ Step 2 removes unused bones |
| Mesh distortion during animation playback (bind-pose drift) | ✅ Retargeting bakes to the target skeleton's keyframes, eliminating reference-frame mismatch |
| Missing Animation Clip wrappers in Modding Kit | ✅ Clean exports with proper conventions auto-produce Clip wrappers (like Wargs) |
| Empty `SkeletonUserData` (no ragdoll constraints) | ❌ Authored post-import in the Skeleton Editor (or via `tools/tpac_skeleton_transplant.py` as a shortcut) |
| `Usage='other'` vs `'horse'` on the skeleton resource | ❌ Set at FBX import time in the Resource Browser, not during this workflow |

### Reference

Workflow attributed to an animator who had the spider working end-to-end in a parallel project. The `mapper.py` bone-retargeter was their tool, archived here as `tools/blender_bone_retargeter.py`.

## Configuration

| File / Class | Purpose |
|---|---|
| `Main/Features/Spider/SpiderConfig.cs` | Static config (C# spawner path): `SpawnCount=5`, `SpawnRadius=12f`, `TargetDetectionRange=4f`, `SleepAfterAttack=2`, damage formula constants, fang bone indices (placeholder), pre-allocated bone-list arrays for BT-tick reuse |
| `LOTRLOME_Armory/ModuleData/monsters.xml` | `<Monster id="spider">` with 35+ bone refs (ragdoll, blood, splash, terrain decals), `Mountable="true"`, `rider_sit_bone="chest_m"` |
| `LOTRLOME_Armory/ModuleData/action_sets.xml` | `as_spider` action_set binding 23 `act_spider_*` types to animation clips |
| `LOTRLOME_Armory/ModuleData/action_types.xml` | 23 `act_spider_*` type declarations |
| `LOTRLOME_Armory/ModuleData/monster_usage_sets.xml` | Spider AI behavior template (movements + bite strikes) |
| `LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` | **Mount path:** 3 spider HorseItems (`spider_mount_a1/a2/a3`) — visual variants with stat differentiation (a3 Brood Mother is heavier/tougher) |
| `Main/_Module/ModuleData/troops/troops_dolguldur.xml` | **Mount path:** `dg_giant_spider_rider` Cavalry troop — uruk rider with halberd/shield/mace + spider mount in equipment |
| `Main/_Module/ModuleData/characters/spider_creature.xml` | **C# spawner path:** TAOM-side anchor `<NPCCharacter id="taom_spider_creature">` (occupation="Wanderer" — kept out of troop pickers; only used by `AgentBuildData` in `SpiderSpawnerService`) |

The spider Monster's bone names — `root_m`, `spine1_m`, `spine2_m`, `chest_m`, `head_m`, `joint5_l/r` (fangs), `joint13_m`/`joint14_m` (abdomen), `joint15_m`/`joint16_m` (stinger), `joint40_l/r` (front legs), `joint44_l/r` (front feet) — were confirmed bone-by-bone via `tools/extract_fbx_bones.js` against `sk_spider_forest_c.fbx`.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/Spider/ISpiderAttackService.cs` | Interface: bite damage calc, hit handling, attack trigger (all take `IAgentAdapter`) |
| `Main/Features/Spider/SpiderAttackService.cs` | Implementation. Damage formula mirrors `WargAttackService` shape but uses `SpiderConfig` |
| `Main/Features/Spider/ISpiderSpawnerService.cs` | Interface: `SpawnSpiders(Team, Vec3, count, radius)` returning adapter list |
| `Main/Features/Spider/SpiderSpawnerService.cs` | Resolves Monster + anchor character, builds `AgentBuildData`, calls injected spawn delegate. Test seam: `monsterLookup` and `spawnDelegate` `Func<>` constructor params |
| `Main/Features/Spider/SpiderMissionBehavior.cs` | `MissionLogic` — spawns N spiders 1 sec after Custom Battle start, attaches `SpiderTree` BT to each, manages `SpatialGrid` if no other behavior is doing it |
| `Main/Features/Spider/SpiderBehaviorTree.cs` | Minimal tree: `selector(no-enemy-near → sleep, otherwise → SpiderAttackTask + sleep)` + `OnSpiderDied` constant listener |
| `Main/Features/Spider/SpiderConfig.cs` | Static config |
| `Main/Features/Spider/SpiderIoC.cs` | DryIoc registration |
| `Main/Features/Spider/BehaviorTreeElements/IBTSpiderBlackboard.cs` | BT blackboard interface |
| `Main/Features/Spider/BehaviorTreeElements/NoEnemyNearSpiderDecorator.cs` | Detects enemies via `SpatialGrid` |
| `Main/Features/Spider/BehaviorTreeElements/SpiderAttackTask.cs` | Triggers `ISpiderAttackService.SpiderAttack` |
| `Main/Features/Spider/BehaviorTreeElements/OnSpiderDied.cs` | Cleanup listener |
| `Main/Adapters/IAgentAdapter.cs` + `AgentAdapter.cs` | Added `IsSpider()`, `Health`, `State`, `IsSameTeam()`, `GetBaseArmorEffectivenessForBodyPart()` |
| `Main/_Module/SubModule.xml` | Added `<DependedModule Id="LOTRLOME_Armory" optional="true" />` and registered `characters/spider_creature.xml` |

## Dependencies

- **`LOTRLOME_Armory`** module (load before TAOM) — provides spider Monster, action_sets, action_types, monster_usage_sets, mesh, textures, animations
- **`Main/Features/AdvancedCombat`** — `IBoneCollisionService`, `SpatialGrid`, `CustomAttack` plumbing
- **`BehaviorTrees` + `BehaviorTreeWrapper` DLLs** — BT framework (already used by Warg)

## Tests

`TAOM.Tests/Features/Spider/SpiderAttackServiceTests.cs` — 14 tests:
- Pure damage formula: zero/max/excessive velocity, zero/half/full armor (5 tests)
- Skip-guard exhaustion: null target, inactive target, fading-out target, null attacker, same-team, killed state (6 tests)
- `SpiderAttack` null + inactive guards (2 tests)
- Constructor smoke (1 test)

`TAOM.Tests/Features/Spider/SpiderSpawnerServiceTests.cs` — 6 tests:
- Pre-spawn validation: missing anchor character, count=0, count<0 (3 tests)
- `ComputeSpawnPosition` math: distance bounds within `[0.5*radius, radius]`, preserves Z+W axes (2 tests)
- Constructor smoke (1 test)

BT nodes and `SpiderMissionBehavior` are not unit-tested (engine-coupled — covered by in-game smoke test).

## How-To

### Test the spider in Custom Battle

1. `./build.ps1 -RunTests` — confirm green.
2. Launch Bannerlord with TAOM + LOTRLOME_Armory enabled (load order: LOTRLOME_Armory before TAOM).
3. Custom Battle → any scenario.
4. Look for `[Spider] Initialized` and `[Spider] Custom Battle spider spawn: N agents` in `rgl_log.txt` after mission load.
5. Visual checks: spiders appear near player, walk toward enemies, bite, die without crashing.

### Tune attack tuning

Edit `Main/Features/Spider/SpiderConfig.cs` constants. `SpawnCount` and `SpawnRadius` control how many spiders spawn and how spread out. `MaxBaseDamage`/`MaxSpeedDamage`/`SpeedForMaxDamage` tune the bite damage formula. `TargetDetectionRange` is the BT's "I see an enemy, attack" distance. Rebuild — changes apply on next Custom Battle.

### Refine fang bone indices

`SpiderConfig.FangBoneIndex*` are placeholder values copied from the warg pattern (23, 37, 43). To get the real spider-specific indices:
1. Spawn one spider in-game.
2. Add a temporary log line in `SpiderMissionBehavior.AttachBehaviorTree` that iterates `agent.GetCurrentSkeleton()` bones and logs `index → name`.
3. Find the indices for `joint5_l`, `joint5_r`, `joint12_m` and update `SpiderConfig`.
4. Remove the temporary log.

## Known Issues / Open Items

- **Fang bone indices are placeholders** — bite collision currently uses warg's bone indices (23/37/43) which may be off-target on the spider skeleton. Visual smoke test will confirm whether bites land in the right places.
- **Custom Battle only in v1** — campaign integration (Mirkwood spider scene triggers, party-template entries) deferred until smoke test passes.
- **Animations not yet authored against `_c` per Erkam's confirmation that the case-mismatch and bone-count mismatch are resolved** — the in-game smoke test will confirm whether the Modding Kit binding actually succeeded.
