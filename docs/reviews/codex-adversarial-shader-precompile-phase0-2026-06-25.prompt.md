# Codex adversarial review -- shader pre-compile Phase 0 crash-mitigation (#287)

You are an adversarial reviewer. Read the ACTUAL files in this repo. Verify each Known Suspect as CONFIRMED or DISPUTED with file:line evidence. Use `--` not the em-dash. This is a small, self-contained C# changeset (MCM toggles + a fallback-list sync + post-crash user guidance) on an existing feature. No new Harmony patch, no GameModel, no config IDs. The only TaleWorlds surface touched is `InitialStateOption.isHidden` (a `Func<bool>`) and `InformationManager.DisplayMessage` -- both pre-existing API, verified present in installed v1.4.6.

## Background

The "Pre-compile Shaders" main-menu walk hard-crashes the PROCESS (native AV in the engine's `pbr_terrain` vista permutation -- `normalize()` of a `lerp`-to-zero normal, `Shaders/Sources/terrain_pixel_functions.rsh:818`) while loading certain TAOM `_forceatmo` scenes on SOME GPUs. The existing `ShaderPrecompileCrashGuard` self-heals but records only ONE crashing scene per crash -> restart -> relaunch cycle, so an affected GPU forced up to ~12 restarts. This Phase 0 changeset does NOT build the native guard (that is gated on a real fault offset); it ships the immediate mitigations + the crash-data capture that unblocks the native work.

## What changed (6 files -- ignore all other uncommitted work in the tree)

- `Main/Features/ShaderPrecompilation/PrecompileSceneProvider.cs` -- `DefaultScenes` (baked fallback used when `precompile_scenes.txt` is missing/empty) had the 6 Mordor open-field scenes UNCOMMENTED while the live config disables them (fallback drift). Commented them out so the fallback mirrors the live config.
- `Main/Features/TaomSettings.cs` -- new MCM group "Graphics/Shader Precompilation" (GroupOrder 15): `EnableShaderPrecompilation` (master) + `EnableScenePassPrecompilation` (both bool, default true).
- `Main/SubModule.cs` -- the existing `TaomPrecompileShaders` `InitialStateOption` registration: `isHidden:` changed from `null` to `() => !(Features.TaomSettings.Instance?.EnableShaderPrecompilation ?? true)`.
- `Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs` -- `Begin()` reads `EnableScenePassPrecompilation`; when off, the scene list is `Array.Empty<string>()` so the plan is character-battle-only. New `ShowCrashCaptureToast(int)` helper, called from `Begin()` when the crash skip-set is non-empty.
- `Main/Features/ShaderPrecompilation/ShaderPrecompileCrashGuard.cs` -- the fresh-crash branch of `ConsumeAndGetSkipSet()` got an extra `_logger.LogWarning` with Windows Event Viewer export steps (log-only; the guard stays TaleWorlds-free).
- `TAOM.Tests/Features/ShaderPrecompilation/ShaderPrecompilePlannerTests.cs` -- replaced `DefaultScenes_IncludesTheCrashScene` with `DefaultScenes_ExcludesDisabledCrashScenes` (asserts the 9 disabled crashers are absent) + `DefaultScenes_IncludesActiveSiegeScene`.

## READ FIRST

- `Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs` (Begin + ShowCrashCaptureToast + the per-frame Tick state machine, to confirm nothing new is on the hot path)
- `Main/Features/ShaderPrecompilation/ShaderPrecompilePlanner.cs` (BuildPlan ALWAYS prepends the character battle)
- `Main/Features/ShaderPrecompilation/PrecompileSceneProvider.cs` (DefaultScenes + GetScenes fallback)
- `Main/_Module/ModuleData/shader_precompilation/precompile_scenes.txt` (the live config -- the source of truth DefaultScenes must mirror)
- `Main/Features/ShaderPrecompilation/ShaderPrecompileCrashGuard.cs` (MarkLoading / ClearLoading / ConsumeAndGetSkipSet lifecycle)
- `Main/Features/TaomSettings.cs` (the new group + the existing settings pattern; note EVERY MCM hint in this file is plain English, no `{=KEY}`)
- `docs/features/shader-precompilation.md`

## Known Suspects -- CONFIRM or DISPUTE each with file:line evidence

1. **Direct settings read in the runner.** `ShaderPrecompileRunner.Begin()` reads `TAOM.Features.TaomSettings.Instance?.EnableScenePassPrecompilation ?? true` directly, NOT via an injected `*SettingsProvider`. ~30 TAOM features wrap MCM reads in a provider, BUT that is the SERVICE pattern; boundary classes read `TaomSettings.Instance` directly by precedent -- grep confirms `Main/Features/TroopWeight/Hooks/*` (8 Harmony patches), `Main/Features/SettlementFood/Models/TaomSettlementFoodModel.cs`, `Main/Features/PartyIconScale/PartyIconScaleConfig.cs` all read it directly. `ShaderPrecompileRunner` is documented as an ADR-008 engine boundary (game-only, NOT unit-tested). Is adding `IShaderPrecompilationSettingsProvider` (interface + class + IoC reg) justified for two bool reads in an untested boundary, or is the direct read consistent with the boundary-class precedent + simplicity-criterion (tiny win + added complexity = reject)? Give your verdict with the strongest argument either way.

2. **Master-toggle promise.** `EnableShaderPrecompilation` is read ONLY in the `isHidden` lambda (`SubModule.cs`). The ONLY call site of `_shaderRunner.Begin()` is the menu option's `action` (confirm by grepping all of `Main/` for `.Begin()`). So toggling off hides the option and blocks NEW walks, but does NOT abort a walk already running (the per-frame `OnApplicationTick` keeps calling `runner.Tick()` while `IsActive`, with no mid-walk re-check). The HintText was tightened to: "the option is hidden so no NEW walk can be started (a walk already in progress finishes -- it is not aborted mid-flight)." Confirm the implementation now matches the promise, and judge whether leaving a running walk un-abortable is acceptable (it is a deliberate multi-hour user action; aborting on an MCM flip is arguably wrong). Is this LOW or higher?

3. **Scene-pass gate airtightness.** With `EnableScenePassPrecompilation` off: `Begin()` sets `scenes = Array.Empty<string>()`, the `skip.Count > 0` filter runs on the empty array (no-op), `BuildPlan([])` returns a 1-item plan (character battle only, since `BuildPlan` unconditionally prepends it). `StartCurrentItem` only calls `MarkLoading` for `ScenePass` items, so no inflight marker is written. Trace EVERY path and confirm NO scene is loaded when the toggle is off. Flag any leak.

4. **Toast coherence across toggle states.** `ShowCrashCaptureToast(skip.Count)` fires in `Begin()` whenever `ConsumeAndGetSkipSet()` returns a non-empty set (a persisted `shader-precompile-crashed-scenes.txt`), INDEPENDENT of the scene-pass toggle. Note `ConsumeAndGetSkipSet` is called BEFORE the scene-pass gate, so a prior crash is still recorded + the inflight marker consumed even when scene passes are now off. Confirm: (a) a prior crash is still recorded when scene passes are off; (b) the toast wording (now "N scene(s) crashed your GPU on a previous shader pre-compile...") is accurate in BOTH toggle states (it was reworded away from "is skipping N scene(s)" which was misleading when passes are off). Flag any residual incoherence.

5. **DefaultScenes vs live config -- zero drift.** Read BOTH `PrecompileSceneProvider.DefaultScenes` AND `Main/_Module/ModuleData/shader_precompilation/precompile_scenes.txt`. Enumerate the ACTIVE (uncommented) scene id set in each. They MUST be identical: 12 active (`taom_gondor_castle_001/002/003`, `taom_gondor_town_minas_tirith/osgiliath_w/osgiliath_e/lossarnach`, `taom_isengard_town_orthanc`, `taom_gondor_village_001/002/003/004`), with 6 Mordor + 2 Rohan open-field + `taom_rohan_castle_helms_deep` commented out in BOTH. Flag ANY scene active in one but not the other (case-insensitive). Confirm `DefaultScenes_ExcludesDisabledCrashScenes` pins the right 9-scene exclusion set.

6. **Localization.** The new in-game toast (`ShowCrashCaptureToast`) is plain English, not `{=KEY}`-wrapped. CLAUDE.md says wrap player-facing text. BUT: confirm by reading the feature that its OTHER player-facing toasts are ALSO plain English -- the `Finish()` completion toast ("Shader pre-compilation COMPLETE..."), the per-second `StatusLine` toast (`OnApplicationTick`), the loading-screen status (`LoadingScreen_ShaderProgress_Patch`), and the inquiry-dialog body (`SubModule.cs`). Also confirm EVERY MCM hint in `TaomSettings.cs` is plain English. Given that precedent, is localizing ONLY the new toast the right call (creating a half-localized feature), or is matching the established plain-English precedent (and localizing the WHOLE feature as a separate task, if at all) the correct decision? The crash-guard `LogWarning` is a LOG line, never localized -- confirm that is correct as-is.

## Also look for what we might have missed

- Any path where the `isHidden` lambda captures problematic state (it should capture nothing -- pure static read).
- The menu option is registered once (`GetInitialStateOptionWithId(...) == null` guard) -- confirm `isHidden` is the only dynamic gate and nothing re-registers it.
- Case-sensitivity: scene ids are matched case-insensitively in the skip set; the new DefaultScenes entries are lowercase -- confirm no mismatch.
- `Array.Empty<string>()` typing: `IReadOnlyList<string> scenes = includeScenePasses ? _sceneProvider.GetScenes() : Array.Empty<string>();` -- confirm this compiles + behaves (it built green, but confirm the ternary common-type is `IReadOnlyList<string>`).
- Anything new added to the per-frame `Tick()` path (there should be nothing -- all new work is in `Begin()`, once per walk).
- Whether the `EnableScenePassPrecompilation` off-path should ALSO suppress the crash-capture toast (it currently fires; is that desirable as a standing "send us the data" reminder, or noise?).

## Output

A findings section (N CRITICAL / N HIGH / N MED / N LOW), a per-suspect CONFIRM/DISPUTE table with file:line evidence, and a verdict (SHIP / NEEDS-FIX). The TAOM position is that this is a small, well-tested mitigation changeset; the highest-value outcome is either confirming the 2 LOW data-flow items are correctly handled or finding a scene-load leak / a toggle-promise gap we missed. Try hard to find a path where a scene still loads with scene passes off, or where the master toggle's promise is broken in a way worse than "running walk finishes."
