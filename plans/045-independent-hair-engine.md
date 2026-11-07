# Independent TAOM Hair Engine

Status: functional implementation completed and verified in Blender 5.2.3. The implementation is
behaviorally independent, not bit-for-bit equivalent. Exact node layouts, some bake semantics,
advanced upstream editing behavior, and geometric distribution differ.

## Outcome

Build a TAOM-owned hair engine with functional coverage of every Hair Tool 4.6.1
subsystem needed for authoring and delivery. The implementation is a behavioral
port informed by the GPL source audit, with TAOM data structures, decomposition,
error handling, and tests. It is not a clean-room implementation and does not
vendor Hair Tool source or Blender assets.

The engine must run its geometry kernels without Blender. Blender adapters own
object conversion, dependency-graph evaluation, rendering, armatures, libraries,
and interactive operators. Existing TAOM weight, material, morph, LOD, preview,
and Kit gates remain mandatory.

Issue: waived by Mike on 2026-11-07 for this implementation.

## Acceptance matrix

| Subsystem | Required functional coverage | Completion evidence |
| --- | --- | --- |
| Curve data | Variable-length strands, radius, tilt, tags, groups, point and curve attributes, deterministic validation | Unit fixtures and Blender round-trip |
| Curve math | Catmull-Rom, centripetal interpolation, arc-length resampling, RDP and VW simplification, guide sorting, parallel-transport frames | Known-answer and differential fixtures |
| Generation | Surface distribution, interpolated guides, UV grids, mesh borders, grid guides, seeded variation | Empty, masked, degenerate, and normal fixtures |
| Grooming | Clump, curls, noise, length, trim, radius, tilt, roll, twist, smooth, simplify, mirror, surface attachment, selection masks | Pure geometry tests and interactive cancellation tests |
| Profiles and UVs | Flat, round, curl, braid, multi-curve, mesh profiles, `Profile_ID`, UV-region assignment and flipping | Topology, UV, and multi-profile fixtures |
| Conversion | Mesh cards, attribute transfer, welding, instancing, restoration, proxies, pin weights, no source destruction | Blender re-import and exception-restoration tests |
| Short hair | KEEP, triangulated cells, diamond, triangulated diamond, smoothing, projection, edge UVs | Four topology fixtures and Blender conversion |
| Shell hair | Layered shells, guide deformation, explicit mesh ownership, tiling texture bake | Layer, material, ownership, and bake fixtures |
| Particle hair | Particle-to-curve, curve-to-particle, uniform length scaling | Blender interoperability fixtures |
| Baking | AO, diffuse, opacity, normal, root, ID, random, clump, flow, directional, depth, color, padding and preview | Every pass, color space, and padding mode |
| Channel mixing | Input, output, padding, mix, alpha, RGB separation and combination, dependency-ordered execution | CPU known-answer and graph-order tests |
| Rigging | Ribbon bones, weights, root sampling, pin gradients, shape transfer, spring and collision simulation | Deformation and deterministic simulation fixtures |
| Libraries | Append, duplicate remapping, bind, snap, thumbnail generation and preset storage | Blender round-trip and duplicate-name fixtures |
| Presets | Deformer, system, and complete-stack JSON with schema versions and validation | Round-trip, malformed-input, and migration tests |
| Migration | Explicit tuple versions, non-destructive backups, node and setting upgrades | Old-version fixtures and rollback tests |
| Interactive tools | Drawing, sculpting, selection, proportional transforms, UV editing, grid deformation | Real Blender operator and cancellation probes |
| Export and packaging | TAOM delivery, source-only recipient ZIP, safe archive validation and atomic installation | Recipient tests, traversal and failure-recovery tests |

The unsupported `empty.afphoto` and `draw_xray.draw_quads.dat` files are not
runtime dependencies. They remain documented unknowns and will not receive
invented decoders. The upstream updater will not be ported. Safe installation
requires explicit local packages or verified hashes, path containment checks,
and preservation of the previous installation on every failure.

## Architecture

```text
tools/hair_engine/
  model.py          strand, mesh, profile, attribute, and report data
  curves.py         interpolation, resampling, simplification, frames
  generation.py     surfaces, guides, distribution, seeded variation
  grooming.py       pure deformers and selection masks
  profiles.py       profile evaluation and UV contracts
  convert.py        mesh cards, welding, instances, restoration data
  short_hair.py     short-hair topology and generation
  shell_hair.py     layered shells and ownership
  particle.py       legacy particle interchange helpers
  baking.py         passes, padding, and channel mixing
  rigging.py        bones, weights, gradients, springs, collisions
  presets.py        validated JSON and tuple-version migrations
  library.py        preset manifests and duplicate-safe naming
  packaging.py      safe local archives and atomic installation

tools/blender/hair_engine_adapter.py
  Blender Curves and mesh round-trips, objects, modifiers, and transactions

tools/blender/hair_engine_bake.py
  Cycles or Eevee pass materials, scenes, rendering, and preview

tools/blender/hair_engine_operators.py
  Drawing, sculpting, selection, UV, grid, library, and export operators
```

Every module raises on invalid input rather than logging and continuing. Every
Blender mutation is transactional: preserve the source, restore selection, mode,
active object, visibility, and transforms on failure, and validate a new object
or staged export before replacing anything.

## Phases

1. Core model, curves, generation, and grooming.
2. Profiles, UVs, conversion, and Blender round-trips.
3. Short, shell, and particle hair.
4. Baking, materials, and channel mixing.
5. Rigging, libraries, presets, and migration.
6. Interactive Blender tools and safe packaging.
7. Differential fixtures, full suite, documentation, and recipient verification.

A phase is complete only when its tests and real Blender paths pass. The overall
feature is not complete while any acceptance-matrix row remains unimplemented
or unverified. Final documentation will distinguish behavioral equivalence,
independent improvements, and remaining upstream differences.

## Provenance

Update the existing Hair Tool register row from `interop-only` for the bridge to
`behavioural-port` for the new engine. Include GPL-3.0-or-later notices with the
derived engine and recipient package. Keep the existing bridge and its separate
interop boundary intact. Do not copy upstream source files, node libraries,
textures, presets, activation data, or unknown binary formats.
