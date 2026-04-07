namespace TAOM.Features.SpecialResources.Hooks;

public interface IOnPartyUpgradeResourceCheck
{
    bool CanAffordUpgrade(string heroId, string troopId, int count);
    void QueueUpgradeSpend(string heroId, string troopId, int count);
    int ClampUpgradeCount(string heroId, string troopId, int requestedCount);
    int GetUpgradeCost(string troopId);
    float GetAvailableAmount(string heroId);
    string GetResourceDisplayName(string kingdomId);
    void BeginSession();
    void CommitSession(string heroId);
    void CancelSession();
}
