# Cube Map Quad Remesher

## Overview

`tools/quad_engine/cube_map.py` is an independent quad-remeshing engine for TAOM asset workflows.
It uses a cube-map parameterization and source-mesh raycasting rather than Exoside's field solver.
The Blender adapter is `tools/blender/cube_map_remesh.py`.

The current vertical slice handles closed, manifold, star-shaped triangle meshes and produces a
single closed quad mesh with material assignments and quality metrics. It is not an Exoside
replacement for general organic or highly concave assets.

## Required workflow selection

Every Blender quad-remeshing or retopology task in TAOM must use the quad toolkit. Do not write a
new bridge, invoke `xremesh.exe` directly, or use the official modal Exoside add-on in a background
job. Ordinary LOD reduction and animation-keyframe decimation are different operations and keep
their existing tools.

1. For a closed, manifold, star-shaped mesh where cube-directed quad flow is acceptable, use the
   independent engine through `tools/blender/cube_map_remesh.py` or
   `tools/examples/cube_map_active.py`. It requires NumPy but no Exoside installation.
2. For open, nested, strongly concave, or organic work that needs better edge flow, use the
   [licensed Exoside bridge](quad-remesher-job.md) through `tools/blender/quad_remesh_job.py` or
   `tools/examples/remesh_active.py`. Do not force such input through the independent engine. If
   the required Exoside license is unavailable, stop the retopology stage and report that blocker.
3. Use a new output object when comparing candidates. Use in-place replacement only when the
   workflow calls for it, preserve the source in staging, and inspect the generated report and
   topology metrics before UV, baking, collision, LOD, or Kit stages.
4. Use an older collapse-decimation route only for polygon reduction rather than quad retopology,
   or when the maintainer explicitly accepts it as the fallback.

The independent engine does not retain UVs. Rebuild UVs downstream, and expect FBX export to
triangulate the quad faces for Bannerlord.

## Why This Exists

TAOM needs an engine it can inspect, extend, and tune for its own asset pipeline instead of
treating a proprietary remesher as the only option. The Exoside analysis established useful
requirements, but this implementation is deliberately independent and favors a small, testable
baseline over an attempted clone.

## Architecture

```text
Blender mesh
  -> triangulation and closed-manifold validation
  -> six cube-face quad grids with shared edge vertices
  -> radial ray intersection against source triangles
  -> material assignment from projected source faces
  -> orientation, degeneracy, boundary, aspect, and projection metrics
  -> new Blender object or in-place mesh replacement
```

The six grids have matching samples on cube edges, so topology is welded by construction rather
than by a positional post-merge. Each output vertex is the first valid intersection of a radial
ray with the source mesh. Output winding is checked using signed volume and reversed when needed.

## Configuration

`remesh(vertices, faces, material_ids=None, target_quads=96)` accepts NumPy arrays. The target is
rounded up to the next complete set of six cube faces: `6 * ceil(sqrt(target / 6)) ** 2` quads.

`remesh_object(obj, target_quads=96, work_dir=None)` returns a new Blender object and a metrics
dictionary. `remesh_in_place` swaps the object's mesh and returns the same metrics. `work_dir` is
optional; when supplied, each run writes `output.npz` and `report.json` in a unique subdirectory.

NumPy is required. SciPy supplies an optional `cKDTree` accelerator; a NumPy-only fallback is
covered by tests.

## Key Files

| File | Purpose |
| --- | --- |
| `tools/quad_engine/cube_map.py` | Validation, cube topology, raycasting, projection, materials, and metrics |
| `tools/blender/cube_map_remesh.py` | Blender conversion, object creation, in-place replacement, and artifacts |
| `tools/tests/test_cube_map_remesh.py` | Fourteen unit tests for validation, topology, materials, projection, and fallback behavior |
| `docs/research/quad-remesher-engine.md` | Native Exoside research and future field-guided algorithm requirements |

## Limitations and next work

- Input must be a closed manifold and star-shaped around its bounding-box center. Open meshes and
  strongly concave assets are outside this slice.
- Cube directions are not curvature- or material-aligned. This baseline does not provide
  Exoside-quality organic edge flow.
- UVs are rebuilt downstream, not retained. Material ids come from the nearest projected source
  triangle.
- Large meshes currently use straightforward radial intersection and nearest-triangle searches.
  A proper raycast BVH and profiling are still needed.
- Next algorithmic work: open-boundary handling, curvature-aware sampling, field-aligned guidance,
  better anisotropy, and comparison against representative TAOM assets.

## Tests

- `python -m unittest tools.tests.test_cube_map_remesh`: fourteen tests covering closed/open validation,
  invalid values, target rounding, deterministic topology, materials, finite metrics, exact
  projection, and the NumPy-only fallback.
- `python -m ruff check tools/quad_engine tools/blender/cube_map_remesh.py
  tools/tests/test_cube_map_remesh.py`: all checks passed.
- Real Blender 5.2.3 LTS run: 98 vertices and 96 quads, zero boundary edges, signed volume 8.0,
  aspect ratio 1.0, no degenerate quads, and zero maximum or mean center error.

## How to verify in game

This is a Blender asset tool and is not deployed into Bannerlord.

1. Run `python -m unittest tools.tests.test_cube_map_remesh`. Expect fourteen passing tests.
2. In Blender, call `remesh_in_place(obj, 96, work_dir)` from
   `tools/blender/cube_map_remesh.py`. The returned `report.json` must show `quad_count=96`,
   `boundary_edges=0`, `degenerate_quads=0`, and a positive `signed_volume`.
3. For the unit cube, also require `signed_volume=8.0`, `max_aspect_ratio=1.0`, and
   `max_center_error=0.0`.

## Performance

SciPy's spatial index accelerates nearest-triangle lookup when installed. The NumPy fallback is
functionally equivalent but slower. Exact radial intersection is currently linear in source
triangles per ray, so production-scale meshes need an acceleration structure and timings before
making throughput claims.

## Changelog

- 2026-11-07: made the toolkit the required Blender retopology path and documented backend
  selection for repository workflows.
- 2026-11-06: first independent vertical slice with closed quad topology, material projection,
  NumPy-only fallback, metrics, Blender integration, and real-runtime verification.

## GitHub Issue

- **Issue:** not created; no public issue was requested for this prototype.
- **Status:** working vertical slice, not a general remesher.
