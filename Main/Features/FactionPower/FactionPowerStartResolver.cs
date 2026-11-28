using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FactionPower;

/// <summary>
/// Turns what the start screen wrote (the preset item and the sliders the player moved) into the percents to store. Pure.
/// Standard, an absent or an unknown item writes nothing. A side preset writes 150 for that side's realms. Custom writes the
/// moved sliders of catalog realms: a slider outside 50 to 200 is skipped with a warning (never clamped), and one at 100 is
/// not stored because 100 is the absent state.
/// </summary>
public static class FactionPowerStartResolver
{
    public static IReadOnlyDictionary<string, int> Resolve(
        string? presetItem, IReadOnlyDictionary<string, int> sliders, IReadOnlyList<FactionPowerEntry> catalog, Action<string>? warn)
    {
        switch (presetItem)
        {
            case null:
            case FactionPowerScreenKeys.StandardItem:
                return new Dictionary<string, int>();
            case FactionPowerScreenKeys.EvilItem:
                return FactionPowerPresets.Resolve(FactionPowerPresets.EvilPlus50, catalog, Array.Empty<string>())!;
            case FactionPowerScreenKeys.FreeItem:
                return FactionPowerPresets.Resolve(FactionPowerPresets.FreePlus50, catalog, Array.Empty<string>())!;
            case FactionPowerScreenKeys.CustomItem:
                return Custom(sliders, catalog, warn);
            default:
                warn?.Invoke($"FactionPower: unknown start preset '{presetItem}'; writing nothing");
                return new Dictionary<string, int>();
        }
    }

    private static Dictionary<string, int> Custom(
        IReadOnlyDictionary<string, int> sliders, IReadOnlyList<FactionPowerEntry> catalog, Action<string>? warn)
    {
        var result = new Dictionary<string, int>();
        foreach (var entry in catalog.OrderBy(e => e.CultureId, StringComparer.Ordinal))
        {
            if (!sliders.TryGetValue(entry.CultureId, out var percent)) continue;
            if (!FactionPowerMath.IsValidPercent(percent))
            {
                warn?.Invoke($"FactionPower: start slider {percent} for '{entry.CultureId}' is outside {FactionPowerMath.MinPercent} to {FactionPowerMath.MaxPercent}; using 100");
                continue;
            }
            if (percent != FactionPowerMath.NeutralPercent) result[entry.CultureId] = percent;
        }
        return result;
    }
}
