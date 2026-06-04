# Creature Pipeline — Improvements Ledger (before → after, with evidence)

Append-only. Each entry: the concrete gain, **before → after**, and the **evidence** (numbers/paths) so progress is visible and auditable. Updated every loop iteration that produces a measurable improvement.

---

## I1 — Reusable bpy op library stood up  *(iter 2)*
- **Before:** every Blender operation was an ad-hoc inline script.
- **After:** `tools/blender/creature_anim_ops.py` — a reusable, self-checking module with VALIDATED `audit_actions` + `strip_root_motion` (rest honestly stubbed as `NotImplementedError`).
- **Evidence:** `audit_actions` self-check reproduced iter-1 numbers exactly; `strip_root_motion('sp_walk_left_001')` zeroed root `[-0.0001,0.0072,2.3624] → [0,0,0]` (120 keys), original untouched, session returned to 34 actions.

## I2 — Root-motion foot-slide risk found + fix ready  *(iters 1–2)*
- **Before:** `walk_left`/`walk_right` bake ±2.362u of Root_M Z-translation while all other locomotion is in-place — engine-driven movement would foot-slide.
- **After:** `strip_root_motion` validated to neutralize it; the canonical in-place set will be generated once names are content-verified.
- **Evidence:** audit `root_z` column (`spider-action-audit.md`); strip test (I1).

## I3 — Reliable duplicate-detection metric  *(iters 3 → 4)*
- **Before:** checksum clustering implied ~12–15 canonical clips, but **over-grouped** (false dups) and a naive pose-distance was non-discriminative (walk≈attack).
- **After:** variance-weighted pose-distance (moving channels only, z-normalized) cleanly separates duplicates from variants.
  - **Confirmed duplicates:** `attack_front`≡`attack_front_001` (0.0), `attack_bottom`≡`bottom_attack_001` (0.145), `charge_001`≡`charge_002` (0.161).
  - **Variant-not-dup (curate, keep best take):** `attack_top_001`/`002` (0.356), `run_001`/`.001` (0.405), `attack_charge`/`charge_attack` (0.41).
  - **Distinct confirmed:** `attack_right_002` ≠ `walk_right_001` (earlier checksum collision was a false alarm).
- **Evidence:** iter-4 matrix (`loop-log.md`); 234/413 channels kept.
- **Next:** promote this metric into `creature_anim_ops.py` as `pose_distance` / `find_duplicates`.

## I4 — Three more validated ops in the toolkit  *(iter 5)*
- **Before:** `creature_anim_ops.py` had only `audit_actions` + `strip_root_motion`; the dedup metric lived in throwaway scripts.
- **After:** `find_duplicates` + `pose_distance` (the validated variance-weighted metric) + `close_loop` are live, self-checking ops.
- **Evidence:** `find_duplicates` reproduced the iter-4 dup set (`attack_front`≡`attack_front_001` 0.0, `attack_bottom`≡`bottom_attack_001` 0.138, `charge_001`≡`charge_002` 0.165; variants excluded); `close_loop` drove `walk_right` rot-seam **13.355 → 0** (410 channels) while correctly leaving root translation (loc-seam) intact.

## I5 — Spider integration de-risked (59→62 mapping)  *(iter 6)*
- **Before:** unknown whether the new rig could reuse the in-game skeleton (the A-vs-B fork was open; A = re-import skeleton+mesh+monster XML).
- **After:** proven the new 59-bone rig is a **strict subset** of the in-game 62-bone `spider_skeleton` (only `joint16_m`, `joint21_l`, `joint21_r` missing — minor tips). → **Strategy B (retarget onto existing skeleton) is clean**: 1:1 by lowercased name, no skeleton/mesh/monster-XML/fang-index rework.
- **Evidence:** `tpac_skeleton_dump` of `spider_skeleton` (62 bones) vs the `.blend`'s 59; set-diff. See `spider-skeleton-mapping.md`.
- **Bonus finding:** skeleton-name mismatch (action_set binds `erkamspider_skeleton`, resource is `spider_skeleton`) → queued for in-game verification (possible root cause for non-playing anims).

## I6 — Working FBX export (all-takes) for the Modding Kit  *(iter 7)*
- **Before:** no path from Blender to the game; nothing for the morning compile queue.
- **After:** `export_all_actions_fbx` (validated) writes a 59-bone, armature-only FBX with all 34 actions as takes (Bannerlord axes, no leaf bones, baked) → one Modding-Kit import yields all clips.
- **Evidence:** `node extract_fbx_bones.js` → 1 Null + 59 LimbNode, 0 meshes; re-import showed 34 takes with correct frame ranges + real motion.
- **Caveat:** true single-clip-per-FBX deferred (Blender 5.x slotted-action limitation — see L6).

## I7 — Declarative skeleton-spec scaffolder (custom-creature foundation)  *(iter 8)*
- **Before:** no way to build creature skeletons from data; each rig hand-made in a DCC tool.
- **After:** `tools/blender/skeleton_spec.py` (`extract_spec` / `build_from_spec` / `auto_weight` / `roundtrip_check`) turns any armature into a JSON spec and rebuilds it — the foundation for chariot/ram/goat/troll skeletons.
- **Evidence:** `roundtrip_check("sp_skeleton")` → 59 bones, names_match true, 0 parent mismatches; spider persisted as `spider_skeleton_spec.json`.
- **Next:** author chariot/ram/goat starter specs; build + auto-weight a test mesh.

## I8 — Scaffolder generalizes to a new creature (chariot)  *(iter 9)*
- **Before:** the scaffolder was only proven by round-tripping the spider's own skeleton.
- **After:** a brand-new creature (chariot) built from a hand-authored JSON spec **and** auto-weighted end-to-end.
- **Evidence:** `build_from_spec(chariot.json)` → 9 bones, names_match true, 0 parent mismatches; `auto_weight` on a proxy cube → 9 vertex groups (one per bone); session clean.
- **Artifact:** `tools/blender/specs/chariot.json`. This is the "build custom skeletons for many creatures" capability working (chariot = the fully-automatable case; ram/goat/troll next).

## I9 — Spider animation deliverables + morning queue  *(iter 10)*
- **Before:** improvements lived only as toolkit ops; nothing compile-ready; no human handoff.
- **After:** foot-slide-fixed **in-place walks** generated + an **UNCURATED all-takes FBX dump** (`spider_anims_ALL.fbx`, 59-bone armature, **75 takes** — re-import-verified R2.4; `export_all_actions_fbx` dumps every session action incl `_inplace`/intermediates + import `.001` dups, **NOT** a curated 36-take set; the curated reference is `clip-binding-map.md` = 31 distinct source takes); `MORNING-QUEUE.md` has the ordered human steps incl the (now-applied) skeleton-name fix.
- **Evidence:** `strip_root_motion` root `[±2.3624]→[0]`; export = 59-bone armature, **75 takes** (uncurated dump, re-import-verified R2.4 — the earlier "36" was wrong, see L10); session clean (34).

## I10 — Quadruped specs (ram + goat) built from data  *(iter 11)*
- **Before:** generalization proven only on the chariot (mechanical, 9 bones).
- **After:** two quadrupeds (ram, goat — 22 bones each) built + verified from JSON specs; ram auto-weighted (22 vgroups). Scaffolder now spans **mechanical + quadruped + (extracted) arachnid**.
- **Evidence:** `build_from_spec` both → 22 bones, names_match true, 0 parent mismatches; ram `auto_weight` → 22 vgroups; session clean.
- **Artifacts:** `tools/blender/specs/ram.json`, `goat.json`; index `skeleton-specs.md`.

## I11 — Humanoid (troll) spec — scaffolder covers all 4 named morphologies  *(iter 12)*
- **Before:** specs covered mechanical (chariot) + quadruped (ram/goat) + arachnid (spider).
- **After:** + humanoid (troll, 21 bones; can reuse the human action_set as an animation base). The scaffolder is proven across all four creature types the user named (spider, troll, ram/goat, chariot).
- **Evidence:** `build_from_spec(troll.json)` → 21 bones, names_match true, 0 parent mismatches; `auto_weight` → 21 vgroups; session clean.
- **Artifact:** `tools/blender/specs/troll.json`.

## I14 — Action_set bindings verified valid + complete (Layer-c)  *(R2.2)*
- **Before:** unknown whether the bound `an_dg_spi_*` clips actually exist compiled (the skeleton fix made bindings *resolvable*, but clip existence was unverified).
- **After:** scanned every compiled tpac — 20 distinct clips; EVERY binding resolves; the two fallbacks (walk_right→walk, attack_back→attack_front) are correct (no genuine clip exists). **No broken bindings.**
- **Evidence:** tpac scan of all `spider_*_geo.tpac` → `spider_skeleton|an_dg_spi_*` names; only gaps are walk_right (mislabeled tpac) + attack_back (no source).
- **Impact:** the skeleton-name fix was the *sole* binding blocker; the spider's existing animations should all play in-game (pending human test).

## I13 — 3-layer clip→binding map (round 2)  *(R2.1)*
- **Before:** the three naming layers (`act_spider_*` types / `an_dg_spi_*` clips / `sp_*` takes) were conflated across docs; no single source-of-truth, and the dedup wasn't applied to a binding view.
- **After:** `clip-binding-map.md` maps every `act_spider_*` type → bound `an_dg_spi_*` clip → chosen `sp_*` source take, dedup applied (31 canonical takes), with fallback/missing/unbound cases flagged (walk_right fallback, attack_back no-source, taunt absent, hit_right unbound).
- **Evidence:** `find_duplicates` (245 kept channels) → 3 dups; bindings parsed from the live XML.

## I12 — True single-clip FBX export (NLA isolation)  *(iter 13)*
- **Before:** only `export_all_actions_fbx` (all 34 takes, 19-36 MB) worked; single-clip was blocked by the slotted-action limitation (L6).
- **After:** `export_bannerlord_fbx(action)` exports exactly ONE clip via a single non-muted NLA strip + `bake_anim_use_nla_strips=True`.
- **Evidence:** `TEST_nla_single.fbx` = 372 KB (vs 19-36 MB), re-import = exactly 1 take (`run2`, frames 2-13); session clean.
