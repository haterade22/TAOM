using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.Arachnophobia;

/// <summary>Which warg replaces each spider mount (docs/features/arachnophobia-mode.md). The items live in the Armory.</summary>
public static class ArachnophobiaRules
{
    public const string HarnessItemId = "warg_saddle";

    private const string BrownWarg = "warg_brown";
    private const string DarkWarg = "warg_dark";
    private const string FellWarg = "fell_warg";

    // By tier: the common spiders, the great spiders, then the pale spider.
    private static readonly Dictionary<string, string> WargBySpiderItem = new(StringComparer.Ordinal)
    {
        ["spider_mount_a"] = BrownWarg,
        ["spider_mount_mountain_a1"] = BrownWarg,
        ["spider_mount_brown"] = DarkWarg,
        ["spider_mount_mountain_a2"] = DarkWarg,
        ["spider_mount_pale"] = FellWarg,
    };

    /// <summary>Every item the swap writes. If one is missing, the mode changes no troop; the daily brood disband still runs.</summary>
    public static IReadOnlyCollection<string> RequiredItemIds { get; } =
        WargBySpiderItem.Values.Concat(new[] { BrownWarg, HarnessItemId }).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>A spider item added later gets the brown warg, so no spider renders while the table catches up.</summary>
    public static string WargFor(string spiderItemId)
        => WargBySpiderItem.TryGetValue(spiderItemId, out string warg) ? warg : BrownWarg;
}
