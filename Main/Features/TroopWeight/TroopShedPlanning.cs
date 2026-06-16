namespace TAOM.Features.TroopWeight;

/// <summary>
/// Engine-free description of one member-roster element for shed planning. Carries everything the
/// pure <see cref="ITroopWeightService.PlanShed"/> planner needs so it never touches a sealed
/// TaleWorlds type. <see cref="Count"/> is the element's total <c>Number</c> (incl. wounded), to
/// match <see cref="ITroopWeightService.CalculateWeightedMemberCount"/> / the patched
/// <c>NumberOfAllMembers</c> the party-size limit is compared against.
/// </summary>
public readonly struct WeightedTroopEntry
{
    public readonly string TroopId;
    public readonly int Tier;
    public readonly float Weight;
    public readonly int Count;
    public readonly bool IsHero;

    public WeightedTroopEntry(string troopId, int tier, float weight, int count, bool isHero)
    {
        TroopId = troopId;
        Tier = tier;
        Weight = weight;
        Count = count;
        IsHero = isHero;
    }
}

/// <summary>
/// "Remove <see cref="Count"/> of <see cref="TroopId"/> from the member roster." The boundary maps
/// <see cref="TroopId"/> back to its <c>CharacterObject</c> (roster elements are unique per character)
/// and applies it via <c>MemberRoster.AddToCounts(character, -Count)</c>.
/// </summary>
public readonly struct ShedInstruction
{
    public readonly string TroopId;
    public readonly int Count;

    public ShedInstruction(string troopId, int count)
    {
        TroopId = troopId;
        Count = count;
    }
}
