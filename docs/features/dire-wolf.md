# Dire wolves

## GitHub Issue

[#1081](https://github.com/haterade22/TAOM/issues/1081) ridden dire wolves. Follow-up: #1082 riderless packs.

## Overview

The purchased ADOD_Beasts wolves become ridden dire wolves for Gundabad and the shared goblin tree (Goblin-town,
Misty Mountain Orcs, Blue Craig). Each culture gets a three-step rider line. The existing wargs, fell wargs and
spiders do not change.

**Status (2026-11-04):** the art, the Armory data, the troops, the costs and the C# are written, and the static
gates pass. Nothing has run in game. Everything under "How to verify in game" is still open.

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
  indices mean nothing on this skeleton. `RiderlessMountRule` lists it too, and a test holds that list to `WargConfig`.
- **Skeleton item:** the GUID stays the pack's, because the masters reference it. It would collide only if the
  ADOD_Beasts module loaded beside the Armory, and TAOM's module set does not include it.
- **Dedicated server:** `DsAssetPackages/taom_creature_skeletons.tpac` holds the wolf skeleton, so that Monsters bone
  lookups do not assert on the server.

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

- **Grey mesh re-import:** the Kit drops the Skeleton item. Run `tools/apply_dire_wolf_skeleton.py` and the capsule fit
  again. See [lotrlome-dire-wolf-changes.md](../reference/lotrlome-dire-wolf-changes.md).
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
- **Gait tuning:** to change `$GAIT_SCALE`, delete those clips, run `-Apply`, then open and save the Kit once. `-Apply`
  never overwrites, and it exits 1 on an existing clip that differs from the plan.

## How to verify in game

Nothing below has run yet. Run these in Custom Battle first, then in a campaign. A new item needs a full game restart.
Spawn with `taom.spawn_troops goblin_wolf_lord 3 ally` and `taom.spawn_troops gundabad_wolf_lord 3 enemy`.

1. All four skins spawn. The rider sits on the saddle (`SEAT_ABOVE_BACK`), and the straps do not pass through the fur.
2. The gaits do not slide. The tune value is `$GAIT_SCALE` (2.4). `LoopDisplacement` scaling with agent size is not
   established (`docs/features/animalia-elk-moose.md`).
3. The bite and the rage fire. The log shows `[Warg] Added behavior trees to N wargs`, and no `act_none` plays.
4. The size looks right beside a warg (`taom_body_length`, start 240; the log shows a `[MonsterSize]` line), with the
   rider camera and eye values.
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
- `TAOM.Tests/Features/Warg/WargMonsterIdTests.cs`, `TAOM.Tests/Features/FordsOfWar/Domain/RiderlessMountRuleTests.cs`.
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
