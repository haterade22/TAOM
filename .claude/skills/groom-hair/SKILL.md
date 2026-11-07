---
name: groom-hair
description: Use when authoring or exporting hair, beards, eyebrows, or other grooms with Hair Tool for TAOM races.
argument-hint: [dwarf_hair|dwarf_beard|elf_hair]
---

# Author and deliver a TAOM groom

Use Hair Tool 4.6.1 for curve authoring and `tools/blender/hair_tool_pipeline.py` for delivery. The
canonical process and engine constraints are in
[docs/features/hair-tool-workflow.md](../../../docs/features/hair-tool-workflow.md).

## Required inputs

- The exact LOD0 head for the race, its armature, the intended Kit material, and finished sibling
  grooms for weight donors. Do not fit a groom to a different head.
- Hair Tool 4.6.1 at a local path. It is GPL-3.0-or-later and is not vendored into TAOM.
- A GitHub issue for the asset, unless Mike waived it for this work.
- An explicit staging directory outside the live Armory. Never overwrite the source blend, donor
  FBX, or an existing staged output without `--overwrite`.

## Author

Run Blender through `%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe`, which detaches.
Poll `<out>/hair-tool-report.json` instead of stdout.

1. Inspect the scene if object names or weights are uncertain:
   `blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_tool_pipeline.py -- --mode inspect --hair-tool <path> --out <out>`
2. Create deterministic downward guides on the active head:
   `blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_tool_pipeline.py -- --mode author --profile <dwarf_hair|dwarf_beard|elf_hair> --hair-tool <path> --mesh <head> --out <out>`
   The report's `hair_object` names the guide system to style and finalize. Profiles set only the
   starting length, density, width, resolution, seed, and guide count. Groom and braid them into
   the actual design with Hair Tool.
3. Open the `.author.blend` in GUI Blender and style the guides. Preserve the source and donor
  objects, both UV channels, the target head, and the armature.

## Finalize

1. Use the guide object from the author report:
   `blender-launcher -b <author.blend> --python-exit-code 1 -P tools/blender/hair_tool_pipeline.py -- --mode finalize --profile <profile> --hair-tool <path> --mesh <guide> --armature <rig> --preview --head <head> --out <out>`
   Elf runs also need `--material <actual Kit material>` and at least one `--reference <finished
   groom>`. Defaults are not guessed for elves.
2. Require `"status": "ok"` in `hair-tool-report.json`. The bridge refuses missing `UVMap` or
   `HairTool_UV`, unweighted vertices, morph channels, wrong materials, empty output, and donor
   projection above 500 mm. It restores the display transform after weight projection.
3. Build the contact sheet:
   `python tools/groom_contact_sheet.py <out>/preview --meta <out>/preview/previews.json --out <out>`
   Read it before accepting the asset. A valid FBX is not evidence of good grooming.
4. Create the standard LOD chain, with the bare target name as `--mesh`:
   `blender-launcher -b --factory-startup -P tools/blender/add_mesh_lods.py -- --fbx <out>/<target>.fbx --mesh <target without _lod0> --levels 1,2,3,4,5 --ratios 0.7,0.3,0.15,0.07,0.03`
   Poll `<fbx>.lods-report.json` for `"stage": "done"`.
5. Append new grooms at the end of the skin list, never in the middle:
   `python tools/oneoff/append_dwarf_grooms.py --hair <name>:<StyleTag>` for dwarf hair or
   `python tools/oneoff/append_dwarf_grooms.py --beard <name>` for a dwarf beard. Review the dry
   run before `--apply`.
6. Mike imports and saves in the Kit. Check material, every LOD, face fit at several sliders,
  helmet variants, and an actual head turn in game before closing the asset work.

## Traps

- Blender 5.2 background mode needs `gpu.init()` before importing Hair Tool. The bridge handles it.
- Hair Tool conversion removes the source object and returns world-shaped vertices with the source
  transform. The bridge projects weights in the raw frame, then restores the display transform.
- `UVMap` is the visible atlas. `HairTool_UV` carries root-to-tip direction. Losing either is a hard
  failure.
- Upper meshes follow the head through engine mapping and weights, not groom morph channels.
- Keep the kitbash classifier and recipes for reusing finished cards. Use this skill for new curve
  authoring; do not replace one with the other.
