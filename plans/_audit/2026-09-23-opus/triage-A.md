# Triage A: June 2026 harvest bucket A (CORRECTNESS, SEC, PERF, TEST), lines 1 to 298, against HEAD b2e387db

Source: `plans/_audit/2026-06-12-harvest.md`. Findings keyed by the line number of their `### [` heading.
Skip-listed working-tree paths (`Main/SubModule.cs`, `Main/IoC.cs`, `Main/Features/ElephantLike/**`, `CustomAttacksUtils.cs`) read via `git show b2e387db:<path>` only.

### L11 CORRECTNESS-01: CareerDataService never reset on new campaign
- **Verdict:** FIXED. **Impact:** LOW (residual)
- **Evidence:** `Main/Features/CareerSystem/CareerPersistenceBehavior.cs:21-35` registers `OnNewGameCreatedEvent` calling `_dataService.ResetForNewCampaign()`; `CareerDataService.cs:98-103` swaps in a fresh dict. Commit `f4273639` (`git log -S ResetForNewCampaign`).
- **Note:** June verdict unmatched. Residual: a load of a save lacking the `_taom_career*` keys still keeps the prior campaign's dict (no starter-change reset), the same class as L56.

### L21 CORRECTNESS-02: SpecialResourceStorageService balances survive campaign boundaries
- **Verdict:** STILL_VALID. **Impact:** MED
- **Evidence:** `Main/Features/SpecialResources/SpecialResourcesBehavior.cs:118-133` `OnNewGameCreated` calls only `_service.ResetSessionState()` (which at `SpecialResourceService.cs:402-417` clears `_pendingSpend/_inSession/_loggedResolveKeys`, never storage). `SpecialResourcesBehavior.cs:104-110` still passes the live `_storage.GetAllData()` dict as the `ref`, so an absent key re-installs the old dict. Storage is `Reuse.Singleton` (`SpecialResourcesIoC.cs:11`).
- **Note:** June unmatched; agree with the finding. CC finalize now reseeds the player's current resource unconditionally (`:151`), so only non-current (hero, resource) pairs leak; the `OnGameLoaded` Contains gate (`:166`) still suppresses seeding on a stale pair.

### L30 CORRECTNESS-04: TroopWeight count hooks cache on GetHashCode, never evict, swallow exceptions
- **Verdict:** STALE. **Impact:** LOW
- **Evidence:** `Main/Features/TroopWeight/Hooks/` no longer contains `PartyBaseNumberOfAllMembersHook.cs` or `PartyBaseNumberOfRegularMembersHook.cs` (ls); `git log --diff-filter=D` names `bee07b48` "refactor(troop-weight): weight the party-size limit, not the count", which removed both count-getter patches and hooks; `grep -rn GetHashCode() Main/Features/TroopWeight` returns nothing.
- **Note:** June unmatched. The count getters are no longer patched at all; the weight now deflates `TaomPartySizeModel`'s limit.

### L39 CORRECTNESS-05: TroopWeightXmlLoader admits NaN/Infinity weights
- **Verdict:** FIXED. **Impact:** LOW
- **Evidence:** `Main/Features/TroopWeight/TroopWeightXmlLoader.cs:82-86` now `if (!FiniteFloatValidator.IsFinite(weight) || weight <= 0)`; commit `bee07b48` body: "TroopWeightXmlLoader now rejects NaN/Infinity weights". `TAOM.Tests/Features/TroopWeight/TroopWeightXmlLoaderTests.cs` contains NaN/Infinity cases (grep).
- **Note:** June unmatched; agree it was real.

### L47 CORRECTNESS-06: MutationParams.GetFloat passes NaN/Infinity into ability templates
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Main/Features/CareerSystem/Mutations/MutationParams.cs:15-20` returns raw `float.TryParse(..., NumberStyles.Float, ...)` output with no finite check; `MutationService.cs:88-97` writes the calculator result via `prop.SetValue(target, newValue)` with no finite guard; `BuiltInCalculators.cs:8-20` add/multiply `GetFloat` results.
- **Note:** June unmatched; agree. Input is shipped `career_system/taom_career_choices.xml`, so only a hand-edited or typo'd config triggers it.

### L56 CORRECTNESS-03: load of a save lacking a behavior entry leaks prior-campaign state (SiegeDefense, RacePersistence)
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** Engine premise holds at v1.5.3: `E:/Decompiled_Bannerlord/_categories_v1.5.3/Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CampaignBehaviorDataStore.cs:86-106` calls `SyncData` only when the behavior's StringId (or a name match) is in the save. `Main/Features/Siege/SiegeDefenseBehavior.cs:30-57` resets only on `OnNewGameCreatedEvent`; `RacePersistenceBehavior.cs:14-39` resets only on `OnNewGameCreatedEvent` and `OnSessionLaunched` restores then captures whatever map is in the singleton (`RacePersistenceService.cs:89-99` documents the absent-key carryover). No `_lastSessionStarter` pattern in either (grep).
- **Note:** June unmatched; agree. Trigger needs a warm process plus a save predating the feature, which ages out over time; the same residual now applies to Career (L11) and SpecialResources (L21).

### L69 SEC-01: pin the stdio MCP servers in .mcp.json
- **Verdict:** STILL_VALID. **Impact:** MED
- **Evidence:** `.mcp.json:3-15` serena `uvx --from git+https://github.com/oraios/serena` (no rev); `:22-32` filesystem `npx -y @modelcontextprotocol/server-filesystem` (no version); `:34-38` git `uvx mcp-server-git` (no version). New since June: `:58-66` elevenlabs `uvx elevenlabs-mcp`, also unpinned, and it receives `ELEVENLABS_API_KEY`.
- **Note:** June verifier REAL; agree. Overlaps seed F6. Remediation plan `plans/005-security-hygiene.md` was never executed (no status change since `4236e6c9`).

### L77 SEC-02: SignatureScanner has no ambiguity detection; literal CC byte is a wildcard
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Dependencies/NativeSkinFixes.NativeHooks/SignatureScanner.cpp:41-62` still writes `kWildcardSentinel` into the byte buffer; `:91-101` returns the first match. File last touched `dee5ef88` (the port). Premise shifted: `Signatures.h:80,105,133,147,161,176,192` now hold real patterns (no `<PATTERN_TBD>` left), none containing a literal `CC` (grep of pattern lines for ` CC`: 0).
- **Note:** June verifier REAL; agree the code defect stands. Impact LOW because NativeSkinFixes is PARKED at the wiring (`git show b2e387db:Main/SubModule.cs` lines 721-739, install call commented out), a decided tradeoff; it matters only on re-enable.

### L86 SEC-03: process_faction_map.py templates paths into `python -c` source
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `tools/process_faction_map.py:160-161,260` (`read_png_rgba(r'{filepath}')`) and `:283-284,383` (`crop_and_save(r'{input_path}', r'{output_path}', ...)`). Only commit touching the file is `f023eb76`.
- **Note:** June verifier REAL; agree. Duplicate of L117. Operator-run local tool.

### L93 SEC-01: pin MCP servers in .codex/config.toml
- **Verdict:** STILL_VALID. **Impact:** MED
- **Evidence:** `.codex/config.toml:12-23` filesystem `npx -y` unpinned (roots now include TAOM_Map and LOTRLOME_Armory ModuleData and `E:\Decompiled_Bannerlord`); `:25-27` `uvx mcp-server-git` unpinned; `:9-10` `inherit = "all"`. `tools/audit_claude_config.py` still has no `codex` reference (grep), so `/security-scan` does not see this file.
- **Note:** June verifier REAL; agree. Same fix as L69 (plan 005, unexecuted).

### L102 SEC-02: vendored UIExtenderEx nuget.config carries an upstream GitHub Packages credential
- **Verdict:** STALE. **Impact:** LOW
- **Evidence:** The extracted tree `Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2/` no longer exists; the directory holds only six `.tar.gz` archives (ls). The credential line is still inside `uiextenderex-v2.13.2.tar.gz` at `Bannerlord.UIExtenderEx-2.13.2/src/nuget.config` (count of `ClearTextPassword` lines: 1; value not read). Still gitignored (`.gitignore:151`).
- **Note:** June verifier REAL. The loose, port-reachable copy is gone, so the premise lapsed; the suggested vet-checklist grep was never added (only plan files mention `ClearTextPassword`), so re-extracting the archive restores the June state. Credential belongs to upstream (BUTR).

### L109 SEC-03: enforce pattern uniqueness and section-bounded scanning in SignatureScanner
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Dependencies/NativeSkinFixes.NativeHooks/SignatureScanner.cpp:80-103` scans the whole `SizeOfImage` range and returns the first hit; no `ambiguous`/second-match logic anywhere in the native sources (grep). Patterns are now authored (`Signatures.h:80-192`), so the "gate before patterns exist" window has passed.
- **Note:** June verifier REAL; agree. Duplicate of L77's ambiguity half. LOW while the feature is parked.

### L117 SEC-05: stop interpolating paths into python -c in process_faction_map.py
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** Same sites as L86: `tools/process_faction_map.py:260` and `:383`.
- **Note:** June verifier REAL; agree. Duplicate of L86; count once.

### L124 SEC-04: guard the CI build job against fork-PR execution
- **Verdict:** FIXED. **Impact:** LOW
- **Evidence:** `.github/workflows/build.yml:253-259` the C# job is `runs-on: [self-hosted, windows]` with `if: ${{ vars.BANNERLORD_GAME_DIR != '' && github.event_name != 'pull_request' }}` and a comment explaining why PRs are excluded; `workflow_dispatch` is now declared at `:8`. Guard landed in `59fa6222` "ci: self-host the build job so C# compiles" (`git log -S`).
- **Note:** June verifier REAL. The residual problem (no C# CI on `bannerlord-1.5.x` at all) is seed F1, not this finding.

### L131 SEC-04: gate DllMain DLL_PROCESS_DETACH teardown on lpReserved == nullptr
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Dependencies/NativeSkinFixes.NativeHooks/dllmain.cpp:15-20` still runs three `*_Uninstall()` calls plus `Logging::Close()` unconditionally; `lpReserved` (`:7`) is unused.
- **Note:** June verifier REAL; agree. Unreachable while parked: the installer that loads the DLL is commented out (`git show b2e387db:Main/SubModule.cs` 733-736).

### L140 PERF-01: SpatialGrid rebuilt every 2s; the #219 0.1s fix is in a dead branch
- **Verdict:** STILL_VALID. **Impact:** MED
- **Evidence:** `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs:16` `GridUpdateInterval = 2f`, `:39-46` rebuilds only then. `Main/Features/Warg/WargMissionBehavior.cs:25-34` still carries the #219 comment and `GridUpdateInterval = 0.1f`, used only when `GetMissionBehavior<AdvancedCombatBehavior>() == null` (`:58-61`). `git show b2e387db:Main/SubModule.cs` 1957-1960 registers `AdvancedCombatBehavior` before `WargMissionBehavior` in the same block, so the warg branch is still dead. `SpatialGrid.cs:10` now documents "rebuilt every two seconds"; `docs/features/advanced-combat.md:28,44` also say 2s.
- **Note:** June unmatched; agree. No recorded decision to keep 2s beyond the April Codex note that introduced it. Since #592/#595 the rebuild allocates a fresh map per pass (`SpatialGrid.cs:38`), so a 10 Hz fix needs the creature-presence gate from the June sketch.

### L149 PERF-02: Patch30 allocates a FormationAdapter and walks MCM per call
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs:28` (field-battle gate) and `:36` (banner gate) now short-circuit first, but `:41` still does `new FormationAdapter(__instance)` before the enabled check, which is the first gate inside `FormationLayoutService.cs:68` (`_settings.IsEnabled`). `MixedFormationsSettingsProvider.cs:7-14` reads `TaomSettings.Instance` per property. MCM 5.12.3 (the version in `Main/TAOM.csproj:99`) `GlobalSettings<T>.Instance` does a ConcurrentDictionary lookup plus `DefaultSettingsProvider.GetSettings`, a loop over settings containers (ilspycmd decompile this run).
- **Note:** June unmatched. Partly mitigated (sieges, hideouts and banner bearers exit before the allocation); the field-battle path is unchanged.

### L158 PERF-03: TroopWeight postfixes re-resolve TaomSettings.Instance on every PartyBase member-count read
- **Verdict:** STALE. **Impact:** LOW
- **Evidence:** The two hot-path count-getter patches (`PartyBase_NumberOfAllMembers_Patch.cs`, `PartyBase_NumberOfRegularMembers_Patch.cs`) were deleted in `bee07b48`. The remaining `TaomSettings.Instance?.EnableTroopWeight` reads are UI refresh and upgrade patches (`Main/Features/TroopWeight/Hooks/*_Patch.cs`, six files, low frequency).
- **Note:** June unmatched. Successor site to watch: `TroopWeightService.cs:105` reads `TaomSettings.Instance` inside `ApplyPartySizeWeightPenalty`, reached from `TaomPartySizeModel.GetPartyMemberSizeLimit` (`:74`); call frequency not measured this run (UNVERIFIED).

### L167 PERF-04: TaomMilitaryPowerModel reads up to 7 MCM properties per GetDefaultTroopPower call
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Main/Features/BattleBalance/Models/TaomMilitaryPowerModel.cs:21-33` reads `EnableCustomTroopPower`, then eagerly passes `OverrideVanillaTierPower` and `Tier7Power` to `Tier10Power` as arguments, then one multiplier; `BattleBalanceSettingsProvider.cs:5-12` is `TaomSettings.Instance?.X` per property. Engine still calls it per troop at v1.5.3 (`MapEventSide.cs`, `DefaultCombatSimulationModel.cs`, `DefaultMilitaryPowerModel.cs` in the 1.5.3 dump, grep).
- **Note:** June unmatched; agree. Cost per read is a small container loop; LOW unless profiling says otherwise.

### L175 PERF-05: TroopWeight count-hook caches keyed on GetHashCode with no eviction
- **Verdict:** STALE. **Impact:** LOW
- **Evidence:** Both hooks deleted in `bee07b48` (see L30). The surviving caches in `TroopWeightService.cs:100` are `ConditionalWeakTable<PartyBase, ...>`.
- **Note:** June unmatched. Duplicate of L30's cache half.

### L183 PERF-07: Warg BT nodes and WargRiderHandManager resolve IoC per evaluation
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Main/Features/Warg/WargRiderHandManager.cs:14`; `Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs:15,45` and `WargAiControlledIsNotFacingEnemy.cs:16` are `static ... AdapterFactory => IoC.Resolve<...>()` properties; `WargAttackTask.cs:30-31` resolves two services per attack. The closure half is FIXED: `Main/Adapters/MissionAdapterFactory.cs:20-33` now passes a cached `_build` delegate to `GetOrAdd` (`0c323437`).
- **Note:** June unmatched; agree. Same as seed F4 (cite F4, do not double count).

### L193 PERF-06: SpatialGrid.GetAgentsInRadius scans every occupied cell and allocates per query
- **Verdict:** FIXED. **Impact:** LOW
- **Evidence:** `Main/Features/AdvancedCombat/SpatialGrid.cs:99-129` iterates only the bbox cells with `TryGetValue` and offers a caller-buffer overload (`:99`); the allocating overload (`:86-91`) delegates to it. Landed in `7402fa8e` (`git log -S "Enumerate ONLY the cells"`; comment cites deep-review 2026-06-15).
- **Note:** June unmatched. The allocating overload still exists and is used by `GetNearAliveAgentsInRange` (`:131-139`); whether hot callers moved to the buffer form was not checked.

### L205 TEST-01: ConfigIdValidationTests mirror tables have no source-parse consistency check
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `TAOM.Tests/Core/ConfigIdValidationTests.cs:12-23` (20 cultures) and `:25-34` (22 kingdoms) are hand-coded; the only consistency checks are self-counts at `:196-198` (`Assert.AreEqual(20, ValidCultureIds.Count`) and `:206-208`. Drift measured this run (python regex over `<Culture id>` in `Main/_Module/ModuleData/taom_spcultures.xml`): the XML declares `shaghana` and `abanissa` as cultures, which the mirror lacks (it lists them only as kingdoms), plus bandit cultures such as `umbar_corsairs`.
- **Note:** June verifier REAL; agree. The mirror has been hand-edited since June (18 to 20 cultures, comment at `:20-21`), which is the drift this finding predicted.

### L214 TEST-03: shipped SpecialResources XML never read by any test or schema (troop ids, kingdom mappings)
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** Partly addressed: `TAOM.Tests/Features/SpecialResources/TroopResourceCostDataTests.cs:16-60` (added in `fc07d925`) now loads the shipped `troop_resource_costs.xml` and `special_resources_config.xml` and gates `resource_id`, icons and upkeep bands. Still missing: no test checks that each `<Troop id>` exists as an NPCCharacter, nor the kingdom/culture mappings in `special_resources_config.xml`; `tools/schemas/` still holds only three schemas and `validate_moduledata.py`/`taom_schema.py` never mention `special_resources` (grep). Today all 87 rows resolve (python cross-check against repo `<NPCCharacter id>`), so there is no live orphan.
- **Note:** June verifier REAL; agree on the remaining half. Overlaps L230; count once.

### L222 TEST-05: CastleNotableMaintainer vanilla-mirror volunteer math untested
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Main/Features/CastleRecruitment/Hooks/CastleNotableMaintainer.cs:164-168` still holds `MathF.Log(notable.Power / (float)current.Tier, 2f) * 0.01f` and the tier/upgrade-target pick inside the Hooks boundary; `EnsureCastleNotables` at `:68`. `grep -rln "CastleNotableMaintainer|FillCastleVolunteers" TAOM.Tests --include=*.cs` returns nothing.
- **Note:** June unmatched; agree.

### L230 TEST-02: shipped feature-config cross-refs (SpecialResources, SettlementGuards troop ids) validated by nothing
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** SpecialResources: see L214 (resource_id now gated; troop ids not). SettlementGuards: the only real-XML gate is still culture-only (`TAOM.Tests/Core/ConfigIdValidationTests.cs:117-171`); `SettlementGuardConfigProviderTests.cs` uses inline XML. No test or validator reads the 38 `troop=` refs in `Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml`; all 38 resolve today (python cross-check).
- **Note:** June unmatched; agree. Merge with L214 when planning.

### L240 TEST-04: config-provider validation untested (BattleBalance, WarOfTheRing) and absent (ArmyTargeting)
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** Two of three parts FIXED: `TAOM.Tests/Features/Diplomacy/WarOfTheRingConfigProviderTests.cs` exists (`e273cd23`); `Main/Features/ArmyTargeting/ArmyTargetingConfigProvider.cs:57-181` now validates with `FiniteFloatValidator.IsFiniteInRange` (multipliers at `:143-148`) and `TAOM.Tests/Features/ArmyTargeting/ArmyTargetingConfigProviderValidationTests.cs` covers it (`dca8daad`). Still open: `Main/Features/BattleBalance/BattleBalanceConfigProvider.cs:57-89` `ValidateConfig` has no test (`grep -rln BattleBalanceConfigProvider TAOM.Tests` returns nothing).
- **Note:** June verifier REAL. Remaining scope is one provider test file.

### L249 TEST-05: TaomPartyWageModel ComputeMountedWageShare is untested inline math
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Main/Features/TroopProgression/Models/TaomPartyWageModel.cs:129-141` (roster loop plus division, in the model); called at `:62`. The only test reference (`TAOM.Tests/Features/AiPartySize/AiPartySizeOrderingTests.cs:103`) reads the file as text, not the math. Violates `.claude/rules/gamemodels.md:37` rule 4.
- **Note:** June unmatched. The 199-line size is KNOWN (`docs/migration/v1.5.2-impact.md` "Findings outstanding"); the untested share math is not on that list. Overlaps L256.

### L256 TEST-02: economy math untested in GameModel entry points (TaomPartyWageModel, TaomPartyHealingModel)
- **Verdict:** STILL_VALID. **Impact:** MED
- **Evidence:** Wage model: `ComputeMountedWageShare` (`TaomPartyWageModel.cs:129`) and `ResolveBuyerRecruitmentPerks` (`:161-183`) plus perk helpers (`:184-195`) remain in the model with no test. Healing model grew to 144 lines: `Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs:29-98` keeps the settings, config, culture, bonus and career-passive gates inline, and `:121-129` adds enlistment gating; `TaomPartyHealingModelTests.cs` still tests only `ApplyCulturalSurvivalBonus`, `GetCulturalSurvivalBonus` and `GetTierPower` (method list via grep).
- **Note:** June verifier REAL; agree. MED because these run for every party daily and the healing model has gained logic since June.

### L265 TEST-04: CultureSettingService, CharacterTableauService, CrashReportService have zero tests
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Main/Features/FactionMap/CultureSettingService.cs` (115 lines) has zero test references; `Main/Features/CrashReport/CrashReportService.cs` (285 lines) is mentioned only in a comment in `TAOM.Tests/Core/Logging/FileLoggerTests.cs:288`. `CharacterTableauService.cs` is gone: deleted in `6071df03` "fix(heroRace): apply the dead 3D tableau race offsets" and replaced by `TableauPositionService`/`RacePositionStore`, which have tests (`TAOM.Tests/Features/HeroRace/TableauPositionServiceTests.cs`, `RacePositionStoreTests.cs`).
- **Note:** June unmatched. Two of three classes still untested. Overlaps L291.

### L273 TEST-01: CulturalFeats production-metadata pin covers only 24 feats
- **Verdict:** STILL_VALID. **Impact:** MED
- **Evidence:** `TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs:296-357` `Wave1Feats_ProductionMetadata_MatchesSpec` spec table still has 24 rows (awk count between `var spec` and `};`). Production now registers 130 feats (`grep -c 'Register("taom_' Main/Features/CulturalFeats/TaomCulturalFeats.cs` = 130; test `:22-29` pins the count at 130), so 106 feats' bonus, sign and AdditionType are unpinned. `CulturalFeatsServiceTests.cs:1327-1351` still injects a hand-mirrored table.
- **Note:** June unmatched; agree. Not the rejected "table-driven TaomCulturalFeats rewrite": this is a test-side parser extension.

### L282 TEST-03: transpiler IL-match logic untested; binding gate stops at target resolution
- **Verdict:** STILL_VALID. **Impact:** MED
- **Evidence:** The real-IL gate the sketch asked for now exists: `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs` (added `8b9f0a23`) feeds `PatchProcessor.GetOriginalInstructions` through production transpilers, but only for `BannerColorTranspiler` (party visual) and `PartyIconScaleTranspiler` (two sites). The cited families remain uncovered: `CastleAiTranspiler.SwapIsCastleGate` (`Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs:27`), `RefreshCharacterEntityAuxPatch.Transpiler` (`Main/Features/CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs:51-52`, no `TAOM.Tests/Features/CharacterSelection/`), and `Banner_TryGetBannerDataFromCode_Transpiler` / `CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler`; grep for each name in `TAOM.Tests` returns nothing.
- **Note:** June unmatched; agree. Also without a real-IL row, not in the June list: `Enlistment/Hooks/BehaviorComponent_OnBehaviorActivated_Transpiler.cs`, `MarriageAlignment/Hooks/Patch81_MarriageClanDraw.cs`, `RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs` (synthetic tests for them not checked). MED given continuing 1.5.x engine bumps.

### L291 TEST-06: CC-path engine-coupled services have zero tests and sit outside the binding catalogue
- **Verdict:** STILL_VALID. **Impact:** LOW
- **Evidence:** `Main/Features/FactionMap/CultureSettingService.cs:19-90` still reaches `CharacterCreationManager`, `CharacterCreationContent`/`_characterCreationContent` and `SetSelectedCulture` via `AccessTools` and keeps the vlandia clan-name rule at `:59-70`; no test names `SetSelectedCulture` or `_characterCreationContent` (grep of `TAOM.Tests`). The `CharacterTableauService` half is stale (deleted in `6071df03`, see L265).
- **Note:** June unmatched; agree. Duplicate of L265 for CultureSettingService; count once.

## Summary

33 findings: FIXED 4 (L11, L39, L124, L193; the closure half of L183 is also fixed), STALE 4 (L30, L102, L158, L175), STILL_VALID 25, REFUTED 0. Duplicate pairs to count once: L86/L117, L77/L109 (ambiguity half), L214/L230, L249/L256, L265/L291, L30/L175. Every June verifier REAL verdict I could re-check still holds for the code at the time; none is refuted.

## What I did not cover

- No `dotnet build` or `dotnet test` (brief). No test was run to confirm the cited tests pass or fail.
- Engine behaviour for L56 was read from the v1.5.3 decompile dump (`E:/Decompiled_Bannerlord/_categories_v1.5.3`), because `taom-src path CampaignBehaviorDataStore` could not resolve the internal type; not re-checked against the installed DLL.
- MCM cost (L149, L167) confirmed by decompiling MCMv5 5.12.3 from the NuGet cache, not the copy the game loads. No profiling of any PERF item; call frequencies are the June agents' claims except where noted.
- L158 successor site (`TroopWeightService.cs:105` on `GetPartyMemberSizeLimit`): call frequency not measured.
- L193: did not check whether hot callers use the buffer overload of `GetAgentsInRadius`.
- L282: did not check synthetic unit tests for the Enlistment, MarriageAlignment and RaceAge transpilers.
- L102: did not open the credential value (by rule); only counted the line inside the tarball.
- Commit attribution for FIXED items is from `git log -S`/`--diff-filter`, which names the first matching commit; not audited further.

