# War Chariot (Rhûn Wainriders, Erebor rams)

## Overview

The upstream pack's standard war chariot ported into TAOM as a **ridden mount** for Rhûn's Wainrider troop
tree. One Monster (`chariot`), one 60-bone skeleton carrying both horses + cart + wheels; the rider
stands in the cart. Vanilla cavalry spawn via a Horse-slot item — no spawn patch, no behavior tree,
no mission behavior, **no C# at all** (a 100% data port). Issue #279.

Erebor has a second chariot on the same rig, pulled by two war rams, with two Ironpass troops: see
[Dwarven war chariot](#dwarven-war-chariot-erebor-2026-11-24).

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
three Wainrider item rows are only in the Armory, which is not versioned, and no tool puts any of it back after a
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
(`rhun2_armour_swap.add_armour`) puts the armour on both horses. Nothing of it is in the game or in the Armory. The
record is the chariot work folder under `E:\LOTRAOMAssets\_auto_workspace`: its `NIGHT_LOG.md` (newest entries at
the end) and the hand-overs `out\armours\HANDOVER_agent1.md` (armours 1 to 3), `out\armours\HANDOVER_agent2.md`
(armours 4 and 5) and `out\rhun2\HANDOVER_carts.md` (carts). They are newer than this section when the two disagree.

**Working rules of 2026-11-25 (the maintainer).**

- One set at a time (13:10): "Lets work on one cart and one horse armor set, when that is perfect move onto the
  next one". The other sets wait; a builder on another set is paused at a safe point.
- One Blender job at a time (13:10): "We have like 36 blenders open, can we close some?" and "LEts also work on one
  blender item at a time to save memory". At 13:07 there were 49 Blender processes with 37.8 GB of 61.6 GB, 4.2 GB
  free. A review runs one reviewer at a time.
- The triangle count (13:02): "I accept the triangle count for this. BEcause a we have two horses and a cart, that
  is like 3 troops." The 60,000 gate on a set is closed as a blocker. The session's reading, not a limit he named:
  the manifest reports a set's LOD0 count, and a set above about 65,400 goes to him again.
- Wood colour (13:44): "The carts woods can also be different colors of brown, black, red, etc." The colour of a
  cart's wood is a choice per cart. The session's reading: he sees colour variants of a cart's wood before one is fixed.
- What he sees (13:36 to 13:44): a build only after the session's close review finds no fault.

**The six carts.** The pairing of B to E is the session's proposal; he reviewed the sets as paired.

| Cart | Design | Armour | Wood | The maintainer's verdict |
|---|---|---|---|---|
| A `rhun_war_chariot` (in the game) | Scale plates, fan panels, scythe wheels | its Armory armour, none of the new ones | as installed; he named no choice, so it does not change | none recorded in this work |
| B `rhun_war_chariot_b` Reaver | Open lattice frame, red leather X straps, pierced spearhead standard | 1 raider | Painter | "Cart B - Painter wood"; "remove the javelins from cart B too" (waits for set B) |
| C `rhun_war_chariot_c` Sun Car | Fan front wall, lamellar walls, gold disc wheels | 2 sun | Painter with the delivered gloss | "Cart C - Painter wood, delivered gloss" |
| D `rhun_war_chariot_d` Lacquer Court | Red lacquer sun panels in gold frames, gold scale body | 3 lacquer | Painter | "Cart D - Painter wood"; "Once we fix the issues this cart set up is AWESOME"; of the inner fan: "This serves no purpose and wont ever be used, waste of triangles and polys remove it." (gone since D v17) |
| E `rhun_war_chariot_e` Spear-prow | Plank walls, bossed shields, spears along the pole | 4 horned | source wood | "cart e - source wood" |
| F `rhun_war_chariot_f` Black | Cart A's shapes as a variant build with its own slots and maps: black fields, gold trim, wood, steel blades; no javelins, arrows or cases | 5 black | source wood now; Painter wood by his word of 2026-11-25, so a new Painter pass with colour variants is owed (F's new UVs share 0 of 10,984 base triangles with cart A's, so cart A's Painter redo does not fit) | "My expectation is if the horse armor is black, the cart must be the same color. Black, gold trim and wood"; "rhun set f black is good"; "The black cart looks good with the black horse armor (rhun set F)." |

**State on 2026-11-25, 13:34** (the log's newest entry is 13:28). The set in work is the black set, cart F with
armour 5: the cart is approved in look, the count is accepted, and armour 5 v17 is in build with his last marks.
Order: armour 5 v17, the session reads its sheets, a close review, fixes, cart F's 21 review findings, fixes, the
swap, the set shown to him. Order after F, by his word: B, D, C, E.

| Armour | He last saw | In work | State | Open |
|---|---|---|---|---|
| 1 raider | v28: "Rest of armor looks good", with three marks ("clipping", "2 should be straight not bent", "1 should have a buckle here tying it all together") | v30, paused at 13:10 | v29 was built and not shown: with the head up, 988 skin-view samples on the bridle and the crest holder through the first lame. v30's last build has the poll fin on the first neck lame, one plain crown strap and new eye openings at LOD0 and LOD1, 9,732 triangles at LOD0 (the agent's count); `iterations\v30` is mixed and marked "in work, not for review" | Review groups A (body), B and C of v29; 97 skin-view samples "inside" at the tops of both eye openings |
| 2 sun | v07: "Gaps and still not connected (front armor)." / "Texture issues? Or something" / "Clipping and front armor not attached?" / "Weird texturing on those" | v08, waits | First build only (09:49): 9,928 triangles at LOD0, 546 backing faces in front of plates | The connection standard, ties for the front skirt, the fringe sheet out, clean gold, eye openings and bridle |
| 3 lacquer | v06: "Very very cool but the front armor is still not connected to anything." / "The eye armor needs to be cleaned up" / "Attach these reins or horse head armor straps better." | v07, waits | Not built: `rharm_design3.py` does not build yet | Chest suspension straps, the rump panel's joint, a clean eye opening, the poll strap ends |
| 4 horned | v22, in set e; which version his pictures 44 and 45 show is not recorded | v23, not started (folders `v22hb2` and `v22hb3` are on disk; what they hold is not recorded) | v22: 8,968 triangles at LOD0; link gate 426 islands, 0 unlinked | Strap ends on studs, two studs that cut each other, a buckle on a scale row, the neck piece to armour 5's standard |
| 5 black | v16, with set F: "Black neck piece needs to be reworked. it is all off" | v17, the one item in build: trials from `v17t` on (newest on disk at 13:34: `v17aj`) | Trial `v17t` (the hand-over's last numbers, 11:13): neck piece rebuilt as 10 lames, mirror error 0.0 mm, overlap 15 mm; 9,900 triangles at LOD0; gates 1 to 7, 9, 10 and 11 pass | `v17t`: skin-view gate 29 at rest and strap-clip gate 202 face pairs at rest (target 0 each); the seam of picture 65: "this needs to be straight or just remove them and line up the top cloth to the side cloth"; by his word of 13:36 to 13:44 the ten narrow bands stay, the plume goes, and a thin black cloth layer covers the chest and shoulder |

Review counts: the cart review `wf_5c1aaa1f-cb7`, stopped at about 13:07; its checker had finished only cart D.

| Cart | Newest | Staged | State | Open |
|---|---|---|---|---|
| B | v21 (LOD UV fix) | v19, in set b | Painter wood; link gate 482 islands, 0 unlinked | Javelins off; 30 review findings (11 "would mark: yes"), not checked |
| C | v16 (LOD UV fix) | v14, in set c | Painter wood, delivered gloss; link gate 1,291 | 24 findings (5 yes), not checked |
| D | v17 (LOD UV fix, inner fan gone: 1,183 triangles less at LOD0) | v15, in set d | Painter wood; link gate 1,021, 0 unlinked | 23 findings (9 yes); the checker found 21 real and added 6 |
| E | v12 (LOD UV fix) | v10, in set e | Source wood; link gate 845 | 26 findings (6 yes), not checked |
| F | v14 (no javelins, arrows or cases; 1,127 hidden faces cut; 44,406 at LOD0) | not staged | Set v15 with armour 5 v16: 64,154 at LOD0; only the count fails | 21 findings (6 yes), no checker: slits in the front wall, the floor and under the sunburst panel, the yoke halves split in a neck turn, blotches on the pole. The cart agent prepares the fixes with no Blender job |

Staged sets in `out\for_kit_variants\<set>\`, each with a `MANIFEST.md` (hashes, triangles per LOD, slots, gates):

| Set | Cart and armour (built as) | LOD0 | Manifest status |
|---|---|---|---|
| b | cart B v19, armour 1 v23 (v20) | 47,045 | all gates pass |
| c | cart C v14, armour 2 v07 (v15) | 59,270 | all gates pass |
| d | cart D v15, armour 3 v06 (v16) | 57,656 | all gates pass |
| e | cart E v10, armour 4 v22 (v11) | 57,973 | all gates pass |
| f | cart A's cart unchanged, armour 5 v15 (v02) | 64,625 | HELD: 3 gates fail, the 60,000 budget among them |

None is final: every folder predates the corrections of 2026-11-25 and holds the state of the look review, not a
state for the Kit. Each set waits for its newer cart (B v21, C v16, D v17, E v12, F v14) and a newer armour; the
manifest of f still counts the budget that his word of 13:02 closed. Owed for every set: his look, the Kit import,
a check in game. On cart D in a 20 degree neck turn the yoke comes within 7.9 mm of armour 3: watch it in game.

**Limits every build keeps** (beside the art checks of [blender-asset-gates.md](blender-asset-gates.md)).

- A cart is open at the top and the rear, no wall or rail above cart A's v03 (front 2.218 m, side 1.552 m).
- An armour has 9,000 triangles at LOD0 on one horse; for joints 2026-11-25 allows 9,500 (armours 2, 3, 4), 9,800
  (armour 1) and 10,500 (armour 5, after hidden plates go). A set as in the rules above.
- An armour keeps 0.060 m from the pole along its whole length (armour 5: 0.050 m in its filled hip zone) and 0 face
  pairs with the cart at rest and in five neck poses. The forks follow the neck bone at weight 0.89 and move up to
  100 mm in a neck turn. No saddle, no stirrups, nothing below z 0.55 m.
- A clearance is met with tight, thin plates, never with a hole; a conflict with coverage goes to the maintainer.
- Connections: no strap end on a stud (a sewn lap on leather, a ring or buckle with a riveted tab on metal); every
  hanging panel on at least two suspension straps; a top binding band on every panel; tight rows. One palette a set.

**Gates and reviews.**

- G1 LOD audit (`tools/audit_fbx_lods.py`), G2 preflight (`tools/blender/mesh_preflight.py`), G4 FBX re-import, G7
  three 2048 RGB maps, G5 0 armour vertices under the skin, G6 0 face pairs at rest and the pole gap.
- G3 names (`rharm_names.py`, `rharmb_names.py`, `rhun2_name_check.py`): 0 hits of the upstream chariot pack's name.
- G9 link gate (`rharm_links.py`, `rharmb_linkgate.py`, carts `rhun2_gate_links.py`): islands within 4 mm are linked,
  each reaches an anchor, each strap is linked at both ends. It cannot tell a made joint from a touch.
- G10 (`rhun2_invclass.py --classify-armour`): 0 armour faces of class a (seen only from behind) at LOD0 to LOD5.
- G11 (`rhun2_posecheck.py`): 0 cart-to-armour and cart-to-body pairs in five neck poses.
- Skin view (`rharm_skinview.py`, `rharmb_skinview.py`): 64 directions, six cases. It came after the build exported
  a saved pose that the gates cleared (armour 1 v27: 955 of 10,500 neck-lame samples inside the skin, 0 without it).
- Strap to plate (`rharm_strapplate.py`, `rharmb_strapclip.py`): strap or band to plate face pairs, target 0.
- Carts: coplanar twins (`rhun2_twins.py`) and the collapsed UV area per LOD. Ordered, not built: culling on per LOD.

The gates passed every version he rejected, so an independent review runs on a version before he sees it. Its first
run, on armour 1 v29 (workflow `preshow-armour-review`, run `wf_8d001b87-5ca`): four reviewers with one scope each
(straps and joints; plates, crest and neck piece; cloth, textures and LODs; the whole armour against his list of
rejections) and one checker per scope that tries to refute each finding.

| Views read | Confirmed | At normal distance | Close up only | Refuted | Added by the checkers |
|---|---|---|---|---|---|
| 381 | 60 | 26 | 34 | 3 | 20 (M00 to M19) |

The reviewers' own counts add up to 75, so 12 findings are in neither list of `review_result.json`. The session found
all eight findings whose pictures it read real. Not covered: LOD4 and LOD5 in pictures, leg poses, the cart, the
game's shading. Groups A (normal distance or motion), B (LODs) and C (close range) are required; D goes to him.

**Textures.**

- Carts: wood from the modules' wood source textures, projected along each piece's grain and tone-mapped to the
  dark Rhûn wood; steel blades at sRGB 91, 91, 93 (`rhun2_compose.py`). Armours 1 to 3: the paint step `rharm_paint.py`.
- Substance Painter 12.2.0 is one instance, shared with other sessions through the work claim `painter-instance`.
  The pass of 2026-11-24 (`out\painter_pilot\`, `ppilot_*.py`) put `wood_wenge` on cart A in three face classes from
  the grain attribute `gco`, and gold with steel blades on its wheels (716,147 texels changed); about 3 minutes per set.
- The first UV layer fault: the mesh sent to Painter had the layers `["dco", "UVMap"]`, and Painter textures the
  first. Export padding filled 52.65 percent of the visible texels of the first wood set and 86.78 percent of the
  wheel texels of the gold set. Do not install `out\painter_pilot\maps`, `maps_ao` or `gold\maps_final*`, or use
  `out\view\painter_pilot_cart_d_VIEW.blend`. Clean sets: `cart_a_redo\maps`, `gold_redo\maps_final`.
- After the fix: Painter sets for B, C, E (13 s each), with `maps_delivered_gloss` variants. Open: 2.2 to 3.2
  percent of the Painter wood texels are neutral dark grey single texels, cause not confirmed.
- The blue channel of `_s`: `standart.rsh` reads `specular_sample.xy` as metal and gloss and multiplies the ambient
  occlusion by `specular_sample.b` under `use_specular`, which the cart materials carry. The early flat blue took
  about 38 percent of the ambient light off the wood; carts now write the occlusion bake (90 at 0, 235 at 0.6, 255 at
  1; `rhun2_spec_ao.py`). Cart A and the two dwarven chariots still have the flat blue. Not checked in game.

**Decided.**

- 07:54 and 08:44: the wood per cart. 08:03: a black armour needs a black cart; armour 5 goes on cart F.
- Armour 1: "make fin bigger, but v24b raider is good" (08:36); "These should end with a fine point." (09:48).
- 09:16: cart D's inner fan goes. 09:58: cart F's javelins and arrows go: "They are not needed".
- 13:02: the triangle count. 13:10: cart B's javelins go; one set and one Blender job at a time.
- 13:36 to 13:44, for the black set: armour 5 keeps ten narrow neck bands; its plume goes ("We dont need the plume
  when the armor is very appealing anyways."); a thin black cloth layer covers the bare chest and shoulder under
  the forks; cart F gets Painter wood; the yoke's split is measured on the real clips before a weight changes.
- The same answers: cart A gets cart F's repairs after the black set; the order after F is B, D, C, E; the two
  black neck reference pictures go through Tripo first (the account showed 940 credits at 13:36).
- The session's own: armour 1's poll fin on the first neck lame (12:24; way back: v29's `crest_clean` and
  `fin_socket` in `rharm_design1.py`); armour 2's fringe sheet out, a red breast band kept (08:36).

**Open decisions (the maintainer's).**

- The name `rhun_war_chariot_f` (the session's choice), the pairing, and the wood colour of each cart.
- Cart A in the Armory (its repairs follow the black set): the flat blue, gold blades, a rear step 5.5 mm behind the frame, one face the wrong way at
  LOD1, maybe the wall slits and the missing slab face found on cart F (not checked). One Kit update can fix all of it.
- The gold tone: the muted gold measured on cart A, or the brighter gold of the concept pictures.
- Group D of the armour 1 review: strap ends under the junction discs, the fringe as flat tabs, the quilt's soft
  colour, the matt trim gold, ten-sided discs, the fin tone, the girth buckles; and six broad neck bands against
  nine to ten narrow ones in the reference pictures (the session keeps six).
- Armour 4: the bare hip where the pole passes.
- Tripo: the other four reference pictures wait for their sets.
- A push of the docs branch.
- Each on his word: a set into the Armory's `AssetSources`, the harness item rows and names, the checks in game.
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

## Changelog

- 2026-11-24: Added the section on the Rhûn cart variants and the new horse armours (in work, not in the game), and the art checks that came from that work to [blender-asset-gates.md](blender-asset-gates.md). No issue: waived by the maintainer.
- 2026-11-24: Recorded the rig that replaced the first one on 2026-11-23 (one package from the upstream chariot pack, TAOM's own Rhûn cart as the harness mesh), refreshed five snapshot files, made the two asset checks read each clip's usage list, added the gate `chariot-body` and rebuilt the server skeleton pack. Review: [rca-chariot-rig-2026-11-24.md](../reviews/rca-chariot-rig-2026-11-24.md). No issue: waived by the maintainer.
- 2026-11-24: Added the dwarven war chariot for Erebor: a ram team (`taom_ram_chariot_a`), two harness meshes (chariot a in iron, chariot b in the ram barding style) and two Ironpass troops at levels 41 and 46. In-game checks are owed. No issue: waived by the maintainer.
- 2026-06-13 — Tagged the 2 untagged gait clips (`chariot_gait_walkfast` + `chariot_gait_walkbackfast`) with `quad_movement` to fix a latent mount-crash; in-game mission test still pending.
- 2026-06-12 — Ported the upstream pack's war chariot as the Rhûn Wainrider ridden mount (issue #279): one Monster / 60-bone skeleton, zero TAOM C#, swift/warlord chariot tiers swapped onto `taom_chariot_a`.
