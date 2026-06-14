---
name: refine-creature-anim
description: Use when a creature's locomotion or mounted-rider animation looks wrong (pace/float/walk-in-place/idle) and needs refining or authoring in Blender before Kit-compile.
---

# Refine Creature / Rider Animation (Blender-MCP)

Thin entry point. The authoritative workflow, theory, toolkit, and lessons live in
[`docs/ai-includes/creature-animation-blender-mcp-workflow.md`](../../../docs/ai-includes/creature-animation-blender-mcp-workflow.md) — READ IT FIRST.

## When to use
- A shipped creature gait reads wrong (elephant pacing/waddle, spider walk-in-place idle, foot float, slip).
- Authoring new locomotion clips (walk/run/trot/turn/idle/backwards) for a quadruped/octoped.
- Authoring or fixing mounted-rider clips (warg/spider/elephant) — the composite-fit method (master §4a).

NOT for the data/XML/C# side of a mount (that is `/new-creature-mount`), and NOT for Kit-compile or in-game testing (GUI-only, out of scope — the hand-off target).

## Precondition (report-don't-fix per environment-failures.md)
Live Blender 5.1.2 session with Blender-MCP connected, plus the toolkit on the E: drive
(`E:\LOTRAOMAssets\Elephant\_refine_tools\harness.py`, `tpac_clipinfo.py`, `_spider_refine\spider_cfg.py`).
If Blender-MCP is down or the harness is missing, STOP and report — do not self-heal.

## Steps (locomotion — master doc §3)
1. `tpac_clipinfo.py` on the deployed `_anm.tpac` → recover per-clip frame ranges.
2. Import the source FBX with the UI `temp_override`; confirm bone names match the target rig 1:1.
3. `extract_clip(src,f0,f1,name)` → retimed in-place clip; snapshot a `_SRC` copy.
4. `analyze_gait` → quantify defects (lift asymmetry, swing-phase order, stance slip, bob, loop seam).
5. Refine, keeping a step ONLY if render + metrics improve (simplicity criterion): `phase_shift_bones`
   (re-phase a gait), `damp_leg_lift` (tame over-lift), `timescale_action` (cadence), `reverse_action`
   (walk_backwards), `freeze_toward_rest` (walk→idle). Loop: `render_frame` → `montage` → READ the PNG + re-`analyze_gait`.
6. `export_clip_fbx` → armature-only, `primary_bone_axis=Y, forward=-Y, up=Z`, no leaf bones, baked,
   take = scene name, armature renamed `<skel>_notused`; round-trip verify by re-import.
7. Hand off to `/new-creature-mount` for Kit-compile (tag `quad_movement` + step points on MOVEMENT clips) + in-game test.

## MANDATORY post-deploy gate (after replacing ANY `_anm`/`_geo` tpac — refinement is constant, the swap is where it breaks)
Back up the old tpac first (`*.backup`), then after deploying the new one, BEFORE battle-testing:
```
python tools/verify_mount_assets.py <spider|elephant>
```
It catches the three silent regressions a Kit re-export causes — **dropped `<creature>_skeleton`
resource** (mesh-only re-export → `CreateAgentSkeleton` null → RIDERLESS mount, NO crash),
**dropped `quad_movement`** on a measured-gait clip (TickAnimations AV), and **orphaned bindings**.
PASS is necessary, not sufficient — it can't see the mesh bone-palette split or in-game behaviour,
so still do an in-game spawn-with-rider check. Full requirements + the four failure modes:
[creature-mount-authoring.md](../../../docs/ai-includes/creature-mount-authoring.md) → "REPLACING
FBX / TPAC FILES". Lesson: `feedback-mesh-reexport-drops-skeleton-resource` (spider rework
2026-06-13 shipped mesh-only → riderless until caught).

For rider clips use the composite method (master §4a): parent rider `human_skeleton` to the mount's sit
bone, verify fit with the meshed `orc_rider.fbx`, re-pose per clip, export `rider_<mount>_*`, bind an `as_human_warrior` partial.

## Top gotchas (full list in master §8)
- **`quad_movement` is MANDATORY on movement clips** or the game AVs at `Skeleton.TickAnimations`
  (mission-only). This skill produces FBX; the tag is applied at Kit-compile — never ship an untagged
  gait. Byte-graft recipe in `spider.md` How-to.
- **In-place authoring** — locomotion clips have zero net root forward travel (`analyze_gait → rootY_net ≈ 0`);
  the engine supplies translation. Baked root motion double-speeds + slides.
- **Blender 5.1.2 slotted-action API** — `Action.fcurves` is GONE; author via `pose_bone.keyframe_insert`.
  FBX import/export needs `bpy.context.temp_override(window, area=VIEW_3D, region=WINDOW)`.
- **Anchor amplitude edits to the planted-foot pose** (`damp_leg_lift`) or the stance foot floats.
  Re-phasing (`phase_shift_bones`) is float-safe.
- **Re-verify ported-data completeness claims** — a sibling doc once falsely stated all clips were tagged;
  2 were not (chariot RCA).
- **Rider sit-bones:** warg `Spine1_M`, spider `chest_m`, elephant ` Spine1_05`, horse `horsespine2`.
