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

    public bool CanAffordUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count)
        => _service.CanAffordUpgrade(heroId, kingdomId, cultureId, troopId, count);

    public void QueueUpgradeSpend(string heroId, string troopId, int count)
        => _service.QueueUpgradeSpend(heroId, troopId, count);

    public int ClampUpgradeCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount)
        => _service.ClampUpgradeCount(heroId, kingdomId, cultureId, troopId, requestedCount);

    public int GetUpgradeCost(string troopId)
    {
        var cost = _config.GetTroopCost(troopId);
        return cost?.UpgradeCost ?? 0;
    }

    public float GetAvailableAmount(string heroId, string kingdomId, string cultureId)
        => _service.GetAvailableAfterPending(heroId, kingdomId, cultureId);

    public string GetResourceDisplayName(string kingdomId, string cultureId)
    {
        var resource = _service.ResolveResource(kingdomId, cultureId);
        return resource?.DisplayName;
    }

    public void BeginSession() => _service.BeginPartyScreenSession();
    public void CommitSession(string heroId, string kingdomId, string cultureId) => _service.CommitSession(heroId, kingdomId, cultureId);
    public void CancelSession() => _service.CancelSession();
}
