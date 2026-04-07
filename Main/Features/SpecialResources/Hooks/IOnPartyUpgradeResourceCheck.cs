namespace TAOM.Features.SpecialResources.Hooks;

public interface IOnPartyUpgradeResourceCheck
{
    bool CanAffordUpgrade(string heroId, string troopId, int count);
    void SpendForUpgrade(string heroId, string troopId, int count);
    int GetUpgradeCost(string troopId);
    float GetCurrentAmount(string heroId);
    string GetResourceDisplayName(string kingdomId);
}
