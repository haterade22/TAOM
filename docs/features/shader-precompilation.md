# Shader Pre-compilation

## Overview

Adds a "Pre-compile Shaders" button to the main menu that runs a hidden custom battle containing all TAOM characters. The Bannerlord engine compiles shaders for every unique model/material it renders, eliminating mid-game stutter caused by first-encounter shader compilation. The loading screen shows live progress during compilation.

## Why This Exists

- **Vanilla behavior:** Bannerlord compiles shaders on-demand — the first time a mesh/material combination is rendered, the engine compiles the shader synchronously, causing a visible frame spike.
- **TAOM requirement:** With 13 custom cultures and hundreds of unique armor sets from `LOTRLOME_Armory`, first-encounter stutter is frequent. Players fighting Gondor troops for the first time, entering a new tournament, or encountering a new faction all trigger shader compilation mid-combat.
- **Without this feature:** Players experience frame drops ranging from 100–2000ms whenever the renderer first encounters a TAOM-specific material. This is especially severe on first install when the shader cache is cold.

The feature is manual (not automatic) because first-time compilation can take 20–70 minutes depending on hardware and installed cultures. Users run it once after installation, then never again unless they clear the shader cache.

## Architecture

### Design Challenge

The Bannerlord shader compiler runs as part of the rendering pipeline — there is no API to pre-compile shaders directly. The only way to force compilation is to render the meshes. This requires loading a game state (a mission/battle), not just the main menu, because the render pipeline is not active at the menu.

Additionally, the loading screen's progress text is controlled by `LoadingWindowViewModel.Update()` which is `internal` — it cannot be called or subclassed from a mod. Harmony patching via `AccessTools` is required to inject text into it.

### Solution Approach

1. Extend `CustomGameManager` (the same base class Bannerlord's custom battle uses) so the engine loads all necessary module data.
2. Override `OnLoadFinished()` to call `CustomBattleHelper.StartGame()` with a `CustomBattleData` that has all TAOM characters split across both sides.
3. The engine renders all characters and their equipment, forcing shader compilation for every unique material.
4. A Harmony postfix on `LoadingWindowViewModel.Update()` reads `Utilities.GetNumberOfShaderCompilationsInProgress()` and writes the count to `DescriptionText` — but only when the count changes (avoiding per-frame string allocation).
5. The menu button is registered via `Module.CurrentModule.AddInitialStateOption()` from `OnBeforeInitialModuleScreenSetAsRoot()`, which fires exactly before the main menu is displayed.

### Component Diagram

```
SubModule.OnBeforeInitialModuleScreenSetAsRoot()
    └── Module.CurrentModule.AddInitialStateOption("Pre-compile Shaders", orderIndex=100)
            └── Action: MBGameManager.StartNewGame(new TaomShaderGameManager(service, logger))

TaomShaderGameManager : CustomGameManager
    └── OnLoadFinished()
            └── base.OnLoadFinished()            ← sets IsLoaded=true, pushes CustomBattleState
            └── CustomBattleHelper.StartGame(BuildBattleData())
                    └── IShaderPrecompilationService.GetCharacterIdsForShaderBattle()
                    └── IShaderPrecompilationService.GetCultureIdsForShaderBattle()
                    └── MBObjectManager.Instance.GetObject<>() per character ID
                    └── CustomBattleCombatant × 2 (≤3000 troops each side)

Patch21_ShaderPrecompilation:
    └── LoadingScreen_ShaderProgress_Patch
            └── AccessTools.Method(typeof(LoadingWindowViewModel), "Update")
            └── Postfix: Utilities.GetNumberOfShaderCompilationsInProgress()
                    → updates DescriptionText only when count changes
```

## Configuration

No configuration file. The feature has tunable constants in `TaomShaderGameManager.cs` and `Hooks/LoadingScreen_ShaderProgress_Patch.cs`:

| Constant | Value | Description |
|----------|-------|-------------|
| `MaxTroopsPerSide` | `3000` | Cap on troop slots per side. 6000 total slots, sized to fit ~1600 TAOM characters + vanilla characters with no silent drops. |
| `SoldierCopies` | `2` | How many instances of each soldier-occupation troop are spawned. Each copy lets Bannerlord pick a random `BattleEquipments` variant, so 2 gives reasonable statistical variant coverage without exploding slot use. |
| `HeroCopies` | `1` | Heroes have one equipment loadout — single render covers their shaders. |
| `BattleScene` | `"battle_terrain_029"` | `CustomBattleData.CoreContentDefaultSceneName` — the default custom battle scene, always present. |
| `StuckWarnSeconds` (patch) | `300` | Show a "stuck Ns" warning after 5 min of no count change, but only when in the tail (`remaining <= 5`). |
| `StuckAbortSeconds` (patch) | `600` | Auto-abort via `MBGameManager.EndGame()` after 10 min of tail-end stall. Large-count pauses are not treated as stuck — Bannerlord's shader compiler is single-threaded and a single heavy material can legitimately hold for several minutes. |
| `StuckTailRemainingMax` (patch) | `5` | Stuck-detection only fires when `remaining <= 5`; higher counts can pause without aborting. |

### Why the constants were tuned (2026-05-04)

The original values (`MaxTroopsPerSide=2000`, `SoldierCopies=4`, `StuckAbortSeconds=120`) silently dropped roughly 1,000–1,400 characters when the slot budget filled before all characters were added — users ran the 20–70 minute process, saw the loading screen finish, and still hit mid-game stutter on the dropped characters. They reported "Pre-compile Shaders doesn't work." The old 120 s abort also fired prematurely on slower hardware, terminating compilation a few shaders short of completion. The current values close both gaps.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ShaderPrecompilation/IShaderPrecompilationService.cs` | Service interface |
| `Main/Features/ShaderPrecompilation/ShaderPrecompilationService.cs` | Queries `IObjectManagerAdapter` for all cultures (bandits included — they have unique meshes/equipment that need shader coverage too), deduplicates character IDs, caches culture set |
| `Main/Features/ShaderPrecompilation/ShaderPrecompilationIoC.cs` | DryIoc singleton registration + hook init |
| `Main/Features/ShaderPrecompilation/TaomShaderGameManager.cs` | Extends `CustomGameManager`, builds `CustomBattleData` from service output |
| `Main/Features/ShaderPrecompilation/Hooks/LoadingScreen_ShaderProgress_Patch.cs` | `Patch21_ShaderPrecompilation` — loading screen progress text |
| `Main/SubModule.cs` | Applies `Patch21_ShaderPrecompilation`, calls `InitializeHooks`, registers menu button in `OnBeforeInitialModuleScreenSetAsRoot()` |
| `Main/IoC.cs` | `ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container)` |

## Dependencies

- `IObjectManagerAdapter` (Adapters) — provides `GetAllCharacterInfos()` and `GetAllCultureInfos()`
- `IModLogger` (Core/Logging) — log info/error during battle setup
- `CustomGameManager` (`TaleWorlds.MountAndBlade.CustomBattle.dll`) — base class that loads CustomBattle module data
- `CustomBattleHelper` (`TaleWorlds.MountAndBlade.CustomBattle.dll`) — `StartGame(CustomBattleData)` to open the mission
- `TaleWorlds.Engine.Utilities.GetNumberOfShaderCompilationsInProgress()` — live shader count from engine

## Tests

- `TAOM.Tests/Features/ShaderPrecompilation/ShaderPrecompilationServiceTests.cs` — 7 tests covering:
  - Happy path: returns character IDs from all included cultures
  - Bandit culture **inclusion** (bandits have unique meshes/equipment that need shader coverage too)
  - `GetCharacterIdsForShaderBattle` adapter exception → empty result + logged error
  - Deduplication of character IDs
  - Null/empty ID exclusion
  - Mixed bandit + non-bandit culture handling
  - `GetCultureIdsForShaderBattle` adapter exception → empty result + logged error

`TaomShaderGameManager` and `LoadingScreen_ShaderProgress_Patch` are not unit-tested — they are entry points that directly call TaleWorlds APIs (no logic to test).

## How to Add Coverage for a New Culture

When a new TAOM culture is added, its characters are automatically included — no changes needed here. The service queries `IObjectManagerAdapter.GetAllCultureInfos()` and `GetAllCharacterInfos()` at runtime, picking up every loaded culture (vanilla, TAOM custom, and bandit) and all of its characters.

If a culture's characters are not getting compiled, verify:
1. The culture's character XML files are loaded and the characters have a valid `culture` attribute matching the culture ID
2. The `IObjectManagerAdapter` implementation's `GetAllCharacterInfos()` returns them (check `ObjectManagerAdapter.cs`)
3. The slot budget hasn't filled — the manager logs `[ShaderPrecompilation] N characters skipped` to `rgl_log` if the cap is hit. If you see that line with a non-zero count, raise `MaxTroopsPerSide` or lower `SoldierCopies` in `TaomShaderGameManager.cs`.

## Performance

- **LoadingScreen patch:** Runs every frame during loading screens. Calls `Utilities.GetNumberOfShaderCompilationsInProgress()` (a native engine call) then early-exits if the count hasn't changed. String allocation (`$"Compiling shaders... {n} remaining"`) only occurs when the count changes — typically once per second during active compilation.
- **Service:** `GetValidCultureIds()` builds the culture `HashSet` once and caches it for the service's lifetime. `GetAllCharacterInfos()` is only called once per shader battle initiation.

## GitHub Issues

- [#57 — feat: Shader Pre-compilation at Main Menu](https://github.com/haterade22/TAOM/issues/57) — original feature, OPEN
- [#106 — fix: silent character drop + premature 120s abort + stale latch on retry/abort](https://github.com/haterade22/TAOM/issues/106) — 2026-05-04 stability fix, OPEN until in-game verification

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
