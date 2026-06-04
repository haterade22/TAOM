# Creature Pipeline — MORNING QUEUE (human-gated steps)

Ordered actions only a human can do (Modding-Kit GUI, in-game test, Cascadeur, git). The autonomous loop made **no commits** and edited **no live game assets**. Artifacts live in `E:\LOTRAOMAssets\_auto_workspace\` and `docs/research/creature-pipeline/`. Full context: `loop-log.md`, `IMPROVEMENTS.md`, `LESSONS-LEARNED.md`, `spider-skeleton-mapping.md`.

## A. Spider animations → into the game (do in order)
1. ✅ **DONE (round 2) — skeleton-name mismatch fixed.** `…/LOTRLOME_Armory/ModuleData/Animations/action_sets_spider.xml` line ~13 now binds `skeleton="spider_skeleton"` (was `erkamspider_skeleton`; the compiled skeleton resource + every compiled clip name are `spider_skeleton|…`). A `.bak` was written to `E:\LOTRAOMAssets\_auto_workspace\xml_backups\` first. This was the **sole binding blocker** — R2.2 verified all 20 compiled clips now resolve (`clip-binding-map.md`, `IMPROVEMENTS.md` I14). **The spider's existing animations should play in-game as-is; just smoke-test (step 4).** No further XML edit needed unless the test shows otherwise.
2. **(OPTIONAL — only to ship the *improved* clips) Modding-Kit import** `E:\LOTRAOMAssets\_auto_workspace\spider_anims_ALL.fbx` — armature-only, 59-bone, but a **75-take UNCURATED dump** (re-import-verified R2.4; `export_all_actions_fbx` dumped every session action + loop intermediates + import `.001` dups, **not** a clean set). Import only the takes named in `clip-binding-map.md` (31 canonical), name clips `an_dg_spi_*` to match the bindings. **The existing in-game clips already work after step 1 — this step is only for the foot-slide-fixed `_inplace` walks + the genuine `walk_right`.** (Cleaner alternative: have the loop re-export just the needed clips with `export_bannerlord_fbx` — one take each, ~370 KB.)
   - **Walks:** use the `sp_walk_left_001_inplace` / `sp_walk_right_001_inplace` takes — the originals foot-slide (root translation ±2.36u was stripped). (If Bannerlord quadruped locomotion turns out to want root motion, use the originals instead — verify in-game.)
   - **walk_right:** use the genuine `sp_walk_right_001` take. The *old compiled* clip was a mislabeled `turn_right` copy (LESSON L1) — that's why it looked wrong.
3. **Rebind in `action_sets_spider.xml`:** `act_spider_walk_right` → the real walk_right clip (drop the fallback to `walk`); `act_spider_attack_back` → still genuinely missing (see C).
4. **In-game test** (Custom Battle, per `docs/features/spider.md`). Confirm anims play after the name fix.

## B. Integration strategy for the NEW rig (REVISED round 2)
Strategy B (bake the new 59-bone clips onto the in-game 62-bone `spider_skeleton`) is **BLOCKED**: that 62-bone skeleton is **not importable** — no 62-bone source FBX is on disk; it lives only inside `sk_spider_forest_c_geo.tpac` (R2.3, `spider-rig-generations.md`).
**RECOMMENDED instead — option (c) re-import-and-rename:** re-import the NEW 59-bone rig into the Modding Kit and **name its skeleton `spider_skeleton`**. Since the 59 bones are a lowercased subset of the 62, the new clips then compile directly against the (now-correct) action_set bindings, unchanged; the 3 missing tip bones (`joint16_m`, `joint21_l/r`) simply go unskinned (acceptable). Full options table + the 3 rig generations: `spider-rig-generations.md`.

## C. Cascadeur (only for net-new motion)
- `attack_back`: no source clip exists anywhere — author it (or mirror `attack_front`) in Cascadeur, or use `creature_anim_ops.mirror_action` once it's validated.
- Optional physics-driven additions (pounce, death variants) per the original plan's Phase 5.

## D. New-creature skeletons (generalization — proven, all 4 morphologies)
- All four named creature types build + verify from JSON specs (names + parents match, 0 mismatches): `chariot.json` (9, mechanical), `ram.json` / `goat.json` (22, quadruped), `troll.json` (21, humanoid). chariot/ram/troll auto-weighted on proxy meshes. Index + how-to: `skeleton-specs.md`.
- To finish any creature: model a mesh, refine the auto-weights, animate, export via `export_bannerlord_fbx` (one clip) or `export_all_actions_fbx` (all takes), then the Modding-Kit compile seam.

## E. Commit the loop's artifacts (when ready)
The loop deliberately made **no commits** (your new-factions work is uncommitted). Stage ONLY these — **never `git add -A`**:
```
git add tools/blender/creature_anim_ops.py tools/blender/skeleton_spec.py tools/blender/specs/
git add docs/research/creature-pipeline/
git commit -m "feat(tools): Blender creature animation + skeleton-spec pipeline (overnight loop)"
```
The `.blend`-derived art under `E:\LOTRAOMAssets\_auto_workspace\` is outside the repo — keep or relocate as you prefer.

## F. Doc-accuracy note (Main repo — human edit, loop did not touch)
- **`docs/features/spider.md:103`** cites `LOTRLOME_Armory/ModuleData/action_sets.xml` for the `as_spider` binding, but the **live** binding (and the `skeleton="spider_skeleton"` attribute fixed in round 2) is in the split file `…/Animations/action_sets_spider.xml`. Propose updating that line to cite the split `Animations/action_sets_spider.xml`. (The monolithic `action_sets.xml` may also exist, but the loaded/edited binding is the split one.)
