namespace TAOM.Features.SpecialResources.Hooks;

public class PartyUpgradeResourceCheckHook : IOnPartyUpgradeResourceCheck
{
    private readonly ISpecialResourceService _service;
    private readonly ISpecialResourceConfigProvider _config;

    public PartyUpgradeResourceCheckHook(ISpecialResourceService service, ISpecialResourceConfigProvider config)
    {
        _service = service;
        _config = config;
    }

    public bool CanAffordUpgrade(string heroId, string kingdomId, string troopId, int count)
        => _service.CanAffordUpgrade(heroId, kingdomId, troopId, count);

    public void QueueUpgradeSpend(string heroId, string troopId, int count)
        => _service.QueueUpgradeSpend(heroId, troopId, count);

    public int ClampUpgradeCount(string heroId, string kingdomId, string troopId, int requestedCount)
        => _service.ClampUpgradeCount(heroId, kingdomId, troopId, requestedCount);

    public int GetUpgradeCost(string troopId)
    {
        var cost = _config.GetTroopCost(troopId);
        return cost?.UpgradeCost ?? 0;
    }

    public float GetAvailableAmount(string heroId, string kingdomId)
        => _service.GetAvailableAfterPending(heroId, kingdomId);

    public string GetResourceDisplayName(string kingdomId)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        return resource?.DisplayName;
    }

    public void BeginSession() => _service.BeginPartyScreenSession();
    public void CommitSession(string heroId, string kingdomId) => _service.CommitSession(heroId, kingdomId);
    public void CancelSession() => _service.CancelSession();
}
