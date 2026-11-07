# TAOM Hair Toolkit

A source-only Blender grooming package for KEYforce, Kysaro, and TAOM contributors. It includes the
independent TAOM hair engine, the optional Hair Tool bridge, deterministic authoring, weight, UV,
preview, LOD, kitbash, baking, rigging, preset, and skin-list workflows. Mesh assets are separate
dependencies.

## Requirements

- Blender 5.2.3 LTS is the tested version.
- NumPy inside Blender for geometry and kitbash operations.
- Pillow in ordinary Python for `tools/groom_contact_sheet.py`.
- The exact LOD0 race head, its armature, the intended Kit material, and finished donor grooms.
- Hair Tool 4.6.1 only for upstream-specific authoring or differential checks. It is not required
  by the independent engine.

No Exoside installation is needed. No race heads, donor FBX files, generated meshes, Hair Tool
source, node assets, textures, presets, or activation data are included.

## Verify the extracted package

From the `hair-toolkit` directory after extraction:

```bat
python -m unittest discover -s tools/tests -t . -p "test_hair_engine_*.py"
python -m unittest tools.tests.test_hair_tool_pipeline tools.tests.test_package_hair_toolkit tools.tests.test_groom_kitbash tools.tests.test_append_dwarf_grooms tools.tests.test_add_mesh_lods
python -m ruff check tools/hair_engine tools/blender/hair_engine_*.py tools/tests/test_hair_engine_*.py
```

The independent API is `tools.hair_engine`. Use `tools.blender.hair_engine_pipeline` for independent
integration and `tools.blender.hair_tool_pipeline` for the legacy bridge. Their script paths are
`tools/blender/hair_engine_pipeline.py` and `tools/blender/hair_tool_pipeline.py`.

## Start an independent groom

Use an explicit staging directory outside the live Armory. Replace `blender-launcher` with your
Blender executable or the TAOM Store-app launcher.

```bat
blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_engine_pipeline.py -- --mode inspect --out <out>
blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_engine_pipeline.py -- --mode author --profile dwarf_hair --mesh <head> --out <out>
blender-launcher -b <author.blend> --python-exit-code 1 -P tools/blender/hair_engine_pipeline.py -- --mode finalize --profile dwarf_hair --mesh <guide> --armature <rig> --preview --head <head> --out <out>
```

Profiles are `dwarf_hair`, `dwarf_beard`, and `elf_hair`. The author report names the `hair_object`
to style and finalize. These are starting guides, not finished hairstyles. Groom and braid them
with the independent operators or another authoring tool before delivery.

Elven runs require the actual material and at least one donor:

```bat
blender-launcher -b <author.blend> --python-exit-code 1 -P tools/blender/hair_engine_pipeline.py -- --mode finalize --profile elf_hair --material <actual Kit material> --reference <finished groom> --mesh <guide> --armature <rig> --preview --head <head> --out <out>
```

Dwarf defaults use material `m_dwarf_beards_b` and existing dwarf donors. Override names explicitly
when your scene differs. Existing staged outputs require `--overwrite`.

Read `docs/features/independent-hair-engine.md` for baking, rigging, libraries, particles, channel
mixing, safe installation, and the complete architecture. Register
`tools/blender/hair_engine_operators.py` for the **TAOM Hair Engine** panel and modal tools.

## Use the legacy Hair Tool bridge

Install Hair Tool 4.6.1 separately and set `HT` to its package folder. Replace the script and add
`--hair-tool <HT>` in these commands:

```bat
blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_tool_pipeline.py -- --mode inspect --hair-tool <HT> --out <out>
blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_tool_pipeline.py -- --mode author --profile dwarf_hair --hair-tool <HT> --mesh <head> --out <out>
blender-launcher -b <author.blend> --python-exit-code 1 -P tools/blender/hair_tool_pipeline.py -- --mode finalize --profile dwarf_hair --hair-tool <HT> --mesh <guide> --armature <rig> --preview --head <head> --out <out>
```

The independent report is `hair-engine-report.json`; the legacy report is `hair-tool-report.json`.

## Delivery checks

Require `"status": "ok"` in the relevant report. Delivery rejects empty meshes, missing `UVMap` or
`HairTool_UV`, incomplete weights, morph channels, wrong materials, and donor projection above
500 mm. The independent engine preserves source objects and the legacy bridge restores the display
transform after projecting weights.

Create and read the contact sheet:

```bat
python tools/groom_contact_sheet.py <out>/preview --meta <out>/preview/previews.json --out <out>
```

Build all five standard LODs with the bare target name:

```bat
blender-launcher -b --factory-startup -P tools/blender/add_mesh_lods.py -- --fbx <out>/<target>.fbx --mesh <target without _lod0> --levels 1,2,3,4,5 --ratios 0.7,0.3,0.15,0.07,0.03
```

Poll `<fbx>.lods-report.json` for `"stage": "done"`. Append dwarf grooms at the end of the skin list
with `tools/oneoff/append_dwarf_grooms.py`; review its dry run before `--apply`. Other races must
follow their actual skin structure without renumbering existing face-key groom choices.

## Reuse existing dwarf cards

The package includes `tools/blender/kitbash_grooms.py`, the pure classifier in
`tools/blender/groom_kitbash.py`, the dwarf recipe in `tools/blender/specs/`, and their tests. Read
the module docstrings for inventory, build, and preview commands. Use kitbash for finished cards and
the independent engine or Hair Tool bridge for new curve-authored grooms.

## Acceptance and license

Kit import, every LOD, face-slider fitting, helmet variants, and an actual head turn in game are
required before shipping art. The package does not claim that these checks have happened for your
asset.

Original bridge and packaging software is MIT licensed in `LICENSE.txt`. The independent engine and
its Blender adapters are GPL-3.0-or-later; their license is `tools/hair_engine/LICENSE.txt`.
Workflow prose is CC BY-NC-SA 4.0. Hair Tool is an optional separate GPL dependency and is not
redistributed. See `NOTICE.txt`.

## Rebuild the ZIP

```bat
python tools/package_hair_toolkit.py --out dist/hair-toolkit.zip
```

The builder is `tools.package_hair_toolkit`. It validates every source before writing and emits the
ZIP's SHA-256 digest. Safe installation is available through
`tools.hair_engine.packaging.install_archive` with an optional expected hash and backup preservation.
