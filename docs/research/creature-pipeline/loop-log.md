# Creature-Pipeline Autonomous Loop — Log

Self-paced overnight loop. Brief: `autonomous-loop-prompt.md`. Guardrails: no git commits, no C#, non-destructive (Blender ops on duplicates; new art → `E:\LOTRAOMAssets\_auto_workspace\`), queue the manual seams (Modding-Kit compile / in-game test / Cascadeur). Blender MCP live (`localhost:9876`).

---

## Iteration 1 — 2026-06-02 — Track A: full 34-action audit
- **SELECT:** audit every action in `ErkamSpider (1).blend` — foundation for all Track-A decisions.
- **GROUND:** reused the Blender 5.1 slotted-action fcurve path (`action.layers[].strips[].channelbag(slot).fcurves`) validated in Phase 0.
- **IMPLEMENT:** read-only `bpy` audit (root-motion, loop-seam, keyframes, checksum) over all 34 actions.
- **VERIFY (evidence):** full results in `spider-action-audit.md`. Confirmed: `walk_left/right` carry ±2.362u Root_M (all other locomotion in-place); 34 → ~12-15 canonical (dup clusters enumerated); **names unreliable** (`attack_left_001`≈`attack_top_001/002`; `attack_right_002` checksum-collides with `walk_right_001`); `walk_right` rot-seam 13.35 outlier.
- **KEEP:** analysis only; no asset modified.
- **PROCESS LESSON:** action NAMES are unreliable (proven twice now — old `walk_right=turn_right`, plus these clusters) → the pipeline MUST verify name↔content (render/scrub) before any export or bind. Bake this into `creature_anim_ops.audit` and the standard creature workflow.
- **NEXT:** Track B — author `tools/blender/creature_anim_ops.py` (`audit_actions` formalized + dedup report + name↔content verify helper); then Track A — strip Root_M from a *duplicate* of `walk_left`, measure.

---

## Iteration 2 — 2026-06-02 — Track B: stand up `creature_anim_ops.py` (audit + strip_root_motion)
- **SELECT:** author the reusable bpy op library; ship only validated ops.
- **GROUND (empirical):** tested `action.copy()` on a throwaway dup of `walk_left` — copy preserves all 413 fcurves; `Root_M.location` (3 fcurves, 120 keys over 40 frames) flattens to frame-0 value; ORIGINAL untouched (still 2.3624); test copy removed cleanly (34→34).
- **IMPLEMENT:** `tools/blender/creature_anim_ops.py` — `_get_fcurves`, `_root_translation`, `audit_actions` (VALIDATED), `strip_root_motion` (VALIDATED — copy + flatten root, non-destructive). mirror/reverse/retime/close_loop/decimate/export are honest `NotImplementedError` stubs until each is verified.
- **VERIFY (evidence):** exec'd the module in Blender — `audit_actions` self-check matches iteration 1 (`walk_left` kf16520/root2.362/chk357594; `idle_1` all-zero); `strip_root_motion('sp_walk_left_001')` → `sp_walk_left_001_inplace`, root_before `[-0.0001,0.0072,2.3624]` → root_after `[0,0,0]`, 120 keys; stub raised `NotImplementedError`.
- **KEEP:** module kept (validated). Session left clean (34 actions).
- **PROCESS LESSON:** ship only VALIDATED ops (stub the rest); derived-action name is exactly `src+suffix` (`<src>_inplace`) — downstream curation/cleanup must use that exact name (a verify-harness typo looked for `sp_walk_left_inplace` and leaked one action; fixed by confirming counts return to baseline). Always assert action-count returns to baseline after a non-destructive op test.
- **NEXT:** Track A — render/scrub to verify name↔content for locomotion clips (names proven unreliable) before generating the canonical in-place set; Track B — implement+verify `close_loop` then `mirror_action`.

---

## Iteration 3 — 2026-06-02 — Track A: name↔content via pose-distance (FLAWED metric — became a lesson)
- **SELECT:** verify name↔content (names unreliable) via numeric pose comparison instead of fragile viewport renders (which mutate session state + depend on camera/lighting).
- **GROUND:** evaluate fcurves at 8 phase-normalized frames per clip — no session mutation, no render.
- **IMPLEMENT:** naive RMS pose-distance over ALL channels, 23 clips.
- **VERIFY:** NON-DISCRIMINATIVE — walk↔run 0.023, walk↔attack 0.05 (false "identical"). Only `<0.03` trustworthy: `attack_front`≡`attack_front_001` (0.0). Useful side-result: `attack_right_002` never paired near `walk_right_001` → the earlier checksum collision was a **FALSE alarm** (they are distinct).
- **REVERTED** the metric (flawed). → **LESSON L2** recorded in LESSONS-LEARNED.md.

## Iteration 4 — 2026-06-02 — Track A: variance-weighted dedup (FIXED metric)
- **SELECT:** fix the metric so dedup is reliable.
- **IMPLEMENT:** keep only moving channels (`std>0.02` → 234/413), z-normalize each, RMS.
- **VERIFY (evidence):** discriminative now — dups 0.0–0.16, distinct 0.4–2.6. **Confirmed dups:** `attack_front`≡`attack_front_001` (0.0), `attack_bottom`≡`bottom_attack_001` (0.145), `charge_001`≡`charge_002` (0.161). **Variant (NOT dup):** `attack_top_001` vs `_002` (0.356), `run_001` vs `.001` (0.405), `attack_charge` vs `charge_attack` (0.41). `idle_1` vs `idle_2` distinct (0.239 — keep both).
- **KEEP:** metric validated → promote to `creature_anim_ops.pose_distance`/`find_duplicates` next iter. → **LESSON L3** (checksum over-groups) + **IMPROVEMENT I3** recorded.
- **NEXT:** Track B — add `pose_distance`+`find_duplicates` to `creature_anim_ops.py` (now validated), then `close_loop`; Track A — build the verified canonical clip list from the dedup, then generate the in-place set.

---

## Iteration 5 — 2026-06-02 — Track B: promote dedup metric + add close_loop (3 ops validated)
- **SELECT:** ship the validated variance-weighted dedup into the toolkit + implement `close_loop`.
- **IMPLEMENT:** added `_pose_samples`, `_distance_matrix`, `find_duplicates`, `pose_distance`, `close_loop` to `creature_anim_ops.py` (mirror/reverse/retime/decimate/export still stubbed).
- **VERIFY (evidence):** `find_duplicates` (16-clip set, thr 0.17) → 231 kept channels; dups `attack_front`≡`attack_front_001` (0.0), `attack_bottom`≡`bottom_attack_001` (0.138), `charge_001`≡`charge_002` (0.165); variants correctly EXCLUDED (`attack_top_001/002` 0.389, `run_001/.001` 0.401). `pose_distance(attack_front, attack_front_001)` = 0. `close_loop('sp_walk_right_001')`: rot_seam **13.355 → 0** (410 channels closed), loc_seam 2.848 → 2.37 (root location skipped by design). Session clean (34).
- **KEEP.** → IMPROVEMENT I4.
- **LESSON L4:** `find_duplicates` distances are reference-set-relative (std basis) — absolute values shift across runs but the dup/variant *separation* is robust.
- **NEXT:** Track B — `mirror_action` (verify a true L/R mirror round-trips: mirror(walk_left) ≈ walk_right), then `export_bannerlord_fbx`; Track A — build the canonical clip list + generate the in-place locomotion set into `_auto_workspace`; Track C — skeleton-spec scaffolder.

---

## Iteration 6 — 2026-06-02 — Track B: spider 59↔62 bone mapping + skeleton-name finding
- **SELECT:** compute new-rig(59)↔in-game(62) bone mapping to decide the integration strategy (A vs B).
- **GROUND:** `tpac_skeleton_scan` + `tpac_skeleton_dump` on the live `sk_spider_forest_c_geo.tpac`. In-game skeleton = **`spider_skeleton`**, 62 bones lowercase, Usage=`horse`, 62 bodies + 61 D6 constraints (ragdoll already populated).
- **VERIFY (evidence):** set-diff (single python invocation) → the new 59 bones are a **STRICT SUBSET** of the in-game 62. NEW-RIG-ONLY: none. IN-GAME-ONLY: `joint16_m`, `joint21_l`, `joint21_r` (3 tip bones). → **Strategy B (retarget onto existing 62) is clean** (1:1 by lowercased name; 3 extra bones stay at rest). Artifact: `spider-skeleton-mapping.md`.
- **⚠️ FINDING:** `action_sets_spider.xml` binds `skeleton="erkamspider_skeleton"` but the compiled resource is `spider_skeleton` — likely binding mismatch → queued for in-game verification (possible root cause for non-playing anims).
- **LESSON L5:** git-bash `/tmp` scratch is unreliable here (write failed silently, masked by `|| true` → empty set + bogus diff). Keep multi-step shell work in ONE invocation or use repo-relative scratch.
- **KEEP.** → IMPROVEMENT I5.
- **NEXT:** Track B — `mirror_action` + `export_bannerlord_fbx`; Track A — canonical clip list + in-place set; Track C — skeleton-spec scaffolder.

---

## Iteration 7 — 2026-06-02 — Track B: FBX export (all-takes validated; single-clip blocked by slotted-action limitation)
- **SELECT:** implement+verify FBX export (needed for the morning compile queue).
- **GROUND:** `get_python_api_docs` confirmed the `bpy.ops.export_scene.fbx` 5.1 signature; slotted-action binding wasn't in docs → tested empirically (`action_slot = act.slots[0]` binds).
- **VERIFY (evidence):** export FINISHED; `node extract_fbx_bones.js` → 1 Null + **59 LimbNode, 0 meshes**, correct hierarchy. BUT two different clips exported **byte-identical 19 MB** → red flag → re-import showed the FBX contains **ALL 34 actions as takes** (correct frame ranges `run_2`[1,12]/`walk_1`[1,30] + real varying fcurves), not one. Cause: Blender 5.x slotted actions make `bake_anim_use_all_actions=False` export every compatible action.
- **DECISION:** shipped `export_all_actions_fbx` (validated multi-take export — armature-only, Bannerlord axes, all clips in one FBX → Modding Kit imports each as a clip). Demoted `export_bannerlord_fbx` (true single-clip) to TODO (needs NLA isolation).
- **LESSONS L6** (slotted-action FBX = all-takes), **L7** (bash cwd persists; use absolute paths). **IMPROVEMENT I6.**
- **KEEP.** **NEXT:** Track A — generate in-place locomotion set + queue the all-anims FBX for Modding-Kit compile; Track C — skeleton-spec scaffolder; Track B — single-clip via NLA (lower priority, all-takes works).

---

## Iteration 8 — 2026-06-02 — Track C: skeleton-spec scaffolder (generalization foundation)
- **SELECT:** build the declarative skeleton-spec + bpy scaffolder — the core capability for custom creature skeletons (chariot/ram/goat/troll).
- **GROUND/PROVE:** inline round-trip of `sp_skeleton` (extract edit-bone head/tail/roll/parent → rebuild → diff) → 59 bones, names match, 0 parent mismatches, rebuilt armature cleaned up.
- **IMPLEMENT:** `tools/blender/skeleton_spec.py` — `extract_spec` / `build_from_spec` / `auto_weight` (ARMATURE_AUTO first-pass, unverified) / `roundtrip_check`. Spec = `{name, bones:[{name,parent,head,tail,roll}]}`.
- **VERIFY (evidence):** `roundtrip_check("sp_skeleton")` → `{bones:59, names_match:true, parent_mismatches:[]}`; saved the spider as a reusable spec `E:\LOTRAOMAssets\_auto_workspace\spider_skeleton_spec.json`; session clean (7 objects).
- **KEEP.** → IMPROVEMENT I7.
- **NEXT:** Track C — author chariot (rigid: body + axle + wheels + yoke) / ram / goat starter specs + build+auto-weight demo; Track A — in-place locomotion set + morning compile checklist.

---

## Iteration 9 — 2026-06-02 — Track C: chariot spec built + auto-weighted (scaffolder generalizes)
- **SELECT:** prove the scaffolder + `auto_weight` on a NEW creature (not just the spider round-trip). Chariot = the most automatable case (rigid/mechanical).
- **IMPLEMENT:** authored `tools/blender/specs/chariot.json` (9 bones: root, body, axle, `wheel_L`/`wheel_R` as separate spin bones, pole, yoke, `hook_L`/`hook_R`).
- **VERIFY (evidence):** `build_from_spec` → 9 bones, names_match true, 0 parent mismatches; `auto_weight` on a proxy cube created **9 vertex groups** (one per bone); session clean (7 objects = baseline).
- **KEEP.** → IMPROVEMENT I8 — generalization proven end-to-end (JSON spec → armature → skinned).
- **NEXT:** Track C — ram + goat quadruped specs (same pipeline); Track A — in-place locomotion set + `MORNING-QUEUE.md`.

---

## Iteration 10 — 2026-06-02 — Track A: in-place locomotion set + canonical FBX + MORNING-QUEUE
- **SELECT:** produce the concrete spider-animation deliverables + the human morning queue (the user's primary goal).
- **IMPLEMENT/VERIFY (evidence):** `strip_root_motion` on `walk_left/right` → root `[±2.3624]→[0,0,0]` (120 keys each; foot-slide fix); `export_all_actions_fbx` → `E:\…\_auto_workspace\spider_anims_canonical.fbx` (59 bones, 36 takes incl the 2 in-place; 36 MB). Session restored to baseline (34 actions). Wrote `MORNING-QUEUE.md` (ordered human steps: skeleton-name fix, Modding-Kit import, rebinds, in-game test, Cascadeur, exact `git add` paths).
- **KEEP.** → IMPROVEMENT I9.
- **NEXT:** Track C — ram/goat specs; Track B — `mirror_action` (for `attack_back`) + single-clip NLA export.

---

## Iteration 11 — 2026-06-02 — Track C: ram + goat quadruped specs (generalization across creature types)
- **SELECT:** extend the scaffolder to quadrupeds (ram/goat) — chose this over the risky quaternion `mirror_action`.
- **IMPLEMENT:** generated `tools/blender/specs/ram.json` + `goat.json` (22 bones each: root, pelvis, spine_mid, chest, neck, head, horn_L/R, tail1/2, 4 legs×3) via a single python generator (L5); parent-integrity self-check passed (no dangling parents).
- **VERIFY (evidence):** `build_from_spec` both → 22 bones, names_match true, 0 parent mismatches; ram `auto_weight` on a sphere proxy → 22 vgroups; session clean (7 objects = baseline).
- **KEEP.** → IMPROVEMENT I10. Wrote `skeleton-specs.md` (index + how-to-author).
- **NEXT:** Track B — `mirror_action` (GROUND the roll-aware flip; the naive quaternion mirror likely needs Blender's pose-flip) + single-clip NLA export; Track C — troll spec (reuse human skeleton base).

---

## Iteration 12 — 2026-06-02 — Track C: troll (humanoid) spec; mirror_action deferred (honest)
- **SELECT:** add a humanoid creature type → scaffolder covers mechanical + quadruped + humanoid + arachnid.
- **IMPLEMENT:** generated `tools/blender/specs/troll.json` (21 bones: root/pelvis/spine1-2/chest/neck/head + 2 arms×4 + 2 legs×3, oversized; can reuse the human action_set).
- **VERIFY (evidence):** `build_from_spec` → 21 bones, names_match true, 0 parent mismatches; `auto_weight` on a cylinder proxy → 21 vgroups; session clean (7).
- **DECISION (Track B): `mirror_action` DEFERRED — not shipped.** Low spider value (`walk_right` exists; `attack_back` is front/back, not a L/R mirror) + high complexity (roll-aware pose-flip needed; the naive quaternion mirror is unreliable for arbitrary bone orientations). Approach documented (Blender `bpy.ops.pose.paste(flipped=True)`) for a future session that actually needs a missing L/R counterpart. Honest non-result — don't ship unvalidated.
- **KEEP** troll. → IMPROVEMENT I11. Updated `skeleton-specs.md`.
- **NEXT:** Track B — single-clip export via NLA isolation (verifiable); otherwise the high-value backlog is largely complete → approach loop-until-dry → `MORNING-REPORT.md`.

---

## Iteration 13 — 2026-06-02 — Track B: single-clip export (NLA) VALIDATED + MORNING-REPORT; loop wind-down
- **SELECT:** close the last toolkit gap (true single-clip-per-FBX) + consolidate.
- **GROUND/IMPLEMENT:** NLA isolation — push one action to a single non-muted NLA strip, export with `bake_anim_use_nla_strips=True` + `bake_anim_use_all_actions=False`.
- **VERIFY (evidence):** `TEST_nla_single.fbx` = **372 KB** (vs 19-36 MB all-takes); re-import showed **exactly 1 take** (`run2`, frames 2-13); session clean (34; NLA removed, action restored). Shipped `export_bannerlord_fbx` (single-clip).
- **KEEP.** → IMPROVEMENT I12, LESSON L8 (NLA resolves L6).
- **WROTE `MORNING-REPORT.md`** (consolidation of everything).
- **STOP:** high-value backlog complete + MORNING-REPORT written → **loop winds down (no further ScheduleWakeup).** Remaining work is incremental / human-gated — see `MORNING-QUEUE.md` + `MORNING-REPORT.md`.

---

# ROUND 2 — curate, correctly-name, wire (Armory-module XML edits NOW PERMITTED by the user; Main-module re-enable still human)

## R2.1 — 2026-06-03 — Track 1: 3-layer clip→binding map (source-of-truth)
- **SELECT:** build the verified deduped clip list + 3-layer naming table (foundation for everything else).
- **GROUND:** live `action_types_spider.xml` (24 `act_spider_*` types) + `action_sets_spider.xml` (`an_dg_spi_*` bindings; skeleton now `spider_skeleton`) + `find_duplicates` on the 34 `.blend` actions.
- **VERIFY (evidence):** session clean at **34 base actions**; `find_duplicates` (245 kept channels) → **3 confirmed dups** (attack_front≡_001 0.0, attack_bottom≡bottom_attack 0.104, charge_001≡002 0.137) → **31 canonical takes**.
- **OUTPUT:** `clip-binding-map.md` — per `act_spider_*` type: bound `an_dg_spi_*` clip + chosen `sp_*` source take + notes. Surfaced: walk_right (fallback; genuine take exists; old tpac mislabeled), attack_back (no source), taunt (absent from new blend), `sp_hit_right_001` (unbound bonus).
- **KEEP.** → IMPROVEMENT I13.
- **NEXT:** task 3 (scan all `spider_*_geo.tpac` → verify each bound `an_dg_spi_*` clip exists = Layer-c truth) + task 2 (per-clip FBX export named from the map).

## R2.2 — 2026-06-03 — Task 3: Layer-c verification (compiled-clip existence)
- **SELECT:** confirm every bound `an_dg_spi_*` clip exists in a compiled tpac (is the action_set actually valid after the skeleton fix?).
- **VERIFY (evidence):** scanned ALL `spider_*_geo.tpac` → 20 distinct compiled clips (`spider_skeleton|an_dg_spi_*`). EVERY action_set binding resolves; the only two missing (walk_right, attack_back) are CORRECT intentional fallbacks. `spider_walk_right_geo.tpac` = mislabeled `an_dg_spi_turn_right.001` (confirmed). idle_03/scratch/v2 tpacs yield no usable clip.
- **CONCLUSION:** with the skeleton-name fix, the action_set is **VALID + COMPLETE — no broken bindings.** The skeleton name was the sole blocker; the spider's existing animations should all bind in-game (pending human test). No further action_set edits warranted (walk_right/attack_back rebinds must wait for compiled clips).
- **KEEP** (verification; updated `clip-binding-map.md`). → IMPROVEMENT I14.
- **NEXT:** task 5 (Strategy-B retarget: extract the 62-bone in-game skeleton to a spec, bake one new-rig clip onto it) → enables compiling the new rig's improved clips (in-place walks, genuine walk_right) against `spider_skeleton`; then task 2 (per-clip FBX export).

## R2.3 — 2026-06-03 — Task 5 GROUND: retarget target unavailable; 3-rig-generation finding
- **SELECT:** Strategy-B retarget validation (bake a new-rig clip onto the 62-bone in-game skeleton).
- **GROUND:** searched `E:\LOTRAOMAssets` for the 62-bone source. Found `sk_spider_forest_a_01.fbx` = **46 LimbNodes** (a THIRD generation; mesh `sk_spider_forest_bm_a1`) + my 59-bone export. **NO 62-bone (`sk_spider_forest_c`) source FBX present.**
- **FINDING:** three spider rig generations — 62-bone in-game `spider_skeleton` / 59-bone new `sp_skeleton` / 46-bone `a_01.fbx`. Documented in `spider-rig-generations.md` with the consequence + a practical recommendation.
- **DECISION:** task 5's clean path (import the 62-bone target) is **BLOCKED** (no source FBX). Reconstructing from tpac rest-frames is feasible but fiddly, and the retarget is a *refinement* (R2.2: existing anims already work). **RECOMMEND** the simpler option (c): re-import the new rig naming its skeleton `spider_skeleton` so the new clips compile against the existing bindings unchanged. Recorded; not attempting the fragile reconstruction.
- Also fixed the verified doc contradiction: `autonomous-loop-prompt.md` line 22 said the in-game rig is `erkamspider_skeleton`=62 (wrong) → corrected to `spider_skeleton`=62.
- **LESSON L9.** → IMPROVEMENT (none — verification/finding iteration).
- **NEXT:** task 7 remaining doc fixes (34/36/77 count + the "canonical" mislabel) + task 4 (close_loop cyclic clips); then wind to an updated MORNING-REPORT (round-2 core complete: the spider is correctly wired; remaining = refinements + human seams).

## R2.4 — 2026-06-03 — Task 7 FINALIZATION: doc-accuracy fixes + round-2 handoff; LOOP STOP
- **SELECT:** correct the verified doc errors (take count + "canonical" mislabel), update the morning handoff to round-2 state, then wind down (round-2 high-value backlog complete).
- **VERIFY (evidence):** re-imported `spider_anims_ALL.fbx` → **75 distinct takes** (authoritative; the docs' "36" was wrong, the "~77 AnimStacks" estimate was right); session restored to 34 base actions after cleanup (no leaked datablocks).
- **IMPLEMENT (non-destructive):**
  - Renamed the dump `spider_anims_canonical.fbx` → **`spider_anims_ALL.fbx`** (35.1 MB; regenerable) so the name stops implying curation; the curated reference is `clip-binding-map.md` (31 source takes).
  - Corrected `IMPROVEMENTS.md` I9 (75 takes, "uncurated dump"), `MORNING-REPORT.md` (count + filename + I/L ranges I1–I14 / L1–L10 + root-cause "FIXED" + revised integration strategy), `MORNING-QUEUE.md` (skeleton fix DONE; new-rig path = option (c) re-import-and-rename; all 4 specs done; new filename; added §F `spider.md:103` doc-fix note).
  - Added **L10** (binary-verify every count) to `LESSONS-LEARNED.md`.
- **CRITIQUE:** tasks 4 (close_loop the cyclic clips) + 6 (`mirror_action`) were optional polish — **deliberately not run**: the loop prompt said don't grind refinements once the round-2 backlog is clear, and neither blocks the human handoff (existing clips already bind; new-rig clips need the human Modding-Kit compile regardless). `mirror_action`/`reverse`/`retime`/`decimate` remain honest `NotImplementedError` stubs.
- **STATE:** ROUND 2 COMPLETE. Spider correctly wired (skeleton fix applied + all 20 clips verified R2.2); pipeline generalizes to 4 creature types; docs accurate. Remaining = human gates only (Modding-Kit compile of any new walk_right/attack_back clips, in-game smoke-test, re-enable the 4 `DISABLED` markers + fang-index probe — all Main-module/human).
- **LOOP STOPPED** — no ScheduleWakeup. Handoff is `MORNING-REPORT.md` + `MORNING-QUEUE.md`.














