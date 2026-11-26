# Technique library

What an outside reference taught us about making an asset, and what is now our default. One entry per technique,
each with the number that shows it and with a plain statement of whether the default acts on the next build or is
still only a sentence. The procedure that fills this doc is section 7b of the
[`/blender-asset`](../../.claude/skills/blender-asset/SKILL.md) skill, the teardown of a reference tool's result.

The maintainer's order behind it, 2026-11-26, after the black Rhûn chariot set was imported:

> This is a perfect usecase for future research and other things like it. We struggled with a specific part, the
> horse helmet armor. Then we used tripo or another 3D tool to see what they did. At that point we should better our
> own workflow/processes with what the 3D tool did so that we do that by default moving forward. Like what did that
> tool do, that I didnt and why/how is it better. Let me add it to my own workflow/processes/toolbox. So in the
> future I do the same level of work and learn!
>
> Knowledge Management and improving our knowledge

## When a teardown is owed

- A reference for a part was made with an outside tool: an image to 3D model, a generated picture, a bought asset.
- The maintainer rejected the same part twice. Get a reference, then take it apart; a third round of patches comes
  after that. A reference from a paid tool is made under that tool's own rules for a run (cost, credits, what may
  be sent), or the maintainer makes it.
- Work that is not 3D (a code generator, a translation tool, a review result that solved what we could not): the
  same question is step 3 of [external-repo-adoption.md](../ai-includes/external-repo-adoption.md) ("Map novel vs
  duplicative"). The technique goes to the doc that owns the subject and the run gets a row in
  [adoption-history.md](../reviews/adoption-history.md). No such case is recorded yet.

A tool run with no row in the teardown log below is not closed. In a teardown the tool's mesh is a reference and
is never copied into the asset; when a tool's mesh is to become the asset, `/prep-tripo-prop` owns that work.

## How to read an entry

Each technique row says what the reference has, what our build had before the reference and has now, the
mechanism that makes the reference's way better (light, silhouette, rhythm, cleanliness, fit), and where the
default lives. The last column says how the default acts today:

- **code**: a shared function or parameter builds it;
- **gate**: a check fails without it;
- **report**: a build prints the number;
- **sentence**: only this doc says it. A sentence row names what would make it act.

"Not built" on an approved asset means the asset stays as the maintainer approved it. The default is for the next
part of that kind, or for his word. A number marked "the session's target" is the session's choice from the
reference, not his decision.

## Teardown log

| Id | Date | Part | Reference | Measured against | Files | Rows |
|---|---|---|---|---|---|---|
| T1 | 2026-11-26 | Neck bands and head shell of horse armour 5 (the black Rhûn chariot set) | Two image to 3D models (Tripo, `tripo-v3.1`, 50 credits each), made on 2026-11-25 at 13:40 to 13:46 from the concept pictures `01_black_neck_front.png` and `02_black_neck_rear.png` | Armour 5 `v16` (the last build before the reference pictures of 10:23; the `v17` trials followed the pictures and were not measured) and `v18ay` (in set v27, which he approved and imported) | `out\tripo_in\_teardown\` in the chariot work folder under `E:\LOTRAOMAssets\_auto_workspace`: the probe `td_probe.py`, the reports `t01.json`, `t02.json`, `o_v16.json`, `o_v18ay.json` and their `_crest` files, `ends_*.json`, the sections `x_*_band.png`, `x_*_head.png` and `*_crest_zoom.png` | L1 to L11 |

T1 came a day late. On 2026-11-25 the build followed concept picture 01, which he approved ("LOOKS AWESOME!"), and
of the two models only the preview pictures were read. The models were first opened and measured on 2026-11-26,
after the set was approved. That is why several rows below say "not built".

## Techniques

Lengths on the two models are model units read as millimetres. One landmark, the head length from the nose tip to
the throat corner (0.3686 m on our horse), gives 1.026 m per unit for model 01 and 1.012 for model 02, so at our
horse's size the model lengths are 1 to 3 percent larger than written here. The measurer's own estimate, not
tested, is that a length from one landmark holds to about one part in ten. The ratios do not depend on the scale.
Crest numbers are from one section along the top of the neck. A spread is the standard deviation over the mean.

| Id | Technique | The reference has | Ours before, ours now | Why it is better | Our default, and where it lives | How it acts today |
|---|---|---|---|---|---|---|
| L1 | An even rhythm in a stack of bands | Model 01: on the crest the ten beads (the collar's edge and nine more) stand 75.4 to 83.3 units apart, a spread of 0.031 over the nine pitches (0.033 without the collar's). The eight bands above the collar tilt a steady 2.0 to 4.2 degrees (the collar 8.7) | `v16` (eight plates, no collar): 75.7 to 90.5 mm, spread 0.068, tilt -7.4 to +3.8 degrees (the sign changes). `v18ay` with its collar: 51.0 to 72.7 mm, spread 0.104, tilt 8.7 to 23.5 degrees; without the collar 55.0 to 72.7 mm, spread 0.087, tilt 8.7 to 17.3 | Rhythm: the eye reads equal spacing and an equal tilt as made, and a changing tilt as warped | Bands of equal length on one smooth field, each lapping the next by the same step (`build_lames` in `rharmb_crinet.py`). The session's target: a pitch spread of 0.05 or less on the crest | **code** for the method, in a shared module that only armour 5 calls. **sentence** for the target: no build prints the spread (today a manual run of `td_crest_ours.py` with the band island ids). It acts when the neck build's summary prints the spread and the tilt and a gate holds a limit. `v18ay` misses the target and is less even on the crest than `v16`. Not built: he approved the set |
| L2 | A round bead on every free edge | A bead in real geometry on every lower edge and round the band ends. Model 01: radius 5.6 to 6.1 units, 230 to 300 degrees of arc, diameter 0.147 of the pitch. Model 02: radius 5.0 to 7.6 (three poor fits left out), 100 to 300 degrees of arc, diameter 0.167 of the pitch | `v16`: a square edge 4.4 to 8.5 mm thick with a kink. `v18ay`: a flat plate of 3.5 to 4.1 mm with a short lip; the bead is a height stripe in the normal map under 10.9 to 16.1 mm of painted gold | Light: a round section has normals over more than 180 degrees, so one bright line shows on every band from any view. At the outline and at grazing angles a bead in the normal map goes flat | A bead on every free edge of a rigid band or shell. In geometry where the triangle cap allows it; else one lifted lip row for the outline and the bead as height in the map: `BAND_PROTO["H"]` in `rharmb_design5.py`, which builds mode N of `rharmb_bands.py` with the finer rounded end of mode G, painted by `paint_lacquer` in `rharmb_paint.py`. Measured cost on the neck piece: 1,839 triangles with the bead in geometry, 1,003 with it in the map. Width in code: `bead_w` 0.013 m, about 0.21 of `v18ay`'s mean pitch, the width he approved on the build | **code**, in armour 5 only, as the map version by his choice of 2026-11-25 ("Rounded ends, rim in normal map"). The models' 0.147 to 0.167 is a note for a next piece, not his word and not in code. `paint_plate`, the look of the gold lames of armour 4, has no bead |
| L3 | A closed lap | Almost no air under a band's lower edge. Model 01: the bead's centre stands 3.8 to 8.6 units off the fitted line of the next band against a radius of about 5.8, so at most about 3 units are clear; the step between two bands is 4.6 at the median. Model 02 steps about 19 | `v16`: a lap gap of -10.5 to +5.5 mm, so lower edges cut into the next plate. `v18ay`: 10.2 to 19.1 mm of air under each lower edge | Cleanliness, and one thin shadow line under each bead. An air gap shows as a dark slot from a low or side view | The session's target for a band stack, from model 01 (the model of the picture he approved): a tilt of 5 degrees or less and an air gap no larger than the bead's radius, on the crest section. A stack that must slide over a bending neck keeps a gap, and then has a liner under it (L7) | **sentence**. It acts when the neck build prints the air gap and the tilt (with L1). Not built: armour 5's bands are rigid plates that stand 12 mm over the next one (`LAME_STEP`) so the neck can bend under them. Open: whether a closer lap passes the pose gates |
| L4 | A smooth outline | Model 01: neck edges of about 1.2 units; 4.6 percent of the neck's faces meet a neighbour at more than 30 degrees | `v16` 77.0 percent, `v18ay` 75.9 percent; about six straight facets on half a hoop | Silhouette: a faceted hoop reads as low detail at the outline | Spend columns where the outline shows (the crest, the band ends); carry the normals of LOD0 to the far levels (`rharmb_lodnormals.py`, run for armour 5 only) | **code** for the normals, armour 5 only. **sentence** for the outline: no report holds the facets on half a hoop or the share of faces over 30 degrees. Bound by the cap of 11,300 triangles on one horse |
| L5 | Band ends that taper, with the bead round them | On seven mid bands of model 01 the end narrows over about half a band width (an eighth gave no valid measure, three at the poll are square), and the bead runs round it | `v16`: clipped corners. `v18ay`: 7 of 11 ends are a half disc, 3 measure square, the collar's is longer | The end line: the row of ends reads as one drawn line | A rounded end as a small grid with a least-worst triangulation and no fan (`_cap_grid` in `rharmb_bands.py`), the same on both sides. An end is lifted only over parts it covers (`end_lift`, the end rule of `v18ay`) | **code**, in armour 5. A taper longer than a half disc is not built |
| L6 | One head shell | One continuous shell with a bead rim (radius about 6.6 units) and eye rings as raised flanges that stand 33 to 40 units out. These three numbers were read off the section `x_t01_head`; no kept script makes them | `v16`: six pieces. `v18ay`: one open shell with a turned lip, the bead in the map, two eye and two ear openings | Silhouette, and the face reads as one piece | One shell on a chart, its outline a smooth closed curve, its openings analytic curves (ellipse, circle), straps under its rim (`rharmb_shell.py`) | **code**, in armour 5; he approved the head ("That looks fantastic"). Raised eye flanges are not built |
| L7 | Armour that closes onto the body | The bands are the surface, so no skin shows between them | Ours are rigid bands over a neck that bends | No skin between or through plates, from any direction | A matt liner under rigid parts that move apart (`build_liner` in `rharmb_bands.py`), and the skin view gate (`rharmb_skinview.py`, 85 directions, for armours 4 and 5; `rharm_skinview.py`, 64, for armours 1 to 3) | **code** and **gate**, in armour 5. Armour 3 has a lining. Open in armours 1, 2 and 4 |
| L8 | One smooth field per part | A regular surface: 4.3 percent of all faces of model 01 meet a neighbour at more than 30 degrees | `v16` pushed each grid vertex off the skin on its own (`enforce_grid`), which gave the steps and notches he rejected: 53.7 percent. `v18ay`: 45.2 percent | Cleanliness: one smooth cause gives parallel edges and equal widths | Build the need once (skin, clearance, the lift over parts already built), make it mirror-symmetric, take a smooth upper envelope, place the geometry on it, and move no vertex afterwards (`envelope` in `rharmb_crinet.py`, the height field of `rharmb_shell.py`) | **code** where it is called, which is armour 5. The stock plate builders (`plate` in `rharmb_designs.py` and `rharm_designs.py`) still call `enforce_grid`, so the old way is the default of armours 1 to 4. It acts for them when their neck and head builders call the field |
| L9 | Symmetry, measured | Mirror error of the neck of model 01: 0.46 units at the median, 1.05 at the 95th percentile | `v18ay` neck: 0.00 mm. `v18ay` head shell: 0.92 mm at the median and 12.65 mm at the 95th percentile | A part on the centre line reads as wrong when its halves differ (his correction of 2026-11-25) | Symmetry by construction (the larger of both sides on a field, the mean of both sides on a found feature) and a mirror error in the build report | **code** and **report** for the neck lames (`mirror_mm`). **sentence** for every other centre-line part: the head shell prints none, and its 12.65 mm has no known cause, an open check |
| L10 | Ratios from a logged probe | A reference gives ratios, not lengths: bead to pitch, step to pitch, tilt | The picture probe of 2026-11-25 (a bead of 0.2 of the pitch) could not be repeated: its columns were not logged, and runs at 26 columns on both pictures gave uneven values. The models give 0.147 and 0.167 | A number nobody can run again is a guess, and a width in the build came from it | Scale the reference by one named landmark, measure ratios with a script kept beside the result, and let the script write its arguments into its output | **sentence**. Of T1's scripts only `td_probe.py` writes its arguments; the band island and component ids of the others are in the research report only. It acts when one general probe is in `tools/blender` |
| L11 | Two prototypes before a costly technique | (our own method, kept) | Two neck prototypes at LOD0 behind a switch, the bead in geometry and the bead in the map, each measured for triangles and gates and shown beside the reference from the reference's camera; the maintainer chose | The cost of a technique is known before it is built into six LODs | For a technique with a real triangle cost: two prototypes at LOD0, one sheet from the reference's camera, his choice | **sentence**, done once (the neck of armour 5). It acts as a line of the builder's brief |

## What ours has and the reference does not

Measured in T1:

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

## Not yet taken from T1 into armour 5

Set v27 is approved and imported. These are for the next neck or head piece, or for the maintainer's word on this
one: the crest numbers of L1, the closed lap of L3, the outline of L4, the longer taper of L5, the eye flanges of
L6, and the check of the head shell's mirror error in L9.

## The property list for a teardown

Start with what the eye sees in the reference, and name a property a script can measure for each thing. T1's list,
for a stack of bands and a shell, is below; another kind of part (a broken wall face, a tracery) needs its own
rows, for example the spectrum of a relief, the width of a bar and its crossings.

| Property | How T1 measured it |
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

## Open for the procedure

- **No gate proves that a teardown ran.** The hole is a tool run with no row at all. The gate that closes it: every
  run in the run record of `docs/features/tripo-cli.md` dated 2026-11-26 or later names a teardown id that exists
  in this log, or says `none:` with a reason; `tools/lint_docs.py` checks it. That doc is in the kingdom kits
  job's worktree today and not on trunk, so the column and the check are owed together with that job.
- **Paths that still miss section 7b:** a builder subagent cannot invoke a skill, so the session that briefs it
  puts the technique rows into the brief; a model the maintainer makes himself and drops into a work folder; a
  work folder outside the repo, where no path rule loads.
- **The probe is not in the toolbox.** T1's scripts are in the chariot work folder and are specific to bands. One
  general probe in `tools/blender` (sections, circle fits, pitch, mirror error, arguments written into its output)
  is owed.
- **The rows marked "sentence"** act only when their report line or gate exists. First: the crest numbers of a
  neck build (L1, L3).
- **More cases.** The kingdom kits job has Tripo runs with no teardown yet (a rubble heap, a torn wall core) and two
  references that are not tool runs (a railing tracery from a licensed kit, the building profile of two existing
  kits), as that job reports. Planned: that job adds its rows in this
  format once this procedure is in its worktree.

## Key Files

| File | Purpose |
|---|---|
| `.claude/skills/blender-asset/SKILL.md` section 7b | The teardown steps |
| `docs/features/blender-asset-gates.md` | The art checks and the reference analysis template that section 7 of the skill uses |
| `docs/features/chariot.md` | The asset job of T1 |
| `docs/reviews/lessons/build-tooling-workflow.md` | The lesson of 2026-11-26 on a reference that was matched and not taken apart |
