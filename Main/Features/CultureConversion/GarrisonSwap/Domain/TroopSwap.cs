using System.Collections.Generic;

namespace TAOM.Features.CultureConversion.GarrisonSwap.Domain;

/// <summary>
/// "Remove <see cref="Count"/> of <see cref="OldTroopId"/> and add the same number of
/// <see cref="NewTroopId"/>, of which <see cref="WoundedCount"/> are wounded." The boundary maps
/// both ids back to their <c>CharacterObject</c> and applies the pair against one roster.
///
/// Head count is preserved by construction: there is one number, used for both halves. A swap that
/// wanted to change the count would be a different feature (see the plan's out-of-scope list).
/// </summary>
public sealed class TroopSwap
{
    public string OldTroopId { get; }
    public string NewTroopId { get; }
    public int Count { get; }
    public int WoundedCount { get; }

    public TroopSwap(string oldTroopId, string newTroopId, int count, int woundedCount)
    {
        OldTroopId = oldTroopId;
        NewTroopId = newTroopId;
        Count = count;
        WoundedCount = woundedCount;
    }
}

/// <summary>
/// The mapper's verdict for one roster: the swaps to apply, plus the troop ids it could not place
/// in the target culture at all.
///
/// <see cref="UnmappedTroopIds"/> is not an error path — it is the fail-safe. TAOM's culture rosters
/// are deliberately uneven (measured 2026-09-20: Mirkwood has no troop at tiers 4, 5 or 6; Goblin
/// has no cavalry at any tier), so a cell with no reachable candidate is expected. Those stacks stay
/// exactly as they are and the service logs them. A gap in a culture's roster must never delete a
/// garrison, which is the same stance <c>ReplaceForeignNotables</c> takes when a culture has no
/// template for an occupation.
/// </summary>
public sealed class TroopSwapPlan
{
    public static readonly TroopSwapPlan Empty =
        new(new List<TroopSwap>(), new List<string>());

    public IReadOnlyList<TroopSwap> Swaps { get; }
    public IReadOnlyList<string> UnmappedTroopIds { get; }

    public TroopSwapPlan(IReadOnlyList<TroopSwap> swaps, IReadOnlyList<string> unmappedTroopIds)
    {
        Swaps = swaps;
        UnmappedTroopIds = unmappedTroopIds;
    }
}
