ADVERSARIAL REVIEW: War Chronicle Batch A (issue #765, milestones M1 war ledger, M2 timed-effect registry and its two consumers, M5 rally catch-up)

REVIEW ONLY. Do not modify, create or delete any file anywhere, including docs/reviews/raw/. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-war-chronicle-batch-a-2026-10-09.md, so never write that path yourself.

Feature: a War of the Ring stalemate layer for the AI war, uncommitted on branch bannerlord-1.5.x over d1b9fd91a, target Bannerlord v1.5.4. (Steam updated the installed game to v1.5.5 this morning; the maintainer handles that engine update in another session. Read engine semantics from v1.5.4: C:\Users\mikew\.taom-src\v1.5.4 and E:\Decompiled_Bannerlord\_categories_v1.5.4. Do not report the version drift itself.)
-- A timed, kingdom-keyed effect registry (Main/Features/WarChronicle/Effects/): kinds VolunteerRate and PrisonerEscape; multiplier = clamp(1 + strength x sum of magnitudes), clamps 0.5 to 1.5 and 1.0 to 3.0, MCM strength 0 to 2 baked in on the daily expire.
-- Two consumers. Volunteers: TaomVolunteerModel.GetDailyVolunteerProductionProbability and the castle recruitment path delegate to VolunteerProductionService (culture feats, then the war multiplier of the owner's kingdom; the player's own clan never gets the war multiplier). Escape: a daily pass gives a captured lord of a boosted kingdom an extra roll p x (m - 1), mirroring vanilla PrisonerReleaseCampaignBehavior.DailyHeroTick (v1.5.4 :201-250) with its CanHeroBeReleased veto and, since the review, the perk and Valor factor read by PrisonerEscapeAdapter.
-- The rally: fortification points 2 x towns + castles against a baseline taken on the first daily tick (a late take is logged when no baselines exist after day 1); tiers with hysteresis at 25/15 and 50/40 percent loss; AI-ruled kingdoms at war only; effects as "rally:<kingdom>" sources refreshed daily with a 48-hour lifetime.
-- A daily [WarLedger] log line per kingdom in a pinned v=1 format, parsed by tools/analyze_war_ledger.py; a shared fixture tools/tests/fixtures/war_ledger_v1.txt pins both sides.
-- One chunked SyncData payload (MomentumSyncChunker, keys _taom_war_chronicle_v1_*) in WarChronicleBehavior, a behavior built with new per campaign through WarChronicleModule, whose constructor resets every per-campaign singleton (the session-reset rule in .claude/rules/csharp-architecture.md).
-- Console commands (taom.war_effects, taom.war_effect_add, taom.rally_status, taom.print_war_ledger).
An eight-lens Claude deep review with adversarial verification already ran (53 findings, 42 confirmed, 3 refuted); its fixes are applied in this working tree. Do not re-report a finding the code now handles unless the fix is wrong or incomplete.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST
1. AGENTS.md, then .ai/review-reference.md (TAOM's review rules, severity and evidence format).
2. docs/features/war-chronicle.md (the feature as built, known limitations).
3. C:\Users\mikew\.claude\plans\we-have-the-war-resilient-clock.md, Parts A, C, D and Wiring (the approved design).
4. .claude/rules/csharp-architecture.md (session reset, config validation, NaN gates) and .claude/rules/gamemodels.md rule 9.

DESIGN DECISIONS (do not report them as defects, but do report a defect in how they are implemented)
-- No player gate; small temporary effects only (volunteers and escape, never combat power); catch-up for AI kingdoms that lose; the player's own clan never gains or loses; momentum victory stays off.
-- The volunteer roll also gates the promotion of an occupied slot, so the effect also raises volunteer quality slightly; documented.
-- Castles get the war multiplier but not the culture respawn feats they never had.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code you read; cite file:line on both the TAOM and the engine side)
S1. Escape parity. Compare WarEscapeService and WarEscapeDailyPass plus PrisonerEscapeAdapter with vanilla DailyHeroTick line by line: the base chance, the mobile-captor factor, the three player-held halvings, the perk and Valor factor now read by the adapter, the CanHeroBeReleased veto, the captor states it skips, and EndCaptivityAction.ApplyByEscape. Hypothesis: the extra roll fires for a hero vanilla would never release, or double-counts a factor, or the adapter's factor read throws or reads the wrong side's perks.
S2. Volunteer reach (gamemodels.md rule 9). Every engine caller of GetDailyVolunteerProductionProbability and of the castle slot path: what the multiplier propagates to (garrison auto-recruitment, slot promotion, the player's recruit screen), and whether any path lets the player's own clan or a player-ruled kingdom gain.
S3. Save path. WarChronicleBehavior.SyncData, WarChronicleSaveCodec (section isolation), the chunking, the constructor reset: walk a new campaign, a load with the record, a load of a save written before this feature (no record, so SyncData never runs in loading mode), a second campaign in one process, and a co-op client. Hypothesis: a state survives into the wrong campaign or a corrupt section kills the others.
S4. The rally numbers. Baselines (first tick, late take, new kingdoms, destroyed kingdoms), loss math, the hysteresis machine, eligibility (AI-ruled, at war), RemoveSource on a drop or on disable, the 48-hour lifetime against a skipped daily tick (fast-forward, a long menu). Hypothesis: a tier flaps, sticks after the kingdom recovers, or a rally effect outlives the rally.
S5. Float safety. Every float gate on engine or config input: strength, magnitudes, the escape chance, the volunteer probability (NaN and Infinity), the ledger numbers (str -1 for NaN), casts to int. Hypothesis: a NaN reaches an engine number or a save.
S6. Ledger contract. WarLedgerFormatter against tools/analyze_war_ledger.py and the shared fixture: InvariantCulture, the logger prefix, every key, dedup, the verdicts. Hypothesis: a format detail drifts between the two and the fixture does not catch it.
S7. Co-op. The daily tick is host-only and save writes check CoopSessionPolicy.MayWriteSaveBackedState; the volunteer model runs on every peer. Hypothesis: a client reads an effect registry the host never syncs, and two peers compute different volunteer numbers.

FILES
New: Main/Features/WarChronicle/** (WarChronicleModule, WarChronicleIoC, WarChronicleBehavior, WarChronicleTickService, WarChronicleStateService, WarChroniclePayload, WarChronicleSaveCodec, IWarChronicleSettingsProvider, WarChronicleSettingsProvider, Effects/*, Rally/*, Ledger/*, Cheats/*); Main/Adapters/{IKingdomWarSnapshotAdapter,KingdomWarSnapshotAdapter,KingdomWarSnapshot,IPrisonerEscapeAdapter,PrisonerEscapeAdapter,PrisonerEscapeSnapshot}.cs; Main/Features/TroopProgression/VolunteerProductionService.cs; Main/_Module/ModuleData/war_chronicle/rally.json; tools/analyze_war_ledger.py; tools/tests/test_analyze_war_ledger.py; tools/tests/fixtures/war_ledger_v1.txt; docs/features/war-chronicle.md; tests under TAOM.Tests/Features/WarChronicle/, TAOM.Tests/Adapters/{KingdomWarSnapshotAdapterTests,PrisonerEscapeAdapterTests}.cs, TAOM.Tests/Features/TroopProgression/{VolunteerProductionServiceTests,VolunteerProductionWiringTests}.cs.
Modified (git diff): Main/Composition/FeatureModules.cs; Main/Features/CastleRecruitment/{CastleRecruitmentService,ICastleRecruitmentService}.cs and Hooks/CastleNotableMaintainer.cs; Main/Features/TaomSettings.cs (the War of the Ring/Rally group); Main/Features/TroopProgression/Models/TaomVolunteerModel.cs; Main/Features/TroopProgression/TroopProgressionIoC.cs; Main/SubModule.cs (the TaomVolunteerModel construction and one comment); tests CampaignHotPathSettingsProvidersTests, CastleRecruitmentServiceTests, SettingsFingerprintTests, ConsoleCommandBindingTests; docs bannerlord-together-compat.md, castle-recruitment.md, coop-interop.md, dev-console.md, troop-progression.md, INDEX.md, feature-map.md, gamemodel-registry.md, tools/README.md.

NOT IN SCOPE: the committed #764 and #772 work (Main/Features/Diplomacy/**). Two tests fail before and after this change and are unrelated: EveryLanguage_DeclaresARowForEveryEnglishKey (32 race-ability keys untranslated) and BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild (the installed game moved to v1.5.5).

REQUIRED SECTIONS
1. VANILLA CODE. Paste as code blocks from v1.5.4: PrisonerReleaseCampaignBehavior.DailyHeroTick and what it calls; EndCaptivityAction.ApplyByEscape; DefaultVolunteerModel.GetDailyVolunteerProductionProbability; RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement; CampaignBehaviorDataStore.LoadBehaviorData and SaveBehaviorData; the castle volunteer path TAOM hooks.
2. DEEP ANALYSIS, concrete scenarios, each with the call sequence and end state: a. new campaign to day 120 with Gondor losing half its fiefs; b. that kingdom recovering to 35% lost; c. a save and reload at day 80; d. a save from before this feature loaded at day 200; e. a captured Gondor lord held by a mobile Mordor party during tier 2; f. the player as a vassal of a rallying kingdom; g. the MCM strength set to 0, then to 2.
3. CONFIG CROSS-REFERENCE. rally.json against RallyConfigProvider validation and the MCM group in TaomSettings.cs.
4. FINDINGS OR OBSERVATIONS. For each: severity, TAOM file:line, engine file:line, the code quoted, a reproduction or proving code, and a fix. Mark anything you could not prove UNVERIFIED.

QUALITY GATES
-- Every finding cites a TAOM file:line and, when it rests on engine behaviour, a v1.5.4 engine file:line, with the code quoted.
-- CONFIRM or DISPUTE every known suspect; never skip one.
-- Do not report a design decision above as a defect; do report a defect in its implementation.
-- Say "no finding" plainly when a suspect is clean; do not pad.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write docs/reviews/raw/codex-adversarial-war-chronicle-batch-a-2026-10-09.md yourself; the dispatcher redirects stdout there.
