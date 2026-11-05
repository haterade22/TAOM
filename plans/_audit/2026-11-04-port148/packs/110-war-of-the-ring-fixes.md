# Input pack: WP 110 war-of-the-ring-fixes

Title (draft): Fix the War of the Ring: real Full War declarations, per-campaign reset, settings migration, honest victory
Wave: 1   Lane: E   Draft priority: P1   Groups: 6   Port commits: 8
Depends on (WP ids): 103, 104

## Orchestrator notes for this WP (binding)
- V06: chain-apply in order 2719e59e8, 9ab3952a7, d1b9fd91a, the momentum halves of 16a34027d and ec2a3026f, the provider hunk of 429713746; adapt ec2a3026f without IPlayerControlQuery (co-op stays out, D7).
- New hardening 1.5.x lacks: empty-roster tie must not resolve as a Free win (WOTR-03), non-finite strength (WOTR-08), state surviving into a load with no momentum record (#773, WOTR-05), and the phase clock (WOTR-10) if cheap.
- WOTR-04: keep realm-wide participation credit (deliberate, #375); fix the misleading MCM hint text instead, and show the absolute lead the decision uses (the documented runaway-momentum case).
- Log line a player can send: '[Momentum] War of the Ring decided: FreeVictory (momentum X, threshold Y)'; make sure it stays.
- Optional lead (batch notes): #774 raid counted twice; verify on 1.4.x before porting.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V06-war-of-the-ring.md

## Deep-dive findings and non-port items this WP owns
- WOTR-01 [P1/high] Full War never declares its Hostile-pair wars, so no new battles happen and the meter stays centred | port: d1b9fd91a,cf7486cba  (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- WOTR-02 [P1/high] WarOfTheRingService is a process singleton: a second campaign inherits Full War (meter from day 0) or WarEnded (no war) | port: 2719e59e8,9ab3952a7,cf7486cba  (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- WOTR-03 [P2/medium] A Free win at zero momentum is only reachable through the elimination branch, and an all-empty roster resolves as Free | port:   (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- WOTR-04 [P2/high] Opt-in victory is decided by an absolute lead the ratio meter hides, and the 'player participation' gate counts the player's realm's AI lords | port:   (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- WOTR-05 [P3/high] Momentum state survives into a loaded campaign whose save has no momentum record (#773) | port:   (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- WOTR-06 [P2/high] Logged phase days are the JSON pair; MCM files saved 2026-05-22 to 07-30 run Phase 1 at day 2 and Full War at day 14 | port: 429713746  (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- WOTR-07 [P3/high] A relief-force win outside the walls (SiegeOutside) scores a full siege capture in momentum | port: ec2a3026f  (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- WOTR-08 [P3/medium] A non-finite realm strength casts to int.MinValue and can decide the war for either side with the meter at centre | port:   (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- WOTR-09 [P3/high] A corrupt momentum chunk count throws out of SyncData during load | port: 16a34027d  (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- WOTR-10 [P3/low] Phase clock trusts the engine's recomputed CampaignStartTime; a mid-campaign time-model change moves every phase day | port:   (report: plans/_audit/2026-11-04-port148/deepdive/wotr.md)
- SAVE-TIME-06 [P3/high] Momentum SyncData chunk loop trusts the saved count (robustness) | port: 16a34027d  (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)

## Port groups owned by this WP (from the commit classification)

### group `issue-764-wotr-session-reset`  P1  track=shared-feature-fix  commits=3  sim: clean=1 conflict=2
symptoms: wotr-early-end
- `2719e59e8` 2026-10-08 PORT P1 pick=conflict :: fix(wotr): v2.0.34 - reset the War of the Ring per campaign
  - why: Fixes the process-lifetime WarOfTheRingService carrying the previous campaign's phase and outcome into a new campaign; origin/bannerlord-1.4.5 has the same bug (WarOfTheRingService has no reset, and WarOfTheRingBehavior's OnNewGameCreated listener only zeroes the fresh behavior's own two ints).
  - port notes: Production code picks with no code conflicts (bookkeeping only). The constructor reset relies on SubModule building WarOfTheRingBehavior with new per campaign, which holds on 1.4.x (origin/bannerlord-1.4.5:Main/SubModule.cs:981). The new WarOfTheRingWiringTests call RepoPaths.ReadSource/StripComments/RepoPath, absent on 1.4.x: copy TAOM.Tests/Infrastructure/RepoPaths.cs as of a0db0558b (the file was created in 35007dbb8 beside an unrelated field-commission fix, so do not cherry-pick that) or inline a helper. Take the RCA and the state-lifecycle-save and testing-qa lessons. WotR link is plausible, not proven: a stale FullWar phase on day 0 of a second campaign lets the container-singleton momentum behavior (SubModule.cs:985) enroll and start the momentum war at once; the momentum-store half (#773) is a later 1.5.x commit.
  - prereqs: a0db0558b
  - issues: 764
- `9ab3952a7` 2026-10-08 PORT P3 pick=clean :: docs(rules): v2.0.34 - reset per-campaign state in the constructor
  - why: Amends the session-reset rule in .claude/rules/csharp-architecture.md to reset per-campaign singleton state in the constructor of a behavior built per campaign, a rule that applies unchanged to the 1.4.x codebase.
  - port notes: Rule text only. It cites WarOfTheRingWiringTests and rca-wotr-session-reset-2026-10-08.md, so land it with or after 2719e59e8; confirm the 1.4.x rule still carries the old OnSessionLaunched wording before applying.
  - prereqs: 2719e59e8
  - issues: 764
- `cf7486cba` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(wotr): v2.0.34 - #764 and #772 in diplomacy and momentum docs
  - why: Documents the #764 (2719e59e8) and #772 (d1b9fd91a) War of the Ring fixes, both absent on 1.4.x, plus Arthedain content and 1.5.x counts; only the four-WarPhase-values line is already true on 1.4.x.
  - port notes: Take now: diplomacy.md's four WarPhase values in the overview and file table (WarEnded exists in 1.4.x Main/Features/Diplomacy/Models/WarPhase.cs). Take with the ports of 2719e59e8 and d1b9fd91a: the #764 and #772 lines in diplomacy.md and war-of-the-ring.md (stored-stance guard, ReconcileDeclaredWars, session-reset and wiring test rows, the idempotent-declaration step, the GitHub Issue section). Leave the Gundabad/Arthedain pair (content), WarOfTheRingPhaseDayTests (#765, War Chronicle), the 73-key string count (includes War Chronicle keys) and the 174-test count (recount on 1.4.x). The momentum doc's 32,763 correction is the same fact as ae1a6b553's save-system bullet.
  - prereqs: 2719e59e8, d1b9fd91a
  - issues: 764, 765, 772, 792

### group `issue-772-wotr-fullwar-declarations`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
symptoms: wotr-early-end
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/Diplomacy/IWarOfTheRingService.cs, Main/Features/Diplomacy/WarOfTheRingBehavior.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs
- `d1b9fd91a` 2026-10-08 PORT P1 pick=conflict :: fix(wotr): v2.0.34 - declare the Full War wars for real
  - why: Full War never declared a Hostile-pair war because each guard asked AreAtWar, which TaomDiplomacyModel.IsAtConstantWar answers true once the phase is FullWar; 1.4.x has the same order (TransitionToPhase sets CurrentPhase before DeclareHostileTierWars, shipped config has autoWar and blockPeace on) and v1.4.8 FactionManager.IsAtWarAgainstFaction consults IsAtConstantWar first (FactionManager.cs:144-153).
  - port notes: Code conflicts in IWarOfTheRingService.cs, WarOfTheRingBehavior.cs and WarOfTheRingBehaviorSessionResetTests.cs come from 2719e59e8, so port that first. HasDeclaredWar uses Kingdom.GetStanceWith, present in v1.4.8 (Kingdom.cs:662). Brings a host-only load reconcile that declares missing Full War wars once, and war_of_the_ring.json pair validation; CoopVetoClassificationTests exists on 1.4.x and needs its row. WotR tag is inference: with no real declarations a Full War adds no new wars, so AI fights fewer cross-side battles and the meter moves less.
  - prereqs: 2719e59e8
  - conflicts: Main/Features/Diplomacy/IWarOfTheRingService.cs, Main/Features/Diplomacy/WarOfTheRingBehavior.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs
  - issues: 772

### group `wotr-syncdata-chunker-guard`  P2  track=crash-robustness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/WarChronicle/WarChronicleBehavior.cs, Main/Features/WarChronicle/WarChronicleStateService.cs, TAOM.Tests/Features/WarChronicle/Events/ChroniclePersistenceTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleStateServiceTests.cs
- `16a34027d` 2026-10-14 PORT-PARTIAL P2 pick=conflict :: refactor(core): v2.0.34 - one guarded SyncData chunk loop for two saves
  - why: 1.4.x WarOfTheRingMomentumBehavior passes the saved chunk count straight to new List<string>(count) (line 114), so a corrupt count throws on load or exhausts memory; SyncDataChunker.SyncChunks ignores a count below 0 or above 4,096 with a warning and loads fresh momentum.
  - port notes: Take Main/Core/Persistence/SyncDataChunker.cs, the WarOfTheRingMomentumBehavior and MomentumCheats changes, delete MomentumSyncChunker.cs and MomentumSyncChunkerTests.cs, add SyncDataChunkerTests, SyncDataChunkerSyncTests and WarOfTheRingMomentumBehaviorSyncDataTests, the MomentumCheatsFormatTests edits, and the war-of-the-ring-momentum.md and gamemodels-services lesson hunks. The momentum code files on origin/bannerlord-1.4.5 are byte-identical to this commit's parent (git diff --shortstat empty), so they apply cleanly. Leave all WarChronicle files. Re-cite the 32,763-byte limit to v1.4.8 SaveContext.SaveStringTo (SaveContext.cs:308-321, verified: writes (short)(4 + UTF-8 bytes)). Saved keys unchanged, so saves stay compatible; also drops the retained _chunks field.
  - conflicts: Main/Features/WarChronicle/WarChronicleBehavior.cs, Main/Features/WarChronicle/WarChronicleStateService.cs, TAOM.Tests/Features/WarChronicle/Events/ChroniclePersistenceTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleStateServiceTests.cs
  - issues: 792

### group `issue-818-wotr-war-rule`  P3  track=tests-ci  commits=1  sim: clean=0 conflict=1
- `3fe86e22b` 2026-10-18 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - RCA, lessons and generator region gate
  - why: The RCA covers the out-of-scope war rule, but the commit adds three engine and data lessons and a generator-region drift gate pattern.
  - port notes: Take the three lessons (a veto must also gate where the engine charges for or offers the war; a negative relation value declares war at load whatever isAtWar says; never make peace from OnSessionLaunched on a load) after checking each on v1.4.8. tools/tests/test_generator_regions_in_sync.py has no 1.4.x owner as written: generate_arthedain_troops.py is absent there and the 1.4.x generate_new_faction_kingdoms.py has no --check; take the pattern only if a 1.4.x generator owns a ModuleData marker region with a passing --check. Leave the RCA.
  - issues: 818
skipped in this group: `2410aec51` SKIP-FEATURE feat(diplomacy): v2.0.34 - no wars before the War of the Ring

### group `issue-1015-siege-capture-credit`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Adapters/WarEventSnapshotAdapter.cs
- `ec2a3026f` 2026-10-26 PORT-PARTIAL P1 pick=conflict :: fix(career): v2.0.35 - siege and capture credit follow the player
  - why: Career quest, LotrIssues and War of the Ring credit a siege or capture only when the player's own party is the captor, and momentum counts a SiegeOutside defender win as a capture; the 1.4.x code matches the pre-fix code, and v1.4.8 KingdomManager.SiegeCompleted (KingdomManager.cs:240) transfers only on Siege, SallyOut and BlockadeSallyOutBattle.
  - port notes: Take CareerQuest (CreditsSettlementCapture, PlayerFoughtOnCapturingSide, IsPlayersCapture), CombatLotrIssue, the momentum CapturesSettlement gate and the two new test files. WarEventSnapshotAdapter.PlayerFought calls HasPlayerParty, which uses IPlayerControlQuery from 7f3636b2e (absent on 1.4.x): port that co-op change first, or test the map event side for PartyBase.MainParty instead. The EnlistmentBattleBehavior hunk only edits a comment (#557 FlushArmyLeaveAfterBattle exists on 1.4.x).
  - conflicts: Main/Adapters/WarEventSnapshotAdapter.cs
  - issues: 1015

### group `issue-1017-mcm-renamed-defaults`  P1  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
symptoms: wotr-early-end
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs, Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs, Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs, Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs, Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs, Main/Features/TaomSettings.cs, TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs, TAOM.Tests/Features/CompanionTactics/CompanionTacticsSettingsProviderTests.cs, TAOM.Tests/Features/ServerSettings/ServerSettingsModuleTests.cs, TAOM.Tests/Features/ServerSettings/ServerSettingsReaderTests.cs, TAOM.Tests/Features/SmartCavalryAI/SmartCavalryAISettingsProviderTests.cs, TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs
- `429713746` 2026-10-26 PORT-PARTIAL P1 pick=conflict :: fix(settings): v2.0.35 - changed defaults reach every player's settings
  - why: Thirteen of the fourteen trapped settings keep the same old names and post-flip defaults on origin/bannerlord-1.4.5 (Phase1TriggerDay=30 and Phase2TriggerDay=44 at TaomSettings.cs:202/207, Smart Cavalry, Formation Presets, Field Commission, AI Party Size, and the bandit pair flipped by 2652fe2f1), so a TAOM.json saved earlier still loads the old values, for example a day-2 War of the Ring start.
  - port notes: Take the renames except ChargeAutoKnockdownRatio (1.4.x still ships 8; the 8-to-6 flip is 1.5.x-only balance), their settings-provider and cheat call sites, McmRenamedDefaultsTests minus that row, tools/check_mcm_default_flips.py with its allowlist and tests (run against the 1.4.x tags v2.0.0 to v2.0.28 and recheck the allowlist), and the /release and .ai/verification wiring. Skip the ServerSettings test edits (1.5.x-only). The link to the early-end report is unproven: a day-2 start alone does not end the war (see batch notes).
  - conflicts: Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs, Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs, Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs, Main/Features/FieldCommission/FieldCommissionSettingsProvider.cs, Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs, Main/Features/TaomSettings.cs, TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs, TAOM.Tests/Features/CompanionTactics/CompanionTacticsSettingsProviderTests.cs, TAOM.Tests/Features/ServerSettings/ServerSettingsModuleTests.cs, TAOM.Tests/Features/ServerSettings/ServerSettingsReaderTests.cs, TAOM.Tests/Features/SmartCavalryAI/SmartCavalryAISettingsProviderTests.cs, TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs
  - issues: 1017

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 15, batch 16, batch 17, batch 19, batch 28
