using TAOM.Features.SpecialResources.Domain;

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

    public bool CanAffordUpgrade(string heroId, string troopId, int count)
        => _service.CanAffordUpgrade(heroId, troopId, count);

    public void SpendForUpgrade(string heroId, string troopId, int count)
        => _service.SpendForUpgrade(heroId, troopId, count);

    public int GetUpgradeCost(string troopId)
    {
        var cost = _config.GetTroopCost(troopId);
        return cost?.UpgradeCost ?? 0;
    }

    public float GetCurrentAmount(string heroId)
        => _service.GetCurrentAmount(heroId);

    public string GetResourceDisplayName(string kingdomId)
    {
        var resource = _config.GetByKingdomId(kingdomId);
        return resource?.DisplayName;
    }
}
