---
name: prep-tripo-prop
description: Use when a Tripo AI FBX must become a Bannerlord prop or map icon, or a prepped one needs new UVs, Painter textures or a rebuild from solids.
argument-hint: [<tripo fbx> ...]
---

# Prepare a Tripo prop or map icon

A Tripo export is a dense triangle soup with a fragmented UV atlas and a texture nobody can paint.
This skill makes it a game asset by one of two routes: decimate it and re-UV it, or, when its
sculpted geometry melts under decimation, rebuild it from clean solids. Either way Substance Painter
paints it from baked mesh maps, the textures are packed into `t_<stem>_{d,n,s}`, and it goes to the
Modding Kit. Every step is a script, so a run can be repeated and tuned.

**Owning docs:** the UV method data is in
[ue-to-bannerlord-asset-pipeline.md](../../../docs/reference/ue-to-bannerlord-asset-pipeline.md)
("Painter retexture variant", "Rebuild variant"); the rebuild steps, icon conventions and registry
in [map-icons.md](../../../docs/kitbash/map-icons.md); the Painter bridge and its traps in
[substance-painter-mcp-setup.md](../../../docs/reference/substance-painter-mcp-setup.md). Tool flags
are in each script's docstring; the scripts are listed in [tools/README.md](../../../tools/README.md)
"Tripo props and map icons".

## Before you start

- A GitHub issue for the asset (AGENTS.md "Documentation duty").
- Staging goes to `E:\LOTRAOMAssets\_export\<asset>\`. Writing into a live module's `AssetSources\`
  needs Mike's word, and never overwrites a file without a backup.
- Blender: `"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe"` (written `blender-launcher`
  below). With `-b` it detaches and prints nothing; wait for the script's `_report\DONE.txt`, which
  says `ok` or `error` with the trace (`tools/blender/blender_job.py`).
- Painter: start `C:\Users\mikew\substance_painter_mcp\launch-painter-remote.bat`.
  `python tools/painter/painter_run.py <script> [--arg k=v] [--args-json '{...}']` needs only that
  bridge. In `--args-json`, write paths with forward slashes: a backslash is a JSON escape.

## Phase 1: Analyze

1. Probe every candidate and the asset it replaces:
   `blender-launcher -b -P tools/blender/probe_fbx.py -- <in.fbx> <out.json>`.
2. Look at each `.fbm` texture. A Tripo atlas is noise: plan to retexture, not to paint on it.
3. Set the triangle budget from the reference asset. Map icon textures are 1K at most; prop textures
   are diffuse 2K and the rest 1K.
4. Report triangles, textures and atlas state per candidate before changing anything.
5. **Pick the route.** For quad retopology, use the
   [required toolkit selection](../../../docs/features/cube-map-remesh.md#required-workflow-selection)
   and Phase 2A step 1. Decimate when the model has flat faces worth keeping: Phases 2A, 3, 4, 5.
   Rebuild when it is a sculpt (rounded corners, dripping walls read as melted wax at any triangle
   count in the Kit, and decimation cannot add hard edges back): Phases 2A step 4 (for the scaled
   source only), 2B, 4, 5. Show Mike the decimated mesh beside the source in Blender before you decide.

## Phase 2A: Remesh or decimate and re-UV

1. **Run quad retopology before UV probes.** For an eligible closed, manifold, star-shaped mesh:
   `blender-launcher -b <scene.blend> --python-exit-code 1 -P tools/examples/cube_map_active.py -- --quads <n> --work-dir <job> --output <staging>\<mesh>_low.fbx`.
   For open, nested, concave, or organic retopology, use `quad_remesh` or `remesh_in_place` from
   `tools/blender/quad_remesh_job.py`; `tools/examples/remesh_active.py` is the invocation template.
   Never call `xremesh.exe` directly. Feed the resulting FBX to the prep script as `--src` and omit
   `--decimate-tris` unless further polygon reduction is explicitly wanted. Preserve the source and
   inspect the remesh report before continuing.
2. Probe two UV methods on the remeshed or decimated mesh, in parallel, each with its own `--staging`
   folder (two runs in one folder overwrite each other's report). Same other arguments, then
   `--uv-method sharp_seams --probe-spreads 50` for one and `--uv-method chart_abf --probe-spreads 75`
   for the other:
   `blender-launcher -b -P tools/oneoff/blender_prep_tripo_prop.py -- --src <fbx> --dst <dir> --staging <dir> --name <mesh> --material t_<mesh> --scale-mode length --size <m> --decimate-tris <n> ...`
3. Pick from `_report\DONE.txt` with the targets in the pipeline doc (use at least 40%, texel density
   p95/p5 at most 2.5, folds under 1%). `sharp_seams` wins on a clean low poly; `chart_abf` 75 on a
   decimated Tripo surface. Never `chart`.
4. Full run with the chosen method. Add `--skip-bakes --export-high` when Painter will retexture:
   it writes `<mesh>.fbx` (with `bo_`) to `--dst`, and `<mesh>_low.fbx` and `<mesh>_high.fbx` to
   `--staging`. Without `--skip-bakes` the Tripo maps are rebaked onto the new UVs instead.
5. To let Mike pick between candidates, run them without `--skip-bakes` and with `--dst` equal to
   `--staging`, then in GUI mode (no `-b`):
   `blender-launcher -P tools/blender/compare_prop_candidates.py -- <mesh> A=<dir a> B=<dir b>`.

## Phase 2B: Rebuild from solids, unique UVs, Painter

1. Measure the scaled source: `blender-launcher -b -P tools/blender/measure_tripo_layout.py -- --src <staging>\<mesh>_high.fbx --out <dir>\measure`.
   `layout.json` gives the height profile and tower peaks for the builder's layout table.
2. Copy `tools/oneoff/blender_build_carn_dum_icon.py` as the pattern: primitives that wind every face
   outward, a material slot and a `part` per face, and no face running down into the solid that hides
   it (the unwrap keeps a partly visible face at full texture density).
   `blender-launcher -b -P <builder> -- <dir>\vN`; read the renders, then open
   `<dir>\vN\<name>_blockout.blend` in GUI Blender for Mike. Iterate until he approves the shape.
   No texturing before that.
3. Unwrap, `--probe` first: `blender-launcher -b -P tools/blender/unwrap_texture_sets.py -- --blend <dir>\vN\<name>_blockout.blend --rules <rules.json> --out <dir>\vN\uv --name <name> [--probe]`.
   A run fails when faces look inverted over 0.1% of the area (a winding bug in the builder). Read
   every `uv_<set>.png`, and per set `visible_px_per_m`, `visible_share` and `hidden_kept_m2`.
4. Painter, then a preview:
   - `painter_run.py tools/painter/painter_new_project.py --args-json '{"low": "<dir>/vN/uv/<name>.fbx", "low_as_high": true, "uv_report": "<dir>/vN/uv/_report/DONE.txt"}'`
   - `painter_run.py tools/painter/painter_stacks.py` (sets matched by name suffix; colours and
     `sets` overridable through `--args-json`)
   - `painter_run.py tools/painter/painter_export.py --arg out_dir=<dir>/vN/paint_rM --arg spp=<dir>/vN/<name>.spp`
   - `blender-launcher -b -P tools/blender/render_painter_preview.py -- <dir>\vN\uv\<name>.fbx <dir>\vN\paint_rM <dir>\vN\paint_rM\preview [--albedo]`
5. Show Mike, iterate rounds, then Phase 4 with one triple per texture set.

## Phase 3: Retexture in Painter (decimate route)

1. `painter_run.py tools/painter/painter_new_project.py --arg low=<staging>\<mesh>_low.fbx --arg high=<staging>\<mesh>_high.fbx`.
   Expect `bake: Success` and six maps (AO, Curvature, Normal, Position, Thickness, WorldSpaceNormal).
2. `painter_run.py tools/painter/painter_stacks.py --args-json '{"sets": {"t_<mesh>": "frost_stone"}}'`.
   The docstring lists the recipes and keys. Another look is another recipe in that file.
3. `painter_run.py tools/painter/painter_export.py --arg out_dir=<staging>/painter --arg spp=<staging>/<mesh>.spp`.
4. `blender-launcher -b -P tools/blender/render_painter_preview.py -- <staging>\<mesh>_low.fbx <staging>\painter <staging>\painter\preview`.
5. Read the previews before you show them. A lit render can lie (tone mapping, a bad normal map):
   render with `--albedo` or measure the exported `baseColor` PNG, and to see what one layer covers,
   give its colour key a flat false colour through `--args-json` (`{"grime": [1, 0, 0]}` for the
   cavity grime; the docstring lists the keys, and an unknown key fails the run), export and look.
   Show Mike the previews and the `.spp`; iterate until he approves.

## Phase 4: Convert and hand off

1. Convert: `python tools/oneoff/convert_tripo_prop_textures.py --src <export dir> --dst <dir>\converted --max-size 1024`
   plus `--stem t_<mesh>` for the one set of the decimate route. Without `--stem` every set in the
   folder becomes `t_<set>`. It refuses to overwrite (`--force`), warns on a flat normal, and
   "metal map is near-constant" is correct for stone.
2. Check every Kit name is free. `python tools/validate_mesh_refs.py --check-name <mesh>` sees
   metamesh names only, so also search the faction's `AssetSources` and `Assets` folders for each
   material and `t_` texture name: the Kit binds materials by name module-wide and a same-named older
   one wins silently.
3. With Mike's word, copy the FBX and the textures into the Kit folder, new names or with a backup,
   and compare hashes after. A prop takes `<mesh>.fbx` (with its `bo_`). A map icon takes
   `<mesh>_low.fbx` renamed `<mesh>.fbx` (decimate route) or `<dir>\vN\uv\<name>.fbx` (rebuild
   route), no `bo_`, into `TAOM_Map\AssetSources\Campaign Map\Map Icons\<faction>\`, textures in
   `textures\`; conventions in [map-icons.md](../../../docs/kitbash/map-icons.md). Read back in
   Blender, the FBX sits at rotation X 90 and scale 0.01: the Kit's proven format, not a fault.
4. Mike restarts the editor (it scans resources at startup only), imports the textures, then the
   mesh. One Kit material per FBX material, named exactly as the FBX material (`t_<mesh>` on the
   decimate route, the set names on the rebuild route), with `use_specular` on, or the `_s` texture
   is never read. For a map icon he points the settlement entity's child `meta_mesh_component` in
   `SceneObj/Main_map/scene.xscene` at the new metamesh. Change only that one entity: a faction icon
   metamesh is shared (`gundabad_town` draws three settlements).
5. In game: right scale, no pink or white texture, siege and gate slots in place.

## Phase 5: Records

Progress comment on the issue with the numbers. A map icon gets a row in the registry in
[map-icons.md](../../../docs/kitbash/map-icons.md); a prop gets one in its kit's row of
[docs/kitbash/README.md](../../../docs/kitbash/README.md). Either names the mesh, the Kit files and the
staging folder, which is the source of truth because the live modules are unversioned. New lessons
go to `docs/reviews/lessons/build-tooling-workflow.md`, a better default goes into this skill.

## Top traps

| Symptom | Fix |
|---|---|
| Painter refuses every color ("non legacy color space") | The project is Legacy: create it with `painter_new_project.py` (OCIO) |
| Every exported colour lighter than written | A fill takes numbers as linear: the recipes' `rgb()` converts sRGB first |
| Blotchy lit preview, flat albedo | Height or a material's normal tilts the normal map: no relief; set `active_channels` after `set_material_source` |
| A layer has no effect | Its feature is under 2 texels, or its mask polarity is wrong (`bricks_01` is white in the joints): false-colour it |
| Whole walls dark | AO baked too deep with a ground plane: `ao_distance` 0.05 |
| Frost or another world-space mask covers every wall | Walls sit at 0.5 in that mask: raise `ws_balance` |
| Blotches that break at UV island edges | Generator grunge runs in UV space: `Use_Triplanar=1` |
| Thousands of islands, single-digit UV use | Sharp seams on a decimated mesh: use `chart_abf`; on a rebuilt mesh, `unwrap_texture_sets.py` nets |
| Converter finds no roughness or metallic | Painter's glTF export packs them in one ORM map; the converter reads R AO, G roughness, B metallic |
| Boxes inside out in game, fine in Blender | Faces wound inward: Blender and Cycles draw back faces. `unwrap_texture_sets.py` fails on them |
