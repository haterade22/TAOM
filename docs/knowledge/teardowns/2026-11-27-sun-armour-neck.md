# Teardown: the neck bands of the sun horse armour (2026-11-27)

The second record of the neck bands of a horse armour, and the case for it: the maintainer rejected one neck twice in a
day. It follows the first record, [2026-11-26-horse-armour-neck-and-head.md](2026-11-26-horse-armour-neck-and-head.md),
and uses its models, its properties and its method; read that record for how the numbers of the reference were taken.
The techniques are entries of the [knowledge store](../../features/knowledge-library.md); the five this record
touches name it as a second source. The asset job is in [chariot.md](../../features/chariot.md), "His second look at
the lacquer and sun sets".

| | |
|---|---|
| **Part** | The neck piece of horse armour 2, the armour of the sun chariot set (set C) |
| **Trigger** | The maintainer's second rejection of this neck on 2026-11-27. At about 09:13, of a Kit picture of set C from above with the red cloth through the lames circled: "Same, pull the neck armor UP until the clipping with the red cloth is gone." At 17:10, of the repaired neck in a Kit picture with five regions circled: "this is jacked up, probably needs triplo to fix it as it seems like it is overlapping instead of being one piece. Like its multiple pieces put together" |
| **Reference** | The neck of the black set (armour 5 `v18ay`), which he held up at about 17:25 in a close picture taken from the fastening page, wide black bands with a gold rim, each one clean piece: "I think they should be like this or one piece , in my opinion or your best recommendation". The measured models of the first record (models 01 and 02) stay the numeric reference. One new Tripo job (Studio MCP, text to model, `HD v3.1-20260211`, no texture, task `01a21f4a`, credits not read) finished at 17:16 (23:16:17 UTC by the server's stamp, 17:16 on the records' clock), was read by the session at 17:27, after his pick, and is one shell with lengthwise ribs and a rolled rim at both ends, not the eight broad bands the prompt asked for: `sun_neck_ref_01.glb` (4,240,368 bytes, sha256 `749393fa`) in the work folder's `out\tripo_in\03_sun_neck\`. Only its preview picture was looked at, for the rolled rim; no lane opened or measured the model |
| **Measured against** | Ours before: armour 2 `v22` as installed in set C (`rhun_war_chariot_c.fbx` sha256 `b14669a6`, the maps `_d` `64157881`, `_n` `76b78c14`, `_s` `9da66919`). Ours now: armour 2 `v23e` (FBX `a2e5dabc`) in set C `v30` (FBX `9b72a9be`, 6,509,132 bytes; maps `_d` `13412c4e`, `_n` `eea3ea7a`, `_s` `c6f5adc7`), built by the new module `rharm2_neck23.py` (sha256 `89f8eb73`) and `rharm_design2.py` (`a8cb2a84`). Three builds between them were measured as well: `v23a` (first full build; armour FBX `d143d311`, set `v27` `1f12d669`), `v23b` (third build; armour `5e502921`, set `v28` `4a7de079`, pictures and probes only) and `v23c` (the build the first check refused; armour `ead9e700`, set `v29` `36ed0c7f`) |
| **Files** | In the chariot work folder: the lane folder `out\armours\2_sun\_work\lane22` (the measure `neckm.py`, its reports `m_v22_lod0.json` and `m_v22_lod1.json`, the colour probe `k\cx_vis.py`, `m3_before_a1.json`, `m3_after_a1.json`); the lane record `out\status\lane22_armour2_neck_2026-11-27\RECORD.md`; the check `out\status\check_setC_neck_2026-11-27.md` (the first look and the SECOND LOOK); the candidate `out\for_kit_candidates\c_neck_2026-11-27` |

This teardown came before a third round of patches, as the rule says. The first record's cause for armours 1 to 4 holds
for this neck: the stock plate builder pushes each vertex off the skin on its own. Armour 5's neck was built on one
smooth field and he approved it; this neck is now built the same way.

## How the numbers were taken

The shape numbers of the neck (islands, triangles, share of faces over 30 degrees, crest pitch, tilt, lap gap, mirror
error) come from `neckm.py`, which is `td_probe.py`'s 30 degree rule and `td_crest_ours.py`'s crest code copied into
the lane, run on the armour's dump for each build; the sign of the lap gap is that of `td_crest_ours.py` (a negative
gap is a lame cutting into the next). The colour and area numbers come from probes that render the neck from
the camera the maintainer looks from (2.5 m over and 1.5 m behind the midpoint of the two withers, lens 37.12 mm), from
above and from a third view (k7), at LOD0 to LOD4, at rest, in four gate poses and in `chariot_idle_1` every second
frame (175 frames), both horses. An area is counted at 2 mm to the pixel in the lane's probes (its rest probe of 18:50 used 1 mm); the check's record states no pixel size. Red is a hue within 20 degrees of 0 with
saturation above 0.40 and value above 0.12 in the check's probe (hue 345 to 15 degrees, saturation above 0.45 in the
lane's), read from the map through the UVs of the face seen. The numbers are sensitive to that threshold, to the
frame chosen (the head-low frames are 207 to 219) and to the camera.

These numbers cannot be made again from the repository: the scripts and the builds are in the work folder.

## What was measured

| Entry | The reference has | Ours before (`v22`), ours now (`v30`) |
|---|---|---|
| [band-stack-even-rhythm](../techniques/band-stack-even-rhythm.toml) | Model 01 and `v18ay`: see the first record (spread 0.031 and 0.104) | Spread at LOD0 and LOD1: `v22` 0.0875 and 0.0988; `v23a` 0.092 and 0.094; the final builds 0.092, 0.091, 0.089 and 0.065 at LOD0 to LOD3. Tilt: `v22` -2.7 to 1.2 degrees, now 5.1 to 9.0 at LOD0. The session's target of 0.05 was dropped on 2026-11-27 (see "Not taken"). Source: `neckm.py` on the armour dumps |
| [band-stack-closed-lap](../techniques/band-stack-closed-lap.toml) | `v18ay`: 10.2 to 19.1 mm of air under each lower edge | `v22` at LOD0: -5.7 to 0.6 mm (lames cut into each other); now 6.7 to 11.4 mm of air (`v23a` at the other levels: LOD1 7.1 to 12.1, LOD2 6.2 to 13.5, LOD3 7.5 to 12.7; `v23b` changed those band ends, and `v30` at LOD1 has 6.61 to 13.62 mm between bands 3 and 4). At LOD1, bands 3 and 4 crossed in 84 and 72 places at rest and now in 0; LOD2 still has 44 and 50. Sources: `neckm.py` for the gaps; `k\c22_cross.py` (lane) and `s2\poke34.json` (check) for the crossings |
| [one-smooth-field-per-part](../techniques/one-smooth-field-per-part.toml) | Model 01: 4.3 percent of faces over 30 degrees. `v18ay`: 45.2 percent | `v22` at LOD0: 74.2 percent of the neck's faces (68.4 of its area), LOD1 85.8 (80.7). Bands of `v23a` at LOD0: 81.3 percent (67.9 of the area), LOD1 84.9 (73.4). The share did not fall. The bands have 528 vertices in 273 skin weight sets at LOD0 (the builder's read of `weights16` in `rharm_design2.py`, RECORD 21:03). Source of the shares: `neckm.py` |
| [band-end-rounded-and-level](../techniques/band-end-rounded-and-level.toml) | see the first record | One island a band with a rounded end at LOD0; mirror error of the bands 0.0 mm (`v22` lames: 0.055 median, 0.065 at the 95th percentile, 0.068 at most); the lining's mirror error 0.0 mm from `v23b` on (`v23a`: 0.22, 2.99, 6.93). Rim dents at most 2.22 mm (the first check). Source: `neckm.py` for the mirror errors |
| [liner-and-skin-view-under-moving-plates](../techniques/liner-and-skin-view-under-moving-plates.toml) | The bands are the surface, so no skin shows between them | Idle frames 207 to 219, from his view, left horse, mean and greatest cm2 (SECOND LOOK). Liner seen: red on `v22` 33.30 and 35.03 at LOD0, 52.33 and 53.79 at LOD1; matt dark on `v30` 32.45 and 33.99, 42.34 and 43.72. Skin seen in the band field: `v22` 0.14 and 0.36, 0.51 and 1.05; `v30` 7.05 and 9.09, 8.16 and 9.66. Red by colour (greatest, same probe): `v22` 111.72 at LOD0; the first candidate (`v23c`, red lining) 33.98 at frame 211, left horse, LOD0 and 49.08 at most (LOD1); `v30` with the gold bead maps 0.00 in all 175 idle frames and every pose. At frame 211, LOD0, through and between the bands: `v22` 28.91 and 3.55 cm2; the first candidate 21.22 and 12.77. In the lit render of frame 211 the openings read as dark lines before the liner change too: the lit pictures differ by 50 pixels at LOD0 and 7,852 at LOD1, so the change shows in the colour probe and little in the lit view. Sources: `k\cx_vis.py` (lane) and `tools\s2_*.py` (check) |

Further numbers of the first candidate against the installed neck, from the first check (NOT READY): band to band pairs
58 to 142 against 660 to 684, band edges through another band at rest 10 (LOD0) and 67 (LOD1) against 238 and 159. The
neck has 1,500 triangles at LOD0 against a limit of 1,513 (`v22`: 1,376); the whole set has 60,052 at LOD0.

## What the reference has and ours did not

- **Separate clean bands on one smooth field, with air under each lower edge.** `v18ay` has 10.2 to 19.1 mm of air;
  `v22` had lames cutting into each other (-5.7 to 0.6 mm). Mechanism: each band reads as one made piece with a shadow line
  under it, where a lame that cuts into the next reads as a seam.
- **Exact mirror.** 0.0 mm on the bands now against 0.055 to 0.068 mm on `v22`. Mechanism: equal widths and no drift
  between the left and right necks.
- **A rim that sets one band off from the next.** The black neck's gold rim on black. On the sun neck the session chose
  a narrow red lacquer edging, with a gold bead on the same mesh in a second map set. Mechanism: the contrast of rim
  against face is what makes the stack read at a distance (the session's reason, told to him at 18:20).

## What the reference has and ours still does not

- **An even crest pitch.** Spread 0.092 at LOD0 against 0.031 on model 01 and 0.104 on `v18ay`. Mechanism: even
  rhythm. The bands are equal along the field's length (0.0762 m) but the crest arc per unit of length is longer at
  the head end (85 mm) than at the withers (66 to 70 mm), and the cure, spacing the band edges by crest arc, is a third
  build over the lane's limit of two.
- **A low share of faces over 30 degrees.** 81.3 percent for the bands against 4.3 on model 01. Mechanism: outline.
  The build set no target on it and it did not fall. RECORD 17:54 gives the cause: the bands' 4 mm rims and rounded ends
  are separate bevelled faces by design, and by area the share is 67.9 against 68.4 percent for `v22`.
- **Closed bands in the idle clip's head-low frames.** With the head low the bands fan open as dark slots, and 7 to 10
  cm2 of skin shows at the lower band ends. Mechanism: each band laps the next toward the withers by 22 to 29 mm at the
  crest, so every slot opens toward the driver. Whether the black neck does the same was not measured: UNVERIFIED.

## What ours has and the reference does not

- **Six levels of detail:** bands 8, 6, 6, 4 and 2 at LOD0 to LOD4, and the old neck at LOD5.
- **Skin weights:** every band vertex on the smooth skin field, so the bands follow the neck in the clips; the models
  have none.
- **A budget and clearances:** 1,500 of 1,513 neck triangles at LOD0, the ear strap's ring never crossing band 0, the
  slip gate G15 passing 0 of 28 classes over their limit.
- **A dark liner chosen by colour:** (34, 34, 33), matt, so that no red shows in any pose measured.

Cost of taking it: 124 more neck triangles at LOD0 (1,376 to 1,500); at LOD1 the neck stands about 10 mm closer to the
yoke than the installed neck, with no contact found; at LOD2 the bands 3 and 4 still cross, a small notch close up and
not from 13.8 m.

## What acts against each technique

- **Even rhythm:** the bands of `rharm2_neck23.py` are spaced equal along the field's length, not along the crest arc,
  so the pitch follows the arc and is longer at the head end (RECORD 17:54). A gate on the spread: none found in the
  records read.
- **Closed lap:** armour 5's step of 12 mm between bands (`LAME_STEP`) stands against a closed lap; armour 2's neck took
  a step of 7 mm in its place (RECORD 17:38) and was not given armour 5's collar.
- **One smooth field:** the stock plate builders of armours 1 to 4 still push each vertex on its own (first record).
  Armour 2's neck now takes `CR.envelope` for its field (RECORD 17:38).
- **Band ends, and the liner with its skin view:** none found in the records read.

## Not taken

- **The one piece reading** (one continuous gold shell with shallow ribs): dropped by the session at about 17:26, on his
  words "like this or one piece ... or your best recommendation", in favour of the band stack he held up. No stop for his pick.
- **The new Tripo model**: not used; it is the one piece reading, and the session read it at 17:27, after his pick.
- **A crest pitch spread of 0.05:** the session's target, dropped by the session at 18:17 because the approved black neck
  measures 0.104 and the clause was stricter than its purpose.
- **Rigid bands or a weights change:** the builder's test at 21:03 showed the rigid shape gives more lining (52.4 to 56.8
  cm2 against 31.3 to 35.1 cm2 posed), and the weights as built are the best of those tested. The session's order for
  it rested on a cause the design file did not show.
- **Closing the openings in the head-low frames:** the session decided at 21:04 (the night's rule, his word of 19:53:
  "Always go with your recommendation") to keep the lap direction and the band shapes he had approved, and to make the
  lining matt and dark in place of closing the lap.
- **The red edging against the gold bead:** he named no rim look. The session chose the red edging for the install and
  kept the gold bead maps; a change is a map update only.
- **LOD5 and LOD2:** LOD5 keeps the old neck; the LOD2 crossings between bands 3 and 4 are not repaired.

## The maintainer's verdicts

His words, with their time. The session's judgements are listed apart below.

| Time | His words | About |
|---|---|---|
| 17:10 | "this is jacked up, probably needs triplo to fix it as it seems like it is overlapping instead of being one piece. Like its multiple pieces put together" | The installed neck (set C v26) |
| about 17:25 | "I think they should be like this or one piece , in my opinion or your best recommendation" | His picture of the black neck |
| 18:20 | "these are way better!" | The first build of eight bands, both rim looks on one sheet |

The session's judgements, not his: the band stack as the pick; the red edging for the install; the 0.05 target dropped;
the matt dark liner (decision 21:04). He had said nothing about the liner or the installed `v30` by the time this record
was written. His update of the mesh `rhun_war_chariot_c` and the three maps `rhun_horse_armour_2_d`, `_n` and `_s` in the
Kit is owed.

## Blind checks

- **No check rendered his view.** The set C checks of that day looked from 7 m straight above and from the side and
  measured red cloth. The lane's own view script gained the shot from above and behind the withers only at 17:21.
- **No check looked at the idle clip's head-low frames from his view until the first check of 20:52.** The lane's probes of 17:56
  (idle every 8th frame) and 19:57 (175 frames) covered the idle clip from above and in the k7 view only. The candidate it refused had
  passed the lane's chain: 0 of 28 slip classes over, 0 unlinked islands, preflight pass, and 0.00 cm2 of red cloth at rest,
  in the turns and with the head down.
- **A geometry measure of lining does not see what colour shows.** Counting lining faces gave areas the maintainer reads
  as red cloth only when the lining is red. The probe by colour from the map (`k\cx_vis.py --maps`) came at 21:15.
- **Not proven.** None of these blind checks was tested by breaking the default in a copy and running the gate; each is
  stated from the order in which the records show the fault was found.
- **The coordinator's cause was never tested before it was ordered.** "Each band rigid on one bone mix" went into two
  orders without a read of the design file; the file showed the bands already on the smooth skin weights.

## The properties this teardown measured

The first record's table holds for this part; one property was added: the area of lining, liner and skin seen in the
idle clip's head-low frames from his view, by part, in cm2 (the pixel size is in "How the numbers were taken"), and the area of red by colour on the
same frames.
