# Spider Skeleton + Animation Pipeline (Blender → Modding Kit → Bannerlord)

> **Companion:** the locomotion/rider *refinement* workflow + theory lives at
> [creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md);
> this file is the rig/skeleton truth.

**Status (2026-06-11): mount lane PROVEN in battle** — spider formations with goblin riders load
and fight (see [spider.md](spider.md) for the full architecture + the 2026-06-10/11 RCA). The
critical pipeline addition since 2026-06-03: **movement clips MUST carry the `quad_movement` tag
+ step points in their `_anm.tpac`** (section below) — clips compiled without it cause a native
AccessViolation the first time a `movement_system="quadrupedal"` action set engages them
(thumbnail/inventory tableau/mission mount paths; the detached spawn paths of the 06-03 era never
exercised them, which is why the gap survived testing). **Registration correction:** the live
action set is `as_spider` in the module-root `action_sets.xml` (registered via `project.mbproj`
`soln_action_sets`) — `Animations/action_sets_spider.xml` is a superseded, unregistered copy;
every reference to it below should be read as the root file.

**Status (2026-06-03, historical):** Skeleton + mesh + IK/ragdoll pipeline **PROVEN in-game**. **Full animation clip set retargeted + bound** — all ~24 clips retargeted `sp_skeleton`→`spider_skeleton` via rest-compensated retarget, exported as `an_spi_*` FBXs; forward walk + run use the procedural metachronal-wave builder; `action_sets_spider.xml` repointed to the `an_spi_*` set (all bindings resolve); `spider_correct.fbx` re-exported with all 3 mesh variants (a/b/c + LODs). **Remaining (human seam):** re-import the full-mesh FBX to the Kit (the `spider_skeleton` already carries its IK/ragdoll joints — no re-transplant needed), then in-game test. This doc is the source of truth — proven recipe, gotchas, deliverables.

> **Final action-set bindings (2026-06-03):** main idle `an_spi_idle_2`, alt idle `an_spi_idle`, forward walk `an_spi_walk_2` (no dedicated walk_forward clip), run `an_spi_run` (never `an_spi_run_2`), strafes `an_spi_walk_left/right`. Locomotion is **in-place** (engine drives travel — see [[feedback_movement_anims_in_place_engine_driven]]). The metachronal-wave gait (home→leg1-4 sequential forward step + lift, sides synced) replaced the animator's alternating gait, which collided adjacent legs.

---

## The three spider rigs (do not confuse them)

| Rig | Bones | Names | Mesh | Where | Verdict |
|---|---|---|---|---|---|
| **`spider_skeleton`** | **62** | lowercase (`root_m`, `joint5_r`, `chest_m`) | `sk_spider_forest_c` (a/b/c + 6 LODs each, skinned) | `E:\LOTRAOMAssets\_auto_workspace\haterade_teach_that_btch.blend` | ✅ **THE correct rig** — symmetric (tail_err≈0), proper spider posture. Matches the names `action_sets_spider.xml`/`monsters.xml` already target. |
| `sp_skeleton` | 59 | mixed-case (`Root_M`, `joint5_R`) | `sk_spider_forest_bm_a1` | `E:\LOTRAOMAssets\ErkamSpider (1).blend` + `E:\LOTRAOMAssets\SpiderFBXs\*.fbx` | ❌ **broken** — heads symmetric but tails/orientations asymmetric (`max_tail_err`=2.73). The animator's 34 clips live on THIS rig. |
| `erkamspider_skeleton` | 58 | mixed-case | `erkamspider` | `LOTRLOME_Armory/.../erkamspider_geo.tpac` | same family as `sp_skeleton`; has a full IK setup (58 bodies/57 d6) — used as a reference for the transplant. |

`sp_skeleton` (59) is `spider_skeleton` (62) minus `joint16_m`,`joint21_l`,`joint21_r` by lowercased name — but their **bone orientations differ**, which is the whole reason animations don't transfer 1:1.

---

## THE PROVEN RECIPE (verified in-game)

### 1. Skeleton + mesh export (from the haterade blend)
- Select `spider_skeleton` + `sk_spider_forest_c` (+ its 5 LODs).
- Export FBX: `object_types={'ARMATURE','MESH'}`, **`primary_bone_axis='Y'`**, `secondary_bone_axis='X'`, `axis_forward='-Y'`, `axis_up='Z'`, `add_leaf_bones=False`, `bake_anim=False`. Armature root keeps the **real name** `spider_skeleton`.
- Output: `E:\LOTRAOMAssets\_auto_workspace\spider_correct.fbx` (2.24 MB).
- Import to Modding Kit → set skeleton **Type = `horse`** (imports as `other`).

### 2. IK / ragdoll joints
- `python tools/tpac_skeleton_transplant.py "<...>/spider_correct_geo.tpac" spider_skeleton`
- Produces 62 bodies + **61 D6 joints** (body/spine mass 8.0, legs 0.6). The `classify_bone()` is case-insensitive → handles the lowercase names; **0 unknowns** (4 body_axis, 2 head, 2 fang, 10 pedipalp, 40 leg, 4 abdomen). Auto-writes a `.backup`.
- The compiled tpac is at `<game>/Modules/LOTRLOME_Armory/Assets/creature/spider/animations/spider_correct_geo.tpac` (created by the Kit on import).

### 3. Animation export (per clip)
- **Armature-only**, root named **`spider_skeleton_notused`** (real-name + `_notused` suffix — see gotcha below), **`primary_bone_axis='Y'`**, `bake_anim_use_nla_strips=True` (bare take name).
- Import as **Skeleton Animation** → Owner Skeleton `spider_skeleton` → make an **Animation Clip** (Source 1=0, Source 2 = the clip's last frame, Duration auto > 0, Blend in ≈ 0.1) → bind in the root `action_sets.xml`.
- **MANDATORY for movement/gait clips (walk/run/strafe/turn/idle-as-movement): set the
  `quad_movement` animation flag (+ `make_walk_sound` + step points) in the Kit's clip editor
  BEFORE saving.** See the next section — an untagged movement clip compiles fine, plays fine on
  a detached agent, and then AVs the engine the first time a quadrupedal action set measures it.

### 3b. THE `quad_movement` TAG (root cause of the 2026-06-10 mount AVs)

Byte-diff of a working ADOD_Beasts clip (`elephant_canter_anm.tpac`) vs ours exposed the difference:

| `_anm.tpac` field | Upstream (works) | our 06-03 compiles (AV'd) |
|---|---|---|
| step points | 4 real fractions (0.11/0.25/0.38/0.67) | `-1,-1,-1,-1` (unset) |
| sound tag list | `make_walk_sound` | empty |
| movement tag list | **`quad_movement`** + speed params | empty |

A `movement_system="quadrupedal"` action set measuring untagged movement clips builds a **null
native gait structure** → `AccessViolation` (+0x10) on the first `Skeleton.TickAnimations` /
`GetWalkSpeedLimitOfMountable`, in every mount context. Non-movement clips (attacks/hits/deaths)
correctly do NOT carry the tag (ADOD_Beasts's attacks don't either — they carry `lock_movement` /
`client_prediction`-class flags only).

**Interim fix applied 2026-06-11:** 9 clips byte-patched onto the elephant template
(`an_spi_walk_2/_left/_right`, `an_spi_run`, `an_spi_idle`, `an_spi_idle2`,
`an_spi_turn_left/right`, `an_spi_jump` — tags + step points grafted; each clip's own
GUIDs/name/duration kept; originals at `*.bak-untagged`). **Durable fix:** recompile in the Kit
with the fields set (next section). The byte-patch recipe lives in [spider.md](spider.md) "How-to".

### 3c. WHERE these live in the Kit's clip editor (captured 2026-06-11, editor screenshots)

Open the animation clip in the Modding Kit editor. The properties panel has, top to bottom:
**Loading Type** (dropdown — ADOD_Beasts ships `Never load` on elephant_attack_1; load-on-demand, works
fine), **Flags** (checkbox list), and a collapsed **Clip usages** section at the bottom.

- **`quad_movement` is a CLIP USAGE, not a Flag** — add it in the "Clip usages" section. This is
  the field whose absence caused the mount AVs.
- **`make_walk_sound` IS a Flag** (footstep sounds) — check it on gait clips.
- **Step points** are the footstep-timing fractions (separate field; ADOD_Beasts's canter has 4).
- `_anm.tpac` serialization (verified by byte-diff): string-list 1 = the CHECKED flags,
  string-list 2 = the clip usages (+ per-usage params). Unchecked = empty lists.

**The full Flags list** (for reference; engine semantics mostly self-describing):
`disable_agent_agent_collisions, ignore_all_collisions, ignore_static_body_collisions,
use_last_step_point_as_data, make_bodyfall_sound, client_prediction, keep, restart,
client_owner_prediction, make_walk_sound, disable_hand_ik, stick_item_to_left_hand,
blends_according_to_look_slope, synch_with_horse, use_left_hand_during_attack, lock_camera,
lock_movement, synch_with_movement, enable_left_hand_ik, enable_hand_spring_ik,
enable_hand_blend_ik, synch_with_ladder_movement, do_not_keep_track_of_sound, enforce_lowerbody,
enforce_all, cyclic, enforce_root_rotation, allow_head_movement, disable_foot_ik,
affected_by_movement, update_bounding_volume, align_with_ground, ignore_slope, displace_position,
reset_camera_height, ignore_scale_on_root_position, blend_main_item_bone_entitially,
enforce_weapon_tip_with_rope_stretched, enforce_weapon_tip_with_rope_relaxed,
disable_auto_increment_progress, switch_item_between_hands, attach_sound_to_agent, spawn_particle`

**ADOD_Beasts per-category flag recipes (parity targets when recompiling spider clips):**

| Clip category | Flags | Clip usages |
|---|---|---|
| gait (walk/run/turn/idle-as-movement) | `make_walk_sound` | **`quad_movement`** (+ step points) |
| attack (`elephant_attack_1` verified in-editor) | `client_prediction, lock_movement, enforce_all` | — |
| death (`elephant_death`) | `make_bodyfall_sound, client_prediction, do_not_keep_track_of_sound, enforce_all, update_bounding_volume` | — |
| rear (`elephant_rear`) | `lock_movement, enforce_lowerbody` | — |

Our spider attack/death clips currently ship with NO flags — they work, but lack ADOD_Beasts's polish
flags (`lock_movement` on attacks stops the mount sliding mid-bite; `make_bodyfall_sound` on
deaths adds the thud). Set these when the clips get their Kit recompile.

---

## Hard-won gotchas (each cost real debugging time)

1. **`primary_bone_axis='Y'`, NOT `'X'`.** The documented TAOM preset was `'X'`, which **force-aligns every bone to world-X and destroys mirror symmetry** (heads kept, tails flipped → `tail_err` 0→2.77 on a *symmetric* rig). `'Y'` (Blender's natural bone axis) preserves it. **Verified in-game**: the spider loads symmetric with `'Y'`. This applies to skeleton, mesh, AND anim exports.
2. **Anim root = `<skeleton>_notused`** (e.g. `spider_skeleton_notused`), not just `_notused`. Confirmed from the working warg: its anim FBXs use `Skeleton_Warg_notused`, and its compiled clips are `skeleton_warg_notused|…|warg_idle`. The skeleton-name prefix binds the anim; the `_notused` suffix tells the engine not to register a second skeleton.
3. **The clip-name prefix form doesn't matter.** The warg ships clips bare (`warg_run`), single-prefix (`skeleton_warg_notused|warg_walk_r`), AND double-prefix (`…_notused|…_notused|warg_idle`) — all work. So NLA-bare or all-actions export are both fine.
4. **The Modding Kit's bone-octahedron DISPLAY looks "jacked" but is COSMETIC.** With `add_leaf_bones=False` the Kit computes bone tails itself and draws them scrambled. **Bannerlord doesn't use bone tails for skinning** — only each bone's origin + rotation. Verified by reading the imported tpac's rest matrices directly: positions AND orientations are mirror-symmetric (`err=0`). **Judge by the model viewer / mesh, never the skeleton-editor bone display.**
5. **The `dir_err` (bone Y-axis mirror) metric is UNRELIABLE** — it reads ~1.9 even on a *confirmed-correct* symmetric rig (degenerate short bones like fangs give meaningless `y_axis`). **Use `tail_err` (head/tail position mirror) for symmetry**, not `dir_err`.
6. **The engine lowercases skeleton + bone names** (`Skeleton_Warg`→`skeleton_warg`, `Root_M`→`root_m`). `spider_skeleton` is already lowercase.
7. **Editing edit-bones does NOT update slotted-action fcurve paths** (Blender 5.x) — lowercasing bones broke an export to a 0.06 MB empty file. Byte-size is a reliable "empty bake" detector.

---

## Animation retargeting (sp_skeleton clips → spider_skeleton)

The animator's 34 clips are on the **broken** `sp_skeleton`; the correct rig is `spider_skeleton`. Because their **rest orientations differ**, clips don't transfer 1:1 (binding `sp_skeleton` clips straight to `spider_skeleton` = the "explosion").

**Retarget method that works (mostly):**
- Import the source clip FBX (brings `sp_skeleton` + the action).
- For each `spider_skeleton` bone, add **`COPY_ROTATION`** constraint → matching `sp_skeleton` bone (by **lowercased name**), **`owner_space='LOCAL'`, `target_space='LOCAL'`** (copy the motion *delta-from-rest*; preserves the target's rest pose). **NOT world-space** — world-space forces the target to the source's world orientation and, since rests differ, shoves the whole body off its rest (abdomen flings up — the first broken attempt).
- `bpy.ops.nla.bake(visual_keying=True, clear_constraints=True, bake_types={'POSE'})` over the source frame range.
- Export with the anim recipe (`spider_skeleton_notused`, primary Y).

**Remaining problem (IN PROGRESS):** LOCAL retarget keeps the body grounded, but **per-leg orientation skew** remains — because `sp_skeleton`'s leg-bone local axes differ from `spider_skeleton`'s, the same local rotation lands in a skewed frame, so **some legs animate correctly and others swing backward** (alternating). Three ways forward:
- **(a)** Proper **rest-compensated retarget** (per-bone delta-conjugation `Q_tgt = D·Q_src·D⁻¹`, `D` = the rest-orientation difference) — would fix all legs at once. Not yet implemented.
- **(b)** Per-leg manual correction (reverse the wrong legs' swing) — the user's chosen path; slow.
- **(c)** Animator re-authors on `spider_skeleton` directly (cleanest, production quality).

### Leg-bone map (spider_skeleton, appendage bases, front→back)
Armature-space head positions (×100 cm; front = −Y / fangs, back = +Y / abdomen). **Bone-name side is FLIPPED vs geometry: `_r` bones are at −X, `_l` at +X.**

| Appendage | Bone (per side) | parent | Y (front→back) |
|---|---|---|---|
| chelicera/fang | `joint5_r/l` | head_m | −50.8 (frontmost) |
| pedipalp / front | `joint17_r/l` | head_m | −31.9 |
| leg | `joint40_r/l` | chest_m | −18.4 |
| leg | `joint34_r/l` | spine2_m | −2.4 |
| leg | `joint28_r/l` | spine1_m | +16.6 |
| leg | `joint22_r/l` | root_m | +35.2 (rearmost) |

(The 4 walking legs are `joint40, joint34, joint28, joint22` front→back; `joint17`=pedipalp, `joint5`=fang.
**CONFIRMED 2026-06-15** (user + the bite-collision work): **Leg 1 = front**, and the engine bone INDICES
(via `python tools/tpac_skeleton_dump.py <spider_correct_geo.tpac> spider_skeleton` — index = bone-array order,
root=0 then constraint-child order) are:

| Leg / part | bones (per side) | front-RIGHT `_r` idx | front-LEFT `_l` idx |
|---|---|---|---|
| **Leg 1 (front)** | `joint40-44` (shoulder→thigh→knee→tibia→tip) | **14,15,16,17,18** | **19,20,21,22,23** |
| Leg 2 | `joint34-38` | 3-7 | 8-12 |
| fang `joint5_r/l` | chelicerae | 26 | 32 |
| mouth `joint12_m` = 25, `chest_m` = 13, `head_m` = 24 | (central) | | |

The combat bite-collision uses the **front legs' outer bones** (`SpiderConfig` front-leg consts: thigh→tip =
15-18 / 20-23). See [spider.md → "Damage + bite-collision tuning"](spider.md) + memory
`feedback_creature_bite_collision_real_bones_not_placeholders`.)

---

## Deliverables (all in `E:\LOTRAOMAssets\_auto_workspace\`)
- **`spider_correct.fbx`** — `spider_skeleton` (62 bones, **untouched**) + all 3 variants split L/R (`sk_spider_forest_{a,b,c}` base 33 bones + `sk_spider_forest_{a,b,c}_2` additional 30 bones, all 6 LODs, primary Y). **NOTE (corrected 2026-06-13): the split was done on a now-refuted "per-mesh bone cap" premise — see the corrected "Mesh-split" section below. It is NOT needed for bone count; a single mesh may skin the whole ≤63-bone skeleton.** (The original single-mesh export is regenerable from the haterade blend.)
- **`spider_correct_geo.tpac`** (in LOTRLOME_Armory) — imported + **IK/ragdoll transplanted** (62 bodies, 61 D6). `.backup` saved.
- **`spider_walk.fbx`** — forward walk (`sp_walk_1_001`) LOCAL-retargeted onto `spider_skeleton` (`spider_skeleton_notused` root). Body grounded; legs need per-leg polish.
- **`spider_test_anim.fbx`** — crude leg-curl test (validated the anim pipeline).
- Variants: `spider_correct_Xaxis.fbx` (primary X — proves it breaks symmetry), `spider_correct_Yleaf.fbx`.

## Source-of-truth reference: the WARG (works in-game)

> **Where the warg FBX sources live after the 2026-08-28 absorption.** The warg's runtime data and
> cooked assets moved into `LOTRLOME_Armory`, but `Alliance.Wargs/AssetSources/` (646 MB of FBX and
> PNG) deliberately did **not**: it is editor-only and does not ship. The paths below therefore still
> point into the retired module's folder. **Retire that folder by renaming it to `Alliance.Wargs.OFF`,
> not by deleting it**, or the rig and animation sources needed for any future warg animation work are
> gone. See [lotrlome-warg-changes.md](../reference/lotrlome-warg-changes.md).

`E:\Steam\...\Modules\Alliance.Wargs` — `AssetSources/2_lotr/monster/warg/Warg_Rig_V5.fbx` (rig, root `Skeleton_Warg`), `animations/*.fbx` (anim roots `Skeleton_Warg_notused`), `Assets/.../animations/*_geo.tpac` (compiled clips). This is where the `_notused` convention + the working-creature structure were confirmed.

## Mesh-split — the "per-mesh bone limit" was a MISDIAGNOSIS (corrected 2026-06-13)

> **⚠️ THE ~40 PER-MESH BONE LIMIT DOES NOT EXIST.** This section originally claimed a single mesh
> could skin to only ~40 bones and that the spider's `PreloadForRendering` AV came from overflowing it.
> That is **false** and was disproven during the chariot port (issue #279). Authoritative correction —
> memory `feedback_no_40_bone_per_mesh_limit.md`:
> - **The only bone limit is `Skeleton.MaxBoneCount = 64`** (engine code), a cap on TOTAL bones per
>   *skeleton*, NOT per mesh. **Author skeletons to ≤63** for safety. There is no smaller per-mesh cap.
> - **Two in-game proofs:** the war elephant renders as ONE mesh skinned to **59 active bones**; the
>   chariot renders as ONE mesh skinned to **54 active bones** (both horses). Both > 40.
> - **The warg is split ONLY for its FUR** — `warg_low_fur` is a separate mesh so the fur can
>   cloth-simulate / move independently of the body. The split is **cloth-driven, not bone-driven**
>   (the original "bone-driven, not just cloth" line below was exactly backwards).
> - **The spider's L/R split was therefore unnecessary for bone count** (62-bone skeleton ≤ 64). Its
>   single-mesh AV's true cause is **unestablished** — do not cite ~40, do not invent a replacement.
>   (That `PreloadForRendering` render AV is distinct from the spider's separate `TickAnimations` AV,
>   which was the missing `quad_movement` tag — different call site, different fix.)

**Historical record (the 2026-06-05 belief, now corrected).** `sk_spider_forest_c` weights to 58 bones
in one mesh; this was *believed* to overflow a "~40 per-draw palette" and cause the
`AccessViolationException` in `Agent.PreloadForRendering` at spawn. The warg's meshes were cited as
proof (`warg_low` ≈ 40 bones, `warg_low_fur` 35) — but those figures are the warg's own design (its
body + a separate cloth-sim fur mesh), not evidence of an engine cap. The split was done anyway and the
AV went away, but the elephant (59 bones, one mesh, no AV) shows the ~40 reasoning was wrong.

**The fix (in `spider_correct.fbx`).** Split each `sk_spider_forest_{a,b,c}` along the body midline into a base (Left, **33 bones**) + a `_2` additional (Right, **30 bones**), all 6 LODs, clamping cross-midline weights so each half references only its side's legs + the 10 central bones (`root_m`, `spine1/2_m`, `chest_m`, `head_m`, `joint12-16_m`). The 62-bone skeleton is left **byte-identical**. Scripted in Blender via the MCP: per vertex, take the dominant-bone side (`_l`/`_r`/`_m` suffix) → duplicate → delete the other side's verts → drop the other side's vertex groups → renormalize → remove empty groups.

**To finish it (human seam):** import `spider_correct.fbx` → **re-run `tpac_skeleton_transplant.py spider_skeleton`** (the import regenerates the geo tpac + wipes the IK, same as any mesh re-import) → in `spider_mount_a` (`LOTRAOM_horses.xml`) add `<AdditionalMeshes><Mesh name="sk_spider_forest_c_2" /></AdditionalMeshes>` (the warg pattern) → flip `SpiderConfig` from the warg stand-in back to `"spider"` / `"spider_mount_a"` → test the render (all 8 legs, no AV).

**Lesson — skeleton + meshes must ship in ONE FBX.** A mesh-only FBX export (armature deselected) **drops the skin weights entirely** (verified: re-import shows 0 vertex groups) — Blender stores skin in the FBX armature deformer, so without the armature the meshes import static. So you cannot import the IK'd skeleton from one file and skinned meshes from another; the split meshes + the unchanged skeleton go in the same `spider_correct.fbx` (the editor also rejects duplicate-named skeletons across two imports).

## Blender debugging notes
- **Render to PNG to SEE the spider** — viewport *screenshots* via MCP came back empty (mesh has a 90° import rotation + framing fails). Instead: add a camera + sun, `sc.render.engine='BLENDER_WORKBENCH'`, `bpy.ops.render.render(write_still=True)` to a file, then `Read` it. This works.
- **Keep the user's viewport intact:** use `hide_render` (render-only) NOT `hide_set` (viewport hide), and don't repoint `scene.camera` / reframe the user's view. Restore with: unhide all, show only `sk_spider_forest_c`+armature, `view_selected`, solid shading, exit camera view.
- `'-Y'` is NOT a valid `to_track_quat` up axis (only X/Y/Z) — caused a render error.

## Editor location
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_wEditor` (the Modding Kit / model viewer). The FBX→tpac import code is native/editor-only (not in the decompiled shipping set), so the import path is traced **empirically** (examine the output tpac) rather than by decompiling.

## Retarget add-on verdict (KBSBAUDRICE/Retarget)
**Not adopting.** Humanoid/preset-based, GUI-first, hard to script, GPL, Blender-5-only — poor fit for creature rigs. Use TAOM's own constraint+bake retarget (above) or `tools/blender_bone_retargeter.py`.

## Next steps (updated 2026-06-11, post-polish)
1. ~~Tag the idle/turn clips~~ DONE via byte-patch (9 clips total tagged: walk_2/left/right, run,
   idle, idle2, turn_left/right, jump — all pose-correct clips bound in `as_spider`).
   **Durable fix remains:** Kit-recompile those clips with the flags set, replacing the patched files.
2. ~~Rider thrust loop~~ DONE: partial `as_human_warrior` block (warg precedent) binding the
   spider's usage actions → `rider_warg_*` clips. Future: bespoke spider-rider clips.
3. Fix the walk's per-leg skew — rest-compensated retarget (a), else per-leg (b), else re-author (c).
4. Bake `primary_bone_axis='Y'` + the `_notused` convention + **the quad_movement flag step** into
   `tools/blender/creature_anim_ops.py` exports / the compile checklist.
5. Evaluate the unused 112KB `an_spi_charge` clip as the pounce visual (vs `an_spi_attack_charge`).

---

## Changelog

- 2026-06-14 — Re-bundled the dropped `spider_skeleton` resource back INTO the split-mesh `spider_correct_geo.tpac` via the new `tpac_skeleton_inject.py` (4-item tpac, distinct package GUID), since the 06-13 standalone skeleton-only tpac crashed the engine; `tpac_skeleton_extract.py` deprecated.
- 2026-06-13 — Restored the loose `spider_skeleton` resource that the Blender-loop mesh re-export had dropped (mesh-only tpac → null `CreateAgentSkeleton` → riderless spider); fixed via an extracted standalone skeleton tpac (later superseded by the 06-14 inject fix).
- 2026-06-11 — Giant spider rideable mount working in battle; root-caused the universal mount-context AccessViolation to `an_spi_*` movement clips compiled without the `quad_movement` tag + step points, and byte-grafted the tag onto the elephant template (9 clips); documented the `quad_movement` section here.
- 2026-06-03 — Established and proved the correct skeleton/mesh/IK pipeline (62-bone `spider_skeleton`, `primary_bone_axis='Y'`, `_notused` anim root, `tpac_skeleton_transplant.py` IK/ragdoll) and authored this doc; full ~24-clip set rest-compensated-retargeted and bound in the action set.

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/creature-animation-blender-mcp-workflow.md](../ai-includes/creature-animation-blender-mcp-workflow.md)
- [docs/ai-includes/creature-mount-authoring.md](../ai-includes/creature-mount-authoring.md)
- [docs/features/spider.md](./spider.md)
- [docs/reference/lotrlome-spider-mount-changes.md](../reference/lotrlome-spider-mount-changes.md)
- [docs/research/creature-pipeline/LESSONS-LEARNED.md](../research/creature-pipeline/LESSONS-LEARNED.md)
- [docs/reviews/rca-spider-troop-2026-06-04.md](../reviews/rca-spider-troop-2026-06-04.md)

<!-- backlinks-end -->
