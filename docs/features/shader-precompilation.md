# Shader Pre-compilation

> **STATUS: PARKED again 2026-09-25** (more problems than it is worth for now; active 2026-09-11 to 2026-09-25, #560; history in the Changelog). The main-menu option, the `Patch21_ShaderPrecompilation` registration and both MCM attribute stacks are commented out, the same wiring-level park as 2026-08-20; code, tests, strings, both properties and `precompile_scenes.txt` are kept, so re-enabling is three uncomments in `SubModule.cs` and `TaomSettings.cs`. The sections below describe the feature as written, for re-enabling; none of it runs while parked. While parked, MCM neither loads nor saves the two settings: the first Mod Options save that changes any TAOM setting drops their stored values, after which un-parking starts both at compiled defaults.

> **2026-06-17 re-enable + scene-walk (issue #287).** Re-enabled (was disabled 2026-05-22) and rewritten to walk the all-characters battle **then each TAOM battle scene**, so terrain + forced-atmosphere shaders compile too, not just character shaders. This targets the intermittent battle-load `d3dcompiler` CTD/hang: TAOM_Map battle scenes ship no `compressed_shader_cache.sack`, so their terrain/atmosphere shaders runtime-compile on entry. See "Scene-walk architecture" below. **Update 2026-06-19:** the open-field battle `_forceatmo` scenes were since **disabled** (Rohan `ee2cb04b`, Mordor `62470413`), their `pbr_terrain` vista permutation hard-crashes some GPUs on scene load; the `Patch16_AtmospherePersistence` patch was audited and **exonerated** as the cause (see [atmosphere-persistence.md](atmosphere-persistence.md)), leaving the terrain shader the live but unproven culprit pending native triage.

## Overview

Adds a "Pre-compile Shaders" main-menu option that walks a sequence of hidden custom battles so the Bannerlord engine compiles every shader it would otherwise compile mid-battle: first the character batches (every loaded troop, lord and piece of equipment from TAOM and the base game, 1,000 characters per battle), then, only when the MCM opt-in is on, one pass per TAOM battle scene (that scene's terrain + forced-atmosphere shaders). Eliminates first-encounter stutter and the render-ready stall on battle entry. Progress shows on the loading screen + a 1 Hz status toast; Ctrl+Shift+K cancels.

## Why This Exists

- **Vanilla behavior:** Bannerlord compiles shaders on-demand, the first time a mesh/material combination is rendered, the engine compiles the shader synchronously, causing a visible frame spike. On a battle load the mission is held one frame short of playable until the queue drains (`MissionState.OnTick` gates `TickMission` on `SceneView.ReadyToRender()`), which is the "stall" b18f3441 reported.
- **TAOM requirement:** With 13 custom cultures and hundreds of unique armor sets from `LOTRLOME_Armory`, first-encounter stutter is frequent. Players fighting Gondor troops for the first time, entering a new tournament, or encountering a new faction all trigger shader compilation mid-combat.
- **Without this feature:** Players experience frame drops ranging from 100-2000ms whenever the renderer first encounters a TAOM-specific material, and multi-minute battle loads on a cold cache.

The feature is manual (not automatic) because a cold walk takes 20 to 70 minutes depending on hardware. Since 1.4.8 the engine deletes the local shader cache after any module-list change, so the walk is repayable, not one-time: players re-run it after adding, removing or reordering mods.

## Architecture

### Design Challenge

The Bannerlord shader compiler runs as part of the rendering pipeline, there is no API to pre-compile shaders directly. The only way to force compilation is to load the meshes into a mission. This requires loading a game state (a mission/battle), not just the main menu, because the render pipeline is not active at the menu.

Additionally, the loading screen's progress text is controlled by `LoadingWindowViewModel.Update()` which is `internal`, it cannot be called or subclassed from a mod. Harmony patching via `AccessTools` is required to inject text into it.

### Solution Approach

1. Extend `CustomGameManager` (the same base class Bannerlord's custom battle uses) so the engine loads all necessary module data.
2. Override `OnLoadFinished()` to call `CustomBattleHelper.StartGame()` with a `CustomBattleData` whose player party is one character and whose enemy party is one batch of the roster.
3. The engine's `MissionCustomBattlePreloadView` hands every character of both combatants to `PreloadHelper.PreloadCharacters`, which walks every battle equipment set, registers every item mesh, and calls `MetaMesh.PreloadShaders` per unique mesh before the first frame. Coverage is therefore decided by **roster membership**, not by how many agents the Battle Size option lets spawn.
4. A Harmony postfix on `LoadingWindowViewModel.Update()` mirrors the runner's status line into `DescriptionText` while a walk is active.
5. The menu button is registered via `Module.CurrentModule.AddInitialStateOption()` from `OnBeforeInitialModuleScreenSetAsRoot()`, which fires exactly before the main menu is displayed.

### Component Diagram

```
SubModule.OnBeforeInitialModuleScreenSetAsRoot()
    └── Module.CurrentModule.AddInitialStateOption("Pre-compile Shaders", orderIndex=100)
            └── Action: inquiry (Start/Cancel) -> ShaderPrecompileRunner.Begin()

ShaderPrecompileRunner.Begin()           (main menu: no MBObjectManager yet)
    └── plan = ShaderPrecompilePlanner.BuildBootstrapPlan(scenes)
            = [ bootstrap CharacterBattle (no ids) ] + [ ScenePass(scene) ... ] (scenes only if the MCM opt-in is on)
    └── MBGameManager.StartNewGame(new TaomShaderGameManager(item 0))

TaomShaderGameManager.OnLoadFinished()   (inside the first custom game: the roster is readable)
    └── roster = IShaderPrecompilationService.GetCharacterIdsForShaderBattle()
    └── ShaderPrecompileRunner.NotifyRosterDiscovered(gen, roster)
            └── plan = ShaderPrecompilePlanner.BuildPlan(roster, scenes)
                = [ Troops batch 1/B .. B/B (1,000 ids each) ] + [ ScenePass(scene) ... ]
    └── CustomBattleHelper.StartGame(one-character player party, batch 0 as the enemy party)

Patch21_ShaderPrecompilation:
    └── LoadingScreen_ShaderProgress_Patch
            └── AccessTools.Method(typeof(LoadingWindowViewModel), "Update")
            └── Postfix: DescriptionText = runner.StatusLine while a walk is active
```

### Scene-walk architecture (2026-06-17)

The character work is item 0 onward of a **work list**; scenes follow. The whole walk:

```
SubModule menu action  ──▶  ShaderPrecompileRunner.Begin()
    plan = BuildBootstrapPlan( scenes )             scenes = PrecompileSceneProvider.GetScenes() if the opt-in is on, else empty

SubModule.OnApplicationTick ──▶  runner.Tick()   (every frame while the walk is active)
    StartCurrentItem → MBGameManager.StartNewGame(new TaomShaderGameManager(item))
        TaomShaderGameManager.OnLoadFinished → (item 0) NotifyRosterDiscovered → runner re-plans the batches
                                             → CustomBattleHelper.StartGame(item data)
                                             → ShaderPrecompileRunner.NotifyItemRendering()  [Running]
    TickRunning → ShaderPrecompileDecider.Decide(remaining, itemElapsed, now, isLoading)
        Wait / AdvanceItem / AbortItem
    BeginEnd → MBGameManager.EndGame()  [Ending]
    TickEnding → back at menu? → next item or Finish()
```

- **`ShaderPrecompileDecider`** (pure, unit-tested) owns per-item compile detection. Completion (count back to 0) requires `_observedWork` first (the 2026-05-04 initial-zero latch fix, generalized). The "nothing to compile, advance" grace counts **render** time (from the first non-loading frame), not load time, so a heavy scene still loading is never skipped. Backstops: a no-progress (count frozen) abort and an absolute per-item cap, both set per item kind by the runner.
- **`ShaderPrecompileRunner`** (engine boundary) owns the outer state machine (Idle→Starting→Running→Ending→Complete) and chains the per-item custom battles. Every state has a timeout escape. `Game.Current==null` is the post-`EndGame` teardown signal, with a 90-s last-resort backstop; `TickEnding` logs the live state at 1 Hz to confirm which path fires. Because each item is a fresh `MBGameManager.StartNewGame`, the walk re-enters `SubModule.OnGameInitializationFinished` once per item, which surfaced a latent per-game re-patch crash (issue #288: patch application was unguarded, so the 2nd game re-applied a non-idempotent transpiler and threw). Now guarded once-per-process; see `docs/reviews/rca-repatch-crash-2026-06-18.md`. The completion line and toast carry the number of items that timed out, failed to start or were aborted, so a partial walk never reads as full coverage.
- **`TaomShaderGameManager`** (`CustomGameManager` subclass) builds the per-item `CustomBattleData`: a character batch = the one designated character as the player party plus the batch as the enemy party; a `ScenePass` = the same one-character player party against five copies of that character on the item's real scene.
- **`PrecompileSceneProvider`** reads the scene list (below) and falls back to a baked default. Of the **21 registered** TAOM `_forceatmo` scenes (8 open-field battle + 9 custom siege + 4 custom village), the live `precompile_scenes.txt` walks **12** (8 siege + 4 village): the 8 open-field battle scenes (6 Mordor `62470413`, 2 Rohan `ee2cb04b`) and the Helm's Deep siege are **disabled** because their `pbr_terrain` vista permutation hard-crashes some GPUs on scene load, re-enable once the native shader-compile-guard hook lands (#287). **Fallback synced (2026-06-25):** the baked `DefaultScenes` mirrors the live `precompile_scenes.txt` exactly, so deleting/emptying the config no longer resurrects the crashing Mordor scenes. Pinned by `PrecompileSceneProviderParseTests.DefaultScenes_ExcludesDisabledCrashScenes` (a class inside `ShaderPrecompilePlannerTests.cs`).

### Roster batching (2026-09-11, #560)

**The defect.** The 2026-06-17 RCA recorded, as finding 5, that "the character battle adds up to 3000 troops/side, but the engine caps actual battle/render size, so not every troop's shaders compile in one pass" and deferred "roster batching across multiple battles" to an iteration 2 that was never built. The mechanism turned out to be the other way round, and worse. On 1.4.8 the `CustomBattle` mission's `MissionCustomBattlePreloadView` collects every `CustomBattleCombatant.Characters` entry from every combatant and hands them to `PreloadHelper.PreloadCharacters`, which walks every `BattleEquipments` set, registers every item mesh (holster, flying, female and slim variants, horse meshes, reins), then calls `MetaMesh.PreloadForRendering()` and `MetaMesh.PreloadShaders(tableau, teamColor)` per unique mesh (`Native__TaleWorlds.MountAndBlade.View.cs:26748-26772` and `:3483-3491` in the v1.4.8 dump). So equipment coverage is decided by roster membership, and the spawn cap is irrelevant to it. The old manager capped the roster at 3,000 per side and added every soldier twice, while the roster that loads under the `CustomGame` game type is roughly 4,600 to 5,300 characters (TAOM's 3,820 `<NPCCharacter>` tags in the 42 of its 44 nodes gated for `CustomGame`, SandBoxCore's 727, CustomBattle's 24, NavalDLC's if enabled). The 6,000 slots overflowed by thousands, and because vanilla modules enumerate first, the dropped tail was TAOM's own. The copies bought nothing: the preload dedupes meshes, and `CustomBattleCombatant.AddCharacter(c, n)` just repeats the list entry.

**The fix.** `ShaderPrecompilePlanner.BuildPlan(roster, scenes)` slices the cleaned roster (trimmed, blanks dropped, ordinal de-dupe, order kept) into `ceil(N / DefaultCharacterBatchSize)` `CharacterBattle` items, each carrying its explicit `CharacterIds`, `BatchIndex` and `BatchCount`, followed by the scene passes. Every character is preloaded exactly once, and each battle keeps at most 1,000 characters' meshes resident, which bounds the memory shape TAOM has crashed on before (#385). Why batches rather than one big battle: the whole roster in one battle keeps every Armory item resident at once; the dev machine survived 3,000 to 4,000 of them, an 8 GB card is unproven.

**Discovery.** The roster cannot be read at the main menu: `MBObjectManager.Instance` is created by the `Game` constructor and nulled by `Game.Destroy()`, and the runner's own "back at the menu" signal is `Game.Current == null`. So `Begin()` starts from `BuildBootstrapPlan(scenes)`, whose item 0 is a placeholder batch with no ids. Inside that first custom game, `TaomShaderGameManager.ResolveBatchIds()` reads the roster through `IShaderPrecompilationService`, calls `ShaderPrecompileRunner.NotifyRosterDiscovered(generation, roster)` (guarded like the other two callbacks: current generation, still `Starting`, and `_index == 0`), and takes batch 0's own share through `SliceBatch(roster, 0)`, which slices exactly as `BuildPlan` does (pinned by `SliceBatch_MatchesBuildPlanBatchAtSameIndex`). The runner swaps in the full plan; the runner log line `roster: N characters, B batches of up to 1000; plan now M items` is the proof it happened. An empty roster keeps the bootstrap plan, the batch throws, and the walk advances to the scene passes with the failure counted.

**The battle shape.** Each character battle takes the vanilla custom-battle shape, not the old headless one. `CustomBattleHelper.StartGame` sets `Game.Current.PlayerTroop = data.PlayerCharacter`, and `Mission.SpawnTroop` flags that troop `AgentControllerType.Player` when it spawns on the player side, which is the one writer of the private `Mission._initialPlayerAgent` that `DeploymentMissionController.SetupTeams` and `FinishDeployment` dereference unconditionally (still true on 1.4.8, `TaleWorlds.MountAndBlade.cs:81659` and `:81557`). So the player party is exactly one character (a hero when the batch has one, else the first that resolves) and the batch is the enemy party; the preload view walks both combatants, so the enemy side is covered. The initial spawn split always gives the smaller side `ceil(ratio * battleSize)`, at least one slot, so the designated character always spawns and the engine sets the field itself. A one-entry player party also keeps `CanPlayerSideDeployWithOrderOfBattle()` false (the gate resolves through `DefaultBattleMissionAgentSpawnLogic.GetNumberOfPlayerControllableTroops()` to the player supplier's `CountOfCharacters`, compared against 20), so `SetupTeams` auto-calls `FinishDeployment`: no Order of Battle screen, no hang. (An earlier draft also claimed the old force-finish path would have written a junk Order of Battle configuration through `OrderOfBattleVM.SaveConfiguration()`; the Codex pass showed Custom Battle constructs the base `OrderOfBattleVM`, whose `SaveConfiguration()` is empty, so nothing is written in this mission either way. The shape stands on the hang alone.) The designated character dies within seconds against the batch; that is the normal "player died in a custom battle" path (`BattleEndLogic` prints "press Tab" and does not end the mission), and because a one-troop side counts as depleted the moment it dies, `BattleEndLogic` also stops both spawners within about a second, so the battle goes quiet early. Coverage is unaffected: the preload walked the whole roster before the first frame. The runner ends the item when the shader count settles.

**What the walk covers, exactly.** The BATTLE equipment sets of every character that loads under the `CustomGame` game type, both combatants. Not covered: characters whose XML node is Campaign-only (in TAOM, `named_companions` and `taom_education_character_templates`, so Aragorn's `strider_sword` never preloads here), civilian equipment (`PreloadHelper` reads `CivilianEquipments` only when `Mission.DoesMissionRequireCivilianEquipment`, which a battle does not set), and race and skin body materials, which compile when an agent of that race first renders (the spawned wave covers most races; the campaign battle under "Verification owed" is the proof). The shipped text therefore says "the troops, lords and battle equipment", not "every".

**Per-batch caps.** A character batch keeps generous decider caps: 60 minutes absolute, 15 minutes frozen-count, churn backstop off. The frozen-count guard is the real stuck detector and does not scale with batch size; a tight absolute cap on a slow HDD machine would recreate silent under-coverage, the defect this work removes. Scene passes keep their tight caps (8 minutes absolute, 3 minutes frozen, 6 minutes churn).

**The crash skip list stays scene-only.** `MarkLoading` is called for scene passes only; a character batch is never marked and never auto-skipped, so a hard crash inside one restarts the walk from batch 1 next time rather than silently dropping coverage.

**A walk whose roster was never discovered reports INCOMPLETE, not COMPLETE.** Batch 1 is the only item that can re-plan. If it times out, fails to start, or finds no characters, `_rosterDiscovered` stays false, the plan stays the one-item bootstrap plan, and `Finish()` writes `WALK INCOMPLETE: the roster was never discovered, no troop shaders compiled` at ERROR level with a status line that says to check the log and run again. The deep review of 2026-09-11 found the earlier shape reporting COMPLETE in that case (`docs/reviews/rca-shader-precompile-reenable-2026-09-11.md`). Every skip path (start timeout, decider abort, failed start, `StartNewGame` throwing, the owned mission ending outside the walk) increments the aborted-items count the completion line carries.

**Teardown is owned until the engine confirms it (Codex pass, 2026-09-11).** `MBGameManager.EndGame()` is `async void` and dereferences `Game.Current` once no manager is current, so the runner never calls it without a game (`BeginEnd` checks `Game.Current` first; a cancel at that instant would otherwise have crashed the process past the caller's catch). Ctrl+Shift+K is a cancellation REQUEST: it enters the same `Ending` state the per-item teardown uses and `TickEnding` finishes the walk as cancelled once the game is gone, idempotent, never a second `EndGame()`, and the runner stays active until then so a fresh `Begin()` cannot start a game on top of one still tearing down. A teardown that has not resolved within `EndTimeoutMs` STOPS the walk (`WALK STOPPED`, error level, a status line telling the player to quit to the main menu and run again) instead of starting the next item on top of a game whose loading callbacks can still fire. And the runner owns exactly one mission per item: `SubModule.OnMissionBehaviorInitialize` calls `TryClaimMission(mission)`, which is true for the first mission initialized while an item is Starting or Running and, for any OTHER mission during that item, stands the walk down without touching it (the player left the shader battle from the scoreboard and started their own from the custom-battle screen; that battle never receives the guard and is never ended by the runner). If the owned mission ends outside the walk, the item counts as aborted and the game is torn down so the walk continues from the menu.

**Log lines a cold walk must show, in order:** `=== WALK START: character batches (roster discovered on first load) + 0 scene passes ===` (plus `scene passes are off ...` on a default install), `--- item 1/1: Troops batch 1 (discovering roster) ---`, `roster: N characters, B batches of up to 1000; plan now B items`, then per batch `--- item i/B: Troops batch i/B (k characters) ---`, `batch i/B: loaded k of k ids, player 1, enemy k, 0 unresolved, 0 skipped`, `item i rendering, watching shader count`, `item i done (compiled, settled) after Ns` (never `aborted`), `Ending item i resolved via clean-menu at Ns`, and finally `=== WALK COMPLETE: B items, 0 aborted in Xm Ys ===`. The sum of `loaded k` over the batches equals N. No line starting `FALLBACK:` should appear (see the guard below).

### Deployment fallback guard (`ShaderPrecompilePlayerAgentGuard`, #336, reshaped by #560)

`TryClaimMission` is true only for the first mission initialized while an item is `Starting` or `Running`, never during the between-item teardown and never for a second mission started by the player, so no battle of the player's ever receives the guard. (`IsWalkInProgress`, true in the same two states, is what `BannerBearerAssignmentMissionLogic` reads to skip banner assignment inside the walk.)

The 1.4.6→1.4.7 engine bump broke the old headless walk. On 1.4.7 the precompile hung indefinitely (users: "stuck for a long time"; worked on 1.4.6). **Two symptoms, one root cause:**

1. **NRE crash/wedge.** 1.4.7 added an **unconditional** deref of `Mission.InitialPlayerAgent` to `DeploymentMissionController.SetupTeams()` and `FinishDeployment()`, the new `AgentControllerType` hand-control lines. `Mission._initialPlayerAgent` is assigned **only** when an agent builds with `Controller == AgentControllerType.Player`. The old precompile battle was **headless** (thousands of troops, no guaranteed player-controlled agent; whether the designated character spawned depended on the spawn priority queue), so the field could stay `null` and `SetupTeams()` NREd on every mission tick.
2. **Deployment-view hang.** With the NRE guarded, the all-characters battle (enough player troops that `CanPlayerSideDeployWithOrderOfBattle()` is true) opened the Order-of-Battle **deployment view** and waited for the player to click *Deploy*. Headless, nobody clicks → the game froze at the deployment screen.

**Since #560 the battle shape above removes both causes on the normal path**: the engine sets `InitialPlayerAgent` itself, and a one-entry player party never opens the deployment view. The guard (`MissionLogic`, added **only to the walk's own battle** from `SubModule.OnMissionBehaviorInitialize`, gated on `ShaderPrecompileRunner.TryClaimMission`, so a **normal battle never gets it**) stays as a fallback for a walk that did not take that shape:

- **Seed `InitialPlayerAgent`** on the first **player-team** agent built while the field is still null (drift-guarded reflection write of the private `_initialPlayerAgent`; pinned by `ReflectionSiteBindingTests`). Player team only, which is the #560 correction: `SetupTeams` spawns the **enemy** side first, so "the first agent built" is an enemy agent, and the old guard seeded that one, which `GeneralsAndCaptainsAssignmentLogic` then read as the player team's general and `FinishDeployment` later made the player-controlled `MainAgent`. `OnSetupTeamsOfSide(PlayerSide)` runs before the deref, so a player-team seed still lands in time. The seed logs a `FALLBACK:` warning; its presence in a walk log means the batch's player party was not the designated character.
- **Force-finish deployment** once `SetupTeams` has run (`deployment.TeamSetupOver`) by calling the controller's public `FinishDeployment()`, only if a controller is still present, which the auto-finish path removes. Also a `FALLBACK:` warning. (Custom Battle uses the base `OrderOfBattleVM`, whose `SaveConfiguration()` is empty, so even this path writes nothing to the player's profile.)

**Wiring gotcha (in-game-caught):** the guard was first added from `TaomShaderGameManager.OnLoadFinished` via `Mission.Current?.AddMissionBehavior`: that **silently no-op'd** because `Mission.Current` isn't the battle mission yet at `OnLoadFinished`. It never registered (absent from the mission behavior dump) and the NRE still fired. Adding mission behaviors to a freshly-opened mission must go through `OnMissionBehaviorInitialize` (the engine hands the mission in directly). See `docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md`. Neither fallback path had ever run in-game before #560: the 2026-07-11 run was warm-cache and advanced before deployment mattered (that RCA's finding 3).

## v1.4.8 and the shader cache

The v1.4.8 bump needed **no code change here** (TAOM compiles against v1.4.8 with 0 errors, `BindingVerification` 106/106: [`docs/migration/v1.4.8-impact.md`](../migration/v1.4.8-impact.md)), and the 2026-09-11 review re-verified every engine member the feature binds against the installed 1.4.8: the `InitialStateOption` constructor, `InquiryData`, `CustomBattleData`, `CustomBattleCombatant`, `Mission._initialPlayerAgent`, `DeploymentMissionController.FinishDeployment` and `TeamSetupOver`, and Patch21's string-bound `LoadingWindowViewModel.Update()`. Three v1.4.8 changelog items land on this feature anyway, none of them through the managed API:

1. **The locally created shader cache is now deleted automatically after a module list change.**
2. **A fallback system detects and removes a corrupted local shader cache.**
3. **The mod publish system now compiles terrain shaders.**

(1) is the one that bites, because of *where* this feature's product lives. Every TAOM `_forceatmo` scene ships **header-only**, which `precompile_scenes.txt:4` already states as the premise and which was re-measured against the live install on 2026-08-10: across the 43 scene folders under `<game>\Modules\TAOM_Map\SceneObj\*\ShaderCache\D3D11\` (excluding `Backups\`) there are **43** `terrain_shaders_header_data.bin` and **zero** `compressed_shader_cache.sack`, against **406** per-scene sacks under `Native` / `SandBox` / `SandBoxCore` / `StoryMode`. So the walk writes nothing per-scene; its entire output is entries in the player's **local** shader cache, exactly what (1) now deletes.

### The "run once" text was wrong; fixed 2026-09-11

Until #560 both copies of `{=taom_precompile_hint}` told the player the walk was a one-time job ("Run once after installing TAOM."), and the two copies did not even match. Under v1.4.8, enabling NavalDLC, toggling any mod, or a launcher reordering the module list discards the work. TAOM writes no completion sentinel that could notice (`ShaderPrecompileCrashGuard` persists exactly two files, `Logs/shader-precompile-inflight.marker` and `Logs/shader-precompile-crashed-scenes.txt`; completion exists solely in-session as the `WALK COMPLETE` status and log line).

Both copies now read: "Pre-compiles every troop and equipment shader so first battles do not stutter or stall. Re-run after any change to your mod list: the game clears its compiled shaders when the module list changes." The inquiry (`{=taom_precompile_inquiry_title}` / `{=taom_precompile_inquiry_body}`, new keys) says the same, gives the character-only expectation (20 to 70 minutes cold, Ctrl+Shift+K cancels, do not start another battle) and points at the MCM opt-in for scenes. Its `{newline}` tokens are a `GameTexts` variable bound in `Game.Initialize`, which has not run at the cold main menu, so `SubModule` binds it on the `TextObject` (`SetTextVariable("newline", "\n")`) before `ToString()`; without that the paragraphs run together. **Translation state:** all 12 language files carry the three rows in English and the stale hint was removed from `tools/translation_cache/<lang>.json`; the translator run itself (`python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply`, per language) is owed because no API key was available in the session that shipped this. A row still equal to its English text is what the tool treats as untranslated, so nothing else needs resetting.

### (3) is an opportunity, per-scene sacks instead of a player-side walk

A publish system that compiles terrain shaders is a candidate path to shipping real per-scene `compressed_shader_cache.sack` files, which would retire the #287 class outright rather than paying for it with the player's time. It runs into **[#448](https://github.com/haterade22/TAOM/issues/448)** first: reading the version dword at offset 4 of all 490 sacks in the install on 2026-08-10, the **486** the v1.4.8 update wrote carry `0x0783` while all three TAOM-side module sacks (`TAOM/`, `TAOM_Map/`, `LOTRLOME_Armory/Shaders/D3D11/`) still carry `0x0782`. Whether v1.4.8 accepts `0x0782`, ignores it, or routes it into (2)'s corrupted-cache fallback is unverified, that is what #448 tracks.

## Configuration

**MCM toggles** (group "Graphics/Shader Precompilation", GroupOrder 15, both `RequireRestart = false`): **Enable Shader Precompilation** (property `EnableShaderPrecompilation`; master; default on) live-hides the main-menu option via its `isHidden` callback when off, no relaunch, and blocks new walks (a walk already running finishes). **Include Scene Passes (can crash some GPUs)** (property `EnableShaderPrecompileScenePasses`; **default off**) gates the terrain/atmosphere scene passes; the runner reads it in `Begin()` with a `false` fallback, so an unreadable settings page never turns scenes on. **Why the property was renamed instead of defaulted off:** `TaomSettings` persists as json2 and MCM loads that file over the compiled default, so every install that launched TAOM while the old attribute was live (2026-06-25 to 2026-08-20) had `EnableScenePassPrecompilation = true` on disk, and a changed default would never have reached it. MCM's json2 reader hands the file to its settings converter, which fills only the properties the class currently declares and ignores the rest, so the orphaned key is ignored on load and dropped by the next save. Rule for the next time: rename, never flip, a persisted MCM default. The co-op exclusion list in `CoopSettingsRelevance` follows the rename; the rename leaves the settable-property count pinned by `SettingsFingerprintTests` unchanged.

**Scene list:** `Main/_Module/ModuleData/shader_precompilation/precompile_scenes.txt`, one scene id per line, `#` comments, blank lines ignored. Read directly by `PrecompileSceneProvider` (no SubModule.xml registration; it is not engine-loaded XML), and only consulted when the opt-in is on. If missing/empty, the baked `DefaultScenes` fallback is used, as of 2026-06-25 it mirrors the live txt exactly (the 6 Mordor open-field scenes were commented out to match), so the fallback no longer re-walks disabled crashers. All are header-only on disk (no `compressed_shader_cache.sack`), so all runtime-compile; reused-vanilla siege/village scenes ship their own `.sack` and need nothing. Sieges currently ride the `"Battle"` ScenePass (terrain + atmosphere + static walls); a `"Siege"`-mission builder is the escalation if siege-engine materials stay cold (probed in-game). Add `battle_terrain_*` ids to also cover vanilla terrains (each adds ~5-15 min to the walk).

**Crash skip list:** `Logs/shader-precompile-crashed-scenes.txt`, auto-managed by `ShaderPrecompileCrashGuard`. If a scene hard-crashes the process during load (a GPU/driver-specific native AV, e.g. `fords_of_isen` on the `pbr_terrain` input-layout-9 compile), the runner records it here (via a surviving `shader-precompile-inflight.marker`) and drops it from subsequent walks so the walk can complete. **Delete this file to retry the skipped scenes.** Only true process crashes during a scene pass are recorded, a slow item, a per-item timeout, a clean exit, or a character batch never lands here.

Tunable constants:

| Constant | Where | Value | Description |
|----------|-------|-------|-------------|
| `DefaultCharacterBatchSize` | `ShaderPrecompilePlanner.cs` | `1000` | Characters per batch. A 4,600 to 5,300 roster becomes 5 or 6 batches, roughly five minutes of load and teardown overhead in total, while each batch preloads about a fifth of what one battle already carried on the dev machine. Re-tune from the walk's per-batch memory lines; halve it if the `MemStats()` commit peak approaches the #385 envelope. |
| `MaxEnemyRoster` | `TaomShaderGameManager.cs` | `3000` | Ceiling on the enemy roster. Unreachable at the default batch size; kept so a future batch-size change cannot silently overflow a combatant. A non-zero `skipped` in the batch log line is the tell. |
| `ScenePassEnemyTroops` | `TaomShaderGameManager.cs` | `5` | Enemy copies of the designated character on a scene pass. Enough to render agents; the point of a scene pass is the scene's own shaders. |
| `CharacterBattleScene` | `ShaderPrecompilePlanner.cs` | `"battle_terrain_029"` | `CustomBattleData.CoreContentDefaultSceneName`, the default custom battle scene, always present (`SandBoxCore/SceneObj/`). |
| `CharBattlePerItemMs` / `CharBattleNoProgressMs` / `CharBattleMaxActiveMs` | `ShaderPrecompileRunner.cs` | 60 min / 15 min / off | Per-batch decider caps: absolute wall, frozen-count abort, churn backstop. |
| `ScenePerItemMs` / `SceneNoProgressMs` / `SceneMaxActiveMs` | `ShaderPrecompileRunner.cs` | 8 min / 3 min / 6 min | Per-scene-pass decider caps. |
| `StartTimeoutMs` / `EndSettleMs` / `EndTimeoutMs` | `ShaderPrecompileRunner.cs` | 10 min / 1.5 s / 90 s | An item that never reaches "rendering" is abandoned after 10 minutes: every item pays a full CustomGame module-data load before its manager can report, and a premature abort on batch 1 discards the whole character phase (only batch 1 can re-plan), so the bound is generous and Ctrl+Shift+K is the human escape; the menu settles 1.5 s after teardown; a teardown that has not resolved after 90 s STOPS the walk (never a second game on top of a live one). |
| `startupGraceMs` / `settleMs` | `ShaderPrecompileDecider.cs` | 30 s / 5 s | Render time with a zero count before an item counts as already cached; how long a zero must hold after observed work before the item is done. |

### Why the constants were tuned (2026-05-04, superseded 2026-09-11)

The original values (`MaxTroopsPerSide=2000`, `SoldierCopies=4`, `StuckAbortSeconds=120`) silently dropped roughly 1,000-1,400 characters when the slot budget filled before all characters were added: users ran the 20-70 minute process, saw the loading screen finish, and still hit mid-game stutter on the dropped characters. They reported "Pre-compile Shaders doesn't work." The 2026-05-04 values (3,000 per side, 2 copies) closed the gap for the roster of that day and re-opened it as the roster grew past 4,600; #560 replaced the cap with batching and removed the copies, so the drop is now unreachable by construction rather than by tuning.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ShaderPrecompilation/IShaderPrecompilationService.cs` | Service interface |
| `Main/Features/ShaderPrecompilation/ShaderPrecompilationService.cs` | Queries `IObjectManagerAdapter` for all cultures (bandits included, they have unique meshes/equipment that need shader coverage too), deduplicates character IDs, caches culture set |
| `Main/Features/ShaderPrecompilation/ShaderPrecompilationIoC.cs` | DryIoc singleton registration (+ `IPrecompileSceneProvider`, `IShaderPrecompileCrashGuard`, `ShaderPrecompileRunner`) + hook init |
| `Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs` | **Orchestrator** (engine boundary): outer state machine, bootstrap plan then the roster re-plan (`NotifyRosterDiscovered`), chains per-item custom battles, drives the decider, owns the status line and the aborted-items count. Per-item-kind decider caps, self-classifying abort logs, `Cancel()` + **Ctrl+Shift+K** cancel hotkey (a request that finishes through the teardown state), `TryClaimMission` (the walk's own battle gets the fallback guard; any other mission stands the walk down), `IsWalkInProgress` (read by the banner-bearer logic) |
| `Main/Features/ShaderPrecompilation/ShaderPrecompileDecider.cs` | **Pure** per-item compile-detection state machine (observed-work latch, render-grace, settle, no-progress + absolute timeouts, **churn backstop** = abort a count that changes forever but never settles, `LastAbortReason`) |
| `Main/Features/ShaderPrecompilation/ShaderPrecompilePlayerAgentGuard.cs` | **Deployment fallback guard** (`MissionLogic`, #336, reshaped by #560): seeds `Mission.InitialPlayerAgent` with a player-team agent only if the engine did not, and force-finishes deployment only if a controller is still waiting. See the section above |
| `Main/Features/ShaderPrecompilation/ShaderPrecompilePlanner.cs` | **Pure** work-list builder: `BuildBootstrapPlan`, `BuildPlan` (character batches of `DefaultCharacterBatchSize`, then one ScenePass per scene), `SliceBatch`, `CountBatches` |
| `Main/Features/ShaderPrecompilation/{IPrecompileSceneProvider,PrecompileSceneProvider}.cs` | Scene list from `precompile_scenes.txt` (baked-default fallback) |
| `Main/Features/ShaderPrecompilation/{IShaderPrecompileCrashGuard,ShaderPrecompileCrashGuard}.cs` | Scene crash skip list (`Logs/` marker + list files) |
| `Main/Features/ShaderPrecompilation/Domain/PrecompileItem.cs` | `PrecompileItem` (`Kind`, `SceneId`, `Description`, `CharacterIds`, `BatchIndex`, `BatchCount`) + `PrecompileItemKind {CharacterBattle, ScenePass}` |
| `Main/_Module/ModuleData/shader_precompilation/precompile_scenes.txt` | Editable scene list, 12 active (8 siege + 4 village); the 8 open-field battle scenes + Helm's Deep are disabled (Mordor + Rohan `pbr_terrain` vista crash, #287). 21 registered total. Baked `DefaultScenes` fallback mirrors this exactly (synced 2026-06-25). |
| `Main/Features/ShaderPrecompilation/TaomShaderGameManager.cs` | Extends `CustomGameManager`; builds the per-item `CustomBattleData` (one-character player party; a character batch or five scene-pass copies as the enemy party); discovers the roster on the bootstrap batch |
| `Main/Features/ShaderPrecompilation/Hooks/LoadingScreen_ShaderProgress_Patch.cs` | `Patch21_ShaderPrecompilation`, loading screen progress text |
| `Main/SubModule.cs` | Applies `Patch21_ShaderPrecompilation`, calls `InitializeHooks`, registers the menu button + inquiry in `OnBeforeInitialModuleScreenSetAsRoot()`, adds the guard to the walk's own battle (`TryClaimMission`) in `OnMissionBehaviorInitialize` |
| `Main/Features/TaomSettings.cs` | The two MCM properties (`EnableShaderPrecompilation`, `EnableShaderPrecompileScenePasses`) |
| `Main/IoC.cs` | `ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container)` |
| `Main/_Module/ModuleData/taom_module_strings.xml` + `Languages/*/std_taom_module_strings_*.xml` | `taom_precompile_shaders`, `taom_precompile_hint`, `taom_precompile_inquiry_title`, `taom_precompile_inquiry_body` |

## Dependencies

- `IObjectManagerAdapter` (Adapters), provides `GetAllCharacterInfos()` and `GetAllCultureInfos()`
- `IModLogger` (Core/Logging), log info/error during battle setup
- `CustomGameManager` (`TaleWorlds.MountAndBlade.CustomBattle.dll`), base class that loads CustomBattle module data
- `CustomBattleHelper` (`TaleWorlds.MountAndBlade.CustomBattle.dll`), `StartGame(CustomBattleData)` to open the mission
- `TaleWorlds.Engine.Utilities.GetNumberOfShaderCompilationsInProgress()`, live shader count from engine

## Tests

The pure core is unit-tested (the runner / game manager / guard / patch are engine boundaries, ADR-008, game-only). 56 tests across four files in `TAOM.Tests/Features/ShaderPrecompilation/`:

- `ShaderPrecompileDeciderTests` (17): the observation state machine: first-frame-zero (RCA regression), render-grace vs load-grace (the 2026-06-17 premature-advance fix), settle, idle-dip, no-progress-stuck, absolute timeout, churn cap + reason, work-observed-during-loading, per-item cap overrides.
- `ShaderPrecompilePlannerTests.cs` (22, two classes), `ShaderPrecompilePlannerTests` (17): character batches (single batch, exact multiple, partial last batch, partition without duplicates in roster order, sequential index and count, scenes after all batches with no ids, empty or null roster gives scenes only, blank and duplicate ids dropped, batch size below one throws, `CountBatches`), the bootstrap plan shape, `SliceBatch` matching `BuildPlan` and returning empty past the end, scene order and scene dedupe. `PrecompileSceneProviderParseTests` (5): `ParseSceneList` comments/blanks/trim and the fallback-drift invariant (`DefaultScenes_ExcludesDisabledCrashScenes` pins the 9 disabled crashers absent + `DefaultScenes_IncludesActiveSiegeScene` pins a representative active scene present).
- `ShaderPrecompileCrashGuardTests` (10): inflight-marker format/parse round-trip, crashed-scene list parse and de-dupe, and the crash-skip lifecycle (mark then consume without clear records and skips; mark then clear then consume records nothing; a skip persists across walks).
- `ShaderPrecompilationServiceTests` (7): character ids from all included cultures, bandit inclusion, adapter exception → empty result + logged error (both queries), deduplication, null/empty id exclusion, mixed bandit + non-bandit cultures.

`TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` pins `Mission._initialPlayerAgent` against the installed engine; `SettingsFingerprintTests` pins the settable-property count the rename must not move; `SettingRequireRestartPostureTests` requires `RequireRestart = false` on both attributes once they are uncommented (while parked it cannot see them).

## Verification owed

Boundary code proves itself only in-game. The cold walk on 1.4.8 has not run since #560. The protocol: force a cold cache by changing the module list (1.4.8 deletes the local cache on that; `Missing shader from sack` lines in `rgl_log` during batch 1 prove it was cold); confirm on a `TAOM.json` that still holds `"EnableScenePassPrecompilation": true` that the new toggle shows OFF and the `Begin` line says scene passes are off; run the walk and read the log lines listed under "Roster batching" (no `FALLBACK:` and no `WALK STOPPED` line may appear); hold Ctrl+Shift+K mid-batch on a second walk and confirm a clean cancel and a clean third start; then fight a campaign battle against a culture not yet fought and read `rgl_log` for `Missing shader from sack` (near zero for `pbr_metallic`; `pbr_terrain` misses are expected with scenes off) and the `WaitingForRender shaders=` series (short or absent, against 305 s in b18f3441). Race body and skin materials come from agents that spawn, not from the item preload, so that battle is also the check for them.

## How to Add Coverage for a New Culture

When a new TAOM culture is added, its characters are automatically included, no changes needed here. The service queries `IObjectManagerAdapter.GetAllCultureInfos()` and `GetAllCharacterInfos()` at runtime, picking up every loaded culture (vanilla, TAOM custom, and bandit) and all of its characters, and the planner slices whatever it gets.

If a culture's characters are not getting compiled, verify:
1. The culture's character XML files are loaded **under the `CustomGame` game type** (the `<IncludedGameTypes>` block of the `NPCCharacters` node in `SubModule.xml`; two TAOM nodes, `taom_education_character_templates` and `named_companions`, deliberately are not) and the characters have a valid `culture` attribute matching the culture ID
2. The `IObjectManagerAdapter` implementation's `GetAllCharacterInfos()` returns them (check `ObjectManagerAdapter.cs`)
3. The per-batch log line reads `... 0 unresolved, 0 skipped`. A non-zero `unresolved` means an id the service returned did not resolve in the custom game (a game-type gate, above); a non-zero `skipped` means `MaxEnemyRoster` was hit, which the default batch size makes impossible.

## Performance

- **LoadingScreen patch:** Vanilla calls `Update` every frame from `GauntletDefaultLoadingWindowManager.OnLateTick`, loading screen or not; the postfix returns at its `Enabled` check outside a loading screen and at the runner check outside a walk, then copies the runner's status line into `DescriptionText`.
- **Service:** `GetValidCultureIds()` builds the culture `HashSet` once and caches it for the service's lifetime. `GetAllCharacterInfos()` is called once per walk, on the bootstrap batch.

## Changelog

- 2026-09-25: **Parked again at the wiring level** (more problems than it is worth for now): menu option, Patch21 registration and both MCM attribute stacks commented out; code, tests, strings, properties and scene list kept.
- 2026-09-11: **Codex adversarial pass (GPT-6-Astra, ultra) on the re-enable**: `EndGame()` is never called without a game and cancellation is a request that finishes through the teardown path (a cancel in the wrong instant would have crashed inside the engine's `async void`), a teardown timeout stops the walk instead of stacking a second game, the runner owns one mission per item and stands down if the player starts their own battle mid-walk, `{newline}` is bound for the cold main menu, the coverage wording is honest (battle equipment of the custom-battle roster; named companions, civilian sets and race skins are outside it), and the Order of Battle profile-write rationale was withdrawn (the custom-battle VM's save is empty).
- 2026-09-11: **Deep review of the re-enable** (`docs/reviews/rca-shader-precompile-reenable-2026-09-11.md`): a bootstrap start timeout would have reported COMPLETE with no troop shaders compiled (now INCOMPLETE, and the start bound is 10 minutes), the `StartNewGame` throw path now counts as aborted, `IsWalkInProgress` is scoped to Starting/Running, the planner slices by index, the game manager is back under 150 lines.
- 2026-09-11: **Re-enabled for 1.4.8 with roster batching, the vanilla battle shape, scene passes off by default (#560).** The walk now preloads every loaded character exactly once in batches of 1,000 (the old 3,000-per-side cap with soldiers added twice dropped TAOM's own troops; equipment shaders compile at preload, so coverage is roster membership). Each battle is a one-character player party against the batch, so the engine sets `InitialPlayerAgent` itself and deployment auto-finishes; the 1.4.7 guard is a player-team-only fallback (the old seed landed on an enemy agent, and the old force-finish wrote a junk Order of Battle config). `EnableScenePassPrecompilation` renamed to `EnableShaderPrecompileScenePasses`, default off, because a json2-persisted default cannot be flipped. Hint and inquiry rewritten for 1.4.8's cache deletion; translation run owed.
- 2026-08-20: **Parked at the wiring level** ("no longer needed"): menu option, Patch21 registration and the MCM attributes commented out; code, tests, strings and scene list kept.
- 2026-08-10: **v1.4.8 engine bump: no code change, one shipped string now wrong.** Nothing in the managed shader API moved, so the feature carried over untouched. But v1.4.8 auto-deletes the local shader cache after a module-list change, and TAOM's scenes ship header-only, so the whole walk's output now lives only in the cache that gets deleted: while `{=taom_precompile_hint}` still promised "Run once after installing TAOM." Recorded as a KNOWN ISSUE (fixed 2026-09-11). Also recorded: v1.4.8's terrain-shader publish support as a candidate path to per-scene sacks, blocked behind the `0x0782` vs `0x0783` cache-format gap (#448).
- 2026-07-11: **1.4.7 deployment-NRE fix (#336).** Root-caused the "precompile stuck on 1.4.7" reports to a 1.4.7 engine regression: `DeploymentMissionController.SetupTeams()`/`FinishDeployment()` now unconditionally deref `Mission.InitialPlayerAgent`, which was null in the headless precompile battle → NRE every mission tick + (once guarded) a freeze at the OoB deployment view. Added `ShaderPrecompilePlayerAgentGuard` (seeds `InitialPlayerAgent` + force-finishes deployment, scoped to the walk via `IsWalkInProgress`). Robustness package alongside: per-item-kind decider caps (scene passes bail at 8 min, not 90), a churn backstop, self-classifying abort logs, and a Ctrl+Shift+K cancel. In-game 1.4.7: full walk completes (13 items, 0 NRE, 0 hang) on a warm cache. RCA `docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md`.
- 2026-06-25: Phase 0 of the native shader-compile guard (#287): fixed the `DefaultScenes` fallback drift (now mirrors the live `precompile_scenes.txt`; no missing-config resurrection of disabled crashers); added MCM "Graphics/Shader Precompilation" toggles (master + Include Scene Passes, off runs only the safe all-characters pass); added post-crash in-game + log guidance for exporting the Windows Event Log fault offset the native guard needs. Root cause confirmed as `normalize()`-of-zero in `pbr_terrain` (`terrain_pixel_functions.rsh:818`) but the shader source is engine-global (unshippable as a module override).
- 2026-06-18: Added a per-scene crash guard (`ShaderPrecompileCrashGuard`) that records hard-crashing scenes to a skip list and drops them from the plan so the walk can finish.
- 2026-06-18: Suppressed the battle-load stall watchdog during the walk (longest legitimate load).
- 2026-06-18: Extended the walk to custom siege + village scenes; `precompile_scenes.txt`/`DefaultScenes` grew from 8 to 21 scenes (#287).
- 2026-06-17: Re-enabled the "Pre-compile Shaders" menu option and rewrote it to scene-walk each TAOM battle scene so terrain/atmosphere shaders compile, targeting the battle-load d3dcompiler CTD (#287).
- 2026-05-22: Hid the Pre-compile Shaders main-menu option (commented the `InitialStateOption`) while the feature was unreliable; rest of the wiring kept active.
- 2026-05-04: Added visible per-second progress UI and fixed the initial-zero latch race (#106 follow-up).
- 2026-05-04: Eliminated the silent character drop and relaxed the premature stuck-abort (#106, follow-up to #57).
- 2026-04-06: Reset the abort latch on completion.
- 2026-04-02: Added stuck-shader auto-abort with a countdown UI (#57).
- 2026-04-02: Added the "Pre-compile Shaders" main-menu option launching a hidden all-characters custom battle (#57).

## GitHub Issues

- [#57, feat: Shader Pre-compilation at Main Menu](https://github.com/haterade22/TAOM/issues/57), original feature, CLOSED
- [#106, fix: silent character drop + premature 120s abort + stale latch on retry/abort](https://github.com/haterade22/TAOM/issues/106), 2026-05-04 stability fix, CLOSED
- [#287, Battle-load CTD/hang: scenes lack precompiled shader caches](https://github.com/haterade22/TAOM/issues/287), 2026-06-17 re-enable + scene-walk, CLOSED 2026-08-20 by the first park
- [#336: crash/hang: shader precompile stuck on 1.4.7 (DeploymentMissionController.SetupTeams NRE on headless battle)](https://github.com/haterade22/TAOM/issues/336): 2026-07-11, in-game confirmed warm (13/13 items, 0 NRE, 0 hang); the cold path is now the #560 shape
- [#448, TAOM's shader caches are one format version behind (`0x0782` vs v1.4.8's `0x0783`)](https://github.com/haterade22/TAOM/issues/448), 2026-08-10, deferred by decision; gates the "publish per-scene sacks" path above
- [#560: Re-enable shader pre-compilation on 1.4.8: batch the roster, scene passes opt-in](https://github.com/haterade22/TAOM/issues/560): 2026-09-11, CLOSED 2026-09-14; the cold in-game walk was still owed when the feature was parked again 2026-09-25

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/atmosphere-persistence.md](./atmosphere-persistence.md)
- [docs/features/battle-load-diagnostics.md](./battle-load-diagnostics.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
