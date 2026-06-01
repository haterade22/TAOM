# Codex Adversarial Review -- TAOM Career Quest System (Phase 1)

You are reviewing a NEW feature in the TAOM Bannerlord 1.4.5 total-conversion mod: a career-tied quest system. Completing a career's tier quest unlocks that tier of the career choice tree (hybrid with the existing level gate) and grants a reward. Adapted from TheOldRealms TOR_Core (which targets Bannerlord 1.3.15 -- API drift is a real risk; TAOM is 1.4.5). The code already passed a 4-cluster 1.4.5 API verification + a 5-agent internal deep-review; you are the adversarial second opinion.

Repo root: c:/Users/mikew/source/repos/TAOM

## TAOM ID CHEATSHEET
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar. The one authored quest is for career "captain_of_osgiliath" (Gondor, tier 2). Skill id used: "OneHanded" (vanilla DefaultSkills id).

## READ FIRST
- docs/features/career-quest-system.md -- architecture, config schema, known limitations
- docs/reviews/rca-career-quest-system-2026-06-01.md -- the deep-review RCA, already-fixed findings incl. the SpecialQuestType save-load fix
- Main/_Module/ModuleData/career_system/taom_career_quests.xml -- the one authored quest

## KNOWN SUSPECTS -- CONFIRM or DISPUTE each with evidence. Decompile INSTALLED 1.4.5 DLLs (ilspycmd on "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/*.dll"), NOT a decompiled dump.
1. SpecialQuestType save-load fix. CareerQuest : QuestBase overrides `public override string SpecialQuestType => "taom_career_quest";` because QuestManager.OnGameLoaded cancels any ongoing quest with no associated IssueBase unless IsSpecialQuest. CONFIRM sufficiency: decompile QuestManager.OnGameLoaded + QuestBase.InitializeQuestOnLoadWithQuestManager on 1.4.5 -- does a non-empty SpecialQuestType fully route the quest through InitializeQuestOnLoadWithQuestManager (which runs RegisterEvents + InitializeQuestOnGameLoad)? Are there OTHER load-path requirements a standalone (issue-less) quest must satisfy to survive AND keep ticking (DailyTick driven by QuestManager)?
2. [SaveableField] List<JournalLog> _logs shared-object-graph. CareerQuest stores its discrete-log refs in a [SaveableField] List<JournalLog> _logs; the base QuestBase stores the same logs in _journalEntries. The code assumes after load _logs[i] is the SAME instance as base _journalEntries[i] so UpdateQuestTaskStage(_logs[i], n) finds it. CONFIRM under TaleWorlds save-graph identity semantics on 1.4.5 -- or does the deserializer mint two distinct JournalLog instances, silently breaking post-load progress display?
3. Hybrid tier gate. CareerQuestService.IsTierUnlocked(level, tier, heroId) = registry.IsTierAvailable(level, tier) OR dataService.IsTierUnlocked(heroId, tier). Quest completion -> CareerQuestService.ApplyRewards -> dataService.UnlockTier(heroId, tier) -> persisted in _taom_careerTiers. Trace: can a completed tier fail to unlock, or a non-completed tier wrongly unlock? Is the VM (CareerScreenVM.IsTierAvail) consistent with the behavior (CareerQuestCampaignBehavior.TryOfferNext uses dataService.IsTierUnlocked only to decide "tier done")?
4. Event detection on 1.4.5. WinBattles via OnPlayerBattleEndEvent + (mapEvent.WinningSide != BattleSideEnum.None && mapEvent.WinningSide == mapEvent.PlayerSide); SettlementsCaptured via OnSettlementOwnerChangedEvent + (capturerHero == Hero.MainHero); KillEnemyLords via HeroKilledEvent + (killer == Hero.MainHero && victim.IsLord); TournamentsWon via TournamentFinished + (winner == CharacterObject.PlayerCharacter); VisitSettlementType via SettlementEntered + (party == MobileParty.MainParty). Decompile each event + its dispatcher invocation on 1.4.5. Any false-positive (counts when the player did not do it) or false-negative (misses a legit completion)? Does mapEvent.PlayerSide deref safely inside OnPlayerBattleEndEvent? Does winner==CharacterObject.PlayerCharacter correctly mean the player won the tournament?
5. Offer loop. CareerQuestCampaignBehavior offers the lowest not-yet-done tier quest on session-launch + daily tick, gated by AnyActiveCareerQuest() + _declined (persisted CSV) + _offerPending (in-memory). Confirm: no stuck _offerPending (both inquiry callbacks reset it; OnAccept sets it false BEFORE a try-block that could throw), no duplicate active quests, no re-offer of a completed/declined quest. A 2-button InquiryData is modal and forces accept/decline.
6. Config-provider validation completeness. CareerQuestConfigProvider.ParseQuests skip-and-warns on: empty id, dup id, missing career_id, tier out of 1-3, no valid objectives, unknown objective/reward type, objective target<=0, SkillThreshold/VisitSettlementType missing param, UnlockTier amount out of 1-3, GrantItem/GrantAttributeFlag missing param, GrantRenown/GrantInfluence amount<=0. Any gap that lets a malformed quest through, or any over-strict reject of a valid quest?
7. Persistence. CareerPersistenceBehavior now syncs a 4th flat dict _taom_careerFlags (heroId->csv) alongside careerIds/choices/tiers, reconstruct gated on dataStore.IsLoading. CareerQuestCampaignBehavior syncs _taom_cq_declined. Confirm round-trip correctness + that the IsLoading gate still prevents a mid-save reconstruct that drops in-flight data (this exact bug -- Phase 9b #128 -- was fixed before for the first 3 dicts; verify the 4th did not reintroduce it).

## FILE LISTS
Domain: Main/Features/CareerSystem/Domain/{CareerQuestDefinition,CareerQuestObjectiveDefinition,CareerQuestRewardDefinition,HeroCareerData}.cs
Service/config: Main/Features/CareerSystem/{ICareerQuestConfigProvider,CareerQuestConfigProvider,ICareerQuestService,CareerQuestService,ICareerDataService,CareerDataService,CareerPersistenceBehavior,CareerSystemIoC}.cs
Engine: Main/Features/CareerSystem/Quests/{CareerQuest,CareerQuestSaveableTypeDefiner,CareerQuestCampaignBehavior}.cs
Adapter: Main/Adapters/{IQuestHeroAdapter,QuestHeroAdapter,IQuestHeroAdapterFactory,QuestHeroAdapterFactory}.cs
UI: Main/Features/CareerSystem/UI/{CareerScreenVM,GauntletCareerScreen}.cs
Wiring: Main/SubModule.cs (search "CareerQuest"); Main/Features/CareerSystem/CareerSystemIoC.cs
Data: Main/_Module/ModuleData/career_system/taom_career_quests.xml ; Main/_Module/ModuleData/taom_module_strings.xml (taom_cq_* keys)
Tests: TAOM.Tests/Features/CareerSystem/{CareerQuestServiceTests,CareerQuestConfigProviderTests}.cs

## REQUIRED SECTIONS in your output
- VANILLA CODE: paste the decompiled 1.4.5 QuestManager.OnGameLoaded, QuestBase.InitializeQuestOnLoadWithQuestManager + the *WithQuestManager tick wrappers, the relevant CampaignEvents dispatcher methods (OnPlayerBattleEnd / OnSettlementOwnerChanged / OnHeroKilled / TournamentFinished / SettlementEntered), and how JournalLog is save-serialized.
- KNOWN SUSPECTS: CONFIRMED / DISPUTED + evidence for each of the 7.
- DEEP ANALYSIS: trace one CareerQuest end-to-end: start -> objective progress -> save -> load -> resume -> complete -> reward -> tier unlock. Flag every step where state could be lost or mis-evaluated.
- ANYTHING MISSED: bugs outside the Known Suspects.
- FINDINGS: a table -- # | Severity | File:line | Issue | Recommended fix.

## QUALITY GATES
Do not flag vanilla-matching code as a bug. Do not assume 1.3.15 signatures -- verify on 1.4.5. If you cannot decompile something, say UNVERIFIED rather than guessing. Prior Codex successes here: catching save-load lifecycle gaps + missing engine gates. Prior failures: flagging correct code as wrong, skipping the hard decompile sections, assuming a vanilla id/culture is something it is not.

Output your full review as markdown (this file's stdout is captured to docs/reviews/codex-adversarial-career-quest-system-2026-06-01.md).
