using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BannerBearers;

namespace TAOM.Tests.Features.BannerBearers;

// Pins the config file we actually ship. The provider is fail-soft by design — a malformed or
// semantically-invalid shipped file would silently revert to compiled defaults and the feature
// would look "fine" while ignoring every value we authored. These tests make that loud.
[TestClass]
public class ShippedBannerBearerConfigTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData"));

    private IModLogger _logger = null!;
    private BannerBearerConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new BannerBearerConfigProvider(pathService, _logger);
    }

    [TestMethod]
    public void ShippedConfig_FileExists()
    {
        var path = Path.Combine(ModuleDataPath, "banner_bearers", "banner_bearers_config.json");

        Assert.IsTrue(File.Exists(path), $"Shipped config missing at {path}");
    }

    [TestMethod]
    public void ShippedConfig_ParsesWithoutErrorOrRejection()
    {
        _sut.GetConfig();

        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
        // Any per-field revert would mean a value we shipped is out of its own valid range.
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("See prior warnings")));
    }

    [TestMethod]
    public void ShippedConfig_ValuesSurviveValidationUnchanged()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled, "shipped config should ship enabled");
        Assert.AreEqual(4, config.MinimumFormationTroopCount);
        Assert.AreEqual(6, config.MaxBearersPerFormation);
        Assert.AreEqual(10, config.InfantryBannerPerSoldiers);
        Assert.AreEqual(25, config.RangedBannerPerSoldiers);
        Assert.AreEqual(15, config.CavalryBannerPerSoldiers);
        Assert.AreEqual(15, config.HorseArcherBannerPerSoldiers);
        Assert.AreEqual(25, config.OtherBannerPerSoldiers);
    }

    [TestMethod]
    public void ShippedConfig_AllowsInfantryOnly()
    {
        // A bearer swaps its weapons for a banner + sidearm — never convert an archer or rider.
        var config = _sut.GetConfig();

        CollectionAssert.Contains(config.AllowedFormationGroups, "Infantry");
        CollectionAssert.DoesNotContain(config.AllowedFormationGroups, "Ranged");
        CollectionAssert.DoesNotContain(config.AllowedFormationGroups, "Cavalry");
        CollectionAssert.DoesNotContain(config.AllowedFormationGroups, "HorseArcher");
    }

    [TestMethod]
    public void ShippedConfig_ExcludesTrollsAndNamedRaces()
    {
        var config = _sut.GetConfig();

        // The race gate the feature exists to honour — trolls must never raise a standard.
        CollectionAssert.Contains(config.ExcludedRaces, "cave_troll");
        CollectionAssert.Contains(config.ExcludedRaces, "hill_troll");
        CollectionAssert.Contains(config.ExcludedRaces, "nazghul");
        CollectionAssert.Contains(config.ExcludedRaces, "saruman");
        CollectionAssert.Contains(config.ExcludedRaces, "sauron");
    }

    [TestMethod]
    public void ShippedConfig_DoesNotExcludeOrdinaryBattlefieldRaces()
    {
        var config = _sut.GetConfig();

        foreach (var race in new[] { "human", "orc", "dwarf", "elf", "uruk", "uruk_hai", "goblin", "dg_uruk", "pale_uruk" })
            CollectionAssert.DoesNotContain(config.ExcludedRaces, race, $"'{race}' must stay eligible to carry a banner");
    }

    [TestMethod]
    public void ShippedConfig_MapsEveryMajorCultureToABanner()
    {
        var config = _sut.GetConfig();

        // Real culture StringIds. The six re-skinned vanilla cultures keep their VANILLA ids —
        // spcultures.xslt overrides <name>, never the id. Rohirrim = vlandia, Dunlendings =
        // empire, Haradrim = aserai, Easterlings/Rhun = khuzait, Barding/Dale = sturgia,
        // Variag/Khand = battania.
        var majorCultures = new[]
        {
            "gondor", "vlandia", "sturgia", "erebor", "rivendell", "lothlorien", "mirkwood",
            "isengard", "mordor", "dolguldur", "gundabad", "goblin", "mistymountainorcs",
            "khuzait", "battania", "aserai", "umbar", "shaghana", "abanissa", "empire",
        };

        foreach (var culture in majorCultures)
        {
            Assert.IsTrue(config.CultureBanners.ContainsKey(culture), $"culture '{culture}' has no banner mapping");
            Assert.IsFalse(string.IsNullOrWhiteSpace(config.CultureBanners[culture]), $"culture '{culture}' maps to an empty banner");
        }
    }

    // ---- Culture-id regression pins (deep-review 2026-07-16 CRITICAL) -----------
    // The shipped config keyed six factions on their LOTR display name ("rohan", "dale",
    // "khand", "dunland", "harad", "rhun"). None is a real StringId — spcultures.xslt renames
    // the culture but keeps the vanilla id — so those six silently flew the default banner.
    // Nothing in the type system or the engine catches a dead dictionary key; these tests do.

    private static readonly string[] VanillaReskinnedCultureIds =
        { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" };

    private static HashSet<string> RealCultureIds()
    {
        var xml = File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml"));
        var ids = Regex.Matches(xml, "<Culture\\b[^>]*?\\bid\\s*=\\s*\"([^\"]+)\"")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        // The six vanilla cultures TAOM re-skins via XSLT rather than declaring outright.
        foreach (var id in VanillaReskinnedCultureIds) ids.Add(id);
        return ids;
    }

    [TestMethod]
    public void ShippedConfig_EveryCultureBannerKeyIsARealCultureStringId()
    {
        var real = RealCultureIds();

        foreach (var key in _sut.GetConfig().CultureBanners.Keys)
            Assert.IsTrue(real.Contains(key),
                $"CultureBanners key '{key}' is not a real culture StringId. TAOM's XSLT renames the six " +
                "vanilla cultures but keeps their ids — use 'vlandia' for Rohirrim, 'empire' for " +
                "Dunlendings, 'aserai' for Haradrim, 'khuzait' for Easterlings, 'sturgia' for Barding, " +
                "'battania' for Variag. A key that matches nothing silently yields the default banner.");
    }

    [TestMethod]
    public void ShippedConfig_NoLotrDisplayNamesUsedAsCultureKeys()
    {
        var banned = new[] { "rohan", "dale", "khand", "dunland", "harad", "rhun" };
        var keys = _sut.GetConfig().CultureBanners.Keys;

        foreach (var name in banned)
            Assert.IsFalse(keys.Contains(name),
                $"'{name}' is a display name, not a culture StringId — it will never match.");
    }

    [TestMethod]
    public void ShippedConfig_EveryBanneredCultureDeclaresReplacementWeapons()
    {
        // GetBannerBearerReplacementWeapon returns null for a culture with no
        // <banner_bearer_replacement_weapons>, and CreateBannerEquipmentForAgent then clears
        // weapon slots 1-3 — producing a bearer holding a banner and nothing else. TAOM does not
        // patch that in C#; this test is the guard. Only checks TAOM-declared cultures: the six
        // XSLT-reskinned ones inherit vanilla's declarations by passthrough.
        var xml = File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml"));
        var declared = Regex.Matches(xml, "<Culture\\b[^>]*?\\bid\\s*=\\s*\"([^\"]+)\"(.*?)(?=<Culture\\b|</Cultures>|\\z)",
                RegexOptions.Singleline)
            .Cast<Match>()
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);

        foreach (var kv in _sut.GetConfig().CultureBanners)
        {
            if (string.IsNullOrEmpty(kv.Value)) continue;                 // opted out of banners
            if (!declared.TryGetValue(kv.Key, out var body)) continue;    // XSLT-reskinned vanilla culture

            Assert.IsTrue(body.Contains("banner_bearer_replacement_weapons"),
                $"culture '{kv.Key}' is mapped to banner '{kv.Value}' but declares no " +
                "<banner_bearer_replacement_weapons> — its bearers would spawn unarmed.");
        }
    }

    [TestMethod]
    public void ShippedConfig_BannerIdsLookLikeVanillaBannerItems()
    {
        var config = _sut.GetConfig();

        // MBObjectManager isn't available in the test host, so we can't resolve the ItemObject
        // here — BannerBearerAssignmentMissionLogic validates that at mission start and warns.
        // What we CAN pin is that every id is one vanilla actually ships in
        // SandBoxCore/ModuleData/items/banners.xml, which is where typos would bite.
        var vanillaBannerIds = new[]
        {
            "standard_of_fury_t1", "standard_of_rage_t2", "standard_of_wrath_t3",
            "deer_bane_flag_t1", "boar_bane_flag_t2", "horse_bane_flag_t3",
            "spear_bracing_banner_t1", "spear_wall_banner_t2", "pike_wall_banner_t3",
            "archers_flag_t1", "bowmans_flag_t2", "marksmans_flag_t3",
            "banner_of_faris_falcon_t1", "banner_of_emirs_hawk_t2", "banner_of_sultans_eagle_t3",
            "tug_of_wooden_arrow_t1", "tug_of_bone_arrow_t2", "tug_of_whistling_arrow_t3",
            "banner_of_the_horseman_t1", "banner_of_the_squire_t2", "banner_of_the_knight_t3",
            "standard_of_duty_t1", "standard_of_courage_t2", "standard_of_discipline_t3",
            "stone_banner_t1", "iron_banner_t2", "steel_banner_t3",
            "phalanx_standard_t1", "fulcum_standard_t2", "testudo_standard_t3",
            "close_shields_banner_t1", "shield_wall_banner_t2", "locked_shields_banner_t3",
            "banner_of_oaken_shields_t1", "banner_of_iron_shields_t2", "banner_of_steel_shields_t3",
            "banner_of_desert_winds_t1", "banner_of_shifting_sands_t2", "banner_of_dust_devils_t3",
            "scouts_flag_t1", "rangers_flag_t2", "striders_flag_t3",
            "tug_of_the_roaming_horse_t1", "tug_of_the_boundless_horde_t2", "tug_of_the_endless_steppe_t3",
        };

        foreach (var kv in config.CultureBanners)
        {
            if (string.IsNullOrEmpty(kv.Value)) continue;
            CollectionAssert.Contains(vanillaBannerIds, kv.Value,
                $"culture '{kv.Key}' maps to '{kv.Value}', which is not a vanilla banner item id");
        }

        if (!string.IsNullOrEmpty(config.DefaultBannerItemId))
            CollectionAssert.Contains(vanillaBannerIds, config.DefaultBannerItemId,
                $"DefaultBannerItemId '{config.DefaultBannerItemId}' is not a vanilla banner item id");
    }

    [TestMethod]
    public void ShippedConfig_DefaultBannerIsEmpty_SoUnmappedCulturesGetNoBanner()
    {
        // Fail closed. 38 cultures are registered; only the 28 in CultureBanners are TAOM's.
        // The remainder are vanilla leftovers still referenced by live TAOM data (looters,
        // sea_raiders, forest_bandits, desert_bandits, mountain_bandits, steppe_bandits, nord,
        // vakken, darshi) plus neutral_culture. Any non-empty default hands a banner to ALL of
        // them — the first cut shipped "standard_of_duty_t1", which would have put the Gondorian
        // Standard of Duty in the hands of every looter warband in the game.
        Assert.AreEqual(string.Empty, _sut.GetConfig().DefaultBannerItemId,
            "DefaultBannerItemId must stay empty so unmapped cultures field no banner. " +
            "Mapping a culture explicitly in CultureBanners is the only way it should get one.");
    }

    [TestMethod]
    public void ShippedConfig_VanillaLeftoverCulturesAreNotMapped()
    {
        var config = _sut.GetConfig();

        foreach (var culture in new[]
        {
            "looters", "sea_raiders", "forest_bandits", "desert_bandits", "mountain_bandits",
            "steppe_bandits", "nord", "vakken", "darshi", "neutral_culture",
        })
        {
            Assert.IsFalse(
                config.CultureBanners.TryGetValue(culture, out var banner) && !string.IsNullOrEmpty(banner),
                $"vanilla leftover culture '{culture}' is mapped to banner '{banner}' — these should field no standard.");
        }
    }
}
