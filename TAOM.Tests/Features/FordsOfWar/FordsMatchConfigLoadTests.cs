using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// match.json: every number reverts to <see cref="FordsMatchConfig.Default"/> with one warning that names the key when it is
/// missing a number, not finite, out of range or not a whole number where one is needed. A file that cannot be read leaves the
/// whole default config.
/// </summary>
[TestClass]
public class FordsMatchConfigLoadTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private FordsMatchConfig Load(string? match) => FordsConfigCatalog.FromTexts(match, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, _logger).Match;

    private void AssertOneWarningNaming(string key) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("match.json") && m.Contains(key)));

    internal static void AssertSameAsDefault(FordsMatchConfig actual)
    {
        var d = FordsMatchConfig.Default;
        Assert.AreEqual(d.Economy, actual.Economy);
        Assert.AreEqual(d.Waves, actual.Waves);
        Assert.AreEqual(d.Towers, actual.Towers);
        Assert.AreEqual(d.Heroes, actual.Heroes);
        Assert.AreEqual(d.Lanes, actual.Lanes);
        Assert.AreEqual(d.Capture, actual.Capture);
        Assert.AreEqual(d.CountdownSeconds, actual.CountdownSeconds);
        CollectionAssert.AreEquivalent(d.AiProfiles.Keys.ToList(), actual.AiProfiles.Keys.ToList());
        foreach (var pair in d.AiProfiles)
            Assert.AreEqual(pair.Value, actual.AiProfiles[pair.Key], pair.Key);
    }

    [TestMethod]
    public void Match_FileMissing_GivesDefaultsAndOneWarning()
    {
        AssertSameAsDefault(Load(null));

        AssertOneWarningNaming("match.json");
    }

    [TestMethod]
    public void Match_NotJson_GivesDefaultsWithoutThrowing()
    {
        AssertSameAsDefault(Load("{ this is not json"));

        AssertOneWarningNaming("match.json");
    }

    [TestMethod]
    public void Match_NotAnObject_GivesDefaults()
    {
        AssertSameAsDefault(Load("[1, 2, 3]"));

        AssertOneWarningNaming("not a JSON object");
    }

    [TestMethod]
    public void Match_EmptyObject_GivesDefaultsAndNoWarning()
    {
        AssertSameAsDefault(Load("{}"));

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Match_GoodOverride_ReplacesOnlyThatValue()
    {
        var config = Load("{ 'economy': { 'startingGold': 900 }, 'countdownSeconds': 5 }");

        Assert.AreEqual(900, config.Economy.StartingGold);
        Assert.AreEqual(FordsMatchConfig.Default.Economy.IncomePerMinute, config.Economy.IncomePerMinute);
        Assert.AreEqual(5f, config.CountdownSeconds);
        Assert.AreEqual(FordsMatchConfig.Default.Towers, config.Towers);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Match_StringWhereNumberExpected_RevertsWithWarningNamingTheKey()
    {
        var config = Load("{ 'economy': { 'startingGold': '900' } }");

        Assert.AreEqual(600, config.Economy.StartingGold);
        AssertOneWarningNaming("economy.startingGold");
    }

    [TestMethod]
    public void Match_FractionWhereWholeNumberExpected_Reverts()
    {
        var config = Load("{ 'waves': { 'maxSpawnsPerTick': 2.5 } }");

        Assert.AreEqual(6, config.Waves.MaxSpawnsPerTick);
        AssertOneWarningNaming("waves.maxSpawnsPerTick");
    }

    [TestMethod]
    public void Match_NaN_RevertsBeforeAnyRangeCheck()
    {
        var config = Load("{ 'towers': { 'rangedDamageFactor': NaN } }");

        Assert.AreEqual(0.15f, config.Towers.RangedDamageFactor);
        AssertOneWarningNaming("towers.rangedDamageFactor");
    }

    [TestMethod]
    public void Match_Infinity_Reverts()
    {
        var config = Load("{ 'towers': { 'fireRange': Infinity } }");

        Assert.AreEqual(45f, config.Towers.FireRange);
        AssertOneWarningNaming("towers.fireRange");
    }

    [TestMethod]
    public void Match_NegativeGold_Reverts()
    {
        var config = Load("{ 'economy': { 'towerBounty': -5 } }");

        Assert.AreEqual(300, config.Economy.TowerBounty);
        AssertOneWarningNaming("economy.towerBounty");
    }

    [TestMethod]
    public void Match_ZeroHitPoints_Reverts()
    {
        var config = Load("{ 'towers': { 'keepHitPoints': 0 } }");

        Assert.AreEqual(18000, config.Towers.KeepHitPoints);
        AssertOneWarningNaming("towers.keepHitPoints");
    }

    [TestMethod]
    public void Match_MistakeChanceAboveOne_Reverts()
    {
        var config = Load("{ 'aiProfiles': { 'easy': { 'mistakeChance': 1.5 } } }");

        Assert.AreEqual(0.25f, config.AiProfiles["easy"].MistakeChance);
        AssertOneWarningNaming("aiProfiles.easy.mistakeChance");
    }

    [TestMethod]
    public void Match_GroupNotAnObject_KeepsThatGroupsDefaultsWithOneWarning()
    {
        var config = Load("{ 'lanes': 7, 'capture': { 'radius': 15 } }");

        Assert.AreEqual(FordsMatchConfig.Default.Lanes, config.Lanes);
        Assert.AreEqual(15f, config.Capture.Radius);
        AssertOneWarningNaming("'lanes'");
    }

    [TestMethod]
    public void Match_AiProfileOverride_KeepsTheOtherProfiles()
    {
        var config = Load("{ 'aiProfiles': { 'normal': { 'decisionIntervalSeconds': 6 } } }");

        Assert.AreEqual(6f, config.AiProfiles["normal"].DecisionIntervalSeconds);
        Assert.AreEqual(FordsMatchConfig.Default.AiProfiles["normal"].HeroReserveGold, config.AiProfiles["normal"].HeroReserveGold);
        Assert.AreEqual(FordsMatchConfig.Default.AiProfiles["easy"], config.AiProfiles["easy"]);
        Assert.AreEqual(FordsMatchConfig.Default.AiProfiles["hard"], config.AiProfiles["hard"]);
        Assert.AreEqual("normal", config.AiProfiles["normal"].Id);
    }

    [TestMethod]
    public void Match_NewAiProfile_IsAddedWithDefaultsForTheRest()
    {
        var config = Load("{ 'aiProfiles': { 'brutal': { 'mistakeChance': 0 } } }");

        Assert.AreEqual(4, config.AiProfiles.Count);
        Assert.AreEqual("brutal", config.AiProfiles["brutal"].Id);
        Assert.AreEqual(0f, config.AiProfiles["brutal"].MistakeChance);
        Assert.AreEqual(FordsMatchConfig.Default.AiProfiles["normal"].DecisionIntervalSeconds, config.AiProfiles["brutal"].DecisionIntervalSeconds);
    }

    [TestMethod]
    public void Match_AiProfileNotAnObject_IsSkippedWithAWarning()
    {
        var config = Load("{ 'aiProfiles': { 'easy': 'soft' } }");

        Assert.AreEqual(FordsMatchConfig.Default.AiProfiles["easy"], config.AiProfiles["easy"]);
        AssertOneWarningNaming("aiProfiles.easy");
    }

    [TestMethod]
    public void FileCtor_ReadsTheModuleDataFolder_AndCachesTheFirstLoad()
    {
        var root = Path.Combine(Path.GetTempPath(), "fords_cfg_" + Path.GetRandomFileName());
        var folder = Path.Combine(root, "fords_of_war");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "match.json"), "{ 'countdownSeconds': 3 }");
            var paths = Substitute.For<IPathService>();
            paths.ModuleDataPath.Returns(root);
            var catalog = new FordsConfigCatalog(paths, _logger);

            Assert.AreEqual(3f, catalog.Match.CountdownSeconds);
            File.WriteAllText(Path.Combine(folder, "match.json"), "{ 'countdownSeconds': 99 }");
            Assert.AreEqual(3f, catalog.Match.CountdownSeconds, "the file is read once per process");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void FileCtor_FolderMissing_NeverThrows()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "fords_cfg_missing_" + Path.GetRandomFileName()));

        var catalog = new FordsConfigCatalog(paths, _logger);

        AssertSameAsDefault(catalog.Match);
        Assert.AreEqual(0, catalog.Factions.Count);
        Assert.AreEqual(0, catalog.Maps.Count);
    }
}
