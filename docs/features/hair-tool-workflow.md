# Hair Tool Groom Workflow

## Overview

`tools/blender/hair_tool_pipeline.py` connects Hair Tool 4.6.1 to TAOM's groom delivery pipeline.
It creates deterministic guides on the actual race head, finalizes existing Hair Systems, transfers
or preserves weights, enforces TAOM's material and upper-mesh rules, writes staged FBX, and renders
four review views.

Hair Tool remains a local GPL-3.0-or-later dependency. No add-on source, node assets, textures, or
presets are vendored. The bridge is TAOM source and reuses the existing TAOM weight, LOD, and
contact-sheet tools.

## Why This Exists

The dwarf kitbash tools already classify and recombine finished cards very well. They do not provide
a modern curve-authoring path for new hair. Hair Tool supplies that path, including interpolated
strands, profiles, UV generation, and mesh conversion. TAOM still owns the parts that determine
whether the result works in Bannerlord: exact head fitting, armature weights, no upper-mesh morph
channels, LODs, material naming, skin registration, and in-game review.

## Architecture

```text
exact race head + armature + finished donors
  -> deterministic TAOM guide curves
  -> Hair Tool interpolated strands and grooming
  -> Hair Tool mesh conversion
  -> material, UV, morph, transform, and weight gates
  -> staged FBX re-import validation
  -> Workbench previews and contact sheet
  -> standard TAOM LODs
  -> skin-list append and Kit import
```

`UVMap` addresses the visible atlas. `HairTool_UV` preserves root-to-tip direction for gradients
and rigging. Both are required after FBX re-import.

Hair Tool conversion removes its source object and returns world-shaped vertices with the source
transform. The bridge projects weights in the raw coordinate frame and then restores the display
transform. A 500 mm projection limit rejects incorrect frames or unusable donors instead of
silently shipping bad weights.

## Reverse-engineered API evidence

The local package is `C:\Users\mikew\Downloads\hair_tool_4.6.1\hair_tool` on the desktop. The
findings below come from its GPL source and actual Blender 5.2.3 calls, not from guessing its UI.

- `__init__.py:58-69` identifies Hair Tool 4.6.1. Its source headers grant GPL-3.0-or-later.
- `hair_baking/hair_geometry_nodes.py:1740-1747` exposes `object.add_hair_system` with `mode`,
  `surface_obj`, `draw_popup`, and `on_active_hair_user`. Author mode uses `INTERPOLATED_STRANDS`.
- `ribbons_operations.py:235-244` exposes `hair_system.convert_to_mesh` with `join_instances`,
  `apply_deformations`, and `weld_verts`. The bridge calls it with `weld_verts=True`.
- `ribbons_operations.py:477-483` converts `UVMapGN` to `UVMap` and `UVHelperGN` to `HairTool_UV`.
- Runtime generator inspection found `Length`, `Density`, `Seed`, and `Resolution` on `Generate
  Strands`, and `Width` on `Flat_Profile_UV`. These are the only starter parameters the bridge sets.
- Background import failed before `gpu.init()`. Registration also requires enabling the add-on
  through `bpy.ops.preferences.addon_enable(module="hair_tool")` so its preferences exist.
- The first conversion probe produced 3,165 vertices with `UVMap` and `HairTool_UV`, but zero
  weights. That established why TAOM's delivery layer is required.
- A metre-scale projection failure exposed the double-transform case. Independent nearest-point
  probes established the fix: project in the raw frame and restore the original display matrix.

The source's broad exception handlers and object-removal behavior are reasons the bridge uses staged
writes, captures names before conversion, and validates a fresh FBX import rather than trusting an
operator's `FINISHED` result alone.

## Dependencies and setup

- Blender 5.2.3 LTS is the tested runtime. Background mode initializes `gpu.init()` before loading
  Hair Tool; the bridge handles this.
- Hair Tool 4.6.1, supplied with `--hair-tool <package or parent folder>`.
- The exact LOD0 `face_base_mesh` for the target race, its armature, and its intended Kit material.
- Finished sibling grooms for weight donors when the converted output is unweighted.
- An explicit staging directory outside the live Armory. Writes are confined to `--out`.

## Profiles

Profiles are starting points for grooming, not finished hairstyles.

| Profile | Target | Material and donors | Length | Density | Width | Resolution | Seed | Guides |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `dwarf_hair` | `Dwarf_Hair_New_lod0` | Verified dwarf material and hair donors | 0.35 | 40 | 0.012 | 6 | 13 | 32 |
| `dwarf_beard` | `SK_Dwarf_Beard_New` | Verified dwarf material and beard donors | 0.45 | 45 | 0.012 | 6 | 17 | 32 |
| `elf_hair` | `Elf_Hair_New_lod0` | Explicit material and donors required | 0.55 | 35 | 0.008 | 8 | 19 | 40 |

Length, density, and width use Blender units. Change them in `PROFILES` when a measured race
workflow justifies a new default. The guides drape down from scalp triangles, with front-face
exclusion for hair and lower-face selection for beards. They are meant to be groomed and braided,
not shipped unchanged.

## Author and finalize

Use the `/groom-hair` skill for the full procedure. The essential commands are:

```bat
blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_tool_pipeline.py -- --mode inspect --hair-tool <path> --out <out>
blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_tool_pipeline.py -- --mode author --profile dwarf_hair --hair-tool <path> --mesh <head> --out <out>
blender-launcher -b <author.blend> --python-exit-code 1 -P tools/blender/hair_tool_pipeline.py -- --mode finalize --profile dwarf_hair --hair-tool <path> --mesh <guide> --armature <rig> --preview --head <head> --out <out>
```

The author report names the `hair_object` to style and finalize. For elves, add the actual
`--material` and at least one `--reference <finished groom>`. Existing staged outputs are preserved
unless `--overwrite` is explicit.

`finalize` requires `"status": "ok"` in `hair-tool-report.json`. It rejects empty output, missing
UV layers, incomplete weights, morph channels, incorrect materials, and excessive donor projection.
The preview files and `preview/previews.json` feed the existing contact-sheet command:

```bat
python tools/groom_contact_sheet.py <out>/preview --meta <out>/preview/previews.json --out <out>
```

Read the sheet. Geometry checks do not establish aesthetic quality.

## LODs and registration

Build the normal five-level chain with the bare target name:

```bat
blender-launcher -b --factory-startup -P tools/blender/add_mesh_lods.py -- --fbx <out>/<target>.fbx --mesh <target without _lod0> --levels 1,2,3,4,5 --ratios 0.7,0.3,0.15,0.07,0.03
```

The ratios follow the existing elf and dwarf groom budgets. Poll `<fbx>.lods-report.json` for
`"stage": "done"`.

For dwarves, append new entries only at the end of the skin's groom list so existing face keys keep
their indices. Use `tools/oneoff/append_dwarf_grooms.py` in dry-run mode before `--apply`. Elven
registration must follow the target race's actual skin structure and preserve its existing indices.

## Key Files

| File | Purpose |
| --- | --- |
| `tools/blender/hair_tool_pipeline.py` | Inspection, guided authoring, delivery, weights, previews, and staged validation |
| `tools/tests/test_hair_tool_pipeline.py` | Profile, guide, launch, projection, and final-mesh unit gates |
| `tools/blender/transfer_upper_mesh_weights.py` | Shared barycentric weight-mixing functions |
| `tools/blender/add_mesh_lods.py` | Standard LOD generation and round-trip checks |
| `tools/groom_contact_sheet.py` | Labelled preview sheets |
| `.claude/skills/groom-hair/SKILL.md` | Automatic Claude workflow for hair and beard tasks |
| `docs/reference/race-face-and-hand-morphs.md` | Engine upper-mesh mapping and morph requirements |
| `docs/reference/provenance-register.md` | Hair Tool license and interop-only provenance |

## Tests

- `python -m unittest tools.tests.test_hair_tool_pipeline`: 20 tests for profiles, guide directions,
  coordinate-launch imports, projection limits, UVs, materials, weights, and morph rejection.
- `python -m ruff check tools/blender/hair_tool_pipeline.py tools/tests/test_hair_tool_pipeline.py`:
  all checks passed.
- Real Blender 5.2.3 inspect and guided-author runs passed with Hair Tool 4.6.1 for dwarf hair,
  dwarf beard, and elf hair.
- Real dwarf delivery produced 1,380 vertices and 832 polygons. All 1,380 vertices are weighted,
  `UVMap` and `HairTool_UV` survived re-import, no morph channels remain, and the only material is
  `m_dwarf_beards_b`.
- Weight projection from the three finished dwarf donors measured 114.156 mm median and 342.1021 mm
  maximum. The four-view preview shows the guided groom on the intended head.
- The standard LOD command produced 1,164, 499, 250, 116, and 50 triangles at LOD1 through LOD5,
  with zero unweighted vertices and no round-trip diffs.

## How to verify in game

This is an authoring pipeline, not a deployed gameplay feature. The current evidence ends at staged
FBX and preview validation; Kit import and in-game acceptance remain required for each asset.

1. Mike imports the staged FBX and saves it in the Modding Kit, then checks every LOD and material.
2. Equip the new groom on its intended race. Compare several face-slider values and confirm the roots
   remain on the skin without eyebrow, beard, or hair lag.
3. Turn the head and confirm the mesh follows the armature rather than staying in world space.
4. Check the relevant helmet and body-armour variants for cover-type behavior and clipping.
5. Record an actual in-game look before calling the asset complete. The current bridge verification
   does not claim that this has happened.

## Limitations

- The bridge does not generate final braids, crowns, or race-specific art direction. Those are
  authored with Hair Tool after guide creation.
- Donor projection is a delivery safeguard, not proof of ideal deformation. Validate a head turn.
- The independent cube-map remesher is not part of this workflow. Hair cards are generated by Hair
  Tool, not quad-remeshed.
- Existing kitbash recipes remain the preferred path for reusing finished cards. This workflow is for
  new curve-authored grooms.

## Changelog

- 2026-11-07: added guided Hair Tool authoring, TAOM delivery gates, previews, profiles, and
  documentation.

## GitHub Issue

- **Issue:** not created; the maintainer waived a retroactive issue for this work.
- **Status:** staged authoring and delivery verified; Kit and in-game acceptance remain asset-specific.
