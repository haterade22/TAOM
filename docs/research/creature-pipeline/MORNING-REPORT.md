# Creature Pipeline — MORNING REPORT (overnight autonomous loop, 2026-06-02)

**13 iterations, self-paced.** Goal: improve the spider's animations **and** build a reusable Blender↔Claude↔Bannerlord pipeline that scales to custom creature skeletons + animations (spider, troll, ram, goat, chariot). **No git commits, no live game assets edited, no C# touched.** Artifacts: `tools/blender/` + `docs/research/creature-pipeline/`; generated FBX/specs in `E:\LOTRAOMAssets\_auto_workspace\`. Full detail: `loop-log.md`, `IMPROVEMENTS.md`, `LESSONS-LEARNED.md`.

## The toolkit (how to call each)
Load in Blender via the MCP: `exec(open(r'…\tools\blender\creature_anim_ops.py').read(), globals())`.

**`tools/blender/creature_anim_ops.py` — 7 validated ops:**
| Op | What it does |
|---|---|
| `audit_actions(names=None)` | per-action metrics: root motion, loop seams, keyframes, checksum |
| `strip_root_motion(name)` | in-place variant (root translation → 0; fixes foot-slide) |
| `find_duplicates(names)` / `pose_distance(a,b)` | variance-weighted dedup (reliable; checksums over-group) |
| `close_loop(name)` | snap last keyframe = first (kills the loop pop) |
| `export_all_actions_fbx()` | armature-only **all-takes** FBX (one import → all clips) |
| `export_bannerlord_fbx(action)` | **true single-clip** FBX via NLA isolation (~370 KB, 1 take) |
| `mirror_action`/`reverse_action`/`retime_action`/`decimate` | honest `NotImplementedError` stubs (not shipped) |

**`tools/blender/skeleton_spec.py`** — `extract_spec(arm)` / `build_from_spec(spec)` / `auto_weight(arm, mesh)` / `roundtrip_check(arm)`. Build any creature armature from a JSON spec.
**`tools/blender/specs/`** — `chariot.json` (9), `ram.json` (22), `goat.json` (22), `troll.json` (21). Spider extracted to `spider_skeleton_spec.json`. All built + verified; chariot/ram/troll auto-weighted. Index + how-to: `skeleton-specs.md`.

## What improved (evidence) — `IMPROVEMENTS.md` I1–I14
- **Spider animations:** full 34-action audit; reliable dedup (3 true dups confirmed); foot-slide fix (in-place walks, root `±2.36 → 0`); loop-close (`walk_right` rot-seam `13.4 → 0`); both export paths working.
- **Skeleton scaffolder** validated by round-tripping the spider (59 bones, 0 mismatches) and generalized to **4 creature types**: chariot (9), ram/goat (22), troll (21) — each built + auto-weighted from JSON.
- **Deliverables:** `spider_anims_ALL.fbx` (an **uncurated 75-take dump** — re-import-verified R2.4; despite the name it is NOT curated; use `clip-binding-map.md` to pick the 31 canonical takes, or have the loop re-export a clean per-clip set) + this report + the morning queue.

## Key findings
- **✅ Root-cause bug FIXED (round 2):** `action_sets_spider.xml` bound `skeleton="erkamspider_skeleton"` but the compiled resource (and every compiled clip name) is **`spider_skeleton`** — this stopped the `as_spider` animations from binding. Now set to `spider_skeleton` (`.bak` saved). R2.2 verified all 20 compiled clips resolve → **the spider's existing animations should play in-game (pending smoke-test).** This was the *sole* binding blocker.
- **Spider integration (revised round 2):** the EXISTING in-game clips need **no rig work** — they bind as-is after the fix above. For the IMPROVED new-rig clips, Strategy B (retarget onto the 62-bone skeleton) is **BLOCKED** — the 62-bone skeleton isn't importable (no source FBX; lives only in `sk_spider_forest_c_geo.tpac`). Use **option (c) re-import-and-rename** (`spider-rig-generations.md`); the new 59-bone rig is a strict subset of the 62 (only `joint16_m`, `joint21_l/r` missing).
- **Action names are unreliable** (mislabels + dups) — verify content, not labels (L1).

## MORNING QUEUE (human-gated — full steps in `MORNING-QUEUE.md`)
1. ✅ **DONE (round 2):** skeleton-name mismatch fixed in `…/LOTRLOME_Armory/ModuleData/Animations/action_sets_spider.xml` (`erkamspider_skeleton` → `spider_skeleton`; `.bak` saved). The existing clips bind now — just smoke-test (step 4).
2. **Modding-Kit import** `E:\LOTRAOMAssets\_auto_workspace\spider_anims_ALL.fbx` — NOTE it is a **75-take uncurated dump**; import only the takes named in `clip-binding-map.md` (31 canonical), name clips `an_dg_spi_*`, and use the `_inplace` walk takes. (Cleaner: have the loop re-export a per-clip set first.) **Simplest integration path overall: re-import the new rig naming its skeleton `spider_skeleton` — see `spider-rig-generations.md` option (c).**
3. **Rebind** `walk_right` (real clip) and `attack_back` in the action_set.
4. **In-game Custom Battle test** (per `docs/features/spider.md`).
5. **Cascadeur:** author `attack_back` (no source clip exists anywhere).
6. **Commit (exact paths — never `git add -A`; your new-factions work is uncommitted):**
   ```
   git add tools/blender/ docs/research/creature-pipeline/
   git commit -m "feat(tools): Blender creature animation + skeleton-spec pipeline (overnight loop)"
   ```

## Process lessons — `LESSONS-LEARNED.md` L1–L10
Names unreliable (L1); variance-weighted distance beats naive RMS + checksums (L2/L3); distance is reference-set-relative (L4); never `/tmp` scratch (L5); slotted-action FBX exports all takes, fixed via NLA (L6/L8); bash cwd persists — use absolute paths (L7); enumerate ALL rig generations before assuming a retarget target is importable (L9); **binary-verify every count** — re-import showed the all-takes FBX is **75 takes, not the 36 first claimed** (L10). **The model lesson: a plausible "FINISHED" is not validation** — the byte-identical FBX size caught a non-per-clip export that looked fine, and a re-import-count caught the take-count overstatement.

## Recommended next priorities
1. The skeleton-name fix is already applied. After an **in-game smoke-test** confirms the existing clips play: **re-enable the disabled spider feature** (4 `DISABLED 2026-05-14` markers in `Main/IoC.cs`, `Main/SubModule.cs`, `Main/_Module/SubModule.xml`, `troops_dolguldur.xml` — Main module, human only) + the runtime fang-bone-index probe.
2. `mirror_action` via Blender's roll-aware pose-flip (when a creature needs a missing L/R clip).
3. Model + skin meshes for chariot/ram/goat/troll — the skeletons + auto-weight first pass are ready; refine weights, animate, compile.
