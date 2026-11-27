# Teardown: the interlaced tracery of the wall walk railing (2026-11-26)

The record of one teardown: what a reference had that our build did not, measured. The techniques that come out of it
become entries of the knowledge store (`docs/features/knowledge-library.md`, issue #1613) when the store is on trunk;
until then the section "Defaults that follow" holds them. The asset job is in
[rivendell-siege-set.md](../rivendell-siege-set.md).

| | |
|---|---|
| **Part** | The tracery railing on the town side of the wall walk of the 20 m wall: the interlaced motif, its rails and its posts |
| **Reference** | The motif data of a licensed elven building kit, read by path in `kit\game_rail` and never copied: the half motif outlines `rail_motif_coarse.json` and `rail_motif.json`, and the panel mesh dump `_explore\panel.json`. In metres, at its own pitch of 0.5616 m (its middle motif). Not a tool run: it is the input that our railing code reads |
| **Measured against** | Before: round 1 (2026-11-25, about 12:17), the lead's flat plate with a rounded outline (`kit\game_rail\build\rivendell_rail_20m_a.fbx`); the round 10 layout at the opening (`build\v07\wall_r10\results_part3.json`); the version 7 slab rail `_m7` (`build\v07\walls\wall_20m`). Now: `_m7` of `build\v07\wall_r12\_stageF_builds\20m`, approved |
| **Files** | `kit_full\teardowns\_scratch_T4\` in the job's work folder `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell`: `t4_dump_fbx.py` (one Blender job through `blender_one.py`, 3 s) and `t4_meshes.json`; `t4_measure.py` and `t4_measures.json` (also `_w38` and `_w52`); `t4_interlace.py`, `t4_rails.py`, `t4_edges.py`, each with its `.json`; the height maps `t4_hmap_*.npz` and the pictures `t4_height_*.png`, `t4_shade_*.png`. The check of this teardown: `kit_full\teardowns\teardowns_check.json` |

The maintainer rejected one railing: the round 10 layout, a pane of one motif between two posts 0.687 m apart at the
opening ("No this looks dumb. One rail.", 2026-11-25, 18:57). He liked the kit's railing on the mock-up ("I do like
the railing on top", 11:38), chose "the full pattern" (13:27) and approved the wall with the railing (18:20). Round 1
was judged by the lead only: "a flat cut-out with no chamfer on the rails, plainer than the mock-up". Ours now is built
from the reference's data, so its outline numbers match by construction; the teardown asked what else the eye sees.

## How the numbers were taken

One motif window: one pitch wide, centred on a motif, in the band between the rails (z 0.158 to 0.733 m above the
foot), 10 mm clear of both rails. The reference at its own pitch (0.5616 m), ours at the run pitch (0.5292 m): round 1
the third motif of its second run, ours now the third motif of its first run. Strand width is twice the distance to
the outline on the medial ridge. Each script writes its arguments into its output.

The strand width spread depends on where a strand ends and a shoulder begins (0.128 against 0.203 at a 52 mm cut), so
only the median width is a finding. The verifier measured again and found the same triangles (12,076, 603.8 a metre),
open edges (0 and 253), plate thickness (45.10 mm) and variant (`kit`). Its open share of the mesh silhouette, on a
2 mm raster over 3 pitches, is 0.7304 now and 0.7246 on the reference, against the teardown's 0.732 and 0.728 at 1 mm.
It did not measure again the crown height, the strand width, the oblique area of 52.8 percent, the rail shares or the
chamfer cost. These numbers cannot be made again from the repository: the scripts and builds are in the work folder.

## What was measured

| Entry | The reference has | Ours before, ours now |
|---|---|---|
| `motif-from-the-reference-outline` | Per cell 99 vertices, 24 kinks of 12 to 50 degrees, 20 corners of 50 degrees or more; 10 arcs in 4 radius classes, 175 to 535 mm; 10 percent of non-corner vertices turn 4 degrees or less. One motif window: 1,045 triangles (611 in the tracery band) | Round 1: 153 vertices, 0 kinks (every turn under 10.5 degrees), 20 corners; 18 arcs in 6 classes, 56 to 555 mm; 8.3 percent; 628 triangles (388). Now: 99, 24, 20; 10 arcs in 5 classes, 179 to 565 mm; 10 percent; 412 triangles (260) |
| `moulded-rail-chamfer-and-channel` | Oblique area share of the rails 28.9 percent (bottom) and 22.6 (top) | Round 1: flat bands, 0 and 0. Now: 16.0 and 14.0 (the kit's 1 mm lips left out) |
| `tracery-plate-thinner-than-rails` | Tracery 39.2 mm thick (interior p50; p5 to p95 32.6 to 42.7) inside rails of 60.8 mm | Round 1: plate and rails one slab of 60.82 mm. Now: plate 45.10 mm inside rails of 60.6 mm |
| `whole-runs-between-posts` | Motif pitch 0.5616 m; 3 whole motifs a panel of 1.680 m | Round 10: runs of 6 and 1 at the opening, the nearest posts 0.687 m apart (rejected). Now: pitch 0.5292 m (5.77 percent finer), runs of 6 and 7, the nearest posts 3.333 m apart |
| `crowned-strand-section` | A faceted crown, 8.0 mm above the strand's edge at the median (edge 12.5, crown 20.5 mm from the mid plane). Tracery area facing front or back (under 10 degrees) / oblique / side (over 80): 13.4 / 52.8 / 33.8 percent | Round 1: flat at 30.41 mm, crown 0; 43.9 / 0 / 56.1. Now: flat at 22.55 mm, crown 0; 50.2 / 0 / 49.8 |
| `mitred-crown-at-a-crossing` | No strand dips under another: crown 20.1 to 20.7 mm (p5 to p95) along every ridge; 15 of 63,392 interior pixels (0.02 percent) with a step over 45 degrees. Mitre lines where two crowns meet, seen in `t4_shade_reference_crop.png`, not counted | Round 1: 0 of 58,226. Now: 0 of 57,922. Nothing marks a crossing |
| `strand-at-the-drawn-width` | Median strand width 31.1 mm | Round 1: 28.3 mm. Now: 29.1 mm, 6.4 percent thinner: `cell_outline` scales the run direction only (0.942) |
| `open-share-from-the-data` | Open share of the tracery band 0.734 (outline raster at 0.5 mm); mesh silhouette 0.7246 (the verifier's) | Version 7 slab: 0.000. Round 1: 0.734, silhouette 0.731 (the teardown's 1 mm raster; now 0.732 on it). Now: 0.734, silhouette 0.7304 (the verifier's) |

## What ours has and the reference does not

- **Exact symmetry:** front surface mirror error about the motif axis 0 against 0.15 / 1.26 mm (p50 / p95); front
  against back 0 against 0.17 / 1.13 mm. Gates `test_loops_are_mirror_symmetric_exactly`,
  `test_cell_mirror_error_and_closed_run`, `test_the_two_sides_are_an_exact_mirror_pair`.
- **A budget:** 412 triangles a motif against 1,045 (39 percent); 12,076 for the 20 m railing with its posts and the
  opening, 604 a metre, against 1,870 a metre for the panel alone (round 1: 1,182). Gate
  `test_default_variant_budget_for_a_20_m_piece`, at most 16,500.
- **Closed shells:** 0 open edges against the panel's 253; 0 edges of 3 or more faces on both.
- **A piece system:** half posts on the end planes, rails in one piece through the posts, an opening for stairs of
  5.331 m clear, a run of any length (`rivendell_rail_runs.py`); 104 tests in `test_rivendell_rail_tracery.py`,
  which passed when the teardown ran them on 2026-11-26.
- **Lower levels and a body:** `.lod3` 1,348 triangles with a see-through card, `.lod6` 532, a body of 96
  (`wall_build_report.json`). The panel file holds one level and a collision hull of 108 triangles.
- **The wall's UV box rule** at 0.45 a metre and a cream tint in the vertex colour.

## Not taken into the approved railing

The crowned strand and with it the mitred crossings: the maintainer approved the railing flat. The cost of a crown is
UNVERIFIED (not measured); the 5 mm chamfer option takes a motif from 412 to 784 triangles and the 20 m railing from
16,304 to 29,744 (`round2\counts_table.json`). The strand width: 29.1 against 31.1 mm. The rail moulding at the kit's
depth: 16.0 / 14.0 percent oblique against 28.9 / 22.6. The kit's pitch: ours is 5.77 percent finer so that whole
motifs fit the 3.333 m post grid.

## The properties this teardown measured

| Property | How it was measured |
|---|---|
| Strand width | Twice the distance to the outline on the medial ridge: median, and spread at two strand cuts |
| Strand section | Crown height on the medial ridge and above the edge; tracery area by normal (front, oblique, side) |
| Interlace | Interior pixels of the height map with a step over 45 degrees; crown height along each ridge |
| Thickness | Interior p50 and p5 to p95 of the tracery; rail thickness |
| Rail profile | Oblique area share of the bottom and top rails |
| Open share | Outline raster at 0.5 mm and mesh silhouette, in the band between the rails |
| Curve vocabulary | Vertices a cell, kinks of 12 to 50 degrees, corners of 50 or more, arcs, radius classes, tangent turns |
| Symmetry | Mirror error of the front about the motif axis, and front against back, p50 and p95 |
| Rhythm | Motif pitch, motifs between posts, the nearest two posts |
| Structure | Open edges and edges of 3 or more faces |
| Budget | Triangles in one motif window and a metre, with and without posts |

## Defaults that follow

The technique files are written when the store is on trunk, one per slug, each with this record as its source. The
states are those of the library's ladder, judged on this job's trees, which are not in the repository, so no row is
above `instance`. `acts against`: a constant or a gate refuses what the reference has. The verifier's findings
(`teardowns_check.json`) replace this teardown's older claims where they differ. A `nothing` row becomes a `sentence`
when its file is written; its next names the step after that. `rivendell_rail_tracery.py` lives in two copies with the
same defaults: `build\v07\_src_w\tools\oneoff\` and the one the kit's railing imports,
`build\v07\wall_r12\_src_w_after_stageB\tools\oneoff\` (`rivendell_kit_paths.DEFAULT_WALL_TOOLS`).

| Slug | Default | Mechanism | Where it lives today | State | Next |
|---|---|---|---|---|---|
| `motif-from-the-reference-outline` | Build a motif from the reference's own outline with its kinks kept, not a rounded redraw | The same open share (0.734) as the redraw at 34 percent fewer triangles a motif (412 against 628), with the strokes straight between kinks. That the maintainer chose the full pattern is his decision, not a mechanism | `DEFAULT_VARIANT = "kit"`, `VARIANT_FILES` and `load_variant` in `rivendell_rail_tracery.py`, called by `blender_build_rivendell_wall.py` (lines 1393 and 1499 in the `_src_w` copy; 1337 and 1443 in `_src_w_after_stageB`, the copy the kit imports) and `rivendell_rail_runs.py` (line 61); `wall_build_report.json` prints `"rail_variant": "kit"`; with `"round"` the 20 m budget test fails (17,692 over 16,500) | instance | A tool in the repository prints a motif's kinks and radius classes against its reference data |
| `moulded-rail-chamfer-and-channel` | Rails carry a chamfer and a channel as real faces | A lit line and a shade line run along each rail, so the frame reads apart from the infill: 28.9 / 22.6 percent oblique on the reference, 16.0 / 14.0 now, 0 in round 1 | `rail_section(motif, which, samples=400, channel_depth=0.004)` and `build_rail`; `test_rails_carry_the_chamfer_as_real_faces` (a square section fails it, 0 of 4 directions) and `test_half_profile_is_chamfers_and_channel_without_the_lips` | instance | A tool in the repository prints the oblique area share of a rail |
| `tracery-plate-thinner-than-rails` | The tracery plate is thinner than its rails and sunk into them | The rails stand about 7.75 mm proud on each face, so a shadow line frames each run: 39.2 in 60.8 mm on the reference, 45.10 in 60.6 now, one slab in round 1 | `motif_thickness` of the data; `_build_plate`, `_sink_and_clip` and `SINK = 0.004`; `test_plate_lies_between_the_rails_and_is_thinner` (a plate as thick as the rails fails it) | instance | A tool in the repository prints plate and rail thickness of a railing mesh |
| `whole-runs-between-posts` | Every post interval holds a whole run of motifs; no short pane and no two posts close together | Every interval carries the same long run; two close posts read as a doubled column. The maintainer on the 0.687 m pane: "No this looks dumb. One rail." | `OPENING_EXTRA_MOTIFS = 1` and `piece_layout`; `layout` in `rivendell_rail_runs.py`; `test_no_run_is_short_and_no_two_posts_stand_close` (the round 10 layout fails it, gap 0.0) | instance | A tool in the repository prints the runs and the nearest two posts of a layout |
| `crowned-strand-section` | A strand of tracery has a faceted crown, about 8 mm above its edge | One side of each strand is lit and one in shade, so the pattern reads by light and not only by its outline: 52.8 percent of the area oblique on the reference, 0 now | Nothing. The only option is `strand_bevel`, a 5 mm chamfer and not a crown, default 0.0, passed as 0.0 at `blender_build_rivendell_wall.py` line 1396 (`_src_w` copy; 1340 in `_src_w_after_stageB`) and `rivendell_rail_runs.py` line 186. The audit of 2026-11-25 noted "the strands are still flat plates with hard edges" | nothing | The plate builder gets a crown parameter and the build prints the tracery's oblique share; the approved railing stays flat without the maintainer's word |
| `mitred-crown-at-a-crossing` | At a crossing each strand keeps its ridge, and the two crowns meet in mitre lines; no depth step | The interlace is drawn by light, not depth: 0.02 percent of the reference's interior steps, and the mitres mark which strand runs where. Ours has the flat overlap without crowns | Nothing; it follows from `crowned-strand-section` | nothing | Count the mitre lines on the reference (seen in a picture only), then build them with the crown |
| `strand-at-the-drawn-width` | A strand keeps the drawn width whatever the run pitch | Weight: the strands stay as heavy as drawn, 31.1 mm on the reference, 29.1 now | `cell_outline` scales the run direction only (0.942); `test_pitch_scales_only_along_the_run` pins that squeeze | acts against | Offset the outline or scale both axes, and change the test that pins the squeeze (no triangle cost) |
| `open-share-from-the-data` | Take the tracery band's open share from the reference data | The wall walk reads open from the town: 0.734 on the reference and now, 0.000 on the version 7 slab | The outline data through `cell_outline`; no gate and no report holds the share | instance | A tool in the repository prints the open share of a tracery band |
