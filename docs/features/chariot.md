# War Chariot (Rhûn Wainriders, Erebor rams)

## Overview

The upstream pack's standard war chariot ported into TAOM as a **ridden mount** for Rhûn's Wainrider troop
tree. One Monster (`chariot`), one 60-bone skeleton carrying both horses + cart + wheels; the rider
stands in the cart. Vanilla cavalry spawn via a Horse-slot item — no spawn patch, no behavior tree,
no mission behavior, **no C# at all** (a 100% data port). Issue #279.

Erebor has a second chariot on the same rig, pulled by two war rams, with two Ironpass troops: see
[Dwarven war chariot](#dwarven-war-chariot-erebor-2026-11-24). Five more Rhûn carts and five new horse armours
are in work; the black set (cart F with armour 5) has its sources and its Kit packages in the Armory since 2026-11-26.
No item draws it yet, so it is not in the game: see [the variants section](#rhûn-cart-variants-and-new-horse-armours-in-work-since-2026-11-24).

## The rig since 2026-11-23

The first rig (June 2026) is gone from the live Armory. Five sections describe it and are its history: "Animation
refinement", "Architecture", "Hit capsules fitted to the mesh", "Status / pending", "The missing second horse" and
the mesh notes of "Rhûn barding". Every other section describes what runs now. The chariot runs on the rig of the
upstream chariot pack, taken whole:

- **One package**, `Assets/creature/chariot/chariot_rig_geo.tpac` (137 items: 75 clips, 54 animation masters, 3
  skeletons, 5 metameshes; no material and no texture). `tools/tpac_slim_package.py` built it from the pack's
  package by leaving out the pack's cart, its materials and its textures. The skeletons are
  `horse_skeleton_chariot` (60 bones: the root `chariot_bone`, two horse skeletons with the suffixes
  `_chariot_left` and `_chariot_right`, the cart bone and two wheel bones), `horse_chariot_rein` and
  `horse_skeleton_chariot_unused`.
- **The horse team** `taom_chariot_a` draws the pack's two horse bodies (`horse_chariot_brown`) and their mane.
- **The cart is the harness item.** `taom_chariot_armour_rhun` and `taom_chariot_armour_imperial` draw
  `rhun_war_chariot`: TAOM's own cart (built 2026-11-24, no geometry of the pack) and the Rhûn horse armour for both
  horses. Its sources are `AssetSources/creature/chariot/mesh/rhun_war_chariot.fbx` and six maps in
  `.../chariot/textures`; the Modding Kit imports them.
- **ModuleData of the Armory** that the port wrote: the Monster (`Monsters/LOTR/lotr_monster_chariot.xml`,
  `rider_sit_bone="chariot_bone"`), the `act_chariot_*` rows of `action_types.xml`, the three `as_chariot` sets and
  the dwarf rider rows in `action_sets.xml`, the rider rows that `action_sets.xslt` adds to the human sets, the
  `chariot` usage set, ten `taom_chariot_*` sounds in `module_sounds.xml` with 131 wave files, the collision class
  `taom_chariot` (its rows in `CollisionInfos/LOTR/collision_infos_warg.xml`, its declaration in
  `physics_materials.xml` and in `physics_materials.xslt`), the cloth bodies, and `voice_definitions.xml` with its
  `project.mbproj` row.
- **Two actions are not typed as the pack typed them.** `act_chariot_rear` is `actt_rear`, as the vanilla horse's
  is, and `act_chariot_kick` has no type. The pack typed both `actt_kick`; the kick is bound to an idle clip, also
  on the rider, which is the hang class that `tools/tests/test_armory_kick_typed_actions.py` guards (#1499).
- **Three gait clips carry no `quad_movement`**: `chariot_gait_walk_backward`, `chariot_gait_walkfast` and
  `chariot_gait_trot`, each bound only on an in-place row (direction and turn `none`). The vanilla horse has the
  same shape on the same three rows (`gait_walk_backward`, `gait_walkfast`, `horse_gait_trot_2`): it binds untagged
  clips on six of its seven in-place rows. `tools/verify_mount_assets.py chariot` lists the three as exempt and
  `tools/audit_mount_parity.py` section F names them. Both read the tag from the usage list of the clip's own
  metadata, and the data gate `chariot-body` runs the first one.
- **The mount, dismount and jump rows are the pack's, not the horse's.** The four actions `act_mount_chariot_left`,
  `_right`, `act_dismount_chariot_left` and `_right` have no action type (vanilla types its own `actt_mount` and
  `actt_dismount`). The jump rows exist for the directions `front` and `none` only, their two start rows name no
  action, and the usage set's `jump_start_action` is `act_chariot_dash`. All of it equals the pack's files. A troop
  spawns mounted, so a battle of troops uses none of it: the player's mount, dismount and jump are checks 8 and 9
  below.
- **TAOM's creature mount patches apply.** `CreatureMounts.IsCreatureMount("chariot")` is true, so
  `Agent_Die_SpiderDismount_Patch` takes a dying rider off the chariot before the native death, and takes the rider
  off a dying chariot (he gets no fall action from the engine), and `Agent_HandleBlowAux_SpiderDismountGuard_Patch`
  removes the dismount flag from a blow on a chariot rider.
- **Hit capsules are the rig's own.** 51 of the 60 bones have a hit capsule. Nine have none, the cart bone
  `chariot_bone.001` and both wheel bones among them, so a blow or an arrow passes through the cart. The fit of
  2026-09-18 was made on the first rig and is gone with it (`python tools/skeleton_hit_capsules.py show`, read
  2026-11-24).
- **Dedicated server.** A server loads no loose Armory asset, so
  `Main/_Module/DsAssetPackages/taom_creature_skeletons.tpac` carries the creature skeletons (8, the two chariot
  skeletons among them); `tools/build_ds_skeleton_pack.py --check` holds it to the live Armory.

**What the repo holds, and what it does not.** `docs/reference/lotrlome-armory-snapshot/` holds five of the port's
files as they were on 2026-11-24: `action_sets.xml`, `action_types.xml`, `monster_usage_sets.xml`,
`monster_usage_sets.xslt` and `project.mbproj`. The rig package, the Monster file, the rider rows of
`action_sets.xslt`, the sounds with their wave files, the collision class, the cloth bodies, the voice file and the
three Wainrider item rows and the black set's harness row (`taom_chariot_armour_rhun_black`, 2026-11-26; its text is kept in the variants section) are only in the Armory, which is not versioned, and no tool puts any of it back after a
reinstall. Only the dwarven chariot's item rows have a tool (`tools/apply_dwarf_chariot_armory.py`). The gate
`chariot-body` goes red when the rig or a clip is gone. The asset mirror still holds the first rig.

**Left as they are in the live files.** Comments in `action_sets.xml`, `action_types.xml`,
`monster_usage_sets.xml`, `monster_usage_sets.xslt` and `LOTRAOM_horses.xml` still describe the first rig (a cart
inside the horse mesh, `chariot_skeleton`, mount names "derived by the engine"). Eight declared action types are
bound nowhere: the first rig's four `act_mount_chariot_*from*` and the pack's four `act_map_chariot_attack_*`.
`taom_chariot` is declared in both physics material files, as the warg's class has been since August 2026.

## Why This Exists

Rhûn's troop tree already had chariot-named tiers (`wainrider_swift_chariot` T8 →
`wainrider_warlord_chariot` T9 in `troops_rhun_new.xml`) riding placeholder `khuzait_horse` — the
Wainriders of Rhûn are Tolkien's canonical chariot-people, and the upstream pack shipped a complete,
fully-animated chariot (rights to the art confirmed by the maintainer). The port follows the proven
warg/elephant/spider creature pipeline, with the spider's most expensive lesson (quad_movement tags
on gait clips) MOSTLY pre-solved — a 2026-06-13 audit caught **2 exceptions**: `chariot_gait_walkfast`
+ `chariot_gait_walkbackfast` shipped WITHOUT `quad_movement` (their siblings had it). Fixed by
byte-grafting the tag + step points from the tagged siblings — see **Animation refinement (2026-06-13)**
below. The other clips do carry step points + `QuadMovementUsage`.

## Animation refinement (2026-06-13, Blender-MCP audit)

A multi-agent investigation + independent `tpac_clipinfo` re-verification of all 24 compiled chariot
`_anm.tpac` clips found the `quad_movement`-"pre-solved" claim above was **not fully true**:
`chariot_gait_walkfast` and `chariot_gait_walkbackfast` shipped WITHOUT the `quad_movement` clip-usage
(siblings `chariot_walkfast`/`walkbackfast`/`canterfast`/`strafe_*`/`turn_*` all have it). Per
`feedback_quad_movement_tag_required_for_gait_clips.md` an untagged movement clip AVs at
`Skeleton.TickAnimations` on the first mount tick in a live mission. **Fixed:** byte-grafted the
`quad_movement` usage + step points from each tagged sibling onto the two gait clips, preserving each
clip's own source range (gait_walkfast 1312-1372, gait_walkbackfast 1372-1312). Originals at
`*.bak-untagged`; patcher `E:\LOTRAOMAssets\_auto_workspace\_chariot_refine\patch_quad_movement.py`.
Structurally verified (both now show `quad_movement`); **in-game mission test still required** (the AV
is mission-only). This is a textbook [ported-data-completeness-not-verified] catch — the doc *claimed*
all clips were tagged; an independent re-check found 2 weren't.

**Verified already-fine — do NOT touch (simplicity criterion):** wheels DO rotate (720° baked X-spin on
`lwheel`/`rwheel` + terrain-decal bones — the thing one would worry about is solved); the 5 rider clips
+ their `action_sets.xslt` injection; the 7 already-tagged gaits; the `*_head` look overlays; the
death/rear/strike substitutions → `chariot_stand_1` (intentional — `CanRear=false`, no `CanAttack`).

**Ranked follow-ups (full plan in [creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md)):**
1. ✅ **DONE 2026-08-02** — `tools/audit_mount_parity.py` section F now audits the chariot against
   the **vanilla-horse** baseline. It re-checks, from the deployed artifacts rather than from this
   doc's claims, the two failure modes that AV natively: every `animation=` target resolves to a
   real clip, and every clip bound in `monster_usage_movements` carries `quad_movement` (upper-body
   `*_head` overlays correctly do NOT — an earlier draft that flagged them was wrong). Current
   result: clean, 24/24 targets resolve, all 10 gait clips tagged. Ran during the dwarf-vs-Rhûn CTD
   investigation ([investigation-rhun-dwarf-ctd-2026-08-02.md](../reviews/investigation-rhun-dwarf-ctd-2026-08-02.md)),
   which is also where the horse-vs-chariot Monster attribute delta is recorded: the chariot ships
   no `fall_blow_damage_bone` and no ragdoll corpse/fall-sound bones. **Do not "fix" that blind** —
   the shipped, battle-proven warg lacks the same attributes, so absence alone is survivable.
2. Horse-team gait naturalness: check whether horse-B's legs are phase-locked to horse-A (unison "toy"
   look); desync horse-B via `phase_shift_bones` if so. Verifiable in Blender.
3. Cart bounce + rider standing-stability — defer (need art-direction + in-game).
4. Missing `as_human_map_with_banner` chariot rider rows (campaign-map rider falls back to default).

## Architecture

The chariot is a VEHICLE — the engine's vanilla mount AI drives it entirely, and there is **no
TAOM C#**. Unlike the elephant/spider, the chariot deliberately has **NO mount-lock** — the
maintainer wants chariots remountable mid-battle (2026-06-12 decision; matches the upstream pack, whose
`ROTAgentStatCalculateModel.CanAgentRideMount` is a pure passthrough). The item's riding
`difficulty="120"` is the only gate. (An earlier `ChariotConfig` + ternary-arm mount-lock was
built TDD-green and then deleted the same day when the design decision landed.)

The upstream pack's own chariot C# was decompiled and verified vestigial (`rot_decompile/` in the workspace):
an `ExtraChariotComponent` that caches the initial `MountManeuver` and is never read, attached by
a `ChariotMissionBehavior` whose tick is an empty null-check chain. Nothing to port.

The upstream pack's one-skeleton trick makes everything data: `chariot_skeleton` embeds the **complete
vanilla horse bone set by exact name** (A-set) plus a `B`-suffixed duplicate for the second horse
plus `root/test/pole/chariot/lwheel/rwheel`. Vanilla horse animations therefore bind to horse A
directly, and vanilla rein/harness systems (`horse_harness_rein_skel`) work unmodified.

### Asset pipeline notes (the non-obvious parts)

- **TpacTool's Assimp FBX export writes no legacy `Takes` section** — the Modding Kit enumerates
  animation takes from it, so Assimp-exported anim FBXs read as "no animations". Fix: round-trip
  through Blender (writes both `Takes` + AnimationStacks). Verified against working warg/elephant
  anim FBXs, which all carry `Takes`.
- **Animation Clips are pure metadata** (verified from a Kit-saved template: AnimationClip v0,
  zero data segments). All 24 clips were generated programmatically with TpacTool.Lib by cloning
  the upstream pack's original clip objects (full fidelity: flags, `QuadMovementUsage`/`MountChangeUsage`, step
  points, blends, priority) and re-pointing `Animation` at the Kit-imported masters. One
  `<name>_anm.tpac` per clip in `Assets/creature/chariot/animations/`.
- **SkeletalAnimation resources need their `Skeleton` GUID wired** (Kit import leaves it empty
  unless set in the editor): `anim_horse_all`/`anim_horse_all2` → `chariot_skeleton`
  (`0c6f9d61-…`), `animsallchariotrider` → vanilla `human_skeleton` (`dd7f3586-…`, stable GUID —
  matches what the upstream pack's own rider master referenced).
- **Single mesh — both horses (NO bone-count split).** The chariot horse mesh is ONE mesh skinned to
  **54 active bones** containing both horses. An earlier per-horse split (`chariot_horse_brown` +
  `chariot_horse_brown_2`) was **REVERTED 2026-06-13**: the disjoint 2nd horse as an `AdditionalMesh`
  would not render (only the base mesh drew — confirmed by an Option-E base/additional swap). The
  "~40-bone per-draw palette" that motivated the split is **false** — the elephant renders as one mesh
  skinned to **59 active bones**, so 54 in one mesh is safe. There is no per-mesh bone limit; the only
  cap is `Skeleton.MaxBoneCount = 64` (author ≤63). The merge fixed the missing horse. The Kit folds
  the merged Blender LODs into 5 Metameshes. See `feedback_no_40_bone_per_mesh_limit`.
- **Materials are 100% vanilla**: `horse_brown_mat`, `horse_tail_mane`, `horse_harness_e_test`,
  `horse_harness_imperial_b`, `roman_statue_chariot` (the vanilla hippodrome cart material —
  the upstream pack's cart texture set IS the vanilla statue set). FBX material slot names must be the bare
  engine names — Blender `.00x` suffixes break Kit binding.
- **Model viewer gotcha**: the viewer defaults entities to vanilla `horse_skeleton`; horse B's
  B-suffixed bones only exist in `chariot_skeleton`, so it renders collapsed until the entity's
  Skeleton dropdown is switched.
- **SkeletonUserData (physics/IK) transplant**: the Kit import auto-generates only a thin default
  UserData payload (6,567 B); the upstream pack's original carries the full authored set (17,648 B — hoof IK,
  per-bone hit bodies; same magnitude as the spider's 62-body set). Transplanted the upstream pack's segment
  verbatim into `chariot_correct_geo.tpac` after a bone name+order identity gate (60/60 match —
  UserData references bones by index). Script: `transplant_userdata.ps1`; backup `.bak-preik`.

### Action wiring (all in LOTRLOME_Armory ModuleData root — filename-convention loading)

| File | Content |
|---|---|
| `Monsters/LOTR/lotr_monster_chariot.xml` (+ SubModule.xml XmlNode) | upstream-pack Monster verbatim: family_type 4, weight 2000/HP 600, rider_sit_bone=chariot_bone, wheel terrain decals, Mountable, CanRear=false, no ragdoll_bone_* attrs (skeleton-side physics/IK comes from the transplanted SkeletonUserData: see pipeline notes) |
| `action_types.xml` | 23 `act_chariot_*` + 4 `actt_mount` (`act_mount_chariot_*` — the engine derives these names from `monster_usage="chariot"`) |
| `action_sets.xml` | `as_chariot` + `_town_and_village` + `_map`. **10 substitutions** → `chariot_stand_1`: the upstream pack mapped rear/strike/fall to `howdah_*`/`elephant_death_front_continue`/`elephant_rear`, absent from TAOM packs (death-visual polish is a follow-up) |
| `monster_usage_sets.xml` | upstream-pack `chariot` usage set verbatim |
| `action_sets.xslt` | `act_chariot_*` → `chariot_rider_*` rows injected into `as_human_warrior` (rider STANDS; same mechanism as the elephant block) + 4 mount actions → `chariot_mount_rider_from_right` |
| `monster_usage_sets.xslt` | `mount_id="chariot"`: 6 mountings (chariot mount actions + vanilla horse dismounts), 8 falls + 8 strikes (vanilla `act_fall_rider_*`/`act_rider_only_fall_*`) |
| `LOTRLOME_items/LOTRAOM_horses.xml` | `taom_chariot_a`: mesh `chariot_horse_brown` (a single mesh with BOTH horses, 54 active bones — the per-horse split was reverted, see above); AdditionalMeshes = mane + `chariot_harness_e_rein` + `chariot_ride` (NOT `chariot_ride_alt` — dead texture refs in the upstream pack itself); maneuver 25 / speed 70 / charge 80 (speed 55 / charge 90 until #615, 2026-09-17); `is_merchandise=false` |

## Configuration

No JSON/MCM config and no C#. Tuning lives in the item (`taom_chariot_a` Horse params: maneuver 25,
speed 70, charge 80 since #615, riding difficulty 120) and the Monster XML.

## Key Files

| File | Purpose |
|---|---|
| `Main/_Module/ModuleData/troops/troops_rhun_new.xml` | Horse-slot swap on the two wainrider chariot tiers (Horse `taom_chariot_a` plus HorseHarness `taom_chariot_armour_rhun`, see the barding section) |
| LOTRLOME_Armory (see "The rig since 2026-11-23") | Monster, actions, usage set, stylesheets, sounds, items, and the compiled packages under `Assets/creature/chariot/` |
| `tools/verify_mount_assets.py chariot`, gate `chariot-body` | The rig's two skeletons, the tag on each gait clip, and every clip a mount or rider row names |
| `tools/audit_mount_parity.py` section F, gate `mount-parity` | The chariot against the vanilla horse: attributes, usage rows, bindings |
| `tools/tpac_slim_package.py` | Built the rig package from the upstream chariot pack's package |
| `docs/reference/lotrlome-armory-snapshot/` | Five of the rig's ModuleData files as they were on 2026-11-24 |
| `Main/_Module/DsAssetPackages/taom_creature_skeletons.tpac` | The creature skeletons a dedicated server needs, the chariot's two among them |
| `Main/_Module/ModuleData/troops/troops_erebor.xml` | The two Ironpass chariot troops and the Ram Vanguard's second upgrade |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`, `special_resources/troop_resource_costs.xml` | Party weight (3.0) and Gems rows of the two troops |
| `tools/apply_dwarf_chariot_armory.py` | Holds the three dwarf chariot items and their names for the unversioned Armory: `--check`, `--apply`, `--remove`; gate `dwarf-chariot-armory` in `tools/data_gates.py` |
| `tools/taom_schema.py` (`DWARF_MOUNT_IDS`) | The ram team is a mount a dwarf may ride |
| `E:\LOTRAOMAssets\_auto_workspace\chariot\*.ps1` | The first rig's TpacTool-based generators (clips, skeleton wiring, inspection) |
| `LOTRLOME_Armory/AssetSources/creature/chariot/mesh/rhun_war_chariot_f.fbx` and nine maps `rhun_war_chariot_f_front_*`, `rhun_war_chariot_f_base_*`, `rhun_horse_armour_5_*` (`_d`, `_n`, `_s`) in `.../chariot/textures` | Sources of the black set (cart F v22 with the maps of v24, armour 5 `v18ay`, set v27), copied 2026-11-26, 09:22, and imported by the maintainer in the Kit at 09:36 to 09:38 (packages `Assets/creature/chariot/mesh/rhun_war_chariot_f_geo.tpac` and twelve map and material packages in `.../chariot/textures`). The harness item `taom_chariot_armour_rhun_black` in `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` draws the mesh |
| The chariot work folder under `E:\LOTRAOMAssets\_auto_workspace`, `out\rhun2\variants\f` | Work, iterations and gate tables of cart F; the dated record is `NIGHT_LOG.md` at the work folder's root |

## Recruitment

None added — the existing Wain tree upgrade path delivers the chariot (wain_youngblood → … →
wainrider_cavalry T6 → swift_chariot T8 → warlord_chariot T9). No VolunteerRecruitmentService
change, no clan wiring.

Erebor: no pool and no party template lists the two chariot troops. They are reached by upgrade only, from
`ironpass_ram_vanguard`, as the Ram Marshal is; `EreborRamCavalry_IsNotOfferedByAnyVolunteerPool` holds that.

## Tests

The chariot has no C# of its own, so no unit test of behaviour. The data and the tools are held by
`DwarfMountRiderRowsTests` (the dwarf rider rows, snapshot against live), `tools/tests/test_verify_mount_assets.py`,
`test_audit_mount_parity_strict.py`, `test_tpac_slim_package.py`, `test_apply_dwarf_chariot_armory.py` and
`test_armory_kick_typed_actions.py`, and by the data gates `chariot-body`, `mount-parity`, `dwarf-chariot-armory`
and `dwarf-rider-rows`. The in-game list is at the end of this doc.

## Provenance & rights

Two sources, at two times. This doc calls the first rig's source "the upstream pack" and the source of the
rig of 2026-11-23 "the upstream chariot pack"; they were two different downloads. On 2026-11-23 the maintainer said
that the rights for the second are cleared, and that the pack and its author are not named in tracked text. The
Rhûn cart and both dwarven chariots are TAOM's own geometry. The register row is
[provenance-register.md](../reference/provenance-register.md), "upstream chariot pack"; its status is the
maintainer's to change.

The first rig: art, animations and XML extracted from the **upstream pack** tpacs; rights to
ship confirmed by the TAOM maintainer 2026-06-12. The upstream pack's chariot itself builds on vanilla empire
hippodrome assets (cart material/textures are vanilla `roman_statue_chariot_*`). Extraction
tooling + format notes: `E:\LOTRAOMAssets\_auto_workspace\chariot\` + the transfer package
INVENTORY.md.

## Hit capsules fitted to the mesh (2026-09-18)

The chariot's per-bone hit capsules (what weapons and missiles strike) were the Modding Kit's defaults, thin rods
along each bone: 57 of 60 bodies, and only 8.8% of the horses-and-cart skin sat inside any hit capsule. `tools/skeleton_hit_capsules.py` fitted them to the skinned mesh (`chariot_horse_brown` with the cart baked in, plus the mane; harness and barding variants left out), a little bigger
than the skin and never more than 20 cm proud of it, and patched `Assets/creature/chariot/mesh/chariot_correct_geo.tpac` in the live Armory (backup
`chariot_correct_geo.tpac.bak-hitcapsules-20260918-201114`): 53 capsules refit, 98.0% of the skin now inside one. Both horses (legs, spine, neck, head, tail) are covered 97 to 100%. The cart is the exception: the cart body is a box skinned to one bone and the wheels are discs, which a round capsule cannot hug within the 20 cm limit, so the cart body kept its old capsule (27% of it covered) and the wheels reach about a third. Whether arrows striking the cart should damage the chariot at all is a design decision, left open. Read back through TpacTool.Lib with zero mismatches and every segment hash intact.
Method and format: [bannerlord-skeleton-authoring.md](../reference/bannerlord-skeleton-authoring.md) "Hit capsules".

**They survived a Kit re-import (2026-09-25).** The Armoury LOD pass re-imported `chariot_correct.fbx`, which
regenerates `chariot_skeleton`. Compared decompressed against the pre-import backup, the capsule, body and ragdoll
segment is byte-identical; the bone segment kept its order and parents and moved by at most 4e-6 (FBX round-off), so
nothing was restored. Check again after any future re-import
([armory-guide.md](../reference/armory-guide.md) "LODs in the FBX sources").
**Owed:** load LOTRLOME_Armory in the Kit once (re-cooks the package's `.rdc`), then a Custom Battle hit test.

The dwarven chariot has tests: `tools/tests/test_apply_dwarf_chariot_armory.py` (the Armory rows, the package
guard, drift and removal, and the live install), two `MountedDwarfTests` cases in
`tools/tests/test_validate_moduledata.py`, and in C#
`EveryTroopOnTheIronpassRamLineFromTheBreaker_CarriesUpgradeCostAndDailyUpkeep` and the two ids in
`EreborRamCavalry_IsNotOfferedByAnyVolunteerPool`.

## Status / pending

- ✅ ModuleData, compiled assets (skeleton + 5 Metameshes w/ upstream-pack IK transplant, 3 masters, 24 clips), validators
- ✅ **In-game verified 2026-06-13** — both horses render and animate in step, rider + cart + full gait set,
  IK intact (deep-review fixes + the single-mesh re-merge landed).
- ✅ `/deep-review` complete (6 reviewers + adversarial verify; HIGH jump-clip typo + parity gaps fixed, RCA filed).
- Black set (cart F with horse armour 5, 2026-11-26): sources, Kit packages and the harness item
  `taom_chariot_armour_rhun_black` in the Armory; owed: the check in game, and a troop that wears it (see the
  variants section).
- Follow-ups: black/white horse variants + `wide_chariot` (drop-in: single merged mesh, items exist in the
  upstream pack), proper mount-death clips (currently `chariot_stand_1` freeze), localization pass for
  `{=taom_chariot_a}`, fix `tools/blender/creature_anim_ops.py` stale `primary_bone_axis='X'` (L13).
  (`tools/audit_mount_parity.py` section F now covers the chariot vs the vanilla-horse baseline.)

## The missing second horse — resolved (2026-06-13)

The first in-game test rendered the chariot + rider + cart + ONE horse; the second was absent. Root cause,
confirmed empirically (not theorized): a fully-disjoint full-body mesh in the `AdditionalMesh` slot does not
render — the base mesh always draws, the additional disjoint horse never did (proven by an Option-E
base/additional swap: the missing horse stayed missing regardless of which horse was base). The per-horse
split that created that disjoint additional was motivated by a **false** "~40 per-draw bone limit". The
elephant disproves it (one mesh, 59 active bones, renders), so merging both horses into one 54-bone mesh is
safe and is the fix. The only real bone cap is `Skeleton.MaxBoneCount = 64` (author ≤63). Full correction +
cross-creature guidance: `feedback_no_40_bone_per_mesh_limit`; the spider/authoring/engine docs were corrected
in the same pass.

## Rhûn barding (swappable HorseHarness, 2026-06-13)

The chariot wears two-horse barding via a HorseHarness item, built from the single Rhûn kataphrakt
armor (`LRD_horse_armour_4`) duplicated to both horse positions: copy A at +0.5X on the A-bones, copy B
at −0.5X with vertex groups renamed `…→…B` on the B-bones (the chariot's horse B is a pure −1.0X
translate of A, so a translate — not a mirror — aligns it), joined per LOD → `chariot_horse_armour_rhun`
(42 active bones, 2 materials). Same construction as `chariot_horse_harness_imperial_b`; both are now
HorseHarness items (`taom_chariot_armour_rhun` / `taom_chariot_armour_imperial`, `family_type="4"` to
match the chariot), the Rhûn one equipped on the wainrider chariot troops.

Two non-obvious authoring rules came out of this (both in `feedback_custom_mount_harness_rules` +
`docs/ai-includes/creature-mount-authoring.md` Phase-4 rows 5–6):
- **A custom-skeleton harness mesh must ship INSIDE the creature FBX** that defines the skeleton (where
  imperial_b lives). A standalone `_notused` harness FBX **crashes the Kit** because the editor binds its
  `B`-set/cart bones to the stock horse skeleton, which lacks them. (First attempt crashed on import.)
- **A HorseHarness suppresses the Horse item's `<AdditionalMeshes>`** (native mount-compositing). The cart
  was an AdditionalMesh and vanished the moment the barding was worn. Fix: the cart + reins are the
  vehicle, not accessories — baked into the base `chariot_horse_brown` mesh (now 59 active bones) so they
  always render; only the mane stays an AdditionalMesh (hidden by the barding's `mane_cover_type="all"`
  anyway). All three render together, verified in-game.

## Dwarven war chariot (Erebor, 2026-11-24)

Two war rams pull an armoured cart for the Ironpass line of Erebor. It reuses the chariot rig as it is: the same
Monster (`chariot`), the same 60-bone skeleton `horse_skeleton_chariot` and the same clips. Only meshes, items and
troops are new, so there is still no C#.

**Meshes** (sources in `LOTRLOME_Armory/AssetSources/creature/chariot/mesh`, imported through the Modding Kit):

| Mesh | Slot | What it holds |
|---|---|---|
| `dwarf_chariot_rams` | Horse | Two war ram bodies on the two horse skeletons of the rig, material `M_EB_Ram_A1`. The horn span of each ram is scaled to 0.581 so the horns of the two rams do not cross. |
| `dwarf_war_chariot` | HorseHarness | Chariot a: an iron-plated cart (materials `dwarf_war_chariot_iron` and `dwarf_war_chariot_wood`, six 2048 maps in `.../chariot/textures`), the elite ram barding on both rams, a leather collar with iron hame plates, the yoke link and the reins. |
| `dwarf_war_chariot_b` | HorseHarness | Chariot b: the same rams, barding and collar with a steel cart built on the ram barding's own two materials (`M_EB_Ram_Barding_A1` for the plate courses with the gold band, `M_EB_Ram_Barding_A2` for the pointed tiles, leather and cloth). The frame, floor, axle and the wheel wood stay on `dwarf_war_chariot_iron` and `dwarf_war_chariot_wood`, so the mesh has four materials. It ships no texture of its own. |

The barding is the `SK_EB_Goat_Bard_Elite_B` set of `AssetSources/creature/ram/SK_EB_Goat_Bard_A.fbx`, placed on each
ram with the saddle seat removed. The rams stand 0.78 m apart because the rig fixes the two skeletons, and the
barding stands 5 to 18 cm off the skin, so the two bardings pass through each other between the rams. The
maintainer accepted that on 2026-11-24. The yoke bar sits 6.3 cm higher than on the unarmoured build so it clears
the neck plates.

**Items** (`LOTRLOME_items/LOTRAOM_horses.xml`):

| Item | Type | Notes |
|---|---|---|
| `taom_ram_chariot_a` | Horse | The ram team. Its Horse row copies `taom_chariot_a` (maneuver 25, speed 70, charge 80, riding difficulty 120, `Monster.chariot`); neither has a `skeleton_scale` (see below). |
| `taom_chariot_armour_dwarf` | HorseHarness | Chariot a. `family_type="4"`, `UseTeamColor`. |
| `taom_chariot_armour_dwarf_b` | HorseHarness | Chariot b. `family_type="4"`, `UseTeamColor`. |

The Armory is unversioned, so `tools/apply_dwarf_chariot_armory.py` holds these three rows and their three English
name rows: `--check` fails when the live files differ from it, `--apply` puts them back after a reinstall (it
refuses while a mesh or material has no Kit-cooked package), and `--remove` takes out only its own rows.

The cloth of the barding and the two rear pennants of chariot b take the team colour: `t_eb_ram_barding_a2_d2.png`
is non-zero only over the grey cloth area of the A2 sheet, and the pennants are mapped inside that area.

**Troops** (`troops_erebor.xml`): `ironpass_ram_vanguard` (level 36) gains a second upgrade, to
`ironpass_war_chariot` (level 41, chariot a, the Ram Marshal's kit), which upgrades to `ironpass_steel_chariot`
(level 46, chariot b, the top iron armour, with a second helmet and bracers in its second set). Both keep the line's weapons: spear, shield and axe. The Erebor emissary sells both for Gems (`elite_emissary/elite_emissary_config.xml`, 18 and 22 before the resource's multiplier), beside the three ram troops it already sold. Their skills are
what `tools/rebalance_troops.py` writes for their level and kit. Each counts 3.0 party slots
(`TroopWeights/troop_weights.xml`, the level rule, as the Ram Marshal). In Gems
(`special_resources/troop_resource_costs.xml`) the War Chariot pays the Ram Marshal's row, so the Ram Vanguard's two
upgrades cost the same, and the Steel Chariot pays one rung more. The line's axe is two-handed, so a rider draws it
only after the shield breaks; the two troops have the same ratchet rows in `tools/audit_polearm_shield_parity.py` as
the ram riders.

**A dwarf on a chariot.** `MOUNTED_DWARF` forbids a dwarf on a mount because the dwarf skeleton does not fit the
horse seat. The ram team is on the allowlist (`DWARF_MOUNT_IDS` in `tools/taom_schema.py`) for another reason than
the war ram. The engine places every rider the same way, his clip against the mount's sit bone, and on this
Monster that bone is `chariot_bone`, not a horse spine bone. The rider plays the chariot rider rows that
`as_dwarf_warrior` binds (all 47 rider action types, which the two other dwarf sets inherit). Those clips are made
for the human skeleton, so where a dwarf stands on the cart is decided by the in-game check below, and the id comes
off the list if he stands wrong. The Wainrider horse team `taom_chariot_a` is not on the list.

**No `skeleton_scale` on a chariot team.** `taom_chariot_a` carried `skeleton_scale="empire_horse"` from the
upstream pack until 2026-11-24. The engine applies a scale by bone index of the scale's own skeleton (`horse_skeleton`), not by name
(`MBGameManager`, then native `ApplySkeletonScale`, which checks only the index bound). On `horse_skeleton_chariot`
the five indices are `chariot_bone` (the root), `horsespine2_chariot_left`, `horsepelvis_chariot_right`,
`horsespine1_chariot_right` and `horsespine2_chariot_right`, so the right animal is stretched by up to 15 percent
and the left one is not. The ram meshes were fitted to the unscaled rig, so the ram team has no scale. The maintainer
had it removed from `taom_chariot_a` too on 2026-11-24, in the live Armory; the Rhûn cart and horse armour were also
fitted to the unscaled rig.

**Known limits.**

- The ram team uses `Monster.chariot`, so it has the horse voices of the Wainrider chariot. A second Monster with
  ram voices is not made.
- Both chariots are `family_type="4"`, so in the inventory the Rhûn barding fits the ram team and a dwarf harness
  fits the horse team. No troop is equipped that way. The dwarf cart is part of the harness mesh, so a ram team
  with no harness, or with the Rhûn harness, has no dwarf cart. All three items are `is_merchandise="false"`.
- `UseTeamColor` reaches every material of the harness mesh, the cart's iron and wood too. What it does on a
  material with no colour mask is not proven.
- `mesh_preflight` reports the far LODs of both harness meshes as FAIL ("seen only from behind") on the barding's
  stacked plates. Back-face-culled views of LOD3 and LOD5 show no hole, and flipping those faces opened real
  holes, so the faces stay as the artist made them.
- The pole shows behind the rams' rumps and at the yoke, and 14 pole faces show from above between the rams' backs.

**Not yet checked in game** (the steps are in [How to verify in game](#how-to-verify-in-game)): where the dwarf stands on the cart, the collar and the reins in a gallop and in a
turn, the far LODs, the team colour on the cloth, and the two troops in a battle.

## Rhûn cart variants and new horse armours (in work since 2026-11-24)

Five more carts for the harness slot of this rig (B to E in new designs, F in cart A's design in black) and five
horse armours of our own for the chariot horses. One cart and one armour make one harness mesh; the swap step
(`rhun2_armour_swap.add_armour`) puts the armour on both horses. Nothing of it is in the game. Since 2026-11-26, 09:22 the sources of the black set (cart F and armour 5, set v27) are in the Armory's `AssetSources/creature/chariot` (mesh `rhun_war_chariot_f.fbx` and nine maps, see the state paragraph), and the maintainer imported them in the Kit the same morning (13 packages). Since 09:56 the harness item `taom_chariot_armour_rhun_black` draws the mesh; no troop wears it yet. The
record is the chariot work folder under `E:\LOTRAOMAssets\_auto_workspace`: its `NIGHT_LOG.md` (newest entries at
the end) and the hand-overs `out\armours\HANDOVER_agent1.md` (armours 1 to 3), `out\armours\HANDOVER_agent2.md`
(armours 4 and 5) and `out\rhun2\HANDOVER_carts.md` (carts). They are newer than this section when the two disagree.

**Working rules of 2026-11-25 (the maintainer).**

| Rule | His words | What follows |
|---|---|---|
| One set at a time | 13:10: "Lets work on one cart and one horse armor set, when that is perfect move onto the next one"; evening: "Are we still only working on one set?" | The black set keeps the priority. The session's reading, not his words: spare capacity goes to set B. Since 18:36 armour 1 v30 goes on by fresh builders (`armour1-v30-review-blocks`, `wf_ee65c8a5-a0f`: four blocks, a read-only checker and one retry each; slot owner `armour1`; limit 9,800 at LOD0; its approved surface stays). Armours 2 to 4, carts A to E stay untouched. The way back: stop that run. The usage limit of about 20:30 stopped this run (see the state paragraph) |
| Blender and memory | 13:10: "We have like 36 blenders open, can we close some?", "LEts also work on one blender item at a time to save memory" (read at 13:07: 49 Blender processes with 37.8 GB, 4.2 GB of 61.6 GB free); evening (log entry 18:35): "You can use more blender, just watch out for the memory. I will not be using the computer for the most part." | The limit is free memory, not one job. Every Blender job of the builders starts through `blender_slot.py` (work folder root; slots and a memory floor since 18:32): 3 slots and a floor of 14 GB from 18:32, and since 19:24 6 slots and a floor of 10 GB (`out\_blender_slot_config.json`); a job starts only with the floor of physical memory free, plus 3 GB for each of these jobs started in the last 90 s (6 GB when none runs); the other owner goes first after an owner's turn; exit 97 when no slot comes free in time. A review runs one reviewer at a time |
| Docs | Log entry 18:35: "Ensure you update all documentation as you go"; 2026-11-26, about 09:24: "ultracode is on. This set was amazingly done. Make sure to update all documentation!" | The docs run `chariot-docs-update-2` (`wf_299c3511-b06`, no Blender) finished at 18:46 on 2026-11-25 (this doc, the asset gates doc, seven lessons, current to 18:41). On 2026-11-26 a second run (`black-set-docs-sweep`, `wf_78b9db73-928`: an auditor, a writer and a checker for each of three doc areas, no Blender) brought the docs and the hand-overs to the state of the black set's install; the session corrected the 32 errors its checkers found. The edits stay uncommitted, as a commit on this branch needs his word |
| Triangle count | 13:02: "I accept the triangle count for this. BEcause a we have two horses and a cart, that is like 3 troops." | The 60,000 gate on a set is closed as a blocker; the manifest flags a set above 65,400 (the session's rule). For the black set he accepted about 66,600 at the neck question |
| Wood colour | 13:44: "The carts woods can also be different colors of brown, black, red, etc." | Cart F's round one: three Painter wood colours for his choice (dark brown, a black stain that still reads as wood, dark red-brown; fields black, trim gold) |
| What he sees | 13:36 to 13:44 | A build only after the session's close review finds no fault; the three wood colour pictures are the exception. To "You have anything to show, or still doing work on one kit?" (log entry 17:03) the session opened one picture marked as in work and not reviewed (`out\status\img\armour5_v18_in_work.png`) |

**The six carts.** The pairing of B to E is the session's proposal; he reviewed the sets as paired.

| Cart | Design | Armour | Wood | The maintainer's verdict |
|---|---|---|---|---|
| A `rhun_war_chariot` (in the game) | Scale plates, fan panels, scythe wheels | its Armory armour, none of the new ones | as installed; he named no choice, so it does not change | "Cart A, we can remove the quiver and javelins and save on some triangles." (2026-11-26; build v06, sources replaced the same day, his update in the Kit owed) |
| B `rhun_war_chariot_b` Reaver | Open lattice frame, red leather X straps, pierced spearhead standard | 1 raider | Painter | "Cart B - Painter wood"; "remove the javelins from cart B too" (waits for set B) |
| C `rhun_war_chariot_c` Sun Car | Fan front wall, lamellar walls, gold disc wheels | 2 sun | Painter with the delivered gloss | "Cart C - Painter wood, delivered gloss" |
| D `rhun_war_chariot_d` Lacquer Court | Red lacquer sun panels in gold frames, gold scale body | 3 lacquer | Painter | "Cart D - Painter wood"; "Once we fix the issues this cart set up is AWESOME"; of the inner fan: "This serves no purpose and wont ever be used, waste of triangles and polys remove it." (gone since D v17) |
| E `rhun_war_chariot_e` Spear-prow | Plank walls, bossed shields, spears along the pole | 4 horned | source wood | "cart e - source wood" |
| F `rhun_war_chariot_f` Black | Cart A's shapes as a variant build with its own slots and maps: black fields, bright polished gold trim, wood, steel blades; no javelins, arrows or cases | 5 black | Painter wood, black-stained (his choice of 2026-11-25: "Wood B Black Stained is the best for the black set"; cart F v24), made in Part 2 after the build (F's new UVs share 0 of 10,984 base triangles with cart A's, so cart A's Painter redo does not fit) | "My expectation is if the horse armor is black, the cart must be the same color. Black, gold trim and wood"; "The black cart looks good with the black horse armor (rhun set F)." On 2026-11-26: "the only problem is this is messed up and not straight and then the armor and cart are good after this fix."; "put set 27 into the asset sources. This is amazing"; "ultracode is on. This set was amazingly done. Make sure to update all documentation!"; after his Kit import: "done, looks good!" |

**State on 2026-11-26, 09:40.** At about 20:30 on 2026-11-25 the weekly usage limit stopped every builder and
reviewer agent (it resets on 2026-11-29); the maintainer's word after it: "proceed throughout the night without
me". The session went on alone, with no review agent, so nothing below is reviewed. The black set stands as set
v27: cart F v22 with the maps of v24 and armour 5 `v18ay`, 67,143 triangles at LOD0, 0 pairs with bodies and
armours in the five neck poses. Three things in it are the maintainer's word. The newest, of the morning of
2026-11-26, on set v26 in Blender with a circle round each end of the lowest neck band: "the only problem is this
is messed up and not straight and then the armor and cart are good after this fix." Each end showed as a gold
spike with a notch: a clearance rule had lifted the band's base field at the lower corner to 73 mm over the skin (the
flare of 12 mm and the lip come on top). One vertex of the curtain's top band, 55 mm below the band's edge, had set
a lift of about 71 mm there. In `v18ay` a part past the band's edge does not count at the band's
ends, and each end is a rounded end in line with the ends above it; the gates equal `v18av` but for four
counts (the work log, 08:40). The other two are of the evening of 2026-11-25. The wood: three Painter colours were shown
(dark brown, black-stained, dark red-brown; sheets in `out\rhun2\_work\f_review\wood_preview\`), and he chose:
"Wood B Black Stained is the best for the black set" (cart F v24, a texture-only version of v22). The neck strap:
on set v23 he circled a strap piece that lay on top of the last neck band, "Both sides the tab should be
under..."; in `v18av` the strap stays under the band along its whole run (4 crossing face pairs a side at LOD0,
from 42 and 48), with every gate equal to `v18aq`. The look scene (`out\view\rhun_cart_F_black_VIEW.blend`) is
set v27 and was opened for him as work in progress (08:39, in Blender).

Shortly before 09:21 he said: "put set 27 into the asset sources. This is amazing". At about
09:24 he said: "ultracode is on. This set was amazingly done. Make sure to update all documentation!" At 09:22:16 ten
files of set v27 were copied into the Armory's `AssetSources` (record `out\rhun2\variants\f\_work\v27\assetsources_install.json`
in the chariot work folder; no Kit or game process ran before the copy):

- `creature\chariot\mesh\rhun_war_chariot_f.fbx` (7,619,660 bytes, SHA-256
  `3bb65df21ba8d3d68f619f96525d6e92e4e4a934527a0f32000f15fdf6f3c19f`).
- In `creature\chariot\textures`: `rhun_war_chariot_f_front_d.png`, `_front_n.png`, `_front_s.png`,
  `rhun_war_chariot_f_base_d.png`, `_base_n.png`, `_base_s.png`, `rhun_horse_armour_5_d.png`, `_n.png`, `_s.png`
  (2048 x 2048 RGB).
- Nothing was overwritten (cart A's seven files keep their dates of 11-24), and each target's hash equals its
  source. The copy wrote no package, no ModuleData row and nothing else in the game install.
- The FBX has one armature `horse_skeleton_chariot` (60 bones, the same names in the same order as cart A's), six mesh
  objects (LOD0 to LOD5: 67,143 / 54,120 / 31,268 / 26,003 / 7,976 / 4,449 triangles) and on every level the slots
  `rhun_war_chariot_f_front`, `rhun_war_chariot_f_base` and `rhun_horse_armour_5`. Unlike cart A's file it has no
  vertex colour layer. Cart A's file has one (its cart faces carry 0, its armour faces 1) and so do the two dwarf
  carts (their cart faces carry 0); `dwarf_chariot_rams.fbx` has none. All of them were imported and ran in game on 11-24, so a
  missing layer did not stop an import and colour 0 did not darken a cart. His import of 09:36 to 09:38 settled it
  for the Kit: the file imported, and his picture of the Kit's viewer shows the set with its colours.

The set's gate table (`variants\f\_work\v27\gate_table\MANIFEST.md`) is HELD on four rows: the count flag (67,143 at
LOD0, above 65,400), the 2 hidden hub discs (link gate), the armour's class a faces (54 at LOD0 to LOD2: 22, 3, 29; the row also lists 7 at LOD3)
and the cart's UV check at the far levels. He chose the import before the cart's far levels and hub discs are repaired. No reviewer has looked
at set v27.

He imported the set in the Modding Kit at 09:36 to 09:38: 13 new packages in the Armory
(`Assets\creature\chariot\mesh\rhun_war_chariot_f_geo.tpac`, 14,048,332 bytes; nine map packages and three material
packages in `Assets\creature\chariot\textures`). The mesh package has its entry in the runtime data cache
(`tools/check_rdc_entries.py`, which checks mesh and clip packages only, because map and material packages render
without one). Shortly before 09:40 he sent a picture of the set in the Kit's viewer: "done, looks good!"

At 09:56, on his word ("add it into the XMLs, new item. We will make a special troop for Khamul's line in Rhun"),
one harness item went into the live Armory's `ModuleData\LOTRLOME_items\LOTRAOM_horses.xml`, between
`taom_chariot_armour_rhun` and `taom_chariot_armour_imperial`, with the values of the first. The Armory is not
versioned and no tool puts this row back, so its text is kept here:

```xml
    <Item
        id="taom_chariot_armour_rhun_black"
        name="{=taom_chariot_armour_rhun_black}[Rhûn] Black Easterling Chariot Barding"
        mesh="rhun_war_chariot_f"
        culture="Culture.khuzait"
        weight="50"
        is_merchandise="false"
        appearance="0.65"
        Type="HorseHarness">
        <ItemComponent>
            <Armor
                body_armor="50"
                mane_cover_type="all"
                family_type="4"
                material_type="Chainmail" />
        </ItemComponent>
        <Flags Civilian="true" />
    </Item>
```

The display name and the copied values (armour 50, weight 50) are the session's choice until he gives others.
No troop wears the item yet; the two older Rhûn harness items still draw cart A's `rhun_war_chariot`. Its English
name row is in the Armory's `Languages\loc_LOTRAOM_horses.xml` (`tools/register_armory_names.py`), and the twelve
translations wait for the language pass. The repo's per-item tables hold it on this branch:
`tools/armour_cover_flags.json`, `tools/data/armour_colour_families.json` (family black),
`tools/external-loc-coverage-baseline.json` (one more untranslated name in each language) and the one changed line
of `docs/reference/armory-catalogue/lod-audit.md` for the new source file. Until those reach trunk,
`tools/check_external_loc_coverage.py` fails on trunk for every session, because the shared Armory already holds
the new name. A reinstall of the Armory drops the row: `tools/apply_armour_cover_flags.py --check` then reports the
id as missing, and once a troop names the item `tools/validate_moduledata.py` does too.

The review of the row on 2026-11-26 (the data flow, engine, XML and tooling lenses, and a Codex pass) found no
defect in it: every attribute is read for a horse harness on v1.5.7, and nothing sells it, gives it as a prize or
as loot, or equips it by chance. `family_type` 4 is the war chariot's, so in the inventory the item also fits the
dwarven ram chariot, as the two older Rhûn harness items do. Owed in game, by a person: add
`taom_chariot_armour_rhun_black` and `taom_chariot_a` with the cheat console, equip both, and ride into a battle
(cart F and the black armour on both horses, nothing stretched, no missing mesh line in the log).

**The raider set (set B) in the game, 2026-11-26.** In the evening the maintainer looked at the staged sets B to E in
one Blender scene and said of set B (cart B v19 with horse armour 1 v23): "chariot b and armour 1 look good enough to
put into the game." The set that went in is set v24: armour 1 v23, the build he looked at, on cart B v23, which is
cart v21 without its javelins and their lashings (his order of 2026-11-25; 3,556 triangles at LOD0). The set has
43,490 triangles at LOD0 and passes every set gate. Later armour 1 builds (gold crest fins, sewn strap ends) are not
in it: he had not seen the newest one, and its review lists faults. Known and approved with the set: strap ends on
studs at the withers and on the lowest neck band, and the black feather crest.

At 19:31 ten source files went into the Armory's `AssetSources\creature\chariot` under new names
(`mesh\rhun_war_chariot_b.fbx`; `textures\rhun_war_chariot_b_front_*`, `rhun_war_chariot_b_base_*` and
`rhun_horse_armour_1_*`, each `_d`, `_n`, `_s`), with equal hashes. He imported the set in the Modding Kit at 19:48 to
19:50: `Assets\creature\chariot\mesh\rhun_war_chariot_b_geo.tpac` (8,459,713 bytes), nine map packages and three
material packages. At 19:56 one harness item went into `LOTRAOM_horses.xml`, between `taom_chariot_armour_rhun_black`
and `taom_chariot_armour_imperial`, with the values of `taom_chariot_armour_rhun`. No tool puts the row back, so
its text is kept here:

```xml
    <Item
        id="taom_chariot_armour_rhun_raider"
        name="{=taom_chariot_armour_rhun_raider}[Rhûn] Raider Chariot Barding"
        mesh="rhun_war_chariot_b"
        culture="Culture.khuzait"
        weight="50"
        is_merchandise="false"
        appearance="0.65"
        Type="HorseHarness">
        <ItemComponent>
            <Armor
                body_armor="50"
                mane_cover_type="all"
                family_type="4"
                material_type="Chainmail" />
        </ItemComponent>
        <Flags Civilian="true" />
    </Item>
```

The id, the display name and the copied values are the session's choice until he gives others: the set is a light
cart with neck bands and a back cloth, so a lower armour value than the Wainrider barding's 50 may fit it better.
No troop wears the item. Its English name row is in `Languages\loc_LOTRAOM_horses.xml`, and the twelve
translations wait for the language pass. The repo's tables hold the item: `tools/armour_cover_flags.json`,
`tools/data/armour_colour_families.json` (family bronze, measured on `rhun_war_chariot_b_front_d`) and
`tools/external-loc-coverage-baseline.json`. Owed in game, by a person: add `taom_chariot_armour_rhun_raider` and
`taom_chariot_a` with the cheat console, equip both, and ride into a battle.

**The lacquer set (set D) in the Armory's sources, 2026-11-26.** In the same look at the staged sets he said: "Cart D
is ready for import. Looks real good." and "Armour 3 v06 just needs the front armour to be fastened to the head armor
so it isnt hanging". Set v21 is cart D v17 with horse armour 3 v16: 58,353 triangles at LOD0, the armour 9,468 for one
horse, every set gate passed. Build v14 changed skin weights only against v13 (see "The armours in motion"), and
build v16 the meshes of LOD2 and LOD3 only (see "The far levels"). The chest panel now hangs from two straight straps with sewn laps, from the breast
strap up to the neck lining under the lowest neck band. A read-only check with close renders found it ready. Against
the build he looked at, these also changed: the face plate is one shell with round rope eye rings (his word of
2026-11-25), the browband runs under the face plate's top band and is lapped onto both cheek straps, the flank and
rump panels have hangers that end under their plates, and the cart has no inner fan screen (his word of
2026-11-25). Open and small: the browband's bend shades differently on the two sides at close range, and the face
plate's lower end has no strap, as in the build he saw.

At 20:48 ten source files went into `AssetSources\creature\chariot` under new names
(`mesh\rhun_war_chariot_d.fbx`; `textures\rhun_war_chariot_d_front_*`, `rhun_war_chariot_d_base_*` and
`rhun_horse_armour_3_*`, each `_d`, `_n`, `_s`), with equal hashes; that was set v19, with armour v13. At 03:40 on
2026-11-27 the mesh was replaced by set v20's and at 07:45 by set v21's, each time with a kept copy of the replaced
file. The import in the Modding Kit and
the harness item are owed: an item row that names a mesh the Kit does not hold yet hangs the preload, so the row follows the
import.

**The sun set (set C) in the Armory's sources, 2026-11-27.** Of the staged sets he said on 2026-11-26: "chariot c is
good, looks real good. Is ready to be imported." and "armour 2 needs work, remove the ear armor on the horse that is a
waste. Then please attach the front armor with straps that are hidden in the back. Having armor not fastened down is
real." Set v24 is cart C v16, unchanged against the stage he saw, with horse armour 2 v18: 59,460 triangles at LOD0,
the armour 8,903 for one horse. The armour has no ear pieces. The chest panel hangs from two straps: each has a
square stitched lap on the panel's band, one fold at the band's top edge, and a straight run under the shoulder
plate's rear corner to its upper end under the back plate. Each shoulder plate is held by a carrier strap to under
the back plate and by the neck band under its front corner. It took four builds (v11 to v14); a read-only check with
its own renders of the staged file found v14 ready.

That check's list of what carries each part showed that no strap held the back plate, from which the chest straps,
the carriers and the flank panels hang. Build v15 adds a girth: an upright red band behind the elbow with a buckle
on each side, its ends under the back plate's rim. Nothing else of v14 moved and no map changed; a second read-only
check found it ready and better than v14. Build v16 changes skin weights only (see "The armours in motion"),
and build v18 the meshes of LOD2 and LOD3 only (see "The far levels").

Open on this armour: the rump plate has no strap of its own (its front row lies under the back plate's rear rim);
the chest plate has no strap of its own (its sides lie under the shoulder plates); the girth is not drawn at LOD4
and LOD5, and the chest straps and the carriers not from LOD3 on; the shoulder plate's front tab holds only near the
rest pose; 448 triangles at LOD2 have no UV area.

At 00:07 ten source files (then set v21, with armour v14) went into `AssetSources\creature\chariot` under new names
(`mesh\rhun_war_chariot_c.fbx`; `textures\rhun_war_chariot_c_front_*`, `rhun_war_chariot_c_base_*` and
`rhun_horse_armour_2_*`, each `_d`, `_n`, `_s`), with equal hashes. At 01:03 the mesh was replaced by set v22's
at 02:30 by set v23's and at 07:45 by set v24's, each time with a kept copy of the replaced file; the nine maps are
the same for all four. The import in the Modding Kit and the harness
item are owed; the item row follows the import.

**The horned set (set E) in the Armory's sources, 2026-11-27.** Of the staged sets he said on 2026-11-26: "Armour 4
horse armor - details matter, fine tune this details!" and "Cart E - Looks good, the only thing I would change is the
texture of the wood being the same direction if we can. Right now it is all over the place." Set v16 is cart E v13
with horse armour 4 v26b: 58,991 triangles at LOD0, the armour 9,477 for one horse (cap 9,500).

On the armour, every strap end that sat on a stud is now a made joint: the neck straps end under the lowest neck lame
and on a ring on a riveted tab on the chest plate; the cheek straps end on ring tabs on the face plate and in sewn
laps on the noseband; the withers, back and rump straps end in sewn laps; each shoulder scale field has a top band
that two withers straps are sewn onto. The scale fields have even borders and no slits at the row ends, and the
neck lames lap closed. A first check found two faults (neck strap tops in the air; eye rims and the poll that
changed shape at the LOD switch); build v25e repaired both, and a second read-only check of the staged file found it
ready. Build v26b changes skin weights only (see "The armours in motion"). On the cart, the wood grain now runs along the boards on the inner front wall and the top planks; the inner
side wall slabs still have level grain under upright battens.

Open on this set: the front field's backing opens into slats at the leg pit in the gallop (see "The armours in
motion"). The eye openings at LOD1 and LOD2 have an angular edge close up. The shoulder band
ends stand 10 to 30 mm off the backing in close views from above.

At 00:52 ten source files went into `AssetSources\creature\chariot` under new names
(`mesh\rhun_war_chariot_e.fbx`; `textures\rhun_war_chariot_e_front_*`, `rhun_war_chariot_e_base_*` and
`rhun_horse_armour_4_*`, each `_d`, `_n`, `_s`), with equal hashes. At 02:24 the mesh was replaced by set v16's,
with a kept copy of the v15 file; the nine maps are the same for both. The import in the Modding Kit and the harness
item are owed; the item row follows the import.

**Cart A without its quiver and javelins, 2026-11-26.** Of the cart that is in the game he said: "Cart A, we can
remove the quiver and javelins and save on some triangles. They dont really do anything." Build v06 of
`rhun_war_chariot` has no javelin case and javelins on the right wall and no bow case and arrows on the left wall,
with their straps, mounts and lashings. The wall faces and the right wall boss that those parts covered are built
again; the right boss is a mirror of the left one at LOD0 to LOD3. The maps give back the light under the removed
parts, and the two strap prints on the right top rail are gone. The horse armour did not change. The whole mesh went
from 59,173 to 57,684 triangles at LOD0 and from 16,817 to 16,581 at LOD3.

At 23:03 five source files in `AssetSources\creature\chariot` were replaced under their old names
(`mesh\rhun_war_chariot.fbx`; `textures\rhun_war_chariot_front_d.png`, `_front_s.png`, `_base_d.png`, `_base_s.png`),
each with a kept copy of the file it replaced and an equal hash after the copy. The two normal maps stay as
installed. The items do not change: the mesh name is the same. The game shows the new cart only after the maintainer
clicks update on the mesh and its maps in the Modding Kit; that step is owed.

**The armours in motion, 2026-11-27.** A check of the horned set found that armour 4's haunch plates parted in one
of the pose gate's own leg poses. A study then read the game's clips of the chariot horses. In the gallop the thigh
bone swings back 41.9 degrees, where the pose gate tests 30, and armours 2, 3, 4 and 5 came apart in every gait and
in the idle clip; armour 1 showed no gap. The cause was skin weights. Each plate, lame or neck band followed one
bone, chosen at its centre, and the backing or cloth under it followed the skin over several bones, so a plate slid
off its backing when a leg or the neck moved. No gate measured a part against the part it lies on, and no gate
posed the rig with a frame of a real clip.

The repair changes weights only: a plate takes the weights of the backing under it, and the neck lames take a blend
along the neck. Positions at rest, UVs, triangle counts and maps stay as they were, with one exception on armour 4,
where two neck lames moved 0.254 mm at LOD0 and 3.136 mm at LOD3. The build scripts are in the work folder; each new
key is off unless an armour's design file asks for it, and a rebuild of every other armour that shares a script was
compared with its existing build (positions, weights and UVs equal at six LODs).

| Armour | Build | Worst slip between neighbours, before and after | State |
|---|---|---|---|
| 2 | v16, in set C v23 | front lames 555 mm to 42 mm, rump lames 323 mm to 14 mm (gallop) | installed at 02:30 after a read-only check found it ready |
| 4 | v26b, in set E v16 | front field 765.5 mm to 127.8 mm, rear field 559.4 mm to 60.4 mm (gallop) | installed at 02:24: equal at rest and better in every motion measure than the build it replaced. Its check did not pass it (see below) |
| 3 | v14, in set D v20 | neck bands 167.9 mm to 13.3 mm in the idle clip; the chest skirt's turned-over faces in the gallop 102 to 5 (the check's count) | installed at 03:40. A read-only check found it ready: nowhere worse than the build it replaced, at rest or in motion |
| 5 | v19w2, a candidate | skirt 507.1 mm to 86.5 mm (gallop), cape 245.1 mm to 44.2 mm (idle) | not installed. The black set is approved and in the game, so its files change only on the maintainer's word. The candidate is one mesh file in the work folder; the nine maps are the installed ones |

Open after the repairs:

- Armour 4's front field: in the gallop the lattice backing ahead of the forelegs stretches up to 8.4 times and 13 to
  17 faces turn over, so it opens into slats at the leg pit. Weights cannot close it: every variant that cut the
  stretch pushed the foreleg through the field. It needs a change of the backing's shape, which changes the armour
  at rest and is not started. The check of set E v16 counted a black patch there and did not pass the set; most of
  that count came from the check's own render script, which turned the normals again at every pose (3,269 dark
  pixels for one frame inside a pose series, 550 for the same frame rendered alone).
- Armour 2: the front skirt folds over the forearm at two gallop frames, and its backing still slips 196 mm against
  the shoulder plates and the chest plate.
- Armour 3: a few faces of the chest skirt still turn over at one gallop frame (5 by the check's count), and a small
  dark patch shows there at 8 m; the build it replaced tore there. The front tassels shorten to 0.881 of their
  length in the gallop. At LOD2 the lining shows as large black areas on the neck and chest already at rest, as in
  the build before.
- Armour 5, as a candidate: the lowest haunch rows pull apart at five frames; a second attempt with smoother weights
  was worse and was dropped.
- The slip measure is not yet a gate of the armour build chains, and no set was checked in the game.

**The far levels, a gate for motion, and what the gate found, 2026-11-27.** Three things followed the weight repairs.

*The far levels.* The checks of the repaired sets found that armours 2 and 3 showed lining and backing at LOD2 and
LOD3 already at rest, where LOD0 shows plates. On armour 3 the neck bands had 5 points across at those levels where
LOD0 has 11, and each chest slat strip was one flat quad; on armour 2 each lame strip of the flank and rump was one
flat quad with a row of backing behind it, and the neck lames were too coarse to cover their backing. Armour 3 v16
and armour 2 v18 rebuild LOD2 and LOD3 only; LOD0, LOD1, LOD4, LOD5 and the maps are identical to the builds before.
A read-only check measured both sets against the installed ones, with back faces culled as the game draws them:

| Set | Lining or backing that shows and LOD0 does not (pixels, side view) | Change at the LOD1 to LOD2 and LOD2 to LOD3 switches (pixels, three views) | Triangles, whole set |
|---|---|---|---|
| D v21 (armour 3 v16) | LOD2 860 to 276, LOD3 235 to 29 | 3,486 to 1,608 and 1,029 to 664 | LOD2 24,545 to 27,497, LOD3 14,517 to 16,301 |
| C v24 (armour 2 v18) | LOD2 1,152 to 185, LOD3 214 to 40 | 3,374 to 886 and 1,382 to 596 | LOD2 25,857 to 28,217, LOD3 15,076 to 17,356 |

Both were found ready and replaced the installed meshes at 07:45. One switch got worse by measure: on set C the
change from LOD3 to LOD4 at about 28 m went from 672 to 1,170 pixels, because LOD3 now has the full neck lames and
the unchanged LOD4 keeps a stepped hood; on a 1080p frame it is a few pixels of crest outline. A first attempt was
stopped by an acceptance rule that asked for a lower difference in every view; the far levels were already far
better, and the rule was the fault.

*A gate for motion.* The slip measure of the study is now gate G15 of both armour build chains and of the stage
script (in the work folder: `rharm_slipgate.py` with a table of pairs per armour and a baseline). It poses the rig
in the gate poses and in every key frame of 16 clips and measures each declared pair of parts. A build fails when a
pair's worst slip is above the installed build's by more than 10 percent plus 2 mm, and a set whose armour failed
or skipped the gate cannot be staged. Proof: the five installed armours pass; armour 2 v15, armour 3 v13 and armour
4 v25e, the builds of before the weight repair, fail (armour 4 v25e on its front field at 765.5 mm against a limit
of 142.6 mm). The gate measures LOD0 only and only the pairs its table names.

*What the wider gate found.* Eleven pair classes were above 150 mm in installed builds. A study rendered each at
its worst frame: nine are faults and two are artefacts of the measure. The first study of the night had said that
armour 1 showed no gap; that was wrong, it had looked at too few frames.

| Armour (set) | What a viewer sees | State |
|---|---|---|
| 1 (B, in the game) | In the idle clip the neck lames part on the right side, a true gap of up to 149 mm | A weights-only candidate brings it to 20 mm and was found ready by a read-only check; not installed: set B is in the game |
| 2 (C) | In a galloping turn the front skirt leaves the shoulder plates and the chest plate by up to 203 mm and rides up with the forearm | Weights cannot cure it: a skirt tied to the chest lets the foreleg through. Two candidates with a changed shape are in work for the maintainer to choose |
| 4 (E) | The same between the front skirt and the chest plate (up to 160 mm); in a trotting turn the neck comes through the chest plate's collar | Open |
| 5 (black set) | Haunch and cape plates off the cloth; fringe bars 97 to 150 mm off the hem; chest bosses up to 93 mm off the belt | A candidate cures the plates and was found ready; the fringe and the bosses are in work; not installed: the set is in the game |

Armour 4's front backing, which opens into slats at the leg pit, has no cure yet after three attempts: a cut of the
field's lower front edge removes the folds, but the withers straps and the shoulder band are sewn onto the part the
cut removes.

Not marked by him on set v26 or v27 and still open on the set, for builders after the reset:

- Armour 5: the dark place at the withers in the hero view (the place of his pictures 64 and 65). Its cause is
  measured: at the rear end of the shoulder zone the curtain's two extra plate rows reach the height of the body's
  top, and the zone's last plates stand straight up from the sloping top edge of the curtain's coarse backing, so
  the back cover is lifted up to 130 mm over the body to pass them. Four repairs (`v18as`, `v18at`, `v18au` and `v18aw`, the last built 23:54 to 23:59 on 2026-11-25) each failed
  a gate; `v18ar` was a test build. `v18aw` shortened the zone's upper curtain row by one plate: G9 0 and G11 0 pairs
  in five poses, but G13 found 4 pairs at rest (the cover's backing through the last plate of that row at
  (0.134, -0.424, 1.573) and its mirror), and the dark place stays, a little smaller. The zone's last two upper-row
  plates and the cover's lift over them hold each other up, so a repair has to treat three things together (those
  plates, the cover's backing under its first plates, the cover's lift), with G13 at rest, G11 in five poses and G9. The cart has 6 to 13 mm of room at this armour in
  the neck poses, so a plate that moves at the neck base or by the forks meets it. The `v18aw` script is kept in
  `iterations\v18aw\scripts`. The live scripts are `v18ay`'s (`iterations\v18ay\scripts`);
  `iterations\v18av\scripts` is the way back. Also open: a skin triangle beside the chest cloth, seen from the front; the neck
  strap at LOD1 to LOD3, where it still crosses the band's rim (25 and 25, 19 and 16, 32 and 30 face pairs a
  side in `v18ay`); the liner against the neck bands (26 face pairs at rest, 155 with the head up, not yet looked
  at; the probe `clothcollar.py` (chest cloth, liner and neck straps against the neck bands, by pose) found equal
  numbers in `v18av` and `v18ay`: at rest 0, 26 and 8 pairs; neck turn +25 cloth 14 and 10, liner 33 and 31, straps
  10 and 14; neck turn -25 0 and 0, 48 and 46, 14 and 14; the two cloth faces by the collar end that cross in the +25
  turn cross in `v18av` too); 22, 3, 29 and 7 faces of class a at LOD0 to LOD3 in set v27 (set v26: 10, 5, 32, 7;
  `v18ae`: 0, 2, 0, 0). On the armour's swap file the counts are 21, 3, 29, 7 (`v18av`: 10, 5, 32, 7): the 11 new
  ones at LOD0 lie under the collar at x 0.155 to 0.167, z 1.34 to 1.45 and their mirror places, where the collar
  now lies close over the cloth, and the culled part colour views from inside the throat show no new hole. All 21
  listed at LOD0 on the swap file are chest cloth faces (18 by the nearest part vertex, 3 nearest a curtain vertex).
  They are the cloth faces that the cull keeps since decision D2, so that the cloth stays one piece (the work log,
  21:35 on 2026-11-25). Whether a camera sees their backs is not checked; 3 and 5 strap-to-plate
  pairs in the two neck turns; the sheets. The belt loop of the third lane was taken out again.
- Cart F: the far levels (LOD2 wall foot, LOD3 underside, a LOD3 of 26,003 in set v27; the UV check holds 100, 88 and 98 triangles at LOD1 to LOD3), the two hidden hub
  discs (`hub_n` and `hub_p`, the flat base discs of the gold blade sockets) that fail the link gate, a cluster rule in the see gate.
- Armour 1 `v30` failed its check against `v29` (five parts worse), so `v29` stays its last good build. Armour 3
  `v08` is complete (9,460 triangles); `v09` with the panel hangers is half done.

The tables below hold the state of 18:41 on 2026-11-25, except the rows for armour 5 and cart F, which hold the state
of 2026-11-26, 09:40 (set v27). Order after F, by his word:
B, D, C, E.

| Armour | He last saw | In work | State | Open |
|---|---|---|---|---|
| 1 raider | v28: "Rest of armor looks good", with three marks ("clipping", "2 should be straight not bent", "1 should have a buckle here tying it all together") | v30, paused at 13:10; since 18:36 fresh builders, block by block (blocks A head, A body, B far levels, C close range; group D not touched) | v29 was built and not shown: with the head up, 988 skin-view samples on the bridle and the crest holder through the first lame. v30's last build has the poll fin on the first neck lame, one plain crown strap and new eye openings at LOD0 and LOD1, 9,732 triangles at LOD0 (the agent's count); `iterations\v30` is mixed and marked "in work, not for review" | Review groups A (body), B and C of v29; 97 skin-view samples "inside" at the tops of both eye openings |
| 2 sun | v07: "Gaps and still not connected (front armor)." / "Texture issues? Or something" / "Clipping and front armor not attached?" / "Weird texturing on those" | v08, waits | First build only (09:49): 9,928 triangles at LOD0, 546 backing faces in front of plates | The connection standard, ties for the front skirt, the fringe sheet out, clean gold, eye openings and bridle |
| 3 lacquer | v06: "Very very cool but the front armor is still not connected to anything." / "The eye armor needs to be cleaned up" / "Attach these reins or horse head armor straps better." | v07, waits | Not built: `rharm_design3.py` does not build yet | Chest suspension straps, the rump panel's joint, a clean eye opening, the poll strap ends |
| 4 horned | v22, in set e; which version his pictures 44 and 45 show is not recorded | v23, not started | v22: 8,968 triangles at LOD0; link gate 426 islands, 0 unlinked | Strap ends on studs, two studs that cut each other, a buckle on a scale row, the neck piece to armour 5's standard |
| 5 black | 2026-11-26: set v26 (`v18av`) in Blender, picture 68: "the only problem is this is messed up and not straight and then the armor and cart are good after this fix."; set v27 (`v18ay`) opened for him at 08:39, then "put set 27 into the asset sources. This is amazing"; after his Kit import: "done, looks good!" | none; its maps and the set F file are in the Armory's `AssetSources` since 09:22 (the armour is inside the set F FBX), imported in the Kit at 09:36 to 09:38 | `v18ay`, built 08:24 to 08:27: 11,130 / 9,229 / 4,763 / 2,604 / 1,103 / 564 triangles at LOD0 to LOD5 on one horse; collar ends clean; its gates equal `v18av` except G11 gaps (head down 22.2 mm, head up 5.9 mm), G14 faces seen from behind at LOD2 (10), G10 class a (21, 3, 29, 7 on the swap file at LOD0 to LOD3) and the neck strap pairs a side at LOD1 to LOD3 (25 and 25, 19 and 16, 32 and 30) | The dark place at the withers (four repairs failed), the neck strap at LOD1 to LOD3, the liner against the bands, the class a faces. The 18:41 row for `v18y` is replaced by this one |

| Cart | Newest | Staged | State | Open (review counts: the cart review `wf_5c1aaa1f-cb7`, stopped at about 13:07; its checker had finished only cart D) |
|---|---|---|---|---|
| B | v23 (v21 without the javelins, 2026-11-26) | v23, in set b (set v24, with armour 1 v23); in the game since 2026-11-26 as `rhun_war_chariot_b` | Painter wood; link gate 444 islands, 0 unlinked; set 43,490 triangles at LOD0 | 30 review findings of 2026-11-25 (11 "would mark: yes"), not checked; the gold bracket legs under the top rail show plain where the javelins hid them |
| C | v16 (LOD UV fix) | v16, in set c (set v24, with armour 2 v18); its sources are in the Armory's `AssetSources` since 2026-11-27 as `rhun_war_chariot_c` | Painter wood, delivered gloss; set 59,460 triangles at LOD0; the stage's manifest reports every gate passed, with the armour UV row as a count | 24 review findings of 2026-11-25 (5 yes), not checked |
| D | v17 (LOD UV fix, inner fan gone: 1,183 triangles less at LOD0) | v17, in set d (set v21, with armour 3 v16); its sources are in the Armory's `AssetSources` since 2026-11-26 as `rhun_war_chariot_d` | Painter wood; set 58,353 triangles at LOD0, every set gate passed | 23 review findings of 2026-11-25 (9 yes); the checker found 21 real and added 6 |
| E | v13 (wood grain along the boards on the inner front wall and the top planks) | v13, in set e (set v16, with armour 4 v26b); its sources are in the Armory's `AssetSources` since 2026-11-27 as `rhun_war_chariot_e` | Source wood; set 58,991 triangles at LOD0; the stage's manifest reports every gate passed | 26 review findings of 2026-11-25 (6 yes), not checked |
| F | 2026-11-26: v22 (hidden faces cut) with the maps of v24 (black-stained wood), in set v27 | not in `out\for_kit_variants` (its `f` folder is the old v02 stage); the sources are in the Armory's `AssetSources` since 09:22, the Kit packages since 09:38 | Built and swapped (`SWAP DONE`, pose check 0 and 0 in five poses); cart 44,883 triangles at LOD0, armour 22,260 on both horses; harness item `taom_chariot_armour_rhun_black` since 09:56 | Gate table HELD: the 2 hidden hub discs (link gate), the cart's UV check at LOD1 to LOD3 (surface 2.12, 2.10 and 2.16 cm2 against LOD0's 0.18), the count flag (67,143, above 65,400). The check in game is owed. The 18:41 row (v17, step 3 of Part 2 stopped at 18:22) is replaced by this one |

**Armour 5: the look target and his choices.**

| Item | His word | What follows |
|---|---|---|
| Look target | About 14:10, on the upper left tile of the Tripo sheet: "LOOKS AWESOME!" | The tile is the reference picture `out\concept_art\tripo_refs\01_black_neck_front.png`, not the Tripo model (the two models were first measured on 2026-11-26: [technique-library.md](../reference/technique-library.md), teardown T1). It is the look target for neck and head: curved bands with rounded ends, a round gold bead along each free edge, an even overlap, a flared lowest band, a smooth black head shell with the same bead, round gold eye rings, no plume |
| Neck bands | Log entry 15:01 (the hand-over says 15:05): "Rounded ends, rim in normal map" | Prototypes at LOD0 (the rework agent's counts): G, the bead as geometry (`v17pg`), neck piece 1,839 triangles, LOD0 12,320; N, the bead in the normal map (`v17pn`), neck piece 1,003, LOD0 10,759. v18 has G's rounded ends with N's bead |
| Surface | About 14:40: "Armour and cart trim" | Glossy black and bright polished gold on armour 5, the same gold on cart F's trim. The session's pin: gold `_d` median (170, 142, 88), metal 255, gloss 215; black about (16, 16, 17), gloss 200 on bands and head shell, 185 on scales; cloth and leather matt. His eye decides on the render |
| Plume, cloth | 13:36 to 13:44: "We dont need the plume when the armor is very appealing anyways." | Ten narrow bands, no plume, a thin black cloth layer on the skin at the chest and shoulder, under the forks |
| Straps at the helm | Log entry 17:03 (the hand-over says 17:10), picture 67: "THis needs to go under the helm" | Every strap that meets the head shell comes out from under its rim, no strap or stitching on its outer face; it replaces the ring-or-tab standard at the head piece |
| The session's rules on v18t | none | A strap end is sewn onto leather and takes its weights (G9 against G13: neither yields); no hidden-face cull where it splits the chest cloth; LOD0 normals onto the lower LODs; end caps with no fan of sliver triangles |
| Builders | none | 17:11: the rework agent found its working memory too long and wrote the hand-over (`HANDOVER_agent2.md`, "v18: what the next builder does"). Now one fresh builder per block, a read-only checker after each that opens every view, one retry. Run 1 (`armour5-v18-blocks-1-3`, `wf_2e723947-d51`): the straps under the helm, the chest cloth, the withers and the seam of picture 65; blocks 4 to 7 after the session's own read of the views |

**Cart F: the review, the yoke and Part 2.**

| Step | Result |
|---|---|
| Review of the prepared scripts before any Blender run (`wf_ce375cfc-246`, 8 agents; `out\rhun2\_work\f_review_check\CONFIRMED.md`) | 29 findings confirmed (4 blockers, 9 majors, 16 minors), 6 refuted, 4 "verified ok" entries untrue. Three blockers would have stopped Part 2 with nothing to show (the swap's cart files, a density file an AO-fix folder lacks, a mask proof that wanted a share of exactly 1.0); the fourth moved the inner fork prongs onto the pole (no part filter). The re-check: 23 of 24 fixed; 2 new majors and 7 minors |
| The game's clips | Cart F v14 splits at the yoke centre (the checker: walk 24.2 mm, trot 20.4, gallop 0.3, turns 206.5 to 365.2) from its own weight transfer. Correction: cart A's yoke centre is on the pole bone (0.98 to 1.00), so cart A does not split there; the session had told the maintainer otherwise, and the 14:52 entry corrects it to him. No bone moves with both horses; at the forks the necks move against the pole by 67, 126, 185 mm (walk, trot, gallop) |
| Design "one stiff yoke" (`wf_61dc2737-1d9`, 9 agents, a checker per design; `out\rhun2\_work\yoke_design\RESULT.md`) | Rejected: the pole stretches up to 10 cm in the gallop; the forks float 6 to 11 cm off a neck in turns |
| Design P70, weights only | Usable: the inner tip slides 13, 26, 49 mm against the bar's dip (walk, trot, gallop), into it up to 13 mm |
| Design S*, chosen by the session | The yoke's middle and both inner prongs one stiff piece on the pole bone, each inner prong 2.7 cm shorter at its top, a blend to the necks from 0.145 to 0.28 m off the centre; no tear by construction; 0 into a body in walk, trot, gallop; the bar's edge change 46 mm in the gallop. Why: it holds every joint in the clips a player sees most, and its bar flexes less than cart A's. The session took the change of shape itself; it goes to him with the way back (W*, weights only) |
| S* as built | `INNER_PRONG_OFFSET` 0.020 (each inner prong 20 mm further from its neck): 5.7 to 12.9 mm from the bodies, 56.5 mm or more from armour 5's neck bands and collar in every gated clip (the cart hand-over: 50 mm or more), tears 0.8 mm at most |
| Known limits, for him with the set | The bar's arm root 21.2 mm into the left horse in `chariot_gallop_turn_right` (accepted, bound 25 mm; cart A's bar reads 18 to 25 mm in the gallop turns); the turn-in-place clips (the design check: inner prong 105 mm into a neck, cart A's bar 113 mm; the cart gate with the offset: 79 mm in `chariot_turn_right`); quick stop and rear push the arm root 106 and 85 mm into a neck; the bar against the collar in the straight gaits (14.4 mm at rest; walk 0.8, trot -2.2, gallop -1.0 mm) |
| Part 2 (released 17:32, `out\rhun2\_work\f_review\run_f_review.sh`, owner `cart`) | Step 1's six jobs ended with exit 0 (build 8 s, bake 541 s, lods 8 s, export 45 s, gates 3 s, preflight 14 s). Cart F v16: 46,552 triangles at LOD0 (v14: 44,406; the strict hidden-face cut of 1,127 not applied); the yoke gate passed against the v18t dump; gold `_d` median (170, 142, 88), gloss 215. Step 2 made v17 (the AO fix) |
| The see gate, first real run (step 3, 23 jobs, stopped at 18:22) | Three faults were in the gate (inside rays that started outside the crew space, the yoke's middle left in as an open piece, rays from inside an open piece passing); fixed, and the control on v14 still finds F1 to F4 from outside. v17 at LOD0 with the fixed gate: front wall from inside 4,836 back (grazing rays 2 to 20 mm above the boards), from outside 61 back; side walls from inside 130 and 136; floor 41. LOD2 and LOD3 floors have holes (1,281 and 1,536 rays miss); LOD2 to LOD4 show faces from behind (up to 8 a view at LOD2, 67 at LOD4) |
| The session's decisions (18:41, the maintainer away) | The lowest inside ray starts 20 mm above the boards and a skirting strip closes the foot of the inner front and side walls; the corner, rim and floor-corner fails are repaired, each with a render from a failing ray's origin before and after; the floor stays closed at every LOD that shows it (0 misses at LOD1 to LOD3) and the back faces at LOD2 to LOD4 are repaired (0 at LOD1 to LOD3) |
| The projection, told to him | Cart 46,552 with armour 5 v18t (22,186 on both horses) gives 68,738; after the hidden-face cut about 67,600 (an estimate), against the about 66,600 he accepted. Without a word from him the session goes on with about 67,600 |

Staged sets in `out\for_kit_variants\<set>\`, each with a `MANIFEST.md` (hashes, triangles per LOD, slots, gates):

| Set | Cart and armour (built as) | LOD0 | Manifest status |
|---|---|---|---|
| b | cart B v19, armour 1 v23 (v20) | 47,045 | all gates pass |
| c | cart C v14, armour 2 v07 (v15) | 59,270 | all gates pass |
| d | cart D v15, armour 3 v06 (v16) | 57,656 | all gates pass |
| e | cart E v10, armour 4 v22 (v11) | 57,973 | all gates pass |
| f | cart A's cart unchanged, armour 5 v15 (v02) | 64,625 | HELD: 3 gates fail, the 60,000 budget among them |

Set f in this folder is the old stage. Set F v27 (cart F v22 with the maps of v24, armour 5 `v18ay`, 67,143 at LOD0) is
not staged here; its gate table is `variants\f\_work\v27\gate_table\MANIFEST.md` (HELD on four rows), and its ten files
were copied from that folder into the Armory's `AssetSources`.

None is final: each folder holds the state of the look review, not a state for the Kit, and waits for a newer cart
(B v21, C v16, D v17, E v12) and armour; set f is superseded by set F v27 (see above) and still counts the budget his word of 13:02 closed. Owed for sets b to e:
his look, the Kit import, a check in game; for set F v27: the check in game. On cart D in a 20 degree neck turn the yoke comes within 7.9 mm of armour 3.

**Limits every build keeps** (beside the art checks of [blender-asset-gates.md](blender-asset-gates.md)).

- A cart is open at the top and the rear, no wall or rail above cart A's v03 (front 2.218 m, side 1.552 m).
- An armour has 9,000 triangles at LOD0 on one horse; for joints 9,500 (armours 2, 3, 4) and 9,800 (armour 1);
  armour 5 has the measured count of its v18 build, stop line 11,300. A set as in the rules above.
- An armour keeps 0.060 m from the pole (armour 5: 0.050 m in its filled hip zone) and 0 face pairs with the cart at
  rest and in five neck poses. The forks follow the neck bone at weight 0.89 and move up to 100 mm in a neck turn.
  No saddle, no stirrups, nothing below z 0.55 m. A clearance is met with tight, thin plates, never with a hole.
- Connections: no strap end on a stud or a plate (a sewn lap on leather; a ring or buckle with a riveted tab on
  metal); a strap that meets a helm or a rigid shell goes under it; every hanging panel on at least two suspension
  straps; a top binding band on every panel; tight rows. One palette a set.

**Gates and reviews.**

- G1 LOD audit (`tools/audit_fbx_lods.py`), G2 preflight (`tools/blender/mesh_preflight.py`), G4 FBX re-import, G7
  three 2048 RGB maps, G5 0 armour vertices under the skin, G6 0 face pairs at rest and the pole gap.
- G3 names (`rharm_names.py`, `rharmb_names.py`, `rhun2_name_check.py`): 0 hits of the upstream chariot pack's name.
- G9 link gate (`rharm_links.py`, `rharmb_linkgate.py`, carts `rhun2_gate_links.py`): islands within 4 mm are linked,
  each reaches an anchor, each strap is linked at both ends. It cannot tell a made joint from a touch.
- G10 (`rhun2_invclass.py --classify-armour`): 0 armour faces seen only from behind, LOD0 to LOD5. G11
  (`rhun2_posecheck.py`): 0 cart-to-armour and cart-to-body pairs in five neck poses. G12 skin view (`rharm_skinview.py`,
  `rharmb_skinview.py`): 64 directions, six cases. G13 strap to plate (`rharm_strapplate.py`, `rharmb_strapclip.py`):
  face pairs, target 0. G14 (`rharmb_lodcull.py`, culling on): 0 back faces, 0 plate through plate at LOD1 to LOD4.
- Carts: coplanar twins (`rhun2_twins.py`), the collapsed UV area per LOD, the see gate (`rhun2_see_gate.py`: rays
  every 3 mm through the walls and the floor at LOD0 to LOD3, the known slits F1 to F4 as control) and the yoke gate
  (`rhun2_yoke_gate.py`, the game's clips: tears at most 1 mm, no part into a body, each prong 10 mm or more from the
  armour's neck bands and collar in the straight gaits).

The gates passed every version he rejected, so an independent review runs before he sees a version. First run, armour
1 v29 (`wf_8d001b87-5ca`): four reviewers with one scope each, a checker each that tries to refute; 381 views, 60
confirmed (26 at normal distance, 34 close up only), 3 refuted, 20 added. Groups A, B (LODs), C required; D to him.

**Textures.**

- Carts: wood from the modules' wood source textures, projected along each piece's grain and tone-mapped to the dark
  Rhûn wood; steel blades at sRGB 91, 91, 93 (`rhun2_compose.py`). Armours 1 to 3: the paint step `rharm_paint.py`.
- Substance Painter 12.2.0 is one instance, shared through the work claim `painter-instance`. It textures the first
  UV layer: never install `out\painter_pilot\maps`, `maps_ao` or `gold\maps_final*` (made on `["dco", "UVMap"]`);
  clean: `cart_a_redo\maps`, `gold_redo\maps_final`. B, C, E have Painter sets with `maps_delivered_gloss` variants;
  2.2 to 3.2 percent of their wood texels are dark grey single texels, cause not confirmed.
- Cart F: `rhun2_f_painter.sh` runs the pilot's Painter scripts into `rhun_f_painter/<run>` beside the work folder
  (Painter keeps absolute paths, and the work folder's own path breaks the name rule), then `rhun2_f_pcompose.py` per
  wood colour. The gold pin (`TONES["f"]["gold_pin"]`, `rhun2_compose.py`) sets the gold `_d` median and gloss.
- `standart.rsh` multiplies the ambient occlusion by the `_s` blue under `use_specular`; carts write the occlusion
  bake (`rhun2_spec_ao.py`); cart A and the two dwarven chariots keep the flat blue. Not checked in game.

**Decided.**

- 07:54 and 08:44: the wood per cart. 08:03: armour 5 goes on cart F (a black armour needs a black cart). 09:16: cart
  D's inner fan goes. 09:58: cart F's javelins and arrows go ("They are not needed"). 13:10: cart B's javelins go.
- Armour 1: "make fin bigger, but v24b raider is good" (08:36); "These should end with a fine point." (09:48).
- 13:36 to 13:44: ten narrow bands, no plume, the cloth layer, Painter wood for cart F; cart A gets cart F's repairs
  after the black set; the order after F; the two black neck pictures through Tripo.
- About 14:10: picture 01 is the look target. About 14:40: glossy black and bright gold on armour 5 and cart F's trim.
  15:01: the hybrid bands. 17:03: straps under the helm. Evening: more Blender within the memory floor; docs as you go.
- Tripo (13:40 to 13:46): `01_black_neck_front`, `02_black_neck_rear`, image to model, `tripo-v3.1`, 50 credits each
  (940 before, 840 after), in `out\tripo_in\<name>\tripo-out\`; the builders use them as a ratio reference only.
- The session's own: scheme S* (16:48) and the arm root limit (17:32); the v18t rules for armour 5 (17:08); fresh
  builders per block (17:11); set B beside the black set (18:35); cart F's see gate repairs (18:41); armour 1's poll fin on the first neck lame (12:24); armour 2's fringe sheet out (08:36).

**Open decisions (the maintainer's).**

- The black set's count: 67,143 at LOD0 (measured, set v27; the manifest flags it above 65,400), against the about
  66,600 he accepted. He chose the import before the cart's far levels and hub discs are repaired; no source records
  him accepting this number.
- The yoke: S*'s change of shape (way back W*) and its known limits, when he sees the set.
- The other carts' wood colours, the name `rhun_war_chariot_f` (the session's choice; the files carry it in the
  Armory's `AssetSources` since 2026-11-26, and no source records him approving the name as such) and the pairing.
  Cart F's wood is decided: Wood B, black-stained.
- Cart A in the Armory (repairs after the black set): the flat blue, gold blades, a rear step 5.5 mm behind the frame,
  one face the wrong way at LOD1, maybe cart F's wall slits and missing slab face (not checked); one Kit update can fix all.
- Group D of the armour 1 review: strap ends under the junction discs, the fringe as flat tabs, the quilt's soft
  colour, the matt trim gold, ten-sided discs, the fin tone, the girth buckles; and six broad neck bands against nine
  to ten narrow ones in the reference pictures (the session keeps six).
- Armour 4: the bare hip where the pole passes. Tripo: the other four pictures (`03` to `06` in
  `out\concept_art\tripo_refs\`) wait for their sets.
- A commit of the uncommitted doc edits and a push of the docs branch.
- The black set (F, v27): in the Armory's `AssetSources` since 2026-11-26, 09:22 (his word), and imported by him in
  the Kit at 09:36 to 09:38 ("done, looks good!"); its harness item `taom_chariot_armour_rhun_black` exists since
  09:56 (his word). Owed: the check in game, the twelve translations of its name, the troop of Khamûl's line that
  will wear it (his plan), and a commit that takes its repo entries to trunk.
- Sets B to E, each on his word: a set into the Armory's `AssetSources`, the harness item rows and names, the checks
  in game.
- From before: the provenance register line, the `is_compressed` fault, stale comments in the live Armory.

## How to verify in game

Owed for the dwarven war chariot (no issue: the maintainer waived the public issue for the chariot work). Close the
game and the Kit, build this branch into the game, then:

1. In a campaign with cheat mode on, run `campaign.add_item_to_player_party taom_ram_chariot_a`, then the same for
   `taom_chariot_armour_dwarf` and `taom_chariot_armour_dwarf_b`. Equip the ram team and one harness and enter a
   battle. Expect two armoured rams and the cart: iron for the first harness, steel plate courses for the second.
2. Give a party `ironpass_ram_vanguard` troops and open the upgrade screen. Expect two upgrades, Ram Marshal and War
   Chariot, each for 5 Gems, and from the War Chariot one upgrade to Steel Chariot for 7 Gems.
3. Field both chariot troops in a battle. Expect a dwarf standing on the cart, not inside a ram or below the floor.
   If he stands wrong, take `taom_ram_chariot_a` out of `DWARF_MOUNT_IDS` and report it.
4. Watch one chariot in a gallop and in a tight turn. Expect the collar on the armour and the reins attached at
   both ends. A rein that stretches or cuts the armour is a known risk: the long rein is one part of the harness and
   can be built without it.
5. Put the same troops on two sides of a battle. Expect the cloth on the rams' backs, and the two rear pennants of
   the steel cart, in each side's colour, and the cart's iron and wood unchanged.
6. Move the camera far from a chariot. Expect no hole in the plates and no sudden change of the cart's shape.
7. Kill a chariot. Expect the rams and the cart to go down together, and no hang.

Owed for the rig (2026-11-23), with sound on:

8. In a custom battle, leave a Rhûn chariot and stand beside it. Mount it from the left and from the right, at a
   walk and at a run, and dismount on both sides. Expect the rider to climb in and out, and no freeze.
9. On a chariot, press the jump key at a stand, and again at a gallop in a turn. Expect a dash or nothing, never a
   freeze or a rider in a T-pose.
10. Drive over grass, stone, sand, wood and snow. Expect wheel sounds that change with the ground, and horse voices.
11. Kill a chariot that has a rider, and kill a rider on a chariot. Expect the chariot to fall with a clip, the rider
    to stand or die beside it, and the battle to go on.
12. Start the dedicated server with this Armory. Expect no new message about a voice definition or a chariot clip.

Owed for the raider set (2026-11-26): in a campaign with cheat mode on, add `taom_chariot_a` and
`taom_chariot_armour_rhun_raider`, equip both and enter a battle. Expect cart B without javelins and armour 1 on
both horses, nothing stretched, and no missing mesh line in the log.

## Changelog

- 2026-11-27: The far levels of the sun and lacquer armours show far less lining and backing at rest (set C v24 and set D v21 installed at 07:45, LOD2 and LOD3 only). The slip measure is gate G15 of the armour build chains. A study of its largest classes found faults on every armour; candidates for set B's neck lames and the black set's plates wait for the maintainer's word. See "The far levels, a gate for motion, and what the gate found".
- 2026-11-27: The armours came apart in the game's clips (one bone per plate over a backing that follows the skin). Weight repairs are installed for the sun set (set C v23, armour 2 v16) and the horned set (set E v16, armour 4 v26b), and for the lacquer set (set D v20, armour 3 v14); the black set has a candidate that waits for the maintainer's word. See "The armours in motion".
- 2026-11-27: The horned set (set E: cart E v13 with the wood grain along the boards, armour 4 v25e with made joints at every strap end, set v15, 58,991 triangles at LOD0) is in the Armory's `AssetSources` (ten files, 00:52). The Kit import and the harness item are owed.
- 2026-11-27: The sun set (set C: cart C v16, armour 2 v15 with the chest panel on two straps, a girth and no ear pieces, set v22, 59,460 triangles at LOD0) is in the Armory's `AssetSources` (ten files, 00:07; the mesh replaced at 01:03). The Kit import and the harness item are owed.
- 2026-11-26: Cart A (`rhun_war_chariot`) lost its quiver, javelin case, javelins and arrows (build v06: 57,684 triangles at LOD0, was 59,173). Five source files in the Armory's `AssetSources` were replaced at 23:03; the update in the Kit is owed.
- 2026-11-26: The lacquer set (set D: cart D v17, armour 3 v13 with the chest panel on two hangers, set v19, 58,353 triangles at LOD0) is in the Armory's `AssetSources` (ten files, 20:48). The Kit import and the harness item are owed.
- 2026-11-26: The raider set (set B: cart B v23 without javelins, armour 1 v23, set v24, 43,490 triangles at LOD0) is in the Armory's `AssetSources` (ten files, 19:31), imported by the maintainer in the Modding Kit (19:48 to 19:50), and has its harness item `taom_chariot_armour_rhun_raider` (19:56) with its rows in the three item tables. No issue: waived for the chariot work.
- 2026-11-26: Recorded the black set (cart F v22 with the maps of v24, armour 5 `v18ay`, set v27, 67,143 triangles at LOD0) and its sources in the Armory's `AssetSources` (ten files, copied 09:22 on the maintainer's word) and his import in the Modding Kit (13 packages, 09:36 to 09:38; "done, looks good!"). Its harness item `taom_chariot_armour_rhun_black` is in the Armory's `LOTRAOM_horses.xml` (09:56, on his word, reviewed the same day). Owed: the check in game. No issue: waived by the maintainer.
- 2026-11-26: New [technique-library.md](../reference/technique-library.md) with teardown T1 (the two image to 3D models of the neck and head against armour 5 `v16` and `v18ay`), and section 7b of the `/blender-asset` skill, on the maintainer's order to make what a reference tool did our default.
- 2026-11-26: The technique table and the teardown log moved to the knowledge store ([knowledge-library.md](knowledge-library.md)); `technique-library.md` is a pointer with the old row ids, and the steps are the `/teardown` skill in place of section 7b.
- 2026-11-24: Added the section on the Rhûn cart variants and the new horse armours (in work, not in the game), and the art checks that came from that work to [blender-asset-gates.md](blender-asset-gates.md). No issue: waived by the maintainer.
- 2026-11-24: Recorded the rig that replaced the first one on 2026-11-23 (one package from the upstream chariot pack, TAOM's own Rhûn cart as the harness mesh), refreshed five snapshot files, made the two asset checks read each clip's usage list, added the gate `chariot-body` and rebuilt the server skeleton pack. Review: [rca-chariot-rig-2026-11-24.md](../reviews/rca-chariot-rig-2026-11-24.md). No issue: waived by the maintainer.
- 2026-11-24: Added the dwarven war chariot for Erebor: a ram team (`taom_ram_chariot_a`), two harness meshes (chariot a in iron, chariot b in the ram barding style) and two Ironpass troops at levels 41 and 46. In-game checks are owed. No issue: waived by the maintainer.
- 2026-06-13 — Tagged the 2 untagged gait clips (`chariot_gait_walkfast` + `chariot_gait_walkbackfast`) with `quad_movement` to fix a latent mount-crash; in-game mission test still pending.
- 2026-06-12 — Ported the upstream pack's war chariot as the Rhûn Wainrider ridden mount (issue #279): one Monster / 60-bone skeleton, zero TAOM C#, swift/warlord chariot tiers swapped onto `taom_chariot_a`.
