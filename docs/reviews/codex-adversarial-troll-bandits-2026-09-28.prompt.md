Adversarial review: Creature Bandits troll bands and the twenty-brood cap (#694), TAOM (Bannerlord v1.5.3 mod, .NET Framework 4.7.2).

You are in the worktree E:/repos/taom-troll-bandits on branch feat/troll-bandits. The change under review is commit b9fdaaf7 (`git show b9fdaaf7`, base 6df36909); HEAD 946f92ac only merges unrelated trunk work (Saruman faces) on top. Read AGENTS.md, then .ai/review-reference.md, and follow the reviewer role: report defects with evidence, fix nothing, edit no file.

What the change does: spider broods (#692, riderless creature bandits) go from 4 to 20, one new brood a day, anchored on all 47 Mirkwood and Dol Guldur settlements. New Wild Troll bandit bands: hidden twins of Mordor's cave_troll/hill_troll (characters/troll_bandits.xml), bandit culture and clan wild_trolls (looter shape, can_have_settlement false), template wild_trolls_band_template (2 to 4 trolls), TrollBandSpawnBehavior keeps one band per living kingdom (one new band a day). Trolls are never prisoners (TaomBattleRewardModel.CanTroopBeTakenPrisoner), never take freed prisoners (TaomBattleRewardModel.GetLootPrisonerChances override, renormalised), skip parley (Patch94 no-parley), and are dropped from the bandit "serve under my command" join list (Patch94_CreatureBandNoJoin prefix on private BanditInteractionsCampaignBehavior.GetMemberAndPrisonerRostersFromParties). Shared spawn helper CreatureBandParties (vanilla looter steps plus up to 15 retries for a point outside the player's sight). MCM switch "Spawn Troll Bands", default on. An 8-lens Claude deep review already ran; its RCA is docs/reviews/rca-troll-bandits-2026-09-28.md. Find what it missed.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
docs/features/creature-bandits.md (Troll bands, Campaign, Known Limitations, In-game checklist)
docs/reviews/rca-troll-bandits-2026-09-28.md
docs/reference/harmony-patch-registry.md section Patch94_CreatureBroodCampaign

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code evidence):
S1. Patch94_CreatureBandNoJoin: the prefix mutates the caller's list2 so bands are neither recruited nor destroyed. Is there any other consumer of those parties (PlayerEncounter joined-party state, a MapEvent the bands were already added to, OnBanditPartyRecruited listeners) that leaves a band in a broken state after the encounter closes?
S2. GetLootPrisonerChances override: renormalising after dropping band winners. Hero prisoners go through the same list (the hero branch in MapEvent.LootDefeatedPartyPrisoners): can a hero now be lost or released where vanilla would have given it to a lord? Is StoryMode's or NavalDLC's wrapper model bypassing TAOM's override?
S3. TrollBandSpawnBehavior counts bands by (HomeSettlement.MapFaction as Kingdom)?.StringId and living kingdoms by !IsEliminated && Settlements.Count > 0. Rebel clans, player kingdom creation, minor factions, Kingdom.All containing inactive entries: any case that spawns bands without bound, or crashes?
S4. The twins in characters/troll_bandits.xml carry occupation Bandit and culture wild_trolls: any vanilla or TAOM code (encyclopedia, troop tree UI, notables, quests, recruitment, TAOM culture-keyed services, alignment, banner bearers) that breaks on a Bandit-occupation humanoid of a culture with no settlements?
S5. CreatureBandParties.Spawn out-of-sight retry uses MobileParty.MainParty.SeeingRange and CampaignVec2.DistanceSquared: does this match vanilla BanditSpawnCampaignBehavior's own rule, and is it safe while the main party is in a settlement, at sea (NavalDLC), or during character creation?
S6. BroodAnchorSettlementIds grew to 47: any settlement type (castle village) that breaks SetMovePatrolAroundSettlement or NavigationHelper.FindPointAroundPosition for a looter party?

FILES (see git show b9fdaaf7 --stat): Main/Features/CreatureBandits/{TrollBandSpawnBehavior,CreatureBandParties,CreatureBroodSpawnBehavior,CreatureBanditRules,CreatureBanditsConfig,CreatureBanditsModule}.cs, Hooks/{Patch94_CreatureBroodCampaign,CreatureBanditAgents}.cs, Diagnostics/*, Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs, Main/Features/BanditManagement/Models/TaomBanditDensityModel.cs, Main/Features/TaomSettings.cs, Main/_Module/ModuleData/{characters/troll_bandits.xml, characters/clans.xml, taom_spcultures.xml, taom_partyTemplates.xml, taom_module_strings.xml}, Main/_Module/SubModule.xml, TAOM.Tests/Features/CreatureBandits/*.

REQUIRED SECTIONS:
1. VANILLA CODE: paste the v1.5.3 engine code you rely on for each suspect as code blocks (decompile the installed DLLs under E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client and Modules/*/bin/Win64_Shipping_Client with ilspycmd, or read C:/Users/mikew/.taom-src/v1.5.3/). Mark anything you could not read UNVERIFIED.
2. SUSPECTS: CONFIRMED / DISPUTED / UNVERIFIED for S1 to S6 with evidence.
3. CONFIG CROSS-REFERENCE: every id the new XML and C# reference (troops, items, culture, clan, template, settlements in the live E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/ModuleData/settlements.xml, action sets in the live LOTRLOME_Armory).
4. FINDINGS OR OBSERVATIONS: each with severity (HIGH/MED/LOW), file:line, the concrete scenario, and proving code.

QUALITY GATES: every finding cites code you read in this run; do not report vanilla-matching behaviour as a bug; do not skip a suspect because it is hard; say UNVERIFIED rather than guess.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Output: return the full report as your FINAL MESSAGE. The dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-troll-bandits-2026-09-28.md; do NOT write that file (or any file) yourself.
