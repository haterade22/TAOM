# Teardown: the building profile of the Rivendell kit, against the Gondor and Erebor sets (2026-11-26)

The record of one teardown: what a reference had that our build did not, measured. The techniques that come out of it
become entries of the knowledge store (`docs/features/knowledge-library.md`, issue #1613) when the store is on trunk;
until then the section "Defaults that follow" holds them. The asset job is in
[rivendell-siege-set.md](../rivendell-siege-set.md).

| | |
|---|---|
| **Part** | The kit's houses, halls and village buildings: the numbers every builder got (`KIT_PLAN.md` section 2), coded in `kit_full\src\tools\oneoff\rivendell_building_profile.py` (`bp`) |
| **Reference** | Not a tool run: two existing sets in `TAOM_Map\AssetSources\Scenes`, read only, in the files' own metres. (a) The control that the survey of 2026-11-25 measured: the six assembled Gondor buildings (`Gondor\buildings_taom\gondor_buildings_a.fbx`) and the three Erebor buildings (`erebor\buildings\*.fbx`). (b) The half that the survey listed as "references, not parts" and did not measure: 56 whole hand-modelled Gondor buildings (the count is UNVERIFIED) and 6 broken states in 26 files of `Gondor\buildings`, the Minas Tirith walls and gate files left out |
| **Measured against** | Before: no rule; the first house came after the survey. Now: the profile module, and the nine buildings of the kit set that the maintainer imported on 2026-11-26 (`kit_full\editor_set\kits`: `buildings\rivendell_house_kit.fbx`, `rivendell_hall_kit.fbx` and `village\rivendell_village_kit.fbx` for look B, `_variants\rivendell_variants_kit.fbx` for look A; 18 meshes) |
| **Files** | `kit_full\teardowns\_scratch_T5\` in the job's work folder `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell`: `t5_fbx.py` (the stage F pure FBX reader with model rotations and parents), `t5_measure.py`, `t5_sets.py`, `t5_list.py`; their outputs `m_control.json`, `m_shipped.json`, `m_ours.json`, `sets.json`, `list_*.json`, `m_shipped.log`, each with its arguments and constants. No Blender ran. The check of this teardown: `kit_full\teardowns\teardowns_check.json` |

The profile was written before the first house, from the survey's control. On 2026-11-26 at about 11:08 the
maintainer judged the buildings built on it: "When making buildings you are very precise. 0 or 1. Black or white."
The teardown, written 11:36 to 11:40, measured the control, the hand-made half and ours with one script.

## How the numbers were taken

Per building, on its triangles over Z 0.25 m (`t5_measure.py`): the bounds; `plan_fill`, the top view cover over the
bounds area (0.1 m cells, 0.2 m over 40 m); the macro roof pitch, the slope of the top view height map over a 1 m
baseline, in whole degrees, a pitch dominant when it holds 10 percent of the sloped area; the roof label from the
downhill sectors of 45 degrees that hold 5 percent (2: gable, 3 or 4: hip or pyramid, 5 or more: round or many faced;
flat when sloped cells are under 30 percent of the plan); skyline peaks of the front and side outline with 1 m
prominence; massing mirror, the share of cells whose height equals the mirrored height to 0.25 m on the better axis;
vertex mirror, the share of vertices with a mirror twin within 0.02 m. A set's silhouettes are the distinct tuples of
label, front peaks, side peaks and top over the longer side to 0.5. The labels are coarse: a dormer or porch can turn
a gable into "hip or pyramid" (our cottage, look B). The tolerances are this job's choices. The hand-made buildings'
grade is taken as Z 0, though some carry foundations to Z -6. The grid, storey, wall, door and window numbers of the
references are from the surveys `kit_full\survey\gondor.md` and `erebor.md` (2026-11-25), not from these scripts.

The verifier measured again and found the same base (0.00), vertex mirror (1.00) and object count (1) on 18 of 18
meshes, the same pitches (55 on 16, 70 on 2), the small house top (12.32 m) and the 6 `_dest` names. It could not
reproduce the count of 56 hand-made buildings: its byte scan found 109 name prefixes before grouping. It did not
measure again the control's numbers or the hand-made shape numbers. These numbers cannot be made again from the
repository: the scripts are in the work folder.

## What was measured

| Entry | The reference has | Ours before, ours now |
|---|---|---|
| `one-plan-grid-for-a-set` | A plan grid of 3.0 m in the Gondor parts and in Erebor | No rule. Now 2.2222 m, one texture tile (`bp.TILE`); footprints in whole tiles (the small house 5 by 3) |
| `storeys-from-one-module` | Storey 3.0 m (the Gondor hall 6.0); door height over storey 0.87 (Gondor) and 0.78 (Erebor); a one storey house 4.69 m (Gondor town house) and 4.80 m (Erebor) high. Top over the short plan side: assembled 0.59 to 1.98, hand-made (56, UNVERIFIED) 0.49 to 7.34 (median about 1.5) | No rule. Now storey 4.4444 m, the top storey 2.2222 m taller; door 0.54 of the 6.667 m wall of a one storey house; the small house 12.32 m in both looks; top over short side 1.32 to 3.53, median 1.59 |
| `wall-thickness-of-the-neighbour-sets` | Gondor box 0.50 m, panels with frames 0.55 to 0.57; Erebor 0.50 to 0.62 | No rule. Now 0.60 m (`WALL_T`) |
| `openings-sized-for-agents` | Doors clear: Gondor 1.20 by 2.61 m, Erebor 1.10 by 2.35. Windows: Gondor 0.50 by 1.05; Erebor slit 0.40 by 1.70, octagon 0.90, tall slit 0.50 by 1.90 | No rule. Now the house door 1.40 by 3.60 m over a 0.10 m threshold; the house lancet 0.80 by 2.90; 11 opening kinds, heads round (rise 0.50 of the width) or pointed (0.57 to 0.87) |
| `base-at-grade-no-skirt` | Lowest point: Gondor's six -0.01 m (the barracks -0.235 at its stair foot); Erebor -0.161 | No rule. Now 0.00 on 18 of 18 (`FOOT_Z` 0) |
| `export-frame-centre-door-minus-y` | Footprint centre at the origin, door side -Y, base at Z 0 | No rule. Now the same: X bounds equal and opposite on 18 of 18 |
| `four-objects-a-building` | 4 objects (mesh, `.lod3`, `.lod6`, `bo_`) on 9 of 9 assembled; `.lod3` 22 to 45 percent of the base | No rule. Now 1 object (level 0 only) a building, 18 of 18 meshes |
| `part-layer-before-buildings` | Gondor 330 parts, Erebor 145; the assembled buildings are made of them | No rule. Now 24 `rivendell_part_*` rows planned, 0 built (no such folder in `kit_full\build`) |
| (no default: a departure by decision) | Roof pitch: 27 degrees on 6 of 9 assembled, 45 on 3 (parts 26.57 and 45). Eave overhang: Erebor 0; Gondor UNVERIFIED | No rule. Now 55 on 16 of 18 and 70 on 2 (the tower house), `bp.ROOF_PITCH_DEG`; eave 0.25 m (`EAVE_OVER`). The lead's art direction H1, "tall and slender" |

**The half the survey did not measure.** The survey measured the nine assembled buildings, and they are as rigid as ours on every row below but plan fill. The
variety lives in the hand-made Gondor buildings, which the survey named and did not measure, and in the Gondor part
library's range: 20 intact wall panel families with curved panels and four arches, and 35 roof families with hip and
valley corners, curved roof parts and two circular roofs (counted by the teardown from
`E:\LOTRAOMAssets\_export\gondor\catalog\gondor_catalog_meshes.json`). The verifier did not check these numbers.

| Property | Gondor hand-made (56, UNVERIFIED) | Gondor assembled (6) | Erebor (3) | Ours look B (9) | Ours look A (9) |
|---|---|---|---|---|---|
| Roof labels | 4: hip or pyramid 19, round or many faced 19, gable 14, flat 4 | 2 | 2 | 2: gable 15, hip or pyramid 3 (18 meshes) | the same |
| Dominant pitch | 27 on 15, 45 on 15, none on 12 (curved), 14 more over 10 other values from 11 to 74 | 27, 45 | 27 | 55 on 16 of 18, 70 on 2 | the same |
| Distinct heights (1 m) | 26, from 7.7 to 110.0 m | 4 | 3 | 7, 10.7 to 25.6 m | 8, 8.5 to 25.6 m |
| Distinct footprints (1 m) | 39 | 6 | 3 | 9 | 8 |
| Silhouettes | 32 | 5 | 2 | 4 | 4 |
| Skyline peaks | 2 to 8 (2 on 35 of 56) | 2 to 7 | 3 to 9 | 3 on 16 of 18, 2 on 2 | the same |
| Plan fill | 0.371 to 0.979, under 0.90 on 27 | 0.749 to 0.948 | 0.894 to 0.970 | 0.766 to 0.956, under 0.90 on 7 (a porch before a rectangle) | 0.858 to 0.963, under 0.90 on 1 |
| No mirror axis (massing under 0.95) | 10 of 56, least 0.339 | 1 of 6 (the barracks and its stair, 0.925) | 0 | 0 of 18, least 0.996 | the same |
| Vertex mirror | median 0.70; 4 of 56 at 0.99 or more | median 0.81 over the 9 assembled | (with Gondor) | 1.00 on 18 of 18 | the same |
| Broken states | 6 `_dest` buildings | 0 | 0 | 0 | 0 |

**The maintainer's verdict on ours (2026-11-26, about 11:08, `painter_lab\STATE.md`):** "When making buildings you are
very precise. 0 or 1. Black or white. I think that is limiting your creativity when making buildings and other things.
There is a precision aspect of making a 3D model and then there is a creativity piece. The balance is key!"

## What ours has and the reference does not

- **Exact members:** vertex mirror 1.00 on 18 of 18 (mirror pairs gated to 0.5 mm, `KIT_PLAN.md` 4.4); whole tiles
  with the stone U in phase at every corner.
- **A written, tested opening rule** (half tile grid, a 0.9 m pier, clear of pilasters, the frame clearance to a
  course) that the Gondor and Erebor assemblers do not state: 81 test functions in `test_rivendell_building_profile.py`.
- **Fewer triangles at level 0:** 5,010 to 35,564 on our 18 meshes against 14,314 to 90,856 on the 9 assembled.
- **Two looks of every building** for the maintainer's choice, and one wall profile shared by the houses and the wall.

## Not taken into the kit's buildings

The four object template; the part layer; the whole second half: roof forms and pitches, asymmetry of the whole, a
spread of outlines, plans that are not rectangles, broken states, ornament placement. Above all the hand-made Gondor
buildings as a reference at all: the profile took its numbers from the assembled control, which is module driven
itself. A limit on any variety measure is the maintainer's word, not this session's.

## The properties this teardown measured

| Property | How it was measured |
|---|---|
| Size | Bounds over Z 0.25 m; top over the short plan side; distinct heights and footprints of a set to 1 m |
| Plan fill | Top view cover over the bounds area, 0.1 m cells |
| Roof pitch | Slope of the top view height map over a 1 m baseline; a pitch is dominant with 10 percent of the sloped area |
| Roof form | Label from the downhill sectors of 45 degrees that hold 5 percent of the sloped area |
| Skyline | Peaks of the front and side outline with 1 m prominence |
| Symmetry | Massing mirror (heights to 0.25 m, the better axis) and vertex mirror (a twin within 0.02 m) |
| Set variety | Distinct silhouettes (label, front peaks, side peaks, top over the longer side) over n buildings |
| Template | Objects per building, levels and their share of the base |
| Grounding and frame | Lowest point; X bounds about the origin |

## Defaults that follow

The technique files are written when the store is on trunk, one per slug, each with this record as its source. The
states are those of the library's ladder, judged on this job's trees, which are not in the repository, so no row is
above `instance`. `acts against`: a constant or a gate refuses what the reference has. The verifier's findings
(`teardowns_check.json`) replace this teardown's older claims where they differ. A `nothing` row becomes a `sentence`
when its file is written; its next names the step after that. `bp` and its tests are in `kit_full\src\tools\`. No
default follows from the roof pitch: 55 degrees is the lead's decision, and the teardown's reason for it ("a steeper
roof carries more of the height") argues for ours, not for the reference, with no number.

**What acts against variety today:** `bp.layout_problems` refuses a layout that is no mirror, and two tests hold that
(`test_a_layout_that_is_no_mirror_or_on_part_tiles_or_of_an_unknown_kind_is_refused` passes even without the mirror
clause, because `opening_layout` refuses first; `test_the_checker_catches_planted_faults` fails without it). One
constant `bp.ROOF_PITCH_DEG` sets every house and hall, and 35 fails `test_the_pitch_and_the_slim_eave_head`.
`bp.footprint(tiles_x, tiles_y)` makes a rectangle only. The eye checker checks "symmetry" (`KIT_PLAN.md` 4.4).

| Slug | Default | Mechanism | Where it lives today | State | Next |
|---|---|---|---|---|---|
| `one-plan-grid-for-a-set` | Every piece of a set stands on one plan grid in whole tiles | The stone texture stays in phase at every corner and parts join on grid lines: 3.0 m in both sets, 2.2222 m in ours | `bp.TILE`; `bp.footprint` refuses part tiles (`test_a_footprint_of_part_tiles_is_refused`); `layout_from_row(whole_tiles=True)`; `check_layouts` of `kit_full\plan\make_kit_plan.py` stops on a break | instance | A tool in the repository prints each piece's footprint in tiles |
| `storeys-from-one-module` | Storeys and wall heights are whole multiples of one module | Scale against a figure: the pieces of a set share one module; our module (4.4444 m) is the lead's art direction, not the reference's 3.0 m | `bp.STOREY`, `bp.wall_top_z`, `SLENDER_TOP_TILES`; `test_storeys_are_whole_storeys_over_the_ground_line_and_look_b_has_a_taller_top_storey` | instance | A tool in the repository prints door height over storey beside the references' 0.78 to 0.87 (ours 0.54) |
| `wall-thickness-of-the-neighbour-sets` | Wall thickness lies in the range of the sets beside ours | Reveals as deep as theirs: 0.50 to 0.62 m on the references, 0.60 ours | `bp.WALL_T` 0.60; no test pins it (0.90 passes all 81 profile tests) | instance | A tool in the repository prints a building's wall thickness beside the neighbour sets' |
| `openings-sized-for-agents` | Doors and windows follow one written opening rule with a clear size and a head rise | Agents pass and the opening shows the scale: reference doors 1.10 to 1.20 m clear, ours 1.40 by the decision of 2026-11-24 | `bp.OPENINGS`; `arch_problems` with `ARCH_MIN_RISE` 0.85 (`test_a_planted_low_head_is_found`; a door of 2.00 m fails 4 tests). Whether G9 of `KIT_PLAN.md` 4.5 runs is UNVERIFIED | instance | A tool in the repository prints each opening's clear size and head rise |
| `base-at-grade-no-skirt` | A building's lowest point is the ground line, with no skirt below it | It sits on the terrain: -0.01 m on Gondor's six, 0.00 on ours. The maintainer: "the floor sits on the terrain" | `bp.FOOT_Z` 0; `test_the_base_is_on_the_ground_with_no_skirt` (-0.2 fails it); probe G5 grounding to 0.01 m | instance | A tool in the repository prints the lowest point of each building export |
| `export-frame-centre-door-minus-y` | Export every building with its footprint centre at the origin and the door side to -Y | The Kit places every building one way: the references and ours on 18 of 18 | Only the docstring of `rivendell_building_profile.py` (line 34) states the frame; no constant or check enforces it. The 18 of 18 X bounds are a measured result | sentence | The frame gate G10 (`fbx_frame_compare.py`) runs on every building export |
| `four-objects-a-building` | A building ships as four objects: the mesh, `.lod3`, `.lod6` and a `bo_` body | A budget at distance and a collision body: 4 objects on 9 of 9 references, 1 on ours | `KIT_PLAN.md` 4.5 G6b; the level code of step 2d is in work, with no level step for the old houses | sentence | G6b runs on a building export |
| `part-layer-before-buildings` | Build a part layer first and assemble buildings from it | Variety by recombination with no new modelling: Gondor 330 parts, Erebor 145; ours 0 built | `KIT_PLAN.md` 1.8 (24 rows planned) | sentence | Build the first `rivendell_part_*` rows and assemble one building from them |
| `several-roof-forms-in-a-set` | A set holds several roof forms and pitches | Different roofs make a group read as many buildings: 4 labels on the hand-made Gondor buildings (56, UNVERIFIED), 2 on ours, one pitch on 16 of 18 | `bp.ROOF_PITCH_DEG` 55, one constant for every house and hall, pinned by `test_the_pitch_and_the_slim_eave_head`. The roof module has gable, gable end, dormer, porch gable, pyramid and spire, and no rule that varies them. A design draft of 11:30 plans a `roof_kinds` report | acts against | A pitch and a roof form per building, and the kit set check prints the set's distinct labels and pitches |
| `asymmetric-whole-exact-members` | A building group may be asymmetric as a whole (an off-axis stair, tower or wing) while its members stay exact | The measured property: 10 of 56 (UNVERIFIED) hand-made buildings have no mirror axis and their vertex mirror is 0.70 at the median; ours 0 of 18 and 1.00. "Grown against stamped" has no number; his words above name the effect | `bp.layout_problems` refuses a layout that is no mirror, held by the two tests named above; the eye checker checks "symmetry". The draft's `group_mirror_iou` 0.70 to 0.90 is a sentence | acts against | `layout_problems` accepts a layout named asymmetric, and the kit set check prints vertex mirror per building |
| `spread-of-silhouettes-in-a-set` | A set spreads its silhouettes, skyline peaks and heights | A skyline of different outlines reads as a town: 32 silhouettes over 56 (UNVERIFIED) hand-made, 4 over 9 ours; peaks 2 to 8 against 3 on 16 of 18 | Nothing. The draft's `silhouette_iou_max` under 0.75 and `skyline_peaks` are sentences | nothing | The kit set check prints silhouettes and skyline peaks for the set |
| `plans-that-are-not-rectangles` | Plans include L plans, courts and round towers | Silhouette from above and from the street: 27 of 56 (UNVERIFIED) hand-made under 0.90 fill, look A 0.858 to 0.963 | `bp.footprint(tiles_x, tiles_y)` makes a rectangle only; no gate or constant refuses another plan | nothing | A footprint that takes a plan other than a rectangle on the tile grid |
| `broken-states-of-buildings` | Key buildings of a siege town have a broken twin | A siege town shows damage: 6 `_dest` buildings beside their whole twins in Gondor, 0 of 9 ours | Nothing for buildings; the wall has its broken piece | nothing | The broken twin of one kit building, built and checked as the wall's broken piece is |
| `ornament-at-the-structure` | Gather ornament where the structure is: corners, storeys, bay joints, the door bay | Ornament at a focus reads as design, spread evenly as texture. UNVERIFIED as a number: it rests on the survey's recipes; no ornament density was measured | Nothing; every course runs the whole closed path of every building | nothing | Measure ornament density on the hand-made Gondor files |
