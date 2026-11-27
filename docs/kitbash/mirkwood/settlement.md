# Mirkwood town and village pieces

These pieces bring the same limestone and heartwood into everyday
buildings. The town uses ashlar lower storeys and sheltered timber
over them. The village modules lower the eaves and shorten the
approaches. The [shared trim](trim.md) stays on structural openings,
fitted rails and the drip below the upper storey.

| Piece | Function |
| --- | --- |
| `mirkwood_house_merchant_a` | Open upper gallery with two fitted timber forks. An exterior limestone stair gives access to the upper floor. |
| `mirkwood_house_lodge_a` | Taller ridge and screened upper lights. An indoor timber stair passes through its own cut floor well. |
| `mirkwood_village_cottage_a` | Lower single-storey ashlar cottage with wood screens and a slate gable. |
| `mirkwood_village_granary_a` | Vented timber storage on individual limestone bearings. Sheltered loading platform at Z0.18 m and a 1.44 × 2.19 m door. |
| `mirkwood_small_market_stall_a` | Covered worktop with fitted timber knees and four stone seats. |
| `mirkwood_small_cistern_a` | Dry stone basin with one coping ring and a 1.24 m opening. |

The exterior and indoor stairs have 19 and 18 treads respectively. The
final Blender evaluation found all 37 treads and their headroom. It checked
25 passage rays and 42 floor samples per building, 16 granary ventilation
rays, and five rays to the cistern floor. The separate geometry checks
confirmed that the granary canopy's carrier stays below the roof finish.

## Compound layouts

The castle and town scenes reuse their own
[siege frontages](siege-kit.md). Each retains one double gatehouse,
two breaches and four defender positions. The castle uses towers.
The town uses forward walls. The compound scenes add real level roads,
a central civic hall reached through a gallery, and side lanes serving
the housing doors. The castle court is 26 m wide and the town court
34 m wide.

| Check | Castle | Town |
| --- | --- | --- |
| Retained frontage positions | 17 | 25 |
| Total saved kit placements | 31 | 44 |
| Surveyed routes | 8 | 11 |
| Grade and headroom samples | 681 clear | 1,047 clear |

The gate passage connects to the gallery at Z0. The surveyed side roads
extend to the house doors and exterior stair foot. They use one mesh at
intersections to keep the flat stone surface free of coplanar overlaps.
Matched overview and court views show the placement choices. An
optional split-slate shell protects the hall's exposed bay and dais.

## Files and staging

The building review and compound review are under `kit_full/houses_01`
and `kit_full/site_01` in the Mirkwood work folder. Each has an editable
file, renders and geometry report. The compound reload confirmed all
75 placement transforms, 21 packed PNGs and 22 inherited scenes.

The corrected civic visual export is `kit_full/houses_01/staging_02`.
It contains five family FBXs with 13 named LOD0 assets, and 21 matching
PNG hashes. Blender reimport preserved the measured pivots, materials
and UV extents. The small bronze lantern had six coincident evaluation
faces when its 12 mm bevel met the other side of a 24 mm plate.
Reducing the bevel to 11 mm removed those faces without altering the
plate's measured dimensions. No tested export mesh has an open edge
or a face below 1e−10 m². The late market fixtures and exterior hall
cladding live in the editable libraries.

The folder has review files for authoring. In-engine material,
collision, pathfinding and siege checks remain pending.

## Reuse in Blender

Register kit_full/library_01 as a Blender Asset Library. Its
mirkwood_asset_library.blend contains all 26 named collections
in Siege, Approach, Hall, Town, Rural and Fixtures. The folder's
README describes collection instancing, appending and shared
placement origins. The original project shelf placements and
the reusable definition origins have different transforms.
Use each collection's placement anchor when docking a piece.

A headless reopen instanced every collection at zero and compared
3,391 evaluated mesh parts with the compound source. Vertex
positions and instance matrices matched, and the largest evaluated
UV difference was under one thousandth of a texel at 1K. All 21
packed map hashes, catalog assignments and 14 custom thumbnails
were verified. Blender's Append operator with Instance Collections
also placed a forward battery, lantern and merchant house at their
stored origins in a fresh scene. See the two reports under
kit_full/library_01/evidence.

## Running-bond slate study

The first Cycles village render made the main cottage and granary
gable courses read as long panels. The granary's smaller canopy
tiles gave a better scale reference. The alternate uses 412
closed slate tiles on the two main gables. The staggered tiles
have 35% headlap, 19 to 21 mm end joints and dark seats below the
joints. Both building envelopes matched the source. All 882
field and edge rays passed, and the same camera framed both
EEVEE panels. The editable variant and matched comparison are
under kit_full/village_roof_02. Blender dropped the unowned scene
on the first save. The corrected file keeps the scene, and a fresh
reopen verified the camera, slate objects and materials.

## Town court paving study

The overview showed the route as a broad continuous slab. The
sibling study lays the existing limestone grain over staggered
flagstone courses. Courses are 0.43 m deep, stones vary from
0.68 to 0.83 m wide and the mortar core is 14 mm wide. The
shader gives the joints depth on the same walk surface.
Reopening checked 216 grade rays across 24 connected pads.
The walk mesh topology and UVs, 21 packed map hashes and the
original camera and lighting match. The editable Blender study
and the matched render pair are under kit_full/court_paving_01.
