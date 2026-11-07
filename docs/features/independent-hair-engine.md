# Independent TAOM Hair Engine

## Overview

`tools/hair_engine/` is TAOM's independent hair generation, grooming, conversion, baking, rigging,
and preset engine. `tools/blender/hair_engine_pipeline.py` connects it to the same delivery gates as
the older Hair Tool bridge. New TAOM grooms should use this engine by default. Hair Tool 4.6.1 is
optional for upstream-specific authoring and differential comparisons, not a runtime dependency.

This is a behavioral implementation informed by the Hair Tool source audit. It is not clean-room,
a verbatim copy, or a claim of bit-for-bit equivalence. The derived engine and Blender adapters are
GPL-3.0-or-later; see `tools/hair_engine/LICENSE.txt` and the
[provenance register](../reference/provenance-register.md). The older interop bridge retains its
existing license.

## Architecture

```text
NumPy strand and mesh data
  -> deterministic generation and pure grooming transforms
  -> flat, round, curl, braid, or custom mesh profiles
  -> source-preserving mesh conversion with per-corner UVs
  -> optional short hair, shells, particles, baking, or armature rig
  -> Blender adapters and transactional operators
  -> TAOM weights, materials, morph, preview, LOD, and FBX gates
```

The geometry kernel does not import `bpy`. Blender code is split into object adapters, baking,
rigging, libraries, particles, modal tools, operators, and the pipeline entry point. Conversion
preserves source objects. A transaction removes newly created objects and restores selection and
mode on failure. No global registration exception is silently ignored.

`UVMap` addresses the visible atlas. `HairTool_UV` stores cross-profile position and root-to-tip
factor. UVs are stored per face corner, so welding preserves UV islands.

## Subsystems

| Area | Implementation |
| --- | --- |
| Curves | Catmull-Rom and centripetal interpolation, arc-length resampling, RDP and Visvalingam-Whyatt simplification, guide ordering, parallel-transport frames |
| Generation | Seeded area-weighted surface roots, downward guides, guide interpolation, UV grids, boundary guides |
| Grooming | Length, trim, taper, twist, smoothing, curls, seeded noise, clumping, mirroring, selection, surface attachment, control deformation |
| Profiles | Flat, round, curl, braid, multi-profile IDs, custom mesh profiles, UV regions |
| Conversion | Independent mesh data, joining, welding, material remapping, point metadata, pin weights |
| Short hair | KEEP, CELLS_TRIS, DIAMOND, DIAMOND_TRIS, smoothing, short cards, `SampledUV` |
| Shell hair | Layered shells with independent mesh ownership and layer attributes |
| Particles | Legacy particle-to-curve and curve-to-particle conversion with normalized point counts |
| Baking | AO, diffuse, opacity, normal, root, ID, random, clump, flow, directional, depth, and color |
| Channel mixing | RGB and alpha operations, padding, deterministic graph execution, cycle and missing-input rejection |
| Rigging | Ribbon bones, normalized weights, root sampling, shape transfer, spring constraints, collision spheres |
| Libraries | JSON presets, `.blend` append, duplicate names, bind, snap, thumbnails |
| Migration | Integer schema tuples, explicit migration, strict JSON, atomic writes |
| Packaging | Hash verification, traversal and symlink rejection, staged installs, backup preservation |
| Interactive tools | Registered generation, grooming, UV, rigging, bake, library, drawing, sculpt, and control-deformation operators |

The implementation favors explicit, testable behavior over reproducing every upstream modal panel or
node-layout detail. Short-hair cell construction, custom bake semantics, and single-strand shape
transfer are independent implementations, not exact replicas of Hair Tool internals.

## Author and finalize

Use the independent entry point without `--hair-tool`:

```bat
blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_engine_pipeline.py -- --mode inspect --out <out>
blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/blender/hair_engine_pipeline.py -- --mode author --profile dwarf_hair --mesh <head> --out <out>
blender-launcher -b <author.blend> --python-exit-code 1 -P tools/blender/hair_engine_pipeline.py -- --mode finalize --profile dwarf_hair --mesh <guide> --armature <rig> --preview --head <head> --out <out>
```

Profiles and donors follow [Hair Tool workflow](hair-tool-workflow.md). Elven runs require the actual
material and at least one finished donor. Require `"status": "ok"` in `hair-engine-report.json`.
The finalizer rejects missing UVs, incomplete weights, morph channels, wrong materials, and excessive
projection distances. Build LODs and the contact sheet with the standard commands in that document.

For interactive work, register `tools/blender/hair_engine_operators.py`. Its panel exposes
**TAOM Hair Engine**. Modal drawing and sculpting are registered by the same call.

## Bake

Use the `Bake TAOM Hair Maps` operator or call `bake_passes(obj, out, resolution=...)` from
`tools/blender/hair_engine_bake.py`. Cycles AO, normal, and diffuse passes use Blender's native bake
operators. Attribute-driven maps use a temporary vertex-color emission material. Original materials
are restored and the temporary attribute is removed. Output directories must be explicit.

Channel mixing is available without Blender through `tools.hair_engine.baking.TextureGraph`.

## Libraries and safe installation

`PresetLibrary` stores versioned JSON hair presets with duplicate-name remapping. Blender library
helpers append `.blend` objects, bind and snap presets, and render thumbnails. Strict JSON and atomic
writes reject malformed presets before touching existing files.

`tools.hair_engine.packaging.install_archive` accepts an explicit local ZIP and optional expected
SHA-256. It rejects absolute paths, traversal, drive paths, and symlinks; reads every member before
installation; stages output beside the destination; and preserves the previous install as a backup.
It does not download updates or reproduce Hair Tool's unsafe updater.

## Tests

- `python -m unittest discover -s tools/tests -t . -p "test_hair_engine_*.py"`: 84 tests for curves,
  generation, profiles, conversion, alternate generators, baking, rigging, services, deformation,
  and safe packaging.
- `python -m unittest tools.tests.test_package_hair_toolkit`: recipient manifest and reproducibility.
- Extracted recipient ZIP: 59 files and 302 passing tests, including all engine and legacy bridge tests.
- Differential Blender probe: Hair Tool produced 1,327 vertices and 785 polygons; the independent
  engine produced 64 vertices and 24 polygons on the same scalp, with both required UV layers. This
  validates behavior and data contracts, not geometric parity.
- `python -m ruff check tools/hair_engine tools/blender/hair_engine_*.py
  tools/tests/test_hair_engine_*.py`: bug-class lint.
- Blender 5.2.3 independent round-trip: four curves, 72 vertices, 50 polygons, both UV layers,
  rollback on failure, and `hair_tool_loaded: false`.
- Operator probe: generation, grooming, UV assignment, conversion, presets, short hair, shell hair,
  and all 12 bake outputs passed.
- Service probe: armature deformation, sculpt delta, thumbnail, library bind and snap, and four
  legacy particle strands passed.
- Independent dwarf-profile delivery: 384 vertices and 160 polygons, all weighted, both UV layers,
  `m_dwarf_beards_b`, no morph channels, and a successful FBX re-import.
- LOD1 through LOD5: 224, 96, 48, 22, and 10 triangles, with zero unweighted vertices and no diffs.

## How to verify in game

The engine is an authoring tool, not a deployed game system. Runtime evidence ends at Blender,
staged FBX, and LOD validation. Kit import and in-game acceptance remain asset-specific.

1. Import the staged FBX in the Modding Kit and verify every LOD and material.
2. Equip the groom on its intended race. Check several face-slider values for root attachment.
3. Turn the head and verify armature deformation rather than world-space hair.
4. Check helmet and armour cover variants for clipping and visibility.
5. Record the in-game review before accepting the asset. A valid Blender or FBX result is not proof
   of aesthetic quality or correct engine behavior.

## Limitations

- The engine is behaviorally independent, not bit-for-bit identical to Hair Tool. Node layouts,
  exact distribution patterns, some bake semantics, and advanced upstream editing behavior differ.
- Shape transfer currently requires one source and one target strand with matching topology.
- Interactive operators passed registration and geometry probes, but have not received a human GUI
  review. Background mode cannot prove viewport ergonomics.
- `empty.afphoto` and `draw_xray.draw_quads.dat` remain unknown upstream support files and are not
  used by this engine.
- No update is downloaded automatically. Installation requires an explicit trusted local package.

## Changelog

- 2026-11-07: added the independent geometry kernel, all hair subsystem routes, Blender adapters,
  baking, rigging, presets, safe installation, recipient packaging, and end-to-end delivery.

## GitHub Issue

- **Issue:** waived by Mike for the independent engine implementation on 2026-11-07.
- **Status:** functional implementation with Blender and FBX verification; Kit and in-game checks
  remain required for each delivered asset.
