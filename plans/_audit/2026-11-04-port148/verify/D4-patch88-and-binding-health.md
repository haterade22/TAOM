# D4: Patch88 BHA0001 and engine-binding health on v1.4.8

Run `2026-11-04-port148`, verifier D4 (adversarial, read-only). Refs: `origin/bannerlord-1.4.5` = `1b00d1acd`
(1.4.x tip), `origin/fix/public-armory-ids-145` = `8df90444c` (public 1.4.8 build), `origin/bannerlord-1.5.x` =
`e45f6df2c`. Engine: installed Bannerlord v1.4.8 build 119303 at `BANNERLORD_GAME_DIR`; taom-src cache
`C:\Users\mikew\.taom-src\v1.4.8`; dump `C:\Decompiled_Bannerlord\_categories_v1.4.8`. Harmony: Lib.Harmony 2.4.2
(`~/.nuget/packages/lib.harmony/2.4.2/lib/net472/0Harmony.dll`), the version players run (E3 line 44 and E4 line 43
log `0Harmony v2.4.2.0`). `SP` = the run scratchpad named in brief.md.

## Verdicts

| # | Claim under test | Verdict |
|---|---|---|
| 1 | BHA0001 at `Patch88_InitializeLordPartyPropertiesScope.cs(22,2)` means the target is missing on v1.4.8 | **REFUTED**. Analyzer false positive caused by its nested-type lookup (section 2) |
| 2 | Harmony binds Patch88 at runtime on v1.4.8 | **CONFIRMED** three ways: Harmony's own resolver offline, TAOM's gate, and both player logs (section 3) |
| 3 | If Patch88 failed, later categories are skipped on 1.4.x without plan 009 | **CONFIRMED** for the TAOM side (33 later categories, ManualPatchApplicator, watchdog, sampler, census). The final player-visible outcome is **UNVERIFIED** on 1.4.8 (section 4) |
| 4 | 1.5.x changed Patch88 | **REFUTED**. Scope files identical on both tips, one doc-comment edit, the warning was never suppressed; 1.5.x fixed the blast radius instead (plan 009) (section 5) |
| 5 | Some Harmony target or reflection site on 1.4.x is missing on the installed v1.4.8 engine | **REFUTED**. None found across 237 patch classes and sites and every statically resolvable reflection site. Drift exists only in TAOM's own snapshot, catalogue and gate semantics (section 6) |

No runtime fix is needed for Patch88. The value is in hygiene (silence the false positive) and in porting plan 009,
which the maintainer already chose (DECISIONS #28, #59 on the 1.5.x ref).

## 1. What Patch88 targets on v1.4.8

Category `Patch88_LordPartyTemplate` (const at `Main/Features/LordPartyTemplates/Hooks/Patch88_LordPartyTemplate.cs:52`),
three classes, applied once and unguarded at `Main/SubModule.cs:1465` (same line on `origin/bannerlord-1.4.5` and on
`origin/fix/public-armory-ids-145`). Added by `6bf93e511` (2026-09-12, #580), so it ships in the public build.

| Class (origin/bannerlord-1.4.5) | Harmony target | v1.4.8 evidence (taom-src cache file:line) |
|---|---|---|
| `Patch88_LordPartyTemplate` (`.cs:48`; Postfix `(Clan __instance, ref PartyTemplateObject __result)`) | `Clan.DefaultPartyTemplate` getter | `TaleWorlds.CampaignSystem.Clan.cs:113-123` |
| `Patch88_SpawnLordPartyScope` (`.cs:24`; Prefix `(Hero hero, out Hero? __state)`, Finalizer) | private `HeroSpawnCampaignBehavior.SpawnLordParty(Hero hero, bool isNewGame)` | `TaleWorlds.CampaignSystem.CampaignBehaviors.HeroSpawnCampaignBehavior.cs:246`, one declaration (`:261` is a call to `MobilePartyHelper.SpawnLordParty`) |
| `Patch88_InitializeLordPartyPropertiesScope` (`.cs:22`; Prefix `(Hero owner, out Hero? __state)`, Finalizer) | `LordPartyComponent.InitializationArgs.InitializeLordPartyProperties(MobileParty mobileParty, Hero owner)` | `TaleWorlds.CampaignSystem.Party.PartyComponents.LordPartyComponent.cs:14` (nested `public class InitializationArgs`), `:29-47` (method), `:127-131` (called from `OnMobilePartySetOnCreation`), `:134-138` (`CreateLordParty` stores the args) |

## 2. Why BHA0001 fires: an analyzer bug, not engine drift

- It is the only BHA warning in the build: `SP\build\origin145.build.log` lines 4 and 2260 (`grep -c BHA` = 2, one
  warning printed twice). The message names type `TaleWorlds.CampaignSystem.Party.PartyComponents.InitializationArgs`,
  which does not exist: the containing type `LordPartyComponent` was dropped.
- It is the only nested-type patch target in TAOM. `git grep -E "HarmonyPatch\(typeof\([A-Za-z_]+\.[A-Za-z_]+" origin/bannerlord-1.4.5 -- Main`
  returns this attribute and `typeof(SandBox.MapScene)`, which is namespace-qualified, not nested.
- Mechanism, from `ilspycmd` on `~/.nuget/packages/butr.harmony.analyzer/1.0.1.50/analyzers/dotnet/cs/BUTR.Harmony.Analyzer.dll`
  (pinned at `Main/TAOM.csproj:112`):
  - `MetadataHelper.FindTypeDefinition` builds `"{ContainingNamespace}.{MetadataName}"` and compares it with
    `TypeDefinition.Namespace + "." + Name`.
  - In the installed `TaleWorlds.CampaignSystem.dll` (read with System.Reflection.Metadata), all seven nested
    `InitializationArgs` TypeDefs (rows 1730-1736: Garrison, Lord, Villager, Bandit, Caravan, Militia and Custom party
    components) carry an empty Namespace, so that comparison can never match a nested type. The dump agrees: seven
    nested `InitializationArgs` classes under `Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\`
    and no top-level one.
  - `CodeMetadataParser.FindMember` returns a type-not-found diagnostic when that lookup fails, and
    `MemberHelper.FindAndReportForAnyTypeMember` (MethodType.Normal) runs the Field, Property and Method lookups and
    reports BHA0001 when all three return diagnostics, naming the type with `NameFormatter.ReflectionName`
    (namespace plus simple name). That is exactly the flattened name in the message.
- The attribute uses `nameof(LordPartyComponent.InitializationArgs.InitializeLordPartyProperties)`, which only compiles
  if the member exists in the referenced assemblies, and `Main/TAOM.csproj:40-58` references the installed game DLLs.

## 3. Does Harmony bind it at runtime? Yes

1. **Harmony's own resolver, offline.** I loaded the orchestrator's origin-tip export build
   (`SP\build\origin145\TAOM.Tests\bin\Debug\net472\TAOM.dll`, from `1b00d1acd`) with the installed v1.4.8 managed
   assemblies and Harmony 2.4.2, constructed `PatchClassProcessor(harmony, type)` per patch class (attribute merge only,
   nothing patched) and resolved each patch method's merged `HarmonyMethod` with Harmony's internal
   `PatchTools.GetOriginalMethod`, which uses `AccessTools.DeclaredMethod` / `DeclaredPropertyGetter`. All three Patch88
   classes resolve to the methods in section 1, declared on the named types, and their non-special parameters
   (`owner`, `hero`) exist on the originals.
2. **TAOM's gate.** `TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs:81-98` resolves
   `LordPartyComponent+InitializationArgs.InitializeLordPartyProperties` and pins `mobileParty`, `owner`; `:60-79` pins
   `SpawnLordParty` (private, one overload, `hero`, `isNewGame`). The orchestrator's origin-tip run (`SP\build\summary.txt`,
   `SP\build\origin145.test.log`) failed only the four repo-root tests and skipped only
   `WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack` and
   `EveryAllowedRaceIsARealRegisteredRace`, so these binding tests ran and passed.
3. **Player runtime (public build, v1.4.8.119303, Harmony 2.4.2).** `Patch43_BattleLoadDiagnostics` is applied at
   `SubModule.cs:1672`, after the bare Patch88 call at `:1465`, and every call between them is bare, so a throw at
   `:1465` would have left the method before `:1672`. Patch43's hooks (for example
   `origin/fix/public-armory-ids-145:Main/Features/BattleLoadDiagnostics/Hooks/PlayerEncounter_Start_Patch.cs:10-11`)
   wrote `[BattleLoad] seq=1 t=+0ms phase=EncounterStart` at E3 line 175 and E4 lines 158 and 352. So
   `PatchCategory("Patch88_LordPartyTemplate")` returned normally on players' machines. E3 lines 2, 33 and 44 give the
   build stamp `+8df90444`, engine `v1.4.8.119303` and `0Harmony v2.4.2.0`.

## 4. If it could not bind: blast radius on 1.4.x without plan 009

Harmony 2.4.2 (decompiled): `Harmony.PatchCategory(Assembly, string)` runs `CreateClassProcessor(type).Patch()` per
class with no catch; `PatchClassProcessor.PatchWithAttributes` throws `ArgumentException("Undefined target method for
patch method ...")`, which `ReportException` wraps in `HarmonyException`. Classes already patched in that category stay
patched (no rollback).

On `origin/bannerlord-1.4.5:Main/SubModule.cs`:

- `:1324-1325` sets `_gameInitPatchesApplied = true` before the batch, so nothing retries in that process.
- A throw at `:1465` skips 29 bare categories, `:1472` Patch82_MapEventObserverInvariant through `:1604`
  Patch53_PartyIconScale (Patch82, 84, 87, 85, 66, 70, 71, 46, 47, 48, 50, 63_BlowDiagnostics, 56, 13, 26, 51, 27, 29,
  44, 33, 34, 35, 36, 38, 74, 75, 76, 73, 53), the four guarded categories after them (Patch43 `:1672`, Patch60 `:1696`,
  Patch69 roster `:1707` and end `:1709`), `BattleLoadStallWatchdog.Start()` `:1677`, the exit-stall sampler
  `:1686-1688`, `ManualPatchApplicator.ApplyAll` `:1715` (seven SettlementGuards and BannerColor `TargetMethod` patches
  plus the CompanionTactics `GetCaptainTooltip` postfix) and the Harmony census `:1722-1754`.
- Player impact of that set: the crash guards behind several CLAUDE.md traps go missing (Patch82 for the
  `AllocateTroops` CTD #551, Patch84 siege-aftermath menus, Patch85 enlisted detach #557, Patch47 and Patch48 spider
  native AVs, Patch50, Patch56, Patch76 uncapturable heroes), and so do the battle-load diagnostics and the watchdog
  that produced the E3 and E4 evidence.

The exception then leaves TAOM. On v1.4.8 (taom-src and dump):

- `TaleWorlds.MountAndBlade.MBGameManager.cs:110-125` (taom-src): the submodule loop has no catch. A throw skips later
  modules' `OnGameInitializationFinished` (the E1/E2 player loads BetterTime after TAOM, brief fact 10) and the
  SkeletonScale bone-index pass at `:116-124`. TAOM ships no skeleton-scale data, so only vanilla scales would be hit
  (impact UNVERIFIED).
- `Campaign.cs:1452` is the last statement of `Campaign.OnInitialize` (`:1366`), reached from `Game.Initialize`
  (`Core\TaleWorlds.Core\TaleWorlds.Core\Game.cs:301`). `GameManagerBase.DoLoadingForGameManager` (`:183-236`) has no
  catch and advances `_stepNo` only when a step returns; `Modules\SandBox\Sandbox\SandBoxGameManager.cs:114` drives
  `Game.Current.DoLoading()` in the final step.
- What the player sees at the native boundary is **UNVERIFIED** on 1.4.8. TAOM attaches 247 finalizers to the engine's
  `*CallbacksGenerated` types (E3 line 4) through `CrashReportPatchHelper.HandleAndSwallow`, which may turn a crash into
  a stall; I did not trace it. Plan 009 on the 1.5.x ref (v1.5.3) records "a crash bundle is written, a saved game never
  finishes loading, and a new game runs half-patched": treat that as a lead only.
- Correction to plan 009's engine facts for a 1.4.x port: on v1.4.8 `Module.InitializeSubModuleBases`
  (`TaleWorlds.MountAndBlade.Module.cs:201-208`, taom-src) calls `OnSubModuleLoad` with **no** try/catch. The
  catch-and-rethrow plan 009 quotes is v1.5.3 code.

A related cliff, also unprotected on 1.4.x: Harmony 2.4.2's `BuildCategoryCache` calls
`GetCustomAttributes(inherit: true)` (via `HarmonyMethodExtensions.GetFromType`) on every type in TAOM.dll with no
catch, and `AssemblyCachedCategories` is a `ConditionalWeakTable` that stores nothing when the factory throws. One class
whose attribute names a type the engine lacks would therefore fail every `PatchCategory` call in the process; the first
bare one on 1.4.x is `Patch41_McmLayoutFix` at `SubModule.cs:190` in `OnSubModuleLoad`. 1.5.x added
`Main/PatchCategoryIndex.cs` (`7912fdd8f`) for this. Not triggered today: all 229 classes construct.

## 5. What 1.5.x did with Patch88

- **Kept it.** `git diff --stat origin/bannerlord-1.4.5 origin/bannerlord-1.5.x` shows no change to
  `Patch88_InitializeLordPartyPropertiesScope.cs` or `Patch88_SpawnLordPartyScope.cs`. The only 1.5.x commit under
  `Main/Features/LordPartyTemplates/Hooks/` after `6bf93e511` is `bc5b5d71d` (2026-09-14), a doc-comment edit in
  `Patch88_LordPartyTemplate.cs` recording the v1.5.2 re-check (getter and both readers byte-identical to v1.4.8;
  `CanHaveNavalNavigationCapability` became `_leader?.CanHaveFleet ?? true` on v1.5.x). Nothing to port beyond comment
  text, and the v1.5.x clause would be wrong on 1.4.x.
- **Diagnosed the warning** as a false positive: `plans/_audit/2026-09-23-opus/lane-3.findings.md:287-316`
  (CORRECTNESS-05) and `verify-a-batch-05.md:49-95`, both on v1.5.3, with a scoped `#pragma` fix sketch.
- **Never suppressed it.** `git grep BHA0001 origin/bannerlord-1.5.x` finds no `.editorconfig`, csproj or scope-file
  entry, and `docs/reviews/deep-review-036-anim-memory-probe-2026-10-02.md:420` still lists the warning.
- **Fixed the blast radius instead.** Plan 009 merged in `998ae7e95` (2026-09-25). At that commit
  `Main/PatchCategoryApplier.cs` is 84 lines (usings `System`, `System.Collections.Generic`, `TaleWorlds.Localization`,
  `TAOM.Core.Logging`) and `Main/PatchCategoryIndex.cs` exists; the plan's test file is
  `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs`. On the 1.5.x tip `Main/SubModule.cs` has 93
  `TryPatchCategory(` calls and no bare `_harmony.PatchCategory(`. The tip helper has since grown 1.5.x-only
  dependencies (`346f3e093` LoadTimeStamps timing, `6d7e67f3f` co-op server class filter), so a 1.4.x port must take the
  `998ae7e95` end state.

## 6. Broader binding health: origin/bannerlord-1.4.5 against installed v1.4.8

### 6.1 Harmony patch targets: none missing

| Set | Count | Method | Result |
|---|---|---|---|
| Classes with a Harmony attribute | 229 (194 attribute-targeted, 30 `TargetMethod`, 5 `TargetMethods`; 92 categories) | Harmony 2.4.2 `PatchClassProcessor` plus `PatchTools.GetOriginalMethod`; `TargetMethod(s)` invoked; parameter rules mirrored from Harmony's `MethodPatcherTools` (special names, `___field` through `AccessTools.Field`, `__N`, `[HarmonyArgument]`, `__result` on void) | 229 OK; 0 undefined, 0 ambiguous, 0 parameter or field-injection errors |
| ManualPatchApplicator `TargetMethod` classes | 7 (3 SettlementGuards, 4 BannerColorPersistence) | `TargetMethod()` plus the same parameter rules | 7 OK |
| ManualPatchApplicator `AccessTools.Method` target | 1 (`OrderOfBattleHeroItemVM.GetCaptainTooltip()`, one private overload) | same | OK (`Postfix(__instance, ref __result)`) |
| Classes with `Prepare()` | 4 (`CultureStageView_Constructor`, `_Finalize`, `_Tick` patches in Patch7; `Patch77_BodyGeneratorView_Constructor`) | targets resolved regardless of Prepare | OK |

Category wiring: 92 categories declared, 90 applied by live code. The two never applied are
`Patch54_NavalTravelBoatVisual` and `Patch57_NavalAtSeaLandRescueGuard` (commented out at `SubModule.cs:1612` and
`:1619`, PARKED by design). No applied name lacks a declaring class, so there is no silent no-op typo.

Transpilers are the one binding class an offline check cannot exercise. Runtime evidence covers part of it: the
transpiler categories in the bare stretch before `:1672` (for example `Patch13_RaceAge` `:1560` and
`Patch53_PartyIconScale` `:1604`, both described as transpilers in SubModule.cs comments) applied without throwing on
the players' runtime, and `Late_Transpiler` logged `APPLIED ... at IL index 28` (E3 line 99, E4 line 97). Whether each
self-bailing transpiler found its anchor is UNVERIFIED: E3 and E4 contain no transpiler or anchor warning, but such
messages may log below the levels those files show.

### 6.2 Reflection sites: none missing on the engine

- 92 literal sites (type literal plus member string, multi-line aware) across `Main/` and `Dependencies/` resolve on
  v1.4.8, apart from one dead fallback; two type names are TAOM-internal (`Native2ManagedBridge`, `PathReuseCache`).
- Runtime-typed chains, checked link by link, all resolve:
  `MapConversationTableau._data` and `_agentVisuals`, then `MapConversationTableauData.ConversationPartnerData`, then
  `ConversationCharacterData.Character`; `AgentVisuals._data`, `AgentVisualsData.ClothColor1(uint)` and
  `ClothColor2(uint)`, `AgentVisuals.Refresh(bool, AgentVisualsData, bool)`; `BodyGeneratorView._dressedEquipment`;
  `FaceGenVM._faceGenerationParams` and `_characterRefreshEnabled`, `FaceGenerationParams.CurrentAge` and
  `SetRaceGenderAndAdjustParams`; `SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView`
  `_characterCreationManager`, `_affirmativeAction` (on base `CharacterCreationStageViewBase`), `GauntletLayer`,
  `_movie`, `_dataSource`, `PreviousStage`, `Tick`, `OnFinalize`, then `CharacterCreationCultureStageVM.Title` and
  `_cultures`, then `CharacterCreationCultureVM.CultureID` and `ExecuteSelectCulture`;
  `CharacterCreationState.CharacterCreationManager`, `CharacterCreationManager.CharacterCreationContent`,
  `CharacterCreationContent.SetSelectedCulture`; `CharacterCreationNarrativeStageView` declared
  `SpawnNonHumanNarrativeMenuCharacter` and `RefreshAgentVisuals` plus `_characterCreationManager`;
  `ArchiveDeserializer.LoadFrom(byte[])`; `ContainerLoadData.ContainerHeaderLoadData`; `ModuleHelper.GetActiveModules`,
  `ModuleInfo.Id`, `Module.AddSubModule`, `SubModuleInfo.SubModuleClassTypeName`,
  `ApplicationVersion.FromParametersFile(string)` (its only overload).
- Dead fallbacks, not defects, because the primary lookup resolves: `PartyScreenLogic_AddCommand_Patch.cs:73-74` falls
  back to a `TotalNumber` field after `<TotalNumber>k__BackingField` (on v1.4.8 `PartyCommand` is a class with an
  auto-property, `PartyScreenLogic.cs:77` and `:85`, so the reflected write lands); `CultureSettingService.cs:34` falls
  back to `_characterCreationContent` after the `CharacterCreationContent` property.
- Not offline-checkable and left as is: ButterLib, MCM and co-op reflection (`ButterLibExceptionHandlerAdapter.cs`,
  `McmSettingsCollector.cs`, `SettingsFingerprint.cs`, `CoopSessionProvider.cs`) targets third-party types and is
  fail-soft. `Native2ManagedPatcher` attaches by name pattern (247 attached, E3 line 4).
- `ReflectionSiteBindingTests` (45 DataRows) passed in the origin-tip run.

### 6.3 Drift that does exist (TAOM artifacts and gate semantics, not the engine)

| Id | Finding | Evidence | Player impact |
|---|---|---|---|
| G1 | Committed snapshot is stale | `docs/reference/taleworlds-api-snapshot/patch-targets.md` (last touched `c5b84fb41`): header "Patches: 229" over 230 rows; missing `Patch37_CrashReport`, `DecisionItemBaseVM_ExecuteDone_Patch`, `KingdomDecisionsVM_RefreshWith_AutoResolved_Patch` and the three Patch88 classes | none (docs) |
| G2 | Gate does not resolve the way Harmony 2.4.2 does | `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs:136-151` uses `AccessTools.Method` and `PropertyGetter` (base-type walk through `FindIncludingBaseTypes`, and a fallback to the parameterless overload on ambiguity), and `:162-190` merges every class and method spec into one; Harmony's `GetOriginalMethod` uses `DeclaredMethod` and `DeclaredPropertyGetter` per patch method. An inherited-only target, a declared-overload ambiguity or a per-method target can pass the gate and throw at `PatchCategory`. `README.md:9`, `:41` and `patch-targets.md:4` describe the test's semantics as Harmony's | none today (0 instances, 6.1); latent |
| G3 | Gate does not check patch parameter names | only the `___field` underscore count is gated (`HarmonyFieldInjectionNamingTests.cs:10-30`); a renamed engine parameter throws "Parameter ... not found" at patch time | none today; latent |
| G4 | Manual patches sit outside the gate and are unguarded | `Patch35_OOBHeroItem_GetCaptainTooltip` has no attribute or `TargetMethod`, so `HarmonyPatchBindingTests` never discovers it (existence only: `ReflectionSiteBindingTests.cs:45`); `ManualPatchApplicator.cs:24-95` guards null targets only, while its header (`:10-16`) says each patch "fail-safes with a logged warning"; a `harmony.Patch` throw propagates from `SubModule.cs:1715` | none today |
| G5 | Mis-catalogued reflection row | `ReflectionSiteBindingTests.cs:65` names `TaleWorlds.Engine.PathReuseCache._store`; the type is TAOM's (`Main/Features/EditorCacheRebuild/Caching/PathReuseCache.cs:7`) and the row passes only through the simple-name fallback at `:133-141` | none |
| G6 | README counts stale | `README.md:19` and `:41` say 220 patches, `:43` says 32 reflection members; actual 236 discoverable patch types (229 plus 7) and 45 DataRows | none |
| G7 | Gate can pass by skipping (1.5.x plan 008) | `Assert.Inconclusive` guards in `GameAssemblies.cs` (1), `GameModelOverrideBindingTests.cs` (5), `HarmonyFieldInjectionNamingTests.cs` (1), `HarmonyPatchBindingTests.cs` (2), `ReflectionSiteBindingTests.cs` (1); MSTest maps Inconclusive to Skipped with exit 0. 1.4.x has no CI (`ccef172ad`), so the gate is only as good as the local shell's `BANNERLORD_GAME_DIR` | none directly |
| G8 | BHA0001 noise | the single analyzer warning trains readers to ignore BHA output, so the next real one is easy to miss | none |

## 7. Corrections to inputs

- brief2.md "Not yet checked against the installed v1.4.8 DLLs": checked; false positive; Patch88 binds.
- Plan 009's v1.5.3 `Module.InitializeSubModuleBases` catch-and-rethrow does not exist on v1.4.8 (`Module.cs:201-208`).
- `ManualPatchApplicator.cs:10-16` "each fail-safes with a logged warning": true for null targets only.
- Snapshot README "resolved as Harmony does at PatchAll" and "overloaded anywhere in the type hierarchy throws
  AmbiguousMatchException" describe `AccessTools.Method`; Harmony 2.4.2 resolves attribute targets declared-only.

## 8. Recommended skills (for the orchestrator; I cannot invoke them)

`/deep-review` before any WP-A to WP-C commit (C# touched); `/verify` before each commit; `/verify-bindings` after
WP-C and WP-D (BindingVerification run plus snapshot refresh).

## 9. How this was verified (read-only)

- Commands: `git show`, `log`, `grep`, `diff`, `ls-tree` on the refs above (with `MSYS_NO_PATHCONV=1`);
  `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>` for LordPartyComponent, Clan, HeroSpawnCampaignBehavior,
  PartyScreenLogic, MBGameManager and Module; `ilspycmd` 9.1 to stdout on `0Harmony.dll` 2.4.2 and
  `BUTR.Harmony.Analyzer.dll` 1.0.1.50; Grep on the v1.4.8 dump; a System.Reflection.Metadata read of the installed
  `TaleWorlds.CampaignSystem.dll`; in-memory PowerShell reflection over the orchestrator's origin-tip export build. No
  dotnet build or test, no git write, nothing written to the game install.
- Process notes: (a) my first in-memory pass loaded every DLL in the game's `bin` folder, which pulled the native
  `GfeSDK.dll` into the analysis process (it printed `Attached GfeSDK.dll`); nothing was written and later passes loaded
  managed assemblies only. (b) One shell redirection briefly wrote `d4_refl_sites.tmp` (only `git grep` output) in the
  session temp folder outside the scratchpad; I deleted it at once. (c) The reflection checks ran under PowerShell 7
  (.NET), not net472. Every result was positive, and the one type .NET did not surface (`PathReuseCache`) is TAOM's own.
- Evidence scope: player-log inferences cover the public build (`8df90444c`); offline checks cover the origin tip
  (`1b00d1acd`). The only patch file that differs between them is
  `Main/Features/HeroRace/Hooks/BasicCharacterTableau_RefreshCharacterTableau_Patch.cs` (`f8c9c8ecb`, Patch55), which
  the offline pass covered.

## 10. Plan inputs

Base every branch on `origin/bannerlord-1.4.5` (`1b00d1acd`). Subjects carry `v2.0.28` (hook-enforced), CHANGELOG.md is
hand-edited on 1.4.x, and every build or test passes both `-p:DisableModuleCopy=true -p:ModuleId=`. Order: WP-A, WP-B,
then WP-C and WP-D if the maintainer wants them.

**WP-A: silence the Patch88 BHA0001 false positive (S, LOW).** File
`Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs:22`. Put
`#pragma warning disable BHA0001` above the `[HarmonyPatch(...)]` line and `#pragma warning restore BHA0001` after it,
with a one-line reason: BUTR.Harmony.Analyzer 1.0.1.50 cannot resolve nested types, and the binding is pinned by
`Patch88LordPartyTemplateTests.InitializeLordPartyProperties_StillResolves_OnTheNestedArgs_MobilePartyAndOwner`.
Keep `nameof`. Do not restructure to `TargetMethod()`: `AccessTools` on the nested type goes through the same analyzer
lookup, and a string-based lookup gives up compile-time safety. Precedent for scoped pragmas: `ExitStallSampler.cs:133`,
`GondorRecruitmentJsonLoader.cs:199`. Test: `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` shows zero
`warning BHA` lines (baseline two lines, one warning); `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification"`
has 0 failed and 0 skipped. STOP if any other BHA warning appears, if BHA0001 moves to another line, or if the analyzer
is not 1.0.1.50.

**WP-B: port plan 009, guarded category apply (M, MED; maintainer-approved).** Port the end state of merge
`998ae7e95`: `Main/PatchCategoryApplier.cs` (84 lines there), `Main/PatchCategoryIndex.cs` (`7912fdd8f`) and
`TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs`. Not the 1.5.x tip versions, which depend on LoadTimeStamps
(`346f3e093`) and the co-op server filter (`6d7e67f3f`). The 1.4.x `Main/SubModule.cs` has 80 live call lines: 64 bare
(OnSubModuleLoad 190, 226, 242, 243, 253, 383, 424, 428, 434, 439, 445, 461, 520; OnGameInitializationFinished 1370 to
1604, 50 sites; OnMissionBehaviorInitialize 1768) and 16 guarded (166 Patch37, 271 Patch83, 313 Patch61 inside the
try at 294-334, the 323 Patch61 loop, 348 Patch62, 371 Patch79, 455 Patch68, 471 Patch63_BannerBearerSpawnGuard,
542 Patch55, the 1353 preview loop, 1381 Patch77, 1397 Patch78, 1672 Patch43, 1696 Patch60, 1707 and 1709 Patch69).
Keep each catch's side effects: Patch37's `AppDomainExceptionHook.Subscribe` and `Native2ManagedPatcher.AttachAll` run
only on success, the preview loop still logs "applied OK", and Patch77 failure still calls `DisableForSession`. Update
the nine tests that pin the `PatchCategory("...")` spelling: `Patch86HideoutBossFightBindingTests.cs:238`,
`Patch80KingdomVoteDeadlockBindingTests.cs:295`, `Patch85EnlistedDetachDeferralBindingTests.cs:132`,
`FieldCampWiringTests.cs:191`, `Patch88LordPartyTemplateTests.cs:163`, `Patch82MapEventObserverInvariantBindingTests.cs:139`,
`Patch84SiegeAftermathMenuGuardTests.cs:205`, `RefugeWiringTests.cs:62`, `Patch87ReturnToArmyTests.cs:256`. In the port's
docs use the v1.4.8 facts: `Module.InitializeSubModuleBases` has no catch (`Module.cs:201-208`), and
`MBGameManager.OnGameInitializationFinished` loops submodules with no catch and then sets SkeletonScale bone indices
(`:110-125`). `Main/SubModule.cs` is single-owner. Test: PatchCategoryApplierTests (a throwing apply returns false,
logs and the next category still applies; source gate: no bare `_harmony.PatchCategory(` left), then the full suite.
Baseline at `1b00d1acd`: 9153 tests, 3 skipped, no real failure (`SP\build\summary.txt`; the four failures there are
repo-root tests that need a `.git` directory, so run in a real worktree), plus the new tests. In game: the taom_debug log has no
`[PatchApply] ... FAILED` line, nine `[TableauDiag] ... applied OK` lines, and a `[BattleLoad] seq=1 ... EncounterStart`
line on the first battle. STOP if the `998ae7e95` helper needs a type absent on 1.4.x, if a guarded site's catch does
work not listed here, or if another session is editing SubModule.cs. Decide separately whether to wrap the eight
`harmony.Patch` calls in `ManualPatchApplicator.cs:24-95`; plan 009 left that file untouched.

**WP-C, optional (S to M; needs a maintainer call, it is not in DECISIONS #28): make the gate resolve exactly like
Harmony 2.4.2.** In `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs:104-190`, resolve per patch method through
`new PatchClassProcessor(harmony, type)`, its private `patchMethods` and `auxilaryMethods`, and the internal
`PatchTools.GetOriginalMethod`; add a guard test that fails if those Harmony internals move. Add parameter-name,
`___field` and `__result`-on-void checks, and cover `Patch35_OOBHeroItem_GetCaptainTooltip`. The stricter rules are
green today (229 plus 7 plus 1, all OK), so the gate starts green. A related option is porting 1.5.x plan 008 (fail the
gate on Inconclusive, through an explicit runsettings file only). Test: the gate passes, and a deliberately wrong
fixture (an inherited-only target) fails it.

**WP-D: refresh the snapshot and fix catalogue drift (docs and tests, S).** Build TAOM.Tests first (the generator reads
`TAOM.Tests/bin/Debug/net472/TAOM.dll`, `tools/snapshot_api_surface.ps1:66-72`), run `pwsh tools/snapshot_api_surface.ps1`,
then `pwsh tools/snapshot_api_surface.ps1 -Check` must exit 0. Expect six new rows (Patch37_CrashReport, the two #550
seam patches, three Patch88 classes) and a header count equal to the row count. Fix `README.md:19`, `:41` and `:43`
(236 types, 45 rows) and its "as Harmony does" wording unless WP-C lands. Move the `PathReuseCache` row
(`ReflectionSiteBindingTests.cs:65`) to the TAOM-internal category of `reflection-sites.md`. Correct the
`ManualPatchApplicator.cs:10-16` header. STOP if `-Check` still differs after regeneration.
