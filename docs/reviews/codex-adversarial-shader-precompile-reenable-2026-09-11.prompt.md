# Codex adversarial review: ShaderPrecompilation re-enable for Bannerlord 1.4.8 (#560)

You are the independent adversarial reviewer for TAOM (a Bannerlord 1.4.8 total conversion). Assume the code under review has bugs and find them. Verify every claim against the INSTALLED engine (the ilspy MCP server or `pwsh tools/taom-src.ps1 path <Full.Type.Name>` from the repo root decompiles the installed 1.4.8 DLLs; the E:\Decompiled_Bannerlord dump is a reference copy of the same version). Read the actual TAOM source before asserting anything about it. Do not skip a section because it is hard; write UNVERIFIED rather than guessing.

## Feature (1-2 lines)

The main-menu "Pre-compile Shaders" walk, parked 2026-08-20, is re-enabled. It now runs the loaded character roster in batches of 1,000 through hidden custom battles so every troop/equipment shader compiles at preload; each battle is a one-character player party against the batch; scene passes are an MCM opt-in that is off for every install via a RENAMED property; the shipped text now says to re-run after mod-list changes. All changes are uncommitted in the working tree (see `git diff HEAD` and `git status`).

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur". (This feature does not touch culture data; the cheatsheet is here so you do not flag vanilla culture ids as bugs.)

## READ FIRST

- docs/features/shader-precompilation.md (the feature doc, rewritten for #560; sections "Roster batching" and "Deployment fallback guard" state the design claims you must attack)
- CHANGELOG.md, the entry "feat(shaders): Pre-compile Shaders is back, covers every character, scene passes off for everyone (#560)" under ## 2026-09-11
- docs/reviews/rca-shader-precompile-1.4.7-2026-07-11.md (the 1.4.7 headless-deployment NRE and its warm-cache blind spot)
- docs/reviews/rca-shaderprecompilation-2026-06-17.md (finding 5 recorded the coverage limitation that #560 closes)
- .claude/rules/harmony-patches.md section "Static State Machines: Sentinel-Collision Check"
- Main/_Module/ModuleData/shader_precompilation/precompile_scenes.txt

## KNOWN SUSPECTS (CONFIRM or DISPUTE each, with decompiled evidence)

S1. Coverage is roster membership. Claim: in the `CustomBattle` mission, `MissionCustomBattlePreloadView.OnPreMissionTick` collects every `CustomBattleCombatant.Characters` entry of BOTH combatants and calls `PreloadHelper.PreloadCharacters`, which walks every `BattleEquipments` set, registers every item mesh (holster/flying/female/slim variants, horse meshes, reins) and calls `MetaMesh.PreloadForRendering()` + `MetaMesh.PreloadShaders(useTableau, useTeamColor)` per unique mesh, BEFORE the first rendered frame. Therefore every character in the enemy roster gets its equipment shaders compiled regardless of the Battle Size spawn cap. Attack: is the preload view really in the view list for GameTypeStringId "Battle" (BannerlordMissions.OpenCustomBattleMission -> MissionState.OpenNew("CustomBattle", ...))? Does `PreloadShaders` actually queue compiles for shader permutations the renderer would later request (skinned, shadow, tableau, team-color variants), or only a subset? Do civilian equipment sets or race/skin meshes (skins.xml body meshes) get compiled by preload, or only when an agent renders? Are there equipment slots or item types the preload skips (banners, siege engines, mounts' harness meshes)?

S2. The vanilla battle shape removes the 1.4.7 NRE without the reflection seed. Claim: `CustomBattleHelper.StartGame` sets `Game.Current.PlayerTroop = data.PlayerCharacter`; `Mission.SpawnTroop` requests `AgentControllerType.Player` for the troop equal to `Game.Current.PlayerTroop` when it spawns on the player side; `Mission.BuildAgent` writes `_initialPlayerAgent` only for a Player-controlled agent; `DefaultBattleMissionAgentSpawnLogic`'s BattleSizeAllocating split gives the smaller side `ceil(ratio * battleSize)`, at least one slot, for every Battle Size option (200..1000) and the default `MaximumBattleSideRatio` / `DefenderAdvantageFactor`; `CustomBattleTroopSupplier` supplies a one-entry roster's only troop in the initial wave; therefore with a one-entry player party the engine sets `InitialPlayerAgent` itself before `DeploymentMissionController.SetupTeams` dereferences it. Attack every step. In particular: which side is "the smaller side" in that split when the player party has 1 entry and the enemy has 1,000, and can `Math.Min(val, array[num5])` or the `MaximumBattleSideRatio` clamp ever leave the player side with 0 initial spawns? Does `SetupTeams` spawn the player side (`OnSetupTeamsOfSide(PlayerSide)`) before the deref at `initialPlayerAgent.Controller = AgentControllerType.None`? Is `TeamSetupOver` set in the same tick?

S3. Deployment auto-finishes and nothing is written to the player's profile. Claim: `CustomBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux` compares `GetNumberOfPlayerControllableTroops()` (which resolves to the PLAYER party's `CustomBattleCombatant.CountOfCharacters`, entries including copies) against 20, so a one-entry player party keeps it false; `SetupTeams` then calls `FinishDeployment()` itself; `MissionGauntletOrderOfBattleUIHandler.OnDeploymentFinished` passes that same flag to `OrderOfBattleVM.OnDeploymentFinalized(bool playerDeployed)`, which calls `SaveConfiguration()` only when true. Attack: is the count really the player party's, or the TEAM's controllable count after spawn? Is `MissionGauntletOrderOfBattleUIHandler` even present in a custom battle whose OoB gate is false, and does anything else persist deployment/formation state to disk (e.g. `OrderOfBattleConfigManager`, `MissionOrderVM`, formation class caches keyed by culture)?

S4. The roster-discovery re-plan is race-free. Claim: the roster cannot be read at the main menu (`MBObjectManager.Instance` exists only inside a `Game`; `Game.Destroy()` nulls it), so `ShaderPrecompileRunner.Begin()` starts from `ShaderPrecompilePlanner.BuildBootstrapPlan(scenes)`; `TaomShaderGameManager.OnLoadFinished` (inside the first custom game) reads the roster through `IShaderPrecompilationService`, calls `ShaderPrecompileRunner.NotifyRosterDiscovered(generation, roster)` which swaps `_plan` under a generation + `Starting` + `_index == 0` guard, then takes batch 0's share via `SliceBatch(roster, 0)`. Attack: is `CustomGameManager.OnLoadFinished` called synchronously from within `MBGameManager.StartNewGame`, or from a later tick (in which case what runs in between)? If the runner's 600 s `StartTimeoutMs` fires first, the walk marks `_rosterDiscovered == false` and `Finish()` reports INCOMPLETE; trace that the late `OnLoadFinished` cannot then start a mission inside a game that `MBGameManager.EndGame()` is tearing down, or that if it does, `EndGame` handles it. Is `MBGameManager.EndGame()` (async void) safe to call while a mission is loading?

S5. The MCM rename reaches every install. Claim: `TaomSettings` persists as json2; MCM v5 loads through `JsonConvert.PopulateObject` with a `BaseSettingsJsonConverter` and no `MissingMemberHandling` override, so the orphaned key `EnableScenePassPrecompilation: true` in an existing `TAOM.json` is ignored and the new property `EnableShaderPrecompileScenePasses` starts at its compiled default `false`. Attack this against the MCM assembly actually shipped in TAOM's dependency module (TAOM.Dependencies / MCMv5.dll: decompile `JsonSettingsFormat` / `BaseSettingsJsonConverter` and the json2 read path). Could an unknown property make the converter throw, log, or reset the whole settings object? Does MCM write the renamed property back with the old value under any migration path?

S6. `{newline}` in the inquiry body. Claim: `new TextObject("{=taom_precompile_inquiry_body}...{newline}{newline}...").ToString()` yields real line breaks inside `InquiryData.Text`. Verify in `TaleWorlds.Localization` (the text processor's handling of `{newline}`) and that `InquiryData` / the inquiry popup renders multi-line text. Also verify that a `{=key}` lookup that misses the string table falls back to the inline default (so a missing translation shows English, never the raw key).

S7. The guard's team gate. `ShaderPrecompilePlayerAgentGuard.OnAgentBuild` returns early unless `agent.Team == Mission.Current.PlayerTeam`. Attack: at the moment `MissionBehavior.OnAgentBuild` fires (from `Mission.SpawnAgent` after `BuildAgent`), is `Agent.Team` already assigned? If not, the fallback seed can never fire. Also: `Mission.PlayerTeam` non-null at that point in a custom battle?

S8. Batch memory. A batch preloads 1,000 characters' equipment meshes and textures at once (the previous shape preloaded up to 3,000 per side). Is there any engine-side cap on `CustomBattleCombatant` roster size, on the number of preloaded metameshes, or on `Mission` agent count that a 1,000-entry enemy roster could hit? What does `PhysicsShape.AddPreloadQueueWithName` / `ProcessPreloadQueue` do with ~6,000 unique body names? Any evidence in the engine of a hard limit around the `rglConcurrentQueue` 131,072 assert (see CLAUDE.md Traps "Prefab entity cap")?

## FILES

Feature (all under Main/Features/ShaderPrecompilation/): Domain/PrecompileItem.cs, ShaderPrecompilePlanner.cs, ShaderPrecompileRunner.cs, TaomShaderGameManager.cs, ShaderPrecompilePlayerAgentGuard.cs, ShaderPrecompileDecider.cs (unchanged), ShaderPrecompileCrashGuard.cs (unchanged), ShaderPrecompilationService.cs (unchanged), PrecompileSceneProvider.cs (unchanged), ShaderPrecompilationIoC.cs (unchanged), Hooks/LoadingScreen_ShaderProgress_Patch.cs (unchanged, re-registered).
Wiring: Main/SubModule.cs (the `_shaderRunner` field near line 103; `_harmony.PatchCategory("Patch21_ShaderPrecompilation")` + runner resolve + `ShaderPrecompilationIoC.InitializeHooks` near line 422; the `AddInitialStateOption` block in `OnBeforeInitialModuleScreenSetAsRoot` near line 630; `OnMissionBehaviorInitialize` adds `ShaderPrecompilePlayerAgentGuard` when `ShaderPrecompileRunner.IsWalkInProgress` near line 1690; `OnApplicationTick` drives `runner.Tick()` near line 1810; `OnGameInitializationFinished` is guarded once-per-process by `_gameInitPatchesApplied`).
Settings: Main/Features/TaomSettings.cs (the "Graphics / Shader Precompilation" block), Main/Features/CoopInterop/CoopSettingsRelevance.cs.
Strings: Main/_Module/ModuleData/taom_module_strings.xml (taom_precompile_hint, taom_precompile_inquiry_title, taom_precompile_inquiry_body), Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml (English rows seeded; translation owed).
Tests: TAOM.Tests/Features/ShaderPrecompilation/ShaderPrecompilePlannerTests.cs (22 tests), ShaderPrecompileDeciderTests.cs, ShaderPrecompileCrashGuardTests.cs, ShaderPrecompilationServiceTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs (pins Mission._initialPlayerAgent).
Docs: docs/features/shader-precompilation.md, CLAUDE.md (Traps row + Harmony status table), docs/reference/harmony-patch-registry.md, CHANGELOG.md.

## REQUIRED SECTIONS

### VANILLA CODE
Decompile from the installed 1.4.8 and paste as code blocks: `MissionCustomBattlePreloadView.OnPreMissionTick`, `PreloadHelper.PreloadCharacters` + `PreloadMeshesAndPhysics`, the `Game.Current.PlayerTroop` branch of `Mission.SpawnTroop`, the `_initialPlayerAgent` write in `Mission.BuildAgent`, `DeploymentMissionController.SetupTeams` + `FinishDeployment` + `OnMissionTick`, the BattleSizeAllocating block of `DefaultBattleMissionAgentSpawnLogic`, `CustomBattleInitializationModel.CanPlayerSideDeployWithOrderOfBattleAux` and the `GetNumberOfPlayerControllableTroops` chain, `CustomBattleTroopSupplier.ArrangePriorities`, `OrderOfBattleVM.OnDeploymentFinalized`, `MissionGauntletOrderOfBattleUIHandler.OnDeploymentFinished`, `LoadingWindowViewModel.Update`, the `InitialStateOption` constructor, MCM's json2 load path, and the `{newline}` handling in TaleWorlds.Localization.

### DEEP ANALYSIS (concrete scenarios)
A. Cold walk on a default install (scene passes off): trace Begin -> bootstrap -> discovery -> batches 1..B -> Finish. For each state transition name the engine callback that drives it and the runner guard that protects it. Where can the walk stall, double-start, or finish early? What does the log say in each case?
B. The single player-side agent dies within seconds against ~1,000 enemies: does `BattleEndLogic`, `CustomBattleAgentLogic`, `MissionScreen` or the retreat/scoreboard flow end the mission, pause it, or throw before the shader count settles and the runner ends the item? Does `Mission.MainAgent` becoming null break anything the runner or guard reads?
C. A second walk after a cancelled one (Ctrl+Shift+K): every piece of static/singleton state that must reset (`_active`, `_plan`, `_scenes`, `_index`, `_abortedItems`, `_rosterDiscovered`, `BattleLoadStallWatchdog.SuppressStallDetection`, the crash guard's inflight marker) and whether it does.
D. TAOM mission behaviors added by `SubModule.OnMissionBehaviorInitialize` to the shader battle (AdvancedCombatBehavior, BehaviorTreeMissionLogic, warg/spider/elephant/mumakil behaviors, enlistment/banner-bearer logics, diagnostics): does any of them assume `Campaign.Current != null` or a `PlayerEncounter` and throw in a CustomGame? The same behaviors run in the vanilla Custom Battle menu, so a throw there would be a pre-existing bug; say which.
E. `ShaderPrecompilePlayerAgentGuard` is added only when `IsWalkInProgress` (Starting or Running). Can a real mission initialize while a walk is Starting or Running? What happens if the fallback force-finish fires in a real battle?

### CONFIG CROSS-REFERENCE
- `precompile_scenes.txt` ids vs `PrecompileSceneProvider.DefaultScenes` vs the scenes registered in the live CustomBattle/TAOM `custom_battle_scenes.xml` (only relevant when the opt-in is on).
- The `{=taom_precompile_*}` C# defaults vs the XML rows (must be byte-identical) and the 12 language files (each must carry all four keys).
- `CoopSettingsRelevance` excluded names vs real `TaomSettings` properties.

### FINDINGS OR OBSERVATIONS
Number every finding. Severity P1 (ships a crash, a hang, a silent coverage loss, or a wrong player-visible promise), P2 (wrong under a reachable condition), P3 (quality). For each: file:line, the exact defect, the decompiled evidence, and the minimal fix. Then a verdict: SHIP / SHIP WITH FIXES / DO NOT SHIP.

## QUALITY GATES
- Every engine claim carries a decompiled quote with type and line.
- Every "missing" claim was checked with a grep of the repo.
- Do not flag vanilla-matching behaviour as a bug; say "matches vanilla" instead.
- Do not report style or naming; TAOM has its own linters.

## PRIOR REVIEW LESSONS
SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches; on this feature specifically, a Codex pass traced `EndGame -> Mission.EndMission -> MissionState CleanStates -> Game destroyed` and corrected a deep-review agent's wrong claim that `Game.Current` never nulls after a custom-battle EndGame (2026-06-17), and another confirmed the stale-callback race that the generation tag now guards.
FAILURES: Codex assumed empire=Rohan (it is Dunland); Codex flagged vanilla-matching code as bugs; Codex skipped hard sections; a Codex pass once disputed a render-grace watchpoint on this feature without decompiling the loading-window path.

## OUTPUT
Write the complete review as markdown to stdout (the caller redirects it to docs/reviews/raw/codex-adversarial-shader-precompile-reenable-2026-09-11.md). Start with a one-paragraph verdict, then the numbered findings, then the Known Suspects table (S1..S8: CONFIRMED / DISPUTED / UNVERIFIED with evidence), then the required sections.
