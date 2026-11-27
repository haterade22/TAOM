# Lessons from the Mirkwood baseline

## Resolve nested prefabs before counting affected scenes

A literal Mirkwood name search missed `taom_dwarf_town_01_forceatmo`. Its two
`dwarf_anvil_runic` placements expand to 32 Mirkwood mesh placements. Dale's XML contains 24 direct
placements, while four house prefabs contribute another 154. Count each explicit mesh component
and expand each prefab instance through the loaded definitions. Keep the showcase separate from
production counts. When a later review adds modules or alternate levels, declare that input scope.

## Compare the full material-split bounds with asymmetric pieces

One cooked metamesh can store several records for a single LOD. Union their bounds before comparing
to the FBX. The identity frame matched 115 of 136 tightly enough to pass a casual sample. The wall
lamp and eave extensions distinguish it from the 180 degree Z frame. Include an asymmetric control
and compare the reported translation as well as the dimensions when reusing a frame rule.

## Read body references as well as names

The FBX has a curved arcade column with no body of its own. Both of its prefab entries point to
the dirty column body. Preserve that distinction in a census. A missing same-name body and a missing
prefab physics reference describe different failures. For a collision change, check the named shape
in the Kit against the actual walk or opening.

## Check source LOD cost against the installed names

All 136 visual base objects end in `.lod0`. A parser that counts only unsuffixed names would report
zero base pieces. In the source 23 LOD3 objects carry at least the same number of triangles as their
LOD0. Geometry names and triangle counts can flag candidates for a runtime check. Verify actual LOD
selection and visual cost in the Kit or game before assigning an optimization budget.

## Remap imported texture paths for the review

The FBX carries a stale path for the vertical dirt decal and omits many usable image links. Use
the installed shared decal source under `Scenes/Decals`, and reconcile slot names with the local
map stems. Three exceptions are documented in [Materials](materials.md). The first Workbench pass
showed magenta decal strips on the root walks. Re-render in a lit view after resolving the source
and alpha, then inspect the walk and the vine before presenting the sheet.

## Keep palette placement and fit evidence separate

The installed showcase calls one palette prefab with 136 individually placed pieces. In the
cooked LOD0 bounding boxes only a pair of display vines came within 30 mm on one axis while
overlapping on the other two. Its position is not a snap specification. Derive any reusable joint
from the contact geometry, collision and access on the specific control. A 3 m wall face is a
useful starting measurement for wall and window joins.

## Set the art direction before choosing siege massing

The siege brief stated the required modules. I built a preliminary Blender study before
settling Mirkwood's visual language. Mike asked to reverse the order and gave permission
to design freely from the references. Use the held kit to spot overlap and downstream
scene use. Establish the stone and timber, true trim scale, colour and composition first,
then use the shared siege clearance requirements to plan pieces. Keep early studies out
of the approved design record until the owner reviews their look.

## Check the operating envelope around what fits

The two measured Dwarf tower pads are 6 x 6 m. Their machines and crew stand on the pads
and their ammunition sits on adjoining walls. Reusing only the deck dimensions would
miss the support those stations rely on. Check body, crew and ammunition together, then
test the moving arm and field-side visibility. A gate's visible arch throat likewise
differs from its animated clearance pocket.


## Inspect close-up joints and cut the hidden geometry

The first close-up timber peg sat above the tenon. The full view made it look
like a fastening, but its axis missed the joint. Cut the pocket and bore into
the beam and tenon, then sample the bore and the adjacent wood with separate
rays before placing the peg. Save a grazing or side view of each critical joint
so the owner can inspect the contact and the termination.

Lay out the stone sweep and the ornament on the same path. The close-up shows
whether the jamb meets the crown tangentially and whether relief stretches at
the spring. Check the opening with rays at several heights, including high
along the curved reveal. Use a smooth height attribute for lighting variation
inside the cut. Face-by-face shade assignments showed stair-stepped marks at
the outline in the first crown close-up.


## Use one leaf pitch in the sculpt and the atlas

The proposed 2.27 m U repeat would hold 10.27 repeats of the approved
221 mm leaf. The 2.210 m atlas holds ten. Sample the same leaf function
for the atlas and the sculpt. Map U by the strip's centreline length.
On a high-detail view, let the mesh carry the carved relief and use the
body normal for fine grain. Judge the trim normal on the simple strip.

## Check the stored normal in both environments

Write the engine candidate normal green down and keep its PNG raw. Flip
green at the Blender shader. Compare red and green against the measured
height derivatives and check each atlas band's U seam through smaller
mips. Checking the whole atlas can hide a defect in one narrow strip.


## Judge limestone on a close oblique view

The first Mirkwood stone looked even in the overview. Mike's jamb crop
showed the uniformly spaced, square-looking pits. Use a low-relief
limestone source for cut architecture and judge it at about a metre of
frame width. Place broader weathering near actual surfaces that collect
water. For the carved atlas, combine the stone normal with the existing
relief. Keep the same leaf pitch and plain stops.

A chamfer can also read as a hard colour band at the same distance.
Check the mesh shading under the same light before attributing every
band to the map.


## Cut the jamb seat as one block

The stacked base divided a simple structural transition into three horizontal
bands. Mike chose one dressed limestone block under each jamb. Establish the
seat height and full bearing footprint first, then read the softened edge,
stone face and damp line together in the same close view.

## Inspect capped trim from both sides

The first trim board ran the end-grain row across the full U range.
The individual cut faces now each use one declared atlas island and
preserve the cross-section ratio. The lintel close-up catches grain
rotating across a joint or an end cap being stretched to fill its UV box.
Keep wood grain along the post on its narrow sides as well as its face.
An extruded trim's side or cap needs UV area for its thickness. Sharing
the foreground UV on the side produced visible streaks in the stone
jamb close-up.

## Separate the profile from its fine grain

A formed stone rib already catches light across its section. The full
baked profile normal adds the same angle a second time. Use the trim
atlas on flat bands and the material's grain normal on real profiles.
The same height function can drive a detailed sculpt and its low-poly
normal. Compare them under one raking light when revising the pattern.
A shallow cut in the section gives a radial stone joint without breaking
the centreline UV. Reserve the fuller frieze for focal openings and
check the plain ribs and timber edges on the smaller window first.

## Measure the whole route after assembly

The tread centres and tower pad passed individually. A reload of the
placed model found 1.58 m between the rear pier and the lower landing
guard. Moving the landing edge outward 0.32 m gave 1.90 m of clear floor
and headroom across three sections. Measure the swept approach, both
landings and the neighbors in the saved assembly. Give a downward
corner test a small inset from the cut plane and test the exact port
separately from outside.

Blender's inactive scene can have unevaluated `matrix_world` values.
Read saved local transforms for unparented, unconstrained placement
empties. For the evaluated geometry and export, switch to the source
scene and update its view layer before reading world transforms.


## Place the shape before the setting

The supplied Rivendell guide compares true geometry from fixed
cameras, prints measured joins and gives the owner a short review
page. The first ravine render hid the screen and the route behind
oversized rock dressing even though the architecture itself passed
the route samples. Build a clean support section for art review.
Use the editor's terrain when placing the scene. Put the bridge
cut, the screen footing and the seated timber joint in their own
close cameras.

Compare a genuine layout change at the same framing. The second
ravine layout puts a repeated gallery in front of the stone screen.
Lighting comparisons use one layout. The route checker must
respond to a planted floor step and a displaced guard. Wood grain
on a fork follows its curved centreline, and handrail grain runs
along the rail even on its narrow face.


## Probe the cut plane separately from the lining

The first hall-port comparison looked 75 mm into each room and
found lining rails behind the floor-to-vault docking section. Those
rails are decorative and stop before the join. A narrower ray into
the true cap measured the common roof and retaining section. Check
the lining in a separate cutaway so a local difference has a clear
cause. Compare more than one repeat of a long bay and reject a
planted shift along the actual route.

## Mind bevels at small joints and folds

A 12 mm bevel on a 24 mm lantern backplate made the opposing
faces coincide at mid-depth. The authored shell was closed. Only
the evaluated mesh and FBX exposed the six zero-area faces.
Use a small flat between opposing bevels and verify both the loaded
mesh and the FBX reimport. This matters most on narrow brackets,
pins, caps and thin end returns.

For a clipped tower end, measure the actual return width after
the layout cuts. Four 40 mm returns kept the bevel widths of
their wider parent parts. The evaluated bevel clamped at 20 mm
and created 48 zero-area faces per detail level. At 18 mm a
4 mm land stays between the bevels. The corrected stage retained
the part bounds, UV layers and triangle counts and verified the
result after FBX reimport.

## Match the road to the feet and the door

A measured door width does not prove a usable approach. Place the
paths at floor datum, route around the projecting stair, and check
the clear lane to the landing. Use one paved surface at junctions.
Test a roof against the version of its room in the compound.
The vaulted interior receives timber ribs. Its exposed exterior
can use a separate continuous slate shell with its own precise seam.


## Keep a lower level smaller and at the right height

Dropping the merchant roof tiles showed the top of the bed below
them. A depth probe disagreed even though the silhouette check
passed. Restoring every tile kept the shape but raised the LOD6
triangle count above LOD3. Keep a continuous replacement surface
where the scale of the piece makes course relief important. Measure
both the outline and a first-hit depth statistic, then check that
the mesh counts decrease through the chain.

## Join simple surfaces in the saved assembly

Walk proxy evidence must use the placed pieces and their actual
transforms. Check the shared floor and guard profile from both
sides of the dock. On the bridge the supporting shell can be much
simpler than the dressed arch, while the clearance beneath it remains
a real opening. A planted 15 mm rise must fail the floor and cap
checks even when the lane still appears continuous on the sheet.

## Compare lighting from a locked camera

Changing the viewpoint makes it hard to distinguish a lighting
improvement from a framing change. Activate and evaluate the
source scene before copying a camera transform. Verify both the
lens data and the evaluated transform after saving. Fast previews
are useful for tuning the size and spacing of light pools. Use
matching views from the same renderer when judging shadow depth.

## Measure the trim where the UV ends

A clean atlas preview and a wrapped U seam do not measure V
bleed. Use the half-texel centre of each usable row edge and
weight the final bilinear taps against the source row boundaries.
Test end-grain island U margins and both V address modes. The
current eight-pixel gutters isolate ordinary row edges at 64 px
in the ideal filter. At 32 px the neighbouring bands already
affect the timber relief and the stone frieze.

## Verify collection placement after Append

Compare an origin instance with its source. A shelf can look right
even when the collection's offset drifts. Keep exact comparisons
for authored vertices, UV attributes and assignments. Also check
evaluated topology and physical transforms. Blender's modifier
evaluation changed interpolated UV values by up to 9.54e-7 after
Append while authored UVs stayed identical. Record the numeric
delta when an evaluated mesh's byte hash changes.

## Inspect gable courses at settlement scale

The first village roofs had staggered geometry, but the shallow
end joints and stronger course steps read as continuous panels.
Compare to a nearby surface at the same camera distance. Here
the granary canopy showed the scale of legible slate. Smaller
individual exposures, overlapping courses, and small dark end
faces resolved the gables while keeping the verge and ridge
profiles.

## Reopen copied scenes before rendering

A scene copied in a background Blender session can render successfully
while it has no persistent user. The first alternate lighting file
saved without Amber Alcoves. Keep the copied scene with a fake user.
Reopen the saved file, resolve every queued scene and camera by name,
and compare the camera framing before starting the full render.

## Start stone runoff at a ledge

Review weathering in a fixed close-up and a wider view. On the
ravine bridge, short gravity-aligned stains starting below the
drip ledge gave the moisture a visible source. Limit the mask
to the exposed ashlar faces and keep the limestone grain.

## Check both gate seats through the full swing

A render of closed leaves proves the centered seam. It cannot
show whether the leaves clear a jamb halfway open. Sample the
conservative envelopes at intermediate angles, check the open
lane and repeat the level-floor probe. The display leaves fold
away from the gate chamber and have separate pivots for each
seat. Keep the art checks tied to the observed display poses.

## Trace black patches to their mesh faces

A near-black patch across a fitted timber or stone joint can come
from coincident outer faces. Trace camera rays through the patch
and inspect both parts' face planes. At the hall dais, stepping
the returns and seating the secondary timber behind the main rib
removed the patches. Check the fit with the same Cycles camera
and exposure. Resample floor and headroom after the repair.
