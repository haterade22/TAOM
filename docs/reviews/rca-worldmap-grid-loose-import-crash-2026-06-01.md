# RCA — Loose `worldmap_battle_scene_grid` import crashes campaign map-load (2026-06-01)

> **CORRECTION appended 2026-06-01 (same day):** the *confirmed* finding below — the loose import artifacts were
> the crash **trigger** (proven by the revert test) — stands. But the original **prescription** ("never import the
> grid; it must be baked into `Main_map`") was **wrong**: the authoritative
> [BannerlordModding.LT docs](https://docs.bannerlordmodding.lt/editor/battle_scene_grid/) say you **do** import
> the grid into the mod's `Assets` folder. The real defect was a **mis-import**: the index map must be **lossless**
> (Texture Inspector → **Do Not Compress** + **Dont Degrade**); compression mangles the R-channel index bytes the
> native sampler reads, and/or a stale `RuntimeDataCache/<guid>.rdc` lingered. Also note the docs are **1.2.12** —
> whether 1.4.5 additionally needs a `Main_map` re-bake is unverified (the crash means a naive 1.2.12-style import
> did not "just work" in 1.4.5). Read the "Prevention" section below through this correction.

## Symptom

Starting/loading a campaign crashed with a native `System.AccessViolationException`:

```
ManagedCallbacks.ScriptingInterfaceOfIMBMapScene.GetBattleSceneIndexMap(UIntPtr scenePointer, Byte[] indexData)   [native]
TaleWorlds.MountAndBlade.MBMapScene.GetBattleSceneIndexMap(Scene, Byte[]& indexData, Int32& width, Int32& height)  MBMapScene.cs:100
SandBox.MapScene.Load()
TaleWorlds.CampaignSystem.Campaign.LoadMapScene()
… GameLoadingState.OnTick
```

Game booted to the main menu fine (`TAOM.Dependencies/diag.log` shows `OnGameInitializationFinished` at 09:48, 11 modules, launch marked good); the crash happened only on **campaign map load**. Debugger state at the fault: `width=1024, height=1024, indexData=byte[2097152]` (= 1024×1024×2) — a **correctly-sized buffer**, valid scene (`ContainsTerrain=true`, `RootEntityCount=2703`).

## Root cause (CONFIRMED by reversible test)

A loose `worldmap_battle_scene_grid` **texture** was imported into the external `TAOM_Map` module — creating `Assets/Battle Map/worldmap_battle_scene_grid/worldmap_battle_scene_grid_tex.tpac` + a `RuntimeDataCache/<guid>.rdc` (2026-05-31 16:46–16:47) — **without re-baking `SceneObj/Main_map`** (which stayed at its 2026-05-28 bake). The battle-scene grid is **baked into the `Main_map` scene** and read at runtime by the **native** `get_battle_scene_index_map`; the loose asset/cache desynced the native sampler from the baked index data → AV on the fill.

**Proof:** deleting the loose import artifacts restored loading on the *same* unchanged 05-28 scene. The map had loaded fine on this scene before the import (user-confirmed). The AV is on the native **read/source** side (the buffer was provably the right size).

## Why the grid behaves this way (evidence-graded)

| Claim | Confidence | Evidence |
|---|---|---|
| The grid is **baked into `Main_map`**, read natively, never a loose texture at runtime | **High** | `MBMapScene.GetBattleSceneIndexMap` takes **no** texture-name arg (only `GetColorGradeGridData(…, string textureName)` does) — user-pasted 1.4.5 `MBMapScene` source; string `worldmap_battle_scene_grid` absent from all managed code |
| Managed code **cannot** be the substitution vector | **High** | `MapScene.Load` → `Scene.Read("Main_map", moduleId, …)` → native `IScene.ReadInModule` (`Scene.cs:1069`); managed passes only name+moduleId, native resolves all resources |
| Vanilla ships **no** loose grid asset / `world_map` folder | **High** | `find` for `battle_scene_grid` in SandBox/NavalDLC → nothing |
| The loose asset **shadowed/desynced** the baked source by name/GUID/stale-offset | **Medium** | Inferred from the managed passthrough + the revert test; the exact native binding is inside `TaleWorlds.Native.dll` (C++) and not readable |
| R channel = scene index, G channel = entry position; 1024×1024 only | **Medium** | Single third-party source (BannerlordModding.LT wiki); the R=index part is consistent with the decompiled 2-byte texel format (`MapScene.cs:456-459`: byte0=sceneIndex, byte1=packed nibble normalized coords). `map_indices` side corroborated against installed v1.4.5 `sp_battle_scenes.xml` |

## Prevention

1. **NEVER import the grid as a loose module texture.** It must be **baked into `Main_map`** in the editor. Importing a loose `worldmap_battle_scene_grid` asset is the exact crash trigger. (Captured in [docs/reference/worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md) "DANGER" section + memory `feedback_battle_scene_grid_baked_not_runtime_swap`.)
2. **Importing the texture ≠ changing the grid.** Only a re-bake-and-save of `Main_map` (its `scene.xscene`/`terrain.bin` mtime must move) regenerates the index map. A loose import without a re-bake is an *inconsistent* state.
3. **Canonical layout:** no `worldmap_battle_scene_grid` under `Assets/` or `AssetSources/` of any module; `SceneObj/Main_map` carries the grid as binary payload. Matches vanilla SandBox/NavalDLC.
4. **Cleanup still outstanding** in `TAOM_Map` (harmless but non-canonical): empty `Assets/Battle Map/worldmap_battle_scene_grid/` + `AssetSources/Battle Map/worldmap_battle_scene_grid/`, and `AssetSources/Battle Map/worldmap_battle_scene_grid.zip` (91,706 B). Delete to restore the canonical layout.

## What this is NOT

- **Not a TAOM code bug.** No TAOM frames in the stack; `Patch0_BattleScenes` was disabled the whole time. TAOM's battle-scene code (commits `f817cec`, `7eea414`) always modeled the grid as a native read of the loaded scene and only loads `sp_battle_scenes.xml` (the data side) — it never handled a grid texture.
- **Not fixable by re-enabling Patch0.** Patch0's `MBMapScene_GetBattleSceneIndexMap_Patch` retry guard only helps a *transient* AV; this was deterministic (bad asset state), so the guard would retry 3× then run vanilla unguarded → crash anyway. Patch0 is a *separate*, non-crash concern (extended-index coverage).

## Open items (need in-editor confirmation — native/editor, not in readable sources)

- The exact editor action that re-bakes the battle index map into `Main_map` (import-alone vs an explicit bake/save step) is undocumented on any authoritative page found — this is the precise gap that caused the crash.
- The verbatim texture import settings (linear vs sRGB, compression, mips) are shown only in wiki screenshots; getting them wrong is plausibly load-bearing.

## Method note

Root cause was settled by a **reversible delete test** (remove the loose artifacts → load succeeds), not by static analysis — the decisive experiment. Mechanism depth came from a 5-agent research workflow whose load-bearing local claims were spot-verified (Scene.cs passthrough, `.zip` existence, vanilla layout, the no-texture-name API from the user's own paste); its single-sourced and native-only claims are graded Medium above per `.claude/rules/evidence-over-claims.md`.
