using System.Collections.Generic;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

public interface ISpecialResourceService
{
    SpecialResource ResolveResource(string kingdomId, string cultureId);
    float GetCurrentAmount(string heroId, string kingdomId, string cultureId);
    void EarnFromBattle(string heroId, string kingdomId, string cultureId, float enemySizeRatio);
    void EarnFromRaid(string heroId, string kingdomId, string cultureId);
    void EarnFromSiege(string heroId, string kingdomId, string cultureId);
    void EarnFromPrisoners(string heroId, string kingdomId, string cultureId, int prisonerCount);
    void EarnFromTournament(string heroId, string kingdomId, string cultureId);
    void EarnFromHideout(string heroId, string kingdomId, string cultureId);
    void ApplyDailyTick(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep);
    bool CanAffordUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count);
    void SpendForUpgrade(string heroId, string kingdomId, string cultureId, string troopId, int count);
    void BeginPartyScreenSession();
    void QueueUpgradeSpend(string heroId, string troopId, int count);
    float GetAvailableAfterPending(string heroId, string kingdomId, string cultureId);
    int ClampUpgradeCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount);
    void CommitSession(string heroId, string kingdomId, string cultureId);
    void CancelSession();
    void InitializeHero(string heroId, string kingdomId, string cultureId);
    float GetDailyEarning(string kingdomId, string cultureId, int ownedTownCount);
    float GetDailyUpkeep(IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep, string heroId = null);
    IReadOnlyList<TroopDesertionEntry> CalculateDesertion(string heroId, string kingdomId, string cultureId, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep);

    /// <summary>
    /// Returns the highest <see cref="ResourceTier"/> the hero has reached,
    /// or <c>null</c> if the resource has no tiers or the hero is below all thresholds.
    /// </summary>
    ResourceTier GetCurrentTier(string heroId, string kingdomId, string cultureId);

    /// <summary>
    /// Returns the current tier level (1-N), or 0 if below all thresholds or no tiers defined.
    /// </summary>
    int GetCurrentTierLevel(string heroId, string kingdomId, string cultureId);
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

public sealed class TroopDesertionEntry
{
    public string TroopId { get; }
    public int DesertCount { get; }

    public TroopDesertionEntry(string troopId, int desertCount)
    {
        TroopId = troopId;
        DesertCount = desertCount;
    }
}
