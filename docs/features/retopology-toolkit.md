# Independent Garment Retopology Toolkit

## Overview

TAOM's retopology toolkit combines an independent UV-guided garment engine with Petik's interactive
patch and line tools. It gives KEYforce and TAOM artists editable quad workflows without shipping
RetopoPlanes' proprietary `.blend`, Geometry Nodes assets, demo meshes, or textures.

## Why This Exists

TAOM needs inspectable garment retopology that preserves UVs and source detail, handles artist-created
quad guides, and fits the existing weight, LOD, staged-FBX, and Kit review gates. The reference product
provides useful behavior but its node assets are not redistributable under the terms established for
this work. Petik is separate: its manifest explicitly declares GPL-3.0-or-later, so its source can be
redistributed with the corresponding license and notice.

## Architecture

```text
source garment with UVs
  -> UV-space triangles and original 3D positions
  -> flat patterns with pattern, part, border, and guide metadata

editable quad guides
  -> validation and optional initialization, mirroring, auto fill, density, and snapping
  -> bounded quad subdivision
  -> barycentric reprojection to the source surface
  -> UV fitting, seam handling, pattern/part separation, optional thickness
  -> ordinary Blender mesh, UV layer, materials, and diagnostic helpers
```

The pure Python engine is independent of Blender. `tools/blender/retopo_adapter.py` converts Blender
meshes and attributes, while `tools/blender/retopo_operators.py` provides the TAOM sidebar and
operators. Outputs are real meshes, not evaluated proprietary node graphs.

Meshes carry explicit `attribute_domains` through copying, repair, auto fill, mirroring, separation,
seam merging, thickness, and Blender conversion. Undeclared attributes use face-length matching
before vertex-length matching, except known point attributes and `pattern_id` paired with
`source_position`. Subdivision keeps original vertex values, uses the lower-index edge endpoint and
first face corner for generated source values, and uses the nearest parent for edge splits. New
`point_guide`, `open_seam`, and `simplify_on` values are false, so generated vertices do not become
manual guides. Domains resolve once at operation entry and inferred values stay explicit through
reconstruction. Unsupported attributes are discarded, and export accepts only point or face arrays
whose lengths match their elements. Mixed-domain face combinations default to zero. Import reads
source and guide attributes only on the point domain; pattern, part, and plane identifiers may use
point or face domains. Wrong-length and unsupported attributes and their domain declarations are
removed at operation entry, and loose-line endpoints follow the same vertex remap as faces.
Debug materials use face-domain identifiers only.

The behavioral input and acceptance matrix are in
[retopo-planes-1-6-3-clean-room.md](../research/retopo-planes-1-6-3-clean-room.md). The independent
implementation does not claim identical internal algorithms or byte-identical meshes.

## Workflow selection

This is a garment and manual-guide workflow, not a replacement for every remesher:

- Use this toolkit for UV-mapped garments, artist-authored quad guides, patch layouts, and source
  detail transfer.
- Use Petik for interactive variable-sided patches and selected-loop control curves.
- Keep using the [required quad toolkit selection](cube-map-remesh.md#required-workflow-selection)
  for automatic quad remeshing: cube-map for eligible closed star-shaped meshes, licensed Exoside
  for open, nested, concave, or organic automatic retopology.
- Do not route an unsupported mesh through the cube-map engine merely because this toolkit is also
  installed.

## Artist workflow

1. Apply source rotation and scale and ensure its UV islands are separate and non-overlapping.
2. In **TAOM Retopology**, assign the original garment as **Source garment** and a collection of
   editable guide meshes as **Retopo Planes**.
3. Use **3D to Patterns** to create a flat reference. Keep the original garment unchanged.
4. Create guides with ordinary Blender editing, **Initialize Planes**, mirroring, and auto fill, or
   use Petik's multi-patch and line operators. Guide faces must be quads.
5. Set density, snapping, smoothing, subdivision, separation, thickness, and preview options.
6. Run **Generate / Update Retopology**. Inspect the output, UVs, and diagnostic helpers.
7. Export to a staging directory outside the live Armory. Complete TAOM's UV, weight, LOD, staged
   FBX, and Kit checks before importing art.

`Output Generated Planes` returns editable flat geometry. `Only Patterns` and `Only Seams` expose
reference geometry without replacing the final workflow. Applying an output requires no script
permission because it is already an ordinary mesh.

## Configuration

The panel exposes the full contract from the behavioral specification:

- **Planes Assist:** initialization angle and corner thresholds, point-guide prepass, connected auto
  fill, bounded iterations, and generated-plane output.
- **Density:** base density, automatic connected propagation, both crease modes, sharp-edge additions,
  density threshold, border-only snapping, independent seam merge tolerance, and low-poly smoothing.
- **Reprojection:** subdivision level, optional UV smoothing, first-tile fitting, pattern separation,
  part separation, and 2D/3D morph preview.
- **Thickness:** enable, thickness, and signed offset.
- **Preview:** point guides, contact/mismatch guides, wireframe, seams, polyloops, edge counts,
  crease labels, snapping labels, source preview and offset, four debug material modes, deterministic
  color seed, hue, GUI scale, and information visibility.

Invalid numbers, missing sources or UV maps, and non-quad guides fail with actionable errors. Source
and guide objects are preserved when generating a new output.

## Key Files

| File | Purpose |
| --- | --- |
| `tools/retopo_engine/` | Independent mesh model and UV-guided geometry algorithms |
| `tools/blender/retopo_adapter.py` | Blender mesh, UV, and attribute conversion |
| `tools/blender/retopo_operators.py` | Add-on operators, diagnostics, materials, helpers, and panel |
| `tools/blender/retopo_selftest.py` | Real Blender end-to-end verification |
| `tools/blender/retopo_installable_selftest.py` | Reproducible nested-archive installation and clean-disable verification |
| `tools/blender/retopo_reference_differential.py` | Original-product differential case capture |
| `tools/blender/petik_selftest.py` | Petik topology, projection, and modal verification |
| `tools/petik/Petik/` | Petik 2.0.7 with six recorded compatibility fixes, GPL license, and notice |
| `tools/package_retopology_toolkit.py` | Deterministic KEYforce source and installable-archive builder |
| `docs/research/retopo-planes-1-6-3-clean-room.md` | Behavior specification and acceptance matrix |

## Tests

- `python -m unittest tools.tests.test_retopo_engine`: 39 tests covering flattening, attributes,
  initialization, holes, mirroring, local and connected fill, density, snapping, reprojection,
  separation, positive and negative thickness, numeric limits, loop iteration, morph preview,
  non-manifold inspection, and split-edge repair.
- `python -m unittest tools.tests.test_retopo_differential`: original-product comparisons for base
  geometry, subdivision, UVs, initialization, X/Y mirroring, auto-fill, density modes, separation,
  thickness, and inspector behavior. Exact cases compare complete vertex positions, oriented
  connectivity, and geometry-UV associations. Validity exceptions and reference density counts are explicit.
  Dedicated UV-bearing contract cases cover inspector face and metadata associations.
- `python -m unittest tools.tests.test_retopo_contract_regressions`: fifty-nine regression tests for topology,
  source attributes, validity, density order, seam merging, bevel propagation, inspector face fans,
  self-mirroring, empty inputs, report naming, and preview density/contact guides.
- `python -m unittest tools.tests.test_petik_compat`: three tests for explicit reload imports,
  instance-context patch refresh, and the registered menu operator.
- `python -m unittest tools.tests.test_package_retopology_toolkit`: source manifest, reproducibility,
  nested installable archives, license, and forbidden-payload checks.
- `python -m ruff check tools/retopo_engine tools/blender/retopo_adapter.py
  tools/blender/retopo_operators.py tools/tests/test_retopo_engine.py`: static checks.
- Real Blender 5.2.3 LTS: `tools/blender/retopo_selftest.py` verifies registration, flattening,
  named UV selection with two layers, reprojection, a real UV layer, initialized quads, source/guide preservation, non-quad rejection,
  and inspector debug/repair. `tools/blender/petik_selftest.py` verifies 3 to 6-sided patches,
  alternative solutions, control-line editing, selected loops, modal controls, reset/undo, preference
  swaps, visibility, ray casting, snapping, and view normals. Both reports require `"status": "ok"`.
  The TAOM test also requires a named `TAOM Morph Preview`, separate mismatch, polyloop and sibling
  helpers, computed density labels, and an unchanged final reprojection. Petik also verifies add-on
  reload, both registered hotkeys, help drawing, right-click handling, and mouse-wheel editing.
- The freshly installed KEYforce archives pass the material, preview, thickness, generated-plane,
  pattern, seam, mirror, auto-fill, crease, validation, and clean-disable matrix. The packaged
  headless example produces a staged `.blend` and writes success only after the save completes.
- The complete public contract and evidence requirements are in
  [retopo-planes-complete-capability-matrix.md](../research/retopo-planes-complete-capability-matrix.md).

## How to verify in game

This is a Blender authoring tool and is not deployed into Bannerlord. Runtime verification happens
in Blender and then at the normal staged-art and Kit gates. A generated mesh is not evidence of an
in-game result until the artist imports it and checks the intended LODs, weights, materials, and
helmet or animation variants.

## Performance

The pure Python reference implementation uses direct UV-triangle lookup, which is straightforward and
deterministic but grows linearly with source triangle count per output vertex. Dense production meshes
need profiling and an accelerated Blender BVH path before throughput claims. Auto fill is intentionally
bounded. Generated helpers are rebuilt only when the operator runs.

## Limitations

The engine reproduces the documented public workflow, not RetopoPlanes' internal solver. Assisted
planes and cuts can require manual adjustment on intricate patterns. Very dense sources, difficult
mirrored layouts, and overlapping or malformed UVs remain artist-review cases. No claim of exact
mesh-for-mesh parity is made without a corresponding differential fixture.

## Changelog

- 2026-11-09: added the behavioral specification, independent engine, Blender adapter and panel,
  GPL Petik package with recorded compatibility fixes, deterministic KEYforce packaging, and
  real-runtime verification.

## GitHub Issue

- **Issue:** waived by the maintainer for this work on 2026-11-09.
- **Status:** implementation and verification evidence recorded here.
