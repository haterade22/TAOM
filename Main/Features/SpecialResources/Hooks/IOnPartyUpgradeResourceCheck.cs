namespace TAOM.Features.SpecialResources.Hooks;

public interface IOnPartyUpgradeResourceCheck
{
    bool CanAffordUpgrade(string heroId, string kingdomId, string troopId, int count);
    void QueueUpgradeSpend(string heroId, string troopId, int count);
    int ClampUpgradeCount(string heroId, string kingdomId, string troopId, int requestedCount);
    int GetUpgradeCost(string troopId);
    float GetAvailableAmount(string heroId, string kingdomId);
    string GetResourceDisplayName(string kingdomId);
    void BeginSession();
    void CommitSession(string heroId, string kingdomId);
    void CancelSession();
}
