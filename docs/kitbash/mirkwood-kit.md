# Mirkwood kit build record

Started 2026-11-26 in `E:/repos/taom-kingdom-kits` on `feat/kingdom-kits`,
at HEAD `13977f7366a3606cee6e3b9aa7475d74ed9c639c`. The Codex work-claim check
returned `free: mirkwood-kit`.

## Baseline, 2026-11-26

- Saved the eight supplied screenshots under the local Mirkwood work folder, in filename order,
  as `ref_01` through `ref_08`. The local manifest maps each filename and SHA-256 to its source.
- Measured the installed FBX, texture sources, geometry package and prefab palette. Cross-checked
  triangle counts with the binary FBX reader and source bounds with the cooked package.
- Expanded scene prefabs recursively across the installed TAOM_Map definitions. Eleven scenes
  outside the palette use 21 different Mirkwood meshes in 436 placements. All 24 Mirkwood
  settlements point to Battania centre scenes.
- Generated a Blender inspection file with the original placement of all 136 showcase pieces
  and 1.8 m scale figures. The fixed-angle renders cover 22 families, with a representative
  piece and a scale figure for each angle. Blender preview materials display the source diffuse
  and relevant alpha under preview lighting. Cooked shader appearance and collision remain
  unverified in the editor.
- Wrote the [catalog](mirkwood/README.md), [coverage](mirkwood/coverage.md),
  [reference study](mirkwood/reference-study.md) and [lessons](mirkwood/lessons.md).
  The shared kit and feature indexes can pick up these links when the kit branches are reconciled.

## Siege brief and concept, 2026-11-26

Mike set the same kit requirements for castles and towns. Build intact and broken wall
pieces, a gatehouse with independent outer and inner gates, and defender siege stations.
He asked for a separate issue, opened as
[#1620](https://github.com/haterade22/TAOM/issues/1620). The measured Gondor, Dwarf and
game prefab examples are in the [siege controls](mirkwood/siege-controls.md).

I made an early Blender dimensional study before Mike directed the work back to the art
concept. That study remains in the local `kit_full/siege_blockout_01` folder. Its
massing and dimensions are provisional. The next decision is the concept, including
the Mirkwood stone and timber character, palette and trim profiles. Mike confirmed the
held kit is optional for the new design.

## Close-up craft pass, 2026-11-26

Mike asked for the precision and detail of a master-carved object. The editable
study in `kit_full/concept_02/view/mirkwood_carving_study.blend` has two scenes.
The stone threshold has a continuous dressed profile, stepped seats, radial
stone joints and a beech-leaf frieze carved in the mesh. The timber sample has
curved braces, cut beam pockets and pegs through the housed tenons.

The Blender geometry report records a 2.320 m clear opening, zero obstructions
across 176 sample rays and a 0.32 degree direction change across the first
sampled crown segment. Two peg bores were checked through both mating parts.
The 53 authored mesh objects passed the manifold-edge and zero-area checks.
The report and five renders sit beside the Blender file. These are high-detail
authoring studies. The trim bakes and kit meshes are later work.

## Texture pass, 2026-11-26

Mike approved the craft sample and asked for texturing. The
[textures](mirkwood/textures.md) document records the four 1K sets, their UV
scales and channel packing. Blender kept the approved geometry and gained
directional wood grain, dressed stone variation and a low damp tint. The trim
scene checks both atlases through straight runs, a bend and a plain stop.
The current review page shows the full pieces, close-ups and all twelve exports.

## Limestone revision, 2026-11-26

Mike marked the square speckling in a jamb close-up and requested smooth
limestone with weathering. The corrected stone body and atlas use the
joint-free *Smoothed Limestone Rock Surface* export from the local
Painter work. The colour grade, 1K exports and material checks are in
the [texture record](mirkwood/textures.md). Damp tint sits near the
plinth, and mineral runoff and recess shading follow the modeled
threshold. The new file opens with the finished arch and includes the
timber scene and the curved trim test.

## Single-block jamb feet, 2026-11-26

Mike approved the limestone finish and marked the three layers under the
jamb. Each stack is now one dressed limestone block with the same 183 mm
seat height and original lower footprint. The softened 9 mm arris and the
damp limestone material carry through the continuous face. The check compared
the remaining meshes and the 12 packed images against the source.

## Siege assembly and correction, 2026-11-26

Built four local siege assemblies as the art and geometry matured. The
latest has ten reusable pieces, a double gatehouse, four tower stations
in the castle layout, four forward stations in the town layout, and
town-side grade access in both. The breach now exposes its limestone
fill with a traversable rubble bed. The wall uses the approved limestone
with a new ashlar map, while the carved profiles and single-block jamb
seats keep the dressed stone. The separate route reload found and
corrected a tight section beside the tower crown pier. See the
[measurements and current files](mirkwood/siege-kit.md).

## Architectural trim, 2026-11-26

Mike made shared stone and timber trim the next priority. Built two
six-band sheets and checked them on a carved entrance, smaller town
windows, a housed timber lintel and curved returns. Preview passes
corrected the material exposure, formed profiles, stopped chamfers, sawn
ends and face grain. The 11.39 mm true beech relief and its atlas-normal
version are side by side under one light. Reopened the editable Blender
file and verified 45 trim UV specimens and 21 packed texture hashes.
The [trim record](mirkwood/trim.md) has the sheet bounds and reproduction
details.

## Ravine approach, 2026-11-26

Mike directed the next build from the Mirkwood references and linked
the Rivendell worked example. Read all nine local references and
checked their hashes. Built a 12 m bridge, 6 m landing, three-bay
stone screen, repeating heartwood gallery, guarded steps and a
bronze lantern using the shared material set. The screen-first and
gallery-first layouts give two ways to reach the recessed doorway.
The fixed views and dimensioned plan show the dock positions and
the scale of each piece. Both layouts passed the measured route and
guard checks. See the [approach record](mirkwood/approach.md).

## Hall, civic buildings and compounds, 2026-11-26

Worked headlessly while Mike was away. Added an adjustable length
[vaulted hall](mirkwood/hall.md), a merchant house, lodge, cottage,
granary, market stall and cistern. The buildings use the same
limestone and heartwood profiles as the approach. The merchant upper
storey remains open on curved forks. The lodge has closer framed
upper lights and a separately checked internal stair.

Assembled the [castle and town compounds](mirkwood/settlement.md)
behind their two existing frontages. Added a continuous set of roads
from the internal gate to the hall approach, building doors and stair
feet. The new compound views and cut plan use measured geometry and a
common scale. The corrected visual FBX stage contains 13 named meshes
and the 21 packed texture maps.

## Distance and walk proof, 2026-11-26

Built and reopened [four local distance pilots](mirkwood/distance-studies.md)
with LOD0, LOD3 and LOD6. Separate closed walk proxies match the real
bridge, gallery, vestibule and hall joins. The studies record the rejected gallery and roof variants and the
replacement slate surface. Five siege structures were checked in the
actual castle and town frontages at all three sampled levels. The
visual FBXs passed reimport after correcting a tower bevel.

## Updated render proof

A bounded local queue was started on 2026-11-26 to produce 22 Cycles
stills using the saved project cameras, 104 samples and six CPU
threads per Blender process. The queue validates each PNG and
regenerates the review index as the output arrives. Its latest files,
logs and status are in kit_full/morning_01. All six groups and 22
stills passed on the evening of 2026-11-26. The separate amber hall
queue failed when the new scene was absent from its saved Blender file.
The corrected file keeps the scene with a persistent user and opens
in Amber Alcoves. Both camera names resolved from the saved file
before the retry in kit_full/morning_lighting_02. Both renders passed. The two looks share
framing and 1760 × 1267 output dimensions. Compare them on
review_hall_light_01.html.

## Blender asset library

Packaged the 26 collections in six Asset Browser catalogs. A saved-file
reopen checked every instance origin, the evaluated positions and the 21
packed texture hashes. The library and usage notes are in kit_full/library_01.
See [reuse in Blender](mirkwood/settlement.md#reuse-in-blender).

## Render-led refinements, 2026-11-27

The overnight renders led to local studies of the materials and joinery. The ravine bridge
[runoff](mirkwood/approach.md#runoff-beneath-the-bridge-drip) starts
beneath the drip ledge. The town court
[flagstones](mirkwood/settlement.md#town-court-paving-study) add
joints to the existing grade. The [gate leaf study](mirkwood/siege-kit.md#timber-gate-leaf-study)
pairs braced outer leaves with quieter inner leaves. Each study
has a saved Blender file, matched view or display poses, and
independent clearance or material checks. The hall
[fitted-joint study](mirkwood/hall.md#fit-the-rear-timber-joints)
clears black patches at the fork roots and pier tops in matched
Cycles renders. The dais floor and headroom checks passed.

The village slate correction in kit_full/village_roof_02 also
reopened with all 412 slates and the fixed village camera.

## Current review

`review_now.html` opens the compound comparison, sized plans and the
other local reviews. It links the hall, town and village buildings,
approach, trim, distance studies and completed Cycles renders. The
[delivery record](mirkwood/delivery.md) lists the master, library, and
export stages. The [game verification steps](../features/mirkwood-kit.md#how-to-verify-in-game)
cover the Modding Kit import and scene tests pending for #1620.
