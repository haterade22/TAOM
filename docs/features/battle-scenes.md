# Battle Scenes

> **Status: ENABLED.** `Patch0_BattleScenes` is applied at module load (`TryPatchCategory("Patch0_BattleScenes")`,
> [Main/SubModule.cs:520](../../Main/SubModule.cs)), re-enabled 2026-06-01. Since 2026-10-03 the field-battle
> scene list is generated per Middle-earth region by `tools/build_battle_scenes.py`, and TAOM's own Mordor and Rohan
> field scenes are back in rotation, with the June `pbr_terrain` GPU crash risk accepted (#287). In-game checks
> per region are owed (see "Verifying" below).

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
covered. 93 scenes today: 85 native ones from SandBoxCore and TAOM's 8 from `TAOM_Map/SceneObj`.

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
element, `--check` / `--dry-run` / `--apply`, and the region regression that kept Mordor scenes off Harad and Khand
cells, plus a CI check that the committed XML matches `REGIONS` and parses the way the engine parses it. The C#
patches have no tests: they are static, IO-bound and need a live game.

## Verifying

1. `python tools/build_battle_scenes.py --check` exits 0 (the file is current and every scene has a `SceneObj`).
2. Build (`./build.ps1`).
3. In game, on a cell of each region you changed: `taom.print_battle_scene` lists the expected candidates; a field
   battle there logs `[BattleLoad] ... mapIndex=<n> sceneId='<id>'` in `taom_debug_*.log`.
4. The rgl_log diagnostic line `TAOM: >>> Selected map module: 'TAOM_Map'` confirms the right `Main_map` won.

## How to Diagnose "wrong map module wins"

The `MapScene_Load_DiagnosticPatch` prints to the engine log every time `MapScene.Load` runs. Bannerlord's "last active module with a `Main_map` scene wins" rule means load order matters. If the diagnostic shows `SandBox` as the selected module instead of `TAOM_Map`, the load-order setting (`Modules/Native/SubModule.xml`-style ordering, or the Launcher) is putting TAOM_Map ahead of SandBox; reverse it.

## Changelog

- 2026-10-03: `sp_battle_scenes.xml` generated per Middle-earth region by `tools/build_battle_scenes.py`, from cells measured against the live settlements (native scenes grouped by type: Plain to the green kingdoms, Desert to Harad and Umbar, Steppe to Rhûn, Khand and the Erebor/Dale plains, Swamp to the Dead Marshes). The 6 Mordor and 2 Rohan field scenes are back in rotation on corrected cells, accepting the June `pbr_terrain` GPU crash risk. Region table: [worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md#region-table-2026-10-03-live).
- 2026-06-01 — Re-enabled `Patch0_BattleScenes` (`feat(battle-scenes)`): uncommented the `PatchCategory` gate so TAOM's full 0–255 `sp_battle_scenes` table loads; doc flipped DISABLED→ENABLED.
- 2026-06-01 — Root-caused/resolved a campaign-load `AccessViolationException` (`fix(taom_map)+docs(battle-scenes)`) traced to a mis-imported `worldmap_battle_scene_grid` texture; fixed by a lossless re-import at the `Assets/world_map/` resource path.
- 2026-05-31 — Deep-dive reference doc on the worldmap battle-scene grid + LOTR re-author plan (`docs(battle-scenes)`).
- 2026-03-05 — Added the `MBMapScene_GetBattleSceneIndexMap_Patch` and `MapScene_Load_DiagnosticPatch` diagnostic patches.
- 2026-02-11 — Implemented the battle scene system (`sp_battle_scenes.xml`) and the `Campaign_InitializeScenes_Patch` loader.

## GitHub Issue

- **Issue:** None yet for the 2026-10-03 region remap (owed).
- **Status:** Enabled; in-game region checks owed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/module-map.md](../modding/module-map.md)
- [docs/reference/worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md)

<!-- backlinks-end -->
