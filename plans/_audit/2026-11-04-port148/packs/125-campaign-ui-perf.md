# Input pack: WP 125 campaign-ui-perf

Title (draft): Open the campaign screens faster (Shadowfax screen-open series) and cut the enlistment board's render cost
Wave: 3   Lane: C   Draft priority: P2   Groups: 2   Port commits: 12
Depends on (WP ids): 117, 118

## Orchestrator notes for this WP (binding)
- Decision D6: port the perf series as ONE squashed change at its net pre-merge state; do NOT port f48ef74dc's instant transitions (a UX change with no setting).
- ea7cce7bc uses the 346f3e093 stamp clock (WP118); 943ab74c0's probe needs MapPerf (f6bf0e588, WP117).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Port groups owned by this WP (from the commit classification)

### group `issue-1019-enlistment-board-render`  P2  track=perf-campaign  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Dependencies/Foundation/PatchShieldPolicy.cs, Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs, Main/Features/Enlistment/ServiceStatusService.cs, Main/Features/MapPerf/Hooks/MapFrameProfilerHooks.cs, Main/Features/MapPerf/Hooks/MapFrameProfilerInstaller.cs, Main/Features/MapPerf/Hooks/MapProfilerTargets.cs, Main/Features/MapPerf/MapProfileLines.cs, TAOM.Tests/Composition/PatchCategoryNumberTests.cs, TAOM.Tests/Features/MapPerf/MapFrameProfilerBindingTests.cs, TAOM.Tests/Features/MapPerf/MapFrameProfilerHooksTests.cs, TAOM.Tests/Features/MapPerf/MapFrameProfilerInstallerTests.cs, TAOM.Tests/Features/MapPerf/MapProfileLinesTests.cs, TAOM.Tests/Features/MapPerf/MapProfilerTargetsTests.cs
- `943ab74c0` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: perf(enlistment): v2.0.35 - the service board is drawn once per change
  - why: Turns the enlistment service board into plain text once per change instead of having the wait menu re-render a nested TextObject every frame, which 1.4.x still does (ServiceStatusTextWriter.cs:67); the profiler probes in the same commit belong to the 1.5.x-only MapPerf feature.
  - port notes: Take the flat render in ServiceStatusTextWriter (set NEWLINE, push text.ToString(), remember the rendered language, IsStale), the language check in ServiceStatusService.RefreshIfChanged, ServiceStatusServiceTests and ServiceStatusTextWriterRenderTests, and optionally the #1019 join-from-inside-a-settlement log line in EncounterAdapter (anchor at EncounterAdapter.cs:225 on 1.4.x; its comment cites v1.5.5 StartBattleAction, re-check on v1.4.8). Both service files drifted (about 80 and 74 changed lines against 943ab74c0^), so hand-apply. Leave Patch101_MapFrameProfilerMenu, MapFrameProfiler*, MapProfilerTargets, MapProfileLines, their tests, map-perf-profiler.md, PatchCategoryNumberTests and the PatchShieldPolicy GameMenuVM.OnFrameTick entry unless MapPerf (f6bf0e588) is ported. Benefits enlisted players only.
  - conflicts: Dependencies/Foundation/PatchShieldPolicy.cs, Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs, Main/Features/Enlistment/ServiceStatusService.cs, Main/Features/MapPerf/Hooks/MapFrameProfilerHooks.cs, Main/Features/MapPerf/Hooks/MapFrameProfilerInstaller.cs, Main/Features/MapPerf/Hooks/MapProfilerTargets.cs, Main/Features/MapPerf/MapProfileLines.cs, TAOM.Tests/Composition/PatchCategoryNumberTests.cs, TAOM.Tests/Features/MapPerf/MapFrameProfilerBindingTests.cs, TAOM.Tests/Features/MapPerf/MapFrameProfilerHooksTests.cs, TAOM.Tests/Features/MapPerf/MapFrameProfilerInstallerTests.cs, TAOM.Tests/Features/MapPerf/MapProfileLinesTests.cs, TAOM.Tests/Features/MapPerf/MapProfilerTargetsTests.cs
  - issues: 1019

### group `shadowfax-screen-open-speed`  P2  track=perf-campaign  commits=11  sim: clean=0 conflict=11
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/IQuestJournalAdapter.cs, Main/Adapters/QuestJournalAdapter.cs, Main/Composition/FeatureModules.cs, Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/ShadowfaxScreens/Clan/ClanFastOpenCalls.cs, Main/Features/ShadowfaxScreens/Clan/ClanScreenFastPathService.cs, Main/Features/ShadowfaxScreens/Clan/Hooks/ClanManagementVM_Ctor_ClanFastOpen_Patch.cs, Main/Features/ShadowfaxScreens/Clan/Hooks/ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch.cs, Main/Features/ShadowfaxScreens/Clan/Hooks/ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.cs, Main/Features/ShadowfaxScreens/IShadowfaxScreensSettingsProvider.cs, Main/Features/ShadowfaxScreens/Inventory/Hooks/SPInventoryVM_InitializeInventory_ShadowfaxPatch.cs, Main/Features/ShadowfaxScreens/Inventory/InventoryOpenFastPathService.cs, Main/Features/ShadowfaxScreens/Inventory/NameSortRewrite.cs, Main/Features/ShadowfaxScreens/Inventory/ShadowfaxInventoryCalls.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_Ctor_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_InitializeTroopLists_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_OnFinalize_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_RefreshValues_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_TransferAllTroops_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/PartyFastOpenCalls.cs, Main/Features/ShadowfaxScreens/Party/PartyScreenFastPathService.cs, Main/Features/ShadowfaxScreens/Quests/Hooks/QuestItemComparerBase_GetJournalLogAt_QuestLogPatch.cs, Main/Features/ShadowfaxScreens/Quests/Hooks/QuestItemVM_PopulateQuestLog_QuestLogPatch.cs, Main/Features/ShadowfaxScreens/Quests/QuestLogDeferCalls.cs, Main/Features/ShadowfaxScreens/Quests/QuestLogDeferService.cs, Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs, TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs ...
- `ea7cce7bc` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: feat(ui): v2.0.35 - screen open timer and Screen Performance switches
  - why: Base of the campaign screen-open speed series: the Screen Performance switches and an off-by-default open timer.
  - port notes: Port the series ea7cce7bc..b747cf398 as ONE squashed change at its pre-merge net state (leave 963218f7f's [ClientOnly] and Client/ moves). This commit brings ScreenOpenTimer, ScreenOpenTiming, ScreenOpenWindow, IShadowfaxScreensSettingsProvider and its provider, the Screen Performance group in BattleLoadDiagnosticsSettings, and the CoopSettingsRelevance rows. Adapt: 1.4.x has no Main/Composition (TaomFeatureModule, PatchCategoryDecl), so register services in IoC.cs and apply the four patch categories from SubModule.cs (both single-owner). ScreenOpenTimer needs IStampClock and LoadTimeStampLines from LoadTimeStamps (346f3e093), or a Stopwatch in their place. Pick free 1.4.x category names (1.5.x used Patch127-130). CampaignHotPathSettingsProvidersTests is a 1.5.x-only file, and SettingsFingerprintTests needs a row. The code does not depend on f48ef74dc.
  - prereqs: 346f3e093
  - conflicts: Main/Composition/FeatureModules.cs, Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
  - issues: 851, 852, 853, 854
- `11aeb8aee` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: feat(ui): v2.0.35 - clan screen builds its party list once
  - why: Clan screen builds its party list once instead of three times, a premise confirmed on v1.4.8 (ClanPartiesVM.cs:387,401 and ClanManagementVM.cs:1363).
  - port notes: Fold into the series squash. The net clan change is c32662872's call swap inside ClanPartiesVM.RefreshValues plus the ClanManagementVM ctor timer. The GauntletClanScreen_OnActivate patch added here is deleted by c32662872. Re-verify every target signature with taom-src on v1.4.8.
  - prereqs: ea7cce7bc
  - conflicts: Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs, TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs, TAOM.Tests/Features/ShadowfaxScreens/ShadowfaxScreensModuleTests.cs
  - issues: 851
- `33c3bdf1a` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: feat(ui): v2.0.35 - inventory and trade screens skip wasted work
  - why: Inventory and trade screens skip a name sort they replace a moment later.
  - port notes: Fold into the squash. Net after 22695bd01: the self-bailing NameSortRewrite on SPInventoryVM.InitializeInventory (the v1.4.8 orderby is at SPInventoryVM.cs:4508/4511, so check the IL shape) and the SPInventoryVM ctor timer. ConceptLookupRewrite, SourceScopedMemo, InventoryOpenAdapter and the SPItemVM and OnReady patches are removed later; do not port them.
  - prereqs: ea7cce7bc
  - conflicts: Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs
  - issues: 852
- `3f8168228` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: feat(ui): v2.0.35 - party screen reuses troop portraits and item counts
  - why: Party screen builds each troop portrait code once per screen instead of once per row.
  - port notes: Fold into the squash. Net after 579bb93f0: the portrait cache on PartyCharacterVM.GetCharacterCode (private, PartyCharacterVM.cs:1476 in v1.4.8) with the PartyVM ctor, InitializeTroopLists, RefreshValues, TransferAllTroops and OnFinalize hooks, all present in v1.4.8. The item category count cache is removed by 579bb93f0.
  - prereqs: ea7cce7bc
  - conflicts: Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs, TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs
  - issues: 853
- `3db115423` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: feat(ui): v2.0.35 - quest log builds finished quests when selected
  - why: Quest log builds a finished quest's stage list only when the player selects it.
  - port notes: Fold into the squash. All six targets exist in v1.4.8: the QuestItemVM JournalLogEntry ctor (QuestItemVM.cs:346), PopulateQuestLog, UpdateIsUpdated, QuestItemComparerBase.GetJournalLogAt, the QuestsVM ctor and SetSelectedItem. Keep QuestJournalAdapter in Main/Adapters (no Client/ on 1.4.x), and add the ReflectionSiteBindingTests and reflection-sites.md rows.
  - prereqs: ea7cce7bc
  - conflicts: Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs, TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
  - issues: 854
- `c32662872` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: fix(ui): v2.0.35 - clan fast open swaps one call, timer on the VM
  - why: Review fixes for the clan fast path: a call swap inside RefreshValues, the timer moved to the VM ctor, and one module test class.
  - port notes: Fold into the series squash; this is the net clan design to port.
  - prereqs: 11aeb8aee
  - conflicts: Main/Features/ShadowfaxScreens/Clan/ClanFastOpenCalls.cs, Main/Features/ShadowfaxScreens/Clan/ClanScreenFastPathService.cs, Main/Features/ShadowfaxScreens/Clan/Hooks/ClanManagementVM_Ctor_ClanFastOpen_Patch.cs, Main/Features/ShadowfaxScreens/Clan/Hooks/ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch.cs, Main/Features/ShadowfaxScreens/Clan/Hooks/ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.cs, Main/Features/ShadowfaxScreens/IShadowfaxScreensSettingsProvider.cs, Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs, TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Clan/ClanFastOpenBindingTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Clan/ClanFastOpenHookTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Clan/ClanScreenFastPathServiceTests.cs, TAOM.Tests/Features/ShadowfaxScreens/ShadowfaxScreensModuleTests.cs, TAOM.Tests/Features/ShadowfaxScreens/ShadowfaxScreensSettingsProviderTests.cs
  - issues: 851
- `22695bd01` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: fix(ui): v2.0.35 - inventory fast open keeps the name sort skip only
  - why: Review fixes cut the inventory fast path to the name-sort skip and drop a concept cache that saved under a microsecond per row.
  - port notes: Fold into the series squash; port the post-fix inventory shape only.
  - prereqs: 33c3bdf1a
  - conflicts: Main/Features/ShadowfaxScreens/Inventory/Hooks/SPInventoryVM_InitializeInventory_ShadowfaxPatch.cs, Main/Features/ShadowfaxScreens/Inventory/InventoryOpenFastPathService.cs, Main/Features/ShadowfaxScreens/Inventory/NameSortRewrite.cs, Main/Features/ShadowfaxScreens/Inventory/ShadowfaxInventoryCalls.cs, Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs, TAOM.Tests/Features/ShadowfaxScreens/Inventory/InventoryBindingTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Inventory/InventoryOpenFastPathServiceTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Inventory/InventoryOpenPatchShapeTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Inventory/NameSortRewriteTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Inventory/ShadowfaxInventoryCallsTests.cs
  - issues: 852
- `579bb93f0` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: fix(ui): v2.0.35 - party fast open keeps the portrait cache only
  - why: Review fixes cut the party fast path to the portrait cache and drop an item-count cache worth about a millisecond per open.
  - port notes: Fold into the series squash; port the post-fix party shape only.
  - prereqs: 3f8168228
  - conflicts: Main/Features/ShadowfaxScreens/Party/Hooks/PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_Ctor_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_InitializeTroopLists_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_OnFinalize_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_RefreshValues_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/Hooks/PartyVM_TransferAllTroops_ShadowfaxParty_Patch.cs, Main/Features/ShadowfaxScreens/Party/PartyFastOpenCalls.cs, Main/Features/ShadowfaxScreens/Party/PartyScreenFastPathService.cs, Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs, TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Party/PartyFastOpenBindingTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Party/PartyFastOpenPatchShapeTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Party/PartyScreenFastPathServiceTests.cs
  - issues: 853
- `642f92e39` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: fix(ui): v2.0.35 - quest log sort stops boxing, rechecks its hooks
  - why: Review fixes for the quest log: the sort stops boxing and each open rechecks its four guard patches.
  - port notes: Fold into the series squash; keep the adapter in Main/Adapters as on the pre-merge layout.
  - prereqs: 3db115423
  - conflicts: Main/Adapters/IQuestJournalAdapter.cs, Main/Adapters/QuestJournalAdapter.cs, Main/Features/ShadowfaxScreens/Quests/Hooks/QuestItemComparerBase_GetJournalLogAt_QuestLogPatch.cs, Main/Features/ShadowfaxScreens/Quests/Hooks/QuestItemVM_PopulateQuestLog_QuestLogPatch.cs, Main/Features/ShadowfaxScreens/Quests/QuestLogDeferCalls.cs, Main/Features/ShadowfaxScreens/Quests/QuestLogDeferService.cs, Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs, TAOM.Tests/Features/ShadowfaxScreens/Quests/QuestLogDeferCallsTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Quests/QuestLogDeferServiceTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Quests/QuestLogFastOpenBindingTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Quests/QuestLogPatchTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
  - issues: 854
- `402280dc2` 2026-10-19 PORT-PARTIAL P3 pick=conflict :: docs(ui): v2.0.35 - Shadowfax Screens doc, feature map and patch targets
  - why: Feature doc, feature-map row and patch-target snapshot rows for the screen-speed series.
  - port notes: Take the screen-speed sections of docs/features/shadowfax-screens.md (that doc is new on 1.4.x). Drop the instant-transition parts unless f48ef74dc is ported, and drop the ADR-013 dedicated-server paragraph. Add the feature-map row, and regenerate patch-targets.md on 1.4.x rather than copying it.
  - prereqs: ea7cce7bc
- `b747cf398` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: fix(ui): v2.0.35 - Shadowfax Screens convergence fixes and RCA
  - why: Convergence fixes for the series (the quest scope cannot stick if its patch check throws, the clan check reads correctly) plus two lessons and an agent-teams checklist line.
  - port notes: Fold the code fixes into the squash. The two lessons (build-tooling-workflow.md, harmony-il.md) and the agent-teams.md parallel-builder line are generic and portable; the RCA can come along as the series review record.
  - prereqs: c32662872, 22695bd01, 579bb93f0, 642f92e39
  - conflicts: Main/Features/ShadowfaxScreens/Clan/ClanFastOpenCalls.cs, Main/Features/ShadowfaxScreens/Clan/ClanScreenFastPathService.cs, Main/Features/ShadowfaxScreens/Clan/Hooks/ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch.cs, Main/Features/ShadowfaxScreens/Inventory/Hooks/SPInventoryVM_InitializeInventory_ShadowfaxPatch.cs, Main/Features/ShadowfaxScreens/Quests/QuestLogDeferService.cs, Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs, TAOM.Tests/Features/ShadowfaxScreens/Clan/ClanFastOpenBindingTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Clan/ClanFastOpenHookTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Inventory/ShadowfaxInventoryCallsTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Quests/QuestLogDeferServiceTests.cs, TAOM.Tests/Features/ShadowfaxScreens/Quests/QuestLogPatchTests.cs
  - issues: 851, 852, 853, 854
skipped in this group: `e77ef9654` SKIP-SUPERSEDED test(ui): v2.0.35 - module wiring tests cover all four screens

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 29, batch 31
