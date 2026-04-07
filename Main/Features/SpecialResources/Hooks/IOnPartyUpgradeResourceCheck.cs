namespace TAOM.Features.SpecialResources.Hooks;

public interface IOnPartyUpgradeResourceCheck
{
    bool CanAffordUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count);
    void QueueUpgradeSpend(string heroId, string troopId, int count);
    int ClampUpgradeCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount);
    int GetUpgradeCost(string troopId);
    float GetAvailableAmount(string heroId, string kingdomId, string cultureId);
    string GetResourceDisplayName(string kingdomId, string cultureId);
    void BeginSession();
    void CommitSession(string heroId, string kingdomId, string cultureId);
    void CancelSession();
}
