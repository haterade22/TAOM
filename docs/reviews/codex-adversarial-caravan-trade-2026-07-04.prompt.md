# Codex Adversarial Review -- TAOM CaravanTrade feature

You are an adversarial code reviewer for TAOM, a Lord of the Rings total-conversion mod for Mount and Blade II Bannerlord v1.4.6 (.NET Framework 4.7.2, Harmony + GameModel + DryIoc + adapter architecture). Be skeptical. Find real bugs. Confirm or DISPUTE each Known Suspect below with evidence from the actual code. Do not flag vanilla-matching code as a bug. Use the installed-engine decompiles as ground truth.

## Feature (1-2 lines)

`CaravanTrade` makes AI/player caravans range past their local town cluster (they were shuttling between Minas Tirith and East/West Osgiliath), trade across TAOM's endless Free-vs-Evil war, and carry fuller baskets. Four Harmony postfixes on vanilla `CaravansCampaignBehavior` private methods + two `TaomCaravanModel` overrides, all delegating to a pure `ICaravanTradeService`. Config-driven (JSON + MCM), master-off = exact vanilla, save-clean (no SyncData).

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
NOTE: "rohan" is NOT a valid ID (Rohan uses "vlandia"). "dol_guldur" is NOT valid -- use "dolguldur".
Alignment: Free vs Evil vs Neutral, defined in Main/_Module/ModuleData/execution/alignment.json. Neutral kingdoms include battania, shaghana, abanissa, umbar.

## READ FIRST

- docs/features/caravan-trade.md (feature design + the four levers + the emergent-money rationale)
- docs/reviews/rca-caravan-trade-2026-07-04.md (the internal 5-agent deep-review already ran; it found + fixed one HIGH -- the war-gate Neutral inversion. Your job is an INDEPENDENT pass, including verifying that fix.)
- Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json (config + validation targets)
- Main/Features/Execution/AlignmentService.cs + IAlignmentService.cs (the alignment predicate semantics are load-bearing -- see Suspect 1)
- Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs (sibling feature that documents the AreEnemyAlignments Neutral trap)

## Known Suspects -- CONFIRM or DISPUTE each with evidence

1. WAR-GATE NEUTRAL SEMANTICS (already fixed in-session -- verify the FIX). `CaravanTradeService.AllowWartimeTrade` under policy `SameAlignmentAndNeutral` must ALLOW trade (return true) when either faction's `GetKingdomSide` is Neutral, and otherwise only when both sides are equal (Free==Free / Evil==Evil), never across the Free/Evil line. It must NOT call `IAlignmentService.AreEnemyAlignments` (whose semantics treat Neutral as an enemy of everyone -- inverted for this purpose). Confirm the current code resolves `GetKingdomSide` directly and branches on `FactionSide.Neutral`, matching `RecruitmentAlignmentService`. Verify the enum values (None/IgnoreWar/SameAlignmentAndNeutral) and that `None` returns false (vanilla veto kept) and `IgnoreWar` returns true unconditionally.

2. PLAYER-CARAVAN DETECTION INCONSISTENCY. The feature scopes behavior off player caravans when `ApplyToPlayerCaravans` is false, using THREE different idioms for "is this a player caravan":
   - `CaravansCampaignBehavior_CanTradeWith_Patch`: `caravanFaction == Hero.MainHero.MapFaction`
   - `TaomCaravanModel.GetInitialTradeGold`: `owner == Hero.MainHero`
   - `TaomCaravanModel.GetMaxGoldToSpendOnOneItemCategory` + `CaravansCampaignBehavior_CalculateBudgetFactor_Patch`: `caravan.Owner?.Clan == Clan.PlayerClan`
   HYPOTHESIS: a player caravan owned/led by a CLAN COMPANION (not Hero.MainHero) is caught by the MapFaction check and the Clan==PlayerClan check, but NOT by `owner == Hero.MainHero`. So with `ApplyToPlayerCaravans` off, a companion-led player caravan would still get the InitialTradeGold buff (scoping leak). Confirm whether player caravans can be owned by a companion (check `CaravanPartyComponent` / how the player creates a caravan). If so, is `owner == Hero.MainHero` in GetInitialTradeGold too narrow -- should it be `owner?.Clan == Clan.PlayerClan` for consistency with the ApplyToPlayer scope? Note vanilla's own `GetInitialTradeGold` uses `owner == Hero.MainHero` for its +5000 bonus, but that is a different question (bonus size, not feature-scoping). DISPUTE if you find player caravans are always MainHero-owned.

3. SCORE RE-WEIGHT MATH. `CaravanTradeService.ReweightTradeScore` for a land, non-home town computes `multiplier = days / (nearFieldFlattenDays + days)^distanceDecayExponent`, clamps `multiplier` to `maxCompensation`, then `result = rawScore * multiplier`, and if `isJustLeftTown` multiplies by `(1 - antiShuttlePenalty)`. Verify: (a) with defaults (flatten=2, decay=0.5, maxComp=6) this actually compresses the near-town advantage and rewards distance rather than inverting or degenerating; (b) NaN/rejection gates are positive-requirement (`!(rawScore > 0f)` and `!(days > 0f)` return rawScore) so a NaN engine float falls through to vanilla, not into the active branch; (c) naval and home-town both pass through unchanged; (d) no divide-by-zero when `nearFieldFlattenDays + days` could be 0 (can it, given days>0 gate and flatten>=0?).

4. CanTradeWith POSTFIX CORRECTNESS vs VANILLA. Vanilla `CanTradeWith` returns false ONLY when at war, OR (player faction + at-peace + target in `_prohibitedKingdomsForPlayerCaravans`). The postfix: returns early if `__result` already true; returns if `!caravanFaction.IsAtWarWith(targetFaction)` (a peacetime false is the prohibited-kingdom exclusion -- leave it); for the player faction, reads `_prohibitedKingdomsForPlayerCaravans` via cached reflection and returns (does not lift) if the target is prohibited; only then may flip false->true. Confirm it can NEVER (a) re-block an already-allowed pairing, (b) enable trade with a player-prohibited kingdom, or (c) throw (it is try/catch wrapped). Check the reflection field name `_prohibitedKingdomsForPlayerCaravans` and type `List<Kingdom>` against the installed engine.

5. DROPDOWN INDEX -> ENUM MAPPING. `TaomSettings.CaravanWarTradePolicy` is `new Dropdown<string>(new[] { "Vanilla (war blocks)", "Same Side + Neutral", "Ignore War" }, 1)`. `CaravanTradeSettingsProvider.ResolveWarPolicy` maps SelectedIndex 0->None, 1->SameAlignmentAndNeutral, 2->IgnoreWar. Confirm the index-to-enum order matches the label order exactly (an off-by-one silently selects the wrong policy) and the default index 1 matches the JSON default "SameAlignmentAndNeutral". Confirm the JSON-string fallback path (WarTradePolicyParser) covers the same three values and the config validator rejects an unknown string (reverts to default + warns).

6. FAIL-SAFE / MASTER-OFF. Confirm EVERY `ICaravanTradeService` method (ReweightTradeScore, ScaleVeryFarDistance, AllowWartimeTrade, ApplyBudgetFactorFloor, ResolveInitialTradeGold, ResolveMaxGoldPerCategory) returns the vanilla passthrough value when `Enabled` is false, so the MCM master toggle truly restores exact vanilla. Confirm every hook try/catch degrades to vanilla on any exception, and the settings provider `TaomSettings.Instance?.X ?? Cfg.X` never produces non-vanilla behavior when MCM is absent.

7. DOUBLE DISTANCE COMPUTE (perf). `CaravansCampaignBehavior_GetTradeScoreForTown_Patch` re-calls `AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` -- a distance vanilla already computed. TAOM's claim (in the RCA) is that this is cache-backed and cheap, because `DefaultMapDistanceModel.GetDistance(MobileParty, Settlement)` resolves to `_navigationCache.GetSettlementToSettlementDistanceWithLandRatio` (a lookup) plus a couple of Vec2.Distance ops, NOT a live navmesh pathfind. Independently verify this against the installed `DefaultMapDistanceModel` decompile and confirm whether the double-call is an acceptable cost on the destination argmax loop (per caravan, on re-think) or a real hot-path concern.

## VANILLA CODE (installed v1.4.6 -- ground truth)

CaravansCampaignBehavior.GetTradeScoreForTown (land distance factor is num4 = 1/num3):
```
private float GetTradeScoreForTown(MobileParty caravanParty, Town town, CampaignTime lastHomeVisitTimeOfCaravan, float caravanFullness, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isTargetingPort)
{
    bool flag = (isTargetingPort = caravanParty.HasNavalNavigationCapability);
    AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(caravanParty, town.Settlement, isTargetingPort, out bestNavigationType, out var bestNavigationDistance, out var _);
    if (bestNavigationType != MobileParty.NavigationType.None)
    {
        float num = bestNavigationDistance / ((flag ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed : Campaign.Current.EstimatedAverageCaravanPartySpeed) * (float)CampaignTime.HoursInDay);
        // ... veryFarAddition, distanceCut reject, then:
        float num4 = (flag ? MathF.Max(0.1f, 1f - num3 / (2f * distanceLimitVeryFarAsDaysForNavigationType)) : (1f / num3));
        // ... num7 sell score, num8 buy score, num5 home-recency, etc.
        return (num7 + num8) * num4 * num13 * num5 * num9 * num10 * num11 * num12 * num2;
    }
    bestNavigationType = MobileParty.NavigationType.None;
    isTargetingPort = false;
    return -1f;
}
```

CaravansCampaignBehavior.CanTradeWith:
```
private bool CanTradeWith(IFaction caravanFaction, IFaction targetFaction)
{
    if (caravanFaction.IsAtWarWith(targetFaction)) return false;
    if (caravanFaction == Hero.MainHero.MapFaction)
    {
        if (targetFaction is Kingdom item) return !_prohibitedKingdomsForPlayerCaravans.Contains(item);
        return true;
    }
    return true;
}
```

CaravansCampaignBehavior.CalculateBudgetFactor + the field:
```
private List<Kingdom> _prohibitedKingdomsForPlayerCaravans = new List<Kingdom>();
private float CalculateBudgetFactor(MobileParty caravanParty)
{
    return 0.1f + MathF.Clamp((float)caravanParty.PartyTradeGold / 5000f, 0f, 1f);
}
```

CaravansCampaignBehavior.CacheVeryFarDistances + fields:
```
private float _navalCaravanVeryFarCache = -1f;
private float _defaultCaravanVeryFarCache = -1f;
private void CacheVeryFarDistances()
{
    // naval: mult 20f; default(land): mult 5f -- each = avgClosestTwoTowns * mult / (speed * HoursInDay)
    _navalCaravanVeryFarCache = ...;
    _defaultCaravanVeryFarCache = ...;
}
```

DefaultCaravanModel overrides TAOM extends:
```
public override int GetInitialTradeGold(Hero owner, bool navalCaravan, bool largeCaravan)
{
    int num = 10000; int num2 = ((owner == Hero.MainHero) ? 5000 : 0);
    if (largeCaravan) num = 17500;
    return num + num2;
}
public override int GetMaxGoldToSpendOnOneItemCategory(MobileParty caravan, ItemCategory itemCategory) { return 1500; }
public override int MaxNumberOfItemsToBuyFromSingleCategory => 300;
```

Decompile the full `DefaultMapDistanceModel.GetDistance(MobileParty, Settlement, ...)` yourself for Suspect 7. Decompile `CaravanPartyComponent` for Suspect 2 (owner semantics).

## TAOM SOURCE FILES

Feature (Main/Features/CaravanTrade/):
- ICaravanTradeService.cs, CaravanTradeService.cs (pure logic -- the decision surface)
- ICaravanTradeSettingsProvider.cs, CaravanTradeSettingsProvider.cs (MCM-over-JSON merge)
- CaravanTradeConfig.cs (DTO + WarTradePolicyParser), ICaravanTradeConfigProvider.cs, CaravanTradeConfigProvider.cs (validation)
- CaravanTradeIoC.cs
- Hooks/CaravansCampaignBehavior_CanTradeWith_Patch.cs
- Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs
- Hooks/CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs
- Hooks/CaravansCampaignBehavior_CalculateBudgetFactor_Patch.cs

Model + wiring:
- Main/Features/CulturalFeats/Models/TaomCaravanModel.cs (the +2 diversity overrides)
- Main/IoC.cs (CaravanTradeIoC.RegisterCaravanTradeFeature)
- Main/SubModule.cs (Patch59_CaravanTrade category + the TaomCaravanModel ctor injection)
- Main/Features/TaomSettings.cs (Caravan Trade MCM group)

Config: Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json

Tests (TAOM.Tests/Features/CaravanTrade/): CaravanTradeServiceTests.cs, CaravanTradeConfigProviderTests.cs, CaravanTradeBindingTests.cs

## REQUIRED SECTIONS in your output

- VANILLA CODE: decompile `DefaultMapDistanceModel.GetDistance(MobileParty, Settlement)` and `CaravanPartyComponent` owner semantics; paste the relevant blocks.
- KNOWN SUSPECTS: CONFIRMED / DISPUTED for each of the 7 above, with evidence (file:line + vanilla decompile).
- CONFIG CROSS-REFERENCE: verify every field in caravan_trade_config.json is validated + consumed; verify the WarTradePolicy string set matches the parser + validator + dropdown.
- DATA FLOW: JSON -> config -> settings -> service -> hook/model; flag any dead config or unread setting.
- FINDINGS OR OBSERVATIONS: any additional bugs (logic, null, lifecycle, fail-safe, convention) beyond the suspects. If none, say so explicitly -- do not invent findings.

## QUALITY GATES

- Verify claims against the actual installed-engine decompile, not assumptions. If you cannot verify something, mark it UNVERIFIED rather than guessing.
- Do NOT flag vanilla-matching behavior as a TAOM bug.
- Rate each finding severity HIGH / MED / LOW with a concrete failure scenario (inputs -> wrong output).
- It is acceptable and expected to conclude a suspect is DISPUTED / not-a-bug. A clean review is a valid result.

## Prior review lessons

SUCCESSES: config ID cross-ref caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches; the internal deep-review data-flow agent caught the AreEnemyAlignments Neutral inversion here.
FAILURES to avoid: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as a bug; skipping the hard decompile sections; inventing findings to look thorough.

Output your review as a structured markdown report.
