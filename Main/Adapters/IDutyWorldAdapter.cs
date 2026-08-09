namespace TAOM.Adapters;

/// <summary>
/// Engine boundary for the enlisted soldier's DAILY UPKEEP — food, morale, healing. Every
/// method that touches a sealed TaleWorlds type for that lives here so
/// <c>EnlistmentDailyService</c> stays a plain testable service (ADR-007).
///
/// Named for the field-duty system it was born in; that system no longer uses it at all.
/// A rename is owed and deliberately not done here — it would touch IoC.cs, a single-owner
/// convergence file, in the same change as a behavioural rework.
/// All methods fail soft (null/0/false + a logged warning) — a duty world-read failing
/// must never throw into the daily/hourly campaign tick.
/// </summary>
public interface IDutyWorldAdapter
{
    /// <summary>Total food count in the player's main party roster (<c>ItemRoster.TotalFood</c>).</summary>
    int CountPlayerFood();

    /// <summary>Grants food (as grain) to the player's main party. No-op for amount &lt;= 0.</summary>
    void GrantPlayerFood(int amount);

    /// <summary>
    /// Raise the player's party morale to <paramref name="floor"/> if it is below. Never lowers it,
    /// and never touches a party that is already content. Returns true when it actually raised.
    /// </summary>
    bool RaisePlayerMoraleTo(float floor);

    /// <summary>
    /// Heal the player hero by <paramref name="hitPoints"/>. Returns false when there is no hero
    /// or nothing to heal. Clamped by the engine at max HP.
    /// </summary>
    bool HealPlayerHero(int hitPoints);
}
