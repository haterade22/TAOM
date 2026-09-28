using System;

namespace TAOM.Features.ArmourAcquisition.Domain;

/// <summary>
/// Where a player may get a piece (docs/features/armour-acquisition.md). Light and medium pieces are
/// sold and looted freely; heavy, elite and lord pieces are sold only by a town whose armoury allows
/// them, and lord pieces can also be forged or earned; named hero kit and named weapons never change
/// hands. Civilian kit is off the combat ladder and stays free.
/// </summary>
public enum ArmourClass
{
    Light,
    Medium,
    Heavy,
    Elite,
    Lord,
    Civilian,
    Named,
}

/// <summary>The rules every consumer of <see cref="ArmourClass"/> shares.</summary>
public static class ArmourClassRules
{
    /// <summary>Parses the table's lowercase class ids ("light" ... "named"); anything else is refused.</summary>
    public static bool TryParse(string? raw, out ArmourClass cls)
    {
        switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "light": cls = ArmourClass.Light; return true;
            case "medium": cls = ArmourClass.Medium; return true;
            case "heavy": cls = ArmourClass.Heavy; return true;
            case "elite": cls = ArmourClass.Elite; return true;
            case "lord": cls = ArmourClass.Lord; return true;
            case "civilian": cls = ArmourClass.Civilian; return true;
            case "named": cls = ArmourClass.Named; return true;
            default: cls = ArmourClass.Light; return false;
        }
    }

    public static string ToId(ArmourClass cls) => cls.ToString().ToLowerInvariant();

    /// <summary>Heavy, elite, lord and named pieces are kept out of markets and loot while gating is on.</summary>
    public static bool IsGated(ArmourClass cls) =>
        cls == ArmourClass.Heavy || cls == ArmourClass.Elite || cls == ArmourClass.Lord || cls == ArmourClass.Named;

    /// <summary>Position on the upgrade ladder, light 0 to lord 4; -1 for civilian and named kit.</summary>
    public static int Rank(ArmourClass cls) => cls switch
    {
        ArmourClass.Light => 0,
        ArmourClass.Medium => 1,
        ArmourClass.Heavy => 2,
        ArmourClass.Elite => 3,
        ArmourClass.Lord => 4,
        _ => -1,
    };

    /// <summary>
    /// The class of a character armour piece the table does not list (a vanilla item, or an Armory piece
    /// added since the table was generated), from its engine tier: the same split the engine's own
    /// <c>DefaultItemCategorySelector</c> makes (Tier1 garment and Tier2 light armour, Tier3 medium,
    /// Tier4 heavy, Tier5 and Tier6 ultra). <paramref name="tierIndex"/> is <c>(int)ItemObject.Tier</c>,
    /// Tier1 = 0 in v1.5.3.
    /// </summary>
    public static ArmourClass FromEngineTier(int tierIndex)
    {
        if (tierIndex <= 1) return ArmourClass.Light;
        if (tierIndex == 2) return ArmourClass.Medium;
        if (tierIndex == 3) return ArmourClass.Heavy;
        return ArmourClass.Elite;
    }

    public static bool IsUpgradeSource(ArmourClass cls) => Rank(cls) >= 0 && cls != ArmourClass.Lord;
}
