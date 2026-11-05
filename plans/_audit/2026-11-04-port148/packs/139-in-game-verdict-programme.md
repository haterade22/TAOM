# Input pack: WP 139 in-game-verdict-programme

Title (draft): In-game verdict programme: outcome log, check lines for ported fixes, the verdict tool, the silent-catch ratchet
Wave: 5   Lane: D   Draft priority: P2   Groups: 5   Port commits: 18
Depends on (WP ids): 118, 126, 129, 130, 131

## Orchestrator notes for this WP (binding)
- Order (batch 32): cb4eeaa3d, c8d69e426, per-feature check lines (only for base fixes that were ported), skill hooks, the swallowed-exception ratchet.
- OutcomeLog needs IStampClock from 346f3e093 (WP118).
- K1: lift 92839bc52's IlCallScanner.ExtractOpCodes hunk (+20 lines; applies cleanly on top of a27c6b338's Fingerprint hunk) before 0ab3dc35a, whose test calls both helpers.
- K1: 852678f86: drop the VolunteerProductionService constructor parameter (1.4.x CastleRecruitmentService.cs:20 takes only the settings provider) and put a local 9-line TryFloorElapsedDays beside CastleRecruitObserver (from War Chronicle commits d340a725c/5ba8d6c82, which stay OUT).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Port groups owned by this WP (from the commit classification)

### group `tools-ingame-verdict`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
- `c8d69e426` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.36 - in-game check verdicts read from session logs
  - why: Adds tools/ingame_verdict.py, which reads session logs and judges owed in-game checks against the build named in the [BuildStamp] line; 1.4.x logs that line in the same format (Main/Core/Diagnostics/BuildStampReport.cs:146, matched by E3 line 2).
  - port notes: Take tools/ingame_verdict.py, tools/tests/test_ingame_verdict.py with its fixtures (synthetic manifests 9001-9102) and the tools/README row. Leave tools/ingame_checks/970.toml (Client/ battle action bar) and 981.toml (co-op server settings); write 1.4.x manifests instead. Adapt: it imports DEFAULT_GAME_ROOT from tools/_gamedir.py, which 1.4.x lacks (1.5.x added it in fd1cee10b). It needs Python 3.11+ for tomllib. 1.5.x follow-ups: 53e13cfe3 and 95647d13b, plus c5705474e for /verify and /release.
  - issues: 1070, 970, 981

### group `issue-1071-outcome-log`  P2  track=diagnostics  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Client/Features/ReevesLedger/UI/TownManagementLedgerMixin.cs, Main/Core/Logging/ScreenFaultLog.cs, Main/SubModule.cs, TAOM.Tests/Core/Logging/ScreenFaultLogTests.cs, TAOM.Tests/Features/ReevesLedger/ReevesLedgerWiringTests.cs
- `cb4eeaa3d` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: feat(logging): v2.0.36 - check lines, counters and a session summary
  - why: Adds the IOutcomeLog/Outcomes logging API ([Check] lines, hot-path counters, once-per-site [Swallowed] warnings) and [Session] summaries at save, crash, unload, process exit and a 10-minute main-thread heartbeat; pure diagnostics infrastructure that every later check-line commit in this batch needs.
  - port notes: Take Main/Core/Logging/IOutcomeLog.cs and OutcomeLog.cs (OutcomeLog, OutcomeCounter, Outcomes facade), OutcomeLogTests/OutcomeSeamsTests/OutcomesTests, the IoC registration (1.4.x IoC.cs:223-225 RegisterLoggingServices), WriteSummary("save") in SaveManager_Save_Patch (same shape on 1.4.x), WriteSummary("crash") in CrashReportService.HandleException (1.4.x computes offMainThread inside the method; adapt ctor and place it before ComposeContext), Outcomes.Initialize right after IoC.Configure, Outcomes.Tick in OnApplicationTick, WriteSummary("exit") in OnSubModuleUnloaded (1.4.x has no EarlyLogBridge and no [Shutdown] marker; that marker came with 1.5.x c80006994). OutcomeLog's default ctor uses StopwatchStampClock/IStampClock from 346f3e093 (load-stamps), absent on 1.4.x: port those two small types or substitute a Stopwatch clock. PatchShield.SwallowedTotal already exists on 1.4.x (Dependencies/Foundation/PatchShield.cs:104). Leave the ScreenFaultLog hunk and ScreenFaultLogTests (ScreenFaultLog arrives with 69b47d00b, absent on 1.4.x) and the Client/ReevesLedger hunk plus ReevesLedgerWiringTests (1.5.x-only feature and project).
  - conflicts: Client/Features/ReevesLedger/UI/TownManagementLedgerMixin.cs, Main/Core/Logging/ScreenFaultLog.cs, Main/SubModule.cs, TAOM.Tests/Core/Logging/ScreenFaultLogTests.cs, TAOM.Tests/Features/ReevesLedger/ReevesLedgerWiringTests.cs
  - issues: 1071

### group `issue-1072-ingame-check-lines`  P2  track=diagnostics  commits=11  sim: clean=0 conflict=11
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Client/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs, Main/Features/CareerSystem/Hooks/Patch27_CareerSystem/ViewModel_ExecuteCommand_CareerScreen_Patch.cs, Main/Features/CareerSystem/UI/CareerScreenChecks.cs, Main/Features/CastleRecruitment/CastleRecruitmentService.cs, Main/Features/CastleRecruitment/Hooks/CastleRecruitObserver.cs, Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs, Main/Features/CastleRecruitment/ICastleRecruitmentService.cs, Main/Features/CompanionTactics/BattleActionBar/BattleActionBarChecks.cs, Main/Features/CompanionTactics/BattleActionBar/BattleActionBarService.cs, Main/Features/HeroRace/RacePersistenceService.cs, TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs, TAOM.Tests/Features/CastleRecruitment/CastleRecruitmentServiceTests.cs, TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarChecksTests.cs, TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs, TAOM.Tests/Features/CompanionTactics/CompanionTacticsWiringTests.cs, TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs, TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs, TAOM.Tests/Features/TroopProgression/VolunteerProductionWiringTests.cs, TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs, tools/ingame_checks/1011.toml, tools/ingame_checks/918.toml, tools/ingame_checks/970.toml, tools/ingame_checks/971.toml, tools/tests/fixtures/ingame_verdict/attach_pass.txt, tools/tests/fixtures/ingame_verdict/attach_pass_and_failed.txt, tools/tests/fixtures/ingame_verdict/attach_pass_no_battle_load.txt, tools/tests/fixtures/ingame_verdict/attach_two_battles.txt, tools/tests/fixtures/ingame_verdict/race_legend_corrupt.txt, tools/tests/fixtures/ingame_verdict/race_load_mismatch.txt, tools/tests/fixtures/ingame_verdict/race_load_no_dead.txt ...
- `b4ad748f2` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: feat(logging): v2.0.36 - battle action bar attach and stance checks
  - why: Battle action bar (feature on both lines) writes attach and stance [Check] lines with a #970 manifest so the in-game verdict tool can judge it from the session log.
  - port notes: Only after the #970 fixes land on 1.4.x: 40385aa5b (deferred attach on the first Battle tick) and d96f349f1 (view registration). The 1.4.x view, Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs, still attaches in OnMissionScreenInitialize with no ShouldAttach. Apply the Client/ hunks to that Main path; the manifest gate's 'Main or Client' literal scan becomes Main only. Needs OutcomeLog (cb4eeaa3d) and the verdict tool (c8d69e426, outside this batch).
  - prereqs: cb4eeaa3d, c8d69e426, 40385aa5b, d96f349f1
  - conflicts: Client/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs, Main/Features/CompanionTactics/BattleActionBar/BattleActionBarService.cs, TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs, TAOM.Tests/Features/CompanionTactics/CompanionTacticsWiringTests.cs, tools/ingame_checks/970.toml, tools/tests/fixtures/ingame_verdict/attach_pass.txt, tools/tests/fixtures/ingame_verdict/attach_pass_and_failed.txt, tools/tests/fixtures/ingame_verdict/attach_pass_no_battle_load.txt, tools/tests/fixtures/ingame_verdict/attach_two_battles.txt, tools/tests/test_ingame_verdict.py
  - issues: 1072, 970
- `30a938689` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(logging): v2.0.36 - fail #970 attach check on an unregistered bar
  - why: Review follow-up: the #970 manifest fails on the view's own 'MissionScreen=NULL' line and a false end-of-mission FAIL is removed.
  - port notes: Ride with b4ad748f2; same Client/ to Main/ relocation; manifest and fixtures need c8d69e426.
  - prereqs: b4ad748f2
  - conflicts: Main/Features/CompanionTactics/BattleActionBar/BattleActionBarChecks.cs, TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarChecksTests.cs, TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs, tools/ingame_checks/970.toml, tools/tests/test_ingame_verdict.py
  - issues: 1072, 970
- `0bc2a132f` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(logging): v2.0.36 - pin the battle action bar attach verdicts
  - why: Moves the battle action bar attach verdict mapping into pure BattleActionBarChecks helpers with tests; behaviour unchanged.
  - port notes: Ride with b4ad748f2 and 30a938689; apply the mission-view hunk to the 1.4.x Main path.
  - prereqs: 30a938689
  - conflicts: Client/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs, Main/Features/CompanionTactics/BattleActionBar/BattleActionBarChecks.cs, TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarChecksTests.cs, TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs
  - issues: 1072
- `852678f86` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: feat(recruitment): v2.0.36 - a daily log check for AI castle recruits
  - why: Adds a daily [Check] line for the AI-castle-recruit toggle (#918), session counters and three engine mirror tests, diagnostics for CastleRecruitment, which exists on both lines.
  - port notes: Builds on the 1.5.x castle recruitment rework: 0d3f2bc88 (#918 entry-recruit fix) and f83a63e57 (volunteer gating); 1.4.x still has Patch42_FillSettlements_Transpiler and Patch42_HourlyTickParty_Postfix and no CastleAiRecruitDispatcher, so port only after those decisions. The three mirror-test facts hold in the v1.4.8 dump: RecruitmentCampaignBehavior.cs:504-555 (RecruitVolunteersFromNotable calls GetRecruitVolunteerFromIndividual), :651-653 (that calls ApplyInternal), :606-638 (ApplyInternal raises OnTroopRecruited); GarrisonRecruitmentCampaignBehavior.cs:124-155 uses MaximumIndexGarrisonCanRecruitFromHero. Manifest and fixtures need c8d69e426.
  - prereqs: cb4eeaa3d, c8d69e426, 0d3f2bc88
  - conflicts: Main/Features/CastleRecruitment/CastleRecruitmentService.cs, Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs, Main/Features/CastleRecruitment/ICastleRecruitmentService.cs, TAOM.Tests/Features/CastleRecruitment/CastleRecruitmentServiceTests.cs, TAOM.Tests/Features/TroopProgression/VolunteerProductionWiringTests.cs, TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs, tools/tests/test_ingame_verdict.py
  - issues: 1072
- `da5c87c23` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(recruitment): v2.0.36 - castle check wording matches the engine
  - why: Review follow-up correcting the #918 check wording and replacing CastleRecruitKind with a bool; no behaviour change.
  - port notes: Ride with 852678f86. The manifest's engine claim (the engine reruns the saved day only for a save made in its first half) was written against v1.5.6; re-check it on v1.4.8 before shipping the text.
  - prereqs: 852678f86
  - conflicts: Main/Features/CastleRecruitment/CastleRecruitmentService.cs, Main/Features/CastleRecruitment/Hooks/CastleRecruitObserver.cs, Main/Features/CastleRecruitment/ICastleRecruitmentService.cs, TAOM.Tests/Features/CastleRecruitment/CastleRecruitmentServiceTests.cs, tools/ingame_checks/918.toml
  - issues: 1072, 918
- `0ab3dc35a` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: feat(career): v2.0.36 - career screen open and init check lines
  - why: Career button and career screen write open and init [Check] lines with a #971 manifest; the career screen and its switch mode exist on 1.4.x.
  - port notes: ViewModel_ExecuteCommand_CareerScreen_Patch.cs on 1.4.x lacks the #971 fix 0761c01b7 (4-line drift), so port after it. 1.4.x GauntletCareerScreen already has _isSwitchMode, so the init check's mode=normal|switch fits unchanged. Needs cb4eeaa3d and c8d69e426.
  - prereqs: cb4eeaa3d, c8d69e426, 0761c01b7
  - conflicts: TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs, tools/tests/test_ingame_verdict.py
  - issues: 1072
- `4e0c2a3b0` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(career): v2.0.36 - pin the career open check's type test and fail
  - why: Review follow-up adding tests that pin the career open check's top-screen type test and its fail line, plus doc and manifest wording.
  - port notes: Ride with 0ab3dc35a.
  - prereqs: 0ab3dc35a
  - conflicts: Main/Features/CareerSystem/Hooks/Patch27_CareerSystem/ViewModel_ExecuteCommand_CareerScreen_Patch.cs, TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs, tools/ingame_checks/971.toml, tools/tests/test_ingame_verdict.py
  - issues: 1072, 971
- `2d6934006` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(career): v2.0.36 - correct career check wording, pin init detail
  - why: Review follow-up correcting the career check notes and pinning the init detail in a test; no runtime change.
  - port notes: Ride with 0ab3dc35a. The doc sentence cites v1.5.6; v1.4.8 behaves the same (GameStateScreenManager.CreateScreen calls Activator.CreateInstance and registers a null listener only when no screen type exists: C:\Decompiled_Bannerlord\_modules_build\Native__TaleWorlds.MountAndBlade.View.cs, class at line 12253), so only the version label changes.
  - prereqs: 4e0c2a3b0
  - conflicts: Main/Features/CareerSystem/UI/CareerScreenChecks.cs, TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs, tools/ingame_checks/971.toml
  - issues: 1072
- `e4dbdf9c4` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: feat(hero-race): v2.0.36 - race persistence writes check lines
  - why: Race persistence writes capture and restore [Check] lines with a read-back comparison (#1011), and the hero roster adapter reports alive status; diagnostics for HeroRace, which exists on both lines.
  - port notes: These lines verify the #1011 fix f981629cd (dead heroes keep their race across a load), which is not on 1.4.x; 1.4.x RacePersistenceService also lacks eb5cf0289. Port after those. No saved field changes. Needs cb4eeaa3d and c8d69e426.
  - prereqs: cb4eeaa3d, c8d69e426, f981629cd
  - conflicts: Main/Features/HeroRace/RacePersistenceService.cs, TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs, TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs, tools/tests/test_ingame_verdict.py
  - issues: 1011, 1072
- `40006d078` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(hero-race): v2.0.36 - a save with no race record skips its check
  - why: Follow-up: a save with no race-persistence record skips the restore check instead of failing it.
  - port notes: Ride with e4dbdf9c4. Its premise that every campaign starts with a dead hero holds on 1.4.x data: lord_E6_2 is alive="false" in Main/_Module/ModuleData/characters/heroes.xml:152-154 on origin/bannerlord-1.4.5.
  - prereqs: e4dbdf9c4
  - conflicts: Main/Features/HeroRace/RacePersistenceService.cs, TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs, tools/ingame_checks/1011.toml, tools/tests/test_ingame_verdict.py
  - issues: 1011, 1072
- `75c0c27a3` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(hero-race): v2.0.36 - dead-hero checks fail on a lost roster
  - why: Follow-up: dead=0 on a capture and dead_seen=0 on a restore now fail, because a TAOM campaign always holds a dead hero.
  - port notes: Ride with e4dbdf9c4; same lord_E6_2 premise, verified on 1.4.x data.
  - prereqs: 40006d078
  - conflicts: Main/Features/HeroRace/RacePersistenceService.cs, TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs, tools/ingame_checks/1011.toml, tools/tests/fixtures/ingame_verdict/race_legend_corrupt.txt, tools/tests/fixtures/ingame_verdict/race_load_mismatch.txt, tools/tests/fixtures/ingame_verdict/race_load_no_dead.txt, tools/tests/fixtures/ingame_verdict/race_load_pass.txt, tools/tests/test_ingame_verdict.py
  - issues: 1011, 1072

### group `issue-1074-ingame-verdict-tool`  P2  track=dev-tools  commits=3  sim: clean=0 conflict=3
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/ingame_verdict.py, tools/tests/test_ingame_verdict.py
- `c5705474e` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.36 - /verify and /release report in-game verdicts
  - why: The verdict reader reports each session's [Session] summary and swallowed sites, /verify gains an information-only Step 6 and /release a verdict step, with a new ingame-verdicts doc and two csharp-architecture rule rows.
  - port notes: Take tools/ingame_verdict.py changes, tests and fixtures, docs/features/ingame-verdicts.md, the two csharp-architecture rows and a feature-map row. /verify on 1.4.x ends at Step 5 (CHANGELOG check), so Step 6 appends cleanly. /release on 1.4.x has a different flow (Phase 4 release note, Phase 5 hand-edited CHANGELOG, no testers' checklist) and tools/ingame_ledger.py is absent there: rewrite that hunk instead of applying it.
  - prereqs: c8d69e426, cb4eeaa3d
  - conflicts: tools/ingame_verdict.py, tools/tests/test_ingame_verdict.py
  - issues: 1074
- `53e13cfe3` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.36 - verdict summaries skip [Swallowed] lines
  - why: Reader fix so a [Swallowed] warning is never taken for a [Session] summary, plus --sessions 10 in the /verify and /release steps.
  - port notes: Ride with c5705474e; same /release adaptation. Drop the #981 re-run note (ServerSettings and dedicated-server staging are 1.5.x-only).
  - prereqs: c5705474e
  - conflicts: tools/ingame_verdict.py, tools/tests/test_ingame_verdict.py
  - issues: 1011, 1074, 981
- `95647d13b` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.36 - tighten the in-game verdict reader and its docs
  - why: Verdict reader robustness (a non-ASCII digit no longer raises, the no-summary reason prints once) and doc corrections.
  - port notes: Ride with c5705474e and 53e13cfe3; drop the #981 dedicated-server folder text (1.5.x-only).
  - prereqs: 53e13cfe3
  - conflicts: tools/ingame_verdict.py, tools/tests/test_ingame_verdict.py
  - issues: 1074, 981

### group `issue-1073-silent-catch-ratchet`  P2  track=diagnostics  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs, Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs, Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs, Main/Features/Enlistment/Hooks/EncounterGameMenuOrderAttackConditionPatch.cs, Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs, TAOM.Tests/Infrastructure/SilentCatchBaseline.txt, TAOM.Tests/Infrastructure/SilentCatchRatchetTests.cs, TAOM.Tests/Infrastructure/SilentCatchScan.cs
- `cd4fb3719` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: feat(logging): v2.0.36 - report swallowed exceptions, add a ratchet
  - why: Silent catches in shared hooks (enlistment gate, character creation, hero race spawning, order of battle, army targeting, the creature behaviour tree wrapper) now report through Outcomes.Swallowed, and SilentCatchRatchetTests pins a per-file silent-catch baseline.
  - port notes: Re-derive on the 1.4.x tree. Of the 21 Main files, 7 are identical on 1.4.x before the change (for example BehaviorTreeBannerlordWrapper.cs and LordConversationsConditionPatches.cs), 12 differ and need hand application, and 2 are 1.5.x-only (Enlistment EncounterGameMenuOrderAttackConditionPatch.cs and MapEventHelperCanMainPartyLeaveBattlePatch.cs: drop). Regenerate TAOM.Tests/Infrastructure/SilentCatchBaseline.txt on 1.4.x, since 155 files and 218 catches is the 1.5.x count. The tests call RepoPaths (TAOM.Tests/Infrastructure/RepoPaths.cs, added on 1.5.x by 35007dbb8, absent on 1.4.x): port it or use a local helper. Needs OutcomeLog (cb4eeaa3d).
  - prereqs: cb4eeaa3d
  - conflicts: Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs, Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs, Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs, Main/Features/Enlistment/Hooks/EncounterGameMenuOrderAttackConditionPatch.cs, Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs
  - issues: 1073
- `00fddd3c5` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(logging): v2.0.36 - tighten the silent catch scan, report one more
  - why: Tightens the silent-catch scan in four ways and makes the order-of-battle finalize hook report its swallowed tracker clear.
  - port notes: Ride with cd4fb3719. 1.4.x Patch35_OrderOfBattleVM_Finalize.cs differs from the 1.5.x parent, so apply that hunk by hand, then regenerate the 1.4.x baseline.
  - prereqs: cd4fb3719
  - conflicts: TAOM.Tests/Infrastructure/SilentCatchBaseline.txt, TAOM.Tests/Infrastructure/SilentCatchRatchetTests.cs, TAOM.Tests/Infrastructure/SilentCatchScan.cs
  - issues: 1073

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 31, batch 32
