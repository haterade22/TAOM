# Complete RetopoPlanes and Petik capability matrix

## Status

Complete public capability coverage verified. This matrix expands the earlier clean-room
specification into an exhaustive adoption contract. Every row has original-product differential
evidence or a real Blender runtime test with an independent expected result. No percentage claim is
based on file count or test count alone.

Exact topology and bounds match for the base, subdivision, UV, initialization, ring, mirror,
separation, thickness, and inspector fixtures. Add-only crease and high-sharp use non-identical
tessellation and are checked for all-quad output, density direction, components, and bounds within
1%. The hex auto-fill solver uses a different central two-quad layout and is checked for its exact
6-vertex, 2-quad result rather than outer-corner identity. These distinctions are acceptance
requirements, not silent parity claims.

Petik evidence includes real BMesh 3 to 6-sided quad patches, alternative solutions, control-line
editing, selected-loop discovery, modal controls, reset/undo, preference swaps, visibility, real
ray casting, snapping, and view normals. Background-only RNA modal-handler registration is isolated
by the test context, while the modal methods themselves run unchanged.

Reference input remains `RetopoPlanes1.6.3vBlender5.1.blend`, SHA-256
`6e00e628d0638fbab315076baf1db466c6b93e583f2a5123dcf50ff510441b64`, inspected with automatic
scripts disabled in Blender 5.2.3 LTS. Internal node graphs are not copied. Public behavior,
interfaces, named data, and independently authored synthetic outputs are the implementation inputs.

## Public entry points

| Product entry point | Required TAOM capability | Evidence |
| --- | --- | --- |
| `3DtoPatterns` modifier | Named UV flattening, original-position retention, source IDs, part/pattern IDs, open seams, point guides, simplification and merge tolerance | Existing unit and Blender tests |
| `RetopoPlanes` modifier and collection asset | Complete guided garment workflow below | Expanded differential suite required |
| `0RetopoInspector` modifier | Highlight edges with more than two adjacent faces, optional split-edge repair, debug-only output, GUI scale and hue | New tests required |
| `Crease To 0.5` geometry-node tool | Set selected edge crease values to 0.5 | Existing operator test |
| Petik `mdutil.multi_petik` | Variable-sided multi-patch creation and editing | Interactive and generated-topology matrix required |
| Petik `mdutil.petik_line` | Selected-loop control curves and editable line density | Interactive and generated-line matrix required |

## Main modifier contract

Every row includes positive behavior, invalid input where meaningful, source preservation, and
packed-addon verification.

| Area | Capabilities that must be exercised |
| --- | --- |
| Source | Source object and guide collection, applied transforms, named UV map, disconnected UV islands, multiple source parts, existing `id`/`ID` attributes |
| Guide validity | Single and multiple quads, disconnected guides, circular holes, non-quad diagnostics, more-than-two-face inspection and repair |
| Plane initialization | Border angles, normalized 0 to 180 degree mapping, top/bottom corner bands, median threshold, point-guide override, generated-plane output |
| Auto fill | Per-pattern point-guide prepass, connected-pattern propagation, red mismatch diagnostics, bounded iterations, editable output |
| Mirroring | X pointer, repeated Y pointer, self-mirror, symmetric pattern centers, UV and winding preservation |
| Density | Base density relative to source, automatic neighboring reconciliation, isolated single-face and circular-loop independence |
| Crease | Add-only mode, full 0 to 1 control, 0.5 neutral, reduction below 0.5, connected-side participation, neutralization tool |
| Sharp | Sharp-edge additions and threshold reach |
| Snapping | Border-only snapping, no guide-guide welding, independent final seam merge tolerance |
| Smoothing | Base-mesh relaxation and independent UV smoothing choice, seam exceptions |
| Reprojection | Zero and bounded subdivision levels, UV barycentric source-detail transfer, UV-name preservation, source UV reproduction |
| UV layout | First-tile fitting, pattern separation, part separation, bevel-weight seam preservation |
| Thickness | Enable, positive and negative values, signed offset, front/back shells, outer and hole walls |
| Preview | Point guides, mismatch lines, patterns-only, seams-only, wireframe, seams, polyloops, debug materials, viewport info |
| Extra UI | 2D/3D morph, sibling contact, edge count, crease labels, snapping labels, source preview and offset, deterministic seed, hue, GUI scale |
| Errors | Missing source, missing UVs, non-finite settings, unsupported guides, empty output, unresolved contacts, simplification warnings |

## Named data contract

The reference exposes internal and public attributes in these families. TAOM may use different
storage names, but equivalent behavior must survive round trips through the Blender adapter.

- Source identity and geometry: `SRCVertexID`, `SRCPartID`, `SRCClothParts`, `SRCEdgesID`,
  `SRCOpenSeams`, `SRCPartsBorders`, `3DPosition`, `UVMap`, `surface_uv_coordinate`.
- Pattern and part routing: `PatternID`, `PatternCenter`, `GRPID`, `LoopID`, `PolyLoopsID`,
  pattern-loop and big-loop identifiers, sibling and mirror identifiers.
- Authoring controls: `PointGuideBool`, `crease_edge`, `sharp_edge`, `bevel_weight_edge`,
  angle/threshold and row/column counts.
- Contact and auto-fill state: contact counts, sibling equality and mismatch state, snap state,
  border spline identifiers, duplicate and contact metadata.
- Diagnostics: non-quad and bad-mesh markers, point guides, mismatch markers, debug color and
  opacity, debug face IDs, viewport text geometry.
- Blender 5.2 compatibility: `id` and `ID` input attributes must not interfere with internal IDs.

## Petik coverage contract

The complete upstream implementation is present, so adoption focuses on proving all public paths
remain usable after the compatibility fixes.

- Multi-patch invocation and modal lifecycle: invoke, keep/exit, keep/continue, cancel, reset,
  undo, and handler cleanup.
- Three, four, five, and six-sided patches, odd-side reduction, alternative solutions, repeated
  patches, shared borders, pre-existing border lines, and patch deletion.
- Control points and lines: create, select, move, change count, change side divisions, uniform or
  initial spacing, curve handles, duplicate rejection, and line deletion.
- Projection and shape: surface snapping, view-normal projection, guessed normals, Aitor distance,
  smooth, fancy quad smoothing, proportional editing, border sticking, and control-point snapping.
- Modal controls: interface scale, hide UI, Q/Space preference swap, keymaps, numeric side counts,
  mouse wheel controls, right-click variants, and keyboard help parity.
- Line operator: selected vertex/edge loop discovery, rescan, create lines, select/deselect,
  control-point weights, count changes, uniform spacing, snap, proportional edit, exit and cleanup.
- Registration: preferences, both keymaps, register/unregister, reload, and the repaired optional
  menu reference.

## Completion rule

A row is complete only when the behavior is implemented and exercised against the original or by a
real Blender runtime test with an independent expected result. Installer tests prove packaging, not
capability coverage. Static inspection alone cannot mark a row complete. Exact internal node or
source-code identity is not required, but missing user-facing behavior is a blocker.
