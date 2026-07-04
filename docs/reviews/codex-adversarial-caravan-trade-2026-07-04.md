OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019f2ede-ee31-7a71-bb6f-583c3c663fc2
--------
user
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

ERROR: The process "5668" not found.
codex
I’ll treat this as a review only: inspect the feature, tests, config, and installed decompile, then report findings without editing code. I’m starting with the TAOM sources and the two required vanilla decompile targets.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "class CaravanTradeService|enum CaravanWarTradePolicy|WarTradePolicy|AllowWartimeTrade|ReweightTradeScore|ResolveInitialTradeGold|ApplyBudgetFactorFloor|ScaleVeryFarDistance|ResolveMaxGoldPerCategory|class TaomCaravanModel|CaravanTrade" Main/Features Main/IoC.cs Main/SubModule.cs Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json TAOM.Tests/Features/CaravanTrade docs/features/caravan-trade.md docs/reviews/rca-caravan-trade-2026-07-04.md' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "class AlignmentService|GetKingdomSide|AreEnemyAlignments|class RecruitmentAlignmentService|FactionSide" Main/Features/Execution Main/Features/AlignmentRecruitment' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg -n \"class DefaultMapDistanceModel|GetDistance\\(MobileParty.*Settlement|class CaravanPartyComponent|Owner|Leader|CreateCaravan|CaravanPartyComponent\" E:\\Decompiled_Bannerlord" in C:\Users\mikew\source\repos\TAOM
2026-07-04T20:43:29.998530Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.9 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class AlignmentService|GetKingdomSide|AreEnemyAlignments|class …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-07-04T20:43:30.000149Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.9 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class CaravanTradeService|enum CaravanWarTradePolicy|WarTradeP …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2904ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class CaravanTradeService|enum CaravanWarTradePolicy|WarTradeP …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2906ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class AlignmentService|GetKingdomSide|AreEnemyAlignments|class …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2905ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class DefaultMapDistanceModel|GetDistance\(MobileParty.*Settle …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-07-04T20:43:30.006321Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.9 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class DefaultMapDistanceModel|GetDistance\(MobileParty.*Settle …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
`rg` is not installed in this environment, so I’m switching to PowerShell’s file search. I’ll keep the searches narrower so the decompile tree does not become noisy.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git grep -n -E "class CaravanTradeService|enum CaravanWarTradePolicy|WarTradePolicy|AllowWartimeTrade|ReweightTradeScore|ResolveInitialTradeGold|ApplyBudgetFactorFloor|ScaleVeryFarDistance|ResolveMaxGoldPerCategory|class TaomCaravanModel|CaravanTrade" -- Main/Features Main/IoC.cs Main/SubModule.cs Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json TAOM.Tests/Features/CaravanTrade docs/features/caravan-trade.md docs/reviews/rca-caravan-trade-2026-07-04.md' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git grep -n -E "class AlignmentService|GetKingdomSide|AreEnemyAlignments|class RecruitmentAlignmentService|FactionSide" -- Main/Features/Execution Main/Features/AlignmentRecruitment' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -File -Filter *.cs | Where-Object { "'$_.Name -in @('"'DefaultMapDistanceModel.cs','CaravanPartyComponent.cs','CaravansCampaignBehavior.cs') } | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 568ms:
Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs:8:/// Diplomacy features use). Deliberately does NOT call <see cref="IAlignmentService.AreEnemyAlignments"/>,
Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs:12:public class RecruitmentAlignmentService : IRecruitmentAlignmentService
Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs:32:        var recruiterSide = _alignment.GetKingdomSide(recruiterKingdomId);
Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs:33:        var sourceSide = _alignment.GetKingdomSide(sourceKingdomId);
Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs:35:        if (recruiterSide == FactionSide.Neutral || sourceSide == FactionSide.Neutral)
Main/Features/AlignmentRecruitment/RecruitmentAlignmentService.cs:39:            return recruiterSide == FactionSide.Free && sourceSide == FactionSide.Evil;
Main/Features/Execution/AlignmentService.cs:7:public class AlignmentService : IAlignmentService
Main/Features/Execution/AlignmentService.cs:9:    private readonly Dictionary<string, FactionSide> _kingdomSides;
Main/Features/Execution/AlignmentService.cs:13:        _kingdomSides = new Dictionary<string, FactionSide>(StringComparer.OrdinalIgnoreCase);
Main/Features/Execution/AlignmentService.cs:18:            if (Enum.TryParse<FactionSide>(kvp.Value, ignoreCase: true, out var side))
Main/Features/Execution/AlignmentService.cs:25:                _kingdomSides[kvp.Key] = FactionSide.Neutral;
Main/Features/Execution/AlignmentService.cs:32:    public FactionSide GetKingdomSide(string kingdomId) => GetSide(kingdomId);
Main/Features/Execution/AlignmentService.cs:34:    public FactionSide GetCultureSide(string cultureId) => GetSide(cultureId);
Main/Features/Execution/AlignmentService.cs:36:    private FactionSide GetSide(string id)
Main/Features/Execution/AlignmentService.cs:39:            return FactionSide.Neutral;
Main/Features/Execution/AlignmentService.cs:41:        return _kingdomSides.TryGetValue(id, out var side) ? side : FactionSide.Neutral;
Main/Features/Execution/AlignmentService.cs:44:    public bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB)
Main/Features/Execution/AlignmentService.cs:46:        var sideA = GetKingdomSide(kingdomIdA);
Main/Features/Execution/AlignmentService.cs:47:        var sideB = GetKingdomSide(kingdomIdB);
Main/Features/Execution/AlignmentService.cs:49:        if (sideA == FactionSide.Neutral || sideB == FactionSide.Neutral)
Main/Features/Execution/AlignmentService.cs:57:        var sideA = GetKingdomSide(kingdomIdA);
Main/Features/Execution/AlignmentService.cs:58:        var sideB = GetKingdomSide(kingdomIdB);
Main/Features/Execution/AlignmentService.cs:60:        if (sideA == FactionSide.Neutral || sideB == FactionSide.Neutral)
Main/Features/Execution/ExecutionRelationService.cs:50:        bool crossAlignment = _alignmentService.AreEnemyAlignments(executorKingdomId, victimKingdomId);
Main/Features/Execution/FactionSide.cs:3:public enum FactionSide
Main/Features/Execution/Hooks/ExecutionActionHook.cs:15:        return !_alignmentService.AreEnemyAlignments(executorKingdomId, victimKingdomId);
Main/Features/Execution/Hooks/ExecutionActionHook.cs:25:        bool crossAlignment = _alignmentService.AreEnemyAlignments(executorKingdomId, victimKingdomId);
Main/Features/Execution/IAlignmentService.cs:5:    FactionSide GetKingdomSide(string kingdomId);
Main/Features/Execution/IAlignmentService.cs:9:    /// <see cref="GetKingdomSide"/>. Most TAOM culture ids equal their kingdom id (or appear in the
Main/Features/Execution/IAlignmentService.cs:15:    FactionSide GetCultureSide(string cultureId);
Main/Features/Execution/IAlignmentService.cs:17:    bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB);

 succeeded in 687ms:
Main/Features/CulturalFeats/Models/TaomCaravanModel.cs:1:using TAOM.Features.CaravanTrade;
Main/Features/CulturalFeats/Models/TaomCaravanModel.cs:9:public class TaomCaravanModel : DefaultCaravanModel
Main/Features/CulturalFeats/Models/TaomCaravanModel.cs:12:    private readonly ICaravanTradeService _caravanTrade;
Main/Features/CulturalFeats/Models/TaomCaravanModel.cs:14:    public TaomCaravanModel(ICulturalFeatsService feats, ICaravanTradeService caravanTrade)
Main/Features/CulturalFeats/Models/TaomCaravanModel.cs:25:    // CaravanTrade basket-diversity levers (delegate to the pure service; vanilla when the feature is off).
Main/Features/CulturalFeats/Models/TaomCaravanModel.cs:27:        => _caravanTrade.ResolveInitialTradeGold(
Main/Features/CulturalFeats/Models/TaomCaravanModel.cs:32:        => _caravanTrade.ResolveMaxGoldPerCategory(
Main/Features/TaomSettings.cs:50:    public bool EnableCaravanTrade { get; set; } = true;
Main/Features/TaomSettings.cs:55:    public bool CaravanTradeApplyToPlayer { get; set; } = true;
Main/Features/TaomSettings.cs:65:    public Dropdown<string> CaravanWarTradePolicy { get; set; } = new Dropdown<string>(
Main/IoC.cs:41:using TAOM.Features.CaravanTrade;
Main/IoC.cs:116:        CaravanTradeIoC.RegisterCaravanTradeFeature(container);
Main/SubModule.cs:228:        // Patch59: CaravanTrade — four postfixes on CaravansCampaignBehavior private methods
Main/SubModule.cs:232:        _harmony.PatchCategory("Patch59_CaravanTrade");
Main/SubModule.cs:519:        campaignStarter.AddModel(new TaomCaravanModel(culturalFeats, IoC.Resolve<TAOM.Features.CaravanTrade.ICaravanTradeService>()));

 succeeded in 1000ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapDistanceModel.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs
E:\Decompiled_Bannerlord\_categories_v1.4.5\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs
E:\Decompiled_Bannerlord\_categories_v1.4.5\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMapDistanceModel.cs
E:\Decompiled_Bannerlord\_categories_v1.4.5\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs

codex
The first repo search only saw tracked matches, so I’m checking the working tree directly as well. That matters here because review targets may be uncommitted.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Features\\CaravanTrade -Recurse -File | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path TAOM.Tests\\Features\\CaravanTrade -Recurse -File | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
 succeeded in 649ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\CaravanTradeConfig.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\CaravanTradeIoC.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\CaravanTradeService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\ICaravanTradeConfigProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\ICaravanTradeService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CalculateBudgetFactor_Patch.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CanTradeWith_Patch.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs

 succeeded in 513ms:
 M CHANGELOG.md
 M Main/Features/CulturalFeats/Models/TaomCaravanModel.cs
 M Main/Features/TaomSettings.cs
 M Main/IoC.cs
 M Main/SubModule.cs
 M docs/reviews/LESSONS-LEARNED.md
?? Main/Features/CaravanTrade/
?? Main/_Module/ModuleData/caravan_trade/
?? TAOM.Tests/Features/CaravanTrade/
?? docs/features/caravan-trade.md
?? docs/reviews/codex-adversarial-caravan-trade-2026-07-04.md
?? docs/reviews/codex-adversarial-caravan-trade-2026-07-04.prompt.md
?? docs/reviews/rca-caravan-trade-2026-07-04.md
warning: could not open directory '.pytest_cache/': Permission denied

 succeeded in 630ms:
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CaravanTrade\CaravanTradeBindingTests.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs

codex
The CaravanTrade feature is untracked plus a few modified wiring files, so I’ll review the working tree rather than only committed content. Next I’m reading the core service, config/provider, hooks, model, and tests with line numbers for precise citations.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\CaravanTradeService.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\CaravanTradeConfig.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\CaravanTradeConfigProvider.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\ICaravanTradeService.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\CaravanTradeSettingsProvider.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 834ms:
   1: using System;
   2: using TAOM.Core.Logging;
   3: using TAOM.Core.Validation;
   4: using TAOM.Features.Execution;
   5: 
   6: namespace TAOM.Features.CaravanTrade;
   7: 
   8: /// <summary>
   9: /// Pure decision logic for the CaravanTrade feature. All four Harmony hooks plus the caravan
  10: /// GameModel delegate here; no TaleWorlds types cross the boundary. See <see cref="ICaravanTradeService"/>.
  11: /// </summary>
  12: public class CaravanTradeService : ICaravanTradeService
  13: {
  14:     private readonly ICaravanTradeSettingsProvider _settings;
  15:     private readonly IAlignmentService _alignment;
  16:     private readonly IModLogger _logger;
  17: 
  18:     public CaravanTradeService(ICaravanTradeSettingsProvider settings, IAlignmentService alignment, IModLogger logger)
  19:     {
  20:         _settings = settings;
  21:         _alignment = alignment;
  22:         _logger = logger;
  23:     }
  24: 
  25:     public float ReweightTradeScore(float rawScore, float days, bool isNaval, bool isHomeTown, bool isJustLeftTown, bool isPlayerCaravan)
  26:     {
  27:         if (!IsActiveFor(isPlayerCaravan))
  28:             return rawScore;
  29: 
  30:         // Positive-requirement gate: NaN rawScore and vanilla rejections (<= 0) pass through untouched.
  31:         if (!(rawScore > 0f))
  32:             return rawScore;
  33: 
  34:         // Naval uses a different vanilla distance factor (not 1/days); the shuttle is a land problem.
  35:         // Home has its own vanilla return-pull tuning we must not disturb.
  36:         if (isNaval || isHomeTown)
  37:             return rawScore;
  38: 
  39:         // Positive-requirement gate: NaN or non-positive days -> vanilla.
  40:         if (!(days > 0f))
  41:             return rawScore;
  42: 
  43:         // Strip vanilla's land 1/days spike and re-apply a gentler curve:
  44:         //   newScore = rawScore * days / (nearFieldFlatten + days)^alpha
  45:         // For an equal base profit P0 (rawScore = P0/days) this is P0 / (flatten+days)^alpha, so near
  46:         // towns lose their runaway advantage and the built-in profit estimate becomes the differentiator.
  47:         double denom = Math.Pow(_settings.NearFieldFlattenDays + days, _settings.DistanceDecayExponent);
  48:         float multiplier = denom > 0d ? (float)(days / denom) : 1f;
  49: 
  50:         float maxComp = _settings.MaxCompensation;
  51:         if (multiplier > maxComp)
  52:             multiplier = maxComp;
  53: 
  54:         float result = rawScore * multiplier;
  55: 
  56:         if (isJustLeftTown)
  57:             result *= 1f - _settings.AntiShuttlePenalty;
  58: 
  59:         return result;
  60:     }
  61: 
  62:     public float ScaleVeryFarDistance(float vanillaVeryFarDays)
  63:     {
  64:         // Applied globally (the vanilla cache is a single shared field, not per-caravan) — it only
  65:         // widens the candidate set; the re-weight and war gate remain player-scoped.
  66:         if (!_settings.Enabled)
  67:             return vanillaVeryFarDays;
  68:         return vanillaVeryFarDays * _settings.RangeMultiplier;
  69:     }
  70: 
  71:     public bool AllowWartimeTrade(string caravanFactionId, string targetFactionId, bool isPlayerCaravan)
  72:     {
  73:         // false = keep the vanilla war veto; true = lift it for this pairing.
  74:         if (!IsActiveFor(isPlayerCaravan))
  75:             return false;
  76: 
  77:         switch (_settings.WarTradePolicy)
  78:         {
  79:             case WarTradePolicy.IgnoreWar:
  80:                 return true;
  81:             case WarTradePolicy.SameAlignmentAndNeutral:
  82:                 // Resolve sides directly — do NOT use IAlignmentService.AreEnemyAlignments, whose Neutral
  83:                 // semantics are inverted for this purpose (it treats Neutral as an enemy of everyone). Here
  84:                 // Neutral on either side is a mercantile "trade with anyone", and otherwise only the same
  85:                 // side trades (Free↔Free / Evil↔Evil), never across the Free/Evil line. Mirrors the sibling
  86:                 // AlignmentRecruitment feature's deliberate work-around.
  87:                 var caravanSide = _alignment.GetKingdomSide(caravanFactionId);
  88:                 var targetSide = _alignment.GetKingdomSide(targetFactionId);
  89:                 if (caravanSide == FactionSide.Neutral || targetSide == FactionSide.Neutral)
  90:                     return true;
  91:                 return caravanSide == targetSide;
  92:             case WarTradePolicy.None:
  93:             default:
  94:                 return false;
  95:         }
  96:     }
  97: 
  98:     public float ApplyBudgetFactorFloor(float vanillaBudgetFactor, bool isPlayerCaravan)
  99:     {
 100:         if (!IsActiveFor(isPlayerCaravan))
 101:             return vanillaBudgetFactor;
 102: 
 103:         // Engine-sourced float: defer to vanilla on garbage rather than emit a corrupted floor.
 104:         if (!FiniteFloatValidator.IsFinite(vanillaBudgetFactor))
 105:             return vanillaBudgetFactor;
 106: 
 107:         return Math.Max(vanillaBudgetFactor, _settings.BudgetFactorFloor);
 108:     }
 109: 
 110:     public int ResolveInitialTradeGold(int vanillaValue, bool isPlayerCaravan)
 111:     {
 112:         if (!IsActiveFor(isPlayerCaravan))
 113:             return vanillaValue;
 114:         // Never lower — preserve vanilla's large-caravan / main-hero bonuses.
 115:         return Math.Max(vanillaValue, _settings.InitialTradeGold);
 116:     }
 117: 
 118:     public int ResolveMaxGoldPerCategory(int vanillaValue, bool isPlayerCaravan)
 119:     {
 120:         if (!IsActiveFor(isPlayerCaravan))
 121:             return vanillaValue;
 122:         return _settings.MaxGoldPerCategory;
 123:     }
 124: 
 125:     private bool IsActiveFor(bool isPlayerCaravan)
 126:     {
 127:         if (!_settings.Enabled)
 128:             return false;
 129:         if (isPlayerCaravan && !_settings.ApplyToPlayerCaravans)
 130:             return false;
 131:         return true;
 132:     }
 133: }

 succeeded in 763ms:
   1: namespace TAOM.Features.CaravanTrade;
   2: 
   3: /// <summary>
   4: /// JSON DTO for <c>caravan_trade/caravan_trade_config.json</c>. Defaults are the shipped tuning.
   5: /// Validated field-by-field by <see cref="CaravanTradeConfigProvider"/>; the MCM layer overrides a
   6: /// subset at runtime via <see cref="ICaravanTradeSettingsProvider"/>.
   7: /// </summary>
   8: public class CaravanTradeConfig
   9: {
  10:     public bool Enabled { get; set; } = true;
  11:     public bool ApplyToPlayerCaravans { get; set; } = true;
  12: 
  13:     // Range envelope + selection re-weight (Levers 2 & 3).
  14:     public float RangeMultiplier { get; set; } = 1.6f;
  15:     public float DistanceDecayExponent { get; set; } = 0.5f;
  16:     public float NearFieldFlattenDays { get; set; } = 2.0f;
  17:     public float MaxCompensation { get; set; } = 6.0f;
  18:     public float AntiShuttlePenalty { get; set; } = 0.35f;
  19: 
  20:     // War policy (Lever 1). Validated against the known set; unknown reverts to the default.
  21:     public string WarTradePolicy { get; set; } = "SameAlignmentAndNeutral";
  22: 
  23:     // Basket diversity (Lever 4).
  24:     public float BudgetFactorFloor { get; set; } = 0.35f;
  25:     public int InitialTradeGold { get; set; } = 15000;
  26:     public int MaxGoldPerCategory { get; set; } = 1500;
  27: }
  28: 
  29: /// <summary>Parsing + known-set validation for the <see cref="WarTradePolicy"/> config string (the M1 typo trap).</summary>
  30: public static class WarTradePolicyParser
  31: {
  32:     /// <summary>Case-insensitively parse a config string into a <see cref="WarTradePolicy"/>. Returns false for null/empty/unknown.</summary>
  33:     public static bool TryParse(string value, out WarTradePolicy policy)
  34:     {
  35:         policy = WarTradePolicy.SameAlignmentAndNeutral;
  36:         if (string.IsNullOrWhiteSpace(value))
  37:             return false;
  38: 
  39:         switch (value.Trim().ToLowerInvariant())
  40:         {
  41:             case "none":
  42:                 policy = WarTradePolicy.None;
  43:                 return true;
  44:             case "ignorewar":
  45:                 policy = WarTradePolicy.IgnoreWar;
  46:                 return true;
  47:             case "samealignmentandneutral":
  48:                 policy = WarTradePolicy.SameAlignmentAndNeutral;
  49:                 return true;
  50:             default:
  51:                 return false;
  52:         }
  53:     }
  54: }

 succeeded in 767ms:
   1: namespace TAOM.Features.CaravanTrade;
   2: 
   3: /// <summary>
   4: /// How AI/player caravans should treat the war state when choosing a trade town.
   5: /// TAOM is endless-war-by-default (Free-vs-Evil), so the vanilla "no trade with any
   6: /// faction you're at war with" veto collapses each caravan's reachable set to its own
   7: /// side and forces the local shuttle. This policy relaxes that veto.
   8: /// </summary>
   9: public enum WarTradePolicy
  10: {
  11:     /// <summary>Vanilla behavior — war blocks trade. The feature makes no war-gate change.</summary>
  12:     None,
  13: 
  14:     /// <summary>Lift the war veto entirely — caravans trade at any non-besieged town regardless of war.</summary>
  15:     IgnoreWar,
  16: 
  17:     /// <summary>
  18:     /// Lift the war veto only between non-enemy alignments — same side (Free↔Free, Evil↔Evil) or
  19:     /// any pairing involving a Neutral faction. A Free caravan reaches other Free/neutral towns but
  20:     /// not Evil towns. Default. Resolved via <see cref="Execution.IAlignmentService.AreEnemyAlignments"/>.
  21:     /// </summary>
  22:     SameAlignmentAndNeutral,
  23: }
  24: 
  25: /// <summary>
  26: /// Pure decision surface for the CaravanTrade feature. No TaleWorlds types cross this boundary —
  27: /// the Harmony postfixes and the caravan GameModel extract primitives and delegate here (ADR-002/007).
  28: /// Every method short-circuits to the vanilla value when the feature is disabled (or when it's a
  29: /// player caravan and player-scoping is off), so master-off restores exact vanilla behavior.
  30: /// </summary>
  31: public interface ICaravanTradeService
  32: {
  33:     /// <summary>
  34:     /// Re-weight vanilla's trade-destination score to stop the closest-town-always-wins shuttle and
  35:     /// make longer viable trips competitive. Strips vanilla's land <c>1/days</c> distance spike and
  36:     /// re-applies a gentler <c>1/(nearFieldFlatten + days)^decayExponent</c> curve, clamped by
  37:     /// <c>maxCompensation</c>; near-equal-distance towns become near-tied so the built-in profit
  38:     /// estimate (which passes through untouched) decides. Naval and the home settlement are returned
  39:     /// unchanged (naval uses a different vanilla factor; home has its own return-pull tuning).
  40:     /// </summary>
  41:     /// <param name="rawScore">Vanilla's <c>GetTradeScoreForTown</c> result. Values ≤ 0 (rejections) pass through.</param>
  42:     /// <param name="days">Raw travel time in days (vanilla's <c>num</c>), recomputed from the same public inputs.</param>
  43:     /// <param name="isNaval">Caravan has naval capability (uses vanilla's different naval distance factor).</param>
  44:     /// <param name="isHomeTown">Candidate is the caravan's home settlement (skip re-weight).</param>
  45:     /// <param name="isJustLeftTown">Candidate is the town the caravan just left and is not home (anti-shuttle penalty).</param>
  46:     /// <param name="isPlayerCaravan">Caravan is player-owned (scoped off when player-application is disabled).</param>
  47:     float ReweightTradeScore(float rawScore, float days, bool isNaval, bool isHomeTown, bool isJustLeftTown, bool isPlayerCaravan);
  48: 
  49:     /// <summary>
  50:     /// Scale the vanilla "very far" distance ceiling so profitable distant towns aren't hard-rejected.
  51:     /// The vanilla cache is a single shared field (not per-caravan), so this is applied globally when
  52:     /// the feature is enabled — it only widens the candidate set; the re-weight and war gate remain
  53:     /// player-scoped. Returns the vanilla value unchanged when disabled.
  54:     /// </summary>
  55:     float ScaleVeryFarDistance(float vanillaVeryFarDays);
  56: 
  57:     /// <summary>
  58:     /// Whether to lift the vanilla war veto for this caravan→town faction pairing. Returns
  59:     /// <c>false</c> to keep the vanilla veto (the caller leaves <c>__result</c> false); <c>true</c>
  60:     /// to allow trade despite the war, per the configured <see cref="WarTradePolicy"/>.
  61:     /// </summary>
  62:     bool AllowWartimeTrade(string caravanFactionId, string targetFactionId, bool isPlayerCaravan);
  63: 
  64:     /// <summary>
  65:     /// Raise vanilla's per-caravan <c>budgetFactor</c> to at least the configured floor so even a
  66:     /// poor caravan clears the per-category buy-value gate on more than one category (the direct
  67:     /// fix for "caravans only buy one item"). Returns the vanilla value unchanged when disabled or
  68:     /// non-finite.
  69:     /// </summary>
  70:     float ApplyBudgetFactorFloor(float vanillaBudgetFactor, bool isPlayerCaravan);
  71: 
  72:     /// <summary>
  73:     /// Resolve the caravan's starting trade gold. A higher floor saturates vanilla's
  74:     /// <c>budgetFactor = 0.1 + clamp(gold/5000)</c>, letting more categories clear the buy gate.
  75:     /// Never lowers the vanilla value (preserves the large/main-hero bonuses). Vanilla when disabled.
  76:     /// </summary>
  77:     int ResolveInitialTradeGold(int vanillaValue, bool isPlayerCaravan);
  78: 
  79:     /// <summary>
  80:     /// Resolve the per-item-category gold cap. Defaults to vanilla; exposed for tuning. Vanilla when disabled.
  81:     /// </summary>
  82:     int ResolveMaxGoldPerCategory(int vanillaValue, bool isPlayerCaravan);
  83: }

 succeeded in 771ms:
   1: namespace TAOM.Features.CaravanTrade;
   2: 
   3: /// <summary>
   4: /// Merges MCM over the validated JSON config. MCM-exposed fields read <c>TaomSettings.Instance?.X</c>
   5: /// and fall back to the JSON config (which is the default source + holds the advanced, JSON-only
   6: /// knobs). MCM slider bounds mirror the JSON validation bounds, so an MCM value can't escape the
   7: /// validated range (the "both surfaces" invariant). The war policy resolves from the MCM dropdown
   8: /// index, falling back to the validated JSON string.
   9: /// </summary>
  10: public class CaravanTradeSettingsProvider : ICaravanTradeSettingsProvider
  11: {
  12:     private readonly ICaravanTradeConfigProvider _configProvider;
  13: 
  14:     public CaravanTradeSettingsProvider(ICaravanTradeConfigProvider configProvider)
  15:     {
  16:         _configProvider = configProvider;
  17:     }
  18: 
  19:     private CaravanTradeConfig Cfg => _configProvider.GetConfig();
  20: 
  21:     public bool Enabled => TaomSettings.Instance?.EnableCaravanTrade ?? Cfg.Enabled;
  22:     public bool ApplyToPlayerCaravans => TaomSettings.Instance?.CaravanTradeApplyToPlayer ?? Cfg.ApplyToPlayerCaravans;
  23:     public float RangeMultiplier => TaomSettings.Instance?.CaravanRangeMultiplier ?? Cfg.RangeMultiplier;
  24: 
  25:     // JSON-only advanced curve knobs.
  26:     public float DistanceDecayExponent => Cfg.DistanceDecayExponent;
  27:     public float NearFieldFlattenDays => Cfg.NearFieldFlattenDays;
  28:     public float MaxCompensation => Cfg.MaxCompensation;
  29:     public float AntiShuttlePenalty => Cfg.AntiShuttlePenalty;
  30: 
  31:     public WarTradePolicy WarTradePolicy => ResolveWarPolicy();
  32:     public float BudgetFactorFloor => TaomSettings.Instance?.CaravanBudgetDiversityFloor ?? Cfg.BudgetFactorFloor;
  33: 
  34:     // JSON-only.
  35:     public int InitialTradeGold => Cfg.InitialTradeGold;
  36:     public int MaxGoldPerCategory => Cfg.MaxGoldPerCategory;
  37: 
  38:     private WarTradePolicy ResolveWarPolicy()
  39:     {
  40:         var dropdown = TaomSettings.Instance?.CaravanWarTradePolicy;
  41:         if (dropdown != null)
  42:         {
  43:             switch (dropdown.SelectedIndex)
  44:             {
  45:                 case 0: return WarTradePolicy.None;
  46:                 case 1: return WarTradePolicy.SameAlignmentAndNeutral;
  47:                 case 2: return WarTradePolicy.IgnoreWar;
  48:             }
  49:         }
  50: 
  51:         // Fall back to the validated JSON string (already normalized to the known set by the provider).
  52:         return WarTradePolicyParser.TryParse(Cfg.WarTradePolicy, out var policy)
  53:             ? policy
  54:             : WarTradePolicy.SameAlignmentAndNeutral;
  55:     }
  56: }

 succeeded in 785ms:
   1: using System;
   2: using System.IO;
   3: using Newtonsoft.Json;
   4: using TAOM.Core.Infrastructure;
   5: using TAOM.Core.Logging;
   6: using TAOM.Core.Validation;
   7: 
   8: namespace TAOM.Features.CaravanTrade;
   9: 
  10: /// <summary>
  11: /// Loads + validates <c>caravan_trade/caravan_trade_config.json</c>. Every numeric field is
  12: /// range-checked (NaN/Infinity rejected via <see cref="FiniteFloatValidator"/>); the war-policy
  13: /// string is validated against the known set (the M1 typo trap); invalid values revert to the
  14: /// shipped default with a warning, and the master toggle stays effective. Cached (Reuse.Singleton) —
  15: /// config edits require an app restart.
  16: /// </summary>
  17: public class CaravanTradeConfigProvider : ICaravanTradeConfigProvider
  18: {
  19:     private readonly IPathService _pathService;
  20:     private readonly IModLogger _logger;
  21:     private readonly Lazy<CaravanTradeConfig> _config;
  22: 
  23:     public CaravanTradeConfigProvider(IPathService pathService, IModLogger logger)
  24:     {
  25:         _pathService = pathService;
  26:         _logger = logger;
  27:         _config = new Lazy<CaravanTradeConfig>(LoadConfig);
  28:     }
  29: 
  30:     public CaravanTradeConfig GetConfig() => _config.Value;
  31: 
  32:     private CaravanTradeConfig LoadConfig()
  33:     {
  34:         var path = Path.Combine(_pathService.ModuleDataPath, "caravan_trade", "caravan_trade_config.json");
  35: 
  36:         if (!File.Exists(path))
  37:         {
  38:             _logger.LogWarning($"CaravanTradeConfigProvider: caravan_trade_config.json not found at {path}, using defaults");
  39:             return new CaravanTradeConfig();
  40:         }
  41: 
  42:         CaravanTradeConfig parsed;
  43:         try
  44:         {
  45:             var json = File.ReadAllText(path);
  46:             parsed = JsonConvert.DeserializeObject<CaravanTradeConfig>(json) ?? new CaravanTradeConfig();
  47:         }
  48:         catch (Exception ex)
  49:         {
  50:             _logger.LogError($"CaravanTradeConfigProvider: Failed to parse caravan_trade_config.json: {ex.Message}");
  51:             return new CaravanTradeConfig();
  52:         }
  53: 
  54:         return Validate(parsed);
  55:     }
  56: 
  57:     private CaravanTradeConfig Validate(CaravanTradeConfig parsed)
  58:     {
  59:         var defaults = new CaravanTradeConfig();
  60:         var c = new CaravanTradeConfig
  61:         {
  62:             Enabled = parsed.Enabled,
  63:             ApplyToPlayerCaravans = parsed.ApplyToPlayerCaravans,
  64:             RangeMultiplier = parsed.RangeMultiplier,
  65:             DistanceDecayExponent = parsed.DistanceDecayExponent,
  66:             NearFieldFlattenDays = parsed.NearFieldFlattenDays,
  67:             MaxCompensation = parsed.MaxCompensation,
  68:             AntiShuttlePenalty = parsed.AntiShuttlePenalty,
  69:             WarTradePolicy = parsed.WarTradePolicy,
  70:             BudgetFactorFloor = parsed.BudgetFactorFloor,
  71:             InitialTradeGold = parsed.InitialTradeGold,
  72:             MaxGoldPerCategory = parsed.MaxGoldPerCategory,
  73:         };
  74: 
  75:         var rejected = false;
  76: 
  77:         // Below 1 would shrink the range below vanilla (worse shuttle); above 4 lets caravans wander map-wide.
  78:         if (!FiniteFloatValidator.IsFiniteInRange(c.RangeMultiplier, 1f, 4f))
  79:         {
  80:             _logger.LogWarning($"CaravanTradeConfigProvider: rangeMultiplier={c.RangeMultiplier} must be finite in [1,4], reverting to {defaults.RangeMultiplier}");
  81:             c.RangeMultiplier = defaults.RangeMultiplier;
  82:             rejected = true;
  83:         }
  84: 
  85:         // Shape only: 0/negative degenerates the curve; > 4 is a near-vanilla steep decay.
  86:         if (!FiniteFloatValidator.IsFiniteInRange(c.DistanceDecayExponent, 0.25f, 4f))
  87:         {
  88:             _logger.LogWarning($"CaravanTradeConfigProvider: distanceDecayExponent={c.DistanceDecayExponent} must be finite in [0.25,4], reverting to {defaults.DistanceDecayExponent}");
  89:             c.DistanceDecayExponent = defaults.DistanceDecayExponent;
  90:             rejected = true;
  91:         }
  92: 
  93:         if (!FiniteFloatValidator.IsFiniteInRange(c.NearFieldFlattenDays, 0f, 20f))
  94:         {
  95:             _logger.LogWarning($"CaravanTradeConfigProvider: nearFieldFlattenDays={c.NearFieldFlattenDays} must be finite in [0,20], reverting to {defaults.NearFieldFlattenDays}");
  96:             c.NearFieldFlattenDays = defaults.NearFieldFlattenDays;
  97:             rejected = true;
  98:         }
  99: 
 100:         // Must be ≥ 1 (a cap below 1 would suppress every score); ≤ 20 keeps far towns from dominating.
 101:         if (!FiniteFloatValidator.IsFiniteInRange(c.MaxCompensation, 1f, 20f))
 102:         {
 103:             _logger.LogWarning($"CaravanTradeConfigProvider: maxCompensation={c.MaxCompensation} must be finite in [1,20], reverting to {defaults.MaxCompensation}");
 104:             c.MaxCompensation = defaults.MaxCompensation;
 105:             rejected = true;
 106:         }
 107: 
 108:         // A fraction. > 1 would flip the score sign; < 0 would reward returning to the just-left town.
 109:         if (!FiniteFloatValidator.IsFiniteInRange(c.AntiShuttlePenalty, 0f, 1f))
 110:         {
 111:             _logger.LogWarning($"CaravanTradeConfigProvider: antiShuttlePenalty={c.AntiShuttlePenalty} must be finite in [0,1], reverting to {defaults.AntiShuttlePenalty}");
 112:             c.AntiShuttlePenalty = defaults.AntiShuttlePenalty;
 113:             rejected = true;
 114:         }
 115: 
 116:         // M1 string-branch trap: an unknown/typo policy must revert, not silently take the switch default.
 117:         if (!WarTradePolicyParser.TryParse(c.WarTradePolicy, out _))
 118:         {
 119:             _logger.LogWarning($"CaravanTradeConfigProvider: warTradePolicy='{c.WarTradePolicy}' is not one of None/IgnoreWar/SameAlignmentAndNeutral, reverting to {defaults.WarTradePolicy}");
 120:             c.WarTradePolicy = defaults.WarTradePolicy;
 121:             rejected = true;
 122:         }
 123: 
 124:         if (!FiniteFloatValidator.IsFiniteInRange(c.BudgetFactorFloor, 0f, 1f))
 125:         {
 126:             _logger.LogWarning($"CaravanTradeConfigProvider: budgetFactorFloor={c.BudgetFactorFloor} must be finite in [0,1], reverting to {defaults.BudgetFactorFloor}");
 127:             c.BudgetFactorFloor = defaults.BudgetFactorFloor;
 128:             rejected = true;
 129:         }
 130: 
 131:         if (c.InitialTradeGold < 1000 || c.InitialTradeGold > 100000)
 132:         {
 133:             _logger.LogWarning($"CaravanTradeConfigProvider: initialTradeGold={c.InitialTradeGold} must be in [1000,100000], reverting to {defaults.InitialTradeGold}");
 134:             c.InitialTradeGold = defaults.InitialTradeGold;
 135:             rejected = true;
 136:         }
 137: 
 138:         if (c.MaxGoldPerCategory < 100 || c.MaxGoldPerCategory > 20000)
 139:         {
 140:             _logger.LogWarning($"CaravanTradeConfigProvider: maxGoldPerCategory={c.MaxGoldPerCategory} must be in [100,20000], reverting to {defaults.MaxGoldPerCategory}");
 141:             c.MaxGoldPerCategory = defaults.MaxGoldPerCategory;
 142:             rejected = true;
 143:         }
 144: 
 145:         if (rejected)
 146:             _logger.LogWarning("CaravanTradeConfigProvider: caravan_trade_config.json contained invalid values. See prior warnings for details.");
 147:         else
 148:             _logger.LogInfo("CaravanTradeConfigProvider: Loaded caravan_trade_config.json");
 149: 
 150:         return c;
 151:     }
 152: }

codex
The service fix for Neutral handling is visible: it calls `GetKingdomSide` and branches on `Neutral` directly. One stale interface comment still claims the policy is resolved through `AreEnemyAlignments`; I’m keeping that in mind as a possible low-severity doc drift, not a logic bug.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\Hooks\\CaravansCampaignBehavior_CanTradeWith_Patch.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\Hooks\\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CulturalFeats\\Models\\TaomCaravanModel.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\Hooks\\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\Hooks\\CaravansCampaignBehavior_CalculateBudgetFactor_Patch.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 809ms:
   1: using System;
   2: using System.Collections.Generic;
   3: using HarmonyLib;
   4: using TAOM.Features.CaravanTrade;
   5: using TaleWorlds.CampaignSystem;
   6: using TaleWorlds.CampaignSystem.CampaignBehaviors;
   7: using TaleWorlds.Core;
   8: 
   9: namespace TAOM.Features.CaravanTrade.Hooks;
  10: 
  11: /// <summary>
  12: /// Lever 1 (highest-impact for TAOM): lift the vanilla caravan war veto so caravans can range beyond
  13: /// their own faction's clustered towns in the endless Free-vs-Evil war. Vanilla <c>CanTradeWith</c>
  14: /// returns <c>false</c> for any at-war pairing; this one method feeds BOTH the destination filter
  15: /// (<c>FindNextDestinationForCaravan</c>) and the mid-route abandon (<c>HourlyTickParty</c>), so a
  16: /// single postfix covers both.
  17: ///
  18: /// The postfix only ever flips a war-caused <c>false</c> to <c>true</c> (per the configured policy):
  19: /// a <c>false</c> when NOT at war is the player's prohibited-kingdom exclusion, which we respect, and
  20: /// the player's exclusion is honored even during war (vanilla short-circuits it on the war check).
  21: /// </summary>
  22: [HarmonyPatch(typeof(CaravansCampaignBehavior), "CanTradeWith")]
  23: [HarmonyPatchCategory("Patch59_CaravanTrade")]
  24: public static class CaravansCampaignBehavior_CanTradeWith_Patch
  25: {
  26:     private static ICaravanTradeService _service;
  27:     private static System.Reflection.FieldInfo _prohibitedField;
  28:     private static bool _prohibitedFieldResolved;
  29: 
  30:     [HarmonyPostfix]
  31:     public static void Postfix(ref bool __result, IFaction caravanFaction, IFaction targetFaction, CaravansCampaignBehavior __instance)
  32:     {
  33:         if (__result) return; // already allowed — never re-block
  34:         if (caravanFaction == null || targetFaction == null) return;
  35: 
  36:         try
  37:         {
  38:             // A false when NOT at war is the player's prohibited-kingdom exclusion — respect it, don't lift.
  39:             if (!caravanFaction.IsAtWarWith(targetFaction)) return;
  40: 
  41:             bool isPlayer = Hero.MainHero != null && caravanFaction == Hero.MainHero.MapFaction;
  42: 
  43:             // Honor the player's explicit prohibited-kingdom list even during war (vanilla only checks it at peace).
  44:             if (isPlayer && IsProhibitedForPlayer(__instance, targetFaction)) return;
  45: 
  46:             _service ??= IoC.Resolve<ICaravanTradeService>();
  47:             if (_service.AllowWartimeTrade(caravanFaction.StringId, targetFaction.StringId, isPlayer))
  48:                 __result = true;
  49:         }
  50:         catch (Exception)
  51:         {
  52:             // IoC not initialized / feature not started — degrade gracefully to vanilla.
  53:         }
  54:     }
  55: 
  56:     private static bool IsProhibitedForPlayer(CaravansCampaignBehavior instance, IFaction targetFaction)
  57:     {
  58:         if (!(targetFaction is Kingdom kingdom)) return false;
  59: 
  60:         if (!_prohibitedFieldResolved)
  61:         {
  62:             _prohibitedField = AccessTools.Field(typeof(CaravansCampaignBehavior), "_prohibitedKingdomsForPlayerCaravans");
  63:             _prohibitedFieldResolved = true;
  64:         }
  65: 
  66:         return _prohibitedField?.GetValue(instance) is List<Kingdom> list && list.Contains(kingdom);
  67:     }
  68: }

 succeeded in 805ms:
   1: using System;
   2: using HarmonyLib;
   3: using Helpers;
   4: using TAOM.Features.CaravanTrade;
   5: using TaleWorlds.CampaignSystem;
   6: using TaleWorlds.CampaignSystem.CampaignBehaviors;
   7: using TaleWorlds.CampaignSystem.Party;
   8: using TaleWorlds.CampaignSystem.Settlements;
   9: 
  10: namespace TAOM.Features.CaravanTrade.Hooks;
  11: 
  12: /// <summary>
  13: /// Lever 2: re-weight the vanilla caravan destination score so caravans stop shuttling between the
  14: /// nearest two towns and range to the profitable far ones. Vanilla folds a <c>1/days</c> distance
  15: /// spike into the score (a town twice as far scores ~half); this postfix recomputes the raw travel
  16: /// days from the SAME public inputs vanilla used (<c>AiHelper</c> + the caravan speed props), strips
  17: /// that spike, and re-applies a gentler curve via the pure service — plus an anti-shuttle cut on the
  18: /// town just left. Selection-only; profit and payout are untouched. Naval + home pass through.
  19: /// </summary>
  20: [HarmonyPatch(typeof(CaravansCampaignBehavior), "GetTradeScoreForTown")]
  21: [HarmonyPatchCategory("Patch59_CaravanTrade")]
  22: public static class CaravansCampaignBehavior_GetTradeScoreForTown_Patch
  23: {
  24:     private static ICaravanTradeService _service;
  25: 
  26:     [HarmonyPostfix]
  27:     public static void Postfix(ref float __result, MobileParty caravanParty, Town town)
  28:     {
  29:         // Positive-requirement gate: vanilla rejections (-1) and any NaN pass through untouched.
  30:         if (!(__result > 0f)) return;
  31:         if (caravanParty == null || town?.Settlement == null) return;
  32: 
  33:         try
  34:         {
  35:             _service ??= IoC.Resolve<ICaravanTradeService>();
  36: 
  37:             bool isNaval = caravanParty.HasNavalNavigationCapability;
  38:             AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
  39:                 caravanParty, town.Settlement, isNaval, out var navType, out var navDistance, out _);
  40:             if (navType == MobileParty.NavigationType.None) return;
  41: 
  42:             float speed = isNaval
  43:                 ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed
  44:                 : Campaign.Current.EstimatedAverageCaravanPartySpeed;
  45:             float days = navDistance / (speed * CampaignTime.HoursInDay);
  46: 
  47:             bool isHome = town.Settlement == caravanParty.HomeSettlement;
  48:             bool isJustLeft = !isHome && town.Settlement == caravanParty.LastVisitedSettlement;
  49:             bool isPlayer = caravanParty.Owner?.Clan == Clan.PlayerClan;
  50: 
  51:             __result = _service.ReweightTradeScore(__result, days, isNaval, isHome, isJustLeft, isPlayer);
  52:         }
  53:         catch (Exception)
  54:         {
  55:             // Degrade gracefully to the vanilla score.
  56:         }
  57:     }
  58: }

 succeeded in 716ms:
   1: using TAOM.Features.CaravanTrade;
   2: using TaleWorlds.CampaignSystem;
   3: using TaleWorlds.CampaignSystem.GameComponents;
   4: using TaleWorlds.CampaignSystem.Party;
   5: using TaleWorlds.Core;
   6: 
   7: namespace TAOM.Features.CulturalFeats.Models;
   8: 
   9: public class TaomCaravanModel : DefaultCaravanModel
  10: {
  11:     private readonly ICulturalFeatsService _feats;
  12:     private readonly ICaravanTradeService _caravanTrade;
  13: 
  14:     public TaomCaravanModel(ICulturalFeatsService feats, ICaravanTradeService caravanTrade)
  15:     {
  16:         _feats = feats;
  17:         _caravanTrade = caravanTrade;
  18:     }
  19: 
  20:     public override int GetCaravanFormingCost(bool largerCaravan, bool navalCaravan)
  21:         => _feats.ApplyCaravanCost(
  22:             CultureFeatAdapter.FromOrNull(CharacterObject.PlayerCharacter?.Culture),
  23:             base.GetCaravanFormingCost(largerCaravan, navalCaravan));
  24: 
  25:     // CaravanTrade basket-diversity levers (delegate to the pure service; vanilla when the feature is off).
  26:     public override int GetInitialTradeGold(Hero owner, bool navalCaravan, bool largeCaravan)
  27:         => _caravanTrade.ResolveInitialTradeGold(
  28:             base.GetInitialTradeGold(owner, navalCaravan, largeCaravan),
  29:             owner == Hero.MainHero);
  30: 
  31:     public override int GetMaxGoldToSpendOnOneItemCategory(MobileParty caravan, ItemCategory itemCategory)
  32:         => _caravanTrade.ResolveMaxGoldPerCategory(
  33:             base.GetMaxGoldToSpendOnOneItemCategory(caravan, itemCategory),
  34:             caravan?.Owner?.Clan == Clan.PlayerClan);
  35: }

 succeeded in 726ms:
   1: using System;
   2: using HarmonyLib;
   3: using TAOM.Features.CaravanTrade;
   4: using TaleWorlds.CampaignSystem;
   5: using TaleWorlds.CampaignSystem.CampaignBehaviors;
   6: using TaleWorlds.CampaignSystem.Party;
   7: 
   8: namespace TAOM.Features.CaravanTrade.Hooks;
   9: 
  10: /// <summary>
  11: /// Lever 4 (direct "caravans only buy one item" fix): floor the vanilla per-caravan budget factor.
  12: /// Vanilla's <c>budgetFactor = 0.1 + clamp(PartyTradeGold/5000, 0, 1)</c> sits at ~0.1 for a poor
  13: /// caravan, so only the single best category clears the private per-category buy-value gate and the
  14: /// caravan buys one good. Raising the floor lets several categories clear the gate even when poor,
  15: /// producing a fuller basket. Delegated to the pure service; player-scoped; vanilla when disabled.
  16: /// </summary>
  17: [HarmonyPatch(typeof(CaravansCampaignBehavior), "CalculateBudgetFactor")]
  18: [HarmonyPatchCategory("Patch59_CaravanTrade")]
  19: public static class CaravansCampaignBehavior_CalculateBudgetFactor_Patch
  20: {
  21:     private static ICaravanTradeService _service;
  22: 
  23:     [HarmonyPostfix]
  24:     public static void Postfix(ref float __result, MobileParty caravanParty)
  25:     {
  26:         if (caravanParty == null) return;
  27: 
  28:         try
  29:         {
  30:             _service ??= IoC.Resolve<ICaravanTradeService>();
  31:             bool isPlayer = caravanParty.Owner?.Clan == Clan.PlayerClan;
  32:             __result = _service.ApplyBudgetFactorFloor(__result, isPlayer);
  33:         }
  34:         catch (Exception)
  35:         {
  36:             // Degrade gracefully to the vanilla budget factor.
  37:         }
  38:     }
  39: }

 succeeded in 727ms:
   1: using System;
   2: using HarmonyLib;
   3: using TAOM.Features.CaravanTrade;
   4: using TaleWorlds.CampaignSystem.CampaignBehaviors;
   5: 
   6: namespace TAOM.Features.CaravanTrade.Hooks;
   7: 
   8: /// <summary>
   9: /// Lever 3: widen the vanilla "very far" distance ceiling so profitable distant towns aren't
  10: /// hard-rejected. Vanilla <c>CacheVeryFarDistances</c> caches a land + naval ceiling; every derived
  11: /// band (Close/Med/Far), the <c>distanceCut</c> veto, and the <c>AdjustVeryFarAddition</c> escalation
  12: /// key off these two fields, so scaling them once (this postfix runs once per session — zero hot-path
  13: /// cost) widens the whole tolerance system coherently. Field-rename drift degrades to a no-op.
  14: /// </summary>
  15: [HarmonyPatch(typeof(CaravansCampaignBehavior), "CacheVeryFarDistances")]
  16: [HarmonyPatchCategory("Patch59_CaravanTrade")]
  17: public static class CaravansCampaignBehavior_CacheVeryFarDistances_Patch
  18: {
  19:     private static ICaravanTradeService _service;
  20: 
  21:     private static AccessTools.FieldRef<CaravansCampaignBehavior, float> _defaultCache;
  22:     private static AccessTools.FieldRef<CaravansCampaignBehavior, float> _navalCache;
  23:     private static bool _fieldsResolved;
  24:     private static bool _fieldsBindFailed;
  25: 
  26:     [HarmonyPostfix]
  27:     public static void Postfix(CaravansCampaignBehavior __instance)
  28:     {
  29:         if (__instance == null) return;
  30: 
  31:         try
  32:         {
  33:             if (!ResolveFields()) return;
  34: 
  35:             _service ??= IoC.Resolve<ICaravanTradeService>();
  36: 
  37:             _defaultCache(__instance) = _service.ScaleVeryFarDistance(_defaultCache(__instance));
  38:             _navalCache(__instance) = _service.ScaleVeryFarDistance(_navalCache(__instance));
  39:         }
  40:         catch (Exception)
  41:         {
  42:             // IoC not started / reflection failure — leave the vanilla cache untouched.
  43:         }
  44:     }
  45: 
  46:     private static bool ResolveFields()
  47:     {
  48:         if (_fieldsResolved) return !_fieldsBindFailed;
  49:         _fieldsResolved = true;
  50:         try
  51:         {
  52:             _defaultCache = AccessTools.FieldRefAccess<CaravansCampaignBehavior, float>("_defaultCaravanVeryFarCache");
  53:             _navalCache = AccessTools.FieldRefAccess<CaravansCampaignBehavior, float>("_navalCaravanVeryFarCache");
  54:         }
  55:         catch (Exception)
  56:         {
  57:             _fieldsBindFailed = true;
  58:         }
  59:         return !_fieldsBindFailed;
  60:     }
  61: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\TaomSettings.cs' | Select-Object -First 110 | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\_Module\\ModuleData\\caravan_trade\\caravan_trade_config.json' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\Execution\\AlignmentService.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }; $j=1; Get-Content '"'Main\\Features\\Execution\\FactionSide.cs' | ForEach-Object { '{0,4}: {1}' -f "'$j++, $_ }; $k=1; Get-Content '"'Main\\Features\\AlignmentRecruitment\\RecruitmentAlignmentService.cs' | ForEach-Object { '{0,4}: {1}' -f "'$k++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\CaravanTradeIoC.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\IoC.cs' | Select-Object -First 140 | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }; $j=1; Get-Content '"'Main\\SubModule.cs' | Select-Object -First 260 | ForEach-Object { '{0,4}: {1}' -f "'$j++, $_ }; $k=1; Get-Content '"'Main\\SubModule.cs' | Select-Object -Skip 500 -First 35 | ForEach-Object { '{0,4}: {1}' -f ("'$k+500), $_; $k++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 843ms:
   1: {
   2:   "_comment": "AI/player caravan trade tuning (TAOM CaravanTrade feature). Master toggle + Range Multiplier + War Trade Policy + Basket Diversity Floor are also on the MCM 'Caravan Trade' page and override the matching fields below at runtime; the rest are JSON-only advanced knobs. Singleton-cached: edits require an app restart. Invalid values revert to these defaults with a warning in the log.",
   3: 
   4:   "enabled": true,
   5:   "applyToPlayerCaravans": true,
   6: 
   7:   "_comment_range": "Lever 2/3 — how far caravans range and how strongly distance is rewarded. rangeMultiplier scales the vanilla 'very far' ceiling [1..4]. The re-weight strips vanilla's 1/days spike and re-applies 1/(nearFieldFlattenDays+days)^distanceDecayExponent, clamped by maxCompensation. Lower distanceDecayExponent = ranges further. nearFieldFlattenDays ties near towns so profit decides. antiShuttlePenalty cuts the score of the town just left.",
   8:   "rangeMultiplier": 1.6,
   9:   "distanceDecayExponent": 0.5,
  10:   "nearFieldFlattenDays": 2.0,
  11:   "maxCompensation": 6.0,
  12:   "antiShuttlePenalty": 0.35,
  13: 
  14:   "_comment_war": "Lever 1 — one of None (vanilla, war blocks trade), IgnoreWar (trade anywhere non-besieged), SameAlignmentAndNeutral (default: trade with same-side or neutral factions but not the enemy side).",
  15:   "warTradePolicy": "SameAlignmentAndNeutral",
  16: 
  17:   "_comment_diversity": "Lever 4 — fuller baskets. budgetFactorFloor [0..1] raises poor caravans' buying power so more categories clear the buy gate. initialTradeGold is a floor (never lowers vanilla's large/main-hero bonus). maxGoldPerCategory defaults to vanilla 1500.",
  18:   "budgetFactorFloor": 0.35,
  19:   "initialTradeGold": 15000,
  20:   "maxGoldPerCategory": 1500
  21: }

 succeeded in 873ms:
   1: using MCM.Abstractions.Attributes;
   2: using MCM.Abstractions.Attributes.v2;
   3: using MCM.Abstractions.Base.Global;
   4: using MCM.Common;
   5: using TaleWorlds.Core;
   6: using TaleWorlds.Library;
   7: 
   8: namespace TAOM.Features;
   9: 
  10: public class TaomSettings : AttributeGlobalSettings<TaomSettings>
  11: {
  12:     public override string Id => "TAOM";
  13:     public override string DisplayName => "TAOM - Tales From the Age of Men";
  14:     public override string FolderName => "TAOM";
  15:     public override string FormatType => "json2";
  16: 
  17:     // --- Encyclopedia ---
  18: 
  19:     [SettingPropertyGroup("Encyclopedia")]
  20:     [SettingPropertyBool("Show All Characters", Order = 0,
  21:         HintText = "Reveals all characters in the encyclopedia, including those not yet encountered. Equivalent to the 'campaign.toggle_information_restrictions' cheat.")]
  22:     public bool ShowAllEncyclopediaCharacters { get; set; } = true;
  23: 
  24:     // --- Troop Weight ---
  25: 
  26:     [SettingPropertyGroup("Troop Weight")]
  27:     [SettingPropertyBool("Enable Troop Weight", Order = 0,
  28:         HintText = "Weighted party size — elite units consume more party capacity. Cave trolls (4x), elves (2x), warg riders (2x).")]
  29:     public bool EnableTroopWeight { get; set; } = true;
  30: 
  31:     // --- Settlement Food ---
  32: 
  33:     [SettingPropertyGroup("Settlement Food")]
  34:     [SettingPropertyBool("Enable Settlement Food Tuning", Order = 0,
  35:         HintText = "Corrects garrison food consumption (Troop Weight no longer inflates it for elite garrisons) and applies the tunable food knobs in settlement_food/settlement_food_config.json (consumption divisors, base/village/flat production, storage caps). Off = vanilla engine food math (garrison food stays weighted). Config edits need an app restart.")]
  36:     public bool EnableSettlementFoodTuning { get; set; } = true;
  37: 
  38:     // --- Settlement Economy ---
  39: 
  40:     [SettingPropertyGroup("Settlement Economy")]
  41:     [SettingPropertyBool("Enable Settlement Economy Tuning", Order = 0,
  42:         HintText = "Regenerates town market gold toward a higher target (base 25000 vs vanilla 10000) so drained town markets recover — tunable in settlement_economy/settlement_economy_config.json (base, gold per prosperity, daily regen rate). Applies to existing saves immediately. Off = vanilla engine gold math. Config edits need an app restart.")]
  43:     public bool EnableSettlementEconomyTuning { get; set; } = true;
  44: 
  45:     // --- Caravan Trade ---
  46: 
  47:     [SettingPropertyGroup("Caravan Trade")]
  48:     [SettingPropertyBool("Enable Caravan Trade Overhaul", Order = 0,
  49:         HintText = "Makes AI/player caravans range beyond the local town cluster instead of shuttling between very-close towns, trade across the endless Free-vs-Evil war (per War Trade Policy below), and carry fuller baskets. Off = exact vanilla caravan behavior. Advanced curve knobs live in caravan_trade/caravan_trade_config.json; config edits need an app restart.")]
  50:     public bool EnableCaravanTrade { get; set; } = true;
  51: 
  52:     [SettingPropertyGroup("Caravan Trade")]
  53:     [SettingPropertyBool("Apply To Player Caravans", Order = 1,
  54:         HintText = "When on, your OWN caravans also range further, trade cross-war, and buy fuller baskets. With War Trade Policy above Vanilla they may travel into contested territory and risk being attacked. Off = only NPC caravans change.")]
  55:     public bool CaravanTradeApplyToPlayer { get; set; } = true;
  56: 
  57:     [SettingPropertyGroup("Caravan Trade")]
  58:     [SettingPropertyFloatingInteger("Caravan Range Multiplier", 1.0f, 4.0f, "#0.0", Order = 2,
  59:         HintText = "How much further caravans range past the vanilla distance ceiling. 1.0 = vanilla reach; higher = they visit more distant, more profitable markets. Default: 1.6.")]
  60:     public float CaravanRangeMultiplier { get; set; } = 1.6f;
  61: 
  62:     [SettingPropertyGroup("Caravan Trade")]
  63:     [SettingPropertyDropdown("War Trade Policy", Order = 3,
  64:         HintText = "Which towns caravans may trade with despite the war. Vanilla = war blocks trade (the cause of the shuttle). Same Side + Neutral = trade with same-alignment or neutral factions but not the enemy side (default, lore-friendly). Ignore War = trade anywhere non-besieged.")]
  65:     public Dropdown<string> CaravanWarTradePolicy { get; set; } = new Dropdown<string>(
  66:         new[] { "Vanilla (war blocks)", "Same Side + Neutral", "Ignore War" }, 1);
  67: 
  68:     [SettingPropertyGroup("Caravan Trade")]
  69:     [SettingPropertyFloatingInteger("Basket Diversity Floor", 0.0f, 1.0f, "#0.00", Order = 4,
  70:         HintText = "Raises poor caravans' buying power so they stock several goods instead of one. 0 = vanilla (poor caravans buy a single item); higher = fuller baskets. Default: 0.35.")]
  71:     public float CaravanBudgetDiversityFloor { get; set; } = 0.35f;
  72: 
  73:     // --- Native Skin Fixes ---
  74: 
  75:     [SettingPropertyGroup("Native Skin Fixes")]
  76:     [SettingPropertyBool("Enable Native Skin Fixes", Order = 0,
  77:         HintText = "Installs the native MinHook detours that fix engine rendering bugs TaleWorlds won't: the covers_head hand-morph freeze (jazz-hands under closed helms) + hair/beard cloth physics. ON by default — all 7 hook targets are authored + verified against Bannerlord v1.4.6's TaleWorlds.Native.dll (RTTI-anchored disassembly + interior byte-triangulation; each pattern single-matches its expected address). Turn OFF to fully disable the native hooks (vanilla rendering) if you ever hit instability. Requires an app restart to take effect.")]
  78:     public bool EnableNativeSkinFixes { get; set; } = true;
  79: 
  80:     // --- Castle Recruitment ---
  81: 
  82:     [SettingPropertyGroup("Castle Recruitment")]
  83:     [SettingPropertyBool("Enable Castle Recruitment", Order = 0,
  84:         HintText = "When enabled, castles gain notables with recruitable volunteers — the player can 'Recruit troops' at any accessible castle. Existing notables remain in the save if you later disable this.")]
  85:     public bool EnableCastleRecruitment { get; set; } = true;
  86: 
  87:     [SettingPropertyGroup("Castle Recruitment")]
  88:     [SettingPropertyBool("AI Recruits From Castles", Order = 1,
  89:         HintText = "When enabled, AI lord parties also score, travel to, and recruit volunteers from castles like they do from towns. Requires Enable Castle Recruitment.")]
  90:     public bool EnableCastleRecruitmentAi { get; set; } = true;
  91: 
  92:     [SettingPropertyGroup("Castle Recruitment")]
  93:     [SettingPropertyInteger("Notables Per Castle", 1, 5, Order = 2,
  94:         HintText = "How many recruiters each castle is populated with (vanilla towns = 5, villages = 3). Higher = more recruitment volume per castle. Default: 3.")]
  95:     public int CastleNotablesPerCastle { get; set; } = 3;
  96: 
  97:     // --- Elite Emissary ---
  98: 
  99:     [SettingPropertyGroup("Elite Emissary")]
 100:     [SettingPropertyBool("Enable Elite Emissary", Order = 0,
 101:         HintText = "At a faction's key settlements (capitals), speak with the faction emissary to buy that faction's elite troops for its special resource (Castar, War Spoils, Gems...). Conquering a settlement flips its offerings to the new owner.")]
 102:     public bool EnableEliteEmissary { get; set; } = true;
 103: 
 104:     [SettingPropertyGroup("Elite Emissary")]
 105:     [SettingPropertyBool("Hide Emissary Without Resource", Order = 1,
 106:         HintText = "When on, the emissary option is hidden at settlements whose owner faction has no special resource. When off, the option still appears but is disabled with an explanatory hint.")]
 107:     public bool HideEmissaryWhenNoResource { get; set; } = true;
 108: 
 109:     // --- Culture Conversion ---
 110: 

 succeeded in 726ms:
   1: using DryIoc;
   2: 
   3: namespace TAOM.Features.CaravanTrade;
   4: 
   5: public static class CaravanTradeIoC
   6: {
   7:     public static void RegisterCaravanTradeFeature(IContainer container)
   8:     {
   9:         container.Register<ICaravanTradeConfigProvider, CaravanTradeConfigProvider>(Reuse.Singleton);
  10:         container.Register<ICaravanTradeSettingsProvider, CaravanTradeSettingsProvider>(Reuse.Singleton);
  11:         container.Register<ICaravanTradeService, CaravanTradeService>(Reuse.Singleton);
  12:     }
  13: }

 succeeded in 771ms:
   1: using System;
   2: using System.Collections.Generic;
   3: using TAOM.Core.Logging;
   4: 
   5: namespace TAOM.Features.Execution;
   6: 
   7: public class AlignmentService : IAlignmentService
   8: {
   9:     private readonly Dictionary<string, FactionSide> _kingdomSides;
  10: 
  11:     public AlignmentService(IAlignmentConfigProvider configProvider, IModLogger logger)
  12:     {
  13:         _kingdomSides = new Dictionary<string, FactionSide>(StringComparer.OrdinalIgnoreCase);
  14: 
  15:         var alignments = configProvider.LoadAlignments();
  16:         foreach (var kvp in alignments)
  17:         {
  18:             if (Enum.TryParse<FactionSide>(kvp.Value, ignoreCase: true, out var side))
  19:             {
  20:                 _kingdomSides[kvp.Key] = side;
  21:             }
  22:             else
  23:             {
  24:                 logger.LogWarning($"AlignmentService: Unknown side '{kvp.Value}' for kingdom '{kvp.Key}', defaulting to Neutral");
  25:                 _kingdomSides[kvp.Key] = FactionSide.Neutral;
  26:             }
  27:         }
  28: 
  29:         logger.LogInfo($"AlignmentService: Loaded {_kingdomSides.Count} kingdom alignments");
  30:     }
  31: 
  32:     public FactionSide GetKingdomSide(string kingdomId) => GetSide(kingdomId);
  33: 
  34:     public FactionSide GetCultureSide(string cultureId) => GetSide(cultureId);
  35: 
  36:     private FactionSide GetSide(string id)
  37:     {
  38:         if (string.IsNullOrEmpty(id))
  39:             return FactionSide.Neutral;
  40: 
  41:         return _kingdomSides.TryGetValue(id, out var side) ? side : FactionSide.Neutral;
  42:     }
  43: 
  44:     public bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB)
  45:     {
  46:         var sideA = GetKingdomSide(kingdomIdA);
  47:         var sideB = GetKingdomSide(kingdomIdB);
  48: 
  49:         if (sideA == FactionSide.Neutral || sideB == FactionSide.Neutral)
  50:             return true;
  51: 
  52:         return sideA != sideB;
  53:     }
  54: 
  55:     public bool AreSameAlignment(string kingdomIdA, string kingdomIdB)
  56:     {
  57:         var sideA = GetKingdomSide(kingdomIdA);
  58:         var sideB = GetKingdomSide(kingdomIdB);
  59: 
  60:         if (sideA == FactionSide.Neutral || sideB == FactionSide.Neutral)
  61:             return false;
  62: 
  63:         return sideA == sideB;
  64:     }
  65: }
   1: namespace TAOM.Features.Execution;
   2: 
   3: public enum FactionSide
   4: {
   5:     Free,
   6:     Evil,
   7:     Neutral
   8: }
   1: using TAOM.Features.Execution;
   2: 
   3: namespace TAOM.Features.AlignmentRecruitment;
   4: 
   5: /// <summary>
   6: /// Pure decision for whether a recruiter↔source alignment pairing blocks recruitment. Reuses the
   7: /// existing kingdom-keyed <see cref="IAlignmentService"/> (the same lookup the Execution and
   8: /// Diplomacy features use). Deliberately does NOT call <see cref="IAlignmentService.AreEnemyAlignments"/>,
   9: /// whose Neutral semantics are inverted for this purpose (it treats Neutral as an enemy of everyone);
  10: /// here Neutral on either side is a mercenary "serve/accept anyone" and never blocks.
  11: /// </summary>
  12: public class RecruitmentAlignmentService : IRecruitmentAlignmentService
  13: {
  14:     private readonly IAlignmentService _alignment;
  15:     private readonly IRecruitmentAlignmentSettingsProvider _settings;
  16: 
  17:     public RecruitmentAlignmentService(IAlignmentService alignment, IRecruitmentAlignmentSettingsProvider settings)
  18:     {
  19:         _alignment = alignment;
  20:         _settings = settings;
  21:     }
  22: 
  23:     public bool IsRecruitmentBlocked(string recruiterKingdomId, string sourceKingdomId, bool isPlayerRecruiter)
  24:     {
  25:         if (!_settings.IsEnabled)
  26:             return false;
  27:         if (isPlayerRecruiter && !_settings.ApplyToPlayer)
  28:             return false;
  29:         if (!isPlayerRecruiter && !_settings.ApplyToAi)
  30:             return false;
  31: 
  32:         var recruiterSide = _alignment.GetKingdomSide(recruiterKingdomId);
  33:         var sourceSide = _alignment.GetKingdomSide(sourceKingdomId);
  34: 
  35:         if (recruiterSide == FactionSide.Neutral || sourceSide == FactionSide.Neutral)
  36:             return false;
  37: 
  38:         if (_settings.GoodRejectsEvilOnly)
  39:             return recruiterSide == FactionSide.Free && sourceSide == FactionSide.Evil;
  40: 
  41:         // Symmetric: both sides are non-Neutral here, so a difference is a Free↔Evil opposition.
  42:         return recruiterSide != sourceSide;
  43:     }
  44: }

 succeeded in 815ms:
   1: using DryIoc;
   2: using System.Collections.Generic;
   3: using TAOM.Adapters;
   4: using TAOM.Core.Domain;
   5: using TAOM.Core.Infrastructure;
   6: using TAOM.Core.Logging;
   7: using TAOM.Features.BannerColorPersistence;
   8: using TAOM.Features.BannerInjection;
   9: using TAOM.Features.HeroRace;
  10: using TAOM.Features.CharacterCreation;
  11: using TAOM.Features.FactionMap;
  12: using TAOM.Features.InitialChildGeneration;
  13: using TAOM.Features.Diplomacy;
  14: using TAOM.Features.Encyclopedia;
  15: using TAOM.Features.RaceAge;
  16: using TAOM.Features.Execution;
  17: using TAOM.Features.AlignmentRecruitment;
  18: using TAOM.Features.AlignmentDesertion;
  19: using TAOM.Features.NazgulFamily;
  20: using TAOM.Features.NavalTravel;
  21: using TAOM.Features.StartupResources;
  22: using TAOM.Features.TroopProgression;
  23: using TAOM.Features.AdvancedCombat;
  24: using TAOM.Features.CustomBattles;
  25: using TAOM.Features.TroopWeight;
  26: using TAOM.Features.Warg;
  27: using TAOM.Features.Spider;
  28: using TAOM.Features.BattleBalance;
  29: using TAOM.Features.MainMenuCustomizer;
  30: using TAOM.Features.Messengers;
  31: using TAOM.Features.ShaderPrecompilation;
  32: using TAOM.Features.Siege;
  33: using TAOM.Features.ArmyTargeting;
  34: using TAOM.Features.TimeAcceleration;
  35: using TAOM.Features.SpecialResources;
  36: using TAOM.Features.CareerSystem;
  37: using TAOM.Features.SettlementGuards;
  38: using TAOM.Features.NamedCompanions;
  39: using TAOM.Features.RevoltTuning;
  40: using TAOM.Features.SettlementEconomy;
  41: using TAOM.Features.CaravanTrade;
  42: using TAOM.Features.SettlementFood;
  43: using TAOM.Features.BanditManagement;
  44: using TAOM.Features.CastleRecruitment;
  45: using TAOM.Features.EliteEmissary;
  46: using TAOM.Features.SiegeDismount;
  47: using TAOM.Features.MixedFormations;
  48: using TAOM.Features.SmartCavalryAI;
  49: using TAOM.Features.QuickActions;
  50: using TAOM.Features.CompanionTactics;
  51: using TAOM.Features.FiefManagement;
  52: using TAOM.Features.EquipPresets;
  53: using TAOM.Features.EditorCacheRebuild;
  54: using TAOM.Features.CulturalFeats;
  55: using TAOM.Features.CultureMarketplace;
  56: using TAOM.Features.CultureConversion;
  57: using TAOM.Features.MissionDiagnostic;
  58: using TAOM.Features.CrashReport;
  59: using TAOM.Features.BattleLoadDiagnostics;
  60: using TAOM.Features.SettlementNameplateFade;
  61: 
  62: namespace TAOM;
  63: 
  64: public static class IoC
  65: {
  66:     private static IContainer _container;
  67: 
  68:     public static void Configure()
  69:     {
  70:         var container = new Container();
  71: 
  72:         container.RegisterInstance<IContainer>(container);
  73: 
  74:         RegisterCoreServices(container);
  75:         RegisterLoggingServices(container);
  76: 
  77:         HeroRaceIoC.RegisterHeroRaceFeature(container);
  78:         BannerInjectionIoC.RegisterBannerInjectionFeature(container);
  79:         BannerColorPersistenceIoC.RegisterBannerColorPersistenceFeature(container);
  80:         TroopProgressionIoC.RegisterTroopProgressionFeature(container);
  81:         Features.Arena.ArenaIoC.RegisterArenaFeature(container);
  82:         FactionMapIoC.RegisterFactionMapFeature(container);
  83:         CharacterCreationIoC.RegisterCharacterCreationFeature(container);
  84:         InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
  85:         DiplomacyIoC.RegisterDiplomacyFeature(container);
  86:         Features.WarOfTheRingMomentum.WarOfTheRingMomentumIoC.RegisterWarOfTheRingMomentumFeature(container);
  87:         RaceAgeIoC.RegisterRaceAgeFeature(container);
  88:         ExecutionIoC.RegisterExecutionFeature(container);
  89:         RecruitmentAlignmentIoC.RegisterAlignmentRecruitmentFeature(container);
  90:         AlignmentDesertionIoC.RegisterAlignmentDesertionFeature(container);
  91:         NazgulFamilyIoC.RegisterNazgulFamilyFeature(container);
  92:         NavalTravelIoC.RegisterNavalTravelFeature(container);
  93:         EncyclopediaIoC.RegisterEncyclopediaFeature(container);
  94:         StartupResourcesIoC.RegisterStartupResourcesFeature(container);
  95:         TroopWeightIoC.RegisterTroopWeightFeature(container);
  96:         AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
  97:         WargIoC.RegisterWargFeature(container);
  98:         SpiderIoC.RegisterSpiderFeature(container);
  99:         Features.Elephant.ElephantIoC.RegisterElephantFeature(container);
 100:         Features.Mumakil.MumakilIoC.RegisterMumakilFeature(container);
 101:         CustomBattlesIoC.RegisterCustomBattlesFeature(container);
 102:         BattleBalanceIoC.RegisterBattleBalanceFeature(container);
 103:         Features.CombatMechanics.CombatMechanicsIoC.RegisterCombatMechanicsFeature(container);
 104:         MainMenuCustomizerIoC.RegisterMainMenuCustomizerFeature(container);
 105:         ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container);
 106:         SiegeDefenseIoC.RegisterSiegeDefenseFeature(container);
 107:         ArmyTargetingIoC.RegisterArmyTargetingFeature(container);
 108:         TimeAccelerationIoC.RegisterTimeAccelerationFeature(container);
 109:         SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
 110:         CareerSystemIoC.RegisterCareerSystemFeature(container);
 111:         SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
 112:         NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
 113:         RevoltTuningIoC.RegisterRevoltTuningFeature(container);
 114:         SettlementFoodIoC.RegisterSettlementFoodFeature(container);
 115:         SettlementEconomyIoC.RegisterSettlementEconomyFeature(container);
 116:         CaravanTradeIoC.RegisterCaravanTradeFeature(container);
 117:         BanditManagementIoC.RegisterBanditManagementFeature(container);
 118:         CastleRecruitmentIoC.RegisterCastleRecruitmentFeature(container);
 119:         EliteEmissaryIoC.RegisterEliteEmissaryFeature(container);
 120:         SiegeDismountIoC.RegisterSiegeDismountFeature(container);
 121:         MixedFormationsIoC.RegisterMixedFormationsFeature(container);
 122:         SmartCavalryAIIoC.RegisterSmartCavalryAIFeature(container);
 123:         QuickActionsIoC.RegisterQuickActionsFeature(container);
 124:         MessengerIoC.RegisterMessengerFeature(container);
 125:         EquipPresetsIoC.RegisterEquipPresetsFeature(container);
 126:         CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);
 127:         FiefManagementIoC.RegisterFiefManagementFeature(container);
 128:         EditorCacheRebuildIoC.RegisterEditorCacheRebuildFeature(container);
 129:         CulturalFeatsIoC.RegisterCulturalFeatsFeature(container);
 130:         CultureMarketplaceIoC.RegisterCultureMarketplaceFeature(container);
 131:         CultureConversionIoC.RegisterCultureConversionFeature(container);
 132:         MissionDiagnosticIoC.RegisterMissionDiagnosticFeature(container);
 133:         CrashReportIoC.RegisterCrashReportFeature(container);
 134:         BattleLoadDiagnosticsIoC.RegisterBattleLoadDiagnosticsFeature(container);
 135:         NameplateFadeIoC.RegisterSettlementNameplateFadeFeature(container);
 136:         Features.LotrIssues.LotrIssuesIoC.RegisterLotrIssuesFeature(container);
 137: 
 138:         _container = container;
 139: 
 140:         // Post-registration initialization
   1: using Bannerlord.UIExtenderEx;
   2: using HarmonyLib;
   3: using TaleWorlds.CampaignSystem;
   4: using TaleWorlds.Core;
   5: using TaleWorlds.Engine;
   6: using TaleWorlds.Library;
   7: using TaleWorlds.Localization;
   8: using TaleWorlds.MountAndBlade;
   9: using TaleWorlds.MountAndBlade.ComponentInterfaces;
  10: using TAOM.Features;
  11: using TAOM.Features.BannerInjection;
  12: using TAOM.Features.HeroRace;
  13: using TAOM.Core.Infrastructure;
  14: using TAOM.Core.Logging;
  15: using TAOM.Features.CharacterCreation;
  16: using TAOM.Features.FactionMap;
  17: using TAOM.Features.InitialChildGeneration;
  18: using TAOM.Adapters;
  19: using TAOM.Features.Diplomacy;
  20: using TAOM.Features.Diplomacy.Hooks;
  21: using TAOM.Features.Diplomacy.Models;
  22: using TAOM.Features.Execution;
  23: using TAOM.Features.Execution.Hooks;
  24: using TAOM.Features.Execution.Models;
  25: using TAOM.Features.RaceAge;
  26: using TAOM.Features.RaceAge.Models;
  27: using TAOM.Features.StartupResources;
  28: using TAOM.Features.NamedCompanions;
  29: using TAOM.Features.TroopProgression;
  30: using TAOM.Features.TroopWeight;
  31: using TAOM.Features.TroopWeight.Diagnostics;
  32: using TAOM.Features.TroopWeight.Hooks;
  33: using TAOM.Features.AtmospherePersistence.Hooks;
  34: using TAOM.Features.TroopProgression.Models;
  35: using TAOM.Features.AdvancedCombat;
  36: using TAOM.Features.CulturalFeats.Models;
  37: using TAOM.Features.NavalTravel;
  38: using TAOM.Features.NavalTravel.Models;
  39: using TAOM.Features.NazgulFamily;
  40: using TAOM.Features.NazgulFamily.Models;
  41: using TAOM.Features.CustomBattles;
  42: using TAOM.Features.CustomBattles.Hooks;
  43: using TAOM.Features.Warg;
  44: using TAOM.Features.Spider;
  45: using TAOM.Features.BattleBalance;
  46: using TAOM.Features.BattleBalance.Models;
  47: using TAOM.Features.Arena.Models;
  48: using TAOM.Features.Encyclopedia;
  49: using TAOM.Features.Encyclopedia.Models;
  50: using TAOM.Features.MainMenuCustomizer;
  51: using TAOM.Features.NativeSkinFixes;
  52: using TAOM.Features.ShaderPrecompilation;
  53: using TAOM.Features.Siege;
  54: using TAOM.Features.Siege.Models;
  55: using TAOM.Features.ArmyTargeting;
  56: using TAOM.Features.ArmyTargeting.Models;
  57: using TAOM.Features.TimeAcceleration;
  58: using TAOM.Features.BannerColorPersistence;
  59: using TAOM.Features.BannerColorPersistence.Hooks;
  60: using TAOM.Features.LocalizationOverride;
  61: using TAOM.Features.LocalizationOverride.Hooks;
  62: using TAOM.Features.SpecialResources;
  63: using TAOM.Features.SpecialResources.Hooks;
  64: using TAOM.Features.CareerSystem;
  65: using TAOM.Features.CareerSystem.Models;
  66: using TAOM.Features.CombatMechanics.Models;
  67: using TAOM.Features.SettlementGuards;
  68: using TAOM.Features.SettlementGuards.Hooks;
  69: using TAOM.Features.RevoltTuning;
  70: using TAOM.Features.SettlementEconomy;
  71: using TAOM.Features.SettlementEconomy.Models;
  72: using TAOM.Features.SettlementFood;
  73: using TAOM.Features.SettlementFood.Models;
  74: using TAOM.Features.BanditManagement;
  75: using TAOM.Features.BanditManagement.Models;
  76: using TAOM.Features.CastleRecruitment;
  77: using TAOM.Features.CastleRecruitment.Hooks;
  78: using TAOM.Features.SiegeDismount.Hooks;
  79: using TAOM.Features.MixedFormations.Hooks;
  80: using TAOM.Features.SmartCavalryAI.Hooks;
  81: using TAOM.Features.FiefManagement;
  82: using TAOM.Features.FiefManagement.Hooks;
  83: using TAOM.Features.SettlementNameplateFade;
  84: using TAOM.Features.SettlementNameplateFade.Hooks;
  85: using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
  86: using BehaviorTreeWrapper;
  87: using TaleWorlds.CampaignSystem.CampaignBehaviors;
  88: 
  89: namespace TAOM;
  90: 
  91: public class SubModule : MBSubModuleBase
  92: {
  93:     private Harmony _harmony;
  94:     private UIExtender? _uiExtender;
  95:     private ITimeAccelerationService? _timeAccelerationService;
  96:     private static float _shaderTickAccumulator;
  97:     private static ShaderPrecompileRunner _shaderRunner;
  98:     private static bool _missionTimePatchesApplied;
  99:     private static bool _gameInitPatchesApplied;
 100:     private static bool _basicTableauGuardApplied;
 101: 
 102:     protected override void OnSubModuleLoad()
 103:     {
 104:         base.OnSubModuleLoad();
 105: 
 106:         IoC.Configure();
 107: 
 108:         // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
 109:         // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
 110:         // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
 111:         // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot
 112:         // is the IoC.Configure() call itself — if THAT throws, the entire feature is
 113:         // unreachable. Split CrashReport bootstrap doesn't fix this without re-implementing
 114:         // a manual DI container; accept and document the residual.
 115:         _harmony = new Harmony("com.taom.mod");
 116:         if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
 117:         {
 118:             try
 119:             {
 120:                 _harmony.PatchCategory("Patch37_CrashReport");
 121:                 IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
 122:                 if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
 123:                 {
 124:                     IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
 125:                 }
 126:             }
 127:             catch (System.Exception ex)
 128:             {
 129:                 IoC.Resolve<IModLogger>().LogError($"[CrashReport] init failed: {ex.GetType().Name}: {ex.Message}");
 130:             }
 131:         }
 132: 
 133:         _uiExtender = UIExtender.Create("TAOM");
 134:         _uiExtender.Register(typeof(SubModule).Assembly);
 135:         _uiExtender.Enable();
 136: 
 137:         // Patch41_McmLayoutFix — flip MCM's embedded options-screen prefabs from VerticalBottomToTop
 138:         // to VerticalTopToBottom (v1.4.0 layout regression). MCM's prefabs are embedded in
 139:         // Bannerlord.MBOptionScreen and load via WidgetFactoryManager.CreateAndRegister, which bypasses
 140:         // UIExtenderEx's [PrefabExtension] hook — so this is a Harmony Postfix, not a PrefabExtension.
 141:         // MUST be applied here in OnSubModuleLoad: MCM's ResourceInjector.Inject() runs at
 142:         // OnBeforeInitialModuleScreenSetAsRoot (after every module's OnSubModuleLoad), so the Postfix
 143:         // must already be attached when MCM calls CreateAndRegister.
 144:         _harmony.PatchCategory("Patch41_McmLayoutFix");
 145: 
 146:         _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();
 147: 
 148:         // Must be first — intercepts GetLocalizedText before any game texts are resolved.
 149:         // Loads English string overrides from taom_module_strings.xml (removes hardcoded "The" articles).
 150:         _harmony.PatchCategory("Patch25_LocalizationOverride");
 151:         var pathService0 = IoC.Resolve<IPathService>();
 152:         var logger0 = IoC.Resolve<IModLogger>();
 153:         var xmlPath = System.IO.Path.Combine(pathService0.ModuleDataPath, "taom_module_strings.xml");
 154:         try
 155:         {
 156:             var overrides = LocalizationOverrideLoader.ParseOverridesFromFile(xmlPath);
 157:             foreach (var kvp in overrides)
 158:                 MBTextManager_GetLocalizedText_Patch.RegisterOverride(kvp.Key, kvp.Value);
 159:             logger0.LogInfo($"[LocalizationOverride] Registered {overrides.Count} English string overrides");
 160:         }
 161:         catch (System.Exception ex)
 162:         {
 163:             logger0.LogError($"[LocalizationOverride] Failed to load overrides: {ex.Message}");
 164:         }
 165: 
 166:         _harmony.PatchCategory("Patch18_CulturalFeats");
 167:         _harmony.PatchCategory("Patch19_CustomBattles");
 168: 
 169:         // Patch58_SkipCampaignIntro — Prefix on SandBoxGameManager.OnLoadFinished that skips the vanilla
 170:         // SandBox campaign intro video on a NEW game (mirrors the engine's own IsDevelopmentMode no-video
 171:         // bypass), dropping straight into character creation; save-loads run vanilla untouched. Applied here
 172:         // in OnSubModuleLoad (process-static one-shot) — NOT the late OnGameInitializationFinished batch —
 173:         // because the target fires during the new-game load sequence (after campaign init but before
 174:         // character creation), so the patch must already be attached before any new game can start. Any
 175:         // binding failure inside the prefix falls back to the vanilla video. See docs/features/skip-campaign-intro.md.
 176:         Features.SkipCampaignIntro.Hooks.Patch58_SkipCampaignIntro.Initialize(IoC.Resolve<IModLogger>());
 177:         _harmony.PatchCategory("Patch58_SkipCampaignIntro");
 178:         // Patch0_BattleScenes: loads TAOM's sp_battle_scenes.xml (full 0-255 map_indices coverage) so the
 179:         // TAOM_Map Main_map grid's extended indices (158-255) resolve to real battle terrains instead of
 180:         // FailedAsserting against vanilla's 1-157 table. Re-enabled 2026-06-01 (TAOM_Map ships Main_map +
 181:         // the extended XML exists; 3 patch targets verified against installed 1.4.5). In-game grid validation
 182:         // pending the worldmap_battle_scene_grid re-author. See docs/reference/worldmap-battle-scene-grid.md.
 183:         _harmony.PatchCategory("Patch0_BattleScenes");
 184:         // Remaining patches applied in OnGameInitializationFinished — View assembly must be initialized first
 185: 
 186:         var pathService = IoC.Resolve<IPathService>();
 187:         var logger = IoC.Resolve<IModLogger>();
 188:         FactionMapPaths.Initialize(pathService.ModuleRootPath, logger);
 189: 
 190:         var allianceHook = IoC.Resolve<IOnAllianceAction>();
 191:         var peaceHook = IoC.Resolve<IOnPeaceAction>();
 192:         DiplomacyIoC.InitializeHooks(allianceHook, peaceHook);
 193:         AllianceCampaignBehavior_EndAlliance_Patch.Initialize(logger);
 194:         AllianceCampaignBehavior_StartAlliance_Patch.Initialize(logger);
 195:         AllianceCampaignBehavior_AddAllianceDecision_Patch.Initialize(logger);
 196:         DeclareWarAction_ApplyInternal_Patch.Initialize(logger);
 197:         MakePeaceAction_ApplyInternal_Patch.Initialize(logger);
 198: 
 199:         var executionHook = IoC.Resolve<IOnExecutionAction>();
 200:         ExecutionIoC.InitializeHooks(executionHook);
 201: 
 202:         TroopWeightIoC.InitializeHooks(
 203:             IoC.Resolve<IOnPartyBaseNumberOfAllMembers>(),
 204:             IoC.Resolve<IOnPartyBaseNumberOfRegularMembers>(),
 205:             IoC.Resolve<IOnRecruitmentVMRefreshPartyProperties>(),
 206:             IoC.Resolve<IOnPartyVMPopulatePartyListLabel>(),
 207:             IoC.Resolve<IOnPartyUpgraderUpgradeReadyTroops>(),
 208:             IoC.Resolve<TroopWeightDisplayHook>());
 209: 
 210:         CustomBattlesIoC.InitializeHooks(
 211:             IoC.Resolve<IOnGetCustomBattleCommanders>(),
 212:             IoC.Resolve<IOnGetCustomBattleFactions>(),
 213:             IoC.Resolve<IOnGetDefaultTroopOfFormation>(),
 214:             IoC.Resolve<ISideCommanderFilter>(),
 215:             logger);
 216: 
 217:         _harmony.PatchCategory("Patch21_ShaderPrecompilation");
 218:         _shaderRunner = IoC.Resolve<ShaderPrecompileRunner>();
 219:         ShaderPrecompilationIoC.InitializeHooks(logger, _shaderRunner);
 220: 
 221:         _harmony.PatchCategory("Patch22_ArmyTargeting");
 222:         // Patch49: Finalizer guarding vanilla Army.FindBestGatheringSettlementAndMoveTheLeader,
 223:         // which NREs (Army.cs:726 settlement.GatePosition / 659 Kingdom.Settlements, v1.4.6) when a
 224:         // besieger army can't resolve a gathering fortification — a map-tick CTD on siege start.
 225:         // No TAOM patch is on the stack; aggressive Patch22 targeting just makes it more reachable.
 226:         // Crash report 2026-06-17. See the patch's doc-comment.
 227:         _harmony.PatchCategory("Patch49_ArmyGatheringNreGuard");
 228:         // Patch59: CaravanTrade — four postfixes on CaravansCampaignBehavior private methods
 229:         // (war gate, destination re-weight, range envelope, budget-factor floor) so AI/player caravans
 230:         // range past the local town cluster instead of shuttling. Campaign-behavior target, so applied
 231:         // in this campaign-phase block alongside the other AI patches.
 232:         _harmony.PatchCategory("Patch59_CaravanTrade");
 233:         _harmony.PatchCategory("Patch30_MixedFormations");
 234:         // Patch_MissionTime_SetMovementOrder (shared by Patch31_SmartCavalryAI +
 235:         // Patch35_CompanionTactics' Formation.SetMovementOrder hook) is applied in
 236:         // OnMissionBehaviorInitialize — MovementOrder.cctor reads Mission.Current.CurrentTime,
 237:         // which is null during OnSubModuleLoad and would crash JIT prep with NRE.
 238: 
 239:         var bannerColorConfig = IoC.Resolve<IBannerColorConfigProvider>();
 240:         var bannerColorService = IoC.Resolve<IBannerColorService>();
 241:         var bannerHeroAdapter = IoC.Resolve<IBannerHeroAdapter>();
 242: 
 243:         Banner_TryGetBannerDataFromCode_Transpiler.Initialize(bannerColorConfig, logger);
 244:         Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(bannerColorService);
 245:         Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(logger);
 246:         Clan_UpdateBannerColor_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 247:         Banner_GetFirstIconColor_Patch.Initialize(bannerColorService);
 248:         BannerEditorView_OnTick_Patch.Initialize(bannerColorService, logger);
 249:         CampaignUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 250:         SandBoxUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 251:         SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 252:         PartyVM_RefreshCurrentCharacterInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 253:         HeroViewModel_FillFrom_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 254:         PartyCharacterVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 255:         ClanPartyItemVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 256:         CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Initialize(bannerColorService);
 257:         var agentColorStore = IoC.Resolve<IAgentColorStore>();
 258:         Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
 259:         Agent_EquipItemsFromSpawnEquipment_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
 260:         AgentVisuals_Create_Patch.Initialize(bannerColorService);
 501:         campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));
 502:     }
 503: 
 504:     // Cultural feat models — Phase 9b #144/#176: dispatch logic extracted to
 505:     // ICulturalFeatsService. Each model is a thin boundary that converts
 506:     // CultureObject → ICultureFeatAdapter and delegates (gamemodels.md rule 4).
 507:     // `culturalFeats` is passed in (hoisted resolve, shared with TaomVolunteerModel).
 508:     private static void RegisterCulturalFeatModels(
 509:         CampaignGameStarter campaignStarter,
 510:         TAOM.Features.CulturalFeats.ICulturalFeatsService culturalFeats,
 511:         ICareerPassiveService careerPassives)
 512:     {
 513:         campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
 514:         campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
 515:         campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
 516:         campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
 517:         campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
 518:         campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
 519:         campaignStarter.AddModel(new TaomCaravanModel(culturalFeats, IoC.Resolve<TAOM.Features.CaravanTrade.ICaravanTradeService>()));
 520:         campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
 521:         campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
 522:         campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
 523:         campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
 524:         campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
 525:         campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
 526:         campaignStarter.AddModel(new TaomSettlementFoodModel(IoC.Resolve<ISettlementFoodService>(), IoC.Resolve<ISettlementFoodConfigProvider>()));
 527:         campaignStarter.AddModel(new TaomSettlementEconomyModel(IoC.Resolve<ISettlementEconomyService>(), IoC.Resolve<ISettlementEconomyConfigProvider>()));
 528:         campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
 529:         campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
 530:         campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));
 531:         campaignStarter.AddModel(new TaomClanFinanceModel(culturalFeats));
 532:         campaignStarter.AddModel(new TaomRaidModel(culturalFeats, careerPassives));
 533:         campaignStarter.AddModel(new TaomNotableSpawnModel(culturalFeats));
 534:     }
 535: 

codex
So far the hook bodies are thin and wrapped, and the patch category is applied in the campaign-phase block. I’m now checking tests and vanilla ownership/distance contracts, since the companion-owned caravan question and double-distance cost depend on engine semantics rather than TAOM code style.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'TAOM.Tests\\Features\\CaravanTrade\\CaravanTradeServiceTests.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'TAOM.Tests\\Features\\CaravanTrade\\CaravanTradeBindingTests.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party.PartyComponents\\CaravanPartyComponent.cs' | ForEach-Object { if ("'$i -le 260) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'TAOM.Tests\\Features\\CaravanTrade\\CaravanTradeConfigProviderTests.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultMapDistanceModel.cs' | ForEach-Object { if ("'$i -le 220) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 763ms:
   1: using Microsoft.VisualStudio.TestTools.UnitTesting;
   2: using NSubstitute;
   3: using TAOM.Core.Logging;
   4: using TAOM.Features.CaravanTrade;
   5: using TAOM.Features.Execution;
   6: 
   7: namespace TAOM.Tests.Features.CaravanTrade;
   8: 
   9: [TestClass]
  10: public class CaravanTradeServiceTests
  11: {
  12:     private ICaravanTradeSettingsProvider _settings = null!;
  13:     private IAlignmentService _alignment = null!;
  14:     private IModLogger _logger = null!;
  15:     private CaravanTradeService _sut = null!;
  16: 
  17:     [TestInitialize]
  18:     public void Setup()
  19:     {
  20:         _settings = Substitute.For<ICaravanTradeSettingsProvider>();
  21:         _alignment = Substitute.For<IAlignmentService>();
  22:         _logger = Substitute.For<IModLogger>();
  23: 
  24:         // Default: feature on, applies to everyone, mid-range tuning.
  25:         _settings.Enabled.Returns(true);
  26:         _settings.ApplyToPlayerCaravans.Returns(true);
  27:         _settings.RangeMultiplier.Returns(1.6f);
  28:         _settings.DistanceDecayExponent.Returns(0.5f);
  29:         _settings.NearFieldFlattenDays.Returns(2.0f);
  30:         _settings.MaxCompensation.Returns(6.0f);
  31:         _settings.AntiShuttlePenalty.Returns(0.35f);
  32:         _settings.WarTradePolicy.Returns(WarTradePolicy.SameAlignmentAndNeutral);
  33:         _settings.BudgetFactorFloor.Returns(0.35f);
  34:         _settings.InitialTradeGold.Returns(15000);
  35:         _settings.MaxGoldPerCategory.Returns(1500);
  36: 
  37:         _sut = new CaravanTradeService(_settings, _alignment, _logger);
  38:     }
  39: 
  40:     // ---------------- ReweightTradeScore ----------------
  41: 
  42:     [TestMethod]
  43:     public void ReweightTradeScore_Disabled_ReturnsRawScore()
  44:     {
  45:         _settings.Enabled.Returns(false);
  46:         Assert.AreEqual(42f, _sut.ReweightTradeScore(42f, 3f, false, false, false, false), 0.0001f);
  47:     }
  48: 
  49:     [TestMethod]
  50:     public void ReweightTradeScore_PlayerCaravanWhenPlayerScopeOff_ReturnsRawScore()
  51:     {
  52:         _settings.ApplyToPlayerCaravans.Returns(false);
  53:         Assert.AreEqual(42f, _sut.ReweightTradeScore(42f, 3f, false, false, false, isPlayerCaravan: true), 0.0001f);
  54:     }
  55: 
  56:     [TestMethod]
  57:     public void ReweightTradeScore_RejectionScore_PassesThroughUnchanged()
  58:     {
  59:         // Vanilla returns -1 for non-navigable / distance-cut rejects.
  60:         Assert.AreEqual(-1f, _sut.ReweightTradeScore(-1f, 3f, false, false, false, false), 0.0001f);
  61:         Assert.AreEqual(0f, _sut.ReweightTradeScore(0f, 3f, false, false, false, false), 0.0001f);
  62:     }
  63: 
  64:     [TestMethod]
  65:     public void ReweightTradeScore_NaNRawScore_ReturnsRawScore()
  66:     {
  67:         // Positive-requirement gate: NaN must fail into the vanilla passthrough.
  68:         Assert.IsTrue(float.IsNaN(_sut.ReweightTradeScore(float.NaN, 3f, false, false, false, false)));
  69:     }
  70: 
  71:     [TestMethod]
  72:     public void ReweightTradeScore_NonPositiveDays_ReturnsRawScore()
  73:     {
  74:         Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 0f, false, false, false, false), 0.0001f);
  75:     }
  76: 
  77:     [TestMethod]
  78:     public void ReweightTradeScore_NaNDays_ReturnsRawScore()
  79:     {
  80:         Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, float.NaN, false, false, false, false), 0.0001f);
  81:     }
  82: 
  83:     [TestMethod]
  84:     public void ReweightTradeScore_NavalCaravan_ReturnsRawScoreUnchanged()
  85:     {
  86:         // Naval uses a different vanilla distance factor; the shuttle is a land problem.
  87:         Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 3f, isNaval: true, false, false, false), 0.0001f);
  88:     }
  89: 
  90:     [TestMethod]
  91:     public void ReweightTradeScore_HomeTown_ReturnsRawScoreUnchanged()
  92:     {
  93:         Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, false, false), 0.0001f);
  94:     }
  95: 
  96:     [TestMethod]
  97:     public void ReweightTradeScore_LandTown_AppliesStripAndReweight()
  98:     {
  99:         // m = days / (flatten+days)^alpha = 3 / (2+3)^0.5 = 3 / 2.23607 = 1.34164; result = 10 * 1.34164.
 100:         float result = _sut.ReweightTradeScore(10f, 3f, false, false, false, false);
 101:         Assert.AreEqual(13.4164f, result, 0.001f);
 102:     }
 103: 
 104:     [TestMethod]
 105:     public void ReweightTradeScore_MoreProfitableFarTown_BeatsCloseTown_WhereVanillaWouldNot()
 106:     {
 107:         // Vanilla rawScore already embeds 1/days. Near town: days=1, base profit 100 -> rawScore 100.
 108:         // Far town: days=5, base profit 200 -> rawScore 40. Vanilla picks near (100 > 40) despite the
 109:         // far town being twice as profitable per trip; after the reweight the far town wins.
 110:         float near = _sut.ReweightTradeScore(100f, 1f, false, false, false, false);
 111:         float far = _sut.ReweightTradeScore(40f, 5f, false, false, false, false);
 112:         Assert.IsTrue(far > near, $"expected far({far}) > near({near}) after reweight");
 113:     }
 114: 
 115:     [TestMethod]
 116:     public void ReweightTradeScore_EqualBaseProfit_StillPrefersNear_ButCompressed()
 117:     {
 118:         // Equal base profit P0=100: near rawScore=100/1, far rawScore=100/5=20.
 119:         float near = _sut.ReweightTradeScore(100f, 1f, false, false, false, false);
 120:         float far = _sut.ReweightTradeScore(20f, 5f, false, false, false, false);
 121:         Assert.IsTrue(near > far, "near should still edge out an equally-profitable far town");
 122:         Assert.IsTrue(near / far < 2.0f, $"advantage should be compressed well below vanilla's 5x (was {near / far:F2})");
 123:     }
 124: 
 125:     [TestMethod]
 126:     public void ReweightTradeScore_VeryFarTown_MultiplierClampedToMaxCompensation()
 127:     {
 128:         // m = 1000/(2+1000)^0.5 = 31.6, clamped to maxCompensation 6 -> result = 10 * 6.
 129:         float result = _sut.ReweightTradeScore(10f, 1000f, false, false, false, false);
 130:         Assert.AreEqual(60f, result, 0.01f);
 131:     }
 132: 
 133:     [TestMethod]
 134:     public void ReweightTradeScore_JustLeftTown_AppliesAntiShuttlePenalty()
 135:     {
 136:         float without = _sut.ReweightTradeScore(10f, 3f, false, false, isJustLeftTown: false, false);
 137:         float with = _sut.ReweightTradeScore(10f, 3f, false, false, isJustLeftTown: true, false);
 138:         // penalty 0.35 -> with == without * (1 - 0.35).
 139:         Assert.AreEqual(without * 0.65f, with, 0.001f);
 140:     }
 141: 
 142:     // ---------------- ScaleVeryFarDistance ----------------
 143: 
 144:     [TestMethod]
 145:     public void ScaleVeryFarDistance_Disabled_ReturnsVanilla()
 146:     {
 147:         _settings.Enabled.Returns(false);
 148:         Assert.AreEqual(12f, _sut.ScaleVeryFarDistance(12f), 0.0001f);
 149:     }
 150: 
 151:     [TestMethod]
 152:     public void ScaleVeryFarDistance_Enabled_ScalesByRangeMultiplier()
 153:     {
 154:         Assert.AreEqual(19.2f, _sut.ScaleVeryFarDistance(12f), 0.0001f); // 12 * 1.6
 155:     }
 156: 
 157:     // ---------------- AllowWartimeTrade ----------------
 158: 
 159:     [TestMethod]
 160:     public void AllowWartimeTrade_Disabled_ReturnsFalse()
 161:     {
 162:         _settings.Enabled.Returns(false);
 163:         Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "rohan", false));
 164:     }
 165: 
 166:     [TestMethod]
 167:     public void AllowWartimeTrade_PlayerCaravanWhenPlayerScopeOff_ReturnsFalse()
 168:     {
 169:         _settings.ApplyToPlayerCaravans.Returns(false);
 170:         Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "rohan", isPlayerCaravan: true));
 171:     }
 172: 
 173:     [TestMethod]
 174:     public void AllowWartimeTrade_PolicyNone_ReturnsFalse()
 175:     {
 176:         _settings.WarTradePolicy.Returns(WarTradePolicy.None);
 177:         Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "rohan", false));
 178:     }
 179: 
 180:     [TestMethod]
 181:     public void AllowWartimeTrade_PolicyIgnoreWar_ReturnsTrue()
 182:     {
 183:         _settings.WarTradePolicy.Returns(WarTradePolicy.IgnoreWar);
 184:         Assert.IsTrue(_sut.AllowWartimeTrade("gondor", "mordor", false));
 185:     }
 186: 
 187:     [TestMethod]
 188:     public void AllowWartimeTrade_SameAlignment_SameSide_ReturnsTrue()
 189:     {
 190:         // Free caravan reaching another Free town despite the war.
 191:         _alignment.GetKingdomSide("gondor").Returns(FactionSide.Free);
 192:         _alignment.GetKingdomSide("rohan").Returns(FactionSide.Free);
 193:         Assert.IsTrue(_sut.AllowWartimeTrade("gondor", "rohan", false));
 194:     }
 195: 
 196:     [TestMethod]
 197:     public void AllowWartimeTrade_SameAlignment_OppositeSides_ReturnsFalse()
 198:     {
 199:         // A Free caravan must NOT resupply an Evil town.
 200:         _alignment.GetKingdomSide("gondor").Returns(FactionSide.Free);
 201:         _alignment.GetKingdomSide("mordor").Returns(FactionSide.Evil);
 202:         Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "mordor", false));
 203:     }
 204: 
 205:     [TestMethod]
 206:     public void AllowWartimeTrade_SameAlignment_NeutralCaravan_ReturnsTrue()
 207:     {
 208:         // Neutral (Umbar etc.) trades with anyone — regression guard for the AreEnemyAlignments
 209:         // inversion (which treats Neutral as an enemy of everyone). This is the bug the deep-review
 210:         // data-flow agent caught: the shipped default policy silently blocked neutral trade.
 211:         _alignment.GetKingdomSide("umbar").Returns(FactionSide.Neutral);
 212:         _alignment.GetKingdomSide("gondor").Returns(FactionSide.Free);
 213:         Assert.IsTrue(_sut.AllowWartimeTrade("umbar", "gondor", false));
 214:     }
 215: 
 216:     [TestMethod]
 217:     public void AllowWartimeTrade_SameAlignment_NeutralTarget_ReturnsTrue()
 218:     {
 219:         _alignment.GetKingdomSide("mordor").Returns(FactionSide.Evil);
 220:         _alignment.GetKingdomSide("umbar").Returns(FactionSide.Neutral);
 221:         Assert.IsTrue(_sut.AllowWartimeTrade("mordor", "umbar", false));
 222:     }
 223: 
 224:     // ---------------- ApplyBudgetFactorFloor ----------------
 225: 
 226:     [TestMethod]
 227:     public void ApplyBudgetFactorFloor_Disabled_ReturnsVanilla()
 228:     {
 229:         _settings.Enabled.Returns(false);
 230:         Assert.AreEqual(0.1f, _sut.ApplyBudgetFactorFloor(0.1f, false), 0.0001f);
 231:     }
 232: 
 233:     [TestMethod]
 234:     public void ApplyBudgetFactorFloor_PlayerScopeOff_ReturnsVanilla()
 235:     {
 236:         _settings.ApplyToPlayerCaravans.Returns(false);
 237:         Assert.AreEqual(0.1f, _sut.ApplyBudgetFactorFloor(0.1f, isPlayerCaravan: true), 0.0001f);
 238:     }
 239: 
 240:     [TestMethod]
 241:     public void ApplyBudgetFactorFloor_NaN_ReturnsVanilla()
 242:     {
 243:         Assert.IsTrue(float.IsNaN(_sut.ApplyBudgetFactorFloor(float.NaN, false)));
 244:     }
 245: 
 246:     [TestMethod]
 247:     public void ApplyBudgetFactorFloor_BelowFloor_ReturnsFloor()
 248:     {
 249:         Assert.AreEqual(0.35f, _sut.ApplyBudgetFactorFloor(0.1f, false), 0.0001f);
 250:     }
 251: 
 252:     [TestMethod]
 253:     public void ApplyBudgetFactorFloor_AboveFloor_ReturnsVanilla()
 254:     {
 255:         Assert.AreEqual(0.8f, _sut.ApplyBudgetFactorFloor(0.8f, false), 0.0001f);
 256:     }
 257: 
 258:     // ---------------- ResolveInitialTradeGold ----------------
 259: 
 260:     [TestMethod]
 261:     public void ResolveInitialTradeGold_Disabled_ReturnsVanilla()
 262:     {
 263:         _settings.Enabled.Returns(false);
 264:         Assert.AreEqual(10000, _sut.ResolveInitialTradeGold(10000, false));
 265:     }
 266: 
 267:     [TestMethod]
 268:     public void ResolveInitialTradeGold_PlayerScopeOff_ReturnsVanilla()
 269:     {
 270:         _settings.ApplyToPlayerCaravans.Returns(false);
 271:         Assert.AreEqual(10000, _sut.ResolveInitialTradeGold(10000, isPlayerCaravan: true));
 272:     }
 273: 
 274:     [TestMethod]
 275:     public void ResolveInitialTradeGold_VanillaBelowFloor_ReturnsFloor()
 276:     {
 277:         Assert.AreEqual(15000, _sut.ResolveInitialTradeGold(10000, false));
 278:     }
 279: 
 280:     [TestMethod]
 281:     public void ResolveInitialTradeGold_VanillaAboveFloor_NeverLowers()
 282:     {
 283:         // Large caravan / main hero bonus must be preserved.
 284:         Assert.AreEqual(22500, _sut.ResolveInitialTradeGold(22500, false));
 285:     }
 286: 
 287:     // ---------------- ResolveMaxGoldPerCategory ----------------
 288: 
 289:     [TestMethod]
 290:     public void ResolveMaxGoldPerCategory_Disabled_ReturnsVanilla()
 291:     {
 292:         _settings.Enabled.Returns(false);
 293:         Assert.AreEqual(1500, _sut.ResolveMaxGoldPerCategory(1500, false));
 294:     }
 295: 
 296:     [TestMethod]
 297:     public void ResolveMaxGoldPerCategory_Enabled_ReturnsConfiguredValue()
 298:     {
 299:         _settings.MaxGoldPerCategory.Returns(2500);
 300:         Assert.AreEqual(2500, _sut.ResolveMaxGoldPerCategory(1500, false));
 301:     }
 302: }

 succeeded in 721ms:
   1: using HarmonyLib;
   2: using Microsoft.VisualStudio.TestTools.UnitTesting;
   3: using TAOM.Tests.Migration;
   4: 
   5: namespace TAOM.Tests.Features.CaravanTrade;
   6: 
   7: /// <summary>
   8: /// Drift-guards for the CaravanTrade feature's private-member bindings. The four
   9: /// <c>[HarmonyPatch]</c> targets auto-enroll in <c>HarmonyPatchBindingTests</c>, but the two private
  10: /// FieldRef targets (<c>_defaultCaravanVeryFarCache</c> / <c>_navalCaravanVeryFarCache</c>), the
  11: /// distance helper called directly by the score hook, and the <c>DefaultCaravanModel</c> override
  12: /// targets are not covered there. A rename on an engine bump would silently no-op (fields) or break
  13: /// the build (helper); these tests catch a field/method rename offline first. Types resolved by name
  14: /// (mirrors how Harmony binds), gated on the installed engine like the other binding tests.
  15: /// </summary>
  16: [TestClass]
  17: public class CaravanTradeBindingTests
  18: {
  19:     private const string CaravanBehavior = "TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior";
  20:     private const string DefaultCaravanModelName = "TaleWorlds.CampaignSystem.GameComponents.DefaultCaravanModel";
  21:     private const string AiHelperName = "Helpers.AiHelper";
  22: 
  23:     private static bool _gameLoaded;
  24: 
  25:     [ClassInitialize]
  26:     public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();
  27: 
  28:     private static System.Type RequireType(string name)
  29:     {
  30:         if (!_gameLoaded)
  31:             Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
  32:         var type = AccessTools.TypeByName(name);
  33:         Assert.IsNotNull(type, name + " did not resolve against the installed engine.");
  34:         return type;
  35:     }
  36: 
  37:     [TestMethod]
  38:     [TestCategory("BindingVerification")]
  39:     public void PrivateMethodTargets_Resolve_AgainstInstalledEngine()
  40:     {
  41:         var t = RequireType(CaravanBehavior);
  42:         foreach (var name in new[] { "CanTradeWith", "GetTradeScoreForTown", "CacheVeryFarDistances", "CalculateBudgetFactor" })
  43:             Assert.IsNotNull(AccessTools.Method(t, name),
  44:                 $"CaravansCampaignBehavior.{name} did not resolve — a CaravanTrade postfix target drifted.");
  45:     }
  46: 
  47:     [TestMethod]
  48:     [TestCategory("BindingVerification")]
  49:     public void VeryFarCacheFields_Resolve_AsFloat_AgainstInstalledEngine()
  50:     {
  51:         var t = RequireType(CaravanBehavior);
  52:         foreach (var name in new[] { "_defaultCaravanVeryFarCache", "_navalCaravanVeryFarCache" })
  53:         {
  54:             var field = AccessTools.Field(t, name);
  55:             Assert.IsNotNull(field,
  56:                 $"CaravansCampaignBehavior.{name} did not resolve — the CacheVeryFarDistances FieldRef would silently no-op.");
  57:             Assert.AreEqual(typeof(float), field.FieldType, $"CaravansCampaignBehavior.{name} is no longer a float.");
  58:         }
  59:     }
  60: 
  61:     [TestMethod]
  62:     [TestCategory("BindingVerification")]
  63:     public void DistanceHelper_Resolves_AgainstInstalledEngine()
  64:     {
  65:         var t = RequireType(AiHelperName);
  66:         Assert.IsNotNull(
  67:             AccessTools.Method(t, "GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty"),
  68:             "AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty did not resolve — the score-reweight distance recompute would break.");
  69:     }
  70: 
  71:     [TestMethod]
  72:     [TestCategory("BindingVerification")]
  73:     public void CaravanModelOverrideTargets_Resolve_AgainstInstalledEngine()
  74:     {
  75:         var t = RequireType(DefaultCaravanModelName);
  76:         Assert.IsNotNull(AccessTools.Method(t, "GetInitialTradeGold"),
  77:             "DefaultCaravanModel.GetInitialTradeGold did not resolve — TaomCaravanModel's diversity override target is gone.");
  78:         Assert.IsNotNull(AccessTools.Method(t, "GetMaxGoldToSpendOnOneItemCategory"),
  79:             "DefaultCaravanModel.GetMaxGoldToSpendOnOneItemCategory did not resolve — TaomCaravanModel's diversity override target is gone.");
  80:     }
  81: }

 succeeded in 740ms:
   1: using System.IO;
   2: using Microsoft.VisualStudio.TestTools.UnitTesting;
   3: using NSubstitute;
   4: using TAOM.Core.Infrastructure;
   5: using TAOM.Core.Logging;
   6: using TAOM.Features.CaravanTrade;
   7: 
   8: namespace TAOM.Tests.Features.CaravanTrade;
   9: 
  10: [TestClass]
  11: public class CaravanTradeConfigProviderTests
  12: {
  13:     private string _tempDir = null!;
  14:     private string _configDir = null!;
  15:     private IPathService _pathService = null!;
  16:     private IModLogger _logger = null!;
  17:     private CaravanTradeConfigProvider _sut = null!;
  18: 
  19:     [TestInitialize]
  20:     public void Setup()
  21:     {
  22:         _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CaravanTrade_" + Path.GetRandomFileName());
  23:         _configDir = Path.Combine(_tempDir, "caravan_trade");
  24:         Directory.CreateDirectory(_configDir);
  25: 
  26:         _pathService = Substitute.For<IPathService>();
  27:         _pathService.ModuleDataPath.Returns(_tempDir);
  28:         _logger = Substitute.For<IModLogger>();
  29: 
  30:         _sut = new CaravanTradeConfigProvider(_pathService, _logger);
  31:     }
  32: 
  33:     [TestCleanup]
  34:     public void Cleanup()
  35:     {
  36:         if (Directory.Exists(_tempDir))
  37:             Directory.Delete(_tempDir, true);
  38:     }
  39: 
  40:     private void WriteConfig(string json) =>
  41:         File.WriteAllText(Path.Combine(_configDir, "caravan_trade_config.json"), json);
  42: 
  43:     [TestMethod]
  44:     public void GetConfig_ValidJson_ParsesAllFieldsAndLogsInfo()
  45:     {
  46:         WriteConfig(@"{
  47:   ""enabled"": true,
  48:   ""applyToPlayerCaravans"": false,
  49:   ""rangeMultiplier"": 2.0,
  50:   ""distanceDecayExponent"": 0.6,
  51:   ""nearFieldFlattenDays"": 3.0,
  52:   ""maxCompensation"": 8.0,
  53:   ""antiShuttlePenalty"": 0.5,
  54:   ""warTradePolicy"": ""IgnoreWar"",
  55:   ""budgetFactorFloor"": 0.4,
  56:   ""initialTradeGold"": 20000,
  57:   ""maxGoldPerCategory"": 2500
  58: }");
  59: 
  60:         var c = _sut.GetConfig();
  61: 
  62:         Assert.IsTrue(c.Enabled);
  63:         Assert.IsFalse(c.ApplyToPlayerCaravans);
  64:         Assert.AreEqual(2.0f, c.RangeMultiplier, 0.0001f);
  65:         Assert.AreEqual(0.6f, c.DistanceDecayExponent, 0.0001f);
  66:         Assert.AreEqual(3.0f, c.NearFieldFlattenDays, 0.0001f);
  67:         Assert.AreEqual(8.0f, c.MaxCompensation, 0.0001f);
  68:         Assert.AreEqual(0.5f, c.AntiShuttlePenalty, 0.0001f);
  69:         Assert.AreEqual("IgnoreWar", c.WarTradePolicy);
  70:         Assert.AreEqual(0.4f, c.BudgetFactorFloor, 0.0001f);
  71:         Assert.AreEqual(20000, c.InitialTradeGold);
  72:         Assert.AreEqual(2500, c.MaxGoldPerCategory);
  73:         _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
  74:     }
  75: 
  76:     [TestMethod]
  77:     public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
  78:     {
  79:         var c = _sut.GetConfig();
  80: 
  81:         Assert.AreEqual(1.6f, c.RangeMultiplier, 0.0001f);
  82:         Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
  83:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
  84:     }
  85: 
  86:     [TestMethod]
  87:     public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
  88:     {
  89:         WriteConfig("not valid json {{{");
  90: 
  91:         var c = _sut.GetConfig();
  92: 
  93:         Assert.AreEqual(1.6f, c.RangeMultiplier, 0.0001f);
  94:         _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
  95:     }
  96: 
  97:     [TestMethod]
  98:     public void GetConfig_PartialJson_MergesWithDefaults()
  99:     {
 100:         WriteConfig(@"{ ""rangeMultiplier"": 2.5 }");
 101: 
 102:         var c = _sut.GetConfig();
 103: 
 104:         Assert.AreEqual(2.5f, c.RangeMultiplier, 0.0001f);
 105:         Assert.AreEqual(0.5f, c.DistanceDecayExponent, 0.0001f);
 106:         Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
 107:     }
 108: 
 109:     [TestMethod]
 110:     public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
 111:     {
 112:         WriteConfig(@"{ ""rangeMultiplier"": 2.0 }");
 113:         Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
 114:     }
 115: 
 116:     [TestMethod]
 117:     public void GetConfig_RangeMultiplierBelowOne_RevertsAndWarns()
 118:     {
 119:         // Below 1 shrinks the range below vanilla — worsening the very shuttle this feature fixes.
 120:         WriteConfig(@"{ ""rangeMultiplier"": 0.5 }");
 121:         Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
 122:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
 123:     }
 124: 
 125:     [TestMethod]
 126:     public void GetConfig_RangeMultiplierOversized_RevertsAndWarns()
 127:     {
 128:         WriteConfig(@"{ ""rangeMultiplier"": 12 }");
 129:         Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
 130:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
 131:     }
 132: 
 133:     [TestMethod]
 134:     public void GetConfig_NaNRangeMultiplier_RevertsToFiniteDefault()
 135:     {
 136:         WriteConfig(@"{ ""rangeMultiplier"": NaN }");
 137:         Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
 138:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
 139:     }
 140: 
 141:     [TestMethod]
 142:     public void GetConfig_DecayExponentZero_RevertsAndWarns()
 143:     {
 144:         WriteConfig(@"{ ""distanceDecayExponent"": 0 }");
 145:         Assert.AreEqual(0.5f, _sut.GetConfig().DistanceDecayExponent, 0.0001f);
 146:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("distanceDecayExponent")));
 147:     }
 148: 
 149:     [TestMethod]
 150:     public void GetConfig_InfinityNearFieldFlatten_RevertsAndWarns()
 151:     {
 152:         WriteConfig(@"{ ""nearFieldFlattenDays"": Infinity }");
 153:         Assert.AreEqual(2.0f, _sut.GetConfig().NearFieldFlattenDays, 0.0001f);
 154:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("nearFieldFlattenDays")));
 155:     }
 156: 
 157:     [TestMethod]
 158:     public void GetConfig_MaxCompensationBelowOne_RevertsAndWarns()
 159:     {
 160:         WriteConfig(@"{ ""maxCompensation"": 0.5 }");
 161:         Assert.AreEqual(6.0f, _sut.GetConfig().MaxCompensation, 0.0001f);
 162:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxCompensation")));
 163:     }
 164: 
 165:     [TestMethod]
 166:     public void GetConfig_AntiShuttlePenaltyAboveOne_RevertsAndWarns()
 167:     {
 168:         // > 1 would flip the score sign.
 169:         WriteConfig(@"{ ""antiShuttlePenalty"": 1.5 }");
 170:         Assert.AreEqual(0.35f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
 171:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
 172:     }
 173: 
 174:     [TestMethod]
 175:     public void GetConfig_NegativeAntiShuttlePenalty_RevertsAndWarns()
 176:     {
 177:         // < 0 would reward returning to the town just left.
 178:         WriteConfig(@"{ ""antiShuttlePenalty"": -0.2 }");
 179:         Assert.AreEqual(0.35f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
 180:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
 181:     }
 182: 
 183:     [TestMethod]
 184:     public void GetConfig_UnknownWarTradePolicy_RevertsAndWarns()
 185:     {
 186:         // The M1 typo trap: an unknown string must revert, not silently take the service switch default.
 187:         WriteConfig(@"{ ""warTradePolicy"": ""IgnoreWarr"" }");
 188:         Assert.AreEqual("SameAlignmentAndNeutral", _sut.GetConfig().WarTradePolicy);
 189:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("warTradePolicy")));
 190:     }
 191: 
 192:     [TestMethod]
 193:     public void GetConfig_KnownWarTradePolicyCaseInsensitive_Accepted()
 194:     {
 195:         WriteConfig(@"{ ""warTradePolicy"": ""ignorewar"" }");
 196:         Assert.AreEqual("ignorewar", _sut.GetConfig().WarTradePolicy);
 197:         _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("warTradePolicy")));
 198:     }
 199: 
 200:     [TestMethod]
 201:     public void GetConfig_BudgetFactorFloorAboveOne_RevertsAndWarns()
 202:     {
 203:         WriteConfig(@"{ ""budgetFactorFloor"": 1.5 }");
 204:         Assert.AreEqual(0.35f, _sut.GetConfig().BudgetFactorFloor, 0.0001f);
 205:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("budgetFactorFloor")));
 206:     }
 207: 
 208:     [TestMethod]
 209:     public void GetConfig_InitialTradeGoldTooLow_RevertsAndWarns()
 210:     {
 211:         WriteConfig(@"{ ""initialTradeGold"": 500 }");
 212:         Assert.AreEqual(15000, _sut.GetConfig().InitialTradeGold);
 213:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("initialTradeGold")));
 214:     }
 215: 
 216:     [TestMethod]
 217:     public void GetConfig_MaxGoldPerCategoryTooHigh_RevertsAndWarns()
 218:     {
 219:         WriteConfig(@"{ ""maxGoldPerCategory"": 999999 }");
 220:         Assert.AreEqual(1500, _sut.GetConfig().MaxGoldPerCategory);
 221:         _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxGoldPerCategory")));
 222:     }
 223: 
 224:     [TestMethod]
 225:     public void GetConfig_AllValid_LogsInfoNotWarning()
 226:     {
 227:         WriteConfig(@"{ ""rangeMultiplier"": 1.6, ""warTradePolicy"": ""None"" }");
 228: 
 229:         _sut.GetConfig();
 230: 
 231:         _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
 232:         _logger.DidNotReceive().LogWarning(Arg.Any<string>());
 233:     }
 234: }

 succeeded in 780ms:
   1: using TaleWorlds.CampaignSystem.ComponentInterfaces;
   2: using TaleWorlds.CampaignSystem.Party;
   3: using TaleWorlds.CampaignSystem.Settlements;
   4: using TaleWorlds.Library;
   5: 
   6: namespace TaleWorlds.CampaignSystem.GameComponents;
   7: 
   8: public class DefaultMapDistanceModel : MapDistanceModel
   9: {
  10: 	private INavigationCache _navigationCache;
  11: 
  12: 	public override int RegionSwitchCostFromLandToSea => 0;
  13: 
  14: 	public override int RegionSwitchCostFromSeaToLand => 0;
  15: 
  16: 	public override float MaximumSpawnDistanceForCompanionsAfterDisband => 150f;
  17: 
  18: 	public override void RegisterDistanceCache(MobileParty.NavigationType navigationCapability, INavigationCache cacheToRegister)
  19: 	{
  20: 		_navigationCache = cacheToRegister;
  21: 		cacheToRegister.FinalizeInitialization();
  22: 	}
  23: 
  24: 	public override float GetMaximumDistanceBetweenTwoConnectedSettlements(MobileParty.NavigationType navigationCapabilities)
  25: 	{
  26: 		return _navigationCache?.MaximumDistanceBetweenTwoConnectedSettlements ?? 0f;
  27: 	}
  28: 
  29: 	public override float GetLandRatioOfPathBetweenSettlements(Settlement fromSettlement, Settlement toSettlement, bool isFromPort, bool isTargetingPort)
  30: 	{
  31: 		if (_navigationCache != null)
  32: 		{
  33: 			_navigationCache.GetSettlementToSettlementDistanceWithLandRatio(fromSettlement, isAtSea1: false, toSettlement, isAtSea2: false, out var landRatio);
  34: 			return landRatio;
  35: 		}
  36: 		return 1f;
  37: 	}
  38: 
  39: 	public override float GetDistance(Settlement fromSettlement, Settlement toSettlement, bool isFromPort = false, bool isTargetingPort = false, MobileParty.NavigationType navigationCapability = MobileParty.NavigationType.Default)
  40: 	{
  41: 		float landRatio;
  42: 		return GetDistance(fromSettlement, toSettlement, isFromPort, isTargetingPort, MobileParty.NavigationType.Default, out landRatio);
  43: 	}
  44: 
  45: 	public override float GetDistance(Settlement fromSettlement, Settlement toSettlement, bool isFromPort, bool isTargetingPort, MobileParty.NavigationType navigationCapability, out float landRatio)
  46: 	{
  47: 		float result = float.MaxValue;
  48: 		landRatio = 1f;
  49: 		if (fromSettlement != null && toSettlement != null)
  50: 		{
  51: 			if (fromSettlement != toSettlement)
  52: 			{
  53: 				return _navigationCache.GetSettlementToSettlementDistanceWithLandRatio(fromSettlement, isFromPort, toSettlement, isTargetingPort, out landRatio);
  54: 			}
  55: 			result = 0f;
  56: 		}
  57: 		return result;
  58: 	}
  59: 
  60: 	public override float GetDistance(MobileParty fromMobileParty, Settlement toSettlement, bool isTargetingPort, MobileParty.NavigationType customCapability, out float estimatedLandRatio)
  61: 	{
  62: 		float value = 100000000f;
  63: 		estimatedLandRatio = 1f;
  64: 		if (fromMobileParty.CurrentNavigationFace.FaceIndex == toSettlement.GatePosition.Face.FaceIndex)
  65: 		{
  66: 			if (Campaign.Current.Models.PartyNavigationModel.IsTerrainTypeValidForNavigationType(Campaign.Current.MapSceneWrapper.GetFaceTerrainType(fromMobileParty.Position.Face), MobileParty.NavigationType.Default))
  67: 			{
  68: 				value = fromMobileParty.Position.Distance(toSettlement.GatePosition);
  69: 			}
  70: 		}
  71: 		else if (fromMobileParty.IsCurrentlyAtSea)
  72: 		{
  73: 			value = 100000000f;
  74: 		}
  75: 		else
  76: 		{
  77: 			Settlement item = Campaign.Current.Models.MapDistanceModel.GetClosestEntranceToFace(fromMobileParty.CurrentNavigationFace, MobileParty.NavigationType.Default).Item1;
  78: 			if (item != null)
  79: 			{
  80: 				value = fromMobileParty.Position.Distance(toSettlement.GatePosition) - item.GatePosition.Distance(toSettlement.GatePosition) + Campaign.Current.Models.MapDistanceModel.GetDistance(item, toSettlement, isFromPort: false, isTargetingPort: false, MobileParty.NavigationType.Default);
  81: 			}
  82: 		}
  83: 		return MBMath.ClampFloat(value, 0f, float.MaxValue);
  84: 	}
  85: 
  86: 	public override float GetDistance(MobileParty fromMobileParty, MobileParty toMobileParty, MobileParty.NavigationType customCapability, out float landRatio)
  87: 	{
  88: 		Campaign.Current.Models.MapDistanceModel.GetDistance(fromMobileParty, toMobileParty, customCapability, 100000000f, out var distance, out landRatio);
  89: 		return distance;
  90: 	}
  91: 
  92: 	public override bool GetDistance(MobileParty fromMobileParty, MobileParty toMobileParty, MobileParty.NavigationType customCapability, float maxDistance, out float distance, out float landRatio)
  93: 	{
  94: 		landRatio = 1f;
  95: 		distance = float.MaxValue;
  96: 		if (fromMobileParty.CurrentNavigationFace.FaceIndex == toMobileParty.CurrentNavigationFace.FaceIndex)
  97: 		{
  98: 			if (Campaign.Current.Models.PartyNavigationModel.IsTerrainTypeValidForNavigationType(Campaign.Current.MapSceneWrapper.GetFaceTerrainType(fromMobileParty.Position.Face), MobileParty.NavigationType.Default))
  99: 			{
 100: 				distance = fromMobileParty.Position.Distance(toMobileParty.Position);
 101: 			}
 102: 		}
 103: 		else if (fromMobileParty.IsCurrentlyAtSea || toMobileParty.IsCurrentlyAtSea)
 104: 		{
 105: 			distance = float.MaxValue;
 106: 		}
 107: 		else
 108: 		{
 109: 			distance = fromMobileParty.Position.Distance(toMobileParty.Position);
 110: 		}
 111: 		distance = MBMath.ClampFloat(distance, 0f, float.MaxValue);
 112: 		return distance <= maxDistance;
 113: 	}
 114: 
 115: 	public override float GetDistance(MobileParty fromMobileParty, in CampaignVec2 toPoint, MobileParty.NavigationType customCapability, out float landRatio)
 116: 	{
 117: 		float value = float.MaxValue;
 118: 		landRatio = 1f;
 119: 		PathFaceRecord face = toPoint.Face;
 120: 		if (fromMobileParty.CurrentNavigationFace.FaceIndex == face.FaceIndex)
 121: 		{
 122: 			if (Campaign.Current.Models.PartyNavigationModel.IsTerrainTypeValidForNavigationType(Campaign.Current.MapSceneWrapper.GetFaceTerrainType(fromMobileParty.Position.Face), MobileParty.NavigationType.Default))
 123: 			{
 124: 				value = fromMobileParty.Position.Distance(toPoint);
 125: 			}
 126: 		}
 127: 		else
 128: 		{
 129: 			MapDistanceModel mapDistanceModel = Campaign.Current.Models.MapDistanceModel;
 130: 			(Settlement, bool) closestEntranceToFace = mapDistanceModel.GetClosestEntranceToFace(fromMobileParty.CurrentNavigationFace, MobileParty.NavigationType.Default);
 131: 			(Settlement, bool) closestEntranceToFace2 = mapDistanceModel.GetClosestEntranceToFace(face, MobileParty.NavigationType.Default);
 132: 			var (settlement, _) = closestEntranceToFace;
 133: 			var (settlement2, _) = closestEntranceToFace2;
 134: 			if (settlement != null && settlement2 != null)
 135: 			{
 136: 				value = fromMobileParty.Position.Distance(toPoint) - settlement.GatePosition.Distance(settlement2.GatePosition) + GetDistance(settlement, settlement2, isFromPort: false, isTargetingPort: false, MobileParty.NavigationType.Default);
 137: 			}
 138: 		}
 139: 		return MBMath.ClampFloat(value, 0f, float.MaxValue);
 140: 	}
 141: 
 142: 	public override float GetDistance(Settlement fromSettlement, in CampaignVec2 toPoint, bool isFromPort, MobileParty.NavigationType customCapability)
 143: 	{
 144: 		float value = float.MaxValue;
 145: 		CampaignVec2 campaignVec = (isFromPort ? fromSettlement.PortPosition : fromSettlement.GatePosition);
 146: 		PathFaceRecord face = toPoint.Face;
 147: 		PathFaceRecord face2 = campaignVec.Face;
 148: 		if (face2.FaceIndex == face.FaceIndex)
 149: 		{
 150: 			if (Campaign.Current.Models.PartyNavigationModel.IsTerrainTypeValidForNavigationType(Campaign.Current.MapSceneWrapper.GetFaceTerrainType(face2), MobileParty.NavigationType.Default))
 151: 			{
 152: 				value = campaignVec.Distance(toPoint);
 153: 			}
 154: 		}
 155: 		else
 156: 		{
 157: 			MapDistanceModel mapDistanceModel = Campaign.Current.Models.MapDistanceModel;
 158: 			Settlement item = mapDistanceModel.GetClosestEntranceToFace(face, MobileParty.NavigationType.Default).Item1;
 159: 			if (item != null)
 160: 			{
 161: 				value = fromSettlement.GatePosition.Distance(toPoint) - fromSettlement.GatePosition.Distance(item.GatePosition) + mapDistanceModel.GetDistance(fromSettlement, item, isFromPort: false, isTargetingPort: false, MobileParty.NavigationType.Default);
 162: 			}
 163: 		}
 164: 		return MBMath.ClampFloat(value, 0f, 100000000f);
 165: 	}
 166: 
 167: 	public override float GetPortToGateDistanceForSettlement(Settlement settlement)
 168: 	{
 169: 		return 100000000f;
 170: 	}
 171: 
 172: 	public override bool PathExistBetweenPoints(in CampaignVec2 fromPoint, in CampaignVec2 toPoint, MobileParty.NavigationType navigationType)
 173: 	{
 174: 		if (fromPoint.IsOnLand)
 175: 		{
 176: 			return toPoint.IsOnLand;
 177: 		}
 178: 		return false;
 179: 	}
 180: 
 181: 	public override (Settlement, bool) GetClosestEntranceToFace(PathFaceRecord face, MobileParty.NavigationType navigationCapabilities)
 182: 	{
 183: 		bool isAtSea;
 184: 		return (_navigationCache.GetClosestSettlementToFaceIndex(face.FaceIndex, out isAtSea), isAtSea);
 185: 	}
 186: 
 187: 	public override MBReadOnlyList<Settlement> GetNeighborsOfFortification(Town town, MobileParty.NavigationType navigationCapabilities)
 188: 	{
 189: 		return _navigationCache.GetNeighbors(town.Settlement);
 190: 	}
 191: 
 192: 	public override float GetTransitionCostAdjustment(Settlement settlement1, bool isFromPort, Settlement settlement2, bool isTargetingPort, bool fromIsCurrentlyAtSea, bool toIsCurrentlyAtSea)
 193: 	{
 194: 		return 0f;
 195: 	}
 196: }

 succeeded in 788ms:
   1: using System.Collections.Generic;
   2: using System.Linq;
   3: using TaleWorlds.CampaignSystem.Actions;
   4: using TaleWorlds.CampaignSystem.Extensions;
   5: using TaleWorlds.CampaignSystem.Roster;
   6: using TaleWorlds.CampaignSystem.Settlements;
   7: using TaleWorlds.Core;
   8: using TaleWorlds.Library;
   9: using TaleWorlds.Localization;
  10: using TaleWorlds.SaveSystem;
  11: 
  12: namespace TaleWorlds.CampaignSystem.Party.PartyComponents;
  13: 
  14: public class CaravanPartyComponent : PartyComponent
  15: {
  16: 	protected class InitializationArgs
  17: 	{
  18: 		public readonly Hero CaravanLeader;
  19: 
  20: 		public readonly ItemRoster CaravanItems;
  21: 
  22: 		public readonly PartyTemplateObject PartyTemplateObject;
  23: 
  24: 		public InitializationArgs(PartyTemplateObject partyTemplateObject, Hero caravanLeader = null, ItemRoster caravanItems = null)
  25: 		{
  26: 			CaravanLeader = caravanLeader;
  27: 			CaravanItems = caravanItems;
  28: 			PartyTemplateObject = partyTemplateObject;
  29: 		}
  30: 
  31: 		public void InitializeCaravanOnCreation(MobileParty mobileParty, Settlement settlement)
  32: 		{
  33: 			int num;
  34: 			CampaignVec2 campaignVec;
  35: 			if (PartyTemplateObject.ShipHulls != null)
  36: 			{
  37: 				num = ((PartyTemplateObject.ShipHulls.Count > 0) ? 1 : 0);
  38: 				if (num != 0)
  39: 				{
  40: 					campaignVec = settlement.PortPosition;
  41: 					goto IL_0034;
  42: 				}
  43: 			}
  44: 			else
  45: 			{
  46: 				num = 0;
  47: 			}
  48: 			campaignVec = settlement.GatePosition;
  49: 			goto IL_0034;
  50: 			IL_0034:
  51: 			CampaignVec2 position = campaignVec;
  52: 			if (num != 0)
  53: 			{
  54: 				mobileParty.SetLandNavigationAccess(access: false);
  55: 			}
  56: 			mobileParty.InitializeMobilePartyAtPosition(PartyTemplateObject, position);
  57: 			if (CaravanLeader != null)
  58: 			{
  59: 				CaravanLeader?.PartyBelongedTo?.MemberRoster?.AddToCounts(CaravanLeader.CharacterObject, -1);
  60: 				mobileParty.MemberRoster.AddToCounts(CaravanLeader.CharacterObject, 1, insertAtFront: true);
  61: 			}
  62: 			else
  63: 			{
  64: 				mobileParty.MemberRoster.AddToCounts(mobileParty.Party.Owner.Culture.CaravanMaster, 1, insertAtFront: true);
  65: 			}
  66: 			if (CaravanItems != null)
  67: 			{
  68: 				mobileParty.ItemRoster.Add(CaravanItems);
  69: 				return;
  70: 			}
  71: 			float num2 = 10000f;
  72: 			ItemObject itemObject = null;
  73: 			foreach (ItemObject item in Items.All)
  74: 			{
  75: 				if (item.ItemCategory == DefaultItemCategories.PackAnimal && !item.NotMerchandise && (float)item.Value < num2)
  76: 				{
  77: 					itemObject = item;
  78: 					num2 = item.Value;
  79: 				}
  80: 			}
  81: 			if (itemObject != null)
  82: 			{
  83: 				mobileParty.ItemRoster.Add(new ItemRosterElement(itemObject, (int)((float)mobileParty.MemberRoster.TotalManCount * 0.5f)));
  84: 			}
  85: 		}
  86: 	}
  87: 
  88: 	private InitializationArgs _initializationArgs;
  89: 
  90: 	[CachedData]
  91: 	private TextObject _cachedName;
  92: 
  93: 	[SaveableField(4)]
  94: 	private bool _isElite;
  95: 
  96: 	[SaveableField(3)]
  97: 	private Hero _leader;
  98: 
  99: 	[SaveableProperty(1)]
 100: 	public Settlement Settlement { get; private set; }
 101: 
 102: 	[SaveableProperty(2)]
 103: 	public Hero Owner { get; private set; }
 104: 
 105: 	public override Hero PartyOwner => Owner;
 106: 
 107: 	public bool IsElite => _isElite;
 108: 
 109: 	public override TextObject Name
 110: 	{
 111: 		get
 112: 		{
 113: 			if (_cachedName == null)
 114: 			{
 115: 				if (base.MobileParty == null)
 116: 				{
 117: 					return GameTexts.FindText("str_caravan_party_name");
 118: 				}
 119: 				CacheName();
 120: 			}
 121: 			return _cachedName;
 122: 		}
 123: 	}
 124: 
 125: 	public override bool CanHaveNavalNavigationCapability => !base.MobileParty.HasLandNavigationCapability;
 126: 
 127: 	public override bool CanHaveLandNavigationCapability => base.MobileParty.HasLandNavigationCapability;
 128: 
 129: 	public override Settlement HomeSettlement => Settlement;
 130: 
 131: 	public override Hero Leader => _leader;
 132: 
 133: 	internal static void AutoGeneratedStaticCollectObjectsCaravanPartyComponent(object o, List<object> collectedObjects)
 134: 	{
 135: 		((CaravanPartyComponent)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
 136: 	}
 137: 
 138: 	protected override void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
 139: 	{
 140: 		base.AutoGeneratedInstanceCollectObjects(collectedObjects);
 141: 		collectedObjects.Add(_leader);
 142: 		collectedObjects.Add(Settlement);
 143: 		collectedObjects.Add(Owner);
 144: 	}
 145: 
 146: 	internal static object AutoGeneratedGetMemberValueSettlement(object o)
 147: 	{
 148: 		return ((CaravanPartyComponent)o).Settlement;
 149: 	}
 150: 
 151: 	internal static object AutoGeneratedGetMemberValueOwner(object o)
 152: 	{
 153: 		return ((CaravanPartyComponent)o).Owner;
 154: 	}
 155: 
 156: 	internal static object AutoGeneratedGetMemberValue_isElite(object o)
 157: 	{
 158: 		return ((CaravanPartyComponent)o)._isElite;
 159: 	}
 160: 
 161: 	internal static object AutoGeneratedGetMemberValue_leader(object o)
 162: 	{
 163: 		return ((CaravanPartyComponent)o)._leader;
 164: 	}
 165: 
 166: 	public override Banner GetDefaultComponentBanner()
 167: 	{
 168: 		if (Leader != null)
 169: 		{
 170: 			return Leader.ClanBanner;
 171: 		}
 172: 		if (Owner == Hero.MainHero)
 173: 		{
 174: 			return Owner.MapFaction.Banner;
 175: 		}
 176: 		return Owner.HomeSettlement.OwnerClan.MapFaction.Banner;
 177: 	}
 178: 
 179: 	protected CaravanPartyComponent(Settlement settlement, Hero owner, Hero partyLeader, bool isElite, InitializationArgs args)
 180: 	{
 181: 		Settlement = settlement;
 182: 		Owner = owner;
 183: 		_leader = partyLeader;
 184: 		_isElite = isElite;
 185: 		_initializationArgs = args;
 186: 	}
 187: 
 188: 	protected override void OnMobilePartySetOnCreation()
 189: 	{
 190: 		base.MobileParty.Aggressiveness = 0f;
 191: 		base.MobileParty.ActualClan = Owner.Clan;
 192: 		base.MobileParty.Party.SetVisualAsDirty();
 193: 		if (_initializationArgs != null)
 194: 		{
 195: 			_initializationArgs.InitializeCaravanOnCreation(base.MobileParty, Settlement);
 196: 			_initializationArgs = null;
 197: 		}
 198: 	}
 199: 
 200: 	protected override void OnInitialize()
 201: 	{
 202: 		Owner.OwnedCaravans.Add(this);
 203: 	}
 204: 
 205: 	protected override void OnFinalize()
 206: 	{
 207: 		Owner.OwnedCaravans.Remove(this);
 208: 	}
 209: 
 210: 	public static void ConvertPartyToCaravanParty(MobileParty mobileParty, Hero caravanOwner, Settlement spawnSettlement, bool isInitialSpawn = false, Hero caravanLeader = null, ItemRoster caravanItems = null, bool isElite = false)
 211: 	{
 212: 		mobileParty.SetPartyComponent(new CaravanPartyComponent(spawnSettlement, caravanOwner, caravanLeader, isElite, null));
 213: 		if (mobileParty.LeaderHero != null)
 214: 		{
 215: 			CampaignEventDispatcher.Instance.OnHeroGetsBusy(mobileParty.LeaderHero, HeroGetsBusyReasons.BecomeCaravanLeader);
 216: 		}
 217: 	}
 218: 
 219: 	protected override void OnChangePartyLeader(Hero newLeader)
 220: 	{
 221: 		_leader = newLeader;
 222: 	}
 223: 
 224: 	public override void ClearCachedName()
 225: 	{
 226: 		_cachedName = null;
 227: 	}
 228: 
 229: 	private void CacheName()
 230: 	{
 231: 		if (base.MobileParty.HasNavalNavigationCapability)
 232: 		{
 233: 			_cachedName = GameTexts.FindText(IsElite ? "str_armed_convoy_party_name" : "str_convoy_party_name");
 234: 		}
 235: 		else
 236: 		{
 237: 			_cachedName = GameTexts.FindText(IsElite ? "str_armed_caravan_party_name" : "str_caravan_party_name");
 238: 		}
 239: 		_cachedName.SetCharacterProperties("OWNER", base.MobileParty.LeaderHero?.CharacterObject ?? Owner.CharacterObject);
 240: 	}
 241: 
 242: 	public static MobileParty CreateCaravanParty(Hero caravanOwner, Settlement spawnSettlement, PartyTemplateObject templateObject, bool isInitialSpawn = false, Hero caravanLeader = null, ItemRoster caravanItems = null, bool isElite = false)
 243: 	{
 244: 		bool flag = templateObject.ShipHulls.Any();
 245: 		InitializationArgs args = new InitializationArgs(templateObject, caravanLeader, caravanItems);
 246: 		MobileParty mobileParty = MobileParty.CreateParty("caravan_template_" + spawnSettlement.Culture.StringId.ToLower() + "_1", new CaravanPartyComponent(spawnSettlement, caravanOwner, caravanLeader, isElite, args));
 247: 		mobileParty.InitializePartyTrade(Campaign.Current.Models.CaravanModel.GetInitialTradeGold(caravanOwner, flag, isElite));
 248: 		if (spawnSettlement.Party.MapEvent == null && spawnSettlement.SiegeEvent == null)
 249: 		{
 250: 			mobileParty.SetMoveGoToSettlement(spawnSettlement, (!flag) ? MobileParty.NavigationType.Default : MobileParty.NavigationType.Naval, flag);
 251: 			mobileParty.RecalculateShortTermBehavior();
 252: 			EnterSettlementAction.ApplyForParty(mobileParty, spawnSettlement);
 253: 		}
 254: 		else
 255: 		{
 256: 			mobileParty.SetMoveModeHold();
 257: 		}
 258: 		if (mobileParty.LeaderHero != null)
 259: 		{
 260: 			CampaignEventDispatcher.Instance.OnHeroGetsBusy(mobileParty.LeaderHero, HeroGetsBusyReasons.BecomeCaravanLeader);

codex
The decompiled `CaravanPartyComponent` already shows `Owner` and `Leader` are separate: `CreateCaravanParty(caravanOwner, ..., caravanLeader)` calls `GetInitialTradeGold(caravanOwner, ...)`. I’m checking the vanilla creation actions now to see what `caravanOwner` is for player-created caravans.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign -Recurse -File -Filter *.cs | Select-String -Pattern 'CreateCaravanParty\\(|ConvertPartyToCaravanParty\\(|caravanOwner|caravanLeader' | Select-Object -First 200 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path, $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -File -Filter AiHelper.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party.PartyComponents\\CaravanPartyComponent.cs' | Select-Object -Skip 238 -First 70 | ForEach-Object { '{0,4}: {1}' -f ("'$i+238), $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 643ms:
 239: 		_cachedName.SetCharacterProperties("OWNER", base.MobileParty.LeaderHero?.CharacterObject ?? Owner.CharacterObject);
 240: 	}
 241: 
 242: 	public static MobileParty CreateCaravanParty(Hero caravanOwner, Settlement spawnSettlement, PartyTemplateObject templateObject, bool isInitialSpawn = false, Hero caravanLeader = null, ItemRoster caravanItems = null, bool isElite = false)
 243: 	{
 244: 		bool flag = templateObject.ShipHulls.Any();
 245: 		InitializationArgs args = new InitializationArgs(templateObject, caravanLeader, caravanItems);
 246: 		MobileParty mobileParty = MobileParty.CreateParty("caravan_template_" + spawnSettlement.Culture.StringId.ToLower() + "_1", new CaravanPartyComponent(spawnSettlement, caravanOwner, caravanLeader, isElite, args));
 247: 		mobileParty.InitializePartyTrade(Campaign.Current.Models.CaravanModel.GetInitialTradeGold(caravanOwner, flag, isElite));
 248: 		if (spawnSettlement.Party.MapEvent == null && spawnSettlement.SiegeEvent == null)
 249: 		{
 250: 			mobileParty.SetMoveGoToSettlement(spawnSettlement, (!flag) ? MobileParty.NavigationType.Default : MobileParty.NavigationType.Naval, flag);
 251: 			mobileParty.RecalculateShortTermBehavior();
 252: 			EnterSettlementAction.ApplyForParty(mobileParty, spawnSettlement);
 253: 		}
 254: 		else
 255: 		{
 256: 			mobileParty.SetMoveModeHold();
 257: 		}
 258: 		if (mobileParty.LeaderHero != null)
 259: 		{
 260: 			CampaignEventDispatcher.Instance.OnHeroGetsBusy(mobileParty.LeaderHero, HeroGetsBusyReasons.BecomeCaravanLeader);
 261: 		}
 262: 		return mobileParty;
 263: 	}
 264: 
 265: 	public override void GetMountAndHarnessVisualIdsForPartyIcon(PartyBase party, out string mountStringId, out string harnessStringId)
 266: 	{
 267: 		string text = party.MapFaction?.Culture?.StringId ?? string.Empty;
 268: 		if (text == "aserai" || text == "khuzait")
 269: 		{
 270: 			mountStringId = "camel";
 271: 			if (party.Index % 2 == 0)
 272: 			{
 273: 				harnessStringId = "camel_saddle_a";
 274: 			}
 275: 			else
 276: 			{
 277: 				harnessStringId = "camel_saddle_b";
 278: 			}
 279: 			return;
 280: 		}
 281: 		mountStringId = "mule";
 282: 		switch (party.Index % 3)
 283: 		{
 284: 		case 0:
 285: 			harnessStringId = "mule_load_a";
 286: 			break;
 287: 		case 1:
 288: 			harnessStringId = "mule_load_b";
 289: 			break;
 290: 		default:
 291: 			harnessStringId = "mule_load_c";
 292: 			break;
 293: 		}
 294: 	}
 295: 
 296: 	public static void TransferCaravanOwnership(MobileParty caravan, Hero newOwner, Settlement homeSettlement)
 297: 	{
 298: 		int partyTradeGold = caravan.PartyTradeGold;
 299: 		ConvertPartyToCaravanParty(caravan, newOwner, homeSettlement, isInitialSpawn: false, caravan.LeaderHero, null, caravan.CaravanPartyComponent.IsElite);
 300: 		caravan.PartyTradeGold = partyTradeGold;
 301: 	}
 302: 
 303: 	public void ChangeHomeSettlement(Settlement newHomeSettlement)
 304: 	{
 305: 		Settlement = newHomeSettlement;
 306: 	}
 307: }

 succeeded in 694ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs
E:\Decompiled_Bannerlord\_categories_v1.4.5\Campaign\TaleWorlds.CampaignSystem\Helpers\AiHelper.cs

 succeeded in 940ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\HeroGetsBusyReasons.cs:7: BecomeCaravanLeader,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ApplyHeirSelectionAction.cs:26: TransferCaravanOwnerships(heir);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ApplyHeirSelectionAction.cs:82: private static void TransferCaravanOwnerships(Hero newLeader)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ApplyHeirSelectionAction.cs:88: CaravanPartyComponent.TransferCaravanOwnership(hero.PartyBelongedTo, newLeader, hero.PartyBelongedTo.HomeSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravanConversationsCampaignBehavior.cs:250: CaravanPartyComponent.CreateCaravanParty(Hero.MainHero, Settlement.CurrentSettlement, randomCaravanTemplate, isInitialSpawn: false, characterObject.HeroObject, null, flag);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravanConversationsCampaignBehavior.cs:257: private void FadeOutSelectedCaravanCompanionInMission(CharacterObject caravanLeader)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravanConversationsCampaignBehavior.cs:259: CampaignMission.Current?.FadeOutCharacter(caravanLeader);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:501: MobileParty caravanParty = CaravanPartyComponent.CreateCaravanParty(spawnSettlement: (settlement == null) ? Town.AllTowns.GetRandomElementWithPredicate((Town x) => x.Settlement.HasPort == isNaval).Settlement : (settlement.IsTown ? settlement : ((!settlement.IsVillage) ? Town.AllTowns.GetRandomElementWithPredicate((Town x) => x.Settlement.HasPort == isNaval).Settlement : (settlement.Village.TradeBound ?? Town.AllTowns.GetRandomElementWithPredicate((Town x) => x.Settlement.HasPort == isNaval).Settlement))), caravanOwner: hero, templateObject: randomElementWithPredicate, isInitialSpawn: initialSpawn, caravanLeader: null, caravanItems: null, isElite: flag);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2844: NarrativeMenuOption narrativeMenuOption3 = new NarrativeMenuOption("adulthood_caravan_leader_option", new TextObject("{=wfbtS71d}you led a caravan."), new TextObject("{=joRHKCkm}Your family needed someone trustworthy to take a caravan to a neighboring town. You organized supplies, ensured a constant watch to keep away bandits, and brought it safely to its destination."), GetAdulthoodCaravanLeaderOptionArgs, AdulthoodCaravanLeaderOptionOnCondition, AdulthoodCaravanLeaderOptionOnSelect, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2940: private void GetAdulthoodCaravanLeaderOptionArgs(NarrativeMenuOptionArgs args)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2957: private bool AdulthoodCaravanLeaderOptionOnCondition(CharacterCreationManager characterCreationManager)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterCreationCampaignBehavior.cs:2970: private void AdulthoodCaravanLeaderOptionOnSelect(CharacterCreationManager characterCreationManager)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:337: CaravanPartyComponent.TransferCaravanOwnership(item.MobileParty, hero, hero.CurrentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:541: _caravanParty = CaravanPartyComponent.CreateCaravanParty(base.QuestGiver, base.QuestGiver.CurrentSettlement, randomCaravanTemplate, isInitialSpawn: false, null, itemRoster);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1291: CaravanPartyComponent.ConvertPartyToCaravanParty(_questCaravanMobileParty, base.QuestGiver, base.QuestGiver.CurrentSettlement);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:18: public readonly Hero CaravanLeader;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:24: public InitializationArgs(PartyTemplateObject partyTemplateObject, Hero caravanLeader = null, ItemRoster caravanItems = null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:26: CaravanLeader = caravanLeader;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:57: if (CaravanLeader != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:59: CaravanLeader?.PartyBelongedTo?.MemberRoster?.AddToCounts(CaravanLeader.CharacterObject, -1);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:60: mobileParty.MemberRoster.AddToCounts(CaravanLeader.CharacterObject, 1, insertAtFront: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:210: public static void ConvertPartyToCaravanParty(MobileParty mobileParty, Hero caravanOwner, Settlement spawnSettlement, bool isInitialSpawn = false, Hero caravanLeader = null, ItemRoster caravanItems = null, bool isElite = false)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:212: mobileParty.SetPartyComponent(new CaravanPartyComponent(spawnSettlement, caravanOwner, caravanLeader, isElite, null));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:215: CampaignEventDispatcher.Instance.OnHeroGetsBusy(mobileParty.LeaderHero, HeroGetsBusyReasons.BecomeCaravanLeader);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:242: public static MobileParty CreateCaravanParty(Hero caravanOwner, Settlement spawnSettlement, PartyTemplateObject templateObject, bool isInitialSpawn = false, Hero caravanLeader = null, ItemRoster caravanItems = null, bool isElite = false)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:245: InitializationArgs args = new InitializationArgs(templateObject, caravanLeader, caravanItems);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:246: MobileParty mobileParty = MobileParty.CreateParty("caravan_template_" + spawnSettlement.Culture.StringId.ToLower() + "_1", new CaravanPartyComponent(spawnSettlement, caravanOwner, caravanLeader, isElite, args));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:247: mobileParty.InitializePartyTrade(Campaign.Current.Models.CaravanModel.GetInitialTradeGold(caravanOwner, flag, isElite));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:260: CampaignEventDispatcher.Instance.OnHeroGetsBusy(mobileParty.LeaderHero, HeroGetsBusyReasons.BecomeCaravanLeader);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:296: public static void TransferCaravanOwnership(MobileParty caravan, Hero newOwner, Settlement homeSettlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:299: ConvertPartyToCaravanParty(caravan, newOwner, homeSettlement, isInitialSpawn: false, caravan.LeaderHero, null, caravan.CaravanPartyComponent.IsElite);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravanConversationsCampaignBehavior.cs' | Select-Object -Skip 220 -First 45 | ForEach-Object { '{0,4}: {1}' -f ("'$i+220), $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Actions\\ApplyHeirSelectionAction.cs' | Select-Object -Skip 70 -First 35 | ForEach-Object { '{0,4}: {1}' -f ("'$i+70), $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\Helpers\\AiHelper.cs' | ForEach-Object { if ("'$i -ge 1 -and $i -le 220) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-String -Pattern 'CanTradeWith|_prohibitedKingdomsForPlayerCaravans|CalculateBudgetFactor|GetTradeScoreForTown|CacheVeryFarDistances' -Context 2,8 | ForEach-Object { "'$_.Context.PreContext; $_.Line; $_.Context.PostContext; '"'---' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip 450 -First 90 | ForEach-Object { '{0,4}: {1}' -f ("'$i+450), $_; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 688ms:
 221: 	{
 222: 		if (ShouldCreateConvoy())
 223: 		{
 224: 			MBTextManager.SetTextVariable("CARAVAN_NOTABLE_FINAL_TALK", new TextObject("{=2WFPZrFf}Ok then. I will call my men to help you form a trade convoy. I hope it brings you a good profit."));
 225: 		}
 226: 		else
 227: 		{
 228: 			MBTextManager.SetTextVariable("CARAVAN_NOTABLE_FINAL_TALK", new TextObject("{=Z2Lq2QLq}Ok then. I will call my men to help you form a caravan. I hope it brings you a good profit."));
 229: 		}
 230: 		return true;
 231: 	}
 232: 
 233: 	private bool conversation_magistrate_form_a_caravan_accepted_leader_is_chosen_on_condition()
 234: 	{
 235: 		if (ConversationSentence.CurrentProcessedRepeatObject is CharacterObject character)
 236: 		{
 237: 			StringHelpers.SetRepeatableCharacterProperties("HERO", character);
 238: 			return true;
 239: 		}
 240: 		return false;
 241: 	}
 242: 
 243: 	private void conversation_magistrate_form_a_caravan_accept_on_consequence()
 244: 	{
 245: 		CharacterObject characterObject = ConversationSentence.SelectedRepeatObject as CharacterObject;
 246: 		FadeOutSelectedCaravanCompanionInMission(characterObject);
 247: 		LeaveSettlementAction.ApplyForCharacterOnly(characterObject.HeroObject);
 248: 		bool flag = _selectedCaravanType == 1;
 249: 		PartyTemplateObject randomCaravanTemplate = CaravanHelper.GetRandomCaravanTemplate(Settlement.CurrentSettlement.Culture, flag, !ShouldCreateConvoy());
 250: 		CaravanPartyComponent.CreateCaravanParty(Hero.MainHero, Settlement.CurrentSettlement, randomCaravanTemplate, isInitialSpawn: false, characterObject.HeroObject, null, flag);
 251: 		GiveGoldAction.ApplyForCharacterToSettlement(Hero.MainHero, Settlement.CurrentSettlement, (!flag) ? GetSmallCaravanGoldCost() : GetLargeCaravanGoldCost());
 252: 		TextObject textObject = ((!ShouldCreateConvoy()) ? new TextObject("{=RmtTsqcx}A new caravan is created for {HERO.NAME}.") : new TextObject("{=c7VOPmSb}A new trade convoy is created for {HERO.NAME}."));
 253: 		StringHelpers.SetCharacterProperties("HERO", Hero.MainHero.CharacterObject, textObject);
 254: 		InformationManager.DisplayMessage(new InformationMessage(textObject.ToString()));
 255: 	}
 256: 
 257: 	private void FadeOutSelectedCaravanCompanionInMission(CharacterObject caravanLeader)
 258: 	{
 259: 		CampaignMission.Current?.FadeOutCharacter(caravanLeader);
 260: 	}
 261: 
 262: 	private List<CharacterObject> FindSuitableCompanionsToLeadCaravan()
 263: 	{
 264: 		List<CharacterObject> list = new List<CharacterObject>();
 265: 		foreach (TroopRosterElement item in MobileParty.MainParty.MemberRoster.GetTroopRoster())

 succeeded in 674ms:
  71: 
  72: 	public static void ApplyByDeath(Hero heir)
  73: 	{
  74: 		ApplyInternal(heir);
  75: 	}
  76: 
  77: 	public static void ApplyByRetirement(Hero heir)
  78: 	{
  79: 		ApplyInternal(heir, isRetirement: true);
  80: 	}
  81: 
  82: 	private static void TransferCaravanOwnerships(Hero newLeader)
  83: 	{
  84: 		foreach (Hero hero in Clan.PlayerClan.Heroes)
  85: 		{
  86: 			if (hero.PartyBelongedTo != null && hero.PartyBelongedTo.IsCaravan)
  87: 			{
  88: 				CaravanPartyComponent.TransferCaravanOwnership(hero.PartyBelongedTo, newLeader, hero.PartyBelongedTo.HomeSettlement);
  89: 			}
  90: 		}
  91: 	}
  92: }

 succeeded in 690ms:
   1: using TaleWorlds.CampaignSystem;
   2: using TaleWorlds.CampaignSystem.Naval;
   3: using TaleWorlds.CampaignSystem.Party;
   4: using TaleWorlds.CampaignSystem.Settlements;
   5: using TaleWorlds.Library;
   6: 
   7: namespace Helpers;
   8: 
   9: public static class AiHelper
  10: {
  11: 	public static void GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(MobileParty mobileParty, Settlement settlement, bool isTargetingPort, out MobileParty.NavigationType bestNavigationType, out float bestNavigationDistance, out bool isFromPort)
  12: 	{
  13: 		bestNavigationType = MobileParty.NavigationType.None;
  14: 		bestNavigationDistance = float.MaxValue;
  15: 		isFromPort = false;
  16: 		float landRatio = -1f;
  17: 		if (mobileParty.CurrentSettlement != null && mobileParty.CurrentSettlement == settlement)
  18: 		{
  19: 			bestNavigationDistance = 0f;
  20: 			bestNavigationType = ((!isTargetingPort) ? MobileParty.NavigationType.Default : MobileParty.NavigationType.Naval);
  21: 			return;
  22: 		}
  23: 		float num = float.MaxValue;
  24: 		if (mobileParty.HasLandNavigationCapability && !isTargetingPort)
  25: 		{
  26: 			num = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(mobileParty, settlement, MobileParty.NavigationType.Default, out landRatio);
  27: 		}
  28: 		if (num < Campaign.MapDiagonal * 5f && !isTargetingPort)
  29: 		{
  30: 			bestNavigationType = MobileParty.NavigationType.Default;
  31: 			bestNavigationDistance = num;
  32: 		}
  33: 		if (!mobileParty.HasNavalNavigationCapability)
  34: 		{
  35: 			return;
  36: 		}
  37: 		float num2 = float.MaxValue;
  38: 		if (isTargetingPort)
  39: 		{
  40: 			num2 = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(mobileParty, settlement, MobileParty.NavigationType.Naval, out landRatio);
  41: 		}
  42: 		if (num2 < Campaign.MapDiagonal * 5f)
  43: 		{
  44: 			num2 *= CalculateShipDistanceAmplifier(mobileParty, num2);
  45: 			if (num2 < num && isTargetingPort)
  46: 			{
  47: 				bestNavigationType = MobileParty.NavigationType.Naval;
  48: 				bestNavigationDistance = num2;
  49: 				isFromPort = mobileParty.CurrentSettlement != null;
  50: 			}
  51: 		}
  52: 		if (!mobileParty.HasLandNavigationCapability)
  53: 		{
  54: 			return;
  55: 		}
  56: 		float num3 = float.MaxValue;
  57: 		bool flag = false;
  58: 		if (mobileParty.CurrentSettlement != null)
  59: 		{
  60: 			float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(mobileParty.CurrentSettlement, settlement, isFromPort: false, isTargetingPort, MobileParty.NavigationType.All, out landRatio);
  61: 			if (distance < Campaign.MapDiagonal * 5f)
  62: 			{
  63: 				float num4 = distance * landRatio;
  64: 				float num5 = distance - num4;
  65: 				num5 *= CalculateShipDistanceAmplifier(mobileParty, num5);
  66: 				num3 = num5 + num4;
  67: 			}
  68: 			if (mobileParty.CurrentSettlement.HasPort)
  69: 			{
  70: 				float distance2 = Campaign.Current.Models.MapDistanceModel.GetDistance(mobileParty.CurrentSettlement, settlement, isFromPort: true, isTargetingPort, MobileParty.NavigationType.All, out landRatio);
  71: 				if (distance2 < Campaign.MapDiagonal * 5f)
  72: 				{
  73: 					float num6 = distance2 * landRatio;
  74: 					float num7 = distance2 - num6;
  75: 					num7 *= CalculateShipDistanceAmplifier(mobileParty, num7);
  76: 					float num8 = num7 + num6;
  77: 					if (num8 < num3)
  78: 					{
  79: 						num3 = num8;
  80: 						flag = true;
  81: 					}
  82: 				}
  83: 			}
  84: 			if (num3 < num2 && num3 < num)
  85: 			{
  86: 				bestNavigationType = MobileParty.NavigationType.All;
  87: 				bestNavigationDistance = num3;
  88: 				isFromPort = flag;
  89: 			}
  90: 			return;
  91: 		}
  92: 		float distance3 = Campaign.Current.Models.MapDistanceModel.GetDistance(mobileParty, settlement, isTargetingPort, MobileParty.NavigationType.All, out var estimatedLandRatio);
  93: 		if (distance3 < Campaign.MapDiagonal * 5f)
  94: 		{
  95: 			float num9 = distance3 * estimatedLandRatio;
  96: 			float num10 = distance3 - num9;
  97: 			num10 *= CalculateShipDistanceAmplifier(mobileParty, num10);
  98: 			distance3 = num10 + num9;
  99: 			if (distance3 < num2 && distance3 < num)
 100: 			{
 101: 				bestNavigationType = MobileParty.NavigationType.All;
 102: 				bestNavigationDistance = distance3;
 103: 				isFromPort = false;
 104: 			}
 105: 		}
 106: 	}
 107: 
 108: 	public static void GetBestNavigationTypeAndDistanceOfMobilePartyForMobileParty(MobileParty mobileParty, MobileParty toMobileParty, out MobileParty.NavigationType bestNavigationType, out float bestNavigationDistance)
 109: 	{
 110: 		bestNavigationType = MobileParty.NavigationType.None;
 111: 		bestNavigationDistance = float.MaxValue;
 112: 		float landRatio = -1f;
 113: 		float num = float.MaxValue;
 114: 		if (mobileParty.HasLandNavigationCapability)
 115: 		{
 116: 			num = DistanceHelper.FindClosestDistanceFromMobilePartyToMobileParty(mobileParty, toMobileParty, MobileParty.NavigationType.Default, out landRatio);
 117: 		}
 118: 		if (num < Campaign.MapDiagonal * 5f)
 119: 		{
 120: 			bestNavigationType = MobileParty.NavigationType.Default;
 121: 			bestNavigationDistance = num;
 122: 		}
 123: 		if (!mobileParty.HasNavalNavigationCapability)
 124: 		{
 125: 			return;
 126: 		}
 127: 		float num2 = DistanceHelper.FindClosestDistanceFromMobilePartyToMobileParty(mobileParty, toMobileParty, MobileParty.NavigationType.Naval, out landRatio);
 128: 		if (num2 < Campaign.MapDiagonal * 5f)
 129: 		{
 130: 			num2 *= CalculateShipDistanceAmplifier(mobileParty, num2);
 131: 			if (num2 < num)
 132: 			{
 133: 				bestNavigationType = MobileParty.NavigationType.Naval;
 134: 				bestNavigationDistance = num2;
 135: 			}
 136: 		}
 137: 		if (!mobileParty.HasLandNavigationCapability)
 138: 		{
 139: 			return;
 140: 		}
 141: 		float num3 = DistanceHelper.FindClosestDistanceFromMobilePartyToMobileParty(mobileParty, toMobileParty, MobileParty.NavigationType.All, out landRatio);
 142: 		if (num3 < Campaign.MapDiagonal * 5f)
 143: 		{
 144: 			num3 *= CalculateShipDistanceAmplifier(mobileParty, num3);
 145: 			if (num3 < num2 && num3 < num)
 146: 			{
 147: 				bestNavigationType = MobileParty.NavigationType.All;
 148: 				bestNavigationDistance = num3;
 149: 			}
 150: 		}
 151: 	}
 152: 
 153: 	private static float CalculateShipDistanceAmplifier(MobileParty mobileParty, float navalDistance)
 154: 	{
 155: 		if (mobileParty.HasLandNavigationCapability)
 156: 		{
 157: 			float num = (mobileParty.IsLordParty ? Campaign.Current.EstimatedAverageLordPartyNavalSpeed : (mobileParty.IsCaravan ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed : (mobileParty.IsBandit ? Campaign.Current.EstimatedAverageBanditPartyNavalSpeed : (mobileParty.IsVillager ? Campaign.Current.EstimatedAverageVillagerPartyNavalSpeed : (Campaign.Current.EstimatedMaximumLordPartySpeedExceptPlayer * 0.5f)))));
 158: 			float num2 = navalDistance / num;
 159: 			float estimatedSafeSailDuration = Campaign.Current.Models.CampaignShipDamageModel.GetEstimatedSafeSailDuration(mobileParty);
 160: 			float num3 = Campaign.MapDiagonal * 0.5f;
 161: 			if (estimatedSafeSailDuration > num2)
 162: 			{
 163: 				float num4 = estimatedSafeSailDuration / num2;
 164: 				if (num4 > 4f)
 165: 				{
 166: 					num3 = 0.35f;
 167: 				}
 168: 				else if (num4 > 3f)
 169: 				{
 170: 					num3 = MBMath.Map(num4, 3f, 4f, 0.35f, 0.6f);
 171: 				}
 172: 				else if (num4 > 2f)
 173: 				{
 174: 					num3 = MBMath.Map(num4, 2f, 3f, 0.6f, 1f);
 175: 				}
 176: 				else if (num4 > 1f)
 177: 				{
 178: 					num3 = MBMath.Map(num4, 1f, 2f, 1f, 1.25f);
 179: 				}
 180: 			}
 181: 			int num5 = 0;
 182: 			foreach (Ship ship in mobileParty.Ships)
 183: 			{
 184: 				if (ship.HitPoints / ship.MaxHitPoints > 0.2f)
 185: 				{
 186: 					num5 += ship.TotalCrewCapacity;
 187: 				}
 188: 			}
 189: 			int num6 = mobileParty.MemberRoster.TotalManCount;
 190: 			foreach (MobileParty attachedParty in mobileParty.AttachedParties)
 191: 			{
 192: 				num6 += attachedParty.MemberRoster.TotalManCount;
 193: 			}
 194: 			float num7 = (float)num5 / (float)num6;
 195: 			if (num7 < 1f)
 196: 			{
 197: 				num3 = ((num7 > 0.8f) ? (num3 * MBMath.Map(num7, 0.8f, 1f, 1.5f, 1f)) : ((!(num7 > 0.6f)) ? (num3 * 3.5f) : (num3 * MBMath.Map(num7, 0.6f, 0.8f, 2.2f, 1.5f))));
 198: 			}
 199: 			return num3;
 200: 		}
 201: 		return 1f;
 202: 	}
 203: }

 succeeded in 744ms:
 451: 							{
 452: 								num2 = itemValue;
 453: 								itemRosterElement = item;
 454: 							}
 455: 						}
 456: 					}
 457: 					int num3 = TaleWorlds.Library.MathF.Min(itemRosterElement.Amount, TaleWorlds.Library.MathF.Max(1, num - totalManCount));
 458: 					mobileParty.ItemRoster.AddToCounts(itemRosterElement.EquipmentElement, -num3);
 459: 					num -= num3;
 460: 				}
 461: 			}
 462: 			int inventoryCapacity = mobileParty.InventoryCapacity;
 463: 			float totalWeightCarried = mobileParty.TotalWeightCarried;
 464: 			float num4 = 0f;
 465: 			if (!(totalWeightCarried - num4 > (float)inventoryCapacity))
 466: 			{
 467: 				continue;
 468: 			}
 469: 			int num6;
 470: 			float weight;
 471: 			for (; totalWeightCarried - num4 > (float)inventoryCapacity; num4 += weight * (float)num6)
 472: 			{
 473: 				int num5 = 10000;
 474: 				ItemRosterElement itemRosterElement2 = involvedParty.MobileParty.ItemRoster[0];
 475: 				foreach (ItemRosterElement item2 in involvedParty.MobileParty.ItemRoster)
 476: 				{
 477: 					if (!item2.EquipmentElement.Item.IsMountable)
 478: 					{
 479: 						int itemValue2 = item2.EquipmentElement.ItemValue;
 480: 						if (itemValue2 < num5)
 481: 						{
 482: 							num5 = itemValue2;
 483: 							itemRosterElement2 = item2;
 484: 						}
 485: 					}
 486: 				}
 487: 				int val = TaleWorlds.Library.MathF.Ceiling((totalWeightCarried - num4 - (float)inventoryCapacity) / itemRosterElement2.EquipmentElement.Weight);
 488: 				num6 = Math.Max(1, Math.Min(itemRosterElement2.Amount, val));
 489: 				weight = itemRosterElement2.EquipmentElement.Weight;
 490: 				mobileParty.ItemRoster.AddToCounts(itemRosterElement2.EquipmentElement, -num6);
 491: 			}
 492: 		}
 493: 	}
 494: 
 495: 	public void SpawnCaravan(Hero hero, bool initialSpawn = false)
 496: 	{
 497: 		bool flag = Campaign.Current.Models.CaravanModel.GetEliteCaravanSpawnChance(hero) > hero.RandomFloat();
 498: 		PartyTemplateObject randomElementWithPredicate = ((MBReadOnlyList<PartyTemplateObject>)(flag ? hero.Culture.EliteCaravanPartyTemplates : hero.Culture.CaravanPartyTemplates)).GetRandomElementWithPredicate((Func<PartyTemplateObject, bool>)((PartyTemplateObject x) => x.ShipHulls.Count == 0 != hero.CurrentSettlement.HasPort));
 499: 		bool isNaval = randomElementWithPredicate.ShipHulls.Any();
 500: 		Settlement settlement = hero.HomeSettlement ?? hero.BornSettlement;
 501: 		MobileParty caravanParty = CaravanPartyComponent.CreateCaravanParty(spawnSettlement: (settlement == null) ? Town.AllTowns.GetRandomElementWithPredicate((Town x) => x.Settlement.HasPort == isNaval).Settlement : (settlement.IsTown ? settlement : ((!settlement.IsVillage) ? Town.AllTowns.GetRandomElementWithPredicate((Town x) => x.Settlement.HasPort == isNaval).Settlement : (settlement.Village.TradeBound ?? Town.AllTowns.GetRandomElementWithPredicate((Town x) => x.Settlement.HasPort == isNaval).Settlement))), caravanOwner: hero, templateObject: randomElementWithPredicate, isInitialSpawn: initialSpawn, caravanLeader: null, caravanItems: null, isElite: flag);
 502: 		if (!initialSpawn)
 503: 		{
 504: 			hero.AddPower(Campaign.Current.Models.CaravanModel.GetPowerChangeAfterCaravanCreation(hero, caravanParty));
 505: 		}
 506: 	}
 507: 
 508: 	private void UpdateAverageValues()
 509: 	{
 510: 		Dictionary<ItemCategory, (float, int)> dictionary = new Dictionary<ItemCategory, (float, int)>();
 511: 		foreach (ItemObject item in Items.All)
 512: 		{
 513: 			if (item.IsReady)
 514: 			{
 515: 				dictionary.TryGetValue(item.ItemCategory, out var value);
 516: 				dictionary[item.ItemCategory] = (value.Item1 + (float)TaleWorlds.Library.MathF.Min(500, item.Value), value.Item2 + 1);
 517: 			}
 518: 		}
 519: 		_packAnimalCategoryIndex = -1;
 520: 		for (int i = 0; i < ItemCategories.All.Count; i++)
 521: 		{
 522: 			ItemCategory itemCategory = ItemCategories.All[i];
 523: 			(float, int) value2;
 524: 			bool flag = dictionary.TryGetValue(itemCategory, out value2);
 525: 			_averageValuesCached[itemCategory] = (flag ? (value2.Item1 / (float)value2.Item2) : 1f);
 526: 			if (itemCategory == DefaultItemCategories.PackAnimal)
 527: 			{
 528: 				_packAnimalCategoryIndex = i;
 529: 			}
 530: 		}
 531: 	}
 532: 
 533: 	private void CreatePriceDataCache()
 534: 	{
 535: 		foreach (ItemCategory item in ItemCategories.All)
 536: 		{
 537: 			float num = 0f;
 538: 			float num2 = 1000f;
 539: 			float num3 = 0f;
 540: 			float num4 = 1000f;

 succeeded in 782ms:
	private TradeActionLogPool _tradeActionLogPool;

	private List<Kingdom> _prohibitedKingdomsForPlayerCaravans = new List<Kingdom>();

	private int _packAnimalCategoryIndex = -1;

	private readonly Dictionary<ItemCategory, float> _averageValuesCached = new Dictionary<ItemCategory, float>();

	private readonly Dictionary<ItemCategory, PriceIndexData> _priceDictionary = new Dictionary<ItemCategory, PriceIndexData>();

	private readonly Dictionary<ItemCategory, PriceIndexData> _coastalPriceDictionary = new Dictionary<ItemCategory, PriceIndexData>();
---
	private void OnKingdomDestroyed(Kingdom destroyedKingdom)
	{
		if (_prohibitedKingdomsForPlayerCaravans.Contains(destroyedKingdom))
		{
			_prohibitedKingdomsForPlayerCaravans.Remove(destroyedKingdom);
		}
	}

	private void OnGameLoadFinished()
	{
		CreatePriceDataCache();
---
		if (_prohibitedKingdomsForPlayerCaravans.Contains(destroyedKingdom))
		{
			_prohibitedKingdomsForPlayerCaravans.Remove(destroyedKingdom);
		}
	}

	private void OnGameLoadFinished()
	{
		CreatePriceDataCache();
		foreach (MobileParty allCaravanParty in MobileParty.AllCaravanParties)
		{
---
		dataStore.SyncData("_tradeActionLogs", ref _tradeActionLogs);
		dataStore.SyncData("_caravanLastHomeTownVisitTime", ref _caravanLastHomeTownVisitTime);
		dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
	}

	private void DoInitialTradeRuns()
	{
		foreach (MobileParty allCaravanParty in MobileParty.AllCaravanParties)
		{
			Town town = null;
			Town town2 = allCaravanParty.CurrentSettlement?.Town;
---
	public void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
	{
		CacheVeryFarDistances();
		AddDialogs(campaignGameStarter);
		UpdateAverageValues();
	}

	private void CacheVeryFarDistances()
	{
		MobileParty.NavigationType navigationType = MobileParty.NavigationType.Naval;
		float num = 20f;
---
	}

	private void CacheVeryFarDistances()
	{
		MobileParty.NavigationType navigationType = MobileParty.NavigationType.Naval;
		float num = 20f;
		float num2 = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigationType) * num;
		float num3 = Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed * (float)CampaignTime.HoursInDay;
		_navalCaravanVeryFarCache = num2 / num3;
		navigationType = MobileParty.NavigationType.Default;
		num = 5f;
---
		{
			Town destinationForMobileParty = GetDestinationForMobileParty(mobileParty);
			flag = destinationForMobileParty == null || (destinationForMobileParty.IsUnderSiege && (!mobileParty.HasNavalNavigationCapability || destinationForMobileParty.Settlement.SiegeEvent.IsBlockadeActive)) || !CanTradeWith(mobileParty.MapFaction, destinationForMobileParty.MapFaction);
		}
		if (flag)
		{
			if (mobileParty.CurrentSettlement != null && mobileParty.CurrentSettlement.IsTown)
			{
				Town town = mobileParty.CurrentSettlement.Town;
				BuyGoods(mobileParty, town);
			}
---
		foreach (Town allTown in Town.AllTowns)
		{
			if (allTown.Owner.Settlement != caravanParty.CurrentSettlement && (!allTown.IsUnderSiege || (!allTown.Settlement.SiegeEvent.IsBlockadeActive && hasNavalNavigationCapability)) && CanTradeWith(caravanParty.MapFaction, allTown.MapFaction) && (allTown.Settlement.HasPort || !hasNavalNavigationCapability) && (!allTown.Settlement.Parties.Contains(MobileParty.MainParty) || !MobileParty.MainParty.MapFaction.IsAtWarWith(caravanParty.MapFaction)))
			{
				MobileParty.NavigationType bestNavigationType2;
				bool isTargetingPort2;
				float tradeScoreForTown = GetTradeScoreForTown(caravanParty, allTown, value, input, distanceCut, out bestNavigationType2, out isTargetingPort2);
				if (tradeScoreForTown > num)
				{
					num = tradeScoreForTown;
					result = allTown;
---
				MobileParty.NavigationType bestNavigationType2;
				bool isTargetingPort2;
				float tradeScoreForTown = GetTradeScoreForTown(caravanParty, allTown, value, input, distanceCut, out bestNavigationType2, out isTargetingPort2);
				if (tradeScoreForTown > num)
				{
					num = tradeScoreForTown;
					result = allTown;
					isTargetingPort = isTargetingPort2;
					bestNavigationType = bestNavigationType2;
				}
			}
---
	}

	private float GetTradeScoreForTown(MobileParty caravanParty, Town town, CampaignTime lastHomeVisitTimeOfCaravan, float caravanFullness, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isTargetingPort)
	{
		bool flag = (isTargetingPort = caravanParty.HasNavalNavigationCapability);
		AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(caravanParty, town.Settlement, isTargetingPort, out bestNavigationType, out var bestNavigationDistance, out var _);
		if (bestNavigationType != MobileParty.NavigationType.None)
		{
			float num = bestNavigationDistance / ((flag ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed : Campaign.Current.EstimatedAverageCaravanPartySpeed) * (float)CampaignTime.HoursInDay);
			float veryFarAddition = 0f;
			AdjustVeryFarAddition(flag, num, 0.15f, ref veryFarAddition);
---
		List<(EquipmentElement, int)> list = new List<(EquipmentElement, int)>();
		float capacityFactor = CalculateCapacityFactor(caravanParty);
		float budgetFactor = CalculateBudgetFactor(caravanParty);
		RefreshTotalValueOfItemsAtCategoryForParty(caravanParty);
		MBList<ItemCategory> mBList = ItemCategories.All.OrderByDescending((ItemCategory x) => CalculateBuyValue(x, town, caravanParty, budgetFactor, capacityFactor)).ToMBList();
		int num = (caravanParty.HasNavalNavigationCapability ? 10 : 5);
		for (int num2 = 0; num2 < num; num2++)
		{
			BuyCategory(caravanParty, town, mBList[num2], budgetFactor, capacityFactor, list);
		}
		if (caravanParty.HasNavalNavigationCapability)
---
	}

	private float CalculateBudgetFactor(MobileParty caravanParty)
	{
		return 0.1f + TaleWorlds.Library.MathF.Clamp((float)caravanParty.PartyTradeGold / 5000f, 0f, 1f);
	}

	private float CalculateCapacityFactor(MobileParty caravanParty)
	{
		float num = caravanParty.TotalWeightCarried / ((float)caravanParty.InventoryCapacity + 1f);
		num *= 0.9f;
---
		if (ConversationSentence.CurrentProcessedRepeatObject is Kingdom kingdom)
		{
			bool num = _prohibitedKingdomsForPlayerCaravans.Contains(kingdom);
			TextObject empty = TextObject.GetEmpty();
			empty = ((!num) ? new TextObject("{=KsFOH8vo}Let's stop trading with {KINGDOM_NAME}.") : new TextObject("{=1QBbbq4h}Let's continue trading with {KINGDOM_NAME}."));
			empty.SetTextVariable("KINGDOM_NAME", kingdom.Name);
			ConversationSentence.SelectedRepeatLine.SetTextVariable("CONTINUE_OR_STOP_TRADE", empty);
			return true;
		}
		return false;
	}
---
	{
		Kingdom kingdom = ConversationSentence.SelectedRepeatObject as Kingdom;
		bool num = _prohibitedKingdomsForPlayerCaravans.Contains(kingdom);
		TextObject textObject = TextObject.GetEmpty();
		if (num)
		{
			_prohibitedKingdomsForPlayerCaravans.Remove(kingdom);
		}
		else
		{
			_prohibitedKingdomsForPlayerCaravans.Add(kingdom);
---
		if (num)
		{
			_prohibitedKingdomsForPlayerCaravans.Remove(kingdom);
		}
		else
		{
			_prohibitedKingdomsForPlayerCaravans.Add(kingdom);
			if (kingdom.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
			{
				textObject = new TextObject("{=y9sgoggj}We are currently at war with {KINGDOM_NAME}, and we shall not start trading with them even if we make peace.");
				textObject.SetTextVariable("KINGDOM_NAME", kingdom.Name);
---
		else
		{
			_prohibitedKingdomsForPlayerCaravans.Add(kingdom);
			if (kingdom.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
			{
				textObject = new TextObject("{=y9sgoggj}We are currently at war with {KINGDOM_NAME}, and we shall not start trading with them even if we make peace.");
				textObject.SetTextVariable("KINGDOM_NAME", kingdom.Name);
			}
		}
		MBTextManager.SetTextVariable("DESPITE_WAR", textObject);
	}
---
	}

	private bool CanTradeWith(IFaction caravanFaction, IFaction targetFaction)
	{
		if (caravanFaction.IsAtWarWith(targetFaction))
		{
			return false;
		}
		if (caravanFaction == Hero.MainHero.MapFaction)
		{
			if (targetFaction is Kingdom item)
---
			if (targetFaction is Kingdom item)
			{
				return !_prohibitedKingdomsForPlayerCaravans.Contains(item);
			}
			return true;
		}
		return true;
	}
}
---

codex
The player caravan creation path disputes Suspect 2’s ownership premise: vanilla passes `Hero.MainHero` as `caravanOwner` and the companion only as `caravanLeader`. I’ll still include the exact decompile lines in the report, because the distinction is the whole point.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -First 70 | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=135; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 45 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=175; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 160 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=555; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 45 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=960; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 35 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 758ms:
   1: using System;
   2: using System.Collections.Generic;
   3: using System.Linq;
   4: using Helpers;
   5: using TaleWorlds.CampaignSystem.Actions;
   6: using TaleWorlds.CampaignSystem.CharacterDevelopment;
   7: using TaleWorlds.CampaignSystem.Conversation;
   8: using TaleWorlds.CampaignSystem.Encounters;
   9: using TaleWorlds.CampaignSystem.Extensions;
  10: using TaleWorlds.CampaignSystem.MapEvents;
  11: using TaleWorlds.CampaignSystem.Naval;
  12: using TaleWorlds.CampaignSystem.Party;
  13: using TaleWorlds.CampaignSystem.Party.PartyComponents;
  14: using TaleWorlds.CampaignSystem.Roster;
  15: using TaleWorlds.CampaignSystem.Settlements;
  16: using TaleWorlds.CampaignSystem.Settlements.Buildings;
  17: using TaleWorlds.CampaignSystem.Siege;
  18: using TaleWorlds.Core;
  19: using TaleWorlds.Library;
  20: using TaleWorlds.Localization;
  21: using TaleWorlds.SaveSystem;
  22: 
  23: namespace TaleWorlds.CampaignSystem.CampaignBehaviors;
  24: 
  25: public class CaravansCampaignBehavior : CampaignBehaviorBase
  26: {
  27: 	public class CaravansCampaignBehaviorTypeDefiner : SaveableTypeDefiner
  28: 	{
  29: 		public CaravansCampaignBehaviorTypeDefiner()
  30: 			: base(60000)
  31: 		{
  32: 		}
  33: 
  34: 		protected override void DefineEnumTypes()
  35: 		{
  36: 			AddEnumDefinition(typeof(PlayerInteraction), 1);
  37: 		}
  38: 
  39: 		protected override void DefineContainerDefinitions()
  40: 		{
  41: 			ConstructContainerDefinition(typeof(Dictionary<MobileParty, PlayerInteraction>));
  42: 			ConstructContainerDefinition(typeof(List<TradeActionLog>));
  43: 			ConstructContainerDefinition(typeof(Dictionary<MobileParty, List<TradeActionLog>>));
  44: 		}
  45: 
  46: 		protected override void DefineClassTypes()
  47: 		{
  48: 			AddClassDefinition(typeof(TradeActionLog), 2);
  49: 		}
  50: 	}
  51: 
  52: 	private enum PlayerInteraction
  53: 	{
  54: 		None,
  55: 		Friendly,
  56: 		TradedWith,
  57: 		Hostile
  58: 	}
  59: 
  60: 	private struct PriceIndexData(float averageBuySellPriceIndex, float minBuySellPriceIndex)
  61: 	{
  62: 		internal readonly float AverageBuySellPriceIndex = averageBuySellPriceIndex;
  63: 
  64: 		internal readonly float MinBuySellPriceIndex = minBuySellPriceIndex;
  65: 	}
  66: 
  67: 	internal class TradeActionLog
  68: 	{
  69: 		[SaveableField(0)]
  70: 		public Settlement BoughtSettlement;

 succeeded in 742ms:
 960: 		{
 961: 			veryFarAddition += (distanceAsDays - distanceLimitCloseAsDaysForNavigationType) * minimumAddition;
 962: 		}
 963: 	}
 964: 
 965: 	private float GetTradeScoreForTown(MobileParty caravanParty, Town town, CampaignTime lastHomeVisitTimeOfCaravan, float caravanFullness, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isTargetingPort)
 966: 	{
 967: 		bool flag = (isTargetingPort = caravanParty.HasNavalNavigationCapability);
 968: 		AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(caravanParty, town.Settlement, isTargetingPort, out bestNavigationType, out var bestNavigationDistance, out var _);
 969: 		if (bestNavigationType != MobileParty.NavigationType.None)
 970: 		{
 971: 			float num = bestNavigationDistance / ((flag ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed : Campaign.Current.EstimatedAverageCaravanPartySpeed) * (float)CampaignTime.HoursInDay);
 972: 			float veryFarAddition = 0f;
 973: 			AdjustVeryFarAddition(flag, num, 0.15f, ref veryFarAddition);
 974: 			float elapsedDaysUntilNow = lastHomeVisitTimeOfCaravan.ElapsedDaysUntilNow;
 975: 			bool flag2 = elapsedDaysUntilNow > GetDistanceLimitVeryFarAsDaysForNavigationType(flag);
 976: 			if (flag2)
 977: 			{
 978: 				float distanceAsDays = bestNavigationDistance / ((flag ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed : Campaign.Current.EstimatedAverageCaravanPartySpeed) * (float)CampaignTime.HoursInDay);
 979: 				AdjustVeryFarAddition(flag, distanceAsDays, ((elapsedDaysUntilNow - 1f) * TaleWorlds.Library.MathF.Sqrt(elapsedDaysUntilNow - 1f) - 1f) * 0.008f, ref veryFarAddition);
 980: 			}
 981: 			ExplainedNumber result = default(ExplainedNumber);
 982: 			town.AddEffectOfBuildings(BuildingEffectEnum.CaravanAccessibility, ref result);
 983: 			float num2 = Math.Max(1f, result.ResultNumber);
 984: 			float distanceLimitVeryFarAsDaysForNavigationType = GetDistanceLimitVeryFarAsDaysForNavigationType(flag);
 985: 			float num3 = num + veryFarAddition;
 986: 			if (distanceCut && (town.Owner.Settlement != caravanParty.HomeSettlement || !flag2) && num3 > distanceLimitVeryFarAsDaysForNavigationType)
 987: 			{
 988: 				bestNavigationType = MobileParty.NavigationType.None;
 989: 				isTargetingPort = false;
 990: 				return -1f;
 991: 			}
 992: 			float num4 = (flag ? TaleWorlds.Library.MathF.Max(0.1f, 1f - num3 / (2f * distanceLimitVeryFarAsDaysForNavigationType)) : (1f / num3));
 993: 			float num5 = 1f;
 994: 			if (caravanParty.HomeSettlement == town.Owner.Settlement)

 succeeded in 761ms:
 135: 
 136: 		internal static object AutoGeneratedGetMemberValueSoldSettlement(object o)
 137: 		{
 138: 			return ((TradeActionLog)o).SoldSettlement;
 139: 		}
 140: 
 141: 		internal static object AutoGeneratedGetMemberValueBoughtTime(object o)
 142: 		{
 143: 			return ((TradeActionLog)o).BoughtTime;
 144: 		}
 145: 	}
 146: 
 147: 	internal class TradeActionLogPool
 148: 	{
 149: 		private Stack<TradeActionLog> _stack;
 150: 
 151: 		public int Size => _stack?.Count ?? 0;
 152: 
 153: 		private int MaxSize { get; }
 154: 
 155: 		public TradeActionLogPool(int size)
 156: 		{
 157: 			MaxSize = size;
 158: 			_stack = new Stack<TradeActionLog>(size);
 159: 			for (int i = 0; i < size; i++)
 160: 			{
 161: 				_stack.Push(new TradeActionLog());
 162: 			}
 163: 		}
 164: 
 165: 		public TradeActionLog CreateNewLog(Settlement boughtSettlement, int buyPrice, ItemRosterElement itemRosterElement)
 166: 		{
 167: 			TradeActionLog obj = ((_stack.Count > 0) ? _stack.Pop() : new TradeActionLog());
 168: 			obj.BoughtSettlement = boughtSettlement;
 169: 			obj.BuyPrice = buyPrice;
 170: 			obj.ItemRosterElement = itemRosterElement;
 171: 			obj.BoughtTime = CampaignTime.Now;
 172: 			return obj;
 173: 		}
 174: 
 175: 		public void ReleaseLog(TradeActionLog log)
 176: 		{
 177: 			log.Reset();
 178: 			if (_stack.Count < MaxSize)
 179: 			{

 succeeded in 803ms:
 175: 		public void ReleaseLog(TradeActionLog log)
 176: 		{
 177: 			log.Reset();
 178: 			if (_stack.Count < MaxSize)
 179: 			{
 180: 				_stack.Push(log);
 181: 			}
 182: 		}
 183: 
 184: 		public override string ToString()
 185: 		{
 186: 			return $"TrackPool: {Size}";
 187: 		}
 188: 	}
 189: 
 190: 	private const float InventoryFullnessGoal = 0.9f;
 191: 
 192: 	private const float AverageCaravanWaitAtSettlement = 3f;
 193: 
 194: 	private const float ProfitRateRumorThreshold = 1.2f;
 195: 
 196: 	private const float ReferenceBudgetValue = 5000f;
 197: 
 198: 	private const float HighSecurityThreshold = 75f;
 199: 
 200: 	private const float MustDiscardPriorityValue = float.MinValue;
 201: 
 202: 	private const float CaravanTradeAgreementBonus = 2f;
 203: 
 204: 	private const float ConvoyTradeAgreementBonus = 1.5f;
 205: 
 206: 	private float _navalCaravanVeryFarCache = -1f;
 207: 
 208: 	private float _defaultCaravanVeryFarCache = -1f;
 209: 
 210: 	private ITradeAgreementsCampaignBehavior _tradeAgreementsBehavior;
 211: 
 212: 	private Dictionary<MobileParty, CampaignTime> _tradeRumorTakenCaravans = new Dictionary<MobileParty, CampaignTime>();
 213: 
 214: 	private Dictionary<MobileParty, CampaignTime> _caravanLastHomeTownVisitTime = new Dictionary<MobileParty, CampaignTime>();
 215: 
 216: 	private Dictionary<MobileParty, CampaignTime> _lootedCaravans = new Dictionary<MobileParty, CampaignTime>();
 217: 
 218: 	private Dictionary<MobileParty, PlayerInteraction> _interactedCaravans = new Dictionary<MobileParty, PlayerInteraction>();
 219: 
 220: 	private Dictionary<MobileParty, List<TradeActionLog>> _tradeActionLogs = new Dictionary<MobileParty, List<TradeActionLog>>();
 221: 
 222: 	private TradeActionLogPool _tradeActionLogPool;
 223: 
 224: 	private List<Kingdom> _prohibitedKingdomsForPlayerCaravans = new List<Kingdom>();
 225: 
 226: 	private int _packAnimalCategoryIndex = -1;
 227: 
 228: 	private readonly Dictionary<ItemCategory, float> _averageValuesCached = new Dictionary<ItemCategory, float>();
 229: 
 230: 	private readonly Dictionary<ItemCategory, PriceIndexData> _priceDictionary = new Dictionary<ItemCategory, PriceIndexData>();
 231: 
 232: 	private readonly Dictionary<ItemCategory, PriceIndexData> _coastalPriceDictionary = new Dictionary<ItemCategory, PriceIndexData>();
 233: 
 234: 	private readonly Dictionary<ItemCategory, int> _totalValueOfItemsAtCategory = new Dictionary<ItemCategory, int>();
 235: 
 236: 	private int MaxNumberOfItemsToBuyFromSingleCategory => Campaign.Current.Models.CaravanModel.MaxNumberOfItemsToBuyFromSingleCategory;
 237: 
 238: 	public ITradeAgreementsCampaignBehavior TradeAgreementsCampaignBehavior
 239: 	{
 240: 		get
 241: 		{
 242: 			if (_tradeAgreementsBehavior == null)
 243: 			{
 244: 				_tradeAgreementsBehavior = Campaign.Current.GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
 245: 			}
 246: 			return _tradeAgreementsBehavior;
 247: 		}
 248: 	}
 249: 
 250: 	private float GetDistanceLimitVeryFarAsDaysForNavigationType(bool isNavalCaravan)
 251: 	{
 252: 		if (!isNavalCaravan)
 253: 		{
 254: 			return _defaultCaravanVeryFarCache;
 255: 		}
 256: 		return _navalCaravanVeryFarCache;
 257: 	}
 258: 
 259: 	private float GetDistanceLimitFarAsDaysForNavigationType(bool isNavalCaravan)
 260: 	{
 261: 		return GetDistanceLimitVeryFarAsDaysForNavigationType(isNavalCaravan) * 0.75f;
 262: 	}
 263: 
 264: 	private float GetDistanceLimitMediumAsDaysForNavigationType(bool isNavalCaravan)
 265: 	{
 266: 		return GetDistanceLimitVeryFarAsDaysForNavigationType(isNavalCaravan) * 0.5f;
 267: 	}
 268: 
 269: 	private float GetDistanceLimitCloseAsDaysForNavigationType(bool isNavalCaravan)
 270: 	{
 271: 		return GetDistanceLimitVeryFarAsDaysForNavigationType(isNavalCaravan) * 0.25f;
 272: 	}
 273: 
 274: 	public CaravansCampaignBehavior()
 275: 	{
 276: 		_tradeActionLogPool = new TradeActionLogPool(4096);
 277: 	}
 278: 
 279: 	public override void RegisterEvents()
 280: 	{
 281: 		CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
 282: 		CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
 283: 		CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, DailyTick);
 284: 		CampaignEvents.DailyTickHeroEvent.AddNonSerializedListener(this, DailyTickHero);
 285: 		CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, HourlyTickParty);
 286: 		CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
 287: 		CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUpEndEvent);
 288: 		CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
 289: 		CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, OnMobilePartyCreated);
 290: 		CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
 291: 		CampaignEvents.OnLootDistributedToPartyEvent.AddNonSerializedListener(this, OnLootDistributedToParty);
 292: 		CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, OnSiegeEventStarted);
 293: 		CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
 294: 		CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
 295: 	}
 296: 
 297: 	private void OnKingdomDestroyed(Kingdom destroyedKingdom)
 298: 	{
 299: 		if (_prohibitedKingdomsForPlayerCaravans.Contains(destroyedKingdom))
 300: 		{
 301: 			_prohibitedKingdomsForPlayerCaravans.Remove(destroyedKingdom);
 302: 		}
 303: 	}
 304: 
 305: 	private void OnGameLoadFinished()
 306: 	{
 307: 		CreatePriceDataCache();
 308: 		foreach (MobileParty allCaravanParty in MobileParty.AllCaravanParties)
 309: 		{
 310: 			if ((!allCaravanParty.IsActive || !allCaravanParty.IsReady) && _caravanLastHomeTownVisitTime.ContainsKey(allCaravanParty))
 311: 			{
 312: 				_caravanLastHomeTownVisitTime.Remove(allCaravanParty);
 313: 			}
 314: 		}
 315: 	}
 316: 
 317: 	private void OnSiegeEventStarted(SiegeEvent siegeEvent)
 318: 	{
 319: 		for (int i = 0; i < siegeEvent.BesiegedSettlement.Parties.Count; i++)
 320: 		{
 321: 			if (siegeEvent.BesiegedSettlement.Parties[i].IsCaravan)
 322: 			{
 323: 				siegeEvent.BesiegedSettlement.Parties[i].SetMoveModeHold();
 324: 			}
 325: 		}
 326: 	}
 327: 
 328: 	private void OnLootDistributedToParty(PartyBase winnerParty, PartyBase defeatedParty, ItemRoster lootedItems)
 329: 	{
 330: 		if (winnerParty.IsMobile && defeatedParty.IsMobile && defeatedParty.MobileParty.IsCaravan)
 331: 		{
 332: 			SkillLevelingManager.OnLoot(winnerParty.MobileParty, defeatedParty.MobileParty, lootedItems, attacked: true);
 333: 		}
 334: 	}

 succeeded in 805ms:
 555: 					}
 556: 				}
 557: 			}
 558: 			float averageBuySellPriceIndex = num / (float)Town.AllTowns.Count;
 559: 			float averageBuySellPriceIndex2 = num3 / (float)Town.AllTowns.Count((Town x) => x.Settlement.HasPort);
 560: 			_priceDictionary[item] = new PriceIndexData(averageBuySellPriceIndex, num2);
 561: 			_coastalPriceDictionary[item] = new PriceIndexData(averageBuySellPriceIndex2, num4);
 562: 		}
 563: 	}
 564: 
 565: 	public void DailyTick()
 566: 	{
 567: 		DeleteExpiredTradeRumorTakenCaravans();
 568: 		DeleteExpiredLootedCaravans();
 569: 		CreatePriceDataCache();
 570: 	}
 571: 
 572: 	private void DailyTickHero(Hero hero)
 573: 	{
 574: 		if (hero != Hero.MainHero && MBRandom.RandomFloat < 0.75f && Campaign.Current.Models.CaravanModel.CanHeroCreateCaravan(hero))
 575: 		{
 576: 			SpawnCaravan(hero);
 577: 		}
 578: 	}
 579: 
 580: 	private void DeleteExpiredTradeRumorTakenCaravans()
 581: 	{
 582: 		List<MobileParty> list = new List<MobileParty>();
 583: 		foreach (KeyValuePair<MobileParty, CampaignTime> tradeRumorTakenCaravan in _tradeRumorTakenCaravans)
 584: 		{
 585: 			if (CampaignTime.Now - tradeRumorTakenCaravan.Value >= CampaignTime.Days(1f))
 586: 			{
 587: 				list.Add(tradeRumorTakenCaravan.Key);
 588: 			}
 589: 		}
 590: 		foreach (MobileParty item in list)
 591: 		{
 592: 			_tradeRumorTakenCaravans.Remove(item);
 593: 		}
 594: 	}
 595: 
 596: 	private void DeleteExpiredLootedCaravans()
 597: 	{
 598: 		List<MobileParty> list = new List<MobileParty>();
 599: 		foreach (KeyValuePair<MobileParty, CampaignTime> lootedCaravan in _lootedCaravans)

codex
I found one test smell: several tests use `"rohan"` as a sample id, but the substitutes define the side for that same string, so it does not affect production behavior. I’m treating it as test-data sloppiness unless it weakens a real assertion.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=992; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 115 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=870; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 60 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=1120; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 60 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultCaravanModel.cs'; if (Test-Path "'$path) { $i=1; Get-Content $path | Select-Object -First 140 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign -Recurse -File -Filter DefaultCaravanModel.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 761ms:
 992: 			float num4 = (flag ? TaleWorlds.Library.MathF.Max(0.1f, 1f - num3 / (2f * distanceLimitVeryFarAsDaysForNavigationType)) : (1f / num3));
 993: 			float num5 = 1f;
 994: 			if (caravanParty.HomeSettlement == town.Owner.Settlement)
 995: 			{
 996: 				num5 = 1f + elapsedDaysUntilNow * 0.1f * (elapsedDaysUntilNow * 0.1f);
 997: 				if (num4 < 0.5f)
 998: 				{
 999: 					num4 = 0.5f;
1000: 				}
1001: 			}
1002: 			TownMarketData marketData = town.MarketData;
1003: 			float num6 = 1.1f;
1004: 			float num7 = 0f;
1005: 			for (int i = 0; i < caravanParty.Party.ItemRoster.Count; i++)
1006: 			{
1007: 				ItemObject item = caravanParty.ItemRoster.GetElementCopyAtIndex(i).EquipmentElement.Item;
1008: 				float limitValue = num6 - TaleWorlds.Library.MathF.Sqrt((float)TaleWorlds.Library.MathF.Min(_totalValueOfItemsAtCategory[item.ItemCategory], 5000) / 5000f) * 0.2f;
1009: 				num7 += CalculateTownSellScoreForCategory(caravanParty, marketData, i, limitValue);
1010: 			}
1011: 			num7 *= (flag ? 0.5f : 0.3f) + caravanFullness;
1012: 			float num8 = 0f;
1013: 			for (int j = 0; j < ItemCategories.All.Count; j++)
1014: 			{
1015: 				ItemCategory itemCategory = ItemCategories.All[j];
1016: 				if (itemCategory.IsTradeGood || itemCategory.IsAnimal)
1017: 				{
1018: 					num8 += CalculateTownBuyScoreForCategory(marketData, j, caravanParty);
1019: 				}
1020: 			}
1021: 			num8 *= TaleWorlds.Library.MathF.Max(0.1f, 1f - 2f * (caravanFullness - (flag ? 0.5f : 0.3f) * TaleWorlds.Library.MathF.Min(num7, 1000f) / 1000f));
1022: 			num8 = TaleWorlds.Library.MathF.Min(num8, (float)(int)(0.5f * (float)caravanParty.PartyTradeGold));
1023: 			float num9 = ((caravanParty.IsCurrentlyUsedByAQuest && town.Settlement == caravanParty.HomeSettlement && caravanParty.Position.Distance(caravanParty.HomeSettlement.Position) < Campaign.Current.Models.EncounterModel.NeededMaximumDistanceForEncounteringTown * 5f) ? 0.1f : 1f);
1024: 			float num10 = 1f;
1025: 			float num11 = ((town.Security >= 75f) ? (1f + TaleWorlds.Library.MathF.Clamp((town.Security - 75f) * 0.002f, 0f, 0.05f)) : 1f);
1026: 			float num12 = ((caravanParty.Owner != null) ? caravanParty.Owner.RandomFloat(1f, 1.03f) : 1f);
1027: 			float num13 = 1f;
1028: 			if (TradeAgreementsCampaignBehavior != null && caravanParty.MapFaction.IsKingdomFaction && town.MapFaction.IsKingdomFaction && TradeAgreementsCampaignBehavior.HasTradeAgreement((Kingdom)caravanParty.MapFaction, (Kingdom)town.MapFaction, out var _))
1029: 			{
1030: 				num13 = (flag ? 1.5f : 2f);
1031: 			}
1032: 			return (num7 + num8) * num4 * num13 * num5 * num9 * num10 * num11 * num12 * num2;
1033: 		}
1034: 		bestNavigationType = MobileParty.NavigationType.None;
1035: 		isTargetingPort = false;
1036: 		return -1f;
1037: 	}
1038: 
1039: 	private float CalculateTownSellScoreForCategory(MobileParty party, TownMarketData marketData, int i, float limitValue)
1040: 	{
1041: 		ItemRosterElement itemRosterElement = party.Party.ItemRoster[i];
1042: 		ItemCategory itemCategory = itemRosterElement.EquipmentElement.Item.ItemCategory;
1043: 		GetCategoryPriceData(itemCategory, party, out var priceIndex);
1044: 		float num = marketData.GetPriceFactor(itemCategory) - priceIndex.AverageBuySellPriceIndex * limitValue;
1045: 		if (num > 0f)
1046: 		{
1047: 			int num2 = ((itemRosterElement.EquipmentElement.Item.ItemCategory != DefaultItemCategories.PackAnimal || !party.HasLandNavigationCapability) ? itemRosterElement.Amount : TaleWorlds.Library.MathF.Max(0, itemRosterElement.Amount - party.MemberRoster.TotalManCount));
1048: 			float num3 = ((itemCategory.Properties == ItemCategory.Property.BonusToFoodStores) ? 1.1f : 1f);
1049: 			return num * num3 * (float)TaleWorlds.Library.MathF.Min(4000, itemRosterElement.EquipmentElement.Item.Value * num2);
1050: 		}
1051: 		return 0f;
1052: 	}
1053: 
1054: 	private void SetPlayerInteraction(MobileParty mobileParty, PlayerInteraction interaction)
1055: 	{
1056: 		if (_interactedCaravans.ContainsKey(mobileParty))
1057: 		{
1058: 			_interactedCaravans[mobileParty] = interaction;
1059: 		}
1060: 		else
1061: 		{
1062: 			_interactedCaravans.Add(mobileParty, interaction);
1063: 		}
1064: 	}
1065: 
1066: 	private PlayerInteraction GetPlayerInteraction(MobileParty mobileParty)
1067: 	{
1068: 		if (_interactedCaravans.TryGetValue(mobileParty, out var value))
1069: 		{
1070: 			return value;
1071: 		}
1072: 		return PlayerInteraction.None;
1073: 	}
1074: 
1075: 	private float CalculateTownBuyScoreForCategory(TownMarketData marketData, int categoryIndex, MobileParty mobileParty)
1076: 	{
1077: 		ItemCategory itemCategory = ItemCategories.All[categoryIndex];
1078: 		GetCategoryPriceData(itemCategory, mobileParty, out var priceIndex);
1079: 		float priceFactor = marketData.GetPriceFactor(itemCategory);
1080: 		float num = priceIndex.AverageBuySellPriceIndex / priceFactor;
1081: 		float num2 = num * num - 1.1f;
1082: 		if (num2 > 0f)
1083: 		{
1084: 			return TaleWorlds.Library.MathF.Min(TaleWorlds.Library.MathF.Sqrt(_averageValuesCached[itemCategory]) * 3f * num2, 0.3f * (float)marketData.GetCategoryData(itemCategory).InStoreValue);
1085: 		}
1086: 		return 0f;
1087: 	}
1088: 
1089: 	private bool GetCategoryPriceData(ItemCategory category, MobileParty mobileParty, out PriceIndexData priceIndex)
1090: 	{
1091: 		bool result = true;
1092: 		if (!(ShouldPartyUseCoastalPrices(mobileParty) ? _coastalPriceDictionary : _priceDictionary).TryGetValue(category, out priceIndex))
1093: 		{
1094: 			result = false;
1095: 			priceIndex = new PriceIndexData(1f, 1f);
1096: 		}
1097: 		return result;
1098: 	}
1099: 
1100: 	private void RefreshTotalValueOfItemsAtCategoryForParty(MobileParty caravanParty)
1101: 	{
1102: 		_totalValueOfItemsAtCategory.Clear();
1103: 		for (int i = 0; i < caravanParty.ItemRoster.Count; i++)
1104: 		{
1105: 			ItemRosterElement elementCopyAtIndex = caravanParty.ItemRoster.GetElementCopyAtIndex(i);
1106: 			ItemObject item = elementCopyAtIndex.EquipmentElement.Item;

 succeeded in 766ms:
 870: 		{
 871: 			return;
 872: 		}
 873: 		if (_interactedCaravans.ContainsKey(mobileParty))
 874: 		{
 875: 			_interactedCaravans.Remove(mobileParty);
 876: 		}
 877: 		if (_tradeActionLogs.TryGetValue(mobileParty, out var value))
 878: 		{
 879: 			_tradeActionLogs.Remove(mobileParty);
 880: 			for (int i = 0; i < value.Count; i++)
 881: 			{
 882: 				TradeActionLog log = value[i];
 883: 				_tradeActionLogPool.ReleaseLog(log);
 884: 			}
 885: 		}
 886: 		if (_caravanLastHomeTownVisitTime.ContainsKey(mobileParty))
 887: 		{
 888: 			_caravanLastHomeTownVisitTime.Remove(mobileParty);
 889: 		}
 890: 	}
 891: 
 892: 	private void OnMobilePartyCreated(MobileParty mobileParty)
 893: 	{
 894: 		if (mobileParty.IsCaravan)
 895: 		{
 896: 			_caravanLastHomeTownVisitTime.Add(mobileParty, CampaignTime.Now);
 897: 		}
 898: 	}
 899: 
 900: 	private Town ThinkNextDestination(MobileParty caravanParty, out MobileParty.NavigationType bestNavigationType, out bool isFromPort, out bool isTargetingPort)
 901: 	{
 902: 		RefreshTotalValueOfItemsAtCategoryForParty(caravanParty);
 903: 		Town town = FindNextDestinationForCaravan(caravanParty, distanceCut: true, out bestNavigationType, out isFromPort, out isTargetingPort);
 904: 		if (town == null)
 905: 		{
 906: 			town = FindNextDestinationForCaravan(caravanParty, distanceCut: false, out bestNavigationType, out isFromPort, out isTargetingPort);
 907: 		}
 908: 		return town;
 909: 	}
 910: 
 911: 	private Town FindNextDestinationForCaravan(MobileParty caravanParty, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isFromPort, out bool isTargetingPort)
 912: 	{
 913: 		float num = 0f;
 914: 		Town result = null;
 915: 		bestNavigationType = MobileParty.NavigationType.None;
 916: 		isTargetingPort = false;
 917: 		float input = caravanParty.TotalWeightCarried / (float)caravanParty.InventoryCapacity;
 918: 		input = MBMath.Map(input, 0f, 1f, 0f, 0.9f);
 919: 		_caravanLastHomeTownVisitTime.TryGetValue(caravanParty, out var value);
 920: 		bool hasNavalNavigationCapability = caravanParty.HasNavalNavigationCapability;
 921: 		foreach (Town allTown in Town.AllTowns)
 922: 		{
 923: 			if (allTown.Owner.Settlement != caravanParty.CurrentSettlement && (!allTown.IsUnderSiege || (!allTown.Settlement.SiegeEvent.IsBlockadeActive && hasNavalNavigationCapability)) && CanTradeWith(caravanParty.MapFaction, allTown.MapFaction) && (allTown.Settlement.HasPort || !hasNavalNavigationCapability) && (!allTown.Settlement.Parties.Contains(MobileParty.MainParty) || !MobileParty.MainParty.MapFaction.IsAtWarWith(caravanParty.MapFaction)))
 924: 			{
 925: 				MobileParty.NavigationType bestNavigationType2;
 926: 				bool isTargetingPort2;
 927: 				float tradeScoreForTown = GetTradeScoreForTown(caravanParty, allTown, value, input, distanceCut, out bestNavigationType2, out isTargetingPort2);
 928: 				if (tradeScoreForTown > num)
 929: 				{

 succeeded in 692ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultCaravanModel.cs

 succeeded in 689ms:
1120: 	{
1121: 		if (mobileParty.IsCaravan)
1122: 		{
1123: 			return mobileParty.CaravanPartyComponent.CanHaveNavalNavigationCapability;
1124: 		}
1125: 		return false;
1126: 	}
1127: 
1128: 	private void SellGoodsInternal(MobileParty mobileParty, Town town, bool sellHorses, List<(EquipmentElement, int)> soldItems, float priceIndexSellLimit = 1.1f, bool toLoseWeight = false)
1129: 	{
1130: 		int itemAverageWeight = Campaign.Current.Models.InventoryCapacityModel.GetItemAverageWeight();
1131: 		RefreshTotalValueOfItemsAtCategoryForParty(mobileParty);
1132: 		for (int num = mobileParty.ItemRoster.Count - 1; num >= 0; num--)
1133: 		{
1134: 			int num2 = (int)((float)mobileParty.ItemRoster.NumberOfPackAnimals - (float)mobileParty.Party.NumberOfAllMembers * 0.6f);
1135: 			int num3 = (int)((float)mobileParty.ItemRoster.NumberOfLivestockAnimals - (float)mobileParty.Party.NumberOfAllMembers * 0.6f);
1136: 			ItemRosterElement elementCopyAtIndex = mobileParty.ItemRoster.GetElementCopyAtIndex(num);
1137: 			ItemObject item = elementCopyAtIndex.EquipmentElement.Item;
1138: 			if (GetCategoryPriceData(item.GetItemCategory(), mobileParty, out var priceIndex) && sellHorses == (item.HasHorseComponent || item.ItemCategory == DefaultItemCategories.PackAnimal) && (!toLoseWeight || !item.HasHorseComponent || !mobileParty.HasLandNavigationCapability))
1139: 			{
1140: 				bool flag = item.ItemCategory == DefaultItemCategories.PackAnimal;
1141: 				if (!flag || num2 > 0 || !mobileParty.HasLandNavigationCapability)
1142: 				{
1143: 					float priceFactor = town.MarketData.GetPriceFactor(item.ItemCategory);
1144: 					float num4 = priceFactor / priceIndex.AverageBuySellPriceIndex;
1145: 					float num5 = priceIndexSellLimit - (Campaign.Current.GameStarted ? (TaleWorlds.Library.MathF.Sqrt((float)TaleWorlds.Library.MathF.Min(_totalValueOfItemsAtCategory[item.ItemCategory], 5000) / 5000f) * 0.4f) : 0f);
1146: 					bool flag2 = num2 > 0 && flag;
1147: 					bool flag3 = num3 > 0 && item.HorseComponent != null && item.HorseComponent.IsLiveStock;
1148: 					if (!(num4 < num5) || (mobileParty.HasLandNavigationCapability && (flag3 || flag2)))
1149: 					{
1150: 						float num6 = 0.8f * priceIndex.AverageBuySellPriceIndex + 0.2f * priceIndex.MinBuySellPriceIndex;
1151: 						if (!(priceFactor < num6 * num5) || (mobileParty.HasLandNavigationCapability && (flag3 || flag2)))
1152: 						{
1153: 							float num7 = priceFactor - num6 * num5;
1154: 							float demand = town.MarketData.GetDemand(item.ItemCategory);
1155: 							float num8 = Campaign.Current.Models.SettlementEconomyModel.CalculateDailySettlementBudgetForItemCategory(town, demand, item.ItemCategory) + (float)(2 * item.Value);
1156: 							int itemPrice = town.GetItemPrice(item, mobileParty, isSelling: true);
1157: 							float num9 = ((item.ItemCategory == DefaultItemCategories.PackAnimal) ? 1.5f : 1f);
1158: 							float num10 = (mobileParty.HasNavalNavigationCapability ? 5f : 3f);
1159: 							float num11 = num8 * num7 * num4 * num9 * num10;
1160: 							if (num11 > 0f || flag3 || flag2)
1161: 							{
1162: 								int num12 = MBRandom.RoundRandomized(num11 / (float)itemPrice);
1163: 								if (mobileParty.HasLandNavigationCapability)
1164: 								{
1165: 									if (flag2)
1166: 									{
1167: 										num12 = num2;
1168: 									}
1169: 									else if (flag3)
1170: 									{
1171: 										num12 = num3;
1172: 									}
1173: 								}
1174: 								int amount = elementCopyAtIndex.Amount;
1175: 								if (num12 > amount)
1176: 								{
1177: 									num12 = amount;
1178: 								}
1179: 								if (num12 * itemPrice > town.Gold)

 succeeded in 731ms:
   1: using System.Linq;
   2: using TaleWorlds.CampaignSystem.CharacterDevelopment;
   3: using TaleWorlds.CampaignSystem.ComponentInterfaces;
   4: using TaleWorlds.CampaignSystem.Party;
   5: using TaleWorlds.CampaignSystem.Party.PartyComponents;
   6: using TaleWorlds.Core;
   7: using TaleWorlds.Library;
   8: 
   9: namespace TaleWorlds.CampaignSystem.GameComponents;
  10: 
  11: public class DefaultCaravanModel : CaravanModel
  12: {
  13: 	public override int MaxNumberOfItemsToBuyFromSingleCategory => 300;
  14: 
  15: 	public override float GetEliteCaravanSpawnChance(Hero hero)
  16: 	{
  17: 		float result = 0f;
  18: 		if (hero.Power >= 112f)
  19: 		{
  20: 			result = hero.Power * 0.0045f - 0.5f;
  21: 		}
  22: 		return result;
  23: 	}
  24: 
  25: 	public override int GetPowerChangeAfterCaravanCreation(Hero hero, MobileParty caravanParty)
  26: 	{
  27: 		if (hero.Power >= 50f)
  28: 		{
  29: 			return -30;
  30: 		}
  31: 		return 0;
  32: 	}
  33: 
  34: 	public override bool CanHeroCreateCaravan(Hero hero)
  35: 	{
  36: 		if (hero.IsMerchant && hero.PartyBelongedTo == null && hero.OwnedCaravans.Count((CaravanPartyComponent x) => !x.MobileParty.Ai.IsDisabled) == 0 && hero.IsActive && !hero.IsTemplate)
  37: 		{
  38: 			return hero.CanLeadParty();
  39: 		}
  40: 		return false;
  41: 	}
  42: 
  43: 	public override int GetCaravanFormingCost(bool largerCaravan, bool navalCaravan)
  44: 	{
  45: 		int num = (largerCaravan ? 22500 : 15000);
  46: 		if (CharacterObject.PlayerCharacter.Culture.HasFeat(DefaultCulturalFeats.AseraiTraderFeat))
  47: 		{
  48: 			return MathF.Round((float)num * DefaultCulturalFeats.AseraiTraderFeat.EffectBonus);
  49: 		}
  50: 		return num;
  51: 	}
  52: 
  53: 	public override int GetInitialTradeGold(Hero owner, bool navalCaravan, bool largeCaravan)
  54: 	{
  55: 		int num = 10000;
  56: 		int num2 = ((owner == Hero.MainHero) ? 5000 : 0);
  57: 		if (largeCaravan)
  58: 		{
  59: 			num = 17500;
  60: 		}
  61: 		return num + num2;
  62: 	}
  63: 
  64: 	public override int GetMaxGoldToSpendOnOneItemCategory(MobileParty caravan, ItemCategory itemCategory)
  65: 	{
  66: 		return 1500;
  67: 	}
  68: }

codex
The distance path is cache-backed in the installed code: same-face is a Vec2 distance; otherwise it goes through closest-entrance cache plus `GetSettlementToSettlementDistanceWithLandRatio`. That supports the “acceptable double compute” claim unless the hook is firing at a much higher cadence than vanilla re-think.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' -Pattern 'private bool CanTradeWith' | ForEach-Object { "'$_.LineNumber }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' -Pattern 'private float CalculateBudgetFactor' | ForEach-Object { "'$_.LineNumber }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' -Pattern 'private void BuyCategory|GetMaxGoldToSpendOnOneItemCategory|CalculateBudgetFactor\\(' -Context 0,6 | ForEach-Object { '{0}: {1}' -f "'$_.LineNumber,$_.Line.Trim(); $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' -Pattern 'private void CacheVeryFarDistances' | ForEach-Object { "'$_.LineNumber }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' -Pattern 'SyncData' -Context 0,8 | ForEach-Object { '{0}: {1}' -f "'$_.LineNumber,$_.Line.Trim(); $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 774ms:
2231

 succeeded in 769ms:
1267

 succeeded in 677ms:
407

 succeeded in 699ms:
1244: float budgetFactor = CalculateBudgetFactor(caravanParty);
		RefreshTotalValueOfItemsAtCategoryForParty(caravanParty);
		MBList<ItemCategory> mBList = ItemCategories.All.OrderByDescending((ItemCategory x) => CalculateBuyValue(x, town, caravanParty, budgetFactor, capacityFactor)).ToMBList();
		int num = (caravanParty.HasNavalNavigationCapability ? 10 : 5);
		for (int num2 = 0; num2 < num; num2++)
		{
			BuyCategory(caravanParty, town, mBList[num2], budgetFactor, capacityFactor, list);
1267: private float CalculateBudgetFactor(MobileParty caravanParty)
	{
		return 0.1f + TaleWorlds.Library.MathF.Clamp((float)caravanParty.PartyTradeGold / 5000f, 0f, 1f);
	}

	private float CalculateCapacityFactor(MobileParty caravanParty)
	{
1279: private void BuyCategory(MobileParty caravanParty, Town town, ItemCategory category, float budgetFactor, float capacityFactor, List<(EquipmentElement, int)> boughtItems)
	{
		float num = CalculateBuyValue(category, town, caravanParty, budgetFactor, capacityFactor);
		if (num < 7f || (caravanParty.TotalWeightCarried / (float)caravanParty.InventoryCapacity > 0.9f && !category.IsAnimal) || town.MarketData.GetCategoryData(category).InStore == 0)
		{
			return;
		}
1286: float num2 = TaleWorlds.Library.MathF.Min(TaleWorlds.Library.MathF.Min((float)caravanParty.PartyTradeGold * 0.5f, num * 1.5f), (float)Campaign.Current.Models.CaravanModel.GetMaxGoldToSpendOnOneItemCategory(caravanParty, category));
		if (!Campaign.Current.GameStarted)
		{
			num2 *= 0.5f;
		}
		float num3 = num2;
		int num4 = 0;

 succeeded in 703ms:
352: public override void SyncData(IDataStore dataStore)
	{
		dataStore.SyncData("_tradeRumorTakenCaravans", ref _tradeRumorTakenCaravans);
		dataStore.SyncData("_lootedCaravans", ref _lootedCaravans);
		dataStore.SyncData("_interactedCaravans", ref _interactedCaravans);
		dataStore.SyncData("_tradeActionLogs", ref _tradeActionLogs);
		dataStore.SyncData("_caravanLastHomeTownVisitTime", ref _caravanLastHomeTownVisitTime);
		dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
	}
354: dataStore.SyncData("_tradeRumorTakenCaravans", ref _tradeRumorTakenCaravans);
		dataStore.SyncData("_lootedCaravans", ref _lootedCaravans);
		dataStore.SyncData("_interactedCaravans", ref _interactedCaravans);
		dataStore.SyncData("_tradeActionLogs", ref _tradeActionLogs);
		dataStore.SyncData("_caravanLastHomeTownVisitTime", ref _caravanLastHomeTownVisitTime);
		dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
	}

	private void DoInitialTradeRuns()
355: dataStore.SyncData("_lootedCaravans", ref _lootedCaravans);
		dataStore.SyncData("_interactedCaravans", ref _interactedCaravans);
		dataStore.SyncData("_tradeActionLogs", ref _tradeActionLogs);
		dataStore.SyncData("_caravanLastHomeTownVisitTime", ref _caravanLastHomeTownVisitTime);
		dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
	}

	private void DoInitialTradeRuns()
	{
356: dataStore.SyncData("_interactedCaravans", ref _interactedCaravans);
		dataStore.SyncData("_tradeActionLogs", ref _tradeActionLogs);
		dataStore.SyncData("_caravanLastHomeTownVisitTime", ref _caravanLastHomeTownVisitTime);
		dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
	}

	private void DoInitialTradeRuns()
	{
		foreach (MobileParty allCaravanParty in MobileParty.AllCaravanParties)
357: dataStore.SyncData("_tradeActionLogs", ref _tradeActionLogs);
		dataStore.SyncData("_caravanLastHomeTownVisitTime", ref _caravanLastHomeTownVisitTime);
		dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
	}

	private void DoInitialTradeRuns()
	{
		foreach (MobileParty allCaravanParty in MobileParty.AllCaravanParties)
		{
358: dataStore.SyncData("_caravanLastHomeTownVisitTime", ref _caravanLastHomeTownVisitTime);
		dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
	}

	private void DoInitialTradeRuns()
	{
		foreach (MobileParty allCaravanParty in MobileParty.AllCaravanParties)
		{
			Town town = null;
359: dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
	}

	private void DoInitialTradeRuns()
	{
		foreach (MobileParty allCaravanParty in MobileParty.AllCaravanParties)
		{
			Town town = null;
			Town town2 = allCaravanParty.CurrentSettlement?.Town;

codex
I’m also checking whether every JSON field has a consumer and a validation gate. The current map looks complete: booleans route through `IsActiveFor`, distance fields into score/range, war string into parser/provider, and basket fields into budget/model.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=407; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 28 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=1238; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 58 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=2231; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 18 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Features\\CaravanTrade Main\\Features\\CulturalFeats\\Models Main\\Features -Recurse -File -Include *.cs | Select-String -Pattern 'Enabled|ApplyToPlayerCaravans|RangeMultiplier|DistanceDecayExponent|NearFieldFlattenDays|MaxCompensation|AntiShuttlePenalty|WarTradePolicy|BudgetFactorFloor|InitialTradeGold|MaxGoldPerCategory' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content 'docs\\reviews\\rca-caravan-trade-2026-07-04.md' -TotalCount 220" in C:\Users\mikew\source\repos\TAOM
 succeeded in 778ms:
 407: 	private void CacheVeryFarDistances()
 408: 	{
 409: 		MobileParty.NavigationType navigationType = MobileParty.NavigationType.Naval;
 410: 		float num = 20f;
 411: 		float num2 = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigationType) * num;
 412: 		float num3 = Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed * (float)CampaignTime.HoursInDay;
 413: 		_navalCaravanVeryFarCache = num2 / num3;
 414: 		navigationType = MobileParty.NavigationType.Default;
 415: 		num = 5f;
 416: 		num2 = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigationType) * num;
 417: 		num3 = Campaign.Current.EstimatedAverageCaravanPartySpeed * (float)CampaignTime.HoursInDay;
 418: 		_defaultCaravanVeryFarCache = num2 / num3;
 419: 	}
 420: 
 421: 	private void OnMapEventEnded(MapEvent mapEvent)
 422: 	{
 423: 		foreach (PartyBase involvedParty in mapEvent.InvolvedParties)
 424: 		{
 425: 			if (!involvedParty.IsMobile || !involvedParty.MobileParty.IsCaravan || !mapEvent.IsWinnerSide(involvedParty.Side))
 426: 			{
 427: 				continue;
 428: 			}
 429: 			if (involvedParty.MobileParty.HasNavalNavigationCapability)
 430: 			{
 431: 				DiscardShips(involvedParty.MobileParty);
 432: 			}
 433: 			MobileParty mobileParty = involvedParty.MobileParty;
 434: 			int numberOfPackAnimals = mobileParty.ItemRoster.NumberOfPackAnimals;

 succeeded in 695ms:
# RCA — CaravanTrade deep-review (2026-07-04)

## Summary

New feature `CaravanTrade` (AI/player caravans range past the local town cluster, trade across the endless Free-vs-Evil war, carry fuller baskets). Five-agent `/deep-review`: **Standards PASS, Compat 24/24, Efficiency 1 MED, Completeness COMPLETE, Data-Flow 1 HIGH + 1 design note.** One HIGH bug confirmed against source and fixed in-session; one MED verified as a non-issue (cache-backed) and downgraded; one design observation recorded for playtest.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `AllowWartimeTrade`'s `SameAlignmentAndNeutral` policy (the shipped default) used `!IAlignmentService.AreEnemyAlignments(a,b)`, but that method treats **Neutral as an enemy of everyone** (`AlignmentService.cs:49-50`). So for any pairing involving a Neutral kingdom (Umbar, battania, shaghana, abanissa) the war veto was **not** lifted — the exact opposite of the feature's documented "neutrals always tradeable." | Shared-service semantics misuse | (a) I called a shared `IAlignmentService` method without reading its implementation; its Neutral semantics are subtle and asymmetric. (b) A **sibling feature — AlignmentRecruitment — had already hit this exact trap and documented the work-around** (`RecruitmentAlignmentService.cs:8-10`, `docs/features/alignment-recruitment.md`), but I didn't consult it before reusing the same service. (c) My unit test **mocked `AreEnemyAlignments` directly** with hand-picked returns, so it validated my *assumption* about the contract rather than the *real* behavior — the mock masked the inversion. | Fixed: resolve sides via `GetKingdomSide` and treat Neutral as "trade with anyone", mirroring `RecruitmentAlignmentService` (`CaravanTradeService.cs`). Tests rewritten to mock `GetKingdomSide` (the real inputs) not the derived predicate, + explicit Neutral-caravan / Neutral-target / opposite-side regression cases. LESSONS-LEARNED entry added. |
| 2 | MED→LOW | `GetTradeScoreForTown` postfix re-calls `AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty`, recomputing a distance vanilla already computed (double compute per (caravan, town) in the destination loop). | Efficiency | Efficiency agent hypothesized "potentially expensive navigation pathfinding." | **Verified against `DefaultMapDistanceModel.GetDistance(MobileParty, Settlement)` (`:60-84`): the party→settlement distance is served from `_navigationCache.GetSettlementToSettlementDistanceWithLandRatio` (the precomputed settlement distance cache) plus a couple of `Vec2.Distance` ops — a cache read, NOT a live pathfind.** The double-call is cheap and on an infrequent per-re-think path. **Kept the AiHelper call**: it is both cache-cheap AND terrain-accurate (a straight-line proxy would ignore the LOTR map's mountains/water and mis-order towns). No code change; MED downgraded to LOW-accepted with evidence. |
| 3 | — | Home-town is exempt from the distance re-weight while non-home near towns are scaled down; could bias caravans toward home ("rubber-band") more than intended. | Design observation | Not a wiring gap — the exemption is deliberate (don't disturb vanilla's home-return `num5` quadratic pull). | Recorded as a **playtest item** in `docs/features/caravan-trade.md`. If home-return proves too frequent in play, options: apply a mild reweight to home too, or expose a home-pull knob. No pre-emptive code change (YAGNI). |

## Root-cause pattern (Finding 1)

**Reusing a shared decision service without reading its implementation — and mocking the very predicate whose behavior is in question.** `IAlignmentService.AreEnemyAlignments` reads like "are these two at odds?" but encodes a policy choice (Neutral is everyone's enemy) that is correct for *execution-relation penalties* but wrong for *trade/recruitment permissiveness*. The sibling AlignmentRecruitment feature had already paid for this lesson and left a signpost; the trap recurred because the signpost wasn't consulted. The unit test compounded it: mocking `AreEnemyAlignments("gondor","rohan") => false` tested a hand-authored contract, not the shipped logic, so a green suite gave false confidence.

## Why each agent's scope did/didn't catch it

- **Data-Flow agent (Agent 5): CAUGHT IT.** It traced the war-policy field to the *real* `AlignmentService.AreEnemyAlignments` implementation and cross-referenced the sibling `RecruitmentAlignmentService`'s documented work-around — exactly the cross-file, cross-feature trace this agent exists for. Every prior HIGH in this project's reviews was a data-flow gap; this holds.
- **Standards (Agent 1):** scope is ADR compliance / structure — the code *is* well-structured; a semantic inversion inside a correctly-shaped delegate is out of scope.
- **Compat (Agent 2):** verifies TaleWorlds signatures; `IAlignmentService` is TAOM-internal, not an engine API.
- **Efficiency (Agent 3):** perf-only.
- **Completeness (Agent 4):** verified tests *exist* and pass — but a test that mocks the buggy predicate passes green, so "tests pass" was not evidence of correctness (the `feedback_audit_findings_not_always_correct` / evidence-over-claims lesson: a green mock-based test proves the mock, not the integration).

## Lesson codified

Appended to `docs/reviews/LESSONS-LEARNED.md` → "Adapters & TaleWorlds API" (shared-service reuse) + "Testing & QA" (don't mock the predicate under test). Short form:

> **Before reusing a shared decision service (`IAlignmentService`, etc.), read its implementation AND grep for a sibling feature that already consumes it** — subtle policy semantics (Neutral-as-enemy) differ by caller intent, and a prior feature has often already documented the work-around. **In the unit test, mock the lowest-level inputs (`GetKingdomSide`), never the derived predicate you depend on** — mocking `AreEnemyAlignments` validates your assumption about the contract, not the shipped behavior, and masks an inverted implementation behind a green suite.

## Verification

- `dotnet build Main/TAOM.csproj`: 0 errors.
- `dotnet test` full suite: **4085 passed, 0 failed, 2 skipped.**
- CaravanTrade binding tests pass against installed v1.4.6 (private methods, FieldRef targets, AiHelper, model overrides all resolve).

 succeeded in 772ms:
2231: 	private bool CanTradeWith(IFaction caravanFaction, IFaction targetFaction)
2232: 	{
2233: 		if (caravanFaction.IsAtWarWith(targetFaction))
2234: 		{
2235: 			return false;
2236: 		}
2237: 		if (caravanFaction == Hero.MainHero.MapFaction)
2238: 		{
2239: 			if (targetFaction is Kingdom item)
2240: 			{
2241: 				return !_prohibitedKingdomsForPlayerCaravans.Contains(item);
2242: 			}
2243: 			return true;
2244: 		}
2245: 		return true;
2246: 	}
2247: }

 succeeded in 806ms:
1238: 	}
1239: 
1240: 	private void BuyGoods(MobileParty caravanParty, Town town)
1241: 	{
1242: 		List<(EquipmentElement, int)> list = new List<(EquipmentElement, int)>();
1243: 		float capacityFactor = CalculateCapacityFactor(caravanParty);
1244: 		float budgetFactor = CalculateBudgetFactor(caravanParty);
1245: 		RefreshTotalValueOfItemsAtCategoryForParty(caravanParty);
1246: 		MBList<ItemCategory> mBList = ItemCategories.All.OrderByDescending((ItemCategory x) => CalculateBuyValue(x, town, caravanParty, budgetFactor, capacityFactor)).ToMBList();
1247: 		int num = (caravanParty.HasNavalNavigationCapability ? 10 : 5);
1248: 		for (int num2 = 0; num2 < num; num2++)
1249: 		{
1250: 			BuyCategory(caravanParty, town, mBList[num2], budgetFactor, capacityFactor, list);
1251: 		}
1252: 		if (caravanParty.HasNavalNavigationCapability)
1253: 		{
1254: 			BuyCategory(caravanParty, town, DefaultItemCategories.Grain, budgetFactor, capacityFactor, list);
1255: 			BuyCategory(caravanParty, town, DefaultItemCategories.Fish, budgetFactor, capacityFactor, list);
1256: 		}
1257: 		else if ((float)(caravanParty.ItemRoster.NumberOfPackAnimals + caravanParty.ItemRoster.NumberOfLivestockAnimals) < (float)caravanParty.Party.NumberOfAllMembers * 2f && caravanParty.ItemRoster.NumberOfPackAnimals < caravanParty.Party.NumberOfAllMembers && _packAnimalCategoryIndex >= 0 && caravanParty.PartyTradeGold > 1000)
1258: 		{
1259: 			BuyCategory(caravanParty, town, DefaultItemCategories.PackAnimal, budgetFactor, capacityFactor, list);
1260: 		}
1261: 		if (!list.IsEmpty())
1262: 		{
1263: 			CampaignEventDispatcher.Instance.OnCaravanTransactionCompleted(caravanParty, town, list);
1264: 		}
1265: 	}
1266: 
1267: 	private float CalculateBudgetFactor(MobileParty caravanParty)
1268: 	{
1269: 		return 0.1f + TaleWorlds.Library.MathF.Clamp((float)caravanParty.PartyTradeGold / 5000f, 0f, 1f);
1270: 	}
1271: 
1272: 	private float CalculateCapacityFactor(MobileParty caravanParty)
1273: 	{
1274: 		float num = caravanParty.TotalWeightCarried / ((float)caravanParty.InventoryCapacity + 1f);
1275: 		num *= 0.9f;
1276: 		return 1.1f - TaleWorlds.Library.MathF.Clamp(num, 0f, 1f);
1277: 	}
1278: 
1279: 	private void BuyCategory(MobileParty caravanParty, Town town, ItemCategory category, float budgetFactor, float capacityFactor, List<(EquipmentElement, int)> boughtItems)
1280: 	{
1281: 		float num = CalculateBuyValue(category, town, caravanParty, budgetFactor, capacityFactor);
1282: 		if (num < 7f || (caravanParty.TotalWeightCarried / (float)caravanParty.InventoryCapacity > 0.9f && !category.IsAnimal) || town.MarketData.GetCategoryData(category).InStore == 0)
1283: 		{
1284: 			return;
1285: 		}
1286: 		float num2 = TaleWorlds.Library.MathF.Min(TaleWorlds.Library.MathF.Min((float)caravanParty.PartyTradeGold * 0.5f, num * 1.5f), (float)Campaign.Current.Models.CaravanModel.GetMaxGoldToSpendOnOneItemCategory(caravanParty, category));
1287: 		if (!Campaign.Current.GameStarted)
1288: 		{
1289: 			num2 *= 0.5f;
1290: 		}
1291: 		float num3 = num2;
1292: 		int num4 = 0;
1293: 		int num5;
1294: 		do
1295: 		{

2026-07-04T20:46:16.950922Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.9 seconds
Output:
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path Main\Features\CaravanTrade Main\Features\Cultural[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mA positional parameter cannot be found that accepts argument 'Main\Features'.[0m

 exited 1 in 857ms:
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path Main\Features\CaravanTrade Main\Features\Cultural[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mA positional parameter cannot be found that accepts argument 'Main\Features'.[0m

codex
One command for field-consumer tracing had a PowerShell path-list syntax error; I’m rerunning it with explicit path arrays. The RCA itself confirms the internal review downgraded the distance recompute after the same cache-backed check, but I’m relying on the decompile I just read, not the RCA claim.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$paths=@('"'Main\\Features\\CaravanTrade','Main\\Features\\CulturalFeats\\Models','Main\\Features\\TaomSettings.cs','Main\\IoC.cs','Main\\SubModule.cs','TAOM.Tests\\Features\\CaravanTrade'); Get-ChildItem -Path "'$paths -Recurse -File -Include *.cs | Select-String -Pattern '"'Enabled|ApplyToPlayerCaravans|RangeMultiplier|DistanceDecayExponent|NearFieldFlattenDays|MaxCompensation|AntiShuttlePenalty|WarTradePolicy|BudgetFactorFloor|InitialTradeGold|MaxGoldPerCategory|CaravanRangeMultiplier|CaravanBudgetDiversityFloor|CaravanTradeApplyToPlayer|EnableCaravanTrade|CaravanWarTradePolicy' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -File -Include *.cs,json,xml | Select-String -Pattern 'rohan|dol_guldur|dolguldur|caravan_trade|SameAlignmentAndNeutral|IgnoreWar|warTradePolicy' | Select-Object -First 220 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'docs\\features\\caravan-trade.md' | Select-Object -First 220 | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path TAOM.Tests\\Features\\CaravanTrade -Recurse -File -Include *.cs | Select-String -Pattern 'rohan|dol_guldur|dolguldur|SameAlignmentAndNeutral|IgnoreWar|None|SelectedIndex|TaomSettings|CaravanWarTradePolicy' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 761ms:
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CalculateBudgetFactor_Patch.cs:32: __result = _service.ApplyBudgetFactorFloor(__result, isPlayer);
Main\Features\CaravanTrade\CaravanTradeConfig.cs:10: public bool Enabled { get; set; } = true;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:11: public bool ApplyToPlayerCaravans { get; set; } = true;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:14: public float RangeMultiplier { get; set; } = 1.6f;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:15: public float DistanceDecayExponent { get; set; } = 0.5f;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:16: public float NearFieldFlattenDays { get; set; } = 2.0f;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:17: public float MaxCompensation { get; set; } = 6.0f;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:18: public float AntiShuttlePenalty { get; set; } = 0.35f;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:21: public string WarTradePolicy { get; set; } = "SameAlignmentAndNeutral";
Main\Features\CaravanTrade\CaravanTradeConfig.cs:24: public float BudgetFactorFloor { get; set; } = 0.35f;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:25: public int InitialTradeGold { get; set; } = 15000;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:26: public int MaxGoldPerCategory { get; set; } = 1500;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:29: /// <summary>Parsing + known-set validation for the <see cref="WarTradePolicy"/> config string (the M1 typo trap).</summary>
Main\Features\CaravanTrade\CaravanTradeConfig.cs:30: public static class WarTradePolicyParser
Main\Features\CaravanTrade\CaravanTradeConfig.cs:32: /// <summary>Case-insensitively parse a config string into a <see cref="WarTradePolicy"/>. Returns false for null/empty/unknown.</summary>
Main\Features\CaravanTrade\CaravanTradeConfig.cs:33: public static bool TryParse(string value, out WarTradePolicy policy)
Main\Features\CaravanTrade\CaravanTradeConfig.cs:35: policy = WarTradePolicy.SameAlignmentAndNeutral;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:42: policy = WarTradePolicy.None;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:45: policy = WarTradePolicy.IgnoreWar;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:48: policy = WarTradePolicy.SameAlignmentAndNeutral;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:62: Enabled = parsed.Enabled,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:63: ApplyToPlayerCaravans = parsed.ApplyToPlayerCaravans,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:64: RangeMultiplier = parsed.RangeMultiplier,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:65: DistanceDecayExponent = parsed.DistanceDecayExponent,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:66: NearFieldFlattenDays = parsed.NearFieldFlattenDays,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:67: MaxCompensation = parsed.MaxCompensation,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:68: AntiShuttlePenalty = parsed.AntiShuttlePenalty,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:69: WarTradePolicy = parsed.WarTradePolicy,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:70: BudgetFactorFloor = parsed.BudgetFactorFloor,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:71: InitialTradeGold = parsed.InitialTradeGold,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:72: MaxGoldPerCategory = parsed.MaxGoldPerCategory,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:78: if (!FiniteFloatValidator.IsFiniteInRange(c.RangeMultiplier, 1f, 4f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:80: _logger.LogWarning($"CaravanTradeConfigProvider: rangeMultiplier={c.RangeMultiplier} must be finite in [1,4], reverting to {defaults.RangeMultiplier}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:81: c.RangeMultiplier = defaults.RangeMultiplier;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:86: if (!FiniteFloatValidator.IsFiniteInRange(c.DistanceDecayExponent, 0.25f, 4f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:88: _logger.LogWarning($"CaravanTradeConfigProvider: distanceDecayExponent={c.DistanceDecayExponent} must be finite in [0.25,4], reverting to {defaults.DistanceDecayExponent}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:89: c.DistanceDecayExponent = defaults.DistanceDecayExponent;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:93: if (!FiniteFloatValidator.IsFiniteInRange(c.NearFieldFlattenDays, 0f, 20f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:95: _logger.LogWarning($"CaravanTradeConfigProvider: nearFieldFlattenDays={c.NearFieldFlattenDays} must be finite in [0,20], reverting to {defaults.NearFieldFlattenDays}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:96: c.NearFieldFlattenDays = defaults.NearFieldFlattenDays;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:101: if (!FiniteFloatValidator.IsFiniteInRange(c.MaxCompensation, 1f, 20f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:103: _logger.LogWarning($"CaravanTradeConfigProvider: maxCompensation={c.MaxCompensation} must be finite in [1,20], reverting to {defaults.MaxCompensation}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:104: c.MaxCompensation = defaults.MaxCompensation;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:109: if (!FiniteFloatValidator.IsFiniteInRange(c.AntiShuttlePenalty, 0f, 1f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:111: _logger.LogWarning($"CaravanTradeConfigProvider: antiShuttlePenalty={c.AntiShuttlePenalty} must be finite in [0,1], reverting to {defaults.AntiShuttlePenalty}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:112: c.AntiShuttlePenalty = defaults.AntiShuttlePenalty;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:117: if (!WarTradePolicyParser.TryParse(c.WarTradePolicy, out _))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:119: _logger.LogWarning($"CaravanTradeConfigProvider: warTradePolicy='{c.WarTradePolicy}' is not one of None/IgnoreWar/SameAlignmentAndNeutral, reverting to {defaults.WarTradePolicy}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:120: c.WarTradePolicy = defaults.WarTradePolicy;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:124: if (!FiniteFloatValidator.IsFiniteInRange(c.BudgetFactorFloor, 0f, 1f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:126: _logger.LogWarning($"CaravanTradeConfigProvider: budgetFactorFloor={c.BudgetFactorFloor} must be finite in [0,1], reverting to {defaults.BudgetFactorFloor}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:127: c.BudgetFactorFloor = defaults.BudgetFactorFloor;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:131: if (c.InitialTradeGold < 1000 || c.InitialTradeGold > 100000)
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:133: _logger.LogWarning($"CaravanTradeConfigProvider: initialTradeGold={c.InitialTradeGold} must be in [1000,100000], reverting to {defaults.InitialTradeGold}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:134: c.InitialTradeGold = defaults.InitialTradeGold;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:138: if (c.MaxGoldPerCategory < 100 || c.MaxGoldPerCategory > 20000)
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:140: _logger.LogWarning($"CaravanTradeConfigProvider: maxGoldPerCategory={c.MaxGoldPerCategory} must be in [100,20000], reverting to {defaults.MaxGoldPerCategory}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:141: c.MaxGoldPerCategory = defaults.MaxGoldPerCategory;
Main\Features\CaravanTrade\CaravanTradeService.cs:47: double denom = Math.Pow(_settings.NearFieldFlattenDays + days, _settings.DistanceDecayExponent);
Main\Features\CaravanTrade\CaravanTradeService.cs:50: float maxComp = _settings.MaxCompensation;
Main\Features\CaravanTrade\CaravanTradeService.cs:57: result *= 1f - _settings.AntiShuttlePenalty;
Main\Features\CaravanTrade\CaravanTradeService.cs:66: if (!_settings.Enabled)
Main\Features\CaravanTrade\CaravanTradeService.cs:68: return vanillaVeryFarDays * _settings.RangeMultiplier;
Main\Features\CaravanTrade\CaravanTradeService.cs:77: switch (_settings.WarTradePolicy)
Main\Features\CaravanTrade\CaravanTradeService.cs:79: case WarTradePolicy.IgnoreWar:
Main\Features\CaravanTrade\CaravanTradeService.cs:81: case WarTradePolicy.SameAlignmentAndNeutral:
Main\Features\CaravanTrade\CaravanTradeService.cs:92: case WarTradePolicy.None:
Main\Features\CaravanTrade\CaravanTradeService.cs:98: public float ApplyBudgetFactorFloor(float vanillaBudgetFactor, bool isPlayerCaravan)
Main\Features\CaravanTrade\CaravanTradeService.cs:107: return Math.Max(vanillaBudgetFactor, _settings.BudgetFactorFloor);
Main\Features\CaravanTrade\CaravanTradeService.cs:110: public int ResolveInitialTradeGold(int vanillaValue, bool isPlayerCaravan)
Main\Features\CaravanTrade\CaravanTradeService.cs:115: return Math.Max(vanillaValue, _settings.InitialTradeGold);
Main\Features\CaravanTrade\CaravanTradeService.cs:118: public int ResolveMaxGoldPerCategory(int vanillaValue, bool isPlayerCaravan)
Main\Features\CaravanTrade\CaravanTradeService.cs:122: return _settings.MaxGoldPerCategory;
Main\Features\CaravanTrade\CaravanTradeService.cs:127: if (!_settings.Enabled)
Main\Features\CaravanTrade\CaravanTradeService.cs:129: if (isPlayerCaravan && !_settings.ApplyToPlayerCaravans)
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:21: public bool Enabled => TaomSettings.Instance?.EnableCaravanTrade ?? Cfg.Enabled;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:22: public bool ApplyToPlayerCaravans => TaomSettings.Instance?.CaravanTradeApplyToPlayer ?? Cfg.ApplyToPlayerCaravans;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:23: public float RangeMultiplier => TaomSettings.Instance?.CaravanRangeMultiplier ?? Cfg.RangeMultiplier;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:26: public float DistanceDecayExponent => Cfg.DistanceDecayExponent;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:27: public float NearFieldFlattenDays => Cfg.NearFieldFlattenDays;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:28: public float MaxCompensation => Cfg.MaxCompensation;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:29: public float AntiShuttlePenalty => Cfg.AntiShuttlePenalty;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:31: public WarTradePolicy WarTradePolicy => ResolveWarPolicy();
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:32: public float BudgetFactorFloor => TaomSettings.Instance?.CaravanBudgetDiversityFloor ?? Cfg.BudgetFactorFloor;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:35: public int InitialTradeGold => Cfg.InitialTradeGold;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:36: public int MaxGoldPerCategory => Cfg.MaxGoldPerCategory;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:38: private WarTradePolicy ResolveWarPolicy()
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:40: var dropdown = TaomSettings.Instance?.CaravanWarTradePolicy;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:45: case 0: return WarTradePolicy.None;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:46: case 1: return WarTradePolicy.SameAlignmentAndNeutral;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:47: case 2: return WarTradePolicy.IgnoreWar;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:52: return WarTradePolicyParser.TryParse(Cfg.WarTradePolicy, out var policy)
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:54: : WarTradePolicy.SameAlignmentAndNeutral;
Main\Features\CaravanTrade\ICaravanTradeService.cs:9: public enum WarTradePolicy
Main\Features\CaravanTrade\ICaravanTradeService.cs:37: /// <c>maxCompensation</c>; near-equal-distance towns become near-tied so the built-in profit
Main\Features\CaravanTrade\ICaravanTradeService.cs:52: /// the feature is enabled — it only widens the candidate set; the re-weight and war gate remain
Main\Features\CaravanTrade\ICaravanTradeService.cs:60: /// to allow trade despite the war, per the configured <see cref="WarTradePolicy"/>.
Main\Features\CaravanTrade\ICaravanTradeService.cs:70: float ApplyBudgetFactorFloor(float vanillaBudgetFactor, bool isPlayerCaravan);
Main\Features\CaravanTrade\ICaravanTradeService.cs:77: int ResolveInitialTradeGold(int vanillaValue, bool isPlayerCaravan);
Main\Features\CaravanTrade\ICaravanTradeService.cs:82: int ResolveMaxGoldPerCategory(int vanillaValue, bool isPlayerCaravan);
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:13: bool Enabled { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:16: bool ApplyToPlayerCaravans { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:19: float RangeMultiplier { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:22: float DistanceDecayExponent { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:25: float NearFieldFlattenDays { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:28: float MaxCompensation { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:31: float AntiShuttlePenalty { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:34: WarTradePolicy WarTradePolicy { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:37: float BudgetFactorFloor { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:40: int InitialTradeGold { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:43: int MaxGoldPerCategory { get; }
Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:26: public override int GetInitialTradeGold(Hero owner, bool navalCaravan, bool largeCaravan)
Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:27: => _caravanTrade.ResolveInitialTradeGold(
Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:28: base.GetInitialTradeGold(owner, navalCaravan, largeCaravan),
Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:32: => _caravanTrade.ResolveMaxGoldPerCategory(
Main\Features\TaomSettings.cs:50: public bool EnableCaravanTrade { get; set; } = true;
Main\Features\TaomSettings.cs:55: public bool CaravanTradeApplyToPlayer { get; set; } = true;
Main\Features\TaomSettings.cs:60: public float CaravanRangeMultiplier { get; set; } = 1.6f;
Main\Features\TaomSettings.cs:65: public Dropdown<string> CaravanWarTradePolicy { get; set; } = new Dropdown<string>(
Main\Features\TaomSettings.cs:71: public float CaravanBudgetDiversityFloor { get; set; } = 0.35f;
Main\Features\TaomSettings.cs:84: HintText = "When enabled, castles gain notables with recruitable volunteers — the player can 'Recruit troops' at any accessible castle. Existing notables remain in the save if you later disable this.")]
Main\Features\TaomSettings.cs:89: HintText = "When enabled, AI lord parties also score, travel to, and recruit volunteers from castles like they do from towns. Requires Enable Castle Recruitment.")]
Main\Features\TaomSettings.cs:113: HintText = "When enabled, a town/castle (and its villages) conquered by a different culture gradually adopts the new owner's culture — producing their troops, militia, and identity. Disabling stops NEW conversions; already-converted settlements stay converted.")]
Main\Features\TaomSettings.cs:123: HintText = "When enabled, a conquered fief only converts once its loyalty is high enough (configured in culture_conversion_config.json), so a city in unrest never flips. Default: off.")]
Main\Features\TaomSettings.cs:128: HintText = "When enabled, conversion also replaces the settlement's notables with ones of the new culture (a Mordor-held Gondor town gets orc notables). Their workshops, alleys and caravans transfer to the newcomers; your relations with the old notables do not. Default: on.")]
Main\Features\TaomSettings.cs:135: HintText = "When enabled, a scripted war will escalate between Free Peoples and Dark Powers.")]
Main\Features\TaomSettings.cs:136: public bool WarOfTheRingEnabled { get; set; } = true;
Main\Features\TaomSettings.cs:158: public bool MomentumEnabled { get; set; } = true;
Main\Features\TaomSettings.cs:168: public bool MomentumVictoryEnabled { get; set; } = false;
Main\Features\TaomSettings.cs:199: HintText = "If enabled, battle_balance_config.json TierPower values replace the vanilla formula for T1-T6.")]
Main\Features\TaomSettings.cs:258: HintText = "When enabled, you receive an event when a watched faction's settlement is besieged, with a timed window to help defend.")]
Main\Features\TaomSettings.cs:270: HintText = "When enabled, AI armies stick to their current target rather than re-optimising every 3 hours. Reduces army thrashing and improves siege follow-through.")]
Main\Features\TaomSettings.cs:623: HintText = "When enabled, a recruiter cannot recruit volunteers at a settlement controlled by an opposed-alignment kingdom (Free vs Evil). Alignment comes from execution/alignment.json, keyed by the kingdom you serve. Neutral factions (Umbar etc.) never block. When off, recruitment is vanilla.")]
Main\Features\TaomSettings.cs:645: HintText = "Currently DISABLED — the map's navmesh isn't set up for naval travel yet, so this feature is parked in code and this toggle has no effect for now. (When re-enabled: parties sail across water on the campaign map — the engine's native naval travel, unlocked without the Naval DLC.)")]
Main\Features\TaomSettings.cs:662: HintText = "When enabled, troops whose culture is opposed in alignment to their lord (Free vs Evil) desert each day — an Evil lord sheds Good troops and a Good lord sheds Evil troops. Alignment comes from execution/alignment.json. Neutral cultures (Umbar etc.) never desert. When off, vanilla.")]
Main\SubModule.cs:180: // FailedAsserting against vanilla's 1-157 table. Re-enabled 2026-06-01 (TAOM_Map ships Main_map +
Main\SubModule.cs:334: bool nsfEnabled = false;
Main\SubModule.cs:335: try { nsfEnabled = TaomSettings.Instance?.EnableNativeSkinFixes == true; }
Main\SubModule.cs:337: if (nsfEnabled)
Main\SubModule.cs:343: // Pre-compile Shaders — RE-ENABLED 2026-06-17 (issue #287). Walks the all-characters battle
Main\SubModule.cs:369: enabledHint:         new TextObject("{=taom_precompile_hint}Pre-compiles shaders to eliminate in-game stutter + the battle-load crash. Run once after installing TAOM."),
Main\SubModule.cs:454: // OnGameInitializationFinished, and flip the `enabled` defaults back to true.
Main\SubModule.cs:745: // Patch47 RE-ENABLED 2026-06-12 after full exoneration: its 06-12 morning indictment
Main\SubModule.cs:888: if (battleLoadDiagSvc != null && battleLoadDiagSvc.IsEnabled)
TAOM.Tests\Features\CaravanTrade\CaravanTradeBindingTests.cs:76: Assert.IsNotNull(AccessTools.Method(t, "GetInitialTradeGold"),
TAOM.Tests\Features\CaravanTrade\CaravanTradeBindingTests.cs:77: "DefaultCaravanModel.GetInitialTradeGold did not resolve — TaomCaravanModel's diversity override target is gone.");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:47: ""enabled"": true,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:48: ""applyToPlayerCaravans"": false,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:49: ""rangeMultiplier"": 2.0,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:50: ""distanceDecayExponent"": 0.6,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:51: ""nearFieldFlattenDays"": 3.0,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:52: ""maxCompensation"": 8.0,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:53: ""antiShuttlePenalty"": 0.5,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:54: ""warTradePolicy"": ""IgnoreWar"",
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:55: ""budgetFactorFloor"": 0.4,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:56: ""initialTradeGold"": 20000,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:57: ""maxGoldPerCategory"": 2500
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:62: Assert.IsTrue(c.Enabled);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:63: Assert.IsFalse(c.ApplyToPlayerCaravans);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:64: Assert.AreEqual(2.0f, c.RangeMultiplier, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:65: Assert.AreEqual(0.6f, c.DistanceDecayExponent, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:66: Assert.AreEqual(3.0f, c.NearFieldFlattenDays, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:67: Assert.AreEqual(8.0f, c.MaxCompensation, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:68: Assert.AreEqual(0.5f, c.AntiShuttlePenalty, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:69: Assert.AreEqual("IgnoreWar", c.WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:70: Assert.AreEqual(0.4f, c.BudgetFactorFloor, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:71: Assert.AreEqual(20000, c.InitialTradeGold);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:72: Assert.AreEqual(2500, c.MaxGoldPerCategory);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:81: Assert.AreEqual(1.6f, c.RangeMultiplier, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:82: Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:93: Assert.AreEqual(1.6f, c.RangeMultiplier, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:100: WriteConfig(@"{ ""rangeMultiplier"": 2.5 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:104: Assert.AreEqual(2.5f, c.RangeMultiplier, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:105: Assert.AreEqual(0.5f, c.DistanceDecayExponent, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:106: Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:112: WriteConfig(@"{ ""rangeMultiplier"": 2.0 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:117: public void GetConfig_RangeMultiplierBelowOne_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:120: WriteConfig(@"{ ""rangeMultiplier"": 0.5 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:121: Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:122: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:126: public void GetConfig_RangeMultiplierOversized_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:128: WriteConfig(@"{ ""rangeMultiplier"": 12 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:129: Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:130: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:134: public void GetConfig_NaNRangeMultiplier_RevertsToFiniteDefault()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:136: WriteConfig(@"{ ""rangeMultiplier"": NaN }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:137: Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:138: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:144: WriteConfig(@"{ ""distanceDecayExponent"": 0 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:145: Assert.AreEqual(0.5f, _sut.GetConfig().DistanceDecayExponent, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:146: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("distanceDecayExponent")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:152: WriteConfig(@"{ ""nearFieldFlattenDays"": Infinity }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:153: Assert.AreEqual(2.0f, _sut.GetConfig().NearFieldFlattenDays, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:154: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("nearFieldFlattenDays")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:158: public void GetConfig_MaxCompensationBelowOne_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:160: WriteConfig(@"{ ""maxCompensation"": 0.5 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:161: Assert.AreEqual(6.0f, _sut.GetConfig().MaxCompensation, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:162: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxCompensation")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:166: public void GetConfig_AntiShuttlePenaltyAboveOne_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:169: WriteConfig(@"{ ""antiShuttlePenalty"": 1.5 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:170: Assert.AreEqual(0.35f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:171: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:175: public void GetConfig_NegativeAntiShuttlePenalty_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:178: WriteConfig(@"{ ""antiShuttlePenalty"": -0.2 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:179: Assert.AreEqual(0.35f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:180: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:184: public void GetConfig_UnknownWarTradePolicy_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:187: WriteConfig(@"{ ""warTradePolicy"": ""IgnoreWarr"" }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:188: Assert.AreEqual("SameAlignmentAndNeutral", _sut.GetConfig().WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:189: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("warTradePolicy")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:193: public void GetConfig_KnownWarTradePolicyCaseInsensitive_Accepted()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:195: WriteConfig(@"{ ""warTradePolicy"": ""ignorewar"" }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:196: Assert.AreEqual("ignorewar", _sut.GetConfig().WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:197: _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("warTradePolicy")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:201: public void GetConfig_BudgetFactorFloorAboveOne_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:203: WriteConfig(@"{ ""budgetFactorFloor"": 1.5 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:204: Assert.AreEqual(0.35f, _sut.GetConfig().BudgetFactorFloor, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:205: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("budgetFactorFloor")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:209: public void GetConfig_InitialTradeGoldTooLow_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:211: WriteConfig(@"{ ""initialTradeGold"": 500 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:212: Assert.AreEqual(15000, _sut.GetConfig().InitialTradeGold);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:213: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("initialTradeGold")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:217: public void GetConfig_MaxGoldPerCategoryTooHigh_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:219: WriteConfig(@"{ ""maxGoldPerCategory"": 999999 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:220: Assert.AreEqual(1500, _sut.GetConfig().MaxGoldPerCategory);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:221: _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxGoldPerCategory")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:227: WriteConfig(@"{ ""rangeMultiplier"": 1.6, ""warTradePolicy"": ""None"" }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:25: _settings.Enabled.Returns(true);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:26: _settings.ApplyToPlayerCaravans.Returns(true);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:27: _settings.RangeMultiplier.Returns(1.6f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:28: _settings.DistanceDecayExponent.Returns(0.5f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:29: _settings.NearFieldFlattenDays.Returns(2.0f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:30: _settings.MaxCompensation.Returns(6.0f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:31: _settings.AntiShuttlePenalty.Returns(0.35f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:32: _settings.WarTradePolicy.Returns(WarTradePolicy.SameAlignmentAndNeutral);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:33: _settings.BudgetFactorFloor.Returns(0.35f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:34: _settings.InitialTradeGold.Returns(15000);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:35: _settings.MaxGoldPerCategory.Returns(1500);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:45: _settings.Enabled.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:52: _settings.ApplyToPlayerCaravans.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:126: public void ReweightTradeScore_VeryFarTown_MultiplierClampedToMaxCompensation()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:128: // m = 1000/(2+1000)^0.5 = 31.6, clamped to maxCompensation 6 -> result = 10 * 6.
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:134: public void ReweightTradeScore_JustLeftTown_AppliesAntiShuttlePenalty()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:147: _settings.Enabled.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:152: public void ScaleVeryFarDistance_Enabled_ScalesByRangeMultiplier()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:162: _settings.Enabled.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:169: _settings.ApplyToPlayerCaravans.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:176: _settings.WarTradePolicy.Returns(WarTradePolicy.None);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:183: _settings.WarTradePolicy.Returns(WarTradePolicy.IgnoreWar);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:224: // ---------------- ApplyBudgetFactorFloor ----------------
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:227: public void ApplyBudgetFactorFloor_Disabled_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:229: _settings.Enabled.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:230: Assert.AreEqual(0.1f, _sut.ApplyBudgetFactorFloor(0.1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:234: public void ApplyBudgetFactorFloor_PlayerScopeOff_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:236: _settings.ApplyToPlayerCaravans.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:237: Assert.AreEqual(0.1f, _sut.ApplyBudgetFactorFloor(0.1f, isPlayerCaravan: true), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:241: public void ApplyBudgetFactorFloor_NaN_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:243: Assert.IsTrue(float.IsNaN(_sut.ApplyBudgetFactorFloor(float.NaN, false)));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:247: public void ApplyBudgetFactorFloor_BelowFloor_ReturnsFloor()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:249: Assert.AreEqual(0.35f, _sut.ApplyBudgetFactorFloor(0.1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:253: public void ApplyBudgetFactorFloor_AboveFloor_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:255: Assert.AreEqual(0.8f, _sut.ApplyBudgetFactorFloor(0.8f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:258: // ---------------- ResolveInitialTradeGold ----------------
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:261: public void ResolveInitialTradeGold_Disabled_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:263: _settings.Enabled.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:264: Assert.AreEqual(10000, _sut.ResolveInitialTradeGold(10000, false));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:268: public void ResolveInitialTradeGold_PlayerScopeOff_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:270: _settings.ApplyToPlayerCaravans.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:271: Assert.AreEqual(10000, _sut.ResolveInitialTradeGold(10000, isPlayerCaravan: true));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:275: public void ResolveInitialTradeGold_VanillaBelowFloor_ReturnsFloor()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:277: Assert.AreEqual(15000, _sut.ResolveInitialTradeGold(10000, false));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:281: public void ResolveInitialTradeGold_VanillaAboveFloor_NeverLowers()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:284: Assert.AreEqual(22500, _sut.ResolveInitialTradeGold(22500, false));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:287: // ---------------- ResolveMaxGoldPerCategory ----------------
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:290: public void ResolveMaxGoldPerCategory_Disabled_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:292: _settings.Enabled.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:293: Assert.AreEqual(1500, _sut.ResolveMaxGoldPerCategory(1500, false));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:297: public void ResolveMaxGoldPerCategory_Enabled_ReturnsConfiguredValue()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:299: _settings.MaxGoldPerCategory.Returns(2500);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:300: Assert.AreEqual(2500, _sut.ResolveMaxGoldPerCategory(1500, false));

 succeeded in 672ms:
   1: # CaravanTrade
   2: 
   3: ## Overview
   4: 
   5: Makes AI (and optionally player) caravans range across the map instead of shuttling between very-close towns (e.g. Minas Tirith ↔ East/West Osgiliath), trade across TAOM's endless Free-vs-Evil war, and carry fuller baskets of goods. Four coordinated Harmony postfixes on the vanilla `CaravansCampaignBehavior` plus two `TaomCaravanModel` overrides, all delegating to one pure `ICaravanTradeService`.
   6: 
   7: ## Why This Exists
   8: 
   9: Players observed caravans orbiting a dense town cluster and appearing to trade a single good. Research into the decompiled v1.4.6 `CaravansCampaignBehavior` (2248 lines, all decision logic in **private** methods — `AiVisitSettlementBehavior` `return`s on `IsCaravan`, so it is irrelevant) found three root causes, and a fourth latent opportunity:
  10: 
  11: 1. **Distance is a penalty, not a reward.** `GetTradeScoreForTown` multiplies expected profit by `1/days` (land) plus an escalating `veryFarAddition`, and `distanceCut` hard-rejects towns past ~5× the average nearest-two-town distance. The closest town almost always wins the argmax → the shuttle.
  12: 2. **Perpetual war collapses the reachable set.** `CanTradeWith` excludes any town whose faction the caravan is at war with. In TAOM's endless war this leaves only friendly, clustered towns → forces the ping-pong. (This one method feeds both the destination filter and the mid-route abandon.)
  13: 3. **"One item" is budget-gated, not a hard cap.** `BuyGoods` attempts the top-5 (land)/top-10 (naval) categories, but `BuyCategory` skips any category whose buy-value `< 7f`, and buy-value scales with `budgetFactor = 0.1 + clamp(PartyTradeGold/5000, 0, 1)`. A poor caravan sits at `budgetFactor ≈ 0.1` → only the single best category clears the gate → buys one thing.
  14: 4. **"Further = more money" already exists, latent.** Prices are pure local supply/demand (`DefaultTradeItemPriceFactorModel`, up to 10× base at undersupplied towns) with zero distance term. Distant towns are *already* more profitable — vanilla just vetoes reaching them. So the fix is to **lift the vetoes and re-weight selection**, not fabricate gold.
  15: 
  16: ## Architecture
  17: 
  18: Mirrors the `ArmyTargeting` precedent (which solves the identical "AI thrashes between close targets + distance-decays away far ones" for besieger armies): thin Harmony postfixes → pure `ICaravanTradeService` → validating config provider + MCM-over-JSON settings. Every service method short-circuits to the vanilla value when the master toggle is off (or when it's a player caravan and `ApplyToPlayerCaravans` is off), so **master-off restores exact vanilla behavior**. No new GameModel file — the diversity overrides live on the already-owned `TaomCaravanModel`.
  19: 
  20: ### The four levers
  21: 
  22: | # | Lever | Engine seam (all private) | Mechanism |
  23: |---|-------|---------------------------|-----------|
  24: | 1 | **War gate** (highest impact) | `CanTradeWith(IFaction, IFaction)` postfix (`ref bool __result`) | Flips a war-caused `false → true` per `WarTradePolicy`. Guards: only when `IsAtWarWith` (a peacetime false is the player's prohibited-kingdom exclusion — respected); the player's `_prohibitedKingdomsForPlayerCaravans` list is honored even during war (cached reflection). Policy resolves via `IAlignmentService.GetKingdomSide` (Free/Evil/Neutral). |
  25: | 2 | **Range re-weight + anti-shuttle** | `GetTradeScoreForTown(...)` postfix (`ref float __result`) | Recomputes raw travel days from the same public inputs vanilla used (`AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` + caravan-speed props), strips vanilla's `1/days` spike and re-applies `1/(nearFieldFlatten+days)^decayExponent` clamped by `maxCompensation`; cuts the score of the town just left (`LastVisitedSettlement`, not home). Selection-only; profit + payout untouched. Naval + home pass through. |
  26: | 3 | **Range envelope** | `CacheVeryFarDistances()` postfix (`__instance` + cached `FieldRef`) | Scales the two private `_*VeryFarCache` fields by `RangeMultiplier`. Once per session (no hot-path cost); coherently widens the Close/Med/Far bands + `distanceCut` veto. Field-rename drift → no-op. |
  27: | 4 | **Basket diversity** | `CalculateBudgetFactor(MobileParty)` postfix + `TaomCaravanModel` overrides | Floors the vanilla `budgetFactor` to `BudgetFactorFloor` so even poor caravans clear the `< 7f` gate on several categories. `TaomCaravanModel.GetInitialTradeGold` raises the starting-gold floor (never lowers vanilla's large/main-hero bonus); `GetMaxGoldToSpendOnOneItemCategory` is exposed for tuning (default = vanilla). |
  28: 
  29: ### "Further = more money" — emergent, not injected
  30: 
  31: Levers 1–3 let caravans reach the undersupplied far / same-alignment towns vanilla already prices up to 10× — real market arbitrage, which flows to the owner through the existing `ClanFinance` 10%-of-surplus daily drip. **No `TaomClanFinanceModel` change, no injected gold, no `SyncData`.** The feature is fully **save-clean**: toggles apply to existing saves immediately, and master-off leaves no residue.
  32: 
  33: ### Data flow
  34: 
  35: `caravan_trade_config.json` → `CaravanTradeConfigProvider` (validate-and-fall-back) → `CaravanTradeSettingsProvider` (MCM-over-JSON merge) → `CaravanTradeService` (pure decisions) ← the 4 hooks + `TaomCaravanModel`. War policy additionally consults `IAlignmentService` (Execution feature) — resolving `GetKingdomSide` directly and branching on `FactionSide.Neutral`, **not** `AreEnemyAlignments` (whose Neutral-as-enemy-of-everyone semantics are inverted for this purpose — see RCA below).
  36: 
  37: ## Configuration
  38: 
  39: `Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json` (singleton-cached — edits need an app restart). Validated field-by-field; invalid values revert to the shipped default with a logged warning. MCM group **"Caravan Trade"** exposes the headline knobs (which override the matching JSON fields at runtime); the curve internals stay JSON-only.
  40: 
  41: | Field | Default | Range | MCM? | Meaning |
  42: |-------|---------|-------|------|---------|
  43: | `enabled` | `true` | — | ✅ master | Off = exact vanilla. |
  44: | `applyToPlayerCaravans` | `true` | — | ✅ | Scope all levers off player caravans when false. |
  45: | `rangeMultiplier` | `1.6` | [1, 4] | ✅ | Scale of the vanilla "very far" ceiling. |
  46: | `distanceDecayExponent` | `0.5` | [0.25, 4] | JSON | Curve alpha; lower = ranges further. |
  47: | `nearFieldFlattenDays` | `2.0` | [0, 20] | JSON | Ties near towns so profit decides. |
  48: | `maxCompensation` | `6.0` | [1, 20] | JSON | Clamp so one far town can't pull caravans map-wide. |
  49: | `antiShuttlePenalty` | `0.35` | [0, 1] | JSON | Score cut on the town just left. |
  50: | `warTradePolicy` | `SameAlignmentAndNeutral` | enum | ✅ dropdown | `None` (vanilla) / `IgnoreWar` / `SameAlignmentAndNeutral`. |
  51: | `budgetFactorFloor` | `0.35` | [0, 1] | ✅ | Fuller baskets for poor caravans. |
  52: | `initialTradeGold` | `15000` | [1000, 100000] | JSON | Starting-gold floor. |
  53: | `maxGoldPerCategory` | `1500` | [100, 20000] | JSON | Per-category gold cap (default = vanilla). |
  54: 
  55: **War policy default (`SameAlignmentAndNeutral`):** a Free caravan trades at any Free or Neutral town despite the war, but not Evil towns (and vice-versa); Neutral factions (Umbar, etc.) trade with anyone. Lore-coherent and still hugely widens the reachable set.
  56: 
  57: ## Key Files
  58: 
  59: | File | Purpose |
  60: |------|---------|
  61: | `Main/Features/CaravanTrade/ICaravanTradeService.cs` | Pure decision surface + `WarTradePolicy` enum. |
  62: | `Main/Features/CaravanTrade/CaravanTradeService.cs` | All logic (reweight, war policy, budget floor, gold resolution). TaleWorlds-free. |
  63: | `Main/Features/CaravanTrade/CaravanTradeConfig.cs` | JSON DTO + `WarTradePolicyParser` (known-set validation). |
  64: | `Main/Features/CaravanTrade/CaravanTradeConfigProvider.cs` | Load + field-by-field validation. |
  65: | `Main/Features/CaravanTrade/CaravanTradeSettingsProvider.cs` | MCM-over-JSON merge; dropdown-index → enum. |
  66: | `Main/Features/CaravanTrade/CaravanTradeIoC.cs` | 3 singleton registrations. |
  67: | `Main/Features/CaravanTrade/Hooks/*.cs` | The 4 postfixes (Patch59_CaravanTrade). |
  68: | `Main/Features/CulturalFeats/Models/TaomCaravanModel.cs` | +2 diversity overrides (existing forming-cost override kept). |
  69: | `Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json` | Config + inline docs. |
  70: 
  71: Wiring: `Main/IoC.cs` (registration), `Main/SubModule.cs` (`Patch59_CaravanTrade` in the campaign-phase block + the `TaomCaravanModel` ctor injection), `Main/Features/TaomSettings.cs` (MCM group).
  72: 
  73: ## Dependencies
  74: 
  75: - **Execution feature** — `IAlignmentService` + `execution/alignment.json` for the war-policy side resolution.
  76: - **CulturalFeats** — owns `TaomCaravanModel` (single GameModel owner for `DefaultCaravanModel`).
  77: - **MCM** (`TaomSettings`), **DryIoc**, `TAOM.Core.Validation.FiniteFloatValidator`, `TAOM.Core.Logging`, `TAOM.Core.Infrastructure.IPathService`.
  78: 
  79: ## Tests
  80: 
  81: - `TAOM.Tests/Features/CaravanTrade/CaravanTradeServiceTests.cs` — every lever + the war-policy matrix (same-side / opposite-side / **Neutral-on-each-side** regression) + NaN/disabled/player-scope gates.
  82: - `TAOM.Tests/Features/CaravanTrade/CaravanTradeConfigProviderTests.cs` — one test per validation rule, incl. the `warTradePolicy` M1 typo-trap.
  83: - `TAOM.Tests/Features/CaravanTrade/CaravanTradeBindingTests.cs` — `[BindingVerification]` drift-guards for the 4 private methods, the 2 `FieldRef` targets, the `AiHelper` helper, and the `DefaultCaravanModel` override targets (all pass against installed v1.4.6). The 4 postfixes also auto-enroll in `HarmonyPatchBindingTests`.
  84: 
  85: ## How-To
  86: 
  87: - **Retune ranging:** lower `distanceDecayExponent` or raise `rangeMultiplier` (MCM) for more aggressive spreading; raise `nearFieldFlattenDays` to make profit dominate more among near towns.
  88: - **Change war behavior:** MCM "War Trade Policy" dropdown, or the JSON `warTradePolicy` string.
  89: - **Fuller/leaner baskets:** raise/lower `budgetFactorFloor` (MCM) and `initialTradeGold` (JSON).
  90: - **Revert to vanilla:** MCM master toggle off — exact vanilla, existing saves included.
  91: 
  92: ## Performance
  93: 
  94: All 4 hooks lazy-cache their `IoC.Resolve` (`??=`) and their reflection (`FieldInfo` / `FieldRefAccess`). `GetTradeScoreForTown` runs in the destination argmax loop (per caravan, on re-think — infrequent, not per-frame) and recomputes the distance via `AiHelper`; this was reviewed and **verified cache-backed** — `DefaultMapDistanceModel.GetDistance(MobileParty, Settlement)` serves from the precomputed settlement distance cache (`_navigationCache`), not a live navmesh pathfind, so the recompute is cheap and terrain-accurate (a straight-line proxy was rejected because it would ignore the LOTR map's mountains/water). `CacheVeryFarDistances` fires once per session.
  95: 
  96: ## Known limitations / playtest items
  97: 
  98: - **Home rubber-band (design note):** home is exempt from the distance re-weight while non-home near towns are scaled down, which could bias caravans toward returning home more than intended. Deliberate (vanilla's home-return `num5` pull must not be disturbed); flagged for playtest. If too frequent, apply a mild reweight to home or add a home-pull knob.
  99: - **Naval caravans unchanged:** the shuttle is a land problem; naval caravans pass through vanilla (naval travel is parked in TAOM anyway, #296).
 100: - **Category-count cap:** the vanilla top-5/top-10 category *breadth* cap is unchanged in v1; the budget-floor + initial-gold levers make more of those slots fill, which is the primary "one item" fix. Raising the count itself would need a `BuyGoods` transpiler (deferred).
 101: 
 102: ## References
 103: 
 104: - Deep-review RCA: `docs/reviews/rca-caravan-trade-2026-07-04.md` (HIGH war-gate Neutral-inversion caught + fixed).
 105: - Engine background: `docs/reference/engine/settlement-economy-food-prosperity.md` §Caravans.

 succeeded in 690ms:
TAOM.Tests\Features\CaravanTrade\CaravanTradeBindingTests.cs:78: Assert.IsNotNull(AccessTools.Method(t, "GetMaxGoldToSpendOnOneItemCategory"),
TAOM.Tests\Features\CaravanTrade\CaravanTradeBindingTests.cs:79: "DefaultCaravanModel.GetMaxGoldToSpendOnOneItemCategory did not resolve — TaomCaravanModel's diversity override target is gone.");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:54: ""warTradePolicy"": ""IgnoreWar"",
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:69: Assert.AreEqual("IgnoreWar", c.WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:82: Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:106: Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:187: WriteConfig(@"{ ""warTradePolicy"": ""IgnoreWarr"" }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:188: Assert.AreEqual("SameAlignmentAndNeutral", _sut.GetConfig().WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:195: WriteConfig(@"{ ""warTradePolicy"": ""ignorewar"" }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:196: Assert.AreEqual("ignorewar", _sut.GetConfig().WarTradePolicy);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:227: WriteConfig(@"{ ""rangeMultiplier"": 1.6, ""warTradePolicy"": ""None"" }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:32: _settings.WarTradePolicy.Returns(WarTradePolicy.SameAlignmentAndNeutral);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:163: Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "rohan", false));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:170: Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "rohan", isPlayerCaravan: true));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:174: public void AllowWartimeTrade_PolicyNone_ReturnsFalse()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:176: _settings.WarTradePolicy.Returns(WarTradePolicy.None);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:177: Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "rohan", false));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:181: public void AllowWartimeTrade_PolicyIgnoreWar_ReturnsTrue()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:183: _settings.WarTradePolicy.Returns(WarTradePolicy.IgnoreWar);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:192: _alignment.GetKingdomSide("rohan").Returns(FactionSide.Free);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:193: Assert.IsTrue(_sut.AllowWartimeTrade("gondor", "rohan", false));

 succeeded in 1296ms:
Main\Adapters\ItemPoolAdapter.cs:29: ("rohan_",        "vlandia"),
Main\Adapters\ItemPoolAdapter.cs:43: ("cts_rohan_",    "vlandia"),
Main\Features\ArmyTargeting\Models\TaomTargetScoreModel.cs:26: // (Rohan), and empire (Dunland) all share culture "empire" so culture cannot distinguish
Main\Features\BattleBalance\BattleBalanceConfig.cs:32: ["mordor"] = -0.2f,   ["gundabad"] = -0.1f,  ["dolguldur"] = -0.1f
Main\Features\CaravanTrade\CaravanTradeConfig.cs:4: /// JSON DTO for <c>caravan_trade/caravan_trade_config.json</c>. Defaults are the shipped tuning.
Main\Features\CaravanTrade\CaravanTradeConfig.cs:21: public string WarTradePolicy { get; set; } = "SameAlignmentAndNeutral";
Main\Features\CaravanTrade\CaravanTradeConfig.cs:29: /// <summary>Parsing + known-set validation for the <see cref="WarTradePolicy"/> config string (the M1 typo trap).</summary>
Main\Features\CaravanTrade\CaravanTradeConfig.cs:30: public static class WarTradePolicyParser
Main\Features\CaravanTrade\CaravanTradeConfig.cs:32: /// <summary>Case-insensitively parse a config string into a <see cref="WarTradePolicy"/>. Returns false for null/empty/unknown.</summary>
Main\Features\CaravanTrade\CaravanTradeConfig.cs:33: public static bool TryParse(string value, out WarTradePolicy policy)
Main\Features\CaravanTrade\CaravanTradeConfig.cs:35: policy = WarTradePolicy.SameAlignmentAndNeutral;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:42: policy = WarTradePolicy.None;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:44: case "ignorewar":
Main\Features\CaravanTrade\CaravanTradeConfig.cs:45: policy = WarTradePolicy.IgnoreWar;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:47: case "samealignmentandneutral":
Main\Features\CaravanTrade\CaravanTradeConfig.cs:48: policy = WarTradePolicy.SameAlignmentAndNeutral;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:11: /// Loads + validates <c>caravan_trade/caravan_trade_config.json</c>. Every numeric field is
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:34: var path = Path.Combine(_pathService.ModuleDataPath, "caravan_trade", "caravan_trade_config.json");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:38: _logger.LogWarning($"CaravanTradeConfigProvider: caravan_trade_config.json not found at {path}, using defaults");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:50: _logger.LogError($"CaravanTradeConfigProvider: Failed to parse caravan_trade_config.json: {ex.Message}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:69: WarTradePolicy = parsed.WarTradePolicy,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:117: if (!WarTradePolicyParser.TryParse(c.WarTradePolicy, out _))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:119: _logger.LogWarning($"CaravanTradeConfigProvider: warTradePolicy='{c.WarTradePolicy}' is not one of None/IgnoreWar/SameAlignmentAndNeutral, reverting to {defaults.WarTradePolicy}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:120: c.WarTradePolicy = defaults.WarTradePolicy;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:146: _logger.LogWarning("CaravanTradeConfigProvider: caravan_trade_config.json contained invalid values. See prior warnings for details.");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:148: _logger.LogInfo("CaravanTradeConfigProvider: Loaded caravan_trade_config.json");
Main\Features\CaravanTrade\CaravanTradeService.cs:77: switch (_settings.WarTradePolicy)
Main\Features\CaravanTrade\CaravanTradeService.cs:79: case WarTradePolicy.IgnoreWar:
Main\Features\CaravanTrade\CaravanTradeService.cs:81: case WarTradePolicy.SameAlignmentAndNeutral:
Main\Features\CaravanTrade\CaravanTradeService.cs:92: case WarTradePolicy.None:
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:31: public WarTradePolicy WarTradePolicy => ResolveWarPolicy();
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:38: private WarTradePolicy ResolveWarPolicy()
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:40: var dropdown = TaomSettings.Instance?.CaravanWarTradePolicy;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:45: case 0: return WarTradePolicy.None;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:46: case 1: return WarTradePolicy.SameAlignmentAndNeutral;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:47: case 2: return WarTradePolicy.IgnoreWar;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:52: return WarTradePolicyParser.TryParse(Cfg.WarTradePolicy, out var policy)
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:54: : WarTradePolicy.SameAlignmentAndNeutral;
Main\Features\CaravanTrade\ICaravanTradeConfigProvider.cs:3: /// <summary>Loads + validates <c>caravan_trade/caravan_trade_config.json</c>. Cached for the process (Reuse.Singleton).</summary>
Main\Features\CaravanTrade\ICaravanTradeService.cs:9: public enum WarTradePolicy
Main\Features\CaravanTrade\ICaravanTradeService.cs:15: IgnoreWar,
Main\Features\CaravanTrade\ICaravanTradeService.cs:22: SameAlignmentAndNeutral,
Main\Features\CaravanTrade\ICaravanTradeService.cs:60: /// to allow trade despite the war, per the configured <see cref="WarTradePolicy"/>.
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:34: WarTradePolicy WarTradePolicy { get; }
Main\Features\CareerSystem\CareerSystemIoC.cs:98: // ═══ ROHAN ═══
Main\Features\CulturalFeats\Models\TaomNotableSpawnModel.cs:12: /// concentration to keep their recruitment competitive with Rohan's distributed map.
Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:39: _feats.ApplyRohanInfantryPenalty(culture, mountedCount, totalCount, ref result);
Main\Features\CulturalFeats\Models\TaomPartySpeedModel.cs:67: /// two primitives <see cref="ICulturalFeatsService.ApplyRohanInfantryPenalty"/>
Main\Features\CulturalFeats\CulturalFeatsService.cs:67: if (culture.HasFeat(TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:68: multiplier += TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat.EffectBonus;
Main\Features\CulturalFeats\CulturalFeatsService.cs:113: ApplyIfHas(culture, TaomCulturalFeats.RohanPlainSpeedFeat, ref result);
Main\Features\CulturalFeats\CulturalFeatsService.cs:134: public void ApplyRohanInfantryPenalty(
Main\Features\CulturalFeats\CulturalFeatsService.cs:139: if (!culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:142: result.AddFactor(TaomCulturalFeats.RohanInfantrySpeedFeat.EffectBonus, CultureText);
Main\Features\CulturalFeats\CulturalFeatsService.cs:170: if (culture.HasFeat(TaomCulturalFeats.DolGuldurMilitiaProductionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:171: result.Add(TaomCulturalFeats.DolGuldurMilitiaProductionFeat.EffectBonus);
Main\Features\CulturalFeats\CulturalFeatsService.cs:184: if (culture.HasFeat(TaomCulturalFeats.DolGuldurConstructionSpeedFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:185: result.AddFactor(TaomCulturalFeats.DolGuldurConstructionSpeedFeat.EffectBonus, CultureText);
Main\Features\CulturalFeats\CulturalFeatsService.cs:239: if (culture.HasFeat(TaomCulturalFeats.RohanMountedCostFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:240: result.AddFactor(TaomCulturalFeats.RohanMountedCostFeat.EffectBonus, CultureText);
Main\Features\CulturalFeats\CulturalFeatsService.cs:257: if (culture.HasFeat(TaomCulturalFeats.DolGuldurPartySizeFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:258: result.AddFactor(TaomCulturalFeats.DolGuldurPartySizeFeat.EffectBonus, CultureText);
Main\Features\CulturalFeats\CulturalFeatsService.cs:285: if (culture.HasFeat(TaomCulturalFeats.DolGuldurVolunteerRateFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:286: result.AddFactor(TaomCulturalFeats.DolGuldurVolunteerRateFeat.EffectBonus, CultureText);
Main\Features\CulturalFeats\CulturalFeatsService.cs:310: if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownMerchantFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:311: add += (int)TaomCulturalFeats.DolGuldurNotableCountTownMerchantFeat.EffectBonus;
Main\Features\CulturalFeats\CulturalFeatsService.cs:319: if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownArtisanFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:320: add += (int)TaomCulturalFeats.DolGuldurNotableCountTownArtisanFeat.EffectBonus;
Main\Features\CulturalFeats\CulturalFeatsService.cs:330: if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountTownGangLeaderFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:331: add += (int)TaomCulturalFeats.DolGuldurNotableCountTownGangLeaderFeat.EffectBonus;
Main\Features\CulturalFeats\CulturalFeatsService.cs:348: if (culture.HasFeat(TaomCulturalFeats.DolGuldurNotableCountVillageFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:349: multiplier += TaomCulturalFeats.DolGuldurNotableCountVillageFeat.EffectBonus;
Main\Features\CulturalFeats\CulturalFeatsService.cs:375: if (culture.HasFeat(TaomCulturalFeats.DolGuldurFoodConsumptionFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:376: result.AddFactor(TaomCulturalFeats.DolGuldurFoodConsumptionFeat.EffectBonus, CultureText);
Main\Features\CulturalFeats\CulturalFeatsService.cs:403: if (culture.HasFeat(TaomCulturalFeats.RohanLoyaltyFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:404: result.Add(TaomCulturalFeats.RohanLoyaltyFeat.EffectBonus, CultureText);
Main\Features\CulturalFeats\CulturalFeatsService.cs:419: if (culture.HasFeat(TaomCulturalFeats.RohanMoraleFeat))
Main\Features\CulturalFeats\CulturalFeatsService.cs:420: result.Add(TaomCulturalFeats.RohanMoraleFeat.EffectBonus, CultureText);
Main\Features\CulturalFeats\ICulturalFeatsService.cs:39: /// <summary>Applies the Rohan infantry-speed penalty when &gt;50% of the party is infantry.</summary>
Main\Features\CulturalFeats\ICulturalFeatsService.cs:40: void ApplyRohanInfantryPenalty(ICultureFeatAdapter? culture, int mountedCount, int totalCount, ref ExplainedNumber result);
Main\Features\CulturalFeats\ICulturalFeatsService.cs:67: /// <summary>Applies the Isengard/Rohan mounted-upgrade-cost factors. No-op when troop is not mounted.</summary>
Main\Features\CulturalFeats\ICulturalFeatsService.cs:97: /// <summary>Applies Gondor/Erebor/Lothlorien/Rivendell/Rohan loyalty bonuses (Add — not AddFactor).</summary>
Main\Features\CulturalFeats\ICulturalFeatsService.cs:101: /// <summary>Applies Gondor/Rohan/Erebor/Mirkwood/Lothlorien morale bonuses (Add).</summary>
Main\Features\CulturalFeats\TaomCulturalFeats.cs:106: private FeatObject _dolguldurArmyInfluenceCost;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:107: private FeatObject _dolguldurMilitiaProduction;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:108: private FeatObject _dolguldurConstructionSpeed;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:109: private FeatObject _dolguldurPartySize;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:110: private FeatObject _dolguldurFoodConsumption;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:111: private FeatObject _dolguldurVolunteerRate;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:113: // notables, Gang-Leader-heavy to match Isengard's recruitment competitiveness vs Rohan.
Main\Features\CulturalFeats\TaomCulturalFeats.cs:114: private FeatObject _dolguldurNotableCountTownMerchant;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:115: private FeatObject _dolguldurNotableCountTownArtisan;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:116: private FeatObject _dolguldurNotableCountTownGangLeader;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:117: private FeatObject _dolguldurNotableCountVillage;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:143: // Rohan (XSLT culture — custom C# feats)
Main\Features\CulturalFeats\TaomCulturalFeats.cs:144: private FeatObject _rohanMountedCost;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:145: private FeatObject _rohanMountedWage;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:146: private FeatObject _rohanInfantrySpeed;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:147: private FeatObject _rohanLoyalty;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:148: private FeatObject _rohanMorale;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:149: private FeatObject _rohanPlainSpeed;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:276: public static FeatObject DolGuldurArmyInfluenceCostFeat => Instance._dolguldurArmyInfluenceCost;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:277: public static FeatObject DolGuldurMilitiaProductionFeat => Instance._dolguldurMilitiaProduction;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:278: public static FeatObject DolGuldurConstructionSpeedFeat => Instance._dolguldurConstructionSpeed;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:279: public static FeatObject DolGuldurPartySizeFeat => Instance._dolguldurPartySize;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:280: public static FeatObject DolGuldurFoodConsumptionFeat => Instance._dolguldurFoodConsumption;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:281: public static FeatObject DolGuldurVolunteerRateFeat => Instance._dolguldurVolunteerRate;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:282: public static FeatObject DolGuldurNotableCountTownMerchantFeat => Instance._dolguldurNotableCountTownMerchant;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:283: public static FeatObject DolGuldurNotableCountTownArtisanFeat => Instance._dolguldurNotableCountTownArtisan;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:284: public static FeatObject DolGuldurNotableCountTownGangLeaderFeat => Instance._dolguldurNotableCountTownGangLeader;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:285: public static FeatObject DolGuldurNotableCountVillageFeat => Instance._dolguldurNotableCountVillage;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:310: // Rohan
Main\Features\CulturalFeats\TaomCulturalFeats.cs:311: public static FeatObject RohanMountedCostFeat => Instance._rohanMountedCost;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:312: public static FeatObject RohanMountedWageFeat => Instance._rohanMountedWage;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:313: public static FeatObject RohanInfantrySpeedFeat => Instance._rohanInfantrySpeed;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:314: public static FeatObject RohanLoyaltyFeat => Instance._rohanLoyalty;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:315: public static FeatObject RohanMoraleFeat => Instance._rohanMorale;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:316: public static FeatObject RohanPlainSpeedFeat => Instance._rohanPlainSpeed;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:446: _dolguldurArmyInfluenceCost = Register("taom_dolguldur_army_influence_cost");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:447: _dolguldurMilitiaProduction = Register("taom_dolguldur_militia_production");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:448: _dolguldurConstructionSpeed = Register("taom_dolguldur_construction_speed");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:449: _dolguldurPartySize = Register("taom_dolguldur_party_size");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:450: _dolguldurFoodConsumption = Register("taom_dolguldur_food_consumption");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:451: _dolguldurVolunteerRate = Register("taom_dolguldur_volunteer_rate");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:452: _dolguldurNotableCountTownMerchant = Register("taom_dolguldur_notable_count_town_merchant");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:453: _dolguldurNotableCountTownArtisan = Register("taom_dolguldur_notable_count_town_artisan");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:454: _dolguldurNotableCountTownGangLeader = Register("taom_dolguldur_notable_count_town_gang_leader");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:455: _dolguldurNotableCountVillage = Register("taom_dolguldur_notable_count_village");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:478: _rohanMountedCost = Register("taom_rohan_mounted_cost");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:479: _rohanMountedWage = Register("taom_rohan_mounted_wage");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:480: _rohanInfantrySpeed = Register("taom_rohan_infantry_speed");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:481: _rohanLoyalty = Register("taom_rohan_loyalty");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:482: _rohanMorale = Register("taom_rohan_morale");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:483: _rohanPlainSpeed = Register("taom_rohan_plain_speed");
Main\Features\CulturalFeats\TaomCulturalFeats.cs:813: _dolguldurArmyInfluenceCost.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:817: _dolguldurMilitiaProduction.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:821: _dolguldurConstructionSpeed.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:825: _dolguldurPartySize.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:829: _dolguldurFoodConsumption.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:833: _dolguldurVolunteerRate.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:838: _dolguldurNotableCountTownMerchant.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:842: _dolguldurNotableCountTownArtisan.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:846: _dolguldurNotableCountTownGangLeader.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:850: _dolguldurNotableCountVillage.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:936: // Rohan — Horse-lords: cheap mounted troops, slow without cavalry
Main\Features\CulturalFeats\TaomCulturalFeats.cs:937: _rohanMountedCost.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:941: _rohanMountedWage.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:945: _rohanInfantrySpeed.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:949: _rohanLoyalty.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:953: _rohanMorale.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:957: _rohanPlainSpeed.Initialize(
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1151: yield return _instance._dolguldurArmyInfluenceCost;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1152: yield return _instance._dolguldurMilitiaProduction;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1153: yield return _instance._dolguldurConstructionSpeed;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1154: yield return _instance._dolguldurPartySize;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1155: yield return _instance._dolguldurFoodConsumption;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1156: yield return _instance._dolguldurVolunteerRate;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1157: yield return _instance._dolguldurNotableCountTownMerchant;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1158: yield return _instance._dolguldurNotableCountTownArtisan;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1159: yield return _instance._dolguldurNotableCountTownGangLeader;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1160: yield return _instance._dolguldurNotableCountVillage;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1180: yield return _instance._rohanMountedCost;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1181: yield return _instance._rohanMountedWage;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1182: yield return _instance._rohanInfantrySpeed;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1183: yield return _instance._rohanLoyalty;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1184: yield return _instance._rohanMorale;
Main\Features\CulturalFeats\TaomCulturalFeats.cs:1185: yield return _instance._rohanPlainSpeed;
Main\Features\CultureMarketplace\CultureItemPoolService.cs:12: // Codex review 2026-05-20 (C2): LOTRAOM_horses.xml lines 231-330 declare Rohan
Main\Features\CultureMarketplace\CultureItemPoolService.cs:13: // harnesses with culture="Culture.rohan" — but `rohan` is NOT a valid TAOM culture
Main\Features\CultureMarketplace\CultureItemPoolService.cs:14: // ID (Rohan uses `vlandia` per CLAUDE.md cheatsheet). Without normalization those
Main\Features\CultureMarketplace\CultureItemPoolService.cs:15: // items group into a `rohan` pool no town ever queries.
Main\Features\CultureMarketplace\CultureItemPoolService.cs:19: ["rohan"] = "vlandia",
Main\Features\CultureMarketplace\CultureItemPoolService.cs:65: // entries that collide post-alias (e.g., `cultures="rohan,vlandia"` — both alias
Main\Features\CustomBattles\Config\CustomBattleCommandersProvider.cs:30: "mirkwood", "isengard", "gundabad", "dolguldur", "umbar",
Main\Features\EliteEmissary\EliteEmissaryConfigProvider.cs:28: "isengard", "gundabad", "dolguldur", "umbar", "goblin", "mistymountainorcs",
Main\Features\FactionMap\CultureSettingService.cs:60: // culture id. TAOM repurposes vlandia as Rohan, so replace that placeholder with a
Main\Features\FactionMap\LandmarkService.cs:29: new LandmarkDef { Id = "dol_guldur_fortress", Name = "Dol Guldur", Description = "The Fortress of the Necromancer", Type = LandmarkType.Capital, X = 870, Y = 510, FactionId = 17 },
Main\Features\NazgulFamily\Models\TaomMarriageModel.cs:13: /// initial child generation already excludes both wraith cultures (<c>mordor</c> + <c>dolguldur</c>).
Main\Features\ShaderPrecompilation\IShaderPrecompileCrashGuard.cs:6: // GPU/driver-specific native AV (e.g. `taom_rohan_battle_fords_of_isen_forceatmo` AV'd one user's GPU
Main\Features\ShaderPrecompilation\PrecompileSceneProvider.cs:40: // Rohan field-battle scenes DISABLED 2026-06-19 (pbr_terrain input-layout-9 GPU crash; also removed
Main\Features\ShaderPrecompilation\PrecompileSceneProvider.cs:42: // "taom_rohan_battle_001_forceatmo",
Main\Features\ShaderPrecompilation\PrecompileSceneProvider.cs:43: // "taom_rohan_battle_fords_of_isen_forceatmo",
Main\Features\ShaderPrecompilation\PrecompileSceneProvider.cs:53: // "taom_rohan_castle_helms_deep_forceatmo",  // DISABLED 2026-06-19: same Rohan pbr_terrain input-layout-9 crash class
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:52: // inline. Behavior shift: a Rohan hero leading a Gundabad-owned party now pays Rohan wage
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:57: float rohanMountedWageBonus = ResolveRohanMountedWageBonus(partyCulture);
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:58: float mountedWageShare = ComputeMountedWageShare(rohanMountedWageBonus, result.BaseNumber, troopRoster);
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:61: ref result, garrisonInputs, partyInputs, rohanMountedWageBonus, mountedWageShare, CultureText);
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:115: private static float ResolveRohanMountedWageBonus(CultureObject? partyCulture)
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:116: => BonusIfHas(partyCulture, TaomCulturalFeats.RohanMountedWageFeat);
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:118: private float ComputeMountedWageShare(float rohanMountedWageBonus, float baseWageTotal, TroopRoster troopRoster)
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:120: if (rohanMountedWageBonus == 0f || troopRoster == null || baseWageTotal <= 0f)
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:139: rohanMountedCostBonus: BonusIfHas(buyerCulture, TaomCulturalFeats.RohanMountedCostFeat));
Main\Features\TroopProgression\Models\TaomPartyWageModel.cs:145: // intentionally NOT resolved here — TAOM replaces it with the Isengard/Rohan mounted-cost feats.
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:15: private static readonly (string, int)[] DolGuldurSettlementPool =
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:25: private static readonly (string, int)[] DolGuldurClanPool =
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:36: private static void InitializeDolGuldurSettlements()
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:41: // absent from the clan-path pool (InitializeDolGuldurClans) to keep that source clean.
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:48: AddSettlement("town_DG1",   DolGuldurSettlementPool);
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:49: AddSettlement("castle_DG1", DolGuldurSettlementPool);
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:50: AddSettlement("castle_DG2", DolGuldurSettlementPool);
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:51: AddSettlement("castle_DG3", DolGuldurSettlementPool);
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:56: private static void InitializeDolGuldurClans()
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:60: // clan-army recruit — see InitializeDolGuldurSettlements).
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:62: AddClan($"clan_dolguldur_{i}", DolGuldurClanPool);
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:67: private static void InitializeDolGuldurCulture()
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.DolGuldur.cs:69: CultureMap["dolguldur"] = new List<VolunteerChance>
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:5: // Volunteer recruitment pools for Rohan (clan pools + culture fallback) — the data half of
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:12: // --- Rohan Clan Pools ---
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:13: // Every Rohan (Culture.vlandia) clan recruits all 7 Rohan basic troops (is_basic_troop=true)
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:14: // at equal weight 1, so the player can recruit the full Rohan T2 lineup from any settlement
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:15: // bound to a Rohan clan regardless of region (Wold, Westemnet, Eastemnet, Eastfold, Westfold,
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:20: // future per-settlement Rohan pool would still win. Without a per-settlement entry, the lookup
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:22: private static void InitializeRohanClans()
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:26: ("rohan_wold_recruit",        1),
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:27: ("rohan_westemnet_recruit",   1),
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:28: ("rohan_eastemnet_recruit",   1),
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:29: ("rohan_eastfold_recruit",    1),
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:30: ("rohan_westfold_recruit",    1),
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:31: ("rohan_westmarches_recruit", 1),
Main\Features\TroopProgression\RecruitmentPools\VolunteerRecruitmentService.Rohan.cs:32: ("rohan_edoras_recruit",      1),

codex
There is a second scoping edge worth checking before I settle the review: the war-gate hook uses vanilla’s `caravanFaction == Hero.MainHero.MapFaction` idiom. I’m decompiling faction ownership to make sure that does not accidentally classify NPC caravans in the player’s kingdom as “player caravans” when the player-scope toggle is off.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -File -Filter Hero.cs | Select-Object -ExpandProperty FullName; Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -File -Filter Clan.cs | Select-Object -ExpandProperty FullName; Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -File -Filter MobileParty.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'public IFaction MapFaction|IFaction MapFaction|MapFaction =>' | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'ActualClan|PartyOwner|MapFaction' | Select-Object -First 160 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign -Recurse -File -Filter *.cs | Select-String -Pattern 'class Hero|public IFaction MapFaction|class Clan|class MobileParty' | Select-Object -First 160 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 692ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs

 succeeded in 970ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:216: public static IEnumerable<Army> GetKingdomArmies(IFaction mapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:28: IFaction mapFaction = mapEvent.MapEventSettlement.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:456: IFaction mapFaction = PlayerEncounter.EncounteredParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:519: IFaction mapFaction = PlayerEncounter.EncounteredParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:456: IFaction mapFaction = hero.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:464: IFaction mapFaction2 = item.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:214: public bool IsMapFaction => _kingdom == null;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:338: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:566: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\IFaction.cs:70: IFaction MapFaction { get; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:123: public bool IsMapFaction => true;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:262: public IFaction MapFaction => this;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeOwnerOfSettlementAction.cs:63: IFaction mapFaction = newOwner.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem\BarterData.cs:27: public IFaction OffererMapFaction => OffererHero?.MapFaction ?? OffererParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem\BarterData.cs:29: public IFaction OtherMapFaction => OtherHero?.MapFaction ?? OtherParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CampaignWarManagerBehavior.cs:21: IFaction mapFaction = raidEvent.AttackerSide.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CampaignWarManagerBehavior.cs:22: IFaction mapFaction2 = raidEvent.DefenderSide.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CampaignWarManagerBehavior.cs:47: IFaction mapFaction = mapEvent.AttackerSide.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CampaignWarManagerBehavior.cs:48: IFaction mapFaction2 = mapEvent.DefenderSide.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1752: IFaction mapFaction = MobileParty.ConversationParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:70: IFaction mapFaction = siegeEvent.BesiegerCamp.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:679: IFaction mapFaction = encounteredBattle.GetLeaderParty(BattleSideEnum.Defender).MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:828: IFaction mapFaction = encounteredBattle.GetLeaderParty(BattleSideEnum.Attacker).MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1318: IFaction mapFaction = Hero.MainHero.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1320: IFaction mapFaction2 = Settlement.CurrentSettlement.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:1773: IFaction mapFaction = PlayerEncounter.EncounterSettlement.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PartyDiplomaticHandlerCampaignBehavior.cs:269: IFaction mapFaction = MobileParty.MainParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerCaptivityCampaignBehavior.cs:78: IFaction mapFaction = Hero.MainHero.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PlayerCaptivityCampaignBehavior.cs:79: IFaction mapFaction2 = PlayerCaptivity.CaptorParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RebellionsCampaignBehavior.cs:278: IFaction mapFaction = settlement.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RecruitmentCampaignBehavior.cs:311: IFaction mapFaction = mobileParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RecruitmentCampaignBehavior.cs:571: IFaction mapFaction = settlement.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RomanceCampaignBehavior.cs:1209: IFaction mapFaction = Hero.OneToOneConversationHero.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RomanceCampaignBehavior.cs:1243: IFaction mapFaction = Hero.OneToOneConversationHero.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\TownSecurityCampaignBehavior.cs:51: private bool ValidCivilianPartyCondition(PartyBase party, MapEvent mapEvent, IFaction mapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\VassalAndMercenaryOfferCampaignBehavior.cs:326: IFaction mapFaction = Hero.OneToOneConversationHero.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\VassalAndMercenaryOfferCampaignBehavior.cs:435: IFaction mapFaction = Hero.OneToOneConversationHero.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\VillagerCampaignBehavior.cs:692: IFaction mapFaction = MobileParty.ConversationParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors\AiEngagePartyBehavior.cs:80: IFaction mapFaction = mobileParty2.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors\AiEngagePartyBehavior.cs:85: IFaction mapFaction2 = mobileParty2.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors\AiMilitaryBehavior.cs:120: IFaction mapFaction = mobilePartyOf.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors\AiMilitaryBehavior.cs:274: IFaction mapFaction = mobileParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors\AiVisitSettlementBehavior.cs:139: IFaction mapFaction = mobileParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors\AiVisitSettlementBehavior.cs:562: IFaction mapFaction = mobileParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors\DiplomaticBartersBehavior.cs:162: IFaction mapFaction = randomElement3.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\DefaultEncyclopediaHeroPage.cs:252: IFaction mapFaction = hero.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultClanFinanceModel.cs:543: IFaction mapFaction = clan.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultTargetScoreCalculatingModel.cs:126: IFaction mapFaction = mobileParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameMenus.GameMenuInitializationHandlers\DefaultEncounter.cs:303: IFaction mapFaction = PlayerEncounter.EncounteredMobileParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\PlayerBattleEndedLogEntry.cs:511: IFaction mapFaction = talkTroop.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Map\IMapPoint.cs:14: IFaction MapFaction { get; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.MapEvents\MapEventSide.cs:106: public IFaction MapFaction => _mapFaction ?? LeaderParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1079: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:2383: IFaction mapFaction = TargetParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:2391: IFaction mapFaction2 = TargetSettlement.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:236: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Hideout.cs:27: public override IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\RetirementSettlementComponent.cs:25: public override IFaction MapFaction => null;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:292: public IFaction MapFaction => SettlementComponent?.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\SettlementComponent.cs:61: public abstract IFaction MapFaction { get; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Town.cs:304: public override IFaction MapFaction => OwnerClan?.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Village.cs:124: public override IFaction MapFaction => Bound.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Siege\BesiegerCamp.cs:33: public IFaction MapFaction => _faction;

 succeeded in 823ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:77: return (color1: (uint)(((int?)character.HeroObject.MapFaction?.Color) ?? (-3357781)), color2: (uint)(((int?)character.HeroObject.MapFaction?.Color2) ?? (-3357781)));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:79: if (character.HeroObject.MapFaction == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:83: uint[] colors = character.HeroObject.MapFaction.Culture.StringId switch
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:93: return (color1: character.HeroObject.MapFaction.Color, color2: GetDeterministicColorFromListForHero(character.HeroObject, colors));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:95: int item = ((int?)partyBelongsTo?.MapFaction?.Color) ?? (-3357781);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:96: uint item2 = (uint)(((int?)partyBelongsTo?.MapFaction?.Color2) ?? (-3357781));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:356: if (PlayerEncounter.Current != null && PlayerEncounter.Current.PlayerSide == BattleSideEnum.Defender && (PlayerEncounter.EncounteredMobileParty == null || PlayerEncounter.EncounteredMobileParty.Ai.DoNotAttackMainPartyUntil.IsPast) && PlayerEncounter.EncounteredParty.Owner != null && FactionManager.IsAtWarAgainstFaction(PlayerEncounter.EncounteredParty.MapFaction, Hero.MainHero.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:360: if (Campaign.Current.CurrentConversationContext == ConversationContext.CapturedLord && character.IsHero && character.HeroObject.MapFaction == PlayerEncounter.EncounteredParty.MapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:488: if (Hero.MainHero.MapFaction != null && Hero.MainHero.MapFaction.Leader == Hero.MainHero && character.HeroObject.MapFaction == Hero.MainHero.MapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\CharacterHelper.cs:492: if (character.IsHero && character.HeroObject.MapFaction != null && character.HeroObject.MapFaction.IsKingdomFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DiplomacyHelper.cs:19: if (faction1 == Hero.MainHero.MapFaction && Hero.MainHero.MapFaction.Leader == Hero.MainHero)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DiplomacyHelper.cs:25: if (faction2 == Hero.MainHero.MapFaction && faction1.MainHeroCrimeRating > Campaign.Current.Models.CrimeModel.DeclareWarCrimeRatingThreshold)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DiplomacyHelper.cs:31: if (faction1 == Hero.MainHero.MapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DiplomacyHelper.cs:75: if (aliveLord.IsPrisoner && aliveLord.PartyBelongedToAsPrisoner?.MapFaction == capturerFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:177: float num = Campaign.Current.Settlements.Where((Settlement settlement) => (settlement.IsVillage || settlement.IsTown || settlement.IsCastle) && settlement.OwnerClan.Leader == Hero.MainHero).Sum((Settlement settlement) => settlement.GetSettlementValueForFaction(Hero.OneToOneConversationHero.MapFaction));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:179: float num3 = Clan.PlayerClan.Renown + (asVassal ? (num / 5000f) : 0f) + (asVassal ? ((float)Hero.MainHero.Gold / 10000f) : 0f) + TaleWorlds.Library.MathF.Min(num2, Clan.PlayerClan.Renown) / num2 * 0.2f * Clan.PlayerClan.CurrentTotalStrength + Hero.OneToOneConversationHero.MapFaction.Leader.GetRelationWithPlayer() * 2f;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:216: public static IEnumerable<Army> GetKingdomArmies(IFaction mapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:218: if (!mapFaction.IsKingdomFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:222: return ((Kingdom)mapFaction).Armies;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:294: if (noble.PartyBelongedTo != null && noble.PartyBelongedTo.MapEvent != null && ((noble.PartyBelongedTo.MapEvent.AttackerSide.LeaderParty == noble.PartyBelongedTo.Party && ((faction.IsKingdomFaction && noble.PartyBelongedTo.MapEvent.DefenderSide.LeaderParty.MapFaction == faction) || (!faction.IsKingdomFaction && noble.PartyBelongedTo.MapEvent.DefenderSide.LeaderParty.Owner != null && noble.PartyBelongedTo.MapEvent.DefenderSide.LeaderParty.Owner.Clan == faction))) || (noble.PartyBelongedTo.MapEvent.DefenderSide.LeaderParty == noble.PartyBelongedTo.Party && ((faction.IsKingdomFaction && noble.PartyBelongedTo.MapEvent.AttackerSide.LeaderParty.MapFaction == faction) || (!faction.IsKingdomFaction && noble.PartyBelongedTo.MapEvent.AttackerSide.LeaderParty.Owner != null && noble.PartyBelongedTo.MapEvent.AttackerSide.LeaderParty.Owner.Clan == faction)))))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:304: foreach (WarPartyComponent warPartyComponent in noble.PartyBelongedTo.MapEvent.MapEventSettlement.MapFaction.WarPartyComponents)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:327: if (partyBelongedTo.BesiegedSettlement != null && ((faction.IsKingdomFaction && partyBelongedTo.BesiegedSettlement.MapFaction == faction) || (!faction.IsKingdomFaction && partyBelongedTo.BesiegedSettlement.OwnerClan == faction)))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:329: foreach (WarPartyComponent warPartyComponent2 in partyBelongedTo.BesiegedSettlement.MapFaction.WarPartyComponents)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:340: if ((partyBelongedTo.DefaultBehavior == AiBehavior.RaidSettlement || partyBelongedTo.DefaultBehavior == AiBehavior.BesiegeSettlement || partyBelongedTo.DefaultBehavior == AiBehavior.AssaultSettlement) && ((faction.IsKingdomFaction && partyBelongedTo.TargetSettlement.MapFaction == faction) || (!faction.IsKingdomFaction && partyBelongedTo.TargetSettlement.OwnerClan == faction)))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:348: if (partyBelongedTo.ShortTermBehavior == AiBehavior.EngageParty && partyBelongedTo.ShortTermTargetParty != null && partyBelongedTo.ShortTermTargetParty.MapFaction == faction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:481: if (settlement.MapFaction != faction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:673: if (Clan.PlayerClan.MapFaction.IsAtWarWith(item) && item.CurrentTotalStrength > strengthThresholdForNonMutualWarsToBeIgnoredToJoinKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:699: if (Clan.PlayerClan.MapFaction.IsAtWarWith(item) && item.CurrentTotalStrength > strengthThresholdForNonMutualWarsToBeIgnoredToJoinKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:100: Hero.OneToOneConversationHero.MapFaction.Leader.SetTextVariables();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:108: if (suggestingBetrayal && Hero.OneToOneConversationHero.Clan == Hero.OneToOneConversationHero.MapFaction.Leader.Clan)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:111: textObject.SetTextVariable("FIRST_NAME", Hero.OneToOneConversationHero.MapFaction.Leader.FirstName);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:112: textObject.SetTextVariable("RELATIONSHIP", ConversationHelper.HeroRefersToHero(Hero.OneToOneConversationHero, Hero.OneToOneConversationHero.MapFaction.Leader, uppercaseFirst: true));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:137: if ((hero.MapFaction == null || hero.MapFaction.Leader != Hero.MainHero) && (!hero.IsNotable || hero.HomeSettlement.OwnerClan != Hero.MainHero.Clan))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:146: string text = hero.MapFaction.Culture.StringId;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:151: if (hero.MapFaction.IsKingdomFaction && hero.MapFaction.Leader == hero)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:254: if (partyBase.Owner != null && partyBase.LeaderHero != null && FactionManager.IsAtWarAgainstFaction(partyBase.MapFaction, Hero.MainHero.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:296: if (hero.MapFaction == otherHero.MapFaction && hero.CharacterObject.Culture == otherHero.CharacterObject.Culture && hero.Age > (float)middleAdultHoodAge && otherHero.Age > (float)middleAdultHoodAge && NPCPersonalityClashWithNPC(hero, otherHero) > 40)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:300: if (hero.MapFaction == otherHero.MapFaction && hero.CharacterObject.Culture == otherHero.CharacterObject.Culture && hero.Age > (float)middleAdultHoodAge && otherHero.Age > (float)middleAdultHoodAge)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:304: if (hero.MapFaction == otherHero.MapFaction && hero.CharacterObject.Culture == otherHero.CharacterObject.Culture)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:28: IFaction mapFaction = mapEvent.MapEventSettlement.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:29: raiderSide = (mapEvent.AttackerSide.LeaderParty.MapFaction.IsAtWarWith(mapFaction) ? BattleSideEnum.Attacker : BattleSideEnum.Defender);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MapEventHelper.cs:141: if (PlayerEncounter.Current != null && ((PlayerEncounter.EncounteredMobileParty != null && PlayerEncounter.EncounteredMobileParty.MapFaction != null && !PlayerEncounter.EncounteredMobileParty.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)) || (PlayerEncounter.EncounteredParty != null && PlayerEncounter.EncounteredParty.MapFaction != null && !PlayerEncounter.EncounteredParty.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:456: IFaction mapFaction = PlayerEncounter.EncounteredParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:457: if (mapFaction != null && mapFaction.NotAttackableByPlayerUntilTime.IsFuture)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:519: IFaction mapFaction = PlayerEncounter.EncounteredParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:520: if (mapFaction == null || mapFaction.NotAttackableByPlayerUntilTime.IsPast)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:600: PlayerEncounter.Finish(currentSettlement == null && (MobileParty.MainParty.CurrentSettlement?.SiegeEvent == null || MobileParty.MainParty.CurrentSettlement?.MapFaction != MobileParty.MainParty.MapFaction));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\PartyScreenHelper.cs:570: OpenScreenWithDummyRoster(troopRoster, leftPrisonerRoster, troopRoster2, rightPrisonerRoster, textObject, MobileParty.MainParty.Name, Campaign.Current.Models.PartySizeLimitModel.GetAssumedPartySizeForLordParty(hero, hero.Clan.MapFaction, hero.Clan), MobileParty.MainParty.Party.PartySizeLimit, null, onScreenClosed ?? new PartyScreenClosedDelegate(OpenScreenAsCreateClanPartyForHeroPartyScreenClosed), isTroopTransferable ?? new IsTroopTransferableDelegate(OpenScreenAsCreateClanPartyForHeroTroopTransferableDelegate), canTalkToTroopDelegate);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\QuestHelper.cs:140: if (questToCheck.QuestGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:363: if (party.IsVillager && party.HomeSettlement.IsVillage && party.HomeSettlement.Village.Bound == settlementWhichChangedFaction && party.HomeSettlement.MapFaction != settlementWhichChangedFaction.MapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:384: if (party2.IsCaravan && FactionManager.IsAtWarAgainstFaction(party2.MapFaction, settlementWhichChangedFaction.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:432: if ((fromFaction == null || settlement.MapFaction == fromFaction) && (settlement.IsTown || settlement.IsVillage))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:440: if ((fromFaction == null || settlement2.MapFaction == fromFaction) && (settlement2.IsTown || settlement2.IsVillage))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:456: IFaction mapFaction = hero.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:464: IFaction mapFaction2 = item.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:483: num2 = ((mapFaction2 == mapFaction) ? 0.2f : 0f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:486: if (mapFaction2 == mapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:490: else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mapFaction2, mapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:494: else if (FactionManager.IsNeutralWithFaction(mapFaction2, mapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:502: float value = Campaign.Current.Models.MapDistanceModel.GetDistance(hero.MapFaction.FactionMidSettlement, item, isFromPort: false, isTargetingPort: false, MobileParty.NavigationType.Default) / Campaign.MapDiagonal;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:642: if (settlement.MapFaction == consideringClan.MapFaction && settlement.IsFortification)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:651: num = ((!settlement.MapFaction.IsAtWarWith(neighborFortification.MapFaction)) ? ((settlement.MapFaction != consideringClan.MapFaction) ? (num + 0.05f) : (num + 0.1f)) : (num - 0.2f));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\SettlementHelper.cs:661: num = ((!settlement.MapFaction.IsAtWarWith(neighborFortification2.MapFaction)) ? ((settlement.MapFaction != consideringClan.MapFaction) ? (num + 0.01f) : (num + 0.02f)) : (num - 0.04f));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:24: return ship.Owner.MobileParty.Army.LeaderParty.MapFaction.Banner;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:36: result.Item1 = characterObject.HeroObject.MapFaction.Color;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:37: result.Item2 = characterObject.HeroObject.MapFaction.Color2;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:43: result.Item1 = ship.Owner.MobileParty.Army.LeaderParty.MapFaction.Color;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:44: result.Item2 = ship.Owner.MobileParty.Army.LeaderParty.MapFaction.Color2;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:48: result.Item1 = ship.Owner.MapFaction.Color;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:49: result.Item2 = ship.Owner.MapFaction.Color2;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:61: return party.MobileParty.Army.LeaderParty.MapFaction.Banner;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:75: result.Item1 = party.MobileParty.Army.LeaderParty.MapFaction.Color;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:76: result.Item2 = party.MobileParty.Army.LeaderParty.MapFaction.Color2;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:80: result.Item1 = party.Owner.MapFaction.Color;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\ShipHelper.cs:81: result.Item2 = party.Owner.MapFaction.Color2;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\StringHelpers.cs:73: if (character.HeroObject.MapFaction != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\StringHelpers.cs:75: empty.SetTextVariable("FACTION", character.HeroObject.MapFaction.Name);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:254: if (IsWaitingForArmyMembers() && AiBehaviorObject is Settlement settlement2 && settlement2 == settlement && settlement.MapFaction != LeaderParty.MapFaction && LeaderParty.SiegeEvent == null && LeaderParty.MapEvent == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:423: if (MBRandom.RandomFloat < 0.25f && !LeaderParty.MapFaction.FactionsAtWarWith.AnyQ((IFaction x) => x.Fiefs.Any()))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:450: if (!LeaderParty.TargetSettlement.MapFaction.IsAtWarWith(LeaderParty.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:504: else if (LeaderParty.BesiegedSettlement != null && LeaderParty.MapFaction.IsAtWarWith(LeaderParty.BesiegedSettlement.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:685: if (settlement2.MapFaction == Kingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:689: else if (!FactionManager.IsAtWarAgainstFaction(settlement2.MapFaction, Kingdom))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Army.cs:756: return settlement.SiegeEvent.BesiegerCamp.MapFaction.IsAtWarWith(LeaderParty.MapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:442: obj37.GetFieldDefinitionWithId(new MemberTypeId(2, 130)).InitializeForAutoGeneration(MapEventSide.AutoGeneratedGetMemberValue_mapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:520: obj42.GetFieldDefinitionWithId(new MemberTypeId(4, 1120)).InitializeForAutoGeneration(MobileParty.AutoGeneratedGetMemberValue_actualClan);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:906: obj88.GetFieldDefinitionWithId(new MemberTypeId(3, 124)).InitializeForAutoGeneration(CharacterKilledLogEntry.AutoGeneratedGetMemberValueVictimMapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:907: obj88.GetFieldDefinitionWithId(new MemberTypeId(3, 125)).InitializeForAutoGeneration(CharacterKilledLogEntry.AutoGeneratedGetMemberValueKillerMapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:959: obj97.GetFieldDefinitionWithId(new MemberTypeId(3, 221)).InitializeForAutoGeneration(GatherArmyLogEntry.AutoGeneratedGetMemberValueArmyMapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1030: obj108.GetFieldDefinitionWithId(new MemberTypeId(3, 330)).InitializeForAutoGeneration(TakePrisonerLogEntry.AutoGeneratedGetMemberValueCapturerPartyMapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1039: obj109.GetFieldDefinitionWithId(new MemberTypeId(3, 730)).InitializeForAutoGeneration(EndCaptivityLogEntry.AutoGeneratedGetMemberValueCapturerMapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\AutoGeneratedSaveManager.cs:1053: obj111.GetFieldDefinitionWithId(new MemberTypeId(3, 343)).InitializeForAutoGeneration(VillageStateChangedLogEntry.AutoGeneratedGetMemberValueRaiderPartyMapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1250: if (faction == faction2 || faction.MapFaction == faction2.MapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1254: if (!faction.IsMapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1258: if (!faction2.IsMapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1379: if (faction == faction2 || faction.MapFaction == faction2.MapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1383: if (!faction.IsMapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1387: if (!faction2.IsMapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1723: if (obj.MapFaction == obj2)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1791: if (Hero.MainHero.MapFaction.Leader != Hero.MainHero)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1793: if (Hero.MainHero.MapFaction.IsKingdomFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1795: ChangeRulingClanAction.Apply(Hero.MainHero.MapFaction as Kingdom, Clan.PlayerClan);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1799: (Hero.MainHero.MapFaction as Clan).SetLeader(Hero.MainHero);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2263: if (item.TargetParty == MobileParty.MainParty && item.MapFaction.IsAtWarWith(MobileParty.MainParty.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2676: if (!FactionManager.IsAtWarAgainstFaction(obj.MapFaction, obj2.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:214: public bool IsMapFaction => _kingdom == null;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:338: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1314: clan.Banner = Banner.CreateOneColoredBannerWithOneIcon(settlement.MapFaction.Banner.GetFirstIconColor(), settlement.MapFaction.Banner.GetPrimaryColor(), iconMeshId);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1317: clan.Color = settlement.MapFaction.Color2;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1318: clan.Color2 = settlement.MapFaction.Color;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1320: clan.BannerBackgroundColorPrimary = settlement.MapFaction.Banner.GetFirstIconColor();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1321: clan.BannerBackgroundColorSecondary = settlement.MapFaction.Banner.GetFirstIconColor();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1322: clan.BannerIconColor = settlement.MapFaction.Banner.GetPrimaryColor();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1341: Clan clan = CreateClan(Hero.MainHero.MapFaction.StringId + "_companion_clan");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1344: clan.Banner = Banner.CreateOneColoredBannerWithOneIcon(settlement.MapFaction.Banner.GetFirstIconColor(), settlement.MapFaction.Banner.GetPrimaryColor(), newClanIconId);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:50: if ((!mobileParty.IsCurrentlyEngagingSettlement || mobileParty.ShortTermTargetSettlement == null || mobileParty.ShortTermTargetSettlement != mobileParty.CurrentSettlement) && (!mobileParty.IsCurrentlyEngagingParty || (mobileParty.ShortTermTargetParty.IsActive && (mobileParty.ShortTermTargetParty.CurrentSettlement == null || (mobileParty.ShortTermTargetParty.MapEvent != null && (mobileParty.ShortTermTargetParty.MapEvent.GetLeaderParty(BattleSideEnum.Attacker).MapFaction == mobileParty.MapFaction || mobileParty.ShortTermTargetParty.MapEvent.GetLeaderParty(BattleSideEnum.Defender).MapFaction == mobileParty.MapFaction))))) && mobileParty.Ai.AiBehaviorInteractable.CanPartyInteract(mobileParty, dt))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:81: if (attackerParty.MapFaction == defenderParty.MapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:155: if (mapEvent != null && mapEvent.IsRaid && attackerParty.MapFaction.IsAtWarWith(settlement.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:195: if (attackerParty.MapFaction == MobileParty.MainParty.MapFaction || (PartyBase.MainParty.MapEvent.AttackerSide.LeaderParty != PartyBase.MainParty && PartyBase.MainParty.MapEvent.DefenderSide.LeaderParty != PartyBase.MainParty))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:213: if (mapEvent2 != null && !mapEvent2.IsFinalized && (mapEvent2.AttackerSide.MapFaction == attackerParty.MapFaction || mapEvent2.DefenderSide.MapFaction == attackerParty.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:219: settlement.Party.MapEventSide = ((mapEvent2.AttackerSide.MapFaction == attackerParty.MapFaction) ? mapEvent2.DefenderSide : mapEvent2.AttackerSide);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:221: else if (settlement.Party.MapEvent == null && attackerParty != MobileParty.MainParty && attackerParty.ShortTermBehavior == AiBehavior.RaidSettlement && attackerParty.ShortTermTargetSettlement == settlement && FactionManager.IsAtWarAgainstFaction(attackerParty.MapFaction, settlement.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:233: if (MobileParty.MainParty.MapFaction == settlement.MapFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\EncounterManager.cs:244: else if (attackerParty != MobileParty.MainParty && attackerParty.ShortTermBehavior == AiBehavior.AssaultSettlement && attackerParty.ShortTermTargetSettlement == settlement && FactionManager.IsAtWarAgainstFaction(attackerParty.MapFaction, settlement.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\FactionManager.cs:192: if (faction1.MapFaction != faction1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:566: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:581: return HomeSettlement.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:585: return PartyBelongedTo.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:597: if (MapFaction != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:599: return MapFaction.Leader == this;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:609: if (MapFaction != null && MapFaction.IsKingdomFaction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:611: return MapFaction.Leader == this;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1866: MBTextManager.SetTextVariable("FACTION_NAME", GameTexts.FindText("str_neutral_term_for_culture", o.MapFaction.IsMinorFaction ? o.Culture.StringId : o.MapFaction.Culture.StringId));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1867: if (o.MapFaction.Culture.StringId == "empire")
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1908: List<Kingdom> list = Campaign.Current.Kingdoms.Where((Kingdom x) => x.Culture == o.MapFaction.Culture).ToList();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1911: MBTextManager.SetTextVariable("RULER", o.MapFaction.Leader.Name);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\IFaction.cs:66: bool IsMapFaction { get; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\IFaction.cs:70: IFaction MapFaction { get; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:123: public bool IsMapFaction => true;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:262: public IFaction MapFaction => this;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:285: if (mobileParty.MapFaction == this)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\KingdomManager.cs:244: Hero newOwner = ((capturerParty.MapFaction is Kingdom kingdom) ? kingdom.Leader : capturerParty.MapFaction.Leader);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\PartyRole.cs:11: PartyOwner,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\PartyThinkParams.cs:58: foreach (Hero hero in MobilePartyOf.MapFaction.Heroes)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\PlayerCaptivity.cs:171: return (int)((MBRandom.RandomFloat * 0.5f + 0.5f) * ((float)Hero.MainHero.Gold * 0.05f + 300f) * (float)((!Hero.MainHero.PartyBelongedToAsPrisoner.IsSettlement) ? 1 : (Hero.MainHero.PartyBelongedToAsPrisoner.Settlement.MapFaction.IsKingdomFaction ? 4 : 2)) * (float)((!Hero.MainHero.PartyBelongedToAsPrisoner.IsMobile || !Hero.MainHero.PartyBelongedToAsPrisoner.MobileParty.IsLordParty) ? 1 : 2) * (Hero.MainHero.GetPerkValue(DefaultPerks.Trade.ManOfMeans) ? (1f + DefaultPerks.Trade.ManOfMeans.SecondaryBonus) : 1f));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ApplyHeirSelectionAction.cs:18: Settlement settlement = SettlementHelper.FindNearestSettlementToMobileParty(heir.PartyBelongedTo, MobileParty.NavigationType.All, (Settlement s) => (s.IsTown || s.IsCastle) && !FactionManager.IsAtWarAgainstFaction(s.MapFaction, heir.MapFaction));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:17: if (defenderParty.IsMobile && defenderParty.MobileParty.MapFaction == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:28: bool flag = attackerParty.MapFaction.IsAtWarWith(defenderParty.MapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:101: bool isClan = attackerParty.MapFaction.IsClan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:113: ChangeCrimeRatingAction.Apply(defenderParty.MapFaction, num2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:131: ChangeRelationAction.ApplyRelationChangeBetweenHeroes(leaderHero, leaderHero.MapFaction.Leader, num3);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:140: if (num3 < 0 && attackerParty.MapFaction != null && defenderParty.MapFaction != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:142: ChangeRelationAction.ApplyRelationChangeBetweenHeroes(leaderHero, defenderParty.MapFaction.Leader, num3);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:192: if (attackerParty == PartyBase.MainParty && attackerParty.MapFaction != defenderParty.MapFaction && !FactionManager.IsAtWarAgainstFaction(attackerParty.MapFaction, defenderParty.MapFaction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:194: ChangeRelationAction.ApplyPlayerRelation(defenderParty.MapFaction.Leader, -10);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\BeHostileAction.cs:195: DeclareWarAction.ApplyByPlayerHostility(attackerParty.MapFaction, defenderParty.MapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeCrimeRatingAction.cs:23: if (num > Campaign.Current.Models.CrimeModel.DeclareWarCrimeRatingThreshold && Hero.MainHero.MapFaction.Leader == Hero.MainHero && !faction.IsAtWarWith(Hero.MainHero.MapFaction) && Hero.MainHero.MapFaction != faction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeCrimeRatingAction.cs:26: DeclareWarAction.ApplyByCrimeRatingChange(faction, Hero.MainHero.MapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeKingdomAction.cs:229: if (item.IsVisible && ((item.Party.Owner != null && item.Party.Owner.Clan == clan) || (clan == Clan.PlayerClan && ((!FactionManager.IsAtWarAgainstFaction(item.MapFaction, faction3) && FactionManager.IsAtWarAgainstFaction(item.MapFaction, faction4)) || (FactionManager.IsAtWarAgainstFaction(item.MapFaction, faction3) && !FactionManager.IsAtWarAgainstFaction(item.MapFaction, faction4))))))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeOwnerOfSettlementAction.cs:54: if (item.MapEvent == null && item != MobileParty.MainParty && item.ShortTermTargetParty == boundVillage.VillagerPartyComponent.MobileParty && !item.MapFaction.IsAtWarWith(newOwner.MapFaction))

 succeeded in 942ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:18: public static class HeroHelper
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MobilePartyHelper.cs:13: public static class MobilePartyHelper
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignTickCacheDataStore.cs:22: private class MobilePartyComparer : IComparer<MobileParty>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CharacterRelationManager.cs:11: internal class HeroRelations
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:21: public sealed class Clan : MBObjectBase, IFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:338: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:26: public sealed class Hero : MBObjectBase, ITrackableCampaignObject, ITrackableBase, IRandomOwner
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:566: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\HeroCreator.cs:9: public static class HeroCreator
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\HeroCreator.cs:11: private class HeroInitializationArgs
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:262: public IFaction MapFaction => this;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:17: public class ClanVariablesCampaignBehavior : CampaignBehaviorBase
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CraftingCampaignBehavior.cs:90: internal class HeroCraftingRecord
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroAgentSpawnCampaignBehavior.cs:16: public class HeroAgentSpawnCampaignBehavior : CampaignBehaviorBase
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroKnownInformationCampaignBehavior.cs:15: public class HeroKnownInformationCampaignBehavior : CampaignBehaviorBase
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpawnCampaignBehavior.cs:12: public class HeroSpawnCampaignBehavior : CampaignBehaviorBase
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\MobilePartyTrainingBehavior.cs:9: public class MobilePartyTrainingBehavior : CampaignBehaviorBase
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CharacterDevelopment\HeroDeveloper.cs:10: public class HeroDeveloper
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\ClanFinanceModel.cs:8: public abstract class ClanFinanceModel : MBGameModel<ClanFinanceModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\ClanMemberPartyRoleModel.cs:7: public abstract class ClanMemberPartyRoleModel : MBGameModel<ClanMemberPartyRoleModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\ClanPoliticsModel.cs:6: public abstract class ClanPoliticsModel : MBGameModel<ClanPoliticsModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\ClanTierModel.cs:6: public abstract class ClanTierModel : MBGameModel<ClanTierModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\HeroAgentLocationModel.cs:7: public abstract class HeroAgentLocationModel : MBGameModel<HeroAgentLocationModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\HeroCreationModel.cs:9: public abstract class HeroCreationModel : MBGameModel<HeroCreationModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\HeroDeathProbabilityCalculationModel.cs:5: public abstract class HeroDeathProbabilityCalculationModel : MBGameModel<HeroDeathProbabilityCalculationModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\MobilePartyAIModel.cs:8: public abstract class MobilePartyAIModel : MBGameModel<MobilePartyAIModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\MobilePartyFoodConsumptionModel.cs:6: public abstract class MobilePartyFoodConsumptionModel : MBGameModel<MobilePartyFoodConsumptionModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\MobilePartyMoraleModel.cs:7: public abstract class MobilePartyMoraleModel : MBGameModel<MobilePartyMoraleModel>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Election\SettlementClaimantDecision.cs:19: public class ClanAsDecisionOutcome : DecisionOutcome
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameState\ClanState.cs:8: public class ClanState : TaleWorlds.Core.GameState
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\HeroRelatedIssueCoolDownData.cs:6: public class HeroRelatedIssueCoolDownData : IssueCoolDownData
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\ClanChangeKingdomLogEntry.cs:9: public class ClanChangeKingdomLogEntry : LogEntry, IChatNotification, IWarLog
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\ClanDestroyedLogEntry.cs:9: public class ClanDestroyedLogEntry : LogEntry, IEncyclopediaLog, IChatNotification
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.LogEntries\ClanLeaderChangedLogEntry.cs:9: public class ClanLeaderChangedLogEntry : LogEntry, IEncyclopediaLog
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.MapEvents\MapEventSide.cs:106: public IFaction MapFaction => _mapFaction ?? LeaderParty.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:25: public sealed class MobileParty : CampaignObjectBase, ILocatable<MobileParty>, IMapPoint, ITrackableCampaignObject, ITrackableBase, IRandomOwner
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:1079: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobilePartyAi.cs:17: public class MobilePartyAi
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\PartyBase.cs:236: public IFaction MapFaction
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.SaveCompability\HeroDeveloperResolver.cs:9: public class HeroDeveloperResolver : IConflictResolver
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.SaveCompability\HeroTraitDeveloperResolver.cs:9: public class HeroTraitDeveloperResolver : IConflictResolver
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.SceneInformationPopupTypes\ClanMemberPeaceDeathSceneNotificationItem.cs:9: public class ClanMemberPeaceDeathSceneNotificationItem : SceneNotificationData
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.SceneInformationPopupTypes\ClanMemberWarDeathSceneNotificationItem.cs:8: public class ClanMemberWarDeathSceneNotificationItem : SceneNotificationData
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.SceneInformationPopupTypes\HeroExecutionSceneNotificationData.cs:12: public class HeroExecutionSceneNotificationData : SceneNotificationData
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:292: public IFaction MapFaction => SettlementComponent?.MapFaction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Siege\BesiegerCamp.cs:33: public IFaction MapFaction => _faction;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection\CampaignUIHelper.cs:94: public class MobilePartyPrecedenceComparer : IComparer<MobileParty>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection\HeroViewModel.cs:8: public class HeroViewModel : CharacterViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection\HeroVM.cs:7: public class HeroVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanCardSelectionPopupItemPropertyVM.cs:6: public class ClanCardSelectionPopupItemPropertyVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanCardSelectionPopupItemVM.cs:9: public class ClanCardSelectionPopupItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanCardSelectionPopupVM.cs:12: public class ClanCardSelectionPopupVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanFinanceExpenseItemVM.cs:8: public class ClanFinanceExpenseItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanFinanceIncomeItemBaseVM.cs:8: public class ClanFinanceIncomeItemBaseVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanLordItemVM.cs:18: public class ClanLordItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanLordStatusItemVM.cs:7: public class ClanLordStatusItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanManagementVM.cs:19: public class ClanManagementVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanPartyBehaviorSelectorVM.cs:8: public class ClanPartyBehaviorSelectorVM : SelectorVM<SelectorItemVM>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanPartyItemVM.cs:19: public class ClanPartyItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanPartyMemberItemVM.cs:8: public class ClanPartyMemberItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanRoleAssignedThroughClanScreenEvent.cs:5: public class ClanRoleAssignedThroughClanScreenEvent : EventBase
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanRoleItemVM.cs:11: public class ClanRoleItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanRoleItemVM.cs:13: private class ClanRoleMemberComparer : IComparer<ClanRoleMemberItemVM>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanRoleMemberItemVM.cs:14: public class ClanRoleMemberItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement\ClanSettlementItemVM.cs:16: public class ClanSettlementItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories\ClanFiefsSortControllerVM.cs:7: public class ClanFiefsSortControllerVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories\ClanFiefsVM.cs:17: public class ClanFiefsVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories\ClanIncomeSortControllerVM.cs:9: public class ClanIncomeSortControllerVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories\ClanIncomeVM.cs:15: public class ClanIncomeVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories\ClanMembersSortControllerVM.cs:7: public class ClanMembersSortControllerVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories\ClanMembersVM.cs:15: public class ClanMembersVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories\ClanPartiesSortControllerVM.cs:8: public class ClanPartiesSortControllerVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories\ClanPartiesVM.cs:20: public class ClanPartiesVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance\ClanFinanceAlleyItemVM.cs:18: public class ClanFinanceAlleyItemVM : ClanFinanceIncomeItemBaseVM
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance\ClanFinanceCommonAreaItemVM.cs:8: public class ClanFinanceCommonAreaItemVM : ClanFinanceIncomeItemBaseVM
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance\ClanFinanceMercenaryItemVM.cs:7: public class ClanFinanceMercenaryItemVM : ClanFinanceIncomeItemBaseVM
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance\ClanFinanceTownItemVM.cs:10: public class ClanFinanceTownItemVM : ClanFinanceIncomeItemBaseVM
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance\ClanFinanceWorkshopItemVM.cs:17: public class ClanFinanceWorkshopItemVM : ClanFinanceIncomeItemBaseVM
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Supporters\ClanSupporterGroupVM.cs:9: public class ClanSupporterGroupVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Supporters\ClanSupporterItemVM.cs:6: public class ClanSupporterItemVM : ViewModel
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages\HeroAgeComparer.cs:5: public class HeroAgeComparer : IComparer<HeroVM>
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages\HeroRelationComparer.cs:5: public class HeroRelationComparer : IComparer<HeroVM>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=332; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Clan.cs' | Select-Object -Skip ("'$start-1) -First 28 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=560; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Hero.cs' | Select-Object -Skip ("'$start-1) -First 35 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=1068; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs' | Select-Object -Skip ("'$start-1) -First 35 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs' -Pattern 'ActualClan' -Context 2,4 | Select-Object -First 20 | ForEach-Object { '{0}: {1}' -f "'$_.LineNumber,$_.Line.Trim(); $_.Context.PreContext; $_.Context.PostContext; '"'---' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=224; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\PartyBase.cs' | Select-Object -Skip ("'$start-1) -First 30 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 834ms:
 332: 				value = minClanTier;
 333: 			}
 334: 			_tier = value;
 335: 		}
 336: 	}
 337: 
 338: 	public IFaction MapFaction
 339: 	{
 340: 		get
 341: 		{
 342: 			if (Kingdom != null)
 343: 			{
 344: 				return Kingdom;
 345: 			}
 346: 			return this;
 347: 		}
 348: 	}
 349: 
 350: 	[SaveableProperty(100)]
 351: 	public CampaignTime NotAttackableByPlayerUntilTime { get; set; }
 352: 
 353: 	public float Aggressiveness
 354: 	{
 355: 		get
 356: 		{
 357: 			return _aggressiveness;
 358: 		}
 359: 		internal set

 succeeded in 693ms:
 224: 		get
 225: 		{
 226: 			return _index;
 227: 		}
 228: 		private set
 229: 		{
 230: 			_index = value;
 231: 		}
 232: 	}
 233: 
 234: 	public bool IsValid => Index >= 0;
 235: 
 236: 	public IFaction MapFaction
 237: 	{
 238: 		get
 239: 		{
 240: 			if (MobileParty != null)
 241: 			{
 242: 				return MobileParty.MapFaction;
 243: 			}
 244: 			if (Settlement != null)
 245: 			{
 246: 				return Settlement.MapFaction;
 247: 			}
 248: 			return null;
 249: 		}
 250: 	}
 251: 
 252: 	[SaveableProperty(210)]
 253: 	public int RandomValue { get; private set; } = MBRandom.RandomInt(1, int.MaxValue);

 succeeded in 839ms:
 560: 			{
 561: 				_governorOf = value;
 562: 			}
 563: 		}
 564: 	}
 565: 
 566: 	public IFaction MapFaction
 567: 	{
 568: 		get
 569: 		{
 570: 			if (Clan != null)
 571: 			{
 572: 				IFaction kingdom = Clan.Kingdom;
 573: 				return kingdom ?? Clan;
 574: 			}
 575: 			if (IsSpecial)
 576: 			{
 577: 				return null;
 578: 			}
 579: 			if (HomeSettlement != null)
 580: 			{
 581: 				return HomeSettlement.MapFaction;
 582: 			}
 583: 			if (PartyBelongedTo != null)
 584: 			{
 585: 				return PartyBelongedTo.MapFaction;
 586: 			}
 587: 			return null;
 588: 		}
 589: 	}
 590: 
 591: 	public List<Alley> OwnedAlleys { get; private set; }
 592: 
 593: 	public bool IsFactionLeader
 594: 	{

 succeeded in 717ms:
221: private Clan _actualClan;

	[SaveableField(1120)]

	[SaveableField(1200)]
	private float _moraleDueToEvents;

---
942: public Clan ActualClan
	public float BaseFoodChange => Campaign.Current.Models.MobilePartyFoodConsumptionModel.CalculateDailyBaseFoodConsumptionf(this).ResultNumber;

	{
		get
		{
			return _actualClan;
---
946: return _actualClan;
		get
		{
		}
		set
		{
			if (_actualClan != value)
---
950: if (_actualClan != value)
		set
		{
			{
				if (_actualClan != null && value != null && PartyComponent is WarPartyComponent warPartyComponent)
				{
					warPartyComponent.OnClanChange(_actualClan, value);
---
952: if (_actualClan != null && value != null && PartyComponent is WarPartyComponent warPartyComponent)
			if (_actualClan != value)
			{
				{
					warPartyComponent.OnClanChange(_actualClan, value);
				}
				_actualClan = value;
---
954: warPartyComponent.OnClanChange(_actualClan, value);
				if (_actualClan != null && value != null && PartyComponent is WarPartyComponent warPartyComponent)
				{
				}
				_actualClan = value;
			}
		}
---
956: _actualClan = value;
					warPartyComponent.OnClanChange(_actualClan, value);
				}
			}
		}
	}

---
1083: if (ActualClan != null)
		get
		{
			{
				return ActualClan.MapFaction;
			}
			if (Party.Owner != null)
---
1085: return ActualClan.MapFaction;
			if (ActualClan != null)
			{
			}
			if (Party.Owner != null)
			{
				if (Party.Owner == Hero.MainHero)
---
1304: collectedObjects.Add(_actualClan);
		CampaignVec2.AutoGeneratedStaticCollectObjectsCampaignVec2(_pathLastPosition, collectedObjects);
		CampaignTime.AutoGeneratedStaticCollectObjectsCampaignTime(_navigationTransitionStartTime, collectedObjects);
		CampaignVec2.AutoGeneratedStaticCollectObjectsCampaignVec2(_position, collectedObjects);
		collectedObjects.Add(_partyComponent);
		collectedObjects.Add(LastVisitedSettlement);
		collectedObjects.Add(Ai);
---
1597: internal static object AutoGeneratedGetMemberValue_actualClan(object o)
	}

	{
		return ((MobileParty)o)._actualClan;
	}

---
1599: return ((MobileParty)o)._actualClan;
	internal static object AutoGeneratedGetMemberValue_actualClan(object o)
	{
	}

	internal static object AutoGeneratedGetMemberValue_moraleDueToEvents(object o)
	{
---
2373: if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.2.0") && IsCaravan && Owner == Hero.MainHero && ActualClan == null)
			SetWagePaymentLimit(Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit);
		}
		{
			ActualClan = Owner.Clan;
		}
		if (!MBSaveLoad.IsUpdatingGameVersion || !(MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.1.4")))
---
2375: ActualClan = Owner.Clan;
		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.2.0") && IsCaravan && Owner == Hero.MainHero && ActualClan == null)
		{
		}
		if (!MBSaveLoad.IsUpdatingGameVersion || !(MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.1.4")))
		{
			return;
---
3023: ActualClan = null;
		}
		PartyComponent?.Finish();
		Anchor = null;
		Campaign.Current.CampaignObjectManager.RemoveMobileParty(this);
		foreach (Settlement item in list)
		{
---

 succeeded in 733ms:
1068: 
1069: 	public MapEvent MapEvent => Party.MapEvent;
1070: 
1071: 	public TroopRoster MemberRoster => Party.MemberRoster;
1072: 
1073: 	public TroopRoster PrisonRoster => Party.PrisonRoster;
1074: 
1075: 	public ItemRoster ItemRoster => Party.ItemRoster;
1076: 
1077: 	public bool IsMainParty => this == MainParty;
1078: 
1079: 	public IFaction MapFaction
1080: 	{
1081: 		get
1082: 		{
1083: 			if (ActualClan != null)
1084: 			{
1085: 				return ActualClan.MapFaction;
1086: 			}
1087: 			if (Party.Owner != null)
1088: 			{
1089: 				if (Party.Owner == Hero.MainHero)
1090: 				{
1091: 					return Party.Owner.MapFaction;
1092: 				}
1093: 				if (Party.Owner.IsNotable)
1094: 				{
1095: 					return Party.Owner.HomeSettlement.MapFaction;
1096: 				}
1097: 				if ((IsMilitia || IsGarrison || IsVillager || IsPatrolParty) && HomeSettlement?.OwnerClan != null)
1098: 				{
1099: 					return HomeSettlement.OwnerClan.MapFaction;
1100: 				}
1101: 				if (IsCaravan || IsBanditBossParty)
1102: 				{

codex
The faction decompile shows why this matters: `Hero.MapFaction` and `Clan.MapFaction` collapse a clan into its kingdom when it has one. I’m reading the rest of `MobileParty.MapFaction` before deciding whether that creates a real NPC-scope regression or just mirrors vanilla’s prohibited-list semantics.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=1079; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs' | Select-Object -Skip ("'$start-1) -First 60 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=936; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs' | Select-Object -Skip ("'$start-1) -First 28 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'IsMerchant|IsNotable|OwnedCaravans|CanHeroCreateCaravan' | Select-Object -First 100 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'ActualClan = Owner\\.Clan|ActualClan = .*Clan|new CaravanPartyComponent' | Select-Object -First 80 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 757ms:
 936: 			return Campaign.Current.Models.MobilePartyFoodConsumptionModel.CalculateDailyFoodConsumptionf(this, baseConsumption).ResultNumber;
 937: 		}
 938: 	}
 939: 
 940: 	public float BaseFoodChange => Campaign.Current.Models.MobilePartyFoodConsumptionModel.CalculateDailyBaseFoodConsumptionf(this).ResultNumber;
 941: 
 942: 	public Clan ActualClan
 943: 	{
 944: 		get
 945: 		{
 946: 			return _actualClan;
 947: 		}
 948: 		set
 949: 		{
 950: 			if (_actualClan != value)
 951: 			{
 952: 				if (_actualClan != null && value != null && PartyComponent is WarPartyComponent warPartyComponent)
 953: 				{
 954: 					warPartyComponent.OnClanChange(_actualClan, value);
 955: 				}
 956: 				_actualClan = value;
 957: 			}
 958: 		}
 959: 	}
 960: 
 961: 	public ExplainedNumber FoodChangeExplained
 962: 	{
 963: 		get

 succeeded in 760ms:
1079: 	public IFaction MapFaction
1080: 	{
1081: 		get
1082: 		{
1083: 			if (ActualClan != null)
1084: 			{
1085: 				return ActualClan.MapFaction;
1086: 			}
1087: 			if (Party.Owner != null)
1088: 			{
1089: 				if (Party.Owner == Hero.MainHero)
1090: 				{
1091: 					return Party.Owner.MapFaction;
1092: 				}
1093: 				if (Party.Owner.IsNotable)
1094: 				{
1095: 					return Party.Owner.HomeSettlement.MapFaction;
1096: 				}
1097: 				if ((IsMilitia || IsGarrison || IsVillager || IsPatrolParty) && HomeSettlement?.OwnerClan != null)
1098: 				{
1099: 					return HomeSettlement.OwnerClan.MapFaction;
1100: 				}
1101: 				if (IsCaravan || IsBanditBossParty)
1102: 				{
1103: 					return Party.Owner.MapFaction;
1104: 				}
1105: 				if (_isCurrentlyUsedByAQuest && Party.Owner != null)
1106: 				{
1107: 					return Party.Owner.MapFaction;
1108: 				}
1109: 				return (LeaderHero != null) ? LeaderHero.MapFaction : null;
1110: 			}
1111: 			if (HomeSettlement != null)
1112: 			{
1113: 				return HomeSettlement.OwnerClan.MapFaction;
1114: 			}
1115: 			return (LeaderHero != null) ? LeaderHero.MapFaction : null;
1116: 		}
1117: 	}
1118: 
1119: 	public TextObject ArmyName
1120: 	{
1121: 		get
1122: 		{
1123: 			if (Army == null || Army.LeaderParty != this)
1124: 			{
1125: 				return Name;
1126: 			}
1127: 			return Army.Name;
1128: 		}
1129: 	}
1130: 
1131: 	public SiegeEvent SiegeEvent => BesiegerCamp?.SiegeEvent;
1132: 
1133: 	public float Food => (float)Party.RemainingFoodPercentage * 0.01f + (float)TotalFoodAtInventory;
1134: 
1135: 	public int TotalFoodAtInventory => ItemRoster.TotalFood;
1136: 
1137: 	public float SeeingRange => Campaign.Current.Models.MapVisibilityModel.GetPartySpottingRange(this).ResultNumber;
1138: 

 succeeded in 901ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:137: if ((hero.MapFaction == null || hero.MapFaction.Leader != Hero.MainHero) && (!hero.IsNotable || hero.HomeSettlement.OwnerClan != Hero.MainHero.Clan))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\HeroHelper.cs:172: if (hero.IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\MenuHelper.cs:390: bool flag2 = MapEvent.PlayerMapEvent.PartiesOnSide(BattleSideEnum.Defender).Any((MapEventParty involvedParty) => involvedParty.Party.IsMobile && (involvedParty.Party.MobileParty.IsCaravan || (involvedParty.Party.Owner != null && involvedParty.Party.Owner.IsMerchant)));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1766: if (!obj.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:3114: Hero randomElementWithPredicate = Hero.AllAliveHeroes.GetRandomElementWithPredicate((Hero x) => !x.IsChild && x.SupporterOf != Clan.PlayerClan && x.IsNotable);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Clan.cs:1363: if (hero != Leader && hero.IsAlive && hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.None && !hero.IsNotSpawned && !hero.IsDisabled && !hero.IsWanderer && !hero.IsNotable && hero.Age >= (float)heroComesOfAge)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:335: public bool IsMerchant => Occupation == Occupation.Merchant;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:387: public bool IsNotable
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:391: if (!IsArtisan && !IsGangLeader && !IsPreacher && !IsMerchant && !IsRuralNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:629: public List<CaravanPartyComponent> OwnedCaravans { get; private set; }
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1356: else if (IsNotable && CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1489: OwnedCaravans = new List<CaravanPartyComponent>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1507: OwnedCaravans = new List<CaravanPartyComponent>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Hero.cs:1688: if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.1.1") && !IsDead && CurrentSettlement == null && IsNotable && BornSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\NameGenerator.cs:72: if (hero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\NameGenerator.cs:103: else if (hero.IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\NameGenerator.cs:534: if (!hero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\NameGenerator.cs:538: else if (hero.IsMerchant && hero.OwnedWorkshops.Count > 0)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\QuestBase.cs:542: result = !hero.IsNotable || hero != QuestGiver;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\AddHeroToPartyAction.cs:14: _ = hero.IsNotable;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\KillCharacterAction.cs:42: if (victim.IsNotable && victim.Issue?.IssueQuest != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravanConversationsCampaignBehavior.cs:56: if (!Hero.OneToOneConversationHero.IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:342: if (allAliveHero.Clan != Clan.PlayerClan && Campaign.Current.Models.CaravanModel.CanHeroCreateCaravan(allAliveHero))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:574: if (hero != Hero.MainHero && MBRandom.RandomFloat < 0.75f && Campaign.Current.Models.CaravanModel.CanHeroCreateCaravan(hero))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterRelationCampaignBehavior.cs:123: Hero randomElementWithPredicate = Hero.AllAliveHeroes.GetRandomElementWithPredicate((Hero x) => x.IsActive && x.IsNotable && x.CurrentSettlement?.MapFaction == winnerSide.LeaderParty.MapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterRelationCampaignBehavior.cs:391: if ((notable.IsArtisan || notable.IsMerchant) && MBRandom.RandomFloat < 0.05f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CharacterRelationCampaignBehavior.cs:406: if ((notable2.IsArtisan || notable2.IsMerchant) && MBRandom.RandomFloat < 0.05f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:479: if (hero.IsActive && hero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroAgentSpawnCampaignBehavior.cs:138: text = (hero.IsArtisan ? "sp_notable_artisan" : (hero.IsMerchant ? "sp_notable_merchant" : (hero.IsPreacher ? "sp_notable_preacher" : (hero.IsGangLeader ? "sp_notable_gangleader" : (hero.IsRuralNotable ? "sp_notable_rural_notable" : ((hero.GovernorOf == hero.CurrentSettlement.Town) ? "sp_governor" : "sp_notable"))))));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroAgentSpawnCampaignBehavior.cs:185: text2 = (hero.IsArtisan ? "_villager_artisan" : (hero.IsMerchant ? "_villager_merchant" : (hero.IsPreacher ? "_villager_preacher" : (hero.IsGangLeader ? "_villager_gangleader" : (hero.IsRuralNotable ? "_villager_ruralnotable" : (hero.IsFemale ? "_lord" : "_villager_merchant"))))));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpawnCampaignBehavior.cs:163: if (hero.Clan != Clan.PlayerClan && !hero.IsTemplate && hero.IsAlive && !hero.IsNotable && !hero.IsHumanPlayerCharacter && !hero.IsPartyLeader && !hero.IsPrisoner && hero.HeroState != Hero.CharacterStates.Disabled && hero.GovernorOf == null && hero.PartyBelongedTo == null && !hero.IsWanderer && hero.PartyBelongedToAsPrisoner == null && hero.CharacterObject.Occupation != Occupation.Special && hero.Age >= (float)Campaign.Current.Models.AgeModel.HeroComesOfAge && (hero.CurrentSettlement?.Town == null || (!hero.CurrentSettlement.Town.HasTournament && !hero.CurrentSettlement.IsUnderSiege)))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroSpawnCampaignBehavior.cs:395: if (hero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:160: if (issue.Key.IsNotable && issue.Key.CurrentSettlement == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\IssuesCampaignBehavior.cs:412: if (issueSolver.GetPerkValue(DefaultPerks.Trade.LocalConnection) && issue.IssueOwner.IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\LordConversationsCampaignBehavior.cs:125: if (!hero.IsLord && !hero.IsWanderer && !hero.IsMerchant && !hero.IsPreacher && !hero.IsHeadman && !hero.IsArtisan && !hero.IsGangLeader && !hero.IsRuralNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\LordConversationsCampaignBehavior.cs:1538: if (Campaign.Current.ConversationManager.CurrentConversationIsFirst && Hero.OneToOneConversationHero.IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\LordConversationsCampaignBehavior.cs:2792: if (Hero.OneToOneConversationHero == null || Hero.OneToOneConversationHero.IsNotable || Hero.OneToOneConversationHero.IsWanderer || Hero.OneToOneConversationHero.Occupation == Occupation.Special)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotableHelperCharacterCampaignBehavior.cs:61: int characterToSpawnCount4 = settlement.Notables.Count((Hero x) => x.IsMerchant);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablePowerManagementBehavior.cs:24: if (hero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablePowerManagementBehavior.cs:32: if (hero.IsAlive && hero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:62: if (item.IsDead && item.IsNotable && item.DeathDay.ElapsedDaysUntilNow >= 7f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:176: if (allAliveHero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:266: if (hero.IsNotable && hero.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:280: if (notable.OwnedWorkshops.IsEmpty() && notable.OwnedCaravans.IsEmpty() && notable.OwnedAlleys.IsEmpty() && notable.CanDie(KillCharacterAction.KillCharacterActionDetail.Lost) && notable.CanHaveCampaignIssues() && notable.Power < (float)Campaign.Current.Models.NotablePowerModel.NotableDisappearPowerLimit)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:294: for (int num = notable.OwnedCaravans.Count - 1; num >= 0; num--)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:296: CaravanPartyComponent caravanPartyComponent = notable.OwnedCaravans[num];
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:323: if (!victim.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:335: foreach (CaravanPartyComponent item in victim.OwnedCaravans.ToList())
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotablesCampaignBehavior.cs:342: foreach (CaravanPartyComponent item2 in victim.OwnedCaravans.ToList())
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotableSupportersCampaignBehavior.cs:53: if (Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotableSupportersCampaignBehavior.cs:131: if (Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PrisonerCaptureCampaignBehavior.cs:82: if (hero != null && hero != Hero.MainHero && !hero.IsWanderer && !hero.IsNotable && hero.HeroState != Hero.CharacterStates.Prisoner && hero.HeroState != Hero.CharacterStates.Dead && hero.MapFaction != null && hero.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PrisonerReleaseCampaignBehavior.cs:146: foreach (CaravanPartyComponent ownedCaravan in aliveLord.OwnedCaravans)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\PrisonerReleaseCampaignBehavior.cs:153: foreach (CaravanPartyComponent ownedCaravan2 in companion.OwnedCaravans)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RansomOfferCampaignBehavior.cs:119: if (hero.PartyBelongedToAsPrisoner.Owner.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\WorkshopsCharactersCampaignBehavior.cs:109: if (Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.IsNotable && Hero.OneToOneConversationHero.CurrentSettlement == Settlement.CurrentSettlement && Hero.OneToOneConversationHero.OwnedWorkshops.Count((Workshop x) => !x.WorkshopType.IsHidden) == 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\WorkshopsCharactersCampaignBehavior.cs:124: if (Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.IsNotable && Hero.OneToOneConversationHero.CurrentSettlement == Settlement.CurrentSettlement)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.ComponentInterfaces\CaravanModel.cs:18: public abstract bool CanHeroCreateCaravan(Hero hero);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation.Tags\AnyNotableTypeTag.cs:13: return character.HeroObject.IsNotable;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation.Tags\ConversationTagHelper.cs:45: if (character.HeroObject.IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Conversation.Tags\ImpoliteTag.cs:18: if ((character.HeroObject.IsLord || character.HeroObject.IsMerchant || character.HeroObject.IsGangLeader) && Clan.PlayerClan.Renown < 100f && heroRelation < 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\DefaultEncyclopediaHeroPage.cs:131: if (IsValidEncyclopediaItem(hero) && !hero.IsNotable && hero.Age >= (float)comingOfAge)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Encyclopedia.Pages\DefaultEncyclopediaHeroPage.cs:152: if (!IsValidEncyclopediaItem(hero2) || hero2.IsNotable || !(hero2.Age >= (float)comingOfAge))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultCaravanModel.cs:34: public override bool CanHeroCreateCaravan(Hero hero)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultCaravanModel.cs:36: if (hero.IsMerchant && hero.PartyBelongedTo == null && hero.OwnedCaravans.Count((CaravanPartyComponent x) => !x.MobileParty.Ai.IsDisabled) == 0 && hero.IsActive && !hero.IsTemplate)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultClanFinanceModel.cs:652: foreach (CaravanPartyComponent ownedCaravan in aliveLord.OwnedCaravans)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultClanFinanceModel.cs:663: foreach (CaravanPartyComponent ownedCaravan2 in companion.OwnedCaravans)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultClanFinanceModel.cs:727: foreach (CaravanPartyComponent ownedCaravan in aliveLord.OwnedCaravans)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultClanFinanceModel.cs:745: foreach (CaravanPartyComponent ownedCaravan2 in companion.OwnedCaravans)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultClanFinanceModel.cs:877: foreach (CaravanPartyComponent ownedCaravan in hero.OwnedCaravans)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDiplomacyModel.cs:932: num = (hero.IsNotable ? (num * 20f) : ((hero.MapFaction != null && hero.MapFaction.Leader == hero) ? (num * 30f) : ((hero.Clan == null || hero.Clan.Leader != hero) ? (num * 10f) : (num * 20f))));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultDiplomacyModel.cs:934: else if (!hero.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultHeroAgentLocationModel.cs:44: bool flag4 = settlement.HeroesWithoutParty.Contains(hero) && hero.Age >= (float)heroComesOfAge && !hero.IsPrisoner && !hero.IsNotable && ((!hero.IsWanderer && hero.Clan != Clan.PlayerClan) || flag);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultHeroAgentLocationModel.cs:88: if (hero.IsNotable && !hero.IsPartyLeader)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultMarriageModel.cs:145: if (maidenOrSuitor.IsActive && maidenOrSuitor.Spouse == null && maidenOrSuitor.IsLord && !maidenOrSuitor.IsMinorFactionHero && !maidenOrSuitor.IsNotable && !maidenOrSuitor.IsTemplate && maidenOrSuitor.PartyBelongedTo?.MapEvent == null && maidenOrSuitor.PartyBelongedTo?.Army == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultNotablePowerModel.cs:70: if (hero.IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPartySizeLimitModel.cs:152: if (owner != null && owner.IsNotable)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultPlayerProgressionModel.cs:11: return MBMath.ClampFloat((float)Clan.PlayerClan.Fiefs.Count * 0.1f + Clan.PlayerClan.CurrentTotalStrength * 0.0008f + Clan.PlayerClan.Renown * 1.5E-05f + (float)Clan.PlayerClan.AliveLords.Count * 0.002f + (float)Clan.PlayerClan.Companions.Count * 0.01f + (float)Clan.PlayerClan.SupporterNotables.Count * 0.001f + (float)Hero.MainHero.OwnedCaravans.Count * 0.01f + (float)PartyBase.MainParty.NumberOfAllMembers * 0.002f + (float)CharacterObject.PlayerCharacter.Level * 0.002f, 0f, 1f);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultVolunteerModel.cs:26: if (sellerHero.IsMerchant && buyerHero.GetPerkValue(DefaultPerks.Trade.ArtisanCommunity))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Inventory\InventoryLogic.cs:571: if (currentSettlementComponent.Settlement.Notables[i].IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:288: if (notable.IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:336: foreach (Hero item in base.IssueOwner.CurrentSettlement.Notables.Where((Hero x) => x.IsMerchant))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:351: if (notable.IsMerchant)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:369: foreach (Hero item in base.IssueOwner.CurrentSettlement.Notables.Where((Hero x) => x.IsMerchant))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:830: foreach (Hero item in base.QuestGiver.CurrentSettlement.Notables.Where((Hero x) => x.IsMerchant))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:910: foreach (Hero item in base.QuestGiver.CurrentSettlement.Notables.Where((Hero x) => x.IsMerchant))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:932: foreach (Hero item in base.QuestGiver.CurrentSettlement.Notables.Where((Hero x) => x.IsMerchant))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanCantSellProductsAtAFairPriceIssueBehavior.cs:996: return issueGiver.CurrentSettlement.Notables.FirstOrDefault((Hero x) => x.CharacterObject.IsHero && x.CanHaveCampaignIssues() && x.CharacterObject.HeroObject != issueGiver && x.CharacterObject.HeroObject.IsMerchant);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanOverpricedGoodsIssueBehavior.cs:426: private Hero AntagonistHero => base.QuestGiver.CurrentSettlement.Notables.FirstOrDefault((Hero x) => x != base.QuestGiver && x.IsMerchant && x.GetTraitLevel(DefaultTraits.Mercy) <= 0);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\ArtisanOverpricedGoodsIssueBehavior.cs:745: return issueOwner.CurrentSettlement.Notables.GetRandomElementWithPredicate((Hero x) => x != issueOwner && x.IsMerchant && x.GetTraitLevel(DefaultTraits.Mercy) <= 0 && x.CanHaveCampaignIssues());
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CapturedByBountyHuntersIssueBehavior.cs:512: if (issueGiver.IsLord || (issueGiver.IsNotable && issueGiver.CurrentSettlement == null))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:229: if (base.IssueOwner != null && base.IssueOwner.OwnedCaravans.Count > 0)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\CaravanAmbushIssueBehavior.cs:879: if (issueGiver != null && issueGiver.IsNotable && !issueGiver.OwnedCaravans.IsEmpty() && (issueGiver.IsArtisan || issueGiver.IsMerchant) && issueGiver.CurrentSettlement != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:218: if (base.IssueOwner.OwnedCaravans.Count < 2)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1403: if (issueGiver.IsMerchant && issueGiver.CurrentSettlement != null && issueGiver.CurrentSettlement.IsTown && !issueGiver.CurrentSettlement.HasPort && issueGiver.CurrentSettlement.Town.Security <= 50f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1405: return issueGiver.OwnedCaravans.Count < 2;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:192: foreach (Hero item in base.IssueOwner.CurrentSettlement.Notables.WhereQ((Hero notable) => notable.IsMerchant))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:248: CounterOfferHero = base.IssueOwner.CurrentSettlement.Notables.FirstOrDefault((Hero x) => x != base.IssueOwner && x.IsMerchant) ?? base.IssueOwner.CurrentSettlement.Notables.FirstOrDefault();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:723: foreach (Hero item in base.QuestGiver.CurrentSettlement.Notables.WhereQ((Hero notable) => notable.IsMerchant))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.cs:758: foreach (Hero item in base.QuestGiver.CurrentSettlement.Notables.WhereQ((Hero notable) => notable.IsMerchant))

 succeeded in 909ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionRolesCampaignBehavior.cs:280: partyBelongedTo.ActualClan = clan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\DesertersCampaignBehavior.cs:250: banditParty.ActualClan = DeserterClan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\SiegeAftermathCampaignBehavior.cs:825: Clan actualClan = attackerParty.ActualClan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents\DefaultSettlementSecurityModel.cs:272: Clan actualClan = x.ActualClan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Issues\EscortMerchantCaravanIssueBehavior.cs:1267: _questBanditMobileParty.ActualClan = clan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:2375: ActualClan = Owner.Clan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\BanditPartyComponent.cs:160: base.MobileParty.ActualClan = _initializationArgs.Clan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:191: base.MobileParty.ActualClan = Owner.Clan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:212: mobileParty.SetPartyComponent(new CaravanPartyComponent(spawnSettlement, caravanOwner, caravanLeader, isElite, null));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CaravanPartyComponent.cs:246: MobileParty mobileParty = MobileParty.CreateParty("caravan_template_" + spawnSettlement.Culture.StringId.ToLower() + "_1", new CaravanPartyComponent(spawnSettlement, caravanOwner, caravanLeader, isElite, args));
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CustomPartyComponent.cs:51: mobileParty.ActualClan = Clan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\CustomPartyComponent.cs:58: mobileParty.ActualClan = Clan;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party.PartyComponents\LordPartyComponent.cs:120: base.MobileParty.ActualClan = Owner.Clan;

codex
A stronger lifecycle issue is emerging around the global very-far cache: returning vanilla when `Enabled=false` only helps when the cache method runs while disabled. If the user turns the MCM master off after the session already scaled the private fields, I need to verify whether vanilla ever recomputes those fields.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main\\Features\\CaravanTrade\\**\\*.cs','Main\\Features\\CaravanTrade\\*.cs','docs\\features\\caravan-trade.md','Main\\Features\\TaomSettings.cs' -Pattern 'master|Off =|exact vanilla|Apply To Player|CacheVeryFarDistances|ScaleVeryFarDistance|RangeMultiplier|existing saves' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' -Pattern 'CacheVeryFarDistances\\(' | ForEach-Object { '{0}: {1}' -f "'$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign -Recurse -File -Filter *.cs | Select-String -Pattern 'CacheVeryFarDistances\\(' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -File -Include *.cs | Select-String -Pattern 'CacheVeryFarDistances|_defaultCaravanVeryFarCache|_navalCaravanVeryFarCache|ScaleVeryFarDistance' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 678ms:
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:10: /// hard-rejected. Vanilla <c>CacheVeryFarDistances</c> caches a land + naval ceiling; every derived
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:15: [HarmonyPatch(typeof(CaravansCampaignBehavior), "CacheVeryFarDistances")]
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:17: public static class CaravansCampaignBehavior_CacheVeryFarDistances_Patch
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:37: _defaultCache(__instance) = _service.ScaleVeryFarDistance(_defaultCache(__instance));
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:38: _navalCache(__instance) = _service.ScaleVeryFarDistance(_navalCache(__instance));
Main\Features\CaravanTrade\CaravanTradeConfig.cs:14: public float RangeMultiplier { get; set; } = 1.6f;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:14: /// shipped default with a warning, and the master toggle stays effective. Cached (Reuse.Singleton) —
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:64: RangeMultiplier = parsed.RangeMultiplier,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:78: if (!FiniteFloatValidator.IsFiniteInRange(c.RangeMultiplier, 1f, 4f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:80: _logger.LogWarning($"CaravanTradeConfigProvider: rangeMultiplier={c.RangeMultiplier} must be finite in [1,4], reverting to {defaults.RangeMultiplier}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:81: c.RangeMultiplier = defaults.RangeMultiplier;
Main\Features\CaravanTrade\CaravanTradeService.cs:62: public float ScaleVeryFarDistance(float vanillaVeryFarDays)
Main\Features\CaravanTrade\CaravanTradeService.cs:68: return vanillaVeryFarDays * _settings.RangeMultiplier;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:23: public float RangeMultiplier => TaomSettings.Instance?.CaravanRangeMultiplier ?? Cfg.RangeMultiplier;
Main\Features\CaravanTrade\ICaravanTradeService.cs:29: /// player caravan and player-scoping is off), so master-off restores exact vanilla behavior.
Main\Features\CaravanTrade\ICaravanTradeService.cs:55: float ScaleVeryFarDistance(float vanillaVeryFarDays);
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:12: /// <summary>Master toggle. Off ⇒ every service method returns the vanilla value ⇒ exact vanilla behavior.</summary>
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:15: /// <summary>Whether the changes apply to player-owned caravans too (else only NPC caravans change).</summary>
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:19: float RangeMultiplier { get; }
docs\features\caravan-trade.md:18: Mirrors the `ArmyTargeting` precedent (which solves the identical "AI thrashes between close targets + distance-decays away far ones" for besieger armies): thin Harmony postfixes → pure `ICaravanTradeService` → validating config provider + MCM-over-JSON settings. Every service method short-circuits to the vanilla value when the master toggle is off (or when it's a player caravan and `ApplyToPlayerCaravans` is off), so **master-off restores exact vanilla behavior**. No new GameModel file — the diversity overrides live on the already-owned `TaomCaravanModel`.
docs\features\caravan-trade.md:26: | 3 | **Range envelope** | `CacheVeryFarDistances()` postfix (`__instance` + cached `FieldRef`) | Scales the two private `_*VeryFarCache` fields by `RangeMultiplier`. Once per session (no hot-path cost); coherently widens the Close/Med/Far bands + `distanceCut` veto. Field-rename drift → no-op. |
docs\features\caravan-trade.md:31: Levers 1–3 let caravans reach the undersupplied far / same-alignment towns vanilla already prices up to 10× — real market arbitrage, which flows to the owner through the existing `ClanFinance` 10%-of-surplus daily drip. **No `TaomClanFinanceModel` change, no injected gold, no `SyncData`.** The feature is fully **save-clean**: toggles apply to existing saves immediately, and master-off leaves no residue.
docs\features\caravan-trade.md:43: | `enabled` | `true` | — | ✅ master | Off = exact vanilla. |
docs\features\caravan-trade.md:45: | `rangeMultiplier` | `1.6` | [1, 4] | ✅ | Scale of the vanilla "very far" ceiling. |
docs\features\caravan-trade.md:87: - **Retune ranging:** lower `distanceDecayExponent` or raise `rangeMultiplier` (MCM) for more aggressive spreading; raise `nearFieldFlattenDays` to make profit dominate more among near towns.
docs\features\caravan-trade.md:90: - **Revert to vanilla:** MCM master toggle off — exact vanilla, existing saves included.
docs\features\caravan-trade.md:94: All 4 hooks lazy-cache their `IoC.Resolve` (`??=`) and their reflection (`FieldInfo` / `FieldRefAccess`). `GetTradeScoreForTown` runs in the destination argmax loop (per caravan, on re-think — infrequent, not per-frame) and recomputes the distance via `AiHelper`; this was reviewed and **verified cache-backed** — `DefaultMapDistanceModel.GetDistance(MobileParty, Settlement)` serves from the precomputed settlement distance cache (`_navigationCache`), not a live navmesh pathfind, so the recompute is cheap and terrain-accurate (a straight-line proxy was rejected because it would ignore the LOTR map's mountains/water). `CacheVeryFarDistances` fires once per session.
Main\Features\TaomSettings.cs:35: HintText = "Corrects garrison food consumption (Troop Weight no longer inflates it for elite garrisons) and applies the tunable food knobs in settlement_food/settlement_food_config.json (consumption divisors, base/village/flat production, storage caps). Off = vanilla engine food math (garrison food stays weighted). Config edits need an app restart.")]
Main\Features\TaomSettings.cs:42: HintText = "Regenerates town market gold toward a higher target (base 25000 vs vanilla 10000) so drained town markets recover — tunable in settlement_economy/settlement_economy_config.json (base, gold per prosperity, daily regen rate). Applies to existing saves immediately. Off = vanilla engine gold math. Config edits need an app restart.")]
Main\Features\TaomSettings.cs:49: HintText = "Makes AI/player caravans range beyond the local town cluster instead of shuttling between very-close towns, trade across the endless Free-vs-Evil war (per War Trade Policy below), and carry fuller baskets. Off = exact vanilla caravan behavior. Advanced curve knobs live in caravan_trade/caravan_trade_config.json; config edits need an app restart.")]
Main\Features\TaomSettings.cs:53: [SettingPropertyBool("Apply To Player Caravans", Order = 1,
Main\Features\TaomSettings.cs:54: HintText = "When on, your OWN caravans also range further, trade cross-war, and buy fuller baskets. With War Trade Policy above Vanilla they may travel into contested territory and risk being attacked. Off = only NPC caravans change.")]
Main\Features\TaomSettings.cs:60: public float CaravanRangeMultiplier { get; set; } = 1.6f;
Main\Features\TaomSettings.cs:319: HintText = "Master toggle for the main-menu 'Pre-compile Shaders' option. When off, the option is hidden so no NEW walk can be started (a walk already in progress finishes — it is not aborted mid-flight). Takes effect immediately, no relaunch. Default: on.")]
Main\Features\TaomSettings.cs:331: HintText = "Master toggle for the siege auto-dismount feature. When off, sieges behave vanilla (mount stays equipped).")]
Main\Features\TaomSettings.cs:341: HintText = "Show diagnostic [SiegeDismount] messages on the in-game HUD. Off = file log only.")]
Main\Features\TaomSettings.cs:370: HintText = "Master toggle. When off, formations use vanilla positioning. When on, formations with mixed melee + ranged units are reordered per the chosen layout while holding position.")]
Main\Features\TaomSettings.cs:385: HintText = "Show diagnostic [MixedFormations] messages on the in-game HUD. Off = file log only.")]
Main\Features\TaomSettings.cs:392: HintText = "Master toggle. When off, the F6 hotkey is inert and the carousel options are disabled. Effective immediately at runtime. Default: true.")]
Main\Features\TaomSettings.cs:402: HintText = "Write diagnostic [FiefManagement] messages to the TAOM file log. Off = silent.")]
Main\Features\TaomSettings.cs:409: HintText = "Master toggle. When off, inventory 'Sell All' uses vanilla. When on, it opens a 4-option menu.")]
Main\Features\TaomSettings.cs:470: [SettingPropertyBool("Quick Actions Debug Mode", Order = 2, HintText = "Show diagnostic [QuickActions] HUD messages. Off = file log only.")]
Main\Features\TaomSettings.cs:477: HintText = "Master toggle. When off, the Presets overlay is not added to the inventory screen and existing presets are inert (preserved in save).")]
Main\Features\TaomSettings.cs:487: HintText = "Show diagnostic [EquipPresets] messages on the in-game HUD. Off = file log only.")]
Main\Features\TaomSettings.cs:494: HintText = "Master toggle. When off, cavalry uses vanilla charge logic. When on, the player's cavalry formations execute coordinated line charges with passthrough + reform behavior. Default OFF while war-elephant interaction is being tuned.")]
Main\Features\TaomSettings.cs:499: HintText = "When charging, cavalry will reroute around friendly infantry on the charge line. Off = vanilla collision behavior (cavalry trample friendly).")]
Main\Features\TaomSettings.cs:519: HintText = "Show diagnostic [SmartCavalryAI] state-transition messages on the in-game HUD. Off = file log only.")]
Main\Features\TaomSettings.cs:586: HintText = "Master toggle. When off, hideout density + bandit party sizes use vanilla values. When on, both scale with PlayerProgress (0.0 new campaign -> 1.0 endgame) per the curves below.")]
Main\Features\TaomSettings.cs:632: [SettingPropertyBool("Apply To Player", Order = 2,
Main\Features\TaomSettings.cs:633: HintText = "When ON (default), YOU are blocked from recruiting in opposed-alignment settlements. When OFF, you may recruit anyone regardless of alignment (AI lords are still gated if 'Apply To AI Lords' is on). The master 'Enable Recruitment Alignment Block' toggle off disables the whole feature for everyone.")]
Main\Features\TaomSettings.cs:638: HintText = "When ON (default), AI lords are also blocked from recruiting in opposed-alignment settlements. When OFF, AI recruits freely (you are still gated if 'Apply To Player' is on).")]
Main\Features\TaomSettings.cs:649: [SettingPropertyBool("Apply To Player", Order = 1,
Main\Features\TaomSettings.cs:650: HintText = "When ON (default), YOUR party can sail. When OFF, you stay land-bound (AI still sails if 'Apply To AI Lords' is on). The master 'Enable Naval Travel' toggle off disables sailing for everyone.")]
Main\Features\TaomSettings.cs:671: [SettingPropertyBool("Apply To Player", Order = 2,
Main\Features\TaomSettings.cs:677: HintText = "When ON (default), AI lords' parties and garrisons also shed opposed-alignment troops. When OFF, only the player is affected (if 'Apply To Player' is on).")]
Main\Features\TaomSettings.cs:694: HintText = "Master toggle for the TOR-derived combat feel pack: skill-based crush-through, monster/orc crush-through, creature cleave, creature stagger immunity, weight-based charge knockdown, shield penetration, race combat modifiers. When off, everything below is inert and combat behaves exactly as before this feature.")]

 succeeded in 701ms:
402: CacheVeryFarDistances();
407: private void CacheVeryFarDistances()

 succeeded in 845ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:402: CacheVeryFarDistances();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:407: private void CacheVeryFarDistances()

 succeeded in 1185ms:
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:10: /// hard-rejected. Vanilla <c>CacheVeryFarDistances</c> caches a land + naval ceiling; every derived
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:15: [HarmonyPatch(typeof(CaravansCampaignBehavior), "CacheVeryFarDistances")]
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:17: public static class CaravansCampaignBehavior_CacheVeryFarDistances_Patch
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:37: _defaultCache(__instance) = _service.ScaleVeryFarDistance(_defaultCache(__instance));
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:38: _navalCache(__instance) = _service.ScaleVeryFarDistance(_navalCache(__instance));
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:52: _defaultCache = AccessTools.FieldRefAccess<CaravansCampaignBehavior, float>("_defaultCaravanVeryFarCache");
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:53: _navalCache = AccessTools.FieldRefAccess<CaravansCampaignBehavior, float>("_navalCaravanVeryFarCache");
Main\Features\CaravanTrade\CaravanTradeService.cs:62: public float ScaleVeryFarDistance(float vanillaVeryFarDays)
Main\Features\CaravanTrade\ICaravanTradeService.cs:55: float ScaleVeryFarDistance(float vanillaVeryFarDays);

codex
The cache lifecycle is now a confirmed issue: the installed engine calls `CacheVeryFarDistances()` only from `OnSessionLaunched`, and TAOM mutates private cache fields there. A later MCM master-off or range-slider change does not recompute those fields, so the “immediate exact vanilla” claim is false for the range envelope.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "dotnet test TAOM.Tests\\TAOM.Tests.csproj --filter \"FullyQualifiedName~CaravanTrade\" --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-07-04T20:49:00.619471Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.6 seconds
Output:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 578ms:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

codex
The first targeted test run hit the usual sandboxed .NET first-use path issue. I’m retrying with `DOTNET_CLI_HOME` pointed at a writable temp directory; if that still fails, I’ll report the test limitation explicitly.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$env:DOTNET_CLI_HOME='"'C:\\tmp\\dotnet_cli_home'; "'$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; dotnet test TAOM.Tests\\TAOM.Tests.csproj --filter \"FullyQualifiedName~CaravanTrade\" --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-07-04T20:49:06.716459Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
Failed to add 'C:\tmp\dotnet_cli_home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet_cli_home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 545ms:
Failed to add 'C:\tmp\dotnet_cli_home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet_cli_home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

codex
The retry also failed before MSBuild due first-use directory creation permissions, so I’m not going to burn time on the local runner. The review evidence will be source/decompile based, with tests marked not run in this sandbox.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content '"'Main\\Features\\CaravanTrade\\ICaravanTradeSettingsProvider.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Core -Recurse -File -Include *.cs | Select-String -Pattern 'class FiniteFloatValidator|IsFiniteInRange|IsFinite\\(' -Context 0,8 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim(); $_.Context.PostContext }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData -Recurse -File -Include *.xml,*.json | Select-String -Pattern 'id=\"rohan\"|id=\"dol_guldur\"|\"rohan\"|\"dol_guldur\"' | Select-Object -First 120 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1), $_.LineNumber, $_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\_Module\\ModuleData\\execution -File | Select-Object -ExpandProperty FullName; "'$i=1; Get-Content '"'Main\\_Module\\ModuleData\\execution\\alignment.json' | Select-Object -First 80 | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 680ms:
   1: namespace TAOM.Features.CaravanTrade;
   2: 
   3: /// <summary>
   4: /// Single merged read surface for the CaravanTrade feature: MCM-over-JSON. For fields exposed in
   5: /// MCM the implementation reads <c>TaomSettings.Instance?.X</c> and falls back to the validated
   6: /// JSON config; JSON-only advanced fields come straight from the config. The service depends only
   7: /// on this interface (plus <see cref="Execution.IAlignmentService"/> + logger), so all validation
   8: /// and the MCM/JSON merge live in one place.
   9: /// </summary>
  10: public interface ICaravanTradeSettingsProvider
  11: {
  12:     /// <summary>Master toggle. Off ⇒ every service method returns the vanilla value ⇒ exact vanilla behavior.</summary>
  13:     bool Enabled { get; }
  14: 
  15:     /// <summary>Whether the changes apply to player-owned caravans too (else only NPC caravans change).</summary>
  16:     bool ApplyToPlayerCaravans { get; }
  17: 
  18:     /// <summary>Multiplier on the vanilla "very far" distance ceiling — how much further caravans may range.</summary>
  19:     float RangeMultiplier { get; }
  20: 
  21:     /// <summary>Distance-decay exponent (alpha) in <c>1/(nearFieldFlatten+days)^alpha</c>. Lower ⇒ ranges further.</summary>
  22:     float DistanceDecayExponent { get; }
  23: 
  24:     /// <summary>Days added inside the decay denominator so near towns tie on distance and profit decides.</summary>
  25:     float NearFieldFlattenDays { get; }
  26: 
  27:     /// <summary>Upper clamp on the score multiplier so one hyper-profitable far town can't pull caravans map-wide.</summary>
  28:     float MaxCompensation { get; }
  29: 
  30:     /// <summary>Fractional score cut applied to the town a caravan just left (0 = off, 1 = fully deprioritize).</summary>
  31:     float AntiShuttlePenalty { get; }
  32: 
  33:     /// <summary>Resolved war-trade policy (MCM dropdown over validated JSON string).</summary>
  34:     WarTradePolicy WarTradePolicy { get; }
  35: 
  36:     /// <summary>Floor applied to vanilla's per-caravan budget factor so more categories clear the buy gate.</summary>
  37:     float BudgetFactorFloor { get; }
  38: 
  39:     /// <summary>Starting trade-gold floor (vanilla 10000) — higher saturates budgetFactor for fuller baskets.</summary>
  40:     int InitialTradeGold { get; }
  41: 
  42:     /// <summary>Per-item-category gold cap (vanilla 1500).</summary>
  43:     int MaxGoldPerCategory { get; }
  44: }

 succeeded in 719ms:
Main\Core\Validation\FiniteFloatValidator.cs:19: public static class FiniteFloatValidator
{
    /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
    public static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
    public static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
Main\Core\Validation\FiniteFloatValidator.cs:22: public static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
    public static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>
    /// Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).
Main\Core\Validation\FiniteFloatValidator.cs:26: public static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    /// <summary>
    /// Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).
    /// NaN/Infinity always return false. Standard pattern for config range validation.
    /// </summary>
    public static bool IsFiniteInRange(float value, float min, float max) =>
        IsFinite(value) && value >= min && value <= max;
Main\Core\Validation\FiniteFloatValidator.cs:33: public static bool IsFiniteInRange(float value, float min, float max) =>
        IsFinite(value) && value >= min && value <= max;

    /// <summary>
    /// Returns true if <paramref name="value"/> is finite AND less than or equal to <paramref name="max"/>.
    /// Use for penalty fields constrained to be non-positive (e.g., loyalty penalties must be ≤ 0).
    /// NaN/Infinity always return false.
    /// </summary>
    public static bool IsFiniteAtMost(float value, float max) =>
Main\Core\Validation\FiniteFloatValidator.cs:34: IsFinite(value) && value >= min && value <= max;

    /// <summary>
    /// Returns true if <paramref name="value"/> is finite AND less than or equal to <paramref name="max"/>.
    /// Use for penalty fields constrained to be non-positive (e.g., loyalty penalties must be ≤ 0).
    /// NaN/Infinity always return false.
    /// </summary>
    public static bool IsFiniteAtMost(float value, float max) =>
        IsFinite(value) && value <= max;
Main\Core\Validation\FiniteFloatValidator.cs:42: IsFinite(value) && value <= max;

    /// <summary>
    /// Returns true if <paramref name="value"/> is finite AND greater than or equal to <paramref name="min"/>.
    /// Use for bonus fields constrained to be non-negative.
    /// NaN/Infinity always return false.
    /// </summary>
    public static bool IsFiniteAtLeast(float value, float min) =>
        IsFinite(value) && value >= min;
Main\Core\Validation\FiniteFloatValidator.cs:50: IsFinite(value) && value >= min;

    // Phase 9b #126 — double overloads. InitialChildGeneration (and other features that parse
    // JSON via Newtonsoft `Value<double?>()`) need double-typed validation. Same semantics as
    // the float overloads — NaN/Infinity always fail, range is inclusive.

    /// <summary>Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).</summary>
    public static bool IsFiniteInRange(double value, double min, double max) =>
        IsFinite(value) && value >= min && value <= max;
Main\Core\Validation\FiniteFloatValidator.cs:57: public static bool IsFiniteInRange(double value, double min, double max) =>
        IsFinite(value) && value >= min && value <= max;

    /// <summary>Returns true if <paramref name="value"/> is finite AND ≤ <paramref name="max"/>.</summary>
    public static bool IsFiniteAtMost(double value, double max) =>
        IsFinite(value) && value <= max;

    /// <summary>Returns true if <paramref name="value"/> is finite AND ≥ <paramref name="min"/>.</summary>
    public static bool IsFiniteAtLeast(double value, double min) =>
Main\Core\Validation\FiniteFloatValidator.cs:58: IsFinite(value) && value >= min && value <= max;

    /// <summary>Returns true if <paramref name="value"/> is finite AND ≤ <paramref name="max"/>.</summary>
    public static bool IsFiniteAtMost(double value, double max) =>
        IsFinite(value) && value <= max;

    /// <summary>Returns true if <paramref name="value"/> is finite AND ≥ <paramref name="min"/>.</summary>
    public static bool IsFiniteAtLeast(double value, double min) =>
        IsFinite(value) && value >= min;
Main\Core\Validation\FiniteFloatValidator.cs:62: IsFinite(value) && value <= max;

    /// <summary>Returns true if <paramref name="value"/> is finite AND ≥ <paramref name="min"/>.</summary>
    public static bool IsFiniteAtLeast(double value, double min) =>
        IsFinite(value) && value >= min;
}
Main\Core\Validation\FiniteFloatValidator.cs:66: IsFinite(value) && value >= min;
}

 succeeded in 670ms:
C:\Users\mikew\source\repos\TAOM\Main\_Module\ModuleData\execution\alignment.json
   1: {
   2:   "empire_w": "free",
   3:   "gondor": "free",
   4:   "mordor": "evil",
   5:   "empire": "evil",
   6:   "vlandia": "free",
   7:   "erebor": "free",
   8:   "sturgia": "free",
   9:   "rivendell": "free",
  10:   "lothlorien": "free",
  11:   "mirkwood": "free",
  12:   "empire_s": "evil",
  13:   "isengard": "evil",
  14:   "gundabad": "evil",
  15:   "dolguldur": "evil",
  16:   "khuzait": "evil",
  17:   "battania": "neutral",
  18:   "aserai": "evil",
  19:   "shaghana": "neutral",
  20:   "abanissa": "neutral",
  21:   "umbar": "neutral",
  22:   "goblin": "evil",
  23:   "mistymountainorcs": "evil",
  24:   "bluecraig": "evil",
  25:   "lindon": "free"
  26: }

 succeeded in 1005ms:
Main\_Module\ModuleData\Languages\BR\std_taom_module_strings_por-BR.xml:61: <string id="taom_str_faction_informal_name_for_culture.vlandia" text="Rohan" />
Main\_Module\ModuleData\Languages\BR\std_taom_xslt_strings_por-BR.xml:67: <string id="TAOM_rohan" text="Rohan" />
Main\_Module\ModuleData\Languages\BR\std_taom_xslt_strings_por-BR.xml:69: <string id="TAOM_rohan_short" text="Rohan" />
Main\_Module\ModuleData\Languages\DE\std_taom_module_strings_deu-DE.xml:61: <string id="taom_str_faction_informal_name_for_culture.vlandia" text="Rohan" />
Main\_Module\ModuleData\Languages\DE\std_taom_xslt_strings_deu-DE.xml:67: <string id="TAOM_rohan" text="Rohan" />
Main\_Module\ModuleData\Languages\DE\std_taom_xslt_strings_deu-DE.xml:69: <string id="TAOM_rohan_short" text="Rohan" />
Main\_Module\ModuleData\Languages\FR\std_taom_module_strings_fre-FR.xml:61: <string id="taom_str_faction_informal_name_for_culture.vlandia" text="Rohan" />
Main\_Module\ModuleData\Languages\FR\std_taom_xslt_strings_fre-FR.xml:67: <string id="TAOM_rohan" text="Rohan" />
Main\_Module\ModuleData\Languages\FR\std_taom_xslt_strings_fre-FR.xml:69: <string id="TAOM_rohan_short" text="Rohan" />
Main\_Module\ModuleData\Languages\IT\std_taom_module_strings_ita-IT.xml:61: <string id="taom_str_faction_informal_name_for_culture.vlandia" text="Rohan" />
Main\_Module\ModuleData\Languages\IT\std_taom_xslt_strings_ita-IT.xml:67: <string id="TAOM_rohan" text="Rohan" />
Main\_Module\ModuleData\Languages\IT\std_taom_xslt_strings_ita-IT.xml:69: <string id="TAOM_rohan_short" text="Rohan" />
Main\_Module\ModuleData\Languages\PL\std_taom_module_strings_pol-PL.xml:61: <string id="taom_str_faction_informal_name_for_culture.vlandia" text="Rohan" />
Main\_Module\ModuleData\Languages\PL\std_taom_xslt_strings_pol-PL.xml:67: <string id="TAOM_rohan" text="Rohan" />
Main\_Module\ModuleData\Languages\PL\std_taom_xslt_strings_pol-PL.xml:69: <string id="TAOM_rohan_short" text="Rohan" />
Main\_Module\ModuleData\Languages\SP\std_taom_module_strings_spa-LA.xml:61: <string id="taom_str_faction_informal_name_for_culture.vlandia" text="Rohan" />
Main\_Module\ModuleData\Languages\SP\std_taom_xslt_strings_spa-LA.xml:67: <string id="TAOM_rohan" text="Rohan" />
Main\_Module\ModuleData\Languages\SP\std_taom_xslt_strings_spa-LA.xml:69: <string id="TAOM_rohan_short" text="Rohan" />
Main\_Module\ModuleData\Languages\TR\std_taom_module_strings_tur-TR.xml:61: <string id="taom_str_faction_informal_name_for_culture.vlandia" text="Rohan" />
Main\_Module\ModuleData\Languages\TR\std_taom_xslt_strings_tur-TR.xml:67: <string id="TAOM_rohan" text="Rohan" />
Main\_Module\ModuleData\Languages\TR\std_taom_xslt_strings_tur-TR.xml:69: <string id="TAOM_rohan_short" text="Rohan" />

codex
Config ID cross-check found no CaravanTrade config ids at all besides the war-policy strings; the `rohan` occurrences in this feature are only unit-test sample strings, not shipped config data. The alignment file uses `vlandia` for Rohan and `dolguldur` for Dol Guldur.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=392; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 20 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=336; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 30 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=382; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\Hero.cs' | Select-Object -Skip ("'$start-1) -First 20 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=284; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\NotablesCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 68 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 678ms:
 392: 					SellGoods(allCaravanParty, town, 0.7f);
 393: 					BuyGoods(allCaravanParty, town);
 394: 					SellGoods(allCaravanParty, town2, 0.7f);
 395: 				}
 396: 			}
 397: 		}
 398: 	}
 399: 
 400: 	public void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
 401: 	{
 402: 		CacheVeryFarDistances();
 403: 		AddDialogs(campaignGameStarter);
 404: 		UpdateAverageValues();
 405: 	}
 406: 
 407: 	private void CacheVeryFarDistances()
 408: 	{
 409: 		MobileParty.NavigationType navigationType = MobileParty.NavigationType.Naval;
 410: 		float num = 20f;
 411: 		float num2 = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigationType) * num;

 succeeded in 678ms:
 336: 	private void OnNewGameCreatedPartialFollowUpEndEvent(CampaignGameStarter obj)
 337: 	{
 338: 		for (int i = 0; i < 2; i++)
 339: 		{
 340: 			foreach (Hero allAliveHero in Hero.AllAliveHeroes)
 341: 			{
 342: 				if (allAliveHero.Clan != Clan.PlayerClan && Campaign.Current.Models.CaravanModel.CanHeroCreateCaravan(allAliveHero))
 343: 				{
 344: 					SpawnCaravan(allAliveHero, initialSpawn: true);
 345: 				}
 346: 			}
 347: 			UpdateAverageValues();
 348: 			DoInitialTradeRuns();
 349: 		}
 350: 	}
 351: 
 352: 	public override void SyncData(IDataStore dataStore)
 353: 	{
 354: 		dataStore.SyncData("_tradeRumorTakenCaravans", ref _tradeRumorTakenCaravans);
 355: 		dataStore.SyncData("_lootedCaravans", ref _lootedCaravans);
 356: 		dataStore.SyncData("_interactedCaravans", ref _interactedCaravans);
 357: 		dataStore.SyncData("_tradeActionLogs", ref _tradeActionLogs);
 358: 		dataStore.SyncData("_caravanLastHomeTownVisitTime", ref _caravanLastHomeTownVisitTime);
 359: 		dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
 360: 	}
 361: 
 362: 	private void DoInitialTradeRuns()
 363: 	{
 364: 		foreach (MobileParty allCaravanParty in MobileParty.AllCaravanParties)
 365: 		{

 succeeded in 654ms:
 382: 			}
 383: 			return false;
 384: 		}
 385: 	}
 386: 
 387: 	public bool IsNotable
 388: 	{
 389: 		get
 390: 		{
 391: 			if (!IsArtisan && !IsGangLeader && !IsPreacher && !IsMerchant && !IsRuralNotable)
 392: 			{
 393: 				return IsHeadman;
 394: 			}
 395: 			return true;
 396: 		}
 397: 	}
 398: 
 399: 	public bool IsLord => Occupation == Occupation.Lord;
 400: 
 401: 	public int MaxHitPoints => CharacterObject.MaxHitPoints();

 succeeded in 690ms:
 284: 			if (randomFloat < notableDisappearProbability)
 285: 			{
 286: 				KillCharacterAction.ApplyByRemove(notable);
 287: 				notable.Issue?.CompleteIssueWithAiLord(notable.CurrentSettlement.OwnerClan.Leader);
 288: 			}
 289: 		}
 290: 	}
 291: 
 292: 	private void ManageCaravanExpensesOfNotable(Hero notable)
 293: 	{
 294: 		for (int num = notable.OwnedCaravans.Count - 1; num >= 0; num--)
 295: 		{
 296: 			CaravanPartyComponent caravanPartyComponent = notable.OwnedCaravans[num];
 297: 			int totalWage = caravanPartyComponent.MobileParty.TotalWage;
 298: 			if (caravanPartyComponent.MobileParty.PartyTradeGold >= totalWage)
 299: 			{
 300: 				caravanPartyComponent.MobileParty.PartyTradeGold -= totalWage;
 301: 			}
 302: 			else
 303: 			{
 304: 				int num2 = TaleWorlds.Library.MathF.Min(totalWage, notable.Gold);
 305: 				notable.Gold -= num2;
 306: 			}
 307: 			if (caravanPartyComponent.MobileParty.PartyTradeGold < 5000)
 308: 			{
 309: 				int num3 = TaleWorlds.Library.MathF.Min(5000 - caravanPartyComponent.MobileParty.PartyTradeGold, notable.Gold);
 310: 				caravanPartyComponent.MobileParty.PartyTradeGold += num3;
 311: 				notable.Gold -= num3;
 312: 			}
 313: 		}
 314: 	}
 315: 
 316: 	private float GetNotableDisappearProbability(Hero hero)
 317: 	{
 318: 		return ((float)Campaign.Current.Models.NotablePowerModel.NotableDisappearPowerLimit - hero.Power) / (float)Campaign.Current.Models.NotablePowerModel.NotableDisappearPowerLimit * 0.02f;
 319: 	}
 320: 
 321: 	private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
 322: 	{
 323: 		if (!victim.IsNotable)
 324: 		{
 325: 			return;
 326: 		}
 327: 		if (victim.Power >= (float)Campaign.Current.Models.NotablePowerModel.NotableDisappearPowerLimit)
 328: 		{
 329: 			Hero hero = HeroCreator.CreateRelativeNotableHero(victim);
 330: 			if (victim.CurrentSettlement != null)
 331: 			{
 332: 				ChangeDeadNotable(victim, hero, victim.CurrentSettlement);
 333: 			}
 334: 			{
 335: 				foreach (CaravanPartyComponent item in victim.OwnedCaravans.ToList())
 336: 				{
 337: 					CaravanPartyComponent.TransferCaravanOwnership(item.MobileParty, hero, hero.CurrentSettlement);
 338: 				}
 339: 				return;
 340: 			}
 341: 		}
 342: 		foreach (CaravanPartyComponent item2 in victim.OwnedCaravans.ToList())
 343: 		{
 344: 			DestroyPartyAction.Apply(null, item2.MobileParty);
 345: 		}
 346: 	}
 347: 
 348: 	private void ChangeDeadNotable(Hero deadNotable, Hero newNotable, Settlement notableSettlement)
 349: 	{
 350: 		EnterSettlementAction.ApplyForCharacterOnly(newNotable, notableSettlement);
 351: 		foreach (Hero allAliveHero in Hero.AllAliveHeroes)

codex
I’m checking one more classification edge from prior TAOM reviews: dynamically created player kingdoms. `AlignmentService` defaults any unknown id to `Neutral`, which is deliberately permissive for CaravanTrade; I need to decide whether that is an accepted neutral fallback or an unintended player-founded kingdom free-pass.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign -Recurse -File -Filter KingdomManager.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\**\\*.cs' -Pattern 'new_kingdom|CreateKingdom|Player.*Kingdom|Kingdom\\.CreateKingdom' | Select-Object -First 120 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Features Main\\_Module\\ModuleData -Recurse -File -Include *.cs,*.json,*.xml | Select-String -Pattern 'new_kingdom|PlayerFounded|GetKingdomSide\\(|AreSameAlignment|AreEnemyAlignments' | Select-Object -First 120 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1),$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 683ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\KingdomManager.cs

 succeeded in 803ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:138: if (all != null && all.Any((Kingdom x) => x != Clan.PlayerClan.Kingdom && string.Equals(x.Name.ToString(), name, StringComparison.InvariantCultureIgnoreCase)))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:666: public static bool CanPlayerOfferMercenaryService(Kingdom offerKingdom, out List<IFaction> playerWars, out List<IFaction> warsOfFactionToJoin)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:673: if (Clan.PlayerClan.MapFaction.IsAtWarWith(item) && item.CurrentTotalStrength > strengthThresholdForNonMutualWarsToBeIgnoredToJoinKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:685: if (Clan.PlayerClan.Kingdom == null && !Clan.PlayerClan.IsAtWarWith(offerKingdom) && Clan.PlayerClan.Tier >= Campaign.Current.Models.ClanTierModel.MercenaryEligibleTier && offerKingdom.Leader.GetRelationWithPlayer() >= (float)Campaign.Current.Models.DiplomacyModel.MinimumRelationWithConversationCharacterToJoinKingdom && warsOfFactionToJoin.Intersect(playerWars).Count() == playerWars.Count)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:692: public static bool CanPlayerOfferVassalage(Kingdom offerKingdom, out List<IFaction> playerWars, out List<IFaction> warsOfFactionToJoin)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:699: if (Clan.PlayerClan.MapFaction.IsAtWarWith(item) && item.CurrentTotalStrength > strengthThresholdForNonMutualWarsToBeIgnoredToJoinKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\FactionHelper.cs:711: if ((Clan.PlayerClan.Kingdom == null || Clan.PlayerClan.IsUnderMercenaryService) && !Clan.PlayerClan.IsAtWarWith(offerKingdom) && Clan.PlayerClan.Tier >= Campaign.Current.Models.ClanTierModel.VassalEligibleTier && !offerKingdom.IsEliminated && offerKingdom.Leader.GetRelationWithPlayer() >= (float)Campaign.Current.Models.DiplomacyModel.MinimumRelationWithConversationCharacterToJoinKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1581: [CommandLineFunctionality.CommandLineArgumentFunction("activate_all_policies_for_player_kingdom", "campaign")]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1590: return "Format is \"campaign.activate_all_policies_for_player_kingdom";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1592: if (Clan.PlayerClan.Kingdom != null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1594: Kingdom kingdom = Clan.PlayerClan.Kingdom;
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1602: return "All policies are now active for player kingdom.";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1604: return "Player is not in a kingdom.";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1976: [CommandLineFunctionality.CommandLineArgumentFunction("create_player_kingdom", "campaign")]
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1977: public static string CreatePlayerKingdom(List<string> strings)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1985: return "Format is \"campaign.create_player_kingdom\".";
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:1987: Campaign.Current.KingdomManager.CreateKingdom(Clan.PlayerClan.Name, Clan.PlayerClan.InformalName, Clan.PlayerClan.Culture, Clan.PlayerClan);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2044: if (Clan.PlayerClan.Kingdom == null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignCheats.cs:2048: ChangeRulingClanAction.Apply(Clan.PlayerClan.Kingdom, Clan.PlayerClan);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:963: public override void OnVassalOrMercenaryServiceOfferedToPlayer(Kingdom offeredKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:968: eventReceivers[i].OnVassalOrMercenaryServiceOfferedToPlayer(offeredKingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventReceiver.cs:421: public virtual void OnVassalOrMercenaryServiceOfferedToPlayer(Kingdom offeredKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:237: private readonly MbEvent<Kingdom> _onVassalOrMercenaryServiceOfferedToPlayerEvent = new MbEvent<Kingdom>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1933: public override void OnVassalOrMercenaryServiceOfferedToPlayer(Kingdom offeredKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1935: Instance._onVassalOrMercenaryServiceOfferedToPlayerEvent.Invoke(offeredKingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:550: public static Kingdom CreateKingdom(string stringID)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:703: bool isPlayerInvolved = kingdomDecision.DetermineChooser().Leader.IsHumanPlayerCharacter || kingdomDecision.DetermineSupporters().Any((Supporter x) => x.IsPlayer);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\Kingdom.cs:705: if (kingdomDecision.Kingdom != Clan.PlayerClan.Kingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\KingdomManager.cs:124: public void CreateKingdom(TextObject kingdomName, TextObject informalName, CultureObject culture, Clan founderClan, MBReadOnlyList<PolicyObject> initialPolicies = null, TextObject encyclopediaText = null, TextObject encyclopediaTitle = null, TextObject encyclopediaRulerTitle = null)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\KingdomManager.cs:126: Kingdom kingdom = Kingdom.CreateKingdom("new_kingdom");
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\KingdomManager.cs:147: ChangeKingdomAction.ApplyByCreateKingdom(founderClan, kingdom, showNotification: false);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeKingdomAction.cs:21: CreateKingdom,
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeKingdomAction.cs:52: case ChangeKingdomActionDetail.CreateKingdom:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeKingdomAction.cs:61: if (newKingdom != null && detail == ChangeKingdomActionDetail.CreateKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeKingdomAction.cs:175: public static void ApplyByCreateKingdom(Clan clan, Kingdom newKingdom, bool showNotification = true)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Actions\ChangeKingdomAction.cs:177: ApplyInternal(clan, newKingdom, ChangeKingdomActionDetail.CreateKingdom, CampaignTime.Zero, 0, byRebellion: false, showNotification);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.BarterSystem.Barterables\JoinKingdomAsClanBarterable.cs:59: if (TargetKingdom == Clan.PlayerClan.Kingdom && Hero.MainHero.GetPerkValue(DefaultPerks.Trade.SilverTongue))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:152: public void OnAllianceOfferedToPlayer(Kingdom offeringKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:154: if (Clan.PlayerClan.Kingdom.Clans.Count == 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:181: public void OnAllianceOfferedToPlayerKingdom(Kingdom offeringKingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:183: if (Clan.PlayerClan.Kingdom.Clans.Count == 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:191: AddAllianceDecision(Clan.PlayerClan.Kingdom, offeringKingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:195: public void OnCallToWarAgreementProposedToPlayer(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:197: if (Clan.PlayerClan.Kingdom.Clans.Count == 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:199: int callToWarCost = Campaign.Current.Models.AllianceModel.GetCallToWarCost(proposerKingdom, Clan.PlayerClan.Kingdom, kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:212: StartCallToWarAgreement(proposerKingdom, Clan.PlayerClan.Kingdom, kingdomToCallToWarAgainst, callToWarCost);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:215: DenyCallToWarAgreement(proposerKingdom, Clan.PlayerClan.Kingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:232: public void OnCallToWarAgreementProposedToPlayerKingdom(Kingdom proposerKingdom, Kingdom kingdomToCallToWarAgainst)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:234: if (Clan.PlayerClan.Kingdom.Clans.Count == 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:242: KingdomDecision kingdomDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision s) => s is AcceptCallToWarAgreementDecision acceptCallToWarAgreementDecision && acceptCallToWarAgreementDecision.CallingKingdom == proposerKingdom && acceptCallToWarAgreementDecision.KingdomToCallToWarAgainst == kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:245: Clan.PlayerClan.Kingdom.RemoveDecision(kingdomDecision);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:247: AcceptCallToWarAgreementDecision kingdomDecision2 = new AcceptCallToWarAgreementDecision(Clan.PlayerClan, proposerKingdom, kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:248: Clan.PlayerClan.Kingdom.AddDecision(kingdomDecision2, ignoreInfluenceCost: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:251: public void OnCallToWarAgreementProposedByPlayer(Kingdom proposedKingdom, Kingdom kingdomToCallToWarAgainst)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:253: if (Clan.PlayerClan.Kingdom.Clans.Count == 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:255: int callToWarCost = Campaign.Current.Models.AllianceModel.GetCallToWarCost(Clan.PlayerClan.Kingdom, proposedKingdom, kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:270: StartCallToWarAgreement(Clan.PlayerClan.Kingdom, proposedKingdom, kingdomToCallToWarAgainst, callToWarCost);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:298: public void OnCallToWarAgreementProposedByPlayerKingdom(Kingdom proposedKingdom, Kingdom kingdomToCallToWarAgainst)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:300: if (Clan.PlayerClan.Kingdom.Clans.Count == 1)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:308: KingdomDecision kingdomDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision s) => s is ProposeCallToWarAgreementDecision proposeCallToWarAgreementDecision && proposeCallToWarAgreementDecision.CalledKingdom == proposedKingdom && proposeCallToWarAgreementDecision.KingdomToCallToWarAgainst == kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:311: Clan.PlayerClan.Kingdom.RemoveDecision(kingdomDecision);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:313: ProposeCallToWarAgreementDecision kingdomDecision2 = new ProposeCallToWarAgreementDecision(Clan.PlayerClan, proposedKingdom, kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:314: Clan.PlayerClan.Kingdom.AddDecision(kingdomDecision2, ignoreInfluenceCost: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:346: if (proposerKingdom == Clan.PlayerClan.Kingdom && !Hero.MainHero.Clan.IsUnderMercenaryService)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:348: OnCallToWarAgreementProposedByPlayerKingdom(receiverKingdom, (Kingdom)item);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:356: if (receiverKingdom == Clan.PlayerClan.Kingdom && !Hero.MainHero.Clan.IsUnderMercenaryService)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:358: OnCallToWarAgreementProposedByPlayerKingdom(proposerKingdom, (Kingdom)item2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:533: StartAlliance(proposerKingdom, Clan.PlayerClan.Kingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:544: AddAllianceDecision(Clan.PlayerClan.Kingdom, proposerKingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:550: ProposeCallToWarAgreementDecision proposeCallToWarAgreementDecision = new ProposeCallToWarAgreementDecision(Clan.PlayerClan, proposedKingdom, kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:554: StartCallToWarAgreement(Clan.PlayerClan.Kingdom, proposedKingdom, kingdomToCallToWarAgainst, callToWarCost);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:557: KingdomDecision kingdomDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision s) => s is ProposeCallToWarAgreementDecision proposeCallToWarAgreementDecision2 && proposeCallToWarAgreementDecision2.CalledKingdom == proposedKingdom && proposeCallToWarAgreementDecision2.KingdomToCallToWarAgainst == kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:560: Clan.PlayerClan.Kingdom.RemoveDecision(kingdomDecision);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:562: Clan.PlayerClan.Kingdom.AddDecision(proposeCallToWarAgreementDecision, ignoreInfluenceCost: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:567: AcceptCallToWarAgreementDecision acceptCallToWarAgreementDecision = new AcceptCallToWarAgreementDecision(Clan.PlayerClan, proposerKingdom, kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:571: StartCallToWarAgreement(proposerKingdom, Clan.PlayerClan.Kingdom, kingdomToCallToWarAgainst, callToWarCost);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:574: KingdomDecision kingdomDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision s) => s is AcceptCallToWarAgreementDecision acceptCallToWarAgreementDecision2 && acceptCallToWarAgreementDecision2.CallingKingdom == proposerKingdom && acceptCallToWarAgreementDecision2.KingdomToCallToWarAgainst == kingdomToCallToWarAgainst);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:577: Clan.PlayerClan.Kingdom.RemoveDecision(kingdomDecision);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:579: Clan.PlayerClan.Kingdom.AddDecision(acceptCallToWarAgreementDecision, ignoreInfluenceCost: true);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:584: Hero obj = ((detail == DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility) ? Hero.MainHero : kingdom.Leader);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:657: if (kingdom == Clan.PlayerClan.Kingdom)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:687: if (kingdom == Clan.PlayerClan.Kingdom && !Hero.MainHero.Clan.IsUnderMercenaryService)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:689: OnCallToWarAgreementProposedByPlayerKingdom(item, kingdom2);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:700: if (kingdom2 == Clan.PlayerClan.Kingdom && !Hero.MainHero.Clan.IsUnderMercenaryService)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\AllianceCampaignBehavior.cs:702: OnCallToWarAgreementProposedByPlayerKingdom(item2, kingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:224: private List<Kingdom> _prohibitedKingdomsForPlayerCaravans = new List<Kingdom>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:299: if (_prohibitedKingdomsForPlayerCaravans.Contains(destroyedKingdom))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:301: _prohibitedKingdomsForPlayerCaravans.Remove(destroyedKingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:359: dataStore.SyncData("_prohibitedKingdomsForPlayerCaravans", ref _prohibitedKingdomsForPlayerCaravans);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1435: foreach (Kingdom suitableKingdomsAsTradePartnerForPlayerCaravan in GetSuitableKingdomsAsTradePartnerForPlayerCaravans())
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1465: starter.AddPlayerLine("caravan_companion_prohibit_kingdoms", "caravan_companion_talk_start_reply", "caravan_companion_prohibit_kingdoms_selected", "{=5LhfbFpX}Let's discuss our trade partners.", caravan_companion_prohibit_kingdoms_on_condition, caravan_companion_prohibit_kingdoms_on_consequence, 100, caravan_companion_prohibit_kingdoms_clickable_condition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1473: starter.AddRepeatablePlayerLine("caravan_companion_ask_change_home_settlement_2", "caravan_companion_ask_change_home_settlement_2", "caravan_companion_ask_change_home_settlement_3", "{=!}{KINGDOM_NAME}", "{=bKqka5Uj}I am thinking of a different realm.", "caravan_companion_ask_change_home_settlement", caravan_companion_ask_change_home_settlement_3_on_condition, caravan_companion_ask_change_home_settlement_3_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1480: starter.AddRepeatablePlayerLine("caravan_companion_prohibit_kingdoms_selected_2", "caravan_companion_prohibit_kingdoms_selected_2", "caravan_companion_prohibit_kingdoms_selected", "{=!}{CONTINUE_OR_STOP_TRADE}", "{=bKqka5Uj}I am thinking of a different realm.", "caravan_companion_prohibit_kingdoms_selected", caravan_companion_prohibit_kingdoms_selected_2_on_condition, caravan_companion_prohibit_kingdoms_selected_2_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1481: starter.AddPlayerLine("caravan_companion_prohibit_kingdoms_selected_cancel", "caravan_companion_prohibit_kingdoms_selected_2", "lord_pretalk", "{=FM7YZaOa}Alright, that is all.", null, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1568: starter.AddDialogLine("caravan_warn_player_to_take_prisoner", "caravan_taken_prisoner_warning_check", "caravan_taken_prisoner_warning_answer", "{=NuYzgBZB}You are going too far. The {KINGDOM} won't stand for the destruction of its caravans.", conversation_warn_player_on_condition, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1683: bool num = _prohibitedKingdomsForPlayerCaravans.Contains(kingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1696: bool num = _prohibitedKingdomsForPlayerCaravans.Contains(kingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1700: _prohibitedKingdomsForPlayerCaravans.Remove(kingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CaravansCampaignBehavior.cs:1704: _prohibitedKingdomsForPlayerCaravans.Add(kingdom);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:186: if (item2 != Clan.PlayerClan && item2.Leader != null && item2.Leader.MapFaction != null && item2.Leader.MapFaction.IsKingdomFaction && item2.Renown > 0f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:395: if (clan != Clan.PlayerClan && clan.IsUnderMercenaryService && clan.Kingdom != null && clan.Kingdom.Leader != Hero.MainHero && MBRandom.RandomFloat < 0.1f)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:399: if (clan == Clan.PlayerClan && clan.IsUnderMercenaryService && clan.Kingdom != null && Campaign.CurrentTime > Campaign.Current.KingdomManager.PlayerMercenaryServiceNextRenewalDay)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\ClanVariablesCampaignBehavior.cs:404: if (clan != Clan.PlayerClan && clan.IsUnderMercenaryService && clan.Kingdom != null && clan.Kingdom.RulingClan.DebtToKingdom > 10000 && MBRandom.RandomFloat < 0.25f && clan.ShouldStayInKingdomUntil.IsPast)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CompanionRolesCampaignBehavior.cs:173: if (oneToOneConversationHero != null && oneToOneConversationHero.IsPlayerCompanion && Hero.MainHero.IsKingdomLeader)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:114: if (IsCriminalPlayerInSameKingdomOf(faction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:128: if (IsCriminalPlayerInSameKingdomOf(faction))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:153: private static bool IsCriminalPlayerInSameKingdomOf(IFaction faction)
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\CrimeCampaignBehavior.cs:327: return IsCriminalPlayerInSameKingdomOf(Settlement.CurrentSettlement.MapFaction);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\EncounterGameMenuBehavior.cs:3175: args.MenuContext.SetBackgroundMeshName(PlayerEncounter.EncounteredMobileParty.Army.Kingdom.Culture.EncounterBackgroundMesh);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\FactionDiscontinuationCampaignBehavior.cs:55: if (clan == Clan.PlayerClan && oldKingdom != null && CanKingdomBeDiscontinued(oldKingdom))
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\FactionDiscontinuationCampaignBehavior.cs:71: bool result = !kingdom.IsEliminated && kingdom != Clan.PlayerClan.Kingdom && kingdom.Settlements.IsEmpty();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GovernorCampaignBehavior.cs:17: private List<CultureObject> _availablePlayerKingdomCultures = new List<CultureObject>();
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GovernorCampaignBehavior.cs:106: starter.AddPlayerLine("governor_talk_kingdom_creation_start", "hero_main_options", "governor_kingdom_creation_reply", "{=EKuB6Ohf}It is time to take a momentous step... It is time to proclaim a new kingdom.", governor_talk_kingdom_creation_start_on_condition, governor_talk_kingdom_creation_start_on_consequence, 200, governor_talk_kingdom_creation_start_clickable_condition);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GovernorCampaignBehavior.cs:109: starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_option", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_culture_selected", "{CULTURE_OPTION_0}", governor_talk_kingdom_creation_culture_option_0_on_condition, governor_talk_kingdom_creation_culture_option_0_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GovernorCampaignBehavior.cs:110: starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_option_2", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_culture_selected", "{CULTURE_OPTION_1}", governor_talk_kingdom_creation_culture_option_1_on_condition, governor_talk_kingdom_creation_culture_option_1_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GovernorCampaignBehavior.cs:111: starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_option_3", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_culture_selected", "{CULTURE_OPTION_2}", governor_talk_kingdom_creation_culture_option_2_on_condition, governor_talk_kingdom_creation_culture_option_2_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GovernorCampaignBehavior.cs:112: starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_other", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_culture_selection", "{=kcuNzSvf}I have another people in mind.", governor_talk_kingdom_creation_culture_other_on_condition, governor_talk_kingdom_creation_culture_other_on_consequence);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GovernorCampaignBehavior.cs:113: starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_cancel", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_exit", "{=hbzs5tLd}On second thought, perhaps now is not the right time.", null, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GovernorCampaignBehavior.cs:116: starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selected_player_reply", "governor_kingdom_creation_culture_selected_confirmation", "governor_kingdom_creation_name_selection", "{=dzXaXKaC}Very well.", null, null);
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GovernorCampaignBehavior.cs:117: starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selected_player_reply_2", "governor_kingdom_creation_culture_selected_confirmation", "governor_kingdom_creation_culture_selection", "{=kTjsx8gN}Perhaps we should choose another set of laws and customs.", null, null);

2026-07-04T20:50:13.581024Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path Main\Features Main\_Module\ModuleData -Recurse -F[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCould not find a part of the path 'C:\Users\mikew\source\repos\TAOM\Main\Features\Main\_Module'.[0m

 exited 1 in 736ms:
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path Main\Features Main\_Module\ModuleData -Recurse -F[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCould not find a part of the path 'C:\Users\mikew\source\repos\TAOM\Main\Features\Main\_Module'.[0m

codex
The player-kingdom edge is real at the data layer: vanilla creates player kingdoms with `StringId` `new_kingdom`, and `alignment.json` has no such row. I’ll include it as an observation/finding only if I can tie it to the feature’s policy contract, not merely because the fallback exists.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=118; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\KingdomManager.cs' | Select-Object -Skip ("'$start-1) -First 38 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=100; $i=$start; Get-Content '"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\GovernorCampaignBehavior.cs' | Select-Object -Skip ("'$start-1) -First 95 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$paths=@('"'Main\\Features','Main\\_Module\\ModuleData'); Get-ChildItem -Path "'$paths -Recurse -File -Include *.cs,*.json,*.xml | Select-String -Pattern '"'new_kingdom|PlayerFounded|GetKingdomSide\\(|AreSameAlignment|AreEnemyAlignments' | Select-Object -First 120 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1),$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 649ms:
 118: 
 119: 	private void DailyTickKingdom(Kingdom kingdom)
 120: 	{
 121: 		kingdom.Aggressiveness += -1f;
 122: 	}
 123: 
 124: 	public void CreateKingdom(TextObject kingdomName, TextObject informalName, CultureObject culture, Clan founderClan, MBReadOnlyList<PolicyObject> initialPolicies = null, TextObject encyclopediaText = null, TextObject encyclopediaTitle = null, TextObject encyclopediaRulerTitle = null)
 125: 	{
 126: 		Kingdom kingdom = Kingdom.CreateKingdom("new_kingdom");
 127: 		if (encyclopediaTitle == null)
 128: 		{
 129: 			encyclopediaTitle = new TextObject("{=ZOEamqUd}Kingdom of {NAME}");
 130: 			encyclopediaTitle.SetTextVariable("NAME", founderClan.Name);
 131: 		}
 132: 		if (encyclopediaText == null)
 133: 		{
 134: 			encyclopediaText = ((!founderClan.IsRebelClan) ? new TextObject("{=21yUheIy}The {KINGDOM_NAME} was created in {CREATION_YEAR} by {RULER.NAME}, a rising {CULTURE_ADJECTIVE} warlord.") : new TextObject("{=drZC1Frp}The {KINGDOM_NAME} was created in {CREATION_YEAR} by {RULER.NAME}, leader of a group of {CULTURE_ADJECTIVE} rebels."));
 135: 			encyclopediaText.SetTextVariable("KINGDOM_NAME", encyclopediaTitle);
 136: 			encyclopediaText.SetTextVariable("CREATION_YEAR", CampaignTime.Now.GetYear);
 137: 			encyclopediaText.SetTextVariable("CULTURE_ADJECTIVE", FactionHelper.GetAdjectiveForFactionCulture(culture));
 138: 			StringHelpers.SetCharacterProperties("RULER", founderClan.Leader.CharacterObject, encyclopediaText);
 139: 		}
 140: 		if (encyclopediaRulerTitle == null)
 141: 		{
 142: 			Kingdom kingdom2 = Kingdom.All.FirstOrDefault((Kingdom x) => x.Culture == culture);
 143: 			encyclopediaRulerTitle = ((kingdom2 != null) ? kingdom2.EncyclopediaRulerTitle : TextObject.GetEmpty());
 144: 		}
 145: 		kingdom.InitializeKingdom(kingdomName, informalName, culture, founderClan.Banner, founderClan.Color, founderClan.Color2, founderClan.HomeSettlement, encyclopediaText, encyclopediaTitle, encyclopediaRulerTitle);
 146: 		List<IFaction> list = new List<IFaction>(founderClan.FactionsAtWarWith.WhereQ((IFaction x) => !Campaign.Current.Models.DiplomacyModel.GetShallowDiplomaticStance(x, founderClan).HasValue));
 147: 		ChangeKingdomAction.ApplyByCreateKingdom(founderClan, kingdom, showNotification: false);
 148: 		foreach (IFaction item in list)
 149: 		{
 150: 			DeclareWarAction.ApplyByKingdomCreation(kingdom, item);
 151: 		}
 152: 		if (initialPolicies != null)
 153: 		{
 154: 			foreach (PolicyObject initialPolicy in initialPolicies)
 155: 			{

 succeeded in 644ms:
 100: 	}
 101: 
 102: 	private void AddDialogs(CampaignGameStarter starter)
 103: 	{
 104: 		starter.AddPlayerLine("governor_talk_start", "hero_main_options", "governor_talk_start_reply", "{=zBo78JQb}How are things doing here in {GOVERNOR_SETTLEMENT}?", governor_talk_start_on_condition, null);
 105: 		starter.AddDialogLine("governor_talk_start_reply", "governor_talk_start_reply", "lord_pretalk", "{=!}{SETTLEMENT_DESCRIPTION}", governor_talk_start_reply_on_condition, null, 200);
 106: 		starter.AddPlayerLine("governor_talk_kingdom_creation_start", "hero_main_options", "governor_kingdom_creation_reply", "{=EKuB6Ohf}It is time to take a momentous step... It is time to proclaim a new kingdom.", governor_talk_kingdom_creation_start_on_condition, governor_talk_kingdom_creation_start_on_consequence, 200, governor_talk_kingdom_creation_start_clickable_condition);
 107: 		starter.AddDialogLine("governor_talk_kingdom_creation_reply", "governor_kingdom_creation_reply", "governor_kingdom_creation_culture_selection", "{=ZyNjXUHc}I am at your command.", null, null);
 108: 		starter.AddDialogLine("governor_talk_kingdom_creation_culture_selection", "governor_kingdom_creation_culture_selection", "governor_kingdom_creation_culture_selection_options", "{=jxEVSu98}The language of our documents, and our customary laws... Whose should we use?", null, null);
 109: 		starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_option", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_culture_selected", "{CULTURE_OPTION_0}", governor_talk_kingdom_creation_culture_option_0_on_condition, governor_talk_kingdom_creation_culture_option_0_on_consequence);
 110: 		starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_option_2", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_culture_selected", "{CULTURE_OPTION_1}", governor_talk_kingdom_creation_culture_option_1_on_condition, governor_talk_kingdom_creation_culture_option_1_on_consequence);
 111: 		starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_option_3", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_culture_selected", "{CULTURE_OPTION_2}", governor_talk_kingdom_creation_culture_option_2_on_condition, governor_talk_kingdom_creation_culture_option_2_on_consequence);
 112: 		starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_other", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_culture_selection", "{=kcuNzSvf}I have another people in mind.", governor_talk_kingdom_creation_culture_other_on_condition, governor_talk_kingdom_creation_culture_other_on_consequence);
 113: 		starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selection_cancel", "governor_kingdom_creation_culture_selection_options", "governor_kingdom_creation_exit", "{=hbzs5tLd}On second thought, perhaps now is not the right time.", null, null);
 114: 		starter.AddDialogLine("governor_talk_kingdom_creation_exit_reply", "governor_kingdom_creation_exit", "close_window", "{=ppi6eVos}As you wish.", null, null);
 115: 		starter.AddDialogLine("governor_talk_kingdom_creation_culture_selected", "governor_kingdom_creation_culture_selected", "governor_kingdom_creation_culture_selected_confirmation", "{=VOtKthQU}Yes. A kingdom using {CULTURE_ADJECTIVE} law would institute the following: {INITIAL_POLICY_NAMES}.", governor_kingdom_creation_culture_selected_on_condition, null);
 116: 		starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selected_player_reply", "governor_kingdom_creation_culture_selected_confirmation", "governor_kingdom_creation_name_selection", "{=dzXaXKaC}Very well.", null, null);
 117: 		starter.AddPlayerLine("governor_talk_kingdom_creation_culture_selected_player_reply_2", "governor_kingdom_creation_culture_selected_confirmation", "governor_kingdom_creation_culture_selection", "{=kTjsx8gN}Perhaps we should choose another set of laws and customs.", null, null);
 118: 		starter.AddDialogLine("governor_talk_kingdom_creation_name_selection", "governor_kingdom_creation_name_selection", "governor_kingdom_creation_name_selection_response", "{=wT1ducZX}Now. What will the kingdom be called?", null, null);
 119: 		starter.AddPlayerLine("governor_talk_kingdom_creation_name_selection_player", "governor_kingdom_creation_name_selection_response", "governor_kingdom_creation_name_selection_prompted", "{=XRoG766S}I'll name it...", null, governor_talk_kingdom_creation_name_selection_on_consequence);
 120: 		starter.AddDialogLine("governor_talk_kingdom_creation_name_selection_response", "governor_kingdom_creation_name_selection_prompted", "governor_kingdom_creation_name_selected", "{=shf5aY3l}I'm listening...", null, null);
 121: 		starter.AddPlayerLine("governor_talk_kingdom_creation_name_selection_cancel", "governor_kingdom_creation_name_selection_response", "governor_kingdom_creation_exit", "{=7HpfrmIU}On a second thought... Now is not the right time to do this.", null, null);
 122: 		starter.AddDialogLine("governor_talk_kingdom_creation_name_selection_final_response", "governor_kingdom_creation_name_selected", "governor_kingdom_creation_finalization", "{=CzJZ5zhT}So it shall be proclaimed throughout your domain. May {KINGDOM_NAME} forever be victorious!", governor_talk_kingdom_creation_finalization_on_condition, null);
 123: 		starter.AddPlayerLine("governor_talk_kingdom_creation_finalization", "governor_kingdom_creation_finalization", "close_window", "{=VRbbIWNf}So it shall be.", governor_talk_kingdom_creation_finalization_on_condition, governor_talk_kingdom_creation_finalization_on_consequence);
 124: 	}
 125: 
 126: 	private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
 127: 	{
 128: 		if (victim.GovernorOf != null)
 129: 		{
 130: 			ChangeGovernorAction.RemoveGovernorOf(victim);
 131: 		}
 132: 	}
 133: 
 134: 	private bool governor_talk_start_on_condition()
 135: 	{
 136: 		if (Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.GovernorOf != null && Hero.OneToOneConversationHero.CurrentSettlement != null && Hero.OneToOneConversationHero.CurrentSettlement.IsTown && Hero.OneToOneConversationHero.CurrentSettlement.Town == Hero.OneToOneConversationHero.GovernorOf && Hero.OneToOneConversationHero.GovernorOf.Owner.Owner == Hero.MainHero)
 137: 		{
 138: 			MBTextManager.SetTextVariable("GOVERNOR_SETTLEMENT", Hero.OneToOneConversationHero.CurrentSettlement.Name);
 139: 			return true;
 140: 		}
 141: 		return false;
 142: 	}
 143: 
 144: 	private bool governor_talk_start_reply_on_condition()
 145: 	{
 146: 		Settlement currentSettlement = Hero.OneToOneConversationHero.CurrentSettlement;
 147: 		TextObject textObject = TextObject.GetEmpty();
 148: 		switch (currentSettlement.Town.GetProsperityLevel())
 149: 		{
 150: 		case SettlementComponent.ProsperityLevel.High:
 151: 			textObject = new TextObject("{=8G94SlPD}We are doing well, my {?HERO.GENDER}lady{?}lord{\\?}. The merchants say business is brisk, and everything the people need appears to be in good supply.");
 152: 			break;
 153: 		case SettlementComponent.ProsperityLevel.Mid:
 154: 			textObject = new TextObject("{=HgdbSrq9}Things are all right, my {?HERO.GENDER}lady{?}lord{\\?}. The merchants say that they are breaking even, for the most part. Some prices are high, but most of what the people need is available.");
 155: 			break;
 156: 		case SettlementComponent.ProsperityLevel.Low:
 157: 			textObject = new TextObject("{=rbJEuVKg}Things could certainly be better, my {?HERO.GENDER}lady{?}lord{\\?}. The merchants say business is slow, and the people complain that goods are expensive and in short supply.");
 158: 			break;
 159: 		}
 160: 		StringHelpers.SetCharacterProperties("HERO", CharacterObject.PlayerCharacter, textObject);
 161: 		MBTextManager.SetTextVariable("SETTLEMENT_DESCRIPTION", textObject.ToString());
 162: 		return true;
 163: 	}
 164: 
 165: 	private bool governor_talk_kingdom_creation_start_on_condition()
 166: 	{
 167: 		if (Clan.PlayerClan.Kingdom == null && Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.GovernorOf != null)
 168: 		{
 169: 			return Hero.OneToOneConversationHero.GovernorOf.Settlement.MapFaction == Hero.MainHero.MapFaction;
 170: 		}
 171: 		return false;
 172: 	}
 173: 
 174: 	private void governor_talk_kingdom_creation_start_on_consequence()
 175: 	{
 176: 		_availablePlayerKingdomCultures.Clear();
 177: 		_availablePlayerKingdomCultures = Campaign.Current.Models.KingdomCreationModel.GetAvailablePlayerKingdomCultures().ToList();
 178: 		_kingdomCreationCurrentCulturePageIndex = 0;
 179: 	}
 180: 
 181: 	private bool governor_talk_kingdom_creation_start_clickable_condition(out TextObject explanation)
 182: 	{
 183: 		List<TextObject> explanations;
 184: 		bool result = Campaign.Current.Models.KingdomCreationModel.IsPlayerKingdomCreationPossible(out explanations);
 185: 		string text = "";
 186: 		foreach (TextObject item in explanations)
 187: 		{
 188: 			text += item;
 189: 			if (item != explanations[explanations.Count - 1])
 190: 			{
 191: 				text += "\n";
 192: 			}
 193: 		}
 194: 		explanation = new TextObject(text);

 succeeded in 1524ms:
Main\Features\AlignmentDesertion\AlignmentDesertionService.cs:42: var ownerSide = _alignment.GetKingdomSide(ownerKingdomId);
Main\Features\AlignmentRecruitment\RecruitmentAlignmentService.cs:8: /// Diplomacy features use). Deliberately does NOT call <see cref="IAlignmentService.AreEnemyAlignments"/>,
Main\Features\AlignmentRecruitment\RecruitmentAlignmentService.cs:32: var recruiterSide = _alignment.GetKingdomSide(recruiterKingdomId);
Main\Features\AlignmentRecruitment\RecruitmentAlignmentService.cs:33: var sourceSide = _alignment.GetKingdomSide(sourceKingdomId);
Main\Features\CaravanTrade\CaravanTradeService.cs:82: // Resolve sides directly — do NOT use IAlignmentService.AreEnemyAlignments, whose Neutral
Main\Features\CaravanTrade\CaravanTradeService.cs:87: var caravanSide = _alignment.GetKingdomSide(caravanFactionId);
Main\Features\CaravanTrade\CaravanTradeService.cs:88: var targetSide = _alignment.GetKingdomSide(targetFactionId);
Main\Features\CaravanTrade\ICaravanTradeService.cs:20: /// not Evil towns. Default. Resolved via <see cref="Execution.IAlignmentService.AreEnemyAlignments"/>.
Main\Features\Diplomacy\DiplomacyService.cs:117: if (_alignmentService.AreSameAlignment(kingdomAId, kingdomBId))
Main\Features\Execution\Hooks\ExecutionActionHook.cs:15: return !_alignmentService.AreEnemyAlignments(executorKingdomId, victimKingdomId);
Main\Features\Execution\Hooks\ExecutionActionHook.cs:20: return _alignmentService.AreSameAlignment(executorKingdomId, victimKingdomId);
Main\Features\Execution\Hooks\ExecutionActionHook.cs:25: bool crossAlignment = _alignmentService.AreEnemyAlignments(executorKingdomId, victimKingdomId);
Main\Features\Execution\Hooks\ExecutionActionHook.cs:29: if (_alignmentService.AreSameAlignment(evaluatorKingdomId, executorKingdomId))
Main\Features\Execution\Hooks\ExecutionActionHook.cs:32: if (_alignmentService.AreSameAlignment(evaluatorKingdomId, victimKingdomId))
Main\Features\Execution\Hooks\ExecutionActionHook.cs:38: if (_alignmentService.AreSameAlignment(executorKingdomId, victimKingdomId))
Main\Features\Execution\AlignmentService.cs:32: public FactionSide GetKingdomSide(string kingdomId) => GetSide(kingdomId);
Main\Features\Execution\AlignmentService.cs:44: public bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB)
Main\Features\Execution\AlignmentService.cs:46: var sideA = GetKingdomSide(kingdomIdA);
Main\Features\Execution\AlignmentService.cs:47: var sideB = GetKingdomSide(kingdomIdB);
Main\Features\Execution\AlignmentService.cs:55: public bool AreSameAlignment(string kingdomIdA, string kingdomIdB)
Main\Features\Execution\AlignmentService.cs:57: var sideA = GetKingdomSide(kingdomIdA);
Main\Features\Execution\AlignmentService.cs:58: var sideB = GetKingdomSide(kingdomIdB);
Main\Features\Execution\ExecutionRelationService.cs:50: bool crossAlignment = _alignmentService.AreEnemyAlignments(executorKingdomId, victimKingdomId);
Main\Features\Execution\ExecutionRelationService.cs:55: if (_alignmentService.AreSameAlignment(evaluatorKingdomId, executorKingdomId))
Main\Features\Execution\ExecutionRelationService.cs:59: if (_alignmentService.AreSameAlignment(evaluatorKingdomId, victimKingdomId))
Main\Features\Execution\ExecutionRelationService.cs:67: if (_alignmentService.AreSameAlignment(executorKingdomId, victimKingdomId))
Main\Features\Execution\IAlignmentService.cs:5: FactionSide GetKingdomSide(string kingdomId);
Main\Features\Execution\IAlignmentService.cs:17: bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB);
Main\Features\Execution\IAlignmentService.cs:18: bool AreSameAlignment(string kingdomIdA, string kingdomIdB);
Main\Features\WarOfTheRingMomentum\MomentumEnrollmentService.cs:86: // created kingdoms (id like "new_kingdom") whose culture IS classified — without
Main\Features\WarOfTheRingMomentum\MomentumEnrollmentService.cs:91: var side = _alignmentService.GetKingdomSide(kingdomId);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$start=78; $i=$start; Get-Content '"'Main\\Features\\WarOfTheRingMomentum\\MomentumEnrollmentService.cs' | Select-Object -Skip ("'$start-1) -First 30 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 656ms:
  78:         bool removed = state.Free.RemoveKingdom(kingdomId) || state.Evil.RemoveKingdom(kingdomId);
  79:         if (removed)
  80:             _logger.LogInfo($"[Momentum] {kingdomId} removed from the war (kingdom destroyed)");
  81:         return removed;
  82:     }
  83: 
  84:     // Side = kingdom-id alignment, falling back to the kingdom's CULTURE when the
  85:     // kingdom id isn't in alignment.json. This catches player-founded / dynamically
  86:     // created kingdoms (id like "new_kingdom") whose culture IS classified — without
  87:     // it, the player's own kingdom would resolve Neutral and never enroll, hiding the
  88:     // whole feature from a player who founds a kingdom (Codex #327 HIGH).
  89:     private FactionSide ResolveSide(string kingdomId)
  90:     {
  91:         var side = _alignmentService.GetKingdomSide(kingdomId);
  92:         if (side != FactionSide.Neutral)
  93:             return side;
  94: 
  95:         var cultureId = _allianceAdapter.GetKingdomCultureId(kingdomId);
  96:         return string.IsNullOrEmpty(cultureId) ? FactionSide.Neutral : _alignmentService.GetCultureSide(cultureId);
  97:     }
  98: 
  99:     // Reconcile the enrolled sets against the CURRENT world: drop any enrolled kingdom that
 100:     // is (a) no longer live — a KingdomDestroyed missed because the feature was toggled OFF
 101:     // (without this a wiped side keeps a stale id and its count never reaches 0, blocking the
 102:     // elimination-victory check, Codex #327 MED); or (b) still live but whose side no longer
 103:     // matches where it's enrolled — an alignment.json edit (e.g. Khand → Neutral) or a
 104:     // culture/kingdom change. The enroll loop then re-adds it to the correct side if it moved
 105:     // Free↔Evil. Without (b), a kingdom already enrolled before its alignment changed would be
 106:     // stuck on the old side on an existing save.
 107:     private bool PruneStaleKingdoms(MomentumWarState state, IReadOnlyList<string> liveKingdomIds)

codex
The player-founded kingdom case matches a known TAOM failure pattern: `new_kingdom` falls through `GetKingdomSide` to Neutral. For CaravanTrade that means the default policy treats a classified-by-culture player kingdom as trade-anyone neutral unless the hook passes culture-aware side information.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -File -Include *.cs | Select-String -Pattern 'AddModel\\(new .*CaravanModel|DefaultCaravanModel|TaomCaravanModel' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1),$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Features\\CaravanTrade TAOM.Tests\\Features\\CaravanTrade -Recurse -File -Include *.cs | Select-String -Pattern 'TODO|FIXME|AreEnemyAlignments|new_kingdom|Player|Enabled|ApplyToPlayer|ScaleVeryFarDistance|CacheVeryFar' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1),$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main\\Features\\CaravanTrade -Recurse -File -Include *.cs | Select-String -Pattern 'new List<|new Dictionary<|\\.Select\\(|\\.Where\\(|\\.OrderBy|=>|catch \\(Exception\\)' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path.Substring((Get-Location).Path.Length+1),$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 687ms:
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:40: catch (Exception)
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:55: catch (Exception)
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CalculateBudgetFactor_Patch.cs:34: catch (Exception)
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CanTradeWith_Patch.cs:50: catch (Exception)
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:53: catch (Exception)
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:30: public CaravanTradeConfig GetConfig() => _config.Value;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:19: private CaravanTradeConfig Cfg => _configProvider.GetConfig();
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:21: public bool Enabled => TaomSettings.Instance?.EnableCaravanTrade ?? Cfg.Enabled;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:22: public bool ApplyToPlayerCaravans => TaomSettings.Instance?.CaravanTradeApplyToPlayer ?? Cfg.ApplyToPlayerCaravans;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:23: public float RangeMultiplier => TaomSettings.Instance?.CaravanRangeMultiplier ?? Cfg.RangeMultiplier;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:26: public float DistanceDecayExponent => Cfg.DistanceDecayExponent;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:27: public float NearFieldFlattenDays => Cfg.NearFieldFlattenDays;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:28: public float MaxCompensation => Cfg.MaxCompensation;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:29: public float AntiShuttlePenalty => Cfg.AntiShuttlePenalty;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:31: public WarTradePolicy WarTradePolicy => ResolveWarPolicy();
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:32: public float BudgetFactorFloor => TaomSettings.Instance?.CaravanBudgetDiversityFloor ?? Cfg.BudgetFactorFloor;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:35: public int InitialTradeGold => Cfg.InitialTradeGold;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:36: public int MaxGoldPerCategory => Cfg.MaxGoldPerCategory;

2026-07-04T20:51:06.758054Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path Main\Features\CaravanTrade TAOM.Tests\Features\Ca[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCould not find a part of the path 'C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\TAOM.Tests\Features'.[0m

 exited 1 in 749ms:
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path Main\Features\CaravanTrade TAOM.Tests\Features\Ca[0m …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCould not find a part of the path 'C:\Users\mikew\source\repos\TAOM\Main\Features\CaravanTrade\TAOM.Tests\Features'.[0m

 succeeded in 1219ms:
Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:9: public class TaomCaravanModel : DefaultCaravanModel
Main\Features\CulturalFeats\Models\TaomCaravanModel.cs:14: public TaomCaravanModel(ICulturalFeatsService feats, ICaravanTradeService caravanTrade)
Main\SubModule.cs:519: campaignStarter.AddModel(new TaomCaravanModel(culturalFeats, IoC.Resolve<TAOM.Features.CaravanTrade.ICaravanTradeService>()));

codex
For Suspect 7 I need one more decompile: `AiHelper` uses `DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement` for the default land path before it reaches `DefaultMapDistanceModel.GetDistance`. I’m checking that helper so the perf verdict is not based on the wrong method.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign -Recurse -File -Filter DistanceHelper.cs | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$path='"'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\Helpers\\DistanceHelper.cs'; if (Test-Path "'$path) { $i=1; Get-Content $path | Select-Object -First 240 | ForEach-Object { '"'{0,4}: {1}' -f "'$i++, $_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\Helpers\\DistanceHelper.cs' -Pattern 'FindClosestDistanceFromMobilePartyToSettlement|GetPathDistance|GetDistance\\(' -Context 0,8 | ForEach-Object { '{0}: {1}' -f "'$_.LineNumber,$_.Line.Trim(); $_.Context.PostContext; '"'---' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 676ms:
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\Helpers\DistanceHelper.cs

 succeeded in 676ms:
19: float num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, toSettlement, isFromPort: false, isTargetingPort: false, navCapabilities, out landRatio);
		isFromPort = false;
		isTargetingPort = false;
		if (flag2 && flag)
		{
			float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, toSettlement, isFromPort: true, isTargetingPort: false, navCapabilities, out landRatio);
			if (distance < num2)
			{
				isFromPort = true;
---
24: float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, toSettlement, isFromPort: true, isTargetingPort: false, navCapabilities, out landRatio);
			if (distance < num2)
			{
				isFromPort = true;
				isTargetingPort = false;
				num2 = distance;
			}
		}
		if (flag3 && flag)
---
34: float distance2 = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, toSettlement, isFromPort: false, isTargetingPort: true, navCapabilities, out landRatio);
			if (distance2 < num2)
			{
				isFromPort = false;
				isTargetingPort = true;
				num2 = distance2;
			}
		}
		if (flag2 && flag3)
---
44: float distance3 = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, toSettlement, isFromPort: true, isTargetingPort: true, navCapabilities, out landRatio);
			if (distance3 < num2)
			{
				isFromPort = true;
				isTargetingPort = true;
				num2 = distance3;
			}
		}
		return num2;
---
65: num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: false, isTargetingPort: false, navCapabilities, out landRatio);
		}
		else
		{
			landRatio = 0f;
		}
		bool flag4 = flag2 && mobileParty.Anchor.IsAtSettlement(currentSettlement);
		isFromPort = false;
		isTargetingPort = false;
---
77: float num3 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: true, isTargetingPort: false, navCapabilities, out landRatio2);
			if (!flag4)
			{
				num3 += (float)Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea;
			}
			if (num3 < num2)
			{
				isFromPort = true;
				isTargetingPort = false;
---
93: float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: false, isTargetingPort: true, navCapabilities, out landRatio3);
			if (distance < num2)
			{
				isFromPort = false;
				isTargetingPort = true;
				num2 = distance;
				landRatio = landRatio3;
			}
		}
---
105: float num4 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: true, isTargetingPort: true, navCapabilities, out landRatio4);
			if (!flag4)
			{
				num4 += (float)Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea;
			}
			if (num4 < num2)
			{
				isFromPort = true;
				isTargetingPort = true;
---
136: public static float FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out bool isTargetingPort, out float landRatio)
	{
		float num = float.MaxValue;
		isTargetingPort = false;
		landRatio = -1f;
		if (fromMobileParty.CurrentSettlement != null)
		{
			num = FindClosestDistanceFromSettlementToSettlementForMobileParty(fromMobileParty, toSettlement, navCapabilities, out var _, out isTargetingPort, out landRatio);
		}
---
150: num = Campaign.Current.Models.MapDistanceModel.GetDistance(fromMobileParty, toSettlement, isTargetingPort: false, navCapabilities, out landRatio);
			}
			if (num2 && toSettlement.HasPort)
			{
				float estimatedLandRatio;
				float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(fromMobileParty, toSettlement, isTargetingPort: true, navCapabilities, out estimatedLandRatio);
				if (distance < num)
				{
					isTargetingPort = true;
---
155: float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(fromMobileParty, toSettlement, isTargetingPort: true, navCapabilities, out estimatedLandRatio);
				if (distance < num)
				{
					isTargetingPort = true;
					num = distance;
					landRatio = estimatedLandRatio;
				}
			}
		}
---
167: public static float FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities)
	{
		bool isTargetingPort;
		float landRatio;
		return FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out isTargetingPort, out landRatio);
	}

	public static float FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out float landRatio)
	{
---
171: return FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out isTargetingPort, out landRatio);
	}

	public static float FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out float landRatio)
	{
		bool isTargetingPort;
		return FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out isTargetingPort, out landRatio);
	}

---
174: public static float FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out float landRatio)
	{
		bool isTargetingPort;
		return FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out isTargetingPort, out landRatio);
	}

	public static bool FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, float maxDistance, out float distance, out float landRatio)
	{
		distance = FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out landRatio);
---
177: return FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out isTargetingPort, out landRatio);
	}

	public static bool FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, float maxDistance, out float distance, out float landRatio)
	{
		distance = FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out landRatio);
		return distance < maxDistance;
	}

---
180: public static bool FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, float maxDistance, out float distance, out float landRatio)
	{
		distance = FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out landRatio);
		return distance < maxDistance;
	}

	public static bool FindClosestDistanceFromSettlementToSettlement(Settlement fromSettlement, Settlement toSettlement, MobileParty.NavigationType navCapabilities, float maxDistance, out float distance, out float landRatio)
	{
		distance = FindClosestDistanceFromSettlementToSettlement(fromSettlement, toSettlement, navCapabilities, out landRatio);
---
182: distance = FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out landRatio);
		return distance < maxDistance;
	}

	public static bool FindClosestDistanceFromSettlementToSettlement(Settlement fromSettlement, Settlement toSettlement, MobileParty.NavigationType navCapabilities, float maxDistance, out float distance, out float landRatio)
	{
		distance = FindClosestDistanceFromSettlementToSettlement(fromSettlement, toSettlement, navCapabilities, out landRatio);
		return distance < maxDistance;
	}
---
210: return FindClosestDistanceFromMobilePartyToSettlement(from, currentSettlement2, navigationType, out landRatio);
		}
		if (currentSettlement != null)
		{
			return FindClosestDistanceFromSettlementToPointForMobileParty(from, to.Position, navigationType, out landRatio);
		}
		if (from.Position.DistanceSquared(to.Position) < 2500f)
		{
			return Campaign.Current.Models.MapDistanceModel.GetDistance(from, to, navigationType, out landRatio);
---
218: return Campaign.Current.Models.MapDistanceModel.GetDistance(from, to, navigationType, out landRatio);
		}
		return GetDistanceBetweenMobilePartyToMobileParty(from, to, navigationType, out landRatio);
	}

	public static float FindClosestDistanceFromSettlementToPoint(Settlement fromSettlement, CampaignVec2 point, MobileParty.NavigationType navCapabilities, out bool isFromPort)
	{
		bool num = (navCapabilities & MobileParty.NavigationType.Naval) == MobileParty.NavigationType.Naval && fromSettlement.HasPort;
		isFromPort = false;
---
227: float num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, in point, !point.IsOnLand, navCapabilities);
		if (num)
		{
			float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, in point, isFromPort: true, navCapabilities);
			if (distance < num2)
			{
				isFromPort = true;
				num2 = distance;
			}
---
230: float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, in point, isFromPort: true, navCapabilities);
			if (distance < num2)
			{
				isFromPort = true;
				num2 = distance;
			}
		}
		return num2;
	}
---
250: return FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out isTargetingPort, out landRatio);
		}
		float num = Campaign.Current.Models.MapDistanceModel.GetDistance(toSettlement, mapPoint.Position, isFromPort: false, navCapabilities);
		landRatio = 1f;
		if ((navCapabilities & MobileParty.NavigationType.Naval) == MobileParty.NavigationType.Naval && toSettlement.HasPort)
		{
			float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(toSettlement, mapPoint.Position, isFromPort: true, navCapabilities);
			if (distance < num)
			{
---
252: float num = Campaign.Current.Models.MapDistanceModel.GetDistance(toSettlement, mapPoint.Position, isFromPort: false, navCapabilities);
		landRatio = 1f;
		if ((navCapabilities & MobileParty.NavigationType.Naval) == MobileParty.NavigationType.Naval && toSettlement.HasPort)
		{
			float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(toSettlement, mapPoint.Position, isFromPort: true, navCapabilities);
			if (distance < num)
			{
				isTargetingPort = true;
				num = distance;
---
256: float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(toSettlement, mapPoint.Position, isFromPort: true, navCapabilities);
			if (distance < num)
			{
				isTargetingPort = true;
				num = distance;
				landRatio = 0f;
			}
		}
		return num;
---
281: num = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, in point, isFromPort: false, navCapabilities);
			}
			bool flag2 = flag && mobileParty.Anchor.IsAtSettlement(currentSettlement);
			if (flag)
			{
				float num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, in point, isFromPort: true, navCapabilities);
				if (!flag2)
				{
					num2 += (float)Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea;
---
286: float num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, in point, isFromPort: true, navCapabilities);
				if (!flag2)
				{
					num2 += (float)Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea;
				}
				if (num2 < num)
				{
					num = num2;
				}
---
315: return Campaign.Current.Models.MapDistanceModel.GetDistance(fromMobileParty, in point, navCapabilities, out landRatio);
	}

	public static float FindClosestDistanceFromMapPointToSettlement(IMapPoint mapPoint, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out float landRatio)
	{
		bool isTargetingPort;
		return FindClosestDistanceFromMapPointToSettlement(mapPoint, toSettlement, navCapabilities, out isTargetingPort, out landRatio);
	}

---
338: num = fromMobileParty.Position.Distance(toMobileParty.Position) - campaignVec.Distance(v) + Campaign.Current.Models.MapDistanceModel.GetDistance(item, item2, item3, item4, customCapability, out landRatio);
			if (customCapability == MobileParty.NavigationType.All)
			{
				num += Campaign.Current.Models.MapDistanceModel.GetTransitionCostAdjustment(item, item3, item2, item4, fromMobileParty.IsCurrentlyAtSea, toMobileParty.IsCurrentlyAtSea);
				if (fromMobileParty.IsCurrentlyAtSea == toMobileParty.IsCurrentlyAtSea)
				{
					float distanceBetweenMobilePartyToMobileParty = GetDistanceBetweenMobilePartyToMobileParty(fromMobileParty, toMobileParty, (!fromMobileParty.IsCurrentlyAtSea) ? MobileParty.NavigationType.Default : MobileParty.NavigationType.Naval, out landRatio);
					num = MathF.Min(num, distanceBetweenMobilePartyToMobileParty);
				}
---

 succeeded in 721ms:
   1: using TaleWorlds.CampaignSystem;
   2: using TaleWorlds.CampaignSystem.Map;
   3: using TaleWorlds.CampaignSystem.Party;
   4: using TaleWorlds.CampaignSystem.Settlements;
   5: using TaleWorlds.Library;
   6: 
   7: namespace Helpers;
   8: 
   9: public static class DistanceHelper
  10: {
  11: 	public const int BirdFlyDistanceSquaredThresholdForMobilePartyToMobilePartyDistance = 2500;
  12: 
  13: 	public static float FindClosestDistanceFromSettlementToSettlement(Settlement fromSettlement, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out bool isFromPort, out bool isTargetingPort, out float landRatio)
  14: 	{
  15: 		bool num = (navCapabilities & MobileParty.NavigationType.Naval) == MobileParty.NavigationType.Naval;
  16: 		bool flag = (navCapabilities & MobileParty.NavigationType.Default) == MobileParty.NavigationType.Default;
  17: 		bool flag2 = num && fromSettlement.HasPort && fromSettlement != toSettlement;
  18: 		bool flag3 = num && toSettlement.HasPort && fromSettlement != toSettlement;
  19: 		float num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, toSettlement, isFromPort: false, isTargetingPort: false, navCapabilities, out landRatio);
  20: 		isFromPort = false;
  21: 		isTargetingPort = false;
  22: 		if (flag2 && flag)
  23: 		{
  24: 			float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, toSettlement, isFromPort: true, isTargetingPort: false, navCapabilities, out landRatio);
  25: 			if (distance < num2)
  26: 			{
  27: 				isFromPort = true;
  28: 				isTargetingPort = false;
  29: 				num2 = distance;
  30: 			}
  31: 		}
  32: 		if (flag3 && flag)
  33: 		{
  34: 			float distance2 = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, toSettlement, isFromPort: false, isTargetingPort: true, navCapabilities, out landRatio);
  35: 			if (distance2 < num2)
  36: 			{
  37: 				isFromPort = false;
  38: 				isTargetingPort = true;
  39: 				num2 = distance2;
  40: 			}
  41: 		}
  42: 		if (flag2 && flag3)
  43: 		{
  44: 			float distance3 = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, toSettlement, isFromPort: true, isTargetingPort: true, navCapabilities, out landRatio);
  45: 			if (distance3 < num2)
  46: 			{
  47: 				isFromPort = true;
  48: 				isTargetingPort = true;
  49: 				num2 = distance3;
  50: 			}
  51: 		}
  52: 		return num2;
  53: 	}
  54: 
  55: 	private static float FindClosestDistanceFromSettlementToSettlementForMobileParty(MobileParty mobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out bool isFromPort, out bool isTargetingPort, out float landRatio)
  56: 	{
  57: 		Settlement currentSettlement = mobileParty.CurrentSettlement;
  58: 		bool num = (navCapabilities & MobileParty.NavigationType.Naval) == MobileParty.NavigationType.Naval;
  59: 		bool flag = (navCapabilities & MobileParty.NavigationType.Default) == MobileParty.NavigationType.Default;
  60: 		bool flag2 = num && currentSettlement.HasPort && currentSettlement != toSettlement;
  61: 		bool flag3 = num && toSettlement.HasPort && currentSettlement != toSettlement;
  62: 		float num2 = float.MaxValue;
  63: 		if (navCapabilities != MobileParty.NavigationType.Naval)
  64: 		{
  65: 			num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: false, isTargetingPort: false, navCapabilities, out landRatio);
  66: 		}
  67: 		else
  68: 		{
  69: 			landRatio = 0f;
  70: 		}
  71: 		bool flag4 = flag2 && mobileParty.Anchor.IsAtSettlement(currentSettlement);
  72: 		isFromPort = false;
  73: 		isTargetingPort = false;
  74: 		if (flag2 && flag)
  75: 		{
  76: 			float landRatio2;
  77: 			float num3 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: true, isTargetingPort: false, navCapabilities, out landRatio2);
  78: 			if (!flag4)
  79: 			{
  80: 				num3 += (float)Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea;
  81: 			}
  82: 			if (num3 < num2)
  83: 			{
  84: 				isFromPort = true;
  85: 				isTargetingPort = false;
  86: 				num2 = num3;
  87: 				landRatio = landRatio2;
  88: 			}
  89: 		}
  90: 		if (flag3 && flag)
  91: 		{
  92: 			float landRatio3;
  93: 			float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: false, isTargetingPort: true, navCapabilities, out landRatio3);
  94: 			if (distance < num2)
  95: 			{
  96: 				isFromPort = false;
  97: 				isTargetingPort = true;
  98: 				num2 = distance;
  99: 				landRatio = landRatio3;
 100: 			}
 101: 		}
 102: 		if (flag2 && flag3)
 103: 		{
 104: 			float landRatio4;
 105: 			float num4 = Campaign.Current.Models.MapDistanceModel.GetDistance(currentSettlement, toSettlement, isFromPort: true, isTargetingPort: true, navCapabilities, out landRatio4);
 106: 			if (!flag4)
 107: 			{
 108: 				num4 += (float)Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea;
 109: 			}
 110: 			if (num4 < num2)
 111: 			{
 112: 				isFromPort = true;
 113: 				isTargetingPort = true;
 114: 				num2 = num4;
 115: 				landRatio = landRatio4;
 116: 			}
 117: 		}
 118: 		return num2;
 119: 	}
 120: 
 121: 	public static float FindClosestDistanceFromSettlementToSettlement(Settlement fromSettlement, Settlement toSettlement, MobileParty.NavigationType navCapabilities)
 122: 	{
 123: 		bool isFromPort;
 124: 		bool isTargetingPort;
 125: 		float landRatio;
 126: 		return FindClosestDistanceFromSettlementToSettlement(fromSettlement, toSettlement, navCapabilities, out isFromPort, out isTargetingPort, out landRatio);
 127: 	}
 128: 
 129: 	public static float FindClosestDistanceFromSettlementToSettlement(Settlement fromSettlement, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out float landRatio)
 130: 	{
 131: 		bool isFromPort;
 132: 		bool isTargetingPort;
 133: 		return FindClosestDistanceFromSettlementToSettlement(fromSettlement, toSettlement, navCapabilities, out isFromPort, out isTargetingPort, out landRatio);
 134: 	}
 135: 
 136: 	public static float FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out bool isTargetingPort, out float landRatio)
 137: 	{
 138: 		float num = float.MaxValue;
 139: 		isTargetingPort = false;
 140: 		landRatio = -1f;
 141: 		if (fromMobileParty.CurrentSettlement != null)
 142: 		{
 143: 			num = FindClosestDistanceFromSettlementToSettlementForMobileParty(fromMobileParty, toSettlement, navCapabilities, out var _, out isTargetingPort, out landRatio);
 144: 		}
 145: 		else
 146: 		{
 147: 			bool num2 = (navCapabilities & MobileParty.NavigationType.Naval) == MobileParty.NavigationType.Naval;
 148: 			if ((navCapabilities & MobileParty.NavigationType.Default) == MobileParty.NavigationType.Default)
 149: 			{
 150: 				num = Campaign.Current.Models.MapDistanceModel.GetDistance(fromMobileParty, toSettlement, isTargetingPort: false, navCapabilities, out landRatio);
 151: 			}
 152: 			if (num2 && toSettlement.HasPort)
 153: 			{
 154: 				float estimatedLandRatio;
 155: 				float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(fromMobileParty, toSettlement, isTargetingPort: true, navCapabilities, out estimatedLandRatio);
 156: 				if (distance < num)
 157: 				{
 158: 					isTargetingPort = true;
 159: 					num = distance;
 160: 					landRatio = estimatedLandRatio;
 161: 				}
 162: 			}
 163: 		}
 164: 		return num;
 165: 	}
 166: 
 167: 	public static float FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities)
 168: 	{
 169: 		bool isTargetingPort;
 170: 		float landRatio;
 171: 		return FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out isTargetingPort, out landRatio);
 172: 	}
 173: 
 174: 	public static float FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out float landRatio)
 175: 	{
 176: 		bool isTargetingPort;
 177: 		return FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out isTargetingPort, out landRatio);
 178: 	}
 179: 
 180: 	public static bool FindClosestDistanceFromMobilePartyToSettlement(MobileParty fromMobileParty, Settlement toSettlement, MobileParty.NavigationType navCapabilities, float maxDistance, out float distance, out float landRatio)
 181: 	{
 182: 		distance = FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, toSettlement, navCapabilities, out landRatio);
 183: 		return distance < maxDistance;
 184: 	}
 185: 
 186: 	public static bool FindClosestDistanceFromSettlementToSettlement(Settlement fromSettlement, Settlement toSettlement, MobileParty.NavigationType navCapabilities, float maxDistance, out float distance, out float landRatio)
 187: 	{
 188: 		distance = FindClosestDistanceFromSettlementToSettlement(fromSettlement, toSettlement, navCapabilities, out landRatio);
 189: 		return distance < maxDistance;
 190: 	}
 191: 
 192: 	public static bool FindClosestDistanceFromMobilePartyToMobileParty(MobileParty from, MobileParty to, MobileParty.NavigationType navigationType, float maxDistance, out float distance, out float landRatio)
 193: 	{
 194: 		distance = FindClosestDistanceFromMobilePartyToMobileParty(from, to, navigationType, out landRatio);
 195: 		return distance < maxDistance;
 196: 	}
 197: 
 198: 	public static float FindClosestDistanceFromMobilePartyToMobileParty(MobileParty from, MobileParty to, MobileParty.NavigationType navigationType)
 199: 	{
 200: 		float landRatio;
 201: 		return FindClosestDistanceFromMobilePartyToMobileParty(from, to, navigationType, out landRatio);
 202: 	}
 203: 
 204: 	public static float FindClosestDistanceFromMobilePartyToMobileParty(MobileParty from, MobileParty to, MobileParty.NavigationType navigationType, out float landRatio)
 205: 	{
 206: 		Settlement currentSettlement = from.CurrentSettlement;
 207: 		Settlement currentSettlement2 = to.CurrentSettlement;
 208: 		if (currentSettlement2 != null)
 209: 		{
 210: 			return FindClosestDistanceFromMobilePartyToSettlement(from, currentSettlement2, navigationType, out landRatio);
 211: 		}
 212: 		if (currentSettlement != null)
 213: 		{
 214: 			return FindClosestDistanceFromSettlementToPointForMobileParty(from, to.Position, navigationType, out landRatio);
 215: 		}
 216: 		if (from.Position.DistanceSquared(to.Position) < 2500f)
 217: 		{
 218: 			return Campaign.Current.Models.MapDistanceModel.GetDistance(from, to, navigationType, out landRatio);
 219: 		}
 220: 		return GetDistanceBetweenMobilePartyToMobileParty(from, to, navigationType, out landRatio);
 221: 	}
 222: 
 223: 	public static float FindClosestDistanceFromSettlementToPoint(Settlement fromSettlement, CampaignVec2 point, MobileParty.NavigationType navCapabilities, out bool isFromPort)
 224: 	{
 225: 		bool num = (navCapabilities & MobileParty.NavigationType.Naval) == MobileParty.NavigationType.Naval && fromSettlement.HasPort;
 226: 		isFromPort = false;
 227: 		float num2 = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, in point, !point.IsOnLand, navCapabilities);
 228: 		if (num)
 229: 		{
 230: 			float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(fromSettlement, in point, isFromPort: true, navCapabilities);
 231: 			if (distance < num2)
 232: 			{
 233: 				isFromPort = true;
 234: 				num2 = distance;
 235: 			}
 236: 		}
 237: 		return num2;
 238: 	}
 239: 
 240: 	public static float FindClosestDistanceFromMapPointToSettlement(IMapPoint mapPoint, Settlement toSettlement, MobileParty.NavigationType navCapabilities, out bool isTargetingPort, out float landRatio)

codex
**VANILLA CODE**

Required decompile checks:

```csharp
// DefaultMapDistanceModel.cs:45-54
return _navigationCache.GetSettlementToSettlementDistanceWithLandRatio(...);

// DefaultMapDistanceModel.cs:60-84
value = fromMobileParty.Position.Distance(toSettlement.GatePosition);
...
value = fromMobileParty.Position.Distance(toSettlement.GatePosition)
    - item.GatePosition.Distance(toSettlement.GatePosition)
    + Campaign.Current.Models.MapDistanceModel.GetDistance(item, toSettlement, ...);
```

`DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement` also delegates to `MapDistanceModel.GetDistance` (`DistanceHelper.cs:136-165`). This supports the cache-backed distance claim.

```csharp
// CaravanPartyComponent.cs:99-105, 131
public Hero Owner { get; private set; }
public override Hero PartyOwner => Owner;
public override Hero Leader => _leader;

// CaravanPartyComponent.cs:242-248
CreateCaravanParty(Hero caravanOwner, ..., Hero caravanLeader = null, ...)
...
GetInitialTradeGold(caravanOwner, flag, isElite)

// CaravanConversationsCampaignBehavior.cs:243-250
CreateCaravanParty(Hero.MainHero, ..., characterObject.HeroObject, ...)
```

So vanilla player caravans are owned by `Hero.MainHero`; the companion is the leader, not the owner.

**KNOWN SUSPECTS**

1. **CONFIRMED FIXED.** `CaravanTradeService.AllowWartimeTrade` uses `GetKingdomSide` directly and allows `FactionSide.Neutral` on either side (`CaravanTradeService.cs:77-95`). It does not call `AreEnemyAlignments`; `None` returns false and `IgnoreWar` returns true. This matches `RecruitmentAlignmentService.cs:32-42`.

2. **DISPUTED as written, but related scope bugs found.** Companion-led player caravans are still `Hero.MainHero`-owned in vanilla, so `TaomCaravanModel.GetInitialTradeGold(owner == Hero.MainHero)` is not too narrow for that path. However, `CanTradeWith` uses map-faction identity, not owner identity, and the range cache is global. See MEDIUM findings below.

3. **DISPUTED.** The default reweight compresses vanilla’s near-town spike: equal base profit changes from `5x` near advantage at days `1` vs `5` to about `1.53x` (`100/sqrt(3)` vs `100/sqrt(7)`). A far town with double base profit wins after reweight. NaN/raw rejection gates are positive-requirement, naval and home pass through, and `nearFieldFlattenDays >= 0` plus `days > 0` prevents zero denominator.

4. **DISPUTED.** The postfix only flips false to true after a war-caused veto. It returns early when `__result` is already true (`CanTradeWith_Patch.cs:33`), leaves peacetime false alone (`:39`), respects `_prohibitedKingdomsForPlayerCaravans` (`:44, :62-66`), and is try/catch wrapped. Vanilla field/type and method match (`CaravansCampaignBehavior.cs:224`, `:2231-2246`).

5. **DISPUTED.** Dropdown order matches mapping: MCM labels index `0/1/2` are Vanilla/Same Side/Ignore War (`TaomSettings.cs:63-66`), provider maps `0->None`, `1->SameAlignmentAndNeutral`, `2->IgnoreWar` (`CaravanTradeSettingsProvider.cs:43-47`), and JSON default is `SameAlignmentAndNeutral` (`caravan_trade_config.json:15`). Parser covers the same three strings and validation rejects unknowns (`CaravanTradeConfig.cs:39-52`, `CaravanTradeConfigProvider.cs:117-121`).

6. **PARTIAL.** Every service method returns vanilla passthrough when `Enabled=false` if it is called in that disabled state, and hooks catch exceptions. But the master-off claim is not true after the range cache has already been scaled. See MEDIUM finding.

7. **DISPUTED.** The second distance computation is not a live navmesh pathfind in the checked path. `AiHelper` delegates through `DistanceHelper`, which delegates to `MapDistanceModel`; `DefaultMapDistanceModel` uses `_navigationCache.GetSettlementToSettlementDistanceWithLandRatio` plus Vec2 distances. Acceptable cost for the caravan destination re-think loop.

**CONFIG CROSS-REFERENCE**

All JSON fields are validated and consumed:

`enabled`, `applyToPlayerCaravans`, `rangeMultiplier`, `distanceDecayExponent`, `nearFieldFlattenDays`, `maxCompensation`, `antiShuttlePenalty`, `warTradePolicy`, `budgetFactorFloor`, `initialTradeGold`, `maxGoldPerCategory`.

No dead config field found. War-policy string set matches parser, validator, dropdown labels, and service enum. The shipped alignment config uses `vlandia` for Rohan and `dolguldur` for Dol Guldur; no CaravanTrade config uses invalid faction ids.

**DATA FLOW**

`caravan_trade_config.json` -> `CaravanTradeConfigProvider` validation -> `CaravanTradeSettingsProvider` MCM-over-JSON -> `CaravanTradeService` -> hooks/model.

No unread setting found. The runtime caveat is that `rangeMultiplier` flows only when vanilla calls `CacheVeryFarDistances()` at session launch, so MCM changes do not immediately update that cached lever.

**FINDINGS OR OBSERVATIONS**

[MEDIUM] Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:37 — Master-Off — Range cache is scaled once at `OnSessionLaunched`; turning the MCM master off later does not restore `_defaultCaravanVeryFarCache` / `_navalCaravanVeryFarCache`, so “MCM master toggle off = exact vanilla immediately” is false — Store original cache values or compute the scaled value dynamically, and add a toggle-off regression.

[MEDIUM] Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:37 — Player Scope — `ApplyToPlayerCaravans=false` cannot exclude player caravans from the range-envelope lever because the vanilla cache is global and `ScaleVeryFarDistance` has no owner input; player caravans still get wider distance cuts while the UI says “Off = only NPC caravans change” — Move range gating to a per-caravan seam or document/rename the setting honestly.

[MEDIUM] Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_CanTradeWith_Patch.cs:41 — Player Scope — `caravanFaction == Hero.MainHero.MapFaction` classifies NPC caravans in the player’s current kingdom as “player” for the war-gate scope; with `ApplyToPlayerCaravans=false`, those NPC caravans keep vanilla war blocking while other NPC caravans get the feature — Patch a caller with `MobileParty` context or separate “player prohibited list” detection from “player-owned caravan” scope.

[MEDIUM] Main/Features/CaravanTrade/CaravanTradeService.cs:87 — Alignment Fallback — Player-founded kingdoms are created as `new_kingdom` (`KingdomManager.cs:124-150`) and are absent from `alignment.json`, so `GetKingdomSide` returns Neutral (`AlignmentService.cs:36-42`); default `SameAlignmentAndNeutral` then lets a Free/Evil-cultured player kingdom trade across the Free/Evil line — Pass culture-aware faction data into the service and fall back to `GetCultureSide`, as `WarOfTheRingMomentum` now does.

[LOW] Main/Features/CaravanTrade/ICaravanTradeService.cs:20 — Documentation Drift — XML doc still says `SameAlignmentAndNeutral` is resolved via `AreEnemyAlignments`, contradicting the fixed implementation and the RCA lesson — Update the comment to `GetKingdomSide` plus explicit Neutral branch.

Tests not run: `dotnet test ... --filter CaravanTrade` failed before MSBuild because the sandbox denied .NET first-use directory creation, including a retry with `DOTNET_CLI_HOME=C:\tmp\dotnet_cli_home`.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 4 | LOW: 1  
VERDICT: ISSUES FOUND
SUCCESS: The process with PID 68548 (child process of PID 64908) has been terminated.
SUCCESS: The process with PID 64908 (child process of PID 60784) has been terminated.
SUCCESS: The process with PID 60784 (child process of PID 42196) has been terminated.
SUCCESS: The process with PID 42196 (child process of PID 66316) has been terminated.
SUCCESS: The process with PID 66316 (child process of PID 60440) has been terminated.
SUCCESS: The process with PID 12272 (child process of PID 49728) has been terminated.
SUCCESS: The process with PID 49728 (child process of PID 66124) has been terminated.
SUCCESS: The process with PID 66124 (child process of PID 59204) has been terminated.
SUCCESS: The process with PID 59204 (child process of PID 60440) has been terminated.
tokens used
263,981
**VANILLA CODE**

Required decompile checks:

```csharp
// DefaultMapDistanceModel.cs:45-54
return _navigationCache.GetSettlementToSettlementDistanceWithLandRatio(...);

// DefaultMapDistanceModel.cs:60-84
value = fromMobileParty.Position.Distance(toSettlement.GatePosition);
...
value = fromMobileParty.Position.Distance(toSettlement.GatePosition)
    - item.GatePosition.Distance(toSettlement.GatePosition)
    + Campaign.Current.Models.MapDistanceModel.GetDistance(item, toSettlement, ...);
```

`DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement` also delegates to `MapDistanceModel.GetDistance` (`DistanceHelper.cs:136-165`). This supports the cache-backed distance claim.

```csharp
// CaravanPartyComponent.cs:99-105, 131
public Hero Owner { get; private set; }
public override Hero PartyOwner => Owner;
public override Hero Leader => _leader;

// CaravanPartyComponent.cs:242-248
CreateCaravanParty(Hero caravanOwner, ..., Hero caravanLeader = null, ...)
...
GetInitialTradeGold(caravanOwner, flag, isElite)

// CaravanConversationsCampaignBehavior.cs:243-250
CreateCaravanParty(Hero.MainHero, ..., characterObject.HeroObject, ...)
```

So vanilla player caravans are owned by `Hero.MainHero`; the companion is the leader, not the owner.

**KNOWN SUSPECTS**

1. **CONFIRMED FIXED.** `CaravanTradeService.AllowWartimeTrade` uses `GetKingdomSide` directly and allows `FactionSide.Neutral` on either side (`CaravanTradeService.cs:77-95`). It does not call `AreEnemyAlignments`; `None` returns false and `IgnoreWar` returns true. This matches `RecruitmentAlignmentService.cs:32-42`.

2. **DISPUTED as written, but related scope bugs found.** Companion-led player caravans are still `Hero.MainHero`-owned in vanilla, so `TaomCaravanModel.GetInitialTradeGold(owner == Hero.MainHero)` is not too narrow for that path. However, `CanTradeWith` uses map-faction identity, not owner identity, and the range cache is global. See MEDIUM findings below.

3. **DISPUTED.** The default reweight compresses vanilla’s near-town spike: equal base profit changes from `5x` near advantage at days `1` vs `5` to about `1.53x` (`100/sqrt(3)` vs `100/sqrt(7)`). A far town with double base profit wins after reweight. NaN/raw rejection gates are positive-requirement, naval and home pass through, and `nearFieldFlattenDays >= 0` plus `days > 0` prevents zero denominator.

4. **DISPUTED.** The postfix only flips false to true after a war-caused veto. It returns early when `__result` is already true (`CanTradeWith_Patch.cs:33`), leaves peacetime false alone (`:39`), respects `_prohibitedKingdomsForPlayerCaravans` (`:44, :62-66`), and is try/catch wrapped. Vanilla field/type and method match (`CaravansCampaignBehavior.cs:224`, `:2231-2246`).

5. **DISPUTED.** Dropdown order matches mapping: MCM labels index `0/1/2` are Vanilla/Same Side/Ignore War (`TaomSettings.cs:63-66`), provider maps `0->None`, `1->SameAlignmentAndNeutral`, `2->IgnoreWar` (`CaravanTradeSettingsProvider.cs:43-47`), and JSON default is `SameAlignmentAndNeutral` (`caravan_trade_config.json:15`). Parser covers the same three strings and validation rejects unknowns (`CaravanTradeConfig.cs:39-52`, `CaravanTradeConfigProvider.cs:117-121`).

6. **PARTIAL.** Every service method returns vanilla passthrough when `Enabled=false` if it is called in that disabled state, and hooks catch exceptions. But the master-off claim is not true after the range cache has already been scaled. See MEDIUM finding.

7. **DISPUTED.** The second distance computation is not a live navmesh pathfind in the checked path. `AiHelper` delegates through `DistanceHelper`, which delegates to `MapDistanceModel`; `DefaultMapDistanceModel` uses `_navigationCache.GetSettlementToSettlementDistanceWithLandRatio` plus Vec2 distances. Acceptable cost for the caravan destination re-think loop.

**CONFIG CROSS-REFERENCE**

All JSON fields are validated and consumed:

`enabled`, `applyToPlayerCaravans`, `rangeMultiplier`, `distanceDecayExponent`, `nearFieldFlattenDays`, `maxCompensation`, `antiShuttlePenalty`, `warTradePolicy`, `budgetFactorFloor`, `initialTradeGold`, `maxGoldPerCategory`.

No dead config field found. War-policy string set matches parser, validator, dropdown labels, and service enum. The shipped alignment config uses `vlandia` for Rohan and `dolguldur` for Dol Guldur; no CaravanTrade config uses invalid faction ids.

**DATA FLOW**

`caravan_trade_config.json` -> `CaravanTradeConfigProvider` validation -> `CaravanTradeSettingsProvider` MCM-over-JSON -> `CaravanTradeService` -> hooks/model.

No unread setting found. The runtime caveat is that `rangeMultiplier` flows only when vanilla calls `CacheVeryFarDistances()` at session launch, so MCM changes do not immediately update that cached lever.

**FINDINGS OR OBSERVATIONS**

[MEDIUM] Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:37 — Master-Off — Range cache is scaled once at `OnSessionLaunched`; turning the MCM master off later does not restore `_defaultCaravanVeryFarCache` / `_navalCaravanVeryFarCache`, so “MCM master toggle off = exact vanilla immediately” is false — Store original cache values or compute the scaled value dynamically, and add a toggle-off regression.

[MEDIUM] Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_CacheVeryFarDistances_Patch.cs:37 — Player Scope — `ApplyToPlayerCaravans=false` cannot exclude player caravans from the range-envelope lever because the vanilla cache is global and `ScaleVeryFarDistance` has no owner input; player caravans still get wider distance cuts while the UI says “Off = only NPC caravans change” — Move range gating to a per-caravan seam or document/rename the setting honestly.

[MEDIUM] Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_CanTradeWith_Patch.cs:41 — Player Scope — `caravanFaction == Hero.MainHero.MapFaction` classifies NPC caravans in the player’s current kingdom as “player” for the war-gate scope; with `ApplyToPlayerCaravans=false`, those NPC caravans keep vanilla war blocking while other NPC caravans get the feature — Patch a caller with `MobileParty` context or separate “player prohibited list” detection from “player-owned caravan” scope.

[MEDIUM] Main/Features/CaravanTrade/CaravanTradeService.cs:87 — Alignment Fallback — Player-founded kingdoms are created as `new_kingdom` (`KingdomManager.cs:124-150`) and are absent from `alignment.json`, so `GetKingdomSide` returns Neutral (`AlignmentService.cs:36-42`); default `SameAlignmentAndNeutral` then lets a Free/Evil-cultured player kingdom trade across the Free/Evil line — Pass culture-aware faction data into the service and fall back to `GetCultureSide`, as `WarOfTheRingMomentum` now does.

[LOW] Main/Features/CaravanTrade/ICaravanTradeService.cs:20 — Documentation Drift — XML doc still says `SameAlignmentAndNeutral` is resolved via `AreEnemyAlignments`, contradicting the fixed implementation and the RCA lesson — Update the comment to `GetKingdomSide` plus explicit Neutral branch.

Tests not run: `dotnet test ... --filter CaravanTrade` failed before MSBuild because the sandbox denied .NET first-use directory creation, including a retry with `DOTNET_CLI_HOME=C:\tmp\dotnet_cli_home`.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 4 | LOW: 1  
VERDICT: ISSUES FOUND
