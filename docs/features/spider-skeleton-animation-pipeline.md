# Spider Skeleton + Animation Pipeline (Blender → Modding Kit → Bannerlord)

**Status (2026-06-03):** Skeleton + mesh + IK/ragdoll pipeline **PROVEN in-game**. **Full animation clip set retargeted + bound** — all ~24 clips retargeted `sp_skeleton`→`spider_skeleton` via rest-compensated retarget, exported as `an_spi_*` FBXs; forward walk + run use the procedural metachronal-wave builder; `action_sets_spider.xml` repointed to the `an_spi_*` set (all bindings resolve); `spider_correct.fbx` re-exported with all 3 mesh variants (a/b/c + LODs). **Remaining (human seam):** re-import the full-mesh FBX to the Kit (the `spider_skeleton` already carries its IK/ragdoll joints — no re-transplant needed), then in-game test. This doc is the source of truth — proven recipe, gotchas, deliverables.

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
- Import as **Skeleton Animation** → Owner Skeleton `spider_skeleton` → make an **Animation Clip** (Source 1=0, Source 2 = the clip's last frame, Duration auto > 0, Blend in ≈ 0.1) → bind in `action_sets_spider.xml`.

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

(The 4 walking legs are most likely `joint40, joint34, joint28, joint22` front→back; `joint17`=pedipalp, `joint5`=fang. **Confirm leg numbering with the user before per-leg edits.**)

---

## Deliverables (all in `E:\LOTRAOMAssets\_auto_workspace\`)
- **`spider_correct.fbx`** — `spider_skeleton` + `sk_spider_forest_c` + LODs (primary Y). The skeleton+mesh foundation.
- **`spider_correct_geo.tpac`** (in LOTRLOME_Armory) — imported + **IK/ragdoll transplanted** (62 bodies, 61 D6). `.backup` saved.
- **`spider_walk.fbx`** — forward walk (`sp_walk_1_001`) LOCAL-retargeted onto `spider_skeleton` (`spider_skeleton_notused` root). Body grounded; legs need per-leg polish.
- **`spider_test_anim.fbx`** — crude leg-curl test (validated the anim pipeline).
- Variants: `spider_correct_Xaxis.fbx` (primary X — proves it breaks symmetry), `spider_correct_Yleaf.fbx`.

## Source-of-truth reference: the WARG (works in-game)
`E:\Steam\...\Modules\Alliance.Wargs` — `AssetSources/2_lotr/monster/warg/Warg_Rig_V5.fbx` (rig, root `Skeleton_Warg`), `animations/*.fbx` (anim roots `Skeleton_Warg_notused`), `Assets/.../animations/*_geo.tpac` (compiled clips). This is where the `_notused` convention + the working-creature structure were confirmed.

## Blender debugging notes
- **Render to PNG to SEE the spider** — viewport *screenshots* via MCP came back empty (mesh has a 90° import rotation + framing fails). Instead: add a camera + sun, `sc.render.engine='BLENDER_WORKBENCH'`, `bpy.ops.render.render(write_still=True)` to a file, then `Read` it. This works.
- **Keep the user's viewport intact:** use `hide_render` (render-only) NOT `hide_set` (viewport hide), and don't repoint `scene.camera` / reframe the user's view. Restore with: unhide all, show only `sk_spider_forest_c`+armature, `view_selected`, solid shading, exit camera view.
- `'-Y'` is NOT a valid `to_track_quat` up axis (only X/Y/Z) — caused a render error.

## Editor location
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_wEditor` (the Modding Kit / model viewer). The FBX→tpac import code is native/editor-only (not in the decompiled shipping set), so the import path is traced **empirically** (examine the output tpac) rather than by decompiling.

## Retarget add-on verdict (KBSBAUDRICE/Retarget)
**Not adopting.** Humanoid/preset-based, GUI-first, hard to script, GPL, Blender-5-only — poor fit for creature rigs. Use TAOM's own constraint+bake retarget (above) or `tools/blender_bone_retargeter.py`.

## Next steps
1. Fix the walk's per-leg skew — try the rest-compensated retarget (a), else per-leg (b), else re-author (c).
2. Retarget the full clip set (walk_left/right, idle, run, turns, attacks, deaths) the same way → bind in `action_sets_spider.xml`.
3. Bake `primary_bone_axis='Y'` + the `_notused` anim convention into `tools/blender/creature_anim_ops.py` exports.
4. Re-enable the disabled spider feature (4 `DISABLED 2026-05-14` markers) once animations are in.
