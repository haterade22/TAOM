# Worldmap Battle-Scene Grid (how field-battle terrain is chosen)

How Bannerlord 1.4.5 decides **which battle-terrain scene loads when a field battle starts on the campaign
map**, what the `worldmap_battle_scene_grid` texture actually is, and how to **re-author it for TAOM's
Middle-earth map**. Companion to [scene-reference-audit.md](scene-reference-audit.md) (which validates the
`sp_battle_scenes.xml` *data*) — this doc explains the *texture* that drives it.

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
> now confirmed to still apply on 1.4.5).

> ## ⚠️ A mis-imported grid CRASHES campaign load
>
> If a campaign crashes on load with a native `AccessViolationException` in `get_battle_scene_index_map` (boots to
> menu fine, dies loading a campaign), the `worldmap_battle_scene_grid` texture is mis-imported. **Both** of these
> must hold: (1) resource path = **`world_map/worldmap_battle_scene_grid`** (a wrong path like `Battle Map/` leaves
> a conflicting/orphaned resource); (2) **lossless** — Texture Inspector → **Do Not Compress** + **Dont Degrade**
> (DXT mangles the R-channel index bytes; a `~700 KB` compressed `.rdc` vs the correct `~4.19 MB` uncompressed one
> is the tell). Recovery during the 2026-06-01 incident was to **delete the bad import**; the definitive fix was
> re-importing lossless at `world_map/`. Patch0's AV retry guard does **not** rescue a deterministic mis-import.

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
| Grid resolved (2026-06-01): re-imported **lossless** at `Assets/world_map/` (4.19 MB uncompressed `.rdc`) → campaign loads with `Main_map` unchanged | filesystem scan |
| `Patch0_BattleScenes` is **ENABLED** (re-enabled 2026-06-01, `Main/SubModule.cs:159`) → loads TAOM's `sp_battle_scenes.xml` | grep |
| `sp_battle_scenes.xml` is **not** an XmlNode in `Main/_Module/SubModule.xml` (only `CustomBattleScenes`→`custom_battle_scenes`, a different file) → it is loaded **only** by Patch0's `Campaign_InitializeScenes_Patch.cs:19` | grep |

### The coupling that matters

The grid's pixel **index values must all be covered by the *active* `sp_battle_scenes.xml`.**

- **Patch0 enabled (current, since 2026-06-01):** the **active** file is **TAOM's `Modules/TAOM/ModuleData/sp_battle_scenes.xml`** (covers all 0–255, 0 crash suspects). Indices 158–255 resolve to `battle_terrain_r`.
- *(Before re-enabling, vanilla `SandBox/sp_battle_scenes.xml` was active — 1–157 only — so the grid's extended indices `Debug.FailedAssert`ed + fell back to terrain-type. That's the gap Patch0 closes.)*

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

### Phase 2 — author + import the grid (Bannerlord editor — your domain)

Per the authoritative [BannerlordModding.LT › Battle Scene Grid](https://docs.bannerlordmodding.lt/editor/battle_scene_grid/)
(a **1.2.12-era** source — see the 1.4.5 caveat). The `map_indices`↔R-byte mapping is corroborated against installed v1.4.5:

1. Author the grid texture **externally** at **1024×1024** ("native's size; not sure if other sizes work").
   **R channel = scene index** 0–255 — the value that must appear in a `<Scene map_indices="…">` of
   `sp_battle_scenes.xml` (corroborated by the decompiled 2-byte texel format, `MapScene.cs:456-459`; the docs'
   `battle_terrain_020` example list is verbatim TAOM's xml). **G channel = party entry orientation** (which side
   parties enter from).
2. **Import it at the `Assets/world_map/` resource path**, **LOSSLESS**: Texture Inspector → check **Do Not
   Compress** + **Dont Degrade**. **Both** matter (CONFIRMED 2026-06-01): the resource name must be
   `world_map/worldmap_battle_scene_grid` (a wrong path like `Battle Map/` orphans/conflicts the resource → crash),
   and compression mangles the R-channel index bytes (the correct import is ~4.19 MB = 1024×1024×4 uncompressed;
   a ~700 KB compressed `.rdc` is the bad-import tell).
3. **That's it — no `Main_map` re-bake.** CONFIRMED on 1.4.5 (2026-06-01): a lossless import to `Assets/world_map/`
   with `SceneObj/Main_map` **unchanged** loads correctly. Launch a campaign to verify (Patch0's diagnostic patch
   logs the selected map module; the retry guard wraps `GetBattleSceneIndexMap`).
4. Ensure `sp_battle_scenes.xml` covers every R-byte value the grid uses, and confirm `TAOM_Map` loads **after**
   SandBox/NavalDLC so its `Main_map` wins. Run `python tools/audit_battle_scenes.py`.

**Source archive:** keep `AssetSources/world_map/worldmap_battle_scene_grid.png` (and/or the `.zip`) as your
editable grid **source** — `AssetSources/` is editor-only, never loaded at runtime.

### Phase 3 — Patch0 (repo) — DONE
`_harmony.PatchCategory("Patch0_BattleScenes");` was **re-enabled 2026-06-01** at `Main/SubModule.cs:159` (loads
TAOM's `sp_battle_scenes.xml` so extended indices 158–255 resolve). The historical disabled-state notes below are
kept for context.
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
- [docs/reviews/rca-worldmap-grid-loose-import-crash-2026-06-01.md](../reviews/rca-worldmap-grid-loose-import-crash-2026-06-01.md)

<!-- backlinks-end -->
