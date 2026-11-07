# Quad Remesher Engine Reverse Engineering

Status: active native-code research, not a completed reimplementation.

## Scope and binary identity

This document records analysis of the installed Exoside engine, not the Blender bridge alone.
Licensing and activation routines are explicitly out of scope.

- Engine version: `1.4.1.3852`.
- Module: `xremeshlib.dll`, 12,267,520 bytes, x86-64 PE.
- SHA-256: `0a77802b7b8299b12818efe88260d44c7ee816ddc2575593c30e802c5a8d3ff5`.
- Native analysis: Ghidra 12.1.4, project `TestResults/quadremesher-ghidra`.
- Decompiled functions and extracted strings: `.tmp/quadremesher-re`.

The native remesher uses the `MX` framework, with `MXAutoRetopoAlgo`, `MXAutoRetopoCurv`,
`MXMeshCurvatureAlgo`, and dedicated smooth-vector-field and UV solver drivers. FBX SDK and Qt
are support dependencies, not the remeshing algorithm.

## Confirmed top-level pipeline

The following calls form the main path. Addresses are RVAs for the binary identified above.

```text
QRLib_doQuadRemeshing                         0x60D3E0
  FUN_180630eb0                              0x630EB0
    FUN_1806191c0                            0x6191C0
      FUN_180621710                          0x621710  input and maps
      FUN_180711f40                          0x711F40  retopo execution
        FUN_1806e1d80                        0x6E1D80  topology preparation
        FUN_18070e790                        0x70E790  component orchestration
          FUN_18070d2b0                      0x70D2B0  component construction
          FUN_18070d710                      0x70D710  one connected component
            FUN_18070a840                    0x70A840  constraint preparation
            FUN_18070cb00                    0x70CB00  CalcVF
            FUN_18070be90                    0x70BE90  SmoothVF
            FUN_1807e1f60                    0x7E1F60  scale and alternate UV path
            FUN_1807e1b00                    0x7E1B00  main UV path
            FUN_1807c66a0                    0x7C66A0  mesh extraction
      FUN_1806200b0                          0x6200B0  result export
```

Components are remeshed independently and then merged. Target counts are distributed by
surface area. For multiple components the recovered calculation is approximately:

```text
component_target = max(10, round(total_target * component_area / total_area))
```

An explicit target edge length takes precedence. Otherwise the scale setup uses
`sqrt(surface_area / target_quad_count)` as the base edge length. Component-local scale values
are stored at offsets `0x6C8`, `0x6CC`, and `0x6D0`.

## Input and topology preparation

`0x621710` loads FBX or ExoMesh input, resolves mesh maps, applies target-count options, and
prepares normals, material ids, polygon groups, hard edges, vertex colors, welding, and
symmetry-related data. It can also derive a target count from an input-polygon percentage.

`0x6e1D80` performs the deeper topology preparation: triangulation, topology and UV caches,
guide-curve processing, hard-edge handling, symmetry processing, and construction of working
mesh maps. `0x70A840` then marks constrained vertices from mesh maps and symmetry planes before
the densification step recorded in debug strings as `0c-AfterDensifyAroundIncompat`.

Important map names found in the binary include:

- `FB_vtxFlags`.
- `meshWithUVsVerticesInd`.
- `ExtCrv_origVtxInd`.
- `pgborders_ec`.
- `AUTO-DETECTED-HARD-EDGE-CURV`.
- `replacedVar_source`, `replacedVar_Flags`.
- `integerVarU_Flags`, `integerVarV_Flags`, `integerVar_Flags`.

## CalcVF: initial vector field

`0x70CB00` initializes the vector-field stage. Its important numerical callees are:

| RVA | Confirmed role |
| --- | --- |
| `0x79EFE0` | Builds hard-edge and topology bitsets used by the field solver. |
| `0x79E5D0` | Computes per-face direction metrics, edge angles, and curvature-dependent weights. |
| `0x816170` | Selects guide directions and assigns constraint weights using geometry and edge lengths. |
| `0x815AC0` | Projects guide directions into tangent planes and initializes constrained directions. |
| `0x7A2AB0` | Allocates per-face solver state and work arrays. |
| `0x7A2C60` | Runs and finalizes the vector-field solve. |

The per-face metric is stored as six doubles. Its layout is a symmetric 3 by 3 outer-product
form:

```text
[ x*x  x*y  x*z  y*y  y*z  z*z ] * weight
```

The exact curvature-to-weight formula is not yet isolated and should not be inferred from the
shape alone. The same routine computes signed edge angles with `acos` of normalized dot products.
Guide projection has the expected tangent-plane form:

```text
direction = guide - normal * dot(guide, normal)
```

Short edges and high-curvature features influence which directions become constraints and how
strongly they are weighted.

## SmoothVF: field optimization

`0x70BE90` optimizes the initial field. Relevant routines are:

| RVA | Confirmed role |
| --- | --- |
| `0x7A9E50` | Builds local edge and face frames using normalized cross products. |
| `0x7A9720` | Computes constraint directions and angles from projected geometry. |
| `0x7B0010` | Builds and solves the sparse field-optimization system. |
| `0x7A65C0` | Post-processes solver output and working maps. |

The solver has separate `__SmoothVFSolverDriver` and `__SpeedUpSystemSolverDriver` types. The
large system-building routine handles constraints, work arrays, and iterative solver state. Its
exact energy terms, stopping criteria, and singularity-index treatment remain to be documented.

## UV and integer-grid computation

The engine has both a main UV path and an alternate path. `0x7E1F60` establishes scale before
calling `0x71D370`. `0x7E1B00` enters the main path.

| RVA | Confirmed role |
| --- | --- |
| `0x7DAAC0` | Initializes the UV system, constraints, and solver state. |
| `0x7DC7B0` | Builds the sparse parametrization system and its constraints. |
| `0x7DA8A0` | Executes the solver and reports status. |
| `0x7DA4E0` | Propagates computed values and orientation bits across selected mesh edges. |
| `0x7CCB60` | Releases UV solver storage. |
| `0x7188A0` | Alternate-path processing. |
| `0x716B70` | Additional symmetry-related alternate processing. |
| `0x715D70` | Additional alternate-path processing. |

Debug strings explicitly identify integer variables for `U`, `V`, and `U+V`, replacement of
one variable by another, grid poles, frozen borders, and genus reduction. This confirms a
mixed-integer parametrization rather than ordinary per-triangle UV unwrapping.

`__CalcUVsMainSystemSolverDriver` is the dedicated main solver type. The exact objective,
rounding schedule, treatment of singularities, and border constraints still require a focused
read of `0x7DC7B0` and `0x7B0010`.

## Quad extraction and repair

`0x7C66A0` drives mesh extraction. `0x7BEEE0` is its main implementation, with `0x7BDE70`,
`0x7BD510`, `0x7BB8F0`, `0x7B87E0`, `0x7B4BD0`, and `0x7B3D30` providing extraction and repair
paths.

The integer UV representation and this downstream stage strongly support iso-parametric quad
extraction. Exact triangle traversal, stitching, singularity patching, and non-quad handling are
not yet fully recovered. Those details are required before claiming algorithmic equivalence.

`0x6200B0` exports the result as FBX or ExoMesh and handles output naming and map export.

## Algorithm family

The native structure is consistent with field-guided quadrangulation: compute a cross field,
obtain a global parametrization with integer constraints, and extract quads, with special
handling for singularities and borders.

A useful conceptual reference is
[Quadrangulation through Morse-Parameterization Hybridization](https://www.geometry.caltech.edu/pubs/FBTDH18.pdf).
It describes a periodic vector field, parameterization, and special singularity-region handling.
This is a conceptual comparison only. It is not evidence that Exoside uses that paper's exact
algorithm or source code.

## Independent implementation plan

A working cube-map baseline now exists at [cube-map-remesh.md](../features/cube-map-remesh.md).
It is deliberately simpler than the field-guided pipeline below and is limited to closed,
manifold, star-shaped inputs.

1. Build a half-edge or equivalent mesh topology layer with triangles, borders, creases,
   materials, guide curves, symmetry, and per-vertex density.
2. Compute local frames and an initial constrained cross field using the recovered projection
   and per-face metric structure.
3. Optimize the field with a documented smoothness energy and explicit singularity tracking.
4. Solve a global UV parametrization with integer `U`, `V`, seam, and border constraints.
5. Extract quads along integer isocurves and implement singularity patches.
6. Project output vertices to the source surface, then transfer requested material and mesh-map
   attributes.
7. Add differential fixtures for a cube, a cylinder, a sphere, an open grid, a creased hard
   surface, and a mesh with multiple connected components. Compare counts, manifoldness,
   boundary preservation, orientation, and geometric error, not only visual appearance.

## Remaining research

Highest-priority unresolved items:

- Exact smooth-field energy and iteration schedule in `0x7B0010`.
- Exact UV objective, rounding, and replacement rules in `0x7DC7B0`.
- Singularity indexing and local repair in the extraction routines.
- Surface projection and final smoothing after extraction.
- Exact curvature-adaptivity and vertex-density formulas.
- Structure layouts and meaningful names for the current `FUN_180...` placeholders.

Future sessions should continue from the Ghidra project and the named decompilation artifacts
rather than restarting with the executable launcher or the Blender bridge.
