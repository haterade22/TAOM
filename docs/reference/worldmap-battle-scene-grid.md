# Worldmap Battle-Scene Grid (how field-battle terrain is chosen)

How Bannerlord 1.4.5 decides **which battle-terrain scene loads when a field battle starts on the campaign
map**, what the `worldmap_battle_scene_grid` texture actually is, and how to **re-author it for TAOM's
Middle-earth map**. Companion to [scene-reference-audit.md](scene-reference-audit.md) (which validates the
`sp_battle_scenes.xml` *data*) — this doc explains the *texture* that drives it.

> **TL;DR — the one fact that changes everything:** `worldmap_battle_scene_grid` is **never loaded by filename
> at runtime.** It is a *source asset the Bannerlord editor bakes into the `Main_map` scene*; at runtime the
> engine reads the baked index map **natively** out of the loaded scene. "Replacing the texture" is an **editor
> bake operation**, not a file swap. The editor's *"Source file is missing"* is a content-pipeline condition,
> not a code bug.

## Data flow (verified against installed v1.4.5 + decompiled SandBox)

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
   │    PRIMARY:  pick the <Scene> whose map_indices="…" contains sceneIndex (random among ties)
   │    FALLBACK: no index match → filter scenes by navmesh TerrainType (FaceGroupIndex);
   │             then by IsNaval; then any scene — each fallback logs Debug.FailedAssert
   │
   ▼ returns SceneID (e.g. "battle_terrain_a")
   ▼ CampaignMission.OpenBattleMission(sceneID, …)
```

There are **two independent signals**, used in priority order:

1. **`sceneIndex` (primary)** — comes from the **baked grid texture**. This is the `map_indices="…"` attribute
   in `sp_battle_scenes.xml`.
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

### Baked, not bound (proof) — so importing the texture is not enough

The index map is **baked into the `Main_map` scene data**, *not* bound as a named runtime texture the engine
samples at load. Evidence (verified 2026-05-31):

- **Neither** vanilla `SandBox/SceneObj/Main_map/references.txt` **nor** TAOM_Map's references `battle_scene_grid`
  (both reference only `worldmap_colorgrade_*`). A texture read at runtime by name would appear here.
- Vanilla SandBox ships **no** loose `worldmap_battle_scene_grid` asset and **no** `world_map/` Assets folder —
  the grid exists only as data baked into its `Main_map`.

**Consequence:** importing/compiling `worldmap_battle_scene_grid_tex.tpac` makes the source available **to the
editor's bake tool** — it does **not** change battle-terrain selection by itself. Selection only changes after the
`Main_map` scene is **re-baked** (the bake reads the grid texture and writes the index data into the scene). A
`Main_map` whose `scene.xscene`/`terrain.bin` timestamp hasn't moved is still serving the *old* index map.

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
selection chain, and a separate `NavalDLC/ModuleData/sp_battle_scenes.xml`. The native **bake/encode** of the PNG
→ index map is C++ and not decompilable; **the exact channel encoding must be confirmed in the editor** (the raw
red-channel data strongly implies red = scene index).

## Current TAOM state (verified on disk 2026-05-31)

| Fact | Evidence |
|---|---|
| TAOM_Map ships a full custom `Main_map` (11 MB `scene.xscene` + 56 MB `terrain.bin`), **baked 2026-05-28** | `Modules/TAOM_Map/SceneObj/Main_map/` |
| 3 active modules own `Main_map`: SandBox, NavalDLC, **TAOM_Map** → TAOM_Map's map is used only if it loads **last** | filesystem scan |
| Grid **source** PNG (113 KB) at `AssetSources/Battle Map/worldmap_battle_scene_grid/` (16:36); **reimported 2026-05-31** → compiled `Assets/Battle Map/worldmap_battle_scene_grid/worldmap_battle_scene_grid_tex.tpac` (16:47) now present, so *"Source file is missing"* is **resolved** | filesystem scan |
| **`Main_map` still baked 2026-05-28** (`scene.xscene`/`terrain.bin` timestamps unchanged after the reimport) → the new grid is **NOT yet baked into the scene**; battles still use the old index map until `Main_map` is re-baked | filesystem scan |
| TAOM imported the grid to `Assets/Battle Map/…`, **not** vanilla's `world_map/…` resource path → **verify in-editor** the bake tool finds the grid where it expects it | filesystem scan |
| `Patch0_BattleScenes` is **DISABLED** — `Main/SubModule.cs:158` has `// _harmony.PatchCategory("Patch0_BattleScenes");` commented out | grep |
| `sp_battle_scenes.xml` is **not** an XmlNode in `Main/_Module/SubModule.xml` (only `CustomBattleScenes`→`custom_battle_scenes`, a different file) → it is loaded **only** by Patch0's `Campaign_InitializeScenes_Patch.cs:19` | grep |

### The coupling that matters

The baked grid's pixel **index values must all be covered by the *active* `sp_battle_scenes.xml`.**

- **Patch0 disabled (current):** the **active** file is **vanilla `SandBox/ModuleData/sp_battle_scenes.xml`**.
  TAOM's extended copy (deployed at `Modules/TAOM/ModuleData/sp_battle_scenes.xml`, covers all 0–255) is **inert**.
- A custom LOTR grid that paints indices the *active* XML doesn't cover → `Debug.FailedAssert` + terrain-type
  fallback (often the wrong terrain), not necessarily a crash but wrong/asserting.

> Verify index coverage any time the grid or XML changes: `python tools/audit_battle_scenes.py`
> (as of 2026-05-31 the *deployed TAOM* file covers all 256 indices with 0 crash suspects — but that file is
> inert until Patch0 is enabled).

## Re-authoring the grid for the Middle-earth map

Two coupled halves. **Asset half = Bannerlord editor (your domain, external tool).** **Data/code half = repo.**

### Decision fork

| | Approach A — reuse vanilla indices | Approach B — custom indices |
|---|---|---|
| Grid paints | only vanilla indices (those in `SandBox/sp_battle_scenes.xml`) | any index 0–255 |
| Code change | **none** (Patch0 stays disabled) | re-enable `Patch0_BattleScenes` so TAOM's extended XML loads |
| Battle terrains available | vanilla scenes only | custom LOTR battle-terrain scenes possible |
| Risk | lowest — ships immediately | Patch0's AccessViolation retry fires every battle; needs in-game smoke test |

**Recommended:** start with **A** (correct vanilla terrain under each region, zero code risk); escalate to **B**
per-region only where vanilla terrain can't express the lore.

### Proposed region → terrain mapping (design starting point — refine in-editor)

Paint each Middle-earth region with an index that resolves (in the active XML) to this terrain profile. Candidate
vanilla scene ids exist today; the editor author picks the specific index that maps to one of them.

| Region | Target `terrain` / `forest_density` / `TerrainType` | Candidate vanilla scene id(s) |
|---|---|---|
| Gondor lowlands, Pelennor, Rohan plains | `Plain` / `Low` | `battle_terrain_a` |
| Rohan / Wold grassland, Rhûn steppe, Khand | `Steppe` | `battle_terrain_012`, `battle_terrain_014`, `battle_terrain_017` |
| Mirkwood, Lothlórien, Fangorn, Druadan | `Plain` / `High` (+ `Mountain` / `Lake`) | `battle_terrain_h`, `battle_terrain_k`, `battle_terrain_001`, `battle_terrain_004` |
| Misty Mountains, Ered Luin, White Mountains | `Plain` (+ `Canyon` / `Mountain`) | `battle_terrain_031` |
| Mordor (Gorgoroth ash), Harad desert, Near Harad | `Desert` (+ `Canyon`) | `battle_terrain_g`, `battle_terrain_b`, `battle_terrain_d`, `battle_terrain_009` |
| Dead Marshes, Nindalf, Nan Curunír fens | `Swamp` | `battle_terrain_005`, `battle_terrain_034` |
| Anduin banks, Entwash, river crossings | `Plain` / `Low` (+ `River` / `Water`) | `battle_terrain_f`, `battle_terrain_s`, `battle_terrain_011` |

*(Scene ids above were read from TAOM's `sp_battle_scenes.xml`; the exact `terrain`/`TerrainType` of each in the
**active vanilla** file should be re-confirmed with `audit_battle_scenes.py` before the editor pass.)*

### Phase 1 — design the index→terrain table (repo)
Refine the table above against the **active** XML; run `python tools/audit_battle_scenes.py`. Any desired terrain
with no vanilla index is the trigger to use Approach B for those cells.

### Phase 2 — author + bake (Bannerlord editor — your domain)
1. ~~Resolve *"Source file is missing"*~~ — **done 2026-05-31** (grid reimported; compiled `tex.tpac` present).
   Open question to confirm in-editor: the grid imported to `Assets/Battle Map/…`, but the inspector showed
   vanilla's resource path as `world_map/worldmap_battle_scene_grid.png` — **verify the bake tool resolves the
   grid where it expects it** (it may need to sit at the `world_map/` resource name, not `Battle Map/`).
2. Paint the grid per the Phase 1 table (scene index in the channel the bake reads — **confirm which channel**;
   raw data implies red).
3. **Re-bake `Main_map`** so the scene carries the new index map — **this is the step that actually changes
   battles** (importing the texture does not; see "Baked, not bound" above). Confirm the `SceneObj/Main_map`
   `scene.xscene`/`terrain.bin` timestamp **moves off 2026-05-28** — if it doesn't, the bake didn't take.
4. Confirm TAOM_Map loads **after** SandBox/NavalDLC so its `Main_map` wins.

### Phase 3 — (Approach B only) re-enable Patch0 (repo)
- Uncomment `_harmony.PatchCategory("Patch0_BattleScenes");` in `Main/SubModule.cs:158`.
- Ensure `Main/_Module/ModuleData/sp_battle_scenes.xml` covers every painted index and every Scene id resolves to
  a real `SceneObj/<id>/` (run `audit_battle_scenes.py`).
- Follow the in-game smoke test in [features/battle-scenes.md](../features/battle-scenes.md#re-enable) (watch
  rgl_log for the diagnostic "Selected map module" line + retry-guard warnings).

## Verification

- **Data:** `python tools/audit_battle_scenes.py` → 0 crash suspects; every painted index covered by the *active*
  XML; every Scene id has a `SceneObj/<id>/` folder.
- **Code (Approach B):** `./build.ps1 -RunTests` green; `/verify-bindings` resolves the Patch0 targets.
- **In-game:** start a field battle in 2–3 distinct regions (e.g. Mordor, Rohan, Mirkwood); confirm the loaded
  battle terrain matches the painted region. `Debug.FailedAssert` in rgl_log = an index the active XML doesn't
  cover.

## Reference files

- `E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\MapScene.cs` (Load 168–251, GetMapPatchAtPosition 436–467)
- `…\TaleWorlds.MountAndBlade\…\{MBMapScene,IMBMapScene}.cs` (native bridge)
- `…\TaleWorlds.CampaignSystem.GameComponents\DefaultSceneModel.cs:26` (`GetBattleSceneForMapPatch`)
- `…\TaleWorlds.CampaignSystem\GameSceneDataManager.cs:76` (`LoadSPBattleScenes`)

## Related

- [features/battle-scenes.md](../features/battle-scenes.md) — the (disabled) `Patch0_BattleScenes` feature + re-enable checklist
- [scene-reference-audit.md](scene-reference-audit.md) — validates `sp_battle_scenes.xml` Scene ids vs on-disk SceneObj
- [taom-map-settlement-naming.md](taom-map-settlement-naming.md) — TAOM_Map is a live external module, not a repo shadow
- [.claude/rules/vanilla-data-comparison.md](../../.claude/rules/vanilla-data-comparison.md) — diff vs installed vanilla before editing mirrored data

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-scenes.md](../features/battle-scenes.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/scene-reference-audit.md](./scene-reference-audit.md)

<!-- backlinks-end -->
