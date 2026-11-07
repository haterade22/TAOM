# Exoside Quad Remesher Job Bridge

## Overview

`tools/blender/quad_remesh_job.py` is a headless Blender driver for an existing, licensed Exoside
Quad Remesher 1.4 installation. It preserves the proprietary engine as the compute backend while
making its fixed-file protocol safe and useful in production workflows.

This is separate from the independent cube-map engine. It does not replace or bypass Exoside
licensing and does not provide a fallback remesher.

## Required workflow selection

This bridge is the required Exoside path for Blender retopology outside the independent engine's
limits. Follow the [remesher selection](cube-map-remesh.md#required-workflow-selection) before
choosing a backend. Never invoke `xremesh.exe` directly or use the official modal add-on in a
background job. A missing Exoside license blocks this route; it does not authorize an unsupported
independent-engine run.

## Why This Exists

The official Blender bridge uses a modal operator driven by a window timer, which does not finish
in background Blender jobs. Engine 1.4.1.3852 also rejects renamed or relocated settings, input,
and output files, so simply passing an arbitrary staging directory fails before remeshing.

## Architecture

```text
Blender mesh
  -> unique artifact directory
  -> cross-process bridge lock
  -> preserve previous official bridge files
  -> official fixed-path settings, input, output, and progress files
  -> xremesh.exe -s RetopoSettings.txt
  -> progress validation and process cleanup
  -> FBX import, materials and shading
  -> restore previous official bridge files
```

The official job is under `%TEMP%\Exoside\QuadRemesher\Blender`. Successful runs retain settings,
progress, and output FBX in a unique artifact directory. Failed runs also retain the input FBX
for reproduction. Existing official bridge files are restored after either outcome.

The official Exoside add-on does not participate in the new lock, so it must not run concurrently
with this driver.

## Configuration

`quad_remesh(obj, target_quads, work_dir, timeout_s=900, **options)` returns a new object.
`remesh_in_place` replaces the source object's mesh and returns it.

Options include `adaptive_size`, `exact_count`, `use_materials`, `use_normals`, `hard_edges`,
`symmetry`, and `max_threads`. `work_dir` is an artifact root, not the engine job directory.

A licensed engine is required at
`%ALLUSERSPROFILE%\Exoside\QuadRemesher\Datas_Blender\QuadRemesherEngine_1.4\xremesh.exe`.

## Key Files

| File | Purpose |
| --- | --- |
| `tools/blender/quad_remesh_job.py` | Locking, settings, process management, artifact preservation, and Blender integration |
| `tools/tests/test_quad_remesh_job.py` | Eighteen tests for settings, progress, locks, artifacts, and process cleanup |
| `tools/examples/remesh_active.py` | Command-line example for the active Blender mesh |
| `tools/quad_toolkit/LICENSE.txt` | GPL-2.0-or-later license for the distributed toolkit |
| `tools/quad_toolkit/NOTICE.txt` | Source and provenance notices |

## Tests

- `python -m unittest tools.tests.test_quad_remesh_job`: eighteen passing tests.
- Real Blender 5.2.3 LTS to Exoside to Blender round trip: 98 vertices and 96 faces.
- All four pre-existing official bridge files matched their original SHA-256 hashes afterward.

## How to verify in game

This is a Blender asset tool and is not deployed into Bannerlord.

1. Run `python -m unittest tools.tests.test_quad_remesh_job`. Expect eighteen passing tests.
2. Run `tools/examples/remesh_active.py` in Blender with one active mesh and a licensed engine.
3. Expect `remesh-example-report.json` to show `"status": "ok"` and a nonzero face count.
4. Confirm the previous official bridge files retain their pre-run hashes.

## Limitations

The Exoside algorithm remains proprietary and unchanged. No speed or quality improvement is
claimed for the remeshing kernel. The improvement is deterministic orchestration, failure
behavior, artifact handling, and safe integration into background workflows.

## Changelog

- 2026-11-07: documented required backend selection and prohibited direct engine calls in
  repository Blender workflows.
- 2026-11-06: added fixed-path locking, preservation, artifacts, process cleanup, Blender
  integration, tests, and real-runtime verification.

## GitHub Issue

- **Issue:** not created during the prototype.
- **Status:** working bridge with the documented concurrency and licensing requirements.
