using System;
using System.Collections.Generic;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>
/// The closed set of tactics a doctrine may register. The first nine are the engine's field
/// tactics (the id is the type name without its <c>Tactic</c> prefix); the rest are TAOM's own.
/// Growing the set is one enum member, its class, and one <c>TacticFactory</c> case.
/// </summary>
public enum DoctrineTactic
{
    Charge,
    FullScaleAttack,
    DefensiveEngagement,
    DefensiveLine,
    DefensiveRing,
    FrontalCavalryCharge,
    RangedHarrassmentOffensive,
    HoldChokePoint,
    CoordinatedRetreat,
    ShieldWall,
    InfantryMass,
    CavalryDominance,
    ArcherRing,
}

/// <summary>Which battle side an entry applies to. <c>Any</c> is the JSON default.</summary>
public enum DoctrineSide
{
    Any,
    Attacker,
    Defender,
}

public static class DoctrineTacticIds
{
    private static readonly DoctrineTactic[] AllTactics = (DoctrineTactic[])Enum.GetValues(typeof(DoctrineTactic));

    private static readonly HashSet<DoctrineTactic> VanillaTactics = new HashSet<DoctrineTactic>
    {
        DoctrineTactic.Charge, DoctrineTactic.FullScaleAttack, DoctrineTactic.DefensiveEngagement,
        DoctrineTactic.DefensiveLine, DoctrineTactic.DefensiveRing, DoctrineTactic.FrontalCavalryCharge,
        DoctrineTactic.RangedHarrassmentOffensive, DoctrineTactic.HoldChokePoint, DoctrineTactic.CoordinatedRetreat,
    };

    public static IReadOnlyList<DoctrineTactic> All => AllTactics;

    public static bool IsVanilla(DoctrineTactic tactic) => VanillaTactics.Contains(tactic);

    /// <summary>Case-insensitive name parse. Rejects integers and the engine's <c>Tactic</c> prefix
    /// so a typo in the JSON is a warning, not a silently different tactic.</summary>
    public static bool TryParse(string? id, out DoctrineTactic tactic)
    {
        tactic = default;
        if (string.IsNullOrWhiteSpace(id) || !char.IsLetter(id![0]))
            return false;
        return Enum.TryParse(id.Trim(), ignoreCase: true, out tactic) && Enum.IsDefined(typeof(DoctrineTactic), tactic);
    }

    /// <summary>The engine type's simple name for a vanilla tactic, or the TAOM type's for ours.</summary>
    public static string EngineTypeName(DoctrineTactic tactic) =>
        (IsVanilla(tactic) ? "Tactic" : "TaomTactic") + tactic;

    public static bool TryParseSide(string? side, out DoctrineSide result)
    {
        result = DoctrineSide.Any;
        if (string.IsNullOrWhiteSpace(side))
            return true;
        return Enum.TryParse(side!.Trim(), ignoreCase: true, out result)
            && Enum.IsDefined(typeof(DoctrineSide), result)
            && char.IsLetter(side.Trim()[0]);
    }
}
