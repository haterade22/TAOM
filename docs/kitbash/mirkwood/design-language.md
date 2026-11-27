# Mirkwood design language

First concept, 2026-11-26. Mike directed this round to settle architecture, colour and trim
before planning the siege blockout. The existing kit is a measured comparison and gives
scene-use evidence. The new architecture and motifs can be designed independently.

## Stone and timber architecture

Mirkwood's public buildings read as built thresholds in the forest. Stone is the weight
at ground level: retaining faces, walls, piers, entry reveals, steps, sills and bridge
springings. Recesses cut deep enough to hold shadow. Two or three carved ribs follow each
important opening and collect into the pier rather than stopping on the face. A flared
shaft base sits on a single dressed limestone block with a softened edge.

Wood puts roofs and covered walks over those stone routes. Use dark heartwood for posts,
carriers, arch braces, doors and screens. A brace splits from the post and meets the beam
at a visible joint. Timber sits in stone shoes where it reaches the ground. Dark bronze
belongs on small fittings. Warm light sits in individual sheltered bays, beneath a
roof or deep in a stone reveal.

The castle can have longer sheer wall runs between carved thresholds. The town can use
more timber at its upper levels and around court openings. Both use the same stone base,
shaft treatment and shadowed reveals. Vary bay width, length of plain wall, the depth of
a recess and the amount of timber in a grouping. Keep mating ends, walk surfaces, door
clearance and supports exact when they become kit pieces.

## Proposed material colours

These are sRGB design swatches. Read them in neutral light and again under a shaded
approach. Use larger surfaces to judge the stone and timber contrast before baking maps.

| Role | sRGB | Placement |
| --- | --- | --- |
| Curtain stone | `#68695C` | Main masonry, plinth and stone structure |
| Recess stone | `#383F37` | Back of blind arches, shadow cuts and sheltered stone |
| Worked stone | `#A6A68E` | Edge of a carved rib, jamb, sill or seat |
| Moss | `#626943` | Damp sills, shaded feet and selected joints |
| Heartwood | `#443429` | Structural wood and deep grain |
| Worked timber | `#77604B` | Bevels and carved faces |
| Door infill | `#283E3B` | Recessed panels and screens |
| Dark bronze | `#78664A` | Hardware, collars and lantern fittings |
| Lamplight | `#D9A15C` | Small pools at entries and in interior bays |

Use plain dressed stone for broad faces and concentrate carving at springing lines,
entrances and transitions to wood. Moss follows water and shade. Lamps read locally.
Use the smooth limestone material in the [texture study](textures.md).
Keep mineral runoff soft and local to joints, recesses and the plinth.


## Shared trim in the current kit

The [architectural trim record](trim.md) specifies the current six-band
limestone and heartwood atlases. The ravine screen and ceremonial hall use
the deeper bundled ribs and beech accents. Repeated streets and village
buildings use the plain profiles, with timber carving concentrated around
bearing joints. Ashlar carries the broad masonry faces. Dressed limestone
carries the profiles and single-block seats. Put dark heartwood along
grain on every structural piece, including sloped braces, and take cut
faces from the end-grain islands.

## Initial five-band concept

The first 1024 px sheet proposals are recorded below. Set U along the edge and reserve each V strip below.
Row coordinates are top-down and end-exclusive. Allow 8 px of extended edge on each side
of a row, leaving approximately 451 to 452 px/m across the usable bands. A full U repeat
covers 2.210 m along the band, with ten complete 221 mm leaf repeats. Use arc length around a curve and whole repeats
on a closed run. A stop, inside corner or end return gets its own geometry and a clean
termination. Separate repeatable stone and timber body maps carry broad faces.

| Stone row | Pixels | Member width | Profile |
| --- | --- | --- | --- |
| Arch reveal | 0 to 224 | 0.46 m | Rolled lip, shaded channel and narrow fillet |
| Bundled shaft | 224 to 416 | 0.39 m | Three unequal vertical beads |
| Carved fascia | 416 to 608 | 0.39 m | Low folded-leaf rhythm |
| Sill and coping | 608 to 832 | 0.46 m | Stepped water drip and smooth arris |
| Threshold return | 832 to 1024 | 0.39 m | Plain dressed return and corner stock |

| Timber row | Pixels | Member width | Profile |
| --- | --- | --- | --- |
| Beam face | 0 to 224 | 0.46 m | Long grain and bevelled edge |
| Forked rib | 224 to 448 | 0.46 m | Dark channels between paired edge ridges |
| Door and screen | 448 to 640 | 0.39 m | Narrow paired fillets for framed infill |
| Handrail edge | 640 to 832 | 0.39 m | Round grip with a shadow cut below |
| Joint collar | 832 to 1024 | 0.39 m | Plain timber seat for a separate bronze fitting |

The concept study holds both member samples, ten profile meshes and nine colour
swatches. The textured study applies the new stone and timber body maps to the
crafted members and previews the two 1K atlases along a straight run, a curve
and a plain stop. See the [textures](textures.md) for the files and checks.

## Carved detail and joinery

Use the two member studies in the local `kit_full/concept_02` folder for the
close-up standard. The stone study carries one moulding section from plinth to
crown and back down. The jamb-to-arch transition is tangent. Separate dressed
masonry courses use radial joints around the arch. Keep their thin joints
consistent and put the stronger shadow in the carved section.

The beech-leaf fascia is actual relief geometry. It has 46 complete repeats
at 221 mm pitch measured along the centre of the band. The runs pair from the
jambs toward a three-leaf crown. The finished
relief rises up to about 21.5 mm above its field. Leave a deliberate plain stop
at the seat and space the repeat along the curved path. Keep the raised detail
inside the surrounding ribs so the moulding reads at a distance.

Seat the timber braces in actual housings. Shape the shoulders and give the
pegs a single axis through the beam and tenon. Stopped edge carving dies into
the brace before it reaches the seat. In close-up, the edge bevel, grain
direction and metal collar should follow the shape of the member. Show a
grazing-light crop of the joint before it becomes a repeated kit part.

## Use across the kit

Start the civic route with the [ravine approach](approach.md) where the
terrain supports a crossing. The deep screen and flaring vestibule
compress the passage before the repeat [hall](hall.md). The court-facing
merchant gallery shows its timber structure. The lodge and grain store
use narrow screened openings and keep the larger opening at grade.
At the city boundary, the [siege kit](siege-kit.md) gives both compounds
the same gate and wall joins. Its bearing detail stays legible beside the
court architecture. See the [compound layouts](settlement.md) for the
connections through to the hall and housing.

[Reference study](reference-study.md) and [Mirkwood build record](../mirkwood-kit.md).
