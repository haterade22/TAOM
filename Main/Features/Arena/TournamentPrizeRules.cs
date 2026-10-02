using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.Arena;

/// <summary>The two prize lists the engine asks for: fewer than 4 heroes draw Regular, 4 or more Elite.</summary>
public enum PrizeBand
{
    Regular,
    Elite,
}

/// <summary>
/// Which items a tournament may award (docs/features/arena.md "Prize pools"). Every prize is light, medium or
/// heavy and below on the armour ladder (Mike, 2026-10-02): a small tournament awards light, medium and
/// civilian kit, a big one heavy. Elite, lord and named kit never, nor anything its XML keeps off the market
/// (the troll gear). Weapons, shields and harness carry no table class and are judged by engine tier, the
/// split the armour gate's own fallback makes (Tier4 heavy, Tier5 and Tier6 elite).
/// </summary>
public static class TournamentPrizeRules
{
    /// <summary>
    /// The regular band's junk floor. Engine Tier is Round(Tierf) - 1, so this drops Tier1 and the lower half
    /// of Tier2 (Tierf 1.5 to 2).
    /// </summary>
    internal const float RegularMinTierf = 2f;

    /// <summary>The table's class when it has one, else the class the engine tier implies.</summary>
    public static ArmourClass PrizeClass(ArmourClass? tableClass, int engineTierIndex) =>
        tableClass ?? ArmourClassRules.FromEngineTier(engineTierIndex);

    /// <summary>
    /// The item's <c>is_merchandise</c> as loaded: the armour gate's record keeps it from before the gate
    /// flipped heavy and above to NotMerchandise. Without a record (a failed gate init) the live flag decides.
    /// </summary>
    public static bool XmlMerchandise(bool? recorded, bool liveNotMerchandise) =>
        recorded ?? !liveNotMerchandise;

    /// <param name="xmlMerchandise">From <see cref="XmlMerchandise"/>: a gated heavy piece is still a prize,
    /// an XML non-merchandise item never is.</param>
    public static bool Fits(PrizeBand band, ArmourClass cls, float tierf, bool xmlMerchandise)
    {
        if (!xmlMerchandise)
            return false;
        return band == PrizeBand.Elite
            ? cls == ArmourClass.Heavy
            : (cls == ArmourClass.Light || cls == ArmourClass.Medium || cls == ArmourClass.Civilian)
              && tierf >= RegularMinTierf;
    }

    /// <summary>
    /// The town culture's share of the fitting items, in order, or every fitting item when that share is
    /// empty or the town has no culture: the engine's prize roll indexes the list unguarded
    /// (FightTournamentGame.GetTournamentPrize), so it must never be empty while anything fits.
    /// </summary>
    public static List<T> PreferCulture<T>(IReadOnlyList<T> fitting, string? cultureId, Func<T, string?> cultureOf)
    {
        if (!string.IsNullOrEmpty(cultureId))
        {
            var own = fitting.Where(item => cultureOf(item) == cultureId).ToList();
            if (own.Count > 0)
                return own;
        }
        return fitting.ToList();
    }
}
