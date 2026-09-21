namespace TAOM.Features.CultureConversion.GarrisonSwap.Domain;

/// <summary>
/// One member-roster row crossing the adapter boundary, for a settlement garrison or a militia
/// party. Mirrors <c>DesertionTroopInfo</c> (the other roster-snapshot DTO in this repo) and carries
/// the two extra fields a culture swap needs that desertion does not: the troop's
/// <see cref="Tier"/> and its <see cref="Role"/>, which together address a cell in a
/// <see cref="CultureTroopIndex"/>.
///
/// <see cref="Count"/> is the element's total <c>Number</c> including wounded, matching the engine's
/// <c>TroopRosterElement.Number</c>; <see cref="WoundedCount"/> is the <c>WoundedNumber</c> subset,
/// carried so a swap can rebuild the stack with the same wounded proportion instead of silently
/// healing a besieged garrison.
/// </summary>
public sealed class GarrisonTroopInfo
{
    public string TroopId { get; }

    /// <summary>The troop template's own culture id. Null when the template has no culture.</summary>
    public string? CultureId { get; }

    /// <summary>Engine tier (<c>CharacterObject.Tier</c>), which on TAOM runs 0..10.</summary>
    public int Tier { get; }

    public TroopRole Role { get; }

    /// <summary>Total bodies in the stack, wounded included.</summary>
    public int Count { get; }

    /// <summary>How many of <see cref="Count"/> are wounded.</summary>
    public int WoundedCount { get; }

    public bool IsHero { get; }

    public GarrisonTroopInfo(string troopId, string? cultureId, int tier, TroopRole role, int count, int woundedCount, bool isHero)
    {
        TroopId = troopId;
        CultureId = cultureId;
        Tier = tier;
        Role = role;
        Count = count;
        WoundedCount = woundedCount;
        IsHero = isHero;
    }
}
