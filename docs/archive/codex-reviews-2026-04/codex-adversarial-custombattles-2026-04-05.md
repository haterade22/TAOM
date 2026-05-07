# Codex Adversarial Review: CustomBattles

**Date:** 2026-04-05
**Target:** working tree diff
**Verdict:** needs-attention

No-ship. Commander resolution is provably dropping TAOM lords from the working data set, and one of the critical Harmony targets is still an exact-match constructor patch with no v1.3.15 proof or runtime assertion.

## Section 1: Vanilla Code

Codex verified `BannerlordMissions.cs` from `E:\Decompiled_Bannerlord\` but could not find decompiled `CustomBattleData.cs`, `CustomBattleSideVM.cs`, or `CustomBattleHelper.cs` in the decompiled source tree. Those targets remain unverified.

## Section 2: Patch Target Verification

- **CustomBattleData.Characters / Factions:** Could not verify — decompiled source not available in `E:\Decompiled_Bannerlord\`. Target implementation (property getter vs method, lazy init vs static list) is unconfirmed.
- **CustomBattleSideVM constructor:** TAOM targets a specific 4-arg overload `(TextObject, bool, TroopTypeSelectionPopUpVM, Action)`. Could not independently confirm this signature exists in v1.3.15. See Finding 2.
- **BannerlordMissions:** Verified from decompiled source. `OpenCustomBattleMission()` and `OpenSiegeMissionWithDeployment()` signatures confirmed.

## Section 3: Data Resolution

### Commander filtering

`CustomBattleService` uses regex `^lord_\d+_\d+$` to filter commanders. This excludes TAOM lords with alphanumeric suffixes. See Finding 1.

### Faction filtering

Factions filtered by `CanHaveSettlement + !IsBandit`. Custom cultures in `taom_spcultures.xml` set `can_have_settlement="true"` where appropriate. No cultures found that should appear but are filtered out.

### Troop formation mapping

Uses culture militia properties (`MeleeMilitiaTroop`, `RangedMilitiaTroop`, etc.). These are defined for all TAOM cultures in the culture XML files.

## Findings

### [HIGH] Commander regex excludes real TAOM lords — factions surface with missing commander choices

**File:** `CustomBattleService.cs:126-134`

**TAOM code:** `CustomBattleService` only accepts commander IDs that match `^lord_\d+_\d+$`.

**Evidence:** In `Main/_Module/ModuleData/lords.xslt`, many hero lords have non-numeric IDs:
- `lord_V11_l` / `lord_V11_c1` / `lord_V11_u` (lines 10489-10835)
- `lord_A9_l` / `lord_A9_u` (lines 13787-13862)
- `lord_NE8_l` / `lord_NE9_l` (lines 14141-14326)
- `lord_SE9_l` / `lord_WE9_l` (lines 14639-14851)

These are `is_hero="true"` lords that can never pass the service filter. Factions backed by these IDs load into Custom Battle without their intended commanders, which is directly user-visible and can cascade into null/empty commander selection.

**Remediation:** Replace the numeric regex with rule-based filtering on hero/occupation/culture, or use an explicit negative filter for known non-commander patterns (`companion`, `wanderer`, `notable`, `tutorial`, `commander_`). Add regression tests covering alphanumeric TAOM lord IDs.

### [MEDIUM] CustomBattleSideVM constructor patch has no v1.3.15 assertion — one overload drift away from silent failure

**File:** `CustomBattleSideVM_Constructor_Patch.cs:12-13`

**TAOM code:** Hardcodes a single constructor signature: `(TextObject, bool, TroopTypeSelectionPopUpVM, Action)`. Harmony resolves at runtime, not compile time.

**Evidence:** `E:\Decompiled_Bannerlord\` does not contain decompiled `CustomBattleSideVM.cs`, so the constructor could not be independently verified. No startup assertion exists that the target method was found or patched. A version-skewed build ships with the constructor patch inert and no strong signal beyond downstream UI behavior being wrong.

**Remediation:** Add explicit startup validation that the expected `CustomBattleSideVM` constructor was found and patched. Capture the exact v1.3.15 signature evidence used for the target. Fail loudly if Harmony cannot bind.

## Observations

- `CustomBattleData` and `CustomBattleHelper` decompiled sources are missing from `E:\Decompiled_Bannerlord\` — these should be decompiled and added for future verification
- `BannerlordMissions` signatures were confirmed from decompiled source
- Faction and troop formation data resolution appears correct for all TAOM cultures

## Recommended Next Steps

1. Fix commander regex to include alphanumeric TAOM lord IDs — add regression tests
2. Decompile `CustomBattleSideVM`, `CustomBattleData`, and `CustomBattleHelper` from v1.3.15 and record exact signatures
3. Add startup validation for the `CustomBattleSideVM` constructor patch binding
