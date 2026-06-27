using System.Collections.Generic;

namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Pure decision for which troops in a party/garrison desert because their culture's alignment is
/// opposed to the owner's kingdom alignment (Free vs Evil). Neutral on either side never deserts;
/// named heroes never desert. Reuses the kingdom-keyed <see cref="Execution.IAlignmentService"/>
/// (owner side via <c>GetKingdomSide</c>, troop side via <c>GetCultureSide</c>).
/// </summary>
public interface IAlignmentDesertionService
{
    /// <summary>Master toggle shortcut so the behavior can early-out before snapshotting rosters.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns the per-troop-type desertion for a single party or garrison. Empty when the feature or
    /// the relevant owner/location toggle is off, when the owner is Neutral/kingdomless, or when no
    /// troop is opposed.
    /// </summary>
    /// <param name="ownerKingdomId">Kingdom StringId of the party leader's clan, or the garrison settlement owner's clan.</param>
    /// <param name="isPlayerOwned">True when the owning clan is the player's clan.</param>
    /// <param name="isGarrison">True for a settlement garrison roster; false for a mobile party.</param>
    /// <param name="troops">Snapshot of the roster (regular + hero rows).</param>
    IReadOnlyList<TroopDesertionResult> CalculateDesertion(
        string ownerKingdomId, bool isPlayerOwned, bool isGarrison, IReadOnlyList<DesertionTroopInfo> troops);
}

/// <summary>One roster row supplied to the service: troop id, its culture, hero flag, and count.</summary>
public sealed class DesertionTroopInfo
{
    public string TroopId { get; }
    public string CultureId { get; }
    public bool IsHero { get; }
    public int Count { get; }

    public DesertionTroopInfo(string troopId, string cultureId, bool isHero, int count)
    {
        TroopId = troopId;
        CultureId = cultureId;
        IsHero = isHero;
        Count = count;
    }
}

/// <summary>One computed desertion: a troop id and how many to remove from the roster.</summary>
public sealed class TroopDesertionResult
{
    public string TroopId { get; }
    public int DesertCount { get; }

    public TroopDesertionResult(string troopId, int desertCount)
    {
        TroopId = troopId;
        DesertCount = desertCount;
    }
}
