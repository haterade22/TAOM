# Battle Scenes

> **Status: ENABLED.** `Patch0_BattleScenes` is applied at module load (`TryPatchCategory("Patch0_BattleScenes")`,
> [Main/SubModule.cs:495](../../Main/SubModule.cs)), re-enabled 2026-06-01. Since 2026-10-03 the field-battle
> scene list is generated per Middle-earth region by `tools/build_battle_scenes.py`, and TAOM's own Mordor and Rohan
> field scenes are back in rotation, with the June `pbr_terrain` GPU crash risk accepted (#287). Two Gondor field
> scenes joined on 2026-10-17 before any in-game load. In-game checks per region are owed (see "Verifying" below).

## Overview

When two parties fight on the campaign map, the cell under the main party decides which battle scene loads. This
feature makes that decision TAOM's: it replaces vanilla's scene loading so only TAOM's `sp_battle_scenes.xml` is
read, maps every grid cell to scenes that fit its Middle-earth region, and guards the native index-map read
against a cold-cache `AccessViolationException`.

- How the grid texture and the selection chain work, and the cell to region table:
  [reference/worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md).
- How to add or move a custom scene:
  [modding/recipe-add-a-field-battle-scene.md](../modding/recipe-add-a-field-battle-scene.md).

## Why This Exists

- **Vanilla behaviour:** `Campaign.InitializeScenes` loads `sp_battle_scenes.xml`, `conversation_scenes.xml` and
  `meeting_scenes.xml` from **every** active module and appends them (`Campaign.cs:1347-1368`). A child mod can add
  scenes but cannot take vanilla's away, so SandBox's Calradian indices 1-157 would keep rolling Calradian scenes
  on TAOM's map, and indices above 157 would have no scene at all.
- **TAOM requirement:** `TAOM_Map` ships its own `Main_map` and its own grid, painted with Middle-earth indices
  1-180 (plus 255 for the unpainted area). Those indices need a scene list built for them, and nothing else.
- `MBMapScene.GetBattleSceneIndexMap` reads the index map natively and has produced `AccessViolationException` on
  cold-cache loads ("stuck on loading screen / crash on first encounter").

## Architecture

All three patches share `[HarmonyPatchCategory("Patch0_BattleScenes")]`, so the feature is gated on one line.

| Patch | Target | Type | Behavior |
|---|---|---|---|
| `Campaign_InitializeScenes_Patch` | `Campaign.InitializeScenes` | Prefix, returns false | Loads TAOM's `sp_battle_scenes.xml` and SandBox's `conversation_scenes.xml` / `meeting_scenes.xml` through `GameSceneDataManager.Instance.Load*Scenes`, then skips vanilla's per-module loop. Runs for new and loaded campaigns (the manager is rebuilt each load, `Campaign.cs:1405-1408`), so a changed list reaches existing saves. Each load is gated on `File.Exists`: a missing TAOM file loads **no** battle scenes at all. Nothing catches a throw from the loader, so a malformed file stops campaigns starting; the generator refuses to write one. |
| `MapScene_Load_DiagnosticPatch` | `SandBox.MapScene.Load` | Prefix, void | Diagnostic only: prints which active module's `SceneObj/Main_map/scene.xscene` wins ("last wins"). |
| `MBMapScene_GetBattleSceneIndexMap_Patch` | `MBMapScene.GetBattleSceneIndexMap` | Prefix, returns false | A 3x retry loop with `Thread.Sleep(250)` around the original call (`[HandleProcessCorruptedStateExceptions]` so the catch sees an AV on .NET Framework 4.7.2); the last attempt runs unguarded. It cannot rescue a mis-imported grid, which fails every time. |

**The data.** `Main/_Module/ModuleData/sp_battle_scenes.xml` is generated; its header says so. The source of truth
is the `REGIONS` table in `tools/build_battle_scenes.py`: (region, grid cells, scene ids). A cell in several
regions gets the union of their scenes; the engine picks among a cell's scenes with equal odds. Cells no region
claims (0 and 181-254, which the grid never or almost never paints) fall to `battle_terrain_r`, so 0-255 is always
covered. The pool is SandBoxCore's native scenes plus TAOM's own `taom_*` scenes from `TAOM_Map/SceneObj`; the
dry run prints the count.

**Related systems that read the same scenes.** Custom Battle's picker (`custom_battle_scenes.xml`) lists TAOM's
scenes separately. Scene ids containing `forceatmo` get the forced atmosphere
([atmosphere-persistence.md](atmosphere-persistence.md)). The parked shader-precompile walk keeps its own scene
list ([shader-precompilation.md](shader-precompilation.md)). `BattleLoadDiagnostics` logs every pick, and the dev
console's `taom.print_battle_scene` lists a cell's candidates.

## Key Files

| File | Purpose |
|---|---|
| [tools/build_battle_scenes.py](../../tools/build_battle_scenes.py) | The region table and the generator (`--apply`, `--check`) |
| [tools/tests/test_build_battle_scenes.py](../../tools/tests/test_build_battle_scenes.py) | Guards, coverage, byte shape, modes, region regressions (runs in CI, no game needed) |
| `Main/_Module/ModuleData/sp_battle_scenes.xml` | The generated scene list |
| [Main/Features/BattleScenes/Hooks/Campaign_InitializeScenes_Patch.cs](../../Main/Features/BattleScenes/Hooks/Campaign_InitializeScenes_Patch.cs) | Replaces vanilla scene loading |
| [Main/Features/BattleScenes/Hooks/MapScene_Load_DiagnosticPatch.cs](../../Main/Features/BattleScenes/Hooks/MapScene_Load_DiagnosticPatch.cs) | Logs which map module wins |
| [Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs](../../Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs) | AccessViolationException retry guard |
| `TAOM_Map/AssetSources/world_map/worldmap_battle_scene_grid.png` | The grid source (live, unversioned module) |

No service, no IoC registration, no adapters: the patches and the data are the whole feature.

## Tests

`tools/tests/test_build_battle_scenes.py` covers the generator: no empty or out-of-range cell, no scene written
without cells, no `--` in a comment, unknown ids refused, 0-255 coverage, CRLF with no BOM and the root as the first
element, `--check` / `--dry-run` / `--apply`, and the region regressions that keep Mordor scenes off Harad and Khand
cells and Pelennor Fields on cell 85, plus a CI check that the committed XML matches `REGIONS` and parses the way the engine parses it. The C#
patches have no tests: they are static, IO-bound and need a live game.

`SettlementSceneLiveDataTests` (`LiveInstall`, inconclusive without `TAOM_Map`) pins the 11 hand-wired `Location`
rows in the live `TAOM_Map` `settlements.xml` and checks that every `taom_*` scene that file names has its
`scene.xscene`.

## Verifying

1. `python tools/build_battle_scenes.py --check` exits 0 (the file is current and every scene has a `SceneObj`).
2. Build (`./build.ps1`).
3. In game, on a cell of each region you changed: `taom.print_battle_scene` lists the expected candidates; a field
   battle there logs `[BattleLoad] ... mapIndex=<n> sceneId='<id>'` in `taom_debug_*.log`.
4. The rgl_log diagnostic line `TAOM: >>> Selected map module: 'TAOM_Map'` confirms the right `Main_map` won.

## How to Diagnose "wrong map module wins"

The `MapScene_Load_DiagnosticPatch` prints to the engine log every time `MapScene.Load` runs. Bannerlord's "last active module with a `Main_map` scene wins" rule means load order matters. If the diagnostic shows `SandBox` as the selected module instead of `TAOM_Map`, the load-order setting (`Modules/Native/SubModule.xml`-style ordering, or the Launcher) is putting TAOM_Map ahead of SandBox; reverse it.

## Changelog

- 2026-10-17 (later): Kysaro's `taom_rohan_battle_002_forceatmo` ("Rohan Plains II", lotraom-assets `db387f23f`) joins the Rohan region (cells 80, 81, 87, 88, 89) and the Custom Battle picker. Rohan Plains and Rohan Plains II each come up in 1 battle of 9 on cells 81 and 87, 1 of 10 on cells 88 and 89 (which also draw Fords of Isen) and 1 of 12 on cell 80 (shared with the three Lothlorien scenes). The same drop's town, arena and village scenes are wired to Pelargir, Serelond (town and arena), Amon Gelin, Lothgobel, Eastern Moria and five Mordor towns in the live `TAOM_Map/ModuleData/settlements.xml` (the repo's copy of that file is a stale shadow that never loads). Minas Morgul keeps its own scene, and Calembel waits until Kysaro finishes its scene. `SettlementSceneLiveDataTests` holds the list and fails on a revert. The town and village scenes are in the picker too, the 7 new picker names await translation, and none of the new scenes has been loaded in game yet.
- 2026-10-17: two new Gondor field scenes. `taom_gondor_battle_001_forceatmo` ("Ruined Outpost") joins the Pelennor and Anorien region (cells 85, 86, 90, 91, 92) and comes up in 1 battle of 6 there, 1 of 10 on cell 85. `taom_gondor_pelennor_fields_forceatmo` ("Pelennor Fields") shows Minas Tirith and Osgiliath on its horizon, so it gets its own region on their cell 85 only, at 1 in 10 (cell 85 also draws the Anduin crossings scenes). Both are in the Custom Battle picker for the load test, and their names await translation. Ruined Outpost is town-sized (about 22,600 entities against a vanilla field maximum near 1,300), so its load and frame cost are measured at that test.
- 2026-10-03: `sp_battle_scenes.xml` generated per Middle-earth region by `tools/build_battle_scenes.py`, from cells measured against the live settlements (native scenes grouped by type: Plain to the green kingdoms, Desert to Harad and Umbar, Steppe to Rhûn, Khand and the Erebor/Dale plains, Swamp to the Dead Marshes). The 6 Mordor and 2 Rohan field scenes are back in rotation on corrected cells, accepting the June `pbr_terrain` GPU crash risk. Region table: [worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md#region-table-2026-10-03-live).
- 2026-06-01 — Re-enabled `Patch0_BattleScenes` (`feat(battle-scenes)`): uncommented the `PatchCategory` gate so TAOM's full 0–255 `sp_battle_scenes` table loads; doc flipped DISABLED→ENABLED.
- 2026-06-01 — Root-caused/resolved a campaign-load `AccessViolationException` (`fix(taom_map)+docs(battle-scenes)`) traced to a mis-imported `worldmap_battle_scene_grid` texture; fixed by a lossless re-import at the `Assets/world_map/` resource path.
- 2026-05-31 — Deep-dive reference doc on the worldmap battle-scene grid + LOTR re-author plan (`docs(battle-scenes)`).
- 2026-03-05 — Added the `MBMapScene_GetBattleSceneIndexMap_Patch` and `MapScene_Load_DiagnosticPatch` diagnostic patches.
- 2026-02-11 — Implemented the battle scene system (`sp_battle_scenes.xml`) and the `Campaign_InitializeScenes_Patch` loader.

## GitHub Issue

- **Issue:** None yet for the 2026-10-03 region remap, the 2026-10-17 Gondor scenes or Kysaro's 2026-10-17 drop and
  its known issues below (owed, on Mike's word).
- **Status:** Enabled; in-game region checks owed.
- **Known limitation (2026-10-17):** no release gate checks that a channel's `TAOM_Map/SceneObj` holds every `taom_*`
  scene that TAOM's `sp_battle_scenes.xml` and `custom_battle_scenes.xml` name; `--check` reads this machine only. On
  2026-10-17 the patreon and public channels lacked both Gondor scenes and `taom_rohan_edoras_town_forceatmo`, and
  no channel (testing included) held any of the 8 scenes from Kysaro's drop (lotraom-assets `db387f23f`) or its
  `settlements.xml` wiring. Before a release, after an ordinary test build, run `BANNERLORD_GAME_DIR=E:/LOTRAOM_Releases/<channel>
  dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter
  "FullyQualifiedName~CustomBattleSceneLiveDataTests|FullyQualifiedName~SettlementSceneLiveDataTests" --
  MSTest.MapInconclusiveToFailed=true` (`--no-build` because the build reads that variable as its game folder; the
  last argument makes a channel without `TAOM_Map` fail instead of passing as inconclusive). Every `taom_*` field scene is also a picker row, so the
  two classes cover both files and the settlement wiring; on 2026-10-17 the patreon channel failed both. The planned
  gate is a refusal in `tools/package_release.py`'s dry run.
- **Known issues in Kysaro's 2026-10-17 scenes (wired as delivered on Mike's word, fixes requested from Kysaro):**
  `taom_mordor_town_01_forceatmo`, now the center of five Mordor towns including the Mordor start town Barad-dûr, has
  no `sp_player`, so a night walk, a second walk in one stay or a disguised entry spawns the player at the world
  origin, outside the walk area, and the mission retreats after 10 s. Pelargir, Serelond (town and arena) and
  Lothgobel place `Lotraom_comprehend_*` prefabs no loaded module defines (`Decoration_014` to `_016` exist nowhere;
  `House_12` and `House_50` sit only in `TAOM_Map/Prefabs_Unused`), so those pieces load empty. Serelond's town has
  no usable siege rock piles (`audit_siege_props.py`) and its arena no tournament fight set. Load cost, unmeasured
  in game: Pelargir (about 33,300 entities) and Serelond (about 29,400) are Dol Amroth-sized against vanilla towns
  near 12,000; their guard points spawn about 180 to 200 guards per visit at their starting prosperity and about 280 to 300 once
  prosperous, which Battle Size does not cap; the
  Serelond arena carries the whole town terrain and flora; Lothgobel holds about 13,000 rock-pile entities and 198
  sheep.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/shader-precompilation.md](./shader-precompilation.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/module-map.md](../modding/module-map.md)
- [docs/modding/recipe-add-a-field-battle-scene.md](../modding/recipe-add-a-field-battle-scene.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md)

<!-- backlinks-end -->
