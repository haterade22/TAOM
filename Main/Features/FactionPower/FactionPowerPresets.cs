using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FactionPower;

/// <summary>
/// The presets, as the values they write. A preset never stores its own id, so a later side change in factions.json leaves a
/// running campaign alone. Standard writes nothing; a side preset writes 150 for every catalog culture on that side and leaves
/// the rest; reset writes 100 to every key that is present.
/// </summary>
public static class FactionPowerPresets
{
    public const string Standard = "standard";
    public const string EvilPlus50 = "evil50";
    public const string FreePlus50 = "free50";
    public const string ResetAll = "reset";

    public const int SidePresetPercent = 150;

    public static readonly string[] Ids = { Standard, EvilPlus50, FreePlus50, ResetAll };

    public static IReadOnlyDictionary<string, int>? Resolve(
        string? presetId, IReadOnlyList<FactionPowerEntry> catalog, IEnumerable<string> presentKeys)
    {
        switch ((presetId ?? "").Trim().ToLowerInvariant())
        {
            case Standard:
                return new Dictionary<string, int>();
            case EvilPlus50:
                return SideAt(catalog, FactionPowerCatalog.Evil);
            case FreePlus50:
                return SideAt(catalog, FactionPowerCatalog.Free);
            case ResetAll:
                return presentKeys.Distinct(StringComparer.Ordinal).ToDictionary(k => k, _ => FactionPowerMath.NeutralPercent);
            default:
                return null;
        }
    }

    private static IReadOnlyDictionary<string, int> SideAt(IReadOnlyList<FactionPowerEntry> catalog, string side)
        => catalog.Where(e => e.Side == side).ToDictionary(e => e.CultureId, _ => SidePresetPercent);
}
