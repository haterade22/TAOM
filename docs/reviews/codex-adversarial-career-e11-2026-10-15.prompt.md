You are an adversarial reviewer for TAOM (Tales From the Age of Men), a Lord of the Rings total conversion for Mount & Blade II: Bannerlord, C# .NET Framework 4.7.2. Assume the authors missed something. Read AGENTS.md and .ai/review-reference.md first and follow the reviewer role: report defects with evidence; do NOT edit any file.

Scope: the commit e91660cc9 on branch feature/career-forks, in this worktree (`git show e91660cc9`; its parent is 9f58c58de). Ignore the untracked generator files (tools/generate_career_forks.py, tools/career_design/, their tests and doc, tools/translate_with_claude.py changes): they are a separate change. The installed game is Bannerlord v1.5.5 (.claude/pinned-game-version.txt). Engine facts must come from the installed DLLs under E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client and Modules\SandBox\bin\Win64_Shipping_Client and Modules\SandBox.ViewModelCollection (decompile with ilspycmd), not from memory. E:\Decompiled_Bannerlord is an older dump: browse only.

What changed (career redesign #769, engine step E11 and E11b): ten campaign career passives, each read by a TAOM GameModel override through ICareerPassiveService.
- InfluenceGain: TaomBattleRewardModel.CalculateInfluenceGain, owner-first key (CareerPassiveHero.ResolveId), like RenownGain.
- RecruitCost (negative = cheaper): TaomPartyWageModel.GetTroopRecruitmentCost passes the buyer's magnitude into WageModifierService.CalculateRecruitmentCost, summed with the buyer perk factors before LimitMin(1).
- PrisonerConversion: TaomPrisonerRecruitmentCalculationModel.GetConformityChangePerHour, leader key.
- FoodConsumption (negative = eats less): TaomFoodConsumptionModel.CalculateDailyFoodConsumptionf, leader key, after the cultural feats.
- WoundRecovery: TaomPartyHealingModel.GetDailyHealingForRegulars, leader key, only a positive value on the non-prisoner call, factor stopped at -1. The TroopSurvival read in the same model moved to ScaleByPassive.
- DailyGold (flat): TaomClanFinanceModel.CalculateClanGoldChange and CalculateClanIncome, clan leader key, outside the applyWithdrawals return.
- LootGold: TaomBattleRewardModel.GetLootGoldChances rebuilds the share list (CareerLootShares), each winner's own leader key.
- RaidLoot: TaomRaidModel.GetRaidLootMultiplier, receiving party's leader.
- TroopTraining (flat XP per troop per day, class-filtered): new TaomPartyTrainingModel.GetEffectiveDailyExperience, ApplyFlat with the troop's class.
- TradePrices (negative = better prices): new TaomPartyTradeModel.GetTradePenaltyFactor through ScaleByPassive, which floors the scale at 0.
- Service: ScaleByPassive (value * max(0, 1 + m)), ApplyFlat with an optional troop class, ApplyWoundRecovery; ApplyFactor skips its event log line when the number did not move. Display labels, consumer map, log policy, three console probes, localization rows (English seeds in 12 languages), docs.
- Shipped data carries none of these types yet: inert until the generator writes a kingdom.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST: docs/reviews/rca-career-e11-2026-10-15.md (what a six-lens Claude review, its fix round and a convergence review found and fixed; do not re-report those unless the fix is wrong), docs/features/career-system.md section "Campaign consumers (#769 E11)", .claude/rules/gamemodels.md.

ALREADY KNOWN, do not report: 9 dotnet tests fail on this tree AND on pure trunk (Harad, Umbar, Shaghana, Abanissa, Aserai data); co-op behaviour of these effects is unverified and recorded; HeroHealing's older starving and prisoner flaws and its log frequency are follow-ups; LootGold mints extra gold by design (MapEvent does not renormalise); DailyGold cannot pay the same day's wages (documented); the API snapshot -Check fails on the backlinks footer since 2026-09-06.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with evidence):
1. Money: can any of the ten effects charge or pay the wrong party, double count, or leave the shown number different from the real one (recruit screen vs charge, clan tooltip vs daily payment, loot message vs gold, trade screen buy and sell)?
2. AI reach: can an AI lord, a companion, a caravan or a garrison of another clan receive a career effect it should not, or the player miss one in a case the docs promise (the player inside an AI army, a player-owned caravan, a companion-led party of the player's clan)?
3. Vanilla clamps and ordering: does any override cancel a vanilla limit (LimitMin, LimitMax, a floor at 0), multiply after a brake, or put a flat term before a factor?
4. The two new model slots: registration order against SandBox's defaults, any other module or Harmony patch on PartyTrainingModel, PartyTradeModel or their callers, and whether GetTradePenaltyFactor and GetEffectiveDailyExperience have callers the comments do not name.
5. Edge values: NaN, infinity, a total of -1 or less, a huge value, a null party or hero, a party with no leader, in each override and in ScaleByPassive, ApplyFlat and ApplyWoundRecovery.
6. Tests: do the source pins fail on the regressions they claim to catch (mutation), and does any test pass on code that is wrong?

REQUIRED SECTIONS:
- VANILLA CODE: paste the decompiled v1.5.5 members you relied on.
- DEEP ANALYSIS of the six suspects with concrete scenarios.
- CONFIG CROSS-REFERENCE: no shipped career pip uses any of the ten types; every new label key exists in Main/_Module/ModuleData/taom_module_strings.xml and all 12 language files.
- FINDINGS: each with severity (P1/P2/P3), file:line, evidence, and a fix; say UNVERIFIED where you could not prove it.

QUALITY GATES: every claim cites a file:line you read; no finding about code that matches vanilla behaviour on purpose; no style-only findings; do not re-report what the RCA records as fixed unless the fix is wrong.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. A two-thread harness against the installed MBFastRandom proved a shared-generator race (E8-E10 review).
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Output: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-career-e11-2026-10-15.md; do NOT write that file yourself.
