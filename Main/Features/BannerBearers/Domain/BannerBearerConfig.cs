using System.Collections.Generic;

namespace TAOM.Features.BannerBearers.Domain;

// User-editable config for banner_bearers/banner_bearers_config.json.
// Every field is an int/string/collection — no floats, so the FiniteFloatValidator
// NaN gate (csharp-architecture.md) has nothing to guard here.
public sealed class BannerBearerConfig
{
    public bool Enabled { get; set; } = true;

    // Formations smaller than this never raise a banner. Vanilla uses 2; TAOM defaults
    // higher so a 3-man remnant doesn't field a standard. MUST stay constant for a
    // mission — BannerBearerLogic.OnAgentAdded/OnAgentRemoved use exact-equality edge
    // detection (CountOfUnits == minimum), which a mid-mission change would silently break.
    public int MinimumFormationTroopCount { get; set; } = 4;

    // Hard ceiling of 6: the engine's arrangement tables are RelativeFormationPosition[6]
    // (BannerBearerLineFormationPositions et al). Beyond 6 the extra bearers keep their
    // existing slots — cosmetic degradation, not a crash — so we simply don't ask for more.
    public int MaxBearersPerFormation { get; set; } = 4;

    // "One banner per N soldiers", per formation class (the Raise-your-Banner knob).
    // 0 disables banners for that class entirely.
    public int InfantryBannerPerSoldiers { get; set; } = 20;
    public int RangedBannerPerSoldiers { get; set; } = 25;
    public int CavalryBannerPerSoldiers { get; set; } = 15;
    public int HorseArcherBannerPerSoldiers { get; set; } = 15;
    public int OtherBannerPerSoldiers { get; set; } = 25;

    // Races that never carry a standard. Beasts and named/unique races: a cave troll
    // hoisting a banner reads as absurd, and the named races are heroes anyway
    // (CanAgentBecomeBannerBearer already excludes !IsHero) — listed for belt-and-braces.
    public List<string> ExcludedRaces { get; set; } = new List<string>
    {
        "cave_troll",
        "hill_troll",
        "nazghul",
        "saruman",
        "sauron",
    };

    // Culture StringId -> banner ItemObject id. Vanilla's banner items are
    // culture="Culture.neutral_culture" and using_tableau="true", so the cloth renders the
    // party's own heraldry regardless of which mesh family we pick — the item choice is a
    // pole/cloth silhouette AND a BannerComponent effect tier, not a faction lock.
    //
    // KEYS ARE StringIds, NOT display names. TAOM re-skins the six vanilla cultures through
    // spcultures.xslt, which overrides <name> but NEVER the id — so Rohan's StringId really is
    // "vlandia", Dunland's is "empire", and so on. Keying on the LOTR name silently misses those
    // six factions (deep-review 2026-07-16 CRITICAL; see docs/reviews/rca-banner-bearers-2026-07-16.md).
    // ShippedBannerBearerConfigTests pins every key against the real culture set.
    public Dictionary<string, string> CultureBanners { get; set; } = new Dictionary<string, string>
    {
        // Men of the West — Numenorean/Roman standard silhouette
        { "gondor", "standard_of_duty_t1" },
        { "gondor_soldiers", "standard_of_duty_t1" },
        { "vlandia", "banner_of_the_horseman_t1" },      // Rohirrim — charge standard
        { "sturgia", "close_shields_banner_t1" },        // Barding (Dale)

        // Dwarves — heavy shield-wall standards
        { "erebor", "banner_of_oaken_shields_t1" },
        { "erebor_warriors", "banner_of_oaken_shields_t1" },

        // Elves — ranged/precision standards
        { "rivendell", "archers_flag_t1" },
        { "lothlorien", "archers_flag_t1" },
        { "mirkwood", "scouts_flag_t1" },
        { "mirkwood_stalkers", "scouts_flag_t1" },

        // Isengard / Mordor / Misty Mountains — melee-fury standards
        { "isengard", "standard_of_fury_t1" },
        { "mordor", "standard_of_fury_t1" },
        { "dolguldur", "standard_of_fury_t1" },
        { "gundabad", "standard_of_fury_t1" },
        { "gundabad_raiders", "standard_of_fury_t1" },
        { "goblin", "standard_of_fury_t1" },
        { "mistymountainorcs", "standard_of_fury_t1" },

        // Easterlings / Southrons — tug and desert standards
        { "khuzait", "tug_of_wooden_arrow_t1" },         // Easterlings (Rhun)
        { "rhun_raiders", "tug_of_wooden_arrow_t1" },
        { "battania", "tug_of_the_roaming_horse_t1" },   // Variag (Khand)
        { "aserai", "banner_of_faris_falcon_t1" },       // Haradrim
        { "harad_raiders", "banner_of_faris_falcon_t1" },
        { "umbar", "banner_of_desert_winds_t1" },
        { "umbar_corsairs", "banner_of_desert_winds_t1" },
        { "shaghana", "banner_of_faris_falcon_t1" },
        { "abanissa", "banner_of_faris_falcon_t1" },

        // Wild men
        { "empire", "deer_bane_flag_t1" },               // Dunlendings
        { "dunland_raiders", "deer_bane_flag_t1" },
    };

    // Fallback for any culture not in CultureBanners. Empty => unmapped cultures get no banner
    // at all (and therefore no bearers).
    //
    // Deliberately empty: fail CLOSED. 38 cultures are registered but only the 28 above are
    // meaningfully TAOM's; the rest are vanilla leftovers still referenced by live data
    // (looters, sea_raiders, forest_bandits, desert_bandits, mountain_bandits, steppe_bandits,
    // nord, vakken, darshi) plus neutral_culture. A non-empty default hands every one of them
    // whatever banner is named here -- e.g. a looter warband raising the Gondorian Standard of
    // Duty. A forgotten culture with NO banner is a cosmetic absence; a forgotten culture with
    // the WRONG banner is an immersion break. Absence wins.
    public string DefaultBannerItemId { get; set; } = "";
}
