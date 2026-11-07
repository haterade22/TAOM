# Hair Tool 4.6.1 Architecture and Algorithm Audit

Status: complete static package audit with representative Blender 5.2.3 runtime probes. This is not a
bit-for-bit reimplementation, and not every interactive operator has been executed.

## Scope and coverage proof

The examined package is `C:\Users\mikew\Downloads\hair_tool_4.6.1\hair_tool` on the desktop. Hair
Tool identifies itself as version 4.6.1 in `__init__.py`. Its source headers grant GPL-3.0-or-later;
TAOM's integration and redistribution boundary is recorded in the
[provenance register](../reference/provenance-register.md).

The audit covered the whole package rather than only the operators used by TAOM:

- 65 Python modules, 49,640 lines, 616 top-level functions, 425 classes, and 352 operators.
- 80 non-Python files: 65 generated CPython 3.13 bytecode caches and 15 source assets or support
  files. The bytecode duplicates the audited Python modules; it is not a separate native engine.
- Eight `.blend` libraries, including every node group, material, object, image, text, and external
  library reference exposed by Blender's data API.
- Two PNGs, one Affinity Photo file, one drawing data file, two asset catalog copies, and one
  registration note.
- Real add-on registration, generator and profile inspection, Hair System conversion, short-hair
  conversion, shell-hair conversion, and jiggle assignment in Blender 5.2.3 LTS.

Local evidence is under `.tmp/hair-tool-re/`: `full-inventory.json`, `full-api-summary.md`,
`full-assets.json`, `embedded-texts.json`, `probe-report.json`, `parameters-report.json`, and
`subsystem-report.json`. These files are analysis artifacts, not redistributable upstream source.
The inventory and API report enumerate every module and operator; the asset report enumerates every
node group and material. Re-run the extraction against a new package before treating these counts as
current for another version.

Coverage means every file was classified and its static role audited. It does not mean every line was
manually read, every node was executed with every input, or all 352 operators received an interactive
UI test. Runtime sections below name the paths actually exercised; unexecuted behavior remains a
source-derived finding rather than a runtime guarantee.

## What the engine is

Hair Tool has no separate compiled grooming executable. Its engine is the combination of:

```text
Python operators and property state
  -> Blender Curves and mesh attributes
  -> appended Geometry Nodes and shader groups
  -> evaluated curve, ribbon, mesh-card, short-hair, or shell geometry
  -> Python attribute and UV conversion
  -> optional baking, rigging, library, preset, and export helpers
```

`auto_load.py` discovers modules recursively, topologically sorts registrable classes from annotation
and panel dependencies, and registers them. `__init__.py` then installs properties, menus, tools,
keymaps, node types, overlay handlers, message-bus handlers, and library previews. `utils/handlers.py`
batches load, save, undo, and dependency-graph callbacks. `hair_baking/hair_nodes_tree.py` and
`hair_tree_update.py` track node-tree structure and socket changes.

Blender 5.2 background mode needs `gpu.init()` before importing the add-on. Registration must go
through `bpy.ops.preferences.addon_enable(module="hair_tool")`, because several registration paths
expect add-on preferences to exist. Direct module import is not an equivalent lifecycle.

## Generation and deformation

### Hair Systems

`object.add_hair_system` is the main entry point. Its runtime-confirmed parameters are `mode`,
`surface_obj`, `draw_popup`, and `on_active_hair_user`. A system separates setup, generation or
filtering, deformers, and profile evaluation into Geometry Nodes modifiers. It can copy or link whole
systems and individual deformers, bake up to a selected stage, transfer between mesh and curve
owners, and make shared node data unique.

The source library contains generators for surface distribution, interpolated guides, grid guides,
UV grids, mesh borders, mesh parts, and custom interpolation. Deformer groups cover clumping, curls,
noise, length, trim, radius, roundness, tilt, roll, twist, smoothing, simplification, subdivision,
mirroring, tagging, grouping, surface attachment, tracking, mesh islands, profiles, UVs, and
spring or collision experiments. The node names establish available stages, not a claim that every
stage was runtime-tested.

The starter generator inspected at runtime exposes `Resolution`, `Length`, `Mask`, relax controls,
`Distribution`, `Density`, spacing and density factors, and `Seed`. TAOM deliberately changes only
`Length`, `Density`, `Seed`, and `Resolution`, leaving the other behavior at upstream defaults.

### Curves and guides

The recovered curve algorithms include:

- Catmull-Rom interpolation, including nonuniform and centripetal parameterization.
- Arc-length-aware resampling, point-count normalization, root or tip bias, and length adjustment.
- Parallel-transport frames for stable tangent, normal, and binormal propagation.
- Ramer-Douglas-Peucker simplification and Visvalingam-Whyatt area simplification, with a
  velocity-weighted variant for timed input.
- Guide-stroke ordering for complex interpolated-strand layouts.
- Mesh-island extraction, UV-based orientation, strip reconstruction, and smoothing.
- BVH and KD-tree acceleration for surface snapping, root lookup, collision, and brush queries.

`grid_spline_deform.py` builds guide rows from a marked base loop, evaluates rows through nonuniform
parameters, propagates frames, and maps mesh deformation through guide control points. The modal tool
supports picking, selection, transform, preview, and cancellation. `linear_deformer/` supplies a
separate gizmo-driven deformation path with mirrored-strand handling and local history.

### Drawing and sculpting

Drawing projects screen strokes onto a surface, extends or trims strands, smooths point data, assigns
UV regions and profiles, and initializes per-strand attributes. Sculpting adds screen-space picking,
surface raycasts, radius-dependent point selection, weighted smoothing, grooming, straightening, and
rest-length relaxation. Workspace tools add proportional transforms, region selection, symmetry,
cleanup, lengthening along local curvature, and duplicate or zero-length removal.

These paths depend heavily on Blender's active object, mode, selection, viewport, dependency graph,
and event loop. A future independent implementation should separate pure geometry operations from
that UI state rather than reproducing large modal operators as one block.

## Profiles, UVs, materials, and conversion

The profile library includes flat, round, curl, braid, multi-curve, and mesh profiles. Multi-profile
systems load objects individually or from collections and assign `Profile_ID` to selected strands.
UV-region holders store boxes and associated settings in mesh attributes. Assignment can be direct,
randomized, selected in the UV editor, or picked through profile objects in the 3D view.

The profile group inspected at runtime is `Flat_Profile_UV`. It exposes spline type, strand and profile
resolution, width, UV-derived width, random UV flip, roundness and its influence, uplift, UV taper,
bevel controls, and UV tiling. TAOM's starter bridge sets only `Width` there.

Two UV outputs have different jobs:

- `UVMap` addresses the visible hair texture atlas.
- `HairTool_UV` preserves root-to-tip direction for gradients and downstream rigging or shading.

During conversion, `ribbons_operations.py:477-483` maps the Geometry Nodes `UVMapGN` attribute to
`UVMap` and `UVHelperGN` to `HairTool_UV`. Conversion can also transfer color, selection groups,
face sets, sampled normals, packed vertex attributes, and sampled surface data. It supports welding,
joining instances, applying deformations, restoration helpers, proxy meshes, and pin-weight gradients.

Conversion is destructive to the source object in the exercised path. It also produces world-shaped
vertices while retaining a source transform, which can apply the transform twice if a consumer treats
those vertices as ordinary local coordinates. TAOM projects donor weights in the raw frame, restores
the display matrix, and re-imports the staged FBX before accepting the result. An operator returning
`FINISHED` is not sufficient validation.

`material_operators.py` manages the default material, UV regions, input-mask previews, and base-color
preview. `hair_material.blend` contains the default anisotropic hair shader and a packed 1024 by 1024
default texture. TAOM replaces the authoring material with the intended Kit material at delivery.

## Baking and channel mixing

`hair_bake.py` imports or appends the baking scene, assigns pass materials, controls visibility and
render settings, bakes maps, applies padding, updates previews, and feeds channel mixing. The source
contains pass materials for AO, diffuse, opacity, normal, root, ID, random color, clump color, flow,
directional, depth, and color. Depth uses an additional attribute group. Normal, flow, and directional
maps receive non-color treatment in the preview path.

`hair_bake_nodes.py` defines the custom `TextureChannelMixing` tree, single-channel, RGB, and RGBA
sockets, and input, output, padding, mix, alpha, separate, and combine nodes. Parent depth determines
an execution queue. `hair_tree_update.py` separately hashes node links to find changed consumers.

The baking scene, node library, materials, channel mixing, and preview path were statically audited.
A complete multi-pass bake was not run in this audit, so image fidelity, padding quality, color-space
behavior for every pass, and all channel-mix combinations remain unverified at runtime.

## Short hair, shell hair, particle hair, and rigging

Short hair rebuilds or preserves a scalp mesh and grows cards from its edges. Its four topology modes
are `KEEP`, `CELLS_TRIS`, `DIAMOND`, and `DIAMOND_TRIS` (`short_hair.py:113-119`). The cell path uses
triangulated Voronoi-style cells, smoothing, and shrinkwrap projection; the diamond paths trade
regularity and card density differently. Vertex groups and edge attributes drive generation and UVs.

Shell hair builds layered shells from a base and guide object. Setup assigns 12 layers by default
(`shell_hair.py:139`). Its conversion shares mesh data between shell objects and relies on
object-level material slots. That is efficient for repeated geometry, but unrelated mesh edits must
not assume each shell owns unique data. A separate scene bakes the tiling shell texture.

Legacy particle hair can convert particles to curves, create particle strands from curves, and scale
particle length. This is an interoperability path, not the basis of TAOM's new groom workflow.

Rigging includes ribbon-to-bone generation, generated weights, pin gradients, surface and root weight
sampling, curve shape attributes, and transfer to matching mesh topology. `jiggle_chain_b4.py` adds
pose-bone spring settings, collision queries, frame-change simulation, and a modal simulation runner.
Jiggle assignment passed the runtime probe. This does not establish Bannerlord physics support;
TAOM still delivers ordinary armature weights and must validate deformation in game.

## Libraries, presets, migration, and export

The hair library appends `.blend` presets, remaps duplicate data, prepares a bind root, snaps presets
through deformation and shrinkwrap modifiers, and renders or crops thumbnails. Preset serialization
handles individual deformers, one Hair System, or a complete system stack, recursively caching node
properties, links, UV regions, and modifier settings.

Migration operators rebuild setup, profile, deformer, and whole-system node packs while preserving
selected input and output state. Update code covers older distribute-point, crease, and
Track-to-Curve settings and repairs Blender 5.2 join-output links. Several operators explicitly warn
that rebuilding a complete setup is destructive. Backups of the actual groom are therefore required.

`utils/export.py` supports FBX and OBJ. FBX export applies mesh modifiers, preserves polygons with
`use_triangles=False`, and exposes orientation, scale, smoothing, tangent-space, color, and leaf-bone
options. It does not enforce TAOM's exact weights, material, UV pair, morph exclusion, LOD chain, or
re-import checks. `utils/finalize_to_copy.py` joins converted hair into a copy, but that is still an
authoring finalizer rather than TAOM delivery.

The updater is not part of grooming. Do not run it from an automated TAOM workflow; see the security
findings below.

## Binary asset audit

Node-group counts include geometry, shader, and compositor groups. Counts come from Blender's data
API, not filename inference.

| Asset | Node groups | Materials | Objects | Images | Texts | Role |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `hair_baking/hair_bake_5.2.blend` | 229 | 14 | 81 | 0 | 2 | Baking scene, pass materials, previews, examples, and UV data |
| `hair_baking/hsystem_nodes_lib_5.2.blend` | 217 | 1 | 0 | 0 | 2 | Main generator, deformer, profile, UV, interpolation, and simulation library |
| `hair_library/render_thumb.blend` | 0 | 0 | 0 | 1 | 0 | Thumbnail render scene and viewer image |
| `hair_material.blend` | 30 | 1 | 5 | 1 packed | 1 | Default hair material, texture, profile, UV, and deformation helpers |
| `ht_nodes_tools/hair_operators.blend` | 30 | 2 | 4 | 0 | 1 | Asset-browser operator and primitive node groups |
| `shell_hair_geo_nodes.blend` | 4 | 1 | 2 | 1 packed | 0 | Shell generation and shader, with a packed 256 by 256 texture |
| `shell_hair_texture_bake.blend` | 8 | 3 | 13 | 0 | 0 | Tiling shell-texture authoring and bake scene |
| `short_hair_geo_nodes.blend` | 9 | 1 | 3 | 1 packed | 0 | Short-hair generation and shader, with a packed 256 by 256 texture |

The only external Blender library reference is `ht_nodes_tools/hair_operators.blend` to
`//..\hair_baking\hsystem_nodes_lib_5.2.blend`. Packed images make the default, shell, and short-hair
materials independent of their original absolute texture paths.

Embedded texts in the two main libraries and `hair_material.blend` document UV-box semantics:
nonnegative IDs select a region, `-1` forwards the existing box, and values below `-1` use all boxes
with the value as a randomization seed. `hair_operators.blend` embeds Blender's stock
`space_userpref.py`, not a hidden Hair Tool runtime.

The two PNGs were inspected: `hair_library/empty.png` is a library placeholder and
`textures/short_hair.png` is the short-hair texture. Both copies of `blender_assets.cats.txt` are
identical and define `HT Operators`, `Internal (Do not use)`, and `Primitives` catalogs.
`duplicate_reg_clss.txt` records a duplicate-panel registration concern.

`hair_library/empty.afphoto` and `icons/draw_xray.draw_quads.dat` have no recognized decoder or useful
plain-text structure in the performed inspection. Their exact contents and necessity remain
**UNVERIFIED**. They must not be described as decoded, and no TAOM process should depend on a guessed
parser for them.

## Dependencies and data contracts

The only non-stdlib Python dependency found by the import audit is NumPy, supplied by Blender. Other
Blender APIs used are `bpy`, `bmesh`, `mathutils`, `bl_math`, `gpu`, `gpu_extras`, `blf`,
`bpy_extras`, `addon_utils`, `nodeitems_utils`, and `rna_keymap_ui`. There is no separate pip package,
native DLL, or external grooming executable in the package.

Important persisted data contracts are Blender curve and mesh attributes, modifier input identifiers,
Geometry Nodes group names, material names, UV-layer names, custom property collections, serialized
JSON presets, `.blend` libraries, and thumbnail names. Node input identifiers such as `Input_15` are
version-sensitive. TAOM should prefer inspecting socket names and types over copying upstream numeric
identifiers into new code.

The updater can additionally contact remote hosts and download ZIP data. That network path is
optional, is not needed for grooming, and is excluded from TAOM automation.

## Runtime evidence

The following are measured results, not inferred behavior:

| Probe | Result |
| --- | --- |
| Add Hair System and inspect parameters | Passed; setup, generator, and profile modifiers created |
| Basic Hair System conversion | 3,165 vertices and 1,936 polygons; `UVMap` and `HairTool_UV`; no weights |
| Short-hair setup and conversion | 2,160 vertices, 540 polygons, `UVMap`, `HairTool_UV`, `SampledUV`, `ShortHairMaterial` |
| Shell-hair setup and conversion | 744 vertices, 864 polygons, `UVMap`, `ShellHairMaterial` |
| Jiggle assignment | Passed after selecting pose bones through `bpy.ops.pose.select_all` |
| Guided dwarf delivery through TAOM | 1,380 vertices, 832 polygons, all weighted, both required UVs, no morph channels |

The dwarf delivery used material `m_dwarf_beards_b`. Weight projection measured 114.156 mm median
and 342.1021 mm maximum. LOD1 through LOD5 contain 1,164, 499, 250, 116, and 50 triangles, with zero
unweighted vertices and no round-trip diffs. These results validate TAOM's staged bridge for that
asset, not every Hair Tool generator or every race.

No updater execution, complete bake, library append, preset round-trip, interactive modal grooming,
particle conversion, or export-matrix equivalence test was performed in this audit.

## Module-by-module coverage

Operator counts include classes derived from `bpy.types.Operator`; panels, menus, gizmos, node types,
and property groups account for the remaining classes.

| Module | Operators | Responsibility |
| --- | ---: | --- |
| `__init__.py` | 0 | Add-on metadata, lifecycle, menus, overlays, handlers, tools, and registration orchestration |
| `auto_load.py` | 0 | Recursive module discovery, class dependency sorting, registration, and unregistration |
| `curve_simplify.py` | 1 | RDP and Visvalingam-Whyatt curve simplification |
| `curves_from_grid.py` | 5 | Guide-grid systems, mesh-guide curves, displacement baking, profile guides, grid display |
| `curves_resample.py` | 2 | Point resampling, distribution bias, and strand-length adjustment |
| `curves_sorting.py` | 1 | Stroke-drawn guide ordering for interpolated strands |
| `drawing_curves.py` | 3 | Surface drawing, BVH snapping cache, extension, UV and profile assignment |
| `generate_curls.py` | 1 | Curl generation and curve-tilt alignment |
| `grid_spline_deform.py` | 1 | Grid guide construction, transported frames, modal mesh deformation |
| `hair_baking/__init__.py` | 0 | Baking package marker |
| `hair_baking/curves_attributes_manager.py` | 7 | Attribute editing, smoothing, selection conversion, and curve mapping |
| `hair_baking/edit_input_mask.py` | 5 | Input-mask creation, painting workflow, reset, gizmos, and restoration |
| `hair_baking/hair_bake.py` | 6 | Bake scene, pass setup, rendering, padding, previews, and UV flattening |
| `hair_baking/hair_bake_nodes.py` | 11 | Channel-mixing tree, sockets, texture operations, execution queues |
| `hair_baking/hair_bake_ui.py` | 2 | Baking panel and scene switching |
| `hair_baking/hair_geometry_nodes.py` | 48 | Hair System lifecycle, deformers, masks, tags, copying, transfer, and baking |
| `hair_baking/hair_geometry_nodes_backups.py` | 0 | Nested backup storage and profile or node-setting restoration |
| `hair_baking/hair_geometry_nodes_overlay.py` | 2 | Strand overlays, island selection, and group-ID picking |
| `hair_baking/hair_geometry_nodes_profile.py` | 10 | Profile creation, copying, uniqueness, switching, and defaults |
| `hair_baking/hair_geometry_nodes_selections.py` | 5 | Selection attributes, group IDs, tags, islands, and active-object switching |
| `hair_baking/hair_geometry_nodes_shape_keys.py` | 6 | Curve shape attributes and transfer to matching mesh cards |
| `hair_baking/hair_geometry_nodes_shared.py` | 0 | Shared node imports, system state, UVs, profiles, masks, surfaces, and caches |
| `hair_baking/hair_geometry_nodes_ui.py` | 42 | Hair System panels, menus, search, profile controls, and repair UI |
| `hair_baking/hair_geometry_nodes_update.py` | 6 | Node-pack migration, listener updates, and destructive system rebuilds |
| `hair_baking/hair_geometry_nodes_utils.py` | 11 | Curve edit primitives, resampling, joining, separation, hiding, and tilt |
| `hair_baking/hair_geometry_nodes_uvs.py` | 9 | UV layout editing, region assignment, copying, cleanup, and profile generation |
| `hair_baking/hair_multi_profile.py` | 10 | Multi-object profiles, `Profile_ID`, collection loading, preview, and selection |
| `hair_baking/hair_nodes_tree.py` | 0 | Node-tree indentation and hierarchy caches |
| `hair_baking/hair_tree_update.py` | 0 | Link hashing and changed-node execution |
| `hair_baking/ht_image_operators.py` | 2 | Image and file-output saving |
| `hair_curve_helpers.py` | 10 | Strand selection, taper, roundness, tilt, roots, radius, cutting, and adjustment |
| `hair_library/__init__.py` | 0 | Hair-library package marker |
| `hair_library/generate_thumb.py` | 0 | Thumbnail scene preparation, camera, append, and cleanup |
| `hair_library/hair_lib.py` | 12 | Library scanning, append, bind, snap, duplicate remapping, and previews |
| `hair_library/modal_save_hair.py` | 1 | Interactive preset saving and cropped thumbnail capture |
| `hair_mesh_helpers.py` | 13 | Mesh-island guides, bones, gradients, sampling, weights, and color packing |
| `hair_presets_to_file.py` | 15 | JSON presets for deformers, Hair Systems, and complete stacks |
| `hair_tool_props.py` | 0 | Property groups, scene state, preferences, and registration |
| `hair_tool_ui.py` | 21 | Main panels, context menus, preferences, asset tools, and keymaps |
| `hair_workspace_tool.py` | 22 | Modal grooming, selection, proportional editing, symmetry, and cleanup |
| `highlight_selected.py` | 0 | Selection-highlight cache and dependency-graph handlers |
| `jiggle_chain_b4.py` | 9 | Pose-bone spring setup, collision, simulation, and jiggle UI |
| `linear_deformer/__init__.py` | 0 | Linear-deformer package marker |
| `linear_deformer/deformer_curve.py` | 1 | Gizmo-driven linear deformation, mirroring, and local history |
| `linear_deformer/mi_utils_base.py` | 0 | Vertex bounds, centers, and dimensions |
| `linear_deformer/mi_widget_linear_deform.py` | 0 | Linear-deformer widget drawing, picking, and setup |
| `material_operators.py` | 5 | Default material, UV region, mask, and base-color previews |
| `particle_hair.py` | 3 | Legacy particle-to-curve, curve-to-particle, and length conversion |
| `resample2d.py` | 0 | Catmull-Rom interpolation, resampling, and parallel-transport frames |
| `ribbons_operations.py` | 8 | Mesh conversion, UV mapping, restoration, proxies, and pin weights |
| `sculpt_brushes.py` | 6 | Hair sculpt brush, BVH queries, grooming, smoothing, and gizmos |
| `shell_hair.py` | 8 | Shell setup, conversion, restoration, material, and texture baking |
| `short_hair.py` | 13 | Short-hair topology, generation, UVs, conversion, and restoration |
| `update_addon.py` | 3 | Release checks, download, install, and rollback |
| `utils/__init__.py` | 0 | Utility package marker |
| `utils/curve_wrapper.py` | 0 | Curve data abstraction, point access, attributes, and editing helpers |
| `utils/export.py` | 2 | FBX and OBJ conversion and export |
| `utils/finalize_to_copy.py` | 2 | Conversion and joining into finalized copies |
| `utils/general_utils.py` | 0 | Object, collection, attribute, modifier, node, and material utilities |
| `utils/gpu_helpers.py` | 0 | GPU boxes, text, gradients, buttons, and depth offsets |
| `utils/hair_common.py` | 0 | Shared operator-settings save and restore mixin |
| `utils/handlers.py` | 0 | Message-bus subscription and batched lifecycle callbacks |
| `utils/helper_functions.py` | 0 | Falloffs, remapping, resampling, BVH, drivers, and grid checks |
| `utils/mesh_utils.py` | 0 | Mesh islands, UV sorting, strips, smoothing, and positions |
| `uv_draw_boxes.py` | 2 | UV-region overlays, palette, offsets, and UV-editor header |

## Defects and security findings

These findings apply to the audited upstream package. They are not reasons to bypass TAOM's bridge.

1. **Unsafe updater transport and archive handling.** `update_addon.py:188-240` disables TLS
   verification, performs no package signature or hash check, deletes the existing installation
   before extracting members, and joins unnormalized ZIP subpaths to the install directory. A
   malicious archive can therefore write outside the intended directory, and a failed install can
   destroy the working add-on. The release index itself uses plain HTTP at line 29. Do not use this
   updater; install a trusted package manually and keep a backup.
2. **Fragile update metadata handling.** `str_version_to_float` at lines 96-99 is not a reliable
   version ordering scheme. JSON decoding and several response-shape failures are outside the
   updater's exception handling. Update and rollback operators can report success without checking
   the string failure result returned by `get_update`.
3. **Partial registration is allowed.** `auto_load.py:28-40` catches class-registration exceptions
   and continues. Other modules also suppress broad exceptions. A visible panel or a `FINISHED`
   result does not prove complete registration or valid output.
4. **Changed-node exclusion is ineffective.** `hair_baking/hair_tree_update.py:85` calls
   `nodes_for_update.difference(inputNodes)` without assigning the returned set. Input nodes remain
   in the execution set and may run repeatedly. Use `difference_update` in an independent design.
5. **Unreachable operator code.** `curves_from_grid.py:85-86` returns the popup result and then has
   an unreachable second return. This is harmless dead code, not an alternate success path.
6. **Windows path literals contain invalid escapes.** Examples occur in `hair_baking/hair_bake.py:47`,
   `hair_curve_helpers.py:631`, `hair_mesh_helpers.py:954`, `shell_hair.py:598`, `short_hair.py:34`,
   and `utils/general_utils.py:273`. These can warn and make path intent fragile; use raw strings or
   `pathlib` in new code.
7. **Shared shell geometry needs explicit ownership.** Sharing is intentional and efficient, but a
   consumer that edits one shell's mesh can affect others. Copy data before unrelated edits.
8. **Conversion and finalization need transactions.** Source and temporary object removal, global
   selection changes, mode switches, and broad exception handlers make exception recovery difficult.
   TAOM's staged output and post-import checks remain mandatory.

## Implications for TAOM

The [independent TAOM hair engine](../features/independent-hair-engine.md) is now the default path
for new curve-authored grooms. The older [Hair Tool workflow](../features/hair-tool-workflow.md)
remains available for upstream-specific scenes. Both retain TAOM's weight, material, UV, morph,
preview, LOD, and staged FBX gates. Neither uses Hair Tool's generic exporter directly.

The independent engine was built in these layers, with unit and Blender fixtures at each layer:

1. Typed curve, attribute, surface, UV-region, and profile data structures independent of Blender UI.
2. Deterministic generation, interpolation, resampling, simplification, guide ordering, and transport
   frames, compared against simple known curves and upstream outputs.
3. Pure grooming transforms, with modal Blender code acting only as an input and display adapter.
4. Flat, round, curl, braid, multi-curve, and mesh profile evaluation, preserving both UV contracts.
5. Transactional conversion that never silently deletes the only source and always restores context.
6. Short-hair and shell-hair generators only after card conversion has equivalent fixtures.
7. Baking, library, presets, and migration as separate services, not prerequisites for basic grooming.
8. TAOM delivery through the existing weight, material, morph, preview, LOD, and in-game gates.

The main improvements over the recovered architecture should be explicit data flow, pure and testable
geometry kernels, bounded caches with clear invalidation, version-tuple migrations, transactional
operator context, safe archive handling, and reports that fail closed. Efficiency should come from
NumPy batch operations and appropriately scoped BVH or KD-tree caches, not from suppressing errors or
sharing mutable mesh data without an ownership contract.

## Remaining research

- Decode or obtain authoritative documentation for `empty.afphoto` and `draw_xray.draw_quads.dat`, or
  establish by controlled removal that they are optional placeholders. Current format knowledge is
  unverified.
- Exercise every generator and deformer against versioned fixture scenes, including empty, masked,
  degenerate, multi-object, and shared-data inputs.
- Run complete bake and channel-mix comparisons for every pass, color space, padding mode, and output
  format.
- Test library and preset round-trips across Blender versions and the destructive migration operators.
- Add interactive UI coverage for drawing, sculpting, UV editing, grid deformation, and cancellation.
- Measure geometry-node performance and memory before choosing which kernels to reimplement.

Until those checks exist, the accurate conclusion is **full package static audit, representative
runtime verification, no complete clone**. The audited algorithms are sufficient to guide an
independent implementation, but not to claim behavioral equivalence today.

## Cross-references

- [Independent hair engine](../features/independent-hair-engine.md): TAOM's default authoring and delivery.
- [Hair Tool groom workflow](../features/hair-tool-workflow.md): optional upstream-specific bridge.
- [Race face and hand morphs](../reference/race-face-and-hand-morphs.md): upper-mesh and morph rules.
- [Provenance register](../reference/provenance-register.md): GPL behavioral-port and interop boundaries.
- [Quad Remesher engine research](quad-remesher-engine.md): separate native reverse-engineering effort.
