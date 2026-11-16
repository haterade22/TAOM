# Retopo Planes 1.6.3 clean-room behavioral specification

## Status and boundary

This is the frozen behavioral input for TAOM's independent garment retopology engine. The source
product was inspected to identify its public contract and to measure small synthetic outputs. Its
node graph, demo meshes, packed textures, materials, and layout are not implementation inputs and
must not be copied into TAOM or the KEYforce package.

Source inspected once before this specification was frozen:

- File: `C:\Users\mikew\Downloads\RetopoPlanes1.6.3vBlender5.1.blend`
- SHA-256: `6e00e628d0638fbab315076baf1db466c6b93e583f2a5123dcf50ff510441b64`
- Runtime used for inspection: Blender 5.2.3 LTS, with automatic script execution disabled.
- Public documentation: [general guide](https://nodesinteractive.com/how-to-use-general-guide/),
  [release notes](https://nodesinteractive.com/retopo-planes-release-notes/),
  [planes assist](https://nodesinteractive.com/retopo-planes-planes-assist/),
  [retopo settings](https://nodesinteractive.com/features-retopo-settings/),
  [density control](https://nodesinteractive.com/features-crease-sharp-density-control/),
  [subdivision and reprojection](https://nodesinteractive.com/features-subdiv-reproject/),
  [mirrored patterns](https://nodesinteractive.com/how-to-use-mirrored-patterns/),
  [extra controls](https://nodesinteractive.com/retopo-planes-extra/), and
  [3D to patterns](https://nodesinteractive.com/retopo-planes-3dtopatterns/).

The implementation may use this specification, ordinary Blender APIs, and independently authored
synthetic fixtures. After implementation, one final cross-check may compare public behavior and
look for accidental structural collision. A passing matrix is required before any feature-parity
claim. Matching the source's internal 83 node groups or 8,486 nodes is not a goal.

## Public workflow

1. The artist imports a garment, applies rotation and scale, and gives it non-overlapping UV islands.
2. A flatten operation converts UV coordinates into an editable 2D pattern while retaining each
   source vertex's 3D position, source identity, pattern identity, part identity, and border data.
3. The artist creates or generates quad-only guide meshes over the flat patterns. Knife cuts,
   extrusion, duplication, circular holes, and multiple connected patterns are valid operations.
4. Density controls adjust cuts. Snapping moves guide vertices to pattern borders, never merges
   guide vertices with one another. Seam merging is a separate final-mesh operation.
5. The engine maps the guide mesh back to the source garment, optionally subdividing and
   reprojecting it. Output UVs reproduce the source layout and output positions reproduce source
   detail. The source garment and guide meshes remain available for further editing.
6. Applying the result removes helpers. The artist can then reconstruct subdivision history in
   Blender or another sculpting tool and continue through TAOM's UV, weight, LOD, and Kit gates.

## Feature contract

### UV pattern conversion

- Read a named UV map, defaulting to the active map when no name is supplied.
- Place each flattened vertex at its UV X and Y coordinates with Z equal to zero.
- Preserve the source UV map and store original 3D positions for later barycentric reprojection.
- Derive stable source vertex, UV-island pattern, connected-part, open-border, and part-border data.
- Offer optional simplification with an explicit merge distance. Simplification must not discard
  the mapping needed for later detail transfer. Dense-source simplification is a performance aid,
  not permission to lose final detail when reprojection runs.
- Existing point attributes named `id` or `ID` must not corrupt internal identity bookkeeping.

### Guide meshes and plane assistance

- Accept one or more guide meshes in a named collection.
- Guide faces must be quads. Triangles and ngons are rejected with their face indices and sizes.
- Single-face guides, multi-face grids, disconnected patterns, open shapes, and circular holes are
  supported. Guide vertices that form point guides may override generated corners.
- `Initialize Planes` uses border angles, normalized corner thresholds, median corner thresholds,
  and explicit point guides to propose editable quad guides.
- The angle control maps the normalized 0 to 1 range onto 0 to 180 degrees.
- `Auto Fill Prepass` proposes cuts at point guides independently for each pattern.
- `Auto Fill` propagates missing cuts across connected patterns for a bounded number of iterations.
  The default bound is 5. The bound must prevent unbounded work.
- `Output Generated Planes` returns flat editable guide geometry instead of the reprojected result.

### Mirroring

- A guide vertex extruded over a symmetric pattern requests a mirrored guide across X.
- Repeating that operation across Y requests X and Y mirroring as appropriate for the source layout.
- Mirrored output must reverse winding where reflection would invert normals.
- Mirroring must preserve UV coordinates, pattern association, and editable guide topology.

### Density, snapping, and smoothing

- `Base Density` sets the baseline sampling density. It must be finite and non-negative.
- `Auto Density` reconciles divisions across connected patterns without letting an isolated
  single-face guide or a circular loop distort unrelated regions.
- Edge crease controls cuts. With full crease control disabled, the 0 to 1 range adds cuts.
- With full crease control enabled, 0 to 0.5 reduces cuts, 0.5 is neutral, and 0.5 to 1 adds cuts.
  Reducing density requires the relevant sides and connected patterns to participate.
- `Density Add` uses sharp-edge weights to add cuts, with `Density Add Threshold` offsetting their
  reach. A crease-neutralization helper sets selected edges to 0.5.
- `Snapping Distance` snaps guide vertices only to source pattern borders within the configured
  distance. It does not weld guide vertices together.
- `Seams Merge Distance` merges final output seam vertices within tolerance. Edge bevel weight above
  zero marks seams that must remain unmerged when patterns are separated.
- `Smooth LowPoly` relaxes the base guide mesh a bounded integer number of iterations.
- `Smooth UVs` controls UV relaxation independently from 3D source-detail reprojection.

### Subdivision, reprojection, UVs, and separation

- `SubDiv + Reproject` accepts a bounded non-negative subdivision level. Zero still maps the base
  guide to the source. Each additional level produces a denser all-quad result and reprojects new
  points onto the original source surface.
- Reprojection uses UV-space triangle lookup and barycentric interpolation of original 3D positions.
  It must handle UV seams without mixing unrelated islands.
- Output includes a real, active UV layer with the source map's name and coordinates.
- `Fit UVs to 1st Tile` normalizes the complete UV layout into the 0 to 1 UV tile without changing
  its aspect ratio or island association.
- `Separate Patterns` disconnects output islands at UV-pattern boundaries.
- `Separate Parts` disconnects output at source-part boundaries.
- `Morph 2D > 3D` exposes a preview interpolation between flat and reprojected positions. A final
  applied mesh remains fully reprojected.
- `Offset 3D Cloth` offsets only the optional 3D source preview, not the final UV mapping.

### Thickness

- Thickness is optional and disabled by default.
- Enabled thickness creates front and back shells plus boundary side quads, with finite thickness
  and offset values. The simple one-quad reference becomes eight vertices and six quad faces.
- Existing holes remain open through their boundary walls; thickness must not cap pattern holes.

### Preview and diagnostics

- Preview controls cover point guides, mismatch lines, pattern-only output, seam-only output,
  wireframe, seams, polyloops, edge counts, manual crease density, snapping distance, sibling
  contact, source cloth, and viewport information visibility.
- Debug material modes are `Patterns`, `Parts`, `Retopo Planes`, and `None`, with a deterministic
  color seed and configurable hue.
- Diagnostics identify non-quad guides, unresolved pattern contacts, missing cuts, invalid numeric
  settings, missing source objects, missing UV maps, and disabled source simplification.
- GUI scale affects helper display only. Hiding debug or viewport information must not remove the
  final retopology mesh.

## Measured synthetic oracle

These cases use an independently authored 3 by 3 source grid. Its UV coordinates cover the unit
square and its 3D Z values range from 0.0 at the corners to 0.2 at the center.

| Case | Required observable result |
| --- | --- |
| UV flatten | 9 vertices, 4 quads; positions exactly cover `(0,0,0)` through `(1,1,0)` in 0.5 steps |
| Flatten attributes | Original 3D position, source vertex id, source part id, open-seam, part-border, pattern id, point-guide, and pattern-center data exist |
| Base reprojection | One unit-square guide quad produces 4 vertices and 1 quad at the four source corners |
| One-level subdivision | 9 vertices and 4 quads; edge midpoints have Z 0.1 and center Z 0.2 |
| Initialization | Unit-square pattern produces one editable flat quad with corners on the unit square |
| Thickness | One guide quad with thickness 0.05 and offset 0 produces 8 vertices and 6 quads; front/back separation is 0.05 |
| Disabled density | No implicit interior tessellation of an existing guide face; subdivision remains an explicit stage |

The reference file exposed `Base Density=25`, `Auto Fill Max Iterations=5`, `Seams Merge Distance=0.003`,
`Density Add=2`, `Density Add Threshold=0.7`, and neutral full-crease value 0.5. TAOM may choose
its own defaults but must document them and test every setting.

## Acceptance matrix

The implementation is not complete until all rows have automated or Blender-runtime evidence:

1. UV flatten, named-map selection, simplification, source attributes, and existing-id resilience.
2. Quad validation, single and multi-face guides, disconnected islands, and circular holes.
3. Initialization, point-guide override, prepass, bounded auto fill, and generated-plane output.
4. X/Y mirror generation, winding, and UV preservation.
5. Base and automatic density, both crease modes, sharp additions, neutralization, and thresholding.
6. Border-only snapping, independent seam merging, bevel-weight seams, and low-poly smoothing.
7. Subdivision/reprojection, UV-name preservation, UV smoothing choice, first-tile fitting,
   pattern separation, part separation, morph preview, and source-preview offset.
8. Thickness, offsets, boundary walls, and holes.
9. Every preview/debug control and all four material modes.
10. Error reports, deterministic output, source/guide preservation, Blender 5.2.3 registration,
    KEYforce ZIP contents, license notices, and extraction-time tests.
11. TAOM handoff through UV, weight, LOD, staged-FBX, and Kit review gates.

Exact internal node identity, proprietary demo content, and redistribution of the source `.blend`
are explicitly excluded.
