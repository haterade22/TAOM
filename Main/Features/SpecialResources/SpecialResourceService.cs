using System;
using System.Collections.Generic;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

public class SpecialResourceService : ISpecialResourceService
{
    private readonly ISpecialResourceConfigProvider _config;
    private readonly ISpecialResourceStorageService _storage;
    private float _pendingSpend;
    private bool _inSession;

    public SpecialResourceService(ISpecialResourceConfigProvider config, ISpecialResourceStorageService storage)
    {
        _config = config;
        _storage = storage;
    }

    public SpecialResource GetResourceForKingdom(string kingdomId)
    {
        return _config.GetByKingdomId(kingdomId);
    }

    public float GetCurrentAmount(string heroId)
    {
        return _storage.Get(heroId);
    }

    public void EarnFromBattle(string heroId, string kingdomId, float enemySizeRatio)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return;

        var amount = resource.PerBattleVictoryBase * Math.Max(0.5f, Math.Min(2f, enemySizeRatio));
        AddCapped(heroId, amount, resource.Cap);
    }

    public void EarnFromRaid(string heroId, string kingdomId)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return;

        AddCapped(heroId, resource.PerRaid, resource.Cap);
    }

    public void EarnFromSiege(string heroId, string kingdomId)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return;

        AddCapped(heroId, resource.PerSiegeVictory, resource.Cap);
    }

    public void EarnFromPrisoners(string heroId, string kingdomId, int prisonerCount)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return;

        AddCapped(heroId, resource.PerPrisoner * prisonerCount, resource.Cap);
    }

    public void EarnFromTournament(string heroId, string kingdomId)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return;

        AddCapped(heroId, resource.PerTournamentWin, resource.Cap);
    }

    public void EarnFromHideout(string heroId, string kingdomId)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return;

        AddCapped(heroId, resource.PerHideoutClear, resource.Cap);
    }

    public void ApplyDailyTick(string heroId, string kingdomId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return;

        var earning = resource.DailyPerTown * ownedTownCount;
        var upkeep = GetDailyUpkeep(troopsWithUpkeep);
        var net = earning - upkeep;

        if (net >= 0)
            AddCapped(heroId, net, resource.Cap);
        else
            _storage.Add(heroId, net);
    }

    public bool CanAffordUpgrade(string heroId, string troopId, int count)
    {
        var cost = _config.GetTroopCost(troopId);
        if (cost == null) return true;

        var totalCost = cost.UpgradeCost * count;
        return _storage.Get(heroId) >= totalCost;
    }

    public void SpendForUpgrade(string heroId, string troopId, int count)
    {
        var cost = _config.GetTroopCost(troopId);
        if (cost == null) return;

        _storage.Add(heroId, -(cost.UpgradeCost * count));
    }

    public void BeginPartyScreenSession()
    {
        _pendingSpend = 0f;
        _inSession = true;
    }

    public void QueueUpgradeSpend(string heroId, string troopId, int count)
    {
        var cost = _config.GetTroopCost(troopId);
        if (cost == null) return;

        _pendingSpend += cost.UpgradeCost * count;
    }

    public float GetAvailableAfterPending(string heroId)
    {
        return _storage.Get(heroId) - _pendingSpend;
    }

    public int ClampUpgradeCount(string heroId, string troopId, int requestedCount)
    {
        var cost = _config.GetTroopCost(troopId);
        if (cost == null || cost.UpgradeCost <= 0) return requestedCount;

        var available = GetAvailableAfterPending(heroId);
        var maxAffordable = (int)(available / cost.UpgradeCost);
        return Math.Max(0, Math.Min(requestedCount, maxAffordable));
    }

    public void CommitSession(string heroId)
    {
        if (!_inSession) return;

        if (_pendingSpend > 0f)
            _storage.Add(heroId, -_pendingSpend);

        _pendingSpend = 0f;
        _inSession = false;
    }

    public void CancelSession()
    {
        _pendingSpend = 0f;
        _inSession = false;
    }

    public float GetDailyEarning(string kingdomId, int ownedTownCount)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return 0f;

        return resource.DailyPerTown * ownedTownCount;
    }

    public float GetDailyUpkeep(IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep)
    {
        var total = 0f;
        if (troopsWithUpkeep == null) return total;

        foreach (var troop in troopsWithUpkeep)
        {
            var cost = _config.GetTroopCost(troop.TroopId);
            if (cost != null)
                total += cost.DailyUpkeep * troop.Count;
        }

        return total;
    }

    private void AddCapped(string heroId, float amount, float cap)
    {
        var current = _storage.Get(heroId);
        var newAmount = Math.Min(current + amount, cap);
        _storage.Set(heroId, newAmount);
    }
}
