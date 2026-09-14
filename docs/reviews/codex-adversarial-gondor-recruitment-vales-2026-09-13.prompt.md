# Adversarial review: Gondor volunteer pools, four vale fixes and the household-line availability retune (2026-09-13)

You are reviewing an UNCOMMITTED changeset in the TAOM repository (Bannerlord v1.4.8 total-conversion mod, .NET Framework 4.7.2). See it with `git diff HEAD -- Main/_Module/ModuleData/recruitment_pools/gondor.json Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Gondor.cs TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs docs/features/volunteer-recruitment.md` (four files are staged; the JSON is not). The JSON diff ALSO carries a concurrent session's hunks (castle_village_EW7_2 and castle_village_EW7_3 removed from the Anfalas group; village_EW10_1..3 added there; village_EW11_1..3 added to the Harondor group). Those belong to a map re-binding (#597) and are out of scope except where noted. Ignore every other uncommitted file in the tree. Try to refute every claim below. Report only defects you can prove from the TAOM source, the installed engine DLLs (E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client) or the LIVE data (the filesystem MCP reaches Modules/TAOM_Map/ModuleData); state UNVERIFIED where you cannot. Do NOT edit files. Return the full report as your FINAL MESSAGE; do not write any file under docs/reviews/raw yourself (the dispatcher redirects stdout there).

## Feature in four lines

TAOM replaces `DefaultVolunteerModel.GetBasicVolunteer` with `TaomVolunteerModel`, which asks `VolunteerRecruitmentService` for a weighted pick from per-settlement pools; Gondor's pools come from `recruitment_pools/gondor.json` (loaded once per process by `GondorRecruitmentJsonLoader`, overwriting the hand-written mirror in `VolunteerRecruitmentService.Gondor.cs`, which is live only when the JSON is missing and in the unit tests). Castles have notables too, through the CastleRecruitment feature (`CastleNotableMaintainer.FillCastleVolunteers`, Patch42 for AI). This change fixes four fiefs that recruited the wrong region for where they stand (Glanhir castle_EW2 and its villages castle_village_EW2_1/_2 gave Lossarnach and Lamedon, now Ringlo Vale only; Morlad castle_EW6, its four villages and Blackroot Village Haven castle_village_EW3_3 gave Pinnath Gelin or Belfalas, now Blackroot Vale only; Bar-en-Siril castle_EW7 gave Anfalas, now Serelond only) and, on player feedback that the household lines were too rare, makes every castle that has a household line exclusive to it (also castle_EW9 Tolfalas, castle_EW12 Linhir, castle_EW4 Cair Andros 90 / Ithilien Ranger 10) and raises the towns' household share from 20% to 40% (Minas Tirith and Osgiliath 50/40/10). Villages keep the regional levies. No troop id or settlement key was added or removed by this session.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, shaghana, abanissa. Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar. Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor region, empire_s=Mordor region, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale. "rohan" and "dol_guldur" are NOT valid ids. Gondor settlements carry the EW prefix; Gondor clans are clan_empire_west_1..14.

## READ FIRST

- docs/features/volunteer-recruitment.md (the "Weighting standard (2026-09-13)" paragraph is the spec for this change; the cascade is conditional > settlement > bound settlement > clan > culture)
- docs/features/castle-recruitment.md (castle notables, castle-safe occupations, the fill path)
- docs/reviews/rca-gondor-recruitment-2026-07-27.md (the previous retune, the lockstep test and the typo gate; do not re-report what it fixed)
- .claude/rules/troops.md "Volunteer Recruitment Lookup Priority"
- Main/_Module/ModuleData/recruitment_pools/gondor.json (the `notes` array states the rules)
- Main/_Module/ModuleData/troops/troops_gondor.xml (levels and upgrade_targets of every gondor_* troop)

## Files under review

Data: Main/_Module/ModuleData/recruitment_pools/gondor.json
C#: Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Gondor.cs (weights only)
Tests: TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs (new: GondorJsonLoader_ProductionJson_HouseholdLinePools_OfferOnlyThatLine, GondorJsonLoader_ProductionJson_TownHouseholdLineShareIsFortyPercent; updated DataRows in GetVolunteerTroopId_SpecificSettlements_ReturnExpectedRegularTroop, GetVolunteerTroopId_MinasTirith_BoundaryRolls_ReturnExpectedTroop, GetVolunteerTroopId_OsgiliathSettlements_BoundaryRolls_ReturnExpectedTroop)
Docs: docs/features/volunteer-recruitment.md, CHANGELOG.md (top entry)
Unchanged context you must read: Main/Features/TroopProgression/VolunteerRecruitmentService.cs (PickWeighted, ResolveStandardCascade), Main/Features/TroopProgression/GondorRecruitmentJsonLoader.cs, Main/Features/TroopProgression/Models/TaomVolunteerModel.cs, Main/Adapters/VolunteerContextAdapter.cs, Main/Features/CastleRecruitment/Hooks/CastleNotableMaintainer.cs, Main/Features/CastleRecruitment/CastleRecruitmentService.cs, Main/Features/CultureConversion (the converted-settlement recruitment branch)

## VANILLA CODE (decompile from the installed DLLs and quote; E:\Decompiled_Bannerlord is a reference only)

- TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior: UpdateVolunteersOfNotablesInSettlement (the daily fill, the in-slot upgrade against MaxVolunteerTier, the tier sort), HourlyTickParty and CheckRecruiting (AI recruiting), the player-facing slot gating helper it or HeroHelper exposes (MaximumIndexHeroCanRecruitFromHero or its 1.4.8 equivalent)
- TaleWorlds.CampaignSystem.GameComponents.DefaultVolunteerModel: GetBasicVolunteer, GetDailyVolunteerProductionProbability, MaxVolunteerTier, CanHaveRecruits
- TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment.RecruitmentVM (or its 1.4.8 location): how a volunteer's cost and availability are computed and shown, and any tier or level condition on a slot
- TaleWorlds.CampaignSystem.CampaignBehaviors.AiVisitSettlementBehavior: the recruitment scoring that Patch42 unlocks for castles (what value it puts on a tier-3+ volunteer versus a tier-1 one)

## KNOWN SUSPECTS (CONFIRM or DISPUTE with evidence)

KS1 -- Castle pools now contain ONLY level 16+ roots (gondor_brv_bowman L16, gondor_ser_noble L16, gondor_tol_arbalest L16, gondor_lin_noble L16, gondor_ca_noble; gondor_ring_peasant is L6) plus the L51 gondor_ithilien_ranger at Cair Andros. Trace a castle notable's daily fill (CastleNotableMaintainer.FillCastleVolunteers plus whatever vanilla path also touches castle notables) and the vanilla town/village fill, and show whether any consumer filters, sorts out or rejects a basic volunteer whose tier is already 3 or more (IsBasicTroop, Tier, Level, UpgradeTargets, the MaxVolunteerTier upgrade loop). If any path leaves such a slot empty or throws, that is P1 because the castle then offers nothing.

KS2 -- Slot access by relation. Vanilla gates which volunteer INDICES the player may recruit by relation with the notable (and perks). Vanilla sorts a notable's VolunteerTypes by tier so the cheap ones sit at the low indices the player reaches first. TAOM's castle fill deliberately does not re-sort. With an exclusive pool every slot is the same line, so does the relation gate now hide the whole pool from a low-relation player at castles, and is that materially different from towns? Quote the gate and give the index reachable at relation 0, 10, 20 for a castle notable of each castle-safe occupation.

KS3 -- In-slot upgrade drift. MaxVolunteerTier is overridden to 6. With Blackroot Vale bowman (tier 3 at L16) as the only root, the daily upgrade roll can carry a slot up to tier 6 (gondor_brv_ranger L36 / shadowbow L41) before the player buys it. Compute from the decompiled loop the expected share of tier-5/6 volunteers at a castle notable after 30 days of no recruiting, for the Morlad pool, and say whether that is a design consequence the CHANGELOG should state (it does not today) or a defect.

KS4 -- Lockstep mirror. GondorPools_HandWrittenFallback_MatchesProductionJson compares normalised shares with tolerance 1e-4. Recompute every changed settlement by hand: castle_EW4 9/9/2 versus 45/45/10, town_EW11 9/9/9/9/8/8/8 versus 15x4 + 13.3333/13.3333/13.3334, town_EW5 3x5 + 5x2 versus 12x5 + 20x2, town_EW7 3x4 + 8 versus 15x4 + 40. Report any share that differs by more than 1e-4, and whether the test's tolerance could mask a wrong weight elsewhere.

KS5 -- The JSON notes claim "Every other village keeps its regional line." Check it against the groups: the Bar Melui group lists village_EW7_1..3 alongside town_EW7 at 60/40, so those three villages get the Lossarnach noble at 40%, and any other village listed in a mixed group. Is the note wrong, is the data wrong, or both, given the note "If a rule says only a settlement, only that town/castle receives the specific pool, not its bound villages" that sits four lines below it?

KS6 -- Live-map coverage after the concurrent rename. Read the LIVE Modules/TAOM_Map/ModuleData/settlements.xml. List every EW-prefixed settlement id there and diff it against the JSON keys (98 keys after both sessions' edits). For any live id absent from the JSON, state what the cascade resolves it to (bound settlement, then the owner clan pool in InitializeGondorClans, then the gondor CultureMap) and whether that is acceptable. In particular castle_village_EW7_2 and castle_village_EW7_3: if they still exist in the live file they now inherit castle_EW7's Serelond-only pool via BoundSettlementId, which is not the regional-levy rule for villages.

KS7 -- Clan and culture pools were NOT retuned (clan_empire_west_2 is still bel_recruit 7 / da_noble 3, the culture fallback is ano_peasant 7 / bel 1 / lam 1 / loss 1). They fire only for settlements with no pool: converted fiefs (CultureConversion) and unmapped ids. Trace CultureConversion's recruitment branch and say which pool a Mordor-captured castle_EW6 resolves to and which pool a Gondor-captured non-Gondor castle resolves to, and whether either now contradicts the "castle offers its household line" rule in a way a player would notice.

KS8 -- AI army composition. Patch42 lets AI lords drain castle volunteers. With castle pools exclusive, do Gondor AI lords now field noticeably more household troops, and does AiVisitSettlementBehavior's scoring prefer or avoid a castle whose volunteers are all tier 3+? Quote the scoring term. This is an observation unless the scoring makes castles unattractive, which would make the change invisible for AI.

## FEATURE-SPECIFIC DEEP ANALYSIS

1. Walk one daily tick for a Morlad castle notable (GangLeader) from an empty volunteer array: which slots fill, with what, and what the player sees in the recruit screen at relation 0.
2. Walk the same for Dol Amroth (town_EW5) under 60/40: expected count of gondor_da_* volunteers across the town's notables on an average day versus under the old 80/20, so the CHANGELOG's "commoner" claim has a number.
3. A save made under the old JSON, loaded under the new one: existing volunteers in slots are untouched (they are CharacterObject references) and only refills change. Confirm from RecruitmentCampaignBehavior that nothing revalidates existing slots against the pool.
4. The reachability guard (AllNonMilitiaNonBossTroops_AreReachableFromARecruitmentPoolRoot) floods from pool roots. Lamedon regulars are no longer offered at Glanhir and Pinnath Gelin regulars no longer at Morlad; confirm those lines remain rooted elsewhere (castle_EW1, town_EW9, castle_EW8/13, town_EW8) so the guard's pass is not a coincidence of the hand-written layer.
5. Are the two new tests honest: prefix matching ("gondor_ring_", "gondor_brv_", "gondor_ser_", "gondor_tol_", "gondor_lin_", "gondor_ca_,gondor_ithilien_ranger") cannot pass on a troop from another line; grep troops_gondor.xml for prefix collisions.

## CONFIG CROSS-REFERENCE

Every troop id in the changed groups exists in troops_gondor.xml with the level the prompt states. Every settlement id in the changed groups exists in the LIVE settlements.xml. Every group totals 100 (the loader multiplies by 10000; 13.3333 + 13.3333 + 13.3334 + 4 x 15 = 100). No settlement appears in two groups. The C# mirror lists exactly the JSON's towns and castles (27) plus the town_ES2 conditional.

## FINDINGS OR OBSERVATIONS

Report each finding as: severity (P1 ship-blocking / P2 should fix / P3 nit / observation), file:line, what the code or data does, what it should do, proof (quoted TAOM and engine lines). Then the Known Suspects verdicts KS1 to KS8 with numbers. Then anything the five-agent deep review missed (its report is summarised in docs/reviews/rca-gondor-recruitment-vales-2026-09-13.md if that file exists when you run; if it does not, skip).

## QUALITY GATES

- Every engine claim quotes decompiled source from the installed 1.4.8 DLLs.
- No finding on deliberate design: villages regional-only, castles exclusive, towns 60/40, the ranger at 10%, the clan and culture pools untouched.
- Do not propose new troop ids, new settlement keys or a different split; the numbers are the user's decision. A wrong number (a group not totalling 100, a mirror share off) is in scope.
- State UNVERIFIED rather than guessing.

## PRIOR REVIEW LESSONS

SUCCESSES: config-id cross-reference against the live files has caught dead keys; decompiling the engine consumer of a GameModel result caught cross-entity propagation the per-file reviews missed; recomputing a pool's shares by hand caught a group that shipped at 120%.
FAILURES: Codex has flagged vanilla-matching code as bugs, assumed empire=Rohan (it is Dunland), skipped hard sections, and once wrote its report into the transcript file. Do none of these.
