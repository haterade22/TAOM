# Creature Pipeline — Lessons Learned (running ledger)

Append-only. Each lesson records: **what we believed**, **what was actually true**, the **evidence**, and the **rule it changes** — so mistakes are made once and the pipeline gets smarter. Updated every loop iteration that produces a lesson.

---

## L1 — Action NAMES are unreliable; verify content, not labels  *(iters 0–4)*
- **Believed:** a clip's filename / action name describes its motion.
- **Reality:** proven false twice. The *old compiled* `walk_right_geo.tpac` actually held a clip named `an_dg_spi_turn_right.001` (a duplicate of turn_right). In the *new* `.blend`, `attack_left_001` checksum-matched `attack_top_001/002`.
- **Evidence:** audit checksums (`spider-action-audit.md`) + pose-distance matrices (iters 3–4).
- **Rule:** for **every creature**, verify each clip's actual motion (pose data) before exporting or binding it. Never trust the label.

## L2 — Naive all-channel RMS pose-distance is NON-discriminative  *(iter 3)*
- **Believed:** averaging per-frame bone differences across all channels measures motion similarity.
- **Reality:** it is dominated by the ~180 near-static channels (quaternion `w`≈1, tiny joints), so a *walk vs a run* scored 0.023 and a *walk vs an attack* scored 0.05 — false "identical."
- **Evidence:** iter-3 matrix (`loop-log.md`).
- **Rule:** use **variance-weighted** distance — keep only moving channels (`std > 0.02`; 234 of 413 here) and z-normalize each so it contributes equally. Validated iter 4: true dups land 0.0–0.16, distinct clips 0.4–2.6 (clean separation).

## L3 — Checksum clustering OVER-groups (false duplicates)  *(iter 4)*
- **Believed:** equal keyframe-coord checksum + equal keyframe count ⇒ duplicate.
- **Reality:** it over-groups. `attack_top_001`/`002` are checksum-near-equal but **0.356** apart by the discriminative metric — a *variant*, not a dup. Only 3 pairs are genuine duplicates.
- **Evidence:** iter-4 variance-weighted matrix.
- **Rule:** treat checksum as a cheap **pre-filter only**; confirm a duplicate with variance-weighted pose-distance (`< ~0.17`) before merging anything.

## L4 — Variance-weighted distance is reference-set-relative  *(iter 5)*
- **Believed:** a pose-distance value is absolute.
- **Reality:** the per-channel std (the normalization basis) is computed over the comparison SET, so the same pair shifts with the set (kept channels 234 vs 231; `attack_bottom`/`bottom_attack` 0.145 vs 0.138 across a 26- vs 16-clip set). The dup-vs-variant **separation** is robust; the absolute number is not.
- **Evidence:** iter-4 vs iter-5 runs.
- **Rule:** when comparing distances across runs, pass a fixed `reference_names` set; treat the `~0.17` threshold as set-relative, not universal.

## L5 — git-bash `/tmp` scratch is unreliable in this environment  *(iter 6)*
- **Believed:** I could write a scratch file to `/tmp` in one shell command and read it back in the next.
- **Reality:** the `/tmp` write failed (and `|| true` masked it), so the follow-up read got "No such file" → an empty in-game set and a bogus diff (all 59 bones looked "new-rig-only").
- **Evidence:** iter-6 first attempt vs the corrected single-invocation rerun.
- **Rule:** keep multi-step shell work in ONE invocation (e.g. `subprocess` + parse stdout inside a single python heredoc) or use a **repo-relative** scratch path — never `/tmp`. (Reinforces the standing memory `feedback_no_write_before_reading_tool_output`.)

## L6 — Blender 5.x slotted actions → the FBX exporter writes ALL takes, not one  *(iter 7)*
- **Believed:** `bake_anim_use_all_actions=False` + assigning one action exports just that clip.
- **Reality:** with slotted actions every compatible action is exported as a take regardless — two *different* clips produced a **byte-identical** FBX size, and re-import showed all 34 actions present (correct frame ranges + real motion).
- **Evidence:** iter-7 export + re-import.
- **Rule:** for one-clip-per-FBX under Blender 5.x, isolate via NLA (one non-muted strip + `bake_anim_use_nla_strips=True`) — or accept the all-takes FBX (the Modding Kit imports each take as a clip, which is fine). **The identical-file-size check is a reliable detector that an export isn't actually per-clip.**

## L7 — Bash working directory persists between calls  *(iter 7)*
- **Believed:** each Bash call starts at the repo root.
- **Reality:** a `cd tools` in one call leaked into the next, so `node tools/extract_fbx_bones.js` resolved to `tools/tools/...` and failed (`MODULE_NOT_FOUND`) twice.
- **Evidence:** iter-7 path errors → fixed with an absolute script path.
- **Rule:** use **absolute paths** for scripts and args; avoid `cd` (the cwd carries over, and `cd` can also trigger a permission prompt).

## L8 — NLA isolation gives a true single-clip FBX under Blender 5.x slotted actions (resolves L6)  *(iter 13)*
- **Believed (L6):** you can't get one-clip-per-FBX with slotted actions (`bake_anim_use_all_actions=False` still exports all takes).
- **Reality:** push ONE action to a single non-muted NLA strip and export with `bake_anim_use_nla_strips=True` + `bake_anim_use_all_actions=False` → exactly one take.
- **Evidence:** 372 KB / 1 take (`run2`) vs the 19-36 MB all-takes export; re-import confirmed the take count.
- **Rule:** single-clip → NLA isolation (`creature_anim_ops.export_bannerlord_fbx`); all-clips-in-one → `export_all_actions_fbx`. **File size + re-import take-count are the validators.**

## L9 — Enumerate ALL rig generations; the retarget target may not be importable  *(R2.3)*
- **Believed:** the in-game 62-bone skeleton could just be loaded into Blender to validate a retarget.
- **Reality:** **three** spider rig generations exist — 62-bone in-game `spider_skeleton`, 59-bone new `sp_skeleton`, 46-bone `a_01.fbx` — and **no 62-bone source FBX is on disk**; the 62-bone target lives only inside `sk_spider_forest_c_geo.tpac`.
- **Evidence:** glob of `E:\LOTRAOMAssets` (only `sk_spider_forest_a_01.fbx`=46 + my export=59); R2.2 tpac scan (`spider_skeleton`=62).
- **Rule:** before planning a retarget, confirm the TARGET skeleton is importable; if it lives only in a `.tpac`, either reconstruct from `tpac_skeleton_dump` (fiddly: rest-frame space + roll) or prefer the simpler re-import-and-rename path. Enumerate every generation of a creature's assets (`.blend`, compiled tpac, stray FBXs) before assuming one is canonical.

## L10 — Binary-verify every count; a remembered figure is not evidence  *(R2.4)*
- **Believed:** the all-takes FBX held "36 takes" (34 base actions + 2 in-place walks) — carried in `IMPROVEMENTS.md` I9 + `MORNING-REPORT.md` + `loop-log.md` iter 10 from the export-time guess.
- **Reality:** re-importing `spider_anims_ALL.fbx` and counting distinct imported actions gave **75 takes** — `export_all_actions_fbx` writes every session action *plus* loop intermediates (`_inplace`, loop-close variants) *plus* Blender's `.001` import-collision dups. The figure was ~2× the claim, and the name "canonical" implied a curated set it never was.
- **Evidence:** R2.4 re-import → `imported_take_count = 75`; session restored to 34 base actions after cleanup. (The earlier "~77 AnimStacks" critic estimate was essentially right; "36" was wrong.)
- **Rule:** any count that lands in a doc (take count, bone count, ref count, clip count) must be produced by a fresh tool run *this pass* — re-import + count, scan, `wc -l` — never recalled from an earlier step or estimated at write-time. Renamed the artifact `spider_anims_canonical.fbx` → `spider_anims_ALL.fbx` so the name stops implying curation; the curated reference is `clip-binding-map.md` (31 distinct source takes). Reinforces `feedback_no_write_before_reading_tool_output` + `evidence-over-claims.md` §C.

## L11 — Bannerlord clip names are bare; the `<skeleton>|` is the Owner-Skeleton prefix (+ how to export bare takes)  *(2026-06-03, user-taught + source-verified)*
- **Believed:** a compiled clip's name *is* `spider_skeleton|an_dg_spi_walk` (one string), and the FBX take should carry that.
- **Reality:** the Modding Kit has TWO resource types — **Skeleton Animation** (raw motion; name = `<OwnerSkeleton>|<take>`, where the `<skeleton>|` is applied by the **Owner Skeleton** field, and the FBX take is **bare**) and **Animation Clip** (gameplay resource referencing a skeleton anim + duration/blend/hand-pose/sound/flags params, e.g. `spiderclip`). The FBX take name must be **bare** (`an_dg_spi_walk`); a baked `sp_skeleton|` prefix risks a double prefix.
- **Root cause of the baked prefix (Blender 5.1):** `export_fbx_bin.py:2444` `name = get_blenderID_name(ref_id)`. For `bake_anim_use_all_actions=True`, `ref_id=(ob,act)` → take name `"{object}|{action}"` (always armature-prefixed). For `bake_anim_use_nla_strips=True`, `ref_id=strip` → take name = the **strip name** (bare).
- **Evidence:** all-actions export re-imported as `sp_skeleton.001|sp_skeleton|an_dg_spi_*` (double); NLA export re-imported as `sp_skeleton.001|an_dg_spi_*` (single → bare take). 21 clips, distinct per-clip frame-range/valsum confirmed correct mapping.
- **Rule:** to emit a single multi-take FBX with **bare** take names, lay one NLA track+strip per action (strip named the bare clip name, `action_slot` set for slotted actions), export with `use_nla_strips=True` + `use_all_actions=False`. Codified as `creature_anim_ops.export_bannerlord_clips_fbx`. The re-import single-vs-double-prefix differential is the validator.

## L12 — Lowercasing edit-bones does NOT update slotted-action fcurve paths (silently kills motion)  *(2026-06-03)*
- **Believed:** renaming `armature.data.edit_bones[].name` to lowercase would propagate to the actions' fcurve data paths (legacy Blender behavior).
- **Reality:** under Blender 5.x slotted actions the fcurves live in channelbags; the edit-bone rename did **not** rewrite their `data_path`, so they kept pointing at `pose.bones["Root_M"]` while the bone became `root_m` → no resolve → export baked **empty** takes.
- **Evidence:** post-lowercase export = **0.06 MB / 0.009 s** (vs 6.8 MB / 5 s healthy) and the probe fcurve path still read `pose.bones["Root_M"]`. (Byte-size-as-validator again, cf. L6/L8.)
- **Rule:** never rely on edit-bone rename to fix slotted-action paths. To rename bones for animation, also rewrite each channelbag `fcurve.data_path` (`.replace('pose.bones["Old"]','pose.bones["new"]')`). If only matching a target skeleton's case, prefer letting the Modding-Kit importer case-fold (verify in-editor) over a fragile in-Blender rename.

---

> **L13–L18 below are the spider skeleton+animation pipeline findings (2026-06-03), all VERIFIED in-game. Full writeup: [`docs/features/spider-skeleton-animation-pipeline.md`](../../features/spider-skeleton-animation-pipeline.md).**

## L13 — `primary_bone_axis='Y'`, not `'X'`, for mirrored creature rigs  *(2026-06-03, verified in-game)*
- **Believed:** the documented TAOM FBX preset (`primary_bone_axis='X'`) was correct for Bannerlord creatures.
- **Reality:** `'X'` **force-aligns every bone to world-X and destroys mirror symmetry** — exporting a perfectly symmetric rig with `'X'` and re-importing gave `max_tail_err` 0→2.77 (heads kept, tails flipped). `'Y'` (Blender's natural bone axis) preserves symmetry. The Modding Kit **accepts `'Y'`** — the spider loads symmetric in the model viewer.
- **Rule:** export creature skeletons, meshes, AND animations with `primary_bone_axis='Y'`, `secondary_bone_axis='X'`. The old `'X'` preset in `creature_anim_ops.py` is wrong for mirrored rigs and should be changed. The symptom (heads symmetric, tails asymmetric) gets baked into any `.blend` made from an X-axis FBX import.

## L14 — Animation FBX root = `<skeleton>_notused` (warg-proven)  *(2026-06-03)*
- The working warg's rig FBX has root `Skeleton_Warg` (registers the skeleton); its **animation** FBXs have root `Skeleton_Warg_notused`, and compiled clips are `skeleton_warg_notused|…|warg_idle`. The skeleton-name prefix binds the anim to the right skeleton; the `_notused` suffix stops the engine registering a 2nd skeleton.
- **Rule:** anim-only exports use root `<skeleton>_notused` (e.g. `spider_skeleton_notused`), real bone names underneath, armature-only. NOT just `_notused`. Clip-name prefix form (bare/single/double) does NOT matter — the warg ships all three and works.

## L15 — The Modding-Kit bone-octahedron display is COSMETIC; judge by the mesh  *(2026-06-03)*
- The skeleton editor draws bones from head to a *computed* tail. With `add_leaf_bones=False` the Kit guesses tails and draws them scrambled ("jacked"), even when the skeleton DATA is perfectly symmetric.
- **Verified:** read the imported tpac's rest matrices directly — positions AND orientations mirror-symmetric (`err=0`) — while the editor display looked jacked. **Bannerlord uses only bone origin+rotation for skinning, not tails.**
- **Rule:** validate creature rigs by the **model viewer / mesh**, never the bone-editor display. Read the tpac rest frames (`tpac_skeleton_dump` + world-accumulate) for ground truth.

## L16 — `dir_err` (bone y_axis mirror) is an unreliable symmetry metric; use `tail_err`  *(2026-06-03)*
- The y_axis-mirror error reads ~1.9 even on a *confirmed-correct* symmetric rig (degenerate short bones like fangs → meaningless `y_axis`). Wasted analysis chasing it.
- **Rule:** measure left/right symmetry by `head_err` + `tail_err` (head/tail position X-mirror). Ignore `dir_err`.

## L17 — Cross-rig animation retarget: LOCAL-space, not WORLD-space  *(2026-06-03)*
- Retargeting clips from one rig to another with **different rest orientations** (e.g. broken `sp_skeleton` → correct `spider_skeleton`): **WORLD-space** Copy Rotation forces the target to the source's world orientation → since rests differ, the whole body is shoved off its rest (abdomen flings up). **LOCAL-space** Copy Rotation copies the motion *delta-from-rest* → preserves the target's rest pose (body grounded).
- **Caveat:** LOCAL still skews per-bone where local axes differ (some legs animate right, others reversed). The complete fix is a **rest-compensated retarget** (`Q_tgt = D·Q_src·D⁻¹`, D = rest-orientation difference) or re-authoring on the correct rig.
- **Rule:** retarget with `COPY_ROTATION` `owner_space='LOCAL'`, `target_space='LOCAL'`, map bones by lowercased name, `nla.bake(visual_keying=True)`. Verify by RENDER (workbench+camera→PNG), not viewport screenshot.

## L18 — Render to a PNG to see Blender; keep the user's viewport intact  *(2026-06-03)*
- MCP viewport *screenshots* came back empty for this rig (90° mesh import rotation + framing fails). A real render works: add camera+sun, `render.engine='BLENDER_WORKBENCH'`, `render.render(write_still=True)` → `Read` the PNG.
- Using `hide_set` (viewport hide) + reframing the user's view + repointing `scene.camera` **blanked the user's viewport** — they couldn't see the work. **Rule:** for render isolation use `hide_render` (render-only); never disturb the user's viewport view/camera. (`'-Y'` is not a valid `to_track_quat` up axis — only X/Y/Z.)
