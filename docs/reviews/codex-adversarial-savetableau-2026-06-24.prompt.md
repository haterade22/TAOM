# Adversarial Review -- Save/Load Hero Preview CTD Guard (issue #299)

You are an adversarial reviewer. Find real bugs. Confirm or DISPUTE each Known Suspect below with evidence from the actual code. Deep-review (5 Claude agents) already PASSED with no code changes -- your job is to find what they missed or confirm they were right.

Target: Bannerlord v1.4.6 (installed). The E:\Decompiled_Bannerlord\ dump is v1.4.5 -- for signatures prefer ilspycmd against the installed DLLs at "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/" (note BasicCharacterTableau lives in Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll, NOT the main bin).

## What the fix does

Loading a save from the main menu hard-crashed with a native System.AccessViolationException in BasicCharacterTableau.RefreshCharacterTableau when the save's character was a custom (non-human) race. The Load Game hero preview builds the body via the agentless native MBAgentVisuals.FillEntityWithBodyMeshesWithoutAgentVisuals on the human skeleton, and the native static-morph build dereferences a null morph-data pointer for a custom-race head that lacks per-face-component morph data (same crash class as issue #295). A native AV is a corrupted-state exception so it cannot be try/caught -- the fix is preventive.

The fix: a Harmony Prefix on the private BasicCharacterTableau.RefreshCharacterTableau coerces the private _race field (Harmony parameter `ref int ____race`, four underscores) to the human base race (0) for any race not on an allow-list, before the native build reads it.

## TAOM ID note

Race is an int. Race 0 = vanilla human base (FaceGen.GetBaseMonsterFromRace(0); BasicCharacterTableau.ResetProperties sets _race=0). TAOM custom races (dwarf=1, elf=2, orc, goblin, ...) are non-zero and defined in monsters.xml. All TAOM/LOTRLOME custom heads lack the per-face-component morph data vanilla human heads carry.

## READ FIRST

- docs/features/hero-race.md (section "Save/Load Hero Preview CTD Guard (2026-06-24)")
- The 5 changed files (below)
- E:\LOTRAOMAssets\taom_crash_20260624_183532_c70614e2\report.txt (the crash report)

## Files in scope

- Main/Features/HeroRace/IBasicTableauRaceGuard.cs (new)
- Main/Features/HeroRace/BasicTableauRaceGuard.cs (new)
- Main/Features/HeroRace/Hooks/BasicCharacterTableau_RefreshCharacterTableau_Patch.cs (new)
- Main/Features/HeroRace/HeroRaceIoC.cs (modified -- register guard + Initialize patch)
- TAOM.Tests/Features/HeroRace/BasicTableauRaceGuardTests.cs (new)

Context files (do not need changes, read for verification): Main/SubModule.cs (Patch2_RefreshTableau application), Main/IoC.cs (HeroRace registration order), Main/Features/HeroRace/Hooks/CharacterTableau_RefreshCharacterTableau_Patch.cs (sibling patch using ____race), Main/Features/HeroRace/Hooks/FaceGen_GetBaseMonsterFromRace_Patch.cs (Initialize pattern), Main/Core/Domain/IRaceManager.cs.

## VANILLA CODE (installed v1.4.6, relevant excerpts)

BasicCharacterTableau.RefreshCharacterTableau -- the crash site (decompiled):

```
private void RefreshCharacterTableau()
{
    if (!_initialized) { return; }
    _currentEntityToShowIndex = (_currentEntityToShowIndex + 1) % 2;
    GameEntity val = _currentCharacters[_currentEntityToShowIndex];
    val.ClearEntityComponents(true, true, true);
    // ... equipment meshes added ...
    if (!string.IsNullOrEmpty(_skeletonName))
    {
        AnimationSystemData hardcoded = AnimationSystemData.GetHardcodedAnimationSystemDataForHumanSkeleton();
        bool flag = ((BodyProperties)(ref _bodyProperties)).Age >= 14f && _isFemale;
        val.Skeleton = MBSkeletonExtensions.CreateWithActionSet(ref hardcoded);
        // ... builds equipment meshes from _equipmentMeshes[] ...
        SkinGenerationParams val6 = default(SkinGenerationParams);
        ((SkinGenerationParams)(ref val6))..ctor((int)_skinMeshesMask, _underwearType, (int)_bodyMeshType,
            (int)_hairCoverType, (int)_beardCoverType, (int)_bodyDeformType, true, _faceDirtAmount,
            flag ? 1 : 0, _race, false, false, 0);
        MBAgentVisuals.FillEntityWithBodyMeshesWithoutAgentVisuals(val, val6, _bodyProperties, val3);  // <-- AVs here
        // ...
    }
}
```

_race is parsed in DeserializeCharacterCode: `_race = int.Parse(array[num]);` and reset in ResetProperties: `_race = 0;`. _race is the ONLY race input to this method.

SkinGenerationParams (TaleWorlds.MountAndBlade) ctor parameter order:

```
public SkinGenerationParams(int skinMeshesVisibilityMask, Equipment.UnderwearTypes underwearType,
    int bodyMeshType, int hairCoverType, int beardCoverType, int bodyDeformType, bool prepareImmediately,
    float faceDirtAmount, int gender, int race, bool useTranslucency, bool useTesselation, int faceCacheID)
```

MBAgentVisuals.FillEntityWithBodyMeshesWithoutAgentVisuals is a thin P/Invoke -- the body is native (opaque):

```
public static void FillEntityWithBodyMeshesWithoutAgentVisuals(GameEntity entity, SkinGenerationParams skinParams,
    BodyProperties bodyProperties, MetaMesh glovesMesh)
{
    MBAPI.IMBAgentVisuals.FillEntityWithBodyMeshesWithoutAgentVisuals(entity.Pointer, ref skinParams, ref bodyProperties, glovesMesh);
}
```

## Known Suspects -- CONFIRM or DISPUTE each with evidence

1. COERCION SUFFICIENCY (highest priority). The Prefix coerces only _race. _bodyMeshType, _bodyDeformType, _skinMeshesMask are still the custom char's values from the save code. With _race=0 (human), can the native build still AV on a custom _bodyMeshType / _bodyDeformType / _skinMeshesMask value? The native fn is opaque -- reason from the SkinGenerationParams field semantics: is _race the sole selector of the head mesh whose missing morph data causes the AV, or do the other fields independently index race-specific mesh/morph tables? If you believe coercing _race alone is insufficient, state exactly which additional field(s) must be coerced and to what safe value (note ResetProperties sets _bodyMeshType=(BodyMeshTypes)0, _bodyDeformType=(BodyDeformTypes)0, _skinMeshesMask=(SkinMask)0). If you believe it IS sufficient, justify why.

2. HARMONY PRIVATE-FIELD REF-WRITE. The Prefix signature is `static void Prefix(ref int ____race)`. Field is `private int _race`. Confirm: (a) four underscores is correct for field _race (three Harmony + literal `_race`), (b) `ref` lets the Prefix write the field so the original method body reads the coerced value, (c) the sibling CharacterTableau_RefreshCharacterTableau_Patch uses the same `____race` form (cross-check). DISPUTE if the underscore count or ref semantics are wrong (this would silently no-op the fix).

3. PATCH CATEGORY. The patch uses [HarmonyPatchCategory("Patch2_RefreshTableau")] and no SubModule.cs change was made. Confirm SubModule.cs already calls `_harmony.PatchCategory("Patch2_RefreshTableau")` (it hosts the sibling CharacterTableau patch). DISPUTE if the patch would be dead because the category is unregistered or the string mismatches.

4. INIT-BEFORE-USE ORDERING. The patch reads a static `_guard` set by Initialize() called from HeroRaceIoC.RegisterHeroRaceFeature (inside IoC.Configure in OnSubModuleLoad). The patch is applied later via PatchCategory in OnGameInitializationFinished. The Prefix null-guards (_guard==null returns without coercing). Confirm no window exists where a BasicCharacterTableau renders with _guard still null (BasicCharacterTableau is only used on the Load Game menu screen, reached long after module load). DISPUTE if there is a reachable null-guard window that leaves the crash unguarded.

5. BLAST RADIUS. The fix claims BasicCharacterTableau is instantiated ONLY by SaveLoadHeroTableauTextureProvider, so coercing race->human is contained to the Save/Load preview and does NOT affect the in-game inventory / character-creation screens (which use the separate CharacterTableau class, AgentVisuals path). Verify by decompiling the installed Modules/Native GauntletUI + View DLLs (grep for `new BasicCharacterTableau`). DISPUTE if any other screen instantiates BasicCharacterTableau (that would be an unintended visible regression).

6. SECOND SOURCE OF TRUTH. BasicTableauRaceGuard hardcodes `HumanBaseRace = 0` + an allow-list `TableauSafeRaces = { 0 }`, independent of RaceManager's dynamic id->name mapping. Deep-review rated this LOW/won't-fix (the engine itself hardcodes race 0 as base everywhere -- ResetProperties, the hardcoded human skeleton). CHALLENGE that verdict: is coupling to literal 0 actually a defect here, or correct? If you think it should consult IRaceManager/a shared constant, justify the concrete failure scenario.

## REQUIRED OUTPUT SECTIONS

- KNOWN SUSPECTS: one CONFIRMED/DISPUTED verdict per suspect above, with evidence (file:line or decompiled signature).
- ADDITIONAL FINDINGS: anything the 6 suspects + deep-review missed (test gaps, edge cases, a reachable code path, a convention break).
- TEST REVIEW: are BasicTableauRaceGuardTests non-vacuous (would they fail if ResolveSafeRace returned the input race)? Any missing case?
- FINDINGS OR OBSERVATIONS: severity-ranked (HIGH/MED/LOW) list with file:line + concrete fix for each.

## QUALITY GATES

- Verify claims against the ACTUAL code in scope -- do not assume.
- For the native-opaque suspect (#1), reason explicitly from field semantics; flag your confidence level. The in-game test is the final arbiter, but give your best technical judgment on whether _race-only coercion is sufficient.
- Do NOT flag the 9 pre-existing VolunteerRecruitment spider test failures -- they are unrelated in-flight spider-mount work, out of scope.
- Do NOT flag vanilla-matching code as a bug.

## Prior review lessons

SUCCESSES: vanilla decompilation caught missing gates; lifecycle tracing caught stale caches; cross-referencing the engine consumer caught cross-entity propagation bugs the per-file agents missed.
FAILURES: Codex has flagged vanilla-matching code as bugs; Codex has skipped hard/opaque sections -- do NOT skip suspect #1 just because the native fn is opaque, reason about it.

Write your review to docs/reviews/codex-adversarial-savetableau-2026-06-24.md
