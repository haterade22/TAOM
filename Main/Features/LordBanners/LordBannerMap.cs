using System;
using System.Collections.Generic;

namespace TAOM.Features.LordBanners;

/// <summary>
/// The validated banner map: a lower-case culture id to the vanilla banner cultures whose banner items its
/// lords may also receive. The built-in <see cref="Default"/> equals the shipped
/// <c>lord_banners/lord_banner_cultures.json</c>.
/// </summary>
public sealed class LordBannerMap
{
    public const string Neutral = "neutral_culture";

    /// <summary>The vanilla cultures that own banner items, and the only values a row may list.</summary>
    public static readonly IReadOnlyList<string> BannerCultures = new[]
    {
        "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania",
    };

    public LordBannerMap(IReadOnlyDictionary<string, IReadOnlyList<string>> cultures)
    {
        Cultures = cultures;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Cultures { get; }

    /// <summary>Trimmed, lower-case, or null when empty: the form every key and value is stored and looked up in.</summary>
    internal static string? Normalize(string? raw)
    {
        var trimmed = raw?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static bool IsBannerCulture(string value) => ((IList<string>)BannerCultures).Contains(value);

    public static LordBannerMap Default()
    {
        var cultures = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        Add(cultures, "empire", "gondor", "gondor_soldiers", "arthedain", "cardolan", "rhudaur");
        Add(cultures, "sturgia", "erebor", "erebor_warriors", "eredluin", "anduinvale", "angaladh", "dorwinion");
        Add(cultures, "vlandia", "southrhovanion");
        Add(cultures, "battania", "mirkwood", "mirkwood_stalkers", "dunland_raiders");
        Add(cultures, "aserai", "umbar", "umbar_corsairs", "abanissa", "shaghana", "harad_raiders");
        Add(cultures, "khuzait", "rhun_raiders");
        return new LordBannerMap(cultures);
    }

    private static void Add(Dictionary<string, IReadOnlyList<string>> target, string bannerCulture, params string[] cultureIds)
    {
        foreach (var id in cultureIds)
            target[id] = new[] { bannerCulture };
    }
}
