# Gondor kitbash: understand it, then build with it in Blender

Paste everything below the line into a fresh Claude Code session in `E:\repos\TAOM`.

**Status (2026-11-03):** Phases 1 to 6 have run once. The catalog is in [README.md](README.md),
[meshes.md](meshes.md) and [design-patterns.md](design-patterns.md), and six buildings are exported
in one FBX ([gondor-buildings.md](../gondor-buildings.md)). The tools take `--kit erebor|gondor`
(`tools/oneoff/kitbash_kits.py`). The counts below are the first session's measurements; the
catalog holds the later ones.

---

You will learn the Gondor kitbash piece by piece, then assemble buildings from it in headless
Blender and export each as one FBX. This repeats the Erebor work of 2026-10-15 (issue #797) on a
much larger kit. Work in phases. Stop at every GATE and wait for Mike's word.

## Read first (the Erebor run is your template)

- [docs/kitbash/erebor/blender-build-prompt.md](../erebor/blender-build-prompt.md): the phases
  and gates this prompt repeats.
- [docs/kitbash/erebor/design-patterns.md](../erebor/design-patterns.md) "The frame rule" and the
  join matrix; [docs/kitbash/erebor-buildings.md](../erebor-buildings.md): the export, the probe,
  the review history and what Mike changed on review.
- [docs/kitbash/lond-cirion-buildings.md](../lond-cirion-buildings.md) and
  [lond-cirion-walls.md](../lond-cirion-walls.md): earlier Gondor work. Its seven placement rules
  were written for exactly these part families; they apply in full here.
- `docs/reviews/lessons/build-tooling-workflow.md`: the two #797 lessons (measure a catalog;
  prove the frame).

## The kit (measured 2026-10-15)

`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\AssetSources\Scenes\Gondor\`,
110 FBX files. Read-only: never modify or re-save a source FBX.

| Folder | FBX | Part prefixes | Base sub-objects | LOD objects | `bo_` | Role |
|---|---|---|---|---|---|---|
| `meshes` | 25 | 266 | 626 | 1,529 | 261 | **The kit**: wall panels, trims, roofs, pillars, stairs, ground, railing, buttress, bridge, rubble |
| `walls` | 10 | 67 | 73 | 144 | 67 | **The kit**: castle walls and towers (used by the Lond Cirion walls) |
| `buildings` | 31 | 88 | 167 | 233 | 63 | Shipped buildings, already merged: reference and whole props, not parts |
| `statues` | 13 | 14 | 14 | 128 | 24 | Props |
| `Comprehend` | 17 | 49 | 49 | 0 | 0 | Props from another pack; no LODs, no bodies |
| `ships` | 3 | 3 | 3 | 0 | 3 | Harbour props (Tripo) |
| `blockout` | 11 | 66 | 67 | 92 | 48 | **Ours**: the Lond Cirion exports. Exclude from the catalog |

Counts come from `tools/audit_fbx_lods.read_fbx_meshes` with a prefix taken as the name before the
first dot; Phase 1 replaces them with real groupings. Two files in `meshes/` are not kit families:
`gate bak.fbx` (a backup) and `VEG_Grass_Tussock_A.fbx` (vegetation). The 2026-07 Lond Cirion parts
catalog (`E:\LOTRAOMAssets\_export\lond_cirion\parts_catalog\`) covers 15 of the families: a start,
not a catalog.

**Authored evidence:** 51,358 Gondor placements of 342 distinct pieces in the TAOM_Map prefabs.
Largest: `X_Lossarnach.xml` 14,056, `dwarf_armory.xml` 5,931, `lotraom_kysaro_prefabs.xml` 5,453,
`Soisson_Prefabs_2.xml` 5,356, `X_Dol_Amroth.xml` 3,983, `taom_gondor_buildings.xml` 862.

## How Gondor differs from Erebor (plan for these)

- **A piece is a part family, not one object.** `<prefix>` plus sub-objects (`<p>.wall`,
  `<p>.stonebrick`, `<p>.door`, `<p>.decalleak`, ...), LODs **per sub-object** (`.lod2/.lod4/.lod5` on
  walls and roofs, `.lod3/.lod6` on trims), one `bo_<p>` per prefix. The Erebor tools treat each
  object as a piece: group by prefix before measuring, placing, tiering or probing.
- **The traps of lond-cirion-buildings.md apply:** decal sub-objects hang far below the part and
  must be excluded; a bare trailing `.lod` can be the only copy of a sub-object; some parts are
  anchored by origin, not bounds; gables seat on their infill plate; trims straddle the wall line;
  grip a wall panel by its structural `.wall` body. `gondor_roof_a_30_*` is 26.57 degrees, not 30.
  No Gondor family has a door leaf or window glazing.
- **The frame rule must be proved again**, the same two ways as for Erebor (prefab rebuild under
  each reading, and `fbx_frame_compare.py` of an untouched exported part against its source).
  Measured 2026-10-15 with `fbx_frame_compare.py`:
  - **Kit parts are stored like Erebor.** `meshes/gondor_wall_3m_straight_a.fbx`: the same axis
    header (Y up, front Z, coord X, 1 unit = 1 cm), centimetre data under a node rotation X -90 and
    scale 100. `walls/gd_ramp_large_a1.fbx` the same, with an extra node rotation Y -90 on some
    objects. Erebor's rule (the engine sees each file turned 180 degrees about Z) is the likely
    answer; prove it before relying on it.
  - **Shipped buildings are stored differently.** `buildings/BuildingC.fbx`: `UnitScaleFactor` 100,
    identity nodes, raw values up to about 1,200 units. The "identity/metres" note in
    lond-cirion-buildings.md describes these files, not the kit parts. Check their scale and frame
    separately before using one as a reference or a prop.
- **Scale of the work.** About 450 kit pieces and twice the prefab evidence. Run the catalog per
  file or per folder, keep every run's DONE/log protocol, and summarise in tables before reading
  images.

## Mike's standing decisions (from the Erebor review)

- **Scene dressing, exterior shells.** No interiors; open doorways are fine.
- **No below-grade skirt.** A building's floor slab sits on the terrain at z 0.
- **A floor must read as a floor in game.** The Erebor `_C1` paving looked like roof tiles; check a
  floor texture against the roof textures before choosing it.
- **Columns get a base as well as a cap** where the kit has a capital or base piece.
- **No statues or props on a building unless the brief asks.**
- **Apply your own recommendation.** When your analysis ends in a recommendation, carry it out and
  report it with how to undo it. Gates, commits, pushes, GitHub issues and paid AI calls still need
  his word.
- **Budget cuts are per placement** (`base_lod` in the assembler): an interior floor seen only
  through doors can use its flat LOD.

## Ground rules

- **Reuse the Erebor tools; generalise, do not fork.** The kit table (`--kit erebor|gondor`:
  source folders, family rules, output folder) is in `tools/oneoff/kitbash_kits.py` and used by
  `tools/oneoff/blender_catalog_erebor.py`, `erebor_catalog_to_md.py`,
  `mine_erebor_prefab_joins.py`, `blender_render_erebor_prefab.py` and
  `blender_test_erebor_joins.py`. The assembler for Gondor is the separate
  `blender_assemble_gondor_buildings.py`, which imports the Lond Cirion helpers;
  `fbx_frame_compare.py` already takes any file. Prove parity:
  re-run the Erebor catalog with `--kit erebor` and diff its JSON against
  `E:\LOTRAOMAssets\_export\erebor\catalog\erebor_catalog.json` (identical), and keep
  `python -m pytest tools/tests/test_erebor_kit_tools.py` green. Renaming the tools to kit-neutral
  names is fine if every doc link and test import follows.
- **Blender** runs headless through the MS-Store launcher, which detaches:
  `"%LOCALAPPDATA%\Microsoft\WindowsApps\blender-launcher.exe" -b -P <script> -- <args>`. Each
  script writes `DONE.txt` (or `<x>_DONE.txt`) and a log; poll for it, then read the log.
- **Scripts** go in `tools/oneoff/`, written with the Write tool, never a Bash heredoc (it mangles
  backslashes and quotes). Use `python`, never `python3`. Pure-Python logic gets tests in
  `tools/tests/`.
- **Scratch, renders and JSON** go under `E:\LOTRAOMAssets\_export\gondor\`. Exports go to
  `TAOM_Map\AssetSources\Scenes\Gondor\buildings_taom\<name>.fbx` (a new folder: `buildings\` holds
  the shipped buildings) with `<name>_import.md` beside them.
- **Evidence:** every number you write comes from output you read this session; mark catalog
  claims `measured` or `inferred`. Textures do not resolve in Blender: judge geometry by numbers and
  rays, not by preview colour.
- **Docs:** the catalog goes to `docs/kitbash/gondor/` (`README.md`, `meshes.md`,
  `design-patterns.md`), the buildings to `docs/kitbash/gondor-buildings.md`, the kit row of
  `docs/kitbash/README.md` is updated, lessons go to `docs/reviews/lessons/build-tooling-workflow.md`.
  No em or en dashes in prose; CRLF files stay CRLF. Run `python tools/lint_docs.py`.
- **Git:** no commit, push or issue without Mike's word. Before a commit run `/verify`, stage only
  your own paths (other sessions work in this tree), and check `git diff --staged --stat`.
- **Before Phase 3**, ask Mike whether to file a tracking issue (as #797 was for Erebor).

## Phase 1: measure every piece

Group objects into parts by prefix, with each part's sub-objects, per-sub-object LOD ladder,
`bo_` twin and any decal or bare-`.lod` sub-object listed. Per part record what the Erebor catalog
records (bounds, pivot class, normal-area buckets, slopes, see-through openings, walkable top,
climb profile, grid fit, node transform, materials) plus the bounds of its structural sub-object.
Render a four-view sheet per part and numbered contact sheets per family. Catalog `meshes/` and
`walls/` fully; `statues/`, `Comprehend/`, `ships/` and `buildings/` as props and references (size,
pivot, LODs, body, materials).

## Phase 2: understand how the pieces join

1. Mine the 51,358 prefab placements: per-part usage, rotations, scales, material overrides, and
   the most common relative offsets. Expect heavy reuse of pieces as greebles and props; the
   building prefabs (`taom_gondor_buildings.xml`, the Lossarnach and Dol Amroth sets) carry the
   joins.
2. **Prove the frame rule** before using any offset (see above).
3. Rebuild two or three authored buildings in Blender and check they close.
4. Write the join matrix and test each join in Blender (same-facing shared faces, back-to-back
   faces, intersections), and check every Lond Cirion rule against what you measured.
5. Give every part a role; note which floor textures read as floors.
6. Write `docs/kitbash/gondor/` and propose a list of Gondor buildings to build (for example a
   town house, a larger townhouse, a garrison or barracks, a tower or keep, a hall or temple, a
   market or forge), with a recommended order.

**GATE 1.** Show Mike: the catalog summary, the frame-rule proof, the join matrix, the rebuilt
authored buildings, every place an older doc or the Lond Cirion catalog was wrong, the parts you
could not classify, and the building list. Stop.

## Phase 3: design brief per building

One building at a time, in the agreed order: purpose, footprint on the kit's grid, storeys, roof
form, piece list with counts, materials, blockout renders (`--preview`), the triangle budget per
tier and the risky joins. Apply your recommended budget cuts and say what they save.

**GATE 2 (per building).** Mike approves or changes the brief. Stop.

## Phase 4: assemble and export

The assembler merges each LOD tier per sub-object (Lond Cirion ladder), joins the parts' bodies
into one `bo_` (a same-shape twin's body where a part has none; none for thin trims by design),
folds Blender's `.NNN` material duplicates back to the exact names, asserts each tier's triangle
sum, writes the import note (it flags any material with no `_mtl.tpac` under `TAOM_Map/Assets`) and
the probe layout (openings, wall spans per storey, chamfer walls, eave, roof type, footprint).

## Phase 5: verify the exported FBX

Run the probe on the exported file: tiers equal the brief, grounding, footprint, every wall line
over its full height (only planned openings pass), chamfer walls, gables or roof, floor,
collision coverage, shared faces grouped by plane (each group judged hidden or visible). Build an
inspection `.blend` from the exported FBX (one collection per tier, flat material colours) for
Mike. Run `fbx_frame_compare.py` on the first export against a source part.

**GATE 3 (per building).** Show Mike the renders, the probe table and the import note. Mike
imports in the Modding Kit. Record his review in `docs/kitbash/gondor-buildings.md`, fix what he
asks for, re-export and re-probe. Do not start the next building until he approves this one.

## Phase 6: record what you learned

After each building, update `docs/kitbash/gondor-buildings.md` (buildings table, recipe, probe
results, review history, deliberate choices, known open) and `design-patterns.md`, append lessons,
and run `python tools/lint_docs.py`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/kitbash/gondor/README.md](./README.md)
- [docs/kitbash/README.md](../README.md)

<!-- backlinks-end -->
