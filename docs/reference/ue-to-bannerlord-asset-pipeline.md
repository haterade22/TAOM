# UE → Bannerlord Asset Pipeline (Rivendell / Tents, 2026-07-15/16)

How a purchased Unreal Engine kit (or raw-FBX marketplace pack) becomes a Bannerlord kit under
`TAOM_Map/AssetSources/Scenes/<Kit>/`. Distilled from the ElvenForestCity (UE 5.1 → Rivendell) and
Fab Medieval Tent Collection (→ Tents) conversions. Scripts: `tools/oneoff/*rivendell*.py`,
`tools/oneoff/convert_tent_textures.py` (registry: `tools/README.md` § UE→Bannerlord asset pipeline).
Review record: `docs/reviews/rca-asset-pipeline-tools-2026-07-16.md`; Blender gotchas:
`docs/reviews/lessons/build-tooling-workflow.md` (2026-07-16 entries).

## Pipeline stages

1. **UE bulk export** (`ue_export_rivendell.py`, headless: `UnrealEditor-Cmd.exe <uproject>
   -run=pythonscript -script=<py> -EnablePlugins=PythonScriptPlugin -stdout -unattended -nosplash
   -nullrhi`). A content-only 5.1 project opens fine in a newer editor (5.7.4 used); export-only, the
   source project is never saved. Produces per-asset FBX (UCX collision riding along, LOD0 only),
   TGA textures, and `material_bindings.json` — the mesh→material→texture-parameter map that drives
   every later naming decision. UE *LEVEL* exports (File → Export All) are a different beast: full-res
   Nanite triangle soup, light actors, `_LOD`/UCX baked in depending on dialog checkboxes (uncheck
   "Level of Detail" + "Collision"), instanced (multi-user) objects with negative (mirrored) scales.
2. **Blender normalization** (`blender_normalize_rivendell.py`, headless — see invocation note below).
   Per mesh: world-space data bake, cm-vintage detection, lowercase `sm_<kit>_<stem>` naming (the
   engine lowercases mesh IDs on import), `bo_` collision twin (UCX joined when present, else
   decimated copy) carrying a **physics material as a material slot named after the physics id**
   (`stone`/`wood` — Erebor precedent), weld+decimate for Nanite-density sources, per-FBX meshlist
   dumps. 8-way sharding cut 460 meshes to ~5 min.
3. **Texture conversion** (`convert_*_textures.py`): metal-rough → spec-gloss triples
   `t_<stem>_{d,n,s}` (+`_h`). `_s` packing (empirical, verified against shipped Gondor/Mirkwood
   kits): **R = metallic, G = gloss (255−roughness), B = AO**. UE ORM confirmed R=AO/G=rough/B=metal
   from master-material parameter names — never assume a packed map's layout; a constant-high metal
   channel read wrong **blacks out the diffuse**.
4. **Material generation** (`build_rivendell_material_sheet.py` → `generate_rivendell_materials.py`):
   materials are named **exactly like their texture set including the `t_` prefix** (user decision —
   editor material creation then points straight at its textures; deliberate exception to the
   kitbash `m_`/`t_` split). Same-set UE instances merge; `_foliage`/`_translucent` suffixes mark
   different shader-flag families. The generator clones a hand-made template `_mtl.tpac` per row.
5. **Modding Kit import**: textures first, then meshes; slots bind by name. The editor only scans
   resources at STARTUP — write tpacs with the editor closed; re-import overwrites in place.

## Hard-won facts (verify-before-reuse quality)

- **tpac material files**: one `<name>_mtl.tpac` per material under `Assets/.../textures/`; tpac v2
  container (layout == `tpac_skeleton_inject.py` parsing); 338-byte meta for the standard opaque
  config with three 16-byte texture item-GUID slots (d/n/s); texture GUID = bytes 52:68 of the
  `_tex.tpac`; header bytes 28:36 = TOC size (filesize−36). **The 8-byte post-meta checksum is NOT
  validated by the editor** (9 hash algos × 7 slices found no match; copied-verbatim pilot loaded).
- **Blender on this machine is the MS-Store app**: raw `blender.exe` is ACL-blocked — invoke
  `%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe -b -P <script> -- <args>`; it DETACHES
  (no stdout/exit code) — completion protocol is a DONE/report file. GUI viewers: defer imports via
  `bpy.app.timers.register` (startup `-P` operator calls die silently).
- **Blender headless staleness** (4 incidents): `view_layer.update()` before reading
  `matrix_world`/`dimensions`/`bound_box` after import/join/transform_apply; `transform_apply`
  silently SKIPS multi-user data (make single-user or bake at data level); baking a
  negative-determinant matrix requires `flip_normals()`; weld (1 mm) destroys paper-thin
  double-shell geometry (drapes) — only weld above the decimate target, and know that thin pieces
  inside a large join are still exposed (open limitation).
- **UE level exports are triangle soup**: collapse-decimate shaves ~2% until a weld rebuilds
  connectivity; per-ASSET exports have real connectivity and decimate fine. Whole-level merged
  meshes and per-structure single meshes both failed the quality bar for buildings — decimating
  already-decimated stamped pieces melts flat architecture. Assembled-scene direction is PARKED
  (per-piece editor prefabs — the native Erebor/Mirkwood pattern — is the quality-lossless option).
- **Mesh IDs are globally unique** in the engine; a kit may ship as ONE FBX with many uniquely-named
  meshes (Mirkwood pattern — used for `tents_medieval_kit.fbx`).
- **Fab vault downloads can be partial**: the tent collection shipped texture zips for only 3 of 6
  tent families; re-download missing "additional files" from the product page before converting.

## Single-prop path — Tripo AI assets (2026-07-25 throne; 2026-07-28 Gondor ships)

A Tripo-generated FBX (single mesh, embedded `.fbm` JPEG textures: basecolor/normal/roughness/
metallic, no AO, ~1-unit normalized scale) is a different beast from a UE kit: the batch stages
above don't apply, but the auto-UV atlas is hundreds of fragmented islands — unpaintable in
Substance. The path (`tools/oneoff/blender_prep_tripo_prop.py` — named
`blender_prep_witchking_throne.py` for the pilot — + `convert_tripo_prop_textures.py`):

1. Scale to real-world height, pivot to base centre, kit-rename (`sm_mordor_mm_throne_001`).
2. **Chart re-UV, not Smart UV Project.** Probed on the throne's 42.7k-tri organic
   triangulation, `uv.smart_project` produced 1,485–2,112 islands at 17–24% utilization at every
   angle limit (66–89°) — worse than the Tripo atlas (298 / 53%). The script's xatlas-style
   charter (BFS region-growing gated on angle to the chart's area-weighted normal, sub-20-face
   fragments absorbed into the most-shared-boundary neighbour, planar projection, per-chart
   texel-density equalization, `uv.pack_islands`) landed 128 islands / 57% / 1.4% fold-over at
   spread 75°. Probe first (`--probe-angles` / `--probe-spreads` report islands + utilization +
   flipped faces without baking); spread ≥90° halves the island count but fold-over jumps to
   5.6–10% — bake artifacts.
3. **Rebake selected-to-active onto the new UVs** (identical geometry, 0.02 cage): EMIT bakes
   through the source JPEGs for basecolor/rough/metal, tangent NORMAL bake (carries the source
   normal-map perturbation into the new tangent space), fresh geometry AO (Tripo ships none).
   Per-bake min/max/mean in DONE.txt catches an all-black pass; a Cycles preview render in the
   staging dir catches seam garbage before any editor time.
4. `convert_tripo_prop_textures.py` packs the plain maps into the `t_<stem>_{d,n,s}` triple —
   and is the **Substance round-trip**: paint on the prepped FBX, export plain PBR PNGs, re-run
   with `--src <export dir>`. Normal-map relief direction stays smoke-test-arbitrated
   (`--flip-green`).

**Multi-million-tri variant (Gondor harbor ships, 2026-07-28):** Tripo "detailed" exports run
~1.9M tris. `--decimate-tris 40000` decimates the visual AFTER the full-res bake source is
duplicated and with the UV layer stripped first (UV-boundary preservation fights a 0.02-ratio
collapse) — the rebake becomes a true high-to-low bake, the normal map absorbing the lost
geometry; the cage auto-scales (`max(0.02, 0.005 × max_dim)`) to clear the decimation gap on
hull-sized props. `--scale-mode length` rotates the longest horizontal extent to +X and scales
it to `--size` (Tripo props are not consistently oriented — one of the three ships was
length-along-Y). Ship results at spread 75°: 108–171 islands, fold-over 3.5–9.2% — concentrated
in thin rigging/chain cylinders where mirrored texels are invisible; all three passed the
preview-render check, so the flipped-face gate is a *prompt to look at the preview*, not an
auto-fail.

## Current state / open items

Rivendell modular kit + 204 materials + textures: done and imported-ready. Tents: meshes + 10 sets
done; Wide/On_Sticks textures pending user re-download. Open: foliage material shader flags (need
one hand-configured sample to clone), 16 manual translucent materials (`material_sheet.csv` notes
column), assembled-scene direction decision, deferred review items (stem-map sidecar, `sanitize()`
unification — see the RCA).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/README.md](../kitbash/README.md)

<!-- backlinks-end -->
