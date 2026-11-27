# Tiling texture sets for a kingdom kit

How the stone sets of a kingdom kit are made: what the engine reads, where the stone comes from, how
Substance 3D Painter turns it into a 1024 px set, and how a set is judged. First written for the
[Rivendell siege set](rivendell-siege-set.md) (issue #1600).

**State on 2026-11-25, 13:34.** The look is decided: smooth stone with no tile grid, a light cream trim and the roof
slate as it is (Mike, 08:25), and the trim sheet carved bold with the rope on the string course only (13:27). The section
"The state of the sets" says where each set is. The sections after it are the record of how the sets were made, newest
first; the night loop of 2026-11-24 ended at about 02:05. The lab's state file is `WORK\painter_lab\STATE.md`
(`WORK` is `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\rivendell`). Nothing here is committed, and
nothing was imported in the Modding Kit or seen in the game.

## What a set is

- Three files of 1024 px: `<set>_d.png` (diffuse), `<set>_n.png` (normal) and `<set>_s.png`
  (specular). Mike's rule: every new kingdom kit texture is 1024 px.
- The Rivendell sets, five to a set folder (15 files): `t_rivendell_wall_stone_a` (the wall),
  `t_rivendell_wall_rubble_a` (the plinth), `t_rivendell_wall_trim_a` (five strips for the carved members: plain
  dressed stone, cornice, interlace band, arch moulding, and the frieze under the parapet cap),
  `t_rivendell_roof_slate_a` (roofs) and `t_rivendell_wall_heap_a` (the rubble of the breach).

## What the engine reads (checked 2026-11-24)

Evidence: the files in `WORK\painter_lab\research\engine\`, the vendor's Material Editor page
(`https://moddocs.bannerlord.com/editor/resource-editors/material_editor/`) and the game's shader
sources in `<game>\Shaders\Sources`. A research worker made the records; the main session read the
shader lines and the normal map table again.

| Subject | Fact | Evidence |
|---|---|---|
| `_s` channels | R metallic, G gloss (255 minus roughness), B ambient occlusion. This confirms the record in [ue-to-bannerlord-asset-pipeline.md](../reference/ue-to-bannerlord-asset-pipeline.md) | Vendor page; `standart.rsh` lines 1162 (`specularity_info = specular_sample.xy`) and 1289 (`ambient_occlusion_factor *= specular_sample.b`); `pbr_metallic.rs` line 15 (`USE_METALLIC_WORKFLOW 1`) |
| `_s` only counts with a flag | The shader reads `_s` only when the material flag `use_specular` is set. All 55 Gondor control materials set it | `gondor_materials_dump.json` |
| Control values of `_s` | Stone: R 0, G mean 17 to 75 (rough), B near white (mean 163 to 245) and darker in joints. Polished marble: G about 200. A smooth stone needs a higher G than the controls | `spec_channel_stats.json` (14 Gondor and 7 vanilla maps) |
| Normal map green | **Green down** (the DirectX form) in every control that gave a clear answer: 17 Gondor normal maps and 8 vanilla ones. The Kit keeps the row order of the PNG. So Painter exports `Normal_DirectX`, or the tool flips green | `normal_sense_controls.json`, `normal_sense_vanilla.json`, `gondor_rdc_orient.py` |
| Normal map blue | Lost. The Kit writes BC5, and the shader builds Z from R and G | `standart.rsh` line 726 |
| Formats after import | `_d` DXT1 (DXT5 with alpha), `_n` BC5, `_s` DXT1, `_h` BC4, full mip chain | `gondor_bricks_dump.json`, `vanilla_native_materials_dump.json` |
| Shader | `pbr_metallic` on 53 of the 55 Gondor materials (the other two are a grass and a moss material). Slots: 0 diffuse, 2 normal, 3 detail normal, 4 specular, 6 height | `gondor_materials_dump.json`, `pbr_metallic.rs` lines 4 to 13 |
| Detail normal map | Flag `use_detailnormalmap`, slot 3, `DetailnormalScale` sets how often it repeats inside the base tile. 23 of 497 vanilla `pbr_metallic` materials use one | `standart.rsh` lines 739 to 800 |
| Vertex colour | With flag `use_vertex_colors` the mesh's vertex colour multiplies the diffuse. A wall can carry large stains and a darker foot this way, on top of a tiling texture | `standart.rsh` lines 1074 to 1081 |
| Height map | Flags `use_parallaxmapping` and `separate_displacement_map`, a BC4 `_h` in slot 6. Vanilla castle walls use it (`empire_wall_a`: amount 0.16, offset 0.55) | `vanilla_native_materials_dump.json` |

Open, and only a Modding Kit test settles it: the green sense as the player sees it (it also depends
on the tangent sign that the Kit writes at mesh import), and whether `DetailnormalScale` needs the
flag `use_texture_scalers`. Only Substance 3D Painter 12.2.0 is installed on the desktop; Designer
and Sampler are not.

**Two defects in the tools of 2026-11-24, corrected on 2026-11-24:**

1. `tools/oneoff/convert_painter_stone_sets.py` filled the blue channel of `_s` from roughness
   statistics. It now writes R 0, G = 255 minus roughness (shifted to a chosen mean, with an optional
   cap on the spread) and B = the Painter ambient occlusion export. 13 tests in
   `tools/tests/test_convert_painter_stone_sets.py`.
2. The sets carried `_n.png` with green up and a second file `_n_flipgreen.png`. The converter now
   writes one `_n.png` with green down and no flipgreen file. Painter exports it as `Normal_DirectX`
   (`NORMAL_MAP` in `painter_tile_lib.py`); the exported normal and height of one run correlate
   with the sign of a green-down map (R against dh/dx -0.741, G against dh/drow -0.738). The trim
   builder still writes both files; `painter_tile_candidates.engine_trim` keeps the second as `_n.png`.

## Where the stone came from: five sources, one kept

| Source | Folder under `WORK\textures` | Verdict |
|---|---|---|
| Number noise (`tools/oneoff/make_rivendell_wall_textures.py`) | `v01` | Rejected by Mike: "Texturing is very very bad" |
| Pictures from the image connector, made to tile (`tools/oneoff/make_tileable_texture_set.py`) | `v02_imagine` | Rejected by me: one stain repeats on every tile, and the second ashlar has a doubled block |
| Scanned stone from public-domain libraries | `v02_cc0` | Rejected: the worker rated none above the Gondor stone |
| Painter's stock shelf | `v02_painter\exports\stock` | Rejected: no masonry pattern, the blocks read as flat cartoon brick |
| Substance 3D Assets materials that Mike downloaded into Painter (shelf `your_assets`, 35 materials) | `v02_painter\exports`, set `v03`, catalogue in `WORK\painter_lab\catalogue` | **Kept as the source.** Set `v03` (rough Medieval Castle Wall) was "better than before", and the look must become smooth |

## The method that ran on 2026-11-24

1. Start Painter with its bridge: `C:\Users\mikew\substance_painter_mcp\launch-painter-remote.bat`
   ([setup and traps](../reference/substance-painter-mcp-setup.md)).
2. The project is a flat plane whose UVs fill 0 to 1 (`tools/oneoff/blender_tile_plane_1333.py`
   makes the plane): `WORK\textures\v02_painter\rivendell_painter_stone_v02.spp`.
3. A Painter-side script puts one downloaded material on a fill layer, sets its parameters and
   exports base colour, normal, height, roughness and occlusion at 1024. The first scripts are
   `WORK\textures\v02_painter\scripts\yexp.py` and `lib.py`; the scripts of the catalogue are in
   `tools/painter/painter_tile_*.py` and run through `tools/painter/painter_run.py`.
4. `python tools/oneoff/convert_painter_stone_sets.py <export folder> --out <set folder> --name <set>
   --target <hex> --gloss-mean 100 --gloss-std 14` multiplies 35 percent of the occlusion into the
   diffuse, moves each colour channel to the target mean, keeps the normal map (green down) and
   writes `_s` as the engine reads it. A smooth stone wants a gloss mean of 90 to 120 (the rough
   Gondor controls sit at 64 to 71).
5. The trim sheet: `python tools/oneoff/make_tileable_texture_set.py --out <set folder> --only
   t_rivendell_wall_trim_a --trim-surface <dressed stone _d.png> --trim-target <#rrggbb>
   --trim-border-rows 28`. The surface is a real stone from Painter. The carved strips are still
   number functions; the loop replaces them with a bake from a high-poly model.
6. The look on the wall: `tools/oneoff/blender_view_textured.py --piece a=<wall fbx> --piece b=<wall
   fbx> --gap 0 --tex <set folder> --masonry-uv-scale 0.6 --out <dir> --save-blend <file>`.
7. The flat sheet beside the Gondor stone at the same pixels per metre:
   `python tools/oneoff/preview_painter_stone_sets.py <folder>`.

Set `v03` (`WORK\textures\v03`, `sets.json` beside it) came from this method:

| Set | Material | Parameters changed from the default |
|---|---|---|
| `t_rivendell_wall_stone_a` | `medieval_castle_wall` | 4 by 5 blocks, cracks 0.12, moss, lichen and flowers 0, block colours (0.76, 0.71, 0.64) and (0.73, 0.68, 0.61), erosion colour (0.66, 0.61, 0.52), mortar colour (0.58, 0.54, 0.47); mean moved to `#c7b89e` |
| `t_rivendell_wall_rubble_a` | `stacked_stone_wall` | Colours (0.56, 0.50, 0.43) and (0.53, 0.47, 0.40); mean moved to `#877a68` |
| Surface of `t_rivendell_wall_trim_a` | `medieval_castle_wall` as one dressed block | 1 by 1 blocks, no cracks, no mortar, no depth or angle spread, roundness 0, erosion 0.35, colour spread 0.2; mean moved to `#d6cab4` |

## The size of one tile on the wall

At 1024 px the density decides how the masonry reads. Measured on the v2 wall with the Medieval stone:

| UV units per metre | One tile | Pixels per metre | Result |
|---|---|---|---|
| 0.75 (the build constant until now) | 1.333 m | 768 | Courses 0.33 m high: the wall reads as brick, and the tile repeats 15 times along 20 m |
| 0.45 (recommended) | 2.222 m | 461 | Courses 0.44 to 0.55 m high: reads as ashlar; 9 tiles along 20 m |

The Gondor control has 683 pixels per metre from a 2048 px texture over 3 m. Only a density of 0.15
times a whole number keeps whole tiles on a 20 m wall and on a 26.667 m gatehouse, so that the
courses run through every joint: 0.30, 0.45, 0.60 or 0.75. The value is one constant, `TD` in
`tools/oneoff/rivendell_joint_profile.py`; a change means a rebuild of every piece. It was 0.75 in the
meshes of version 2 and is 0.45 in the repo and in every piece since version 3. Three judges compared 2.222 m with 3.333 m on 2026-11-24 and chose 2.222 m.

## The downloaded materials (catalogue of 2026-11-24)

`WORK\painter_lab\catalogue\catalogue.json` holds all 35 materials of the shelf `your_assets` with
every parameter (label, default, limits), a default export at 1024 and a lit preview;
`contact_sheet_1.png` to `_3.png` show them. What the catalogue worker proved, one parameter at a
time (numbers in each material's `tests\effects.json`):

- `smoothed_limestone_rock_surface` (Mike: "Smoothed limestone rock surface may work"): a continuous
  smooth rock with no joints. `normal_intensity` and `height_range` set the relief, `contrast` the
  spread of the colour, `color` the tone (0.82, 0.80, 0.74 gives a pale limestone). `luminosity` is
  very sensitive. It has no joint or block parameter, so the joints must come from a layer. It
  ignores the random seed.
- `slick_stone_wall`: the same kind, with the same controls.
- `cinderblock_wall`: the only smooth grey stone with joints. `surface_noise_intensity` is its
  roughness control, `tile` its block count.
- `rectangle_stone_tiles_wall` and `limestone_floor` are smooth; `large_cliff_smooth_chalk` is
  craggy in spite of its name.
- `cathedral_limestone_wall`: one chip mark repeats on every second block (seen on the wall).
- A material's random seed is not a sure way to a second variant: three of the tested materials
  ignore it.

## Painter scripting: what the research of 2026-11-24 found

A worker read the Python sources that ship with Painter 12.2.0 (`resources\python\modules`, with
Adobe's own tests), the bridge server, the vendor documents and the repo's Painter tools. It did not
call Painter. Its recipes are in `WORK\painter_lab\recipes` (`r00_lib.py` to `r07_bridge_probe.py`),
each marked NOT RUN at that time; the later rounds ran most of them, and the proven record is in "Painter probes" and
in the Round 2b sections. Until a recipe has run,
read each line below as "the source says":

- **Parameters without Painter.** A `.sbsar` file is a 7-Zip archive with an XML description.
  `tools/painter/sbsar_schema.py` reads it and gives each parameter's label, group, limits, presets
  and the outputs it changes. It ran on all 955 starter assets with no failure.
- **A material on a fill resets its channels.** After `set_material_source` set `active_channels`
  again, or the material's normal and height come back.
- **Tiling on a plane.** UV projection, wrap mode `Repeat`, a whole-number scale. The wrap toggles of
  the blur and bevel filters (`blur_tilling`, `uv_tiling`) must be on, or the effect fades at the
  tile border.
- **Joints from the tile generator.** `tile_generator` draws its pattern at 256 px by default
  (`pattern_resolution_x` and `_y` are 8); recipe `r02` sets 10 for 1024 px (the softening at 256 is
  an inference).
- **An earlier layer's height in a later mask.** An anchor point in the earlier layer, read in the
  later mask with `referenced_channel = Height`. An anchor above its reader is refused.
- **The trim sheet bake.** `BakingParameters.from_texture_set`, the high-poly file as a `file:///`
  address, the bakers Normal, AO, Curvature, Height, Position; matching by mesh name uses the
  suffixes `_low` and `_high`. On a flat low-poly plane start with averaged normals off.
- **The export, packed for the engine in one step.** A map with three channels: R from the document
  map `metallic`, G from the virtual map `Glossiness`, B from the virtual map `AO_Mixed`; the normal
  from the virtual map `Normal_DirectX`. `AO_Mixed` needs an AO channel in the stack. Padding
  `passthrough` or `infinite` for a tiling plane.
- **A view of the result.** The public API has no viewport capture, and a Qt grab shows a black 3D
  view. Adobe's tests call a hidden function, `_substance_painter.display.save_viewport(path, w, h,
  flag)`; the name is in the 12.2.0 program file. Tried in the probes: it writes a file of the asked size in which every pixel is flat
  grey, so Painter gives no picture (see "Painter probes").
- **A check of the wrap.** `tools/painter/tile_seam_metric.py` measures the step at a tile's border.
  It flags tiles that have a joint on the border although they look right, so it screens and does
  not decide.
- Painter ships a tiling sample project, `TilingMaterial.spp`, under its `resources\samples`.

## Direction and research for the smooth look

- Mike, 2026-11-24: "I think it is better than before, but I think it should be a smooth stone look."
- A research worker collected what makes smooth dressed stone read as stone (sources with their
  addresses and a table of target numbers are in `WORK\painter_lab\research\smooth_ashlar_targets.json`;
  values with no source are marked DESIGN there): tight joints (2.5 to 3.2 mm in a builder's manual,
  which is 1.2 to 1.5 px at 461 px per metre, so the joint is drawn 2 px wide), small crisp edges,
  faint tooling, and a small step of tone from block to block. Surface noise does not do it.
- A block needs its own tone, and Painter's `UV Random Color` works per UV island: on a one-island
  tiling plane it gives nothing. The route that was built is a block layout made by a script
  (`tools/painter/block_layout.py`: height, block id and edge maps), loaded into Painter as mesh maps, with the smooth
  limestone as the surface. (On 2026-11-25 Mike ruled out drawn blocks: see the owner's direction.)
- Against the repeat of a 1024 px tile on a long wall the engine offers a detail normal map and
  vertex colour (see the table above). The detail normal map was tried in round 2b and dropped for the wall face; the
  vertex colour wash is in the built meshes and was measured in round 5.

## Layered stone by script (v04 candidates, 2026-11-24)

The wall stone of `v04_candidates` comes from `tools/painter/painter_tile_candidates.py`, which reads
`WORK\painter_lab\candidates04.json` and runs, per candidate: the wall set, the dressed stone (the same
stone without joints), the trim sheet, the plinth, `params.json` and a flat sheet beside the Gondor
control at 683 px per metre. It uses these pieces, each with tests where the part is pure:

| Piece | What it does |
|---|---|
| `tools/painter/block_layout.py` | Draws the block layout without Painter: courses and block lengths wrapped in both axes, vertical joints staggered at least a given distance from the next course (also across the tile wrap), a 2 px joint, a 2 px 45 degree bevel. Writes `layout_id/height/edge/ao/stain.png`. The stain map is seamless 1/f noise. 14 tests |
| `tools/painter/painter_tile_build.py` | Painter side. One material on a fill with its parameters, then optional layers from the bitmaps: tone per block (id map, Overlay), stain (mask), joint colour (mask from the edge map), joint height (edge map, Subtract), joint occlusion (Multiply on AO). Exports with `Normal_DirectX` |
| `tools/painter/painter_tile_run_spec.py` | Host side: runs a spec through `painter_run.py` |
| `tools/painter/painter_tile_view.py` | Shows a candidate on the wall in headless Blender (two pieces, `--normal-green down`, `--masonry-uv-scale 0.6`) |

**What the recipes proved (Painter 12.2.0):** a greyscale PNG imported with
`resource.import_project_resource(path, Usage.TEXTURE)` is a legal source for a fill channel and for a
mask fill; `SourceBitmap.set_color_space(Raw)` reads it as data; `BlendingMode.Subtract` on the Height
channel cuts joints; `set_opacity(value, channel)` scales the depth. This closes the plan of a block
layout by script (gap G11). The one-island plane gives UV Random Color nothing to work on, so the tone
per block comes from the id map instead.

**Trap:** set `active_channels` before `set_source`. A source set on a channel that is not yet active is
lost, and the layer shows its defaults (grey 0.8 base colour, roughness 0.3): the joints came out pale
and shiny in the first run.

### The v04 candidates and their ranking (read on 2026-11-24 from the Blender pictures)

Folders: `WORK\textures\v04_candidates\<name>` (three sets, `params.json`, `flat_vs_gondor.png`,
`VIEW_v04_<name>.blend`); pictures: `WORK\build\v02\wall_solid\compare\v04_<name>\a_3q.png` and
`all_front.png`. Every wall set has the target mean #d0c9ba, gloss mean 100 (spread 4.7 to 13.4),
R 0, B = the AO export (mean 238 to 253); the Gondor stone controls have G 64 to 71 and B 229 to 232.

| Rank | Name | Made how | Look on the wall |
|---|---|---|---|
| 1 | `lim_tuned_b` | Smoothed Limestone Rock Surface tuned (normal_intensity 0.1, height_range 0.3, contrast -0.6, colour pale) plus the scripted layout of 5 courses of 205 px (0.44 m), tone Overlay 0.14, stain, joint depth 0.5 | Calm smooth ashlar, crisp tight joints, quiet tone per block. Flatter than the Gondor control |
| 2 | `lim_tuned` | The same stone with 3 courses of 0.74 m | Smooth, large blocks, stronger tone steps; the repeat of the block pattern shows every 2.2 m |
| 3 | `cinder_3x6` | Cinderblock Wall tuned (tile 3x6, edge 0.12, surface noise 0.02, normal_intensity 0.3, brick_variation 0.25), no layers | Very clean and flat, thin joints, concrete-like |
| 4 | `cinder_2x4` and `cinder_2x4_b` | Cinderblock Wall tile 2x4; `_b` has more noise and variation | `_b` is mottled like marbled concrete |
| 5 | `slick_tuned` | Slick Stone Wall tuned plus 4 courses of 0.56 m | Smooth but veined and busy |
| 6 | `lim_mix` | Limestone, mix of 6 courses of 0.28 to 0.49 m | Small blocks read as bathroom tile |
| 7 | `lim_asis` | Limestone with no tuning plus 6 courses | Rough and lumpy: the material as it is is not smooth |

Plinth: `lim_asis`, `lim_tuned`, `cinder_3x6` keep the converted Stacked Stone Wall of `v03`; the others
use a calmer plinth from Cinderblock Wall tile 2x3. The flat sheets show ours smoother and warmer
than the Gondor control, with less tonal detail. Not checked: the normal-map seam ratio at the wrap
reads 4 to 9 on most candidates (a screening value that over-flags joints near the tile edge); the
wall pictures show no step. No Kit or game view.

### What three judges said (round 1, 2026-11-24, 21:05)

Three workers that built nothing read every wall picture and flat sheet, each with one lens. Best
candidate for two of them: `lim_tuned_b`. Two of the three said it is not yet good enough to show as
the smooth stone look.

| Lens | Score of `lim_tuned_b` | Main findings |
|---|---|---|
| Art direction | 6 of 10 | The first set that reads as smooth dressed ashlar. Open: the plinth reads as concrete floor slabs; the joints are flat grey lines with no bevel; the colour is a cool grey-green where the concepts are warm ivory; the trim is whiter than the wall, so the carving looks applied |
| Comparison with the Gondor stone | 5 of 10 (`lim_tuned` 6) | Every candidate is too bright and too coloured beside the control (mean luminance 200 to 202 against 168; saturation 0.10 against 0.03). The block edges are machine-straight; the control has worn edges. Inside a block the tone is flat: p5 to p95 covers 36 levels against 47, and the fine grain is 2.9 to 5.6 against 7.9. Courses of 0.44 m look small beside the control's 0.74 m |
| Technique | 6.5 of 10 | The cleanest tile. Open: the 2.22 m tile repeats about 18 times along 40 m; the joints are 54 levels darker than the face (30 to 35 is enough); no block edge catches the light; the coping and string course have no joints of their own; the niche sides of the piers show vertical streaks (a mesh UV fault) |

### Plan for round 2 (decided 2026-11-24, 21:10)

1. Stone, from `lim_tuned_b`: rounded block edges in the height map (6 to 10 px) with a light line on
   the edge; joints as darkened stone, 30 to 35 levels under the face, 5 to 7 px wide; soft clouds of
   tone inside each block (40 to 80 px wide, 10 to 15 levels); fine grain near the control's 7.9.
2. Colour: the warm ivory of the concepts at a lower value. Target mean near 188 sRGB with red about
   14 to 18 above blue. This lies between the art judge (warmer) and the control judge (darker,
   greyer); Rivendell stays paler and warmer than Gondor on purpose.
3. Layouts to compare: 5 courses of 0.44 m; courses that alternate 0.55 m and 0.42 m with blocks of
   0.8 to 1.4 m; and one tile over 3.333 m of wall (0.30 UV units per metre) with larger blocks and a
   detail normal map for the close view.
4. The plinth in the wall's own stone family: large rough-faced ashlar or coursed rubble, 25 to 30
   percent darker than the wall (value 145 to 160).
5. The trim sheet from a high-poly bake in Painter, with joints every 1.0 to 1.2 m in the coping and
   the string course, and a value within 5 levels of the wall face.
6. Pictures that can be judged: a low raking sun, and a Gondor control wall with its own textures
   beside ours in one scene.
7. Against the repeat: a detail normal map, and vertex colour for a broad wash of tone, rain streaks
   under the courses and damp at the foot (the engine facts are in the table above). A 2048 px tile,
   which one judge proposed, is against the 1024 px rule and is not used.

## Round 2a: a preview without Painter (2026-11-24, 21:15 to 21:48)

Painter was with another session, so this round worked with files only. Its three tools stay in use.

| Tool | What it does | Proof |
|---|---|---|
| `tools/painter/block_layout.py --v2` | Layout maps, version 2: a rounded block edge and an edge-light map, a joint width in millimetres, a cloud map that does not cross a joint, sparse chips, course heights as a list, the pixels per metre as a parameter. Writes `layout_id`, `_cloud`, `_joint`, `_edgelight`, `_chip`, `_ao`, `_stain` and a 16 bit `_height` | Tests in `tools/tests/test_block_layout.py` |
| `tools/painter/tile_compose.py` | Composes an engine-form set on the host from a Painter export of the dressed stone and a layout folder: `build <candidates json>`, and `stats <_d.png> --ppm 461` for the numbers of a diffuse against the Gondor control. Each step stands for one Painter layer; a candidate's `params.json` lists them under `painter_layers` | Tests in `tools/tests/test_tile_compose.py`; candidates in `WORK\textures\v05_preview` |
| `tools/oneoff/blender_view_textured.py` | New: `--sun low` (a raking sun, files `_low`), `--control <fbx> --control-tex <dir> --control-lift -3` (the Gondor wall with its own textures in the scene; views `pair_outside` and `pair_close`), `--vertex-colour on`, `--detail-normal <png> --detail-scale N`, and the view `all_front_far` | 40 tests; proof runs in `WORK\build\v02\wall_solid\compare\v04_lim_tuned_b_control` |

**The trim sheet model.** `tools/oneoff/blender_build_trim_highpoly.py` builds the five strips as a
high-poly model (751,296 triangles over three tiles) with the low-poly planes, and
`blender_bake_trim_reference.py` with `rivendell_trim_maps.py` bakes reference maps in Blender:
height (16 bit), normal in both green senses, occlusion and curvature, all in
`WORK\painter_lab\trim_bake`. The strips: S1 two fitted stones per tile with a fine joint; S2 a moulding
with joints; S3 a three-strand interlace in true over and under relief, four periods per tile; S4 a
roll moulding; S5 a blind arcade of eight pointed arches. The bake matches the strip functions to
0.04 mm in the mean. For the Painter bake: rays start 30 mm above the plane and run 60 mm; all relief
lies between 0.5 and 23 mm. The arcade is rather plain; the worker says so.

**The candidates and the judges (21:48).** Seven wall candidates from the dressed stone of
`lim_tuned_b`, all with rounded edges, cut joints (31 to 32 levels under the face, 5 px), clouds and
the warm colour (mean luminance 188, red 17 above blue). Three judges read the wall pictures in both
lights and beside the Gondor wall.

| Candidate | Layout | Art | Control | Technique |
|---|---|---|---|---|
| `alt_calm` (best for all three) | Courses alternate 0.625 m and 0.486 m, blocks 0.8 to 1.4 m, clouds 0.22, tone step 0.24 | 6.5 | 6.5 | 7.0 |
| `alt_warm` | The same with clouds 0.36 | 5.5 | 6.0 | 6.0 |
| `e5_warm` | 5 courses of 0.44 m | 5.0 | 5.5 | 5.5 |
| `c3_warm` | 3 courses of 0.74 m, clouds 0.42 | 4.0 | 5.0 | 5.0 |
| `t33_warm`, `alt33_warm` | One tile over 3.333 m | 5.0, 5.5 | 5.5, 6.0 | 6.0, 6.0 |
| `e5_cool` | Grey, as the control judge asked in round 1 | 4.5 | 5.5 | 5.5 |
| `lim_tuned_b` (round 1, the baseline) | 5 courses of 0.44 m | 6.0 | 5.0 | 6.0 |

What the three judges agree on:

- **Tile size: 2.222 m.** At 3.333 m the joints are 3 px and soft, the stretched base loses its
  grain, and the clouds grow into stains. So the density stays 0.45 UV units per metre.
- **Plinth: the rustic one** (large rough-faced ashlar from Medieval Castle Wall, value 150), with
  blocks 1.5 times larger and its normal weaker (mean tilt 0.30 down to 0.2). The fine rubble reads
  as flat banding and is out.
- **Not yet good enough to show**, for the same reasons each time: the clouds are round blotches
  (cut them to 0.10 at 160 to 200 px, or replace them with streaks under each course and darker lower
  block edges); the block edges do not catch the low sun (edge light 0.20 to 0.35, edge relief 0.6 to
  1.0, edges irregular and chipped); the coping and string course have no joints (the baked trim
  sheet brings them); the detail normal map cannot be seen from the wall view and needs a view from
  less than 3 m.
- The grey candidate does not raise the grade and loses the ivory. The warm colour stays.

**Round 2b (Painter):** rebuild `alt_calm` in Painter from its `painter_layers` list and prove that
the export matches the preview numbers (luminance 188.4, joint contrast 31.7, p5 to p95 40.4, joint
5 px, normal tilt 0.105, gloss mean 100); then the changes above in the same stack; the plinth; the
trim sheet bake; pictures in both lights and beside the control; the judges again.

## Round 2b: the stone stack in Painter (2026-11-24, 21:55 to 22:07)

Painter, at 2048 with an export at 1024, gives the same stone as the host preview of `alt_calm`:
luminance 188.7 against 188.4, red minus blue 17.8 against 17.0, joint contrast 32.0 against 31.7,
joint width 5 px, p5 to p95 40.6 against 40.4, the same blocks (block tone correlation 0.988). So a
look can be tuned on the host and then built in Painter from the same maps and numbers. One run in
Painter (stack, compute at 2048, export, conversion, numbers) takes about 5 seconds.

**How to run it.** `python tools/painter/painter_stone_layout.py --from <layout folder> --out
<folder> --cfg <json>` draws the layout maps at 2048 px (922 px per metre; `px_scale` 2.0 must be
given). `python tools/painter/painter_stone_recipes.py run <name> --set key=json ...` writes the
recipe, runs `tools/painter/painter_stone_build.py` in Painter, converts the export to the engine form
and prints the numbers. Recipes and layout configs of this round: `WORK\textures\v06\cfg`.

**The stack, bottom to top** (texture set at 2048, Height `L16F`, export `Normal_DirectX` at 1024,
every map imported as a project texture and set to Raw; colours are sRGB numbers):

| Layer | Channel and blend | Source | Opacity in `alt_calm_p3` |
|---|---|---|---|
| Base | All channels | Material `smoothed_limestone_rock_surface`: `normal_intensity` 0.1, `height_range` 0.3, `contrast` -0.6, colour (0.82, 0.80, 0.74). In its content stack the filter `sharpen/sharpen` at 0.5 on the base colour | |
| Colour | Base colour, add | Colour (0.49, 0.45, 0.43) | 1 |
| Tone per block | Base colour, overlay | `layout_id` | 0.38 (0.32 in `p2`) |
| Clouds | Base colour, overlay | `layout_cloud` (features 180 px at 1024) | 0.14 |
| Streaks | Base colour, normal | Colour (0.52, 0.46, 0.38), mask `layout_streak` (from each bed joint down 60 to 150 mm) | 0.45 (0.30 in `p2`) |
| Lower edge | Base colour, normal | Colour (0.5, 0.46, 0.4), mask `layout_lower` | 0.18 |
| Stain | Base colour, normal | Colour (0.55, 0.5, 0.44), mask `layout_stain` | 0.04 |
| Joint | Base colour, multiply | Colour (0.915, 0.901, 0.878), mask `layout_joint` | 1 |
| Joint occlusion | Base colour, multiply | `layout_ao` | 0.15 |
| Edge light | Base colour, screen | Colour (1.0, 0.97, 0.9), mask `layout_edgelight` | 0.30 (0.35 in `p2`) |
| Chip light | Base colour, screen | The same colour, mask `layout_chip` | 0.30 |
| Relief | Height only, add | `layout_height` (16 bit) | 0.9 (0.667 in `p2`, 0.40 for parity) |
| Gloss shift | Roughness only, subtract | Grey 0.085 | 1 |
| Joint and chip roughness | Roughness only, add | Grey 0.137 and 0.078, masks `layout_joint` and `layout_chip` | 1 |
| Occlusion | AO only, multiply | `layout_ao` | 0.3 |

Conversion: `convert_one(export, out, set, target colour, gloss_mean 100, gloss_std 14, normal_form
"down", ao_strength 0.0)`.

**What this stage proved:**

- Painter blends the base colour in linear light and reads a Raw map as a linear value. A host
  preview must use the same formulas, or its opacities differ from Painter's (the preview's tone 0.24
  is 0.32 in Painter).
- A 2048 set exported at 1024 loses fine grain on this material (3.8 against 6.9). The sharpen filter
  on the base brings it back: 0.5 gives about 6.3.
- The layout height map, used directly as an added Height layer, gives the rounded edge; the bevel
  filter was not needed.
- The three results: `alt_calm_p` (parity), `alt_calm_p2` (the judges' changes), `alt_calm_p3` (the
  worker's variant with stronger streaks and more chips), all in `WORK\textures\v06`. In the wall
  pictures the round blotches are gone in `p2` and `p3`. The block edges show in a low sun only as a
  thin pale line along the bed joints; at wall distance that stays subtle. The streaks show on the
  flat sheet and hardly on the wall.

## Round 2b: the trim sheet bake in Painter (2026-11-24, 22:08 to 22:37)

The trim sheet now comes from a bake of the high-poly strips in Painter. The bake agrees with the
reference maps that Blender baked from the same model: the normal maps differ by 0.30 degrees in the
mean (correlation 0.996 and 0.998 in red and green), the height correlates 0.99987 (mean error
0.033 mm), the occlusion 0.84 raw and 0.93 after a 1 px blur (the Blender occlusion is the noisier
one). Result: `WORK\textures\v06\trim_baked`; project `WORK\painter_lab\trim_bake\rivendell_trim_bake.spp`;
baked maps and the settings file `final_bake.json` in `trim_bake\painter_bake`.

**The bake, step by step** (scripts `tools/painter/painter_trim_*.py`, check module
`tools/painter/trim_bake_check.py` with 19 tests):

1. The low-poly plane: export it from `trim_highpoly.blend` with the Kit's FBX settings
   (`painter_trim_plane.py`). A plane that went through Blender's FBX import comes back 100 times
   too large and turned.
2. A new project on the plane: 2048, normal format DirectX, an AO channel added to the stack.
3. Bake settings. Painter reads the scene in centimetres, so every distance is in centimetres.
   Common: `HipolyMesh` as a `file:///` address, `OutputSize` (11, 11), `LowAsHigh` off,
   `FilterMethod` Always, `RelativeScale` off, `MaxHeight` 3.0 (30 mm in front), `MaxDepth` 6.0,
   `SmoothNormals` off, `SubSampling` 2x2. Occlusion: 128 rays, `MaxDistance` 4.0, attenuation
   Smooth. Curvature: from the mesh, `SamplingRadius` 0.2. Height: `MaximizeRange` "Relative to ray
   distances", which gives a fixed scale (height in mm = 119.99 times the value minus 59.994).
   Before each bake: set the curvature method, then set every key back to its default, then set the
   keys above. One bake takes about 3 seconds.
4. The material, from the bakes: the wall's stone as the base (the same material, colour and sharpen
   step); a cavity tone through a mask that is the baked occlusion with a Levels step (input 0.7 to
   1.0, turned over), colour (0.36, 0.33, 0.29) at 0.4; an edge light through the baked curvature
   with Levels 0.56 to 0.85, screen at 0.30; the joints of S1 and S2 through the baked height below
   2.5 mm, multiply (0.95, 0.94, 0.92); the gloss shift and joint roughness as on the wall; the baked
   occlusion into the AO channel. No layer carries the baked normal map: Painter adds it to the
   exported normal by itself.
5. Export at 1024 and convert (`painter_trim_convert.py`): the colour is moved so that the flat
   faces of the sheet have the median of the wall stone (197, 188, 178); gloss mean 100.

**What mattered and what did not.** It mattered: the unit (centimetres), `RelativeScale` off, a front
ray distance not shorter than the relief (1.5 cm against 2.3 cm of relief cut the bake with no error:
normal correlation 0.46), match Always (match by mesh name baked nothing, with no error, for a low
object named `trim_low` and high objects `S1_high` to `S5_high`), the occlusion attenuation. It did
not matter on this plane: subsampling above 2x2, smooth normals, the rear distance, 128 against 256
occlusion rays, the dilation width.

**The plinth (22:38 to 22:46).** `medieval_castle_wall` on one fill at 2048, exported at 1024:
2 by 3 blocks (1.5 times larger than the preview's), moss and lichen 0, cracks 0.1,
`normal_intensity` 0.3, mortar thickness 0.25, erosion 0.2, sharpen 0.5. Measured: value 150.4, red
17 above blue, mean normal tilt 0.204 (the preview had 0.302, the wall has 0.125), joints 9 px. The
plinth face is battered, so V runs along 3.06 m of slant, not 3.0 m of height: the export is rolled
by whole rows so that a bed joint sits under the string course (`tools/painter/plinth_tools.py`, 12
tests). A damp band at the foot cannot live in the tile (it repeats at 2.22 m, in the middle of the
plinth); that is a job for vertex colour. The set of the round, with the stone `alt_calm_p3`, the
baked trim and this plinth: `WORK\textures\v06\set_final`.

## The state of the sets on 2026-11-25, 13:34

| Set | Where | State |
|---|---|---|
| The working set (stone, plinth, trim, roof slate, heap) | `WORK\textures\v10\set_smooth_w2` | On every built piece. Stone and plinth with no drawn joints; trim light cream; a little wear. Its trim is the old bake with the joint columns patched over |
| Four trim sheets with no joints | `WORK\textures\v11`: `set_original_clean`, `set_carved_fine`, `set_carved_bold`, `set_inlay_gold` | Built and measured by their worker. Mike chose **carved bold** (13:27), with the rope on the string course only: a rope strip of its own is owed (version 3). See the next section |
| A new heap map from a 3D model | `WORK\textures\v11\set_smooth_w2_heap2_b` | Used on the breach trial that Mike approved. See "A tiling set from a 3D model" |

Painter has not been used since about 02:05 on 2026-11-25; the sets made from 08:25 on were made on the host. The sections on the Painter rounds below are the record of how the stone and the
first trim sheet were made; the joint design of the trim sheet in them is replaced by the next section.

## The trim sheet, version 2: no joints, four sets (2026-11-25)

**Why:** Mike ruled out the tile look, also on the trim, and brought trim concepts (a carved sample with a lattice
frieze, bead rows and a rope; a sheet with gold inlay). The five strips keep their pixel rows
(`rivendell_joint_profile.STRIPS`), so no mesh changes when one of these four sheets replaces another. (Version 3,
below, changes the rows.)

**How it is made** (code in the private copy `WORK\build\v07\_src_trim`, not yet in the repo):

1. `rivendell_trim_strips.py` (rewritten, pure Python with numpy, 55 tests) gives the height of each strip as a
   function of U and of the distance across the strip, for a named design (`original`, `carved_fine`, `carved_bold`). No
   joint functions. Every ornament has a period that divides the tile, and the new frieze and band are mirror symmetric
   in U.
2. `blender_build_trim_highpoly.py` meshes the functions (1.1 to 2.5 million triangles), and a Cycles bake gives the
   normal map (green down), the occlusion and the height. The baked height agrees with the function to 0.03 to 0.06 mm.
3. `make_trim_sets.py` builds the diffuse map from the cream with the hollows a little darker and the arrises a little
   lighter, adds sparse wear (a few chips on arrises), writes `_s` (R metal 0, G gloss as the working set has it, B
   occlusion) and copies the 12 files of the other sets unchanged.
4. The gold inlay (`rivendell_trim_inlay.py`, `make_inlay_set.py`) is drawn as masks at four times the size and reduced,
   not baked: a pit of 0.5 mm is too small for a bake. Inside the gold `_s` has R 0.9 and G 0.25 above the stone.

| Strip (pixel rows) | Members | `original_clean` | `carved_fine` | `carved_bold` | `inlay_gold` |
|---|---|---|---|---|---|
| S5 (0 to 200) | The frieze under the parapet cap | Blind arcade | Interlaced pointed arcade, a bead row at each edge | The same, 1.8 times the relief | Gold S scrolls in mirror pairs |
| S4 (200 to 307) | Arch and window frames, jambs, rib chamfers | Cove, torus, cove | The same | 1.8 times | The same |
| S3 (307 to 522) | The band under the blind arches | Three-strand interlace with beads | The same | A two-strand band with bead rows | A gold chain of two weaving lines |
| S2 (522 to 737) | String course, plinth cap, caps, collars, sills | Cornice | A rope on the 96 mm torus | 1.8 times | As carved fine |
| S1 (737 to 1024) | Plain faces | Plain | Plain | Plain | Plain |

**Version 3 (decided by Mike at 13:27, not built yet).** He chose carved bold, with the rope on the string course only.
So S2 goes back to a plain cornice (the string course shares it with the plinth cap, the collars, the sills and the
caps), and the rope gets a strip of its own of about 48 px, taken from the plain strip S1, with a thin half-round member
along the string course. `STRIPS` in the shared profile changes with it, so every piece maps its trim again when it is
built. Order: the trim modules of `_src_trim` are merged into the wall working copy `_src_w` first.

**Version 3 as designed (14:04, code and tests done; baked at 14:10, see the next paragraph).** The design `bold_v3` in `rivendell_trim_strips.py`
and a new module `rivendell_trim_rope.py` (private copy `_src_trim`). Rows: S5 0 to 200, S4 200 to 307, S3 307 to 522, S2
522 to 737 (plain cornice), the rope strip SR 737 to 793 (48 rows of rope with 4 flat rows at each side; 40 whole
twists to a tile), S1 793 to 1024. The rope member is a segment of a circle 87.6 mm wide and 24 mm out, mapped by its
arc length, so the density across the rope equals the density along it. Previews (shaded height, no Blender):
`WORK\build\v07\trim_v3\preview`. The bake commands are in `trim_v3\_scripts\bake_v3.sh`.

**Version 3 baked (2026-11-25, 14:10).** Four steps, the two Blender steps through the lock tool: the high-poly strips and
the low planes (`blender_build_trim_highpoly.py --design bold_v3`), the Cycles bake of height, normal and ambient occlusion
onto the low plane (`blender_bake_trim_reference.py`, cage 0.06, ray 0.12, 512 samples), the PNG maps with their checks
(`rivendell_trim_maps.py`), and the engine set (`make_trim_sets.py --designs set_carved_bold_v3`). The two Blender steps
took under half a minute together. Result: `WORK\build\v07\trim_v3\sets\set_carved_bold_v3` (15 files; the 12 that are
not trim come unchanged from `set_smooth_w2`). The report `refmaps_bold_v3\trim_ref_report.json`: baked height against
the designed height 0.06 mm in the mean, 97.8 percent of the sheet under 0.5 mm, largest 1.9 mm; the flat margin rows of
every strip at exactly 6.0 mm; the rope strip 6.0 to 15.2 mm; the normal map's green sense proved for both files. Trap:
`rivendell_trim_maps.py` does not make its `--out` folder, so make the folder first (the script does).

**Traps found:**

- The occlusion from a Cycles bake comes in steps of 1/80 at any sample count; the worker blurs it by one pixel.
- The view tool does not read `_s`, so metal cannot be judged in its pictures. The gold needs the game or a renderer
  that reads the metal channel.
- A strip is shared by several members: a rope on S2 shows on the string course and also on collars, sills and caps
  (the reason for the strip of its own in version 3).
- A weave that is mirror symmetric in U cannot go one over, one under; the new ones go two over, two under.
- The old joint tools (`tools/painter/trim_joint_check.py` and its test) need functions that are gone.

**Not checked:** the worker's numbers by an independent measure; the sets in the game.

## A tiling set from a 3D model: the heap (2026-11-25)

The heap map of the working set draws round stones in wide mortar bands and read as paving. The new one is a model of
real rubble (from the Tripo tool, [tripo-cli.md](../features/tripo-cli.md)) seen straight from above. Trial scripts,
with no tests yet, in `WORK\tripo\rubble_job\_scripts`:

1. `rubble_topdown.py`: scale the model, find the square window that lies fully on it, has stones in every cell of a 3 by 3
   grid and no big block, and render colour, normal (green down) and height as emission passes with no light,
   192 px wider than the tile.
2. `make_heap_from_topdown.py`: close the tile along the path of least difference through the 192 px band (a cut, not
   a cross-fade, so no stone shows twice); the second path starts and ends in one column, or a step shows across the
   first wrap; level the large light and dark areas; set the colour to 0.93 of the wall stone; write the set.

Lessons: take a window twice as wide as the tile will be on the mesh (a 2.64 m window gave lanes of stones and dust on
a ramp; a 5.28 m window mapped at half size did not); map a mound from above, not by a box rule; loose blocks take the
plain wall stone, because a map of many small stones reads as a broken mosaic on a block of 0.3 m.

## The owner's direction (2026-11-25, 08:25): no tile look, a cream trim, the slate accepted

Mike saw the round 4 set on the pieces of version 4 and said: "Remove the TILE look from this and make it smooth stone. The trim should be a smooth stone no tile but a cream color (a bit darker than the stone)". Of the roof
slate `slate_b`: "These tiles and color is perfect for the roof".

So five rounds worked on the wrong reading of "a smooth stone look". I read it as smooth dressed
blocks with drawn joints. He means a smooth stone surface with no block grid. The set with no joints
existed since round 3 as a baseline (`set_dressed`); the judges gave it 3 to 4 of 10 ("plaster, no
masonry"), so I never showed it. The judges also called the slate "square pavers"; he calls it
perfect. The lesson is in `docs/reviews/lessons/build-tooling-workflow.md`.

**The first smooth sets, made on the host with no Painter** (`tools/oneoff/make_smooth_sets.py`, 10 tests at that time and 16 since the wear was added;
tests; output `WORK\textures\v10`, each set 15 files of 1024 px):

| Material | Source | What the tool does |
|---|---|---|
| Wall stone | The dressed stone export of round 3 (`WORK\textures\v07\dressed\engine`): the stack with no joint layers and with the low relief of `stone_a` | Taken as it is: mean (197.9, 189.0, 173.7) |
| Plinth | The same maps | The diffuse scaled to luminance 180, the same hue: mean (187.6, 179.3, 164.7) |
| Trim | The baked sheet of round 2b (`WORK\textures\v06\trim_baked`): the mouldings and the interlace, with one faint joint per half tile | The joint columns (17 px at U 0 and U 0.5) are patched in all three maps with the same strip from 48 px further on, cross-faded over 6 px (a blend of two columns left a smear with no grain, seen from 1 m); the diffuse is scaled per channel to a cream: (190, 176, 148) in `set_smooth`, (182, 167, 138) in `set_smooth_b` |
| Roof slate | `slate_b` of round 4 | Copied (accepted) |
| Heap | `heap_a` of round 4 | Copied (not yet judged by him) |

`set_hairline` has joints of one pixel, 6 percent darker, on the wall stone; from 10 m they cannot be
seen, so it equals `set_smooth` in the pictures.

My reading of the pictures (`WORK\build\v04_tex\v10_smooth`, `ga_3q.png` and `w1_3q_low.png`): the
tile grid is gone, the faces are smooth with soft clouds and show no dent in the low sun, and the
string courses, arch frames, ribs, door ring and coping read as cream members. Now that no block
pattern covers it, the drum of the gatehouse shows faint vertical lines at its facets: a matter of
the mesh normals, for the next geometry pass.

**What a proper pass still owes** (when he accepts the direction): the trim sheet baked again from a
high-poly model with no joints (this one is a blend over the old joints); a plinth and a trim tone
of his choice; the gloss map checked for a surface with no joints; the smooth sets on every piece.

## Round 5: what was complete when the night stopped (2026-11-25, about 02:05)

The session ended at about 02:05 and both workflows stopped with it. Round 5 (run
wf_cfb18db0-e9a) was in its first phase. Complete: the web research and the picture tool. Cut off:
the fish-scale slate (trial folders `slate_c*` and `slate_d*` in `WORK\textures\v09`, no final
set). Not started: the heap, the wall stone, the plinth, the trim, the renders, the judges. Painter
did not run at 07:41, so the open project of the night is closed.

**The picture tool (complete):** `--close-piece <label>` (without it the close views aim at the
first solid wall); `face_close_graze.png` (a sun 12 degrees from the face plane; at this camera two
thirds of the frame lie in the shadow of the left pier); `a_3q_warm.png`, `face_close_warm.png` and
`all_front_warm.png` (a sun of about 5500 K, sky strength 0.3).

**The vertex colour (measured on the wall of version 4, the wash on against off):** the attribute
`Col` arrives in Blender (corner domain, byte colour, stored range 0.80 to 0.99). The wash shows,
but weakly: 2 levels on the upper wall, 4 in the bay fields and on the plinth, 10 at the foot of the
plinth, 16 at most. It scales red, green and blue alike, so it does not tint the plinth. The plinth
is a little pinker than the wall with the wash off too (red minus green 6.2 against 5.0), so that
comes from its map.

**The web research (`WORK\painter_lab\research\limestone_character.json`):** few numbers that a
texture can use. From sources: fine ashlar joints are 2.5 to 3.2 mm (1 to 1.5 px here; ours is 13 mm);
chiselled limestone has strokes at 3.3 to 5 mm (1.6 to 2.3 px), parallel to an edge or at 45
degrees, so the grain of a dressed face has a direction; base colour stays inside 50 to 240. Marked
as inference, not from a source: a value drift of 4 to 8 percent from block to block with a slight
warm or cool shift, bedding bands and shell fragments inside a block (cast stone is uniform),
roughness offsets per block, darkening under projections. Not found: the finish of plain faces on
refined buildings, arris radius, fish-scale sizes, rubble sizes.

## Round 4: what three judges said, and the plan for round 5 (2026-11-25, 01:40)

The round 4 sets are `WORK\textures\v08\set_final` (`stone_d`, `plinth_c`, the new trim, `slate_b`,
`heap_a`; 15 files, each 1024 px) and `set_alt` (`stone_e`, `plinth_d`). Renders:
`WORK\build\v04_tex\v08r_*` (wall, towers and gatehouse `a` of version 4; the breach and gatehouse `b`
of version 3, because their version 4 was not complete). Scores for `set_final`: art 7, control 6.5,
technique 7 (round 3: 7 each). All three: not yet good enough to show.

| Fault of round 3 | State after round 4 |
|---|---|
| The plinth is another stone and another masonry | Solved (three of three). It still reads a little pink and darker than the wall in the pictures; its map has the wall's hue (red 23.5 above blue against 24.4) |
| The face is flat honed concrete from 2.5 m | Not solved. The close view moved by 3.4 levels against round 3. In a low sun the face now shows **dents**: the broad undulation that I asked for reads as plaster or dough, not as dressed stone |
| A patchwork of single blocks | Solved (three of three) |
| Trim joints lighter and thinner than the wall joints | Partly. The darkness matches now. The trim joint renders 7 to 8 px wide against 10 to 19 px on the wall, and it is 5.5 mm deep against 22 mm. Coping joints are lost in the far front view |
| No roof slate, no heap rubble | Partly. Both sets exist. The slate reads as square pavers in its map and no picture shows it on a roof. The heap reads as crazy paving on a smooth mound |

**My error in the brief of round 4, and the lesson.** One judge of round 3 asked for a broad
undulation of the face (20 cm and more) so that it takes the light. I put it into the brief. All
three judges of round 4 say that it reads as dents in a low sun, and I see the same in
`v08r_set_final\a_3q_low.png`. A relief feature larger than about 5 cm on a dressed face reads as a
fault of the wall, not as stone. The light response of a smooth stone has to come from fine relief
(grain, tooling, small pits), from gloss that differs from block to block, and from the bevels.

Other findings:

- The joint (a floor of 13 mm, a halo, a bright bevel on the top edge) reads as tile grout to the
  art judge: too wide and too even for tight cut ashlar.
- The colour reads grey-beige in the pictures. The diffuse has red to blue 1.14; the renders have
  1.08 to 1.09 under a white sun and a cool sky. The art judge asks for 1.20 and for a picture under
  a warm sun beside the neutral one.
- The control judge measured the maps: our normal tilt median is 0.058 against 0.155 to 0.177 for
  the Gondor stone at 1024; our gloss is 100 with a spread of 8.1 against 54 to 66 with 14.5 to 18.9.
  The same judge found no weathering in the pictures, although every piece carries the vertex
  colour wash. Whether the wash shows at all, and whether it makes the plinth pink, is not tested yet.
- A close view with grazing light is missing (the present close views meet the face at a cosine of
  0.54 to 0.70). The close view of a row aims at the last piece, which was a gatehouse arch.

### Plan for round 5 (decided 2026-11-25, 01:43; run wf_cfb18db0-e9a started 01:46; Painter, output `WORK\textures\v09`)

1. **Research on the web first** (the loop's rule for a knowledge gap): what makes a dressed
   limestone face read as stone and not as cast concrete at 2 m; the finishes of fine ashlar and
   their tool marks; real joint widths; fish-scale slate; how texture artists get that character
   with the tools of Painter.
2. **Wall stone `stone_f`:** no relief feature larger than 5 cm (the undulation and the cushion go);
   fine grain and sparse small pits in the normal; bedding bands and rare hairline veins in the
   colour; gloss that differs from block to block; a little wear on the arris; half the light rim; a
   joint floor of 10 to 11 mm with half the halo; a warmer ivory (red to blue 1.17 to 1.18). A second
   variant with a very small plane per block.
3. **Plinth `plinth_e`:** the same stack, luminance 182 to 184, the wall's hue.
4. **Trim:** the joint as deep as the bake allows (15 to 22 mm) and as wide as the wall's.
5. **Roof slate, second try:** a layout of our own, as for the wall: rows of fish-scale slates, as
   the concept of gatehouse `b` shows them, in the concept's teal grey, with an overlap shadow.
6. **Heap rubble, second try:** angular chips of three sizes that overlap, no network of gaps,
   dark dust between them.
7. **Picture tool:** a grazing close view, a warm sun beside the neutral one, the close view aimed
   at a wall, and a test of the vertex colour (on against off).

## Round 4: the plinth from the wall's stack, the wall stone, the trim (2026-11-25, 00:29 to 01:27)

Output: `WORK\textures\v08` (recipes and layout configs in `cfg`); pictures on the wall of
version 3: `WORK\build\v04_tex\v08_stone_d`, `v08_stone_e`, `v08_stone_d_pd`, `v08_trim`.
The working set is `set_wip`: `stone_d`, `plinth_c` and the new trim sheet. The roof slate, the heap
rubble, the final renders and the judges were still in work at 01:27.

### The plinth (`plinth_c`), built with the stone tools

The same stack as the wall stone (material `smoothed_limestone_rock_surface`, the same joint, a
round bevel of 15 mm, no chips) on a layout of its own.

| Target | Asked | Measured |
|---|---|---|
| Mean luminance | 172 to 178 (the wall has 190) | 175.4 |
| Red above blue | 22 to 26 | 23.5 |
| Means of the six blocks | Within 8 levels | Within 4.1 (175.9 to 180.0) |
| Low-frequency spread (the judge's measure) | At most 5 (`plinth_a` had 8.07) | 4.87 |
| Edge deviation; tile tilt | Under 1 px; 0.10 to 0.12 | 0.0 px; 0.105 |
| Courses from the top | 0.837, 0.740, 0.645, 0.837 m; the bed joint at V phase 0.3767 | 0.838, 0.740, 0.645, 0.838 m; the joint within 1 px of the wanted row |
| Blocks | Two per course, 0.95 to 1.30 m, no head joint over another | 0.97 to 1.25 m; head joints at least 0.3 m apart |

`plinth_d` is a second variant: luminance 179.3, gloss 86, a little more relief.

### The wall stone (`stone_d`; `stone_e` is the stronger variant)

| Target | Asked | `stone_d` | `stone_e` |
|---|---|---|---|
| Block means | Within 6 levels | 5.3 | 5.3 |
| Course means; four vertical bands | Within 3 | 1.5; 0.7 | |
| Joint under the face | 50 to 55 levels | 52.8 | 55.0 |
| Dark tail of the tile (median minus 5th percentile) | 24 or more (`stone_a` 17.9, Gondor 33.0) | 25.6 | 25.6 |
| Joint width | 12 to 13 mm | Floor 13.0 mm; 17.4 mm with the shade beside it | |
| Median face tilt | 0.055 to 0.07 | 0.055 | 0.068 |
| Fine grain; clouds | 4.5 to 5; 0.3 to 0.5 m at 3 levels at most | 4.83; about 0.39 m at 1.55 levels | |
| Kept | Edge under 1 px, luminance 190, red 24 to 25 above blue, gloss 100 | 0.0 px, 190.0, 24.4, 100.2 | |

What changed in the recipe against `stone_a`:

- `tone_amp` 0.11 to 0.045 (0.055 still gave a block spread of 6.7, because the cloud field of each
  block adds to it), cloud overlay 0.10 to 0.07.
- The joint: floor 10.5 to 13.0 mm, colour (0.80, 0.788, 0.768) to (0.74, 0.729, 0.71), the round
  bevel of 13 mm kept.
- **A halo layer:** a contact shade beside the joint (not in it), sigma 7 mm, Multiply at 0.45. The
  joint is only 3.5 percent of the tile, so its colour alone gives a dark tail of 17 to 18. The halo
  brings 25.6 and makes the joint read 17.4 mm wide. The two targets pull against each other.
- **A second height layer for the face** (Add, opacity 1; one layer cannot hold the full bevel and
  the face relief): a plane per block, tilted up to 1.5 degrees and capped at 5 mm (a corner of a
  1 m block would move 13 mm, more than the joint is deep), a cushion of 0.8 mm over 90 mm toward
  the edges, and a broad undulation of 2.3 mm rms at 0.2 to 0.6 m (`stone_e`: 3.6 mm).
- Sharpen on the base fill 0.5 to 0.2 (fine grain 6.40 to 4.83).

My reading of `v08_stone_d` and `v08_trim` (a_3q, face_close_low, all_front_far): the plinth is one
stone with the wall, with larger blocks and one step darker; the checker is gone. The faces show
soft clouds. The joints read from far. The verdict of the judges is not in yet.

### The trim sheet (`WORK\textures\v08\trim_baked`)

- The joint of every strip has the wall's section now: a floor of 13 mm and a round of 13 mm on both
  sides. The arch moulding S4 has six real joints per tile, 8.4 mm wide at half depth.
- The depth stays 5.5 mm. The wall's joint is 22 mm deep; that depth would put the floor 16 mm
  under the plane and the relief at 40 mm, against the limit of 25 mm. So the round of a trim joint
  is gentler than the wall's (about 13 degrees at the middle against about 43; a calculation).
- The bake agrees with a new Blender reference: the normal maps differ by 0.32 degrees in the mean
  (S4 and S5 alone 0.52 and 0.56), the height correlates at 0.99988.
- The joint colour mask is the floor only, like the wall's, and the sheet has the wall's halo
  (`rivendell_trim_maps.py --halo-map`). Painter's cavity adds to the joint colour: with cavity 0.40
  the joints measured 58 to 66 levels; 0.10 gives 53.
- Measured at 1024: the joints are 53.1 to 55.9 levels under the face (the wall has 52.8) and 6 to
  7 px wide (S4: 4 px; the wall: 8 px); the flat faces equal the wall face.
- The joints of the coping, the string course and the ribs show in the near view and in the far
  front view (my reading of `v08_trim`).

### The two new sets of round 4 (first try, both to be made again)

A new tool makes a set from one material: `tools/painter/painter_material_set.py` (a config file, a
Painter run, a gain per channel to a target mean, the gloss, the measures, a 3 by 3 tiling and a lit
picture). `painter_rubble_layout.py` draws a layout of rubble pieces on the host.

| Set | Source and settings | Numbers | Reading |
|---|---|---|---|
| `slate_a` | `slate_tile`: 8 slates across, 10 courses (0.278 by 0.222 m), `color_variations` 0.05, `pattern_strength` 0, mean (57, 68, 97) | Luminance 67.9, blue 40 above red, gloss 130, tilt 0.28 | Looked like blue paint beside the ivory wall in a test picture |
| `slate_b` (in `set_final`) | The same with 9 across, mean (88, 94, 108) | Luminance 93.8, blue 20 above red, gloss 128 | Square tiles with wobbly edges and no overlap: pavers, not roof slate (technique judge, from the map) |
| `heap_a` (in `set_final`) | 82 pieces of 6 to 53 cm drawn on the host over `stone_crumble`, dust in the gaps | Luminance 157.9, red 26 above blue, tilt 0.25, gloss 80 | Rounded pale plates in a network of gaps: crazy paving. The repeat shows |
| `heap_b` | `large_cliff_smooth_chalk` blended with a copy turned 90 degrees | The same targets; low-frequency spread 1.63 | Not judged on a piece |

## Round 3: what three judges said, and the plan for round 4 (2026-11-25, 00:25)

Three independent judges read the round 3 renders: art direction, the comparison with the Gondor
control, and technique. Each gave `set_final` 7 of 10 on the wall (round 2b: 6.5 each) and 7.5, 7
and 7 on the frontage with tower `b`. All three said: not yet good enough to show, and all three
named the plinth as the reason.

| Fault of round 2b | State after round 3 (the three judges agree) |
|---|---|
| Courses alternate light and dark | Solved. A faint patchwork of single blocks stays at middle distance, and the eight block tones repeat every 2.22 m |
| A ragged, chipped lip on every block | Solved |
| Joints pale, the face flat beside the control | Partly. The joints are dark cut lines now. The face takes almost no light: normal tilt 0.087 against 0.294 on the Gondor stone, 6.7 percent of the texels tilt more than 0.2 against 67.6 percent, and the dark tail of the joint is 17.9 levels against 33.0. From 2.5 m the fine flecks read as honed concrete |
| No joints in the coping and the string course | Partly. They show near and at middle distance and are lost from far |
| The plinth is a darker, browner stone | Not solved, and now the largest fault. `plinth_a` is a checker of brown and grey blocks with veins and wavy joints: another stone and another kind of masonry than the wall. `plinth_b` is calmer but darker |

Other findings of the judges:

- `stone_b` (a 15 mm mitred chamfer, a wider joint) reads as a bevelled tile close up: dropped. Its
  darker joint is wanted. `stone_c` cannot be told from `stone_a`: no more work on the shape of the
  face.
- The trim joints are lighter than the wall joints beside them, and the fine joints of the arch
  moulding are lines of one pixel with no bevel.
- Seen in the renders but not faults of a texture: stretched UVs on the side face of the pier, a
  dashed hairline across the upper band (it shows on the set with no joints, so it is a UV border),
  no plinth course on the tower base, and a `pair_close` view that frames the plinth and a jamb in
  place of the ashlar face. These go to the geometry pass and the view tool.
- One judge asked for a 2048 px export to match the control. Not taken: every new texture is 1024 px
  by the maintainer's rule. The density is 461 px per metre.
- The art judge saw a cool grey cast in the close view. The exported diffuse of `stone_a` measures
  (198, 189, 174), red 24.5 above blue, so the texture is warm. My first reading was that the face
  stood in shade under the sky light. That was wrong: a worker measured the light at 01:00. In
  `face_close` the face is in direct sun (38 degrees above the horizon, cosine 0.696 on the face).
  The render of the face is about 5 percent less warm than the diffuse (red to blue 1.086 against
  1.14), in every view, from the sky colour `#cfd9e4` at strength 0.4. No cool cast was measured.

### Plan for round 4 (decided 2026-11-25, 00:25; run wf_b8c74099-9a8 started 00:29; Painter, output `WORK\textures\v08`)

1. **Plinth from the wall's own stack.** The same material (`smoothed_limestone_rock_surface`), the
   same straight joint with the round bevel, no chips, no veins. A layout of its own with larger
   blocks: three courses on the 2.222 m tile and two blocks per course. Luminance 172 to 178 (the
   wall has 190), red 24 above blue, block tone within 4 levels, low-frequency spread at most 5
   (`plinth_a` has 8.07), tile tilt 0.10 to 0.12 from a little more face relief.
2. **Wall stone `stone_d`** from `stone_a`: block tone amplitude halved (0.11 to about 0.055); the
   joint colour of `stone_b` on the 13 mm round bevel of `stone_a`, with a joint floor one pixel
   wider at 1024, for a dark tail near 25 levels; a broad, soft undulation of the face (20 cm and
   more) so that the median face tilt rises from 0.039 to about 0.07; soft clouds of 0.3 to 0.5 m at
   3 levels at most in place of the fine flecks. One second variant with a stronger setting.
3. **Trim:** the joints get the wall joint's width, bevel and colour on every strip, the arch
   moulding too, and the sheet is baked again.
4. **Two new sets.** A roof slate, `t_rivendell_roof_slate_a`, from the downloaded `slate_tile`
   material in the roof colour of the palette (`#34405f`), for the roof and the spires of gatehouse
   `b`. A rubble with no courses, `t_rivendell_wall_heap_a`, for the heap and the torn core of the
   breach, because the plinth stone is
   coursed ashlar and reads as paving on a heap.
5. Renders on the pieces of the next geometry version, a close view with the sun on the face, the
   ashlar face beside the Gondor face, then three judges.

On a mesh, a material has the name of its set, so the two new materials are
`t_rivendell_roof_slate_a` (slot 3) and `t_rivendell_wall_heap_a` (slot 4). The picture tool gets
both in this round: without the slate files it shows the flat roof colour, and without the heap
files it shows the plinth set.

**The picture tool after round 4 (`tools/oneoff/blender_view_textured.py`, complete 01:00, 54
tests):**

- A material named `t_rivendell_roof_slate_a` or `t_rivendell_wall_heap_a` is dressed through its
  UVs like the stone set. Without the slate files in the `--tex` folder the roof is the flat colour
  `#34405f`; without the heap files the heap shows the plinth set. The report `_report/DONE.txt`
  names each fallback under `fallbacks` and the light under `light`.
- `face_close_sun.png` on every run: the camera of `face_close`, a sun 40 degrees up and 45 degrees
  from the face normal, that sun alone.
- `pair_face.png` and `pair_face_low.png` with `--control`: the camera 6 m in front of the faces at
  Z 5.5; the control is the left half and our ashlar the right half. For this view only, the control
  moves by the gap so that the two faces meet in the middle of the frame.

**A fault found while I planned (measured 00:27 on the wall of version 3, with a Blender probe of
the UVs):** the bed joint of the plinth texture does not sit at the top of the plinth. The roll tool
(`plinth_tools.py`) assumes V 0 at the ground and V 1.3767 at the top of the battered face (the
slant length 3.0594 m times 0.45). The mesh has V -0.2736 at the ground and 1.1032 at the top,
because the slope rule of `jp.world_uv` projects from the world origin; the inner vertical plinth
face has 1.35 at the top. So the joint sat 0.13 m under the cap on the outer face. The cure is in
the mesh, in one place: version 4 gives every plinth face V 1.3767 at its top edge (Z 3.0), and the
textures keep their rule. The new plinth has courses of 0.837, 0.740 and 0.645 m, so the battered
face shows 0.837, 0.740, 0.645 and 0.837 m from the top down and a bed joint lies at the ground.

## Round 3: the smooth stone (2026-11-24, 23:15 to 23:40)

Three candidates in `WORK\textures\v07`, built in Painter from the recipe of `alt_calm_p2` and rendered
on the wall of version 3 (`WORK\build\v03_tex\v07_stone_a\views`). The run:
`python tools/painter/painter_stone_recipes.py run <name> --round3 --root <v07 folder>`.

| Target of the judges | Asked | Measured on `stone_a` | Met |
|---|---|---|---|
| A straight, crisp edge on every block | Under 1 px off straight; a 12 to 15 mm bevel; no jitter | 0.0 px over 28 edges; a 13 mm round bevel; a constant light line of 2.5 px on the top edge and fainter ones on the other three | Yes |
| A smooth face | The face relief at half of before; fine grain near 6 | Face tilt 0.041 (0.083 before); fine grain 6.39 | Yes |
| Joints that look cut | 10 to 11 mm wide, 40 to 45 levels under the face | 10.8 mm, 43 levels, the colour of darkened stone | Yes |
| The relief of the whole tile | Normal tilt 0.16 to 0.18 | 0.087 (0.105 with a 15 mm chamfer) | No |
| No course stripes | Course means within 3 levels | 190.8, 189.2, 190.8, 190.0 (spread 1.6; it was 16.3). Four vertical bands: spread 0.6 | Yes |
| A warm ivory | Luminance 190, red 24 above blue | 190.3 and 24.5 | Yes |

What changed in the recipe: the layout has no edge jitter and no chips; the tone per block comes
from `balanced_tones()` in `block_layout.py` (the length-weighted mean of every course is zero, no
two touching blocks share an extreme tone, the four vertical bands are balanced); the material's
relief is halved (`normal_intensity` 0.05, `height_range` 0.15); the joint colour is (0.80, 0.788,
0.768) in multiply; streaks 0.10, lower band 0.08, clouds 0.10; the colour target is (198, 189, 174).
`stone_b` has a wider, darker joint and a 15 mm chamfer; `stone_c` a 14.5 mm round bevel, no streaks
and a slightly lighter ivory.

The one target that is not met, and my decision on it: a tile tilt of 0.16 to 0.18 cannot be reached
while the faces stay smooth, because the tile has only 8 blocks and the joints are a small share of
it. A smooth stone has a low tilt by its nature, so about 0.09 to 0.10 is accepted. The Gondor stone
has 0.18 to 0.34 because its faces are rough.

I read `face_close_low.png` and `a_3q.png` of `stone_a`: the edges are straight and crisp, the faces
smooth, the stripes gone, the colour a warm ivory. In those pictures the trim and the plinth are
still the sets of round 2b, greyer than the new stone; the next stage of the round replaces them.

### Round 3: the trim sheet with joints, and the plinth (2026-11-24, 23:40 to 2026-11-25, 00:05)

The set of the round is `WORK\textures\v07\set_final`: the stone `stone_a`, the trim `trim_baked` and the plinth
`plinth_a`. A second set, `set_alt`, has `stone_b` and `plinth_b`. Renders on the pieces of version 3:
`WORK\build\v03_tex\v07r_*`, and five pieces in one scene with a Blender file to orbit:
`WORK\build\v03_tex\v07_view_frontage`.

- **Trim.** The high-poly strips got the wall's joint: 10.5 mm wide at half depth, 5.5 mm deep, with
  a 3 mm bevel, at U 0 and U 0.5 (every 1.11 m) on S1, S2 and S5 and in the two beads of S3; six fine
  joints per tile on the arch moulding S4. The base level of the strips went from 3 mm to 6 mm so
  that the joint floor stays above the plane. The stepped lower edge of the interlace band came from
  the bead's own section (a foot of 73 degrees gave three steps in three pixels); the bead now has a
  foot of 42 degrees. The model has 909,156 triangles. The Painter bake agrees with a new Blender
  reference (normal 0.33 degrees, height correlation 0.99988). The joint colour goes through a joint
  map drawn from the strip functions (`rivendell_trim_maps.py --joint-map`), because a height
  threshold misses a joint that cuts a raised moulding. Measured at 1024 (`tools/painter/
  trim_joint_check.py`): joints 41 to 47 levels under the face (the wall has 43), 6 px wide (the wall
  5); the flat faces equal the wall face to 0.0 levels.
- **On the wall:** the joints of the coping and the string course show in the near and middle views.
  In the far front view they are barely visible.
- **Plinth.** `plinth_a`: the recipe of the last round with no rotation, angle or depth spread,
  roundness 0.3, `normal_intensity` 0.16; luminance 167 (12 percent under the wall), red 24 above
  blue (the wall has 24.5), tilt 0.150. `plinth_b` has luminance 160. The joints are only a little
  straighter: no parameter of the material does more.
- **A trap of this stage:** a project texture imported from a file path can stay dead. The first
  import of the joint map gave a mask with no effect; the same bytes under a new file name worked.
  Give every changed mask file a new name and prove each mask with a false-colour export.
- **Open:** the frieze joint cuts through the small shaft in each arcade pier; the arch moulding
  joints end in small diagonal ticks; the plinth reads patchy from the material's own colour spread.

## Round 2b: what three judges said, and the plan for round 3 (2026-11-24, 23:12)

The set of round 2b (`set_final`: stone `alt_calm_p3`, the baked trim, the plinth `d_2x3_tone`) got
6.5 of 10 from each of the three judges, on the wall of version 2 and on the new wall of version 3.
The round 2a best, rendered again with the new close view, got 6.0. All three said again: not yet
good enough to show as the smooth stone look. The view tool has a new view for this, `face_close`:
2.5 m in front of a bay field, in both lights.

| What the judges wanted in round 2a | State after round 2b |
|---|---|
| No round blotches of tone | Gone in `p2` and `p3`. A new fault took their place: alternate courses are light and dark (course means 180 to 200), so the wall reads striped from far |
| Block edges that catch a low sun | They catch it now, but as a ragged, chipped lip on one side of each joint. Close up that reads as rustic stone, the opposite of smooth. The round 2a preview had the cleaner edge |
| Joints in the coping and the string course | Not solved: the joints of the baked strips are 1 to 2 px wide and cannot be seen on the wall |
| A plinth of larger, calmer blocks | Solved: the blocks are 1.5 times larger and the relief is 1.6 times the wall's, not 3 times. The art judge wants it lighter and of the wall's hue |

More findings: the detail normal map makes the face look tooled and gritty in the close view and is
dropped for the wall face; the baked interlace and arcade read as cut stone; beside the Gondor wall
our colour and grain are now equal in grade, and our joints are thinner, paler and flatter (field
tilt 0.125 against 0.18 to 0.34).

**Plan for round 3 (Painter, from 23:20):**

1. Stone, from `p2`: a straight, crisp edge on all four sides of each block (a bevel of 12 to 15 mm,
   no jitter, a continuous light line of 2 to 3 px), chips on fewer than 1 block in 8; a smoother
   face (less of the material's own relief); joints 10 to 11 mm wide, about 40 to 45 levels under the
   face, with enough recess that the field tilt reaches 0.16 to 0.18; tone per block of plus or minus
   4 to 6 levels with equal course means (within 3 levels), so that no course is lighter than the
   next; streaks at a third or off; a warmer colour (red about 24 above blue at luminance 190).
2. Trim: wider and deeper joints in the high-poly strips of the coping and string course (every
   1.11 m), joints in the frieze and the arch moulding, a clean lower edge on the interlace strip;
   bake again; joints as dark as the wall's.
3. Plinth: one small step darker than the wall (about 12 percent), the wall's hue, straighter
   joints, relief toward 0.15.
4. Renders on the wall of version 3 with `face_close` and the far front view; judges again.

## Painter probes (2026-11-24, 21:35 to 21:52): seven questions, answered by a run

One worker drove Painter. Scripts: `tools/painter/painter_probe_*.py`; outputs:
`WORK\painter_lab\probes`.

| Question | Answer | Use |
|---|---|---|
| Can Painter give a picture of the result? | No. The hidden `_substance_painter.display.save_viewport(path, w, h, flag)` writes a file of the asked size, and every pixel is flat grey; a Qt grab is black too. The camera can be set by script, which does not help | The preview stays: export, then Blender |
| Does an export larger than the texture set compute again? | Yes. A 1024 set exported at 2048 equals a 2048 set exported at 2048. A 2048 set exported at 1024 is a clean reduction of the 2048 result: smoother colour grain (5.8 against 7.7) and a richer normal map and occlusion than the 1024 set gives | Work at 2048 with 2048 layout maps and export at 1024: `tset.set_resolution(ts.Resolution(2048, 2048))` |
| Does an 8 bit height channel make steps in the normal map? | Yes: 3 normal values over a 16 px ramp with `L8`, 8 smooth ones with `L16F` | Keep Height at `L16F` (`stack.edit_channel(ChannelType.Height, ChannelFormat.L16F)`); a new project has it |
| A new project by script | Works: set the `OCIO` variable around `project.create(mesh_file_path=..., settings=project.Settings(default_texture_resolution=1024, normal_map_format=NormalMapFormat.DirectX))`, pump events until a texture set exists, `project.save_as(path)`. The texture set takes the FBX material name. A new project has no AO channel | The trim sheet bake starts this way |
| The names of the bake parameters | `FilterMethod` (match: always 0, by mesh name 1), `LowpolySuffix`, `HipolySuffix`, `BackfaceCullingSuffix`, `CageMode` (distance 0, automatic 1, file 2), `CageMesh`, `HipolyMesh` (a `file:///` address from `QUrl.fromLocalFile`). The Height baker has `MaximizeRange` and `Divisor` and uses the common `MaxHeight` and `MaxDepth`. A low-as-high bake of AO and curvature at 1024 took 5.4 s through the bridge | Full table: `probes\bake_api\keys.json` |
| Rounded block edges inside Painter | The filter `bevel/bevel` on a Height-only fill whose source is the layout height (`distance` 0.01, `smoothing` 1.0, `uv_tiling` 1) rounds each block shoulder over about 8 px, and the joint on the tile border matches an inner joint. With `uv_tiling` 0 the border joint is wrong. `blur/blur` gives a coarser edge. `bevel_smooth` gave a flat result. An anchor point also works; then hide the source layer or the height counts twice | The stone stack of round 2b |
| Masks that do nothing | None in the `lim_tuned_b` stack: the joint mask covers the edge map exactly, the stain mask correlates 0.9999 with its map | The false-colour check stays in the workflow |

## Painter traps of this work (Painter 12.2.0, 2026-11-24)

The general trap tables are in [substance-painter-mcp-setup.md](../reference/substance-painter-mcp-setup.md).
The chariot work of the same night wrote a section "Traps on Painter 12.2.0" into that file on its own
branch (`feat/rhun-chariot-variants`), with the traps that both sessions met: set a fill's
`active_channels` before `set_source`; an imported greyscale PNG is sRGB until it is set to Raw; an
imported bitmap mask can have no effect, so keep an unmasked base layer and prove each mask with a
false-colour export; a procedural source has no colour space list; the texture set takes the FBX
material name. This worktree does not change that file, so that the two branches do not collide.
The rows below are the ones that only this work found:

| Symptom | Cause | Fix |
|---|---|---|
| A map made by a script must become a layer source | A PNG is not a shelf resource | `resource.import_project_resource(path, resource.Usage.TEXTURE, name=...)` returns a `Resource`; pass its `.identifier()` to `fill.set_source(channel, id)` or, inside a mask, to `fill.set_source(None, id)`. Then `get_source(...).set_color_space(Raw)` for data (masks, height, occlusion) |
| Joints must cut the height of a stone material | A fill can carry a Height source alone | A fill on `Height` only, source = the edge map, `set_blending_mode(BlendingMode.Subtract, Height)`; the depth is `set_opacity(value, Height)` |
| The exported normal map has the wrong green sense for the engine | The export virtual map `Normal_OpenGL` is green up | Export `Normal_DirectX`. Proof on an export: corr(R, dh/dx) and corr(G, dh/drow) have the same sign when green is down |
| A material's random seed gives no second variant | Three of seven tested downloads ignore `$randomseed` | Change the layout or the UV scale of the fill, or use two layouts |
| A list argument reaches the Painter script as letters | `painter_run.py --arg names=a` parses a bare word as a string | Pass JSON: `--arg names=["a","b"]` |
| "'list' object is not callable" on a property | `label`, `widget_type`, `enum_values` are methods on some objects and plain attributes on others | Call a value only when it is callable |
| A file from Painter lands in a folder `E:\e\...` | A path in the MSYS form `/e/...` was passed to Painter | Pass Windows paths with forward slashes: `E:/...` |
| A new fill doubles the height of the layer under it | The default Height blend of a new fill is `LinearDodge` (add) | Set the blend to Normal, or hide the source layer that only feeds an anchor |
| A filter changes channels that it should not | A filter effect's `active_channels` default to all channels | Set `{ChannelType.Height}` (or the one channel) on the effect |
| A false-colour check reads coverage where no layer is | With one layer visible the empty area exports sRGB 231 (linear 0.8), not black | Read the coverage from green, or from red minus green |
| A 16 bit normal map looks coarse in a check script | PIL reads a 16 bit RGB PNG as 8 bit | Read it with another reader before you judge the depth |
| `painter_run.py` prints only "a project is open" after a create | The create and the save had worked; the message came from a later guard | Ask for the status after such an error before you run the step again |
| "Cannot insert an effect at the top of the layerstack" | A filter cannot stand alone at the top of the stack | Put the filter into the content stack of the fill that it changes |
| The joints come out darker than the stack says | `convert_one` multiplies 35 percent of the exported occlusion into the diffuse by default, and the stack already has its own joint occlusion | Pass `ao_strength 0.0` for a stack with joint occlusion |
| A Painter-side script fails on `import numpy` | Painter's Python has no numpy and no PIL | Read and measure images on the host; the Painter script returns paths only |
| A host preview and Painter give different strengths for the same opacity | Painter blends in linear light | Use linear-light formulas in the preview |
| The bake is cut off or wrong and no error shows | A front ray distance shorter than the relief; or distances taken for metres (Painter reads centimetres); or `RelativeScale` on, which makes them fractions of the bounding box | Distances in centimetres, `RelativeScale` off, `MaxHeight` above the highest relief |
| A bake gives maps of zero | Match by mesh name with names that do not pair (`trim_low` against `S1_high`) | `FilterMethod` Always, or names that pair by the suffixes |
| A second bake looks like the first, or a sweep of settings drifts | Bake parameters stay in the project between runs | Set the curvature method first, set every key to its default, then set your keys |
| The normal relief of a baked sheet is twice too strong | A layer carries the baked normal map, and Painter adds the baked map to the export by itself | Put the baked normal into no layer |
| A plane exported again from an imported FBX is 100 times too large | Blender's FBX import of a Kit-scale file | Export the plane from the source `.blend` |
| The baked height scale changes with the data | The Height baker's default range follows the data | `MaximizeRange` "Relative to ray distances" for a fixed scale |
| The stains of a vertex colour look much too dark in a Blender picture | Blender hands a byte colour of 0.80 to a material as 0.604 (linear); the engine's shader multiplies the diffuse by the stored value (`standart.rsh`: `albedo_color.rgb * vertex_color.rgb`; `get_vertex_color` only swaps channels) | `blender_view_textured.py` turns the value back with a gamma of 1/2.2 (since 23:19 on 2026-11-24; pictures of version 3 pieces made before that show the stains too dark) |
| The course heights double and the course numbers are not numbers | `painter_stone_layout.py --from` got a layout that is already 2048 px | Give it the 1024 px layout folder; it scales by 2 itself |
| More relief does not come with a higher opacity | A fill's opacity cannot pass 1 | Add a second relief layer, or change the bevel |
| The bed joint of the plinth is not at the top of the plinth on the wall | The texture was rolled for a V that the mesh does not have: the roll tool took V 0 at the ground, and the slope rule of the mesh projects from the world origin (V -0.2736 at the ground) | Read the UV of the built mesh before a texture is aligned to a level on it. Version 4 fixes the mesh rule: V 1.3767 at the top edge of every plinth face |
| Two runs overwrite each other's layout | The stone driver named the layout folder by the last part of the run name: `plinth_d` and `stone_d` both wrote `layout_d` | The layout and config folders carry the full run name (`layout_stone_d`, `layout_plinth_c`) |
| A darker joint colour does not give the dark tail of the control | The joint floor is 3.5 percent of the tile; its colour moves the 5th percentile little | A contact shade beside the joint (a halo layer, Multiply). It widens the joint as measured, so state both widths |
| The trim joints come out darker than the wall joints with the same colour | The cavity layer of the trim texture adds to the joint colour | Lower the cavity (0.40 to 0.10) and measure with `trim_joint_check.py` |
| The trim bake check reads about 10.6 degrees | A Painter export in the DirectX form has green down; the Blender reference has green up | `trim_bake_check.py --flip-green` |
| The trim high-poly test fails after more joint lines | The model has 987,852 triangles; the test limit is 1,000,000 | Count before a new joint or line is added |
| A broad soft relief on the face reads as dents in a low sun | A feature larger than about 5 cm in the normal map of a dressed face reads as a fault of the wall | Keep the relief of a smooth face fine (grain, tooling, pits) and measure the low-pass share of the tilt |
| Brush swirls in the normal map of `slate_tile` | Its `pattern_strength` has the default 0.4 | Set `pattern_strength` 0 |
| The joints of a dark set turn bluer than its faces | The colour was moved to the target by an additive shift | Use a gain per channel for a dark set |
| The seam measure reads 2 to 4 on a slate tile that wraps well | The tile edge lies on a joint of the pattern | Compare with the same phase of the pattern inside the tile |
| A message to a worker that drives Painter starts a second driver | The harness starts a copy of a running workflow agent when it gets a message | Never message a running workflow agent; stop the workflow at a stage start, change its script and resume |

**The engine's `_s` map, blue channel: settled between the two sessions.** The chariot cart pipeline
wrote the material's specular level into blue, and its first note called the meaning open. The
evidence of this work says blue is ambient occlusion: the vendor's Material Editor page ("Blue
channel contains Ambient Occlusion"), and the shader source `<game>\Shaders\Sources\standart.rsh`
line 1289, `ambient_occlusion_factor *= pp_modifiable.specular_sample.b * g_ambient_occlusion_coef;`,
with `specularity_info = pp_modifiable.specular_sample.xy` at line 1162 for red and green. The 21
control maps agree (blue near white, darker in joints). The chariot session read the same lines on
2026-11-24, found `use_specular` on its cart materials, and changed its builders to write the baked
occlusion into blue (its commit 08738388b on `feat/rhun-chariot-variants`, which also carries the
shader lines in the setup document). The rule holds for the `pbr_metallic` shader with the flag
`use_specular`; the legacy `pbr_shading` reads red in another way.

## Sharing Painter between two sessions

One Painter runs on the desktop, and it holds one project. When two sessions need it in one night:

- The lock is the work claim key `painter-instance` (`python tools/work_claims.py claim|check|release
  painter-instance`). The holder owns the open project.
- `python tools/painter/painter_run.py tools/painter/painter_project_session.py --arg action=status`
  shows the open project. `--arg action=open --arg path=<spp>` opens the holder's project and refuses
  if another one is open. `--arg action=save_close --arg expect=<spp>` saves and closes it and
  refuses to close any other project.
- After a pass: save and close, release the key, tell the other session in one line. No session
  closes Painter or a project that it did not open.
- First used on 2026-11-24 between the kingdom kits session and the chariot session.

## Not checked

- Nothing was imported in the Modding Kit or seen in the game. Tone in the Blender pictures is not
  the engine's.
- The green sense of the normal map as the player sees it, and the detail normal scale flag.
- How a 1024 px set looks at 461 pixels per metre beside the 683 of the Gondor pieces in one scene.

### A carved band round a pier: map it along its path

A trim band that turns corners (round a pier, round a tower) takes its U from the length of its path, at one density,
with no jump at a corner. The density is set so that the distance between two joint planes holds a whole number of
tiles; on the Rivendell wall a bay holds 4 tiles at 0.486 U a metre in place of 3.70 tiles at 0.45. A closed path round
a tower holds whole tiles.

Why not a jump of "whole pattern units" at a corner: the braid strip repeats its two strands every half wave, its bead
rows every bead pitch, and which strand is on top only every four half waves. A jump of three half waves met the first
two and broke the third (17 mm of relief at every inner corner, 2026-11-25). A jump of a whole period is exact and puts
all of the difference on one short face (30 percent denser there). The path rule spreads it evenly.
