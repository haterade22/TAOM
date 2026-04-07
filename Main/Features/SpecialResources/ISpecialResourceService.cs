using System.Collections.Generic;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

public interface ISpecialResourceService
{
    SpecialResource GetResourceForKingdom(string kingdomId);
    float GetCurrentAmount(string heroId);
    void EarnFromBattle(string heroId, string kingdomId, float enemySizeRatio);
    void EarnFromRaid(string heroId, string kingdomId);
    void EarnFromSiege(string heroId, string kingdomId);
    void EarnFromPrisoners(string heroId, string kingdomId, int prisonerCount);
    void EarnFromTournament(string heroId, string kingdomId);
    void EarnFromHideout(string heroId, string kingdomId);
    void ApplyDailyTick(string heroId, string kingdomId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep);
    bool CanAffordUpgrade(string heroId, string troopId, int count);
    void SpendForUpgrade(string heroId, string troopId, int count);
    void BeginPartyScreenSession();
    void QueueUpgradeSpend(string heroId, string troopId, int count);
    float GetAvailableAfterPending(string heroId);
    int ClampUpgradeCount(string heroId, string troopId, int requestedCount);
    void CommitSession(string heroId);
    void CancelSession();
    float GetDailyEarning(string kingdomId, int ownedTownCount);
    float GetDailyUpkeep(IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep);
}

public sealed class TroopUpkeepInfo
{
    public string TroopId { get; }
    public int Count { get; }

    public TroopUpkeepInfo(string troopId, int count)
    {
        TroopId = troopId;
        Count = count;
    }
}
