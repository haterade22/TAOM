# Codex Adversarial Review: WeatherBoundsGuard + AtmospherePersistence + ShaderPrecompilation

**Date:** 2026-04-05
**Target:** branch diff against master
**Verdict:** needs-attention

No-ship. WeatherBoundsGuard's three DefaultMapWeatherModel targets match the 1.3.15 decompiled signatures, but ShaderPrecompilation has a behavioral bug (global abort latch stays armed) and an unverified target, and AtmospherePersistence is brittle enough to fail as a silent no-op on minor Mission API drift.

## Section 1: Vanilla Code

### DefaultMapWeatherModel (decompiled v1.3.15)

Weather model methods verified from decompiled source. Signatures match TAOM's patch targets.

### Mission.Initialize (decompiled v1.3.15)

```
private MissionInitializerRecord InitializerRecord { get; set; }  // line 1056
public string SceneName => InitializerRecord.SceneName  // line 1058
// Initialize() begins: MissionInitializerRecord rec = InitializerRecord; MBAPI.IMBMission.InitializeMission(Pointer, ref rec);  // lines 1783-1792
```

### LoadingWindowViewModel.Update

Not found in `E:\Decompiled_Bannerlord\`. Target signature unverified for v1.3.15.

## Section 2: Patch Analysis

### WeatherBoundsGuard

Three postfixes clamp weather values after vanilla computation. Signatures verified against decompiled v1.3.15. Clamping ranges are reasonable defensive bounds. No risk of breaking vanilla weather effects — values are only clamped when they exceed expected ranges (prevents NaN/infinity propagation from TAOM's custom map).

### AtmospherePersistence

Prefix on `Mission.Initialize` uses `AccessTools.Property(typeof(Mission), "InitializerRecord")` to modify the private `MissionInitializerRecord` before vanilla copies it. The reflection target exists in v1.3.15 but is brittle. See Finding 3.

### ShaderPrecompilation

Postfix on `LoadingWindowViewModel.Update` updates loading text with shader progress. Uses `AccessTools.Method(typeof(LoadingWindowViewModel), "Update")` without parameter types. Target unverified. See Finding 2.

## Section 3: Test Coverage Gaps

- **WeatherBoundsGuard:** Zero tests. Low risk — pure clamping postfixes with no business logic. `WeatherBoundsGuardService.Clamp()` could be unit-tested.
- **AtmospherePersistence:** Zero tests. Medium risk — reflection-based prefix with version-sensitive target. The service logic (`ShouldOverrideAtmosphere`, scene name matching) is testable without the game framework.
- **ShaderPrecompilation:** 1 test file covering `ShaderPrecompilationService`. The abort-latch lifecycle (Finding 1) is not tested.

## Findings

### [HIGH] Shader precompilation leaves global abort latch armed after successful run

**File:** `TaomShaderGameManager.cs:38-53`

**TAOM code:** Sets `IsShaderBattleActive = true` in `OnLoadFinished()`, only clears it in the exception path. `LoadingScreen_ShaderProgress_Patch.cs:46-83` gates UI mutation and `MBGameManager.EndGame()` abort on that static bool.

**Evidence:** No success-path reset or teardown hook found. A successful shader-battle startup leaves the process-wide latch armed for later loading windows. An unrelated load that encounters stalled shader compilation can inherit TAOM's text override and 120s forced abort — terminating the wrong session.

**Remediation:** Reset `IsShaderBattleActive` on the normal completion path and on mission/state teardown, or replace with a lifecycle token tied to the specific shader-precompilation load.

### [MEDIUM] Loading-screen patch target unverified for Bannerlord 1.3.15

**File:** `LoadingScreen_ShaderProgress_Patch.cs:11-40`

**TAOM code:** Resolves with `AccessTools.Method(typeof(LoadingWindowViewModel), "Update")` without parameter types, no null/fail-fast handling. Comments cite research against v1.3.12, not the project's current v1.3.15 target.

**Evidence:** `LoadingWindowViewModel` source not found in `E:\Decompiled_Bannerlord\`. If TaleWorlds renamed the method, added overloads, or moved the type, the hook misses entirely or binds the wrong method.

**Remediation:** Decompile the actual 1.3.15 `LoadingWindowViewModel` from the shipping DLL. Bind Harmony using the full method signature with explicit startup validation/logging if the target is missing.

### [MEDIUM] AtmospherePersistence depends on private Mission property — silent no-op on API drift

**File:** `Mission_Initialize_Patch.cs:17-45`

**TAOM code:** Uses `AccessTools.Property(typeof(Mission), "InitializerRecord")` to read/write the private property. Reads `__instance.SceneName`. Mutates boxed `MissionInitializerRecord` via reflection before vanilla copies it.

**Vanilla code:** `private MissionInitializerRecord InitializerRecord { get; set; }` (line 1056). `Initialize()` copies it to a local struct before `MBAPI.IMBMission.InitializeMission`.

**Evidence:** If TaleWorlds renames the property, changes accessibility, or alters `SceneName` routing, the prefix falls into catch/log path. Mission loads with vanilla atmosphere — regression is easy to miss.

**Remediation:** Validate the reflected property once at startup. Treat a missing/incompatible `InitializerRecord` as a hard compatibility failure for this feature, not a swallowed warning.

## Observations

- WeatherBoundsGuard patches are clean and verified against v1.3.15 — lowest risk of the three features
- WeatherBoundsGuard service has testable `Clamp()` logic but zero tests
- AtmospherePersistence scene-matching logic is testable without game framework
- ShaderPrecompilation abort timing test is the highest-value missing test

## Recommended Next Steps

1. Fix shader abort latch — reset `IsShaderBattleActive` on successful completion
2. Decompile and archive 1.3.15 `LoadingWindowViewModel.Update()` for verification
3. Add startup validation for AtmospherePersistence reflection target
4. Add unit tests for `WeatherBoundsGuardService.Clamp()` and `AtmospherePersistenceService` scene matching
