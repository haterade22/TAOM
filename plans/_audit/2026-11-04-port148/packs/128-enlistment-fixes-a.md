# Input pack: WP 128 enlistment-fixes-a

Title (draft): Enlistment fixes, part A: battle exits, raids, service wars, promotion notice, merit attribution
Wave: 4   Lane: A   Draft priority: P1   Groups: 8   Port commits: 14
Depends on (WP ids): 105, 119

## Orchestrator notes for this WP (binding)
- 2d1e5f338 (issue-827) lands before issue-831's hunks that edit MapEventHelperCanMainPartyLeaveBattlePatch.cs.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Port groups owned by this WP (from the commit classification)

### group `issue-827-enlistment-battle-exit`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
- `2d1e5f338` 2026-10-19 PORT P1 pick=conflict :: fix(enlistment): v2.0.35 - no walking out of the commander's battle
  - why: On 1.4.x an enlisted soldier can still take vanilla 'Leave' or 'Abandon army' in his commander's battle, which strands Enlistment in its battle state; the fix hides both through a postfix on MapEventHelper.CanMainPartyLeaveBattleCommonCondition.
  - port notes: Target and callers hold on v1.4.8: Helpers\MapEventHelper.cs:109 and exactly three EncounterGameMenuBehavior readers (:1566, :1638, :1877). The Enlistment files match the pre-commit 1.5.x versions except EncounterAdapter.cs, which differs by e9854bbd0's main-party read (small context conflict; e9854bbd0 deserves its own look). Patch66_Enlistment is applied on 1.4.x at SubModule.cs:1503. Restate the patch comment's v1.5.5 claims (including the unreachable defender branch) for v1.4.8.
  - issues: 827, 831

### group `issue-828-enlistment-discharged-menu`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs
- `b24e8549e` 2026-10-19 PORT P1 pick=conflict :: fix(enlistment): v2.0.35 - a way out of the service menu after discharge
  - why: After a discharge during load the saved enlistment service menu comes back with every option hidden and the campaign is stuck; 1.4.x still gates that wait menu on IsEnlisted (EnlistmentMenuBehavior.cs:85).
  - port notes: EnlistmentWaitMenuOptions, EnlistmentPlayerActionService, IEnlistmentPlayerActionService and ServiceStatusTextWriter on 1.4.x equal the fix's parent; EnlistmentMenuBehavior and EnlistmentWaitMenuPresenter differ only by the session-reset members 6fb37c9f7 added, and the TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs constructor hunk needs 6fb37c9f7 (or drop that hunk). Two new strings go into taom_enlistment_strings.xml and the 12 language files.
  - prereqs: 6fb37c9f7
  - conflicts: TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs
  - issues: 573, 828

### group `issue-831-enlistment-raids`  P1  track=shared-feature-fix  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/CommanderSnapshot.cs, Main/Adapters/CommanderTickSnapshot.cs, Main/Adapters/IEncounterAdapter.cs, Main/Adapters/VillageRaidReader.cs, Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs, Main/Features/Enlistment/ServiceAttachmentService.cs, Main/Features/Enlistment/VillageActionJoinPolicy.cs, TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs, TAOM.Tests/Features/Enlistment/SettlementFollowingTests.cs
- `7a912dcd5` 2026-10-20 PORT-PARTIAL P1 pick=conflict :: fix(enlistment): v2.0.35 - soldiers stay out of their lord's raids
  - why: Enlistment exists on both lines and 1.4.x ServiceAttachmentService.cs:115 joins any commander map event, so an enlisted soldier is held in battle state for a whole village raid and refused release; raids the commander leads stop counting as battles.
  - port notes: The merge onto 1.4.x is clean except MapEventHelperCanMainPartyLeaveBattlePatch.cs, which is absent on 1.4.x (created by 2d1e5f338): drop that comment-only hunk and any doc line that only describes that patch. IsMainPartyInMapEvent and HasCurrent already exist on 1.4.x; IsPartyRaiding, PartyIsRaiding, VillageRaidReader and VillageActionJoinPolicy arrive with this commit. The 1.4.x Enlistment tree has drifted heavily from the pre-image (61 files differ across Main/Features/Enlistment and the five adapters), so build and run the Enlistment tests. taom_enlist_act_raid goes into taom_enlistment_strings.xml (present on 1.4.x) with English placeholders in the 12 language files. Port together with c04ba3fe5, which closes this commit's documented gap.
  - conflicts: Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs
  - issues: 831
- `c04ba3fe5` 2026-10-20 PORT-PARTIAL P1 pick=conflict :: feat(enlistment): v2.0.35 - soldiers fight lords who relieve a raid
  - why: Closes 7a912dcd5's known gap: while a lord party with healthy troops defends the raided village, the soldier joins his commander's fight, using the same lord-defender test that v1.4.8 EncounterManager.cs:157 applies.
  - port notes: Apply after 7a912dcd5 (it edits VillageRaidReader and VillageActionJoinPolicy, which that commit adds). Drop the comment-only hunk in MapEventHelperCanMainPartyLeaveBattlePatch.cs (absent on 1.4.x). Re-verify the engine line refs against v1.4.8: the StartBattleAction raid branch is at :113-118 in the v1.4.8 dump, and the EncounterAdapter comment cites v1.5.5 PlayerEncounter.SetupFields lines. VillageRaidReaderTests stages engine battle objects and may need touching on 1.4.8.
  - prereqs: 7a912dcd5
  - conflicts: Main/Adapters/CommanderSnapshot.cs, Main/Adapters/CommanderTickSnapshot.cs, Main/Adapters/IEncounterAdapter.cs, Main/Adapters/VillageRaidReader.cs, Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs, Main/Features/Enlistment/ServiceAttachmentService.cs, Main/Features/Enlistment/VillageActionJoinPolicy.cs, TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs, TAOM.Tests/Features/Enlistment/SettlementFollowingTests.cs
  - issues: 954

### group `issue-832-enlistment-service-wars`  P1  track=shared-feature-fix  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/EncounterAdapter.cs, Main/Adapters/IEncounterAdapter.cs, Main/Adapters/ServiceDiplomacyAdapter.cs, Main/Features/Enlistment/ServiceBattleService.cs, Main/Features/Enlistment/ServiceDiplomacyService.cs, TAOM.Tests/Features/Enlistment/ServiceBattleServiceTests.cs, TAOM.Tests/Features/Enlistment/ServiceDiplomacyServiceTests.cs
- `1559b3261` 2026-10-21 PORT-PARTIAL P2 pick=conflict :: feat(enlistment): v2.0.35 - a soldier's wars follow his lord's
  - why: On 1.4.x an own-clan soldier who joins his lord's battle against a faction the realm declared war on after the oath starts an untracked private war that discharge never unwinds: in v1.4.8 the encounter Attack (MenuHelper.cs:269) calls BeHostileAction.ApplyEncounterHostileAction, which declares war for the player's MapFaction (BeHostileAction.cs:187-199), and 1.4.x mirrors wars only at the oath.
  - port notes: Take the join-time fix: EncounterAdapter.GetOpposingLeaderFactionId, ServiceDiplomacyService.MirrorWarAtJoin under ServiceWarPolicy.ShouldMirrorMidTerm (own-clan identity only, recorded in MirroredWars so discharge unwinds it), and the top-up in ServiceBattleService before the join. The IsServingAgainstOwnCrown sit-out is optional. These are design extensions the maintainer should opt into: the hourly mid-term reconciliation in EnlistmentReconciler, the honourable release with its InquiryAdapter popup, and the new string in 13 language files. The 1.4.x ServiceDiplomacyService has no player-context dependency, so its constructor and IoC hunks conflict. d4af27d3e needs this commit.
  - conflicts: Main/Adapters/EncounterAdapter.cs, Main/Adapters/IEncounterAdapter.cs, Main/Adapters/ServiceDiplomacyAdapter.cs, Main/Features/Enlistment/ServiceBattleService.cs, TAOM.Tests/Features/Enlistment/ServiceBattleServiceTests.cs
  - issues: 832
- `d4af27d3e` 2026-10-21 PORT-PARTIAL P1 pick=conflict :: fix(enlistment): v2.0.35 - vassals stay out of their lord's new wars
  - why: A 1.4.x vassal can enlist under a lord of another realm (EnlistmentDialogGateService.cs:48-52 refuses only a commander at war with his kingdom), and choosing Attack or Send troops in that lord's battle against a faction his kingdom is at peace with declares a kingdom-wide war (v1.4.8 BeHostileAction.cs:187-199 acts for the player's MapFaction).
  - port notes: Take ShouldSitOutAsVassal, the sit-out check in ServiceBattleService before the state transition, and their tests. It needs EncounterAdapter.GetOpposingLeaderFactionId from 1559b3261. The 1.4.x ServiceDiplomacyService has no player-context adapter: inject IPlayerContextAdapter, or make the kingdom check in ServiceBattleService. The MirrorWarAtJoin context line in the diff belongs to 1559b3261; if that part is not taken, keep only the sit-out.
  - prereqs: 1559b3261
  - conflicts: Main/Features/Enlistment/ServiceBattleService.cs, Main/Features/Enlistment/ServiceDiplomacyService.cs, TAOM.Tests/Features/Enlistment/ServiceBattleServiceTests.cs, TAOM.Tests/Features/Enlistment/ServiceDiplomacyServiceTests.cs
  - issues: 960

### group `enlistment-merit-hero-attribution`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Enlistment/Content/Domain/ContentConfigs.cs, Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs, Main/Features/Enlistment/Content/EnlistmentContentConfigProvider.cs, Main/Features/Enlistment/EnlistmentIoC.cs, Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs, Main/Features/Enlistment/ServiceMaintenanceService.cs, Main/_Module/ModuleData/enlistment/enlistment_config.json, TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs
- `9304a4cb9` 2026-10-23 PORT-PARTIAL P1 pick=conflict :: feat(enlistment): v2.0.35 - spoils from the foes you cut down
  - why: 1.4.x's EnlistmentMeritMissionBehavior counts kills by affectorAgent.IsMainAgent and samples whatever agent is main, and v1.4.8 lets the player take over a non-hero soldier after falling (Mission.CanTakeControlOfAgent requires !Character.IsHero, Mission.cs:6332; enabled in SandBoxMissions.OpenBattleMission and the siege mission), so that soldier's kills and positioning feed the enlisted hero's merit.
  - port notes: Take only the two guards in Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs: OnMissionTick treats a main agent whose Character is not CharacterObject.PlayerCharacter as down, and OnAgentRemoved counts a kill only when affectorAgent.Character == CharacterObject.PlayerCharacter. Take the two PersonalSpoilsWiringTests cases that assert CharacterObject.PlayerCharacter in OnMissionTick and OnAgentRemoved, as their own test file; they use RepoPaths.ReadSource, absent on 1.4.x (added on 1.5.x by 35007dbb8). Leave personal spoils (BattleLootAdapter, PersonalSpoilsPolicy, payout service, config, strings, the ConcurrentQueue of kill ids) and the Consume to ResetForNewSession rename. f75288ebf (#634, evidence item 5) turned the adjacent `_kills++` into Interlocked.Increment: port it first or expect a one-line context conflict.
  - prereqs: f75288ebf
  - conflicts: Main/Features/Enlistment/Content/Domain/ContentConfigs.cs, Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs, Main/Features/Enlistment/Content/EnlistmentContentConfigProvider.cs, Main/Features/Enlistment/EnlistmentIoC.cs, Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs, Main/Features/Enlistment/ServiceMaintenanceService.cs, Main/_Module/ModuleData/enlistment/enlistment_config.json, TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs
  - issues: 834

### group `issue-835-enlistment-promotion-notice`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Enlistment/EnlistmentIoC.cs, Main/Features/Enlistment/Equipment/EnlistmentKitTiers.cs, Main/_Module/ModuleData/taom_enlistment_strings.xml, TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs
- `75e190abe` 2026-10-23 PORT-PARTIAL P1 pick=conflict :: feat(enlistment): v2.0.35 - promotions get a notice, kit gets a line
  - why: Its side fix stops a battle promoting an enlisted player during the CommanderUnavailable grace state; on origin/bannerlord-1.4.5 EnlistmentBattlePayoutService.PayOutBattle calls _promotion.EvaluateAndApply() unguarded while EnlistmentDailyService.cs:194 already refuses promotion in that state.
  - port notes: Take only the 5-line guard in Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs (return true before EvaluateAndApply when _store.Record.State == EnlistmentState.CommanderUnavailable; the enum value exists on 1.4.x at Domain/EnlistmentState.cs:32) plus the test PayOut_CommanderUnavailable_EvaluatesNoPromotion, adapted because 1.4.x has PayOutBattle(bool won) while 1.5.x calls PayOutBattle(won, playerSimulated). Leave the promotion notice, IQuickNoticeAdapter/QuickNoticeAdapter, EnlistmentKitTiers, the kit line, strings and docs (feature). Not already on 1.4.x: git log -S on the guard comment found nothing. Pairs naturally with the 4666a254a grace-state wage fix.
  - conflicts: Main/Features/Enlistment/EnlistmentIoC.cs, Main/Features/Enlistment/Equipment/EnlistmentKitTiers.cs, Main/_Module/ModuleData/taom_enlistment_strings.xml, TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs
  - issues: 835

### group `enlistment-batch-842-845`  P1  track=architecture  commits=5  sim: clean=1 conflict=4
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Enlistment/Content/Domain/ServiceEnums.cs, Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs, Main/Features/Enlistment/Domain/ServiceStatusModel.cs, Main/Features/Enlistment/EnlistmentService.cs, Main/Features/Enlistment/Equipment/IEquipmentIssueLedger.cs, Main/Features/Enlistment/Equipment/InMemoryEquipmentIssueLedger.cs, Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs, Main/Features/Enlistment/ServiceStatusService.cs, TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentDailyServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentReconcilerTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs, TAOM.Tests/Features/Enlistment/Equipment/EquipmentIssueLedgerTests.cs, TAOM.Tests/Features/Enlistment/PersistedEquipmentIssueLedgerTests.cs, TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs
- `f21123c6a` 2026-10-26 PORT P2 pick=clean :: perf(enlistment): v2.0.35 - the commander-party check is an id compare
  - why: IsCommanderParty compares the commander's party id through GetPartyId instead of building a full commander snapshot; the 1.4.x file matches the pre-image.
  - port notes: Clean pick. ICommanderLordAdapter.GetPartyId exists on 1.4.x (Main/Adapters/ICommanderLordAdapter.cs:39). The test file EnlistmentStateQueryTests.cs exists there too. On 1.4.x the only callers are the capture paths in WarEventSnapshotAdapter and CareerQuest, so the gain is tiny there.
  - issues: 842
- `388e89de0` 2026-10-26 PORT-PARTIAL P3 pick=conflict :: refactor(enlistment): v2.0.35 - the daily column check skips unread work
  - why: Removes unread food, troop-tier, wound and kingdom-war scans from the daily army-rhythm probe and the unused hourOfDay parameter in the shared Enlistment feature.
  - port notes: Take every Main/ edit. ArmyRhythmProbeAdapter.cs and DutyOrchestrationService.cs on 1.4.x match the 1.5.x parent exactly. Also take the hourOfDay removal through EnlistmentDailyService, EnlistmentPlayerActionService, the two behaviours and EnlistmentWaitMenuOptions, plus the new ArmyRhythmSnapshotServiceTests.cs.

Conflicts:
- 1.4.x still names IArmyRhythmSnapshotService.Invalidate(); 1.5.x renamed it ResetForNewSession in the e99d705a3/6fb37c9f7/b9d18458e plan-014 series. Adapt the context or port that series first.
- TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs does not exist on 1.4.x; drop that hunk.
- EnlistmentDailyServiceTests.cs differs by about 42 lines and needs a hand merge.
  - conflicts: TAOM.Tests/Features/Enlistment/EnlistmentDailyServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs
  - issues: 843
- `218fa4053` 2026-10-26 PORT-PARTIAL P3 pick=conflict :: refactor(enlistment): v2.0.35 - remove enlistment data nothing reads
  - why: Deletes Enlistment data nothing reads (win and loss counters, duty failures, four reputation scores, unused events, petition withdrawal, payoff calculator) and their config entries; old saves still load because unknown record keys are ignored.
  - port notes: Re-derive on 1.4.x. The non-doc files this commit touches differ between the 1.4.x tip and the 1.5.x parent by about 2,050 inserted and 333 deleted lines, mostly EnlistmentBattlePayoutService, EnlistmentContentConfigProvider and their tests.

- The 1.4.x ServiceContentRecord carries the same dead keys (dutyFails, repF, tourneys, dutyFood). Confirm each removed member is still unread on 1.4.x before deleting it.
- Edit enlistment_config.json and enlistment_duties.json by hand.
- Save-compat holds: the record is a key=value SyncData string and the 1.4.x doc comment states unknown keys are ignored.
- Brings docs/reviews/rca-enlistment-dead-surface-2026-10-26.md and a gamemodels-services lessons entry.
  - conflicts: Main/Features/Enlistment/Content/Domain/ServiceEnums.cs, Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs, Main/Features/Enlistment/EnlistmentService.cs, Main/Features/Enlistment/Equipment/IEquipmentIssueLedger.cs, Main/Features/Enlistment/Equipment/InMemoryEquipmentIssueLedger.cs, TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentReconcilerTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentServiceTests.cs, TAOM.Tests/Features/Enlistment/Equipment/EquipmentIssueLedgerTests.cs, TAOM.Tests/Features/Enlistment/PersistedEquipmentIssueLedgerTests.cs
  - issues: 844
- `a20b4d154` 2026-10-26 PORT-PARTIAL P1 pick=conflict :: fix(enlistment): v2.0.35 - duty text has one source, in every language
  - why: Fixes English field duties showing 'a task for the company' and 'It went well.' for every duty by moving duty words into enlistment_duties.json; the 1.4.x tip has the same fallbacks, and v1.4.8 shows inline fallback text to English players.
  - port notes: Re-apply by hand. By diff --stat, the touched files differ between the lines: ServiceStatusTextWriter about 61 lines, ServiceStatusService 44, ServiceStatusModel 30, enlistment_duties.json 190, taom_enlistment_strings.xml 43.

Take:
- DutyText.cs.
- The per-duty words in enlistment_duties.json with their existing keys.
- The InteractiveDutyPresenter, FieldDutyRuntime and status-writer changes.
- tools/harvest_duty_strings.py, which replaces tools/generate_enlistment_duty_strings.py (present on 1.4.x).
- The tests and the localization-ui lessons entry.

Keys and English lines are unchanged, so translations do not move. It applies more cleanly after 218fa4053, which trims the same JSON rows and duty classes. The engine basis (LocalizedTextManager.cs:235 skips strings for English) was verified on v1.4.8.
  - conflicts: Main/Features/Enlistment/Domain/ServiceStatusModel.cs, Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs, Main/Features/Enlistment/ServiceStatusService.cs, TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs, TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs
  - issues: 845
- `4bd8554a4` 2026-10-26 PORT-PARTIAL P3 pick=conflict :: docs(enlistment): v2.0.35 - the batch's in-game checks and feature map
  - why: Enlistment feature doc in-game checks and feature-map wording for #842 to #845; it follows whichever of those commits land.
  - port notes: Take the in-game check bullets in docs/features/enlistment.md only for the issues that land: f21123c6a, 388e89de0, 218fa4053, a20b4d154. In docs/reference/feature-map.md, replace '4-domain reputation' only if 218fa4053 lands. The 'service pump fed by the campaign tick ... #841' wording describes a separate 1.5.x change; the 1.4.x row still says 'real-time service pump', so leave it unless #841 is ported.
  - issues: 842, 843, 844, 845

### group `issue-1021-enlistment-encounter-exits`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Enlistment/EnlistmentLoadNormalizer.cs, Main/Features/Enlistment/IEnlistmentConfigProvider.cs, TAOM.Tests/Features/Enlistment/DischargeServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentFeatureToggleTests.cs
- `90035a799` 2026-10-26 PORT-PARTIAL P1 pick=conflict :: fix(enlistment): v2.0.35 - no battle encounter is left without a way out
  - why: Closes four ways an enlisted soldier is left stuck after a battle, including the 1.4.x branch (ServiceBattleService.cs:143) that rolls back a landed battle join when the encounter menu fails to open, which can freeze or pull him out of his own map event.
  - port notes: Every precondition is on 1.4.x: the rollback branch (ServiceBattleService.cs:143), the feature-off discharge (EnlistmentReconciler.cs:154) and BreakStaleBattleLatch (:436). The files drifted since the fork (EnlistmentReconciler.cs +121/-43, EncounterAdapter.cs +132/-20, IEncounterAdapter.cs +39/-2 between origin/bannerlord-1.4.5 and 90035a799^) through 1.5.x enlistment fixes and features (for example 2d1e5f338, 1d9cc42f1, 3b848c411, 64691a56b, 1559b3261), so hand-apply: IEncounterAdapter.IsJoinedBattleEncounter and ForgetEndedBattleResult, the in-battle early return in DischargeService.RestoreCampaignContext, the widened latch and inside-settlement release in BreakStaleBattleLatch, the stay-in-battle branch and the ForgetEndedBattleResult call in ServiceBattleService, and the feature-off wait; adapt the four test files. PlayerEncounter.IsJoinedBattle and the static CampaignBattleResult setter exist on v1.4.8 (PlayerEncounter.cs:253, :267-277); re-check the siege continue-check reasoning against the v1.4.8 dump. Neither player log has an enlistment line, so this does not bear on the Harad freeze.
  - conflicts: Main/Features/Enlistment/EnlistmentLoadNormalizer.cs, Main/Features/Enlistment/IEnlistmentConfigProvider.cs, TAOM.Tests/Features/Enlistment/DischargeServiceTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentFeatureToggleTests.cs
  - issues: 1021

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 19, batch 20, batch 21, batch 22, batch 24, batch 25, batch 27, batch 29
