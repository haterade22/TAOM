# Input pack: WP 133 campaign-fixes-inventory-ui

Title (draft): Inventory, UI data, banner colour and other campaign UI fixes
Wave: 4   Lane: E   Draft priority: P1   Groups: 11   Port commits: 19
Depends on (WP ids): 105

## Orchestrator notes for this WP (binding)
- specialresources-map-bar-entry needs fee906345 (#958, in ui-data-sweep-2026-10-23).
- bd457ee6e (ui-data-sweep) is needed by WP136's f963c6188.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Port groups owned by this WP (from the commit classification)

### group `review-130-config-list-entries`  P2  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/SignatureStrikes/Domain/StrikeNames.cs
- `b2e387db7` 2026-09-23 PORT-PARTIAL P2 pick=conflict :: fix(config): v2.0.30 - Review 130 follow-ups: list entries, formations
  - why: DreadAura, UncapturableHeroes and BannerBearers config loaders drop blank list entries and trim padded ones, and AllowedFormationGroups takes declared names only; the 1.4.x tip has the same unfiltered lists and Enum.TryParse (BannerBearerConfigProvider.cs:177).
  - port notes: Take Main/Core/Validation/EnumNames.cs (new on 1.4.x; reword its doc comment without the SignatureStrikes example), the three providers' ValidateList changes, the AllowedFormationGroups fix, their tests and the three feature-doc lines. Drop the StrikeNames.cs hunk (SignatureStrikes is 1.5.x-only). The CustomAttacksUtils edits are comments only and assume c79a58521's version of that file; optional. No behaviour change with the shipped configs.
  - conflicts: Main/Features/SignatureStrikes/Domain/StrikeNames.cs

### group `arena-troll-gear-prizes`  P2  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/ArmourAcquisition/ArmourMarketplaceGate.cs, Main/Features/ArmourAcquisition/IArmourGateService.cs, Main/Features/CultureMarketplace/IMarketplaceStockGate.cs, TAOM.Tests/Features/ArmourAcquisition/ArmourMarketplaceGateTests.cs, tools/taom_schema.py
- `bc39f6e4d` 2026-10-02 PORT-PARTIAL P2 pick=conflict :: fix(arena): v2.0.32 - troll gear unobtainable, prizes capped at heavy
  - why: Mostly balance and Armory content built on the 1.5.x-only ArmourAcquisition gate. One part fixes a bug 1.4.x shares: culture markets stocking items the Armory marks not for sale.
  - port notes: Re-implement rather than cherry-pick. On 1.4.x the CultureMarketplace pool takes every ItemObject (ItemPoolAdapter.GetAllItems, lines 70-78 on origin/bannerlord-1.4.5), and d906fc921 blacklisted only the Mouth of Sauron's helm; add a NotMerchandise skip to the pool, with a test. Optional, only if the maintainer wants trolls-only gear on 1.4.x: the CREATURE_GEAR_OBTAINABLE check in validate_moduledata.py and taom_schema.py, tools/lock_creature_gear.py and the commit-hook line (together they gate an Armory content decision). Leave the TournamentPrizeRules prize bands (balance), the ArmourAcquisition and IMarketplaceStockGate edits (1.5.x-only) and the Armory troll edits. The claim that an empty list crashes the prize roll is unproven on 1.4.x: TAOM's model falls back to vanilla's list, which also gathers other cultures' items (DefaultTournamentModel.cs:95-120 in the v1.4.8 dump).
  - conflicts: Main/Features/ArmourAcquisition/ArmourMarketplaceGate.cs, Main/Features/ArmourAcquisition/IArmourGateService.cs, Main/Features/CultureMarketplace/IMarketplaceStockGate.cs, TAOM.Tests/Features/ArmourAcquisition/ArmourMarketplaceGateTests.cs, tools/taom_schema.py

### group `issue-789-fief-nav-button`  P2  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/GUI/Brushes/MapBar.xml, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
- `ca248d1b7` 2026-10-09 PORT-PARTIAL P2 pick=conflict :: feat(fiefs): v2.0.34 - a Fiefs button on the campaign map bar
  - why: Besides the new map-bar Fiefs button (feature), it centralises menu-push checks in MapMenuGate, adding escape-menu, CurrentMenuContext and vanilla GetMapScreenActionIsEnabledWithReason (prisoner, encounter, raft, ferry) refusals that the 1.4.x F6 hook and field camp query lack.
  - port notes: Take Main/Adapters/MapMenuGate.cs and route Patch36_MapScreenF6 and MapScreenCampMenuActivationQuery.IsMapScreenClear through it (both exist on origin/bannerlord-1.4.5 with the shorter guard list). MapScreen.IsEscapeMenuOpened and CampaignUIHelper.GetMapScreenActionIsEnabledWithReason exist in v1.4.8 (CampaignUIHelper.cs:2387). Keep 1.4.x's EnlistedAttached refusal in F6. Leave the button (Patch36_MapNavigationElements, TaomFiefsNavigationElement, FiefHubOpener, FiefHubHostAdapter, MapBar.xml brush, new strings) and the FiefManagementNavItemVM deletion unless the button is wanted.
  - conflicts: Main/_Module/GUI/Brushes/MapBar.xml, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
  - issues: 782, 789

### group `issue-917-quickactions-unequip`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/PlayerEquipmentAdapter.cs, TAOM.Tests/Core/MainPartyNullCheckScanTests.cs
- `1888b6f28` 2026-10-19 PORT P1 pick=conflict :: fix(quickactions): v2.0.35 - Unequip All updates the open inventory
  - why: Quick Actions Unequip All writes equipment slots directly so the open inventory stays stale and Cancel cannot undo it; the same direct-write call is on 1.4.x (QuickActionsService.cs:209).
  - port notes: QuickActionsService.cs, InventoryVMAdapter.cs and IInventoryVMAdapter.cs on origin/bannerlord-1.4.5 equal the fix's parent; PlayerEquipmentAdapter.cs differs only by an import and PartyBase.MainParty (the method is deleted anyway). Drop the TAOM.Tests/Core/MainPartyNullCheckScanTests.cs hunk (file is 1.5.x-only). v1.4.8 SPInventoryVM has the private UpdateRightCharacter (:4377) and RefreshInformationValues (:3968) the fix binds, same lines as the v1.5.5 cites. DryIoc constructor injection (QuickActionsIoC.cs) needs no wiring edit; the ReflectionSiteBindingTests rows and reflection-sites doc come along.
  - conflicts: Main/Adapters/PlayerEquipmentAdapter.cs, TAOM.Tests/Core/MainPartyNullCheckScanTests.cs
  - issues: 917

### group `issue-937-tavern-specialities`  P2  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs
- `d39370059` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: fix(taverns): v2.0.35 - tavern maids offer a dish in every town
  - why: In a TAOM-culture town the tavern maid offers the previous town's dishes or blank names, and v1.4.8 sets DRINK and FOOD only inside vanilla-culture branches.
  - port notes: Take the prefix on TavernEmployeesCampaignBehavior.conversation_tavernmaid_offers_food_on_condition (present in v1.4.8 SandBox at :729 with per-culture branches and no default), its two strings (taom_tavern_drink_default, taom_tavern_food_default) seeded in the 12 language files, the binding test and the registry rows. Replace TavernSpecialitiesModule (Main/Composition) with a PatchCategory call in SubModule.cs and rewrite the wiring test; Patch111 is free on 1.4.x (highest literal category Patch88).
  - conflicts: Main/Composition/FeatureModules.cs
  - issues: 937, 941

### group `issue-932-banner-bearer-cultures`  P2  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
- `210943076` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: fix(banners): v2.0.35 - Lindon and Blue Craig troops raise banners
  - why: BannerBearers exists on both lines and the 1.4.x config has no lindon or bluecraig rows, while those cultures' infantry troops march in 1.4.x party templates, so their formations raise no banner; adds the rows and a culture coverage test.
  - port notes: Code merges cleanly (the 1.4.x files differ from the pre-image only by the 1.5.x-only arthedain row and one test). Adapt the new ShippedConfig_MapsEveryInfantryTroopCultureToABanner test: drop the wild_trolls NoBannerByDesign entry (no wild_trolls troops exist on 1.4.x, so its stale assertion would fail), then run it on 1.4.x data because other cultures may also lack rows. Take the banner-bearers.md and new-culture-authoring.md hunks; drop the lord-banners.md hunk (1.5.x-only doc).
  - issues: 932

### group `issue-955-liege-titles`  P2  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Core/CultureRaceConsistencyTests.cs
- `30003668b` 2026-10-20 PORT-PARTIAL P2 pick=conflict :: fix(titles): v2.0.35 - every realm's ruler has a liege title
  - why: Completes the liege-title override that both lines share (3e210cd0c, pre-fork): 1.4.x overrides only the six vanilla cultures, so v1.4.8 BecomeKingSceneNotificationItem.cs:34 shows 'ERROR: Text with id str_liege_title' for TAOM realms and LordConversationsCampaignBehavior.cs:95 leaves lords' introductions without a title.
  - port notes: Take the #955 part: TaomCultureTags.cs without TaomArthedainCultureTag (arthedain is 1.5.x-only; the other 16 tag cultures are defined in the 1.4.x taom_spcultures.xml); the str_liege_title rows in taom_module_strings.xml and the 12 language files (English placeholders); the module_strings.xslt str_become_king_empire strip (1.4.x has 16 extra lines there, so resolve context); the comment_strings.xslt comment and the LiegeTitleOverrideTests additions, including EveryTaomConversationTag_IsInstantiableAndUnique (both files are identical to the pre-image on 1.4.x); and the csharp-patterns.md ConversationTag rule (these would be the first ConversationTag subclasses on 1.4.x, and one bad tag breaks every campaign start). Leave #956: the CultureRaceConsistencyTests refactor and its EveryCharacterRace_HasMaleAndFemaleFacegenSet test (it needs an Armory-side Sauron facegen set and hard-codes an E:\Steam path), the armory snapshot and the animation-skeleton lesson. Leave the crown-guard.md edits; write a 1.4.x liege-titles doc rather than copying the 1.5.x one.
  - conflicts: TAOM.Tests/Core/CultureRaceConsistencyTests.cs
  - issues: 763, 955, 956

### group `inventory-sweep-2026-10-23`  P1  track=shared-feature-fix  commits=5  sim: clean=0 conflict=5
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Client/Features/TelcharsMark/Hooks/Patch109_SPInventoryVMAfterTransfer.cs, Main/Adapters/IInventoryVMAdapter.cs, Main/Adapters/InventoryVMAdapter.cs, Main/Features/QuickActions/QuickActionsSettingsProvider.cs, TAOM.Tests/Adapters/InventoryPanelRefreshTests.cs
- `89e7af6c0` 2026-10-23 PORT-PARTIAL P1 pick=conflict :: fix(equippresets): v2.0.35 - preset load refreshes the paper doll
  - why: After an equipment preset load, the 1.4.x adapter calls SPInventoryVM.RefreshValues (origin/bannerlord-1.4.5 Main/Adapters/InventoryScreenAdapter.cs:62), which on v1.4.8 only resets labels and hints (SPInventoryVM.cs:3182), so the doll, armour, weight, capacity, no-saddle warning and Done button stay stale.
  - port notes: Needs 1888b6f28 first: it adds the UpdateRightCharacter and RefreshInformationValues bindings to InventoryVMAdapter, which 1.4.x lacks. Take InventoryVMAdapter.RefreshPanel, the InventoryScreenAdapter and IInventoryScreenAdapter change, InventoryPanelRefreshTests, equip-presets.md and the registry and reflection-sites rows. Leave Client/Features/TelcharsMark/Hooks/Patch109_SPInventoryVMAfterTransfer.cs (Telchar's Mark is 1.5.x-only). The target members exist on v1.4.8 (UpdateRightCharacter :4377, RefreshInformationValues :3968, ExecuteRemoveZeroCounts :3437); re-check the other cited v1.5.5 line numbers against v1.4.8.
  - prereqs: 1888b6f28
  - conflicts: Client/Features/TelcharsMark/Hooks/Patch109_SPInventoryVMAfterTransfer.cs, Main/Adapters/InventoryVMAdapter.cs
  - issues: 924
- `eb89a3e53` 2026-10-23 PORT P2 pick=conflict :: fix(quickactions): v2.0.35 - batch sells remove sold-out rows
  - why: Quick Actions batch sells leave sold-out rows at count 0 because the sell path skips vanilla's prune; the batch refresh now ends with ExecuteRemoveZeroCounts through the shared RefreshPanel helper.
  - port notes: Apply after 1888b6f28 (RefreshDisplay and its bindings) and 89e7af6c0 (RefreshPanel); no 1.5.x-only code is involved. Re-check the cited line (:3565-3568) on v1.4.8.
  - prereqs: 1888b6f28, 89e7af6c0
  - conflicts: Main/Adapters/IInventoryVMAdapter.cs, Main/Adapters/InventoryVMAdapter.cs, TAOM.Tests/Adapters/InventoryPanelRefreshTests.cs
  - issues: 926
- `cd999f917` 2026-10-23 PORT P2 pick=conflict :: fix(quickactions): v2.0.35 - drop the "include equipped" sell options
  - why: Removes the 'Sell Damaged Equipped' and 'Sell Low Value Equipped' MCM options, which exist on 1.4.x (TaomSettings.cs:655-669) and only gate an IsEquipped check that items in the inventory list never satisfy.
  - port notes: Applies to 1.4.x directly: IsEquipped has no consumer there beyond QuickActionsService.cs:263 and :276, and CoopSettingsRelevance.cs:90-91 lists the two keys. Expect bookkeeping conflicts in SettingsFingerprintTests.cs and the co-op docs; the review prompt doc is optional.
  - conflicts: Main/Features/QuickActions/QuickActionsSettingsProvider.cs
  - issues: 925
- `97426dcd0` 2026-10-23 PORT P1 pick=conflict :: fix(banner): v2.0.35 - inventory doll keeps the clan colours
  - why: Banner Color Persistence repaints the inventory doll in clan colours only on a character switch; retargeting the postfix to SPInventoryVM.UpdateCharacterArmorColor (on v1.4.8 at SPInventoryVM.cs:4421, called from UpdateRightCharacter at :4381) covers every repaint, and the stealth-mode skip uses members v1.4.8 also has.
  - port notes: The 1.4.x patch file is identical to the 1.5.x pre-fix version (empty git diff), so the code applies cleanly; re-cite the v1.5.5 line numbers to v1.4.8 (4421 and 4381 match). Take SPInventoryVM_ArmorColor_PatchTests, banner-color-persistence.md and the harmony-il lesson; regenerate the patch-targets.md row rather than copying it.
  - issues: 973
- `399a7fe27` 2026-10-23 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.35 - inventory fixes, docs and lessons catch up
  - why: Docs and two code comments catching up with the inventory fixes; the Patch33 comment's claim that RefreshValues fires once, from the constructor, also holds on v1.4.8 (SPInventoryVM.cs:3163).
  - port notes: Take the equip-presets.md and quick-actions.md text, the feature-map Equipment Presets row, the harmony-patch-registry rows, the adapters and testing-qa lessons, and the Patch33 comment (cite v1.4.8:3163). The Clan_UpdateBannerColorsAccordingToKingdom_Patch comment names PatchCategoryApplier, which exists only with 45bcf80bd: reword it for the 1.4.x patch-application path unless 45bcf80bd is ported first (1.4.x uses Harmony 2.4.2, as the comment says). The rca-quickactions-unequip-2026-10-19.md edit needs that RCA, which arrives with 1888b6f28.
  - prereqs: 1888b6f28, 89e7af6c0, eb89a3e53, cd999f917, 97426dcd0
  - issues: 924, 925, 926, 973

### group `ui-data-sweep-2026-10-23`  P2  track=shared-feature-fix  commits=4  sim: clean=0 conflict=4
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/IArmouryPlayerAdapter.cs
- `fee906345` 2026-10-23 PORT-PARTIAL P2 pick=conflict :: fix(resources): v2.0.35 - map-bar resource shows on the first frame
  - why: The special-resource map-bar entry skips the first MapInfoVM refresh and appears about two seconds late; the same _baseInitialized/_itemAdded gate is on 1.4.x (Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs:31-32, 93-97, 123).
  - port notes: Apply the mixin change to Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs (1.5.x moved the file to Client/). Take SpecialResourceMapBarMixinWiringTests with its source path pointed at Main/, plus the special-resources.md and gui-sprite-system.md lines. The commit comment says the 2 s refresh interval is the same on v1.4.8.
  - issues: 958
- `645068836` 2026-10-23 PORT-PARTIAL P3 pick=conflict :: fix(relations): v2.0.35 - relation notes match the engine
  - why: Drops the ignored affectRelatives: true argument and corrects the 'spreads to kin' notes; v1.4.8's ChangeRelationAction.ApplyPlayerRelation also ignores the flag (ChangeRelationAction.cs:30-33 in the dump), and the only 1.4.x caller is Main/Adapters/CommanderLordAdapter.cs:199.
  - port notes: Take the CommanderLordAdapter change (cite v1.4.8 instead of v1.5.5) and AffectRelativesGateTests. Leave IArmouryPlayerAdapter.cs, which is not on 1.4.x.
  - conflicts: Main/Adapters/IArmouryPlayerAdapter.cs
  - issues: 923
- `bd457ee6e` 2026-10-23 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - kingdom generator keeps its block in place
  - why: The kingdom generator's upsert() deletes its marker block and re-appends it before the closing tag, which moves it below later blocks and misses stale duplicates; the 1.4.x copy has the same upsert (origin/bannerlord-1.4.5 tools/generate_new_faction_kingdoms.py:97-102).
  - port notes: Port the upsert() rewrite (replace in place; refuse a second region, a lone BEGIN or END, or a missing closing tag) into the older 1.4.x generator, which lacks the Arthedain entries. Take the new test file with its marker renamed: it tests upsert on synthetic strings, not Arthedain data. Take recipe-add-a-kingdom.md, the new-factions-misty-mountains-lindon.md lines and the testing-qa lesson; leave dale-lord-expansion.md (Dale is not on 1.4.x).
  - issues: 947
- `60dbf6429` 2026-10-23 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.35 - RCA and lessons for the UI and data sweep
  - why: RCA and lessons for the three sweep fixes above, all of which port to 1.4.x.
  - port notes: Take rca-ui-data-sweep-2026-10-23.md and the #947 lesson ('A fix that removes a false positive must keep what the old code caught by accident'). The 'Recurrence 2026-10-23 (#958)' bullet hangs off an ADR-013 B4c lesson that is not on 1.4.x: fold it into a standalone note or drop it.
  - prereqs: fee906345, 645068836, bd457ee6e
  - issues: 923, 947, 958

### group `specialresources-map-bar-entry`  P1  track=shared-feature-fix  commits=2  sim: clean=1 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs, TAOM.Tests/Features/SpecialResources/SpecialResourceMapBarMixinWiringTests.cs
- `406dd739f` 2026-10-23 PORT-PARTIAL P1 pick=conflict :: fix(resources): v2.0.35 - map-bar resource leaves when the hero loses it
  - why: On 1.4.x the map-bar mixin also returns early when the hero has no resource and never removes the entry (origin/bannerlord-1.4.5 Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs, `if (resource == null) return;`), so a hero who loses a culture resource keeps a stale counter until reload.
  - port notes: Port fee906345 (#958: insert on the first refresh behind Contains; it also creates SpecialResourceMapBarMixinWiringTests) first. The 1.4.x OnRefreshCore still has the _baseInitialized/_itemAdded latch and otherwise matches 1.5.x before fee906345 (remaining diffs are unrelated tooltip/nullable/#767 lines). Apply this hunk to Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs; the Client/ path exists only on 1.5.x. The wiring test reads source through TAOM.Tests.Infrastructure.RepoPaths.ReadSource, absent on 1.4.x (added on 1.5.x by 35007dbb8): copy the helper or inline a reader, and point it at Main/. In .claude/rules/gui-ui.md drop the v1.5.5 mention. In docs/features/special-resources.md re-derive the kingdom count from 1.4.x data rather than copying '11 of the 15'.
  - prereqs: fee906345
  - conflicts: Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs, TAOM.Tests/Features/SpecialResources/SpecialResourceMapBarMixinWiringTests.cs
  - issues: 977
- `8e7d1f4cb` 2026-10-23 PORT P3 pick=clean :: docs(gui): v2.0.35 - collection advice matches the ownership rule
  - why: 1.4.x has the same contradiction: .claude/rules/gui-ui.md:71 says 'Discouraged, on ownership grounds' while docs/features/gui-sprite-system.md:385 still says 'Do NOT add to collection properties'.
  - port notes: Picks clean, but its advice (add on every refresh behind Contains, never skip the first refresh, remove when it no longer applies) describes 1.4.x code only after fee906345 and 406dd739f are ported, so land it with or after them.
  - prereqs: fee906345, 406dd739f
  - issues: 958

### group `issue-993-994-banner-colors-refuge-rows`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs, Main/Features/Refuge/RefugeIoC.cs, TAOM.Tests/Features/Refuge/RefugeWiringTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
- `365f2ecfe` 2026-10-24 PORT-PARTIAL P1 pick=conflict :: fix(ui): v2.0.35 - clan colours reach portraits, refuge rows keep names
  - why: BannerColorPersistence's portrait patches set Color1/Color2 after the CharacterCode string is built, so no clan colour ever reaches a portrait, and Refuge clan rows lose "(being raised)" on refresh; 1.4.x carries both bugs, and v1.4.8 confirms the mechanism (CharacterCode.cs:18 Code has a private setter filled at creation, :30-32 plain colour setters, :87 the 8-argument CreateFrom).
  - port notes: Banner half applies nearly as is: ClanColoredCharacterCode.Recolor, the five postfixes (CampaignUIHelper and SandBoxUIHelper need ref added, both take CharacterCode by value at :22 on 1.4.x; PartyCharacterVM reads type and side from its arguments), Order of Battle pool colouring, the three new tests. Refuge half needs v1.4.8 adaptation: patch ClanPartyItemVM.UpdateProperties (public, ClanPartyItemVM.cs:1125, namespace ...ViewModelCollection.ClanManagement; RefreshValues calls it at :1122) instead of 1.5.x's ClanPartyItemWithPartyVM; drop [ClientOnly] (no ClientOnlyAttribute on 1.4.x) and the TAOM.Composition.ClientComposition guard; initialize with IRefugeBook in 1.4.x's RefugeIoC.InitializePatchStatics (GetByPartyId exists, RefugeDefenseService.cs:16). 1.5.x dropped the PartyWageSubTitleText rewrite because v1.5.5 binds it nowhere; check v1.4.8's ClanPartiesRightPanel.xml before dropping it on 1.4.x. Update harmony-patch-registry, the API snapshot docs and ReflectionSiteBindingTests.
  - conflicts: Main/Features/Refuge/Hooks/RefugeClanScreenPatch.cs, Main/Features/Refuge/RefugeIoC.cs, TAOM.Tests/Features/Refuge/RefugeWiringTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
  - issues: 993, 994

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 3, batch 12, batch 15, batch 20, batch 21, batch 23, batch 24, batch 26
