using System;
using System.Collections.Generic;

namespace TAOM.Features.HallOfFire;

/// <summary>
/// The validated Hall of Fire map: a lower-case culture id to the vanilla culture whose conversation backdrops
/// (<c>conv_&lt;set&gt;_...</c>) it borrows. Only the six <see cref="AllowedSets"/> ids have such atmospheres in
/// Native. The built-in <see cref="Default"/> equals the shipped <c>hall_of_fire/hall_of_fire.json</c>.
/// </summary>
public sealed class HallOfFireMap
{
    public static readonly IReadOnlyList<string> AllowedSets = new[]
    {
        "empire", "vlandia", "sturgia", "aserai", "khuzait", "battania",
    };

    public HallOfFireMap(IReadOnlyDictionary<string, string> cultures)
    {
        Cultures = cultures;
    }

    public IReadOnlyDictionary<string, string> Cultures { get; }

    /// <summary>Trimmed, lower-case, or null when empty: the form every key and value is stored and looked up in.</summary>
    internal static string? NormalizeKey(string? raw)
    {
        var trimmed = raw?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static HallOfFireMap Default()
    {
        var cultures = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(cultures, "empire", "gondor", "arthedain", "cardolan", "rhudaur", "rivendell", "lindon");
        Add(cultures, "battania", "mirkwood", "lothlorien", "dolguldur", "empire");
        Add(cultures, "sturgia", "erebor", "eredluin", "anduinvale", "angaladh", "dorwinion", "gundabad", "mistymountainorcs", "goblin", "bluecraig");
        Add(cultures, "aserai", "umbar", "shaghana", "abanissa");
        Add(cultures, "khuzait", "mordor", "battania");
        Add(cultures, "vlandia", "isengard", "southrhovanion");
        return new HallOfFireMap(cultures);
    }

    private static void Add(Dictionary<string, string> target, string set, params string[] cultureIds)
    {
        foreach (var id in cultureIds)
            target[id] = set;
    }
}
