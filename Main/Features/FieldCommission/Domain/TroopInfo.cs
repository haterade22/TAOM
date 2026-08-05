namespace TAOM.Features.FieldCommission.Domain;

/// <summary>Pure snapshot of a troop template — everything <c>FieldCommissionMeritService</c> and
/// <c>CommissionSkillBudget</c> need, with no TaleWorlds types crossing the boundary (ADR-007).</summary>
public readonly struct TroopInfo
{
    public TroopInfo(string stringId, string name, bool isHero, bool isPrisonGuard, int raceId, int level)
    {
        StringId = stringId;
        Name = name;
        IsHero = isHero;
        IsPrisonGuard = isPrisonGuard;
        RaceId = raceId;
        Level = level;
    }

    public string StringId { get; }
    public string Name { get; }
    public bool IsHero { get; }
    public bool IsPrisonGuard { get; }
    public int RaceId { get; }
    public int Level { get; }

    /// <summary>Default instance for "troop id did not resolve" — StringId is null.</summary>
    public static TroopInfo Missing => new TroopInfo(null, null, false, false, -1, 0);

    public bool IsMissing => StringId == null;
}
