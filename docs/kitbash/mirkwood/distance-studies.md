# Mirkwood distance and walk studies

## Visual distance pilots

The four pilots use the current Blender kit and the
[measured Bannerlord distance table](../../reference/engine/static-mesh-lod-selection.md).
LOD3 and LOD6 give a first comparison at the default 30 m and 130 m
thresholds measured from the bounding sphere. Blender's matched
cameras make detail changes easy to compare directly.

| Source piece | Evaluated LOD0 | LOD3 | LOD6 |
| --- | ---: | ---: | ---: |
| Limestone bridge | 58,608 | 32,476 | 17,940 |
| Vaulted hall bay | 57,668 | 21,780 | 15,396 |
| Merchant house | 78,176 | 34,152 | 18,854 |
| Vented granary | 46,164 | 5,264 | 2,816 |

The simplifier keeps the structural floor, walls, docks, guards and
openings. It can drop small placements and reduce selected secondary
meshes. It keeps UV layers and material boundaries. The geometry tests
check the sampled silhouette and first-hit depth against the evaluated
source. Routes inside the bridge, hall, merchant stair and granary stay
open and level at all checked levels.

The first merchant candidate lost its roof surface height when the
individual slate pieces went away. Reinstating all the slate made
the candidate larger than LOD3. The accepted study adds two shallow
continuous slopes at the measured slate height and rejects a level
whose triangle count fails to fall. The rendered LOD0/3/6 comparisons
sit in `kit_full/lod_02/renders`.

## Walk proxy study

Simpler closed shapes represent the bridge deck and vault, timber
gallery walk and rail envelope, widening vestibule deck, and the
reachable hall shell. Their dimensions follow the same placement
origins and docking caps as the detailed meshes.

On reload, an independent Blender check recentered all 14 proxy
meshes and assembled them at Y0, 9, 16 and 24. It passed the cuts at
Y6, 12 and 20 for floor height, 2.65 m headroom and continuous guard
caps. At the first two cuts the lane is 3.60 m wide. The third is
10.50 m wide. It also passed 49 whole-chain sweeps. A planted 15 mm
gallery shift failed grade and cap comparisons. The curved bridge
proxy retained the 10.08 m opening below the deck.

The review file is
`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood/kit_full/lod_02/view/mirkwood_lod_and_walk_study.blend`.
It reopens the twelve visual collections, four proxy collections
and 21 packed map hashes. The two independent metrics are in
`evidence/lod_reopen.json` and `evidence/proxy_joins.json`. Body flags,
walking AI, detail switches and asset imports remain Modding Kit
and game checks.


## Siege distance pilots

The siege reduction keeps the stairs, gate jambs, deck joins,
wall stops, defender pads and walking rubble as evaluated solids
where changing their dimensions could alter the route. Small
dressings and inactive surfaces can be reduced. The sampled
breach therefore saves less geometry than an intact wall.

| Siege structure | Evaluated LOD0 | LOD3 | LOD6 |
| --- | ---: | ---: | ---: |
| Intact wall | 21,496 | 11,334 | 7,722 |
| Breach | 22,478 | 20,580 | 19,874 |
| Gatehouse | 23,892 | 19,812 | 16,224 |
| Tower | 17,228 | 13,404 | 9,066 |
| Forward battery | 11,268 | 7,788 | 4,530 |

The headless build sampled both gate passage and floor, the breach
walking surface and clearance, the intact wall walk and guards,
and the defender footprints, overhead and outward rays. All five
candidates pass at both reduced levels. The file and measured views
are under `kit_full/lod_siege_01`.

## Placed frontages and visual files

The saved castle and town frontages were rebuilt at each of the
three sampled detail levels. Across six placements of the two
frontages, 3,108 static rays checked gate seats, walking joins and
defender clearances. The same check detected a planted 35 mm join
gap. See the measured placements and the saved-file checks under
kit_full/lod_siege_01/evidence.

The four civic visual FBXs round-trip with twelve mesh names using
the .lod0, .lod3 and .lod6 suffixes. The reimport checked each
pivot, bound, UV, material slot and closed shell. The stage and
21 checked textures are in kit_full/lod_02/staging. These pilots
share filenames with the larger civic family FBXs. Each pilot carries
one base asset at three levels. Keep the exports in their own folders
until the full family is assembled for import. The walk proxy meshes
are editable in the study file.

The five siege visual pilot FBXs and 21 checked maps are in
kit_full/lod_siege_01/staging_02. The ten base pieces use the
independent Siege 04 stage. See the [delivery record](delivery.md)
for both inventories. All 15 named meshes reimported
and passed the bounds, pivot, UV layer, material and topology
checks. The first stage identified four 40 mm tower crown
returns that had clamped opposing bevels. Stage 02 uses 18 mm
bevels on those returns and verifies zero tiny faces after
baking and reimport. The separate correction report records
the affected instances and checks.
