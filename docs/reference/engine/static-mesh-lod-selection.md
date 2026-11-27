# How the engine picks a detail level for a static mesh

Read on 2026-11-25 for the kingdom kits job (issue #1600), from the installed v1.5.7 `TaleWorlds.Native.dll` with
`python tools/native_decompile.py` (the cached Ghidra project) and the managed sources (`pwsh tools/taom-src.ps1 path
<Type>`). A second worker checked the table, the distance and the fill of missing levels with his own disassembly.
Nothing here was tested in the game.

## The rule in short

- The engine picks the level of a scene entity **by distance only**. It uses no screen size, no field of view and no
  triangle count.
- The distances come from **one global table of seven thresholds**, set by the graphics option Environment Detail
  (`NativeOptionsType.EnvironmentDetail`, config field `environment_detail`, default 4 = Very High).
- The distance is from the camera to the **surface of the entity's bounding sphere**: the distance to the centre minus
  the radius, when the radius is over 2.0 m, and never under 0.
- The level is the number of thresholds under that distance, limited to 0 to 7. An entity's enforced maximum and a
  metamesh's own bias (an integer) are added after that.
- **A missing level draws the level before it.** A metamesh with levels 0, 3 and 6 draws level 0 at levels 1 and 2,
  `.lod3` at levels 4 and 5, and `.lod6` at levels 6 and 7.

## The distances

Distance in metres, to the surface of the bounding sphere, at which each level starts.

| Environment Detail | LOD1 | LOD2 | LOD3 | LOD4 | LOD5 | LOD6 | LOD7 |
|---|---|---|---|---|---|---|---|
| Very High (4, the default) | 15 | 22.5 | 30 | 50 | 70 | 130 | 210 |
| High (3) | 11.65 | 17.48 | 23.3 | 38.83 | 54.37 | 100.97 | 163.1 |
| Medium (2) | 8.3 | 12.45 | 16.6 | 27.67 | 38.73 | 71.93 | 116.2 |
| Low (1) | 4.95 | 7.43 | 9.9 | 16.5 | 23.1 | 42.9 | 69.3 |
| Very Low (0) | The Low table, plus one level | | | | | | |

So a mesh with levels 0, 3 and 6 shows its full level up to 30 m, `.lod3` from 30 m and `.lod6` from 130 m at the
default option; from 9.9 m and 42.9 m at Low.

**What it means for an author.** A large piece switches later than a small one, because the radius of its sphere is
subtracted: a 20 m wall piece with a radius near 15 m switches to `.lod3` when the camera is about 45 m from its centre
(the radius is an estimate; the engine's own value was not read). Every entity picks its own level, so the parts of a
piece that are separate entities (the parapet parts of a siege wall) can stand at another level than the main mesh.
The table is the same for every mesh: the per-mesh settings that exist are the metamesh bias, a metamesh visible
distance (it hides the mesh; `FLT_MAX` on a new import) and the entity's enforced maximum level.

## Sources

| Fact | Source |
|---|---|
| The managed calls: `GameEntity.GetLodLevelForDistanceSq`, `GameEntity.SetEnforcedMaximumLodLevel`, `MetaMesh.SetLodBias`, `MetaMesh.GetLodMaskForMeshAtIndex`, `AddMesh(Mesh, uint lodLevel)`, `SetNumLods` | `TaleWorlds.Engine.GameEntity.cs` lines 332 and 337, `MetaMesh.cs` lines 51, 121, 146 and 291. The v1.5.7 sources hold no managed caller of the first three |
| The option: `NativeOptionsType.EnvironmentDetail` (39), five steps from Very Low to Very High, "Sets the detail level of environment props."; a separate option, Character Detail, serves agents | `NativeSelectionOptionData.cs` line 63; `Native/ModuleData/global_strings.xml` lines 1193 to 1206 and 1538; the native config constructor names the field `environment_detail` with default 4 |
| The table: with t = the option minus 1, limited to 0 to 3: d0 = 3.35 t + 4.95; d1 and d2 each add 1.675 t + 2.475; d3 and d4 each add 4.4667 t + 6.6; d5 adds 13.4 t + 19.8; d6 adds 17.8667 t + 26.4. Stored squared. A flag adds one level when the option is 0 | The function at RVA 0x300E0 (`--rva 0x300e0`), constants read from `.rdata` |
| The lookup: the count of table entries under the squared distance, plus offsets that were not traced, limited to 0 to 7 | RVA 0x328DC0; `IGameEntity.GetLodLevelForDistanceSq` only calls it, so the managed call reads the global table and no entity data |
| The distance: centre distance, minus the radius when it is over 2.0, floored at 0; then the entity's enforced maximum and its forced-level bits | RVA 0x328E50, called per entity and per view |
| The metamesh: level = the entity's level plus the metamesh bias, limited to the number of levels minus 1; every mesh whose mask holds that bit is drawn; a visible distance culls the whole metamesh | RVA 0x29DB40 |
| Missing levels: for each level with no mesh, every mesh of the level before gets that level's bit | RVA 0x2A0D60, run after `AddMesh`, the rebuild and the merge paths |
| Earlier readings that agree: "A missing level is drawn as the level before it" | [armory-guide.md](../armory-guide.md), the LOD rules |

## Not verified

- Whether the load path of a Kit-compiled metamesh runs the same fill of missing levels, or stores masks that the Kit
  filled. The fill was read on the runtime and editor paths.
- The extra offsets in the lookup and the two conditions that force level 0 (which modes they are).
- That the value read as the radius is the world bounding-sphere radius, and what the engine builds it from (the
  visible meshes alone, or also the bodies and the children).
- What the entity flags `smooth_lod_transitions`, `do_not_use_smooth_lod` and `no_lods` change at a switch.
- Whether the Modding Kit lets an author set the metamesh bias or the visible distance per mesh.
- Nothing in the game. `GameEntity.GetLodLevelForDistanceSq(d * d)` gives a cheap live check of the table at the
  current option.

Agents follow another rule (one level per agent from its distance, moved by Character Detail):
[agent-spawn-and-render-pipeline.md](agent-spawn-and-render-pipeline.md).
