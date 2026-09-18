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

## Kit-composition path — new pieces from measured existing parts (2026-07-28/29)

The third acquisition path (after UE kits and Tripo props): compose NEW kit pieces
programmatically from parts already in a TAOM kit. Proven by the **Lond Cirion wall kit** — 8
ploppable sections up to a full ~600 m city ring, assembled from the Gondor castle L3 pieces by
`tools/oneoff/blender_assemble_lond_cirion_wall.py`. The discipline: measure every piece
interface first (`blender_dump_fbx_inventory.py` + vertex/ray probes), compose by matrix,
verify by tri-sum + render, iterate against the user's in-editor placements. Full catalog,
registration facts, and the three composition laws (chirality, refill, verification):
[`docs/kitbash/lond-cirion-walls.md`](../kitbash/lond-cirion-walls.md).

## Fab acquisition and skeletal export (2026-09-17, Cave Troll Lightweight)

The fourth acquisition path: a Fab creature pack (skeletal mesh + AnimSequences + textures) rather
than a static kit. Script: `tools/oneoff/ue_export_cave_troll.py`, a sibling of the Rivendell
exporter. What the clips become afterwards is the retarget doc's job:
[`troll-race-arp-retargeting-workflow.md`](../ai-includes/troll-race-arp-retargeting-workflow.md).

### Getting a Fab purchase into a project at all (the launcher gotcha)

The Epic launcher's Fab Library "Add To Project" lists only projects the launcher itself can see,
and on 2026-09-17 it saw none. Read from disk, not guessed:

- The launcher had been running since 11:28; `E:\UE_5.4` (12:00) and `E:\UE_5.3` (12:50) were
  installed after that, and `C:\ProgramData\Epic\UnrealEngineLauncher\LauncherInstalled.dat`
  (mtime 11:28) still listed only `UE_5.7`. The per-item `.item` manifests under
  `C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\` did list both new engines as complete.
- UE 5.4.4, launched from that launcher, read the stale `.dat`, did not find itself, and
  registered as a CUSTOM build: `HKCU\SOFTWARE\Epic Games\Unreal Engine\Builds\{GUID} = E:/UE_5.4`.
  Every project it created carries `"EngineAssociation": "{GUID}"` instead of `"5.4"`.
- The launcher log (`%LOCALAPPDATA%\EpicGamesLauncher\Saved\Logs\EpicGamesLauncher.log`) then
  shows `Enumerating Projects From Engine Ver: {GUID}` looking in
  `E:/UE_5.4/Engine/Saved/Config/WindowsEditor` and finding nothing. For a launcher-installed
  version it would have read `%LOCALAPPDATA%\UnrealEngine\5.4\Saved\Config\WindowsEditor\EditorSettings.ini`,
  which is where the projects were recorded (`RecentlyOpenedProjectFiles`, `CreatedProjectPaths`).

Three ways round it, cheapest first. Every launcher-installed engine here ships
`Engine\Plugins\Fab\Fab.uplugin`, so the first needs no launcher fix.

1. **In-editor Fab plugin.** Open the project, Content Drawer > Fab (or Window > Fab), sign in, My
   Library, Add to Project. The pack lands under `Content/<PackFolder>/`.
2. **Repair the launcher's view.** Quit and relaunch the launcher so it re-reads installed engines,
   then right-click the `.uproject` > Switch Unreal Engine version > pick the launcher entry, which
   rewrites `EngineAssociation` to the plain version string. Tick "Show all projects" in the Add To
   Project dialog if the pack's supported versions exclude the engine.
3. **Direct download.** Some Fab products offer non-UE formats on fab.com > My Library; a plain
   Download gives a zip and no engine is involved. UE-only products do not.

Tell for the future: a `.uproject` whose `EngineAssociation` is a GUID while the engine folder has
a `.egstore` directory is a launcher build that mis-registered itself; the launcher will never list
its projects until the association is repaired.

### The skeletal export stage

`ue_export_cave_troll.py` runs inside the 5.4 editor (`UnrealEditor-Cmd.exe <uproject>
-run=pythonscript -script=<py> -EnablePlugins=PythonScriptPlugin -stdout -unattended -nosplash
-nullrhi`; the docstring has the full line). Roots come from `TAOM_UE_CONTENT_ROOT` (default
`/Game`) and `TAOM_UE_EXPORT_ROOT` (default `E:\LOTRAOMAssets\_export\cave_troll_lightweight`, a
staging folder: authored or exported creature FBX never goes straight into the Armory).
`TAOM_UE_INVENTORY_ONLY=1` stops after the inventory.

- `inventory.json` is written BEFORE any export: class per asset; per `AnimSequence` the skeleton,
  frame count, length and track (bone) names; per `SkeletalMesh` the skeleton, LOD count and
  material slots. The report we quote is this file, not a recollection.
- `SkeletalMesh` to `meshes/<sub>/<Name>.fbx`: LOD0, morph targets on, no collision.
- `AnimSequence` to `anims/<sub>/<Name>.fbx` with `export_preview_mesh` on, so each clip file
  carries the skinned mesh and rig that Blender and the Modding Kit need.
- `Texture2D` to `textures/<sub>/<Name>.tga`; `material_bindings.json` uses the Rivendell parameter
  walker, so the `_s` packing decision is again made from parameter names.
- Physics assets, Skeleton assets, Blueprints and AnimBPs are listed in
  `export_report.json` under `inventoried_not_exported`.

Verified against the 5.4.4 sources (`Engine/Source`, shipped with the launcher build):

- `FbxExportOption` fields: `ASCII`, `ForceFrontXAxis`, `VertexColor`, `LevelOfDetail`,
  `Collision`, `ExportSourceMesh`, `ExportMorphTargets`, `ExportPreviewMesh`,
  `MapSkeletalMotionToRoot`, `ExportLocalTime`. Python drops the `b` prefix and snake_cases.
- **A clip whose skeleton has no preview mesh does not export.**
  `UAnimSequenceExporterFBX::ExportBinary` (`EditorExporters.cpp:2250`) returns false with a
  warning when `GetAssetPreviewMesh` is null. The script assigns the pack's own skeletal mesh via
  `set_preview_skeletal_mesh()` before each clip, in memory only; clips with no mesh on their
  skeleton anywhere in the pack are reported under `anims_skipped_no_skeletal_mesh_for_skeleton`.
- Frame and length readers are `unreal.AnimationLibrary.get_num_frames` /
  `get_sequence_length` / `get_animation_track_names` (`UAnimationBlueprintLibrary`, ScriptName
  `AnimationLibrary`).

### The retarget stage: Mannequin clips onto `human_skeleton` (2026-09-17, all 52 clips)

> Run it with `--engine-skeleton tools/blender/human_skeleton_engine.json` (default root yaw mode `pose`).
> Without it the target is the TpacTool FBX rig, whose frames are not the engine's, and the clips fold in
> the Kit; the measured reason is in "What the Kit did with the FBX" below.

`tools/blender/retarget_mannequin_to_human.py` (headless Blender, plain `bpy`, no Auto-Rig Pro)
takes the exported clip FBX and bakes each onto the canonical
`E:\LOTRAOMAssets\human_skeleton_with_male_body.fbx`, exporting Kit-ready armature-only FBX to
`E:\LOTRAOMAssets\troll_clips_to_import\fab_cave_troll\` (52 clips, `troll_<clip>.fbx`) plus a
`_rootyaw` variant set of the 12 turn clips. The docstring carries the command line.

How it works, and the three decisions that made it match:

- **World-space rotation-delta transfer.** Per mapped bone, the source's world rotation away from
  its reference pose is applied on top of the target's aligned rest. This is independent of either
  rig's bone-axis convention (the Mannequin's bone tails point along UE X, the human's along Y), so
  no bone re-orientation, no ARP, none of ARP's three RCA'd workarounds.
- **Rest alignment by limb direction.** Each chain bone of the human is first swung onto the
  troll's rest limb direction, so the human skeleton stands in the troll's stance (hunch, bent
  knees). Clavicles and pelvis are NOT aligned: their direction is skeleton layout (the troll's
  shoulder girdle sits 0.4 m behind its spine top), and aligning them dragged the shoulders back.
- **Reference pose: bind pose for limbs, idle frame 1 for the trunk and head.** The bind pose says
  nothing about where a head looks; off the bind pose the human stared 40 degrees upward in every
  clip. Frame 1 of `free_idle_0` gives "both look ahead" for free. But an idle frame has the arms
  doing something (that frame holds the right forearm raised 133 degrees), and using it for the
  limbs baked a bent elbow into the walk. Hence `CLIP_REF_BONES` = spine chain + neck + head only.
- Root motion lives on the UE `root` node, which the FBX importer turns into the armature OBJECT;
  ignoring the object's animation gives in-place clips. Pelvis bob is scaled by the pelvis
  rest-height ratio (0.915 / 1.181). `--keep-root-yaw` folds the object's Z rotation back in for
  the turn clips (pelvis rotates 90/180 in place); which flavour the engine's turn codes want is
  the in-game test's call, so both sets are staged.
- Verification is the side-by-side Workbench render per clip in
  `_export\cave_troll_lightweight\retarget_preview\` (human body left, troll right, middle frame);
  an fcurve count is not a check. Round trip of the exported FBX: 28-bone
  `human_skeleton_notused`, take named after the clip, 289 fcurves, full frame span.

Blender 5.2.2 note: `Action.fcurves` is gone (layered actions); read
`action.layers[].strips[].channelbags[].fcurves`. The tool does; `arp_retarget.py` already did.

Owed after this stage (hand steps, `troll-race.md` Track 1): Kit import + compile of the staged
FBX, `_anm.tpac` clip metadata, `as_cave_troll_warrior` `act_*` overrides, Custom Battle smoke.
The six attack clips cannot drive melee (engine pose-blend), so bind movement, idle, hit-reaction
and death codes first.

### The clip stage: `_anm.tpac` metadata from vanilla templates (2026-09-17)

`tools/gen_troll_anim_clips.ps1` (Windows PowerShell 5.1 + TpacTool.Lib, the spider/elephant
`_clipgen` pattern). Facts it rests on, all read from the installed files this time:

- Vanilla's clips live in `Native/AssetPackages/animation_clips.tpac` (6,177 `AnimationClip` items);
  `animations.tpac` holds only the 4,052 `SkeletalAnimation` masters. Cloning the vanilla human clip
  of each type gives the flag lists, priorities, blend periods and usage objects verbatim: walk/run
  `make_walk_sound` + `BipMovIkUsage`; idle priority 1 + `allow_head_movement`; strike priority 80
  with `restart` + `enforce_root_rotation` + `update_bounding_volume` and the direction as
  `CombatParameterId` (`strike_front/back/left/right`); death priority 95 with the fall flag set plus
  `BlendUsage` + `DisplacementUsage`; taunts priority 64 + `lock_movement`. Vanilla has no standalone
  melee clip (melee is engine pose-blend), so attacks get `enforce_all` + `lock_movement` +
  `client_prediction` at priority 60.
- `Source2 = master Duration - 1` (walk master 38 -> clip 1..37, run 26 -> 1..25); Duration in
  seconds is the playback length, span / 30 for these.
- A Kit reimport of the FBX KEEPS the master's GUID (51 of 52 on 2026-09-18), so clips linked by GUID
  survive it and only `Source2` has to follow the new Duration; `gen_troll_anim_clips.ps1 -Verify`
  lists every STALE or ORPHAN clip and exits 1. The 52nd package had also been given a junk Skeleton
  item (`human_skeleton_notused.001`, made from that FBX on the first import) and came back as
  Skeleton + Geometry with no animation at all: the Kit then says "assigned skeleton animation not
  found" on its clip. Delete the junk skeleton, import that FBX again as an animation, then delete
  the clips and `-Apply`.
- The root motion the retarget dropped comes back as metadata, which is how vanilla's own in-place
  clips work: `BipMovIkUsage.LoopDisplacement` = measured travel per loop (1.37 m walk, 3.01 m run,
  human scale), `DisplacementUsage.DisplacementVector` = (0, forward travel, 0) on deaths (+1.15 m
  forward, -0.61 m for the backward fall), step points = the two foot-plant times.
  `tools/blender/measure_fab_clip_roots.py` measures them from the Fab sources.
- Template strings that belong to the human are cleared: facial ids, the taunt's `Fear` voice and
  `afraid` foley, the idle's soldier-armour foley.
- A master the Kit imported without an owner skeleton has 16 zero bytes right before
  `BoneNum=28, Duration` in its metadata; the tool patches the `human_skeleton` GUID there in place
  (backup `.bak-preskel`). TpacTool's `Save` is not used on masters because it zeroes the two 8-byte
  checksum fields; on clip packages it does the same, and `tools/tpac_fix_item_checksums.py`
  recomputes them (xxHash64 over the metadata, the #616 lesson) so every file matches Kit output.
  73 warg, 24 spider and 24 chariot clips ship with zeros and load, so this is hygiene, not a fix.

- **A package written outside the Kit does not exist to the client until the Kit has saved the module**
  and `RuntimeDataCache/<package GUID>.rdc` exists for it (the #616 lesson, same day). Every working clip
  package in the Armory has one (24 spider, 73 warg, 24 chariot, 6 ram, 33 elephant); the 52 troll
  masters imported today and the 52 generated clips had none. `python tools/check_rdc_entries.py --under
  creature/troll` is the gate; it ignores `_mtl` by default because 925 materials have no entry and render.

### What the Kit did with the FBX, measured over three imports (2026-09-17/18)

The first Kit import folded the troll at every joint. Reading the compiled master back with TpacTool
and comparing it bone by bone with the FBX and with the engine's own `human_skeleton` rest frames
(`tools/blender/human_skeleton_engine.json`, read out of `human.tpac`) gave the mechanism rather than a
guess, and each of the three imports removed exactly one identified error:

| Import | Rig / export | Children (27 bones) | Pelvis | Kit result |
|---|---|---|---|---|
| 1 | TpacTool `FixBoneForBlender` rig, node -180 Z | Kit = FBX local, but the rig's frames are 90 to 180 deg off the engine's | 18 deg from rest | folded troll |
| 2 | engine-frame rig, node identity | engine-exact (28 deg mean from rest, a hunched stance) | 176 deg from rest | folded troll |
| 3 | engine-frame rig, node +180 Z | engine-exact | 18 deg | right pose, facing away |
| 4 | engine-frame rig, node identity, pose turned 180 deg | engine-exact | 18 deg | right, facing forward |
| 5 | as 4, plus a REST frame 0 | engine-exact | 18 deg | pelvis height right: the Kit zeroes the root track at frame 0 |

Rules that fall out, now built into `retarget_mannequin_to_human.py --engine-skeleton`: the Kit stores
FBX bone locals verbatim (so author on the engine's frames), it stores the root as `RotZ(180) @ world`
while also applying the armature node's transform (so leave the node at identity and turn the pose), and
it stores the root POSITION track relative to frame 0, which vanilla masters keep as a rest frame (run:
0.6 deg from rest, root (0,0,0)); a clip opening on a posed frame therefore loses its pelvis offset, and
the hunched troll sat 9 cm too high with skating feet until frame 0 became rest (Artem, 2026-09-18).
Vanilla walk/run masters carry ONE root track (the pelvis bob, no forward travel) and no pelvis position
track; dropping the UE `root` travel and keeping the pelvis bob reproduces that layout.
Full write-up: [`bannerlord-skeleton-authoring.md`](bannerlord-skeleton-authoring.md) "Status as of
2026-09-18". The Kit also creates a Skeleton resource named after the FBX armature
(`human_skeleton_notused.001`, stored inside one master package); it is inert and can be deleted in the Kit.

### The bind stage: `as_cave_troll_warrior` (2026-09-18)

`tools/bind_troll_action_set.py` owns the body of `<action_set id="as_cave_troll_warrior">` in the LIVE
Armory `action_sets.xml` (dry run, `--apply`, byte-faithful, non-`.xml` backup, ElementTree parse before
write, idempotent). It reads the code list and the idle `alternative_group`s from Native's full
`as_human_warrior` (the Armory's own `as_human_warrior` at line 15 is only the rider partial) and binds 213
codes: forward walks (unarmed to `anim_troll_walk1`, armed to `combat_walk1`), forward runs, idles
(unarmed `idle1`; armed alternate `combat_idle1`/`2` by code number), the directional strikes to
`combat_hit_<dir>1`, and the fall deaths (back to `death1`, front/left to `death2`, front_heavy/right to
`death3`). Left alone on the human clips: the `_adder` additive overlays, turns, strafes, backward walks,
the knock-down-and-rise strikes, arrow/fire deaths, and every attack (melee is engine pose-blend).
`tools/audit_action_set_parity.py` passes afterwards and the repo snapshot
`docs/reference/lotrlome-armory-snapshot/action_sets.xml` is refreshed from the live file.

## Current state / open items

Cave troll (2026-09-18): 52 masters + 52 clips live in the Armory, playing correctly in the Kit; 213
`as_cave_troll_warrior` overrides bound; LOME meshes re-skinned and in the Armory sources. Open: Kit
reimport of `LOME_troll.fbx` + import of `LOME_troll_armor.fbx`, a Kit save so the 52 masters get their
RDC entries (`tools/check_rdc_entries.py --under creature/troll`), Custom Battle smoke, the junk
`human_skeleton_notused.00x` skeletons to delete, the Fab licence tier for the provenance row, the hill
troll decision (stay on `troll_skeleton` + bind the clips, or conform the mesh), and the Fab troll on its
own proportions as a separate job.

Rivendell modular kit + 204 materials + textures: done and imported-ready. Tents: meshes + 10 sets
done; Wide/On_Sticks textures pending user re-download. Open: foliage material shader flags (need
one hand-configured sample to clone), 16 manual translucent materials (`material_sheet.csv` notes
column), assembled-scene direction decision, deferred review items (stem-map sidecar, `sanitize()`
unification — see the RCA).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/troll-race-arp-retargeting-workflow.md](../ai-includes/troll-race-arp-retargeting-workflow.md)
- [docs/features/troll-race.md](../features/troll-race.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/kitbash/README.md](../kitbash/README.md)
- [docs/modding/items-mounts-and-harness.md](../modding/items-mounts-and-harness.md)
- [docs/modding/module-armory.md](../modding/module-armory.md)
- [docs/modding/recipe-new-mod-from-zero.md](../modding/recipe-new-mod-from-zero.md)
- [docs/reference/development-machines.md](./development-machines.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)

<!-- backlinks-end -->
