# Quad Toolkit for Blender

A source-only toolkit containing an independent cube-map remesher and an optional Exoside job
bridge. The independent engine needs NumPy but not Exoside. The optional bridge needs a licensed
Exoside Quad Remesher 1.4 installation.

## Requirements

- Blender with FBX support. Tested with Blender 5.2.3 LTS.
- NumPy. SciPy is optional and accelerates spatial lookup.
- For the optional bridge: engine 1.4.1.3852 at its standard Exoside location.

No Exoside binaries, activation data, licenses, mesh assets, or decompiled native code are
included.

## TAOM workflow default

TAOM Blender retopology must use this toolkit rather than a new bridge or a direct `xremesh.exe`
call. Use the independent engine for eligible closed, manifold, star-shaped meshes. Use the
licensed Exoside bridge for open, nested, concave, or organic retopology. The canonical repository
rule is in `docs/features/cube-map-remesh.md` under "Required workflow selection".

## Independent engine

Open a scene with the active mesh, then run:

```bat
blender -b scene.blend --python-exit-code 1 --python tools/examples/cube_map_active.py -- --quads 96 --output "D:\retopo.fbx"
```

The input must be closed, manifold, and star-shaped around its bounding-box center. Nested shells,
blind holes, and open meshes are rejected rather than silently remeshed. Cube directions are not
curvature-aligned, so this is a baseline rather than an organic retopology replacement.

## Optional Exoside bridge

```bat
blender -b scene.blend --python-exit-code 1 --python tools/examples/remesh_active.py -- --quads 8000 --work-dir "D:\quad-remesher-jobs"
```

Options include `--adaptive-size`, `--exact`, `--symmetry`, `--max-threads`, `--new-object`, and
`--timeout`. Do not run the official Exoside add-on concurrently with this bridge.

## Tests

From the repository root:

```bat
python -m unittest tools.tests.test_cube_map_remesh tools.tests.test_quad_remesh_job
```

Expected: **32 tests passed**.

The independent engine has 14 tests for validation, topology, exact projection, materials, scale,
and fallback behavior. The Exoside bridge has 18 tests for settings, progress, wait-loop branches,
locks, artifacts, and process cleanup.

## License

The complete toolkit is distributed under GNU GPL-2.0-or-later. See LICENSE.txt and NOTICE.txt.
The proprietary Exoside engine is a separate local dependency and is not redistributed.
