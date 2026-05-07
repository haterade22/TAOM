# Codex Adversarial Review: HeroRace

**Date:** 2026-04-05
**Target:** branch diff against master
**Verdict:** needs-attention

No-ship. The HeroRace patch set changes vanilla action-set selection in a way that can request the wrong animation set for underscore-named monsters, has no migration path for pre-feature saves, and permanently disables dwarf eye-height correction if FaceGen is not ready on the first hit.

## Section 1: Vanilla Code

### FaceGen.GetBaseMonsterFromRace (decompiled)

Read from `E:\Decompiled_Bannerlord\`. Returns the Monster object for a given race ID from the registered monster list.

### ActionSetCode.GenerateActionSetNameWithSuffix (decompiled)

```csharp
return "as_" + (string.IsNullOrEmpty(monster.BaseMonster) ? monster.StringId : monster.BaseMonster) + ... + suffix;
```

Vanilla prefers `monster.BaseMonster` when present, otherwise uses the full `monster.StringId`.

### CharacterTableau / CharacterSpawner

Not found in `E:\Decompiled_Bannerlord\` — target signature validation remains incomplete for these two patch targets.

## Section 2: Patch Interaction Analysis

### a) FaceGen_GetBaseMonsterFromRace

TAOM replaces vanilla monster lookup. Unknown race IDs fall back to the default human monster (race 0). No crash path — graceful degradation.

### b) CharacterTableau patches

Could not verify against decompiled source (files missing from decompiled tree). Patch timing analysis deferred.

### c) ActionSetCode patch — See Finding 1

TAOM truncates `monster.StringId` at the first underscore (`monsterId.Split('_')[0]`), which diverges from vanilla's full-StringId or BaseMonster preference. This can produce wrong animation set keys.

## Section 3: Race Persistence

### a) Persistence mechanism

`RacePersistenceService` uses `SyncData` to serialize `_heroRaceMap` into save files. Race capture happens on `OnBeforeSaveEvent`.

### b) Pre-TAOM save compatibility — See Finding 2

On a save created before the feature, `_taom_heroRaceMap` doesn't exist. The empty-map check causes `RestoreHeroRaces` to exit immediately, leaving all heroes at race 0 (human).

### c) Dead config

No dead config values found. All service methods are called at runtime.

## Findings

### [HIGH] Action-set patch no longer follows vanilla monster lookup semantics — wrong animation sets for underscore-named monsters

**File:** `ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs:17-28`

**TAOM code:** Derives key from `monster.StringId`, truncating at first underscore: `monsterId.Split('_')[0]`.

**Vanilla code:** `"as_" + (string.IsNullOrEmpty(monster.BaseMonster) ? monster.StringId : monster.BaseMonster) + ... + suffix`

**Evidence:** A monster with `StringId = "orc_tracker"` and empty `BaseMonster` resolves to `as_orc_tracker...` in vanilla but `as_orc...` in TAOM. Missing/mismatched action-set key is a plausible route to T-pose or broken animations for non-human visuals.

**Remediation:** Match vanilla: prefer `monster.BaseMonster` when present, otherwise use full `monster.StringId` without underscore truncation. Add tests for monsters with BaseMonster, without BaseMonster, and underscore-containing IDs.

### [HIGH] Race restore is a no-op for pre-feature saves — heroes stay human

**File:** `RacePersistenceService.cs:37-49`

**TAOM code:** `RestoreHeroRaces` exits immediately when `_heroRaceMap` is empty. Map is only populated from serialized save data.

**Evidence:** On a save created before this feature, `_taom_heroRaceMap` doesn't exist. First load has no persisted data. Heroes whose race reset to 0 stay human until an external process fixes them.

**Remediation:** Add a first-load migration/backfill path when `_heroRaceMap` is empty — derive race from authoritative hero metadata (culture/character template). At minimum, detect the empty-map case and log a compatibility warning.

### [MEDIUM] Early FaceGen initialization failure permanently disables dwarf eye-height adjustment

**File:** `EyeHeightAdjustmentHook.cs:35-43`

**TAOM code:** Sets `_initialized = true` before checking whether `GetBaseMonsterFromRace(0)` returned a valid monster. If FaceGen isn't ready, the code logs and returns but never retries because `_initialized` is already true.

**Evidence:** Test `OnGetBaseMonsterFromRace_AfterInitFailure_DoesNotReinitialize` explicitly locks this in. Dwarf eye-height correction stays permanently off for the session — visible camera/framing failure.

**Remediation:** Only mark initialized after a successful fetch of the default monster, or retry until FaceGen is ready. Add a test that verifies recovery after an initial null result.

## Observations

- `CharacterTableau` and `CharacterSpawner` decompiled sources are missing from `E:\Decompiled_Bannerlord\` — target signatures unverified
- Current test set does not cover `ActionSetCode`, `CharacterSpawner`, or `CharacterTableau` patch outcomes
- FaceGen fallback to human (race 0) for unknown race IDs is correct and graceful

## Recommended Next Steps

1. Fix ActionSetCode patch to match vanilla BaseMonster/StringId preference
2. Add first-load migration path for pre-feature saves
3. Fix EyeHeightAdjustmentHook to retry on initialization failure
4. Decompile and add `CharacterTableau.cs` and `CharacterSpawner.cs` to verification tree
5. Add patch-outcome tests for ActionSetCode, CharacterSpawner, CharacterTableau
