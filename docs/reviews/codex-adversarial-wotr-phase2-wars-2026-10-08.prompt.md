ADVERSARIAL REVIEW: War of the Ring Phase 2 war declarations, bug #772 (milestone M0b of the War Chronicle work)

REVIEW ONLY. Do not modify, create or delete any file anywhere, including docs/reviews/raw/. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-wotr-phase2-wars-2026-10-08.md, so never write that path yourself.

Feature: at Phase 2 (Full War, default day 44) WarOfTheRingService.TransitionToPhase declares the configured Phase 2 wars and a war between every pair of kingdoms whose diplomacy.json tier is Hostile, and TaomDiplomacyModel.IsAtConstantWar then answers true for every Hostile pair while FullWar and blockPeaceBetweenHostileTiers hold. Bug #772 (found by the previous Codex review as its S6): the declarations were guarded by IAllianceAdapter.AreAtWar, which goes through Kingdom.IsAtWarWith -> FactionManager.IsAtWarAgainstFaction -> DiplomacyModel.IsAtConstantWar, so once the phase was FullWar every guard answered "already at war" and no Hostile-pair war was ever really declared: the stored stances stayed neutral, no OnWarDeclared fired. The fix, uncommitted on branch bannerlord-1.5.x over 9ab3952a7, Bannerlord v1.5.4:
-- IAllianceAdapter.HasDeclaredWar reads the stored stance link (Kingdom.GetStanceWith(other)?.IsAtWar), and every declaration is guarded by it, never by AreAtWar.
-- TransitionToPhase sets CurrentPhase first, then declares (the reason is in the code comment and in docs/features/war-of-the-ring.md, Phase 2).
-- A TryDeclareWar helper counts a war only when HasDeclaredWar confirms it after DeclareWarAction, and warns when a declaration had no effect.
-- New ReconcileDeclaredWars(): at FullWar, on every host session launch (WarOfTheRingBehavior.OnSessionLaunched, before CheckPhaseTransition), declares each Phase 2 or Hostile-pair war that the model holds at war (ShouldBlockPeace) but whose stored stance is not War. It repairs saves already at Full War.
-- WarOfTheRingConfigProvider.ValidateConfig drops null, blank and self-pair entries from the Phase 1 and Phase 2 wars lists; the service skips configured wars that name an eliminated or unknown kingdom.
An eight-lens Claude deep review with two adversarial verifiers per finding already ran, and its confirmed findings F01 to F20, F23 and F27 are applied in this working tree. The verified finding text is not in the repo; the summary is in the rows of docs/reviews/rca-wotr-session-reset-2026-10-08.md that name #772 and in the docs listed below.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST
1. AGENTS.md, then .ai/review-reference.md (TAOM's review rules, severity and evidence format).
2. docs/features/war-of-the-ring.md: "Phase 2: The Full War" (the new paragraphs on ordering, limits of the repair, allies and dated notices), "Phase state", Tests, How to Test In-Game steps 4 and 7.
3. docs/features/diplomacy.md (the Phase 2 and call-to-war lines) and Main/_Module/ModuleData/diplomacy/diplomacy.json (tiers; 38 Permanent alliances) and Main/_Module/ModuleData/diplomacy/war_of_the_ring.json (phase wars).
4. docs/reviews/rca-wotr-session-reset-2026-10-08.md, the Codex section and its S6 row.

DESIGN DECISIONS (do not report them as defects, but do report a defect in how they are implemented)
-- The phase turns to FullWar BEFORE the declarations. Reason: vanilla's AllianceCampaignBehavior.OnWarDeclared then already sees every Hostile pair as constantly at war and calls no Permanent ally into a war the same loop is about to declare (a simulation on the shipped data: 41 call-to-war proposals instead of 294), and TAOM's peace gates stay live while KingdomDecisionProposalBehavior re-checks pending decisions.
-- When Phase 2 declares, vanilla still asks each Permanent ally that is not itself Hostile with the enemy to join (for example Harad into Mordor's wars). Accepted, pending the maintainer.
-- A repaired Full War save gets its war notices and log entries dated at the load day. Accepted, pending the maintainer.
-- A Hostile pair that had NO stored stance link when Full War began cannot be repaired: Kingdom.GetStanceWith creates the link typed from DiplomacyModel.GetDefaultDiplomaticStance, which is War under IsAtConstantWar, with no DeclareWarAction and no OnWarDeclared. Documented as a limit (only campaigns started on the stale phase of #764 should have such pairs). Report it only if you find a common path that reaches it.
-- The reconcile runs on every host session launch and is idempotent through HasDeclaredWar.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code you read; cite file:line on both the TAOM and the engine side)
S1. Ordering. Hypothesis: with CurrentPhase = FullWar set first, something on the DeclareWarAction path consults IsAtConstantWar or IsAtWarAgainstFaction and short-circuits, so a declaration does nothing and HasDeclaredWar stays false (TryDeclareWar then warns and the war is never declared). Read DeclareWarAction.ApplyInternal, FactionManager.DeclareWar and SetStance, ChangeKingdomAction-style listeners, and every OnWarDeclared listener in the installed v1.5.4 assemblies and in TAOM (grep Main/ for OnWarDeclared and for patches on DeclareWarAction, including TAOM's own IsWarAllowed veto).
S2. HasDeclaredWar side effects. Hypothesis: the stored-link read creates links (FactionManager.GetStanceLinkInternal) for pairs that had none, and at FullWar seeds them as War; enumerate which pairs can have no link at day 44 in a normal new campaign on the shipped data (CampaignFactionManagerBehaviour, FactionHelper.GetStances in the daily tribute pass, kingdoms created later by rebellion or by the player), and what a link created this way lacks (WarStartDate, the FactionsAtWarWith lists, notifications, AI war targets).
S3. The repair at session launch. Hypothesis: declaring wars from WarOfTheRingBehavior.OnSessionLaunched is unsafe or wrong on some path: before other behaviors' OnSessionLaunched (listeners run newest-first), during loading UI, before AI or kingdom decisions are initialized, for a save loaded mid-election, or on a co-op client. Confirm the IsAuthority gate covers the call, and that CoopVetoClassificationTests now lists both mutators.
S4. Counting and the veto. TryDeclareWar counts only a war HasDeclaredWar confirms after DeclareWarAction. Hypothesis: TAOM's own declaration veto (grep IsWarAllowed and DeclareWarAction patches) refuses some Hostile or configured pairs, so every load logs the same warning forever, or the veto and ShouldBlockPeace disagree so a pair is held at constant war while its stance stays neutral forever. Also check the configured-war arm: a pair in war_of_the_ring.json Phase 2 wars that is not Hostile in diplomacy.json (ShouldBlockPeace false) is never reconciled; is that what the doc says?
S5. Call-to-war scale. With the phase set first, confirm or dispute that vanilla's OnWarDeclared ally filter skips allies that are constantly at war with the enemy (AllianceCampaignBehavior.OnWarDeclared and the code it calls), and count, from diplomacy.json, which (ally, enemy) pairs still get a call-to-war proposal at the Phase 2 transition. Say what a call-to-war agreement then does to the player's kingdom when the player is a vassal or a ruler (ballots, map notices, influence, gold).
S6. Tests. Hypothesis: a mutant still passes the suite. Try at least: guarding with `!HasDeclaredWar(a, b) && !AreAtWar(a, b)` in either loop; setting CurrentPhase after the switch; dropping the eligible filter in ReconcileDeclaredWars; counting before the confirmation in TryDeclareWar; calling ReconcileDeclaredWars after CheckPhaseTransition in the behavior; removing the live-kingdom skip. Name the test that kills each mutant, or the mutant that survives.
S7. Config validation. Hypothesis: a wars entry with an unknown kingdom id (a typo such as "rohan") passes ValidateConfig and is then skipped silently at every transition and every load, so the author never learns of it. Say whether the service or the provider should warn once, and where TAOM's other config providers do this.

FILES
Changed C#: Main/Adapters/IAllianceAdapter.cs, Main/Adapters/AllianceAdapter.cs, Main/Features/Diplomacy/IWarOfTheRingService.cs, Main/Features/Diplomacy/WarOfTheRingService.cs, Main/Features/Diplomacy/WarOfTheRingBehavior.cs, Main/Features/Diplomacy/WarOfTheRingConfigProvider.cs.
Tests: TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingConfigProviderTests.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs (one line), TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs.
Docs: docs/features/war-of-the-ring.md, docs/features/diplomacy.md, docs/features/coop-interop.md (only the WarOfTheRingBehavior row; its settings-count lines belong to another change), docs/reviews/rca-wotr-session-reset-2026-10-08.md (the #772 rows).
Other callers of the changed adapter members: Main/Features/Diplomacy/DiplomacyService.cs (AreAtWar at about :91 and :174), Main/Features/WarOfTheRingMomentum/MomentumVictoryService.cs (AreAtWar at about :83), Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs, Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs, Main/Features/Diplomacy/Hooks/PeaceActionHook.cs.
Run `git diff -- <file>` on each changed file.

NOT IN SCOPE (do not review): every other uncommitted file in the tree. Main/Features/WarChronicle/**, Main/Adapters/KingdomWarSnapshot*, PrisonerEscape*, WarEffectKeys.cs, Main/Features/TroopProgression/**, Main/Composition/FeatureModules.cs, Main/Features/TaomSettings.cs, Main/SubModule.cs, tools/analyze_war_ledger.py and their tests and docs are a separate milestone under its own review. One test fails before and after this change and is unrelated: LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey (32 taom_race_ability_* keys untranslated).

REQUIRED SECTIONS

1. VANILLA CODE. Decompile from the installed v1.5.4 DLLs (the filesystem and ilspy MCP servers; for browsing, C:\Users\mikew\.taom-src\v1.5.4 or E:\Decompiled_Bannerlord\_categories_v1.5.4) and paste as code blocks: DeclareWarAction (ApplyByDefault and ApplyInternal); FactionManager.DeclareWar, SetStance, GetStanceLinkInternal, AddStance and IsAtWarAgainstFaction; Kingdom.GetStanceWith and IsAtWarWith; DefaultDiplomacyModel.GetDefaultDiplomaticStance and IsAtConstantWar; AllianceCampaignBehavior.OnWarDeclared and what it calls to propose a call to war; KingdomDecisionProposalBehavior.OnWarDeclared; CampaignFactionManagerBehaviour (new game and load); FactionHelper.GetStances and its daily caller.

2. DEEP ANALYSIS, concrete scenarios. For each, state the call sequence and the end state (stored stance per pair, OnWarDeclared fired or not, call-to-war proposals, the log lines):
a. New campaign, test mode (phase days 1 and 3), the player a vassal of Gondor (empire_w): day 3 transition.
b. A save written on v2.0.34 (the old AreAtWar guard) at day 60, FullWar, loaded on this build: OnSessionLaunched.
c. The same save loaded a second time after one in-game day and a save.
d. A Hostile pair that made peace before Phase 2 (day 30 to 44 window), then Phase 2.
e. A co-op client loading the host's Full War save.

3. CONFIG CROSS-REFERENCE. war_of_the_ring.json Phase 1 and Phase 2 wars against the cheatsheet and against diplomacy.json tiers: which configured pairs are not Hostile, and what the reconcile does with each.

4. FINDINGS OR OBSERVATIONS. For each finding: severity, TAOM file:line, engine file:line, the code you rely on quoted, a reproduction or proving code, and a fix. Mark anything you could not prove UNVERIFIED.

QUALITY GATES
-- Every finding cites a TAOM file:line and, when it rests on engine behaviour, an engine file:line, with the code quoted.
-- CONFIRM or DISPUTE every known suspect; never skip one.
-- Do not report a design decision above as a defect; do report a defect in its implementation.
-- Say "no finding" plainly when a suspect is clean; do not pad.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write docs/reviews/raw/codex-adversarial-wotr-phase2-wars-2026-10-08.md yourself; the dispatcher redirects stdout there.
