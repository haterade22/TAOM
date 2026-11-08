# Dire wolves

## GitHub Issue

[#1081](https://github.com/haterade22/TAOM/issues/1081) ridden dire wolves. Follow-up: #1082 riderless packs.

## Overview

The purchased ADOD_Beasts wolves become ridden dire wolves for Gundabad and the shared goblin tree (Goblin-town,
Misty Mountain Orcs, Blue Craig). Each culture gets a three-step rider line. The existing wargs, fell wargs and
spiders do not change.

**Status (2026-11-06):** the art, the Armory data, the troops, the costs and the C# are written, and the static
gates pass. In game, a portrait thumbnail threw because a merge into the live Armory `SubModule.xml` lost the wolf's
Monster row (restored; `e7ab20fcf` adds a test that every Armory Monster file is registered), and the wolf looked far too small in the
portrait and the inventory, so the rig was rebuilt at warg size (see "Size"). In the first ride the rider sat over the
hips, so the rig was rebuilt again with the seat under the warg's (see "Seat"). That rebuild has not run in game yet.
Everything under "How to verify in game" is still open.

## Why This Exists

- **Vanilla behavior:** no wolf mount exists. ADOD_Beasts uses the wolf as one riderless pet (`Type="Animal"`,
  `is_mountable="false"`).
- **TAOM requirement:** a smaller wolf beside the warg, with the warg's stats and behavior, on a very low War Spoils
  cost.
- **Without this feature:** the goblin cultures have no wolf riders, and they have no special resource at all.

## Architecture

### Design Challenge

The wolf's rig does not fit the available skeletons. The legs and spine miss `horse_skeleton` by a mean of 13.9 cm
and `skeleton_warg` by a mean of 23.9 cm, so a reskin or a weight transfer fails. The pack's compiled mesh tpacs are
about 1.6 GB each, so TAOM imports again from the FBX.

### Solution Approach

**The wolf keeps its own skeleton and clips, and speaks the warg's action vocabulary.** The Monster `dire_wolf` has
`monster_usage="warg"` and `action_set="as_dire_wolf"`. That set binds the same `act_warg_*` action names as `as_warg`
to clips on the wolf's skeleton. The warg usage set, the action types, the rider partial in `as_human_warrior` and the
`act_warg_attack_*` calls all apply unchanged.

- **Clips:** each `warg_X` has a twin `dire_wolf_X` with the warg clip's flags and the wolf's frames
  (`tools/gen_dire_wolf_anim_clips.ps1`). The generator fails on any warg binding it does not map.
- **Behavior:** `WargConfig.WargMonsterIds` holds `dire_wolf`, which attaches the behavior tree and the rider hand
  posing. `WargConfig.ImpactBoneIds` picks the wolf's bite bones by Monster id, because the warg's hard-coded bone
  indices mean nothing on this skeleton. Fords of War's `RiderlessMountRule` removes every riderless mount, so it needs no list.
- **Skeleton item:** the GUID stays the pack's, because the masters reference it. It would collide only if the
  ADOD_Beasts module loaded beside the Armory, and TAOM's module set does not include it.
- **Dedicated server:** `DsAssetPackages/taom_creature_skeletons.tpac` holds the wolf skeleton, so that Monsters bone
  lookups do not assert on the server.

### Size

The pack's wolf stands 0.93 m, about half of `warg_low` (1.70 m). Portraits and the inventory draw the mesh at its own
size, and `taom_body_length` reaches only the battle agent, so a Monster value alone left the wolf small on those
screens (Mike, 2026-11-06). The whole rig is therefore 2.08 times the pack's (`RIG_SCALE`): the mesh stands 1.94 m.
2.08 rather than the first rebuild's 1.73 puts the saddle at the warg's seat height (see "Seat"). In battle the
Monster's `taom_body_length` 115 makes it about 2.22 m, 25% larger than the wargs, whose items have `body_length` 110
(Mike, 2026-11-06, after the first ride). Portraits and the inventory keep the mesh size, so there the wolf is a little
larger than a warg (1.94 m against 1.70 m).

- **Meshes:** `prep_dire_wolf_meshes.py --scale` (default 2.08) scales the rest bones and the vertices about the world
  origin, then `--back` moves them toward the tail. It fails the run unless the pelvis, the paws and the top all moved
  by the factor and the saddle's seat lands on the warg's.
- **Clip masters:** `tools/blender/scale_dire_wolf_anims.py` scales the 6 animation FBX (location keys and rest bones;
  rotations stay), moves the armature object `--back` toward the tail, and checks each export against the pack by
  re-import. Each keeps the pack's take name, which the Kit uses as the master name (`tools/blender/fbx_takes.py`).
- **Skeleton:** `apply_dire_wolf_skeleton.py --scale` scales the rest translations, the capsule ends and radii and the
  joint positions, and `--back` moves the root bone (Pelvis). Masses, joint frames and limits stay. The capsules are
  then refit to the scaled mesh.
- **Gaits:** each gait clip's `LoopDisplacement` is the pack clip's value times `RIG_SCALE`, in the rig's own units.
  The engine applies the agent scale itself (native v1.5.6: the horse movement node compares this value with the
  agent's velocity divided by its agent scale), so `taom_body_length` never enters a clip and a size change needs no
  new clips.
- **Monster lengths:** each height, rein and capsule value in `apply_dire_wolf_armory.py` is the old value times
  `RIG_SCALE`. The body capsule is in model space, so it also moves `RIG_BACK`.

### Seat

**The engine seats a rider from its mount's rider clip, not on `rider_sit_bone`.** The dire wolf has
`monster_usage="warg"`, so its rider plays the warg's rider rows, and those place the rider where the warg's saddle is
in model space. In the first ride the rider sat over the wolf's hips: the warg's seat is at y -0.13, z 1.51 (model
units, nose +y), and the wolf's saddle was at y 0.33, z 1.26.

This is a native v1.5.6 read from the #1081 review (not checked at runtime). When the rider mounts, the engine stores
the rider's offset from the sit bone, taken from the rider's reference clip and the mount's current clip
(`FUN_1806402f0`). Each tick it places the rider at the sit bone plus that offset (`FUN_18063e700`), so in the
reference pose the sit bone cancels out. `rider_sit_bone` only decides what the rider follows as the mount moves.
The rider adders (`rider_body_capsule_forward_adder` and the others) feed the eye, the camera and the collision
capsule, not the drawn rider.

The fix moves the wolf instead of the rider. `RIG_SCALE` 2.08 lifts the saddle to the warg's seat height, and the
whole rig moves `RIG_BACK` 0.52 model units toward the tail: the meshes, the skeleton's root, the animation masters'
object and the Monster's body capsule. The model origin now sits under the saddle. `rider_sit_bone` stays `Spine1`,
the bone under the seat, which gives the shortest lever. `prep_dire_wolf_meshes.py` measures both seats
(`rig_scale.seat_point`) and fails beyond 0.06 model units; the 2026-11-06 run measured the wolf's seat at y -0.102,
z 1.52 against the warg's y -0.108, z 1.51.

### Troops and cost

| Tree | Branch | Line | War Spoils |
|---|---|---|---|
| `troops_goblin.xml` | `goblin_hunter` (L11) | `goblin_wolf_runner` L16, `goblin_dire_wolf_rider` L21, `goblin_wolf_lord` L26 | L26 |
| `troops_gundabad.xml` | `gundabad_lurker` (L16) | `gundabad_wolf_runner` L21, `gundabad_dire_wolf_rider` L26, `gundabad_wolf_lord` L31 | L26 and L31 |

- **Cost:** `upgrade_cost="1" recruit_cost="1" daily_upkeep="0.05"`. The upgrade and upkeep match
  `mordor_warg_beastmaster`. `recruit_cost` gates a notable purchase. A prisoner recruit is charged afterward with no gate, so it succeeds at a zero balance (#563, open, the same for every elite creature).
- **Skins:** Gundabad rides grey and white. The goblin tree rides black and brown. Horse and HorseHarness draw per
  slot, so a soldier gets either skin.
- **War Spoils:** `goblin`, `mistymountainorcs` and `bluecraig` joined the resource as Culture and Kingdom, so these
  realms now earn it and show it on the map bar.
- **Party templates:** the lines are not in any template, the same as `goblin_spider_rider`. Armies get them by
  upgrade, by notables and by the garrison swap. To field them from the first day, add stacks to
  `taom_partyTemplates.xml`.
- **Garrison swap:** wolf riders are warg-style cavalry, so `CreatureMountRiders` does not exclude them and a captured
  knight stack can become wolf riders (Mike, 2026-11-04).
- **Troop weight:** the three paid troops are listed at 2.0, as the warg riders are. The three unpaid rungs keep the
  default.

### Old saves

A save loaded in one of the three goblin realms changes as follows:

- War Spoils starts from 0, and foreign upkeep troops in that party now owe upkeep. They get one tick of grace, then
  desert.
- A hero of another resource culture in these kingdoms moves to War Spoils.
- The Armoury lord forge now needs 150 War Spoils in these realms.

### Eyes

The eyes stay on the body mesh. The FBX defaults are: grey and brown yellow, black red, white white. Four eye materials
exist: `dire_wolf_eye_yellow`, `_blue`, `_red` and `_white`. For an XML override, tag the eye sub-mesh of LOD0 to LOD2
with `horse_body` in the Kit, then add `<Materials><Material name="dire_wolf_eye_blue"/></Materials>` inside the item's
`<ItemComponent><Horse>` node. `MountVisualCreator.SetMaterialProperties` applies materials only to `horse_body`
sub-meshes. This route is not proven in game, and the tag is not set yet.

## Traps

- **Grey mesh re-import:** the Kit can drop the Skeleton item, or keep the old one (2026-11-06). Run
  `tools/apply_dire_wolf_skeleton.py --replace` and the capsule fit again. See
  [lotrlome-dire-wolf-changes.md](../reference/lotrlome-dire-wolf-changes.md).
- **Animation import:** the Kit names a master after the FBX take and refuses a name the module already holds. Keep
  the pack's take names (`scale_dire_wolf_anims.py` checks them). A Kit-imported master names no skeleton: run
  `wire_anim_master_skeletons.ps1` after every import.
- **`warg_attack_run`:** the Kit warns that this combat parameter is missing. The warg's own `warg_attack_running` clip
  names it, no module defines it, and `dire_wolf_attack_running` copies it. It is older than the wolf.
- **`_stand` clips:** `dire_wolf_trot_stand`, `_canter_stand` and `_forward_gallop_left_foot_stand` have no
  `quad_movement`, the same as their warg twins. `verify_mount_assets.py` exempts their four exact action and clip pairs,
  and `gen_dire_wolf_anim_clips.ps1 -Verify` holds their flags to the warg's. A mission crash at
  `Skeleton.TickAnimations` goes to `/native-crash-triage`.
- **Mesh materials:** an FBX with several materials binds per sub-mesh. Setting face indices before
  `materials.clear()` puts every face on one material, and `prep_dire_wolf_meshes.py` has a gate for this.
- **Five rein attributes (known limitation):** the Monster has the warg's 5, not 12. With `dire_wolf_saddle` (no
  `reins_mesh`) the rein cloth is never built, so the absent attributes are not read. A vanilla family 1 harness with
  reins fits in the inventory and reaches a null rein skeleton in `AddHorseReinsClothMesh`: test with `light_harness`.
- **LODs:** LOD1 and LOD2 are heavier than `warg_low`, because fur cards do not survive decimation. Judge the cost in
  battle.
- **Gait tuning:** to change `$RIG_SCALE`, delete those clips, run `-Apply`, then open and save the Kit once. `-Apply`
  never overwrites, and it exits 1 on an existing clip that differs from the plan.
- **Seat:** never move the rider with `rider_sit_bone` or the rider adders; move the rig (see "Seat"). `RIG_SCALE`
  and `RIG_BACK` must agree in every tool that carries them (`RigScaleTests`). An animation file's armature object
  turns, so its move goes on the object, never inside armature space (`rig_scale.move_object`).

## How to verify in game

Nothing below has run yet. Run these in Custom Battle first, then in a campaign. A new item needs a full game restart.
Spawn with `taom.spawn_troops goblin_wolf_lord 3 ally` and `taom.spawn_troops gundabad_wolf_lord 3 enemy`.

1. All four skins spawn. The rider sits in the saddle, not over the hips and not above it (see "Seat"), and the
   straps do not pass through the fur. Check the seat at a stand, a walk, a gallop and a turn.
2. The gaits do not slide. The clips hold the pack's travel times `RIG_SCALE`, and the engine scales it by the
   agent size (see "Size").
3. The bite and the rage fire. The log shows `[Warg] Added behavior trees to N wargs`, and no `act_none` plays.
4. The size looks right beside a warg (`taom_body_length` 115, about 2.22 m against the warg's 1.87 m; the log shows
   a `[MonsterSize]` line), with the rider camera and eye values. The portrait and the inventory show the wolf a little
   larger than a warg.
5. On purpose: kill a ridden wolf, kill a rider while mounted, and charge. Check the ragdoll on the zero-radius head chain.
6. **Harness cross-fit:** at `CharacterDetail` above 2, put `light_harness` on a wolf and ride. A crash here is the
   known limitation under Traps.
7. On a goblin player, run `taom.print_special_resources`. The upgrade to L26 is blocked at 0 and costs 1 when funded
   (`taom.add_special_resources 5`). Upkeep shows 0.05. Buy a wolf rider from a notable; it costs 1.
8. Load a goblin save and pass two daily ticks. Check the desertion behavior under "Old saves".
9. Capture a fief as a goblin realm. The log shows `GarrisonSwap: <id> re-manned N troops as goblin`.
10. The upgrade tree shows the new branch, and the map icon draws through `as_dire_wolf_map` at the right size.
11. Check the LOD cost in a large battle.
12. The dedicated server boots with no skeleton assert.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/Warg/WargConfig.cs` | `DireWolfMonsterId`, `ImpactBoneIds` |
| `Main/Features/Warg/WargAttackService.cs` | Picks the bite bones by Monster id |
| `Main/_Module/ModuleData/troops/troops_goblin.xml`, `troops_gundabad.xml` | The six troops |
| `Main/_Module/ModuleData/special_resources/` | War Spoils cultures and cost rows |
| `tools/apply_dire_wolf_armory.py` | The live Armory data |

## Dependencies

The live `LOTRLOME_Armory` (unversioned), recorded in [the ledger](../reference/lotrlome-dire-wolf-changes.md). A TAOM
release needs an Armory package with the wolves ([release process](../reference/release-process.md)).

## Configuration

Costs: `special_resources/troop_resource_costs.xml`. Size: `taom_body_length` on the Monster.

## Testing

- `TAOM.Tests/Features/Warg/DireWolfTroopDataTests.cs`: the lines, the wolf and saddle in every battle roster, the
  costs, the War Spoils rows and the marketplace.
- `TAOM.Tests/Features/Warg/WargMonsterIdTests.cs`.
- `tools/tests/test_apply_dire_wolf_armory.py`, `test_apply_dire_wolf_skeleton.py`, `test_gen_dire_wolf_anim_clips.py`,
  `test_verify_mount_assets.py`.
- Gates: `python tools/verify_mount_assets.py dire_wolf`, `python tools/audit_mount_parity.py`,
  `python tools/build_ds_skeleton_pack.py --check`.

## Out of scope

Riderless packs (#1082), ADOD's pet system and wolf HP bar, Fords of War rosters, wolf sounds (the wolf uses the warg's
sound class), and the mesh morph targets.

## Provenance

The wolf art and clips are purchased from ADOD_Beasts (`data-port`). No ADOD wolf code is taken.
[provenance-register.md](../reference/provenance-register.md).

## Changelog

- 2026-11-04: first version, #1081.
