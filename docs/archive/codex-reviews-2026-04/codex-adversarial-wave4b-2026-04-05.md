# Codex Adversarial Review: TimeAcceleration + InitialChildGeneration + StartupResources + MainMenuCustomizer + Encyclopedia + BattleScenes

**Date:** 2026-04-05
**Target:** branch diff against master
**Verdict:** needs-attention

No-ship. The strongest blocker is in InitialChildGeneration: the service computes child sex, but the creation path ignores that sex and falls back to same-clan templates only, so zero-male clans cannot honor the intended forced-male behavior. TimeAcceleration also has a sticky turbo path, and StartupResources is retry-unsafe after partial failure.

## Section 1: Service Logic Review

### Encyclopedia (TaomInformationRestrictionModel)

Decompiled `DefaultInformationRestrictionModel` from v1.3.15. TAOM's override adds settings-driven encyclopedia visibility restrictions. Correctly inherits from the default model and calls `base` for non-overridden paths.

### BattleScenes

Confirmed truly dead: `SubModule.cs:74` comments out `Patch0_BattleScenes`. Only references are the three BattleScenes patch class files. Not registered anywhere.

## Section 2: Config Cross-Reference

- **StartupResources:** Includes all custom culture IDs plus remapped vanilla cultures (`vlandia`, `sturgia`, `battania`, `aserai`, `khuzait`). No invalid `rohan` ID found.
- **InitialChildGeneration:** Excludes `mordor`, `isengard`, `gundabad`, `dolguldur` (no-reproduction cultures). No dead override entries in shipped JSON. Exclusion list matches lore intent.
- **No dead config values** found across any of the six features.
- **No missing cultures** that should have config entries.

## Section 3: Dead Code Detection

- **BattleScenes:** Truly dead. `Patch0_BattleScenes` commented out in `SubModule.cs:74`. 3 patch files with no active registration. Intentionally preserved for future use (scene system rework).
- **No dead methods/properties** found in the other 5 features beyond BattleScenes.

## Findings

### [HIGH] Initial child gender selection is not enforced at creation time

**File:** `ChildCreatorAdapter.cs:11-23`

**TAOM code:** `InitialChildGenerationService` decides `isFemale` and forces first child male when a clan has no adult males. But `ChildCreatorAdapter.CreateChild` never uses its `isFemale` parameter — always calls `HeroCreator.CreateChild(templateHero.CharacterObject, ...)`, so the spawned hero inherits the template's sex.

**Vanilla code:** `InitialChildGenerationCampaignBehavior` searches other clans of the same culture when the requested-sex template is missing. `HeroCreator.CreateChild` does not accept a sex argument or override `IsFemale` after creation.

**Evidence:** TAOM's `SelectTemplate` picks from opposite-sex pool when requested pool is empty. A zero-male clan can still spawn a female child even though `DetermineGender` returned male. The `isFemale` parameter is computed but never acted upon.

**Remediation:** Either make child creation honor the requested sex explicitly, or replicate vanilla's same-culture fallback so the selected template already matches the requested sex before calling `HeroCreator.CreateChild`.

### [MEDIUM] Ctrl+Space turbo can get stuck on map/menu transition

**File:** `TimeAccelerationService.cs:25-43`

**TAOM code:** `OnTick` returns immediately when campaign is inactive, map is inactive, or menu is open. These returns happen before the `_ctrlSpaceActive` restore branch.

**Evidence:** If turbo was enabled and the player transitions off the map or opens a menu before the next eligible tick, `_ctrlSpaceActive` remains set and saved speed/mode are not restored. Campaign stays at turbo multiplier until overwritten. Tests cover normal release but not this state-transition path.

**Remediation:** Run the restore logic before early returns, or add a cleanup path that restores saved state whenever turbo is active and the feature cannot process input normally.

### [MEDIUM] Startup resource distribution is not retry-safe after partial failure

**File:** `StartupResourcesBehavior.cs:29-41`

**TAOM code:** Calls gold distribution first, then influence distribution. Flips `_distributed` only after both succeed. If gold succeeds and influence throws, `_distributed` stays false. Any re-entry runs gold again — permanently duplicating that grant.

**Evidence:** Tests only cover all-success duplicate-call case. No test for partial failure between the two side effects. Irreversible state mutation with no compensation path.

**Remediation:** Make the operation idempotent per subsystem. Persist completion state for gold and influence separately, or guard calls so a retry cannot reapply a committed side effect.

## Observations

- BattleScenes is intentionally preserved dead code (scene system rework) — not a cleanup target
- Config cross-check passed: no invalid culture IDs, no dead config, no missing cultures
- Encyclopedia GameModel correctly inherits and calls `base` for non-overridden paths
- MainMenuCustomizer tests pass — `InitialStateOptionAdapter` correctly maps IDs to TaleWorlds menu options
- InitialChildGeneration exclusion list (mordor, isengard, gundabad, dolguldur) matches lore intent for no-reproduction cultures

## Recommended Next Steps

1. Fix `ChildCreatorAdapter.CreateChild` to honor the `isFemale` parameter or match template sex to request
2. Fix TimeAcceleration turbo restore to run before early returns
3. Make StartupResources idempotent per subsystem for retry safety
4. Add tests for: zero-male clan child gen, Ctrl+Space + map transition, startup resources partial failure
