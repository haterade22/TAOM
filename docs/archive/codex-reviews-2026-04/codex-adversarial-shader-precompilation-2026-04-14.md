# Codex Adversarial Review: ShaderPrecompilation

**Date:** 2026-04-14
**Reviewer:** Codex (GPT-5.4)
**Verdict:** ISSUES FOUND
**Score:** CRITICAL: 0 | HIGH: 1 | MEDIUM: 2 | LOW: 2

---

## Findings

### [HIGH] Missing test coverage for GetCultureIdsForShaderBattle exception path
**File:** TAOM.Tests/Features/ShaderPrecompilation/ShaderPrecompilationServiceTests.cs:132
**Rule:** ADR-008

`ShaderPrecompilationService.GetCultureIdsForShaderBattle()` has an exception-handling branch at Main/Features/ShaderPrecompilation/ShaderPrecompilationService.cs:40 that is not exercised by any test, so the service no longer meets the required 100% coverage bar.

**Fix:** Add a test that makes `GetAllCultureInfos()` throw and asserts empty result plus `LogError()`.

---

### [MEDIUM] Troop cap overflow — silent character loss
**File:** Main/Features/ShaderPrecompilation/TaomShaderGameManager.cs:103
**Category:** Shader coverage completeness

Characters are silently dropped once neither side can fit the next unit's copy count. With `MaxTroopsPerSide=2000` and `SoldierCopies=4`, the battle caps at 4000 total members and at most 1000 unique soldiers, while TAOM XML currently contains about 2364 `<NPCCharacter>` definitions across `characters/` and `troops/`.

**Fix:** Split coverage across multiple battles or batch by unique visual/loadout coverage instead of truncating the tail of `characterIds`.

---

### [MEDIUM] Loading screen patch static state not reset between runs
**File:** Main/Features/ShaderPrecompilation/Hooks/LoadingScreen_ShaderProgress_Patch.cs:29
**Category:** Lifecycle state reset

`_lastShaderCount`, `_stuckSinceMs`, and `_abortTriggered` are static and are never reset when a new shader battle starts. If a retry begins with the same remaining count as the previous run, the patch reuses stale stuck/abort state and can suppress the initial update or skip the abort path entirely.

**Fix:** Reset all three fields when starting and ending a shader battle.

---

### [LOW] SubModule static shader fields not reset
**File:** Main/SubModule.cs:69
**Category:** Lifecycle state reset

`_shaderTickAccumulator` and `_lastShaderCount` are static and never reset, so a repeated shader-precompile run can suppress the first out-of-loading-window toast when the new count matches the previous session's cached value.

**Fix:** Reset both fields when launching or finishing shader precompilation.

---

### [LOW] Early return in OnApplicationTick blocks future tick work
**File:** Main/SubModule.cs:440
**Category:** Tick flow

`if (_shaderTickAccumulator < 1f) return;` exits the whole method. It is functionally safe today, but any future tick work appended below this block will be skipped on most frames.

**Fix:** Gate only the shader notification block instead of returning from `OnApplicationTick()`.

---

## Known Suspects — Verdicts

| # | Suspect | Verdict | Details |
|---|---------|---------|---------|
| 1 | TROOP CAP OVERFLOW | **CONFIRMED** | Silent loss happens in TaomShaderGameManager.cs:103. It only drops characters when neither side can fit the next copy count, but that still matters because later characters never contribute shader coverage. |
| 2 | EARLY RETURN IN TICK | **CONFIRMED** | Real maintainability risk, low current impact. |
| 3 | DIALOG CLOSURE RACE | **DISPUTED** | In vanilla, `GauntletQueryManager` dispatches one inquiry action path per tick (`if/else if` at GauntletQueryManager.cs:123-131), and `SingleQueryPopUpVM` invokes one callback then closes the query (SingleQueryPopUpVM.cs:85-94). No engine evidence that both confirm and cancel fire from one inquiry. |
| 4 | STATIC STATE LIFETIME | **CONFIRMED** | The `SubModule` statics can suppress the first toast, and the loading-screen patch statics are the more serious retry bug. |
| 5 | HASHSET TO LIST CONVERSION | **DISPUTED** (as a problem) | `.ToList()` is necessary under the current `IReadOnlyList<string>` contract because `HashSet<string>` does not implement that interface. A better contract would be `IReadOnlyCollection<string>` if ordering is irrelevant. |

---

## Lifecycle Trace

1. Main menu option is added in Main/SubModule.cs:176.
2. Clicking it shows an `InquiryData` confirmation dialog in Main/SubModule.cs:184.
3. Confirm calls `MBGameManager.StartNewGame(new TaomShaderGameManager(...))` in Main/SubModule.cs:191; vanilla `MBGameManager.StartNewGame` creates `GameLoadingState` and pushes it (MBGameManager.cs:46-52).
4. After loading, TaomShaderGameManager.cs:42 sets `IsShaderBattleActive = true`, builds the custom battle, and calls `CustomBattleHelper.StartGame(data)`.
5. While the loading window is active, the Harmony postfix on `LoadingWindowViewModel.Update()` updates the loading text with remaining shader count in LoadingScreen_ShaderProgress_Patch.cs:41.
6. If the count stays unchanged for 30s it warns; at 120s it calls vanilla `MBGameManager.EndGame()` to abort (MBGameManager.cs:182-214).
7. When the loading window is not active, Main/SubModule.cs:443 shows toast-style progress messages instead.
8. Completion path: when remaining count reaches `0` during loading, the patch clears `IsShaderBattleActive`; there is no automatic "return to menu" on success, only on abort. Success leaves the user at deployment, matching the dialog text.

---

## Additional Notes

- **No simultaneous messages:** `LoadingScreen_ShaderProgress_Patch` and `SubModule.OnApplicationTick` do not emit shader messages simultaneously. The patch runs during loading-window updates; `SubModule` explicitly gates on `!LoadingWindow.IsLoadingWindowActive`.
- **Bandit filter removal does change behavior:** `GetValidCultureIds()` no longer excludes `IsBandit` cultures in ShaderPrecompilationService.cs:60, and the updated tests now assert bandit inclusion.
- **Estimated load size:** Maximum 4000 total troops in the battle. In unique-character terms that is at most 1000 soldiers if everything added is a soldier, or up to 4000 heroes if everything added is non-soldier.
