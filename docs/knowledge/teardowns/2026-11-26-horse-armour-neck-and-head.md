# Teardown: the neck bands and head shell of a horse armour (2026-11-26)

The record of one teardown: what a reference had that our build did not, measured. The techniques that came out of
it are entries of the [knowledge store](../../features/knowledge-library.md); each names this record as its source.
The asset job is in [chariot.md](../../features/chariot.md), and the art checks a build passes before it is shown
are in [blender-asset-gates.md](../../features/blender-asset-gates.md).

| | |
|---|---|
| **Part** | The neck bands and the head shell of horse armour 5, the armour of the black Rhûn chariot set |
| **Reference** | Two image to 3D models (Tripo, `tripo-v3.1`, 50 credits each), made on 2026-11-25 at 13:40 to 13:46 from the concept pictures `01_black_neck_front.png` and `02_black_neck_rear.png` |
| **Measured against** | Armour 5 `v16`, the last build before the reference pictures of 2026-11-25 (file times 10:22, logged at 10:23), and `v18ay`, the build in set v27 that the maintainer approved and imported. The `v17` trials followed the pictures and were not measured |
| **Files** | `out\tripo_in\_teardown\` in the chariot work folder under `E:\LOTRAOMAssets\_auto_workspace`: the probe `td_probe.py`, the reports `t01.json`, `t02.json`, `o_v16.json`, `o_v18ay.json` and their `_crest` files, `ends_*.json`, the sections `x_*_band.png`, `x_*_head.png` and `*_crest_zoom.png` |

The teardown came a day late. On 2026-11-25 the build followed concept picture 01, which the maintainer approved
("LOOKS AWESOME!"), and of the two models only the preview pictures were read. The models were first opened and
measured on 2026-11-26, after the set was approved. That is why several entries say that the approved armour does not
have what the reference has: the armour stays as he approved it, and the default is for the next part of that kind,
or for his word.

## How the numbers were taken

Lengths on the two models are model units read as millimetres. One landmark, the head length from the nose tip to
the throat corner (0.3686 m on our horse), gives 1.026 m per unit for model 01 and 1.012 for model 02, so at our
horse's size the model lengths are 1 to 3 percent larger than written here. The measurer's own estimate, not tested,
is that a length from one landmark holds to about one part in ten. The ratios do not depend on the scale. Crest
numbers are from one section along the top of the neck. A spread is the standard deviation over the mean.

These numbers cannot be made again from the repository: the probe and the builds are in the asset work folder. An
entry's evidence row names this record as its method for that reason.

## What was measured

| Entry | The reference has | Ours before, ours now |
|---|---|---|
| [band-stack-even-rhythm](../techniques/band-stack-even-rhythm.toml) | Model 01: on the crest the ten beads (the collar's edge and nine more) stand 75.4 to 83.3 units apart, a spread of 0.031 over the nine pitches (0.033 without the collar's). The eight bands above the collar tilt a steady 2.0 to 4.2 degrees (the collar 8.7) | `v16` (eight plates, no collar): 75.7 to 90.5 mm, spread 0.068, tilt -7.4 to +3.8 degrees (the sign changes). `v18ay` with its collar: 51.0 to 72.7 mm, spread 0.104, tilt 8.7 to 23.5 degrees; without the collar 55.0 to 72.7 mm, spread 0.087, tilt 8.7 to 17.3. The session's target, not his: a pitch spread of 0.05 or less on the crest. `v18ay` misses it and is less even on the crest than `v16`. No build prints the spread: it is a manual run of `td_crest_ours.py` with the band island ids |
| [bead-on-every-free-edge](../techniques/bead-on-every-free-edge.toml) | A bead in real geometry on every lower edge and round the band ends. Model 01: radius 5.6 to 6.1 units, 230 to 300 degrees of arc, diameter 0.147 of the pitch. Model 02: radius 5.0 to 7.6 (three poor fits left out), 100 to 300 degrees of arc, diameter 0.167 of the pitch | `v16`: a square edge 4.4 to 8.5 mm thick with a kink. `v18ay`: a flat plate of 3.5 to 4.1 mm with a short lip; the bead is a height stripe in the normal map under 10.9 to 16.1 mm of painted gold. Measured cost on the neck piece: 1,839 triangles with the bead in geometry, 1,003 with it in the map. Width in code: `bead_w` 0.013 m, about 0.21 of `v18ay`'s mean pitch, the width he approved on the build. The bead in the map is his choice of 2026-11-25 ("Rounded ends, rim in normal map"). The models' 0.147 to 0.167 is a note for a next piece, not his word |
| [band-stack-closed-lap](../techniques/band-stack-closed-lap.toml) | Almost no air under a band's lower edge. Model 01: the bead's centre stands 3.8 to 8.6 units off the fitted line of the next band against a radius of about 5.8, so at most about 3 units are clear; the step between two bands is 4.6 at the median. Model 02 steps about 19 at the median of its nine steps over 1 unit (three more are near 0) | `v16`: a lap gap of -10.5 to +5.5 mm, so lower edges cut into the next plate. `v18ay`: 10.2 to 19.1 mm of air under each lower edge. Its bands are rigid plates that stand 12 mm over the next one (`LAME_STEP`) so the neck can bend under them. The session's target, not his, from model 01: a tilt of 5 degrees or less and an air gap no larger than the bead's radius, on the crest section. Open: whether a closer lap passes the pose gates |
| [outline-geometry-where-it-shows](../techniques/outline-geometry-where-it-shows.toml) | Model 01: neck edges of about 1.2 units; 4.6 percent of the neck's faces meet a neighbour at more than 30 degrees | `v16` 77.0 percent, `v18ay` 75.9 percent; about six straight facets on half a hoop. Bound by the cap of 11,300 triangles on one horse |
| [band-end-rounded-and-level](../techniques/band-end-rounded-and-level.toml) | On seven mid bands of model 01 the end narrows over about half a band width (an eighth gave no valid measure, three at the poll are square), and the bead runs round it | `v16`: clipped corners. `v18ay`: 7 of 11 ends are a half disc, 3 measure square, the collar's is longer. A taper longer than a half disc is not built |
| [head-shell-one-piece](../techniques/head-shell-one-piece.toml) | One continuous shell with a bead rim (radius about 6.6 units) and eye rings as raised flanges that stand 33 to 40 units out. These three numbers were read off the section `x_t01_head`; no kept script makes them | `v16`: six pieces. `v18ay`: one open shell with a turned lip, the bead in the map, two eye and two ear openings. Raised eye flanges are not built |
| [liner-and-skin-view-under-moving-plates](../techniques/liner-and-skin-view-under-moving-plates.toml) | The bands are the surface, so no skin shows between them | Ours are rigid bands over a neck that bends, with a matt liner under them. The skin view gate uses 85 directions for armours 4 and 5 (`rharmb_skinview.py`) and 64 for armours 1 to 3 (`rharm_skinview.py`). Armour 3 has a lining; open in armours 1, 2 and 4 |
| [one-smooth-field-per-part](../techniques/one-smooth-field-per-part.toml) | A regular surface: 4.3 percent of all faces of model 01 meet a neighbour at more than 30 degrees | `v16` pushed each grid vertex off the skin on its own (`enforce_grid`), which gave the steps and notches he rejected: 53.7 percent. `v18ay`: 45.2 percent. The stock plate builders (`plate` in `rharmb_designs.py` and `rharm_designs.py`) still call `enforce_grid`, so the old way is the default of armours 1 to 4 |
| [centre-part-symmetry-by-number](../techniques/centre-part-symmetry-by-number.toml) | Mirror error of the neck of model 01: 0.46 units at the median, 1.05 at the 95th percentile | `v18ay` neck: 0.00 mm. `v18ay` head shell: 0.92 mm at the median and 12.65 mm at the 95th percentile, with no known cause |
| [reference-ratios-from-a-logged-probe](../techniques/reference-ratios-from-a-logged-probe.toml) | A reference gives ratios, not lengths: bead to pitch, step to pitch, tilt | The picture probe of 2026-11-25 (a bead of 0.2 of the pitch) could not be repeated: its columns were not logged, and runs at 26 columns on both pictures gave uneven values. The models give 0.147 and 0.167. Of this teardown's scripts only `td_probe.py` writes its arguments; the band island and component ids of the others are in the research report only |
| [two-prototypes-before-a-costly-technique](../techniques/two-prototypes-before-a-costly-technique.toml) | (our own method, kept) | Two neck prototypes at LOD0 behind a switch, the bead in geometry and the bead in the map, each measured for triangles and gates and shown beside the reference from the reference's camera; the maintainer chose |

## What ours has and the reference does not

- **Triangles:** `v18ay` is 11,130 at LOD0 for the whole armour of one horse (cap 11,300). Model 01 is 1,914,035
  for a bust and its armour; its neck part alone is 490,061, about 255 times our neck piece of 1,922.
- **LODs and skin weights:** ours has six levels and 16 vertex groups. The models have neither.
- **A garment apart from the body:** ours is 830 islands that stand off the horse (the least band distance is
  6.7 mm). The models are one island fused to the mannequin, with no inner face and no body under the armour.
- **Clearances and poses:** `v18ay` has 0 cart to armour pairs in five neck poses, alone and in set v27 (whose gate
  table is held on four other rows). The models have no cart and no rig.
- **Exact symmetry where it is designed:** our neck bands mirror to 0.00 mm.
- **The engine's texture set:** ours has `_d`, `_n` and `_s` maps at 2048. The models have colour, normal and one
  packed map that need a conversion, and their colour map may hold baked light (not checked).

## Not taken into the approved armour

Set v27 is approved and imported. These are for the next neck or head piece, or for the maintainer's word on this
one: the crest numbers of the even rhythm, the closed lap, the outline, a taper longer than a half disc, the eye
flanges, and the check of the head shell's mirror error.

## The properties this teardown measured

A teardown starts with what the eye sees in the reference and names, for each thing, a property a script can
measure. This list is for a stack of bands and a shell. Another kind of part (a broken wall face, a tracery) needs
its own rows, for example the spectrum of a relief, the width of a bar and its crossings.

| Property | How it was measured |
|---|---|
| Rhythm | Pitch of repeated members on one section, least to most, and its spread; the tilt of each member |
| Edge profile | A circle fit to each free edge on the section: radius, fit error, degrees of arc; its width against the pitch |
| Lap | The air under a member's lower edge, or the step between two surfaces |
| End shape | The width of a member's end at 0.1, 0.25 and 0.5 of its width from the tip, against a half disc |
| Outline | Edge lengths, and the share of faces that meet a neighbour at more than 30 degrees |
| Symmetry | Mirror error at the median, the 95th percentile and the most |
| Stand-off | Distance from the part to the body under it |
| Structure | Islands, closed or open shells, one surface or separate parts, thickness |
| Detail carrier | What holds the fine detail: geometry or the maps; texel density |
| Budget | Triangles of the part and of the whole, LODs, skin weights |
