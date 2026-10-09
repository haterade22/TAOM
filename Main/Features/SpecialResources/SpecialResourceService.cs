using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

public class SpecialResourceService : ISpecialResourceService, ISpecialResourceSpender
{
    private readonly ISpecialResourceConfigProvider _config;
    private readonly ISpecialResourceStorageService _storage;
    private readonly IModLogger _logger;
    private readonly ICareerPassiveService _passiveService;
    private readonly HashSet<string> _loggedResolveKeys = new();
    private float _pendingSpend;
    private bool _inSession;

    public SpecialResourceService(ISpecialResourceConfigProvider config, ISpecialResourceStorageService storage, IModLogger logger, ICareerPassiveService passiveService = null)
    {
        _config = config;
        _storage = storage;
        _logger = logger;
        _passiveService = passiveService;
    }

    public SpecialResource ResolveResource(string kingdomId, string cultureId)
    {
        // Resolve is hot-path (called from MapInfoVM.OnRefresh tooltip rebuild several times per tick).
        // Dedupe DEBUG logging by (kingdomId, cultureId) so we keep diagnostics on transitions
        // without flooding the log with thousands of identical lines per session.
        var key = (kingdomId ?? "") + "|" + (cultureId ?? "");
        var firstSeen = _loggedResolveKeys.Add(key);

        if (kingdomId != null)
        {
            var byKingdom = _config.GetByKingdomId(kingdomId);
            if (byKingdom != null)
            {
                if (firstSeen)
                    _logger.LogDebug($"[SpecRes] Resolved resource '{byKingdom.Id}' via kingdom '{kingdomId}'");
                return byKingdom;
            }
        }
        if (cultureId != null)
        {
            var byCulture = _config.GetByCultureId(cultureId);
            if (byCulture != null)
            {
                if (firstSeen)
                    _logger.LogDebug($"[SpecRes] Resolved resource '{byCulture.Id}' via culture '{cultureId}' (kingdom '{kingdomId}' had no match)");
                return byCulture;
            }
        }
        if (firstSeen)
            _logger.LogDebug($"[SpecRes] No resource resolved for kingdom='{kingdomId}', culture='{cultureId}'");
        return null;
    }

    public float GetCurrentAmount(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return 0f;
        return _storage.Get(heroId, resource.Id);
    }

    public void EarnFromBattle(string heroId, string kingdomId, string cultureId, float enemySizeRatio)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var clampedRatio = Math.Max(0.5f, Math.Min(2f, enemySizeRatio));
        Earn(heroId, resource, "BATTLE", resource.PerBattleVictoryBase * clampedRatio, "F1",
            $" (ratio {enemySizeRatio:F2}→{clampedRatio:F2})");
    }

    public void EarnFromRaid(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        Earn(heroId, resource, "RAID", resource.PerRaid, "F0", "");
    }

    public void EarnFromSiege(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        Earn(heroId, resource, "SIEGE", resource.PerSiegeVictory, "F0", "");
    }

    public void EarnFromPrisoners(string heroId, string kingdomId, string cultureId, int prisonerCount)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        Earn(heroId, resource, "PRISONERS", resource.PerPrisoner * prisonerCount, "F0", $" ({prisonerCount} captured)");
    }

    public void EarnFromTournament(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        Earn(heroId, resource, "TOURNAMENT", resource.PerTournamentWin, "F0", "");
    }

    public void EarnFromHideout(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        Earn(heroId, resource, "HIDEOUT", resource.PerHideoutClear, "F0", "");
    }

    // The one earning path: scale by the career gain, add under the cap, log with the source label.
    private void Earn(string heroId, SpecialResource resource, string source, float baseAmount, string amountFormat, string detail)
    {
        var earned = ScaleEarned(heroId, baseAmount);
        var before = _storage.Get(heroId, resource.Id);
        AddCapped(heroId, resource, earned);
        var after = _storage.Get(heroId, resource.Id);
        _logger.LogInfo($"[SpecRes] {source}: +{earned.ToString(amountFormat)} {resource.DisplayName}{detail} | {before:F0}→{after:F0}");
    }

    public void ApplyDailyTick(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var net = ComputeBreakdown(heroId, resource, ownedTownCount, troopsWithUpkeep).Net;

        if (net >= 0)
            AddCapped(heroId, resource, net);
        else
            _storage.Add(heroId, resource.Id, net);
    }

    public DailyResourceBreakdown GetDailyBreakdown(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return DailyResourceBreakdown.Empty;
        return ComputeBreakdown(heroId, resource, ownedTownCount, troopsWithUpkeep);
    }

    public float GetProjectedDailyNet(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
        => GetDailyBreakdown(heroId, kingdomId, cultureId, ownedTownCount, troopsWithUpkeep).Net;

    // The single daily calculation: earning (+SpecialResourceGain) and one upkeep line per troop type
    // that carries a daily_upkeep (each line already scaled by SpecialResourceUpkeepModifier, so the
    // lines sum to the total). ApplyDailyTick applies its Net; every display reads the same object.
    private DailyResourceBreakdown ComputeBreakdown(string heroId, SpecialResource resource, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
    {
        var earning = ScaleEarned(heroId, resource.DailyPerTown * ownedTownCount);

        var lines = BuildUpkeepLines(heroId, troopsWithUpkeep);

        return new DailyResourceBreakdown(earning, lines);
    }

    // The one upkeep computation, read by the breakdown and by desertion. A cost row is not an
    // upkeep row: the Elite Emissary's merchant-only rows and any row without a daily_upkeep fail the
    // CONFIGURED-value gate, which keeps them out of the lines AND out of desertion. A non-finite
    // modifier is treated as no modifier, so the configured upkeep is charged.
    private List<TroopUpkeepLine> BuildUpkeepLines(string heroId, IReadOnlyList<TroopUpkeepInfo> troops)
    {
        var lines = new List<TroopUpkeepLine>();
        if (troops == null) return lines;

        var upkeepModifier = GetPassiveMagnitude(heroId, PassiveEffectType.SpecialResourceUpkeepModifier);
        if (!FiniteFloatValidator.IsFinite(upkeepModifier)) upkeepModifier = 0f;

        foreach (var troop in troops)
        {
            if (troop == null || troop.Count <= 0) continue;
            var perUnit = _config.GetTroopCost(troop.TroopId)?.DailyUpkeep ?? 0f;
            if (!(FiniteFloatValidator.IsFinite(perUnit) && perUnit > 0f)) continue;

            // Total is scaled after the multiply, the order the old single-total math used, so a
            // one-type party debits the same float it always did.
            var total = perUnit * troop.Count;
            if (upkeepModifier != 0f)
            {
                perUnit = Math.Max(0f, perUnit * (1f + upkeepModifier));
                total = Math.Max(0f, total * (1f + upkeepModifier));
            }
            lines.Add(new TroopUpkeepLine(troop.TroopId, troop.Count, perUnit, total));
        }
        return lines;
    }

    // The career gain applies to every earning path, not just the daily town income (#767). It always
    // multiplies (a gain of 0 gives back the base), and a result that is not a finite positive earns
    // nothing, so a -200% pick or a NaN passive can neither debit the wallet nor poison it. Public so
    // the map-bar tooltip projects the same number the earning paths store.
    public float ScaleEarned(string heroId, float amount)
    {
        var gain = GetPassiveMagnitude(heroId, PassiveEffectType.SpecialResourceGain);
        var scaled = amount * (1f + gain);
        return FiniteFloatValidator.IsFinite(scaled) && scaled > 0f ? scaled : 0f;
    }

    public bool CanAffordUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return true;

        var cost = _config.GetTroopCost(troopId);
        if (cost == null) return true;

        var totalCost = GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, count);
        var available = _storage.Get(heroId, resource.Id);
        var canAfford = available >= totalCost;
        _logger.LogDebug($"[SpecRes] CanAfford: {troopId} x{count} cost={totalCost} available={available:F0} → {canAfford}");
        return canAfford;
    }

    public void SpendForUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var cost = _config.GetTroopCost(troopId);
        if (cost == null) return;

        var totalCost = GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, count);
        _storage.Add(heroId, resource.Id, -totalCost);
        _logger.LogInfo($"[SpecRes] SPEND: -{totalCost} {resource.DisplayName} for {troopId} x{count}");
    }

    public float ChargeRecruitCost(string heroId, string kingdomId, string cultureId, string troopId, int count)
    {
        if (count <= 0) return 0f;

        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return 0f;

        var cost = _config.GetTroopCost(troopId);
        if (cost == null || cost.RecruitCost <= 0) return 0f;

        var totalCost = cost.RecruitCost * count;
        // Return what LEFT the wallet, not the nominal cost: the volunteer screen is gated, the party
        // screen's prisoner recruit is not (#563), so the charge can land on a smaller balance and the
        // storage floors at zero. A toast built from the nominal cost would announce currency that was
        // never there (Codex, review 95, F1).
        var before = _storage.Get(heroId, resource.Id);
        _storage.Add(heroId, resource.Id, -totalCost);
        var debited = before - _storage.Get(heroId, resource.Id);
        _logger.LogInfo($"[SpecRes] RECRUIT: -{debited:0.##} {resource.DisplayName} for {troopId} x{count}"
                        + (debited < totalCost ? $" (cost {totalCost}, balance floored at 0)" : ""));
        return debited;
    }

    public RecruitGateResult CanAffordRecruit(string heroId, string kingdomId, string cultureId, IReadOnlyList<RecruitCartEntry> cart)
    {
        if (cart == null || cart.Count == 0) return RecruitGateResult.Allowed;

        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return RecruitGateResult.Allowed;

        var required = 0;
        foreach (var entry in cart)
        {
            if (entry == null || entry.Count <= 0) continue;
            var cost = _config.GetTroopCost(entry.TroopId);
            if (cost == null || cost.RecruitCost <= 0) continue;
            required += cost.RecruitCost * entry.Count;
        }

        if (required <= 0) return RecruitGateResult.Allowed;

        var available = _storage.Get(heroId, resource.Id);
        var blocked = available < required;
        if (blocked)
            _logger.LogDebug($"[SpecRes] RECRUIT GATE: blocked (need {required} {resource.DisplayName}, have {available:F0})");

        return new RecruitGateResult(blocked, required, resource.DisplayName);
    }

    public bool CanAffordMerchantPurchase(string heroId, string kingdomId, string cultureId, string troopId, int count)
    {
        if (count <= 0) return true;

        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return true;

        var cost = _config.GetTroopCost(troopId);
        if (cost == null || cost.MerchantCost <= 0) return true;

        var totalCost = cost.MerchantCost * count;
        var available = _storage.Get(heroId, resource.Id);
        var canAfford = available >= totalCost;
        _logger.LogDebug($"[SpecRes] CanAffordMerchant: {troopId} x{count} cost={totalCost} {resource.DisplayName} available={available:F0} → {canAfford}");
        return canAfford;
    }

    public void ChargeMerchantPurchase(string heroId, string kingdomId, string cultureId, string troopId, int count)
    {
        if (count <= 0) return;

        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null)
        {
            _logger.LogWarning($"[SpecRes] MERCHANT charge skipped: {troopId} x{count} — kingdom='{kingdomId}' culture='{cultureId}' maps to no resource");
            return;
        }

        var cost = _config.GetTroopCost(troopId);
        if (cost == null || cost.MerchantCost <= 0)
        {
            _logger.LogWarning($"[SpecRes] MERCHANT charge skipped: {troopId} has no merchant_cost");
            return;
        }

        var totalCost = cost.MerchantCost * count;
        _storage.Add(heroId, resource.Id, -totalCost);
        _logger.LogInfo($"[SpecRes] MERCHANT: -{totalCost} {resource.DisplayName} for {troopId} x{count} (balance now {_storage.Get(heroId, resource.Id):F0})");
    }

    public void BeginPartyScreenSession()
    {
        _pendingSpend = 0f;
        _inSession = true;
        _logger.LogDebug("[SpecRes] PartyScreen session BEGUN");
    }

    public void QueueUpgradeSpend(string heroId, string troopId, int count)
    {
        var cost = _config.GetTroopCost(troopId);
        if (cost == null) return;

        // Phase 9b #174: apply the career-passive SpecialResourceUpgradeCostModifier here too.
        // Pre-fix this queued base cost while ClampUpgradeCount + SpendForUpgrade used the
        // discounted cost — so a player with a -30% career discount got the cheaper *count* but
        // was debited the full price at CommitSession, silently overpaying by the discount %.
        var added = GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, count);
        _pendingSpend += added;
        _logger.LogDebug($"[SpecRes] QUEUED: {troopId} x{count} = {added} pending (total pending={_pendingSpend:F0})");
    }

    public float GetAvailableAfterPending(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return 0f;
        return _storage.Get(heroId, resource.Id) - _pendingSpend;
    }

    public int ClampUpgradeCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount)
    {
        var cost = _config.GetTroopCost(troopId);
        if (cost == null || cost.UpgradeCost <= 0) return requestedCount;

        var effectivePerUnit = GetEffectiveUpgradeCost(heroId, cost.UpgradeCost, 1);
        if (effectivePerUnit <= 0) return requestedCount;

        var available = GetAvailableAfterPending(heroId, kingdomId, cultureId);
        var maxAffordable = (int)(available / effectivePerUnit);
        var clamped = Math.Max(0, Math.Min(requestedCount, maxAffordable));

        if (clamped < requestedCount)
            _logger.LogDebug($"[SpecRes] CLAMP: {troopId} requested={requestedCount} clamped={clamped} (available={available:F0}, cost/unit={cost.UpgradeCost})");

        return clamped;
    }

    public float CommitSession(string heroId, string kingdomId, string cultureId)
    {
        if (!_inSession) return 0f;

        var debited = 0f;
        if (_pendingSpend > 0f)
        {
            var resource = ResolveResource(kingdomId, cultureId);
            if (resource != null)
            {
                // Measured, not nominal: a prisoner recruited in the same party screen is charged before
                // Done commits this queue, so the balance can be below the pending amount and the storage
                // floors at zero (Codex, review 95, F1).
                var before = _storage.Get(heroId, resource.Id);
                _storage.Add(heroId, resource.Id, -_pendingSpend);
                debited = before - _storage.Get(heroId, resource.Id);
                _logger.LogInfo($"[SpecRes] PartyScreen COMMITTED: -{debited:0.##} {resource.DisplayName}"
                                + (debited < _pendingSpend ? $" (pending {_pendingSpend:0.##}, balance floored at 0)" : ""));
            }
        }
        else
        {
            _logger.LogDebug("[SpecRes] PartyScreen COMMITTED: no pending spend");
        }

        _pendingSpend = 0f;
        _inSession = false;
        return debited;
    }

    public void CancelSession()
    {
        var wasPending = _pendingSpend;
        _pendingSpend = 0f;
        _inSession = false;
        _logger.LogDebug($"[SpecRes] PartyScreen CANCELLED: discarded {wasPending:F0} pending spend");
    }

    public void ResetSessionState()
    {
        // Phase 9b #133 P2 R1 — clear singleton-scope state on new-campaign boundary so the
        // second campaign in the same process doesn't inherit:
        //   - _inSession=true from a prior session (would let a stale CommitSession debit
        //     the new hero's balance against the old pending amount)
        //   - _pendingSpend>0 (would be applied at the next legitimate CommitSession)
        //   - _loggedResolveKeys (so the first resolve of every new (kingdom,culture)
        //     pair in the new campaign still surfaces a single diagnostic line)
        var hadPending = _pendingSpend;
        var wasInSession = _inSession;
        _pendingSpend = 0f;
        _inSession = false;
        _loggedResolveKeys.Clear();
        _logger.LogInfo($"[SpecRes] ResetSessionState: cleared (pending was {hadPending:F0}, inSession was {wasInSession})");
    }

    public void InitializeHero(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null)
        {
            _logger.LogWarning($"[SpecRes] InitializeHero: no resource for kingdom='{kingdomId}', culture='{cultureId}'");
            return;
        }

        _storage.Set(heroId, resource.Id, resource.StartingAmount);
        _logger.LogInfo($"[SpecRes] InitializeHero: {heroId} → {resource.DisplayName} = {resource.StartingAmount}");
    }

    public IReadOnlyList<TroopDesertionEntry> CalculateDesertion(string heroId, string kingdomId, string cultureId, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
    {
        var result = new List<TroopDesertionEntry>();

        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null || troopsWithUpkeep == null || troopsWithUpkeep.Count == 0)
            return result;

        var balance = _storage.Get(heroId, resource.Id);
        if (balance > 0f)
            return result;

        // At 0 resources: 10% of each upkeep troop type deserts per day (min 1). Desertion is the
        // consequence of UNPAID UPKEEP, so a troop whose row carries none (the Elite Emissary's
        // merchant-only rows: 50 normal tree troops) is not in arrears and stays (#558 finding 5).
        foreach (var line in BuildUpkeepLines(heroId, troopsWithUpkeep))
        {
            if (!(line.PerUnit > 0f)) continue;

            var desertCount = Math.Max(1, (int)(line.Count * 0.1f));
            desertCount = Math.Min(desertCount, line.Count);
            result.Add(new TroopDesertionEntry(line.TroopId, desertCount));
        }

        if (result.Count > 0)
        {
            var totalDeserted = 0;
            foreach (var entry in result)
                totalDeserted += entry.DesertCount;
            _logger.LogInfo($"[SpecRes] DESERTION: {totalDeserted} elite troops deserting (balance={balance:F0}, {result.Count} troop types affected)");
        }

        return result;
    }

    public ResourceTier GetCurrentTier(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null || resource.TierThresholds.Count == 0)
            return null;

        var amount = _storage.Get(heroId, resource.Id);

        // Walk from highest tier to lowest; return first one whose threshold is met
        for (var i = resource.TierThresholds.Count - 1; i >= 0; i--)
        {
            if (amount >= resource.TierThresholds[i].Threshold)
                return resource.TierThresholds[i];
        }

        return null;
    }

    public int GetCurrentTierLevel(string heroId, string kingdomId, string cultureId)
    {
        var tier = GetCurrentTier(heroId, kingdomId, cultureId);
        return tier?.Level ?? 0;
    }

    public ResourceGrantResult GrantAmount(string heroId, string kingdomId, string cultureId, float amount)
    {
        // Console text is untrusted: float.TryParse accepts "NaN" and "Infinity", and a NaN would
        // survive AddCapped's Math.Min to poison the saved balance (csharp-architecture.md,
        // "Engine-Float Decision Gates").
        if (!FiniteFloatValidator.IsFinite(amount))
        {
            _logger.LogWarning($"[SpecRes] GrantAmount rejected non-finite amount ({amount}) for hero '{heroId}'");
            return ResourceGrantResult.None;
        }

        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return ResourceGrantResult.None;

        var before = _storage.Get(heroId, resource.Id);
        AddCapped(heroId, resource, amount);
        var after = _storage.Get(heroId, resource.Id);

        _logger.LogInfo($"[SpecRes] CHEAT GRANT: {amount:+0.##;-0.##;0} {resource.DisplayName} | {before:F0}→{after:F0} (cap {resource.Cap:F0})");
        return new ResourceGrantResult(true, resource.Id, resource.DisplayName, before, after, resource.Cap);
    }

    public SpecialResourceBalance? GetBalance(string heroId, string? kingdomId, string? cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        return resource == null ? null : new SpecialResourceBalance(resource.DisplayName, _storage.Get(heroId, resource.Id));
    }

    public bool TrySpend(string heroId, string? kingdomId, string? cultureId, float amount)
    {
        // Positive requirement, so NaN fails the gate (csharp-architecture.md, "Engine-Float Decision Gates").
        if (!(amount > 0f) || !FiniteFloatValidator.IsFinite(amount))
            return false;
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null)
            return false;
        var available = _storage.Get(heroId, resource.Id);
        if (!(available >= amount))
            return false;
        _storage.Add(heroId, resource.Id, -amount);
        _logger.LogInfo($"[SpecRes] SPEND: -{amount:0.##} {resource.DisplayName} for the armoury | {available:F0}→{_storage.Get(heroId, resource.Id):F0}");
        return true;
    }

    private void AddCapped(string heroId, SpecialResource resource, float amount)
    {
        var current = _storage.Get(heroId, resource.Id);
        var newAmount = Math.Min(current + amount, resource.Cap);
        _storage.Set(heroId, resource.Id, newAmount);
    }

    private float GetEffectiveUpgradeCost(string heroId, float baseCostPerUnit, int count)
    {
        var totalCost = baseCostPerUnit * count;
        var costModifier = GetPassiveMagnitude(heroId, PassiveEffectType.SpecialResourceUpgradeCostModifier);
        if (costModifier != 0f)
            totalCost *= (1f + costModifier);
        return Math.Max(0f, totalCost);
    }

    private float GetPassiveMagnitude(string heroId, PassiveEffectType type)
    {
        if (_passiveService == null || heroId == null) return 0f;
        return _passiveService.GetPassiveMagnitude(heroId, type);
    }
}
