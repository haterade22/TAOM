# Shader Pre-compilation

> **2026-06-17 re-enable + scene-walk (issue #287).** Re-enabled (was disabled 2026-05-22) and rewritten to walk the all-characters battle **then each TAOM battle scene**, so terrain + forced-atmosphere shaders compile too — not just character shaders. This targets the intermittent battle-load `d3dcompiler` CTD/hang: TAOM_Map battle scenes ship no `compressed_shader_cache.sack`, so their terrain/atmosphere shaders runtime-compile on entry. See "Scene-walk architecture" below. **Update 2026-06-19:** the open-field battle `_forceatmo` scenes were since **disabled** (Rohan `ee2cb04b`, Mordor `62470413`) — their `pbr_terrain` vista permutation hard-crashes some GPUs on scene load; the `Patch16_AtmospherePersistence` patch was audited and **exonerated** as the cause (see [atmosphere-persistence.md](atmosphere-persistence.md)), leaving the terrain shader the live but unproven culprit pending native triage. **In-game-only (ADR-008) — pending a 1-2 hr precompile test.**

## Overview

Adds a "Pre-compile Shaders" main-menu option that walks a sequence of hidden custom battles so the Bannerlord engine compiles every shader it would otherwise compile mid-battle: first an all-characters battle (character/equipment shaders), then one pass per TAOM battle scene (that scene's terrain + forced-atmosphere shaders). Eliminates first-encounter stutter AND the runtime-compile crash/hang on battle entry. Progress shows on the loading screen + a 1 Hz status toast.

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

### Scene-walk architecture (2026-06-17)

The single-battle flow above is now item 0 of a **work list**. The whole walk:

```
SubModule menu action  ──▶  ShaderPrecompileRunner.Begin()
    plan = ShaderPrecompilePlanner.BuildPlan( PrecompileSceneProvider.GetScenes() )
         = [ CharacterBattle(battle_terrain_029) ] + [ ScenePass(scene) for each TAOM battle scene ]

SubModule.OnApplicationTick ──▶ runner.Tick()   (every frame while the walk is active)
    StartCurrentItem → MBGameManager.StartNewGame(new TaomShaderGameManager(item))
        TaomShaderGameManager.OnLoadFinished → CustomBattleHelper.StartGame(item data)
                                             → ShaderPrecompileRunner.NotifyItemRendering()  [Running]
    TickRunning → ShaderPrecompileDecider.Decide(remaining, itemElapsed, now, isLoading)
        Wait / AdvanceItem / AbortItem
    BeginEnd → MBGameManager.EndGame()  [Ending]
    TickEnding → back at menu? → next item or Finish()
```

- **`ShaderPrecompileDecider`** (pure, unit-tested) owns per-item compile detection. Completion (count back to 0) requires `_observedWork` first (the 2026-05-04 initial-zero latch fix, generalized). The "nothing to compile, advance" grace counts **render** time (from the first non-loading frame), not load time, so a heavy scene still loading is never skipped. Backstops: a 15-min no-progress (count frozen) abort and a 90-min absolute per-item cap.
- **`ShaderPrecompileRunner`** (engine boundary) owns the outer state machine (Idle→Starting→Running→Ending→Complete) and chains the per-item custom battles. Every state has a timeout escape. `Game.Current==null` is the post-`EndGame` teardown signal, with a 90-s last-resort backstop; `TickEnding` logs the live state at 1 Hz to confirm which path fires. Because each item is a fresh `MBGameManager.StartNewGame`, the walk re-enters `SubModule.OnGameInitializationFinished` once per item — which surfaced a latent per-game re-patch crash (issue #288: patch application was unguarded, so the 2nd game re-applied a non-idempotent transpiler and threw). Now guarded once-per-process; see `docs/reviews/rca-repatch-crash-2026-06-18.md`.
- **`TaomShaderGameManager`** (`CustomGameManager` subclass) builds the per-item `CustomBattleData`: `CharacterBattle` = all troops; `ScenePass` = a handful of troops on the item's real scene.
- **`PrecompileSceneProvider`** reads the scene list (below) and falls back to a baked default. Of the **21 registered** TAOM `_forceatmo` scenes (8 open-field battle + 9 custom siege + 4 custom village), the live `precompile_scenes.txt` now walks **12** (8 siege + 4 village): the 8 open-field battle scenes (6 Mordor `62470413`, 2 Rohan `ee2cb04b`) and the Helm's Deep siege are **disabled** because their `pbr_terrain` vista permutation hard-crashes some GPUs on scene load — re-enable once the native shader-compile-guard hook lands (#287). **Fallback synced (2026-06-25):** the baked `DefaultScenes` now mirrors the live `precompile_scenes.txt` exactly (12 active siege + village; every open-field battle scene commented out), so deleting/emptying the config no longer resurrects the crashing Mordor scenes. Pinned by `PrecompileSceneProviderParseTests.DefaultScenes_ExcludesDisabledCrashScenes`.

### 1.4.7 headless-deployment guard (`ShaderPrecompilePlayerAgentGuard`, #336)

The 1.4.6→1.4.7 engine bump broke the walk. On 1.4.7 the precompile hung indefinitely (users: "stuck for a long time"; worked on 1.4.6). **Two symptoms, one root cause:**

1. **NRE crash/wedge.** 1.4.7 added an **unconditional** deref of `Mission.InitialPlayerAgent` to `DeploymentMissionController.SetupTeams()` (`:174`) and `FinishDeployment()` (`:72`) — the new `AgentControllerType` hand-control lines. `Mission._initialPlayerAgent` is assigned **only** when an agent builds with `Controller == AgentControllerType.Player` (`Mission.cs:4024`). The precompile custom battle is **headless** — thousands of troops, no human — so nothing is player-controlled, the field stays `null`, and `SetupTeams()` NREs on every mission tick (`TeamSetupOver` never sets → it re-throws forever). 1.4.6 had no such deref. Managed shader APIs are byte-identical 1.4.6↔1.4.7, so this is purely the deployment path.
2. **Deployment-view hang.** With the NRE guarded, the all-characters battle (enough player troops that `CanPlayerSideDeployWithOrderOfBattle()` is true) opens the Order-of-Battle **deployment view** and waits for the player to click *Deploy*. Headless, nobody clicks → the game freezes at the deployment screen (a true freeze — the app tick stops, so even the cancel hotkey can't recover it).

**Fix** — `ShaderPrecompilePlayerAgentGuard : MissionLogic`, added **only while a walk is in flight** from `SubModule.OnMissionBehaviorInitialize` (gated on `ShaderPrecompileRunner.IsWalkInProgress`), so a **normal battle never gets it**. Two jobs:
- **Seed `InitialPlayerAgent`** on the first agent build (drift-guarded reflection write of the private `_initialPlayerAgent`). The first agent build happens inside `SetupTeams` (via `OnSetupTeamsOfSide → SetSpawnTroops(enforceSpawning:true) → CheckDeployment`) **before** the deref — an ordering the deep-review verified is *causally* guaranteed by the engine (`TeamSetupOver` only flips true after `SetupTeams` returns). So the deref finds a non-null agent and harmlessly reconfigures it in the throwaway battle.
- **Force-finish deployment** once `SetupTeams` has run (`deployment.TeamSetupOver`) by calling the controller's public `FinishDeployment()` — the headless battle skips the OoB wait, the same auto-skip a <20-troop scene pass already gets. A scene pass whose deployment already auto-finished leaves no controller, so the force-finish is a no-op there.

**Scope:** the precompile is TAOM's *only* headless battle path (`MBGameManager.StartNewGame` / `CustomBattleHelper.StartGame`). Every real battle (campaign/siege/tournament/custom-from-menu) spawns a Player-controlled agent → non-null `InitialPlayerAgent` → never hits the deref. So the regression is scoped to precompile, and the fix is scoped to the shader mission.

**Wiring gotcha (in-game-caught):** the guard was first added from `TaomShaderGameManager.OnLoadFinished` via `Mission.Current?.AddMissionBehavior` — that **silently no-op'd** because `Mission.Current` isn't the battle mission yet at `OnLoadFinished`. It never registered (absent from the mission behavior dump) and the NRE still fired. Adding mission behaviors to a freshly-opened mission must go through `OnMissionBehaviorInitialize` (the engine hands the mission in directly). Binding drift on `_initialPlayerAgent` is pinned by `ReflectionSiteBindingTests`. See `docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md`.

## Configuration

**MCM toggles** (group "Graphics/Shader Precompilation", GroupOrder 15): **Enable Shader Precompilation** (master; default on) live-hides the main-menu option via its `isHidden` callback when off — no relaunch. **Include Scene Passes** (default on) gates the risky terrain/atmosphere scene passes; off runs only the all-characters pass (compiles every troop/equipment shader, never crashes), read in `ShaderPrecompileRunner.Begin()`. The off-path is the immediate escape hatch for a user whose GPU crashes on the scene loads, while the native shader-compile guard (#287) is built.

**Scene list:** `Main/_Module/ModuleData/shader_precompilation/precompile_scenes.txt` — one scene id per line, `#` comments, blank lines ignored. Read directly by `PrecompileSceneProvider` (no SubModule.xml registration; it is not engine-loaded XML). If missing/empty, the baked `DefaultScenes` fallback is used — as of 2026-06-25 it mirrors the live txt exactly (the 6 Mordor open-field scenes were commented out to match), so the fallback no longer re-walks disabled crashers. All are header-only on disk (no `compressed_shader_cache.sack`), so all runtime-compile; reused-vanilla siege/village scenes ship their own `.sack` and need nothing. Sieges currently ride the `"Battle"` ScenePass (terrain + atmosphere + static walls); a `"Siege"`-mission builder is the escalation if siege-engine materials stay cold (probed in-game). Add `battle_terrain_*` ids to also cover vanilla terrains (each adds ~5-15 min to the walk).

**Crash skip list:** `Logs/shader-precompile-crashed-scenes.txt` — auto-managed by `ShaderPrecompileCrashGuard`. If a scene hard-crashes the process during load (a GPU/driver-specific native AV — e.g. `fords_of_isen` on the `pbr_terrain` input-layout-9 compile), the runner records it here (via a surviving `shader-precompile-inflight.marker`) and drops it from subsequent walks so the walk can complete. **Delete this file to retry the skipped scenes.** Only true process crashes are recorded — a slow item, a per-item timeout, or a clean exit never lands here.

Tunable constants live in `ShaderPrecompileDecider.cs` (grace/settle/no-progress/per-item-timeout), `ShaderPrecompileRunner.cs` (start/end timeouts), and `TaomShaderGameManager.cs`:

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
| `Main/Features/ShaderPrecompilation/ShaderPrecompilationIoC.cs` | DryIoc singleton registration (+ `IPrecompileSceneProvider`, `ShaderPrecompileRunner`) + hook init |
| `Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs` | **Orchestrator** (engine boundary): outer state machine, chains per-item custom battles, drives the decider, owns the status line. Per-item-kind decider caps (scene passes 8 min vs character battle 90 min), self-classifying abort logs, `Cancel()` + **Ctrl+Shift+K** cancel hotkey, `IsWalkInProgress` (gates the 1.4.7 guard) |
| `Main/Features/ShaderPrecompilation/ShaderPrecompileDecider.cs` | **Pure** per-item compile-detection state machine (observed-work latch, render-grace, settle, no-progress + absolute timeouts, **churn backstop** = abort a count that changes forever but never settles, `LastAbortReason`) |
| `Main/Features/ShaderPrecompilation/ShaderPrecompilePlayerAgentGuard.cs` | **1.4.7 headless-deployment guard** (`MissionLogic`, #336): seeds `Mission.InitialPlayerAgent` + force-finishes deployment in the shader mission only. See the "1.4.7 headless-deployment guard" section above |
| `Main/Features/ShaderPrecompilation/ShaderPrecompilePlanner.cs` | **Pure** work-list builder: character battle + one ScenePass per scene |
| `Main/Features/ShaderPrecompilation/{IPrecompileSceneProvider,PrecompileSceneProvider}.cs` | Scene list from `precompile_scenes.txt` (baked-default fallback) |
| `Main/Features/ShaderPrecompilation/Domain/PrecompileItem.cs` | `PrecompileItem` + `PrecompileItemKind {CharacterBattle, ScenePass}` |
| `Main/_Module/ModuleData/shader_precompilation/precompile_scenes.txt` | Editable scene list — 12 active (8 siege + 4 village); the 8 open-field battle scenes + Helm's Deep are disabled (Mordor + Rohan `pbr_terrain` vista crash, #287). 21 registered total. Baked `DefaultScenes` fallback now mirrors this exactly (synced 2026-06-25). |
| `Main/Features/ShaderPrecompilation/TaomShaderGameManager.cs` | Extends `CustomGameManager`; builds per-item `CustomBattleData` (CharacterBattle = all troops; ScenePass = minimal troops on the item's scene) |
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

The pure core is unit-tested (the runner / game manager / patch are engine boundaries, ADR-008, game-only):

- `ShaderPrecompileDeciderTests` — the observation state machine: first-frame-zero (RCA regression), render-grace vs load-grace (the 2026-06-17 premature-advance fix), settle, idle-dip, no-progress-stuck, absolute timeout, work-observed-during-loading.
- `ShaderPrecompilePlannerTests` + `PrecompileSceneProviderParseTests` — work-list order (character battle first), scene dedup, `ParseSceneList` comments/blanks/trim, and the fallback-drift invariant (`DefaultScenes_ExcludesDisabledCrashScenes` pins the 9 disabled crashers absent + `DefaultScenes_IncludesActiveSiegeScene` pins a representative active scene present).

- `ShaderPrecompilationServiceTests` — 7 tests covering:
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

## Changelog

- 2026-07-11 — **1.4.7 deployment-NRE fix (#336).** Root-caused the "precompile stuck on 1.4.7" reports to a 1.4.7 engine regression: `DeploymentMissionController.SetupTeams()`/`FinishDeployment()` now unconditionally deref `Mission.InitialPlayerAgent`, which is null in the headless precompile battle → NRE every mission tick + (once guarded) a freeze at the OoB deployment view. Added `ShaderPrecompilePlayerAgentGuard` (seeds `InitialPlayerAgent` + force-finishes deployment, scoped to the walk via `IsWalkInProgress`). Robustness package alongside: per-item-kind decider caps (scene passes bail at 8 min, not 90), a churn backstop, self-classifying abort logs, and a Ctrl+Shift+K cancel. In-game 1.4.7: full walk completes (13 items, 0 NRE, 0 hang). RCA `docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md`.
- 2026-06-25 — Phase 0 of the native shader-compile guard (#287): fixed the `DefaultScenes` fallback drift (now mirrors the live `precompile_scenes.txt`; no missing-config resurrection of disabled crashers); added MCM "Graphics/Shader Precompilation" toggles (master + Include Scene Passes — off runs only the safe all-characters pass); added post-crash in-game + log guidance for exporting the Windows Event Log fault offset the native guard needs. Root cause confirmed as `normalize()`-of-zero in `pbr_terrain` (`terrain_pixel_functions.rsh:818`) but the shader source is engine-global (unshippable as a module override).
- 2026-06-18 — Added a per-scene crash guard (`ShaderPrecompileCrashGuard`) that records hard-crashing scenes to a skip list and drops them from the plan so the walk can finish.
- 2026-06-18 — Suppressed the battle-load stall watchdog during the walk (longest legitimate load).
- 2026-06-18 — Extended the walk to custom siege + village scenes; `precompile_scenes.txt`/`DefaultScenes` grew from 8 to 21 scenes (#287).
- 2026-06-17 — Re-enabled the "Pre-compile Shaders" menu option and rewrote it to scene-walk each TAOM battle scene so terrain/atmosphere shaders compile, targeting the battle-load d3dcompiler CTD (#287).
- 2026-05-22 — Hid the Pre-compile Shaders main-menu option (commented the `InitialStateOption`) while the feature was unreliable; rest of the wiring kept active.
- 2026-05-04 — Added visible per-second progress UI and fixed the initial-zero latch race (#106 follow-up).
- 2026-05-04 — Eliminated the silent character drop and relaxed the premature stuck-abort (#106, follow-up to #57).
- 2026-04-06 — Reset the abort latch on completion.
- 2026-04-02 — Added stuck-shader auto-abort with a countdown UI (#57).
- 2026-04-02 — Added the "Pre-compile Shaders" main-menu option launching a hidden all-characters custom battle (#57).

## GitHub Issues

- [#57 — feat: Shader Pre-compilation at Main Menu](https://github.com/haterade22/TAOM/issues/57) — original feature, OPEN
- [#106 — fix: silent character drop + premature 120s abort + stale latch on retry/abort](https://github.com/haterade22/TAOM/issues/106) — 2026-05-04 stability fix, OPEN until in-game verification
- [#287 — Battle-load CTD/hang: scenes lack precompiled shader caches](https://github.com/haterade22/TAOM/issues/287) — 2026-06-17 re-enable + scene-walk, OPEN until in-game verification
- [#336 — crash/hang: shader precompile stuck on 1.4.7 (DeploymentMissionController.SetupTeams NRE on headless battle)](https://github.com/haterade22/TAOM/issues/336) — 2026-07-11, in-game confirmed (13/13 items, 0 NRE, 0 hang); OPEN pending optional cold-cache validation of the force-finish path

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/atmosphere-persistence.md](./atmosphere-persistence.md)
- [docs/features/battle-load-diagnostics.md](./battle-load-diagnostics.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
