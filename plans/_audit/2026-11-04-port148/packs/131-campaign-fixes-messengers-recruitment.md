# Input pack: WP 131 campaign-fixes-messengers-recruitment

Title (draft): Messenger, recruitment and troop-weight fixes
Wave: 4   Lane: D   Draft priority: P1   Groups: 8   Port commits: 8
Depends on (WP ids): 105, 123

## Orchestrator notes for this WP (binding)
- 93684c32c needs 423d948d8 (plan 037, WP123). 2c7f7fae9 creates PrefabExtensionBrushTests that WP140 needs.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Port groups owned by this WP (from the commit classification)

### group `troopweight-garrison-patrol-penalty`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
- `c2449a306` 2026-10-16 PORT P1 pick=conflict :: fix(troopweight): v2.0.34 - garrisons and patrols pay no size penalty
  - why: On 1.4.x TaomPartySizeModel.cs:74 applies the troop-weight size penalty to every party, so garrisons desert daily against a deflated limit and elite patrols stay over theirs; this exempts garrisons and patrols.
  - port notes: Near-clean: the TroopWeight files differ from the pre-commit 1.5.x versions only in TroopWeightDisplayHook.cs (3 lines), ClanPartyItemVM_UpdateProperties_Patch.cs and TroopCountDiagnosticsBehavior.cs. Precondition verified on v1.4.8: DefaultPartySizeLimitModel.cs:86-92 routes IsGarrison to CalculateGarrisonPartySizeLimit and IsPatrolParty to CalculatePatrolPartySizeLimit; MobileParty.IsPatrolParty and IsGarrison exist (MobileParty.cs:1247,1250). Restate the v1.5.5 citation in TroopWeightPenaltyScope's comment for v1.4.8 and reword its pointer to docs/reviews/adopt-yotthani-2026-10-16.md (580, absent on 1.4.x). Not already on 1.4.x: git grep TroopWeightPenaltyScope on origin/bannerlord-1.4.5 finds nothing.

### group `issue-886-castle-recruitment`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/SubModule.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, TAOM.Tests/Migration/TranspilerSiteBindingTests.cs
- `f83a63e57` 2026-10-19 PORT-PARTIAL P1 pick=conflict :: feat(recruitment): v2.0.35 - AI castle recruiting gated on volunteers
  - why: On 1.4.x the CastleRecruitment transpiler opens every castle as an AI recruiting stop, so lords ride to empty castles and empty castles lose vanilla's score; this gates a castle on a live notable holding a volunteer in slots 0-3 and replaces the hourly postfix with an event listener.
  - port notes: The touched CastleRecruitment hooks are identical on 1.4.x, and the engine facts hold on v1.4.8: AiVisitSettlementBehavior.cs:279 IsCastle gate before GetAvailableWageBudget, :327 x1.4 castle bonus, :406-435 four own-faction slots and two foreign; RecruitmentCampaignBehavior.cs:291-300 HourlyTickParty skips castles and :414 private CheckRecruiting(MobileParty, Settlement); FillSettlementsToVisitWithDistancesAsDays at :706. Adapt SubModule.cs (1.4.x calls _harmony.PatchCategory) and create TranspilerSiteBindingTests or fold its rows into ReflectionSiteBindingTests (1.4.x lacks the file; on 1.5.x it came with the v1.5.2 bump 8b9f0a233). Dropping the FillSettlements transpiler is a deliberate behaviour change (kingdom lords stop targeting friendly foreign castles); keep it with the rest. Leave the patch-targets.md Diplomacy row, which belongs to the unported 2410aec51.
  - conflicts: Main/SubModule.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, TAOM.Tests/Migration/TranspilerSiteBindingTests.cs
  - issues: 886

### group `issue-944-messenger-button-hint`  P2  track=shared-feature-fix  commits=1  sim: clean=1 conflict=0
- `6647f8f57` 2026-10-20 PORT P2 pick=clean :: fix(messengers): v2.0.35 - greyed Send Messenger button says why
  - why: Messengers exists on both lines: the greyed Send Messenger button's hint never shows because Gauntlet never hovers a disabled widget, so the button moves into an always-enabled wrapper (vanilla governor-button shape).
  - port notes: git diff 6647f8f57^ origin/bannerlord-1.4.5 over MessengerEncyclopediaMixin.cs, MessengerEncyclopediaPrefabExtension.cs and docs/features/messengers.md is empty, so this is a clean cherry-pick plus a v2.0.28 re-subject. The new MessengerEncyclopediaPrefabTests is self-contained (it builds the prefab XML only). Not tested in game upstream either.
  - issues: 944

### group `issue-918-castle-ai-recruit-toggle`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/CastleRecruitment/Hooks/CastleAiRecruitDispatcher.cs, Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs, Main/Features/TroopProgression/Models/TaomVolunteerModel.cs, Main/SubModule.cs, TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
- `0d3f2bc88` 2026-10-20 PORT-PARTIAL P1 pick=conflict :: fix(recruitment): v2.0.35 - AI castle toggle covers entry recruits
  - why: The #918 gap exists on 1.4.x: in v1.4.8, OnBeforeSettlementEntered (RecruitmentCampaignBehavior.cs:563) runs CheckRecruiting (:597-602), then RecruitVolunteersFromNotable (:499) and VolunteerModel.MaximumIndexHeroCanRecruitFromHero (:522), with no castle gate, while the 1.4.x toggle only steers AiVisitSettlementBehavior scoring (CastleAiToggle) and the hourly Patch42 postfix.
  - port notes: Take: the castle rule in TaomVolunteerModel.MaximumIndexHeroCanRecruitFromHero (checked before the alignment rule), ICastleRecruitmentService.BlocksAiRecruitAtCastle with its service body and tests, the SubModule.cs constructor wiring, and a binding test pinning the vanilla VolunteerModel call. Adapt: the 1.4.x TaomVolunteerModel constructor takes ICulturalFeatsService, not VolunteerProductionService, so that hunk conflicts. CastleRecruitmentEngineMirrorTests and CastleAiRecruitDispatcher are 1.5.x-only (from feature commit f83a63e57, which is not needed): write a 1.4.x binding test instead. The #920 co-op hourly pause would go into Patch42_HourlyTickParty_Postfix on 1.4.x, and only if co-op is in scope.
  - conflicts: Main/Features/CastleRecruitment/Hooks/CastleAiRecruitDispatcher.cs, Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs, Main/Features/TroopProgression/Models/TaomVolunteerModel.cs, Main/SubModule.cs, TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
  - issues: 918, 920

### group `issue-887-ai-recruit-trip-alignment`  P2  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs, Main/Features/TroopProgression/Models/TaomVolunteerModel.cs, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
- `93684c32c` 2026-10-23 PORT-PARTIAL P2 pick=conflict :: feat(recruitment): v2.0.35 - AI lords skip recruit trips alignment bars
  - why: AI lords score recruiting trips with vanilla's volunteer estimate, which ignores the Free/Evil block TaomVolunteerModel already applies on 1.4.x, so they ride to towns that give them nothing; the patch target is unchanged in v1.4.8 and the settings provider it reads is an uncached hot-path read on 1.4.x too.
  - port notes: Take Patch114_AiVolunteerEstimate_Postfix (target AiVisitSettlementBehavior.GetApproximateVolunteersCanBeRecruitedDataFromSettlement(Hero, Settlement), same private signature in the v1.4.8 dump at C:\Decompiled_Bannerlord\_categories_v1.4.8\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors\AiVisitSettlementBehavior.cs:406, called from AiHourlyTick at :137/:304; confirm with taom-src), the lazily cached TaomSettings accessor in RecruitmentAlignmentSettingsProvider (1.4.x reads TaomSettings.Instance on every getter), the cross-reference comment in TaomVolunteerModel, the binding and wiring tests, and the harmony-patch-registry row. Adapt registration: AlignmentRecruitmentModule and Main/Composition/FeatureModules.cs are 1.5.x-only, so wire the patch category and static Initialize through RecruitmentAlignmentIoC plus SubModule.cs (single-owner). Renumber Patch114: the highest quoted PatchNN_ category on origin/bannerlord-1.4.5 is Patch88. The CampaignHotPathSettingsProvidersTests row needs the 423d948d8 perf series (test file absent on 1.4.x); port that first or drop the row. Framed as a feature on 1.5.x, so the maintainer may treat the AI half as optional.
  - prereqs: 423d948d8
  - conflicts: Main/Composition/FeatureModules.cs, Main/Features/TroopProgression/Models/TaomVolunteerModel.cs, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
  - issues: 887

### group `issue-898-messenger-session-rules`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs, Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs, Main/Features/Enlistment/Presentation/ServiceVocabulary.cs, Main/Features/Messengers/MessengerCampaignBehavior.cs, Main/Features/TokensOfEsteem/GiftConversationFlow.cs, Main/Features/TokensOfEsteem/Hooks/GiftBehavior.cs, Main/Features/TokensOfEsteem/TokensOfEsteemModule.cs, TAOM.Tests/Features/Enlistment/EnlistOfferVisibilityTests.cs, TAOM.Tests/Features/TokensOfEsteem/GiftBehaviorTests.cs, TAOM.Tests/Features/TokensOfEsteem/GiftConversationFlowTests.cs, TAOM.Tests/Features/TokensOfEsteem/TokensOfEsteemWiringTests.cs
- `1dc0a8d7c` 2026-10-23 PORT-PARTIAL P1 pick=conflict :: fix(messengers): v2.0.35 - messengers no longer move armies or troops
  - why: Messenger conversations (shared Messengers feature) let the player join or call armies, swear enlistment, move troops or start the board game at a distance, and a failed open left state behind; the 1.4.x open path (MessengerCampaignBehavior.cs:390-428 on origin/bannerlord-1.4.5) has the same exposure.
  - port notes: Take MessengerSessionRulesPatch (six postfixes; all six targets exist on v1.4.8: LordConversationsCampaignBehavior.cs:2044, :2622, :3826, :3836, CompanionRolesCampaignBehavior.cs:495, BoardGameCampaignBehavior.cs:823), MessengerEntryPolicy and the WouldAttachToArmy field-path branch, AbortConversation (1.4.x already has CleanUpSettlementEncounter at :449/:471), the Enlistment greyed oath (OfferIsTakeable with a messenger flag, ServiceVocabulary messenger reason; adapt to 1.4.x's one-argument EnlistUnavailableReason), the new strings in taom_module_strings.xml and the 12 language files, tests, doc rows and the RCA. Prerequisite adaptation: 1.4.x has no IMessengerConversationState (on 1.5.x it arrived with feature 06a1a1b71, Lord Courtesies); add only the open/close flag, set in the open path and cleared in OnEndMission and AbortConversation. Leave Main/Composition/FeatureModules.cs and MessengerSessionRulesModule (register through 1.4.x IoC.cs and SubModule.cs, single-owner files), all TokensOfEsteem files (1.5.x-only feature) and the comment-only ReturnToArmy edit. Renumber Patch117: the highest Patch number on origin/bannerlord-1.4.5 is 88. The postfix-after-Patch66 rationale holds (1.4.x LordConversationsConditionPatches.cs:37). Re-check the v1.5.5 Campaign.cs comment it writes in OnSessionLaunched against v1.4.8, or keep the 1.4.x comment.
  - conflicts: Main/Composition/FeatureModules.cs, Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs, Main/Features/Enlistment/Presentation/ServiceVocabulary.cs, Main/Features/Messengers/MessengerCampaignBehavior.cs, Main/Features/TokensOfEsteem/GiftConversationFlow.cs, Main/Features/TokensOfEsteem/Hooks/GiftBehavior.cs, Main/Features/TokensOfEsteem/TokensOfEsteemModule.cs, TAOM.Tests/Features/Enlistment/EnlistOfferVisibilityTests.cs, TAOM.Tests/Features/TokensOfEsteem/GiftBehaviorTests.cs, TAOM.Tests/Features/TokensOfEsteem/GiftConversationFlowTests.cs, TAOM.Tests/Features/TokensOfEsteem/TokensOfEsteemWiringTests.cs
  - issues: 898
skipped in this group: `358c94559` SKIP-1.5-ONLY-FIX fix(enlistment): v2.0.35 - greyed oath keeps the commander's name

### group `issue-989-messenger-button-brushes`  P2  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Client/Features/LinesOfDescent/UI/EncyclopediaHeroFamilyTreePrefab.cs, TAOM.Tests/Features/Messengers/MessengerEncyclopediaPrefabTests.cs
- `2c7f7fae9` 2026-10-24 PORT-PARTIAL P2 pick=conflict :: fix(messengers): v2.0.35 - send-messenger button gets its button look
  - why: The Send Messenger button names two brushes no module declares; 1.4.x's Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs:36,42 names the same ones, and on the installed v1.4.8 game neither Popup.PartySelection.Confirm nor Popup.PartySelection.Button.Text is declared, while Popup.Button.Base and Popup.Button.Text are in Native/GUI/Brushes/Popup.xml.
  - port notes: Apply the brush change at the 1.4.x path Main/Features/Messengers/UI/ (1.5.x moved the file to Client/ in d021426b0). Take TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs but scan only Main/ (no Client/ on 1.4.x); it reads BANNERLORD_GAME_DIR and goes Inconclusive without an install. Take the gui-ui.md and moduledata-validation.md gate lines, the two lessons and the RCA. Leave the LinesOfDescent comment and lines-of-descent.md (1.5.x-only feature). The 2 lines added to MessengerEncyclopediaPrefabTests.cs need that file, which came with 6647f8f57; applies more cleanly after it if that is ported.
  - conflicts: Client/Features/LinesOfDescent/UI/EncyclopediaHeroFamilyTreePrefab.cs, TAOM.Tests/Features/Messengers/MessengerEncyclopediaPrefabTests.cs
  - issues: 989

### group `issue-898-messenger-pricing`  P3  track=architecture  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Messengers/Domain/HeroSnapshot.cs, Main/Features/Messengers/MessengerConfigProvider.cs, Main/Features/Messengers/MessengerTargetReader.cs, TAOM.Tests/Features/Messengers/MessengerConfigProviderTests.cs, TAOM.Tests/Features/Messengers/MessengerServiceTests.cs
- `464cbd375` 2026-10-24 PORT-PARTIAL P3 pick=conflict :: refactor(messengers): v2.0.35 - one config check and a dead field gone
  - why: Mostly cleanup of bbf2db053's pricing config, but HeroSnapshot.IsInMapEvent is dead on 1.4.x too (declared at Messengers/Domain/HeroSnapshot.cs:14, only passed in at MessengerCampaignBehavior.cs:596 and UI/MessengerEncyclopediaMixin.cs:120, never read).
  - port notes: Take only the IsInMapEvent deletion with its two constructor call sites, plus the comment on why ResolveHero uses Hero.FindFirst (1.4.x calls it at MessengerCampaignBehavior.cs:576). The premise holds on v1.4.8: Hero.cs:2130-2138, FindFirst scans Campaign.Characters, Find goes through CampaignObjectManager. Optionally fold 1.4.x's two hand-written range checks into a Checked helper. Leave the pricing fields, MessengerTargetReader, the pricing tests and rca-messenger-pricing.
  - conflicts: Main/Features/Messengers/Domain/HeroSnapshot.cs, Main/Features/Messengers/MessengerConfigProvider.cs, Main/Features/Messengers/MessengerTargetReader.cs, TAOM.Tests/Features/Messengers/MessengerConfigProviderTests.cs, TAOM.Tests/Features/Messengers/MessengerServiceTests.cs
  - issues: 898
skipped in this group: `bbf2db053` SKIP-FEATURE feat(messengers): v2.0.35 - messenger price grows with distance; `00cbd43d1` SKIP-FEATURE docs: v2.0.35 - maps and messenger doc show distance pricing

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 19, batch 21, batch 22, batch 25, batch 26
