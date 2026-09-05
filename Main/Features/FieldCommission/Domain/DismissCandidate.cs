namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// One promoted companion as the dismissal flow sees it: the names the prompts render and the
/// verdict. Troop fields are null on a refusal. Pure data, no TaleWorlds types (ADR-007).
/// </summary>
public readonly struct DismissCandidate
{
    public DismissCandidate(string heroId, string heroName, string troopId, string troopName, DismissOutcome outcome)
    {
        HeroId = heroId;
        HeroName = heroName;
        TroopId = troopId;
        TroopName = troopName;
        Outcome = outcome;
    }

    public string HeroId { get; }
    public string HeroName { get; }

    /// <summary>The troop type the hero was promoted from; the soldier who comes back.</summary>
    public string TroopId { get; }
    public string TroopName { get; }

    public DismissOutcome Outcome { get; }

    public bool IsDismissable => Outcome == DismissOutcome.Ok;

    public static DismissCandidate Refused(string heroId, string heroName, DismissOutcome outcome) =>
        new DismissCandidate(heroId, heroName, null, null, outcome);
}
