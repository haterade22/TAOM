# War Chariot (Rhûn Wainriders)

## Overview

The upstream pack's standard war chariot ported into TAOM as a **ridden mount** for Rhûn's Wainrider troop
tree. One Monster (`chariot`), one 60-bone skeleton carrying both horses + cart + wheels; the rider
stands in the cart. Vanilla cavalry spawn via a Horse-slot item — no spawn patch, no behavior tree,
no mission behavior, **no C# at all** (a 100% data port). Issue #279.

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

**Ranked follow-ups (NOT yet done; full plan in [creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md)):**
1. Parameterize `tools/audit_mount_parity.py` to cover the chariot vs the **vanilla-horse** baseline
   (chariot is absent from its `MOUNTS` list — the RCA's named pending follow-up).
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
- **Per-mesh bone palette**: the upstream pack's single 54-bone horse mesh exceeds the ~40-bone per-draw palette
  (the spider render-AV lesson), so the FBX ships split per horse: `chariot_horse_brown` (A bones)
  + `chariot_horse_brown_2` (B bones), all LODs ≤40 bones. The Kit folds the 29 Blender objects
  into 6 Metameshes.
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
| `Monsters/LOTR/lotr_monster_chariot.xml` (+ SubModule.xml XmlNode) | upstream-pack Monster verbatim: family_type 4, weight 2000/HP 600, rider_sit_bone=root, wheel terrain decals, Mountable, CanRear=false, no ragdoll_bone_* attrs (skeleton-side physics/IK comes from the transplanted SkeletonUserData — see pipeline notes) |
| `action_types.xml` | 23 `act_chariot_*` + 4 `actt_mount` (`act_mount_chariot_*` — the engine derives these names from `monster_usage="chariot"`) |
| `action_sets.xml` | `as_chariot` + `_town_and_village` + `_map`. **10 substitutions** → `chariot_stand_1`: the upstream pack mapped rear/strike/fall to `howdah_*`/`elephant_death_front_continue`/`elephant_rear`, absent from TAOM packs (death-visual polish is a follow-up) |
| `monster_usage_sets.xml` | upstream-pack `chariot` usage set verbatim |
| `action_sets.xslt` | `act_chariot_*` → `chariot_rider_*` rows injected into `as_human_warrior` (rider STANDS; same mechanism as the elephant block) + 4 mount actions → `chariot_mount_rider_from_right` |
| `monster_usage_sets.xslt` | `mount_id="chariot"`: 6 mountings (chariot mount actions + vanilla horse dismounts), 8 falls + 8 strikes (vanilla `act_fall_rider_*`/`act_rider_only_fall_*`) |
| `LOTRLOME_items/LOTRAOM_horses.xml` | `taom_chariot_a`: mesh `chariot_horse_brown`; AdditionalMeshes `chariot_horse_brown_2` + mane + `chariot_harness_e_rein` + `chariot_ride` (NOT `chariot_ride_alt` — dead texture refs in the upstream pack itself); maneuver 25 / speed 55 / charge 200; `is_merchandise=false` |

## Configuration

No JSON/MCM config and no C#. Tuning lives in the item (`taom_chariot_a` Horse params: maneuver 25,
speed 55, charge 200, riding difficulty 120) and the Monster XML.

## Key Files

| File | Purpose |
|---|---|
| `Main/_Module/ModuleData/troops/troops_rhun_new.xml` | Horse-slot swap on the two wainrider chariot tiers (HorseHarness removed — the chariot item carries its own harness mesh) |
| LOTRLOME_Armory (see table above) | Monster/actions/usage/XSLT/item + compiled assets under `Assets/creature/chariot/` |
| `E:\LOTRAOMAssets\_auto_workspace\chariot\*.ps1` | TpacTool-based generators (clips, skeleton wiring, inspection) |

## Recruitment

None added — the existing Wain tree upgrade path delivers the chariot (wain_youngblood → … →
wainrider_cavalry T6 → swift_chariot T8 → warlord_chariot T9). No VolunteerRecruitmentService
change, no clan wiring.

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

## Status / pending

- ✅ ModuleData, compiled assets (skeleton+6 Metameshes w/ upstream-pack IK transplant, 3 masters, 24 clips), validators
- ⏳ **In-game full-path verification** (spider-RCA checklist: inventory tableau, mission mount,
  all gaits, standing rider, mount death, formation, gate/bridge pathing for the 2-horse footprint)
- ⏳ `/deep-review` + `/review-codex` before the closing commit
- Follow-ups: black/white horse variants + `wide_chariot` (drop-in: same split, items exist in the upstream pack),
  proper mount-death clips (currently `chariot_stand_1` freeze), localization pass for
  `{=taom_chariot_a}`, fix `tools/blender/creature_anim_ops.py` stale `primary_bone_axis='X'` (L13)
