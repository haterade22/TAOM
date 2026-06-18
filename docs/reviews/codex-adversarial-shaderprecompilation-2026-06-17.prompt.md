Adversarial code review. TAOM is a Bannerlord v1.4.6 total-conversion mod. Review the re-enabled ShaderPrecompilation feature (issue #287). This feature compiles shaders ahead of time to fix an intermittent battle-load CTD/hang (a d3dcompiler access violation while compiling a TAOM battle scene's terrain/forced-atmosphere shaders at Mission.Initialize).

This is an IN-GAME-ONLY feature (ADR-008): the engine orchestration cannot be unit-tested, so SEMANTIC and RUNTIME correctness of the orchestration is the entire concern. API signatures are already verified against installed v1.4.6 -- do NOT spend time on signature checks. Focus on runtime behavior, lifecycle, re-entrancy, and state-machine correctness.

## What the feature does

A "Pre-compile Shaders" main-menu button starts ShaderPrecompileRunner.Begin(). The runner walks a work list:
- Item 0 = CharacterBattle: a custom battle with ALL TAOM troops (up to 3000/side) on scene battle_terrain_029 -- compiles character/equipment shaders.
- Items 1..N = ScenePass per TAOM battle scene (taom_mordor_battle_001..004_forceatmo, etc.) -- a minimal custom battle on that scene so its terrain + forced-atmosphere shaders compile.

For each item the runner calls MBGameManager.StartNewGame(new TaomShaderGameManager(item, ...)). The game manager's OnLoadFinished() builds CustomBattleData and calls CustomBattleHelper.StartGame(data), then calls the static ShaderPrecompileRunner.NotifyItemRendering(). The runner is driven once per frame from SubModule.OnApplicationTick via runner.Tick(). When an item's shader count settles to 0 (the unit-tested ShaderPrecompileDecider decides this), the runner calls MBGameManager.EndGame() and, once back at the menu, StartNewGame's the next item. When all items are done it Finishes.

Progress shows on the loading screen (LoadingScreen_ShaderProgress_Patch mirrors runner.StatusLine onto LoadingWindowViewModel.DescriptionText) and as a 1 Hz toast.

## Prior bug (2026-05-04 RCA) -- do not let it recur

The old loading-screen patch had a sentinel collision: it used _lastShaderCount=-1 as both "uninitialized" and the change-detection comparand, so the engine's first-frame count==0 (before any shader queued) was read as "complete" and the feature disabled itself. The logic is now a pure ShaderPrecompileDecider with a _observedWork flag (completion on remaining==0 requires having seen remaining>0 first). Confirm the collision genuinely cannot recur in the new code, including any NEW sentinel (e.g. _renderStartedMs=-1, _idleSinceMs=-1, _lastChangeMs=-1, _lastRemaining=int.MinValue).

## Known Suspects -- CONFIRM or DISPUTE each with specific evidence

1. TEARDOWN DETECTION (highest concern). ShaderPrecompileRunner.TickEnding gates the advance to the next item on: atMenu = (Game.Current == null && !LoadingWindow.IsLoadingWindowActive). EndGame() is async void. For a CUSTOM battle, CustomGameManager.OnLoadFinished pushes a CustomBattleState and then TaomShaderGameManager opens a mission on top of it. QUESTION: after EndGame() pops the mission, does Game.Current actually become null (clean menu), or does the leftover CustomBattleState (a live CustomGame) keep Game.Current non-null forever -- so the advance ALWAYS falls through to the 30s EndTimeoutMs backstop, AND each new StartNewGame stacks a GameLoadingState on top of an uncleaned CustomBattleState, growing the state stack across ~10 items until something breaks? Read CustomGameManager.OnLoadFinished, MBGameManager.EndGame, and GameStateManager carefully. If Game.Current does not reliably null, propose the correct teardown signal (e.g. checking the active GameState type, or calling EndGame differently).

2. STALE STATIC CALLBACK. TaomShaderGameManager calls the static ShaderPrecompileRunner.NotifyItemRendering()/NotifyItemFailed() which forward to _active?.OnItemRendering(). If a game manager for item N is still loading when the runner has already moved on (timeout abort), its late OnLoadFinished -> NotifyItemRendering could fire while the runner is in Ending/Starting for a DIFFERENT item. OnItemRendering guards with `if (_state != RunState.Starting) return`. CONFIRM this guard fully prevents a stale callback from corrupting the wrong item's clock (_itemStartedMs) or double-transitioning. Also: IsShaderBattleActive is a separate static bool toggled in OnLoadFinished and ResetShaderBattleActive -- trace whether it can be left true after a walk ends.

3. RENDER-GRACE CORRECTNESS. The decider was just changed so the "nothing to compile, advance" grace counts RENDER time (from the first frame LoadingWindow.IsLoadingWindowActive==false), not LOAD time, to avoid skipping a heavy scene that is still loading. QUESTION: is LoadingWindow.IsLoadingWindowActive==false a reliable proxy for "the scene has rendered and shaders have queued"? Could the loading window be DOWN during a non-rendering window (e.g. a brief gap between scene load and deployment, or a UI state) that would set _renderStartedMs too early and let a still-uncompiled scene advance after the 20s grace? Also confirm shaders compiling DURING the loading screen still set _observedWork (the RCA log shows compilation happens under the loading window).

4. EndGame FROM OnApplicationTick / RE-ENTRANCY. runner.Tick() runs every frame. TickRunning, on decider Advance/Abort, calls BeginEnd() which sets _state=Ending then calls MBGameManager.EndGame(). Since EndGame is async void and Tick runs again next frame, confirm BeginEnd cannot be called twice for the same item (the _state switch should route subsequent frames to TickEnding, not TickRunning). Also StartCurrentItem flips _state=Starting BEFORE StartNewGame -- confirm no frame can re-enter StartCurrentItem.

5. CHARACTER BATTLE SCALE. Item 0 loads up to 3000 troops/side (6000 agents). Its legit compile is documented as 20-70 min. The decider's absolute per-item timeout is 90 min and the no-progress (count frozen) timeout is 15 min. QUESTION: during a legit 70-min character-shader compile, can the count legitimately freeze for >15 min on a single very heavy material (the docs note single-threaded compile can hold for "several minutes")? If so the no-progress timeout would prematurely abort item 0. Assess whether 15 min is safe or should be larger.

6. RE-RUN. After Finish() (state=Complete, _active=null), clicking the menu again calls Begin() (IsActive is false at Complete so it proceeds). The decider instance is reused; it is reset per-item via ResetForItem() in StartCurrentItem. Confirm no stale decider/runner state survives a completed walk into a fresh walk.

## Files to review

Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs (the orchestrator -- MAIN focus)
Main/Features/ShaderPrecompilation/ShaderPrecompileDecider.cs (pure state machine)
Main/Features/ShaderPrecompilation/TaomShaderGameManager.cs (per-item battle loader, CustomGameManager subclass)
Main/Features/ShaderPrecompilation/ShaderPrecompilePlanner.cs (pure work-list)
Main/Features/ShaderPrecompilation/PrecompileSceneProvider.cs (scene-list config)
Main/Features/ShaderPrecompilation/Domain/PrecompileItem.cs
Main/Features/ShaderPrecompilation/Hooks/LoadingScreen_ShaderProgress_Patch.cs
Main/Features/ShaderPrecompilation/ShaderPrecompilationIoC.cs
Main/SubModule.cs (ONLY the shader lines: static _shaderRunner field, InitializeHooks call, the re-enabled "Pre-compile Shaders" InitialStateOption menu, and the OnApplicationTick runner driver)
Main/_Module/ModuleData/shader_precompilation/precompile_scenes.txt
TAOM.Tests/Features/ShaderPrecompilation/ShaderPrecompileDeciderTests.cs
TAOM.Tests/Features/ShaderPrecompilation/ShaderPrecompilePlannerTests.cs

IGNORE everything outside ShaderPrecompilation. There is unrelated parallel "AlignmentRecruitment" work in the tree -- do not review it.

## Vanilla references (read from the installed DLLs / your knowledge)

TaleWorlds.MountAndBlade.CustomBattle.CustomGameManager.OnLoadFinished -- what state does it push? does base.OnLoadFinished set IsLoaded + push CustomBattleState?
TaleWorlds.MountAndBlade.MBGameManager.EndGame -- is it async? does it null Game.Current? what does it pop/clean?
TaleWorlds.MountAndBlade.MBGameManager.StartNewGame -- does it push a GameLoadingState? safe to call from a per-frame tick?
TaleWorlds.Core.GameStateManager -- how to tell "we are back at the empty main menu with no game".

## Required output sections

FINDINGS -- each with: severity (HIGH/MEDIUM/LOW), file:line, the concrete runtime scenario that triggers it, and a specific fix. Distinguish "will break the walk" from "wastes time / cosmetic".
KNOWN SUSPECTS VERDICTS -- CONFIRMED or DISPUTED for each of the 6 above, with evidence.
WHAT THE TESTS DO NOT COVER -- the orchestration is in-game-only; call out the highest-risk untested behaviors to watch in the first real 1-2 hour walk.

Be concrete. A finding without a triggering scenario is not actionable. Use -- not em-dash. Output your review as markdown.
