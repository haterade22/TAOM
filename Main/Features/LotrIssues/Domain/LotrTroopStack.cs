namespace TAOM.Features.LotrIssues.Domain;

/// <summary>
/// One stack of the retainer line in the main party, projected free of TaleWorlds types so the loan rules
/// stay unit-testable (ADR-007).
/// </summary>
public readonly struct LotrTroopStack
{
    public LotrTroopStack(string troopId, int tier, int number, bool isHero)
    {
        TroopId = troopId;
        Tier = tier;
        Number = number;
        IsHero = isHero;
    }

    public string TroopId { get; }

    /// <summary>The troop's tier; the highest tier goes home first.</summary>
    public int Tier { get; }

    /// <summary>Troops in this stack, wounded included.</summary>
    public int Number { get; }

    /// <summary>Heroes never go home with the loan.</summary>
    public bool IsHero { get; }
}
