# Input pack: WP 114 save-growth-investigation

Title (draft): Name what an in-game reload adds to every later save, with probes and a repro matrix, before any fix
Wave: 1   Lane: E   Draft priority: P1   Groups: 3   Port commits: 6
Depends on (WP ids): 104

## Orchestrator notes for this WP (binding)
- V01: each in-game load adds about 71,500 containers (85,000 to 90,000 objects) to every later save in that process; a first load in a fresh process adds nothing. 'Permanent' and 'per load' are NOT proven; the stale-XML-object candidate class is refuted for v1.4.8. Live hypotheses: SaveShield swallowing a load-callback fault on a second load; in-game load vs main-menu load.
- Build the probes (memory tokens, loadsThisProcess, SaveContext object/container counts, per-behaviour SyncData sizes, an opt-in type census at save) and write the repro matrix (fresh-process load then save; in-game reload then save; main-menu reload then save; fresh-process load of a bloated save then save) for the maintainer to run in game.
- STOP: no SAVE-TIME-01 fix in this WP; a follow-up plan is written once the census names the carrier.
- issue-628-race-fertility: only the #628 commits (83bdad853, 925db38d1), not #626.
- Docs: correct the daily-tick-on-load docs for v1.4.8 (573dc155f, a66b5754a, e8504a62b, ae1a6b553).
- Server GC: do NOT recommend (V01: three runtime hosts, unknown on players' machines).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V01-save-growth.md
- plans/_audit/2026-11-04-port148/verify/V07-memory.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Deep-dive findings and non-port items this WP owns
- SAVE-TIME-01 [P0/high] Every in-session reload adds ~88k objects / ~71k containers to the next save, permanently | port:   (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)
- SAVE-TIME-02 [P1/high] Engine mechanics: why the Containers phase dominates and scales worse than volume | port:   (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)
- SAVE-TIME-03 [P1/medium] Within a session, memory pressure (GC/paging) drives the variance; volume sets the floor | port:   (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)
- SAVE-TIME-04 [P2/medium] Large baseline: TAOM's world is 2-3x vanilla's and some vanilla state never prunes | port:   (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)
- SAVE-TIME-05 [P3/high] Inventory of TAOM-persisted containers: all small and bounded | port:   (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)
- SAVE-TIME-07 [P1/high] The 1.4.x [SaveLoad] probe times saves but cannot say what grew; a cheap probe can | port: cb4eeaa3d  (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)
- SAVE-TIME-08 [P3/high] A load re-runs the save's daily tick on v1.4.8 (docs port) | port: ae1a6b553,e8504a62b  (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)
- SAVE-TIME-09 [P3/medium] TAOM ships ButterLib with its Save System subsystem on; it replaces TypeExtensions.IsContainer on every save | port: 1325c36ea  (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)
- LOAD-TIME-02 [P1/high] Every load phase is linear in the save's object count, and E2's save grew 80 percent in one session (root cause owned by the save-time lens) | port:   (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)
- LOAD-TIME-11 [P3/high] A load re-times the daily tick from the save's elapsed fraction of the day, so a save made early in a day repeats that day's tick soon after loading | port: ae1a6b553  (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)
- LOAD-TIME-03 [P2/medium] TAOM's always-on Patch61 finalizers bind __originalMethod on about 8.5 M calls per load, mostly on the parallel load workers; PatchShield doubles it from the second load | port: 5ee49d05f,2a3f71b12  (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)
- MEMORY-05 [P2/medium] A second game in one process keeps much of the first: +1.8 GB, and the managed heap often stays at 200 to 450 MB at the menu | port: e99d705a3,a67792c40,4263535a1  (report: plans/_audit/2026-11-04-port148/deepdive/memory.md)

## Port groups owned by this WP (from the commit classification)

### group `issue-628-race-fertility`  P1  track=shared-feature-fix  commits=2  sim: clean=0 conflict=2
symptoms: save-time, memory
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/Elephant/ElephantConfig.cs, Main/Features/Elephant/ElephantMissionBehavior.cs, Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml, TAOM.Tests/Features/CharacterCreation/StarterKitCoverageTests.cs, TAOM.Tests/Features/RaceAge/ShippedFertilityConfigTests.cs, tools/generate_starter_kit.py, tools/sync_lord_inline_skills.py, tools/tests/test_generate_starter_kit.py, tools/tests/test_skill_template_mismatch.py, tools/tests/test_sync_lord_inline_skills.py, tools/tests/test_wire_starter_kit_rosters.py, tools/validate_moduledata.py, tools/wire_starter_kit_rosters.py
- `83bdad853` 2026-09-19 PORT-PARTIAL P1 pick=conflict :: Add unit tests for HowdahSampleClock and ShippedFertilityConfig, and implement howdah platform prefab
  - why: Under a howdah-and-tests subject it carries the #628 fix: orc and goblin clans breed toward twice their population cap because the race fertility bonus multiplies after the clan brake, and the 1.4.x tip has the identical code and config.
  - port notes: Take only #628: TaomPregnancyModel.ComputeBaseChance (bonus only while aliveLords <= clanCap and never for a player marriage; penalties kept), configs/initial_child_generation.json (add goblin, mistymountainorcs, bluecraig, all present on 1.4.x), raceage/race_age_config.json (orc-kin fertilityMod 2.0-3.0 cut to 1.3-1.5, orc and berserker fertile to 45, humans to 60 instead of 195; these are balance values, so confirm with the maintainer, and ShippedFertilityConfigTests pins them), TaomPregnancyModelTests, the new ShippedFertilityConfigTests and the race-age, initial-child-generation and configs-balance doc lines. Save-time and memory link is plausible but unmeasured: more heroes per clan means more saved containers. Leave the howdah platform and diagnostics (#627), career kits (#629) and every #626 skill-template change (SKILL_TEMPLATE_MISMATCH, sync_lord_inline_skills.py, the taom_schema SKILL_TEMPLATE_SHADOWS_SKILLS removal): v1.4.8 BasicCharacterObject.cs:337-357 still discards inline skills when a template resolves, so the 1.4.x rule is correct.
  - conflicts: Main/Features/CoopInterop/CoopSettingsRelevance.cs, tools/sync_lord_inline_skills.py, tools/tests/test_sync_lord_inline_skills.py, tools/validate_moduledata.py
- `925db38d1` 2026-09-19 PORT-PARTIAL P2 pick=conflict :: feat: Implement career kit generation from troop data (#629)
  - why: Mostly career-kit tooling (#629), the v1.5.2-only skill-template parity gate (#626) and the howdah crew re-enable (#627), but it carries the #628 RCA and a correction to the new fertility test.
  - port notes: Take only docs/reviews/rca-race-fertility-2026-09-19.md, the ShippedFertilityConfigTests delta (the excluded-culture check reads every culture in cultures.json, not only cultures that list a race) and the race-age-system.md lines. Leave generate_career_kits.py and the kit data (content), rca-skill-template-parity and the validate_moduledata SKILL_TEMPLATE hunk (v1.5.2 engine semantics), and HowdahCrewSpawner/HowdahHarness (1.5.x crew feature).
  - prereqs: 83bdad853
  - conflicts: Main/Features/Elephant/ElephantConfig.cs, Main/Features/Elephant/ElephantMissionBehavior.cs, Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml, TAOM.Tests/Features/CharacterCreation/StarterKitCoverageTests.cs, TAOM.Tests/Features/RaceAge/ShippedFertilityConfigTests.cs, tools/generate_starter_kit.py, tools/tests/test_generate_starter_kit.py, tools/tests/test_skill_template_mismatch.py, tools/tests/test_wire_starter_kit_rosters.py, tools/validate_moduledata.py, tools/wire_starter_kit_rosters.py
  - issues: 626, 629

### group `engine-daily-tick-on-load`  P3  track=docs-engine  commits=1  sim: clean=0 conflict=1
- `573dc155f` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(war): v2.0.34 - War Chronicle docs and review follow-ups closed
  - why: Mostly War Chronicle docs, but it corrects docs/reviews/rca-wotr-phase-ordering-2026-07-30.md, which on 1.4.x (line 16) still claims a load re-aligns the daily tick; v1.4.8 waits the elapsed fraction of the day (Campaign.cs:1004-1010, MBCampaignEvent.cs:32), so a pre-midday save ticks its day twice.
  - port notes: Take only the rca-wotr-phase-ordering correction paragraph; replace the v1.5.4/v1.5.5 citations with v1.4.8 Campaign.cs:1004-1010 and MBCampaignEvent.cs:32, and replace the WarChronicleDailyGate sentence (absent on 1.4.x) with a note that the 1.4.x WotR daily handlers have no once-per-day guard. Pairs with the out-of-batch engine-doc commit ae1a6b553 (daily tick on load). Leave war-chronicle.md and both chronicle RCAs.
  - issues: 792

### group `issue-793-daily-tick-repeat-after-load`  P3  track=docs-engine  commits=3  sim: clean=0 conflict=3
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/WarChronicle/Events/ChronicleService.cs, TAOM.Tests/Features/WarChronicle/Events/ChronicleServiceTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleDailyGateTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleTickServiceTests.cs, tools/analyze_war_ledger.py, tools/tests/test_analyze_war_ledger.py
- `a66b5754a` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(war): v2.0.34 - the day boundary is the elapsed day, not midday
  - why: Mostly War Chronicle comment and doc wording (1.5.x-only feature), but it refines the correction to the shared rca-wotr-phase-ordering RCA, whose 1.4.x copy (line 16) still claims the daily event re-aligns on load, which v1.4.8 Campaign.cs:1002-1026 contradicts.
  - port notes: Take only the docs/reviews/rca-wotr-phase-ordering-2026-07-30.md hunk, together with the correction paragraph it refines from 573dc155f (write the final wording directly). Re-cite v1.4.8 Campaign.CreateCampaignEvents (Campaign.cs:1004-1010, called from OnDataLoadFinished at :697/:724) and MBCampaignEvent.cs:31-32; drop the WarChronicleDailyGate sentence. Leave ChronicleService, the War Chronicle tests, analyze_war_ledger.py and its README row, war-chronicle.md, the War Chronicle RCAs and the Codex prompt (all 1.5.x-only).
  - prereqs: 573dc155f
  - conflicts: Main/Features/WarChronicle/Events/ChronicleService.cs, TAOM.Tests/Features/WarChronicle/Events/ChronicleServiceTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleDailyGateTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleTickServiceTests.cs, tools/analyze_war_ledger.py, tools/tests/test_analyze_war_ledger.py
  - issues: 792, 793
- `e8504a62b` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(lessons): v2.0.34 - daily tick after load, MainParty, recurrences
  - why: Adds cross-feature lessons whose engine facts hold on v1.4.8 (daily tick repeat after load, the throwing PartyBase.MainParty getter, ChangeKingdomAction moving fiefs with no owner event, the 32,763-byte SyncData string limit), mixed with War Chronicle recurrence notes.
  - port notes: Take: the daily-tick-after-load lesson (state-lifecycle-save.md; re-cite v1.4.8 Campaign.cs:1004-1010 and MBCampaignEvent.cs:31-32; replace the WarChronicleDailyGate example with the generic 'save the last-run day' prescription; keep the note that TAOM's daily listeners are unaudited, #793). The PartyBase.MainParty lesson (adapters-taleworlds-api.md; re-cite v1.4.8 PartyBase.cs:208-218; keep only the messenger example 91f369984, since the chronicle and hunt examples are 1.5.x-only) goes with the issue-792 series. The ChangeKingdomAction lesson (campaign-mechanics.md; v1.4.8 ChangeKingdomAction.cs:65, 76, 95-98 match) without its Eye of Sauron recurrence line. The 32,767 to 32,763 correction in gamemodels-services.md (v1.4.8 SaveContext.cs:308-321 stores 4 plus the UTF-8 bytes as a short). Optionally the line-neutral 'rebuild owned effects on every refresh' lesson. Leave the War Chronicle recurrence lines and the #764 pointer rewordings (the #764 lesson is absent on 1.4.x unless 9ab3952a7 is ported). Repoint Source links to RCAs that do not exist on 1.4.x.
  - issues: 764, 765, 792, 793
- `ae1a6b553` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(engine): v2.0.34 - daily tick on load, SyncData size, MainParty
  - why: Adds engine facts to the engine docs that hold on v1.4.8 (a load can repeat the save's elapsed day; one SyncData string over 32,763 UTF-8 bytes corrupts the save), along with War Chronicle rows that do not apply.
  - port notes: Take the campaign-tick-time-and-party-ai.md gotcha and the campaignevents-and-campaignbehavior.md pointer, re-cited to v1.4.8: Campaign.cs:1002-1026 (daily :1004-1010, hourly :1012-1018, quarter-hourly :1019-1025), MBCampaignEvent.cs:31-32, called from OnDataLoadFinished (:697/:724). Replace the WarChronicleDailyGate pointer with the generic prescription. Take the save-system.md SyncData bullet (v1.4.8 SaveContext.cs:308-321), pointing at the WotR momentum behaviour's own chunking, because the shared SyncDataChunker (Main/Core/Persistence) does not exist on 1.4.x. The adapters.md MainParty sentence goes with the issue-792 series. Leave the gamemodels.md War Chronicle rows and the orientation.md trap row (that file is absent on 1.4.x). The in-game check (#793) is still owed on both lines.
  - issues: 765, 792, 793

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 3, batch 16, batch 17
