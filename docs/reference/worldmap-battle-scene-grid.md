# Worldmap Battle-Scene Grid (how field-battle terrain is chosen)

How Bannerlord decides **which battle-terrain scene loads when a field battle starts on the campaign
map**, what the `worldmap_battle_scene_grid` texture actually is, and how to **re-author it for TAOM's
Middle-earth map**. First written against 1.4.5; the chain was re-verified against the installed v1.5.3 DLLs and
the native decompile on 2026-10-03. Companion to [scene-reference-audit.md](scene-reference-audit.md) (which
validates the `sp_battle_scenes.xml` *data*); this doc explains the *texture* that drives it. To add a custom
scene, follow [recipe-add-a-field-battle-scene.md](../modding/recipe-add-a-field-battle-scene.md); which cell
belongs to which region is the [Region table](#region-table-2026-10-03-live) below.

> **TL;DR (CONFIRMED on 1.4.5, 2026-06-01):** the battle-scene grid is set by placing a **lossless**
> `worldmap_battle_scene_grid` texture at the **`Assets/world_map/`** resource path (R = scene index → matches
> `sp_battle_scenes.xml` `map_indices`; G = entry orientation; 1024×1024). The engine reads it as a **runtime
> resource by name** — **no `Main_map` re-bake is needed.** Verified: a lossless import to `Assets/world_map/`
> with `Main_map`'s `terrain.bin`/`scene.xscene` **unchanged** loads correctly.
>
> Two things must both be right or campaign-load crashes (native `AccessViolationException` in
> `get_battle_scene_index_map`): the **resource path** must be `world_map/worldmap_battle_scene_grid` (not e.g.
> `Battle Map/`), and the texture must be **lossless** — Texture Inspector → **Do Not Compress** + **Dont Degrade**
> (DXT/compression mangles the exact R-channel index bytes; the crashed import's `.rdc` was 699 KB *compressed*,
> the working one is 4.19 MB = 1024×1024×4 *uncompressed*).
>
> **History of this doc's wrong turns (kept as a caution):** earlier revisions said (1) "re-import the texture"
> alone changes the grid — wrong, *and* (2) "never import a loose texture; it must be **baked into `Main_map`**" —
> also wrong (over-inferred from a crash). The grid is **not** baked into `Main_map`/`terrain.bin`; it is a
> runtime `Assets/world_map/` texture. The authoritative source is
> [BannerlordModding.LT › Battle Scene Grid](https://docs.bannerlordmodding.lt/editor/battle_scene_grid/) (1.2.12,
> confirmed to still apply as of 1.4.5).

> ## ⚠️ A mis-imported grid CRASHES campaign load
>
> If a campaign crashes on load with a native `AccessViolationException` in `get_battle_scene_index_map` (boots to
> menu fine, dies loading a campaign), the `worldmap_battle_scene_grid` texture is mis-imported. **Both** of these
> must hold: (1) resource path = **`world_map/worldmap_battle_scene_grid`** (a wrong path like `Battle Map/` leaves
> a conflicting/orphaned resource); (2) **lossless** — Texture Inspector → **Do Not Compress** + **Dont Degrade**
> (DXT mangles the R-channel index bytes; a `~700 KB` compressed `.rdc` vs the correct `~4.19 MB` uncompressed one
> is the tell). Recovery during the 2026-06-01 incident was to **delete the bad import**; the definitive fix was
> re-importing lossless at `world_map/`. Patch0's AV retry guard does **not** rescue a deterministic mis-import.

## Data flow (re-verified against the installed v1.5.3 DLLs and native decompile, 2026-10-03)

```
field battle starts at map position (CampaignVec2)
   │
   ▼ SandBox.MapScene.GetMapPatchAtPosition(position)              [MapScene.cs:436]
   │    reads _battleTerrainIndexMap — a byte[], 2 bytes per texel:
   │      byte[idx*2]   = sceneIndex   (0–255)
   │      byte[idx*2+1] = packed nibble normalized sub-tile coords (low nibble→X, high→Y, /15f)
   │    → MapPatchData { int sceneIndex; Vec2 normalizedCoordinates }
   │
   ▼ DefaultSceneModel.GetBattleSceneForMapPatch(patch, isNaval)    [DefaultSceneModel.cs:26]
   │    PRIMARY:  every <Scene> whose map_indices="…" contains sceneIndex; one match is returned,
   │             several are picked with equal odds (GetRandomElement) after a
   │             Debug.FailedAssert("Multiple battle scenes ...") [:58]
   │    FALLBACK: no index match → scenes whose terrain= equals the navmesh TerrainType (no assert);
   │             then any non-naval scene, then any scene, each of those two after a FailedAssert
   │    The installed MBDebugManager.Assert is empty (MBDebugManager.cs:33-35); ButterLib, shipped
   │    in TAOM.Dependencies, wraps it to write one Debug-level line. No popup, no exception, and
   │    nothing in rgl_log.
   │
   ▼ returns SceneID (e.g. "battle_terrain_a")
   ▼ CampaignMission.OpenBattleMission(sceneID, …)
```

There are **two independent signals**, used in priority order:

1. **`sceneIndex` (primary):** comes from the **grid texture** (a runtime resource, see below). This is the
   `map_indices="…"` attribute in `sp_battle_scenes.xml`. Lookup: `floor(pos / terrainSize x 1024)` per axis,
   `terrainSize` = node count x node size from `Scene.GetTerrainData` (`MapScene.cs:240-242, 451-455`); TAOM's
   `Main_map` is 16 nodes of 100, so 1600 x 1600.
2. **navmesh `TerrainType` (fallback)** — `MapScene.GetFaceTerrainType` returns `(TerrainType)FaceGroupIndex`
   off the map's **navigation mesh**, a completely separate bake from the texture. Only consulted when no scene's
   `map_indices` contains the pixel's index.

## Where `_battleTerrainIndexMap` comes from

At map load, `SandBox.MapScene.Load()`:

- `GetMainMapModule()` returns the **last active module** that owns `SceneObj/Main_map/scene.xscene`
  (MapScene.cs:203–211 — "last active module wins"). Load order therefore decides whose map is used.
- `_scene.Read("Main_map", module.Id, …)` loads that module's baked map scene.
- `MBMapScene.GetBattleSceneIndexMap(_scene, ref _battleTerrainIndexMap, ref w, ref h)` (MapScene.cs:243) pulls
  the index map out of the **loaded scene** via the **native** `IMBMapScene.get_battle_scene_index_map`
  (`[EngineMethod]`, implemented in native C++). The buffer is `width * height * 2` bytes.

Because the read is native and the texture name lives in native scene data, the string `worldmap_battle_scene_grid`
**does not appear anywhere in managed code** — grepping the entire decompiled tree returns nothing. That's
expected, not a sign anything is broken.

### A global resource by hardcoded name — NOT baked into the scene (corrected 2026-06-01)

> ⚠️ An earlier version of this section claimed the grid is "baked into the `Main_map` scene data, not bound." The
> 2026-06-01 disk evidence **disproves** that: a lossless grid at `Assets/world_map/` loads with `Main_map`'s
> `terrain.bin`/`scene.xscene` **unchanged**. The grid is a **runtime texture resource**, not baked scene data.

The engine loads `world_map/worldmap_battle_scene_grid` as a **global resource by (hardcoded) name** when reading
the map, and samples it for the index map — `MBMapScene.GetBattleSceneIndexMap` takes **no** texture-name argument
because the name is fixed in native code. Evidence:

- The grid name appears in **no** scene file (`scene.xscene`/`terrain.bin`/`terrain_ed.bin`) — it lives in the
  module's `Assets/world_map/` (compiled `…_tex.tpac` + a `RuntimeDataCache/<guid>.rdc`) + the source `.zip`. It is
  absent from `references.txt` because that lists scene-local entities, **not** global resources like this one.
- **Confirmed:** importing a lossless grid to `Assets/world_map/` **without** re-baking `Main_map` (timestamps
  unchanged at 2026-05-28) makes the campaign load with that grid. No bake step exists/needs to run.

**Consequence:** to change the grid, replace the `Assets/world_map/worldmap_battle_scene_grid` texture (lossless) —
that's it, no `Main_map` re-bake. (The earlier "must re-bake `Main_map`" guidance below in older revisions was
wrong; this section supersedes it.)

### Two grids, do not confuse them

| Texture | Drives | Native read |
|---|---|---|
| `worldmap_battle_scene_grid` | **battle-terrain scene selection** (the `sceneIndex`) | `get_battle_scene_index_map` |
| `worldmap_colorgrade_grid` | **campaign-map atmosphere colour-grading** (map tint per region) | `get_color_grade_grid_data` |

TAOM_Map's `SceneObj/Main_map/references.txt` references `worldmap_colorgrade_*` textures — those are the
*colorgrade* grid, **not** the battle-scene grid. When inspecting the map in the editor, a green/orange
false-coloured preview is usually the colorgrade grid or a height-shaded view; the raw battle-scene grid data is
near-monochrome **red** (the scene index lives in the red channel — low indices → near-black-red).

## Mechanism vs 1.2.x

Structurally unchanged: 2-byte index map, native read, `map_indices` matching, navmesh-`TerrainType` fallback.
Confirmed 1.4.x-era additions: `is_naval="true"` scenes + an `isNavalEncounter` branch through the whole
selection chain, and a separate `NavalDLC/ModuleData/sp_battle_scenes.xml` (TAOM declares NavalDLC
incompatible, so the naval branch never runs). **Channel encoding, decompiled 2026-10-03**
(`python tools/native_decompile.py --engine-method IMBMapScene.GetBattleSceneIndexMap`): the native function loads
the texture by the hardcoded name `worldmap_battle_scene_grid`, copies bytes 0 and 1 (R and G) of each 4-byte
texel, and starts at the **last** row, so output row r is texture row 1023 - r. The PNG's top row is the map's
north edge, which two logged battles near Erebor confirm (`mapIndex=46`, `taom_debug_2026-09-29_08-29-26.log`).

## Current TAOM state (verified 2026-10-03)

| Fact | Evidence |
|---|---|
| TAOM_Map ships a full custom `Main_map` (16 x 16 terrain nodes of 100, so 1600 x 1600 units); it wins only when TAOM_Map loads after SandBox | `Modules/TAOM_Map/SceneObj/Main_map/scene.xscene`, "last active module wins" (MapScene.cs:203-211) |
| Grid source: `TAOM_Map/AssetSources/world_map/worldmap_battle_scene_grid.png` (2026-05-31). Runtime copy: `Assets/world_map/worldmap_battle_scene_grid_tex.tpac` + `RuntimeDataCache/3CD25D70-DAD2-4C09-8FF0-D114C7810DF6.rdc` (2026-06-01), pixel-identical to the PNG; the same source hash ships in `pack4.tpac` of the public, patreon and testing channels | review of 2026-10-03 (RDC pixel block compared to the PNG) |
| `Patch0_BattleScenes` is enabled (`Main/SubModule.cs:520`) | grep |
| Vanilla's `Campaign.InitializeScenes` appends every active module's `sp_battle_scenes.xml` (`Campaign.cs:1347-1368`). TAOM's prefix (`Campaign_InitializeScenes_Patch`) loads only TAOM's file and skips the original, for new and loaded campaigns alike (the manager is rebuilt each load, `Campaign.cs:1405-1408`). If the patch ever fails to apply, vanilla's append comes back and SandBox's indices 1-157 roll Calradian scenes on TAOM cells | decompile + `Campaign_InitializeScenes_Patch.cs:14-37` |
| `sp_battle_scenes.xml` is generated by `tools/build_battle_scenes.py`, is not an XmlNode in `Main/_Module/SubModule.xml`, and reaches the game through the build's `_Module` copy | `TAOM.csproj` |

### The coupling that matters

Every index the grid paints must be listed by some scene in the active file; an unlisted index drops to the
terrain-type fallback. The grid paints 1-180 and 255, plus a 2-texel speck of 0 at PNG (504, 847-848) on the Harad
edge; 181-254 never occur. The generator gives every cell no region claims (0 and 181-254) to the catch-all
`battle_terrain_r`, so coverage is 0-255 by construction.

> Verify after any grid or table change: `python tools/build_battle_scenes.py --check` (exit 1 when the file is
> stale or a scene has no `SceneObj` folder). CI runs the generator's tests, which also check the committed file
> against `REGIONS`.

## Re-authoring the grid for the Middle-earth map

Two coupled halves. **Asset half = Bannerlord editor (your domain, external tool).** **Data/code half = repo.**

**History.** The 2026-05/06 plan weighed reusing vanilla's indices (Approach A, no code) against custom indices
(Approach B, re-enable `Patch0_BattleScenes`). B won: the grid was repainted with Middle-earth indices 1-180 and
Patch0 was re-enabled on 2026-06-01. Its first-draft region to terrain table was superseded on 2026-10-03 by the
measured table below and the generator.

### Region table (2026-10-03, live)

`Main/_Module/ModuleData/sp_battle_scenes.xml` is **generated** by `python tools/build_battle_scenes.py --apply`
from its `REGIONS` table; refine a region by editing that table and re-running, never the XML. The table below is
the cell to region half; the scene lists per region live in the tool. A cell in two regions gets the union of
their scenes.

**How the cells were attributed.** The grid PNG's red value at every settlement position in the live
`TAOM_Map/ModuleData/settlements.xml` (pixel = `floor(pos / 1600 x 1024)`, PNG row = 1023 minus that for y, as
the native copy reads the texture bottom row first), grouped by the settlement's culture. Cells holding no
settlement were read off the numbered grid picture and are marked *(visual)*. Shared cells are named.

| Region | Cells |
|---|---|
| Gondor | 85 (Minas Tirith, Pelennor, Osgiliath), 86, 90, 91, 92, 93, 97 (Pelargir), 98, 99, 100, 104, 105 (Dol Amroth), 106, 107, 108; 101, 102, 103 *(visual)* |
| Ithilien | 95, 96; 94 *(visual)* |
| Rohan | 81, 87 (Edoras), 88 (Helm's Deep), 89; 80 (shared with Lórien) |
| Dunland and Enedwaith | 52, 59, 61, 62 (Fords of Isen, shared with Isengard); 57, 58, 60 *(visual)* |
| Isengard | 63 (Orthanc), 62 (13 Isengard settlements against 5 Dunland ones) |
| Lothlórien | 65, 80 |
| Rivendell | 42 (shared with the Misty Mountains) |
| Moria, Misty Mountains, Gundabad, Ettenmoors | 22, 42, 43, 44, 53, 64; 23, 24 *(visual)* |
| Mirkwood and the Woodland Realm | 25, 45, 46 (Felegoth and Caras Laerolin), 54, 55 |
| Dol Guldur | 66 |
| Dale | 46 (Dale, Erebor and two Mirkwood towns: shared by all three), 47, 56 |
| Erebor and the Iron Hills | 26, 27, 28, 46, 48 |
| Rhûn | 20, 29, 30, 49, 50 (the sand patch), 51, 69, 70, 71, 74, 75, 76, 77 (Barad-dûr's cell, shared with Mordor), 78, 160 |
| Khand | 72, 73, 148, 149, 151, 152, 153, 154, 155, 156, 178 (shared with Near Harad) |
| Near Harad | 109, 111, 112, 113, 114 (shared with Harad), 115, 116, 127, 131, 172 (shared with Mordor), 176, 177 (Chelkarâ), 178 (Dûn Shatagûn); 110 *(visual: the Harondor coast, nearest Kes Marzûk)* |
| Harad | 114, 117-122, 124-126, 128, 129, 132, 135, 144, 145; 123, 130, 133, 134 *(visual)* |
| Far Harad | 136-143, 146, 147, 150, 157, 158 |
| Mordor | 84 (Morannon), 161-172 (172 holds three Mordor and three Near Harad settlements), 77 (Barad-dûr); 173-175, 179 *(visual)* |
| Dead Marshes; Emyn Muil; Brown Lands | 82, 83 *(visual)*; 79; 67, 68 *(visual)* |
| Lindon; Ered Luin | 36, 38, 39; 1, 37 *(visual)*; 3, 4, 5, 33 |
| Eriador and Arnor | 2, 6, 7, 12, 31, 32, 34, 35, 40, 41, 180 *(visual)* |
| Grey Mountains; Forodwaith; far east | 13-17; 8-11; 18, 19, 21, 159 *(visual)* |
| **255 (unpainted)** | 25.8% of the grid: the sea, the parchment edge and 36 settlements (33 of Umbar's, one Harad village, and Gorgrim and Mokra of Ered Luin). It gets Umbar's mix of two desert and two coastal scenes, so the two Ered Luin settlements do too until Umbar is painted its own index. |

**Open questions, measured but not settled.** 179 has no settlement and its nearest fortification is Khand's; it
stays Mordor on the picture's evidence. 82 is nearest a Rohan fortification and may be the Wetwang rather than the
Dead Marshes. Each is a one-line move in `REGIONS` plus its row in this table.

**Pitfalls the earlier lists had.** The pre-2026-10-03 Rohan scene listed 85, 86, 90, 91 and 92 (Minas Tirith and
Anórien); the Mordor scenes listed 85, 94, 109, 127, 128, 131, 152 and 153 (Gondor, Ithilien, Near Harad, Harad,
Khand). The first draft of this table put Mordor on 176-178, which hold Harad and Khand castles; a test in
`tools/tests/test_build_battle_scenes.py` now pins that. Check a cell against this table, and the dry run's
"shared cell" lines, before giving it a custom scene.

**Scene pool.** Native scenes are grouped by type. Plain goes to the green kingdoms: River and Water tagged ones on
the Anduin and the coasts, High forest ones to Ithilien, Lórien, Rivendell and Mirkwood, Mountain and Canyon ones to
the ranges, and `biome_144` and `biome_149` to Far Harad as well as Eriador. Desert goes to Harad, Near Harad,
Umbar, the Rhûn sand patch, the Brown Lands and one Khand scene. Steppe goes to Rhûn, Khand, Far Harad, Near Harad's
edges, the Erebor and Dale plains, the Grey Mountains and Forodwaith. Swamp goes to the Dead Marshes. Vanilla's own
XML defines `battle_terrain_002`, `006`, `016`, `c`, `e`, `i`, `j` and `r` only inside comments: the first seven
stay out (`002` has no scene on disk), and `r` is the catch-all, since it ships in SandBoxCore and vanilla's Custom
Battle still lists it. The `terrain`, `forest_density` and `TerrainType` values do not steer anything at runtime:
`terrain` is read only when no scene lists a cell, which full coverage prevents, and the other two are read by
nothing. `battle_terrain_biome_094` had two unexplained load crashes on 2026-09-06 (UNVERIFIED cause); it is now 1
of 8 Khand scenes.

### Authoring and importing the grid (Bannerlord editor, your domain)

Per the authoritative [BannerlordModding.LT › Battle Scene Grid](https://docs.bannerlordmodding.lt/editor/battle_scene_grid/)
(a **1.2.12-era** source), corroborated against the installed engine:

1. Author the grid texture **externally** at **1024×1024** ("native's size; not sure if other sizes work").
   **R channel = scene index** 0–255 — the value that must appear in a `<Scene map_indices="…">` of
   `sp_battle_scenes.xml`. **G channel = party entry orientation** (which side parties enter from). North is the
   PNG's top row.
2. **Import it at the `Assets/world_map/` resource path**, **LOSSLESS**: Texture Inspector → check **Do Not
   Compress** + **Dont Degrade**. **Both** matter (CONFIRMED 2026-06-01): the resource name must be
   `world_map/worldmap_battle_scene_grid` (a wrong path like `Battle Map/` orphans/conflicts the resource → crash),
   and compression mangles the R-channel index bytes (the correct import is ~4.19 MB = 1024×1024×4 uncompressed;
   a ~700 KB compressed `.rdc` is the bad-import tell).
3. **That's it — no `Main_map` re-bake.** CONFIRMED on 1.4.5 (2026-06-01): a lossless import to `Assets/world_map/`
   with `SceneObj/Main_map` **unchanged** loads correctly.
4. A repaint that adds an index or moves a border: update `REGIONS` (re-measure with the settlement method above),
   run the generator, and confirm `TAOM_Map` still loads after SandBox so its `Main_map` wins.

**Source archive:** keep `AssetSources/world_map/worldmap_battle_scene_grid.png` (and/or the `.zip`) as your
editable grid **source** — `AssetSources/` is editor-only, never loaded at runtime.

## Verification

- **Data:** `python tools/build_battle_scenes.py --check` and `python -m unittest tools.tests.test_build_battle_scenes`.
- **In game:** stand on a cell and run `taom.print_battle_scene` (prints the cell's index and every candidate scene);
  fight there and read the `[BattleLoad] ... mapIndex=<n> sceneId='<id>'` line in
  `bin/Win64_Shipping_Client/Logs/taom_debug_*.log`. A failed assert never reaches rgl_log, so it is no signal.

## Reference files

Engine authority is the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>` (v1.5.3 cache in `~/.taom-src/v1.5.3/`).

- `SandBox.MapScene` (terrain size 240-242, `GetMapPatchAtPosition` 436-467)
- `TaleWorlds.CampaignSystem.GameComponents.DefaultSceneModel` (`GetBattleSceneForMapPatch` 26-62)
- `TaleWorlds.CampaignSystem.GameSceneDataManager` (`LoadSPBattleScenes` 76-155: `int.Parse` on every `map_indices` token)
- `TaleWorlds.MountAndBlade.MBDebugManager` (`Assert` 33-35, empty)
- native `IMBMapScene.GetBattleSceneIndexMap`: `python tools/native_decompile.py --engine-method IMBMapScene.GetBattleSceneIndexMap`

## Related

- [features/battle-scenes.md](../features/battle-scenes.md): the `Patch0_BattleScenes` feature and its history
- [modding/recipe-add-a-field-battle-scene.md](../modding/recipe-add-a-field-battle-scene.md): adding a custom field-battle scene
- [scene-reference-audit.md](scene-reference-audit.md) — validates `sp_battle_scenes.xml` Scene ids vs on-disk SceneObj
- [taom-map-settlement-naming.md](taom-map-settlement-naming.md) — TAOM_Map is a live external module, not a repo shadow
- [.claude/rules/vanilla-data-comparison.md](../../.claude/rules/vanilla-data-comparison.md) — diff vs installed vanilla before editing mirrored data

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-scenes.md](../features/battle-scenes.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/module-map.md](../modding/module-map.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/modding/recipe-new-mod-from-zero.md](../modding/recipe-new-mod-from-zero.md)
- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/reference/scene-reference-audit.md](./scene-reference-audit.md)
- [docs/reviews/rca-worldmap-grid-loose-import-crash-2026-06-01.md](../reviews/rca-worldmap-grid-loose-import-crash-2026-06-01.md)

<!-- backlinks-end -->
