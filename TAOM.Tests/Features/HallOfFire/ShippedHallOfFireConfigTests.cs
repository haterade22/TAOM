using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HallOfFire;
using TAOM.Features.TroopProgression;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.HallOfFire;

/// <summary>
/// Pins the Hall of Fire map shipped to players. The provider is fail-soft, so a bad shipped row would be
/// dropped and its culture would keep the stale backdrop without a visible error; these tests make that loud.
/// </summary>
[TestClass]
public class ShippedHallOfFireConfigTests
{
    private static string ModuleDataPath => RepoPath("Main", "_Module", "ModuleData");

    private IModLogger _logger = null!;
    private HallOfFireMap _map = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _map = new HallOfFireConfigProvider(pathService, _logger).GetMap();
    }

    private static string[] DefinedCultureIds() =>
        Regex.Matches(File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml")),
                @"<Culture\s+id=""([^""]+)""")
            .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();

    [TestMethod]
    public void ShippedConfig_ParsesWithoutWarningOrError()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleDataPath, "hall_of_fire", "hall_of_fire.json")));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_EqualsTheBuiltInDefault()
    {
        var builtIn = HallOfFireMap.Default();

        Assert.IsTrue(builtIn.Cultures.Count >= 19, "the built-in map lost rows");
        CollectionAssert.AreEquivalent(builtIn.Cultures.ToList(), _map.Cultures.ToList());
    }

    [TestMethod]
    public void ShippedConfig_EveryKeyIsAKnownCultureId()
    {
        var defined = DefinedCultureIds();
        Assert.IsTrue(defined.Length > 20, "taom_spcultures.xml culture scan found too few ids");
        var known = defined.Concat(HallOfFireMap.AllowedSets).ToHashSet();

        var unknown = _map.Cultures.Keys.Where(k => !known.Contains(k)).ToList();

        Assert.AreEqual(0, unknown.Count, "Culture keys that name no culture: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void ShippedConfig_EveryConversionTargetCultureHasARowOrIsVanilla()
    {
        var random = Substitute.For<IRandomProvider>();
        var targets = new VolunteerRecruitmentService(random, Substitute.For<IModLogger>(), Substitute.For<TAOM.Features.Arachnophobia.IArachnophobiaService>()).GetPooledCultureIds()
            .Where(c => !string.IsNullOrEmpty(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        Assert.IsTrue(targets.Count >= 20, $"Only {targets.Count} conversion targets found; the recruitment pools moved and this gate would pass vacuously.");

        var missing = targets.Where(c => !_map.Cultures.ContainsKey(c) && !HallOfFireMap.AllowedSets.Contains(c)).ToList();

        Assert.AreEqual(0, missing.Count,
            "Conversion targets with no Hall of Fire row (a converted fief keeps a stale backdrop): " + string.Join(", ", missing));
    }
}

/// <summary>
/// Reads the installed Native atmospheres: every borrowed set must ship the five backdrops the engine asks for.
/// Inconclusive on a machine without the game.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class ShippedHallOfFireNativeAtmosphereTests
{
    private static readonly string[] Backdrops =
    {
        "town_noon_0", "town_sunset_0", "town_night_0", "tavern_0", "lordshall_0",
    };

    [TestMethod]
    public void EveryVanillaSet_ShipsItsFiveBackdropsInNative()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        string folder = Path.Combine(game, "Modules", "Native", "Atmospheres");
        if (!Directory.Exists(folder))
            Assert.Inconclusive("Native atmospheres not installed on this machine; the backdrops cannot be checked here.");

        var missing = HallOfFireMap.AllowedSets
            .SelectMany(set => Backdrops.Select(b => $"conv_{set}_{b}.xml"))
            .Where(name => !File.Exists(Path.Combine(folder, name))).ToList();

        Assert.AreEqual(0, missing.Count, "Native atmospheres missing: " + string.Join(", ", missing));
    }
}

/// <summary>
/// Reads the live, unversioned TAOM_Map settlements.xml (TAOM_Map is in no repo): a culture that owns a
/// non-hideout settlement and has neither a row nor a vanilla id would show a stale backdrop in its towns.
/// Inconclusive on a machine without the module.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class ShippedHallOfFireLiveCoverageTests
{
    private static XElement LoadLiveSettlements()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        string path = Path.Combine(game, "Modules", "TAOM_Map", "ModuleData", "settlements.xml");
        if (!File.Exists(path))
            Assert.Inconclusive("TAOM_Map not installed on this machine; the live settlements cannot be checked here.");
        return XDocument.Load(path).Root!;
    }

    [TestMethod]
    public void EveryCultureOwningANonHideoutSettlement_HasARowOrIsVanilla()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(RepoPath("Main", "_Module", "ModuleData"));
        var map = new HallOfFireConfigProvider(pathService, Substitute.For<IModLogger>()).GetMap();

        var settlements = LoadLiveSettlements().Elements("Settlement")
            .Where(s => !string.Equals((string?)s.Attribute("type"), "Hideout", StringComparison.Ordinal)).ToList();
        Assert.IsTrue(settlements.Count > 500, $"the live settlements scan found only {settlements.Count} non-hideout settlements");
        var cultures = settlements.Select(s => (string?)s.Attribute("culture"))
            .Where(c => c != null && c.StartsWith("Culture.", StringComparison.Ordinal))
            .Select(c => c!.Substring("Culture.".Length)).Distinct().ToList();
        Assert.IsTrue(cultures.Count >= 15, $"the live settlements scan found only {cultures.Count} cultures");

        var missing = cultures.Where(c => !map.Cultures.ContainsKey(c) && !HallOfFireMap.AllowedSets.Contains(c)).ToList();

        Assert.AreEqual(0, missing.Count,
            "Cultures that own a settlement but have no Hall of Fire row (their conversations keep a stale backdrop): "
            + string.Join(", ", missing));
    }
}
