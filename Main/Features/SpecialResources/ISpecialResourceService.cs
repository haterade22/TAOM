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

    /// <summary>
    /// Deducts the one-time recruit cost (<c>recruit_cost</c>) for <paramref name="troopId"/> ×
    /// <paramref name="count"/> from the player's resolved resource. No-op when the troop has no
    /// recruit cost or the hero's kingdom/culture maps to no resource. Charged from
    /// <c>OnUnitRecruitedEvent</c> (player-only) for the elephant/spider volunteers; the
    /// RecruitmentVM gate guarantees affordability before this fires.
    /// </summary>
    void ChargeRecruitCost(string heroId, string kingdomId, string cultureId, string troopId, int count);

    /// <summary>
    /// Decides whether the recruit-volunteers cart can be confirmed: sums <c>recruit_cost × count</c>
    /// for cart entries that carry a recruit cost and compares to the player's current resolved-resource
    /// balance. Returns <see cref="RecruitGateResult.Allowed"/> when nothing in the cart costs a resource
    /// or the hero maps to no resource. Used by the RecruitmentVM Done-button gate.
    /// </summary>
    RecruitGateResult CanAffordRecruit(string heroId, string kingdomId, string cultureId, IReadOnlyList<RecruitCartEntry> cart);
    void BeginPartyScreenSession();
    void QueueUpgradeSpend(string heroId, string troopId, int count);
    float GetAvailableAfterPending(string heroId, string kingdomId, string cultureId);
    int ClampUpgradeCount(string heroId, string kingdomId, string cultureId, string troopId, int requestedCount);
    void CommitSession(string heroId, string kingdomId, string cultureId);
    void CancelSession();
    void InitializeHero(string heroId, string kingdomId, string cultureId);
    float GetDailyEarning(string kingdomId, string cultureId, int ownedTownCount);
    float GetDailyUpkeep(IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep, string heroId = null);

    /// <summary>
    /// Projected net resource change (earning − upkeep) for one daily tick, including career
    /// passive modifiers — same math as <see cref="ApplyDailyTick"/>. Returns 0 when the hero's
    /// kingdom/culture maps to no resource. Used to warn the player one tick before a deficit
    /// (and the troop desertion it triggers).
    /// </summary>
    float GetProjectedDailyNet(string heroId, string kingdomId, string cultureId, int ownedTownCount, IReadOnlyList<TroopUpkeepInfo> troopsWithUpkeep);
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

    /// <summary>
    /// Clears singleton-scope session state that must not leak across new campaigns started in the
    /// same process: pending PartyScreen spend, in-session flag, and the dedupe set for resolve
    /// debug logging. Wired to <c>OnNewGameCreatedEvent</c> by the behavior. Phase 9b deferred #133 P2 R1.
    /// </summary>
    void ResetSessionState();
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

/// <summary>One recruit-screen cart line: a troop id and how many copies of it are in the cart.</summary>
public sealed class RecruitCartEntry
{
    public string TroopId { get; }
    public int Count { get; }

    public RecruitCartEntry(string troopId, int count)
    {
        TroopId = troopId;
        Count = count;
    }
}

/// <summary>Outcome of <see cref="ISpecialResourceService.CanAffordRecruit"/> — whether to block the
/// Done button and, if so, the resource amount/name to surface in the hint.</summary>
public sealed class RecruitGateResult
{
    public bool Blocked { get; }
    public int Required { get; }
    public string ResourceDisplayName { get; }

    public static readonly RecruitGateResult Allowed = new(false, 0, null);

    public RecruitGateResult(bool blocked, int required, string resourceDisplayName)
    {
        Blocked = blocked;
        Required = required;
        ResourceDisplayName = resourceDisplayName;
    }
}
