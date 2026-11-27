# Mirkwood ravine approach

The nine [owner references](reference-study.md) give the next kit family.
The stone screen and its shadowed threshold come from refs 01, 03 and
08. The short bridge with a readable arch below a continuous walk comes
from refs 04 and 05. The gallery's split heartwood supports and the
small pools of light follow refs 02, 07 and 09. The design uses the
[architectural trim](trim.md) on the slate-grey limestone, stone
footings, timber forks and guard profiles.

The user supplied the local Rivendell worked example at
`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/rivendell_reference.html`.
The method used here is a measured cut plan, 1.8 m scale figures,
fixed-camera comparisons, one reusable collection per placement and
a saved-file check. The numeric dimensions are authored for Mirkwood.
The reference pictures establish proportions, lighting relationships
and the sequence of spaces.

## Reusable pieces

| Asset | Placement and construction |
| --- | --- |
| `mirkwood_open_bridge_12m_a` | A 12 m limestone crossing with a 10.08 m span under the vault, radial arch masonry, a shaped drip and dark timber handrails. The side braces follow the arch into stone sockets. |
| `mirkwood_open_landing_6m_a` | A 6 m landing with a level deck, solid stone guards and shaped side bearings. |
| `mirkwood_open_threshold_3bay_a` | A freestanding, 6.50 m high screen. Four flared stone piers each bear on one block. The main bay carries 29 full beech pitches with plain stops. The two side bays use quieter stone ribs. |
| `mirkwood_open_gallery_6m_a` | A 6 m repeating timber gallery bay. Branching ribs gather into the roof carriers and lower struts support the deck. The posts stand on four single-block limestone shoes. |
| `mirkwood_open_stair_4m_a` | Six 150 mm risers with integrated guards and landing surfaces. The full docking length is 4 m including both landings. |
| `mirkwood_small_lantern_a` | A bronze fixture with warm glazing and a mounting plate. Local lamps are placed beside it in the lantern review scene. |

The approach runs along positive Y in this review. The bridge, landing
and gallery use a 4.50 m deck and a 3.60 m clear lane. Guard tops meet
Z+1.10 m at the dock. Stair travel begins at Z−0.90 m and rises to the
level deck at Z0. The screening piers bear beyond the shared lane on
separate stone ledges. The rear doorway and the dark section beds set
the scale and support of the composition as scene guides.

## Both layouts

**Screen first** places the screen just past world Y12 and the gallery
behind it. The deeper oak doorway terminates the view. The daylight
and lantern renders use the same positions and camera.

**Gallery first** repeats the gallery from Y12 through Y24 and moves
the screen to the far end, in front of a stone forecourt and the
recessed doorway. The gallery-to-gallery dock at Y18 is a genuine
second placement. Both layouts share the same bridge and stairs.
The fixed hero camera lets the route and framing be compared directly.

## Evidence and use

The project is
`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood/kit_full/approach_01/view/mirkwood_ravine_approach.blend`.
The review page has the measured cut plan, matching overall views,
bridge springing, threshold, gallery joinery and separated pieces.

| Evaluated Blender check | Screen first | Gallery first |
| --- | --- | --- |
| Level route samples | 749/749 | 945/945 |
| Longitudinal paths through the 2.4 m height envelope | 42/42 clear | 42/42 clear |
| Guard samples around the seams | 72/72 | 96/96 |
| Stair tread samples | 18/18 | 18/18 |
| Open rays beneath the stone span | 7/7 | 7/7 |

A deliberate 20 mm bridge lift failed the floor check. Shifting the
gallery sideways by 160 mm failed the surveyed lane. The saved file
reload checked both placement patterns and the common camera, verified
21 packed images, and located the prior trim and siege scenes. The
artwork is an editable Blender review. The bridge LOD0, LOD3 and
LOD6 passed Blender round-trip in the [civic pilots](distance-studies.md).
Other approach modules still need distance variants. Collision, LOD
switching, and navigation remain game checks.

The local `_tools/approach_01` folder holds the authored modules,
Blender assembly script, geometry tests and saved-file verifier.
Rebuild by opening the final trim project in the shared
`blender_one.py` runner and passing `build_approach.py` the approach
output folder and the trim folder. `approach_plan.py <approach-output>`
draws the cut plan from the placement report.

## Runoff beneath the bridge drip

The Cycles springing view showed broad streaks on the two spandrel
faces. The material study starts shorter vertical stains beneath
the cut drip ledge and tapers them before the arch crown. Run
lengths vary from 0.34 to 0.74 m. The spandrels share the existing
mesh, UVs and three packed ashlar maps with the original. The
camera and other 90 bridge parts also match. The comparison and
matched detail are under kit_full/weather_01.

The saved-file check found the weathering scene and compared the
map hashes and camera transform. The runoff mask is an editable
Blender material study on this bridge.
