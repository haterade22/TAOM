# Input pack: WP 112 crash-robustness

Title (draft): Port the crash and NRE guards 1.5.x added on shared code paths
Wave: 1   Lane: D   Draft priority: P0   Groups: 10   Port commits: 20
Depends on (WP ids): 105, 107

## Orchestrator notes for this WP (binding)
- ab78564e0 (1.5.x port of PR #1064) vs 1b00d1acd (the 1.4.x PR itself): diff them and port only what the 1.4.x tip lacks; its two IL-fingerprint tests go to WP118 (they need IlCallScanner.Fingerprint from a27c6b338).
- nre-containment-finalizers: take only 6ae924638's NRE-finalizer narrowing with 0002cb561; its AutoResolve hunk goes to WP123, its HeroRace hunk to WP119, its career-quest hunk to WP129.
- 1165a00de + de90a2451: swap IOutcomeLog for IModLogger (IOutcomeLog arrives only in WP139).
- MainMenuStep (85c608963, 2afea8878) goes into Main/Composition (WP105 exists).
- K2: add BanditHideoutLiveDataTests.EveryHideoutCultureBanditClan_OwnsALiveHideout from 0799af376 with cd06da904/1ef7d1af6 (game dir via GameAssemblies or BANNERLORD_GAME_DIR, Inconclusive when TAOM_Map is absent).
- plan-002-nan-infinity-guards closes the 1.4.x TODO plan 002 (the TroopWeight half is already on 1.4.x as bee07b48).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Deep-dive findings and non-port items this WP owns
- LOADING-HANG-08 [P3/high] Other 1.5.x robustness leads are not load hangs on 1.4.x today; keep two as release gates | port: cd06da904,84f0459bc  (report: plans/_audit/2026-11-04-port148/deepdive/loading-hang.md)

## Port groups owned by this WP (from the commit classification)

### group `plan-002-nan-infinity-guards`  P2  track=data-integrity  commits=3  sim: clean=0 conflict=3
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Features/CareerSystem/MutationParamsTests.cs
- `78889a85c` 2026-09-24 PORT P2 pick=conflict :: fix(config): v2.0.30 - reject NaN and Infinity in mutation floats
  - why: MutationParams.GetFloat accepts 'NaN' and 'Infinity' from career XML; 1.4.x is still unguarded (MutationParams.cs:15-20), and the FiniteFloatValidator the fix uses already exists there (Main/Core/Validation/FiniteFloatValidator.cs:19).
  - port notes: The code and the new MutationParamsTests apply as-is; the only pick conflict is CHANGELOG. The TroopWeight half of plan 002 is already on 1.4.x via bee07b48 (TroopWeightXmlLoader.cs:86). No NaN or Infinity literal appears in shipped 1.4.x ModuleData XML (git grep), so this is a latent guard.
- `968325896` 2026-09-24 PORT P2 pick=conflict :: fix(career): v2.0.30 - review follow-ups for plan 002
  - why: Adds two MutationParamsTests (the unparseable fallback, and a negative finite value passing through) and career-system.md guidance to read floats only through GetFloat; no production code.
  - port notes: Take the test hunk and career-system.md. The review record, RCA, lessons and REVIEW-LOG are optional records that conflict in shared files.
  - prereqs: 78889a85c
  - conflicts: TAOM.Tests/Features/CareerSystem/MutationParamsTests.cs
- `8186cc9ca` 2026-09-24 PORT P3 pick=conflict :: fix(career): v2.0.30 - convergence fixes for plan 002
  - why: Docs-only corrections to the plan 002 records and to career-system.md's Tests table.
  - port notes: Take career-system.md. Skip the LESSONS-LEARNED recount and recount on 1.4.x instead. Take the record edits only if the records are ported.
  - prereqs: 968325896
skipped in this group: `82ae6cf17` SKIP-BOOKKEEPING docs(changelog): v2.0.30 - cite #663 for plan 002

### group `main-party-getter-null-guard`  P1  track=crash-robustness  commits=2  sim: clean=0 conflict=2
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Core/MainPartyNullCheckScanTests.cs, TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs
- `91f369984` 2026-10-14 PORT-PARTIAL P1 pick=conflict :: fix(messengers): v2.0.34 - check the stored main party, not its getter
  - why: 1.4.x MessengerCampaignBehavior.IsPlayerAvailable (line 607, reached from the hourly-tick path at line 305) null-checks PartyBase.MainParty, whose v1.4.8 getter dereferences Campaign.Current.MainParty and throws while it is null (PartyBase.cs:208-217); the fix checks Campaign.Current?.MainParty.
  - port notes: The one-line production fix applies directly. Both new tests (MainPartyNullCheckScanTests and the MessengerCampaignBehaviorTests source check) use RepoPaths.ReadSource/StripComments/MentionsOutsideComments, absent on 1.4.x: port RepoPaths first (see 06252ae2c) or adapt to a local helper. Re-cite v1.5.5 PartyBase lines to v1.4.8 PartyBase.cs:208-217. Continue the series with e9854bbd0 (1.4.x reads PartyBase.MainParty?. at EncounterAdapter.cs:86,124,140 and PartyItemRosterAdapter.cs:29,44,66,88, where ?. does not guard the getter's throw), 2c09a4145 and 4f27b3bde, all outside this batch.
  - prereqs: 06252ae2c
  - conflicts: TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs
  - issues: 792
- `dc1c37031` 2026-10-14 PORT-PARTIAL P2 pick=conflict :: test(core): v2.0.34 - main party scan catches every null-check form
  - why: Broadens the PartyBase.MainParty null-check scan to both polarities, the reversed forms and the is/is not patterns, so the gate from 91f369984 catches every form of the crash pattern.
  - port notes: Take the regex and the data rows; drop HunterPartyAdapter_IsMapFree_ChecksTheStoredMainParty (HunterPartyAdapter is absent on 1.4.x). Needs RepoPaths like 91f369984. The later refinement 4f27b3bde (nested parens) is outside this batch.
  - prereqs: 91f369984
  - conflicts: TAOM.Tests/Core/MainPartyNullCheckScanTests.cs
  - issues: 792

### group `issue-792-mainparty-getter-reads`  P2  track=crash-robustness  commits=5  sim: clean=0 conflict=5
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/PartyItemRosterAdapter.cs, Main/Features/EyeOfSauron/EyeOfSauronService.cs, Main/Features/WarChronicle/Events/ChronicleService.cs, TAOM.Tests/Core/MainPartyNullCheckScanTests.cs, TAOM.Tests/Features/WarChronicle/Events/ChronicleServiceTests.cs
- `e9854bbd0` 2026-10-14 PORT P2 pick=conflict :: fix(adapters): v2.0.34 - read the main party through the campaign
  - why: PartyBase.MainParty dereferences Campaign.Current.MainParty unguarded (v1.4.8 PartyBase.cs:208-218, dump and taom-src agree), so the seven PartyBase.MainParty?. reads in EncounterAdapter and PartyItemRosterAdapter throw while the main party is null, and 1.4.x has identical code.
  - port notes: Adapter hunks apply verbatim (1.4.x EncounterAdapter.cs:86, 124, 140, 143; PartyItemRosterAdapter.cs:29, 44, 66, 88). The MainPartyNullCheckScanTests hunk needs the test file from 91f369984 and dc1c37031 plus TAOM.Tests/Infrastructure/RepoPaths.cs, which is absent on 1.4.x: copy RepoPaths.cs as of 4f27b3bde (72 lines; first added in the unrelated FieldCommission commit 35007dbb8, StripComments from a0db0558b, MentionsOutsideComments from 06252ae2c) rather than cherry-picking those. The adapters.md hunk edits the 'campaign statics' paragraph that bb85e9284 (1.5.x-only so far) added; write that paragraph fresh on 1.4.x. The lesson hunk needs e8504a62b's MainParty lesson. Fold in 74bdd5eff's removal of the now-unused using from PartyItemRosterAdapter.
  - prereqs: 91f369984, dc1c37031
  - conflicts: TAOM.Tests/Core/MainPartyNullCheckScanTests.cs
  - issues: 765, 792
- `186156808` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - Codex lessons from #765 and #792 reviews
  - why: Adds four Codex review heuristics to .ai/review-reference.md; three draw on 1.5.x-only features but read line-neutrally, and the fourth (list ?. and ?? when a scan bans a guard on a throwing getter) belongs with the MainParty series.
  - port notes: Take the fourth bullet ('Every syntax that guards a throwing getter') with the issue-792 series. The other three are optional generic heuristics whose evidence is 1.5.x-only (Eye of Sauron, War Chronicle). Do not copy the 1.5.x review totals into the 1.4.x section header.
  - issues: 765, 792
- `2c09a4145` 2026-10-14 PORT P2 pick=conflict :: fix(adapters): v2.0.34 - three more main party reads via the campaign
  - why: Three more sites null-check a local copy of the throwing PartyBase.MainParty getter (Unequip All, the recruitment screen's weighted party size, the enlistment in-battle check), and all three exist verbatim on 1.4.x.
  - port notes: Code hunks apply as is (1.4.x PlayerEquipmentAdapter.cs:16, EnlistmentBattleBehavior.cs:252, TroopWeightDisplayHook.cs:238), using changes included. The scan-test hunks (parenthesised and ?? forms, the alias-gap test, AliasedSites) need RepoPaths.MentionsOutsideComments and RepoPath (see e9854bbd0). Leave the Codex prompt record docs/reviews/codex-adversarial-docs-audit-code-2026-10-15.prompt.md. The adapters.md and lesson hunks follow the paragraph written for e9854bbd0.
  - prereqs: e9854bbd0
  - conflicts: TAOM.Tests/Core/MainPartyNullCheckScanTests.cs
  - issues: 792
- `74bdd5eff` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: fix(war): v2.0.34 - honest chronicle_open dry-run texts, tidy-ups
  - why: Mostly War Chronicle dry-run texts and an Eye of Sauron comment wrap, but it drops the using that e9854bbd0 leaves unused in the shared PartyItemRosterAdapter.
  - port notes: Take only the removal of `using TaleWorlds.CampaignSystem.Party;` from Main/Adapters/PartyItemRosterAdapter.cs, folded into the e9854bbd0 port. Leave ChronicleService, the chronicle tests and EyeOfSauronService.
  - prereqs: e9854bbd0
  - conflicts: Main/Adapters/PartyItemRosterAdapter.cs, Main/Features/EyeOfSauron/EyeOfSauronService.cs, Main/Features/WarChronicle/Events/ChronicleService.cs, TAOM.Tests/Features/WarChronicle/Events/ChronicleServiceTests.cs
  - issues: 792
- `4f27b3bde` 2026-10-15 PORT P2 pick=conflict :: test(core): v2.0.34 - main party scan sees nested parens
  - why: Hardens the MainParty scan regex to see nested parentheses and spaced call forms (11 of its 15 new rows failed the old regex), extending the gate the issue-792 series brings to 1.4.x.
  - port notes: Test file plus the adapters.md sentence; needs the scan test and RepoPaths brought by the earlier series commits. After 91f369984, e9854bbd0 and 2c09a4145 land, the production scan should find no offender on 1.4.x (1.4.x MessengerCampaignBehavior.cs:607 is the one 91f369984 fixes); run it to confirm.
  - prereqs: 2c09a4145
  - conflicts: TAOM.Tests/Core/MainPartyNullCheckScanTests.cs
  - issues: 792

### group `issue-807-main-menu-step`  P2  track=crash-robustness  commits=2  sim: clean=0 conflict=2
symptoms: loading-hang, crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/MainMenuStep.cs, Main/SubModule.cs, TAOM.Tests/Composition/MainMenuStepTests.cs, TAOM.Tests/Composition/SubModuleMainMenuHookTests.cs
- `85c608963` 2026-10-15 PORT P2 pick=conflict :: fix(main-menu): v2.0.34 - main-menu setup never throws into the engine
  - why: The 1.4.x hook calls CustomizeMenu unguarded (SubModule.cs:528), and on v1.4.8 the engine re-runs every module's OnBeforeInitialModuleScreenSetAsRoot each tick while no state is active (Module.cs:500-509, loop with no try at :756-759) while Patch37 swallows at Module.OnApplicationTick, so one throw becomes a per-tick boot loop.
  - port notes: Port as one change with 562 and take the final MainMenuStep shape. Main/Composition does not exist on 1.4.x: create the folder or move the class to Main/Core. The 1.4.x hook has no ShaderCompileNoticeEntry.Stop line, so only the CustomizeMenu call changes. Rewrite the comment's engine refs to v1.4.8 Module.cs:500-509 and :750-759. SubModuleMainMenuHookTests reads SubModule.cs through RepoPaths (absent on 1.4.x; 35007dbb8, file only). Expect a SubModule.cs context conflict.
  - prereqs: 35007dbb8
  - conflicts: Main/SubModule.cs
  - issues: 807
- `2afea8878` 2026-10-15 PORT P2 pick=conflict :: refactor(main-menu): v2.0.34 - MainMenuStep logs the full exception
  - why: Final shape of the main-menu guard: renamed MainMenuStep, and its one warning carries the full exception, so a contained throw still logs the stack Patch37 would have captured.
  - port notes: Port as one change with 559 (skip the intermediate LifecycleStep files). Same constraints: no Main/Composition on 1.4.x, RepoPaths needed by SubModuleMainMenuHookTests; docs/features/main-menu-customizer.md exists on 1.4.x and takes the doc hunk.
  - prereqs: 85c608963
  - conflicts: Main/Composition/MainMenuStep.cs, Main/SubModule.cs, TAOM.Tests/Composition/MainMenuStepTests.cs, TAOM.Tests/Composition/SubModuleMainMenuHookTests.cs
  - issues: 807

### group `issue-824-bard-town-guard`  P0  track=crash-robustness  commits=1  sim: clean=0 conflict=1
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs
- `eed06b9c7` 2026-10-19 PORT-PARTIAL P0 pick=conflict :: fix(taverns): v2.0.35 - talking to a musician no longer crashes
  - why: Talking to the tavern musician crashes in a town whose culture owns no other town; v1.4.8 has the same unguarded .Name read and the 1.4.8 map has seven such cultures.
  - port notes: v1.4.8 SandBox TavernEmployeesCampaignBehavior.conversation_talk_bard_on_condition (:641) calls Extensions.GetRandomElementWithPredicate<Settlement>(Settlement.All, ...) (MBReadOnlyList overload, Extensions.cs:288) and reads .Name with no null check, so the transpiler's single-site match should hold. The laptop's TAOM_Map settlements.xml has one town each for dolguldur, isengard, lothlorien, rivendell, bluecraig, goblin and lindon. Take the three Hooks files, BardTownGuardBindingTests, the feature doc and registry rows; replace BardTownGuardModule (a Main/Composition TaomFeatureModule) with an Initialize(logger) call plus _harmony.PatchCategory in SubModule.cs (single-owner) and rewrite BardTownGuardWiringTests. Keep the number Patch110: the highest literal category on 1.4.x is Patch88.
  - conflicts: Main/Composition/FeatureModules.cs
  - issues: 824

### group `nre-containment-finalizers`  P2  track=crash-robustness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs, Main/Features/ArmourAcquisition/Hooks/LadderQuests.cs, Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsBehavior.cs, Main/Features/Encyclopedia/Hooks/HeroHelper_GetLastSeenText_Patch.cs, Main/Features/Encyclopedia/LastSeenState.cs, Main/Features/FireAndTribute/FireAndTributeIoC.cs, Main/Features/FireAndTribute/FireAndTributeService.Spawn.cs, Main/Features/FireAndTribute/FireAndTributeService.Steering.cs, Main/Features/FireAndTribute/FireAndTributeService.cs, Main/Features/TaomSettings.cs, Main/Features/UnlookedForAid/Models/TaomMobilePartyAIModel.cs, Main/Features/UnlookedForAid/UnlookedForAidModule.cs, TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsBehaviorTests.cs, TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs, TAOM.Tests/Features/Encyclopedia/HeroHelperLastSeenTextGuardTests.cs, TAOM.Tests/Features/ServerSettings/ServerSettingsSchemaTests.cs, TAOM.Tests/Features/UnlookedForAid/UnlookedForAidWiringTests.cs
- `6ae924638` 2026-10-27 PORT-PARTIAL P2 pick=conflict :: Add tests for NRE containment finalizers and improve documentation
  - why: A mixed commit that narrows the NRE-swallowing finalizers in five shared guards (all five still carry the old finalizer on 1.4.x), cancels a former player hero's career quest, and silently deletes AutoResolveDiagnostics, which is on by default on 1.4.x and wrote 225 synchronous INFO records (421 KB of 1.09 MB) in E4's 35-minute session.
  - port notes: Take four parts. (1) The finalizer changes in Agent_CheckToDropFlaggedItem_Guard_Patch, Patch69_TournamentEndGuard, Army_FindBestGatheringSettlementAndMoveTheLeader_Patch and GauntletSceneNotification_OpenScene_Guard_Patch, and the narrative-horse finalizer turned into a prefix, with ContainmentFinalizerRethrowTests, Patch49GatheringNreRuleTests and NarrativeMountSpawnGuardTests. v1.4.8 Army.cs:721-727 has the same null-fallback dead end; MobileParty.NavigationCapability (MobileParty.cs:465) and SettlementHelper.FindNearestFortificationToMobileParty (SettlementHelper.cs:245) exist. These need RethrowStackPreserver from c79a58521, which lives in TAOM.Dependencies and so needs a paired Dependencies release (#371). (2) The career-quest owner-change cancel (1.4.x CareerQuest exposes OwnerHeroStringId at line 65). (3) The AutoResolveDiagnostics removal (code, MCM rows, IoC and SubModule wiring, tests, tools/analyze_battle_logs.py), or at least a renamed, default-off setting. (4) The RacePersistenceService new-campaign log tweak. Leave the LastSeenText, FireAndTribute, UnlookedForAid, ArmourAcquisition, ServerSettings and SettingsFingerprint hunks (1.5.x-only).
  - prereqs: c79a58521
  - conflicts: Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs, Main/Features/ArmourAcquisition/Hooks/LadderQuests.cs, Main/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsBehavior.cs, Main/Features/Encyclopedia/Hooks/HeroHelper_GetLastSeenText_Patch.cs, Main/Features/Encyclopedia/LastSeenState.cs, Main/Features/FireAndTribute/FireAndTributeIoC.cs, Main/Features/FireAndTribute/FireAndTributeService.Spawn.cs, Main/Features/FireAndTribute/FireAndTributeService.Steering.cs, Main/Features/FireAndTribute/FireAndTributeService.cs, Main/Features/TaomSettings.cs, Main/Features/UnlookedForAid/Models/TaomMobilePartyAIModel.cs, Main/Features/UnlookedForAid/UnlookedForAidModule.cs, TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsBehaviorTests.cs, TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs, TAOM.Tests/Features/Encyclopedia/HeroHelperLastSeenTextGuardTests.cs, TAOM.Tests/Features/ServerSettings/ServerSettingsSchemaTests.cs, TAOM.Tests/Features/UnlookedForAid/UnlookedForAidWiringTests.cs

### group `nre-guard-audit-2026-10-27`  P1  track=crash-robustness  commits=1  sim: clean=0 conflict=1
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/AdvancedCombat/Hooks/Agent_CheckToDropFlaggedItem_Guard_Patch.cs, Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs, Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs, Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs, Main/Features/Encyclopedia/LastSeenState.cs, Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs, Main/SubModule.cs, TAOM.Tests/Features/ArmyTargeting/Patch49GatheringNreRuleTests.cs, TAOM.Tests/Features/CharacterCreation/NarrativeMountSpawnGuardTests.cs, TAOM.Tests/Features/Encyclopedia/HeroHelperLastSeenTextGuardTests.cs, TAOM.Tests/Infrastructure/ContainmentFinalizerRethrowTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
- `0002cb561` 2026-10-27 PORT-PARTIAL P1 pick=conflict :: fix(crash-guards): v2.0.35 - crash guards contain only their own fault
  - why: Narrows the shared null-reference crash guards (Patch49 army gathering, Patch50 flagged-item drop, Patch20 narrative mount, Patch56 race cinematic, Patch69 tournament end) so each contains only its own fault, logs it, keeps the stack on rethrow and always releases cinematic input; the 1.4.x copies equal the pre-image and Patch49 there still swallows every NRE.
  - port notes: Take the Patch49, Patch50, Patch20, Patch56 and Patch69 hunks, the Patch49 comment in SubModule.cs, the Patch20 reflection rows (CharacterCreationNarrativeStageView._characterScene and SpawnNonHumanNarrativeMenuCharacter exist on v1.4.8: _modules_build/SandBox__SandBox.GauntletUI.cs:1537 and :12374), the api-snapshot row in .ai/verification.md (1.4.x has tools/snapshot_api_surface.ps1 -Check but no row), the harmony-patch-registry rows, and the tests Patch49GatheringNreRuleTests, NarrativeMountSpawnGuardTests and ContainmentFinalizerRethrowTests (all created by 6ae924638). Leave Encyclopedia LastSeenState, HeroHelperLastSeenTextGuardTests, the Patch123 SubModule comment and rca-lastseen-guard: Patch123 guards the v1.5.x rewrite of HeroHelper.GetLastSeenText and 12b010626 retires it. Requires the guard hunks of 6ae924638 and Dependencies/Foundation/RethrowStackPreserver.cs, absent on 1.4.x (added inside the c79a58521 umbrella; port the file alone). Comment line numbers are v1.5.5 (Army.cs :661/:728, Mission.cs :3749); re-cite from the v1.4.8 dump. Patch50 and Patch69 already rethrow non-NREs on 1.4.x, so for them this is logging and stack preservation; Patch49 gains real narrowing.
  - prereqs: 6ae924638, c79a58521
  - conflicts: Main/Features/AdvancedCombat/Hooks/Agent_CheckToDropFlaggedItem_Guard_Patch.cs, Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs, Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs, Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs, Main/Features/Encyclopedia/LastSeenState.cs, Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs, Main/SubModule.cs, TAOM.Tests/Features/ArmyTargeting/Patch49GatheringNreRuleTests.cs, TAOM.Tests/Features/CharacterCreation/NarrativeMountSpawnGuardTests.cs, TAOM.Tests/Features/Encyclopedia/HeroHelperLastSeenTextGuardTests.cs, TAOM.Tests/Infrastructure/ContainmentFinalizerRethrowTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs

### group `issue-1063-feral-orcs-hideout-guard`  P2  track=crash-robustness  commits=2  sim: clean=0 conflict=2
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/BanditManagement/Hooks/Patch125_HideoutClanGuard.cs, Main/SubModule.cs, Main/_Module/ModuleData/armour_acquisition/armour_classes.xml, Main/_Module/ModuleData/culture_doctrine/culture_doctrines.json, Main/_Module/ModuleData/taom_spcultures.xml, Main/_Module/ModuleData/troops/troops_feral_orcs.xml, TAOM.Tests/Features/BanditManagement/BanditHideoutLiveDataTests.cs, TAOM.Tests/Features/CultureDoctrine/ShippedCultureDoctrinesConfigTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, tools/ranged_ladders.json, tools/taom_schema.py, tools/translation_name_tags.json
- `cd06da904` 2026-10-29 PORT-PARTIAL P2 pick=conflict :: fix(bandits): v2.0.36 - guard bandit clans and dens that load apart
  - why: Patch125 guards two unguarded vanilla lookups that v1.4.8 has verbatim (BanditSpawnCampaignBehavior.cs:493 indexer, :312-326 null clan), turning a TAOM and TAOM_Map mismatch from a new-game crash into a log line.
  - port notes: Take Main/Features/BanditManagement/HideoutClanGuard.cs, Hooks/Patch125_HideoutClanGuard.cs (use the 1ef7d1af6 doc comment), HideoutClanGuardTests.cs, Patch125HideoutClanGuardBindingTests.cs, the harmony-patch-registry row, the guard section of docs/features/bandit-management.md and the lessons/harmony-il.md entry. Engine fit on v1.4.8: GetInfestedHideoutCount(Clan) is private at BanditSpawnCampaignBehavior.cs:490; _hideouts is Dictionary<CultureObject, List<Hideout>> (line 23); AddBanditToHideout(Hideout hideoutComponent, ...) is public at :312; the Gold Rush InfestNearbyHideout path exists in IncidentsCampaignBehaviour.cs. Register it in Main/SubModule.cs (single-owner) with 1.4.x's _harmony.PatchCategory("Patch125_HideoutClanGuard"). 1.4.x has no TryPatchCategory wrapper, and Patch125 is free there (its highest category is Patch85). Leave all Feral Orcs content: armour_classes.xml, culture_doctrines.json, taom_spcultures.xml, ranged_ladders.json, taom_schema.py, the BR/IT strings, feral-orcs.md, the release-process channel check and the Codex prompt. The guard is defensive with today's pairing: the laptop's TAOM_Map has dens for all 8 of 1.4.x's bandit cultures. It only fires with a TAOM_Map older than the May 2026 wave-2 cultures (4ed298b03).
  - conflicts: Main/SubModule.cs, Main/_Module/ModuleData/armour_acquisition/armour_classes.xml, Main/_Module/ModuleData/culture_doctrine/culture_doctrines.json, Main/_Module/ModuleData/taom_spcultures.xml, TAOM.Tests/Features/BanditManagement/BanditHideoutLiveDataTests.cs, tools/ranged_ladders.json, tools/taom_schema.py
  - issues: 1063
- `1ef7d1af6` 2026-10-29 PORT-PARTIAL P2 pick=conflict :: fix(bandits): v2.0.36 - Feral Orcs review fixes, second wave
  - why: Review follow-up to Patch125 (its notes, the _hideouts reflection-site pin and the snapshot rows); the rest is Feral Orcs content.
  - port notes: Take only four pieces: the Patch125 doc-comment refinement; the BanditSpawnCampaignBehavior._hideouts DataRow in TAOM.Tests/Migration/ReflectionSiteBindingTests.cs; the matching rows in docs/reference/taleworlds-api-snapshot/reflection-sites.md and patch-targets.md (both files exist on 1.4.x); and the harmony-patch-registry caller list. Leave troops_feral_orcs.xml, the 12 translation caches, translation_name_tags.json, ShippedCultureDoctrinesConfigTests and the feral-orcs docs. Fold this into the cd06da904 port.
  - prereqs: cd06da904
  - conflicts: Main/Features/BanditManagement/Hooks/Patch125_HideoutClanGuard.cs, Main/_Module/ModuleData/troops/troops_feral_orcs.xml, TAOM.Tests/Features/CultureDoctrine/ShippedCultureDoctrinesConfigTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, tools/translation_name_tags.json
  - issues: 1063
skipped in this group: `0799af376` SKIP-CONTENT feat(bandits): v2.0.36 - Feral Orcs, an orc bandit faction in 23 dens

### group `issue-1064-supply-desertion-port`  P0  track=crash-robustness  commits=1  sim: clean=0 conflict=1
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/SupplyLines/SupplyCaravanService.cs, Main/Features/SupplyLines/SupplyRouteVisualService.cs, TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionBehaviorTests.cs
- `ab78564e0` 2026-10-30 PORT-PARTIAL P0 pick=conflict :: fix(campaign): v2.0.36 - port PR #1064 supply and desertion fixes
  - why: 1b00d1acd already brought the PR #1064 fixes to 1.4.x, and this port adds an endpoint-face validity guard before the native path query, mirroring vanilla ComputePath's checks (v1.4.8 MobileParty.cs:3696-3712), plus the MSTest coverage 1.4.x never got.
  - port notes: Take the AddLandPath extraction, with its !fromFace.IsValid() || !toFace.IsValid() early return, into Main/Features/SupplyLines/SupplyCaravanService.cs. ComputeNavPathPoints on the 1.4.x tip already passes the land rules (1b00d1acd), so only the extraction and the guard are new. The 1.5.x RCA (finding 1) names TAOM's teleported caravan position as the likely off-navmesh input, a candidate for the unisolated 1.4.8 crash. Engine fit on v1.4.8: PathFaceRecord.IsValid() exists (TaleWorlds.Library/PathFaceRecord.cs:20), and IMapScene.GetPathBetweenAIFaces(..., regionSwitchCostFromLandToSea, regionSwitchCostFromSeaToLand) at IMapScene.cs:34 matches the 1.5.x argument slots. Take the three NSubstitute AddLandPath tests in TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs. Its two IL-fingerprint tests need IlCallScanner.Fingerprint, which 1.5.x added in a27c6b338 and 1.4.x's IlCallScanner lacks. The arrows test must be rewritten for 1.4.x's own fallback in Main/Features/SupplyLines/SupplyRouteVisualService.cs (scene.GetHeightAtPoint(Vec2, BodyFlags, ref float)). Optionally adopt the RCA's design D, IMapScene.GetHeightAtPoint(in CampaignVec2, ref float), which exists on v1.4.8 (IMapScene.cs:52). Adapt the map-event desertion tests to 1.4.x's two-argument AlignmentDesertionBehavior constructor; the test file is absent on 1.4.x (1.5.x created it in 423d948d8). Leave Client/: 1b00d1acd already guards the arrows in Main/. Build the 1.4.x tip first, because 1b00d1acd was merged unbuilt.
  - conflicts: Main/Features/SupplyLines/SupplyCaravanService.cs, Main/Features/SupplyLines/SupplyRouteVisualService.cs, TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionBehaviorTests.cs
  - issues: 1064, 1068, 750

### group `issue-1076-hollow-parent-birth`  P0  track=crash-robustness  commits=2  sim: clean=0 conflict=2
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/HeroCharacterStateAdapter.cs, Main/Features/StaleCharacterRepair/Hooks/Patch83_OffspringParentRepair.cs, Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs, TAOM.Tests/Features/StaleCharacterRepair/HeroCharacterStateAdapterTests.cs, TAOM.Tests/Features/StaleCharacterRepair/Patch83HooksHarmonyTests.cs, TAOM.Tests/Features/StaleCharacterRepair/StaleCharacterParentRepairTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, tools/ingame_checks/1076.toml, tools/tests/test_ingame_verdict.py
- `1165a00de` 2026-11-02 PORT-PARTIAL P0 pick=conflict :: fix(stale-character): v2.0.36 - repair a hollow parent before a birth
  - why: Fixes a campaign-map CTD on child birth (NullReferenceException in CharacterObject.get_StealthEquipments) by repairing a hollow parent character in a Prefix on HeroCreator.DeliverOffSpring; the same engine path and the same StaleCharacterRepair code exist on 1.4.x.
  - port notes: StaleCharacterRepair and StaleCharacterAdapter are identical on 1.4.x and the 1.5.x parent (git diff origin/bannerlord-1.4.5 1165a00de^ over Main/Features/StaleCharacterRepair and the adapter files is empty), so the Main, adapter, test, adapters.md hollow-character rule, feature doc, harmony registry and RCA hunks apply. Engine path verified in the v1.4.8 dump: HeroCreator.cs:258-287 (DeliverOffSpring; CreateHero logs 'creating hero from template' and calls CharacterObject.CreateFrom), BasicCharacterObject.cs:193-195 (FillFrom copies the raw _culture), Hero.cs:1236-1239 and 2237-2256 (SetInitialValuesFromCharacter reads StealthEquipments before HeroObject is set), CharacterObject.cs:136-146 (Culture dereference), MBObjectBase.cs:24-25 and 70-72 (IsRegistered is saved). 1.4.x TaomHeroCreationModel also returns the same-sex parent's CharacterObject. Every member the new adapter touches exists on 1.4.8: CharacterObject._originCharacter (:24), public InitializeHeroCharacterOnAfterLoad (:519), BasicCharacterObject.Race and Culture public setters (:76, :124-134). StaleCharacterRepairService gains an IOutcomeLog ctor parameter: port cb4eeaa3d first or the IoC resolve fails; otherwise swap its check lines for IModLogger lines rather than gate this P0 on the P2 logging work. tools/ingame_checks/1076.toml, its fixtures, test_ingame_verdict.py and ingame-verdicts.md only with c8d69e426. ReflectionSiteBindingTests (exists on 1.4.x) gains one row.
  - conflicts: TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, tools/tests/test_ingame_verdict.py
  - issues: 1076
- `de90a2451` 2026-11-02 PORT-PARTIAL P0 pick=conflict :: fix(stale-character): v2.0.36 - keep a restored race in hollow repair
  - why: Follow-up so the hollow-parent repair keeps a registered hero's race restored at session launch instead of overwriting it, and a birth that ends with no child and no exception writes a fail line.
  - port notes: Ride with 1165a00de. 1.4.x RacePersistenceBehavior also restores races at OnSessionLaunched (Main/Features/HeroRace/RacePersistenceBehavior.cs:20 and 35-38 on origin/bannerlord-1.4.5), so the race-keep logic is needed there too. The outcome-line change needs cb4eeaa3d or the same IModLogger swap; the 1076.toml change needs c8d69e426; the REVIEW-LOG, codex-track-record and Codex prompt records are bookkeeping.
  - prereqs: 1165a00de
  - conflicts: Main/Adapters/HeroCharacterStateAdapter.cs, Main/Features/StaleCharacterRepair/Hooks/Patch83_OffspringParentRepair.cs, Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs, TAOM.Tests/Features/StaleCharacterRepair/HeroCharacterStateAdapterTests.cs, TAOM.Tests/Features/StaleCharacterRepair/Patch83HooksHarmonyTests.cs, TAOM.Tests/Features/StaleCharacterRepair/StaleCharacterParentRepairTests.cs, tools/ingame_checks/1076.toml
  - issues: 1076

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 7, batch 16, batch 17, batch 18, batch 20, batch 28, batch 29, batch 30, batch 32
