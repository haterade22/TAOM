# Teardown: the rubble heap of the broken wall (2026-11-26)

The record of one teardown: what a reference had that our build did not, measured. The techniques that come out of it
become entries of the knowledge store (`docs/features/knowledge-library.md`, issue #1613) when the store is on trunk;
until then the section "Defaults that follow" holds them. The asset job is in
[rivendell-siege-set.md](../rivendell-siege-set.md), the Tripo runs in [tripo-cli.md](../../features/tripo-cli.md).

| | |
|---|---|
| **Part** | The rubble heap of the broken wall `rivendell_castle_wall_20m_a_dest`: the mound `.rubble` and the loose pieces `.brick` |
| **Reference** | One image to 3D model (Tripo, `tripo-v3.1`, 60 credits), made on 2026-11-25 from the picture `heap_b_low_spread.png`: `tripo\out\tripo-out\rubble-spread-2a5f2900\model.glb`, 72,436,080 bytes, one shell of 1,961,462 triangles. Read at 12.5 m long (factor 6.6398, the scale of `relief_spread_0p1.json`), with its render from above `tripo\rubble_job\topdown2\window_*.png` |
| **Measured against** | Before: `build\v04\wall_broken` (built 2026-11-25 01:50), the last heap mesh before the reference. Now: `build\v08\wall_broken\B_fracture` (stage 5, 2026-11-26 11:08), and stage 4 `A_fracture` (02:45) where it differs |
| **Files** | `kit_full\teardowns\_scratch_T2\` in the job's work folder `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell`: `dump_meshes.py` (the one Blender job, through `blender_one.py`) and `dump_done.json`; `measure_heaps.py` and `heaps_measured.json`; `measure_contact.py` and `contact_measured.json`; `measure_textures.py` and `textures_measured.json`. The check of this teardown: `kit_full\teardowns\teardowns_check.json` |

Before the reference, the checkers judged the heap: version 2 "reads as paving", version 3 "the heap as a paved ramp,
the loose blocks as clean cuboids", version 4 "still a smooth ramp of 26.8 degrees" with stones at its edges and toes.
The maintainer saw the model on 2026-11-25: "This is fine but needs to be part of the broken wall, like Gondor and
Erebor have it". The model became data, not geometry: its height from above is the relief of the mound, and its
colour, normal and height from above are the heap's texture. He approved that texture trial ("This is great,
excellent work"). He has not seen stage 5. The model was measured against our builds on 2026-11-26 from 11:27, after
stage 5 was built.

## How the numbers were taken

All lengths are game metres. The top surface was sampled from above on a 0.03 m grid; the relief is the height less
its Gaussian blur of 0.4 m; a stone is a watershed basin of the relief over 0.02 m with 4 cells or more. This finds
stones on a fused shell (the reference) and on our mound alike. Our loose pieces were also measured as islands of
`.brick`, by their longest side. Contact is face sampled: the underside of each piece per cell against the heap and the
pieces under it. Edges are the dihedral angles of every inner edge after welding. Texture: the wrapped autocorrelation
length, the watershed of the high-passed luminance, and the share of normals with z over 0.97.

The reference is a low spread (envelope 1.57 m high on 98.4 m2) and ours a mound over a 4.4 m crest, so slopes compare
form only loosely: envelope slope p50 and p90 21.3 and 32.1 degrees on the reference, 28.3 and 51.4 on v04, 29.9 and
54.5 now. Each script writes its arguments into its output.

The verifier measured again, with a pure FBX reader and the GLB's JSON chunk: the triangles, the piece classes, the
12 triangle stones, the flat share, the lane repeat and the scale ratio. All matched. It did not run the watershed
again, so the basin counts, prominence, contact share, foot bands, fill share and correlation length rest on this
teardown alone. These numbers cannot be made again from the repository: the scripts and builds are in the work folder.

## What was measured

| Entry | The reference has | Ours before, ours now |
|---|---|---|
| `fuse-then-reduce-a-generated-mesh` | One closed shell (`probe_glb.json`: 1 shell) | v04: a smooth ramp with stones on it, `.rubble` median edge 0.560 m, relief p50 0.032 m. Now: the relief is baked from the model after a voxel remesh at 0.06 m (`relief_spread_0p1.json`, `voxel_m` 0.06) and sampled on a 0.1 m grid. That a plain collapse gave shards is from the record of 2026-11-25; those trial meshes were not measured |
| `heap-shape-from-a-generated-relief` | 3.64 stone basins per m2 (358 on 98.38 m2); relief p95 0.275 m | v04: 0.95 per m2 (223 on 235.4), boxes on a ramp. Now: 1.15 (261 on 226.95), `.rubble` alone 1.01; `.rubble` relief p95 0.217 m |
| `heap-stone-prominence` | Prominence over diameter 0.441 at p50, 0.839 at p90 | v04: 0.687 and 1.881 (boxes that stand up). Now: 0.319 and 0.707; `.rubble` alone 0.241 and 0.458 |
| `heap-one-size-series` | Stone diameter p05 0.112, p50 0.281, p95 0.631, largest 1.207 m; p95 over p05 5.62. By class 0 to 0.1, 0.1 to 0.2, 0.2 to 0.4, 0.4 to 0.8, 0.8 to 1.6, over 1.6 m: 13, 82, 182, 70, 11, 0 (one peak) | v04: 0.096, 0.332, 1.273, 3.355 m; 13.3; 15, 60, 59, 53, 29, 7. Now: 0.083, 0.285, 1.075, 2.444 m; 12.97; 27, 72, 57, 73, 28, 4 (two peaks). Loose pieces by longest side, 0.1 to 0.2 up to over 1.6 m: v04 215 pieces, 0, 74, 87, 34, 20; now 83 pieces, 37, 24, 7, 15, 0: 63 stones of 0.115 to 0.412 m and 20 blocks and fragments of 0.59 to 1.36 m |
| `loose-piece-sunk-in-its-bed` | Every stone is one shell with its bed | v04: 72 of 215 float more than 0.02 m, gap p90 0.357 m; 193 of 215 have no vertex in the heap. Now: 0 of 83; sunk vertex share p50 0.625; gate G8 largest gap -0.02 m |
| `heap-stones-lean-on-stones` | 0.872 of stone basins touch another basin (a piece on a piece cannot be told apart on a fused shell) | v04: 47 of 215 rest on another piece, most by chance (72 float). Now: 0 of 83 |
| `heap-foot-stones-beyond-the-toe`, `heap-foot-thins-inside-the-toe` | Stone cover in the outer 0.5 m inside the outline 0.222, against 0.447 at 2 m or more inside (0.50 of it); 2.77 against 4.12 stones per m2 | v04: 0.002 against 0.351, no stone. Now: 0.016 against 0.342 (0.05 of it); 0.15 against 1.32 stones per m2; 6 stones in the 2 m outside the outline (the build plan: 7 beyond the toe) |
| `stone-edges-round-not-boxed` | 0.846 of inner edges under 10 degrees, 0.029 over 30, 0.0085 over 60; median edge 0.0143 m | v04: `.brick` 0.541 over 30, 0.366 over 60, median edge 0.239 m. Now: the 63 stones of 12 triangles 0.667 over 30 and over 60, none between, median edge 0.134 m; `.brick` 0.471 and 0.322 |
| `heap-texture-from-the-model-above` | Texture correlation length 0.078 m (render from above, model scale); flat share 0.179; largest stone in the texture 0.417 m | v04: 0.002 m (pixel grain), 0.608 ("round stones in wide mortar bands"), 0.628 m. Now: 0.030 m, 0.237, 0.297 m. The luminance stone measure also finds grain inside a stone; the first two are the firmer numbers |
| `no-tile-repeat-down-a-lane` | No repeat: one 4096 bake on its own UVs | v04 and stage 4: a 2.222 m tile (0.45 UV a metre) that repeats down the lane. Now: the tile turned 68 degrees; the shortest repeat within 20 degrees of the lane is 4.969 m |
| `one-stone-scale-shape-and-texture` | One model, one scale | Now the drawn stones are 0.421 of the shaped ones: the tile draws 5.2778 m of the model on 2.2222 m, the relief uses the model at 12.5 m. Lumps of 0.34 m (p50 on `.rubble`) carry drawn stones of p95 0.17 m |

## What ours has and the reference does not

- **A budget:** 8,960 triangles for the heap now (`.rubble` 5,600, `.brick` 3,360; 39.5 per m2), stage 4 10,156, v04
  7,100. The model is 1,961,462 (19,938 per m2); prepared by a voxel remesh at 6 cm and a collapse, 14,998. Gate G1
  holds `.rubble` at 6,000 and `.brick` at 3,500.
- **Walk gates and a body:** G5 path steepest 32.08 degrees (limit 35), 0.999 of path nodes under the limit; a ramp
  body of 28.63 degrees (limit 30) cut from the built `.rubble`, never over the visual by more than 0.015 m (G10).
- **Separate pieces placed by rule:** 83 islands from one seed, a clear lane (G6: 0 corners in it), one block rolled
  out of the lane line.
- **A tiling texture at 461 px a metre** (the `heap_a` report) where the model's 4K bake on a reduced mesh gave about
  50 (the Tripo record, 2026-11-25), with a stretch gate (G11: 1.818 against 2.0).
- **A cavity tone in the vertex colour** (`CAVITY_GAIN` 1.6, `CAVITY_SIGMA` 0.4). Whether the model's colour holds
  baked light was not checked.
- **A true scale:** the model has none (`auto_size` gave 1.53 by 1.88 m); ours is set by the wall.

## Not taken into the stage 5 heap

The prominence of 0.44; the middle size class and a spread of about 5.6; stones that lean on stones; stones inside the
last 0.5 m of the foot at half the inner cover; round stone edges; one scale for drawn and shaped stones (his approval
of the texture trial stands); the stone density of 3.64 per m2 against our 1.15; a fill share (footprint in no stone
basin) of 0.605 against our 0.727 (v04 0.684). None has a report line or a gate.

## The properties this teardown measured

| Property | How it was measured |
|---|---|
| Stone count | Watershed basins of the relief (height less a 0.4 m blur, 0.03 m grid), per m2 of footprint |
| Size series | Basin diameter p05, p50, p95, largest; counts in six classes; p95 over p05. Loose pieces by longest side |
| Prominence | Basin height on the relief over its diameter, p50 and p90 |
| Contact | Underside of each piece, faces sampled, against the heap and the pieces under it; share of basins touching |
| Foot | Stone cover and stones per m2 in the outer 0.5 m against 2 m or more inside the outline |
| Fill | Share of the footprint in no stone basin |
| Edge profile | Dihedral angle of every inner edge after welding: shares over 30 and 60 degrees, median edge length |
| Texture grain | Autocorrelation length, share of flat normals, luminance basins of the high-passed texture |
| Repeat | Shortest repeat of the tile within 20 degrees of the lane |
| Scale | Metres of the model drawn on one tile against the scale of the relief |
| Budget | Triangles of the heap and per m2, against the caps of G1 |

## Defaults that follow

The technique files are written when the store is on trunk, one per slug, each with this record as its source. The
states are those of the library's ladder, judged on this job's trees, which are not in the repository, so no row is
above `instance`. `acts against`: a constant or a gate refuses what the reference has. The verifier's findings
(`teardowns_check.json`) replace this teardown's older claims where they differ. A `nothing` row becomes a `sentence`
when its file is written; its next names the step after that.

| Slug | Default | Mechanism | Where it lives today | State | Next |
|---|---|---|---|---|---|
| `fuse-then-reduce-a-generated-mesh` | Remesh a generated model into one closed surface (voxel 0.06 m here) before it is reduced or sampled | One surface has no shards and no faces through each other; the reference is 1 shell (the shards of a plain collapse were not measured) | `VOXEL` 0.06 in `build\v07\_src_b\tools\oneoff\rivendell_relief_frame.py`, used by `blender_bake_relief.py`; this piece only. The build calls `resolve_inputs` without `check_md5` (`rivendell_breach_plan.py` line 150): only `test_rivendell_mound.py` checks the relief's md5 | instance | A tool in the repository fuses a generated model and prints its shell and open edge counts before a reduction |
| `heap-shape-from-a-generated-relief` | Shape a heap from the height of a generated rubble model, not from a rule of numbers | Lumps from a real fall of stone: 3.64 stone basins per m2 on the reference, 0.95 on the rule built v04, 1.15 now | `build_field` with `DESIGN_RULE` (relief 0.8) on `load_relief`, `build\v07\_src_b\tools\oneoff\rivendell_mound.py` (lines 151 to 152 of `rivendell_breach_plan.py` call them) | instance | A tool in the repository prints stone basins per m2 of a heap beside its reference's |
| `heap-stone-prominence` | A heap stone stands out of its fill by about 0.44 of its diameter at p50 (the session's target: 0.40 or more) | A stone half as high as wide casts a shadow on its low side: 0.441 on the reference, 0.319 now, 0.241 on `.rubble` alone. "At a quarter it reads as a wavy surface" has no measured threshold | Nothing holds it. `FieldRule.relief` 0.8, `STEP` 0.1 m (`rivendell_relief_frame.py`) and the `.rubble` cap of 6,000 (`TRIANGLE_CAPS`, gate G1) push it down | nothing | The mound plan's report prints prominence p50; a gate follows at the target the maintainer confirms |
| `heap-one-size-series` | Loose stones form one size series with a middle class: one peak at 0.2 to 0.4 m, about 20 percent at 0.4 to 0.8 m, p95 over p05 about 5.6 | One series reads as one fall of broken stone; two sizes read as two sets of props ("clean cuboids"): now 7 of 83 loose pieces at 0.4 to 0.8 m; the basin classes have two peaks | `STONE_LONG_RANGE` (0.12, 0.45) and `STONE_SKEW` 1.6 in `rivendell_mound.py`; the blocks from `fallen_blocks.fbx`. `test_the_stones_are_small_angular_sizes` pins no value: a range of 0.12 to 0.80 passes | nothing | The mound plan's report prints the size histogram; a gate follows at the target the maintainer confirms |
| `loose-piece-sunk-in-its-bed` | No loose piece has its underside more than 0.02 m above the heap, the ground or the piece under it | No air and so no dark slot at a stone's foot: 72 of 215 floated in v04, 0 of 83 now | `SINK_RANGE` (0.05, 0.08), `STONE_SINK` 0.02, `STONE_BURY_SHARE` 1.5, `BLOCK_FLOAT_MAX` 0.15, `BURIED_TOP_MAX` 0.2 in `rivendell_mound.py`; gate G8 of `build\v08\wall_broken\_mesh_dev\check_rivendell_wall_broken.py`. With a piece lifted 0.10 m, G8 fails on the plan's underside (0.0996 against 0.005); its float row stayed PASS | instance | A tool in the repository prints the largest underside gap of each loose piece of a mesh |
| `heap-stones-lean-on-stones` | Stones of a heap touch and lean on one another | A heap holds itself up; stones kept apart read as placed: 0.872 of basins touch on the reference, 0 of 83 pieces now | `STONE_GAP` 0.02 and `BLOCK_GAP` 0.1 in `rivendell_mound.py` forbid contact. `STONE_GAP` -0.05 breaks none of the 185 mound tests | acts against | Let the placement put a stone on a stone (a negative gap), and print the share of pieces that rest on another |
| `heap-foot-stones-beyond-the-toe` | Single stones lie beyond the heap's toe, 1.0 to 2.0 m out | The outline breaks into stones, so the heap has no drawn edge: v04 had a bare toe; now 6 stones in the 2 m outside it | `BEYOND_STONES` (6, 8), `BEYOND_RANGE` (1.0, 2.0) in `rivendell_mound.py`, held by `BeyondTests` (setting (0, 0) gives 1 failure and 4 errors) | instance | A tool in the repository prints the stones beyond the outline of a heap |
| `heap-foot-thins-inside-the-toe` | Stone cover in the last 0.5 m inside the outline is about half the inner cover | The same edge break inside the outline: 0.50 of the inner cover on the reference, 0.05 now | `fade_field` with `fade_width` 0.6 sets the relief to 0 at the toe; `ZONE_SHARE` stone toe 0.12 in `rivendell_mound.py` | acts against | Let the relief reach the toe (a fade narrower than the 0.5 m band), and print the cover of the foot band |
| `stone-edges-round-not-boxed` | A stone's edges are round, not a hard corner on a small hull | A round edge catches one highlight line; a 12 triangle hull reads as a box: 0.029 of edges over 30 degrees on the reference, 0.667 now | `STONE_TRIANGLES_MAX` 12 in `rivendell_mound.py`; `bp.stone_points(style="cut")` in `blender_build_rivendell_wall_broken.py` (a style `"rock"` exists, unused). How the engine shades these stones is UNVERIFIED | nothing | The stone builder makes round edges within the cap, and the build prints the share of stone edges over 30 degrees |
| `heap-texture-from-the-model-above` | Make a heap's texture from a generated rubble model seen from above | The fill between stones is small stones and dust, not flat bands: flat share 0.179 on the reference, 0.608 on v04, 0.237 now | This piece: the set `set_carved_bold_v3c_heap_b` (diffuse md5 `89091cb6`), read through `INPUT_GROUPS["heap"]` (no md5 check in the build). The method: `rubble_topdown.py` and `make_heap_from_topdown.py` in `tripo\rubble_job\_scripts`, trials with no tests | instance | The two scripts become one tested texture step that the next heap calls |
| `no-tile-repeat-down-a-lane` | A tiling texture does not repeat within 2 tiles along a lane or walk line | A mark that repeats in a column reads as a track: 2.222 m before, 4.969 m now | `HEAP_TURN_DEG` 68, `HEAP_SHIFT`, `LANE_REPEAT_DEG` 20, `LANE_REPEAT_TILES` 2.0 in `rivendell_mound.py`; `test_no_repeat_of_the_tile_runs_down_the_lane` (with the turn at 0 it fails: 2.222 under 4.444); G11 holds the stretch at 2.0 | instance | A tool in the repository prints the shortest repeat of a mesh's texture along a given direction |
| `one-stone-scale-shape-and-texture` | The stones drawn in the texture have the scale of the stones shaped in the mesh | One surface with one scale: now drawn stones are 0.421 of the shaped ones, so lumps of 0.34 m carry drawn stones of p95 0.17 m | Nothing. The 5.28 m patch on a 2.22 m tile is the trial the maintainer approved on 2026-11-25 | nothing | The maintainer's word on a texture at the relief's scale; then the build prints the ratio |
