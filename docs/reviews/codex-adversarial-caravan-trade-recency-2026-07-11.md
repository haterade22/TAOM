OpenAI Codex v0.128.0 (research preview)
--------
workdir: E:\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: read-only
reasoning effort: xhigh
reasoning summaries: none
session id: 019f533b-d701-7a11-b658-ff85f07f1f09
--------
user
# Adversarial review: CaravanTrade recency-visit-memory fix (issue #335)

You are an adversarial code reviewer. Your job is to find real defects, not to rubber-stamp. Read the TAOM source and the decompiled installed engine before asserting anything. State CONFIRMED or DISPUTED for each Known Suspect with evidence (file:line). Use the FINDINGS format at the end.

## Feature (1-2 lines)

TAOM (Bannerlord total-conversion mod, installed engine v1.4.7) `CaravanTrade` fix. Bug: AI caravans leave a town and immediately return instead of circulating. Fix adds a per-caravan recency memory that penalizes just-visited towns, and removes the home town's distance-reweight exemption.

## Scope -- review ONLY these files (the change under review)

New:
- Main/Features/CaravanTrade/ICaravanVisitMemory.cs
- Main/Features/CaravanTrade/CaravanVisitMemory.cs
- Main/Features/CaravanTrade/CaravanVisitMemoryBehavior.cs
- TAOM.Tests/Features/CaravanTrade/CaravanVisitMemoryTests.cs

Modified:
- Main/Features/CaravanTrade/CaravanTradeService.cs (ReweightTradeScore new signature + body)
- Main/Features/CaravanTrade/ICaravanTradeService.cs
- Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs
- Main/Features/CaravanTrade/CaravanTradeConfig.cs
- Main/Features/CaravanTrade/CaravanTradeConfigProvider.cs
- Main/Features/CaravanTrade/CaravanTradeSettingsProvider.cs
- Main/Features/CaravanTrade/ICaravanTradeSettingsProvider.cs
- Main/Features/CaravanTrade/CaravanTradeIoC.cs
- Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json
- Main/SubModule.cs (ONE AddBehavior line near the CultureMarketplace block)
- TAOM.Tests/Features/CaravanTrade/CaravanTradeServiceTests.cs
- TAOM.Tests/Features/CaravanTrade/CaravanTradeConfigProviderTests.cs

IGNORE all other uncommitted changes in the working tree (troop-weight, shader-precompilation, CLAUDE.md, LESSONS-LEARNED, ReflectionSiteBindingTests) -- they are a separate in-flight effort, NOT part of this review.

## READ FIRST

- docs/features/caravan-trade.md (feature doc; the "Home rubber-band -- FIXED" known-limitation describes this change)
- Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json (the shipped config the doc mirrors)

## Verified vanilla facts (confirm independently against the installed DLLs via ilspycmd; do not take these on faith)

Decompile path base: E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\... ; authoritative signatures via `pwsh tools/taom-src.ps1 path <FullType>` against the installed v1.4.7 DLLs.

- CaravansCampaignBehavior.FindNextDestinationForCaravan is a plain argmax over Town.AllTowns; it excludes ONLY the current parked settlement: `allTown.Owner.Settlement != caravanParty.CurrentSettlement` (~line 923). GetTradeScoreForTown (the patched method) is only called for towns that passed this filter.
- The caravan re-decides its destination while STILL PARKED (HourlyTickParty ~669-677 dereferences CurrentSettlement, then calls ThinkNextDestination).
- MobileParty.LastVisitedSettlement is set ONLY on settlement ENTER (MobileParty.cs:602, inside the CurrentSettlement setter's non-null branch) and never cleared on leave. So while parked, LastVisitedSettlement == CurrentSettlement.
- GetTradeScoreForTown real signature (private, single overload): `private float GetTradeScoreForTown(MobileParty caravanParty, Town town, CampaignTime, float, bool, out MobileParty.NavigationType, out bool)`. Home gravity `num5 = 1 + elapsedDays*0.1*(elapsedDays*0.1)` is folded into the returned score upstream; the TAOM reweight only rescales the distance component, so num5 survives.
- DefaultClanFinanceModel.AddIncomeFromParty pays the caravan owner `(PartyTradeGold - 10000)/10` on the finance tick with NO CurrentSettlement==HomeSettlement gate -- i.e. caravan income is NOT home-gated.
- CampaignEvents.SettlementEntered = IMbEvent<MobileParty, Settlement, Hero>; CampaignEvents.MobilePartyDestroyed = IMbEvent<MobileParty, PartyBase>.

## Known Suspects -- CONFIRM or DISPUTE each with evidence

1. INERT-PENALTY REGRESSION (highest priority). The OLD anti-shuttle penalty was inert: it keyed on LastVisitedSettlement, which equals the parked current town at decision time, and vanilla already excludes the current town from candidates -- so it never fired on a selectable town. The NEW design records the last 4 ENTERED towns per caravan (CaravanVisitMemory, depth 4) and penalizes by recency. QUESTION: at the parked decision, is the genuinely-previous town (the one the caravan came from) actually a SELECTABLE candidate that receives a real penalty, or does the recency penalty ALSO only ever land on the excluded current town (i.e. is the fix still inert)? Trace: caravan enters B, enters C (now parked at C), re-decides. What is the ring, what rank is B at, is B in the candidate set, does B get factor < 1? Prove it holds against the engine's parked-decision timing.

2. SINGLETON LIFETIME. CaravanVisitMemoryBehavior WRITES visits; the GetTradeScoreForTown hook READS the recency factor. Both must bind to the SAME ICaravanVisitMemory instance. Check CaravanTradeIoC.cs -- is ICaravanVisitMemory registered Reuse.Singleton (not Transient)? A Transient reg makes the behavior write to one instance and the hook read an empty other -> the fix silently no-ops. Confirm the DryIoc lifetime and that both resolutions go through the one container.

3. HOME-EXEMPTION REMOVAL / PAYOUT STARVATION. ReweightTradeScore now distance-compresses the home town like any other (homeDistanceReweight default true) instead of returning its raw score. Does this starve caravan owner income by making caravans never return home? Verify DefaultClanFinanceModel.AddIncomeFromParty is NOT home-gated (income paid wherever the caravan is), and that num5 home-gravity (upstream in rawScore, uncapped quadratic) still eventually wins the argmax to bring the caravan home. If income IS somehow home-gated, this is a HIGH finding.

3b. RECENCY vs num5 -- does the recency penalty on home ever PERMANENTLY suppress the home return? The recency factor decays out of the ring after 4 other town visits and is floored strictly positive; num5 grows unbounded. Argue whether home return is guaranteed or can be starved.

4. STRANDING. The recency factor must be a strictly-positive multiplicative floor, never a hard exclusion. Read CaravanVisitMemory.GetRecencyPenaltyFactor: can it return 0 (or negative)? MinRecencyFactor floor value? A rawScore>0 multiplied by a factor in (0,1] stays >0 -> still beats a non-candidate (0/-1) in the argmax. Confirm a caravan in a sparse 2-town or all-recently-visited region cannot be stranded (return null forever). Also confirm K=4 depth cannot exceed the reachable-town set in a way that strands.

5. NaN POLARITY (engine-float gates). In ReweightTradeScore: `if (FiniteFloatValidator.IsFiniteInRange(recencyPenaltyFactor,0f,1f)) result *= recencyPenaltyFactor;` -- a NaN factor must be IGNORED (never multiply by NaN). The `days > 0f` gate must keep NaN days out of Math.Pow. In CaravanVisitMemory: a NaN/out-of-range strength must return factor 1.0. Confirm all three polarities (NaN must FAIL the gate, not pass into the active branch).

6. MEMORY LEAK. CaravanVisitMemoryBehavior.OnMobilePartyDestroyed must evict (_memory.Clear(party.StringId)) so the Dictionary<string,List<string>> doesn't grow unbounded over a long campaign. Confirm Clear is keyed by the SAME id (StringId) as RecordVisit -- a key mismatch = eviction never matches = leak. Confirm the per-caravan List is bounded to depth 4.

7. PLAYER-SCOPE ROUTING. The recency factor must flow THROUGH ReweightTradeScore's IsActiveFor(isPlayerCaravan) gate, not be multiplied around it in the hook. With ApplyToPlayerCaravans=false, a player caravan must get NO penalty. Confirm the hook passes the factor as a parameter and the service applies it only after the IsActiveFor early-return. Also: the behavior records player caravans unconditionally (intentional -- confirm it is harmless because recording without an active consumer changes nothing).

8. CONFIG. antiShuttlePenalty default changed 0.35 -> 0.5 and repurposed as recency strength; homeDistanceReweight added (bool, default true). Confirm: CaravanTradeConfigProvider copies homeDistanceReweight in its validated clone; antiShuttlePenalty keeps its [0,1] FiniteFloatValidator gate; the service no longer reads AntiShuttlePenalty (moved to CaravanVisitMemory) -- no dead read; the JSON, the DTO default, and the doc config table agree (0.5 / true).

## Also check what the 5-agent deep-review may have missed

- Master-toggle fold: enabled=false must yield EXACT vanilla score (IsActiveFor is the first line of ReweightTradeScore).
- The behavior is actually AddBehavior'd in SubModule.cs (else RegisterEvents never runs, memory stays empty, hook always sees factor 1.0 = inert).
- RemoveAt(0) ring trim + consecutive-same-town collapse in RecordVisit: any off-by-one that drops the wrong town or lets the ring exceed depth.
- Recency rank math: GetRecencyPenaltyFactor uses the MOST RECENT occurrence of a revisited town; verify the loop direction and rank formula (rank 0 = newest).

## REQUIRED OUTPUT SECTIONS

1. VANILLA CODE -- paste the decompiled snippets you actually verified (FindNextDestinationForCaravan filter line, GetTradeScoreForTown signature + num5, DefaultClanFinanceModel.AddIncomeFromParty, LastVisitedSettlement setter).
2. KNOWN SUSPECTS -- CONFIRMED/DISPUTED per suspect (1-8) with evidence.
3. CONFIG CROSS-REFERENCE -- JSON vs DTO defaults vs doc table vs consumer reads.
4. FINDINGS -- each: severity (HIGH/MED/LOW), file:line, what is wrong, why, minimal fix. If none, say so explicitly per suspect.

## QUALITY GATES

- Do not flag vanilla-matching behavior as a bug.
- Do not assume kingdom/culture IDs; not relevant here (no IDs in this change).
- If you cannot verify a claim against the installed DLLs, say UNVERIFIED rather than guessing.
- The single highest-value questions are Suspect 1 (is the fix actually non-inert?) and Suspect 2 (singleton lifetime) -- a wrong answer on either means the entire fix silently does nothing. Spend your budget there.

codex
I’ll verify the change from source and the installed decompile, with extra attention on whether the new memory actually affects selectable towns and whether the memory instance is shared. I’ll stay in review mode only and ignore unrelated worktree changes.
  → Read scoped TAOM files and feature docs/config
  • Verify vanilla caravan and finance behavior from decompiled engine
  • Trace suspects against source and decompile
  • Assemble findings with file-line evidence
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Content -Path Main/Features/CaravanTrade/CaravanVisitMemoryBehavior.cs' in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Content -Path Main/Features/CaravanTrade/CaravanVisitMemory.cs' in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|caravan_trade" Main/Features/CaravanTrade Main/SubModule.cs TAOM.Tests/Features/CaravanTrade docs/features/caravan-trade.md Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json' in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Content -Path Main/Features/CaravanTrade/CaravanTradeService.cs' in E:\repos\TAOM
 succeeded in 924ms:
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Thin entry point (ADR-002, no logic) that feeds <see cref="ICaravanVisitMemory"/>. Records each
/// caravan's town entries via <c>SettlementEntered</c> (the same signal vanilla's own caravan
/// bookkeeping uses) so the <c>GetTradeScoreForTown</c> reweight can penalize just-visited towns, and
/// evicts a caravan's memory on <c>MobilePartyDestroyed</c> so the dictionary stays bounded to live
/// caravans over a long campaign.
///
/// Registered unconditionally (not gated on the master toggle) so a mid-session toggle-on works
/// immediately; the recording is a cheap dictionary write per caravan town-entry and the penalty only
/// bites when the reweight service is active. No <c>SyncData</c> - the memory is ephemeral runtime
/// state that rebuilds as caravans move (matches the feature's save-clean design; the worst case is
/// one sub-optimal hop after a load before the ring repopulates).
/// </summary>
public class CaravanVisitMemoryBehavior : CampaignBehaviorBase
{
    private readonly ICaravanVisitMemory _memory;

    public CaravanVisitMemoryBehavior(ICaravanVisitMemory memory)
    {
        _memory = memory;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // No persistence - ephemeral recency memory, rebuilt from live caravan movement.
    }

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        // Candidate space is Town.AllTowns, so only town entries matter; village/castle entries would
        // waste a ring slot without ever matching a candidate.
        if (party == null || settlement == null || !settlement.IsTown || !party.IsCaravan)
            return;

        _memory.RecordVisit(party.StringId, settlement.StringId);
    }

    private void OnMobilePartyDestroyed(MobileParty party, PartyBase partyBase)
    {
        // Clear unconditionally on a valid id - a no-op for a party that was never recorded, and
        // robust against IsCaravan being reset during teardown. Mirrors vanilla's own per-caravan
        // dictionary eviction in CaravansCampaignBehavior.OnMobilePartyDestroyed.
        if (party != null)
            _memory.Clear(party.StringId);
    }
}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 938ms:
using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Pure implementation of <see cref="ICaravanVisitMemory"/>. Per caravan, a bounded ring of the last
/// <see cref="MemoryDepth"/> distinct-in-a-row entered towns (newest last). The penalty for a
/// candidate town scales with how recently it was visited (rank 0 = most recent), using the
/// configured <see cref="ICaravanTradeSettingsProvider.AntiShuttlePenalty"/> as the max strength, and
/// is floored at <see cref="MinRecencyFactor"/> so it never turns a positive score into a rejection.
///
/// Depth 4 kills 2-, 3-, and 4-town orbits (the previous town lands at rank 1, penalized strongly)
/// while leaving the rest of the map unpenalized. It is a <c>const</c>, not config - a tiny tuning
/// knob is not worth a new validated surface (simplicity-criterion / YAGNI); the penalty magnitude is
/// tunable via the existing <c>antiShuttlePenalty</c>.
/// </summary>
public class CaravanVisitMemory : ICaravanVisitMemory
{
    private const int MemoryDepth = 4;

    /// <summary>Strictly-positive floor so a penalized lone candidate still outscores a non-candidate (no stranding).</summary>
    private const float MinRecencyFactor = 0.05f;

    private readonly ICaravanTradeSettingsProvider _settings;
    private readonly Dictionary<string, List<string>> _visits = new();

    public CaravanVisitMemory(ICaravanTradeSettingsProvider settings)
    {
        _settings = settings;
    }

    public void RecordVisit(string caravanId, string townId)
    {
        if (string.IsNullOrEmpty(caravanId) || string.IsNullOrEmpty(townId))
            return;

        if (!_visits.TryGetValue(caravanId, out var ring))
        {
            ring = new List<string>(MemoryDepth);
            _visits[caravanId] = ring;
        }

        // Collapse consecutive re-entries of the same town (re-entering the current town, or a
        // possible double-fire) so they don't consume the whole ring with one id.
        if (ring.Count > 0 && ring[ring.Count - 1] == townId)
            return;

        ring.Add(townId);
        while (ring.Count > MemoryDepth)
            ring.RemoveAt(0);
    }

    public float GetRecencyPenaltyFactor(string caravanId, string townId)
    {
        if (string.IsNullOrEmpty(caravanId) || string.IsNullOrEmpty(townId))
            return 1f;

        if (!_visits.TryGetValue(caravanId, out var ring))
            return 1f;

        // Rank 0 = newest (last element). Use the MOST RECENT occurrence of a revisited town.
        int rank = -1;
        for (int i = ring.Count - 1; i >= 0; i--)
        {
            if (ring[i] == townId)
            {
                rank = ring.Count - 1 - i;
                break;
            }
        }

        if (rank < 0)
            return 1f; // not recently visited

        float strength = _settings.AntiShuttlePenalty;
        if (!FiniteFloatValidator.IsFiniteInRange(strength, 0f, 1f))
            return 1f; // bad strength -> no penalty (never emit a corrupted factor)

        // Linear decay by rank: rank 0 -> weight 1, older -> smaller weight.
        float weight = (float)(MemoryDepth - rank) / MemoryDepth;
        float factor = 1f - strength * weight;

        return factor < MinRecencyFactor ? MinRecencyFactor : factor;
    }

    public void Clear(string caravanId)
    {
        if (!string.IsNullOrEmpty(caravanId))
            _visits.Remove(caravanId);
    }
}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 894ms:
using System;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Execution;

namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Pure decision logic for the CaravanTrade feature. All four Harmony hooks plus the caravan
/// GameModel delegate here; no TaleWorlds types cross the boundary. See <see cref="ICaravanTradeService"/>.
/// </summary>
public class CaravanTradeService : ICaravanTradeService
{
    private readonly ICaravanTradeSettingsProvider _settings;
    private readonly IAlignmentService _alignment;
    private readonly IModLogger _logger;

    public CaravanTradeService(ICaravanTradeSettingsProvider settings, IAlignmentService alignment, IModLogger logger)
    {
        _settings = settings;
        _alignment = alignment;
        _logger = logger;
    }

    public float ReweightTradeScore(float rawScore, float days, bool isNaval, bool isHomeTown, float recencyPenaltyFactor, bool isPlayerCaravan)
    {
        if (!IsActiveFor(isPlayerCaravan))
            return rawScore;

        // Positive-requirement gate: NaN rawScore and vanilla rejections (<= 0) pass through untouched.
        if (!(rawScore > 0f))
            return rawScore;

        // Naval uses a different vanilla distance factor (not 1/days); the shuttle is a land problem.
        if (isNaval)
            return rawScore;

        float result = rawScore;

        // Strip vanilla's land 1/days spike and re-apply a gentler curve:
        //   newScore = rawScore * days / (nearFieldFlatten + days)^alpha
        // For an equal base profit P0 (rawScore = P0/days) this is P0 / (flatten+days)^alpha, so near
        // towns lose their runaway advantage and the built-in profit estimate becomes the differentiator.
        // The home town is compressed like everyone else (removing its "rubber-band" proximity edge)
        // UNLESS the HomeDistanceReweight escape hatch is off; either way vanilla's upstream home-gravity
        // (num5, already in rawScore) is untouched, so caravans still return home on the payout cadence.
        // Positive-requirement gate on days keeps NaN/non-positive days out of Math.Pow.
        bool applyDistanceReweight = (!isHomeTown || _settings.HomeDistanceReweight) && days > 0f;
        if (applyDistanceReweight)
        {
            double denom = Math.Pow(_settings.NearFieldFlattenDays + days, _settings.DistanceDecayExponent);
            float multiplier = denom > 0d ? (float)(days / denom) : 1f;

            float maxComp = _settings.MaxCompensation;
            if (multiplier > maxComp)
                multiplier = maxComp;

            result = rawScore * multiplier;
        }

        // Recency penalty (home + non-home): deprioritize just-visited towns so caravans circulate.
        // Engine-Float gate: a NaN / out-of-range factor is ignored rather than emitting a corrupted
        // score. A valid factor is in (0,1], so this never turns a positive score into a rejection.
        if (FiniteFloatValidator.IsFiniteInRange(recencyPenaltyFactor, 0f, 1f))
            result *= recencyPenaltyFactor;

        return result;
    }

    public float ScaleVeryFarDistance(float vanillaVeryFarDays)
    {
        // Applied globally (the vanilla cache is a single shared field, not per-caravan) - it only
        // widens the candidate set; the re-weight and war gate remain player-scoped.
        if (!_settings.Enabled)
            return vanillaVeryFarDays;
        return vanillaVeryFarDays * _settings.RangeMultiplier;
    }

    public bool AllowWartimeTrade(string caravanKingdomId, string caravanCultureId, string targetKingdomId, string targetCultureId, bool isPlayerCaravan)
    {
        // false = keep the vanilla war veto; true = lift it for this pairing.
        if (!IsActiveFor(isPlayerCaravan))
            return false;

        switch (_settings.WarTradePolicy)
        {
            case WarTradePolicy.IgnoreWar:
                return true;
            case WarTradePolicy.SameAlignmentAndNeutral:
                // Resolve sides directly - do NOT use IAlignmentService.AreEnemyAlignments, whose Neutral
                // semantics are inverted for this purpose (it treats Neutral as an enemy of everyone). Here
                // Neutral on either side is a mercantile "trade with anyone", and otherwise only the same
                // side trades (FreeFree / EvilEvil), never across the Free/Evil line. Mirrors the sibling
                // AlignmentRecruitment feature's deliberate work-around.
                var caravanSide = ResolveSide(caravanKingdomId, caravanCultureId);
                var targetSide = ResolveSide(targetKingdomId, targetCultureId);
                if (caravanSide == FactionSide.Neutral || targetSide == FactionSide.Neutral)
                    return true;
                return caravanSide == targetSide;
            case WarTradePolicy.None:
            default:
                return false;
        }
    }

    // Side by kingdom id, falling back to culture id when the kingdom isn't classified in alignment.json.
    // A player-founded / dynamically created kingdom (id like "new_kingdom") resolves Neutral by kingdom
    // id but IS sided by its culture - without this, a Free/Evil-cultured player kingdom would read Neutral
    // and trade across the Free/Evil line. Mirrors WarOfTheRingMomentum's MomentumEnrollmentService.ResolveSide.
    private FactionSide ResolveSide(string kingdomId, string cultureId)
    {
        var side = _alignment.GetKingdomSide(kingdomId);
        if (side != FactionSide.Neutral)
            return side;
        return string.IsNullOrEmpty(cultureId) ? FactionSide.Neutral : _alignment.GetCultureSide(cultureId);
    }

    public float ApplyBudgetFactorFloor(float vanillaBudgetFactor, bool isPlayerCaravan)
    {
        if (!IsActiveFor(isPlayerCaravan))
            return vanillaBudgetFactor;

        // Engine-sourced float: defer to vanilla on garbage rather than emit a corrupted floor.
        if (!FiniteFloatValidator.IsFinite(vanillaBudgetFactor))
            return vanillaBudgetFactor;

        return Math.Max(vanillaBudgetFactor, _settings.BudgetFactorFloor);
    }

    public int ResolveInitialTradeGold(int vanillaValue, bool isPlayerCaravan)
    {
        if (!IsActiveFor(isPlayerCaravan))
            return vanillaValue;
        // Never lower - preserve vanilla's large-caravan / main-hero bonuses.
        return Math.Max(vanillaValue, _settings.InitialTradeGold);
    }

    public int ResolveMaxGoldPerCategory(int vanillaValue, bool isPlayerCaravan)
    {
        if (!IsActiveFor(isPlayerCaravan))
            return vanillaValue;
        return _settings.MaxGoldPerCategory;
    }

    private bool IsActiveFor(bool isPlayerCaravan)
    {
        if (!_settings.Enabled)
            return false;
        if (isPlayerCaravan && !_settings.ApplyToPlayerCaravans)
            return false;
        return true;
    }
}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:11:18.474781Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.6 seconds
Output:
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeD .[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2614ms:
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeD .[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
The first pass shows `CaravanVisitMemory` does keep a bounded newest-last ring and `ReweightTradeScore` applies the recency factor inside the service after the active-scope gate. I’m switching to PowerShell-native search because `rg` is not installed in this environment.
2026-07-11T22:11:33.598594Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/CaravanTradeIoC.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
2026-07-11T22:11:33.599246Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command 'Select-String -Path Main/Features/CaravanTrade/*.cs,Main/Features/CaravanTrade/Hooks/*.cs,Main/SubModule.cs,TAOM.Tests/Features/CaravanTrade/*.cs,docs/features/caravan-trade.md,Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json -Pattern "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|SettlementEntered|MobilePartyDestroyed|IsActiveFor|GetRecencyPenaltyFactor" | ForEach-Object { "{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim() }'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command 'Select-String -Path Main/Features/CaravanTrade/*.cs,Main/Features/CaravanTrade/Hooks/*.cs,Main/SubModule.cs,TAOM.Tests/Features/CaravanTrade/*.cs,docs/features/caravan-trade.md,Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json -Pattern "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|SettlementEntered|MobilePartyDestroyed|IsActiveFor|GetRecencyPenaltyFactor" | ForEach-Object { "{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim() }' in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/CaravanTradeIoC.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }' in E:\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/CaravanTradeIoC.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command 'Select-String -Path Main/Features/CaravanTrade/*.cs,Main/Features/CaravanTrade/Hooks/*.cs,Main/SubModule.cs,TAOM.Tests/Features/CaravanTrade/*.cs,docs/features/caravan-trade.md,Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json -Pattern "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|SettlementEntered|MobilePartyDestroyed|IsActiveFor|GetRecencyPenaltyFactor" | ForEach-Object { "{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim() }'` rejected: blocked by policy
2026-07-11T22:11:33.614357Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }' in E:\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
2026-07-11T22:11:33.675863Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/ICaravanTradeService.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/ICaravanTradeService.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }' in E:\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -NoProfile -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/ICaravanTradeService.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
codex
The shell policy rejected the no-profile PowerShell wrapper, so I’ll keep using the default shell even though it emits a profile warning before the real output. That warning is noisy but not part of the repo behavior.
2026-07-11T22:11:46.194433Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Select-String -Path Main/Features/CaravanTrade/*.cs,Main/Features/CaravanTrade/Hooks/*.cs,Main/SubModule.cs,TAOM.Tests/Features/CaravanTrade/*.cs,docs/features/caravan-trade.md,Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json -Pattern "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|SettlementEntered|MobilePartyDestroyed|IsActiveFor|GetRecencyPenaltyFactor" | ForEach-Object { "{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim() }'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Select-String -Path Main/Features/CaravanTrade/*.cs,Main/Features/CaravanTrade/Hooks/*.cs,Main/SubModule.cs,TAOM.Tests/Features/CaravanTrade/*.cs,docs/features/caravan-trade.md,Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json -Pattern "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|SettlementEntered|MobilePartyDestroyed|IsActiveFor|GetRecencyPenaltyFactor" | ForEach-Object { "{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim() }' in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/CaravanTradeIoC.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }' in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }' in E:\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Select-String -Path Main/Features/CaravanTrade/*.cs,Main/Features/CaravanTrade/Hooks/*.cs,Main/SubModule.cs,TAOM.Tests/Features/CaravanTrade/*.cs,docs/features/caravan-trade.md,Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json -Pattern "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|SettlementEntered|MobilePartyDestroyed|IsActiveFor|GetRecencyPenaltyFactor" | ForEach-Object { "{0}:{1}: {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim() }'` rejected: blocked by policy
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/CaravanTradeIoC.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
2026-07-11T22:11:46.194892Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/CaravanTradeIoC.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
2026-07-11T22:11:46.195327Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/Hooks/CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
2026-07-11T22:11:46.242783Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/ICaravanTradeService.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/ICaravanTradeService.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }' in E:\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=0; Get-Content -Path Main/Features/CaravanTrade/ICaravanTradeService.cs | ForEach-Object { $i++; "{0,4}: {1}" -f $i, $_ }'` rejected: blocked by policy
2026-07-11T22:11:46.398695Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
The command filter appears to block inline PowerShell script blocks. I’ll use plain `Select-String` and `findstr /n` so we can still get reliable line references without writing anything.
2026-07-11T22:11:57.849803Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Select-String -Path Main/Features/CaravanTrade/*.cs,Main/Features/CaravanTrade/Hooks/*.cs,Main/SubModule.cs,TAOM.Tests/Features/CaravanTrade/*.cs,docs/features/caravan-trade.md,Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json -Pattern "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|SettlementEntered|MobilePartyDestroyed|IsActiveFor|GetRecencyPenaltyFactor"'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Select-String -Path Main/Features/CaravanTrade/*.cs,Main/Features/CaravanTrade/Hooks/*.cs,Main/SubModule.cs,TAOM.Tests/Features/CaravanTrade/*.cs,docs/features/caravan-trade.md,Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json -Pattern "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|SettlementEntered|MobilePartyDestroyed|IsActiveFor|GetRecencyPenaltyFactor"' in E:\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Select-String -Path Main/Features/CaravanTrade/*.cs,Main/Features/CaravanTrade/Hooks/*.cs,Main/SubModule.cs,TAOM.Tests/Features/CaravanTrade/*.cs,docs/features/caravan-trade.md,Main/_Module/ModuleData/caravan_trade/caravan_trade_config.json -Pattern "CaravanVisitMemory|ReweightTradeScore|AntiShuttlePenalty|HomeDistanceReweight|homeDistanceReweight|AddBehavior|CultureMarketplace|SettlementEntered|MobilePartyDestroyed|IsActiveFor|GetRecencyPenaltyFactor"'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\CaravanTradeIoC.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\Hooks\\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\ICaravanTradeService.cs" in E:\repos\TAOM
 succeeded in 459ms:
1:using DryIoc;
2:
3:namespace TAOM.Features.CaravanTrade;
4:
5:public static class CaravanTradeIoC
6:{
7:    public static void RegisterCaravanTradeFeature(IContainer container)
8:    {
9:        container.Register<ICaravanTradeConfigProvider, CaravanTradeConfigProvider>(Reuse.Singleton);
10:        container.Register<ICaravanTradeSettingsProvider, CaravanTradeSettingsProvider>(Reuse.Singleton);
11:        container.Register<ICaravanTradeService, CaravanTradeService>(Reuse.Singleton);
12:
13:        // Per-caravan visit memory — MUST be Singleton (shared state: the behavior writes visits,
14:        // the GetTradeScoreForTown hook reads the recency penalty).
15:        container.Register<ICaravanVisitMemory, CaravanVisitMemory>(Reuse.Singleton);
16:        container.Register<CaravanVisitMemoryBehavior>(Reuse.Singleton);
17:    }
18:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 458ms:
1:using System;
2:using HarmonyLib;
3:using Helpers;
4:using TAOM.Features.CaravanTrade;
5:using TaleWorlds.CampaignSystem;
6:using TaleWorlds.CampaignSystem.CampaignBehaviors;
7:using TaleWorlds.CampaignSystem.Party;
8:using TaleWorlds.CampaignSystem.Settlements;
9:
10:namespace TAOM.Features.CaravanTrade.Hooks;
11:
12:/// <summary>
13:/// Lever 2: re-weight the vanilla caravan destination score so caravans stop shuttling between the
14:/// nearest two towns and range to the profitable far ones. Vanilla folds a <c>1/days</c> distance
15:/// spike into the score (a town twice as far scores ~half); this postfix recomputes the raw travel
16:/// days from the SAME public inputs vanilla used (<c>AiHelper</c> + the caravan speed props), strips
17:/// that spike, and re-applies a gentler curve via the pure service — plus an anti-shuttle cut on the
18:/// town just left. Selection-only; profit and payout are untouched. Naval + home pass through.
19:/// </summary>
20:[HarmonyPatch(typeof(CaravansCampaignBehavior), "GetTradeScoreForTown")]
21:[HarmonyPatchCategory("Patch59_CaravanTrade")]
22:public static class CaravansCampaignBehavior_GetTradeScoreForTown_Patch
23:{
24:    private static ICaravanTradeService _service;
25:    private static ICaravanVisitMemory _memory;
26:
27:    [HarmonyPostfix]
28:    public static void Postfix(ref float __result, MobileParty caravanParty, Town town)
29:    {
30:        // Positive-requirement gate: vanilla rejections (-1) and any NaN pass through untouched.
31:        if (!(__result > 0f)) return;
32:        if (caravanParty == null || town?.Settlement == null) return;
33:
34:        try
35:        {
36:            _service ??= IoC.Resolve<ICaravanTradeService>();
37:            _memory ??= IoC.Resolve<ICaravanVisitMemory>();
38:
39:            bool isNaval = caravanParty.HasNavalNavigationCapability;
40:            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
41:                caravanParty, town.Settlement, isNaval, out var navType, out var navDistance, out _);
42:            if (navType == MobileParty.NavigationType.None) return;
43:
44:            float speed = isNaval
45:                ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed
46:                : Campaign.Current.EstimatedAverageCaravanPartySpeed;
47:            float days = navDistance / (speed * CampaignTime.HoursInDay);
48:
49:            bool isHome = town.Settlement == caravanParty.HomeSettlement;
50:            bool isPlayer = caravanParty.Owner?.Clan == Clan.PlayerClan;
51:
52:            // Recency penalty from the per-caravan visit memory (string ids at the boundary, ADR-007).
53:            // Replaces the old LastVisitedSettlement check, which was inert (it only ever matched the
54:            // parked/current town, which vanilla already excludes from candidates).
55:            float recency = _memory.GetRecencyPenaltyFactor(caravanParty.StringId, town.Settlement.StringId);
56:
57:            __result = _service.ReweightTradeScore(__result, days, isNaval, isHome, recency, isPlayer);
58:        }
59:        catch (Exception)
60:        {
61:            // Degrade gracefully to the vanilla score.
62:        }
63:    }
64:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 477ms:
1:namespace TAOM.Features.CaravanTrade;
2:
3:/// <summary>
4:/// How AI/player caravans should treat the war state when choosing a trade town.
5:/// TAOM is endless-war-by-default (Free-vs-Evil), so the vanilla "no trade with any
6:/// faction you're at war with" veto collapses each caravan's reachable set to its own
7:/// side and forces the local shuttle. This policy relaxes that veto.
8:/// </summary>
9:public enum WarTradePolicy
10:{
11:    /// <summary>Vanilla behavior — war blocks trade. The feature makes no war-gate change.</summary>
12:    None,
13:
14:    /// <summary>Lift the war veto entirely — caravans trade at any non-besieged town regardless of war.</summary>
15:    IgnoreWar,
16:
17:    /// <summary>
18:    /// Lift the war veto only between non-enemy alignments — same side (Free↔Free, Evil↔Evil) or
19:    /// any pairing involving a Neutral faction. A Free caravan reaches other Free/neutral towns but
20:    /// not Evil towns. Default. Each side resolves via <see cref="Execution.IAlignmentService.GetKingdomSide"/>
21:    /// (culture-fallback for player-founded kingdoms) with an explicit Neutral-trades-anyone branch —
22:    /// deliberately NOT <c>AreEnemyAlignments</c>, whose Neutral-as-enemy-of-everyone semantics are inverted here.
23:    /// </summary>
24:    SameAlignmentAndNeutral,
25:}
26:
27:/// <summary>
28:/// Pure decision surface for the CaravanTrade feature. No TaleWorlds types cross this boundary —
29:/// the Harmony postfixes and the caravan GameModel extract primitives and delegate here (ADR-002/007).
30:/// Every method short-circuits to the vanilla value when the feature is disabled (or when it's a
31:/// player caravan and player-scoping is off), so master-off restores exact vanilla behavior.
32:/// </summary>
33:public interface ICaravanTradeService
34:{
35:    /// <summary>
36:    /// Re-weight vanilla's trade-destination score to stop the closest-town-always-wins shuttle and
37:    /// make longer viable trips competitive. Strips vanilla's land <c>1/days</c> distance spike and
38:    /// re-applies a gentler <c>1/(nearFieldFlatten + days)^decayExponent</c> curve, clamped by
39:    /// <c>maxCompensation</c>; near-equal-distance towns become near-tied so the built-in profit
40:    /// estimate (which passes through untouched) decides. Then applies the per-caravan recency penalty
41:    /// so just-visited towns are deprioritized. Naval passes through unchanged (different vanilla
42:    /// distance factor). The home settlement is compressed like any other town unless
43:    /// <see cref="ICaravanTradeSettingsProvider.HomeDistanceReweight"/> is off (escape hatch); vanilla's
44:    /// upstream home-gravity (<c>num5</c>, already folded into <paramref name="rawScore"/>) is preserved
45:    /// either way, so caravans still return home to deliver payouts on the natural cadence.
46:    /// </summary>
47:    /// <param name="rawScore">Vanilla's <c>GetTradeScoreForTown</c> result. Values ≤ 0 (rejections) pass through.</param>
48:    /// <param name="days">Raw travel time in days (vanilla's <c>num</c>), recomputed from the same public inputs.</param>
49:    /// <param name="isNaval">Caravan has naval capability (uses vanilla's different naval distance factor).</param>
50:    /// <param name="isHomeTown">Candidate is the caravan's home settlement (distance re-weight gated by the escape hatch).</param>
51:    /// <param name="recencyPenaltyFactor">Recency multiplier in (0,1] from <see cref="ICaravanVisitMemory"/>; 1 = no penalty. NaN/out-of-range is ignored.</param>
52:    /// <param name="isPlayerCaravan">Caravan is player-owned (scoped off when player-application is disabled).</param>
53:    float ReweightTradeScore(float rawScore, float days, bool isNaval, bool isHomeTown, float recencyPenaltyFactor, bool isPlayerCaravan);
54:
55:    /// <summary>
56:    /// Scale the vanilla "very far" distance ceiling so profitable distant towns aren't hard-rejected.
57:    /// The vanilla cache is a single shared field (not per-caravan), so this is applied globally when
58:    /// the feature is enabled — it only widens the candidate set; the re-weight and war gate remain
59:    /// player-scoped. Returns the vanilla value unchanged when disabled.
60:    /// </summary>
61:    float ScaleVeryFarDistance(float vanillaVeryFarDays);
62:
63:    /// <summary>
64:    /// Whether to lift the vanilla war veto for this caravan→town faction pairing. Returns
65:    /// <c>false</c> to keep the vanilla veto (the caller leaves <c>__result</c> false); <c>true</c>
66:    /// to allow trade despite the war, per the configured <see cref="WarTradePolicy"/>. Each faction's
67:    /// alignment resolves by kingdom StringId, falling back to its culture StringId when the kingdom
68:    /// isn't classified (player-founded / dynamically created kingdoms resolve Neutral by kingdom id
69:    /// but are sided by culture) — mirroring WarOfTheRingMomentum's enrollment resolution.
70:    /// </summary>
71:    bool AllowWartimeTrade(string caravanKingdomId, string caravanCultureId, string targetKingdomId, string targetCultureId, bool isPlayerCaravan);
72:
73:    /// <summary>
74:    /// Raise vanilla's per-caravan <c>budgetFactor</c> to at least the configured floor so even a
75:    /// poor caravan clears the per-category buy-value gate on more than one category (the direct
76:    /// fix for "caravans only buy one item"). Returns the vanilla value unchanged when disabled or
77:    /// non-finite.
78:    /// </summary>
79:    float ApplyBudgetFactorFloor(float vanillaBudgetFactor, bool isPlayerCaravan);
80:
81:    /// <summary>
82:    /// Resolve the caravan's starting trade gold. A higher floor saturates vanilla's
83:    /// <c>budgetFactor = 0.1 + clamp(gold/5000)</c>, letting more categories clear the buy gate.
84:    /// Never lowers the vanilla value (preserves the large/main-hero bonuses). Vanilla when disabled.
85:    /// </summary>
86:    int ResolveInitialTradeGold(int vanillaValue, bool isPlayerCaravan);
87:
88:    /// <summary>
89:    /// Resolve the per-item-category gold cap. Defaults to vanilla; exposed for tuning. Vanilla when disabled.
90:    /// </summary>
91:    int ResolveMaxGoldPerCategory(int vanillaValue, bool isPlayerCaravan);
92:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:11:58.520403Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
I’ve confirmed the DryIoc registration is singleton for both the memory and behavior, and the hook resolves both through the shared IoC container. Next I’m pulling config/provider/settings and tests, then I’ll decompile the engine paths the review depends on.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /i \"CaravanVisitMemory ReweightTradeScore AntiShuttlePenalty HomeDistanceReweight homeDistanceReweight AddBehavior CultureMarketplace SettlementEntered MobilePartyDestroyed IsActiveFor GetRecencyPenaltyFactor\" Main\\Features\\CaravanTrade\\*.cs Main\\Features\\CaravanTrade\\Hooks\\*.cs Main\\SubModule.cs TAOM.Tests\\Features\\CaravanTrade\\*.cs docs\\features\\caravan-trade.md Main\\_Module\\ModuleData\\caravan_trade\\caravan_trade_config.json" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\CaravanTradeConfig.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\CaravanTradeConfigProvider.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\CaravanTradeSettingsProvider.cs" in E:\repos\TAOM
 succeeded in 461ms:
1:namespace TAOM.Features.CaravanTrade;
2:
3:/// <summary>
4:/// JSON DTO for <c>caravan_trade/caravan_trade_config.json</c>. Defaults are the shipped tuning.
5:/// Validated field-by-field by <see cref="CaravanTradeConfigProvider"/>; the MCM layer overrides a
6:/// subset at runtime via <see cref="ICaravanTradeSettingsProvider"/>.
7:/// </summary>
8:public class CaravanTradeConfig
9:{
10:    public bool Enabled { get; set; } = true;
11:    public bool ApplyToPlayerCaravans { get; set; } = true;
12:
13:    // Range envelope + selection re-weight (Levers 2 & 3).
14:    public float RangeMultiplier { get; set; } = 1.6f;
15:    public float DistanceDecayExponent { get; set; } = 0.5f;
16:    public float NearFieldFlattenDays { get; set; } = 2.0f;
17:    public float MaxCompensation { get; set; } = 6.0f;
18:
19:    // Recency penalty strength: max fractional score cut on the most-recently-visited town, decaying
20:    // over the last few towns each caravan visited (the anti-shuttle lever, now actually applied to a
21:    // selectable town via the per-caravan visit memory). Kept the old field name for save/config compat.
22:    public float AntiShuttlePenalty { get; set; } = 0.5f;
23:
24:    // Escape hatch: when true (default) the home town is distance-compressed like any other town so it
25:    // loses its near-field proximity advantage (fixes the home rubber-band); set false to restore the
26:    // old home distance exemption if caravans return home too rarely. Vanilla's home-gravity is
27:    // preserved either way, so caravans still deliver payouts home.
28:    public bool HomeDistanceReweight { get; set; } = true;
29:
30:    // War policy (Lever 1). Validated against the known set; unknown reverts to the default.
31:    public string WarTradePolicy { get; set; } = "SameAlignmentAndNeutral";
32:
33:    // Basket diversity (Lever 4).
34:    public float BudgetFactorFloor { get; set; } = 0.35f;
35:    public int InitialTradeGold { get; set; } = 15000;
36:    public int MaxGoldPerCategory { get; set; } = 1500;
37:}
38:
39:/// <summary>Parsing + known-set validation for the <see cref="WarTradePolicy"/> config string (the M1 typo trap).</summary>
40:public static class WarTradePolicyParser
41:{
42:    /// <summary>Case-insensitively parse a config string into a <see cref="WarTradePolicy"/>. Returns false for null/empty/unknown.</summary>
43:    public static bool TryParse(string value, out WarTradePolicy policy)
44:    {
45:        policy = WarTradePolicy.SameAlignmentAndNeutral;
46:        if (string.IsNullOrWhiteSpace(value))
47:            return false;
48:
49:        switch (value.Trim().ToLowerInvariant())
50:        {
51:            case "none":
52:                policy = WarTradePolicy.None;
53:                return true;
54:            case "ignorewar":
55:                policy = WarTradePolicy.IgnoreWar;
56:                return true;
57:            case "samealignmentandneutral":
58:                policy = WarTradePolicy.SameAlignmentAndNeutral;
59:                return true;
60:            default:
61:                return false;
62:        }
63:    }
64:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 473ms:
Main\Features\CaravanTrade\CaravanTradeConfig.cs:22:    public float AntiShuttlePenalty { get; set; } = 0.5f;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:28:    public bool HomeDistanceReweight { get; set; } = true;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:68:            AntiShuttlePenalty = parsed.AntiShuttlePenalty,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:69:            HomeDistanceReweight = parsed.HomeDistanceReweight,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:110:        if (!FiniteFloatValidator.IsFiniteInRange(c.AntiShuttlePenalty, 0f, 1f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:112:            _logger.LogWarning($"CaravanTradeConfigProvider: antiShuttlePenalty={c.AntiShuttlePenalty} must be finite in [0,1], reverting to {defaults.AntiShuttlePenalty}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:113:            c.AntiShuttlePenalty = defaults.AntiShuttlePenalty;
Main\Features\CaravanTrade\CaravanTradeIoC.cs:15:        container.Register<ICaravanVisitMemory, CaravanVisitMemory>(Reuse.Singleton);
Main\Features\CaravanTrade\CaravanTradeIoC.cs:16:        container.Register<CaravanVisitMemoryBehavior>(Reuse.Singleton);
Main\Features\CaravanTrade\CaravanTradeService.cs:25:    public float ReweightTradeScore(float rawScore, float days, bool isNaval, bool isHomeTown, float recencyPenaltyFactor, bool isPlayerCaravan)
Main\Features\CaravanTrade\CaravanTradeService.cs:27:        if (!IsActiveFor(isPlayerCaravan))
Main\Features\CaravanTrade\CaravanTradeService.cs:45:        // UNLESS the HomeDistanceReweight escape hatch is off; either way vanilla's upstream home-gravity
Main\Features\CaravanTrade\CaravanTradeService.cs:48:        bool applyDistanceReweight = (!isHomeTown || _settings.HomeDistanceReweight) && days > 0f;
Main\Features\CaravanTrade\CaravanTradeService.cs:82:        if (!IsActiveFor(isPlayerCaravan))
Main\Features\CaravanTrade\CaravanTradeService.cs:120:        if (!IsActiveFor(isPlayerCaravan))
Main\Features\CaravanTrade\CaravanTradeService.cs:132:        if (!IsActiveFor(isPlayerCaravan))
Main\Features\CaravanTrade\CaravanTradeService.cs:140:        if (!IsActiveFor(isPlayerCaravan))
Main\Features\CaravanTrade\CaravanTradeService.cs:145:    private bool IsActiveFor(bool isPlayerCaravan)
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:29:    public float AntiShuttlePenalty => Cfg.AntiShuttlePenalty;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:30:    public bool HomeDistanceReweight => Cfg.HomeDistanceReweight;
Main\Features\CaravanTrade\CaravanVisitMemory.cs:7:/// Pure implementation of <see cref="ICaravanVisitMemory"/>. Per caravan, a bounded ring of the last
Main\Features\CaravanTrade\CaravanVisitMemory.cs:10:/// configured <see cref="ICaravanTradeSettingsProvider.AntiShuttlePenalty"/> as the max strength, and
Main\Features\CaravanTrade\CaravanVisitMemory.cs:16:/// tunable via the existing <c>antiShuttlePenalty</c>.
Main\Features\CaravanTrade\CaravanVisitMemory.cs:18:public class CaravanVisitMemory : ICaravanVisitMemory
Main\Features\CaravanTrade\CaravanVisitMemory.cs:28:    public CaravanVisitMemory(ICaravanTradeSettingsProvider settings)
Main\Features\CaravanTrade\CaravanVisitMemory.cs:54:    public float GetRecencyPenaltyFactor(string caravanId, string townId)
Main\Features\CaravanTrade\CaravanVisitMemory.cs:76:        float strength = _settings.AntiShuttlePenalty;
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:8:/// Thin entry point (ADR-002, no logic) that feeds <see cref="ICaravanVisitMemory"/>. Records each
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:9:/// caravan's town entries via <c>SettlementEntered</c> (the same signal vanilla's own caravan
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:11:/// evicts a caravan's memory on <c>MobilePartyDestroyed</c> so the dictionary stays bounded to live
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:20:public class CaravanVisitMemoryBehavior : CampaignBehaviorBase
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:22:    private readonly ICaravanVisitMemory _memory;
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:24:    public CaravanVisitMemoryBehavior(ICaravanVisitMemory memory)
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:31:        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:32:        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:40:    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:50:    private void OnMobilePartyDestroyed(MobileParty party, PartyBase partyBase)
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:54:        // dictionary eviction in CaravansCampaignBehavior.OnMobilePartyDestroyed.
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:25:    private static ICaravanVisitMemory _memory;
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:37:            _memory ??= IoC.Resolve<ICaravanVisitMemory>();
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:55:            float recency = _memory.GetRecencyPenaltyFactor(caravanParty.StringId, town.Settlement.StringId);
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:57:            __result = _service.ReweightTradeScore(__result, days, isNaval, isHome, recency, isPlayer);
Main\Features\CaravanTrade\ICaravanTradeService.cs:43:    /// <see cref="ICaravanTradeSettingsProvider.HomeDistanceReweight"/> is off (escape hatch); vanilla's
Main\Features\CaravanTrade\ICaravanTradeService.cs:51:    /// <param name="recencyPenaltyFactor">Recency multiplier in (0,1] from <see cref="ICaravanVisitMemory"/>; 1 = no penalty. NaN/out-of-range is ignored.</param>
Main\Features\CaravanTrade\ICaravanTradeService.cs:53:    float ReweightTradeScore(float rawScore, float days, bool isNaval, bool isHomeTown, float recencyPenaltyFactor, bool isPlayerCaravan);
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:31:    float AntiShuttlePenalty { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:34:    bool HomeDistanceReweight { get; }
Main\Features\CaravanTrade\ICaravanVisitMemory.cs:8:/// Pure + TaleWorlds-free (ADR-007): the thin <see cref="CaravanVisitMemoryBehavior"/> and the
Main\Features\CaravanTrade\ICaravanVisitMemory.cs:18:public interface ICaravanVisitMemory
Main\Features\CaravanTrade\ICaravanVisitMemory.cs:30:    float GetRecencyPenaltyFactor(string caravanId, string townId);
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 444ms:
1:namespace TAOM.Features.CaravanTrade;
2:
3:/// <summary>
4:/// Merges MCM over the validated JSON config. MCM-exposed fields read <c>TaomSettings.Instance?.X</c>
5:/// and fall back to the JSON config (which is the default source + holds the advanced, JSON-only
6:/// knobs). MCM slider bounds mirror the JSON validation bounds, so an MCM value can't escape the
7:/// validated range (the "both surfaces" invariant). The war policy resolves from the MCM dropdown
8:/// index, falling back to the validated JSON string.
9:/// </summary>
10:public class CaravanTradeSettingsProvider : ICaravanTradeSettingsProvider
11:{
12:    private readonly ICaravanTradeConfigProvider _configProvider;
13:
14:    public CaravanTradeSettingsProvider(ICaravanTradeConfigProvider configProvider)
15:    {
16:        _configProvider = configProvider;
17:    }
18:
19:    private CaravanTradeConfig Cfg => _configProvider.GetConfig();
20:
21:    public bool Enabled => TaomSettings.Instance?.EnableCaravanTrade ?? Cfg.Enabled;
22:    public bool ApplyToPlayerCaravans => TaomSettings.Instance?.CaravanTradeApplyToPlayer ?? Cfg.ApplyToPlayerCaravans;
23:    public float RangeMultiplier => TaomSettings.Instance?.CaravanRangeMultiplier ?? Cfg.RangeMultiplier;
24:
25:    // JSON-only advanced curve knobs.
26:    public float DistanceDecayExponent => Cfg.DistanceDecayExponent;
27:    public float NearFieldFlattenDays => Cfg.NearFieldFlattenDays;
28:    public float MaxCompensation => Cfg.MaxCompensation;
29:    public float AntiShuttlePenalty => Cfg.AntiShuttlePenalty;
30:    public bool HomeDistanceReweight => Cfg.HomeDistanceReweight;
31:
32:    public WarTradePolicy WarTradePolicy => ResolveWarPolicy();
33:    public float BudgetFactorFloor => TaomSettings.Instance?.CaravanBudgetDiversityFloor ?? Cfg.BudgetFactorFloor;
34:
35:    // JSON-only.
36:    public int InitialTradeGold => Cfg.InitialTradeGold;
37:    public int MaxGoldPerCategory => Cfg.MaxGoldPerCategory;
38:
39:    private WarTradePolicy ResolveWarPolicy()
40:    {
41:        var dropdown = TaomSettings.Instance?.CaravanWarTradePolicy;
42:        if (dropdown != null)
43:        {
44:            switch (dropdown.SelectedIndex)
45:            {
46:                case 0: return WarTradePolicy.None;
47:                case 1: return WarTradePolicy.SameAlignmentAndNeutral;
48:                case 2: return WarTradePolicy.IgnoreWar;
49:            }
50:        }
51:
52:        // Fall back to the validated JSON string (already normalized to the known set by the provider).
53:        return WarTradePolicyParser.TryParse(Cfg.WarTradePolicy, out var policy)
54:            ? policy
55:            : WarTradePolicy.SameAlignmentAndNeutral;
56:    }
57:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 457ms:
1:using System;
2:using System.IO;
3:using Newtonsoft.Json;
4:using TAOM.Core.Infrastructure;
5:using TAOM.Core.Logging;
6:using TAOM.Core.Validation;
7:
8:namespace TAOM.Features.CaravanTrade;
9:
10:/// <summary>
11:/// Loads + validates <c>caravan_trade/caravan_trade_config.json</c>. Every numeric field is
12:/// range-checked (NaN/Infinity rejected via <see cref="FiniteFloatValidator"/>); the war-policy
13:/// string is validated against the known set (the M1 typo trap); invalid values revert to the
14:/// shipped default with a warning, and the master toggle stays effective. Cached (Reuse.Singleton) —
15:/// config edits require an app restart.
16:/// </summary>
17:public class CaravanTradeConfigProvider : ICaravanTradeConfigProvider
18:{
19:    private readonly IPathService _pathService;
20:    private readonly IModLogger _logger;
21:    private readonly Lazy<CaravanTradeConfig> _config;
22:
23:    public CaravanTradeConfigProvider(IPathService pathService, IModLogger logger)
24:    {
25:        _pathService = pathService;
26:        _logger = logger;
27:        _config = new Lazy<CaravanTradeConfig>(LoadConfig);
28:    }
29:
30:    public CaravanTradeConfig GetConfig() => _config.Value;
31:
32:    private CaravanTradeConfig LoadConfig()
33:    {
34:        var path = Path.Combine(_pathService.ModuleDataPath, "caravan_trade", "caravan_trade_config.json");
35:
36:        if (!File.Exists(path))
37:        {
38:            _logger.LogWarning($"CaravanTradeConfigProvider: caravan_trade_config.json not found at {path}, using defaults");
39:            return new CaravanTradeConfig();
40:        }
41:
42:        CaravanTradeConfig parsed;
43:        try
44:        {
45:            var json = File.ReadAllText(path);
46:            parsed = JsonConvert.DeserializeObject<CaravanTradeConfig>(json) ?? new CaravanTradeConfig();
47:        }
48:        catch (Exception ex)
49:        {
50:            _logger.LogError($"CaravanTradeConfigProvider: Failed to parse caravan_trade_config.json: {ex.Message}");
51:            return new CaravanTradeConfig();
52:        }
53:
54:        return Validate(parsed);
55:    }
56:
57:    private CaravanTradeConfig Validate(CaravanTradeConfig parsed)
58:    {
59:        var defaults = new CaravanTradeConfig();
60:        var c = new CaravanTradeConfig
61:        {
62:            Enabled = parsed.Enabled,
63:            ApplyToPlayerCaravans = parsed.ApplyToPlayerCaravans,
64:            RangeMultiplier = parsed.RangeMultiplier,
65:            DistanceDecayExponent = parsed.DistanceDecayExponent,
66:            NearFieldFlattenDays = parsed.NearFieldFlattenDays,
67:            MaxCompensation = parsed.MaxCompensation,
68:            AntiShuttlePenalty = parsed.AntiShuttlePenalty,
69:            HomeDistanceReweight = parsed.HomeDistanceReweight,
70:            WarTradePolicy = parsed.WarTradePolicy,
71:            BudgetFactorFloor = parsed.BudgetFactorFloor,
72:            InitialTradeGold = parsed.InitialTradeGold,
73:            MaxGoldPerCategory = parsed.MaxGoldPerCategory,
74:        };
75:
76:        var rejected = false;
77:
78:        // Below 1 would shrink the range below vanilla (worse shuttle); above 4 lets caravans wander map-wide.
79:        if (!FiniteFloatValidator.IsFiniteInRange(c.RangeMultiplier, 1f, 4f))
80:        {
81:            _logger.LogWarning($"CaravanTradeConfigProvider: rangeMultiplier={c.RangeMultiplier} must be finite in [1,4], reverting to {defaults.RangeMultiplier}");
82:            c.RangeMultiplier = defaults.RangeMultiplier;
83:            rejected = true;
84:        }
85:
86:        // Shape only: 0/negative degenerates the curve; > 4 is a near-vanilla steep decay.
87:        if (!FiniteFloatValidator.IsFiniteInRange(c.DistanceDecayExponent, 0.25f, 4f))
88:        {
89:            _logger.LogWarning($"CaravanTradeConfigProvider: distanceDecayExponent={c.DistanceDecayExponent} must be finite in [0.25,4], reverting to {defaults.DistanceDecayExponent}");
90:            c.DistanceDecayExponent = defaults.DistanceDecayExponent;
91:            rejected = true;
92:        }
93:
94:        if (!FiniteFloatValidator.IsFiniteInRange(c.NearFieldFlattenDays, 0f, 20f))
95:        {
96:            _logger.LogWarning($"CaravanTradeConfigProvider: nearFieldFlattenDays={c.NearFieldFlattenDays} must be finite in [0,20], reverting to {defaults.NearFieldFlattenDays}");
97:            c.NearFieldFlattenDays = defaults.NearFieldFlattenDays;
98:            rejected = true;
99:        }
100:
101:        // Must be ≥ 1 (a cap below 1 would suppress every score); ≤ 20 keeps far towns from dominating.
102:        if (!FiniteFloatValidator.IsFiniteInRange(c.MaxCompensation, 1f, 20f))
103:        {
104:            _logger.LogWarning($"CaravanTradeConfigProvider: maxCompensation={c.MaxCompensation} must be finite in [1,20], reverting to {defaults.MaxCompensation}");
105:            c.MaxCompensation = defaults.MaxCompensation;
106:            rejected = true;
107:        }
108:
109:        // A fraction. > 1 would flip the score sign; < 0 would reward returning to the just-left town.
110:        if (!FiniteFloatValidator.IsFiniteInRange(c.AntiShuttlePenalty, 0f, 1f))
111:        {
112:            _logger.LogWarning($"CaravanTradeConfigProvider: antiShuttlePenalty={c.AntiShuttlePenalty} must be finite in [0,1], reverting to {defaults.AntiShuttlePenalty}");
113:            c.AntiShuttlePenalty = defaults.AntiShuttlePenalty;
114:            rejected = true;
115:        }
116:
117:        // M1 string-branch trap: an unknown/typo policy must revert, not silently take the switch default.
118:        if (!WarTradePolicyParser.TryParse(c.WarTradePolicy, out _))
119:        {
120:            _logger.LogWarning($"CaravanTradeConfigProvider: warTradePolicy='{c.WarTradePolicy}' is not one of None/IgnoreWar/SameAlignmentAndNeutral, reverting to {defaults.WarTradePolicy}");
121:            c.WarTradePolicy = defaults.WarTradePolicy;
122:            rejected = true;
123:        }
124:
125:        if (!FiniteFloatValidator.IsFiniteInRange(c.BudgetFactorFloor, 0f, 1f))
126:        {
127:            _logger.LogWarning($"CaravanTradeConfigProvider: budgetFactorFloor={c.BudgetFactorFloor} must be finite in [0,1], reverting to {defaults.BudgetFactorFloor}");
128:            c.BudgetFactorFloor = defaults.BudgetFactorFloor;
129:            rejected = true;
130:        }
131:
132:        if (c.InitialTradeGold < 1000 || c.InitialTradeGold > 100000)
133:        {
134:            _logger.LogWarning($"CaravanTradeConfigProvider: initialTradeGold={c.InitialTradeGold} must be in [1000,100000], reverting to {defaults.InitialTradeGold}");
135:            c.InitialTradeGold = defaults.InitialTradeGold;
136:            rejected = true;
137:        }
138:
139:        if (c.MaxGoldPerCategory < 100 || c.MaxGoldPerCategory > 20000)
140:        {
141:            _logger.LogWarning($"CaravanTradeConfigProvider: maxGoldPerCategory={c.MaxGoldPerCategory} must be in [100,20000], reverting to {defaults.MaxGoldPerCategory}");
142:            c.MaxGoldPerCategory = defaults.MaxGoldPerCategory;
143:            rejected = true;
144:        }
145:
146:        if (rejected)
147:            _logger.LogWarning("CaravanTradeConfigProvider: caravan_trade_config.json contained invalid values. See prior warnings for details.");
148:        else
149:            _logger.LogInfo("CaravanTradeConfigProvider: Loaded caravan_trade_config.json");
150:
151:        return c;
152:    }
153:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:12:08.099936Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\ICaravanVisitMemory.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\ICaravanTradeSettingsProvider.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\_Module\\ModuleData\\caravan_trade\\caravan_trade_config.json" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /i \"Home Anti Shuttle homeDistanceReweight antiShuttlePenalty rubber\" docs\\features\\caravan-trade.md" in E:\repos\TAOM
 succeeded in 480ms:
1:namespace TAOM.Features.CaravanTrade;
2:
3:/// <summary>
4:/// Per-caravan short-term memory of the last few towns each caravan visited, used by the
5:/// <c>GetTradeScoreForTown</c> reweight to penalize just-visited towns so caravans stop
6:/// shuttling between the nearest two and circulate instead.
7:///
8:/// Pure + TaleWorlds-free (ADR-007): the thin <see cref="CaravanVisitMemoryBehavior"/> and the
9:/// score hook convert sealed engine types (<c>MobileParty</c>/<c>Settlement</c>) to their
10:/// <c>StringId</c> at the boundary. State is ephemeral (no <c>SyncData</c>) — it rebuilds as
11:/// caravans move, matching the feature's save-clean design.
12:///
13:/// Replaces the previous <c>isJustLeft = town == LastVisitedSettlement</c> logic, which was inert:
14:/// <c>LastVisitedSettlement</c> equals the caravan's CURRENT (parked) town at decision time, and
15:/// vanilla already excludes the current town from candidates — so the old anti-shuttle penalty
16:/// never fired on a selectable town.
17:/// </summary>
18:public interface ICaravanVisitMemory
19:{
20:    /// <summary>Record that <paramref name="caravanId"/> just entered town <paramref name="townId"/>.</summary>
21:    void RecordVisit(string caravanId, string townId);
22:
23:    /// <summary>
24:    /// Multiplicative recency penalty for <paramref name="townId"/> as a destination for
25:    /// <paramref name="caravanId"/>. Returns <c>1.0</c> for a never/long-ago-visited town (no
26:    /// penalty) and a smaller value (down to a strictly-positive floor) for a recently-visited one.
27:    /// Range is <c>(0, 1]</c> — never zero, so a penalized town in a sparse region still scores
28:    /// positive and can be picked (no stranding).
29:    /// </summary>
30:    float GetRecencyPenaltyFactor(string caravanId, string townId);
31:
32:    /// <summary>Drop all memory for a caravan (called when the caravan is destroyed).</summary>
33:    void Clear(string caravanId);
34:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 478ms:
1:namespace TAOM.Features.CaravanTrade;
2:
3:/// <summary>
4:/// Single merged read surface for the CaravanTrade feature: MCM-over-JSON. For fields exposed in
5:/// MCM the implementation reads <c>TaomSettings.Instance?.X</c> and falls back to the validated
6:/// JSON config; JSON-only advanced fields come straight from the config. The service depends only
7:/// on this interface (plus <see cref="Execution.IAlignmentService"/> + logger), so all validation
8:/// and the MCM/JSON merge live in one place.
9:/// </summary>
10:public interface ICaravanTradeSettingsProvider
11:{
12:    /// <summary>Master toggle. Off ⇒ every service method returns the vanilla value ⇒ exact vanilla behavior.</summary>
13:    bool Enabled { get; }
14:
15:    /// <summary>Whether the changes apply to player-owned caravans too (else only NPC caravans change).</summary>
16:    bool ApplyToPlayerCaravans { get; }
17:
18:    /// <summary>Multiplier on the vanilla "very far" distance ceiling — how much further caravans may range.</summary>
19:    float RangeMultiplier { get; }
20:
21:    /// <summary>Distance-decay exponent (alpha) in <c>1/(nearFieldFlatten+days)^alpha</c>. Lower ⇒ ranges further.</summary>
22:    float DistanceDecayExponent { get; }
23:
24:    /// <summary>Days added inside the decay denominator so near towns tie on distance and profit decides.</summary>
25:    float NearFieldFlattenDays { get; }
26:
27:    /// <summary>Upper clamp on the score multiplier so one hyper-profitable far town can't pull caravans map-wide.</summary>
28:    float MaxCompensation { get; }
29:
30:    /// <summary>Recency penalty strength: max fractional cut on the most-recently-visited town, decaying over the last few towns visited (0 = off, 1 = fully deprioritize).</summary>
31:    float AntiShuttlePenalty { get; }
32:
33:    /// <summary>Whether to distance-compress the home town like any other (true, default = fixes the home rubber-band) or keep the old home distance exemption (false).</summary>
34:    bool HomeDistanceReweight { get; }
35:
36:    /// <summary>Resolved war-trade policy (MCM dropdown over validated JSON string).</summary>
37:    WarTradePolicy WarTradePolicy { get; }
38:
39:    /// <summary>Floor applied to vanilla's per-caravan budget factor so more categories clear the buy gate.</summary>
40:    float BudgetFactorFloor { get; }
41:
42:    /// <summary>Starting trade-gold floor (vanilla 10000) — higher saturates budgetFactor for fuller baskets.</summary>
43:    int InitialTradeGold { get; }
44:
45:    /// <summary>Per-item-category gold cap (vanilla 1500).</summary>
46:    int MaxGoldPerCategory { get; }
47:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 426ms:
1:{
2:  "_comment": "AI/player caravan trade tuning (TAOM CaravanTrade feature). Master toggle + Range Multiplier + War Trade Policy + Basket Diversity Floor are also on the MCM 'Caravan Trade' page and override the matching fields below at runtime; the rest are JSON-only advanced knobs. Singleton-cached: edits require an app restart. Invalid values revert to these defaults with a warning in the log.",
3:
4:  "enabled": true,
5:  "applyToPlayerCaravans": true,
6:
7:  "_comment_range": "Lever 2/3 — how far caravans range and how strongly distance is rewarded. rangeMultiplier scales the vanilla 'very far' ceiling [1..4]. The re-weight strips vanilla's 1/days spike and re-applies 1/(nearFieldFlattenDays+days)^distanceDecayExponent, clamped by maxCompensation. Lower distanceDecayExponent = ranges further. nearFieldFlattenDays ties near towns so profit decides.",
8:  "rangeMultiplier": 1.6,
9:  "distanceDecayExponent": 0.5,
10:  "nearFieldFlattenDays": 2.0,
11:  "maxCompensation": 6.0,
12:
13:  "_comment_recency": "Anti-shuttle recency memory. antiShuttlePenalty [0..1] = max score cut on the most-recently-visited town, decaying over the caravan's last 4 visited towns (a caravan is pushed off the 2-4 towns it just orbited toward fresh ones). Raise toward 0.6-0.7 if shuttling persists. homeDistanceReweight (true = fix the 'home rubber-band' by distance-compressing the home town like any other; false = restore the old home exemption if caravans return home too rarely — vanilla home-gravity is preserved either way, so payouts still get delivered home).",
14:  "antiShuttlePenalty": 0.5,
15:  "homeDistanceReweight": true,
16:
17:  "_comment_war": "Lever 1 — one of None (vanilla, war blocks trade), IgnoreWar (trade anywhere non-besieged), SameAlignmentAndNeutral (default: trade with same-side or neutral factions but not the enemy side).",
18:  "warTradePolicy": "SameAlignmentAndNeutral",
19:
20:  "_comment_diversity": "Lever 4 — fuller baskets. budgetFactorFloor [0..1] raises poor caravans' buying power so more categories clear the buy gate. initialTradeGold is a floor (never lowers vanilla's large/main-hero bonus). maxGoldPerCategory defaults to vanilla 1500.",
21:  "budgetFactorFloor": 0.35,
22:  "initialTradeGold": 15000,
23:  "maxGoldPerCategory": 1500
24:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 1027ms:
11:1. **Distance is a penalty, not a reward.** `GetTradeScoreForTown` multiplies expected profit by `1/days` (land) plus an escalating `veryFarAddition`, and `distanceCut` hard-rejects towns past ~5× the average nearest-two-town distance. The closest town almost always wins the argmax → the shuttle.
25:| 2 | **Range re-weight + recency penalty** | `GetTradeScoreForTown(...)` postfix (`ref float __result`) + `CaravanVisitMemoryBehavior` | Recomputes raw travel days from the same public inputs vanilla used (`AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty` + caravan-speed props), strips vanilla's `1/days` spike and re-applies `1/(nearFieldFlatten+days)^decayExponent` clamped by `maxCompensation`; then multiplies by a per-caravan **recency penalty** (from `ICaravanVisitMemory`) that deprioritizes the last few towns visited so caravans circulate. The home town is compressed like any other (`homeDistanceReweight`, default on — fixes the home rubber-band) while vanilla's upstream home-gravity (`num5`) is preserved. Selection-only; profit + payout untouched. Naval passes through. Scoped per **clan** (`Owner.Clan == PlayerClan`). |
35:`caravan_trade_config.json` → `CaravanTradeConfigProvider` (validate-and-fall-back) → `CaravanTradeSettingsProvider` (MCM-over-JSON merge) → `CaravanTradeService` (pure decisions) ← the 4 hooks + `TaomCaravanModel`. For the recency lever, `CaravanVisitMemoryBehavior` records town entries into the singleton `ICaravanVisitMemory`, and the `GetTradeScoreForTown` hook reads the recency penalty from it and passes it into `CaravanTradeService.ReweightTradeScore` (so the `IsActiveFor` player-scope gate governs it). War policy additionally consults `IAlignmentService` (Execution feature) — resolving `GetKingdomSide` directly and branching on `FactionSide.Neutral`, **not** `AreEnemyAlignments` (whose Neutral-as-enemy-of-everyone semantics are inverted for this purpose — see RCA below).
49:| `antiShuttlePenalty` | `0.5` | [0, 1] | JSON | Recency penalty strength: max score cut on the most-recently-visited town, decaying over the caravan's last 4 visited towns. Raise toward 0.6–0.7 if shuttling persists. |
50:| `homeDistanceReweight` | `true` | — | JSON | `true` = distance-compress the home town like any other (fixes the home rubber-band); `false` = restore the old home distance exemption if caravans return home too rarely. Home-gravity preserved either way. |
84:- `TAOM.Tests/Features/CaravanTrade/CaravanTradeServiceTests.cs` — every lever + the war-policy matrix (same-side / opposite-side / **Neutral-on-each-side** regression) + NaN/disabled/player-scope gates + the home-compression regression (`ReweightTradeScore_HomeTown_NowCompressed`) + recency-factor + NaN-factor gates.
103:- **Home rubber-band — FIXED (2026-07-11).** The original home exemption kept the home town's full `1/days` near-field spike while non-home towns were compressed, so a caravan homed at a hub (e.g. Minas Tirith) re-selected home the moment it parked at any neighbor — "leaves and immediately returns." Two root causes: (1) the old anti-shuttle penalty was **inert** — it keyed on `LastVisitedSettlement`, which equals the parked/current town at decision time (that town is already excluded by vanilla), so it never fired on a selectable town; (2) the home distance exemption. Fix: a per-caravan **recency memory** (`ICaravanVisitMemory`) penalizes the genuinely-previous towns, and the home town is now distance-compressed like any other (`homeDistanceReweight`, default on). Vanilla's upstream home-gravity (`num5`) is preserved, and caravan income is paid to the owner wherever the caravan is (verified: `DefaultClanFinanceModel.AddIncomeFromParty` is not home-gated), so payouts are unaffected. Escape hatch: set `homeDistanceReweight=false` if playtest shows home visits are too rare. Known residual: the recency memory enlarges the loop to ~5 distinct towns rather than guaranteeing map-wide circulation (tunable via `antiShuttlePenalty`).
104:- **Naval caravans unchanged:** the shuttle is a land problem; naval caravans pass through vanilla (naval travel is parked in TAOM anyway, #296).
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:12:14.982109Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
The config path looks internally consistent so far: DTO defaults, JSON, settings provider, and doc table all show `antiShuttlePenalty=0.5` and `homeDistanceReweight=true`; the provider also copies the new bool through its validated clone. I’m now reading the ring behavior tests and source with line numbers to check off-by-one and NaN polarity.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\CaravanVisitMemory.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\CaravanVisitMemoryBehavior.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" TAOM.Tests'"\\Features\\CaravanTrade\\CaravanVisitMemoryTests.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /i \"Recency NaN Home Disabled Player AntiShuttle HomeDistance ReweightTradeScore\" TAOM.Tests\\Features\\CaravanTrade\\CaravanTradeServiceTests.cs TAOM.Tests\\Features\\CaravanTrade\\CaravanTradeConfigProviderTests.cs" in E:\repos\TAOM
 succeeded in 449ms:
1:using TaleWorlds.CampaignSystem;
2:using TaleWorlds.CampaignSystem.Party;
3:using TaleWorlds.CampaignSystem.Settlements;
4:
5:namespace TAOM.Features.CaravanTrade;
6:
7:/// <summary>
8:/// Thin entry point (ADR-002, no logic) that feeds <see cref="ICaravanVisitMemory"/>. Records each
9:/// caravan's town entries via <c>SettlementEntered</c> (the same signal vanilla's own caravan
10:/// bookkeeping uses) so the <c>GetTradeScoreForTown</c> reweight can penalize just-visited towns, and
11:/// evicts a caravan's memory on <c>MobilePartyDestroyed</c> so the dictionary stays bounded to live
12:/// caravans over a long campaign.
13:///
14:/// Registered unconditionally (not gated on the master toggle) so a mid-session toggle-on works
15:/// immediately; the recording is a cheap dictionary write per caravan town-entry and the penalty only
16:/// bites when the reweight service is active. No <c>SyncData</c> — the memory is ephemeral runtime
17:/// state that rebuilds as caravans move (matches the feature's save-clean design; the worst case is
18:/// one sub-optimal hop after a load before the ring repopulates).
19:/// </summary>
20:public class CaravanVisitMemoryBehavior : CampaignBehaviorBase
21:{
22:    private readonly ICaravanVisitMemory _memory;
23:
24:    public CaravanVisitMemoryBehavior(ICaravanVisitMemory memory)
25:    {
26:        _memory = memory;
27:    }
28:
29:    public override void RegisterEvents()
30:    {
31:        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
32:        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
33:    }
34:
35:    public override void SyncData(IDataStore dataStore)
36:    {
37:        // No persistence — ephemeral recency memory, rebuilt from live caravan movement.
38:    }
39:
40:    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
41:    {
42:        // Candidate space is Town.AllTowns, so only town entries matter; village/castle entries would
43:        // waste a ring slot without ever matching a candidate.
44:        if (party == null || settlement == null || !settlement.IsTown || !party.IsCaravan)
45:            return;
46:
47:        _memory.RecordVisit(party.StringId, settlement.StringId);
48:    }
49:
50:    private void OnMobilePartyDestroyed(MobileParty party, PartyBase partyBase)
51:    {
52:        // Clear unconditionally on a valid id — a no-op for a party that was never recorded, and
53:        // robust against IsCaravan being reset during teardown. Mirrors vanilla's own per-caravan
54:        // dictionary eviction in CaravansCampaignBehavior.OnMobilePartyDestroyed.
55:        if (party != null)
56:            _memory.Clear(party.StringId);
57:    }
58:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 460ms:
1:using System.Collections.Generic;
2:using TAOM.Core.Validation;
3:
4:namespace TAOM.Features.CaravanTrade;
5:
6:/// <summary>
7:/// Pure implementation of <see cref="ICaravanVisitMemory"/>. Per caravan, a bounded ring of the last
8:/// <see cref="MemoryDepth"/> distinct-in-a-row entered towns (newest last). The penalty for a
9:/// candidate town scales with how recently it was visited (rank 0 = most recent), using the
10:/// configured <see cref="ICaravanTradeSettingsProvider.AntiShuttlePenalty"/> as the max strength, and
11:/// is floored at <see cref="MinRecencyFactor"/> so it never turns a positive score into a rejection.
12:///
13:/// Depth 4 kills 2-, 3-, and 4-town orbits (the previous town lands at rank 1, penalized strongly)
14:/// while leaving the rest of the map unpenalized. It is a <c>const</c>, not config — a tiny tuning
15:/// knob is not worth a new validated surface (simplicity-criterion / YAGNI); the penalty magnitude is
16:/// tunable via the existing <c>antiShuttlePenalty</c>.
17:/// </summary>
18:public class CaravanVisitMemory : ICaravanVisitMemory
19:{
20:    private const int MemoryDepth = 4;
21:
22:    /// <summary>Strictly-positive floor so a penalized lone candidate still outscores a non-candidate (no stranding).</summary>
23:    private const float MinRecencyFactor = 0.05f;
24:
25:    private readonly ICaravanTradeSettingsProvider _settings;
26:    private readonly Dictionary<string, List<string>> _visits = new();
27:
28:    public CaravanVisitMemory(ICaravanTradeSettingsProvider settings)
29:    {
30:        _settings = settings;
31:    }
32:
33:    public void RecordVisit(string caravanId, string townId)
34:    {
35:        if (string.IsNullOrEmpty(caravanId) || string.IsNullOrEmpty(townId))
36:            return;
37:
38:        if (!_visits.TryGetValue(caravanId, out var ring))
39:        {
40:            ring = new List<string>(MemoryDepth);
41:            _visits[caravanId] = ring;
42:        }
43:
44:        // Collapse consecutive re-entries of the same town (re-entering the current town, or a
45:        // possible double-fire) so they don't consume the whole ring with one id.
46:        if (ring.Count > 0 && ring[ring.Count - 1] == townId)
47:            return;
48:
49:        ring.Add(townId);
50:        while (ring.Count > MemoryDepth)
51:            ring.RemoveAt(0);
52:    }
53:
54:    public float GetRecencyPenaltyFactor(string caravanId, string townId)
55:    {
56:        if (string.IsNullOrEmpty(caravanId) || string.IsNullOrEmpty(townId))
57:            return 1f;
58:
59:        if (!_visits.TryGetValue(caravanId, out var ring))
60:            return 1f;
61:
62:        // Rank 0 = newest (last element). Use the MOST RECENT occurrence of a revisited town.
63:        int rank = -1;
64:        for (int i = ring.Count - 1; i >= 0; i--)
65:        {
66:            if (ring[i] == townId)
67:            {
68:                rank = ring.Count - 1 - i;
69:                break;
70:            }
71:        }
72:
73:        if (rank < 0)
74:            return 1f; // not recently visited
75:
76:        float strength = _settings.AntiShuttlePenalty;
77:        if (!FiniteFloatValidator.IsFiniteInRange(strength, 0f, 1f))
78:            return 1f; // bad strength -> no penalty (never emit a corrupted factor)
79:
80:        // Linear decay by rank: rank 0 -> weight 1, older -> smaller weight.
81:        float weight = (float)(MemoryDepth - rank) / MemoryDepth;
82:        float factor = 1f - strength * weight;
83:
84:        return factor < MinRecencyFactor ? MinRecencyFactor : factor;
85:    }
86:
87:    public void Clear(string caravanId)
88:    {
89:        if (!string.IsNullOrEmpty(caravanId))
90:            _visits.Remove(caravanId);
91:    }
92:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 432ms:
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:26:        _settings.ApplyToPlayerCaravans.Returns(true);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:31:        _settings.AntiShuttlePenalty.Returns(0.5f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:32:        _settings.HomeDistanceReweight.Returns(true);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:41:    // ---------------- ReweightTradeScore ----------------
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:44:    public void ReweightTradeScore_Disabled_ReturnsRawScore()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:47:        Assert.AreEqual(42f, _sut.ReweightTradeScore(42f, 3f, false, false, 1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:51:    public void ReweightTradeScore_PlayerCaravanWhenPlayerScopeOff_ReturnsRawScore()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:53:        _settings.ApplyToPlayerCaravans.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:54:        Assert.AreEqual(42f, _sut.ReweightTradeScore(42f, 3f, false, false, 1f, isPlayerCaravan: true), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:58:    public void ReweightTradeScore_RejectionScore_PassesThroughUnchanged()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:61:        Assert.AreEqual(-1f, _sut.ReweightTradeScore(-1f, 3f, false, false, 1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:62:        Assert.AreEqual(0f, _sut.ReweightTradeScore(0f, 3f, false, false, 1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:66:    public void ReweightTradeScore_NaNRawScore_ReturnsRawScore()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:68:        // Positive-requirement gate: NaN must fail into the vanilla passthrough.
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:69:        Assert.IsTrue(float.IsNaN(_sut.ReweightTradeScore(float.NaN, 3f, false, false, 1f, false)));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:73:    public void ReweightTradeScore_NonPositiveDays_ReturnsRawScore()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:75:        // days<=0 skips the distance reweight; a neutral recency factor leaves rawScore unchanged.
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:76:        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 0f, false, false, 1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:80:    public void ReweightTradeScore_NaNDays_ReturnsRawScore()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:82:        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, float.NaN, false, false, 1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:86:    public void ReweightTradeScore_NavalCaravan_ReturnsRawScoreUnchanged()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:89:        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 3f, isNaval: true, false, 1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:93:    public void ReweightTradeScore_HomeTown_NowCompressed()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:95:        // Home-rubber-band regression: with HomeDistanceReweight on (default), the home town is
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:96:        // distance-compressed identically to a non-home town at the same days — it no longer passes
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:98:        float home = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:99:        float nonHome = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:100:        Assert.AreEqual(nonHome, home, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:101:        Assert.IsTrue(home < 10f, $"home should be compressed below raw, was {home}");
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:105:    public void ReweightTradeScore_HomeTown_EscapeHatchOff_PassesDistanceButRecencyStillApplies()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:107:        // HomeDistanceReweight off restores the old home distance exemption: raw distance passes
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:108:        // through, but the recency penalty still applies to home.
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:109:        _settings.HomeDistanceReweight.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:110:        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:111:        Assert.AreEqual(5f, _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 0.5f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:115:    public void ReweightTradeScore_RecencyAppliedToHome()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:117:        // With the escape hatch on (default), home is compressed AND recency-penalized.
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:118:        float noPenalty = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:119:        float penalized = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 0.5f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:124:    public void ReweightTradeScore_LandTown_AppliesStripAndReweight()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:127:        float result = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:132:    public void ReweightTradeScore_MoreProfitableFarTown_BeatsCloseTown_WhereVanillaWouldNot()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:137:        float near = _sut.ReweightTradeScore(100f, 1f, false, false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:138:        float far = _sut.ReweightTradeScore(40f, 5f, false, false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:143:    public void ReweightTradeScore_EqualBaseProfit_StillPrefersNear_ButCompressed()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:146:        float near = _sut.ReweightTradeScore(100f, 1f, false, false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:147:        float far = _sut.ReweightTradeScore(20f, 5f, false, false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:153:    public void ReweightTradeScore_VeryFarTown_MultiplierClampedToMaxCompensation()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:156:        float result = _sut.ReweightTradeScore(10f, 1000f, false, false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:161:    public void ReweightTradeScore_RecencyFactor_MultipliesResult()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:163:        // A recency factor multiplies the reweighted result (the working anti-shuttle penalty).
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:164:        float full = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:165:        float cut = _sut.ReweightTradeScore(10f, 3f, false, false, 0.65f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:170:    public void ReweightTradeScore_NaNRecencyFactor_NoPenaltyApplied()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:172:        // Engine-Float gate: a NaN factor must be ignored (finite result == the un-penalized reweight).
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:173:        float full = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:174:        float withNaN = _sut.ReweightTradeScore(10f, 3f, false, false, float.NaN, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:175:        Assert.AreEqual(full, withNaN, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:179:    public void ReweightTradeScore_RecencyFactorOutOfRange_Ignored()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:181:        float full = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:182:        Assert.AreEqual(full, _sut.ReweightTradeScore(10f, 3f, false, false, 1.5f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:183:        Assert.AreEqual(full, _sut.ReweightTradeScore(10f, 3f, false, false, -0.1f, false), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:189:    public void ScaleVeryFarDistance_Disabled_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:204:    public void AllowWartimeTrade_Disabled_ReturnsFalse()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:211:    public void AllowWartimeTrade_PlayerCaravanWhenPlayerScopeOff_ReturnsFalse()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:213:        _settings.ApplyToPlayerCaravans.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:214:        Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "gondor", "rohan", "rohan", isPlayerCaravan: true));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:271:    public void AllowWartimeTrade_PlayerFoundedKingdom_SidedByCulture_BlocksAcrossLine()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:273:        // A player-founded kingdom (id "new_kingdom") is absent from alignment.json -> GetKingdomSide
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:276:        // WarOfTheRingMomentum's player-founded-kingdom culture fallback.)
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:284:    public void AllowWartimeTrade_PlayerFoundedKingdom_SidedByCulture_AllowsSameSide()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:295:    public void ApplyBudgetFactorFloor_Disabled_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:302:    public void ApplyBudgetFactorFloor_PlayerScopeOff_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:304:        _settings.ApplyToPlayerCaravans.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:305:        Assert.AreEqual(0.1f, _sut.ApplyBudgetFactorFloor(0.1f, isPlayerCaravan: true), 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:309:    public void ApplyBudgetFactorFloor_NaN_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:311:        Assert.IsTrue(float.IsNaN(_sut.ApplyBudgetFactorFloor(float.NaN, false)));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:329:    public void ResolveInitialTradeGold_Disabled_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:336:    public void ResolveInitialTradeGold_PlayerScopeOff_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:338:        _settings.ApplyToPlayerCaravans.Returns(false);
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:339:        Assert.AreEqual(10000, _sut.ResolveInitialTradeGold(10000, isPlayerCaravan: true));
TAOM.Tests\Features\CaravanTrade\CaravanTradeServiceTests.cs:358:    public void ResolveMaxGoldPerCategory_Disabled_ReturnsVanilla()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:48:  ""applyToPlayerCaravans"": false,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:53:  ""antiShuttlePenalty"": 0.5,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:54:  ""homeDistanceReweight"": false,
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:64:        Assert.IsFalse(c.ApplyToPlayerCaravans);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:69:        Assert.AreEqual(0.5f, c.AntiShuttlePenalty, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:70:        Assert.IsFalse(c.HomeDistanceReweight);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:136:    public void GetConfig_NaNRangeMultiplier_RevertsToFiniteDefault()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:138:        WriteConfig(@"{ ""rangeMultiplier"": NaN }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:168:    public void GetConfig_AntiShuttlePenaltyAboveOne_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:171:        WriteConfig(@"{ ""antiShuttlePenalty"": 1.5 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:172:        Assert.AreEqual(0.5f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:173:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:177:    public void GetConfig_NegativeAntiShuttlePenalty_RevertsAndWarns()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:180:        WriteConfig(@"{ ""antiShuttlePenalty"": -0.2 }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:181:        Assert.AreEqual(0.5f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:182:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:186:    public void GetConfig_MissingHomeDistanceReweight_DefaultsTrue()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:189:        Assert.IsTrue(_sut.GetConfig().HomeDistanceReweight);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:193:    public void GetConfig_HomeDistanceReweightFalse_Honored()
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:195:        WriteConfig(@"{ ""homeDistanceReweight"": false }");
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:196:        Assert.IsFalse(_sut.GetConfig().HomeDistanceReweight);
TAOM.Tests\Features\CaravanTrade\CaravanTradeConfigProviderTests.cs:198:        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("homeDistanceReweight")));
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 449ms:
1:using Microsoft.VisualStudio.TestTools.UnitTesting;
2:using NSubstitute;
3:using TAOM.Features.CaravanTrade;
4:
5:namespace TAOM.Tests.Features.CaravanTrade;
6:
7:[TestClass]
8:public class CaravanVisitMemoryTests
9:{
10:    private ICaravanTradeSettingsProvider _settings = null!;
11:    private CaravanVisitMemory _sut = null!;
12:
13:    private const string Caravan = "caravan_1";
14:
15:    [TestInitialize]
16:    public void Setup()
17:    {
18:        _settings = Substitute.For<ICaravanTradeSettingsProvider>();
19:        _settings.AntiShuttlePenalty.Returns(0.5f); // strength
20:        _sut = new CaravanVisitMemory(_settings);
21:    }
22:
23:    // ---------------- no-penalty / passthrough ----------------
24:
25:    [TestMethod]
26:    public void GetRecencyPenaltyFactor_UnknownCaravan_ReturnsOne()
27:    {
28:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor("nobody", "town_A"), 0.0001f);
29:    }
30:
31:    [TestMethod]
32:    public void GetRecencyPenaltyFactor_TownNotVisited_ReturnsOne()
33:    {
34:        _sut.RecordVisit(Caravan, "town_A");
35:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_Z"), 0.0001f);
36:    }
37:
38:    [TestMethod]
39:    public void GetRecencyPenaltyFactor_NullOrEmptyIds_ReturnsOne()
40:    {
41:        _sut.RecordVisit(Caravan, "town_A");
42:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(null!, "town_A"), 0.0001f);
43:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, ""), 0.0001f);
44:    }
45:
46:    [TestMethod]
47:    public void RecordVisit_NullIds_DoesNotThrow()
48:    {
49:        _sut.RecordVisit(null!, "town_A");
50:        _sut.RecordVisit(Caravan, null!);
51:        // No entry recorded -> no penalty.
52:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
53:    }
54:
55:    // ---------------- the core fix: previous town IS penalized ----------------
56:
57:    [TestMethod]
58:    public void GetRecencyPenaltyFactor_PreviousTown_IsPenalized()
59:    {
60:        // Regression sentinel for the inert-penalty bug. Caravan went A -> B; now parked at B and
61:        // deciding the next hop. The PREVIOUS town A must get a live penalty (< 1.0). The old design
62:        // (isJustLeft == LastVisitedSettlement == the parked/current town) produced NO penalty on any
63:        // selectable town.
64:        _sut.RecordVisit(Caravan, "town_A");
65:        _sut.RecordVisit(Caravan, "town_B");
66:
67:        float previous = _sut.GetRecencyPenaltyFactor(Caravan, "town_A"); // rank 1
68:        Assert.IsTrue(previous < 1f, $"previous town must be penalized, was {previous}");
69:    }
70:
71:    // ---------------- recency decay ----------------
72:
73:    [TestMethod]
74:    public void GetRecencyPenaltyFactor_MostRecentTown_ReturnsStrongestPenalty()
75:    {
76:        _sut.RecordVisit(Caravan, "town_A");
77:        _sut.RecordVisit(Caravan, "town_B");
78:        _sut.RecordVisit(Caravan, "town_C");
79:        _sut.RecordVisit(Caravan, "town_D"); // newest
80:
81:        float d = _sut.GetRecencyPenaltyFactor(Caravan, "town_D"); // rank 0
82:        float c = _sut.GetRecencyPenaltyFactor(Caravan, "town_C"); // rank 1
83:        float a = _sut.GetRecencyPenaltyFactor(Caravan, "town_A"); // rank 3
84:        Assert.IsTrue(d < c, $"newest ({d}) must be penalized more than older ({c})");
85:        Assert.IsTrue(c < a, $"middle ({c}) must be penalized more than oldest ({a})");
86:    }
87:
88:    [TestMethod]
89:    public void GetRecencyPenaltyFactor_OlderTown_ReturnsWeakerPenalty()
90:    {
91:        _sut.RecordVisit(Caravan, "town_A");
92:        _sut.RecordVisit(Caravan, "town_B");
93:        _sut.RecordVisit(Caravan, "town_C");
94:        _sut.RecordVisit(Caravan, "town_D");
95:
96:        // rank 1 = C: 1 - 0.5*(4-1)/4 = 1 - 0.375 = 0.625.
97:        Assert.AreEqual(0.625f, _sut.GetRecencyPenaltyFactor(Caravan, "town_C"), 0.0001f);
98:        // rank 3 = A: 1 - 0.5*(4-3)/4 = 1 - 0.125 = 0.875.
99:        Assert.AreEqual(0.875f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
100:    }
101:
102:    [TestMethod]
103:    public void GetRecencyPenaltyFactor_RevisitedTown_UsesMostRecentRank()
104:    {
105:        // C, A, B, A -> A appears at rank 3 (old) and rank 0 (new). Must use the most recent (rank 0),
106:        // i.e. the strongest penalty, not the stale weaker one.
107:        _sut.RecordVisit(Caravan, "town_C");
108:        _sut.RecordVisit(Caravan, "town_A");
109:        _sut.RecordVisit(Caravan, "town_B");
110:        _sut.RecordVisit(Caravan, "town_A"); // newest, rank 0
111:
112:        // rank 0: 1 - 0.5*1 = 0.5.
113:        Assert.AreEqual(0.5f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
114:    }
115:
116:    // ---------------- bounded ring ----------------
117:
118:    [TestMethod]
119:    public void GetRecencyPenaltyFactor_RingBoundedToK_EvictsOldest()
120:    {
121:        // 5 distinct towns, depth 4 -> the oldest (A) is evicted and no longer penalized.
122:        _sut.RecordVisit(Caravan, "town_A");
123:        _sut.RecordVisit(Caravan, "town_B");
124:        _sut.RecordVisit(Caravan, "town_C");
125:        _sut.RecordVisit(Caravan, "town_D");
126:        _sut.RecordVisit(Caravan, "town_E");
127:
128:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
129:        Assert.IsTrue(_sut.GetRecencyPenaltyFactor(Caravan, "town_B") < 1f);
130:    }
131:
132:    [TestMethod]
133:    public void RecordVisit_ConsecutiveSameTown_DoesNotConsumeRing()
134:    {
135:        // Re-entering the same town repeatedly must not evict the earlier towns.
136:        _sut.RecordVisit(Caravan, "town_A");
137:        _sut.RecordVisit(Caravan, "town_B");
138:        _sut.RecordVisit(Caravan, "town_B");
139:        _sut.RecordVisit(Caravan, "town_B");
140:
141:        Assert.IsTrue(_sut.GetRecencyPenaltyFactor(Caravan, "town_A") < 1f, "A must still be remembered");
142:    }
143:
144:    // ---------------- no stranding ----------------
145:
146:    [TestMethod]
147:    public void GetRecencyPenaltyFactor_NeverReturnsZero_NoStranding()
148:    {
149:        // Max strength: even the most-recent town keeps a strictly-positive floor so it still
150:        // outscores a non-candidate (score 0 / -1) and a sparse-region caravan is never stranded.
151:        _settings.AntiShuttlePenalty.Returns(1.0f);
152:        _sut.RecordVisit(Caravan, "town_A");
153:        _sut.RecordVisit(Caravan, "town_B");
154:        _sut.RecordVisit(Caravan, "town_C");
155:        _sut.RecordVisit(Caravan, "town_D");
156:
157:        foreach (var t in new[] { "town_A", "town_B", "town_C", "town_D" })
158:            Assert.IsTrue(_sut.GetRecencyPenaltyFactor(Caravan, t) > 0f, $"{t} must stay positive");
159:
160:        // rank 0 at strength 1 would be 0 -> floored to 0.05.
161:        Assert.AreEqual(0.05f, _sut.GetRecencyPenaltyFactor(Caravan, "town_D"), 0.0001f);
162:    }
163:
164:    // ---------------- strength gates ----------------
165:
166:    [TestMethod]
167:    public void GetRecencyPenaltyFactor_StrengthZero_ReturnsOne()
168:    {
169:        _settings.AntiShuttlePenalty.Returns(0f);
170:        _sut.RecordVisit(Caravan, "town_A");
171:        _sut.RecordVisit(Caravan, "town_B");
172:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
173:    }
174:
175:    [TestMethod]
176:    public void GetRecencyPenaltyFactor_NaNStrength_ReturnsOne()
177:    {
178:        // NaN gate: a corrupted strength must not emit a NaN/garbage factor.
179:        _settings.AntiShuttlePenalty.Returns(float.NaN);
180:        _sut.RecordVisit(Caravan, "town_A");
181:        _sut.RecordVisit(Caravan, "town_B");
182:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
183:    }
184:
185:    [TestMethod]
186:    public void GetRecencyPenaltyFactor_OutOfRangeStrength_ReturnsOne()
187:    {
188:        _settings.AntiShuttlePenalty.Returns(1.5f);
189:        _sut.RecordVisit(Caravan, "town_A");
190:        _sut.RecordVisit(Caravan, "town_B");
191:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
192:    }
193:
194:    // ---------------- Clear ----------------
195:
196:    [TestMethod]
197:    public void Clear_RemovesCaravanMemory_ReturnsOneAfter()
198:    {
199:        _sut.RecordVisit(Caravan, "town_A");
200:        _sut.RecordVisit(Caravan, "town_B");
201:        Assert.IsTrue(_sut.GetRecencyPenaltyFactor(Caravan, "town_A") < 1f);
202:
203:        _sut.Clear(Caravan);
204:        Assert.AreEqual(1f, _sut.GetRecencyPenaltyFactor(Caravan, "town_A"), 0.0001f);
205:    }
206:
207:    [TestMethod]
208:    public void Clear_UnknownCaravan_DoesNotThrow()
209:    {
210:        _sut.Clear("nobody");
211:        _sut.Clear(null!);
212:    }
213:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:12:25.075246Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
The ring implementation is newest-last, trims with `RemoveAt(0)`, and uses a backwards scan so revisits use the most recent occurrence. At default strength, the “previous town” factor after A→B is `1 - 0.5*(3/4) = 0.625`, so the remaining question is whether vanilla considers that previous town selectable at the parked decision; I’m verifying that from the installed decompile now.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Features\\CaravanTrade\\CaravanTradeService.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n \"AddBehavior CultureMarketplace CaravanVisitMemoryBehavior\" Main\\SubModule.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" TAOM.Tests'"\\Features\\CaravanTrade\\CaravanTradeServiceTests.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" TAOM.Tests'"\\Features\\CaravanTrade\\CaravanTradeConfigProviderTests.cs" in E:\repos\TAOM
 succeeded in 471ms:
482:        campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));
486:        campaignStarter.AddBehavior(new BannerInjectionBehavior(bannerInjectionService, bannerExclusionService));
490:        campaignStarter.AddBehavior(new CharacterCreationRegistrationBehavior(ccContentService, ccLogger));
494:        campaignStarter.AddBehavior(new TaomInitialChildGenerationBehavior(childGenService));
524:        campaignStarter.AddBehavior(new RaceAgeBehavior(raceAgeService, heroAgeAdapter, raceAgeLogger));
533:        campaignStarter.AddBehavior(new NazgulFamilyBehavior(nazgulRegistry, IoC.Resolve<IModLogger>()));
542:        campaignStarter.AddBehavior(new DiplomacyBehavior(diplomacyService, diplomacyLogger));
543:        campaignStarter.AddBehavior(new PlayerAllianceProposalBehavior(diplomacyService, diplomacyLogger));
549:        campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger));
552:        campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());
556:        campaignStarter.AddBehavior(new SiegeDefenseBehavior(siegeDefenseService, siegeDefenseLogger));
622:        campaignStarter.AddBehavior(specialResourceBehavior);
627:        campaignStarter.AddBehavior(IoC.Resolve<TroopCountDiagnosticsBehavior>());
633:        campaignStarter.AddBehavior(new CareerPersistenceBehavior(careerDataService, careerLogger));
636:        campaignStarter.AddBehavior(new CareerCampaignBehavior(
642:        campaignStarter.AddBehavior(new CareerSwitchDialogueBehavior(
648:        campaignStarter.AddBehavior(new Features.CareerSystem.Quests.CareerQuestCampaignBehavior(
684:        campaignStarter.AddBehavior(new StartupResourcesBehavior(goldService, influenceService, startupLogger));
687:        campaignStarter.AddBehavior(new NamedCompanionBehavior(namedCompanionService));
691:        campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.QuickActions.Hooks.InventorySearchCampaignBehavior>());
696:        campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.EquipPresets.Hooks.EquipmentPresetCampaignBehavior>());
700:        campaignStarter.AddBehavior(new FiefHubCampaignBehavior(
706:        campaignStarter.AddBehavior(new Features.CompanionTactics.FormationPresets.Hooks.FormationPresetCampaignBehavior(
713:        campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.Messengers.MessengerCampaignBehavior>());
715:        // CultureMarketplace (#207) — daily injection of LOTRLOME items into town markets
717:        campaignStarter.AddBehavior(new Features.CultureMarketplace.CultureMarketplaceBehavior(
718:            IoC.Resolve<Features.CultureMarketplace.ICultureItemPoolService>(),
719:            IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceInjectionService>(),
720:            IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceMaintenanceService>(),
722:            IoC.Resolve<Features.CultureMarketplace.Domain.MarketplaceTuning>(),
728:        campaignStarter.AddBehavior(IoC.Resolve<Features.CaravanTrade.CaravanVisitMemoryBehavior>());
733:        campaignStarter.AddBehavior(new CastleRecruitmentBehavior(
740:        campaignStarter.AddBehavior(new Features.AlignmentDesertion.Hooks.AlignmentDesertionBehavior(
746:        campaignStarter.AddBehavior(new Features.EliteEmissary.Hooks.EliteEmissaryBehavior(
756:        campaignStarter.AddBehavior(new Features.CultureConversion.Hooks.CultureConversionBehavior(
766:        campaignStarter.AddBehavior(new Features.LotrIssues.LotrIssuesCampaignBehavior(
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 471ms:
1:using System;
2:using TAOM.Core.Logging;
3:using TAOM.Core.Validation;
4:using TAOM.Features.Execution;
5:
6:namespace TAOM.Features.CaravanTrade;
7:
8:/// <summary>
9:/// Pure decision logic for the CaravanTrade feature. All four Harmony hooks plus the caravan
10:/// GameModel delegate here; no TaleWorlds types cross the boundary. See <see cref="ICaravanTradeService"/>.
11:/// </summary>
12:public class CaravanTradeService : ICaravanTradeService
13:{
14:    private readonly ICaravanTradeSettingsProvider _settings;
15:    private readonly IAlignmentService _alignment;
16:    private readonly IModLogger _logger;
17:
18:    public CaravanTradeService(ICaravanTradeSettingsProvider settings, IAlignmentService alignment, IModLogger logger)
19:    {
20:        _settings = settings;
21:        _alignment = alignment;
22:        _logger = logger;
23:    }
24:
25:    public float ReweightTradeScore(float rawScore, float days, bool isNaval, bool isHomeTown, float recencyPenaltyFactor, bool isPlayerCaravan)
26:    {
27:        if (!IsActiveFor(isPlayerCaravan))
28:            return rawScore;
29:
30:        // Positive-requirement gate: NaN rawScore and vanilla rejections (<= 0) pass through untouched.
31:        if (!(rawScore > 0f))
32:            return rawScore;
33:
34:        // Naval uses a different vanilla distance factor (not 1/days); the shuttle is a land problem.
35:        if (isNaval)
36:            return rawScore;
37:
38:        float result = rawScore;
39:
40:        // Strip vanilla's land 1/days spike and re-apply a gentler curve:
41:        //   newScore = rawScore * days / (nearFieldFlatten + days)^alpha
42:        // For an equal base profit P0 (rawScore = P0/days) this is P0 / (flatten+days)^alpha, so near
43:        // towns lose their runaway advantage and the built-in profit estimate becomes the differentiator.
44:        // The home town is compressed like everyone else (removing its "rubber-band" proximity edge)
45:        // UNLESS the HomeDistanceReweight escape hatch is off; either way vanilla's upstream home-gravity
46:        // (num5, already in rawScore) is untouched, so caravans still return home on the payout cadence.
47:        // Positive-requirement gate on days keeps NaN/non-positive days out of Math.Pow.
48:        bool applyDistanceReweight = (!isHomeTown || _settings.HomeDistanceReweight) && days > 0f;
49:        if (applyDistanceReweight)
50:        {
51:            double denom = Math.Pow(_settings.NearFieldFlattenDays + days, _settings.DistanceDecayExponent);
52:            float multiplier = denom > 0d ? (float)(days / denom) : 1f;
53:
54:            float maxComp = _settings.MaxCompensation;
55:            if (multiplier > maxComp)
56:                multiplier = maxComp;
57:
58:            result = rawScore * multiplier;
59:        }
60:
61:        // Recency penalty (home + non-home): deprioritize just-visited towns so caravans circulate.
62:        // Engine-Float gate: a NaN / out-of-range factor is ignored rather than emitting a corrupted
63:        // score. A valid factor is in (0,1], so this never turns a positive score into a rejection.
64:        if (FiniteFloatValidator.IsFiniteInRange(recencyPenaltyFactor, 0f, 1f))
65:            result *= recencyPenaltyFactor;
66:
67:        return result;
68:    }
69:
70:    public float ScaleVeryFarDistance(float vanillaVeryFarDays)
71:    {
72:        // Applied globally (the vanilla cache is a single shared field, not per-caravan) — it only
73:        // widens the candidate set; the re-weight and war gate remain player-scoped.
74:        if (!_settings.Enabled)
75:            return vanillaVeryFarDays;
76:        return vanillaVeryFarDays * _settings.RangeMultiplier;
77:    }
78:
79:    public bool AllowWartimeTrade(string caravanKingdomId, string caravanCultureId, string targetKingdomId, string targetCultureId, bool isPlayerCaravan)
80:    {
81:        // false = keep the vanilla war veto; true = lift it for this pairing.
82:        if (!IsActiveFor(isPlayerCaravan))
83:            return false;
84:
85:        switch (_settings.WarTradePolicy)
86:        {
87:            case WarTradePolicy.IgnoreWar:
88:                return true;
89:            case WarTradePolicy.SameAlignmentAndNeutral:
90:                // Resolve sides directly — do NOT use IAlignmentService.AreEnemyAlignments, whose Neutral
91:                // semantics are inverted for this purpose (it treats Neutral as an enemy of everyone). Here
92:                // Neutral on either side is a mercantile "trade with anyone", and otherwise only the same
93:                // side trades (Free↔Free / Evil↔Evil), never across the Free/Evil line. Mirrors the sibling
94:                // AlignmentRecruitment feature's deliberate work-around.
95:                var caravanSide = ResolveSide(caravanKingdomId, caravanCultureId);
96:                var targetSide = ResolveSide(targetKingdomId, targetCultureId);
97:                if (caravanSide == FactionSide.Neutral || targetSide == FactionSide.Neutral)
98:                    return true;
99:                return caravanSide == targetSide;
100:            case WarTradePolicy.None:
101:            default:
102:                return false;
103:        }
104:    }
105:
106:    // Side by kingdom id, falling back to culture id when the kingdom isn't classified in alignment.json.
107:    // A player-founded / dynamically created kingdom (id like "new_kingdom") resolves Neutral by kingdom
108:    // id but IS sided by its culture — without this, a Free/Evil-cultured player kingdom would read Neutral
109:    // and trade across the Free/Evil line. Mirrors WarOfTheRingMomentum's MomentumEnrollmentService.ResolveSide.
110:    private FactionSide ResolveSide(string kingdomId, string cultureId)
111:    {
112:        var side = _alignment.GetKingdomSide(kingdomId);
113:        if (side != FactionSide.Neutral)
114:            return side;
115:        return string.IsNullOrEmpty(cultureId) ? FactionSide.Neutral : _alignment.GetCultureSide(cultureId);
116:    }
117:
118:    public float ApplyBudgetFactorFloor(float vanillaBudgetFactor, bool isPlayerCaravan)
119:    {
120:        if (!IsActiveFor(isPlayerCaravan))
121:            return vanillaBudgetFactor;
122:
123:        // Engine-sourced float: defer to vanilla on garbage rather than emit a corrupted floor.
124:        if (!FiniteFloatValidator.IsFinite(vanillaBudgetFactor))
125:            return vanillaBudgetFactor;
126:
127:        return Math.Max(vanillaBudgetFactor, _settings.BudgetFactorFloor);
128:    }
129:
130:    public int ResolveInitialTradeGold(int vanillaValue, bool isPlayerCaravan)
131:    {
132:        if (!IsActiveFor(isPlayerCaravan))
133:            return vanillaValue;
134:        // Never lower — preserve vanilla's large-caravan / main-hero bonuses.
135:        return Math.Max(vanillaValue, _settings.InitialTradeGold);
136:    }
137:
138:    public int ResolveMaxGoldPerCategory(int vanillaValue, bool isPlayerCaravan)
139:    {
140:        if (!IsActiveFor(isPlayerCaravan))
141:            return vanillaValue;
142:        return _settings.MaxGoldPerCategory;
143:    }
144:
145:    private bool IsActiveFor(bool isPlayerCaravan)
146:    {
147:        if (!_settings.Enabled)
148:            return false;
149:        if (isPlayerCaravan && !_settings.ApplyToPlayerCaravans)
150:            return false;
151:        return true;
152:    }
153:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 461ms:
1:using Microsoft.VisualStudio.TestTools.UnitTesting;
2:using NSubstitute;
3:using TAOM.Core.Logging;
4:using TAOM.Features.CaravanTrade;
5:using TAOM.Features.Execution;
6:
7:namespace TAOM.Tests.Features.CaravanTrade;
8:
9:[TestClass]
10:public class CaravanTradeServiceTests
11:{
12:    private ICaravanTradeSettingsProvider _settings = null!;
13:    private IAlignmentService _alignment = null!;
14:    private IModLogger _logger = null!;
15:    private CaravanTradeService _sut = null!;
16:
17:    [TestInitialize]
18:    public void Setup()
19:    {
20:        _settings = Substitute.For<ICaravanTradeSettingsProvider>();
21:        _alignment = Substitute.For<IAlignmentService>();
22:        _logger = Substitute.For<IModLogger>();
23:
24:        // Default: feature on, applies to everyone, mid-range tuning.
25:        _settings.Enabled.Returns(true);
26:        _settings.ApplyToPlayerCaravans.Returns(true);
27:        _settings.RangeMultiplier.Returns(1.6f);
28:        _settings.DistanceDecayExponent.Returns(0.5f);
29:        _settings.NearFieldFlattenDays.Returns(2.0f);
30:        _settings.MaxCompensation.Returns(6.0f);
31:        _settings.AntiShuttlePenalty.Returns(0.5f);
32:        _settings.HomeDistanceReweight.Returns(true);
33:        _settings.WarTradePolicy.Returns(WarTradePolicy.SameAlignmentAndNeutral);
34:        _settings.BudgetFactorFloor.Returns(0.35f);
35:        _settings.InitialTradeGold.Returns(15000);
36:        _settings.MaxGoldPerCategory.Returns(1500);
37:
38:        _sut = new CaravanTradeService(_settings, _alignment, _logger);
39:    }
40:
41:    // ---------------- ReweightTradeScore ----------------
42:
43:    [TestMethod]
44:    public void ReweightTradeScore_Disabled_ReturnsRawScore()
45:    {
46:        _settings.Enabled.Returns(false);
47:        Assert.AreEqual(42f, _sut.ReweightTradeScore(42f, 3f, false, false, 1f, false), 0.0001f);
48:    }
49:
50:    [TestMethod]
51:    public void ReweightTradeScore_PlayerCaravanWhenPlayerScopeOff_ReturnsRawScore()
52:    {
53:        _settings.ApplyToPlayerCaravans.Returns(false);
54:        Assert.AreEqual(42f, _sut.ReweightTradeScore(42f, 3f, false, false, 1f, isPlayerCaravan: true), 0.0001f);
55:    }
56:
57:    [TestMethod]
58:    public void ReweightTradeScore_RejectionScore_PassesThroughUnchanged()
59:    {
60:        // Vanilla returns -1 for non-navigable / distance-cut rejects.
61:        Assert.AreEqual(-1f, _sut.ReweightTradeScore(-1f, 3f, false, false, 1f, false), 0.0001f);
62:        Assert.AreEqual(0f, _sut.ReweightTradeScore(0f, 3f, false, false, 1f, false), 0.0001f);
63:    }
64:
65:    [TestMethod]
66:    public void ReweightTradeScore_NaNRawScore_ReturnsRawScore()
67:    {
68:        // Positive-requirement gate: NaN must fail into the vanilla passthrough.
69:        Assert.IsTrue(float.IsNaN(_sut.ReweightTradeScore(float.NaN, 3f, false, false, 1f, false)));
70:    }
71:
72:    [TestMethod]
73:    public void ReweightTradeScore_NonPositiveDays_ReturnsRawScore()
74:    {
75:        // days<=0 skips the distance reweight; a neutral recency factor leaves rawScore unchanged.
76:        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 0f, false, false, 1f, false), 0.0001f);
77:    }
78:
79:    [TestMethod]
80:    public void ReweightTradeScore_NaNDays_ReturnsRawScore()
81:    {
82:        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, float.NaN, false, false, 1f, false), 0.0001f);
83:    }
84:
85:    [TestMethod]
86:    public void ReweightTradeScore_NavalCaravan_ReturnsRawScoreUnchanged()
87:    {
88:        // Naval uses a different vanilla distance factor; the shuttle is a land problem.
89:        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 3f, isNaval: true, false, 1f, false), 0.0001f);
90:    }
91:
92:    [TestMethod]
93:    public void ReweightTradeScore_HomeTown_NowCompressed()
94:    {
95:        // Home-rubber-band regression: with HomeDistanceReweight on (default), the home town is
96:        // distance-compressed identically to a non-home town at the same days — it no longer passes
97:        // through raw with its near-field proximity advantage.
98:        float home = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 1f, false);
99:        float nonHome = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: false, 1f, false);
100:        Assert.AreEqual(nonHome, home, 0.0001f);
101:        Assert.IsTrue(home < 10f, $"home should be compressed below raw, was {home}");
102:    }
103:
104:    [TestMethod]
105:    public void ReweightTradeScore_HomeTown_EscapeHatchOff_PassesDistanceButRecencyStillApplies()
106:    {
107:        // HomeDistanceReweight off restores the old home distance exemption: raw distance passes
108:        // through, but the recency penalty still applies to home.
109:        _settings.HomeDistanceReweight.Returns(false);
110:        Assert.AreEqual(10f, _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 1f, false), 0.0001f);
111:        Assert.AreEqual(5f, _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 0.5f, false), 0.0001f);
112:    }
113:
114:    [TestMethod]
115:    public void ReweightTradeScore_RecencyAppliedToHome()
116:    {
117:        // With the escape hatch on (default), home is compressed AND recency-penalized.
118:        float noPenalty = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 1f, false);
119:        float penalized = _sut.ReweightTradeScore(10f, 1f, false, isHomeTown: true, 0.5f, false);
120:        Assert.AreEqual(noPenalty * 0.5f, penalized, 0.0001f);
121:    }
122:
123:    [TestMethod]
124:    public void ReweightTradeScore_LandTown_AppliesStripAndReweight()
125:    {
126:        // m = days / (flatten+days)^alpha = 3 / (2+3)^0.5 = 3 / 2.23607 = 1.34164; result = 10 * 1.34164.
127:        float result = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
128:        Assert.AreEqual(13.4164f, result, 0.001f);
129:    }
130:
131:    [TestMethod]
132:    public void ReweightTradeScore_MoreProfitableFarTown_BeatsCloseTown_WhereVanillaWouldNot()
133:    {
134:        // Vanilla rawScore already embeds 1/days. Near town: days=1, base profit 100 -> rawScore 100.
135:        // Far town: days=5, base profit 200 -> rawScore 40. Vanilla picks near (100 > 40) despite the
136:        // far town being twice as profitable per trip; after the reweight the far town wins.
137:        float near = _sut.ReweightTradeScore(100f, 1f, false, false, 1f, false);
138:        float far = _sut.ReweightTradeScore(40f, 5f, false, false, 1f, false);
139:        Assert.IsTrue(far > near, $"expected far({far}) > near({near}) after reweight");
140:    }
141:
142:    [TestMethod]
143:    public void ReweightTradeScore_EqualBaseProfit_StillPrefersNear_ButCompressed()
144:    {
145:        // Equal base profit P0=100: near rawScore=100/1, far rawScore=100/5=20.
146:        float near = _sut.ReweightTradeScore(100f, 1f, false, false, 1f, false);
147:        float far = _sut.ReweightTradeScore(20f, 5f, false, false, 1f, false);
148:        Assert.IsTrue(near > far, "near should still edge out an equally-profitable far town");
149:        Assert.IsTrue(near / far < 2.0f, $"advantage should be compressed well below vanilla's 5x (was {near / far:F2})");
150:    }
151:
152:    [TestMethod]
153:    public void ReweightTradeScore_VeryFarTown_MultiplierClampedToMaxCompensation()
154:    {
155:        // m = 1000/(2+1000)^0.5 = 31.6, clamped to maxCompensation 6 -> result = 10 * 6.
156:        float result = _sut.ReweightTradeScore(10f, 1000f, false, false, 1f, false);
157:        Assert.AreEqual(60f, result, 0.01f);
158:    }
159:
160:    [TestMethod]
161:    public void ReweightTradeScore_RecencyFactor_MultipliesResult()
162:    {
163:        // A recency factor multiplies the reweighted result (the working anti-shuttle penalty).
164:        float full = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
165:        float cut = _sut.ReweightTradeScore(10f, 3f, false, false, 0.65f, false);
166:        Assert.AreEqual(full * 0.65f, cut, 0.001f);
167:    }
168:
169:    [TestMethod]
170:    public void ReweightTradeScore_NaNRecencyFactor_NoPenaltyApplied()
171:    {
172:        // Engine-Float gate: a NaN factor must be ignored (finite result == the un-penalized reweight).
173:        float full = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
174:        float withNaN = _sut.ReweightTradeScore(10f, 3f, false, false, float.NaN, false);
175:        Assert.AreEqual(full, withNaN, 0.0001f);
176:    }
177:
178:    [TestMethod]
179:    public void ReweightTradeScore_RecencyFactorOutOfRange_Ignored()
180:    {
181:        float full = _sut.ReweightTradeScore(10f, 3f, false, false, 1f, false);
182:        Assert.AreEqual(full, _sut.ReweightTradeScore(10f, 3f, false, false, 1.5f, false), 0.0001f);
183:        Assert.AreEqual(full, _sut.ReweightTradeScore(10f, 3f, false, false, -0.1f, false), 0.0001f);
184:    }
185:
186:    // ---------------- ScaleVeryFarDistance ----------------
187:
188:    [TestMethod]
189:    public void ScaleVeryFarDistance_Disabled_ReturnsVanilla()
190:    {
191:        _settings.Enabled.Returns(false);
192:        Assert.AreEqual(12f, _sut.ScaleVeryFarDistance(12f), 0.0001f);
193:    }
194:
195:    [TestMethod]
196:    public void ScaleVeryFarDistance_Enabled_ScalesByRangeMultiplier()
197:    {
198:        Assert.AreEqual(19.2f, _sut.ScaleVeryFarDistance(12f), 0.0001f); // 12 * 1.6
199:    }
200:
201:    // ---------------- AllowWartimeTrade ----------------
202:
203:    [TestMethod]
204:    public void AllowWartimeTrade_Disabled_ReturnsFalse()
205:    {
206:        _settings.Enabled.Returns(false);
207:        Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "gondor", "rohan", "rohan", false));
208:    }
209:
210:    [TestMethod]
211:    public void AllowWartimeTrade_PlayerCaravanWhenPlayerScopeOff_ReturnsFalse()
212:    {
213:        _settings.ApplyToPlayerCaravans.Returns(false);
214:        Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "gondor", "rohan", "rohan", isPlayerCaravan: true));
215:    }
216:
217:    [TestMethod]
218:    public void AllowWartimeTrade_PolicyNone_ReturnsFalse()
219:    {
220:        _settings.WarTradePolicy.Returns(WarTradePolicy.None);
221:        Assert.IsFalse(_sut.AllowWartimeTrade("gondor", "gondor", "rohan", "rohan", false));
222:    }
223:
224:    [TestMethod]
225:    public void AllowWartimeTrade_PolicyIgnoreWar_ReturnsTrue()
226:    {
227:        _settings.WarTradePolicy.Returns(WarTradePolicy.IgnoreWar);
228:        Assert.IsTrue(_sut.AllowWartimeTrade("gondor", "gondor", "mordor", "mordor", false));
229:    }
230:
231:    [TestMethod]
232:    public void AllowWartimeTrade_SameAlignment_SameSide_ReturnsTrue()
233:    {
234:        // Free caravan reaching another Free town despite the war.
235:        _alignment.GetKingdomSide("empire_w").Returns(FactionSide.Free);
236:        _alignment.GetKingdomSide("vlandia").Returns(FactionSide.Free);
237:        Assert.IsTrue(_sut.AllowWartimeTrade("empire_w", "gondor", "vlandia", "vlandia", false));
238:    }
239:
240:    [TestMethod]
241:    public void AllowWartimeTrade_SameAlignment_OppositeSides_ReturnsFalse()
242:    {
243:        // A Free caravan must NOT resupply an Evil town.
244:        _alignment.GetKingdomSide("empire_w").Returns(FactionSide.Free);
245:        _alignment.GetKingdomSide("empire_s").Returns(FactionSide.Evil);
246:        Assert.IsFalse(_sut.AllowWartimeTrade("empire_w", "gondor", "empire_s", "mordor", false));
247:    }
248:
249:    [TestMethod]
250:    public void AllowWartimeTrade_SameAlignment_NeutralCaravan_ReturnsTrue()
251:    {
252:        // Neutral (Umbar etc.) trades with anyone — regression guard for the AreEnemyAlignments
253:        // inversion (which treats Neutral as an enemy of everyone). This is the bug the deep-review
254:        // data-flow agent caught: the shipped default policy silently blocked neutral trade.
255:        _alignment.GetKingdomSide("umbar").Returns(FactionSide.Neutral);
256:        _alignment.GetCultureSide("umbar").Returns(FactionSide.Neutral);
257:        _alignment.GetKingdomSide("empire_w").Returns(FactionSide.Free);
258:        Assert.IsTrue(_sut.AllowWartimeTrade("umbar", "umbar", "empire_w", "gondor", false));
259:    }
260:
261:    [TestMethod]
262:    public void AllowWartimeTrade_SameAlignment_NeutralTarget_ReturnsTrue()
263:    {
264:        _alignment.GetKingdomSide("empire_s").Returns(FactionSide.Evil);
265:        _alignment.GetKingdomSide("umbar").Returns(FactionSide.Neutral);
266:        _alignment.GetCultureSide("umbar").Returns(FactionSide.Neutral);
267:        Assert.IsTrue(_sut.AllowWartimeTrade("empire_s", "mordor", "umbar", "umbar", false));
268:    }
269:
270:    [TestMethod]
271:    public void AllowWartimeTrade_PlayerFoundedKingdom_SidedByCulture_BlocksAcrossLine()
272:    {
273:        // A player-founded kingdom (id "new_kingdom") is absent from alignment.json -> GetKingdomSide
274:        // returns Neutral, which would wrongly let it trade across the Free/Evil line. Culture fallback
275:        // sides it Free (gondor culture), so it must NOT trade with an Evil town. (Codex MED, mirrors
276:        // WarOfTheRingMomentum's player-founded-kingdom culture fallback.)
277:        _alignment.GetKingdomSide("new_kingdom").Returns(FactionSide.Neutral);
278:        _alignment.GetCultureSide("gondor").Returns(FactionSide.Free);
279:        _alignment.GetKingdomSide("empire_s").Returns(FactionSide.Evil);
280:        Assert.IsFalse(_sut.AllowWartimeTrade("new_kingdom", "gondor", "empire_s", "mordor", false));
281:    }
282:
283:    [TestMethod]
284:    public void AllowWartimeTrade_PlayerFoundedKingdom_SidedByCulture_AllowsSameSide()
285:    {
286:        _alignment.GetKingdomSide("new_kingdom").Returns(FactionSide.Neutral);
287:        _alignment.GetCultureSide("gondor").Returns(FactionSide.Free);
288:        _alignment.GetKingdomSide("vlandia").Returns(FactionSide.Free);
289:        Assert.IsTrue(_sut.AllowWartimeTrade("new_kingdom", "gondor", "vlandia", "vlandia", false));
290:    }
291:
292:    // ---------------- ApplyBudgetFactorFloor ----------------
293:
294:    [TestMethod]
295:    public void ApplyBudgetFactorFloor_Disabled_ReturnsVanilla()
296:    {
297:        _settings.Enabled.Returns(false);
298:        Assert.AreEqual(0.1f, _sut.ApplyBudgetFactorFloor(0.1f, false), 0.0001f);
299:    }
300:
301:    [TestMethod]
302:    public void ApplyBudgetFactorFloor_PlayerScopeOff_ReturnsVanilla()
303:    {
304:        _settings.ApplyToPlayerCaravans.Returns(false);
305:        Assert.AreEqual(0.1f, _sut.ApplyBudgetFactorFloor(0.1f, isPlayerCaravan: true), 0.0001f);
306:    }
307:
308:    [TestMethod]
309:    public void ApplyBudgetFactorFloor_NaN_ReturnsVanilla()
310:    {
311:        Assert.IsTrue(float.IsNaN(_sut.ApplyBudgetFactorFloor(float.NaN, false)));
312:    }
313:
314:    [TestMethod]
315:    public void ApplyBudgetFactorFloor_BelowFloor_ReturnsFloor()
316:    {
317:        Assert.AreEqual(0.35f, _sut.ApplyBudgetFactorFloor(0.1f, false), 0.0001f);
318:    }
319:
320:    [TestMethod]
321:    public void ApplyBudgetFactorFloor_AboveFloor_ReturnsVanilla()
322:    {
323:        Assert.AreEqual(0.8f, _sut.ApplyBudgetFactorFloor(0.8f, false), 0.0001f);
324:    }
325:
326:    // ---------------- ResolveInitialTradeGold ----------------
327:
328:    [TestMethod]
329:    public void ResolveInitialTradeGold_Disabled_ReturnsVanilla()
330:    {
331:        _settings.Enabled.Returns(false);
332:        Assert.AreEqual(10000, _sut.ResolveInitialTradeGold(10000, false));
333:    }
334:
335:    [TestMethod]
336:    public void ResolveInitialTradeGold_PlayerScopeOff_ReturnsVanilla()
337:    {
338:        _settings.ApplyToPlayerCaravans.Returns(false);
339:        Assert.AreEqual(10000, _sut.ResolveInitialTradeGold(10000, isPlayerCaravan: true));
340:    }
341:
342:    [TestMethod]
343:    public void ResolveInitialTradeGold_VanillaBelowFloor_ReturnsFloor()
344:    {
345:        Assert.AreEqual(15000, _sut.ResolveInitialTradeGold(10000, false));
346:    }
347:
348:    [TestMethod]
349:    public void ResolveInitialTradeGold_VanillaAboveFloor_NeverLowers()
350:    {
351:        // Large caravan / main hero bonus must be preserved.
352:        Assert.AreEqual(22500, _sut.ResolveInitialTradeGold(22500, false));
353:    }
354:
355:    // ---------------- ResolveMaxGoldPerCategory ----------------
356:
357:    [TestMethod]
358:    public void ResolveMaxGoldPerCategory_Disabled_ReturnsVanilla()
359:    {
360:        _settings.Enabled.Returns(false);
361:        Assert.AreEqual(1500, _sut.ResolveMaxGoldPerCategory(1500, false));
362:    }
363:
364:    [TestMethod]
365:    public void ResolveMaxGoldPerCategory_Enabled_ReturnsConfiguredValue()
366:    {
367:        _settings.MaxGoldPerCategory.Returns(2500);
368:        Assert.AreEqual(2500, _sut.ResolveMaxGoldPerCategory(1500, false));
369:    }
370:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 460ms:
1:using System.IO;
2:using Microsoft.VisualStudio.TestTools.UnitTesting;
3:using NSubstitute;
4:using TAOM.Core.Infrastructure;
5:using TAOM.Core.Logging;
6:using TAOM.Features.CaravanTrade;
7:
8:namespace TAOM.Tests.Features.CaravanTrade;
9:
10:[TestClass]
11:public class CaravanTradeConfigProviderTests
12:{
13:    private string _tempDir = null!;
14:    private string _configDir = null!;
15:    private IPathService _pathService = null!;
16:    private IModLogger _logger = null!;
17:    private CaravanTradeConfigProvider _sut = null!;
18:
19:    [TestInitialize]
20:    public void Setup()
21:    {
22:        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CaravanTrade_" + Path.GetRandomFileName());
23:        _configDir = Path.Combine(_tempDir, "caravan_trade");
24:        Directory.CreateDirectory(_configDir);
25:
26:        _pathService = Substitute.For<IPathService>();
27:        _pathService.ModuleDataPath.Returns(_tempDir);
28:        _logger = Substitute.For<IModLogger>();
29:
30:        _sut = new CaravanTradeConfigProvider(_pathService, _logger);
31:    }
32:
33:    [TestCleanup]
34:    public void Cleanup()
35:    {
36:        if (Directory.Exists(_tempDir))
37:            Directory.Delete(_tempDir, true);
38:    }
39:
40:    private void WriteConfig(string json) =>
41:        File.WriteAllText(Path.Combine(_configDir, "caravan_trade_config.json"), json);
42:
43:    [TestMethod]
44:    public void GetConfig_ValidJson_ParsesAllFieldsAndLogsInfo()
45:    {
46:        WriteConfig(@"{
47:  ""enabled"": true,
48:  ""applyToPlayerCaravans"": false,
49:  ""rangeMultiplier"": 2.0,
50:  ""distanceDecayExponent"": 0.6,
51:  ""nearFieldFlattenDays"": 3.0,
52:  ""maxCompensation"": 8.0,
53:  ""antiShuttlePenalty"": 0.5,
54:  ""homeDistanceReweight"": false,
55:  ""warTradePolicy"": ""IgnoreWar"",
56:  ""budgetFactorFloor"": 0.4,
57:  ""initialTradeGold"": 20000,
58:  ""maxGoldPerCategory"": 2500
59:}");
60:
61:        var c = _sut.GetConfig();
62:
63:        Assert.IsTrue(c.Enabled);
64:        Assert.IsFalse(c.ApplyToPlayerCaravans);
65:        Assert.AreEqual(2.0f, c.RangeMultiplier, 0.0001f);
66:        Assert.AreEqual(0.6f, c.DistanceDecayExponent, 0.0001f);
67:        Assert.AreEqual(3.0f, c.NearFieldFlattenDays, 0.0001f);
68:        Assert.AreEqual(8.0f, c.MaxCompensation, 0.0001f);
69:        Assert.AreEqual(0.5f, c.AntiShuttlePenalty, 0.0001f);
70:        Assert.IsFalse(c.HomeDistanceReweight);
71:        Assert.AreEqual("IgnoreWar", c.WarTradePolicy);
72:        Assert.AreEqual(0.4f, c.BudgetFactorFloor, 0.0001f);
73:        Assert.AreEqual(20000, c.InitialTradeGold);
74:        Assert.AreEqual(2500, c.MaxGoldPerCategory);
75:        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
76:    }
77:
78:    [TestMethod]
79:    public void GetConfig_MissingFile_ReturnsDefaultsAndWarns()
80:    {
81:        var c = _sut.GetConfig();
82:
83:        Assert.AreEqual(1.6f, c.RangeMultiplier, 0.0001f);
84:        Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
85:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
86:    }
87:
88:    [TestMethod]
89:    public void GetConfig_MalformedJson_ReturnsDefaultsAndLogsError()
90:    {
91:        WriteConfig("not valid json {{{");
92:
93:        var c = _sut.GetConfig();
94:
95:        Assert.AreEqual(1.6f, c.RangeMultiplier, 0.0001f);
96:        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
97:    }
98:
99:    [TestMethod]
100:    public void GetConfig_PartialJson_MergesWithDefaults()
101:    {
102:        WriteConfig(@"{ ""rangeMultiplier"": 2.5 }");
103:
104:        var c = _sut.GetConfig();
105:
106:        Assert.AreEqual(2.5f, c.RangeMultiplier, 0.0001f);
107:        Assert.AreEqual(0.5f, c.DistanceDecayExponent, 0.0001f);
108:        Assert.AreEqual("SameAlignmentAndNeutral", c.WarTradePolicy);
109:    }
110:
111:    [TestMethod]
112:    public void GetConfig_CalledTwice_ReturnsSameCachedInstance()
113:    {
114:        WriteConfig(@"{ ""rangeMultiplier"": 2.0 }");
115:        Assert.AreSame(_sut.GetConfig(), _sut.GetConfig());
116:    }
117:
118:    [TestMethod]
119:    public void GetConfig_RangeMultiplierBelowOne_RevertsAndWarns()
120:    {
121:        // Below 1 shrinks the range below vanilla — worsening the very shuttle this feature fixes.
122:        WriteConfig(@"{ ""rangeMultiplier"": 0.5 }");
123:        Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
124:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
125:    }
126:
127:    [TestMethod]
128:    public void GetConfig_RangeMultiplierOversized_RevertsAndWarns()
129:    {
130:        WriteConfig(@"{ ""rangeMultiplier"": 12 }");
131:        Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
132:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
133:    }
134:
135:    [TestMethod]
136:    public void GetConfig_NaNRangeMultiplier_RevertsToFiniteDefault()
137:    {
138:        WriteConfig(@"{ ""rangeMultiplier"": NaN }");
139:        Assert.AreEqual(1.6f, _sut.GetConfig().RangeMultiplier, 0.0001f);
140:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("rangeMultiplier")));
141:    }
142:
143:    [TestMethod]
144:    public void GetConfig_DecayExponentZero_RevertsAndWarns()
145:    {
146:        WriteConfig(@"{ ""distanceDecayExponent"": 0 }");
147:        Assert.AreEqual(0.5f, _sut.GetConfig().DistanceDecayExponent, 0.0001f);
148:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("distanceDecayExponent")));
149:    }
150:
151:    [TestMethod]
152:    public void GetConfig_InfinityNearFieldFlatten_RevertsAndWarns()
153:    {
154:        WriteConfig(@"{ ""nearFieldFlattenDays"": Infinity }");
155:        Assert.AreEqual(2.0f, _sut.GetConfig().NearFieldFlattenDays, 0.0001f);
156:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("nearFieldFlattenDays")));
157:    }
158:
159:    [TestMethod]
160:    public void GetConfig_MaxCompensationBelowOne_RevertsAndWarns()
161:    {
162:        WriteConfig(@"{ ""maxCompensation"": 0.5 }");
163:        Assert.AreEqual(6.0f, _sut.GetConfig().MaxCompensation, 0.0001f);
164:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxCompensation")));
165:    }
166:
167:    [TestMethod]
168:    public void GetConfig_AntiShuttlePenaltyAboveOne_RevertsAndWarns()
169:    {
170:        // > 1 would flip the score sign.
171:        WriteConfig(@"{ ""antiShuttlePenalty"": 1.5 }");
172:        Assert.AreEqual(0.5f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
173:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
174:    }
175:
176:    [TestMethod]
177:    public void GetConfig_NegativeAntiShuttlePenalty_RevertsAndWarns()
178:    {
179:        // < 0 would reward returning to a just-visited town.
180:        WriteConfig(@"{ ""antiShuttlePenalty"": -0.2 }");
181:        Assert.AreEqual(0.5f, _sut.GetConfig().AntiShuttlePenalty, 0.0001f);
182:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("antiShuttlePenalty")));
183:    }
184:
185:    [TestMethod]
186:    public void GetConfig_MissingHomeDistanceReweight_DefaultsTrue()
187:    {
188:        WriteConfig(@"{ ""rangeMultiplier"": 1.6 }");
189:        Assert.IsTrue(_sut.GetConfig().HomeDistanceReweight);
190:    }
191:
192:    [TestMethod]
193:    public void GetConfig_HomeDistanceReweightFalse_Honored()
194:    {
195:        WriteConfig(@"{ ""homeDistanceReweight"": false }");
196:        Assert.IsFalse(_sut.GetConfig().HomeDistanceReweight);
197:        // A bool has no invalid-but-parseable state -> no warning.
198:        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("homeDistanceReweight")));
199:    }
200:
201:    [TestMethod]
202:    public void GetConfig_UnknownWarTradePolicy_RevertsAndWarns()
203:    {
204:        // The M1 typo trap: an unknown string must revert, not silently take the service switch default.
205:        WriteConfig(@"{ ""warTradePolicy"": ""IgnoreWarr"" }");
206:        Assert.AreEqual("SameAlignmentAndNeutral", _sut.GetConfig().WarTradePolicy);
207:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("warTradePolicy")));
208:    }
209:
210:    [TestMethod]
211:    public void GetConfig_KnownWarTradePolicyCaseInsensitive_Accepted()
212:    {
213:        WriteConfig(@"{ ""warTradePolicy"": ""ignorewar"" }");
214:        Assert.AreEqual("ignorewar", _sut.GetConfig().WarTradePolicy);
215:        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("warTradePolicy")));
216:    }
217:
218:    [TestMethod]
219:    public void GetConfig_BudgetFactorFloorAboveOne_RevertsAndWarns()
220:    {
221:        WriteConfig(@"{ ""budgetFactorFloor"": 1.5 }");
222:        Assert.AreEqual(0.35f, _sut.GetConfig().BudgetFactorFloor, 0.0001f);
223:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("budgetFactorFloor")));
224:    }
225:
226:    [TestMethod]
227:    public void GetConfig_InitialTradeGoldTooLow_RevertsAndWarns()
228:    {
229:        WriteConfig(@"{ ""initialTradeGold"": 500 }");
230:        Assert.AreEqual(15000, _sut.GetConfig().InitialTradeGold);
231:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("initialTradeGold")));
232:    }
233:
234:    [TestMethod]
235:    public void GetConfig_MaxGoldPerCategoryTooHigh_RevertsAndWarns()
236:    {
237:        WriteConfig(@"{ ""maxGoldPerCategory"": 999999 }");
238:        Assert.AreEqual(1500, _sut.GetConfig().MaxGoldPerCategory);
239:        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("maxGoldPerCategory")));
240:    }
241:
242:    [TestMethod]
243:    public void GetConfig_AllValid_LogsInfoNotWarning()
244:    {
245:        WriteConfig(@"{ ""rangeMultiplier"": 1.6, ""warTradePolicy"": ""None"" }");
246:
247:        _sut.GetConfig();
248:
249:        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("Loaded")));
250:        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
251:    }
252:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:12:33.184512Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
The hook is registered in `SubModule.cs` and the behavior is actually added after the CultureMarketplace block, so the runtime event listeners should exist. I’m checking vanilla `FindNextDestinationForCaravan`, `GetTradeScoreForTown`, `MobileParty.CurrentSettlement`, and clan finance now, since those determine whether the design is live or inert.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -Filter CaravansCampaignBehavior.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -Filter DefaultClanFinanceModel.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -Filter MobileParty.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem -Recurse -Filter CampaignEvents.cs" in E:\repos\TAOM
 succeeded in 618ms:

    Directory: E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.GameComponents

[32;1mMode   [0m[32;1m              LastWriteTime[0m[32;1m         Length[0m[32;1m Name[0m
[32;1m----   [0m [32;1m             -------------[0m [32;1m        ------[0m [32;1m----[0m
-a---            7/8/2026 10:50 AM          36547 DefaultClanFinanceModel.cs

[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 636ms:

    Directory: E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors

[32;1mMode   [0m[32;1m              LastWriteTime[0m[32;1m         Length[0m[32;1m Name[0m
[32;1m----   [0m [32;1m             -------------[0m [32;1m        ------[0m [32;1m----[0m
-a---            7/8/2026 10:50 AM          97932 CaravansCampaignBehavior.cs

[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 614ms:

    Directory: E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party

[32;1mMode   [0m[32;1m              LastWriteTime[0m[32;1m         Length[0m[32;1m Name[0m
[32;1m----   [0m [32;1m             -------------[0m [32;1m        ------[0m [32;1m----[0m
-a---            7/8/2026 10:50 AM         125868 MobileParty.cs

[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 614ms:

    Directory: E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem

[32;1mMode   [0m[32;1m              LastWriteTime[0m[32;1m         Length[0m[32;1m Name[0m
[32;1m----   [0m [32;1m             -------------[0m [32;1m        ------[0m [32;1m----[0m
-a---            7/8/2026 10:50 AM         123318 CampaignEvents.cs

[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:12:44.257758Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
The installed decompile files are present under `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem`. I’ll pull targeted line ranges with native tools and avoid relying on the prompt’s pre-verified facts.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"FindNextDestinationForCaravan\" /c:\"GetTradeScoreForTown\" /c:\"allTown.Owner.Settlement\" /c:\"HourlyTickParty\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"AddIncomeFromParty\" /c:\"PartyTradeGold\" /c:\"CurrentSettlement\" /c:\"HomeSettlement\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultClanFinanceModel.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"LastVisitedSettlement\" /c:\"CurrentSettlement\" /c:\"OnSettlementEntered\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"SettlementEntered\" /c:\"MobilePartyDestroyed\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignEvents.cs" in E:\repos\TAOM
 succeeded in 478ms:
656:					int num = AddIncomeFromParty(ownedCaravan.MobileParty, clan, ref goldChange, applyWithdrawals);
667:					int num2 = AddIncomeFromParty(ownedCaravan2.MobileParty, clan, ref goldChange, applyWithdrawals);
676:				int num3 = AddIncomeFromParty(warPartyComponent.MobileParty, clan, ref goldChange, applyWithdrawals);
690:	private int AddIncomeFromParty(MobileParty party, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
695:			int partyTradeGold = party.PartyTradeGold;
701:					party.PartyTradeGold -= num;
831:			num2 = ((party.LeaderHero != null && party.PartyTradeGold < 500) ? MathF.Min(num, 250) : 0);
834:		int partyTradeGold = party.PartyTradeGold;
843:				party.PartyTradeGold -= num3;
857:				party.PartyTradeGold += num4;
866:		return (int)((float)MathF.Max(0, caravan.PartyTradeGold - Campaign.Current.Models.CaravanModel.GetInitialTradeGold(caravan.Owner, caravan.CaravanPartyComponent.CanHaveNavalNavigationCapability, eliteCaravan: false)) / RevenueSmoothenFraction());
879:			if (ownedCaravan.MobileParty.PartyTradeGold > Campaign.Current.Models.CaravanModel.GetInitialTradeGold(ownedCaravan.Owner, ownedCaravan.CanHaveNavalNavigationCapability, eliteCaravan: false))
884:					ownedCaravan.MobileParty.PartyTradeGold -= num2;
895:		if (hero.CurrentSettlement == null)
899:		foreach (Alley alley in hero.CurrentSettlement.Alleys)
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 503ms:
285:		CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, HourlyTickParty);
617:	public void HourlyTickParty(MobileParty mobileParty)
903:		Town town = FindNextDestinationForCaravan(caravanParty, distanceCut: true, out bestNavigationType, out isFromPort, out isTargetingPort);
906:			town = FindNextDestinationForCaravan(caravanParty, distanceCut: false, out bestNavigationType, out isFromPort, out isTargetingPort);
911:	private Town FindNextDestinationForCaravan(MobileParty caravanParty, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isFromPort, out bool isTargetingPort)
923:			if (allTown.Owner.Settlement != caravanParty.CurrentSettlement && (!allTown.IsUnderSiege || (!allTown.Settlement.SiegeEvent.IsBlockadeActive && hasNavalNavigationCapability)) && CanTradeWith(caravanParty.MapFaction, allTown.MapFaction) && (allTown.Settlement.HasPort || !hasNavalNavigationCapability) && (!allTown.Settlement.Parties.Contains(MobileParty.MainParty) || !MobileParty.MainParty.MapFaction.IsAtWarWith(caravanParty.MapFaction)))
927:				float tradeScoreForTown = GetTradeScoreForTown(caravanParty, allTown, value, input, distanceCut, out bestNavigationType2, out isTargetingPort2);
965:	private float GetTradeScoreForTown(MobileParty caravanParty, Town town, CampaignTime lastHomeVisitTimeOfCaravan, float caravanFullness, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isTargetingPort)
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 515ms:
299:	public Settlement LastVisitedSettlement { get; private set; }
577:	public Settlement CurrentSettlement
602:				LastVisitedSettlement = value;
611:				attachedParty.CurrentSettlement = value;
1197:			if (IsActive && !IsTransitionInProgress && CurrentSettlement == null && MapEvent == null && BesiegedSettlement == null)
1307:		collectedObjects.Add(LastVisitedSettlement);
1322:	internal static object AutoGeneratedGetMemberValueLastVisitedSettlement(object o)
1324:		return ((MobileParty)o).LastVisitedSettlement;
1665:		if (CurrentSettlement == null)
1732:		CampaignVec2 position = ((CurrentSettlement == null) ? ((Army != null && AttachedTo != null) ? Army.LeaderParty.EndPositionForNavigationTransition : EndPositionForNavigationTransition) : ((!flag) ? CurrentSettlement.GatePosition : CurrentSettlement.PortPosition));
1796:			CurrentSettlement = _attachedTo.CurrentSettlement;
2064:		if (mobileParty.CurrentSettlement != null)
2068:				flag = mobileParty.MapEvent.MapEventSettlement == mobileParty.CurrentSettlement && (mobileParty.MapEvent.AttackerSide.LeaderParty.MapFaction == engagingParty.MapFaction || mobileParty.MapEvent.DefenderSide.LeaderParty.MapFaction == engagingParty.MapFaction);
2286:		if (MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.15.113119")) && (IsGarrison || IsMilitia) && CurrentSettlement == null && (MapEvent == null || (!MapEvent.IsSallyOut && !MapEvent.IsBlockadeSallyOut)))
2353:		if (IsGarrison && MapEvent == null && SiegeEvent == null && TargetParty != null && CurrentSettlement != null)
2357:		if (CurrentSettlement != null && !CurrentSettlement.Parties.Contains(this))
2359:			CurrentSettlement.AddMobileParty(this);
2364:					CurrentSettlement.AddMobileParty(attachedParty);
2427:			if (LeaderHero != null && CurrentSettlement != null && CurrentSettlement == LeaderHero.HomeSettlement)
2563:					textObject = ((CurrentSettlement != TargetSettlement) ? new TextObject("{=EQHq3bHM}Travelling to {TARGET_PARTY}") : new TextObject("{=Y65gdbrx}Waiting in {TARGET_PARTY}."));
2736:				if (CurrentSettlement != null)
3017:		CurrentSettlement = null;
3021:		if (CurrentSettlement != null)
3023:			list.Add(CurrentSettlement);
3504:		if (settlement == null && (IsGarrison || IsMilitia) && CurrentSettlement != null)
3506:			MapEvent mapEvent = CurrentSettlement.LastAttackerParty?.MapEvent;
3509:				settlement = CurrentSettlement;
3537:			bool flag = (mobileParty.Ai.AiBehaviorPartyBase == Party || (mobileParty.TargetSettlement != null && mobileParty.TargetSettlement == settlement && mobileParty.CurrentSettlement != settlement) || (mobileParty.ShortTermTargetSettlement != null && mobileParty.ShortTermTargetSettlement == settlement && mobileParty.CurrentSettlement != settlement)) && !mobileParty.IsInRaftState && mobileParty.MapEvent == null;
3848:			if (variables.HasMapEvent || CurrentSettlement != null)
3876:		if (BesiegedSettlement != null || CurrentSettlement != null || (!(variables.NextMoveDistance > 0f) && !variables.HasMapEvent))
3984:			SetTargetSettlement(mobileParty.CurrentSettlement, isTargetingPort: true);
4087:		if (Ai.IsDisabled || ShortTermBehavior == AiBehavior.Hold || CurrentSettlement == null || ((ShortTermTargetSettlement != null || TargetSettlement == CurrentSettlement) && ShortTermTargetSettlement == CurrentSettlement) || IsMainParty || (Army != null && AttachedTo != null && Army.LeaderParty != this))
4096:				InitializeNavigationTransitionParallel(CurrentSettlement.PortPosition, CurrentSettlement.PortPosition, ref gridChangeCount, ref gridChangeMobilePartyList);
4113:				return Ai.AiBehaviorPartyBase.MobileParty.CurrentSettlement != null;
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 515ms:
105:	private readonly MbEvent<MobileParty, Settlement, Hero> _afterSettlementEntered = new MbEvent<MobileParty, Settlement, Hero>();
107:	private readonly MbEvent<MobileParty, Settlement, Hero> _beforeSettlementEntered = new MbEvent<MobileParty, Settlement, Hero>();
661:	public static IMbEvent<MobileParty, Settlement, Hero> SettlementEntered => Instance._settlementEntered;
663:	public static IMbEvent<MobileParty, Settlement, Hero> AfterSettlementEntered => Instance._afterSettlementEntered;
665:	public static IMbEvent<MobileParty, Settlement, Hero> BeforeSettlementEnteredEvent => Instance._beforeSettlementEntered;
703:	public static IMbEvent<MobileParty, PartyBase> MobilePartyDestroyed => Instance._mobilePartyDestroyed;
1163:		_afterSettlementEntered.ClearListeners(obj);
1164:		_beforeSettlementEntered.ClearListeners(obj);
1598:	public override void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
1603:	public override void OnAfterSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
1605:		Instance._afterSettlementEntered.Invoke(party, settlement, hero);
1608:	public override void OnBeforeSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
1610:		Instance._beforeSettlementEntered.Invoke(party, settlement, hero);
1703:	public override void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty)
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:12:55.353758Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
The key vanilla search hits are where expected: `FindNextDestinationForCaravan` filters only the current settlement before scoring, and `GetTradeScoreForTown` has the single private signature being patched. I’m pulling concise ranges now so the final review can quote actual code rather than just search hits.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs -TotalCount 1045 | Select-Object -Last 150" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs -TotalCount 690 | Select-Object -Last 90" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultClanFinanceModel.cs -TotalCount 725 | Select-Object -Last 45" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs -TotalCount 625 | Select-Object -Last 65" in E:\repos\TAOM
 succeeded in 676ms:
			_caravanLastHomeTownVisitTime.Add(mobileParty, CampaignTime.Now);
		}
	}

	private Town ThinkNextDestination(MobileParty caravanParty, out MobileParty.NavigationType bestNavigationType, out bool isFromPort, out bool isTargetingPort)
	{
		RefreshTotalValueOfItemsAtCategoryForParty(caravanParty);
		Town town = FindNextDestinationForCaravan(caravanParty, distanceCut: true, out bestNavigationType, out isFromPort, out isTargetingPort);
		if (town == null)
		{
			town = FindNextDestinationForCaravan(caravanParty, distanceCut: false, out bestNavigationType, out isFromPort, out isTargetingPort);
		}
		return town;
	}

	private Town FindNextDestinationForCaravan(MobileParty caravanParty, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isFromPort, out bool isTargetingPort)
	{
		float num = 0f;
		Town result = null;
		bestNavigationType = MobileParty.NavigationType.None;
		isTargetingPort = false;
		float input = caravanParty.TotalWeightCarried / (float)caravanParty.InventoryCapacity;
		input = MBMath.Map(input, 0f, 1f, 0f, 0.9f);
		_caravanLastHomeTownVisitTime.TryGetValue(caravanParty, out var value);
		bool hasNavalNavigationCapability = caravanParty.HasNavalNavigationCapability;
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
					isTargetingPort = isTargetingPort2;
					bestNavigationType = bestNavigationType2;
				}
			}
		}
		isFromPort = isTargetingPort && caravanParty.CurrentSettlement != null;
		return result;
	}

	private void AdjustVeryFarAddition(bool isNavalCaravan, float distanceAsDays, float minimumAddition, ref float veryFarAddition)
	{
		float distanceLimitVeryFarAsDaysForNavigationType = GetDistanceLimitVeryFarAsDaysForNavigationType(isNavalCaravan);
		if (distanceAsDays > distanceLimitVeryFarAsDaysForNavigationType)
		{
			veryFarAddition += (distanceAsDays - distanceLimitVeryFarAsDaysForNavigationType) * minimumAddition * 4f;
		}
		float distanceLimitFarAsDaysForNavigationType = GetDistanceLimitFarAsDaysForNavigationType(isNavalCaravan);
		if (distanceAsDays > distanceLimitFarAsDaysForNavigationType)
		{
			veryFarAddition += (distanceAsDays - distanceLimitFarAsDaysForNavigationType) * minimumAddition * 3f;
		}
		float distanceLimitMediumAsDaysForNavigationType = GetDistanceLimitMediumAsDaysForNavigationType(isNavalCaravan);
		if (distanceAsDays > distanceLimitMediumAsDaysForNavigationType)
		{
			veryFarAddition += (distanceAsDays - distanceLimitMediumAsDaysForNavigationType) * minimumAddition * 2f;
		}
		float distanceLimitCloseAsDaysForNavigationType = GetDistanceLimitCloseAsDaysForNavigationType(isNavalCaravan);
		if (distanceAsDays > distanceLimitCloseAsDaysForNavigationType)
		{
			veryFarAddition += (distanceAsDays - distanceLimitCloseAsDaysForNavigationType) * minimumAddition;
		}
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
			float elapsedDaysUntilNow = lastHomeVisitTimeOfCaravan.ElapsedDaysUntilNow;
			bool flag2 = elapsedDaysUntilNow > GetDistanceLimitVeryFarAsDaysForNavigationType(flag);
			if (flag2)
			{
				float distanceAsDays = bestNavigationDistance / ((flag ? Campaign.Current.EstimatedAverageCaravanPartyNavalSpeed : Campaign.Current.EstimatedAverageCaravanPartySpeed) * (float)CampaignTime.HoursInDay);
				AdjustVeryFarAddition(flag, distanceAsDays, ((elapsedDaysUntilNow - 1f) * TaleWorlds.Library.MathF.Sqrt(elapsedDaysUntilNow - 1f) - 1f) * 0.008f, ref veryFarAddition);
			}
			ExplainedNumber result = default(ExplainedNumber);
			town.AddEffectOfBuildings(BuildingEffectEnum.CaravanAccessibility, ref result);
			float num2 = Math.Max(1f, result.ResultNumber);
			float distanceLimitVeryFarAsDaysForNavigationType = GetDistanceLimitVeryFarAsDaysForNavigationType(flag);
			float num3 = num + veryFarAddition;
			if (distanceCut && (town.Owner.Settlement != caravanParty.HomeSettlement || !flag2) && num3 > distanceLimitVeryFarAsDaysForNavigationType)
			{
				bestNavigationType = MobileParty.NavigationType.None;
				isTargetingPort = false;
				return -1f;
			}
			float num4 = (flag ? TaleWorlds.Library.MathF.Max(0.1f, 1f - num3 / (2f * distanceLimitVeryFarAsDaysForNavigationType)) : (1f / num3));
			float num5 = 1f;
			if (caravanParty.HomeSettlement == town.Owner.Settlement)
			{
				num5 = 1f + elapsedDaysUntilNow * 0.1f * (elapsedDaysUntilNow * 0.1f);
				if (num4 < 0.5f)
				{
					num4 = 0.5f;
				}
			}
			TownMarketData marketData = town.MarketData;
			float num6 = 1.1f;
			float num7 = 0f;
			for (int i = 0; i < caravanParty.Party.ItemRoster.Count; i++)
			{
				ItemObject item = caravanParty.ItemRoster.GetElementCopyAtIndex(i).EquipmentElement.Item;
				float limitValue = num6 - TaleWorlds.Library.MathF.Sqrt((float)TaleWorlds.Library.MathF.Min(_totalValueOfItemsAtCategory[item.ItemCategory], 5000) / 5000f) * 0.2f;
				num7 += CalculateTownSellScoreForCategory(caravanParty, marketData, i, limitValue);
			}
			num7 *= (flag ? 0.5f : 0.3f) + caravanFullness;
			float num8 = 0f;
			for (int j = 0; j < ItemCategories.All.Count; j++)
			{
				ItemCategory itemCategory = ItemCategories.All[j];
				if (itemCategory.IsTradeGood || itemCategory.IsAnimal)
				{
					num8 += CalculateTownBuyScoreForCategory(marketData, j, caravanParty);
				}
			}
			num8 *= TaleWorlds.Library.MathF.Max(0.1f, 1f - 2f * (caravanFullness - (flag ? 0.5f : 0.3f) * TaleWorlds.Library.MathF.Min(num7, 1000f) / 1000f));
			num8 = TaleWorlds.Library.MathF.Min(num8, (float)(int)(0.5f * (float)caravanParty.PartyTradeGold));
			float num9 = ((caravanParty.IsCurrentlyUsedByAQuest && town.Settlement == caravanParty.HomeSettlement && caravanParty.Position.Distance(caravanParty.HomeSettlement.Position) < Campaign.Current.Models.EncounterModel.NeededMaximumDistanceForEncounteringTown * 5f) ? 0.1f : 1f);
			float num10 = 1f;
			float num11 = ((town.Security >= 75f) ? (1f + TaleWorlds.Library.MathF.Clamp((town.Security - 75f) * 0.002f, 0f, 0.05f)) : 1f);
			float num12 = ((caravanParty.Owner != null) ? caravanParty.Owner.RandomFloat(1f, 1.03f) : 1f);
			float num13 = 1f;
			if (TradeAgreementsCampaignBehavior != null && caravanParty.MapFaction.IsKingdomFaction && town.MapFaction.IsKingdomFaction && TradeAgreementsCampaignBehavior.HasTradeAgreement((Kingdom)caravanParty.MapFaction, (Kingdom)town.MapFaction, out var _))
			{
				num13 = (flag ? 1.5f : 2f);
			}
			return (num7 + num8) * num4 * num13 * num5 * num9 * num10 * num11 * num12 * num2;
		}
		bestNavigationType = MobileParty.NavigationType.None;
		isTargetingPort = false;
		return -1f;
	}

	private float CalculateTownSellScoreForCategory(MobileParty party, TownMarketData marketData, int i, float limitValue)
	{
		ItemRosterElement itemRosterElement = party.Party.ItemRoster[i];
		ItemCategory itemCategory = itemRosterElement.EquipmentElement.Item.ItemCategory;
		GetCategoryPriceData(itemCategory, party, out var priceIndex);
		float num = marketData.GetPriceFactor(itemCategory) - priceIndex.AverageBuySellPriceIndex * limitValue;
		if (num > 0f)
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 683ms:
		{
			goldChange.Add(explainedNumber.ResultNumber, _caravanAndPartyIncome);
		}
		else
		{
			goldChange.AddFromExplainedNumber(explainedNumber, _caravanAndPartyIncome);
		}
	}

	private int AddIncomeFromParty(MobileParty party, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
	{
		int num = 0;
		if (party.IsActive && party.LeaderHero != clan.Leader && (party.IsLordParty || party.IsGarrison || party.IsCaravan))
		{
			int partyTradeGold = party.PartyTradeGold;
			if (partyTradeGold > 10000)
			{
				num = (partyTradeGold - 10000) / 10;
				if (applyWithdrawals)
				{
					party.PartyTradeGold -= num;
					if (party.LeaderHero != null && num > 0)
					{
						SkillLevelingManager.OnTradeProfitMade(party.LeaderHero, num);
					}
					if (party.Party.Owner?.Clan?.Leader != null && party.IsCaravan && party.Party.Owner.Clan.Leader.GetPerkValue(DefaultPerks.Trade.GreatInvestor) && num > 0)
					{
						party.Party.Owner.Clan.AddRenown(DefaultPerks.Trade.GreatInvestor.PrimaryBonus);
					}
					if (clan == Clan.PlayerClan && party.IsCaravan)
					{
						CampaignEventDispatcher.Instance.OnPlayerEarnedGoldFromAsset(AssetIncomeType.Caravan, num);
					}
				}
			}
		}
		return num;
	}

	private void AddExpensesFromPartiesAndGarrisons(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, bool includeDetails)
	{
		ExplainedNumber explainedNumber = new ExplainedNumber(0f, goldChange.IncludeDescriptions);
		int num = AddExpenseFromLeaderParty(clan, goldChange, applyWithdrawals);
		explainedNumber.Add(num, _mainPartywageText);
		foreach (Hero aliveLord in clan.AliveLords)
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 687ms:
			if (CampaignTime.Now - lootedCaravan.Value >= CampaignTime.Days(10f))
			{
				list.Add(lootedCaravan.Key);
			}
		}
		foreach (MobileParty item in list)
		{
			_lootedCaravans.Remove(item);
		}
	}

	private Town GetDestinationForMobileParty(MobileParty party)
	{
		return party.TargetSettlement?.Town;
	}

	public void HourlyTickParty(MobileParty mobileParty)
	{
		if (!Campaign.Current.GameStarted || !mobileParty.IsCaravan)
		{
			return;
		}
		bool flag = false;
		float randomFloat = MBRandom.RandomFloat;
		if (mobileParty.MapEvent != null || mobileParty.IsInRaftState || !mobileParty.IsPartyTradeActive || mobileParty.Ai.DoNotMakeNewDecisions || mobileParty.DefaultBehavior == AiBehavior.MoveToNearestLandOrPort)
		{
			return;
		}
		if (mobileParty.CurrentSettlement != null && mobileParty.CurrentSettlement.IsFortification)
		{
			if ((!mobileParty.CurrentSettlement.IsUnderSiege || (!mobileParty.CurrentSettlement.SiegeEvent.IsBlockadeActive && mobileParty.HasNavalNavigationCapability)) && mobileParty.ShortTermBehavior != AiBehavior.FleeToPoint && !mobileParty.Ai.IsAlerted && (mobileParty.IsCurrentlyUsedByAQuest || randomFloat < 1f / 3f))
			{
				float num = ((mobileParty.MemberRoster.TotalManCount > 0) ? ((float)mobileParty.MemberRoster.TotalWounded / (float)mobileParty.MemberRoster.TotalManCount) : 1f);
				float num2 = 1f;
				if ((double)num > 0.4)
				{
					num2 = 0f;
				}
				else if ((double)num > 0.2)
				{
					num2 = 0.1f;
				}
				else if ((double)num > 0.1)
				{
					num2 = 0.2f;
				}
				else if ((double)num > 0.05)
				{
					num2 = 0.3f;
				}
				else if ((double)num > 0.025)
				{
					num2 = 0.4f;
				}
				float randomFloat2 = MBRandom.RandomFloat;
				if (num2 > randomFloat2)
				{
					flag = true;
				}
			}
		}
		else
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
			MobileParty.NavigationType bestNavigationType;
			bool isFromPort;
			bool isTargetingPort;
			Town town2 = ThinkNextDestination(mobileParty, out bestNavigationType, out isFromPort, out isTargetingPort);
			if (town2 != null)
			{
				SetPartyAiAction.GetActionForVisitingSettlement(mobileParty, town2.Settlement, bestNavigationType, isFromPort, isTargetingPort);
			}
		}
	}

	public void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
	{
		Town town = settlement.Town;
		if (Campaign.Current.GameStarted && mobileParty != null && town != null && mobileParty.IsCaravan && mobileParty.IsPartyTradeActive && mobileParty.IsActive)
		{
			if (mobileParty.DefaultBehavior == AiBehavior.MoveToNearestLandOrPort)
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 641ms:

	[SaveableProperty(1097)]
	public CampaignTime NavigationTransitionDuration { get; private set; } = CampaignTime.Zero;

	public NavigationType DesiredAiNavigationType
	{
		get
		{
			return _desiredAiNavigationType;
		}
		set
		{
			_desiredAiNavigationType = value;
		}
	}

	public Settlement CurrentSettlement
	{
		get
		{
			return _currentSettlement;
		}
		set
		{
			if (value == _currentSettlement)
			{
				return;
			}
			if (_currentSettlement != null)
			{
				_currentSettlement.RemoveMobileParty(this);
				if (!_currentSettlement.IsVillage)
				{
					ArmyPositionAdder = Vec2.Zero;
				}
			}
			_currentSettlement = value;
			if (_currentSettlement != null)
			{
				_currentSettlement.AddMobileParty(this);
				Position = (IsCurrentlyAtSea ? _currentSettlement.PortPosition : _currentSettlement.GatePosition);
				LastVisitedSettlement = value;
				EndPositionForNavigationTransition = Position;
			}
			else
			{
				EndPositionForNavigationTransition = CampaignVec2.Invalid;
			}
			foreach (MobileParty attachedParty in _attachedParties)
			{
				attachedParty.CurrentSettlement = value;
			}
			if (_currentSettlement != null && _currentSettlement.IsFortification)
			{
				ArmyPositionAdder = Vec2.Zero;
				Bearing = Vec2.Zero;
				foreach (MobileParty party in _currentSettlement.Parties)
				{
					party.Party.SetVisualAsDirty();
				}
			}
			Party.SetVisualAsDirty();
		}
	}

[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:13:11.604722Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
Vanilla confirms the parked timing: `HourlyTickParty` calls `ThinkNextDestination` while `CurrentSettlement` is non-null, and `FindNextDestinationForCaravan` excludes only that current settlement. In the A→B→C trace, C is excluded but B remains a candidate and the memory ring still contains B, so the new penalty is not inert.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"float num5\" /c:\"caravanParty.HomeSettlement == town.Owner.Settlement\" /c:\"num5 =\" /c:\"return (num7 + num8)\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.CampaignBehaviors\\CaravansCampaignBehavior.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"public Settlement CurrentSettlement\" /c:\"LastVisitedSettlement = value\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"private int AddIncomeFromParty\" /c:\"party.IsActive\" /c:\"partyTradeGold > 10000\" /c:\"num = (partyTradeGold - 10000) / 10\" /c:\"CurrentSettlement\" /c:\"HomeSettlement\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.GameComponents\\DefaultClanFinanceModel.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"public static IMbEvent<MobileParty, Settlement, Hero> SettlementEntered\" /c:\"public static IMbEvent<MobileParty, PartyBase> MobilePartyDestroyed\" /c:\"OnSettlementEntered\" /c:\"OnMobilePartyDestroyed\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignEvents.cs" in E:\repos\TAOM
 succeeded in 424ms:
473:				int num5 = 10000;
482:							num5 = itemValue2;
767:			for (int num5 = 0; num5 < randomCaravanTemplate.Stacks.Count; num5++)
993:			float num5 = 1f;
994:			if (caravanParty.HomeSettlement == town.Owner.Settlement)
996:				num5 = 1f + elapsedDaysUntilNow * 0.1f * (elapsedDaysUntilNow * 0.1f);
1032:			return (num7 + num8) * num4 * num13 * num5 * num9 * num10 * num11 * num12 * num2;
1145:					float num5 = priceIndexSellLimit - (Campaign.Current.GameStarted ? (TaleWorlds.Library.MathF.Sqrt((float)TaleWorlds.Library.MathF.Min(_totalValueOfItemsAtCategory[item.ItemCategory], 5000) / 5000f) * 0.4f) : 0f);
1296:			num5 = 0;
1348:			num5 = num7 * itemPrice;
1404:		float num5 = num2 * 200f;
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 425ms:
577:	public Settlement CurrentSettlement
602:				LastVisitedSettlement = value;
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 398ms:
690:	private int AddIncomeFromParty(MobileParty party, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
693:		if (party.IsActive && party.LeaderHero != clan.Leader && (party.IsLordParty || party.IsGarrison || party.IsCaravan))
696:			if (partyTradeGold > 10000)
698:				num = (partyTradeGold - 10000) / 10;
895:		if (hero.CurrentSettlement == null)
899:		foreach (Alley alley in hero.CurrentSettlement.Alleys)
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 408ms:
661:	public static IMbEvent<MobileParty, Settlement, Hero> SettlementEntered => Instance._settlementEntered;
703:	public static IMbEvent<MobileParty, PartyBase> MobilePartyDestroyed => Instance._mobilePartyDestroyed;
1598:	public override void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
1703:	public override void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty)
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:13:25.445171Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
I also verified finance: `DefaultClanFinanceModel.AddIncomeFromParty` pays based on `PartyTradeGold > 10000` and `IsCaravan`, with no home/current-settlement gate in the payout method. I’m checking the installed-source helper next for type provenance/signature confidence, then I’ll finish the suspect matrix.
2026-07-11T22:13:36.484163Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior' in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.Party.MobileParty' in E:\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior'` rejected: blocked by policy
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.Party.MobileParty'` rejected: blocked by policy
2026-07-11T22:13:36.484431Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.Party.MobileParty'` rejected: blocked by policy
2026-07-11T22:13:36.535261Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignEvents'` rejected: blocked by policy
2026-07-11T22:13:36.535509Z ERROR codex_core::tools::router: error=`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel'` rejected: blocked by policy
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel' in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignEvents' in E:\repos\TAOM
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignEvents'` rejected: blocked by policy
 declined in 0ms:
`"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel'` rejected: blocked by policy
codex
The `tools/taom-src.ps1` helper itself is blocked by this shell policy, so I cannot use that wrapper. I am still verifying against the installed decompiled files under `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\...`, and I’ll call that out rather than pretending the helper ran.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /c:\"static class IoC\" /c:\"public static T Resolve\" /c:\"IContainer\" Main\\*.cs Main\\**\\*.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /c:\"RegisterCaravanTradeFeature\" Main\\*.cs Main\\**\\*.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"RegisterCaravanTradeFeature\" Main\\SubModule.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"CaravansCampaignBehavior_GetTradeScoreForTown_Patch\" /c:\"Patch59_CaravanTrade\" Main\\Features\\CaravanTrade\\Hooks\\*.cs Main\\SubModule.cs" in E:\repos\TAOM
 succeeded in 461ms:
Main\Features\AdvancedCombat\AdvancedCombatIoC.cs:8:    public static void RegisterAdvancedCombatFeature(IContainer container)
Main\Features\AlignmentDesertion\AlignmentDesertionIoC.cs:7:    public static void RegisterAlignmentDesertionFeature(IContainer container)
Main\Features\AlignmentRecruitment\RecruitmentAlignmentIoC.cs:7:    public static void RegisterAlignmentRecruitmentFeature(IContainer container)
Main\Features\Arena\ArenaIoC.cs:7:    public static void RegisterArenaFeature(IContainer container)
Main\Features\ArmyTargeting\ArmyTargetingIoC.cs:8:    public static void RegisterArmyTargetingFeature(IContainer container)
Main\Features\BanditManagement\BanditManagementIoC.cs:7:    public static void RegisterBanditManagementFeature(IContainer container)
Main\Features\BannerColorPersistence\BannerColorPersistenceIoC.cs:8:    public static void RegisterBannerColorPersistenceFeature(IContainer container)
Main\Features\BannerInjection\BannerInjectionIoC.cs:9:    public static void RegisterBannerInjectionFeature(IContainer container)
Main\Features\BattleBalance\BattleBalanceIoC.cs:7:    public static void RegisterBattleBalanceFeature(IContainer container)
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:8:    public static void RegisterBattleLoadDiagnosticsFeature(IContainer container)
Main\Features\CaravanTrade\CaravanTradeIoC.cs:7:    public static void RegisterCaravanTradeFeature(IContainer container)
Main\Features\CareerSystem\CareerSystemIoC.cs:13:    public static void RegisterCareerSystemFeature(IContainer container)
Main\Features\CastleRecruitment\CastleRecruitmentIoC.cs:7:    public static void RegisterCastleRecruitmentFeature(IContainer container)
Main\Features\CharacterCreation\CharacterCreationIoC.cs:8:    public static void RegisterCharacterCreationFeature(IContainer container)
Main\Features\CombatMechanics\CombatMechanicsIoC.cs:7:    public static void RegisterCombatMechanicsFeature(IContainer container)
Main\Features\CompanionTactics\CompanionTacticsIoC.cs:17:    public static void RegisterCompanionTacticsFeature(IContainer container)
Main\Features\CrashReport\CrashReportIoC.cs:10:    public static void RegisterCrashReportFeature(IContainer container)
Main\Features\CulturalFeats\CulturalFeatsIoC.cs:11:    public static void RegisterCulturalFeatsFeature(IContainer container)
Main\Features\CultureConversion\CultureConversionIoC.cs:8:    public static void RegisterCultureConversionFeature(IContainer container)
Main\Features\CultureMarketplace\CultureMarketplaceIoC.cs:9:    public static void RegisterCultureMarketplaceFeature(IContainer container)
Main\Features\CustomBattles\CustomBattlesIoC.cs:9:    public static void RegisterCustomBattlesFeature(IContainer container)
Main\Features\Diplomacy\DiplomacyIoC.cs:9:    public static void RegisterDiplomacyFeature(IContainer container)
Main\Features\EditorCacheRebuild\EditorCacheRebuildIoC.cs:13:    public static void RegisterEditorCacheRebuildFeature(IContainer container)
Main\Features\Elephant\ElephantIoC.cs:7:    public static void RegisterElephantFeature(IContainer container)
Main\Features\EliteEmissary\EliteEmissaryIoC.cs:8:    public static void RegisterEliteEmissaryFeature(IContainer container)
Main\Features\Encyclopedia\EncyclopediaIoC.cs:7:    public static void RegisterEncyclopediaFeature(IContainer container)
Main\Features\EquipPresets\EquipPresetsIoC.cs:9:    public static void RegisterEquipPresetsFeature(IContainer container)
Main\Features\Execution\ExecutionIoC.cs:8:    public static void RegisterExecutionFeature(IContainer container)
Main\Features\FactionMap\FactionMapIoC.cs:9:    public static void RegisterFactionMapFeature(IContainer container)
Main\Features\FiefManagement\FiefManagementIoC.cs:8:    public static void RegisterFiefManagementFeature(IContainer container)
Main\Features\HeroRace\HeroRaceIoC.cs:8:    public static void RegisterHeroRaceFeature(IContainer container)
Main\Features\InitialChildGeneration\InitialChildGenerationIoC.cs:8:    public static void RegisterInitialChildGenerationFeature(IContainer container)
Main\Features\LotrIssues\LotrIssuesIoC.cs:12:    public static void RegisterLotrIssuesFeature(IContainer container)
Main\Features\MainMenuCustomizer\MainMenuCustomizerIoC.cs:7:    public static void RegisterMainMenuCustomizerFeature(IContainer container)
Main\Features\Messengers\MessengerIoC.cs:7:    public static void RegisterMessengerFeature(IContainer container)
Main\Features\MissionDiagnostic\MissionDiagnosticIoC.cs:7:    public static void RegisterMissionDiagnosticFeature(IContainer container)
Main\Features\MixedFormations\MixedFormationsIoC.cs:7:    public static void RegisterMixedFormationsFeature(IContainer container)
Main\Features\Mumakil\MumakilIoC.cs:7:    public static void RegisterMumakilFeature(IContainer container)
Main\Features\NamedCompanions\NamedCompanionIoC.cs:8:    public static void RegisterNamedCompanionsFeature(IContainer container)
Main\Features\NavalTravel\NavalTravelIoC.cs:7:    public static void RegisterNavalTravelFeature(IContainer container)
Main\Features\NazgulFamily\NazgulFamilyIoC.cs:7:    public static void RegisterNazgulFamilyFeature(IContainer container)
Main\Features\QuickActions\QuickActionsIoC.cs:10:    public static void RegisterQuickActionsFeature(IContainer container)
Main\Features\RaceAge\RaceAgeIoC.cs:8:    public static void RegisterRaceAgeFeature(IContainer container)
Main\Features\RevoltTuning\RevoltTuningIoC.cs:7:    public static void RegisterRevoltTuningFeature(IContainer container)
Main\Features\SaveLoadDiagnostics\SaveLoadDiagnosticsIoC.cs:7:    public static void RegisterSaveLoadDiagnosticsFeature(IContainer container)
Main\Features\SettlementEconomy\SettlementEconomyIoC.cs:7:    public static void RegisterSettlementEconomyFeature(IContainer container)
Main\Features\SettlementFood\SettlementFoodIoC.cs:7:    public static void RegisterSettlementFoodFeature(IContainer container)
Main\Features\SettlementGuards\SettlementGuardsIoC.cs:7:    public static void RegisterSettlementGuardsFeature(IContainer container)
Main\Features\SettlementNameplateFade\NameplateFadeIoC.cs:7:    public static void RegisterSettlementNameplateFadeFeature(IContainer container)
Main\Features\ShaderPrecompilation\ShaderPrecompilationIoC.cs:9:    public static void RegisterShaderPrecompilationFeature(IContainer container)
Main\Features\Siege\SiegeDefenseIoC.cs:8:    public static void RegisterSiegeDefenseFeature(IContainer container)
Main\Features\SiegeDismount\SiegeDismountIoC.cs:8:    public static void RegisterSiegeDismountFeature(IContainer container)
Main\Features\SmartCavalryAI\SmartCavalryAIIoC.cs:8:    public static void RegisterSmartCavalryAIFeature(IContainer container)
Main\Features\SpecialResources\SpecialResourcesIoC.cs:8:    public static void RegisterSpecialResourcesFeature(IContainer container)
Main\Features\Spider\SpiderIoC.cs:7:    public static void RegisterSpiderFeature(IContainer container)
Main\Features\StartupResources\StartupResourcesIoC.cs:8:    public static void RegisterStartupResourcesFeature(IContainer container)
Main\Features\TimeAcceleration\TimeAccelerationIoC.cs:7:    public static void RegisterTimeAccelerationFeature(IContainer container)
Main\Features\TroopProgression\TroopProgressionIoC.cs:8:    public static void RegisterTroopProgressionFeature(IContainer container)
Main\Features\TroopWeight\TroopWeightIoC.cs:10:    public static void RegisterTroopWeightFeature(IContainer container)
Main\Features\Warg\WargIoC.cs:7:    public static void RegisterWargFeature(IContainer container)
Main\Features\WarOfTheRingMomentum\WarOfTheRingMomentumIoC.cs:10:    public static void RegisterWarOfTheRingMomentumFeature(IContainer container)
Main\IoC.cs:64:public static class IoC
Main\IoC.cs:66:    private static IContainer _container;
Main\IoC.cs:72:        container.RegisterInstance<IContainer>(container);
Main\IoC.cs:145:    private static void RegisterCoreServices(IContainer container)
Main\IoC.cs:160:    private static void RegisterLoggingServices(IContainer container)
Main\IoC.cs:165:    public static T Resolve<T>()
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:13:48.948652Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 exited 1 in 431ms:
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 388ms:
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CalculateBudgetFactor_Patch.cs:18:[HarmonyPatchCategory("Patch59_CaravanTrade")]
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_CanTradeWith_Patch.cs:23:[HarmonyPatchCategory("Patch59_CaravanTrade")]
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetDistanceLimitVeryFar_Patch.cs:27:[HarmonyPatchCategory("Patch59_CaravanTrade")]
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:21:[HarmonyPatchCategory("Patch59_CaravanTrade")]
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:22:public static class CaravansCampaignBehavior_GetTradeScoreForTown_Patch
Main\SubModule.cs:290:        _harmony.PatchCategory("Patch59_CaravanTrade");
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 461ms:
Main\Features\CaravanTrade\CaravanTradeIoC.cs:7:    public static void RegisterCaravanTradeFeature(IContainer container)
Main\IoC.cs:116:        CaravanTradeIoC.RegisterCaravanTradeFeature(container);
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\IoC.cs -TotalCount 180 | Select-Object -Last 125" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\SubModule.cs -TotalCount 735 | Select-Object -Last 35" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\SubModule.cs -TotalCount 300 | Select-Object -Last 25" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"Initialize\" /c:\"_container\" /c:\"RegisterCaravanTradeFeature\" /c:\"Resolve<T>\" Main\\IoC.cs" in E:\repos\TAOM
 succeeded in 469ms:
66:    private static IContainer _container;
116:        CaravanTradeIoC.RegisterCaravanTradeFeature(container);
139:        _container = container;
142:        CareerSystemIoC.InitializeCalculators(container.Resolve<Features.CareerSystem.Mutations.IMutationCalculatorRegistry>());
165:    public static T Resolve<T>()
167:        return _container.Resolve<T>();
172:        return _container.ResolveMany<T>();
177:        _container?.Dispose();
178:        _container = null;
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 642ms:
            IoC.Resolve<IFiefHubMenuPresenter>(),
            IoC.Resolve<IFiefManagementSettingsProvider>()));

        // CompanionTactics (Patch35) - FormationPresets persistence behavior. Registered
        // unconditionally so SyncData round-trips even when EnableFormationPresets is OFF.
        campaignStarter.AddBehavior(new Features.CompanionTactics.FormationPresets.Hooks.FormationPresetCampaignBehavior(
            IoC.Resolve<Features.CompanionTactics.FormationPresets.IFormationPresetService>(),
            IoC.Resolve<IModLogger>()));

        // Messengers - paid messenger dispatch + dialog hooks + per-save SyncData persistence.
        // Registered unconditionally so saves round-trip pending messengers even when
        // EnableMessengers is OFF (disabled = inert, not absent).
        campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.Messengers.MessengerCampaignBehavior>());

        // CultureMarketplace (#207) - daily injection of LOTRLOME items into town markets
        // keyed by owner culture. No SyncData (stock lives in vanilla Settlement.ItemRoster).
        campaignStarter.AddBehavior(new Features.CultureMarketplace.CultureMarketplaceBehavior(
            IoC.Resolve<Features.CultureMarketplace.ICultureItemPoolService>(),
            IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceInjectionService>(),
            IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceMaintenanceService>(),
            IoC.Resolve<ITownRosterAdapter>(),
            IoC.Resolve<Features.CultureMarketplace.Domain.MarketplaceTuning>(),
            IoC.Resolve<IModLogger>()));

        // CaravanTrade - per-caravan visit memory feeding the GetTradeScoreForTown recency penalty
        // (fixes caravans shuttling between the nearest two towns). Registered unconditionally so a
        // mid-session master-toggle-on works immediately; no SyncData (ephemeral, rebuilds as caravans move).
        campaignStarter.AddBehavior(IoC.Resolve<Features.CaravanTrade.CaravanVisitMemoryBehavior>());

        // CastleRecruitment (Patch42) - castle notable population + maintenance + volunteer fill +
        // player "Recruit troops" castle menu + issue/quest suppression for castle notables.
        // Registered unconditionally so the MCM master toggle takes effect at runtime.
        campaignStarter.AddBehavior(new CastleRecruitmentBehavior(
            IoC.Resolve<ICastleRecruitmentService>(),
            IoC.Resolve<IModLogger>()));
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 660ms:
using TAOM.Features.CultureConversion;
using TAOM.Features.MissionDiagnostic;
using TAOM.Features.CrashReport;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.SettlementNameplateFade;

namespace TAOM;

public static class IoC
{
    private static IContainer _container;

    public static void Configure()
    {
        var container = new Container();

        container.RegisterInstance<IContainer>(container);

        RegisterCoreServices(container);
        RegisterLoggingServices(container);

        HeroRaceIoC.RegisterHeroRaceFeature(container);
        BannerInjectionIoC.RegisterBannerInjectionFeature(container);
        BannerColorPersistenceIoC.RegisterBannerColorPersistenceFeature(container);
        TroopProgressionIoC.RegisterTroopProgressionFeature(container);
        Features.Arena.ArenaIoC.RegisterArenaFeature(container);
        FactionMapIoC.RegisterFactionMapFeature(container);
        CharacterCreationIoC.RegisterCharacterCreationFeature(container);
        InitialChildGenerationIoC.RegisterInitialChildGenerationFeature(container);
        DiplomacyIoC.RegisterDiplomacyFeature(container);
        Features.WarOfTheRingMomentum.WarOfTheRingMomentumIoC.RegisterWarOfTheRingMomentumFeature(container);
        RaceAgeIoC.RegisterRaceAgeFeature(container);
        ExecutionIoC.RegisterExecutionFeature(container);
        RecruitmentAlignmentIoC.RegisterAlignmentRecruitmentFeature(container);
        AlignmentDesertionIoC.RegisterAlignmentDesertionFeature(container);
        NazgulFamilyIoC.RegisterNazgulFamilyFeature(container);
        NavalTravelIoC.RegisterNavalTravelFeature(container);
        EncyclopediaIoC.RegisterEncyclopediaFeature(container);
        StartupResourcesIoC.RegisterStartupResourcesFeature(container);
        TroopWeightIoC.RegisterTroopWeightFeature(container);
        AdvancedCombatIoC.RegisterAdvancedCombatFeature(container);
        WargIoC.RegisterWargFeature(container);
        SpiderIoC.RegisterSpiderFeature(container);
        Features.Elephant.ElephantIoC.RegisterElephantFeature(container);
        Features.Mumakil.MumakilIoC.RegisterMumakilFeature(container);
        CustomBattlesIoC.RegisterCustomBattlesFeature(container);
        BattleBalanceIoC.RegisterBattleBalanceFeature(container);
        Features.CombatMechanics.CombatMechanicsIoC.RegisterCombatMechanicsFeature(container);
        MainMenuCustomizerIoC.RegisterMainMenuCustomizerFeature(container);
        ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container);
        SiegeDefenseIoC.RegisterSiegeDefenseFeature(container);
        ArmyTargetingIoC.RegisterArmyTargetingFeature(container);
        TimeAccelerationIoC.RegisterTimeAccelerationFeature(container);
        SpecialResourcesIoC.RegisterSpecialResourcesFeature(container);
        CareerSystemIoC.RegisterCareerSystemFeature(container);
        SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);
        NamedCompanionIoC.RegisterNamedCompanionsFeature(container);
        RevoltTuningIoC.RegisterRevoltTuningFeature(container);
        SettlementFoodIoC.RegisterSettlementFoodFeature(container);
        SettlementEconomyIoC.RegisterSettlementEconomyFeature(container);
        CaravanTradeIoC.RegisterCaravanTradeFeature(container);
        BanditManagementIoC.RegisterBanditManagementFeature(container);
        CastleRecruitmentIoC.RegisterCastleRecruitmentFeature(container);
        EliteEmissaryIoC.RegisterEliteEmissaryFeature(container);
        SiegeDismountIoC.RegisterSiegeDismountFeature(container);
        MixedFormationsIoC.RegisterMixedFormationsFeature(container);
        SmartCavalryAIIoC.RegisterSmartCavalryAIFeature(container);
        QuickActionsIoC.RegisterQuickActionsFeature(container);
        MessengerIoC.RegisterMessengerFeature(container);
        EquipPresetsIoC.RegisterEquipPresetsFeature(container);
        CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);
        FiefManagementIoC.RegisterFiefManagementFeature(container);
        EditorCacheRebuildIoC.RegisterEditorCacheRebuildFeature(container);
        CulturalFeatsIoC.RegisterCulturalFeatsFeature(container);
        CultureMarketplaceIoC.RegisterCultureMarketplaceFeature(container);
        CultureConversionIoC.RegisterCultureConversionFeature(container);
        MissionDiagnosticIoC.RegisterMissionDiagnosticFeature(container);
        CrashReportIoC.RegisterCrashReportFeature(container);
        BattleLoadDiagnosticsIoC.RegisterBattleLoadDiagnosticsFeature(container);
        Features.SaveLoadDiagnostics.SaveLoadDiagnosticsIoC.RegisterSaveLoadDiagnosticsFeature(container);
        NameplateFadeIoC.RegisterSettlementNameplateFadeFeature(container);
        Features.LotrIssues.LotrIssuesIoC.RegisterLotrIssuesFeature(container);

        _container = container;

        // Post-registration initialization
        CareerSystemIoC.InitializeCalculators(container.Resolve<Features.CareerSystem.Mutations.IMutationCalculatorRegistry>());
    }

    private static void RegisterCoreServices(IContainer container)
    {
        container.Register<IModulePathAdapter, ModulePathAdapter>(Reuse.Singleton);
        container.Register<IFaceGenAdapter, FaceGenAdapter>(Reuse.Singleton);
        container.Register<IPathService, PathService>(Reuse.Singleton);
        container.Register<IReflectionService, ReflectionService>(Reuse.Singleton);
        container.Register<IRaceManager, RaceManager>(Reuse.Singleton);
        container.Register<IHeroRosterAdapter, HeroRosterAdapter>(Reuse.Singleton);
        // Phase 9b #148 P2 - IVolunteerContextAdapter moved to TroopProgressionIoC for cohesion
        // (only consumer is TaomVolunteerModel inside that feature).
        container.Register<IMissionAdapterFactory, MissionAdapterFactory>(Reuse.Singleton);
        container.Register<IObjectManagerAdapter, ObjectManagerAdapter>(Reuse.Singleton);
        container.Register<ICampaignSessionAdapter, CampaignSessionAdapter>(Reuse.Singleton);
    }

    private static void RegisterLoggingServices(IContainer container)
    {
        container.Register<IModLogger, FileLogger>(Reuse.Singleton);
    }

    public static T Resolve<T>()
    {
        return _container.Resolve<T>();
    }

    public static IEnumerable<T> ResolveAll<T>()
    {
        return _container.ResolveMany<T>();
    }

    public static void Dispose()
    {
        _container?.Dispose();
        _container = null;
    }
}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 630ms:
        _shaderRunner = IoC.Resolve<ShaderPrecompileRunner>();
        ShaderPrecompilationIoC.InitializeHooks(logger, _shaderRunner);

        _harmony.PatchCategory("Patch22_ArmyTargeting");
        // Patch49: Finalizer guarding vanilla Army.FindBestGatheringSettlementAndMoveTheLeader,
        // which NREs (Army.cs:726 settlement.GatePosition / 659 Kingdom.Settlements, v1.4.6) when a
        // besieger army can't resolve a gathering fortification - a map-tick CTD on siege start.
        // No TAOM patch is on the stack; aggressive Patch22 targeting just makes it more reachable.
        // Crash report 2026-06-17. See the patch's doc-comment.
        _harmony.PatchCategory("Patch49_ArmyGatheringNreGuard");
        // Patch59: CaravanTrade - four postfixes on CaravansCampaignBehavior private methods
        // (war gate, destination re-weight, range envelope, budget-factor floor) so AI/player caravans
        // range past the local town cluster instead of shuttling. Campaign-behavior target, so applied
        // in this campaign-phase block alongside the other AI patches.
        _harmony.PatchCategory("Patch59_CaravanTrade");
        _harmony.PatchCategory("Patch30_MixedFormations");
        // Patch_MissionTime_SetMovementOrder (shared by Patch31_SmartCavalryAI +
        // Patch35_CompanionTactics' Formation.SetMovementOrder hook) is applied in
        // OnMissionBehaviorInitialize - MovementOrder.cctor reads Mission.Current.CurrentTime,
        // which is null during OnSubModuleLoad and would crash JIT prep with NRE.

        var bannerColorConfig = IoC.Resolve<IBannerColorConfigProvider>();
        var bannerColorService = IoC.Resolve<IBannerColorService>();
        var bannerHeroAdapter = IoC.Resolve<IBannerHeroAdapter>();

[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

codex
One subtle edge I checked: the service’s recency-factor gate accepts `0`, but the only production `ICaravanVisitMemory` implementation floors rank-0 strength-1.0 to `0.05`, so runtime cannot produce a hard exclusion through this path. I’m checking the validator semantics rather than assuming inclusive/exclusive behavior.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /c:\"class FiniteFloatValidator\" /c:\"IsFiniteInRange\" Main\\*.cs Main\\**\\*.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /c:\"interface IModLogger\" /c:\"LogDebug\" /c:\"LogWarning\" Main\\Core\\*.cs Main\\Core\\**\\*.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /c:\"AntiShuttlePenalty\" /c:\"HomeDistanceReweight\" Main\\*.cs Main\\**\\*.cs TAOM.Tests\\Features\\CaravanTrade\\*.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /c:\"LastVisitedSettlement\" Main\\Features\\CaravanTrade\\*.cs Main\\Features\\CaravanTrade\\Hooks\\*.cs" in E:\repos\TAOM
 succeeded in 470ms:
Main\Core\Domain\RaceManager.cs:60:                    _logger?.LogDebug($"  Race ID {i} = '{raceName}'");
Main\Core\Domain\RaceManager.cs:65:                _logger.LogWarning("FaceGen.GetRaceNames() returned null, using fallback mapping");
Main\Core\Domain\RaceManager.cs:117:            _logger.LogWarning($"Unknown race name '{name}' encountered. Defaulting to ID 0 (human). " +
Main\Core\Domain\RaceManager.cs:139:        _logger.LogWarning($"Unknown race ID {id} encountered. Defaulting to 'human'. " +
Main\Core\Logging\FileLogger.cs:31:    public void LogDebug(string message) => Enqueue("DEBUG", message);
Main\Core\Logging\FileLogger.cs:32:    public void LogWarning(string message) => Enqueue("WARNING", message);
Main\Core\Logging\IModLogger.cs:5:public interface IModLogger : IDisposable
Main\Core\Logging\IModLogger.cs:8:    void LogDebug(string message);
Main\Core\Logging\IModLogger.cs:9:    void LogWarning(string message);
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 449ms:
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:53:            // Replaces the old LastVisitedSettlement check, which was inert (it only ever matched the
Main\Features\CaravanTrade\ICaravanVisitMemory.cs:13:/// Replaces the previous <c>isJustLeft = town == LastVisitedSettlement</c> logic, which was inert:
Main\Features\CaravanTrade\ICaravanVisitMemory.cs:14:/// <c>LastVisitedSettlement</c> equals the caravan's CURRENT (parked) town at decision time, and
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 507ms:
Main\Core\Validation\FiniteFloatValidator.cs:19:public static class FiniteFloatValidator
Main\Core\Validation\FiniteFloatValidator.cs:33:    public static bool IsFiniteInRange(float value, float min, float max) =>
Main\Core\Validation\FiniteFloatValidator.cs:57:    public static bool IsFiniteInRange(double value, double min, double max) =>
Main\Features\AlignmentDesertion\AlignmentDesertionConfigProvider.cs:74:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.Rate, 0f, 1f))
Main\Features\BanditManagement\BanditScalingConfigProvider.cs:66:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.DensityCurve, 0f, 5f))
Main\Features\BanditManagement\BanditScalingConfigProvider.cs:73:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.PartySizeCurve, 0f, 5f))
Main\Features\BanditManagement\BanditScalingConfigProvider.cs:80:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.BossFightCurve, 0f, 5f))
Main\Features\BattleBalance\BattleBalanceConfigProvider.cs:66:                if (!FiniteFloatValidator.IsFiniteInRange(value, 0.01f, 1000f))
Main\Features\BattleBalance\BattleBalanceConfigProvider.cs:82:                if (!FiniteFloatValidator.IsFiniteInRange(value, -1f, 1f))
Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:32:            return FiniteFloatValidator.IsFiniteInRange(raw, MinWatchdogSeconds, MaxWatchdogSeconds)
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:79:        if (!FiniteFloatValidator.IsFiniteInRange(c.RangeMultiplier, 1f, 4f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:87:        if (!FiniteFloatValidator.IsFiniteInRange(c.DistanceDecayExponent, 0.25f, 4f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:94:        if (!FiniteFloatValidator.IsFiniteInRange(c.NearFieldFlattenDays, 0f, 20f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:102:        if (!FiniteFloatValidator.IsFiniteInRange(c.MaxCompensation, 1f, 20f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:110:        if (!FiniteFloatValidator.IsFiniteInRange(c.AntiShuttlePenalty, 0f, 1f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:125:        if (!FiniteFloatValidator.IsFiniteInRange(c.BudgetFactorFloor, 0f, 1f))
Main\Features\CaravanTrade\CaravanTradeService.cs:64:        if (FiniteFloatValidator.IsFiniteInRange(recencyPenaltyFactor, 0f, 1f))
Main\Features\CaravanTrade\CaravanVisitMemory.cs:77:        if (!FiniteFloatValidator.IsFiniteInRange(strength, 0f, 1f))
Main\Features\CombatMechanics\ChargeKnockdownService.cs:49:        float speedRef = FiniteFloatValidator.IsFiniteInRange(
Main\Features\CombatMechanics\CombatMechanicsConfigProvider.cs:358:        if (FiniteFloatValidator.IsFiniteInRange(value, min, max))
Main\Features\CultureConversion\CultureConversionConfigProvider.cs:81:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.MinLoyaltyToConvert, 0f, 100f))
Main\Features\CultureMarketplace\CultureMarketplaceConfigProvider.cs:131:                    if (!FiniteFloatValidator.IsFiniteInRange(weight, 0f, MaxWeight))
Main\Features\EditorCacheRebuild\CacheRebuildConfigProvider.cs:85:        if (!FiniteFloatValidator.IsFiniteInRange(parsed.IncrementalSpatialRadius, 0.1f, 100.0f))
Main\Features\EditorCacheRebuild\CacheRebuildConfigProvider.cs:99:        if (!FiniteFloatValidator.IsFiniteInRange(parsed.SmokeTestDistanceTolerance, 1e-8f, 1e-2f))
Main\Features\InitialChildGeneration\InitialChildGenerationConfigProvider.cs:132:        if (!FiniteFloatValidator.IsFiniteInRange(value.Value, 0.0, 1.0))
Main\Features\NavalTravel\NavalTravelConfigProvider.cs:66:        if (!FiniteFloatValidator.IsFiniteInRange(threshold, 0f, NavalTravelConfig.MaxEmbarkThresholdDistance))
Main\Features\NavalTravel\NavalTravelConfigProvider.cs:77:        // (0, MaxBoatScale]; min 0.001 excludes a zero/invisible boat. IsFiniteInRange rejects NaN/Infinity first.
Main\Features\NavalTravel\NavalTravelConfigProvider.cs:78:        if (!FiniteFloatValidator.IsFiniteInRange(boatScale, 0.001f, NavalTravelConfig.MaxBoatScale))
Main\Features\PartyIconScale\PartyIconScaleConfig.cs:39:        raw is float v && FiniteFloatValidator.IsFiniteInRange(v, Min, Max) ? v : Default;
Main\Features\SettlementEconomy\SettlementEconomyConfigProvider.cs:64:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownGoldBase, 0f, 200000f))
Main\Features\SettlementEconomy\SettlementEconomyConfigProvider.cs:72:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownGoldPerProsperity, 0f, 100f))
Main\Features\SettlementEconomy\SettlementEconomyConfigProvider.cs:81:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownGoldRegenRate, 0f, 1f))
Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:83:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.TownBaseFood, 0f, 10000f))
Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:90:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.CastleBaseFood, 0f, 10000f))
Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:97:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.VillageFoodMultiplier, 0f, 10000f))
Main\Features\SettlementFood\SettlementFoodConfigProvider.cs:104:        if (!FiniteFloatValidator.IsFiniteInRange(sanitized.FlatFoodBonus, 0f, 100000f))
Main\Features\WarOfTheRingMomentum\MomentumConfigProvider.cs:90:        if (!FiniteFloatValidator.IsFiniteInRange(parsed.Player.ParticipationMultiplier, 1f, 100f))
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 512ms:
Main\Features\CaravanTrade\CaravanTradeConfig.cs:22:    public float AntiShuttlePenalty { get; set; } = 0.5f;
Main\Features\CaravanTrade\CaravanTradeConfig.cs:28:    public bool HomeDistanceReweight { get; set; } = true;
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:68:            AntiShuttlePenalty = parsed.AntiShuttlePenalty,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:69:            HomeDistanceReweight = parsed.HomeDistanceReweight,
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:110:        if (!FiniteFloatValidator.IsFiniteInRange(c.AntiShuttlePenalty, 0f, 1f))
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:112:            _logger.LogWarning($"CaravanTradeConfigProvider: antiShuttlePenalty={c.AntiShuttlePenalty} must be finite in [0,1], reverting to {defaults.AntiShuttlePenalty}");
Main\Features\CaravanTrade\CaravanTradeConfigProvider.cs:113:            c.AntiShuttlePenalty = defaults.AntiShuttlePenalty;
Main\Features\CaravanTrade\CaravanTradeService.cs:45:        // UNLESS the HomeDistanceReweight escape hatch is off; either way vanilla's upstream home-gravity
Main\Features\CaravanTrade\CaravanTradeService.cs:48:        bool applyDistanceReweight = (!isHomeTown || _settings.HomeDistanceReweight) && days > 0f;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:29:    public float AntiShuttlePenalty => Cfg.AntiShuttlePenalty;
Main\Features\CaravanTrade\CaravanTradeSettingsProvider.cs:30:    public bool HomeDistanceReweight => Cfg.HomeDistanceReweight;
Main\Features\CaravanTrade\CaravanVisitMemory.cs:10:/// configured <see cref="ICaravanTradeSettingsProvider.AntiShuttlePenalty"/> as the max strength, and
Main\Features\CaravanTrade\CaravanVisitMemory.cs:76:        float strength = _settings.AntiShuttlePenalty;
Main\Features\CaravanTrade\ICaravanTradeService.cs:43:    /// <see cref="ICaravanTradeSettingsProvider.HomeDistanceReweight"/> is off (escape hatch); vanilla's
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:31:    float AntiShuttlePenalty { get; }
Main\Features\CaravanTrade\ICaravanTradeSettingsProvider.cs:34:    bool HomeDistanceReweight { get; }
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

codex
The validator search shows `AntiShuttlePenalty` is now only read by the config/settings layer and `CaravanVisitMemory`, not by `CaravanTradeService`; that matches the intended move of the lever. I’m checking whether any additional non-suspect defect falls out of the engine event or IoC wiring before I write the final.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'findstr /n "''^" Main'"\\Core\\Validation\\FiniteFloatValidator.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"public static IMbEvent<MobileParty, Settlement, Hero> SettlementEntered\" /c:\"public override void OnSettlementEntered\" /c:\"_settlementEntered.Invoke\" /c:\"public static IMbEvent<MobileParty, PartyBase> MobilePartyDestroyed\" /c:\"_mobilePartyDestroyed.Invoke\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignEvents.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem\\CampaignEvents.cs -TotalCount 1715 | Select-Object -Last 130" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"IsCaravan\" /c:\"public bool IsCaravan\" E:\\Decompiled_Bannerlord\\Campaign\\TaleWorlds.CampaignSystem\\TaleWorlds.CampaignSystem.Party\\MobileParty.cs" in E:\repos\TAOM
 succeeded in 421ms:
1:namespace TAOM.Core.Validation;
2:
3:/// <summary>
4:/// Centralized float-validation helpers for config providers.
5:///
6:/// Why this exists: range checks like `value &lt; min || value &gt; max` evaluate false for `NaN`
7:/// (all NaN comparisons return false per IEEE-754), so a `NaN` config value sneaks past validation
8:/// and then breaks downstream comparisons in unpredictable ways. This has shipped twice:
9:///
10:/// * Career cooldown review #31 (2026-05-04) — NaN cooldown made `IsOnCooldown =&gt; CooldownRemaining &gt; 0f`
11:///   evaluate false → ability "always ready" → V re-activates indefinitely.
12:/// * EditorCacheRebuild Codex review #38 (2026-05-12) — NaN `SmokeTestDistanceTolerance` made the gate's
13:///   `maxDelta &gt; tolerance` evaluate false → smoke test silently disabled → potential threading
14:///   issues never caught.
15:///
16:/// Use these helpers BEFORE every range check on a `float`/`double` config field. Bool/int fields
17:/// don't need this — only IEEE-754 types are affected.
18:/// </summary>
19:public static class FiniteFloatValidator
20:{
21:    /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
22:    public static bool IsFinite(float value) =>
23:        !float.IsNaN(value) && !float.IsInfinity(value);
24:
25:    /// <summary>Returns true if <paramref name="value"/> is a real, finite number (not NaN, not ±Infinity).</summary>
26:    public static bool IsFinite(double value) =>
27:        !double.IsNaN(value) && !double.IsInfinity(value);
28:
29:    /// <summary>
30:    /// Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).
31:    /// NaN/Infinity always return false. Standard pattern for config range validation.
32:    /// </summary>
33:    public static bool IsFiniteInRange(float value, float min, float max) =>
34:        IsFinite(value) && value >= min && value <= max;
35:
36:    /// <summary>
37:    /// Returns true if <paramref name="value"/> is finite AND less than or equal to <paramref name="max"/>.
38:    /// Use for penalty fields constrained to be non-positive (e.g., loyalty penalties must be ≤ 0).
39:    /// NaN/Infinity always return false.
40:    /// </summary>
41:    public static bool IsFiniteAtMost(float value, float max) =>
42:        IsFinite(value) && value <= max;
43:
44:    /// <summary>
45:    /// Returns true if <paramref name="value"/> is finite AND greater than or equal to <paramref name="min"/>.
46:    /// Use for bonus fields constrained to be non-negative.
47:    /// NaN/Infinity always return false.
48:    /// </summary>
49:    public static bool IsFiniteAtLeast(float value, float min) =>
50:        IsFinite(value) && value >= min;
51:
52:    // Phase 9b #126 — double overloads. InitialChildGeneration (and other features that parse
53:    // JSON via Newtonsoft `Value<double?>()`) need double-typed validation. Same semantics as
54:    // the float overloads — NaN/Infinity always fail, range is inclusive.
55:
56:    /// <summary>Returns true if <paramref name="value"/> is finite AND within [min, max] (inclusive).</summary>
57:    public static bool IsFiniteInRange(double value, double min, double max) =>
58:        IsFinite(value) && value >= min && value <= max;
59:
60:    /// <summary>Returns true if <paramref name="value"/> is finite AND ≤ <paramref name="max"/>.</summary>
61:    public static bool IsFiniteAtMost(double value, double max) =>
62:        IsFinite(value) && value <= max;
63:
64:    /// <summary>Returns true if <paramref name="value"/> is finite AND ≥ <paramref name="min"/>.</summary>
65:    public static bool IsFiniteAtLeast(double value, double min) =>
66:        IsFinite(value) && value >= min;
67:}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 428ms:
661:	public static IMbEvent<MobileParty, Settlement, Hero> SettlementEntered => Instance._settlementEntered;
703:	public static IMbEvent<MobileParty, PartyBase> MobilePartyDestroyed => Instance._mobilePartyDestroyed;
1598:	public override void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
1600:		Instance._settlementEntered.Invoke(party, settlement, hero);
1705:		Instance._mobilePartyDestroyed.Invoke(mobileParty, destroyerParty);
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 458ms:
1101:				if (IsCaravan || IsBanditBossParty)
1243:	public bool IsCaravan { get; private set; }
2372:		if (IsCaravan && Army != null)
2380:		if (MBSaveLoad.IsUpdatingGameVersion && MBSaveLoad.LastLoadedGameVersion < ApplicationVersion.FromString("v1.2.0") && IsCaravan && Owner == Hero.MainHero && ActualClan == null)
4176:		IsCaravan = _partyComponent is CaravanPartyComponent;
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 635ms:
	}

	public override void OnPlayerTraitChanged(TraitObject trait, int previousLevel)
	{
		Instance._playerTraitChangedEvent.Invoke(trait, previousLevel);
	}

	public override void OnVillageStateChanged(Village village, Village.VillageStates oldState, Village.VillageStates newState, MobileParty raiderParty)
	{
		Instance._villageStateChanged.Invoke(village, oldState, newState, raiderParty);
	}

	public override void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
	{
		Instance._settlementEntered.Invoke(party, settlement, hero);
	}

	public override void OnAfterSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
	{
		Instance._afterSettlementEntered.Invoke(party, settlement, hero);
	}

	public override void OnBeforeSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
	{
		Instance._beforeSettlementEntered.Invoke(party, settlement, hero);
	}

	public override void OnMercenaryTroopChangedInTown(Town town, CharacterObject oldTroopType, CharacterObject newTroopType)
	{
		Instance._mercenaryTroopChangedInTown.Invoke(town, oldTroopType, newTroopType);
	}

	public override void OnMercenaryNumberChangedInTown(Town town, int oldNumber, int newNumber)
	{
		Instance._mercenaryNumberChangedInTown.Invoke(town, oldNumber, newNumber);
	}

	public override void OnAlleyOccupiedByPlayer(Alley alley, TroopRoster troops)
	{
		Instance._alleyOccupiedByPlayer.Invoke(alley, troops);
	}

	public override void OnAlleyOwnerChanged(Alley alley, Hero newOwner, Hero oldOwner)
	{
		Instance._alleyOwnerChanged.Invoke(alley, newOwner, oldOwner);
	}

	public override void OnAlleyClearedByPlayer(Alley alley)
	{
		Instance._alleyClearedByPlayer.Invoke(alley);
	}

	public override void OnRomanticStateChanged(Hero hero1, Hero hero2, Romance.RomanceLevelEnum romanceLevel)
	{
		Instance._romanticStateChanged.Invoke(hero1, hero2, romanceLevel);
	}

	public override void OnBeforeHeroesMarried(Hero hero1, Hero hero2, bool showNotification = true)
	{
		Instance._beforeHeroesMarried.Invoke(hero1, hero2, showNotification);
	}

	public override void OnPlayerEliminatedFromTournament(int round, Town town)
	{
		Instance._playerEliminatedFromTournament.Invoke(round, town);
	}

	public override void OnPlayerStartedTournamentMatch(Town town)
	{
		Instance._playerStartedTournamentMatch.Invoke(town);
	}

	public override void OnTournamentStarted(Town town)
	{
		Instance._tournamentStarted.Invoke(town);
	}

	public override void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail declareWarDetail)
	{
		Instance._warDeclared.Invoke(faction1, faction2, declareWarDetail);
	}

	public override void OnTournamentFinished(CharacterObject winner, MBReadOnlyList<CharacterObject> participants, Town town, ItemObject prize)
	{
		Instance._tournamentFinished.Invoke(winner, participants, town, prize);
	}

	public override void OnTournamentCancelled(Town town)
	{
		Instance._tournamentCancelled.Invoke(town);
	}

	public override void OnStartBattle(PartyBase attackerParty, PartyBase defenderParty, object subject, bool showNotification)
	{
		Instance._battleStarted.Invoke(attackerParty, defenderParty, subject, showNotification);
	}

	public override void OnRebellionFinished(Settlement settlement, Clan oldOwnerClan)
	{
		Instance._rebellionFinished.Invoke(settlement, oldOwnerClan);
	}

	public override void TownRebelliousStateChanged(Town town, bool rebelliousState)
	{
		Instance._townRebelliousStateChanged.Invoke(town, rebelliousState);
	}

	public override void OnRebelliousClanDisbandedAtSettlement(Settlement settlement, Clan clan)
	{
		Instance._rebelliousClanDisbandedAtSettlement.Invoke(settlement, clan);
	}

	public override void OnItemsLooted(MobileParty mobileParty, ItemRoster items)
	{
		Instance._itemsLooted.Invoke(mobileParty, items);
	}

	public override void OnMobilePartyDestroyed(MobileParty mobileParty, PartyBase destroyerParty)
	{
		Instance._mobilePartyDestroyed.Invoke(mobileParty, destroyerParty);
	}

	public override void OnMobilePartyCreated(MobileParty party)
	{
		Instance._mobilePartyCreated.Invoke(party);
	}

	public override void OnMapInteractableCreated(IInteractablePoint interactable)
	{
		Instance._mapInteractableCreated.Invoke(interactable);
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:14:45.463035Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
I’m checking one lifecycle risk outside the named suspects: the memory is intentionally not saved, but it is also registered as an IoC singleton. If the IoC container lives longer than a campaign session, stale visit rings could bleed across loads unless something clears them.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /n /c:\"class SubModule\" /c:\"OnSubModuleLoad\" /c:\"OnGameStart\" /c:\"OnBeforeInitialModuleScreenSetAsRoot\" /c:\"IoC.Configure\" /c:\"IoC.Dispose\" /c:\"AddGameMenus\" /c:\"CampaignGameStarter\" Main\\SubModule.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\SubModule.cs -TotalCount 150 | Select-Object -Last 140" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /c:\"OnGameLoaded\" /c:\"GameStarted\" /c:\"CampaignStart\" /c:\"OnSessionLaunched\" Main\\Features Main\\*.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /c:\"CampaignBehaviorBase\" /c:\"SyncData(IDataStore\" /c:\"Clear(\" Main\\Features\\*\\*.cs Main\\Features\\*\\Hooks\\*.cs" in E:\repos\TAOM
 succeeded in 466ms:
91:public class SubModule : MBSubModuleBase
102:    protected override void OnSubModuleLoad()
104:        base.OnSubModuleLoad();
106:        IoC.Configure();
109:        // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
112:        // is the IoC.Configure() call itself — if THAT throws, the entire feature is
141:        // MUST be applied here in OnSubModuleLoad: MCM's ResourceInjector.Inject() runs at
142:        // OnBeforeInitialModuleScreenSetAsRoot (after every module's OnSubModuleLoad), so the Postfix
172:        // in OnSubModuleLoad (process-static one-shot) — NOT the late OnGameInitializationFinished batch —
187:        // HIGH). Applied here in OnSubModuleLoad like Patch58: loads are triggered from the main
295:        // which is null during OnSubModuleLoad and would crash JIT prep with NRE.
328:        // dependency, safe in OnSubModuleLoad). The transpilers swap the AI IsCastle gate to a runtime
340:    protected override void OnBeforeInitialModuleScreenSetAsRoot()
342:        base.OnBeforeInitialModuleScreenSetAsRoot();
349:        // (Codex C1, issue #299). By here, IoC.Configure() (OnSubModuleLoad) has already set the guard,
436:    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
438:        base.OnGameStart(game, gameStarterObject);
442:        // idempotent so OnGameStart on save-load doesn't spam.
447:        catch { /* diagnostic is best-effort, never break OnGameStart */ }
449:        if (gameStarterObject is CampaignGameStarter campaignStarter)
453:            // must run inside this OnGameStart (after Sandbox registered its behaviors) — so the
475:    // former OnGameStart inline block — bodies are verbatim, order unchanged).
477:        CampaignGameStarter campaignStarter,
519:    private static void RegisterRaceAgeAndFamily(CampaignGameStarter campaignStarter)
537:    private static void RegisterDiplomacyAndConflict(CampaignGameStarter campaignStarter)
569:        CampaignGameStarter campaignStarter,
597:    private static void RegisterBattleBalanceAndTargeting(CampaignGameStarter campaignStarter)
613:        CampaignGameStarter campaignStarter,
678:    // conversion, and the LOTR issue takeover (suppression stays inside OnGameStart, last in order).
679:    private static void RegisterCampaignLifeBehaviors(CampaignGameStarter campaignStarter)
762:        // before this OnGameStart) and register the single LOTR custom-issue dispatcher in their
872:        // NavalTravel PARKED 2026-06-26 (#296/#120) — see the model-registration comment in OnGameStart.
1048:        IoC.Dispose();
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:15:31.385135Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.5 seconds
Output:
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 exited 1 in 483ms:
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 495ms:
Main\Adapters\CampaignSessionAdapter.cs:48:            try { snapshot.StartTime = (campaign.Models?.CampaignTimeModel?.CampaignStartTime ?? CampaignTime.Zero).ToString(); }
Main\Features\BannerInjection\BannerInjectionBehavior.cs:27:        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(
Main\Features\CareerSystem\CareerCampaignBehavior.cs:35:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\CareerSystem\CareerCampaignBehavior.cs:44:    private void OnSessionLaunched(CampaignGameStarter starter)
Main\Features\CareerSystem\CareerCampaignBehavior.cs:51:        _logger.LogInfo("CareerSystem: OnSessionLaunched fired");
Main\Features\CareerSystem\CareerCampaignBehavior.cs:55:            _logger.LogWarning("CareerSystem: OnSessionLaunched — MainHero is null, aborting");
Main\Features\CareerSystem\CareerSwitchDialogueBehavior.cs:36:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\CareerSystem\CareerSwitchDialogueBehavior.cs:43:    private void OnSessionLaunched(CampaignGameStarter campaignStarter)
Main\Features\CareerSystem\Quests\CareerQuest.cs:64:    // QuestManager.OnGameLoaded (1.4.5) silently CompleteQuestWithCancel's any ongoing quest that has
Main\Features\CareerSystem\Quests\CareerQuest.cs:67:    // save-load. (deep-review API-compat finding, 2026-06-01; verified vs QuestManager.OnGameLoaded.)
Main\Features\CareerSystem\Quests\CareerQuestCampaignBehavior.cs:39:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\CareerSystem\Quests\CareerQuestCampaignBehavior.cs:49:    private void OnSessionLaunched(CampaignGameStarter starter) => TryOfferNext();
Main\Features\CastleRecruitment\Hooks\CastleNotableMaintainer.cs:37:            // Runs inside OnNewGameCreated/OnGameLoaded: an escaped exception doesn't CTD — it stalls
Main\Features\CastleRecruitment\Hooks\CastleRecruitmentBehavior.cs:40:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\CastleRecruitment\Hooks\CastleRecruitmentBehavior.cs:42:        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
Main\Features\CastleRecruitment\Hooks\CastleRecruitmentBehavior.cs:55:    private void OnSessionLaunched(CampaignGameStarter starter)
Main\Features\CastleRecruitment\Hooks\CastleRecruitmentBehavior.cs:59:        // OnSessionLaunched (registered before TAOM behaviors), so the menu exists by now.
Main\Features\CastleRecruitment\Hooks\CastleRecruitmentBehavior.cs:98:    private void OnGameLoaded(CampaignGameStarter starter)
Main\Features\CompanionTactics\FormationPresets\FormationPresetService.cs:76:    public void OnGameLoaded(List<HoNFormationPreset> loaded)
Main\Features\CompanionTactics\FormationPresets\FormationPresetService.cs:80:        DebugLog($"OnGameLoaded — {_presets.Count} preset(s)");
Main\Features\CompanionTactics\FormationPresets\FormationPresetService.cs:87:        // Phase 9b #139 P2 — explicit new-game reset (was OnGameLoaded(empty)).
Main\Features\CompanionTactics\FormationPresets\Hooks\FormationPresetCampaignBehavior.cs:63:                _service.OnGameLoaded(_savedPresets ?? new List<HoNFormationPreset>());
Main\Features\CompanionTactics\FormationPresets\Hooks\FormationPresetCampaignBehavior.cs:71:            _service.OnGameLoaded(_savedPresets);
Main\Features\CompanionTactics\FormationPresets\Hooks\FormationPresetCampaignBehavior.cs:86:        // Phase 9b #139 P2 — was OnGameLoaded(empty) (semantic mismatch: load-path entry point
Main\Features\CompanionTactics\FormationPresets\IFormationPresetService.cs:24:    /// <summary>Replace all presets (called from <c>OnGameLoaded</c> with the saved list).</summary>
Main\Features\CompanionTactics\FormationPresets\IFormationPresetService.cs:25:    void OnGameLoaded(List<HoNFormationPreset> loaded);
Main\Features\CompanionTactics\FormationPresets\IFormationPresetService.cs:35:    /// called <c>OnGameLoaded(empty)</c> for both save-load and new-game-reset, which is a
Main\Features\CrashReport\Collectors\CampaignStateCollector.cs:28:        try { started = campaign.GameStarted; } catch { }
Main\Features\CrashReport\Domain\CampaignSnapshot.cs:5:// Populated only when Campaign.Current != null AND GameStarted == true.
Main\Features\CultureConversion\Hooks\CultureConversionBehavior.cs:46:        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
Main\Features\CultureConversion\Hooks\CultureConversionBehavior.cs:47:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\CultureConversion\Hooks\CultureConversionBehavior.cs:63:            _justLoadedFromSave = true; // tells OnSessionLaunched NOT to clear the freshly-loaded store
Main\Features\CultureConversion\Hooks\CultureConversionBehavior.cs:83:    private void OnGameLoaded(CampaignGameStarter starter) => _service.ReapplyConvertedCultures();
Main\Features\CultureConversion\Hooks\CultureConversionBehavior.cs:93:    private void OnSessionLaunched(CampaignGameStarter starter)
Main\Features\CultureMarketplace\CultureMarketplaceBehavior.cs:62:        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
Main\Features\CultureMarketplace\CultureMarketplaceBehavior.cs:75:    private void OnGameLoaded(CampaignGameStarter starter) => EnsurePoolBuilt();
Main\Features\Diplomacy\DiplomacyBehavior.cs:23:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
Main\Features\Diplomacy\DiplomacyService.cs:151:                    "Will retry via OnSessionLaunched/EnforcePermanentAlliances.");
Main\Features\Diplomacy\PlayerAllianceProposalBehavior.cs:33:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\Diplomacy\PlayerAllianceProposalBehavior.cs:39:    private void OnSessionLaunched(CampaignGameStarter starter)
Main\Features\Diplomacy\WarOfTheRingBehavior.cs:35:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\Diplomacy\WarOfTheRingBehavior.cs:58:    private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
Main\Features\Diplomacy\WarOfTheRingBehavior.cs:62:        var elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
Main\Features\Diplomacy\WarOfTheRingBehavior.cs:69:        var elapsedDays = Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow;
Main\Features\EliteEmissary\Hooks\EliteEmissaryBehavior.cs:53:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\EliteEmissary\Hooks\EliteEmissaryBehavior.cs:67:    private void OnSessionLaunched(CampaignGameStarter starter)
Main\Features\EquipPresets\Hooks\EquipmentPresetCampaignBehavior.cs:30:        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
Main\Features\EquipPresets\Hooks\EquipmentPresetCampaignBehavior.cs:53:    private void OnGameLoaded(CampaignGameStarter starter)
Main\Features\EquipPresets\Hooks\EquipmentPresetCampaignBehavior.cs:75:            _logger.LogInfo($"[EquipPresets] OnGameLoaded pruned {pruned} orphaned preset bundle(s)");
Main\Features\EquipPresets\IEquipmentPresetService.cs:35:    /// Called by <see cref="Hooks.EquipmentPresetCampaignBehavior.OnGameLoaded"/>; the live-id
Main\Features\FiefManagement\Hooks\FiefHubCampaignBehavior.cs:23:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\FiefManagement\Hooks\FiefHubCampaignBehavior.cs:25:        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
Main\Features\FiefManagement\Hooks\FiefHubCampaignBehavior.cs:34:    private void OnGameLoaded(CampaignGameStarter starter) => _presenter.Reset();
Main\Features\FiefManagement\Hooks\FiefHubCampaignBehavior.cs:40:    public void OnSessionLaunched(CampaignGameStarter starter)
Main\Features\HeroRace\RacePersistenceBehavior.cs:20:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _service.RestoreHeroRaces());
Main\Features\Messengers\MessengerCampaignBehavior.cs:43:    // Bannerlord process. The fields below are RESET when OnSessionLaunched sees a new starter (Codex review #34
Main\Features\Messengers\MessengerCampaignBehavior.cs:65:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\Messengers\MessengerCampaignBehavior.cs:84:            _justLoadedFromSave = true;   // signals OnSessionLaunched to NOT clear the freshly-loaded store
Main\Features\Messengers\MessengerCampaignBehavior.cs:147:    private void OnSessionLaunched(CampaignGameStarter starter)
Main\Features\Messengers\MessengerCampaignBehavior.cs:152:        //     in SyncData(IsLoading=true), which fires before OnSessionLaunched. If it's set, the
Main\Features\MissionDiagnostic\IMissionDiagnosticService.cs:10:    // Called once per session, on OnSessionLaunched.
Main\Features\MissionDiagnostic\MissionDiagnosticService.cs:56:            if (Campaign.Current != null && Campaign.Current.GameStarted)
Main\Features\NamedCompanions\INamedCompanionService.cs:10:    // CampaignBehavior on OnSessionLaunched so a new campaign re-spawns its companions.
Main\Features\NamedCompanions\NamedCompanionBehavior.cs:23:        // Codex review caught the earlier OnSessionLaunchedEvent subscription — that event fires
Main\Features\NamedCompanions\NamedCompanionBehavior.cs:32:        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(
Main\Features\NazgulFamily\NazgulFamilyBehavior.cs:50:        => CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\NazgulFamily\NazgulFamilyBehavior.cs:54:    private void OnSessionLaunched(CampaignGameStarter starter)
Main\Features\QuickActions\Hooks\InventorySearchCampaignBehavior.cs:39:        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
Main\Features\QuickActions\Hooks\InventorySearchCampaignBehavior.cs:61:    private void OnGameLoaded(CampaignGameStarter starter)
Main\Features\RaceAge\RaceAgeBehavior.cs:29:        // showed this can happen). OnSessionLaunched (not OnNewGameCreated) so load also resets.
Main\Features\RaceAge\RaceAgeBehavior.cs:30:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => _raceAgeService.ResetCache());
Main\Features\Siege\SiegeDefenseBehavior.cs:31:        // OnSessionLaunchedEvent (which fires for both new + load) would race with SyncData on load.
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:36:    // Phase 9b deferred #133 P2 — desertion grace flag. Set true in OnSessionLaunched +
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:46:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:53:        // OnSessionLaunched both run while the character-creation PLACEHOLDER culture is still on
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:56:        // OnGameLoaded = existing/legacy save (culture already correct).
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:58:        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:138:    private void OnGameLoaded(CampaignGameStarter starter)
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:161:    private void OnSessionLaunched(CampaignGameStarter starter)
Main\Features\SpecialResources\SpecialResourcesBehavior.cs:163:        // Seeding moved to OnCharacterCreationIsOver (new game) + OnGameLoaded (load): OnSessionLaunched
Main\Features\WarOfTheRingMomentum\WarOfTheRingMomentumBehavior.cs:78:        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
Main\Features\WarOfTheRingMomentum\WarOfTheRingMomentumBehavior.cs:85:        // The map meter needs a live MapScreen — OnSessionLaunched is too early on load.
Main\Features\WarOfTheRingMomentum\WarOfTheRingMomentumBehavior.cs:134:    private void OnSessionLaunched(CampaignGameStarter starter)
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 842ms:
using TAOM.Features.BannerInjection;
using TAOM.Features.HeroRace;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.FactionMap;
using TAOM.Features.InitialChildGeneration;
using TAOM.Adapters;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.Execution.Hooks;
using TAOM.Features.Execution.Models;
using TAOM.Features.RaceAge;
using TAOM.Features.RaceAge.Models;
using TAOM.Features.StartupResources;
using TAOM.Features.NamedCompanions;
using TAOM.Features.TroopProgression;
using TAOM.Features.TroopWeight;
using TAOM.Features.TroopWeight.Diagnostics;
using TAOM.Features.TroopWeight.Hooks;
using TAOM.Features.AtmospherePersistence.Hooks;
using TAOM.Features.TroopProgression.Models;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Features.NavalTravel;
using TAOM.Features.NavalTravel.Models;
using TAOM.Features.NazgulFamily;
using TAOM.Features.NazgulFamily.Models;
using TAOM.Features.CustomBattles;
using TAOM.Features.CustomBattles.Hooks;
using TAOM.Features.Warg;
using TAOM.Features.Spider;
using TAOM.Features.BattleBalance;
using TAOM.Features.BattleBalance.Models;
using TAOM.Features.Arena.Models;
using TAOM.Features.Encyclopedia;
using TAOM.Features.Encyclopedia.Models;
using TAOM.Features.MainMenuCustomizer;
using TAOM.Features.NativeSkinFixes;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.Siege;
using TAOM.Features.Siege.Models;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.ArmyTargeting.Models;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Features.LocalizationOverride;
using TAOM.Features.LocalizationOverride.Hooks;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Hooks;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Models;
using TAOM.Features.CombatMechanics.Models;
using TAOM.Features.SettlementGuards;
using TAOM.Features.SettlementGuards.Hooks;
using TAOM.Features.RevoltTuning;
using TAOM.Features.SettlementEconomy;
using TAOM.Features.SettlementEconomy.Models;
using TAOM.Features.SettlementFood;
using TAOM.Features.SettlementFood.Models;
using TAOM.Features.BanditManagement;
using TAOM.Features.BanditManagement.Models;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.CastleRecruitment.Hooks;
using TAOM.Features.SiegeDismount.Hooks;
using TAOM.Features.MixedFormations.Hooks;
using TAOM.Features.SmartCavalryAI.Hooks;
using TAOM.Features.FiefManagement;
using TAOM.Features.FiefManagement.Hooks;
using TAOM.Features.SettlementNameplateFade;
using TAOM.Features.SettlementNameplateFade.Hooks;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using BehaviorTreeWrapper;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM;

public class SubModule : MBSubModuleBase
{
    private Harmony _harmony;
    private UIExtender? _uiExtender;
    private ITimeAccelerationService? _timeAccelerationService;
    private static float _shaderTickAccumulator;
    private static ShaderPrecompileRunner _shaderRunner;
    private static bool _missionTimePatchesApplied;
    private static bool _gameInitPatchesApplied;
    private static bool _basicTableauGuardApplied;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        IoC.Configure();

        // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
        // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
        // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
        // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot
        // is the IoC.Configure() call itself - if THAT throws, the entire feature is
        // unreachable. Split CrashReport bootstrap doesn't fix this without re-implementing
        // a manual DI container; accept and document the residual.
        _harmony = new Harmony("com.taom.mod");
        if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
        {
            try
            {
                _harmony.PatchCategory("Patch37_CrashReport");
                IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
                if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
                {
                    IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
                }
            }
            catch (System.Exception ex)
            {
                IoC.Resolve<IModLogger>().LogError($"[CrashReport] init failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        _uiExtender = UIExtender.Create("TAOM");
        _uiExtender.Register(typeof(SubModule).Assembly);
        _uiExtender.Enable();

        // Patch41_McmLayoutFix - flip MCM's embedded options-screen prefabs from VerticalBottomToTop
        // to VerticalTopToBottom (v1.4.0 layout regression). MCM's prefabs are embedded in
        // Bannerlord.MBOptionScreen and load via WidgetFactoryManager.CreateAndRegister, which bypasses
        // UIExtenderEx's [PrefabExtension] hook - so this is a Harmony Postfix, not a PrefabExtension.
        // MUST be applied here in OnSubModuleLoad: MCM's ResourceInjector.Inject() runs at
        // OnBeforeInitialModuleScreenSetAsRoot (after every module's OnSubModuleLoad), so the Postfix
        // must already be attached when MCM calls CreateAndRegister.
        _harmony.PatchCategory("Patch41_McmLayoutFix");

        _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();

        // Must be first - intercepts GetLocalizedText before any game texts are resolved.
        // Loads English string overrides from taom_module_strings.xml (removes hardcoded "The" articles).
        _harmony.PatchCategory("Patch25_LocalizationOverride");
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:15:31.886256Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
There is a possible lifecycle gap: `IoC.Configure()` runs in `OnSubModuleLoad`, while the campaign behavior is resolved from that process-level container in `OnGameStart`. I’m checking whether anything clears `CaravanVisitMemory` on new-game/load; if not, stale rings can survive across campaign sessions despite the “ephemeral, rebuilt” comment.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\SubModule.cs -TotalCount 1065 | Select-Object -Last 45" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "findstr /s /n /c:\"ClearAll\" /c:\"Reset\" /c:\"CaravanVisitMemory\" Main\\Features\\CaravanTrade\\*.cs Main\\Features\\CaravanTrade\\Hooks\\*.cs TAOM.Tests\\Features\\CaravanTrade\\*.cs" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Features\\Messengers\\MessengerCampaignBehavior.cs -TotalCount 175 | Select-Object -Last 150" in E:\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-Content Main\\Features\\CompanionTactics\\FormationPresets\\Hooks\\FormationPresetCampaignBehavior.cs -TotalCount 100 | Select-Object -Last 90" in E:\repos\TAOM
 succeeded in 447ms:
Main\Features\CaravanTrade\CaravanTradeIoC.cs:15:        container.Register<ICaravanVisitMemory, CaravanVisitMemory>(Reuse.Singleton);
Main\Features\CaravanTrade\CaravanTradeIoC.cs:16:        container.Register<CaravanVisitMemoryBehavior>(Reuse.Singleton);
Main\Features\CaravanTrade\CaravanVisitMemory.cs:7:/// Pure implementation of <see cref="ICaravanVisitMemory"/>. Per caravan, a bounded ring of the last
Main\Features\CaravanTrade\CaravanVisitMemory.cs:18:public class CaravanVisitMemory : ICaravanVisitMemory
Main\Features\CaravanTrade\CaravanVisitMemory.cs:28:    public CaravanVisitMemory(ICaravanTradeSettingsProvider settings)
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:8:/// Thin entry point (ADR-002, no logic) that feeds <see cref="ICaravanVisitMemory"/>. Records each
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:20:public class CaravanVisitMemoryBehavior : CampaignBehaviorBase
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:22:    private readonly ICaravanVisitMemory _memory;
Main\Features\CaravanTrade\CaravanVisitMemoryBehavior.cs:24:    public CaravanVisitMemoryBehavior(ICaravanVisitMemory memory)
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:25:    private static ICaravanVisitMemory _memory;
Main\Features\CaravanTrade\Hooks\CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:37:            _memory ??= IoC.Resolve<ICaravanVisitMemory>();
Main\Features\CaravanTrade\ICaravanTradeService.cs:51:    /// <param name="recencyPenaltyFactor">Recency multiplier in (0,1] from <see cref="ICaravanVisitMemory"/>; 1 = no penalty. NaN/out-of-range is ignored.</param>
Main\Features\CaravanTrade\ICaravanVisitMemory.cs:8:/// Pure + TaleWorlds-free (ADR-007): the thin <see cref="CaravanVisitMemoryBehavior"/> and the
Main\Features\CaravanTrade\ICaravanVisitMemory.cs:18:public interface ICaravanVisitMemory
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 622ms:

    protected override void OnApplicationTick(float dt)
    {
        _timeAccelerationService?.OnTick();

        // Shader pre-compilation walk: tick the runner every frame (responsive state transitions),
        // and surface its status as a 1 Hz toast when a loading screen isn't already showing it.
        var runner = _shaderRunner;
        if (runner != null && runner.IsActive)
        {
            runner.Tick();
            _shaderTickAccumulator += dt;
            if (_shaderTickAccumulator >= 1f)
            {
                _shaderTickAccumulator = 0f;
                if (!LoadingWindow.IsLoadingWindowActive && !string.IsNullOrEmpty(runner.StatusLine))
                    InformationManager.DisplayMessage(new InformationMessage(runner.StatusLine));
            }
        }
    }

    protected override void OnSubModuleUnloaded()
    {
        base.OnSubModuleUnloaded();
        // Detach the AppDomain.UnhandledException subscription BEFORE IoC disposal so
        // the hook doesn't hold a stale reference to a disposed CrashReportService
        // across game-restart-in-same-process. Deep-review INC 3 (2026-05-25).
        try { IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>()?.Unsubscribe(); }
        catch { /* IoC may already be torn down - best-effort */ }

        // Reverse NativeSkinFixes hooks so DLL unload during reload-in-same-process
        // doesn't leave dangling MinHook trampolines. Best-effort - swallows.
        try { NativeSkinFixesInstaller.Uninstall(); }
        catch { /* shutdown - never block */ }

        _harmony?.UnpatchAll("com.taom.mod");
        IoC.Dispose();

        // Codex review #46 (2026-05-25) HIGH-01: clear the static service cache in
        // the patch helper so the next module load resolves a fresh service graph from
        // the new IoC container. Without this, Finalizers fire against a disposed
        // FileLogger after reload and silently drop every log line.
        TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.ResetForUnload();
    }
}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 625ms:
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Owns the SyncData buffer for formation presets. The try/catch around <c>dataStore.SyncData</c>
/// guards the LOAD path (deserialize / BaseId collision with another mod) and the save-time ref
/// population - on a parse error it degrades gracefully (presets reset, behavior continues).
///
/// It does NOT and cannot guard the SAVE byte-serialization: the engine writes the buffer later on the
/// AsyncFileSaveDriver background thread, outside this method, so an unserializable field would crash
/// there regardless of this catch. The fix for that class of bug lives in the saveable model itself -
/// keep every <see cref="HoNFormationPreset"/> <c>[SaveableField]</c> a serializable type (pinned by
/// <c>HoNFormationPresetSerializationTests</c>). History: a <c>DateTime</c> field used to crash every save.
/// </summary>
public sealed class FormationPresetCampaignBehavior : CampaignBehaviorBase
{
    private readonly IFormationPresetService _service;
    private readonly IModLogger _logger;

    private List<HoNFormationPreset> _savedPresets = new();

    public FormationPresetCampaignBehavior(IFormationPresetService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        // P2-3 fix (Codex review #36, 2026-05-06): the service is a DryIoc singleton, so its
        // _presets survive across campaign sessions in the same process. SyncData(IsLoading)
        // resets them when loading a save, but a NEW campaign has no load snapshot, so without
        // OnNewGameCreated the previous campaign's presets bleed into the new one. Clear the
        // service on new-game creation. Mirrors the EquipPresets pattern.
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // P2-2 fix (Codex review #36, 2026-05-06): vanilla SyncData<T> records the ref value
        // at call time. We MUST populate _savedPresets BEFORE calling SyncData on save, or
        // the engine writes the previous (loaded/default) buffer and the new presets are
        // not persisted until the NEXT save. Mirror the load path: pull the save snapshot
        // first, then call SyncData, then on load hand the deserialized list to the service.
        try
        {
            if (dataStore.IsSaving)
            {
                _savedPresets = _service.GetPresetsForSaving();
            }

            dataStore.SyncData("TAOM_FormationPresets", ref _savedPresets);

            if (dataStore.IsLoading)
            {
                _service.OnGameLoaded(_savedPresets ?? new List<HoNFormationPreset>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[CompanionTactics] FormationPreset SyncData failed (likely BaseId collision): {ex.Message}");
            // On any deserialization error, reset to empty rather than crashing the load.
            _savedPresets = new List<HoNFormationPreset>();
            _service.OnGameLoaded(_savedPresets);
            // Phase 9b #139 P1 - surface to player. Pre-fix only LogWarning to TAOM internal log;
            // player never saw the cause and lost presets repeatedly with no idea why.
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[TAOM] Formation presets could not be loaded - reset to empty (see log for details).",
                    Color.FromUint(0xFFFFAA00)));
            }
            catch { /* InformationManager may not be available in some load paths */ }
        }
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        // Phase 9b #139 P2 - was OnGameLoaded(empty) (semantic mismatch: load-path entry point
        // used for new-game reset). Now calls Reset() explicitly.
        _savedPresets = new List<HoNFormationPreset>();
        _service.Reset();
    }
}
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

 succeeded in 659ms:
// hidden business logic.
public class MessengerCampaignBehavior : CampaignBehaviorBase, IMissionListener
{
    private readonly IMessengerService _service;
    private readonly IMessengerStateStore _store;
    private readonly IMessengerSettingsProvider _settings;
    private readonly IModLogger _logger;

    private static readonly MissionMode[] AllowedMissionModes = { MissionMode.Conversation, MissionMode.Barter };

    private bool _dialogsRegistered;
    private bool _processingArrivedMessenger;
    private PendingMessenger _activeMessenger;
    private Mission _currentMission;
    private Vec2 _originalPosition = Vec2.Invalid;
    private readonly List<string> _toRemoveScratch = new List<string>();
    // Behavior is registered Reuse.Singleton, so a single instance survives across campaigns within the same
    // Bannerlord process. The fields below are RESET when OnSessionLaunched sees a new starter (Codex review #34
    // 2026-05-06: prior session's _dialogsRegistered=true would have suppressed dialog registration in campaign 2).
    private CampaignGameStarter _lastSessionStarter;
    private bool _justLoadedFromSave;

    public MessengerCampaignBehavior(
        IMessengerService service,
        IMessengerStateStore store,
        IMessengerSettingsProvider settings,
        IModLogger logger)
    {
        _service = service;
        _store = store;
        _settings = settings;
        _logger = logger;
    }

    // --- CampaignBehaviorBase ---

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var snapshot = _store.Serialize();
            dataStore.SyncData("_taom_messengers", ref snapshot);
        }
        else
        {
            Dictionary<string, string> snapshot = null;
            dataStore.SyncData("_taom_messengers", ref snapshot);
            _store.Deserialize(snapshot);
            _processingArrivedMessenger = false;
            _activeMessenger = null;
            _currentMission = null;
            _originalPosition = Vec2.Invalid;
            _justLoadedFromSave = true;   // signals OnSessionLaunched to NOT clear the freshly-loaded store
        }
    }

    // --- Public API (callable by other features) ---

    public void SendMessenger(Hero targetHero)
    {
        // Codex review #34: runtime MCM toggle (Settings.Messengers.EnableMessengers OFF mid-game)
        // must short-circuit dispatch in addition to behavior registration.
        if (!_settings.EnableMessengers) return;

        var snapshot = SnapshotHero(targetHero);
        var playerGold = Hero.MainHero?.Gold ?? 0;
        var validation = _service.CanSendMessenger(snapshot, playerGold);
        if (validation != MessengerValidationResult.Ok)
        {
            ShowInquiry(
                new TextObject("{=taom_messenger_cannot_send}Cannot Send Messenger").ToString(),
                BuildValidationReason(validation, targetHero).ToString(),
                affirmative: GameTexts.FindText("str_ok").ToString());
            return;
        }

        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, _settings.MessengerGoldCost, false);

        var startVec = Hero.MainHero?.GetMapPoint()?.Position.ToVec2() ?? Vec2.Zero;
        var messenger = new PendingMessenger(
            targetHeroId: targetHero.StringId,
            dispatchTimeDays: CampaignTime.Now.ToDays,
            position: ToMapCoord(startVec),
            arrived: false);
        _store.Add(messenger);

        var sentText = new TextObject("{=taom_messenger_sent}A messenger has been dispatched to {HERO_NAME} and will arrive within {DAYS} days.");
        sentText.SetTextVariable("HERO_NAME", targetHero.Name);
        sentText.SetTextVariable("DAYS", _settings.MessengerTravelDays);
        ShowInquiry(
            new TextObject("{=taom_messenger_sent_title}Messenger Sent").ToString(),
            sentText.ToString(),
            affirmative: GameTexts.FindText("str_ok").ToString());
    }

    public bool CanSendMessenger(Hero targetHero, out TextObject reason)
    {
        if (!_settings.EnableMessengers)
        {
            reason = new TextObject("{=taom_messenger_disabled}The messenger system is disabled in settings.");
            return false;
        }

        var validation = _service.CanSendMessenger(SnapshotHero(targetHero), Hero.MainHero?.Gold ?? 0);
        if (validation == MessengerValidationResult.Ok)
        {
            reason = TextObject.GetEmpty();
            return true;
        }
        reason = BuildValidationReason(validation, targetHero);
        return false;
    }

    // --- Lifecycle ---

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Singleton behavior + cross-campaign-in-same-process safety:
        //   * A new starter ? reset all per-campaign instance state.
        //   * Clear the store only when this is a NEW campaign - `_justLoadedFromSave` flips true
        //     in SyncData(IsLoading=true), which fires before OnSessionLaunched. If it's set, the
        //     store has freshly-deserialized state we must keep.
        if (_lastSessionStarter != starter)
        {
            _lastSessionStarter = starter;
            _dialogsRegistered = false;
            _processingArrivedMessenger = false;
            _activeMessenger = null;
            _currentMission = null;
            _originalPosition = Vec2.Invalid;
            if (!_justLoadedFromSave)
                _store.Clear();
        }

        // Phase 9b #123 - `_justLoadedFromSave` MUST clear unconditionally, not inside the
        // `if (starter != _lastSessionStarter)` gate. Same-process save  load  save  load gives
        // the SAME starter on the 2nd load, so the gate is false and the flag stayed stuck-on,
        // misleading any future consumer of the flag.
        _justLoadedFromSave = false;

        if (!_dialogsRegistered)
        {
            AddDialogOptions(starter);
            _dialogsRegistered = true;
[31;1mInvalidOperation: [31;1mCannot set property. Property setting is supported only on core types in this language mode.[0m

2026-07-11T22:15:41.925986Z ERROR codex_models_manager::manager: failed to renew cache TTL: unknown variant `max`, expected one of `none`, `minimal`, `low`, `medium`, `high`, `xhigh` at line 29 column 25
codex
**VANILLA CODE**

Verified from `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\...`. The `tools/taom-src.ps1` wrapper was blocked by shell policy, so I used the installed decompiled source directly.

`CaravansCampaignBehavior.cs:911,923,927,965,993-996,1032`:

```csharp
private Town FindNextDestinationForCaravan(...)
{
    foreach (Town allTown in Town.AllTowns)
    {
        if (allTown.Owner.Settlement != caravanParty.CurrentSettlement && ...)
        {
            float tradeScoreForTown = GetTradeScoreForTown(caravanParty, allTown, value, input, distanceCut, out bestNavigationType2, out isTargetingPort2);
```

```csharp
private float GetTradeScoreForTown(MobileParty caravanParty, Town town, CampaignTime lastHomeVisitTimeOfCaravan, float caravanFullness, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isTargetingPort)
...
float num5 = 1f;
if (caravanParty.HomeSettlement == town.Owner.Settlement)
{
    num5 = 1f + elapsedDaysUntilNow * 0.1f * (elapsedDaysUntilNow * 0.1f);
...
return (num7 + num8) * num4 * num13 * num5 * ...
```

`MobileParty.cs:577,602`:

```csharp
public Settlement CurrentSettlement
{
    ...
    if (_currentSettlement != null)
    {
        ...
        LastVisitedSettlement = value;
```

`DefaultClanFinanceModel.cs:690-698`:

```csharp
private int AddIncomeFromParty(MobileParty party, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
{
    ...
    if (party.IsActive && ... && (party.IsLordParty || party.IsGarrison || party.IsCaravan))
    {
        int partyTradeGold = party.PartyTradeGold;
        if (partyTradeGold > 10000)
            num = (partyTradeGold - 10000) / 10;
```

**KNOWN SUSPECTS**

1. DISPUTED. The new penalty is live. After B→C, `CaravanVisitMemory` ring is `[B,C]`; C is newest/rank 0 but vanilla excludes only `CurrentSettlement` C, while B remains selectable (`CaravansCampaignBehavior.cs:923`). B gets rank 1: `CaravanVisitMemory.cs:64-68,80-84`, default factor `0.625`.

2. DISPUTED. `ICaravanVisitMemory` is `Reuse.Singleton` and the behavior is also singleton: `CaravanTradeIoC.cs:13-16`. Behavior writes through constructor injection (`CaravanVisitMemoryBehavior.cs:24-27,47`); hook reads through `IoC.Resolve<ICaravanVisitMemory>()` (`CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:36-37,55`).

3. DISPUTED. Finance payout is not home-gated: `DefaultClanFinanceModel.cs:690-698`. Home gravity remains in vanilla raw score via `num5`, and TAOM only rescales the returned score (`CaravanTradeService.cs:48-58`).

3b. DISPUTED. Home recency cannot permanently suppress return: recency is floored positive (`CaravanVisitMemory.cs:23,84`), depth is 4 (`:20,50-51`), and vanilla `num5` grows unbounded (`CaravansCampaignBehavior.cs:993-996`).

4. DISPUTED. `GetRecencyPenaltyFactor` cannot return zero or negative in production; rank-0 strength-1.0 floors to `0.05` (`CaravanVisitMemory.cs:23,80-84`). Sparse regions may still choose the only positive candidate, but they do not strand.

5. DISPUTED. NaN polarity is correct: raw NaN fails `rawScore > 0` (`CaravanTradeService.cs:31-32`), NaN days fail `days > 0f` before `Math.Pow` (`:48-51`), NaN recency is ignored (`:64-65`; validator rejects NaN at `FiniteFloatValidator.cs:33-34`), and NaN strength returns `1f` (`CaravanVisitMemory.cs:76-78`).

6. DISPUTED for the stated long-campaign leak. Record and clear both key by `party.StringId` (`CaravanVisitMemoryBehavior.cs:47,55-56`), and lists trim to depth 4 (`CaravanVisitMemory.cs:49-51`). Separate lifecycle finding below.

7. DISPUTED. Hook passes the factor into the service (`CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:55-57`); the service returns before applying anything when inactive/player-scoped off (`CaravanTradeService.cs:27-28,145-151`). Unconditional recording is harmless while no consumer is active.

8. DISPUTED. Defaults and validation line up: DTO `0.5/true` (`CaravanTradeConfig.cs:22,28`), provider clone copies both (`CaravanTradeConfigProvider.cs:68-69`), anti-shuttle remains `[0,1]` validated (`:110-113`), settings exposes both (`CaravanTradeSettingsProvider.cs:29-30`), JSON is `0.5/true` (`caravan_trade_config.json:13-15`), and doc table matches (`docs/features/caravan-trade.md:49-50`).

**CONFIG CROSS-REFERENCE**

`antiShuttlePenalty`: JSON `0.5`, DTO default `0.5`, doc table `0.5`, consumed only by `CaravanVisitMemory`.

`homeDistanceReweight`: JSON `true`, DTO default `true`, provider clone copies it, settings exposes it, service uses it at `CaravanTradeService.cs:48`.

Master toggle: `ReweightTradeScore` starts with `IsActiveFor` (`CaravanTradeService.cs:27-28`), so disabled returns exact raw vanilla score.

Behavior registration: present at `SubModule.cs:728`; Harmony category applied at `SubModule.cs:290`.

**FINDINGS**

[MEDIUM] Main/Features/CaravanTrade/CaravanVisitMemoryBehavior.cs:35 — Lifecycle State — Visit memory is not persisted, but also not cleared on load/new campaign. `CaravanVisitMemory` is a DryIoc singleton (`CaravanTradeIoC.cs:15`) created from the process-level container configured in `OnSubModuleLoad` (`SubModule.cs:106`). Because `SyncData` intentionally does nothing and there is no `OnGameLoaded`/`OnNewGameCreated`/`OnSessionLaunched` clear, stale rings can survive a same-process save-load or new campaign and penalize towns visited in an abandoned future/previous campaign. Minimal fix: add `ClearAll()` to `ICaravanVisitMemory` and clear on load/new session before destination scoring resumes.

Summary:
CRITICAL: 0 | HIGH: 0 | MEDIUM: 1 | LOW: 0  
VERDICT: ISSUES FOUND
tokens used
110,135
**VANILLA CODE**

Verified from `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\...`. The `tools/taom-src.ps1` wrapper was blocked by shell policy, so I used the installed decompiled source directly.

`CaravansCampaignBehavior.cs:911,923,927,965,993-996,1032`:

```csharp
private Town FindNextDestinationForCaravan(...)
{
    foreach (Town allTown in Town.AllTowns)
    {
        if (allTown.Owner.Settlement != caravanParty.CurrentSettlement && ...)
        {
            float tradeScoreForTown = GetTradeScoreForTown(caravanParty, allTown, value, input, distanceCut, out bestNavigationType2, out isTargetingPort2);
```

```csharp
private float GetTradeScoreForTown(MobileParty caravanParty, Town town, CampaignTime lastHomeVisitTimeOfCaravan, float caravanFullness, bool distanceCut, out MobileParty.NavigationType bestNavigationType, out bool isTargetingPort)
...
float num5 = 1f;
if (caravanParty.HomeSettlement == town.Owner.Settlement)
{
    num5 = 1f + elapsedDaysUntilNow * 0.1f * (elapsedDaysUntilNow * 0.1f);
...
return (num7 + num8) * num4 * num13 * num5 * ...
```

`MobileParty.cs:577,602`:

```csharp
public Settlement CurrentSettlement
{
    ...
    if (_currentSettlement != null)
    {
        ...
        LastVisitedSettlement = value;
```

`DefaultClanFinanceModel.cs:690-698`:

```csharp
private int AddIncomeFromParty(MobileParty party, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
{
    ...
    if (party.IsActive && ... && (party.IsLordParty || party.IsGarrison || party.IsCaravan))
    {
        int partyTradeGold = party.PartyTradeGold;
        if (partyTradeGold > 10000)
            num = (partyTradeGold - 10000) / 10;
```

**KNOWN SUSPECTS**

1. DISPUTED. The new penalty is live. After B→C, `CaravanVisitMemory` ring is `[B,C]`; C is newest/rank 0 but vanilla excludes only `CurrentSettlement` C, while B remains selectable (`CaravansCampaignBehavior.cs:923`). B gets rank 1: `CaravanVisitMemory.cs:64-68,80-84`, default factor `0.625`.

2. DISPUTED. `ICaravanVisitMemory` is `Reuse.Singleton` and the behavior is also singleton: `CaravanTradeIoC.cs:13-16`. Behavior writes through constructor injection (`CaravanVisitMemoryBehavior.cs:24-27,47`); hook reads through `IoC.Resolve<ICaravanVisitMemory>()` (`CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:36-37,55`).

3. DISPUTED. Finance payout is not home-gated: `DefaultClanFinanceModel.cs:690-698`. Home gravity remains in vanilla raw score via `num5`, and TAOM only rescales the returned score (`CaravanTradeService.cs:48-58`).

3b. DISPUTED. Home recency cannot permanently suppress return: recency is floored positive (`CaravanVisitMemory.cs:23,84`), depth is 4 (`:20,50-51`), and vanilla `num5` grows unbounded (`CaravansCampaignBehavior.cs:993-996`).

4. DISPUTED. `GetRecencyPenaltyFactor` cannot return zero or negative in production; rank-0 strength-1.0 floors to `0.05` (`CaravanVisitMemory.cs:23,80-84`). Sparse regions may still choose the only positive candidate, but they do not strand.

5. DISPUTED. NaN polarity is correct: raw NaN fails `rawScore > 0` (`CaravanTradeService.cs:31-32`), NaN days fail `days > 0f` before `Math.Pow` (`:48-51`), NaN recency is ignored (`:64-65`; validator rejects NaN at `FiniteFloatValidator.cs:33-34`), and NaN strength returns `1f` (`CaravanVisitMemory.cs:76-78`).

6. DISPUTED for the stated long-campaign leak. Record and clear both key by `party.StringId` (`CaravanVisitMemoryBehavior.cs:47,55-56`), and lists trim to depth 4 (`CaravanVisitMemory.cs:49-51`). Separate lifecycle finding below.

7. DISPUTED. Hook passes the factor into the service (`CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:55-57`); the service returns before applying anything when inactive/player-scoped off (`CaravanTradeService.cs:27-28,145-151`). Unconditional recording is harmless while no consumer is active.

8. DISPUTED. Defaults and validation line up: DTO `0.5/true` (`CaravanTradeConfig.cs:22,28`), provider clone copies both (`CaravanTradeConfigProvider.cs:68-69`), anti-shuttle remains `[0,1]` validated (`:110-113`), settings exposes both (`CaravanTradeSettingsProvider.cs:29-30`), JSON is `0.5/true` (`caravan_trade_config.json:13-15`), and doc table matches (`docs/features/caravan-trade.md:49-50`).

**CONFIG CROSS-REFERENCE**

`antiShuttlePenalty`: JSON `0.5`, DTO default `0.5`, doc table `0.5`, consumed only by `CaravanVisitMemory`.

`homeDistanceReweight`: JSON `true`, DTO default `true`, provider clone copies it, settings exposes it, service uses it at `CaravanTradeService.cs:48`.

Master toggle: `ReweightTradeScore` starts with `IsActiveFor` (`CaravanTradeService.cs:27-28`), so disabled returns exact raw vanilla score.

Behavior registration: present at `SubModule.cs:728`; Harmony category applied at `SubModule.cs:290`.

**FINDINGS**

[MEDIUM] Main/Features/CaravanTrade/CaravanVisitMemoryBehavior.cs:35 — Lifecycle State — Visit memory is not persisted, but also not cleared on load/new campaign. `CaravanVisitMemory` is a DryIoc singleton (`CaravanTradeIoC.cs:15`) created from the process-level container configured in `OnSubModuleLoad` (`SubModule.cs:106`). Because `SyncData` intentionally does nothing and there is no `OnGameLoaded`/`OnNewGameCreated`/`OnSessionLaunched` clear, stale rings can survive a same-process save-load or new campaign and penalize towns visited in an abandoned future/previous campaign. Minimal fix: add `ClearAll()` to `ICaravanVisitMemory` and clear on load/new session before destination scoring resumes.

Summary:
CRITICAL: 0 | HIGH: 0 | MEDIUM: 1 | LOW: 0  
VERDICT: ISSUES FOUND
