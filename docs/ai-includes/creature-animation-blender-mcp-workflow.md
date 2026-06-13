# Creature & rider animation refinement via Blender-MCP — workflow, theory, results

How TAOM refines and authors creature locomotion **and** mounted-rider animations directly in a
live Blender session driven over MCP, with a quantitative + visual feedback loop. Distilled from the
2026-06-12/13 session that refined the elephant locomotion set, fixed the spider idle, mapped the
rider-animation system, and fixed the chariot's untagged gait clips. Companion to [`spider-skeleton-animation-pipeline.md`](../features/spider-skeleton-animation-pipeline.md)
(rig/skeleton truth) and [`elephant.md`](../features/elephant.md).

> **Hard boundary:** Blender → `.tpac` compile is **Modding-Kit-GUI-only**. This workflow produces
> refined clips + **Kit-ready FBX**; it cannot compile or in-game-test them. Movement clips then MUST
> be tagged `quad_movement` at Kit compile (see [`spider.md`](../features/spider.md) "How-to") or the
> game AVs at `Skeleton.TickAnimations` ([[feedback-quad-movement-tag-required-for-gait-clips]]).

## 1. Environment & toolkit

> **In-repo copies (committed — use these).** The Python toolkit now lives in the TAOM repo:
> [`tools/blender/harness.py`](../../tools/blender/harness.py),
> [`tools/blender/spider_cfg.py`](../../tools/blender/spider_cfg.py),
> [`tools/blender/creature_anim_ops.py`](../../tools/blender/creature_anim_ops.py),
> [`tools/tpac_clipinfo.py`](../../tools/tpac_clipinfo.py), and
> [`tools/patch_quad_movement.py`](../../tools/patch_quad_movement.py). The `E:\LOTRAOMAssets\…`
> paths cited below are the original authoring-workspace locations (kept for provenance) — prefer the repo copies.

- **Blender 5.1.2** — the **slotted-action API**: `Action.fcurves` is GONE. Fcurves live under
  `action.layers[].strips[].channelbags[].fcurves`. Author keys via `pose_bone.keyframe_insert`
  (auto-creates the slot/layer/channelbag). FBX import/export ops need a UI context — wrap in
  `bpy.context.temp_override(window, area=VIEW_3D, region=WINDOW)` or they fail on internal `mode_set`.
- **Harness:** `E:\LOTRAOMAssets\Elephant\_refine_tools\harness.py` — `exec()` it each MCP call.
  Creature-agnostic via module globals (`ARM_NAME, BODY_NAME, ROOT, FEET, ARMOR, RENDER_DIR`); the
  spider layer `_auto_workspace\_spider_refine\spider_cfg.py` exec's the harness then overrides them
  (+ adds a top-down view for the 8-leg gait). Functions:
  | fn | purpose |
  |---|---|
  | `setup_scene / view_side/front/top/3q / render_frame` | Workbench render rig + cameras |
  | `montage(paths,out,cols)` | numpy in-Blender contact sheet (PowerShell GDI was unreliable) |
  | `set_action(name)` | bind action + slot[0] (works cross-rig if bone names match) |
  | `extract_clip(src,f0,f1,name)` | matrix_basis re-bake of a frame range → retimed 1..N clip |
  | `analyze_gait(name)` | foot lift / swing-phase order / stance slip / body bob / in-place / loop seam |
  | `phase_shift_bones(name,bones,shift,period)` | cyclic time-shift = re-phase a gait |
  | `damp_leg_lift(name,legbones,foot,reduce)` | height-weighted lift reduction; planted frames stay grounded |
  | `freeze_toward_rest(name,bones,factor)` | blend bones toward rest pose (walk→idle conversion) |
  | `timescale_action(src,dst,N)` / `reverse_action` / `boost_loc_amplitude` | cadence / backward / bob |
  | `export_clip_fbx(name,out)` | Kit-ready armature-only FBX |
- **Clip-range parser:** `tpac_clipinfo.py` reads per-clip `_anm.tpac` Source1/Source2 + flags
  (the upstream pack packs many gaits into one long source FBX; this recovers the per-clip frame ranges).
- **quad_movement byte-graft patcher:** `_chariot_refine\patch_quad_movement.py` — parse-based; grafts
  the `quad_movement` ClipUsages + step-points meta tail from a tagged donor clip onto an untagged
  movement clip, preserving the target's own head/timing (the offset-level recipe lives in
  [`spider.md`](../features/spider.md) "How-to"). `montage.ps1` is deprecated — use `harness.montage`.
- **Feedback loop:** `render_frame` → `montage` → **Read the PNG** (visual) + `analyze_gait` (numbers).
  For rider work: composite rider onto mount + **viewport screenshot** (armatures don't render in
  Workbench; bones show only in the viewport).

## 2. Theory (the rules that make it look right)

- **In-place authoring.** Locomotion clips have **zero net root forward travel**; the engine supplies
  translation. Verify `analyze_gait → rootY_net ≈ 0`. Baked forward root motion double-speeds + slides.
  ([[feedback-movement-anims-in-place-engine-driven]])
- **No foot float on amplitude edits.** Reducing a leg's swing toward its *mean* lifts the planted
  foot (floats). Always anchor reductions to the **planted-frame pose** weighted by foot height
  (`damp_leg_lift`) so stance frames are untouched.
- **Phase is time, not amplitude.** Re-sequencing a gait (e.g. pace→lateral) is a cyclic time-shift of
  the leg curves (`phase_shift_bones`) — it preserves each foot's trajectory (no float risk) and only
  changes *when* each leg steps. Safest high-impact gait fix.
- **Gait biomechanics:**
  - *Elephant* — 4-beat **lateral sequence** (LH→LF→RH→RF, ~quarter-cycle spacing). NEVER a pace
    (ipsilateral pair together = camel waddle) or a trot/canter (their mass = no aerial phase; the
    fast gait is a faster **amble**). Feet low; body stays level.
  - *Spider* — 8 legs, **alternating tetrapod** (two diagonal sets of 4 alternate); body low/level.
    An *idle* holds a braced splayed stance — NOT a walk-in-place.
  - *Chariot* — a **horse team** (two full vanilla-named horse bone sets, A + B, on one 60-bone
    `chariot_skeleton`) + cart/pole/wheels; the wheels carry a baked rotation spin, and the rider
    STANDS (sit bone `root`). Watch for the two horses being phase-locked (a unison "toy" look) —
    desync horse-B with `phase_shift_bones` if so.
- **Loop cleanliness** — `analyze_gait` reports the frame-1↔frame-N pose delta; a cyclic clip wants it
  near 0. Time-shift/freeze ops that sample cyclically preserve a clean loop.

## 3. Creature-locomotion workflow (proven on the elephant)

1. **Recover ranges** — `tpac_clipinfo.py` on the deployed `_anm.tpac` → (clip, Source1, Source2, flags).
2. **Import the source FBX** (the long all-gaits take) with the UI temp_override; bones must match the
   target rig 1:1 (they did — actions bind cross-rig by data-path).
3. **`extract_clip(src, f0, f1, name)`** → a faithful, retimed, in-place clip. Snapshot a `_SRC` copy.
4. **`analyze_gait`** → quantify defects (lift asymmetry, swing-phase order, stance slip, bob, seam).
5. **Refine, verifying each step (keep only if render + metrics improve — simplicity criterion):**
   `phase_shift_bones` (fix pacing), `damp_leg_lift` (tame over-lift), `timescale_action` (run cadence),
   `reverse_action` (walk_backwards), `freeze_toward_rest` (walk→idle).
6. **`export_clip_fbx`** → armature-only, `primary_bone_axis=Y, forward=-Y, up=Z`, no leaf bones, baked,
   take = scene name; armature renamed `<skel>_notused`. Round-trip verify by re-import.
7. **Hand off** → Kit-compile each FBX **with `quad_movement` + step points** (movement clips only),
   bind in the action set, in-game Custom-Battle test.

## 4. Rider-animation system (how mounted riders work)

- The rider is a **humanoid on `human_skeleton`** (28 bones: pelvis, spine/spine1/spine2, neck, head,
  l/r clavicle+upperarm_twist×2+foretwist×2+hand+finger0, l/r thigh/calf/foot/toe0). Anim-export name
  `human_skeleton_notused`.
- Mounted rider clips are bound in the **`as_human_warrior`** action set
  (`skeleton="human_skeleton" movement_system="bipedal"`) via per-mount action codes:
  `act_<mount>_*` → `rider_<mount>_*` clip (e.g. `act_warg_forward_gallop` → `rider_warg_forward_gallop`).
- The **rider is parented to the mount's `rider_sit_bone`**; the clip poses the body relative to that.
  Sit bones: **warg `Spine1_M`, spider `chest_m`, elephant ` Spine1_05`, horse `horsespine2`.**
- **Warg has a full dedicated set** (`Alliance.Wargs\AssetSources\2_lotr\monster\warg\animations\
  rider_warg_*.fbx`): idle, stand×2, walk(±L/R, backward), trot, canter, gallop(±L/R), dash, quickstop,
  strafe(L/R), attack_running, attack_stand, jump(start/loop/end), taunt.
- **The spider reuses the warg rider rows** → the goblin sits warg-style (straddling a narrow wolf
  back) on a broad spider — wrong body conformance + height.
- **Elephant has its own** `…\elephant\animations\elephant_rider_*.fbx` (walk/gallop±/trot±/stand1-3/
  attacks/dismount±/fall±/walk_with_banner) — dedicated, but improvable.

### 4a. Rider-authoring workflow (the composite method)

1. Open the **mount** working file (rig + a gait, e.g. spider + `an_spi_idle`).
2. Import a rider clip (`rider_warg_idle.fbx`) — brings `human_skeleton` + the clip (armature-only, no
   body mesh). Bind via `set_action`-style slot assignment.
3. **Composite:** `rider.matrix_world = Matrix.Translation((mount.matrix_world @
   pose.bones[sit_bone].matrix).translation)` then correct the facing to the mount's forward axis.
   `rider.show_in_front=True; rider.data.display_type='OCTAHEDRAL'`.
4. **Verify the seat fit.** Two ways: (a) bone-only — `rider.show_in_front=True`, viewport
   screenshot; or (b) **PROVEN, meshed (preferred)** — import `Alliance.Wargs\...\orc_rider\
   orc_rider.fbx`, which is a full meshed orc rider (armor/arms/greaves/helmet/hood) skinned to the
   **same 28-bone `human_skeleton`**. Assign a `rider_*` clip's action to the orc armature (bones
   match → it poses) and it **renders in Workbench** → real visual verification of height, leg
   straddle vs body width, and lean. (Verified 2026-06-13: warg idle on the spider sits the goblin
   *forward on the cephalothorax in a tight warg-straddle*, small on the giant spider → a
   spider-specific re-pose is warranted. Setup saved as
   `spider_rider_MESHED_composite_WORK_20260613.blend`.)
5. **Re-pose for the mount's seat** — adjust pelvis height + thigh splay + torso lean per clip; for a
   non-bipedal broad mount like the spider the legs sit wider/higher than on a warg.
6. Export `rider_<mount>_*.fbx` (same recipe, armature renamed `human_skeleton_notused`).
7. **Bind:** author a mount-specific rider partial — `act_<mount>_*` codes in an `as_human_warrior`
   block pointing at the new `rider_<mount>_*` clips — and reference it from the mount's monster/action
   set (the warg's `action_sets_warg.xml` is the template).

## 5. Results — how it turned out (2026-06-12/13)

| Area | Status | Evidence |
|---|---|---|
| **Elephant locomotion** | **DONE** — 7 Kit-ready FBX (walk/run/trot/walk_backwards/turn_L/R/idle) | `E:\LOTRAOMAssets\Elephant\clips_refine_20260612\` + README_HANDOFF; export round-trip verified |
| ↳ walk | pace → 4-beat lateral sequence; front lift 1.02→0.48 (grounded); in-place | `CMP_walk_src_vs_refined.png` |
| ↳ run | upstream-pack diagonal trot → natural fast amble (lateral, no suspension) | metrics + strip |
| **Spider idle** | **DONE** — walk-in-place bug → settled braced stance; perfect loop (0°), in-place, breathing | `_spider_refine\renders\STRIP_idleW_*` |
| Spider walk/run | deferred (functions in-game; per-leg skew is a hard retarget issue) | `spi_*_src` kept |
| **Rider system** | **mapped + composite technique established**; spider warg-reuse fit problem shown | viewport composite at spider `chest_m` |
| Spider rider clips | **not authored** — clear path documented (§4a) | — |
| Elephant rider clips | **not improved** — dedicated clips located; assess via composite next | — |
| **Chariot gait clips** | **DONE** — 2 of 24 clips (`chariot_gait_walkfast`/`walkbackfast`) shipped without `quad_movement` (the doc falsely claimed all tagged); byte-grafted the tag + step points from tagged siblings, verified | `_chariot_refine\patch_quad_movement.py`; `*.bak-untagged` |
| Chariot wheels / rider | already-fine, untouched | wheels rotate (720° baked spin); 5 rider clips + XSLT injection clean |

**Net:** the *creature* side landed concretely (elephant locomotion shipped to FBX; spider idle fixed
+ verified). The *rider* side is the larger remaining investment — fully understood, with a working
verification method and a step-by-step authoring path, but the per-clip re-posing + binding is not
done. Nothing here is Kit-compiled or in-game-tested (GUI-only).

## 6. Remaining work (clear next steps)
1. Spider rider: fix composite facing → re-pose `idle/stand/walk/gallop/attack` for the `chest_m` seat
   → export `rider_spider_*` → author `act_spider_*` rider partial + rebind the spider off warg rows.
2. Elephant rider: composite `elephant_rider_*` on ` Spine1_05`, assess, improve the weakest (likely
   the mounted attacks / idle), re-export.
3. Spider walk/run: revisit the per-leg retarget skew if the gait reads poorly in-game.
4. Kit-compile + in-game-test the elephant set (tag `quad_movement`) — the gating step for everything.
5. **Chariot:** horse-team phase-lock check (desync horse-B if unison); parameterize
   `tools/audit_mount_parity.py` to cover the chariot vs the **vanilla-horse** baseline (it is absent
   from the tool's `MOUNTS` list); cart bounce + standing-rider stability (art-direction + in-game);
   the missing `as_human_map_with_banner` chariot rider rows (campaign-map rider falls back to default).
6. **Stale-tool fix:** `tools/blender/creature_anim_ops.py` still has `primary_bone_axis='X'` (≈3
   occurrences) — should be `'Y'` per the export recipe; fix before reusing that tool.

## 7. Reusable assets from this session
- **Python toolkit (committed to the repo — see the §1 callout):**
  [`tools/blender/harness.py`](../../tools/blender/harness.py) (render rig + gait ops),
  [`tools/blender/spider_cfg.py`](../../tools/blender/spider_cfg.py) (8-leg config layer),
  [`tools/blender/creature_anim_ops.py`](../../tools/blender/creature_anim_ops.py) (clip ops),
  [`tools/tpac_clipinfo.py`](../../tools/tpac_clipinfo.py) (per-clip range/flag parser),
  [`tools/patch_quad_movement.py`](../../tools/patch_quad_movement.py) (quad_movement byte-graft patcher).
- Work scenes: `elephant_refine_WORK_20260612.blend`, `spider_anim_WORK_20260612.blend`,
  rider composite WIP. Pristine backups in each `_backups\`.
- Running log: `E:\LOTRAOMAssets\_anim_refine_NOTES_2026-06-12.md`. Memory:
  `project-elephant-animation-refine-inflight`.

## 8. Lessons / pitfalls (transferable)

Each links its backing memory; the session's meta-lesson is last.

- **In-place authoring** — locomotion clips have zero net root forward travel; the engine translates.
  Baked root motion double-speeds + slides. [[feedback-movement-anims-in-place-engine-driven]]
- **`quad_movement` is mandatory on movement clips** — an untagged gait AVs at `Skeleton.TickAnimations`
  on the first mount tick in a *live mission* (never in preview/detached). Fix = the byte-graft (recipe
  in [`spider.md`](../features/spider.md) "How-to"; parse-based patcher in §1).
  [[feedback-quad-movement-tag-required-for-gait-clips]]
- **Blender 5.1.2 slotted-action API** — `Action.fcurves` is gone (layers/strips/channelbags; author via
  `pose_bone.keyframe_insert`); FBX I/O needs a VIEW_3D `temp_override`. The Kit-export recipe is fixed
  (armature-only, `primary_bone_axis=Y, forward=-Y, up=Z`, no leaf bones, baked, take = scene name,
  armature `<skel>_notused`). [[feedback-blender-512-slotted-action-api]]
- **Phase is float-safe; amplitude is not** — re-phasing (`phase_shift_bones`) preserves each foot's
  trajectory; amplitude reductions must anchor to the planted-foot pose (`damp_leg_lift`) or the stance
  foot floats.
- **Quantitative gait QA** — `analyze_gait` (foot lift, swing-phase order, stance slip, body bob,
  in-place, loop seam) turns "looks off" into numbers; pair it with the render→montage→Read visual loop.
- **Rider = the human skeleton, verified by compositing** — author rider poses on the 28-bone
  `human_skeleton`, parent to the mount's `rider_sit_bone`, and verify the *meshed* fit via
  `orc_rider.fbx` (§4a). [[feedback-rider-animation-on-mount-composite-verify]]
- **META-LESSON — re-verify a port's documented completeness.** `chariot.md` *claimed* the upstream pack authored
  *all* clips with `quad_movement`; an independent `tpac_clipinfo` re-check found 2 gait clips were
  untagged. A port's own doc asserting a property holds for *all* elements is a CLAIM to verify against
  the artifacts, not a fact. [[feedback-ported-data-upstream-bugs-vanilla-baseline]]
- **Stale-tool watch** — `tools/blender/creature_anim_ops.py` still has `primary_bone_axis='X'`; audit a
  tool's defaults before reusing it.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/chariot.md](../features/chariot.md)
- [docs/features/elephant.md](../features/elephant.md)
- [docs/features/spider-skeleton-animation-pipeline.md](../features/spider-skeleton-animation-pipeline.md)
- [docs/features/spider.md](../features/spider.md)

<!-- backlinks-end -->
