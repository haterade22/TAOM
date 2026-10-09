ADVERSARIAL REVIEW: War of the Ring session reset, bug #764 (milestone M0 of the War Chronicle work)

REVIEW ONLY. Do not modify, create or delete any file anywhere, including docs/reviews/raw/. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-wotr-session-reset-2026-10-08.md, so never write that path yourself.

Feature: WarOfTheRingService (Main/Features/Diplomacy/) is a process-lifetime singleton (Reuse.Singleton, IoC.Configure runs once in OnSubModuleLoad) that holds the War of the Ring phase (Peace, IsengardWar, FullWar, WarEnded) and the outcome. A second campaign started in the same process without restarting the game inherited the previous campaign's phase and outcome. The fix, uncommitted on branch bannerlord-1.5.x over 9bb78ae5b, Bannerlord v1.5.4: the WarOfTheRingBehavior constructor calls ResetForNewSession(); SubModule builds that behavior with new in every campaign's OnGameStart, before LoadBehaviorData and before every campaign event, and a load's SyncData restores the saved phase and outcome over the reset. A first cut reset from OnSessionLaunched behind a _syncedThisSession latch; an eight-lens Claude deep review found it too late (campaign-event listeners run newest-first, so WarOfTheRingMomentumBehavior, added later, read the stale phase in its own OnSessionLaunched, and vanilla cached at-war lists in OnNewGameCreated). You are reviewing the FIXED working tree. The fix record, row by row, is docs/reviews/rca-wotr-session-reset-2026-10-08.md.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST
1. AGENTS.md, then .ai/review-reference.md (TAOM's review rules, severity and evidence format).
2. docs/features/war-of-the-ring.md: the "Phase state" section, the Tests list and How to Test In-Game step 6.
3. docs/reviews/rca-wotr-session-reset-2026-10-08.md (every finding and its fix; do not re-report a row as found unless the fix is wrong or incomplete).
4. docs/reviews/lessons/state-lifecycle-save.md, the last lesson, and docs/reviews/lessons/harmony-il.md, "Campaign-event listener dispatch is LIFO".

DESIGN DECISIONS (do not report them as defects, but do report a defect in how they are implemented)
-- The reset sits in the WarOfTheRingBehavior constructor, which runs in SubModule.OnGameStart -> RegisterDiplomacyAndConflict for every campaign start, new or loaded. There is no latch.
-- A save with no WarOfTheRingBehavior record stays at the reset Peace and None, and OnSessionLaunched re-derives the phase from elapsed days (the behavior before #129), with every DeclareWar guarded by AreAtWar.
-- The momentum store's own reset gap (WarOfTheRingMomentumBehavior is a container singleton that resets its store only on OnNewGameCreated) is a known follow-up outside this diff. Report it only if this change makes it worse.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code you read; cite file:line on both the TAOM and the engine side)
S1. The constructor runs on every OnGameStart, a load included. Hypothesis: something reads IWarOfTheRingService between OnGameStart (Campaign.cs around :1410) and LoadBehaviorData (around :1448) on a load, and acts on the reset Peace in a way that differs from the first load in a fresh process (where the singleton also starts at Peace). Candidates: TaomDiplomacyModel and TaomKingdomDecisionPermissionModel, the MakePeaceAction.ApplyInternal prefix (PeaceActionHook), anything that runs during save deserialization or AfterLoad.
S2. OnGameStart reach. Hypothesis: SubModule.OnGameStart and RegisterDiplomacyAndConflict run in a context where a live campaign's phase must NOT be reset: a second Game created while a campaign is live (a mission or a Custom Battle from inside a campaign), a co-op reconnect, or any path that constructs WarOfTheRingBehavior again mid-campaign. Enumerate every caller of MBGameManager.OnGameStart and MBSubModuleBase.OnGameStart in the installed v1.5.4 assemblies and in BannerlordCoop's GameInterface (E:\repos\BannerlordCoop\source).
S3. Dispatch order. The doc says listeners run newest-first, so the momentum behavior (SubModule.cs, added after WarOfTheRingBehavior) ran its OnSessionLaunched first, and vanilla CampaignFactionManagerBehaviour.OnNewGameCreated cached each kingdom's at-war list through TaomDiplomacyModel.IsAtConstantWar. Confirm both against MbEvent`1 and CampaignBehaviorManager, and look for any reader of the service that runs even earlier on a new game (a static cache, a model used in character creation).
S4. The wiring pin. TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs asserts that Main/SubModule.cs (comments stripped) contains "new WarOfTheRingBehavior(" and no Resolve of it, and that DiplomacyIoC.cs never names it. Hypothesis: the invariant can break while the test stays green (a second construction path, a feature module, a Resolve with another qualifier, a factory delegate, a string match inside an unrelated identifier).
S5. The tests. WarOfTheRingBehaviorSessionResetTests uses a private FakeDataStore and a real WarOfTheRingService for a save-then-load round trip. Hypothesis: the fake differs from v1.5.4 BehaviorSaveData.SyncData semantics in a way that matters, or a test would still pass if the constructor reset were removed or moved back to OnSessionLaunched.
S6. Outside the diff, evidence only (for a follow-up issue). WarOfTheRingService.TransitionToPhase sets CurrentPhase = FullWar before DeclareHostileTierWars. AllianceAdapter.AreAtWar -> Kingdom.IsAtWarWith -> FactionManager.IsAtWarAgainstFaction -> DiplomacyModel.IsAtConstantWar may already answer true for every Hostile pair (TaomDiplomacyModel returns true while FullWar and blockPeaceBetweenHostileTiers), so `if (!AreAtWar(a, b))` would skip every declaration. CONFIRM or DISPUTE, and say what the game then does for those pairs: stance, the FactionsAtWarWith cache, notifications, AI target selection, peace offers.

FILES
Changed C#: Main/Features/Diplomacy/IWarOfTheRingService.cs, Main/Features/Diplomacy/WarOfTheRingService.cs, Main/Features/Diplomacy/WarOfTheRingBehavior.cs, Main/SubModule.cs (one comment at the WarOfTheRingBehavior construction in RegisterDiplomacyAndConflict).
Tests: TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs (three ResetForNewSession tests), TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs (new), TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs (new).
Docs: docs/features/war-of-the-ring.md, docs/features/diplomacy.md, docs/reviews/lessons/state-lifecycle-save.md, docs/reviews/rca-wotr-session-reset-2026-10-08.md (new).
Readers of the service outside the diff: Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs (OnSessionLaunched), MomentumEnrollmentService.cs, MomentumVictoryService.cs, Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs, Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs, Main/Features/Diplomacy/Hooks/PeaceActionHook.cs, Main/Adapters/AllianceAdapter.cs.
Run `git diff -- <file>` on each changed file; the two new test files and the RCA are untracked.

NOT IN SCOPE (do not review): tools/analyze_war_ledger.py, tools/tests/test_analyze_war_ledger.py and tools/README.md (a later milestone); the live TAOM_Map settlements.xml and its distance cache (another session's work). One test fails before and after this change and is unrelated: LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey (32 taom_race_ability_* keys untranslated).

REQUIRED SECTIONS

1. VANILLA CODE. Decompile from the installed v1.5.4 DLLs (the filesystem and ilspy MCP servers; for browsing, C:\Users\mikew\.taom-src\v1.5.4 or E:\Decompiled_Bannerlord\_categories_v1.5.4) and paste as code blocks: Campaign.OnInitialize and the loading steps around it (the order of OnGameStart, LoadBehaviorData, RegisterEvents, OnNewGameCreated, OnGameLoaded, OnSessionStart); CampaignBehaviorDataStore.LoadBehaviorData and SaveBehaviorData; BehaviorSaveData.SyncData; MbEvent`1.AddNonSerializedListener and Invoke; CampaignBehaviorManager.RegisterEvents; CampaignGameStarter.AddBehavior; CampaignFactionManagerBehaviour.OnNewGameCreated and OnGameLoaded; Kingdom.UpdateFactionsAtWarWith and IsAtWarWith; FactionManager.IsAtWarAgainstFaction; MBGameManager.OnGameStart; the SandBoxGameManager paths for a new game and a load.

2. DEEP ANALYSIS, concrete scenarios. For each, state the exact sequence of engine and TAOM calls and the end state (CurrentPhase, Outcome, momentum HasWarStarted, the FactionsAtWarWith cache for a Hostile pair that is at peace):
a. Fresh process -> new campaign A -> day 50 (FullWar) -> exit to the main menu -> new campaign B -> day 1 -> day 31.
b. Campaign A at WarEnded (momentum victory enabled) -> main menu -> load a save of campaign C written at day 40 (IsengardWar) -> OnSessionLaunched.
c. Load a save that has no WarOfTheRingBehavior record (written before the behavior existed).
d. A BannerlordCoop first-time joiner: a local new campaign, then the host's save loads. What the client's service holds at each step, and whether the host's state can change.
e. An in-campaign load (load a save while a campaign is running).

3. CONFIG CROSS-REFERENCE. Main/_Module/ModuleData/diplomacy/war_of_the_ring.json (phase days, phase wars, testMode) against the cheatsheet, and the MCM phase-day settings in Main/Features/TaomSettings.cs. Confirm that nothing in config interacts with the reset, and that the test config in the new tests matches what they claim (the shipped days 30 and 44).

4. FINDINGS OR OBSERVATIONS. For each finding: severity, TAOM file:line, engine file:line, the code you rely on quoted, a reproduction or proving code, and a fix. Mark anything you could not prove UNVERIFIED.

QUALITY GATES
-- Every finding cites a TAOM file:line and, when it rests on engine behaviour, an engine file:line, with the code quoted.
-- CONFIRM or DISPUTE every known suspect; never skip one.
-- Do not report a design decision above as a defect; do report a defect in its implementation.
-- Do not re-report an RCA row unless its fix is wrong or incomplete.
-- Say "no finding" plainly when a suspect is clean; do not pad.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write docs/reviews/raw/codex-adversarial-wotr-session-reset-2026-10-08.md yourself; the dispatcher redirects stdout there.
