using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>Who a troop or class boon changes (phase2-plan.md section 2, "Boons"): one of the defenders' three groups, or the lord's own body.</summary>
internal enum BoonTarget
{
    Infantry,
    Archers,
    Cavalry,
    Player,
}

internal static class BoonTargets
{
    public static IReadOnlyList<BoonTarget> All { get; } = new[] { BoonTarget.Infantry, BoonTarget.Archers, BoonTarget.Cavalry, BoonTarget.Player };

    /// <summary>The word a boons.json effect writes: <c>infantry</c>, <c>archers</c>, <c>cavalry</c> or <c>player</c>.</summary>
    public static bool TryParse(string? text, out BoonTarget target)
    {
        switch (text)
        {
            case "infantry":
                target = BoonTarget.Infantry;
                return true;
            case "archers":
                target = BoonTarget.Archers;
                return true;
            case "cavalry":
                target = BoonTarget.Cavalry;
                return true;
            case "player":
                target = BoonTarget.Player;
                return true;
            default:
                target = BoonTarget.Infantry;
                return false;
        }
    }

    /// <summary>
    /// A defender's group from its character: <c>IsMounted</c> is cavalry, else <c>IsRanged</c> is archers, else infantry (<c>BasicCharacterObject.cs:70-72</c>,
    /// set once from the XML <c>default_group</c>), so every formation class lands in one group and a rider bought to fight on foot is cavalry (assumption A8).
    /// </summary>
    public static BoonTarget OfCharacter(bool isMounted, bool isRanged) =>
        isMounted ? BoonTarget.Cavalry : isRanged ? BoonTarget.Archers : BoonTarget.Infantry;

    /// <summary>The troop category a group's boon rows are filed under; null for the player.</summary>
    public static BoonCategory? CategoryOf(BoonTarget target) => target switch
    {
        BoonTarget.Infantry => BoonCategory.Infantry,
        BoonTarget.Archers => BoonCategory.Archers,
        BoonTarget.Cavalry => BoonCategory.Cavalry,
        _ => null,
    };
}
