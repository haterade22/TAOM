# Doc pass of 2026-11-03: open questions

A pass compared `docs/features` and `docs/modding` with the commits newer than each doc's last real edit. A writer corrected what it could prove false, and a second reader tried to refute each correction. These are the statements the writers could not settle from the code: live module data, in-game facts, engine lines not re-read on v1.5.6, and maintainer decisions. Each is a lead and not a finding, because nobody checked them a second time.

462 questions in 245 docs.

## docs/features/TEMPLATE.md

- Audit row G10-093 proposes moving the co-op row text (line 74) to /new-feature or the co-op path rule. That is a structural move into .claude or docs/ai-includes, which are out of scope for this sweep. The line is accurate as written. A maintainer decision is needed.

## docs/features/advanced-combat.md

- Key Files table omits ActionSetLookup, AgentSlotIdentity, CreatureTreeTracker, DropFlaggedItemGuard, MissionThreadGuard, TaomBTLogger, the Hooks folder and Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs. None is a false statement, so left.

## docs/features/advanced-start-options.md

- Line 94 ('The 26 menu-facing rows spent 2026-09-14 to 2026-09-15 in taom_module_strings.xml') is dated history of the #604 incident and was left. Line 191 says the rows exist in all 12 languages as English fallback; I confirmed the kingdom keys are in all 12 files but did not check every row. The test names in the header were not re-run.

## docs/features/ai-party-size.md

- The doc's '4 tests in CareerPassiveServiceTests for the flat-passive frame fix' was not counted; the file has 71 TestMethod attributes and which four belong to the fix was not checked.
- The absent tokens FoodConsumptionBehavior.PartyConsumeFood and GarrisonTroopsCampaignBehavior are vanilla types; taom-src resolves both in v1.5.6, and PartyConsumeFood is called at FoodConsumptionBehavior.cs:46. Left as written.

## docs/features/alignment-aware-execution.md

- Key Files omits Main/Features/Execution/FactionSideRules.cs and the tests FactionSideRulesTests, PartyTupleVanillaParityTests and ShippedMainCultureAlignmentCoverageTests detail; they exist but I could not tell whether they belong in this doc without reading their owners.
- The Vanilla Classes and v1.4.8 flow sections (DefaultExecutionRelationModel, AgingCampaignBehavior and so on) are engine history under a banner that already says so; not re-verified against the v1.5 DLLs.
- The 'every kingdom the game actually loads, verified against the live install' sentence was not re-verified against the live install from this worktree; it now matches the repo's taom_spkingdoms.xml and alignment.json.

## docs/features/alignment-desertion.md

- Commit b2cf46b76 changed only the Player Switcher setting in TaomSettings.cs, which this doc does not cover. MCM labels, JSON defaults, TryResolveOwner at AlignmentDesertionBehavior.cs:96-101 and the test counts (31, 13, 12) match the code.

## docs/features/anduin-vale-angaladh.md

- The 'Merging this branch' steps (guard, apply, cleanup) were left as the record of the procedure; I did not verify that the validator reports 0 errors or that the backups were cleaned up, only that the rows are live.
- The 'Pins that moved with the data' numbers (471 settings, etc.) are dated history; TaomSettings now reflects 474 per SettingsFingerprintTests:227, left as written.
- Parked status confirmed (playable false on both cards in factions.json); in-game checks remain owed.

## docs/features/animalia-elk-moose.md

- The doc says the lord-template generator is broken (#637) and the Mirkwood rosters were edited by hand. Four v2.0.35 and v2.0.36 commits changed taom_lord_template_equipment.xml, and its header still says "DO NOT EDIT MANUALLY". I could not tell whether the generator works now without running it, so the claim stays.
- The doc's other claims match the code. That covers AnimaliaConfig constants, the IoC and SubModule lines, the [Animalia] and [MonsterSize] log formats, the test counts of 5, 7 and 4, the market min_stock rows, the elk_rider career rosters, and AssignMainHeroEquipmentKeepingHorse, which exists in vanilla v1.5.6 (found in the taom-src cache). Item and Monster ids in the live Armory could not be read from this worktree.

## docs/features/animation-variety.md

- Verified unchanged: MCM defaults (TaomSettings.cs:2331-2352), GaitVarietyMissionBehavior is 81 lines, test counts 77/28/49/9/10 (TestMethod plus DataRow count), CoopSettingsRelevance classification. Armory facts (monsters.xml lines, variant sets installed) are in the live module and were not checked.

## docs/features/arena.md

- The absent tokens TournamentModel.GetRenownReward and SandBoxMissionManager are vanilla TaleWorlds names, correct as written. I did not run taom-src to re-check the vanilla line-number citations (FightTournamentGame.cs:365 and similar), which the doc dates to v1.5.3.

## docs/features/armor-balance.md

- Absent token tools/gondor_armors_and_troops.md is described by the doc as living in the lotraom-assets mirror, so it was left as written; confirm only if the mirror is reachable.
- The KINGDOM_CAPS table, the 92 noble troops and test_analyze_kingdom_armour.py (22 cases) were checked and match; dated measurement sections (counts from 2026-09 runs) were left as records.

## docs/features/armory-ref-audit.md

- 'bash tools/test_hooks.sh (208)' was not recounted (no way to count without running the script); settle by running it.
- Component diagram still shows only the generator verify line; left as a diagram of the original design.

## docs/features/armour-acquisition.md

- Line 39 says every live town starts at Barracks 1 or more (19/33/26, elite in 59 of 78), while the culture_marketplace paragraph says 92 towns in the live map (counted 2026-10-27). The live TAOM_Map is outside the worktree; the town count and the 'those cultures own 35 of 92 towns' figure predate the four realms added on 2026-10-29. Re-count from the live settlements.xml to settle.
- armour_classes.xml counts are dated 2026-10-05 (2,861 pieces); the file now has 2,927 <Item id> rows. Left because it is a dated snapshot; regenerate the paragraph's counts with tools/generate_armour_classes.py if wanted.

## docs/features/army-targeting.md

- Line 43 (Why weighting and not a gate) says a corrected gate would veto six pairs 'all of which the reach falloff already kills'. The falloff is deleted, so the claim was measured under a design that no longer exists. Re-run tools/analyze_war_theaters.py against vanilla's distance term to restate it.
- Line 206 says 'at the corrected 6.0 G radius, zero of the 80 measured entries sit beyond reach'. The rescue radius is now 3.2 (BorderRescueRadiusInTownGaps). Needs a fresh analyzer run on the live map to restate.
- Whether the analyzer has measured the four new realms' theater entries is unknown from the repo; a map-tools rebuild snapshot would settle it.

## docs/features/atmosphere-persistence.md

- The two scene commits (4bd4f3e54, 0e0387e06) add Gondor/Rohan/town scenes; the doc's mechanism, key files, Patch16 registration (Main/SubModule.cs:1685 inside OnGameInitializationFinished) and Mission_Initialize_Patch lines 27-52/32/42-43 all still match the code, so nothing else changed.

## docs/features/auto-resolve-diagnostics.md

- All absent_tokens are the deleted code the doc says was removed; they are right as written. Decompile line refs (MapEvent.cs:2018 etc.) are v1.4.7 and were not re-checked against v1.5.6.

## docs/features/bandit-management.md

- The Save Compatibility table says '5 new LOTR bandit cultures' and '10 new party templates'. These are original-feature history, so I left them. A maintainer could restate them as current counts (9 cultures, 18 templates).
- The doc says 99 hideouts and 182 live-map locations. Those live in the unversioned TAOM_Map and cannot be checked from this worktree.
- The Feral Orcs numbers (hideout counts, 23 dens) are tied to the live map and were left.

## docs/features/bandit-voices.md

- Checked and matching: Chance 0.25 (BanditVoicesService.cs:12), Patch122_BanditVoices (BanditVoicesModule.cs:17), the MCM group order 78 and default on (TaomSettings.cs:1286-1289), CoopSettingsRelevance.cs:188, 80 string rows, SubModule.xml:1266, the _loc_sources.py and generate_translation_template.py entries, the adapter files, the 6 test files, and the FeatureModules.cs line 92 entry. No commit since base changed the feature's own files. Vanilla line references (BanditInteractionsCampaignBehavior.cs:534-542 and so on) were not re-checked against v1.5.6.

## docs/features/banner-bearers.md

- Test total '111 tests' vs a count of 112 [TestMethod]-plus-DataRow cases (65 service, 29 provider, 14 shipped, 3 binding, 1 replacement-weapon); the test files predate base so I left the figure, and the doc's table omits the binding and replacement-weapon files.
- The DefaultItemPickupModel citation (v1.5.3 lines 14-16, 76-77, 82-101) could not be re-checked: the v1.5.6 source cache in ~/.taom-src lacks that type.
- The two Gotcha 15 / Patch63 and '~400 lines' claims were not re-measured.

## docs/features/banner-color-persistence.md

- The counts '11 patches + 1 manual transpiler' and '[12 UI patches]' were not recounted exactly. Twelve Hooks files carry the Patch23 category, which is plausibly consistent, but the file counting method is ambiguous.

## docs/features/bannerlord-together-compat.md

- Line 23/70 and the v1.5.5 line references (Module.cs:301, Mission.cs:3749) are version-labelled and were not re-read against v1.5.6.
- Line 389 says all 13 TAOM GameModel override classes use lazy ??=; Main now has about 70 Taom*Model classes. It sits in a dated April context, so left; settle by checking which subset overrides GameModel and uses static TextObject fields.
- Line 301 says 509 MCM settings; coop-interop.md breaks this down and SettingsFingerprintTests pins 474 in TaomSettings and 331 covered (matches), but the 509 total across four classes was not recounted.
- CoopSuppressedUi is now also on the Make Tools mixin and prefab (Client/Features/SmithsMeasure/UI), which the doc's time-acceleration section does not mention; not added because no listed commit covers it.
- Line 188 says six unseeded new System.Random() sites; grep shows 6 in Main but the doc's sites list is not the same set, so unverified.

## docs/features/battle-balance.md

- Doc claims vanilla 'never sees a tier above 6' for the militia/power formula; not checked, and the formula itself uses troop.Tier unbounded. Left as written.

## docs/features/battle-corpses.md

- Checked against code and found accurate: MCM defaults and ranges (TaomSettings.cs:1042-1070, BattleCorpsePolicy.cs:22-26), the recommended options 3 and 1 (BattleSettingsAdvisor.cs:15,18), CoopSettingsRelevance.cs:113, the module list entry (FeatureModules.cs:26), the six taom_bc_advice_* strings, the MountDespawnMissionGate conditions, and the file list. No commit since base changed these paths. Unverified and left alone: native-decompile claims (FUN_1805aefc0 and so on) and the owed in-game checks.

## docs/features/battle-load-diagnostics.md

- Absent token SavedGameVM.StartGame (line 374, a dated 2026-08-09 correction) is a vanilla SandBox.GauntletUI member; I could not confirm it in v1.5.6 because taom-src timed out. Settle with: pwsh tools/taom-src.ps1 path SandBox.GauntletUI.Menus.SavedGameVM.
- Absent token Dependencies/Foundation/IncompatibleModDetector is only missing its .cs extension; the file exists at Dependencies/Foundation/IncompatibleModDetector.cs. Left as written.
- Doc says Patch43 binds 'four' private engine methods by string; SubModule.cs comment at ~1936 says 'two of them private', and the string-bound targets include LoadMission, FinishMissionLoading, TickLoading, BuildAgent, EndMissionInternal, OnFinalize. Which are private in v1.5.6 needs taom-src; left unchanged.
- Dated measurements and the v1.4.7 or v1.4.8 engine line citations (MissionState.cs, Mission.cs) were not re-verified against v1.5.6; test count '307 tests as of 2026-09-22' and the 21-class and 'Patch91' prose were not re-counted beyond the hook files (21 HarmonyPatch classes in Hooks/ does match line 630).

## docs/features/battle-scenes.md

- The Known limitation says the release gate in tools/package_release.py is still planned; grep of tools/package_release.py for sp_battle_scenes/custom_battle_scenes found no such gate, so it stays accurate.
- The stale code comment in MBMapScene_GetBattleSceneIndexMap_Patch.cs line 19 says the category is commented out in SubModule.cs; code is out of scope, fix needs a code edit.
- Fords of War commits touch no BattleScenes files; none bear on this doc.

## docs/features/bills-burden.md

- Whether the icon renders in game is unverified; the in-game check stays owed. The doc's sentence that #876 stays open until step 5 passes was left as a maintainer decision.

## docs/features/black-numenorean.md

- Verification block counts (571 unittest OK, 6,655 passed, mordor 196 refs) are point-in-time snapshots; rerunning the suites would settle them, and I did not run tests.
- Lines 469 and 618 still name lotraom-assets/v1.4 inside dated narrative about the 2026-08-20 and 2026-09-15 passes; left as history.
- The 'Wawrim' spelling, lord ids and Armory item counts are live-module data that cannot be read from this worktree; left unchanged.
- validator behaviour after f1675c058 (new codes, data_gates.py) was not re-read against the 'no DUPLICATE_ITEM_DEF' claim.

## docs/features/blow-diagnostics.md

- Line 17 says SiegeEventModel.GetAvailableDefenderSiegeEngines is dead API in 1.4.5/1.4.6/1.4.7 and the defender-trebuchet feature is non-functional. TaomSiegeEventModel still overrides it (Main/Features/Siege/Models/TaomSiegeEventModel.cs:21). Whether the engine calls it in v1.5.6 needs a decompile of the installed TaleWorlds.CampaignSystem.dll; the doc scopes the claim to 1.4.x so I left it.

## docs/features/bond-of-hire.md

- Checked: BondOfHire code and tests untouched since base; GroupOrder 72, default 30, range 0 to 120 match TaomSettings.cs:2869 and BondOfHireSettingsProvider.cs:12-14; FeatureModules.cs:83 lists the module once. Engine line cites ClanVariablesCampaignBehavior:404, DiplomaticBartersBehavior:117 and :147, ChangeKingdomAction:41 and :68, Clan SaveableProperty(188) still match v1.5.6 dumps. Other cites (JoinKingdomAsClanBarterable:124, MercenaryJoinKingdomBarterable:67, LordConversationsCampaignBehavior:2575/2728/3229, VassalAndMercenaryOfferCampaignBehavior:270, CampaignCheats:2139, DefeatTheConspiracyQuestBehavior:344, StartMercenaryServiceAction:23) were not re-checked against v1.5.6.

## docs/features/bonds-of-battle.md

- Checked: no commit since base touches Main/Features/BondsOfBattle, Main/Features/Relations or the three adapters (git log empty); MCM group order 59, setting names and defaults match TaomSettings.cs:2431-2480; loc keys match taom_module_strings.xml:3416-3419. Engine line cite 'PlayerEncounter.DoPlayerVictory v1.5.5 :1392' was not re-checked against v1.5.6.

## docs/features/caravan-bandit-parity.md

- Dry run of tools/rebalance_template_power.py (writes nothing without --apply) reports 4 stacks it would change, all in harad_raiders_raider_party_template (max 68 shipped, tool wants 64; shipped max power 84 vs the 78 budget). Cause unknown (a later harad troop level change, or a hand edit). The doc's headline numbers (weakest caravan 93.2, L = 1.18) still hold; the strongest warband now prints 79.0 instead of the doc's 78.7. Settling it needs a maintainer decision to re-run the tool with --apply, so the doc's 78.7 and '76-79 power' were left.
- The Armory and live-module troop levels cannot be read from this worktree, so the 'harad, rhun and isengard field a caravan_guard one tier below' paragraph and its 69-80 / 77-88 body counts were not rechecked for the 8 new caravan cultures.
- 'the 34 mercenary and outlaw templates still carry the flat 50' was not recounted.

## docs/features/caravan-trade.md

- Settled 2026-11-12: the drift guards in CaravanTradeBindingTests (3 tests) passed against the installed v1.5.7, and the doc's binding-test line now says so. The question was whether the guards written against v1.4.6 still held on the newer engine.
- Key Files omits Main/Features/CaravanTrade/Cheats and Diagnostics (CaravanGateDiagnosticsService etc.) and their tests; the doc links economy-diagnostics.md for that. Left as is.

## docs/features/cardolan-rhudaur.md

- Live-only counts were not checked: 14 items in the live arnor/ranged_ladder.xml, 38 settlements in the live TAOM_Map settlements.xml, the 12 loc files.
- The 'Every table that names arthedain was decided for both' claim: inn_rounds.json and hall_of_fire.json also name cardolan but only hall_of_fire is in the doc table. Not judged false.

## docs/features/career-cc-selection.md

- Related docs not in my list (career-system.md line 642 and gui-sprite-system.md line 356) still say 70 careers.

## docs/features/career-forks-generator.md

- Checked only that the cited files exist (generate_career_forks.py, career_design/_rules.json and gondor.json, the two test files, check_orphan_loc_rows.py), that CareerSystemIoC.BuildCareerArchetypeMap exists as an expression-bodied dictionary (CareerSystemIoC.cs:91), and that _rules.json holds root_budget 1.5 and budget_tolerance 0.5. The 14 Fords of War commits do not touch this doc's subject. The detailed rule numbers (tier budgets, cap values, 63 of 70 cavalry roots) were not recomputed.

## docs/features/career-quest-system.md

- 'Full suite green (2874 passed)' is an old snapshot; only a test run would settle it.
- Settled 2026-11-12 for signatures only: TAOM.Tests builds against the installed v1.5.7 DLLs, so every event and QuestBase member CareerQuest calls exists, and the CareerQuest tests passed. The doc's verification was done on Bannerlord 1.4.5; nobody re-decompiled the events' behaviour since.
- Commit 0ab3dc35a added career screen open and init check lines (CareerScreenChecks.cs); the doc is about the quest framework, so I added nothing.

## docs/features/career-system.md

- The doc is 163 KB and I did not read it whole. The 14 commits are 13 Fords of War changes (each adds one row to taom_module_strings.xml) and f1675c058 (validator codes). Neither contradicts anything I found: line 467 still names tools/validate_moduledata.py, and line 72 only mentions translate_with_claude.py in passing (it now requires --module, not stated in the doc). Every backticked repo path in the doc resolves except retired items the doc already describes as deleted (AbilityHUD.xml, tools/wire_career_starter_armor.py) and vanilla paths. Defaults, counts (49 careers, perk numbers) and described behaviour were not checked against code; a full pass is still owed.
- Not checked: the doc's 841 lines of per-pip counts and constants against the code. The batch's 14 commits are Fords of War and tools commits, and git log shows none on Main/Features/CareerSystem or Client/Features/CareerSystem since base 2d6934006f, so I did not audit them.

## docs/features/castle-recruitment.md

- Doc says CastleRecruitmentServiceTests has 41 tests; a count of [TestMethod] plus [DataRow] lines gives 42 (37 TestMethod, 5 DataRow-or-DataTestMethod lines), so it is ambiguous whether 41 is off by one. Counting the executed cases with dotnet test would settle it.
- Absent tokens: DefaultSettlementAccessModel is a vanilla type; the path TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests is just missing its .cs suffix and the file exists (7 TestMethods, matching the doc).

## docs/features/character-creation-body-properties.md

- cd4fb3719 changed the BodySync patch file only by adding a swallow-ok comment, so no doc change was needed. The doc's changelog still says 17 cultures, left as dated history.

## docs/features/character-creation.md

- Hooks CharacterCreationCultureStageVM_OnCultureSelection_Patch.cs and CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch.cs exist but are not described in the doc; I did not read them, so what they do (and whether the doc should cover them) is unsettled.
- CultureRaceFilterServiceTests count (24) and CharacterCreationContentServiceTests count (16) were consistent with method plus DataRow arithmetic but not exactly confirmed.

## docs/features/character-selection.md

- The Tests section says no unit tests exist for CharacterSelection. A grep of TAOM.Tests for RefreshCharacterEntityAux and CharacterSelection found none for this patch, so that stays.

## docs/features/chariot.md

- Absent tokens ROTAgentStatCalculateModel.CanAgentRideMount and ChariotMissionBehavior are upstream-pack names, and transplant_userdata.ps1 lives in the external LOTRAOMAssets workspace. All three are right as written and I left them. I did not check the workspace folder.
- The dated 'Full suite green at 3,143' test figure was left as history.
- The commits in the batch touch only validator tools, Arthedain and Lindon markets, and the engine bump. None bears on the doc.

## docs/features/clan-heraldry.md

- Key Files table counts (119 Factions, 73 spclans.xslt overrides, 176 new party templates) are as-shipped numbers. Today taom_partyTemplates.xml has 249 kingdom_hero_party_*_template ids and spclans.xslt has 95 template matches, but which of those the pipeline wrote versus later hand edits is not provable from the worktree. The doc's intro already says counts drifted.
- Doc says 46 TAOM banner colour ids shadow Native: re-measured 46, still true.
- check_external_xslt.py '17 stylesheets' PASS claim not re-run (no scripts run).

## docs/features/combat-mechanics.md

- Absent tokens: TaomCustomBattleCreatureDamageModel is described in the doc as gone and is absent from Main; NavalDLCCustomAgentApplyDamageModel is a naval DLC engine type. Both correct as written.
- Checked 13 overrides in both model files, the 18-member MCM group at GroupOrder 24 (TaomSettings.cs:1379) and the Key Files paths; none contradicted. The long engine-line citations (Mission.cs, SandboxAgentStatCalculateModel.cs) were not re-read.

## docs/features/companion-tactics.md

- Per-file test counts (for example FormationPresetLayoutTests 46, OOBButtonsVMTests 22, BattleActionBarServiceTests 10, CompanionTacticsWiringTests 1) differ from the [TestMethod] and [DataRow] counts on disk (38+11 DataRow, 18+8, 12+5, 2); a TRX run of the folder would settle the right numbers.
- All settings, defaults, group orders, save ids, strings and file paths were checked and match the code; the logging commits only added Outcomes.Swallowed calls in catch blocks.

## docs/features/coop-dedicated-server.md

- `strip_main_map.py` and `run-windows.ps1` live in the BannerlordCoop.DedicatedServer repo, not here; not checkable from this worktree. Server PR states (#104 not merged, #115 open) need a gh query to confirm.

## docs/features/coop-interop.md

- Verification status says co-op is blocked on v1.5.6 until Coop is rebuilt (dated 2026-10-27), but the BannerlordCoop versions table at line 65 says the bannerlord-1.5.x branch targets v1.5.6 since 2026-10-29 and works. Left as a dated record. Settling it needs the installed Coop GameInterface.dll build date, which is outside the worktree.
- `LordBarterPatch` and `LordDefectionConversationPatch` are BannerlordCoop types (the doc says read in the local Coop clone), so absent from this tree by design. Left as written.
- Behaviour rows in the Gated table (line 324 onward) for features outside the 4 listed commits were not re-read against each feature's code; only line references and counts were checked.

## docs/features/crash-report.md

- Key Files says Collectors/ holds 14 collectors plus 5 utility classes. The folder now lists 18 files (about 11 named *Collector, plus buffers and builders), so the count does not obviously match. I did not resolve how the doc counts them; settling it needs an agreed definition of collector.
- The doc's Domain count (19 record types) matches the 19 files in Domain/.

## docs/features/creature-bandits.md

- The only commit (f1675c058) touched tools/taom_schema.py; the doc's one reference to it (`_HARNESSLESS_BY_DESIGN`, line 286) still exists at tools/taom_schema.py:1013. Config constants (MaxBroods 20, TrollBanditExtraHitPoints 100, TrollBanditDamageTaken 0.7, SpawnRadiusDays 0.25, clan ids) and the Key Files list match Main/Features/CreatureBandits. Not checked: the MCM option table values in TaomSettings, the 47 anchor count against the live map (live files are outside the worktree), and the in-game status claims.

## docs/features/creature-siege-role.md

- LadderQueueManager (absent token) is a vanilla engine type, left as written.
- The 'Status: Open (in-game check owed)' line and the owed in-game gates cannot be proven or disproven from the code.

## docs/features/crown-guard.md

- Line 66-67 says Rohan's row is the King's Royal Guard (below the Riders of Rohan), but Main/_Module/ModuleData/crown_guard/crown_guard_cultures.json and CrownGuardMap.cs:37 map vlandia to rohan_edoras_golden_hall_kings_own_rider. Troop names and tiers live in the live Armory, so this cannot be settled from the worktree. Read the Armory troop tree for that id, then fix the sentence.
- The new realms (eredluin, anduinvale, angaladh, dorwinion, southrhovanion) all have rows in the JSON and the map, so 'every kingdom culture has a row' holds. The prose list of borrowed or shared troops is still correct (rivendell and lothlorien share imladris_warden; lindon has its own).

## docs/features/cultural-feats.md

- Lines 24 and 36 say '9 new thin models + 2 extended' and '9 GameModel overrides'; Main/Features/CulturalFeats/Models now holds 17 Taom*Model files. The sentence reads as the original design, but whether to restate it is a maintainer call.
- Line 206 cites docs/reviews/codex-adversarial-cultural-feats-per-occupation-2026-05-31.md, which is absent from the tree. It is a review record, so out of scope; the reference may be a dead path.
- Line 255 'DefaultXxxModel' is a placeholder pattern, not a real symbol.

## docs/features/culture-conversion.md

- Whether the four Neutral realms and Feral Orcs add anything to the Known limitations (for example the 'Every playable culture now converts' line) was not checkable without running GarrisonCultureCoverageTests, which this task forbids.
- The 2026-09-20 roster measurements (Mirkwood tiers, Dunland/Dale/Umbar stopping at tier 6, '16 troops_*.xml files') were not re-measured against the current troop XML.

## docs/features/culture-doctrine.md

- The 'Doctrine per culture' table is dated 2026-09-17 (Phase C and D) and has no rows for the cultures added later (anduinvale, angaladh, dorwinion, southrhovanion, feral_orcs). I left it as dated history; add rows if the maintainer wants it current.
- Absent tokens: `ITeamAdapter` is a rejected-idea bullet and is correctly absent from code; `docs/engine/battle-behaviour-measured.md` is an external source path (yotthani's repo) and is right as written. Not changed.
- Engine line citations (v1.5.3/1.5.5 dumps) were not re-verified against v1.5.6, and the A/B scene counts from 2026-10-19 were not re-read (live TAOM_Map not readable from the worktree).

## docs/features/culture-marketplace.md

- Tests were not run (no tests allowed), so the suite's pass state is unverified; I removed the 'all green' claim rather than assert it.

## docs/features/culture-playability-wiring.md

- Row 12 names Main/Features/TroopProgression/RecruitmentPools/. The folder exists, but Gondor pools now come from recruitment_pools/gondor.json. I did not rewrite the row.
- The Tests section says 'All four derive their culture list from the data' above a list of five tests. I left it as a wording nit.
- Dated changelog entries and the 'Verified 2026-08-10' generator-drift figures (257 lines) were not re-run.

## docs/features/custom-battles.md

- Line 105 states the shipped culture data fits the slot check in 33 of 88 culture-and-slot pairs (22 playable cultures x 4 slots) and lists seven cultures naming another culture's troops. With 24+ TAOM cultures now (four new kingdoms, Cardolan, Rhudaur and others) these numbers are probably stale. Settling it needs a rerun of the slot-eligibility check over the merged culture data; I did not recompute it.
- The exact current faction-picker count needs a game run or a merged SPCultures read (vanilla-derived cultures empire, vlandia, sturgia, aserai, khuzait, battania are modified by spcultures.xslt, not countable from this worktree).
- The Fords of War CustomGame-only Items and NPCCharacters nodes (f512c46aa, 33ca557df, 9f430f2a8) mean every Custom Battle loads those troops (obsolete) and the tower arrow item. Whether the doc should mention the Fords of War mode is a scope call; not added.

## docs/features/dale-lord-expansion.md

- Verified: 14 sturgia clans, tiers 10/11=4 and 12-14=3, party-template reuse (1,4,2,3,6), shared banner key on clans 2-9, 27 lord_S1x ids, Assign-SettlementOwners clan counts, retired one-off flag, tools and tests exist, BROKEN_FACTION_REF exists in taom_schema.py, 24 language files. Not provable from the worktree: castle owners in the live TAOM_Map settlements.xml, the '12 of 14 clans hold one fief' count, the 'major-kingdom median is 75' figure now that new realms were added.

## docs/features/diplomacy.md

- Hooks/AllianceCampaignBehavior_AddAllianceDecision_Patch.cs (Patch11_Diplomacy prefix, old, May 2026) and Models/WarOutcome.cs, WarVeto.cs, WarVetoReasons.cs are not in the Key Files table or the Patch11 list. Not a false statement and no newer commit added them, so left; the doc owner may want rows.
- Counts (38 Permanent, 29 Natural, 7 Neutral, 79 Hostile) and the six extra Neutral realms in alignment.json were re-verified and match.

## docs/features/doc-graph.md

- The Dependencies line lists stdlib modules (argparse, collections, json, pathlib, re, sys); doc_graph.py does not import re or json directly, so the list is slightly loose. Left because no claim of behaviour is false. search_docs.py is absent from tools/ by design (ADR-010 deferred it), so both mentions are right as written.

## docs/features/doc-health-linter.md

- The audit rows are already applied in the doc: check 10 is in the table, the gating range reads 5 to 11, and the commit-hook sentence names .githooks/pre-commit (commit 434d1d795). I checked the budget constants against tools/lint_docs.py:59-71 and they match. I did not verify the "14 links" figure on line 192, which is historical.

## docs/features/dorwinion-south-rhovanion.md

- Checked ids, tools, test names, MCM orders 28-30 and troop counts (21 and 22) against the tree. The two newer commits (validator codes ITEM_SLOT_REJECTED, TROOP_ID_REMOVED, data_gates.py) do not contradict any statement. Historical gate output such as '0 errors' in the merge record was left as dated history.

## docs/features/dread-aura.md

- Absent token DesertersCampaignBehavior.MapEventEnded is a private vanilla method: it exists at v1.5.6 DesertersCampaignBehavior.cs:99, so it is right as written. Config defaults, ranges, MCM names and ranges, Key Files and the IoC and SubModule rows match the code (dread_aura_config.json, DreadAuraConfigProvider.cs, DreadAuraSettingsProvider.cs, TaomSettings.cs:1476-1494, IoC.cs:163, SubModule.cs:2235). The commit cb4eeaa3d touched no DreadAura file. Not checked: the test counts at line 351 (dated 2026-10-15) and the engine line citations at v1.5.5.

## docs/features/economy-diagnostics.md

- Engine line numbers (:1179-1182, :672, :677, :928, :1022, :4087 and others) come from a v1.4.7 read. They were not re-diffed against the v1.5.6 engine. Settle with taom-src plus a decompile of CaravansCampaignBehavior.
- Key Files, the Patch68 category, five targets, SubModule/IoC wiring and the TAOM base gold of 25000 (SettlementEconomyConfig.cs:19) were checked and are correct.

## docs/features/editor-cache-rebuild.md

- The only absent token, Main/Adapters/IEditorSceneAdapter.cs (line 207), sits in the 2026-09-24 changelog entry that records its deletion, so it stays. No commit since base touched Main/Features/EditorCacheRebuild. Checked and found accurate: config defaults against CacheRebuildConfig.cs and cache_rebuild_config.json, the builder loop lines (SerialPhase1Builder.cs:44, ParallelPhase1Builder.cs:63, SerialPhase2Builder.cs:36, ParallelPhase2Builder.cs:60), and CampaignSessionAdapter.cs:33. Not re-verified: the 16-site claim at ReflectionSiteBindingTests.cs:65-80 (the rows there now include MissionPerf and MapPerf sites from later commits, but that sentence is inside the dated v1.4.8 verification section).

## docs/features/elephant.md

- Two statements say crew kills count as Field Commission merit for `harad_archer` (open item near line 953, and the 'What the crew do and do not count for' paragraph). The crew troop is now `harad_howdah_crew`. Reading FieldCommissionMissionLogic (and what it keys on) would settle whether the troop id there is stale. I did not trace it.
- The 'Keep the top under 3.2 m' body-capsule rule (collision section) was written at 1.0x. The engine scales capsules with body_length (AuthoredScale 1.3), and the root now follows the spine, so the rule's absolute number may be stale. Needs an in-game or measured capsule top at 1.3x.
- June-dated narrative (TaomHowdahMachine bone tracking commented out, fixed-offset legs bug, harad_archer x4 crew) is dated history and was left; the open item 'Fix the fixed-offset fallback' may be fully resolved by the spine-height path but the doc already says 'likely resolved' pending the layout dump.
- Elite Emissary location 'Korb Taskral' and the 50/70 costs: costs verified in troop_resource_costs.xml:26 and the Emissary offer in elite_emissary_config.xml:129, but the town name was not checked.
- Engine line citations (v1.4.8, v1.5.3) were not re-derived against v1.5.6.

## docs/features/elite-emissary.md

- ROTTownTradersBehavior (another mod's name, flagged as absent) is cited as inspiration and cannot be checked from this worktree; left unchanged.
- The 2026-08-03 changelog line still says the key is not registered; it is dated history and was left alone.

## docs/features/elk.md

- Absent tokens SandboxAgentDecideKilledOrUnconsciousModel and DefaultAgentDecideKilledOrUnconsciousModel are real vanilla types (both present in the v1.5.6 taom-src cache: SandBox.GameComponents.SandboxAgentDecideKilledOrUnconsciousModel.cs, TaleWorlds.MountAndBlade.DefaultAgentDecideKilledOrUnconsciousModel.cs). Right as written.
- The cited engine line numbers (v1.5.3 Agent.cs, ItemObject.cs, CustomGame.cs:102) and Armory item stats are not readable or re-verified from this worktree. ElkConfig constants, ElkAttackServiceTests count (25 methods, one 6-row data test = 30 cases), IoC and SubModule wiring all match the doc.

## docs/features/encyclopedia.md

- Commit 4297cd2f6 only added one Fords of War trait string to taom_module_strings.xml, with no bearing on this doc. Checked: ShowAllEncyclopediaCharacters default true (TaomSettings.cs:29), the 15 taom_ency_ rows, hero_filters.json races and age bounds against the tables, FirstStageAge 18, Patch119 category and files. Not checked: prefab margins and the last-seen-in-army strings.

## docs/features/enlistment.md

- Only commit cd4fb3719 is newer than base; in Enlistment it changed one line each in MapStateEnterMenuModePatch.cs and Patch85_EnlistedDetachDeferral.cs (swallow-ok comments), so no described behaviour changed. Every backticked repo path in the 3,007-line doc resolves to a tracked file (Presentation/ and ModuleData/ names are relative to Main/Features/Enlistment and Main/_Module). I did not re-verify each behavioural claim in the doc line by line; a full pass would need a separate sweep.

## docs/features/equip-presets.md

- No feature code changed since the base. The two Patch33 files hold three patches (OnInitialize, OnFinalize, RefreshValues), matching the doc. Models EquippedSlotSnapshot.cs and PresetSlotRequest.cs exist but are not in Key Files; left alone.
- Engine cite `v1.5.5 :3163` for SPInventoryVM was not re-checked.

## docs/features/ered-luin.md

- Python pins 'test_militia_elite_bonus.py (38 veteran militia)', 'test_rebalance_template_power.py (50 templates)' and 'test_upgrade_skill_monotonicity.py (76 militia-bound troops)' were not checked; running those tests would settle them (a test_rebalance_template_power comment says 42 templates today).
- Ered Luin faction card status: factions.json says playable false but the doc banner says Parked 2026-10-28 while faction-map.md says 2026-10-29; date left as is.
- The 'deployed TAOM v2.0.36 and every release channel lack the culture' statement in Known gaps cannot be verified from the worktree.

## docs/features/execution.md

- FactionSideRulesTests.cs (9 DataRows, added in da6af0c0b) is not listed in the Tests section; adding a bullet is a possible follow-up. Other test counts (36, 12, 17, 3, 3) match the files.

## docs/features/eye-of-sauron.md

- Hero ids (lord_1_15, lord_1_48_*, lord_R2_1) and the troop ids in the Hosts table sit in live or ModuleData content. I did not check them against data, and HuntTroopListsDataTests covers them.

## docs/features/faction-map.md

- The Parked realms section says the Advanced Starting Options menu still offers the ten kingdoms for a King, Vassal or Mercenary start; the code that builds that menu was not located, so a grep of the start-option code would settle it.
- The factions.json playable flags, side values and game_faction ids match the doc's ten parked realms and the FactionMapDataTests rows; no commit since base touched Main/Features/FactionMap or Client/Features/FactionMap.

## docs/features/faction-ui.md

- docs/reference/harmony-patch-registry.md section Patch95_FactionUI (line 1131) still says 'eight patch classes'; it is out of my scope (docs/reference) and needs the same correction. Commit cb4eeaa3d (logging) did not touch FactionUI. Not checked: image counts and sizes (262 images, 337 MB), prefab count was 23 (matches), the 13 MCM toggles match FactionUISettings.cs.

## docs/features/far-watch.md

- git log shows no commit since base on Main/Features/FarWatch, FieldCamp, HiddenHosts, SpottingContributorComposer or TaomMapVisibilityModel. The two absent tokens are fine as written: DesertersCampaignBehavior is a vanilla type, and TAOM.Tests/Core/Domain/SpottingContributorComposerTests exists as a .cs file. Checked and matching: MaxCapBonus 0.29, MaxPenalty -0.5, the FarWatchMath constants, the camp option index 5, the module order in FeatureModules.cs (HostReview line 46, HiddenHosts 48, FarWatch 68, CampActivities 86), and 46 taom_fwatch_ rows. Unverified: the vanilla line numbers in the engine facts table (labelled v1.5.5 while one body sentence cites v1.5.6) and the test counts, which the doc ties to its shipping build. A stale code comment in Main/Features/FarWatch/Hooks/FarWatchCampOption.cs line 16 says Leave is 7 where the doc says 8 once camp work is counted. That is code, so I left it.

## docs/features/fell-foes.md

- Verified unchanged: tunable defaults and ranges (FellFoesTunables.cs:15-90), MCM group order 61 and nine settings with their defaults (TaomSettings.cs:2505-2555), FellFoesDebug excluded in CoopSettingsRelevance.cs:82, file list. Engine line citations (v1.5.5) and the owed in-game spike were not re-checked.

## docs/features/feral-orcs.md

- The Gates table lists python tools/validate_moduledata.py only; f1675c058 added tools/data_gates.py as the one command for data gates. Left alone because no statement is false. The 23 dens, the 20 Gundabad hideouts and the den region table live in the unversioned TAOM_Map and could not be read from the worktree.

## docs/features/fief-granting.md

- Of the listed commits, only 113d3483b (the single-player gate refactor) touched FiefGranting files, and it changes nothing the doc states. The recorder handlers still gate on _coop.IsAuthority (FiefGrantingCampaignBehavior.cs:87 and :127), and Patch70 still skips on ShouldDeferToHost (Patch70_FiefGrantDecisionSwap.cs:87).
- Checked: all ten MCM defaults and ranges against TaomSettings.cs:1606-1654, group Kingdom Politics/Fief Grants, and the file list under Main/Features/FiefGranting and Client/Features/FiefGranting. The two binding-test files each have 4 tests.
- The absent token SettlementValueModel is a vanilla type. The path docs/balance-sim/campaign.md is explicitly cited as another repo's file (the doc says yotthani's repo).
- Not verifiable from this worktree: the starting-ownership data pass claims (live TAOM_Map settlements.xml, the 988 vs 863 settlement counts, the 616 villages), and the v1.4.8 and v1.5.5 engine line references.

## docs/features/fief-management.md

- Absent token `FiefManagementNavItemVM` appears only in the 2026-10-09 changelog entry as a removed type; correct as history, no edit.
- Patch36_MapScreenF6 and Patch36_GameStateScreenManager now carry [ClientOnly] (commit 6d7e67f3f); the doc does not mention it and I added nothing.

## docs/features/field-camp.md

- The Owed section says batch-2 registered all 58 taom_fcamp_ keys, but taom_module_strings.xml now holds 66 distinct taom_fcamp_ ids (grep this turn). The 58 describes that registration batch and the Owed item is dated history, so I left it. A maintainer could confirm whether the 8 extra keys came from a later change that the translation backlog (#508) should count.
- Statements about the 1.4.8 TerrainType values stay as written. The 1.5.6 enum (TaleWorlds.Core.TerrainType.cs) has 23 members, and every one is named in CampTerrainService.cs, so nothing is wrong now. Whether the 1.4.8 values match the 1.5.6 ones byte for byte cannot be proved from this tree.

## docs/features/field-commission.md

- The 'Eight vanilla listeners ... on the installed v1.4.8' paragraph (CompanionRemoved subscribers) was not re-enumerated on v1.5.6; listing event subscribers in the installed DLLs would settle it.
- The '266 tests total' line is stamped 2026-09-04 and was left as a dated record; a current filtered run would give the present count (no tests run per the task rules).
- The '743 of 895 TAOM troop blocks' null FirstCivilianEquipment figure depends on live-module troop data and was left.

## docs/features/fire-and-tribute.md

- No commit since base touched Main/Features/FireAndTribute, its adapters, tests or fire_and_tribute.json. Anchor town ids (town_G1 etc.) live in the unversioned TAOM_Map and cannot be checked from the worktree.
- The new feral_orcs clan in clans.xml does not change 'eight outlaw clans': AllowedClans in FireAndTributeConfigProvider.cs:28 still lists the eight.

## docs/features/from-the-ashes.md

- The absent token VillageHealCampaignBehavior is a vanilla type: it exists in the v1.5.6 source cache and line 30 calls IncreaseSettlementHealthAction.Apply, so the doc is right.
- Checked file list, config JSON values, MCM group and GroupOrder, SideIds/ResolveSide use (AshesWorldAdapter.cs:53, FromTheAshesService.cs:444) and test files; all match.

## docs/features/generated-lord-kits.md

- The phrase 'as TAOM's other sixteen cultures already do' appears in lord_template_rosters.xslt's own comment, and the doc's 'TAOM ships child templates for Khand only' is pinned by LordTemplateRosterTests. taom_child_equipment_templates.xml now carries 22 cultures, so the 'sixteen' count may be stale. The doc does not state that number itself, so I made no edit.

## docs/features/ghidra-native-decompile.md

- The Design challenges and Performance sections quote v1.5.3 measurements (53 of 2,282 names, 947 of 2,093 addresses, 213 s analysis). They are dated measurements and were left; re-measuring on v1.5.6 would settle whether they should be refreshed.

## docs/features/gondor-armor-revamp.md

- Tests section says '84 tests' for VolunteerRecruitmentServiceTests; the file is now 3,687 lines. Counting tests properly needs a test run or a [TestMethod] count, which I did not do; it reads as a historical figure.
- 'troops_gondor.xml 6,800 lines, 107 troops' in Design Challenge and the Key Files '107 troops refit' are historical task sizes; the file is now 10,186 lines with 189 NPCCharacter entries. Left as the record of that work.
- Source-of-truth path names gondor_armors_and_troops.md/.txt in the external lotraom-assets repo were not verified; the lotraom-assets repo is outside this task's remit.

## docs/features/gondor-ithilien-ranger.md

- Changelog and issue lines that mention the two castles were left as dated history.
- 'Full suite at the time of feature ship: 89' is a dated figure, left alone.
- The text about the legacy notable troops gondor_mt_trainee and gondor_ith_watcher being reachable by clan-map fallback was removed with the weight 7 vs 3 paragraph. I did not verify the claim.
- Performance still says '2-entry pool'. It is not literally true now, and I did not change it.

## docs/features/good-deeds.md

- In-game checklist is still owed (doc and issue say so); nothing offline settles it.

## docs/features/graphify-code-graph.md

- Verified verbs, exit codes 1-5, CODE_EXTS, 82 tests, the hook matcher 'Bash|PowerShell' and test_hooks.sh section 7f. `cache.py` is graphify's own upstream file, not a TAOM file, and is right as written.

## docs/features/gui-sprite-system.md

- Checked the commit 0ab3dc35a (one added Outcomes.Pass line in GauntletCareerScreen.OnInitialize and check lines in Patch27) against the Screen Lifecycle and Opening flow text: both still match the code (Patch27 file, ExecuteDone then OpenCareerScreen). CareerAbilityHudVM is correctly described as retired (no hit in Main). The check lines are documented in career-system.md, not here. The rest of this 42 KB doc (sprite counts, brush details) was not re-verified against assets.

## docs/features/hall-of-fire.md

- The Why column for the new rows (why Anduin Vale, Angaladh and Dorwinion borrow Sturgia's hall, South Rhovanion Vlandia's) is not stated in either commit; kept the existing rationale text and invented none.

## docs/features/hard-questions.md

- Checked MCM group/default (TaomSettings.cs:1257-1260), priority 111, 19 taom_hq keys in code, hard_questions.json multipliers and strip costs, bribe cap (InterrogationService.cs:165), file list. None of the newer commits (Warden's Tally, prisoner banners, enlistment exits) changes this feature's code or claims. Not verified: engine line refs (Hero.cs:221-225, SettlementNameplateVM :624-649) against the installed DLLs, and the 'up to about 27' marks figure (live map data).

## docs/features/harvest-road.md

- Castle claim is stated for v1.5.5; the engine is now v1.5.6. Decompiling TryBuyingFood and the villager caller on 1.5.6 would settle whether it still holds.
- The absent token CaravanTransactionNotificationItemVM is a vanilla engine type named in the engine-bump checklist, so it was left as written. My taom-src lookup of it did not finish in time, so I did not confirm it exists in 1.5.6.

## docs/features/hero-race.md

- Commit f1675c058's message (and docs/modding/equipment-rosters.md:420) say CharacterSpawner is a scene script whose only external caller in v1.5.6 is CharacterThumbnailCache. The taom-src cache here holds only CharacterSpawner.cs, so I could not confirm the caller; hero-race.md's wording about 'spawned scenes' and CharacterImagePatch.json 'character spawner scenes' may need narrowing once that is read from the v1.5.6 dump.
- The key-file list, dead/disabled-hero capture, MinimumTrustworthyRaceCount=2, tuning console commands and TableauSafeRaceNames (uruk only) all match the code; validate_moduledata.py changes do not bear on this doc.

## docs/features/hidden-hosts.md

- git log 04c3c4540f..HEAD on Main/Features/HiddenHosts, Client/Features/HiddenHosts and TAOM.Tests/Features/HiddenHosts is empty. The listed commits touched only shared files such as TaomSettings.cs and the strings file.
- Checked and matching: the threshold rows, HorizonFraction 0.9, Hysteresis 1.1, MaxScoutMultiplier 2.25, the 0.12 per 50 skill bonus, FaultBudget 32, the seven taom_hhost_* keys, the MCM group Map UI/Hidden Hosts with HiddenHostsEnabled default true, and the hook file list.
- The absent token EncounterMenuOverlayVM is a vanilla SandBox type and the doc uses it as one. I did not resolve it with taom-src, because the lookup timed out.
- Minor, left alone: the MCM group Map UI/Hidden Hosts also holds ForestSightPenaltyEnabled (Shorter Sight Under Trees, Order 1, TaomSettings.cs:1586-1590), which belongs to Under the Eaves. The Configuration section names only the Hide Exact Troop Counts toggle, which is still true.

## docs/features/hold-the-walls.md

- The 'Engine facts (v1.5.5)' table cites vanilla file:line numbers; commit 12b010626 moved the target to v1.5.6 and I did not re-read each decompiled line, so the numbers could have shifted.
- Doc still lists 'Single-player only' co-op gating matching HoldTheWallsService.cs:110 and coop-interop.md; no stale claim found against the checked code, config defaults (army_targeting.json:145-154), MCM setting (TaomSettings.cs:519), module registration (FeatureModules.cs:74), key file paths and test files.

## docs/features/inn-rounds.md

- The other checked values match the code: priority 111 (InnRoundsDialogLines.cs:21) and MCM group World/Inn Rounds with GroupOrder 79 (TaomSettings.cs:1296). I did not re-verify the numeric defaults in the config table against InnRoundsConfig.cs. The shipped json is pinned to them by a test.

## docs/features/keep-wardens.md

- Per-file test counts in the Tests section (for example CastlePatrolConfigProviderTests 57, CastlePatrolServiceTests 24) cannot be confirmed without a TRX run: TestMethod and DataRow counts on disk are 21+43 and 15+12.
- The Performance section figures (156 castles, 243 fortifications, 28 level-1 Guard Houses) come from the live TAOM_Map settlements.xml, and the doc names new realms (Dorwinion, Anduin Vale, Ered Luin) that may have added castles; re-measure against the live file.
- No KeepWardens code, config or MCM entry changed since the base; the 30-commit list only touches shared files.

## docs/features/kingdom-creation.md

- Existing Kingdom table says Shaghana has 9 NPC lords and Abanissa 8, but clans.xml has 9 and 8 clans and lords.xml has far more lord ids for each prefix (grep matched 90 and 80 id attribute tokens, not de-duplicated). The figure may mean clan leaders; settle by counting NPCCharacter elements with is_hero=true per culture.
- The 'Umbar ... most complete, all 13 files' and Gondor wanderers (13 rather than 10) were left as written; Gondor has 13 wanderer rows against the guide's 10.

## docs/features/kingdom-voices.md

- Race-to-voice binding table and 'seven of the fourteen races' come from the live LOTRLOME_Armory skins.xml, unreadable from this worktree.
- tools/generate_voices.py is still absent from the tree, so the doc's 'planned' wording is still right.
- Doc says 'no voice validator today'; tools/audit_voice_clip_lengths.py exists (clip length only), which the doc already names, so left.

## docs/features/kings-muster.md

- Engine line refs for Army.cs, Hero.cs, PartyConfiguration.cs and ClanPartyItemVM.cs were not re-read. The DefaultArmyManagementCalculationModel refs (:58-117, :84-86, :123, :131, :156-163, :200-203) match the v1.5.6 decompile, but the doc labels the first one v1.5.5. Re-run taom-src on the others to settle whether they hold on 1.5.6.

## docs/features/liege-titles.md

- Why This Exists says 'the 17 TAOM-only kingdom cultures had no variation' (#955). That is dated history, so I left it. The old Configuration line counted 20 cultures at that time, so the 17 does not obviously match any count. The #955 commit would settle whether it was always off.

## docs/features/lines-of-descent.md

- Other test counts (41, 6, 5) and caps (32, 12, 300, 6, 5000) verified against code; no change.

## docs/features/load-time-stamps.md

- Not checked: the 'about 100 categories per process' figure and the 0.384 microsecond overhead (dated measurement, left as is). Commit 6d7e67f3f added RecordClientOnlyClasses to PatchCategoryApplier, which the doc does not mention; it does not make any doc statement false.

## docs/features/localization-override.md

- Line 13 of the doc says localization.md covers 1,773 added strings; docs/features/localization.md carries the same figure and taom_module_strings.xml now has 3,929 string rows. What the 1,773 counted is not stated, so I left it.
- Line 43 (the #706 history) says 311 strings were hidden at that time; left as dated history.
- The doc's Key Files and Solution Approach do not mention GlobalStringsOverrides.cs (global_strings.xml re-apply in SubModule.cs:291-304, #604, added in v2.0.29 before this doc's base). Adding it would be a new section; left for the maintainer's call.

## docs/features/localization.md

- The Tests section's per-test tables do not list every test now in LanguageDataXmlTests.cs, for example the AllLanguageDirs_Have*StringsFile tests and RootLanguageDataFile_IsWellFormedXml. The doc says "alongside the others below", so I left it. I also left the "CharacterCreation JSON narratives" row and the "(13 to 17 LanguageFile entries)" changelog text, which is dated history.
- The 12 TAOM-owned `TAOM_liege_*` key figure and the "36 keys keep SandBox's key" claim (line 182) were not rechecked against the XSLT. The sources are comment_strings.xslt and action_strings.xslt.

## docs/features/long-memory.md

- Test counts per class (36, 37, 38, 45...) count cases; I confirmed only that LongMemoryServiceRaidTests has 37 attributes and LongMemoryWiringTests 15. Running the suite would settle the rest.
- The pinned option ids are described as read from engine v1.5.5; the engine is now v1.5.6 (12b010626). Re-running LongMemoryBindingTests (needs the game) would settle whether the note should say 1.5.6.

## docs/features/lord-banners.md

- The doc line 'No TAOM module adds banner items' and the vanilla banners.xml count of 45 were not re-checked; the Armory is outside this worktree.

## docs/features/lord-face-variety.md

- Checked: LordFaceVariety code untouched since base; constants OwnFaceVariation 0.2, RollAge 30, twins match LordFaceVarietyService.cs:30-49; DefaultHeroCreationModel.cs:197-202 and CampaignEvents.cs:2098-2106 cites still hold in v1.5.6. The Data change table's hero counts were not recounted against the live module data.

## docs/features/lord-identity-reconciliation.md

- Localization table: '~1164/1181 translated x12' not re-measured, would need the 12 language files compared per key.
- heroes.xslt template count '399' vs 397 `<xsl:template match` now (396 with @id); lords.xslt 396 matches the @id count. Unclear which measure the doc used, left unchanged.
- Hero-text 'all registered in taom_hero_text_strings.xml': 6 of 475 keys (TAOM_hero_4_3_2 etc.) are registered in taom_xslt_strings.xml instead; left as is.
- Dated test counts (7748 suite, 1,132 vanilla ids) are as-of-2026-08-29 records and were not re-run.

## docs/features/lord-party-templates.md

- Vanilla line citations (`Clan.cs:112-122`, `LordPartyComponent.cs:32/:38`, `HeroSpawnCampaignBehavior :265`) are labelled v1.4.8; in the v1.5.6 decompile they sit at Clan.cs:121 and LordPartyComponent InitializeLordPartyProperties at about :29, SpawnLordParty at :246. Left as written because they are tagged to v1.4.8; a maintainer may want them refreshed.
- Troop levels and min/max rows in the two template tables were not re-read against taom_partyTemplates.xml beyond Faramir's stacks (which match); ShippedLordPartyTemplateTests pins the sums.

## docs/features/lord-perk-review.md

- The 2026-06-25 first-run findings (1,391 lords, 119 profiles, 93 and 149 flagged lords) are dated and were not re-measured; the generated reports are gitignored, so a regeneration would be needed.

## docs/features/lord-skills.md

- The doc says 17 cultures, but the CULTURES dict now holds 18 keys and the coverage table has 18 rows. I did not change the ~880 total or the 17, since they record the dated sweep. The erratum covers the per-culture counts only.
- 'Bannerlord v1.4.5 SkillSet schema' under Dependencies is unverified and was left.

## docs/features/lord-spawn-guard.md

- The 'Five of the eleven landless cultures can sit on an Occupation.Lord hero' count and the 988/997 settlement counts depend on the live TAOM_Map, which cannot be read from the worktree; the new cultures (Ered Luin, Anduin Vale, Angaladh, Dorwinion, South Rhovanion) were not checked for settlement ownership.
- The Testing table lists HarmonyPatchBindingTests 61/61 against v1.4.7 and 4912 tests passed; these are dated run results from 2026-08-04, left as a record.

## docs/features/loremasters-line.md

- No commit touched LoremastersLine code, client UI or tests since the doc base. Checked: seven taom_ll_ strings, setting group and default, prefab Index 3, calculator constants (4096, 1e-4, BarOverhang 25), Seasoned Hosts 0.05 floor. The runtime behavior and in-game checks stay owed.

## docs/features/lotr-issues.md

- The matrix `Src` column (SBM:162..197) is stale against the v1.5.6 decompile (ArmyNeedsSupplies is now SandBoxManager.cs:166, a shift of 4; SBS:74 still matches). Left because the matrix is kept as the original research record; a maintainer should decide whether to renumber.
- The Verification section still says to add a `taom_lotr_issues` schema under tools/schemas/; none exists, and tools/validate_moduledata.py:242-249 now checks the file's cultures and reward_item ids instead. Left because it is framed as the original plan.
- IssueModel (absent token, lines 245 and 306) is a real engine type (TaleWorlds.CampaignSystem.ComponentInterfaces.IssueModel), so the name is right as written.

## docs/features/main-menu-customizer.md

- The Final Menu Order places Fords of War after the Pre-compile Shaders slot (both orderIndex 100); the position of Custom Battle, Host Co-op and Join Co-op relative to it is not provable from this worktree (Custom Battle and co-op are registered by other modules).

## docs/features/many-hammers.md

- docs/reference/harmony-patch-registry.md says 'Targets (v1.5.5 ...)' with SmeltingVM.cs:229, :189 and RefinementVM.cs:136. That file is out of scope for this batch, so a maintainer or the docs/reference owner should re-check those targets and lines against 1.5.6.

## docs/features/map-event-guard.md

- Engine line citations (MapEvent, MapEventSide, SiegeAftermath, MbEvent) are labelled 'installed v1.4.8' or v1.5.4 and were not re-derived against the pinned v1.5.6. A decompile diff would settle whether they drifted.

## docs/features/map-load-diagnostics.md

- Commit 6d7e67f3f changed no file in MapLoadDiagnostics. Patch89 category and its three sub-categories match SubModule.cs:415-425, and the listed hooks exist. The in-game timing figures are dated evidence and were not re-checked.

## docs/features/map-perf-profiler.md

- SandBoxViewVisualManager.OnFrameTick (line 426) is a vanilla engine type: taom-src resolves SandBox.View.SandBoxViewVisualManager at C:\Users\mikew\.taom-src\v1.5.6\SandBox.View.SandBoxViewVisualManager.cs. I did not open it to confirm the OnFrameTick member.
- docs/engine/perf.md and docs/perf-audit/.../c1_mapgfx_20261018_141618.csv (lines 497, 518) are cited as MithrilForge paths at a pinned commit, not paths in this repo. They are left as written.

## docs/features/map-view-release.md

- Absent tokens are external names (yotthani's docs and MithrilForge paths, ThumbnailCacheManager.ForceClearAllCache quoted from that source), so they are right as written.
- Commit ea7cce7bc added the Screen Performance group; it does not change the two Map Performance settings. Defaults (on, 20), the 1 to 1000 range, the CoopSettingsRelevance entry and the test counts all match the code.

## docs/features/marriage-alignment.md

- The Overview's "Rohan, Dale ... Elves" wording and the 149 orc-culture day-0 pool figure (#542) are historical or loose and were left. The 3-of-3 pool percentages (53% Evil, 25% Free, 16% Neutral) in the Design challenge are a dated clan-pool figure; recounting needs clans.xml analysis I did not do. The Changelog entry dated 2026-10-20 still says the Neutral cultures are Khand, Umbar, Shaghana and Abanissa, left as dated history.

## docs/features/mcm.md

- The Settings posture section's 219 value knobs in 56 groups (measured 2026-09-11) was not re-measured; it is a dated measurement and TaomSettings.cs has grown since. Counting the SettingProperty attributes would settle it.
- The McmLayoutRewriterTests '17 test cases' line was not recounted exactly: the file has 9 DataRow plus 9 TestMethod/DataTestMethod attributes, consistent with 8 plain tests plus 9 rows.

## docs/features/melee-damage-model.md

- Absent token docs/engine/battle-behaviour-measured.md is cited as MithrilForge's file (another repo), so it is right as written.
- Curve (T0 25 ... T10 114), band 0.25 and ceiling 140 match tools/melee_ladders.json; named tools and tests all exist. Measurement counts (364 weapons, 74 lines, 81% on curve) are dated 2026-09 records and were not re-run; settling them needs the live Armory and analyze_melee_ladder.py.

## docs/features/menu-link-colors.md

- The v1.4.7 decompile line references (HyperlinkTexts.cs:59/67/75/91, RichText.cs:486-516, Brush.cs:304) were not re-read against v1.5.6.
- TaomCultureLinkStyles.cs's own comment still says 'The eight bandit cultures' (code, out of scope).

## docs/features/mesh-ref-validation.md

- The 2026-06-01 first-run table and the 'Main/_Module/ModuleData declares 0 references' measurement (2026-09-01) were left as dated records; the skins.xml attributes added in #403 are now scanned, so the 0-reference figure for the repo module should be re-measured with the install present.
- Line 17 says validate_moduledata.py emits MISSING_COLLISION_BODY; confirmed at validate_moduledata.py:306, but the claim about no pre-commit hook for this tool itself was left as written.

## docs/features/messengers.md

- No commit since base touched Main/Features/Messengers, Client/Features/Messengers or its tests. Checked MCM defaults and ranges (TaomSettings.cs 762-780), messenger_config.json defaults, CostCeiling 100000, Patch117 six lines, the five #898 keys in taom_module_strings.xml and the mixin's [ViewModelMixin("RefreshValues")]: all match.

## docs/features/minor-clan-migration.md

- Checked: the four clan ids and two target cultures (MinorClanMigrationService.cs:28-43), OnGameLoadedEvent-only listener (MinorClanMigrationBehavior.cs:23-24), OwnsSaveData false (MinorClanMigrationModule.cs:26), and the module line (FeatureModules.cs:70). Also checked: both test files exist, ShippedClanSideCoverageTests reads ClanCultures at line 387 and is LiveInstall, and the status commits 8788283e8 and 5e43518df exist. The display names (Grey Company, Corsair Blades, Beornings, Dunlending Reavers) map to live-module data I did not read.

## docs/features/minor-factions.md

- The four alignment.json minor-clan rows (ghilman, skolderbrotva, forest_people, eleftheroi) and the clan culture retags (Culture.arthedain, Culture.umbar, Culture.empire in spclans.xslt) match the doc; the new neutral realms in the kingdom commits do not touch the minor clans. The faction table's culture and home-settlement cells for lake_rats and karakhergits were not read in spclans.xslt.

## docs/features/mission-perf-heartbeat.md

- Lines 566 (docs/perf-audit/particle-sim-wait.md) and 1036-1037 (run_scenario.ps1, tools/perf-audit/run_scenario.ps1) are cited as another project's files and are absent from this tree by design; left as written.
- Not read line by line: lines 700-935 (probe status-line tables, the animation memory probe and key files) beyond checking that every Key Files path exists. The Mission Performance MCM defaults (EnableMissionPerfHeartbeat on, EnableTickProfiler off, TickProfilerTopN 8, HitchThresholdMs 250, EnableHitchProbe on, EnableAnimMemoryProbe on), MaxHitchLinesPerMission 100, the PatchShield ExcludedTargetMethods split and the SubModule wiring lines (SubModule.cs:1955, 2245, 2248) match the doc. The remaining commits on SubModule.cs (Fords of War, bandits guard, crash guards, LastSeenTextGuard, outcome log) do not bear on this doc.
- The new Screen Performance settings group (ea7cce7bc) sits on the same MCM page but is not part of this feature, so it was not added.

## docs/features/mission-start-guard.md

- MissionDeploymentPlanningLogic and ApplyWeatherEffectsModel are engine or other-mod names the doc describes as things a mod can supply. They were not checked against the engine DLLs.
- Commit 13fb30f9c only changed CrashReportSettings.cs by adding a static Instance. The toggle (Master group, default on), the provider fallback, the CoopSettingsRelevance entry and all six test counts match the code.

## docs/features/monster-size.md

- Engine line citations (Mission.cs, MissionScreen.cs, Campaign.cs, v1.5.3) were not re-checked against v1.5.6.
- Armory Monster values and the live Monster files cannot be read from this worktree.

## docs/features/mount-despawn.md

- The doc says the two settings are simulation-relevant for co-op parity, while MountDespawnMissionGate.IsEligible returns false whenever GameNetwork.IsSessionActive and CoopMissionRoles.cs:96 marks the behavior CoopMissionRole.Local; whether the parity paragraph still matters needs a read of CoopSettingsRelevance.
- 'verified against installed v1.4.8' and the Mission.cs/Agent.cs line numbers were not re-checked against v1.5.6.
- 'No AgentRemovalLogic exists' holds in the v1.5.6 source cache searched (no hits), but that cache holds only part of the engine.

## docs/features/multi-culture-armor-revamp.md

- Dated sections (deep-review correction, changelog, '7/7 cultures PASS') are history and left alone. validate_all_troop_refs.py now sweeps every troops_*.xml (25 files) and has an sm_/harad prefix, which post-dates the doc; the doc only cites the 7/7 result as of 2026-05-22.

## docs/features/mumakil.md

- Armory-side claims (Monster XML, Horse item body_length 300, prefab snapshot) live outside the worktree and were not checked.
- The v1.4.8 rein-attribute section is dated history and was left as is.

## docs/features/name-grammar-tags.md

- Dated count tables (2026-10-26/27: 75 kingdom keys, 1140 settlements, 57 BR / 58 IT undecided) predate the Dorwinion, South Rhovanion, Anduin Vale, Angaladh and Feral Orcs additions (commits 4b207f951, 88c6365c2, 0799af376). tools/translation_name_tags.json grew by 360 lines since base. Re-measure with python tools/name_tag_inventory.py and generate_name_tags.py --check on a machine with the live TAOM_Map.
- Doc line about tools/add_map_fortifications.py writing names without a tag still looks true (no name_tags use in the script), but the script changed in 4b207f951 and 88c6365c2; a human should confirm the caveat still holds after those commits.

## docs/features/named-companions.md

- Commit 4b207f951 added lord Hero rows for the new kingdoms to heroes.xml, not companions. Checked: 18 NPCCharacter rows, 18 config entries with matching settlements and races, Elegost in town_AN3, seven backstory string families per companion, the adapter files in Main/Adapters. The table's cultures for Igor (harad) and Eohart (rohan) differ from the XML ids (Culture.aserai, Culture.vlandia), which fits the orientation.md culture-id trap but I did not read the culture data to confirm the mapping, so I left them.

## docs/features/namegiving.md

- The absent token KingdomSettlementItemVM is a vanilla type. taom-src resolves it to TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements.KingdomSettlementItemVM, so the doc is right as written.
- The other test counts, 97 for NamegivingServiceTests (89 TestMethod plus 8 DataRow), 6, 5, 18, 7, 11, 3 and 11, still match the files.

## docs/features/nameplate-cull.md

- Settlement counts (1,040 settlements, 82 towns, 150 castles, 649 villages, 158 hideouts) are dated 2026-10-08 against the live TAOM_Map; later kingdom and den commits may have changed them, but the live file is not readable from this worktree.
- Doc cites vanilla SettlementNameplatesVM.Update as v1.5.4 and says the engine was read at 1.5.5; whether the 1.5.6 bump (12b010626) changed Update was not proven (the taom-src lookup ran as a background call and its result was not read). NameplateCullBindingTests would settle it on a build.
- Verified: Patch104_NameplateCull at GameInit, MCM group Map Performance, toggle default on, RequireRestart false, WindowFrames 18000, test file names all exist.

## docs/features/native-skin-fixes.md

- The four absent tokens are the removed paths and the retired hook, named as deleted by the doc itself. tools/package_release.py:114 still defines RETIRED_BINARIES as the doc says. check-native-dll-crt.sh retirement was not checked in .claude (out of scope).

## docs/features/naval-travel.md

- Patch54_NavalTravelBoatVisual gained a [ClientOnly] marker in commit 6d7e67f3f, so a dedicated server leaves it out. The doc does not say so. I did not add it because the feature is parked and the marker does not change its behaviour on a client.
- The PARKED banner says 're-enable = uncomment the 3 SubModule.cs blocks'. SubModule.cs now shows the commented model registration (~1099), the Patch54 wiring (~1877-1879) and a separate Patch57 wiring (~1885), which reads as 3 blocks but was not counted exactly.
- The token NavalPatrolPartiesCampaignBehavior.SpawnPatrolParty is a NavalDLC symbol. Resolving it with taom-src timed out, so it is unchecked and left as written. Test count 57 is right: 25 methods, 6 of them DataTestMethod with 38 DataRows, gives 19 + 38 = 57.

## docs/features/new-factions-misty-mountains-lindon.md

- Settlement counts in the Factions table and Lindon expansion (22 villages, 1,141 settlements) are in the live TAOM_Map settlements.xml, which this worktree cannot read.
- Test counts (VolunteerRecruitmentServiceTests +6, ConfigIdValidationTests 16 to 18 cultures, full suite 2902) are historical snapshots; a test run would settle them.
- The Key Files 'Kingdoms: +3 Kingdom blocks' and 'Clans +9 Faction rows' rows describe the original 2026-06-01 drop and no longer match the table of four realms or the later Lindon and Misty Mountain growth; left as the record of that drop.

## docs/features/no-mount-cultures.md

- Changelog line 2026-10-27 says 55 CC rosters for Ered Luin: matches the 55 ids containing eredluin, left alone. The Erebor horse-decision statement was left as a maintainer decision.

## docs/features/oaths-of-service.md

- Checked against code: priorities 105/110 (OathHireDialog.cs:21, OathIntroDialog.cs:23), validator limits and regexes, key files and tests all exist, config values (Gimli 50000 town_E1, Aragorn 75000 town_AN3, topic flags, relation 5) match named_companion_config.json. The Fords of War and other commits listed do not touch this feature. Elegost was added as a companion without oath terms, which the doc does not contradict.

## docs/features/orc-goblin-locomotion.md

- Commit 30003668b only added a face animation set for Sauron's race to the Armory action_sets snapshot, which the doc does not mention. Doc claims about the orc and goblin action sets were not re-verified against the live Armory, which is not readable from this worktree.

## docs/features/party-icon-scale.md

- Commit 6d7e67f3f only added [ClientOnly] to the two Patch53 hook classes; no behaviour changed. Checked: Default 0.15, Min 0.05, Max 1.0 (PartyIconScaleConfig.cs:26-32), MapFigureScale 0.15 (TaomSettings.cs:1577), SubModule.cs:1869-1871 registration. Key Files lists only Patch53_PartyIconScale.cs although Patch53_PartyIconScaleHumanVisual.cs also exists; the prose names both, so I left the table.

## docs/features/personal-relations.md

- I checked the rest of the doc against the code and it holds. That covers the defaults and ranges, the TaomDiplomacyModel overrides, the Patch107 target, the seeding flow, the SubModule.cs fourth constructor argument, and the test counts (107, counting DataRow rows, matches per file).
- The SubModule.cs commits in the batch (logging, Fords of War and others) do not touch this feature.

## docs/features/player-switcher.md

- The only commit on the list (f1675c058) made `tools/translate_with_claude.py` require `--module`. The doc's mention of a later `tools/translate_with_claude.py` run (about line 655) is still true, so I left it.
- Checked and still true: handler priorities 1100, 1050 and 1060, the four MCM names and defaults (`Main/Features/TaomSettings.cs:1897-1914`), the handover order in `HeroSwitchService.cs`, and the `HeroPickerService.cs:59` and `SwitchPlanner.cs:17` link anchors.
- Not checked: engine-side quotes cited against v1.4.8 or v1.5.3 decompiles, such as the `BodyGeneratorView` constructor having 13 parameters and `KillCharacterAction` line numbers. They are dated, and I did not re-verify them on v1.5.6.

## docs/features/postern-gate.md

- The doc's "Dunland / Khand" note says `empire` and `battania` fall under the default. The new cultures `dorwinion` and `southrhovanion` have no culture rows readable from this worktree. `PosternGateDefinitionTests` should cover the new id, but I did not run tests.

## docs/features/preload-body-guard.md

- The 'Six callers' list (MissionPreloadView, ArenaPreloadView, MissionCustomBattlePreloadView, MissionMultiplayerPreloadView, NavalShipsPreloadView, GauntletEducationScreen.OnFrameTick) was re-verified only for GauntletEducationScreen on v1.5.6; decompiling the other five types on v1.5.6 would settle it. A taom-src lookup for the first two timed out in this run.

## docs/features/prisoner-recruitment.md

- Line 75 labels engine call sites 'v1.4.7'; not re-verified against v1.5.6 decompile.
- Line 173 StoryModePrisonerRecruitmentCalculationModel is a vanilla StoryMode type absent from this tree, as expected; the claim that StoryMode is not a dependency matches Main/_Module/SubModule.xml DependedModules.

## docs/features/provender.md

- Token FoodConsumptionBehavior.SlaughterLivestock is a vanilla private method, so it is right as written: it exists at C:/Users/mikew/.taom-src/v1.5.6/TaleWorlds.CampaignSystem.CampaignBehaviors.FoodConsumptionBehavior.cs:225. Every other claim I checked matched the code: MCM defaults and ranges (ProvenderSettingsProvider.cs:10-34, TaomSettings.cs:2807-2860, GroupOrder 70), rations.json values, and the Key Files list. The doc's ranking text for remote selling was not re-read against ProvenderSaleRules.

## docs/features/quick-actions.md

- The MCM group `Inventory/Quick Actions` also holds the Telchar's Mark settings (`EnableTelcharsMark`, `TelcharsMarkShowOnlyUpgradesByDefault`, `TaomSettings.cs:887-899`). The doc's "13 settings across 4 groups" table covers only the Quick Actions rows. That is probably fine because Telchar's Mark is another feature, but I did not confirm it.
- The test counts (34, 7, 9, 51 total) were not recounted. The `[Fact]` grep returned 0 for those files, so they presumably use another attribute form, and I did not count them.

## docs/features/race-abilities.md

- Absent token `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests` (line 405) is fine. The file exists as `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests.cs`; the doc just omits the extension.
- The Fords of War balance numbers in 33b9deb6b (tower bonus, orc skills) do not bear on this doc. I did not check the doc's per-faction ability tables against `race_abilities.json`.

## docs/features/race-age-system.md

- The doc says Lothlorien shares rivendell items and umbar shares gondor items in the child templates; not verified. The prose list of 6 XSLT cultures (empire/dunland etc.) was left as written.

## docs/features/ranged-ladders.md

- 'Twelve cells sit above hero_ceiling' and 'the 227 rosters' were not recomputed after the new kingdoms; settle by running the rebalance report (needs the live Armory).
- Item/roster counts in the Overview and Config sections (e.g. '65 Armory items restatted') are dated first-run figures and were left.

## docs/features/realm-borders.md

- Cardolan's palette colour changed in 90d4c7f9f (#28745C); the doc names no per-realm colours except in dated changelog entries, so left as is.

## docs/features/red-arrow.md

- Engine line references (v1.5.5 :405-479, :2440-2497, ArmyManagementVM.cs:1122-1137 and others) are tied to v1.5.5 and were not re-checked against the installed v1.5.6 decompile. ArmyManagementVM.UpdateCanConfirm and ExecuteReset are vanilla names: the taom-src lookup timed out, so they are unverified.

## docs/features/reeves-ledger.md

- Absent token KingdomSettlementItemVM (line 29) is a vanilla TaleWorlds type, correct as written.
- ReevesLedgerServiceTests count (24) cannot be reproduced from [TestMethod] (19) or with DataRows (37); the doc's counting convention is unknown, left unchanged.
- FiefLedgerAdapterPinTests, ReevesLedgerPrefabTests, TextsTests, SettingsProviderTests counts match the [TestMethod] counts (7, 7, 8, 5).

## docs/features/refuge.md

- Verified: 14 MCM settings in group Refuge (GroupOrder 47) with the stated defaults (TaomSettings.cs:1812-1885), MilitiaPreRallyCount SaveableField(115), save definer base 726901201, provider-pinned mesh 'empire_street_tent_02' and scale 0.4 (RefugeSettingsProvider.cs:53,55), Client RefugeVisualService scales 4/4.8/4.6/5.4, NullRefugeVisualService registered in core, RefugeClanRowRefreshPatch and listener names. No commit in the list touches Refuge code. Not re-verified: 1.4.8 engine line refs and the Owed in-game items.

## docs/features/relations.md

- Commit cb4eeaa3d (check lines and counters) did not touch the Relations folder, so I made no edit for it. The six "callers outside the seam" still exist in `Main/Adapters` and `SiegeDefenseService.cs:347`. The "see the note above the UncapturableHeroes registration in `Main/IoC.cs`" pointer still holds (`IoC.cs:207-212`).

## docs/features/return-to-army.md

- The doc cites vanilla line numbers from the installed v1.4.8 decompile (:368-376, :994-1006 and others), and the CoopNightly co-op claim. The target is v1.5.6 and I did not re-derive them. Settling this needs `pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior` and a decompile read.

## docs/features/server-settings.md

- Verified unchanged: per-file test counts (6, 35, 5, 12, 18, 9), four settings classes each declare `static new Instance`, 2 buttons (TaomSettings.cs:1066, 1984) and the 2 parked shader settings, ProcessLoad module step. Not verified: the 'more than two hundred call sites' figure and the Newtonsoft/octal behaviour notes.

## docs/features/settlement-building-levels.md

- Commit 0d79058f6 only renamed one rationale string in tools/author_settlement_buildings.py (castle_EW1 to Harlond Quays); the doc's claims hold (DECISIONS, TIER_TOWN, TIER_CASTLE, CULTURE_FLAVOR exist, 19 JSONs under tools/data/settlement_building_levels). The same commit added three havens and three castles to TAOM_Map that have no DECISIONS entry (the script still holds 221 fiefs), so the doc's '221 fiefs' and 'full coverage' statements are true of the 2026-07-08 pass only. Whether the new Lindon fiefs should get building decisions needs the maintainer's call and the live settlements.xml.

## docs/features/settlement-economy.md

- Key Files omits Main/Features/SettlementEconomy/Cheats/SettlementEconomyCheats.cs (taom.print_town_economy, commit 218537baa). It is an omission, not a false statement, so I left it.

## docs/features/settlement-food.md

- Line 92 mention of DefaultSettlementGarrisonModel was removed with the rewrite; the absent token is a vanilla type so nothing else needed.
- The per-test descriptions under the three test files were not re-read against the test bodies; only the counts were updated.
- Map-wide measurements (70/72 towns, Orthanc figures) need the live settlements.xml and were left as is.

## docs/features/settlement-nameplate-relation.md

- I did not check the in-prefab claims (Id names, Large/Medium/Small prefab XML, ancestor depth) or the v1.4.8 widget line citations against the installed engine.

## docs/features/settlement-services.md

- Dependencies and Key Files say ScreenFaultLog is 'shared with Namegiving and the Reeve's Ledger'. It is now also used by FieldCamp, InnRounds and NotableServices. That sentence is incomplete but not false, so I left it.

## docs/features/shader-compile-notice.md

- Engine facts (Module.Initialize v1.5.4 lines 277 and 282, the compile_shader log line, timing from a 2026-10-06 log) were not re-read; they are dated measurements. The doc's line that SubModule starts the notice right after the logger exists is slightly loose, because Start runs after Outcomes init (SubModule.cs:125-137), but the claim is not false.

## docs/features/shader-precompilation.md

- Paths in the yotthani and MithrilForge notes (docs/engine/for-taleworlds/bannerlord-engine-findings.html, docs/realm-ui/feasibility.md) are external-repo citations, so I left them.
- GauntletDefaultLoadingWindowManager.OnLateTick is a vanilla symbol; I did not decompile it.
- Engine line citations (Mission.cs:3750-3753 and :4707-4724, and the 1.4.8 dump lines) were not re-checked.
- Verified true and unchanged: the parked state (SubModule.cs:530-537, 734-762; TaomSettings.cs:554-571), the 12 active scenes in precompile_scenes.txt, the tunable constants (ShaderPrecompilePlanner.cs:20,25; TaomShaderGameManager.cs:32-33; ShaderPrecompileRunner.cs:31-54; ShaderPrecompileDecider.cs:45-46), and the Key Files list.

## docs/features/shader-source-install.md

- The absent tokens tools/shaders/ and tools/shaders/05_making_shaders.md are written as lotraom-assets paths (the doc says that repo's tools/shaders/). They are not in this repo, so they are right as written.
- Checked: Main/_Module/Shaders/Sources holds 18 files; ShaderSourceInstallModule (a TaomFeatureModule) registers the adapter and service and runs the copy at ProcessLoad, skipping a dedicated server; the log format matches; SubModule.cs and IoC.cs have no ShaderSourceInstall code; the taom_shader_source_install_failed string exists; ShaderSourceInstallServiceTests, ShaderSourceInstallWiringTests and TaomShaderNativeTwinTests exist; test_package_release.py has TestShaderSources and TestLiveShaderReports.
- None of the listed commits touched Main/Features/ShaderSourceInstall.

## docs/features/shadowfax-screens.md

- Commit 23799be82 only added a root-perk line to `CareerScreen.xml` (the `RootPerkDescription` TextWidget). It does not change the doc's claim that every VisualDefinition in that prefab has `TransitionDuration="0"`, so I left it. I did not re-read the prefab's VisualDefinitions.
- Checked against the tree: the 17 `[ClientOnly]` hook classes (Clan 3, Inventory 2, Party 6, Quests 6), the four patch categories at GameInit, UIExtenderEx 2.13.3, `DoNotUseGeneratedPrefabs = true` at `Dependencies/SubModule.cs:42`, and every file named in Key Files.
- Not verified: the vanilla animation durations quoted for "v1.5.5" (0.45 s, 0.4 s, and so on). They would need the installed vanilla prefab XML and are possibly unchanged on v1.5.6.

## docs/features/siege-defense.md

- The GitHub Issue section says #660 is Open; not verifiable from the tree. Test count 31 still matches.

## docs/features/siege-forces.md

- `SiegeForcesBindingTests` against the v1.5.6 DLLs was not run, as the task forbids tests. I only compared the two target signatures by reading the v1.5.6 source.
- The "v1.5.3 :539-547" and "v1.5.3 :404-465" line citations in the doc were not re-checked on v1.5.6.

## docs/features/siege.md

- Commit 12b010626 changed only `Main/Features/Siege/Models/TaomSiegeEventModel.cs` in this folder. That model is not described in this doc (it covers only the camp guard), so I made no edit for it.
- The doc's `GetSiegeCampPartyPosition` parameter assumptions match the v1.5.6 signature (`internal`, three parameters). I did not check that `GetSiegeCampFrames` itself dereferences its settlement argument.

## docs/features/signature-strikes.md

- Neither commit (cb4eeaa3d logging, 726975a52 Fords of War) touches Signature Strikes code. Checked against code: shipped config values and cooldowns, SignatureMissionGate (Combat only, no multiplayer), SubModule.cs registration at 2235-2238 and the two damage-model constructor calls (1294-1297, 1345-1348), IoC.cs:164, sound registration at module_sounds.xml:584, MCM toggle names at TaomSettings.cs:1457,1462, file list. A Fords of War match is MissionCombatType.Combat by the gate rule, so signature heroes could fire there; whether the doc should say so needs the Fords mission's combat type, which I did not read.

## docs/features/skeleton-buffer-guard.md

- Checked: test counts sum to 235 across the ten test files, the v1.5.6 KnownBuilds row (global 0xD9F160) exists, both MCM toggles default true in Master group orders 4 and 5, both appear in CoopSettingsRelevance, SubModule.cs:233 comment says CrashReportSettings.Instance is null in OnSubModuleLoad. The Key Files table omits SkeletonBufferTarget.cs and SkeletonBufferSettingsProvider.cs, an omission rather than a false statement, so left. The commit cb4eeaa3d (logging) touched SubModule.cs only, not this feature.

## docs/features/smiths-measure.md

- The engine line numbers cited (v1.5.5 DefaultSmithingModel :178/:336/:140, CraftingVM :963/:925/:1059/:1287/:1293/:1038, CraftingCampaignBehavior :848, SmeltingItemVM :197) were not re-checked against v1.5.6 (the pinned target); settle with pwsh tools/taom-src.ps1 path <Type> then a decompile, or leave as v1.5.5 citations.
- MCM group, defaults, orders, constants (cap 1 to 10, default 1, XP share 0.30, floor 1, stamina gate 10), file list and wiring claims were all verified against the code and need no change.

## docs/features/special-resources.md

- Line 45 says Harmony Patch26 has 3 patches. That is true: the recruit-gate patches are in category Patch51 and the doc covers them separately at line 147.

## docs/features/spider.md

- Line 341 names docs/_scratch_characterspawner.cs as deleted. The path is absent from the tree, which matches the statement, so it was left as written.

## docs/features/starting-equipment-tuning.md

- Absent token wire_career_starter_armor.py is stated as deleted in #629; correct as written.
- The starter item counts (128 on disk, 40/38/50, 39 blades, 15 and 8 registration blocks) live in the unversioned Armory and were not checked; settle with generate_starter_kit.py --verify on the desktop.
- Roster counts for 'Ered Luin's 55 rosters' and the 2026-10 history entries are dated records and were left.

## docs/features/startup-resources.md

- Lord counts quoted in 'Deriving the gold values' (10 for Umbar/Lindon, 150 for Moria) and the 26.1x/18.5x spread were not re-measured; they need live TAOM_Map/lords data.
- The all-module prefab total (130,151 on 2026-08-08) is a live-install measurement not reproducible from the worktree.
- The Tests section omits StartupResourcesConfigCoverageTests.cs, and per-file test counts were not re-counted.

## docs/features/supply-lines.md

- Absent tokens EncyclopediaNavigatorVM.SearchText and FoodConsumptionBehavior are vanilla types, correct as written. All 14 listed commits are Fords of War changes that touch only taom_module_strings.xml and cannot bear on this doc. I spot-checked paths, MCM names and defaults (TaomSettings.cs:1692-1722), GroupOrder 45, SaveableFields 114-117, MaxHits 60, 0.25h and 40-arrow cap, Patch73 registration, ForceDeliverFraction 1.5, and the CoopSettingsRelevance route-visual entry; all hold. Vanilla 1.4.8 and 1.5.6 behaviour claims (food model, DesertionCampaignBehavior gate, Jopila PR crash claims) were not re-verified.

## docs/features/sworn-anew.md

- Known Limits line 130 says BannerlordCoop's LordBarterPatch routes the defection barter to the server. That type is in the external Coop repo and absent from this tree; checking E:\repos\BannerlordCoop for a LordBarterPatch class would settle it.
- Vanilla line citations (LordDefectionCampaignBehavior.cs:781-803, :137-138, :141, :369-384; LordConversationsCampaignBehavior.cs:811) were not re-checked against the v1.5.6 decompile.

## docs/features/tavern-mercenaries.md

- Commit f1675c058 (validator codes) does not bear on this doc beyond the 'run validate_moduledata.py' step, which still holds. Whether the four new realms' _merc troops sit in a TAVERN MERCENARIES section of their own troop files was not checked, so the Key Files sentence about the four generated realm files is unchanged.

## docs/features/time-acceleration.md

- The doc says TimeAccelerationServiceTests carries 53 tests. The file has 46 [TestMethod] plus 7 [DataRow] lines (two theories, 3 and 4 rows), so the count is 46 methods, or 51 test cases if each DataRow replaces its method. 53 matches only methods plus rows. I left it because the counting convention is unclear. A maintainer should pick one convention and fix the number.
- None of the 27 listed commits (Fords of War, staged siege, kingdoms, careers and others) touched Main/Features/TimeAcceleration, Client/Features/TimeAcceleration or the TimeAcceleration tests (git log 04c3c4540f..HEAD on those paths is empty). The other claims I checked still hold: 4 singleton registrations in TimeAccelerationIoC.cs, the 5 prefab patch classes, the ITimeAccelerationService members and the file list.

## docs/features/tokens-of-esteem.md

- Checked: no commit touches Main/Features/TokensOfEsteem since base; settings defaults and ranges match GiftSettingsProvider.cs:8-21 and TaomSettings.cs:2562 (GroupOrder 62); gift_catalog.json values match the Current Values table. The only alignment.json change since base adds five culture rows (anduinvale, angaladh, dorwinion, southrhovanion neutral; eredluin free), which the doc does not enumerate.

## docs/features/tournament-alignment-filter.md

- The line about seven cultures borrowing another culture's troops (Lothlorien, Khand, Shaghana, Abanissa, Umbar, two goblin realms) was not re-derived against the current culture files; reading taom_spcultures.xml basic_troop and elite_basic_troop for the new realms would settle whether the count changed. The token HeroKnownInformationCampaignBehavior.OnPlayerJoinedTournament is a real private method (v1.5.6 source line 51), so it is right as written.

## docs/features/tournament-armor-assignment.md

- The doc's other counts (20 tests in TournamentServiceTests) matched the code (20 [TestMethod]), so were left.

## docs/features/troll-race.md

- Most of this 80 KB doc is dated Track 1 records and I treated them as history. Present-tense claims I verified: troop_resource_costs.xml (50 War Spoils, upkeep 5 and 4), baseHitPoints 200 for both trolls, party templates 0..7 on kingdom_hero_party_mordor_template and 0..2 on the empire_south templates, MordorTarget 260, melee_catalogue.price (a function, tools/melee_catalogue.py:638, so the absent token is fine).
- Commit 7178cd5f3 (Hammerhand) says trolls resist knockdown and are stagger immune while Creature Stagger Immunity is on; the doc does not mention it. I judged it outside the doc's scope and did not add it.
- The doc's Status/pending header says updated 2026-06-14 while holding later entries; sections marked OWED were not re-verified (they need in-game or live Armory evidence).

## docs/features/troop-progression.md

- Commits cb4eeaa3d and 726975a52 only touched Main/IoC.cs and Main/SubModule.cs for logging and Fords of War, so they do not bear on this doc.

## docs/features/troop-skill-balance.md

- Whether the nine culture files with no CULTURAL_MODS entry are intentionally on the bare curve is a maintainer decision. rebalance_troops.py:373 uses CULTURAL_MODS.get(culture, {}), so a --apply would rebaseline them; I stated the fact and did not call it a bug.
- The '6 militia-to-militia edges exempt' count (line ~210) and the Four flat edges paragraph were not recounted; recounting needs running tools/rebalance_troops.py or the C# test, which this task forbids.
- Changelog and dated entries (871 troops, 25 Python tests, 222 troops changed) were left as history.
- The 'Dale, Dunland, Harad, Rhun and Rohan use only the xslt encoding' claim: spcultures.xslt has 24 militia bindings, which is 6 cultures, not 5. Which sixth culture (likely Khand, absent from ExpectedMilitiaCultures) binds there was not settled.

## docs/features/troop-tree-revamp.md

- The per-culture NPCCharacter totals (35/51/50/27/58) are stamped 'verified 2026-05-23' and are a point-in-time record, so they were left. Today's files hold more rows (grep -c '<NPCCharacter' gives 64/53/51/31/67 for mordor/isengard/dolguldur/gundabad/erebor, each including the root element); a recount would settle whether to restate them.
- The Tests paragraph (2122 to 2144 passing) is historical and was left.

## docs/features/troop-weight-system.md

- None of the 15 Fords of War commits touched Main/Features/TroopWeight, troop_weights.xml or the TroopWeight tests (git log b11b6d142e..HEAD on those paths is empty), so they do not bear on the doc.
- Verified as still true: 133 live weight rows (59 at 2.0, 66 at 3.0, 6 at 4.0, 1 at 12.0, 1 at 20.0), EnableTroopWeight default true, the two taom_troop_weight_* strings, the Patch17_TroopWeight and Patch_MainRosterLossDiag categories in SubModule.cs:1686-1690. The absent tokens DisorganizedStateCampaignBehavior (vanilla), PartyVMPopulatePartyListLabelHook and TroopWeightHooksTests.cs (deleted, named as history) are right as written.
- The v1.4.8 and v1.5.5 engine line references and the counts dated 2026-08-14 were not re-measured.
- Duplicate guard entry not applicable; see the corrected entry above.

## docs/features/uncapturable-heroes.md

- Engine line cites (Hero.cs:2010, MapEvent.cs:1957-2008, PlayerEncounter.cs:1611 and others) were not re-checked against the installed v1.5.6 DLLs; a decompile pass would settle them.
- The feature code, config, MCM entry and strings are unchanged since the base; the listed Fords of War and careers commits do not touch this feature.

## docs/features/under-the-eaves.md

- The engine facts are noted as read from v1.5.5, and the engine is now v1.5.6. Running UnderTheEavesEngineFactsTests (needs the game) would settle whether that note is stale.
- The 111 settlement gates, 634 forest faces and Dunland/Khand Battanian feat claims depend on live map or culture data not readable from this worktree.

## docs/features/upstream-review.md

- Checked against .claude/settings.json (upstream-watch.sh registered with timeout 10), the hook's 8.5 s budget comment, tools/upstream_watch.py (20 h default cache, status/areas/advance/seen subcommands), docs/reviews/upstream-watch.json (two yotthani repos, one release repo). Commit 434d1d795 does not change any of it. Not run: tools/test_hooks.sh section 7l existence was confirmed by grep only.

## docs/features/volunteer-recruitment.md

- The doc says MaxVolunteerTier => 6 on TaomVolunteerModel; the model now delegates to IVolunteerTierService (value 6 in VolunteerTierService.cs:5). Left, since the value is still true.

## docs/features/wanderer-allegiance.md

- Line 230 cites `docs/taom/side-boundary-report.md` in the external repo (not in this tree); it is an external path and was left as written.
- The census line (26 cultures on 281 templates, counted 2026-10-27) was not re-counted; it would need a read of taom_wanderers.xml.

## docs/features/war-chronicle.md

- No WarChronicle file changed after the doc's base commit (the listed commits touch only FeatureModules.cs, TaomSettings.cs and CastleRecruitment). Checked and matching: module registration, the six console commands, MCM names, defaults and groups, rally.json values, 13 events, castle volunteer path (CastleRecruitmentService.GetSlotProductionProbability to VolunteerProductionService.Compute), file lists. DefaultCampaignTimeModel is a real engine type. The test counts in the Tests section are stamped 2026-10-14 and were not recounted. Source refs labelled v1.5.5 (Campaign.cs:1024-1030 verified to match v1.5.6; SaveContext.SaveStringTo) could be relabelled v1.5.6 by someone who re-reads them.

## docs/features/war-of-the-ring-momentum.md

- Only the table rows I verified were changed; the lines about the UI sizes, bake state and in-game confirmation are not checkable from code.

## docs/features/war-of-the-ring.md

- The Kingdom ID table was already incomplete before these commits: lindon, bluecraig, goblin, mistymountainorcs, shaghana and abanissa are kingdoms in taom_spkingdoms.xml and diplomacy.json with no row. Their Side labels are a doc convention not stored in one place, so they were not added.
- Verified unchanged: phase days 30/44 in war_of_the_ring.json and TaomSettings.cs:272,279; Ered Luin has exactly 9 Hostile rows in diplomacy.json; test counts for WarOfTheRingServiceTests (58), SessionResetTests (8), WiringTests (3 methods + 4 data rows = 7 cases), PhaseDayTests (5), ConfigProviderTests (18), PeaceActionHookTests (5). The veto, patch and prefix-site claims in 'War Rule by Phase' were not re-read against code this pass.

## docs/features/war-ram.md

- Doc says the WarRamAttackServiceTests has 24 tests; the file has 24 [TestMethod] by count, so unchanged. Not re-verified: the 9 MOUNTED_DWARF python cases and the exact ironpass troop levels (live/XML data not walked in full).
- Ability tooltip in taom_ability_templates.xml also lists +10% melee damage for 8s, which the doc's '+25% charge damage, +20% mount speed' summary omits; left as true-but-partial.

## docs/features/wardens-tally.md

- Engine line references (v1.5.6 :201-250, GameMenu.cs:315-326, MapEventParty.cs:95-98 and others) were not re-decompiled; settle with taom-src path plus ilspy if wanted.

## docs/features/watchful-holds.md

- Status line cites af66af81f; the in-game check, balance runs and translation are still owed per the doc and nothing in the tree contradicts that.
- Culture ids in the shipped JSON (mordor, dolguldur, isengard, lothlorien, mirkwood) are live-module data and were not verified against the Armory.

## docs/features/weapon-xml-pipeline.md

- Only commit f1675c058 is newer than base. It adds validator codes (TROOP_ID_REMOVED, INCONSISTENT_WEAPON_SLOT, TOWNSFOLK_WITHOUT_BATTLE_ROSTER) but the doc's one validator claim (line 56, BROKEN_ITEM_REF) still holds: tools/validate_moduledata.py lines 47 and 745 still name it.

## docs/features/wildmesh-mounts.md

- Line 24-28 says the /release Phase 8 prune and step 5 interact with the test riders; the release skill lives under .claude, which is out of scope, so that statement was not checked.
- The two absent tokens (test_riders/wildmesh.xml, test_wildmesh_test_riders.py) are correct as written: the doc says they were removed for v2.0.35 and the tree has no test_riders folder. DWARF_MOUNT_IDS (tools/taom_schema.py:200-203) and MountedDwarfTests (tools/tests/test_validate_moduledata.py:1230) match the doc.

## docs/features/xml-merge-fast-path.md

- The "about 11 MB of managed heap" for the cached XSLT is a measurement I cannot re-run without a build or the game, so it stays. The lords.xslt timing figures (about 1.1 s on the first load) predate the 360 KB shrink. A new measurement would settle whether they still hold.
- The doc's list of `reason=` values leaves out `not-initialized`. That is only the default value on `XmlMergeCall` (XmlMergeCall.cs:24), and the doc was not edited for it.

## docs/modding/README.md

- Ship sizes and file counts in the four-modules table (2,277 files 0.41 GB, 142 files, 2,519 files 19.39 GB, 4,990 files 4.10 GB) come from a package_release.py dry run and could not be re-measured without running tools; commits since include the TAOM.Client submodule and Feral Orcs/realm additions that likely changed TAOM's count.
- docs/modding/open-questions.md exists but is not linked from the README chapter list (docs/modding is in my write scope only for the listed docs; adding the link was left out as new content).

## docs/modding/balance-levers.md

- Lines 22 and 24 of the Gondor chest example (LOTRLOME_Armory body_armors.xml, 8883 denars) and the armour and settlement-economy floors apply to live-install data and were not re-read.
- The MCM knob-group count method counts distinct group strings (sub-groups included); whether 'groups' should mean top-level groups was not decided.
- docs/features/ai-party-size.md:171-176 and docs/reviews/lessons/gamemodels-services.md:408-425 line cites were not re-checked.

## docs/modding/banners-and-heraldry.md

- The Numbers bullets I did not re-measure keep 2026-09-05 stamps and cite the v1.4.8 engine (BaseBackgroundId hit count, schema line count).
- The 'undefined icon ids' pool-sweep bullet (9 entries, 5 ids, 2 colours) was not re-run; running tools/tests/test_banner_icon_ids.py would settle it.
- Commit 9985f2db3 (realm banners, banner sheets packed) is the likely source of the group, icon and colour growth. It predates the list base, so I re-measured rather than reading its diff.

## docs/modding/body-properties.md

- Armory skins.xml facts (14 races, style_tag tables, 22 of 28 adult skins) are in the live Armory and were not re-read; the race table may have changed.
- The 'Hero face variety' gotcha and the Numbers table's 2026-09-05 rows for items I did not re-measure are unverified against current code.

## docs/modding/clans.md

- Body text of the default_party_template row still says '124 of the 172 TAOM rows name a clan-specific template, 40 name their culture's default and 8 name a raider template (2026-10-27)'. Not re-derived: it needs the culture default templates from taom_spcultures.xml plus spcultures.xslt (my partial read found only 24 cultures with a default). Settle by classifying each of the 188 rows against its culture's effective default_party_template. Bandit clans now number 11, with 9 *_raider_party_template ids and 2 brood/band ids.
- 'check_external_xslt.py ... PASS (17 stylesheets clean)' in the numbers table not rerun (no tool runs allowed); there are 9 .xslt files directly under Main/_Module/ModuleData, the tool may scan a different set.
- Engine file:line citations (Clan.cs, SandBoxManager.cs, MBObjectManager.cs) were not re-checked against v1.5.6; settle with tools/taom-src.ps1 or the decompile dump.

## docs/modding/configs-balance.md

- Provider line cites for the combat_mechanics, race_age, initial_child_generation, field_commission, recruitment_alignment, settlement_* and revolt providers were not re-read one by one; only registration lines and the ones named above were verified.
- The Fords of War commits (68d2da83c, e75ad58af) and the validator-codes commit change nothing this doc states: tools/schemas still holds three files and none of the fifteen filenames appears in tools/validate_moduledata.py or tools/taom_schema.py (checked).
- CasualtyRatios.CulturalSurvivalBonuses default '0' is shown as the absent-key default, but BattleBalanceConfig.cs:32-36 carries a compiled dictionary with per-culture values (including new dorwinion, southrhovanion); whether the row should say so was not decided.
- docs/features/troop-weight-system.md:339 and docs/reviews/lessons/gamemodels-services.md:418 (cited as stale counts) were not opened.

## docs/modding/configs-factions-and-world.md

- The Read-at line numbers for test files other than those named above (FactionMapDataTests.cs:28-141, ConfigIdValidationTests.cs:120-183, WarOfTheRingShippedConfigTests.cs:51-105) were not re-verified line by line.
- The per-row Numbers measured-date stamps were left as dated; counts were updated to 2026-11-03 values.

## docs/modding/cultures.md

- Gate table codes (UNKNOWN_CULTURE, LANDLESS_CULTURE, BROKEN_PARTY_TEMPLATE_REF, MISSING_EDUCATION_TEMPLATES, DUPLICATE_CULTURE_ID) all still exist in tools/taom_schema.py, validate_moduledata.py or tools/schemas/taom_spcultures.json. f1675c058 added codes (TROOP_ID_REMOVED, INCONSISTENT_WEAPON_SLOT, TOWNSFOLK_WITHOUT_BATTLE_ROSTER) that bear on no culture edit. The doc is 57 KB and I did not re-verify its other claims against code.

## docs/modding/editing-safely.md

- All four audit rows (G10-162, 164, 167, 177) are already reflected in the file. Dated 2026-09-05 measurements (BOM counts, sidecar counts, Armory XmlName counts) were not re-measured; the live Armory has drifted since (it now has 3 .bak sidecars per find).

## docs/modding/file-catalogue.md

- Not reached: lines 289-406 (rest of the config master table, now 102 JSON and its row list, and later sections) were not read, so their counts and rows are unchecked. Status not_finished for that tail.
- The 'reach 105 of the 123 XML and XSLT files, 18 miss, 15 by C# loaders' sentence was not recomputed after the manifest grew to 143 rows.
- Eight troop files, eight npcs rows' tools columns, and seven equipment-set files are not in the tables; the rows were not authored because the Tools column cannot be proven here.
- The whole TAOM_Map section (49 files, 8 XmlName rows, nine stubs, mbproj) describes a live module that now has only settlements.xml, settlements.xslt, two track files and a Languages and DistanceCaches folder; confirm whether TAOM_Map was slimmed before rewriting that section.
- Armory counts (448 files, 33 XmlName rows) were not recomputed.

## docs/modding/id-cheatsheet.md

- Other measured counts dated 2026-09-05 are stale on a quick look and were not rewritten: slot= uses (Body 14,701 now, doc 9,648), default_group uses (Infantry 4,016 now, doc 2,906), occupation uses (Lord 1,551 now, doc 1,325), the roster-flag and civilian/equipmentType counts, skill-set count, and the Trait counts in lords.xml. Each needs its stated rg command re-run.
- Live-map figures (1,141 settlements, 158 hideouts, 34 distinct map cultures, per-culture counts such as sturgia 37 and vlandia 48, village_type counts, prefix rows such as A, V, S) cannot be read from this worktree. If the Rhovanion settlements took culture from Dale or Rohan, those old rows and totals changed; a census of TAOM_Map/ModuleData/settlements.xml settles it.
- The four new settlement counts (22, 19, 30, 31) are summed from commit bodies, not censused.
- Armory item-prefix and skins.xml line numbers are live-install data and were left.

## docs/modding/items-armor.md

- Live Armory counts (3,240 items, 2,938 armour components, 677 khuzait-culture items, 33 XmlName rows, etc.) were not recomputed; the live install was only read for the folder list.
- Engine line refs (ItemObject.cs, ArmorComponent.cs, MBObjectManager.cs) come from the decompile dump and were not re-checked against v1.5.6.
- Now that arthedain, cardolan and rhudaur exist as cultures, the claim that the ten folderless folders (including arnor) only serve XSLT-renamed vanilla cultures may be incomplete; the folder-to-culture mapping is not written in code.

## docs/modding/items-mounts-and-harness.md

- Nearly all other claims are about the live LOTRLOME_Armory (monster files, LOTRAOM_horses.xml, harness counts, project.mbproj rows). I could not read them from the worktree, so they are unchanged.
- The 431-line audit_mount_parity output was not re-run, because the script reads the live install.
- I read the repo-side counts for taom_war_ram_a: troops_erebor 12, equipment sets 3, career starting equipment 2 (Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml), marketplace 1. All match the doc.

## docs/modding/items-shields.md

- Everything about LOTRLOME_Armory/ModuleData/LOTRAOM_shields.xml (224 shields, usage split, stat ranges, SubModule.xml:336, 21 Items rows, 14 monster rows) is live-Armory data not readable from this worktree; left as written. Settle by rerunning the quoted commands from the game Modules folder.
- The 'no tool reads ForceAttachOffHand* or item_usage' gotcha: grep of tools, TAOM.Tests and Main found only generate_black_numenorean_weapons.py, a test fixture string, and blender gait_props.json, so it still holds; not otherwise changed.
- The 80 of 224 ARMOR_PREFIX_RE match count needs the live shield file; the regex itself is unchanged.

## docs/modding/items-weapons-and-crafting.md

- Other dated live-Armory counts in the Numbers table are now stale: 672 CraftingPiece rows (now 752), 347 CraftedItem rows (now 335), TwoHandedPolearm 104 (now 110), OneHandedSword 81 (now 73), TwoHandedSword 43 (now 40), and the AvailablePiece/UsablePiece row totals. I left them as 2026-09-05 records; rerunning the Numbers table commands would refresh them.
- Of the 33 listed commits only the tool-side ones (f1675c058, 2420508fb ITEM_SLOT_REJECTED) touch behaviour near this doc, and neither contradicts a statement here. Engine file:line refs (Crafting.cs, ItemObject.cs) are v1.4.8 and were not re-verified against v1.5.6.

## docs/modding/load-order-and-dependencies.md

- Doc is 58 KB; only the validator-related claims were checked against f1675c058. Other claims (engine line cites, settlement counts) not re-verified.

## docs/modding/lords-and-heroes.md

- Vanilla 'is_obsolete used 30 times in obsolete_characters.xml' and the 397 vanilla heroes were not recounted except heroes/lords (397 each, matched); 23 is_hero characters without a Hero row and 176 ids in both lords.xml and vanilla lords were re-verified and are unchanged.
- Engine file:line citations (Hero.cs, SandBoxManager.cs, MBObjectManager.cs, CharacterObject.cs) were not re-checked against v1.5.6; settle with taom-src or the decompile dump.
- Commit 28e03ec61 (Elegost) and the Lindon/Cardolan lord additions only change counts, no behaviour text needed.

## docs/modding/module-dependencies.md

- Audit rows G10-140, G10-141, G10-145, G10-153, G10-149 and G10-161 were already reflected in the doc at its current text (lines 9-13, 189-191, 226, 278, 755-757, the three Check lines carry -p:ModuleId=, number table row 874 reads 0 tracked under Main/_Module/bin), so nothing was applied for them. The proposed lint_docs finding for a missing -p:ModuleId= under docs/modding is a tools change and was not made.
- Live-install figures (42 / 42 live bin files, 3,150,674-byte diag.log, 19 live Modules folders, live Harmony/UIExtenderEx FileVersions, 9 Workshop DLLs) cannot be read from this worktree and were left. The ButterLib live copy and Workshop count may also have moved with 6027ecfae.
- Line references into Dependencies/_Module/SubModule.xml (for example 150-160 and 165-239) were not re-measured after commit 5fcf8390c added two server tags; the doc's own measurement date for that file is 2026-10-22, after that commit, so they were left.

## docs/modding/module-taom.md

- Line 393 states 22 LanguageFile rows 'as of 2026-10-02' (dated record, left); the tree now has 23. Doc is 68 KB and other claims were not re-verified beyond the validator and translate tool changes in f1675c058.

## docs/modding/modules-overview.md

- The Main/_Module/bin file count is stated as 4 in one place and 7 in another; the folder is untracked build output and absent from this worktree. A build on the desktop would settle it.
- Install-side numbers (module sizes, deployed ModuleData count 371, bin file counts, 19 module folders, package dry run, backup sweep, LauncherData rows, live manifest versions v2.0.23 and v1.5.5.*) are dated 2026-09-05 or 2026-10-28 measurements of the game install and could not be re-read from this worktree.
- Whether the deployed ModuleData count of 371 versus the repo's 567 still tells the 'additive deploy' story; needs a fresh find on the install.

## docs/modding/open-questions.md

- The recipe-add-a-kingdom row still calls the missing four 'the four newest kingdoms' and says the ten older kingdoms' <relationships> blocks name them 0 times. Kingdom.lindon, Kingdom.goblin etc. do now appear in taom_spkingdoms.xml (e.g. lines 956, 1056, 1156); which blocks they sit in was not traced.
- configs-balance.md row: TaomSettings.cs knob count is now 470 value knobs; docs/features/mcm.md and other docs quoting 219 were not checked.
- Rows about live Armory/TAOM_Map facts (items-armor, settlements, module-map, module-armory) could not be checked from the worktree; rows citing the v1.4.8 decompile were not re-run.
- culture-marketplace.md and settlement-guards.md ('14 settlements configured') cross-claims in rows were not verified.

## docs/modding/party-templates.md

- Only f1675c058 touches its owned path; BROKEN_PARTY_TEMPLATE_REF still exists in tools/validate_moduledata.py and tools/taom_schema.py, and the --code usage is unchanged. Template counts and other data claims were not re-verified.

## docs/modding/recipe-add-a-culture.md

- Audit row G10-228 is already applied: the validate_all_troop_refs.py step is gone from the recipe and the Check line is the proposed one. No commits touch its paths since base 7e800c5328. The 2026-09-05 counts (24 cultures, 210 wanderers, 383 templates) were not re-measured.

## docs/modding/recipe-add-a-field-battle-scene.md

- Checked against tools/build_battle_scenes.py (Rohan Plains II is in the Rohan region as of 4bd4f3e54; Gondor scenes as of 0e0387e06) and custom_battle_scenes.xml. The cell 77 (Rhun 11 + Barad-dur 4) and cell 114 (8 + 7, 12 distinct) odds examples recompute correctly. Not checked: the claim that the precompile list names taom_dwarves_battle_001_forceatmo only as excluded (shader-precompilation code not read).

## docs/modding/recipe-new-mod-from-zero.md

- The stage 3 to 18 numbers (383 party templates, 1001 heroes, 988 settlements, 100 XmlName rows) are 2026-09-05 measurements in the dated Numbers table and were not re-measured.
- Audit row G10-242 (validate_xml_schemas.py --module) was already applied at line 43; validate_xml_schemas.py does accept --module.

## docs/modding/recipe-retire-content.md

- The dated Numbers table row '16 stylesheets ... quoted from tools/README.md (2026-09-05)' was left as a dated record.
- Audit row G10-234 (troops.md 'Save Compatibility' citation) was already applied at line 46.

## docs/modding/settlements.md

- Installed TAOM_Map/SubModule.xml now has a single XmlName row, with Settlements at line 24 (doc says line 73), and TAOM_Map/ModuleData has no spworkshops.xml or other kit stubs (doc's workshops appendix and 'SubModule.xml line 66' describe them). Left unedited because it looks like a changed install on this machine rather than a repo fact; confirm whether TAOM_Map was deliberately slimmed.
- Engine Campaign.cs/Settlement.cs line refs not re-checked against v1.5.6.
- Last-written dates of the shadow copy (2026-05-26) are file mtimes and were left.

## docs/modding/skill-sets.md

- Archetype alias count: doc says six cultures carry archetype_alias; the tool has 6 'archetype_alias': { entries and tools/README.md says x6, so left as is.
- Engine citations (PropertyOwner.cs, MBObjectManager.cs, DefaultPerks.cs etc.) not re-checked against v1.5.6; the 'v1.4.8 dump' claims (MaxSkillLevels single hit, 25 trait ids) not rerun.
- tools/rebalance_troops.py baseline line ranges (63-75, 77-88, 90-101, 103-113, 115-120, 126) were not rechecked.

## docs/modding/strings-and-localization.md

- Gotchas line about line terminators says 18 + 3 + 1 = 22 std_taom types, but each language folder now holds 23 std_taom files and 23 LanguageFile rows (ls Languages/DE, grep -c LanguageFile). Counting each type's line endings needs a byte scan I did not run.
- The dated tables (2026-09-05 row counts, 2026-10-02 counts of 22 files and 19 GameText rows) are records and were left as written; they no longer match the tree (23 files, 20 rows).
- Commit f1675c058 made --module required for translate_with_claude.py; every invocation in this doc already passes --module TAOM, so no edit was needed.

## docs/modding/submodule-and-registration.md

- Engine file:line citations (Module.cs, MBObjectManager.cs and others) were checked against the v1.5.5 decompile; commit 12b010626 moved TAOM to v1.5.6 and I did not re-verify them.
- Armory and TAOM_Map figures (33 rows, 11 file rows, 18 folder rows, line 216) are in live modules I could not read from the worktree; left unchanged.
- The Numbers table keeps the 2026-09-05 date for rows I did not re-measure, and the intro line now says which rows were re-measured on 2026-11-03.

## docs/modding/troops.md

- Gotcha about validate_all_troop_refs.py hardcoding ten cultures at lines 80-97 was not re-checked (the tool is outside f1675c058's changes). Doc is 52 KB; other claims not re-verified.

## docs/modding/troubleshooting.md

- The validator finding codes named in its symptom table (MISSING_EDUCATION_TEMPLATES, LANDLESS_CULTURE, UPGRADE_TIER_COLLAPSE) still exist in tools/taom_schema.py. The Numbers section and the pasted run (94 INCONSISTENT_ARMOUR_SLOT warnings, 21 codes, registry counts) are dated 2026-09-05 records and were left; they very likely differ now since f1675c058 added codes, but a fresh validator run was not made.

## docs/modding/validation-and-testing.md

- The dated worked-example transcript (94 warnings, 5,900 items) and the 58-of-183 README count are 2026-09-05 measurements; rerunning them would settle whether to refresh them.

## docs/modding/wanderers-and-named-companions.md

- 'About 47 templates in the world at once' rests on 78 live towns (46.8 = 78 x 0.6, measured 2026-09-05 on the live map). The Rhovanion, Anduin Vale and Angaladh realms probably added towns; a town census of the live settlements.xml would settle whether 47 and the 78/143/616/159 figures still hold.
- The 15-race list and live-map facts (town_ES4 present, settlement ids) come from the install and were not re-read.
- WandererBackstoryCoverageTests passing at 321 wanderers was not run (no tests allowed); the string arithmetic 321x7+298=2545 is consistent.
