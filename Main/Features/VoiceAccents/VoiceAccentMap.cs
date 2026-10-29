using System;
using System.Collections.Generic;

namespace TAOM.Features.VoiceAccents;

/// <summary>
/// The validated voice map: a lower-case culture id to an accent class (or <see cref="Silent"/>),
/// and a lower-case race name to an accent class (or <see cref="Silent"/>), which wins over the culture row. The built-in
/// <see cref="Default"/> equals the shipped <c>voice_accents/voice_accents.json</c>.
/// </summary>
public sealed class VoiceAccentMap
{
    public const string Silent = "silent";
    public const string Imperial = "imperial";

    /// <summary>The accent values a culture or race row may carry, besides <see cref="Silent"/>.</summary>
    public static readonly IReadOnlyList<string> Accents = new[]
    {
        Imperial, "vlandian", "sturgian", "khuzait", "aserai", "battanian", "forest_bandits",
        "sea_raiders", "mountain_bandits", "desert_bandits", "steppe_bandits", "looters",
    };

    public VoiceAccentMap(IReadOnlyDictionary<string, string> cultures, IReadOnlyDictionary<string, string> races)
    {
        Cultures = cultures;
        Races = races;
    }

    public IReadOnlyDictionary<string, string> Cultures { get; }

    public IReadOnlyDictionary<string, string> Races { get; }

    /// <summary>Trimmed, lower-case, or null when empty: the form every key and value is stored and looked up in.</summary>
    internal static string? Normalize(string? raw)
    {
        var trimmed = raw?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static bool IsAllowedValue(string value) =>
        value == Silent || ((IList<string>)Accents).Contains(value);

    public static VoiceAccentMap Default()
    {
        var cultures = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(cultures, Imperial, "gondor", "gondor_soldiers", "arthedain", "cardolan", "rhudaur", "neutral_culture", "rivendell", "lothlorien",
            "mirkwood", "lindon", "erebor", "erebor_warriors", "eredluin", "anduinvale", "angaladh", "umbar", "abanissa", "shaghana", "darshi");
        Add(cultures, "khuzait", "battania");
        Add(cultures, "sturgian", "nord", "vakken");
        Add(cultures, Silent, "mordor", "isengard", "gundabad", "dolguldur", "goblin", "bluecraig", "mistymountainorcs",
            "gundabad_raiders", "feral_orcs", "wild_trolls", "mirkwood_spiders");
        Add(cultures, "looters", "dunland_raiders", "mirkwood_stalkers");
        Add(cultures, "steppe_bandits", "rhun_raiders");
        Add(cultures, "desert_bandits", "harad_raiders");
        Add(cultures, "sea_raiders", "umbar_corsairs");
        var races = new Dictionary<string, string>(StringComparer.Ordinal);
        Add(races, Silent, "nazghul", "cave_troll", "hill_troll", "sauron");
        Add(races, Imperial, "saruman");
        return new VoiceAccentMap(cultures, races);
    }

    private static void Add(Dictionary<string, string> target, string accent, params string[] cultureIds)
    {
        foreach (var id in cultureIds)
            target[id] = accent;
    }
}
