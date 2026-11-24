# War Chariot (Rhûn Wainriders, Erebor rams)

## Overview

The upstream pack's standard war chariot ported into TAOM as a **ridden mount** for Rhûn's Wainrider troop
tree. One Monster (`chariot`), one 60-bone skeleton carrying both horses + cart + wheels; the rider
stands in the cart. Vanilla cavalry spawn via a Horse-slot item — no spawn patch, no behavior tree,
no mission behavior, **no C# at all** (a 100% data port). Issue #279.

Erebor has a second chariot on the same rig, pulled by two war rams, with two Ironpass troops: see
[Dwarven war chariot](#dwarven-war-chariot-erebor-2026-11-24).

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
| LOTRLOME_Armory (see table above) | Monster/actions/usage/XSLT/item + compiled assets under `Assets/creature/chariot/` |
| `Main/_Module/ModuleData/troops/troops_erebor.xml` | The two Ironpass chariot troops and the Ram Vanguard's second upgrade |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`, `special_resources/troop_resource_costs.xml` | Party weight (3.0) and Gems rows of the two troops |
| `tools/apply_dwarf_chariot_armory.py` | Holds the three dwarf chariot items and their names for the unversioned Armory: `--check`, `--apply`, `--remove`; gate `dwarf-chariot-armory` in `tools/data_gates.py` |
| `tools/taom_schema.py` (`DWARF_MOUNT_IDS`) | The ram team is a mount a dwarf may ride |
| `E:\LOTRAOMAssets\_auto_workspace\chariot\*.ps1` | TpacTool-based generators (clips, skeleton wiring, inspection) |

## Recruitment

None added — the existing Wain tree upgrade path delivers the chariot (wain_youngblood → … →
wainrider_cavalry T6 → swift_chariot T8 → warlord_chariot T9). No VolunteerRecruitmentService
change, no clan wiring.

Erebor: no pool and no party template lists the two chariot troops. They are reached by upgrade only, from
`ironpass_ram_vanguard`, as the Ram Marshal is; `EreborRamCavalry_IsNotOfferedByAnyVolunteerPool` holds that.

## Tests

No unit tests — there is no C# (the data port is covered by `validate_moduledata.py` +
`validate_all_troop_refs.py`, both PASS, and the in-game checklist). Full suite green at 3,143
after the mount-lock removal.

## Provenance & rights

Art/animations/XML extracted from the **upstream pack** tpacs; rights to
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
| `taom_ram_chariot_a` | Horse | The ram team. Its Horse row copies `taom_chariot_a` (maneuver 25, speed 70, charge 80, riding difficulty 120, `Monster.chariot`) without its `skeleton_scale` (see below). |
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

**No `skeleton_scale` on the ram team.** `taom_chariot_a` carries `skeleton_scale="empire_horse"` from the upstream
pack. The engine applies a scale by bone index of the scale's own skeleton (`horse_skeleton`), not by name
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

## Changelog

- 2026-11-24: Added the dwarven war chariot for Erebor: a ram team (`taom_ram_chariot_a`), two harness meshes (chariot a in iron, chariot b in the ram barding style) and two Ironpass troops at levels 41 and 46. In-game checks are owed. No issue: waived by the maintainer.
- 2026-06-13 — Tagged the 2 untagged gait clips (`chariot_gait_walkfast` + `chariot_gait_walkbackfast`) with `quad_movement` to fix a latent mount-crash; in-game mission test still pending.
- 2026-06-12 — Ported the upstream pack's war chariot as the Rhûn Wainrider ridden mount (issue #279): one Monster / 60-bone skeleton, zero TAOM C#, swift/warlord chariot tiers swapped onto `taom_chariot_a`.
