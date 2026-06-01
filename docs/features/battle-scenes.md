# Battle Scenes (DISABLED)

> **Status: DISABLED.** Code is in the tree, Harmony patches are written, but the `Patch0_BattleScenes` category is **not applied** at module load. The relevant line in [Main/SubModule.cs:115-116](../../Main/SubModule.cs) is commented out with a deliberate explanation:
>
> ```csharp
> // Battle scenes disabled — custom map not yet ready, will re-enable when TAOM_Map is integrated
> // _harmony.PatchCategory("Patch0_BattleScenes");
> ```
>
> **Do not investigate "why isn't this loading?" or "why doesn't sp_battle_scenes.xml take effect?"** — the answer is "the patch category is intentionally inactive." See [Re-enable](#re-enable) below.

## Overview

When enabled, this feature would intercept three Bannerlord scene-loading entry points to redirect them at TAOM-authored XML files (`sp_battle_scenes.xml`, `conversation_scenes.xml`, `meeting_scenes.xml`) and would harden battle-scene index map loading against an `AccessViolationException` race that has historically appeared on cold-cache map loads.

> For **how the battle-terrain index map / `worldmap_battle_scene_grid` texture actually works** (it's baked into `Main_map`, not loaded by filename) and the **LOTR grid re-author + bake workflow**, see [reference/worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md).

## Why This Exists

- **Vanilla behavior:** `Campaign.InitializeScenes` reads scene XMLs only from the `SandBox` module's `ModuleData/`. There is no extension point for a child mod to substitute or augment those files. `MBMapScene.GetBattleSceneIndexMap` reads the binary index map from disk, can throw `AccessViolationException` if the GPU/IO path is contended, and offers no retry.
- **TAOM requirement:** A custom Middle-earth campaign map (the `TAOM_Map` module) needs to provide its own `sp_battle_scenes.xml` (which scenes apply for which terrain types), `conversation_scenes.xml`, and `meeting_scenes.xml`. Cold-cache loads of the index map have produced bug reports that look like "stuck on loading screen / crash on first encounter."
- **Why this is parked:** `TAOM_Map` isn't yet integrated into the main game shipping path. Enabling `Patch0_BattleScenes` without it would attempt to load TAOM XMLs that don't exist (the `File.Exists` guard would skip them silently, but the `MBMapScene.GetBattleSceneIndexMap` retry would still fire on every battle), so the category is held off until the map ships.

## Architecture

### Design Challenge

Three distinct vanilla touch-points need patching, two of them are simple substitutions and one needs corruption-recovery semantics. Bannerlord's `MBMapScene.GetBattleSceneIndexMap` is a static method with `ref byte[]` / `ref int` outputs — it can only be patched as a Prefix that returns `false` to skip the original, then re-invokes the original itself inside a try/catch loop. Marking the prefix `[HandleProcessCorruptedStateExceptions]` + `[SecurityCritical]` is required for the catch to actually intercept `AccessViolationException` on .NET Framework 4.7.2.

### Solution Approach

All three patches share `[HarmonyPatchCategory("Patch0_BattleScenes")]` so the entire feature is gated on a single line in `SubModule.cs`.

| Patch | Target | Type | Behavior |
|---|---|---|---|
| `Campaign_InitializeScenes_Patch` | `Campaign.InitializeScenes` | Prefix → returns false | Loads TAOM's `sp_battle_scenes.xml` (from `TAOM` module) and Sandbox's `conversation_scenes.xml` / `meeting_scenes.xml` via `GameSceneDataManager.Instance.Load*Scenes`. Each load is gated on `File.Exists` so missing files are non-fatal. |
| `MapScene_Load_DiagnosticPatch` | `SandBox.MapScene.Load` | Prefix → void | Diagnostic only — walks `ModuleHelper.GetActiveModules()` looking for an active module with `SceneObj/Main_map/scene.xscene` and prints which one wins ("last wins" semantics for Bannerlord scene resolution). |
| `MBMapScene_GetBattleSceneIndexMap_Patch` | `MBMapScene.GetBattleSceneIndexMap` | Prefix → returns false | Wraps a 3× retry loop with `Thread.Sleep(250)` between attempts around the original call. Uses a `_isRetrying` static flag to avoid recursing when the prefix re-invokes the target. On all-attempts-failed, returns `true` so the original runs unguarded one final time. |

### Component Diagram

```
SubModule.OnSubModuleLoad        (Main/SubModule.cs:115-116, COMMENTED OUT)
        |
_harmony.PatchCategory("Patch0_BattleScenes")     ← gate
        |
   +----+----+--------------+
   |         |              |
   v         v              v
Campaign.InitializeScenes   SandBox.MapScene.Load    MBMapScene.GetBattleSceneIndexMap
        |                        |                        |
   loads TAOM XMLs           prints active           retries 3× on AccessViolation
   via GameSceneDataManager   map module             with 250ms backoff
```

## Configuration

When eventually re-enabled, depends on these XML files existing in module data:

| File | Module | Purpose |
|---|---|---|
| `Main/_Module/ModuleData/sp_battle_scenes.xml` | TAOM | Single-player battle scene table — terrain → scene mapping |
| `Modules/SandBox/ModuleData/conversation_scenes.xml` | SandBox (vanilla, but reloaded) | Conversation scene table |
| `Modules/SandBox/ModuleData/meeting_scenes.xml` | SandBox (vanilla, but reloaded) | Meeting scene table |
| `Modules/<map>/SceneObj/Main_map/scene.xscene` | The map module (`TAOM_Map`) | The 3D campaign map scene |

None of these are currently authored / wired up — that's the work item gating re-enable.

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/BattleScenes/Hooks/Campaign_InitializeScenes_Patch.cs](../../Main/Features/BattleScenes/Hooks/Campaign_InitializeScenes_Patch.cs) | Replaces vanilla scene loading |
| [Main/Features/BattleScenes/Hooks/MapScene_Load_DiagnosticPatch.cs](../../Main/Features/BattleScenes/Hooks/MapScene_Load_DiagnosticPatch.cs) | Diagnostic — logs which map module wins |
| [Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs](../../Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs) | AccessViolationException retry guard |
| [Main/SubModule.cs:115-116](../../Main/SubModule.cs) | The commented-out `PatchCategory` call — the gate |

No service, no IoC registration, no adapters — patches are the entire feature.

## Dependencies

- `TaleWorlds.CampaignSystem.Campaign` (target)
- `TaleWorlds.CampaignSystem.GameSceneDataManager` (vanilla loader API)
- `TaleWorlds.MountAndBlade.MBMapScene` (target + re-invoked from inside the prefix)
- `TaleWorlds.ModuleManager.ModuleHelper` (resolves module folder paths)
- `TaleWorlds.Engine.Scene` (parameter type for `GetBattleSceneIndexMap`)

## Tests

None. There are no tests in `TAOM.Tests/Features/BattleScenes/`. The patches are static-only, IO-bound, and need a live game to exercise — they were always intended to be verified manually after `TAOM_Map` ships.

## Re-enable

When `TAOM_Map` is integrated and the three XML files (or at minimum `sp_battle_scenes.xml`) are in place:

1. In [Main/SubModule.cs:116](../../Main/SubModule.cs), uncomment `_harmony.PatchCategory("Patch0_BattleScenes");`.
2. Verify `Main/_Module/ModuleData/sp_battle_scenes.xml` exists and has at least one `<sp_battle_scenes>` entry.
3. Confirm a map module (likely `TAOM_Map`) is in the active load order with a `SceneObj/Main_map/scene.xscene` file.
4. Boot the game, watch the rgl_log for the diagnostic Prefix's output ("`TAOM:   Module 'X' has Main_map scene at ...`" + "`TAOM: >>> Selected map module: 'X'`"). The selected module should be `TAOM_Map`.
5. Travel into a battle and verify the new scene loads. The retry guard in `MBMapScene_GetBattleSceneIndexMap_Patch` will print yellow warnings if `AccessViolationException` is hit during retry, green confirmation on success.
6. **Add a test or at least a manual checklist to this doc** before considering it shipping.

## How to Diagnose "wrong map module wins"

The `MapScene_Load_DiagnosticPatch` prints to the engine log every time `MapScene.Load` runs. Bannerlord's "last active module with a `Main_map` scene wins" rule means load order matters. If the diagnostic shows `SandBox` as the selected module instead of `TAOM_Map`, the load-order setting (`Modules/Native/SubModule.xml`-style ordering, or the Launcher) is putting TAOM_Map ahead of SandBox; reverse it.

## GitHub Issue

- **Issue:** None.
- **Status:** Parked pending TAOM_Map integration.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md)

<!-- backlinks-end -->
