namespace TAOM.Features.SpecialResources;

/// <summary>A hero's balance of the special resource their kingdom (or culture) uses.</summary>
public sealed class SpecialResourceBalance
{
    public SpecialResourceBalance(string displayName, float amount)
    {
        DisplayName = displayName;
        Amount = amount;
    }

    public string DisplayName { get; }

    public float Amount { get; }
}

/// <summary>
/// A narrow, affordability-checked spend of a hero's special resource, for callers outside the troop
/// economy (the armour acquisition armoury forges lord kit for it). Implemented by
/// <see cref="SpecialResourceService"/>; a separate interface so those callers do not depend on the
/// troop-keyed <see cref="ISpecialResourceService"/>.
/// </summary>
public interface ISpecialResourceSpender
{
    /// <summary>The hero's resource resolved by kingdom, then culture; null when neither maps to one.</summary>
    SpecialResourceBalance? GetBalance(string heroId, string? kingdomId, string? cultureId);

    /// <summary>
    /// Debits <paramref name="amount"/> when the balance covers it and returns true; otherwise changes
    /// nothing and returns false (also for a non-finite or non-positive amount, or no resource).
    /// </summary>
    bool TrySpend(string heroId, string? kingdomId, string? cultureId, float amount);
}
