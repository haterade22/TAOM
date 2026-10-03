# Recipe: adding a custom field-battle scene

## What this file is

The steps for getting a custom battle scene into campaign field battles: the fights that start when two
parties meet on the map. Siege, village, town and arena scenes are wired elsewhere; this is only the
open-field case. Since 2026-10-03 the mapping from map cells to scenes is generated from one table in
`tools/build_battle_scenes.py`, so adding a scene is a table edit and a re-run, never a hand edit of
`sp_battle_scenes.xml`.

## How a field battle picks its scene

1. The campaign map carries a 1024 x 1024 texture, `worldmap_battle_scene_grid`, whose red channel paints
   every map cell with an index. Map position to pixel is `floor(pos / 1600 x 1024)`, and the native copy
   reads the texture bottom row first, so the PNG's top row is the map's north edge.
2. `SandBox.MapScene.GetMapPatchAtPosition` reads the index under the party.
3. `DefaultSceneModel.GetBattleSceneForMapPatch` collects every scene whose `map_indices` list that index
   and picks one with equal odds. (With more than one match it first calls a `Debug.FailedAssert`, which
   the installed engine's debug manager ignores.)
4. The scene list is TAOM's `sp_battle_scenes.xml`, loaded in place of vanilla's by
   `Campaign_InitializeScenes_Patch` (`Patch0_BattleScenes`), for new and loaded campaigns alike.

Which cell belongs to which region, and how that was measured, is the region table in
[worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md#region-table-2026-10-03-live).

## Steps

1. **Build the scene** in the Modding Kit under `TAOM_Map/SceneObj/<id>/`. Name it
   `taom_<region>_battle_<name>_forceatmo`: the `forceatmo` substring is what
   `AtmosphereOverrideService.RequiresAtmosphereOverride` keys on (case-insensitive), see
   [atmosphere-persistence.md](../features/atmosphere-persistence.md).
2. **Load it once in Custom Battle before it goes near the campaign.** Add a `<Scene ... is_siege_map="false" />`
   row under `<!--Battle Scenes-->` in `Main/_Module/ModuleData/custom_battle_scenes.xml`, with a name of the
   form `{=aom_<id>_name}[Region] Name`, register that string in `taom_battle_scene_strings.xml`, and run
   `/localize`. A scene that crashes here crashes every campaign battle on its cells.
3. **Give it cells.** In `tools/build_battle_scenes.py`:
   - add the id to `CUSTOM` with a `terrain` and `forest_density` (`terrain` is read only when no scene lists a
     cell, which full coverage prevents, and `forest_density` by nothing, so both document the scene rather
     than steer it);
   - add the id to the scene list of the region it belongs to in `REGIONS`, or add a new region line with its
     own cells. Take cells from the region table, never from memory: the old Rohan scene sat on Minas Tirith's
     cells for months. A new or moved region also gets its row in that table.
4. **Regenerate:** `python tools/build_battle_scenes.py` (dry run: read its "shared cell" lines, every one should
   be a sharing you meant), then `--apply`, then `--check` (exit 0: the file is current and every scene id has a
   `SceneObj` folder). Run `python -m unittest tools.tests.test_build_battle_scenes`.
5. **Deploy:** `./build.ps1`.
6. **Prove it in game.** Stand on one of its cells and run `taom.print_battle_scene`: it prints the cell's
   index and every candidate scene. Fight a battle there and find the
   `[BattleLoad] ... mapIndex=<n> sceneId='<id>'` line in `bin/Win64_Shipping_Client/Logs/taom_debug_*.log`.
   A cell with several scenes needs several battles before yours comes up.

## Things that bite

| Trap | What to do |
|---|---|
| **Scene asset crashes on load** | `lotrtaom_iron_hills_01_forceatmo` crashed 8 of 8 loads (#280) and is out of every list. Step 2 is the gate. |
| **`pbr_terrain` GPU crash** | The Mordor and Rohan scenes were pulled 2026-06-19 because their terrain shader crashes some GPUs on load (#287), and were put back 2026-10-03 with that risk accepted. A new open-field scene can carry the same risk. |
| **No shipped shader cache** | TAOM_Map scenes ship no `compressed_shader_cache.sack`, so terrain and atmosphere shaders compile on first entry. The precompile walk that would cover this is parked ([shader-precompilation.md](../features/shader-precompilation.md)); its scene list is separate from this one. |
| **Odds** | Every `<Scene>` element on a cell is equally likely, and the generator writes each scene once per cell however many regions name it. The dry run prints each shared cell with every region's scene count, so `77: Rhun (11) + Mordor: Barad-dur (4)` means Mordor 4 times in 15. A scene two regions both list counts once: cell 114 prints `Near Harad (8) + Harad (7)` but holds 12 scenes, since three are in both. To make a scene more common, give its cells fewer other scenes. (The engine counts elements, so a scene written twice would weigh double; the generator does not do that today, and a test pins one element per id.) |
| **A table the engine cannot load** | The loader `int.Parse`s every `map_indices` token at campaign start and nothing catches a throw, so `map_indices=""` or a malformed comment stops every campaign. The generator refuses such a table (exit 1, nothing written); never hand-edit the XML past it. |
| **Index 255** | The grid's unpainted area: Umbar's settlements, the sea, the map edge and two Ered Luin settlements share one scene list. Painting Umbar its own index is the fix. |
| **Shared cells** | A cell in two regions draws from both lists. The dry run prints every shared cell; a test pins that Mordor's scenes stay off the Harad and Khand cells 176-178. |

## Unused custom scenes (2026-10-03)

| Scene | State |
|---|---|
| `taom_dwarves_battle_001_forceatmo` | On disk in `TAOM_Map/SceneObj`; dropped from rotation 2026-06-11 (`8f376d59`, reason not recorded) and loaded by no scene list (the precompile list names it only as excluded). Candidate for Erebor and the Iron Hills after a Custom Battle load test. |
| `lotrtaom_iron_hills_01_forceatmo` | Crashes on load (#280); repair the scene first. |
| `wip_taom_rohan_battle_001` | Work in progress folder, not a finished scene. |

## Related

- [battle-scenes.md](../features/battle-scenes.md): the feature, `Patch0_BattleScenes` and its history
- [worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md): the grid texture, import rules and the region table
- [scene-reference-audit.md](../reference/scene-reference-audit.md): scene id checks after an engine bump
- `tools/README.md`, "Content Generation": the `build_battle_scenes.py` row
