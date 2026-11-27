# Mirkwood siege kit

Issue [#1620](https://github.com/haterade22/TAOM/issues/1620) tracks the new castle
and town siege kit. The editable Blender assemblies use the approved
[limestone and dark heartwood](textures.md) and the [siege controls](siege-controls.md).

## Shared construction

The ground line is Z0 and the outside face is toward negative Y in the straight
pieces. Intact walls, their breach variants and access variants share a 20 m
span. The gatehouse and tower use 16 m spans. Straight joins carry the core
from Y−2.6 m to Y+2.6 m, founded at Z−3 m, with a wall walk at Z12 m and
parapets reaching Z13.15 m across the outer half metre at either edge.

The gatehouse has independent gate seats at Y−4 m and Y+4 m, with a 6.2 m
wide, 7.2 m high passage at grade. The decorated limestone sweep, beech
relief and single-block jamb feet carry the threshold study into the
gateway. The guide outlines in the cutaway mark the two animated gate
envelopes. The game gate prefabs supply the moving leaves.

The castle uses raised tower pads at Z16 m, with 7.4 × 8.0 m of level
space. The town uses lower forward walls at Z8.8 m, with 7.4 × 8.2 m of
level space and a separate descending stair. Both pads accommodate the
measured 5.5 × 5.72 m footprint for the native machine, crew and
ammunition. The guide outlines in the review scenes show that footprint.

## First assembly

The archived review is under
`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood/kit_full/siege_01`.
Its seven asset collections used 12 packed 1K maps from the approved
threshold study. The Blender report recorded:

| Check | Observed result |
| --- | --- |
| Straight wall end profile | 1,300 samples, zero mismatches |
| Gate passage and grade | 63 clear passage rays, 56/56 floor samples |
| Breach | 133/133 floor samples, 16.82° maximum sampled route slope |
| Battery pads | 49/49 floor samples each, clear 3 m overhead, clear outward ray |
| Tower and forward stairs | 36 tread samples, 0.20 m maximum sampled rise |

The images showed a very even seam through the breach bed, and the full
frontage exposed the need for a town-side way up to the wall walk. The
next assembly adds that route and puts a curved return on each flank to
exercise the connection on a change of direction.


## Assembled frontages

The current editable package is
`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood/kit_full/siege_04/view/mirkwood_siege_kit.blend`.
The castle uses four tower stations. The town uses four forward stations.
Each frontage includes both gate seats, two interchangeable breach placements,
two grade-to-wall switchbacks, and two 90° wall returns. The switchback's
3 m-wide flights have a 6 m mid-landing and 12 m top landing. The curved
return has a 6 m centreline radius with straight docking ends.

The second pass replaced the breach's clean surface with exposed fractured
fill and a settled rubble bed. The third added staggered ashlar joints to
broad faces, mapping along the curved corner by arclength. The Painter stone
grain is retained. The new 1K ashlar tile repeats every 2.210 m in both axes,
with three courses, 12 to 20 mm joints and a 5 mm relief at their edges. The
smooth monolithic jamb seats and carved mouldings use the plain limestone.
Dark heartwood bearing plates sit below the tower pads.

A separate reload check found a 1.58 m neck at the castle tower's lower
western landing. Siege 04 moved the landing edge and its supports 0.32 m
outward. Floor and 2.1 m overhead checks now show a 1.90 m clear route at
the same three sampled cross sections.

| Current Blender check | Observed result |
| --- | --- |
| Straight ends and corner ports | 1,560 straight and 260 corner profile rays, zero mismatches |
| Curved crossing | 84/84 walk samples and 56/56 guard samples |
| Assembly docking | 14 paired wall joins per frontage, all matched |
| Gate | Both 8 m-separated seats checked in each frontage |
| Stairs | 96 tread samples, maximum sampled riser 0.20 m |
| Defenders | 49/49 level samples per pad, overhead and field rays clear |
| Saved project | Seven scenes, ten modular assets and 15 packed 1K textures |

The reports are under `siege_04/evidence`. The same version is staged locally
in `siege_04/staging`. Blender reimport matched all ten mesh names,
bounds, origins and UV layers. An independent binary FBX read matched
183,702 triangles. All 15 staged maps match their source files byte
for byte. The Blender review checks static geometry. Modding Kit
collision, stair bodies, navigation, multiplayer and native siege
behavior need separate checks before deployment.

## Timber gate leaf study

The local study in kit_full/gate_leaf_01 places four editable
leaves at the two gate seats. The outer pair has diagonal
heartwood braces and pinned iron straps. The inner pair uses
quieter rails and dowels. The open render folds the leaves away
from the chamber. The sampled swing covers 0 to 90 degrees and
the full open passage measures 5.564 m wide. All 72 passage
rays, 99 level-floor rays and 244 swing envelope checks passed
on reopen. A deliberately shifted hinge failed the test.

The display poses and hinges are Blender authoring controls.
Game gate animation, collision binding and motion still require
native prefab checks. The scene and all four proof images are
listed in gate_leaf_01/evidence/gate_leaf_reopen.json.
