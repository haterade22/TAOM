using System;
using System.Collections.Generic;

namespace TAOM.Features.CrownGuard;

/// <summary>
/// The validated guard map: a lower-case culture id to the id of the troop that stands guard beside a new ruler of
/// that culture. Vanilla's guard lookup knows only its own culture ids and falls back to a Sturgian fighter, so a TAOM kingdom would crown its ruler beside the wrong people; this answers from the table instead. The built-in <see cref="Default"/> equals the shipped <c>crown_guard/crown_guard_cultures.json</c>.
/// </summary>
public sealed class CrownGuardMap
{
    public CrownGuardMap(IReadOnlyDictionary<string, string> cultures)
    {
        Cultures = cultures;
    }

    public IReadOnlyDictionary<string, string> Cultures { get; }

    public string? GuardTroopId(string? cultureId)
    {
        var key = NormalizeKey(cultureId);
        return key != null && Cultures.TryGetValue(key, out var troopId) ? troopId : null;
    }

    /// <summary>Trimmed, lower-case, or null when empty: the form every culture key is stored and looked up in.</summary>
    internal static string? NormalizeKey(string? raw)
    {
        var trimmed = raw?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static CrownGuardMap Default()
    {
        var cultures = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["gondor"] = "gondor_mt_fountain_guard",
            ["vlandia"] = "rohan_edoras_golden_hall_kings_own_rider",
            ["isengard"] = "orthanc_bodyguard",
            ["arthedain"] = "arthedain_kings_guard",
            ["cardolan"] = "cardolan_warden",
            ["rhudaur"] = "rhudaur_lord_of_the_angle",
            ["sturgia"] = "dale_running_river_warden",
            ["empire"] = "dunland_wolf_champion",
            ["erebor"] = "erebor_noble_royal_warden",
            ["eredluin"] = "eredluin_kings_guard",
            ["anduinvale"] = "anduinvale_bear_warden",
            ["angaladh"] = "angaladh_woodmen_guard",
            ["mirkwood"] = "mirkwood_palaceguard",
            ["rivendell"] = "imladris_warden",
            ["lothlorien"] = "imladris_warden",
            ["lindon"] = "lindon_imladris_warden",
            ["mordor"] = "mordor_uruk_baraddurguard",
            ["dolguldur"] = "dg_uruk_black_guard",
            ["gundabad"] = "gundabad_chosen_of_tharzog",
            ["goblin"] = "goblin_chosen_of_tharzog",
            ["bluecraig"] = "bluecraig_bolgs_ironfang",
            ["mistymountainorcs"] = "mistymountainorcs_bolgs_ironfang",
            ["khuzait"] = "dragon_wrath_obsidian_shieldmaster",
            ["battania"] = "dragon_wrath_ash_shieldguard",
            ["aserai"] = "harad_serp_guard",
            ["abanissa"] = "harad_serp_guard",
            ["shaghana"] = "harad_redserp_guard",
            ["umbar"] = "umbar_elite_root0000",
        };
        return new CrownGuardMap(cultures);
    }
}
