using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

public class SpecialResourceService : ISpecialResourceService
{
    private readonly ISpecialResourceConfigProvider _config;
    private readonly ISpecialResourceStorageService _storage;
    private readonly IModLogger _logger;
    private readonly ICareerPassiveService _passiveService;
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
        if (kingdomId != null)
        {
            var byKingdom = _config.GetByKingdomId(kingdomId);
            if (byKingdom != null)
            {
                _logger.LogDebug($"[SpecRes] Resolved resource '{byKingdom.Id}' via kingdom '{kingdomId}'");
                return byKingdom;
            }
        }
        if (cultureId != null)
        {
            var byCulture = _config.GetByCultureId(cultureId);
            if (byCulture != null)
            {
                _logger.LogDebug($"[SpecRes] Resolved resource '{byCulture.Id}' via culture '{cultureId}' (kingdom '{kingdomId}' had no match)");
                return byCulture;
            }
        }
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
        var amount = resource.PerBattleVictoryBase * clampedRatio;
        var before = _storage.Get(heroId, resource.Id);
        AddCapped(heroId, resource, amount);
        var after = _storage.Get(heroId, resource.Id);
        _logger.LogInfo($"[SpecRes] BATTLE: +{amount:F1} {resource.DisplayName} (ratio {enemySizeRatio:F2}→{clampedRatio:F2}) | {before:F0}→{after:F0}");
    }

    public void EarnFromRaid(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var before = _storage.Get(heroId, resource.Id);
        AddCapped(heroId, resource, resource.PerRaid);
        var after = _storage.Get(heroId, resource.Id);
        _logger.LogInfo($"[SpecRes] RAID: +{resource.PerRaid:F0} {resource.DisplayName} | {before:F0}→{after:F0}");
    }

    public void EarnFromSiege(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var before = _storage.Get(heroId, resource.Id);
        AddCapped(heroId, resource, resource.PerSiegeVictory);
        var after = _storage.Get(heroId, resource.Id);
        _logger.LogInfo($"[SpecRes] SIEGE: +{resource.PerSiegeVictory:F0} {resource.DisplayName} | {before:F0}→{after:F0}");
    }

    public void EarnFromPrisoners(string heroId, string kingdomId, string cultureId, int prisonerCount)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var earned = resource.PerPrisoner * prisonerCount;
        var before = _storage.Get(heroId, resource.Id);
        AddCapped(heroId, resource, earned);
        var after = _storage.Get(heroId, resource.Id);
        _logger.LogInfo($"[SpecRes] PRISONERS: +{earned:F0} {resource.DisplayName} ({prisonerCount} captured) | {before:F0}→{after:F0}");
    }

    public void EarnFromTournament(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var before = _storage.Get(heroId, resource.Id);
        AddCapped(heroId, resource, resource.PerTournamentWin);
        var after = _storage.Get(heroId, resource.Id);
        _logger.LogInfo($"[SpecRes] TOURNAMENT: +{resource.PerTournamentWin:F0} {resource.DisplayName} | {before:F0}→{after:F0}");
    }

    public void EarnFromHideout(string heroId, string kingdomId, string cultureId)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var before = _storage.Get(heroId, resource.Id);
        AddCapped(heroId, resource, resource.PerHideoutClear);
        var after = _storage.Get(heroId, resource.Id);
        _logger.LogInfo($"[SpecRes] HIDEOUT: +{resource.PerHideoutClear:F0} {resource.DisplayName} | {before:F0}→{after:F0}");
    }

    public void ApplyDailyTick(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        var earning = resource.DailyPerTown * ownedTownCount;
        var gainModifier = GetPassiveMagnitude(heroId, PassiveEffectType.CustomResourceGain);
        if (gainModifier != 0f)
            earning *= (1f + gainModifier);

        var upkeep = GetDailyUpkeep(troopsWithUpkeep, heroId);
        var net = earning - upkeep;
        var before = _storage.Get(heroId, resource.Id);

        if (net >= 0)
            AddCapped(heroId, resource, net);
        else
            _storage.Add(heroId, resource.Id, net);

        var after = _storage.Get(heroId, resource.Id);
        _logger.LogDebug($"[SpecRes] DAILY: earn={earning:F1} ({ownedTownCount} towns) upkeep={upkeep:F1} net={net:+0.0;-0.0} | {before:F0}→{after:F0}");
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

        var added = cost.UpgradeCost * count;
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

    public void CommitSession(string heroId, string kingdomId, string cultureId)
    {
        if (!_inSession) return;

        if (_pendingSpend > 0f)
        {
            var resource = ResolveResource(kingdomId, cultureId);
            if (resource != null)
            {
                _storage.Add(heroId, resource.Id, -_pendingSpend);
                _logger.LogInfo($"[SpecRes] PartyScreen COMMITTED: -{_pendingSpend:F0} {resource.DisplayName}");
            }
        }
        else
        {
            _logger.LogDebug("[SpecRes] PartyScreen COMMITTED: no pending spend");
        }

        _pendingSpend = 0f;
        _inSession = false;
    }

    public void CancelSession()
    {
        var wasPending = _pendingSpend;
        _pendingSpend = 0f;
        _inSession = false;
        _logger.LogDebug($"[SpecRes] PartyScreen CANCELLED: discarded {wasPending:F0} pending spend");
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

    public float GetDailyEarning(string kingdomId, string cultureId, int ownedTownCount)
    {
        var resource = ResolveResource(kingdomId, cultureId);
        if (resource == null) return 0f;

        return resource.DailyPerTown * ownedTownCount;
    }

    public float GetDailyUpkeep(IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep, string heroId = null)
    {
        var total = 0f;
        if (troopsWithUpkeep == null) return total;

        foreach (var troop in troopsWithUpkeep)
        {
            var cost = _config.GetTroopCost(troop.TroopId);
            if (cost != null)
                total += cost.DailyUpkeep * troop.Count;
        }

        var upkeepModifier = GetPassiveMagnitude(heroId, PassiveEffectType.CustomResourceUpkeepModifier);
        if (upkeepModifier != 0f)
            total *= (1f + upkeepModifier);

        return Math.Max(0f, total);
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

        // At 0 resources: 10% of each upkeep troop type deserts per day (min 1)
        foreach (var troop in troopsWithUpkeep)
        {
            var desertCount = Math.Max(1, (int)(troop.Count * 0.1f));
            desertCount = Math.Min(desertCount, troop.Count);
            result.Add(new TroopDesertionEntry(troop.TroopId, desertCount));
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

    private void AddCapped(string heroId, SpecialResource resource, float amount)
    {
        var current = _storage.Get(heroId, resource.Id);
        var newAmount = Math.Min(current + amount, resource.Cap);
        _storage.Set(heroId, resource.Id, newAmount);
    }

    private float GetEffectiveUpgradeCost(string heroId, float baseCostPerUnit, int count)
    {
        var totalCost = baseCostPerUnit * count;
        var costModifier = GetPassiveMagnitude(heroId, PassiveEffectType.CustomResourceUpgradeCostModifier);
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
