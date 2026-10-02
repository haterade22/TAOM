using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.TournamentRewards;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// Pins the reward factors shipped to players (Mike, 2026-10-02). The provider is fail-soft, so a bad row would
/// quietly revert; these tests make it loud, and they pin the culture-id trap: Rohan is <c>vlandia</c> and Dale
/// <c>sturgia</c>, so a row keyed on the LOTR name applies to nobody.
/// </summary>
[TestClass]
public class ShippedTournamentRewardsConfigTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private static string ConfigPath => Path.Combine(ModuleDataPath, "tournament_rewards", "tournament_rewards.json");

    private IModLogger _logger = null!;
    private TournamentRewardsConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new TournamentRewardsConfigProvider(pathService, _logger);
    }

    [TestMethod]
    public void ShippedConfig_ParsesWithoutErrorOrRejection()
    {
        Assert.IsTrue(File.Exists(ConfigPath), $"Shipped config missing at {ConfigPath}");

        _sut.GetCatalog();

        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("vlandia", 1.5f, 1.25f, 1.0f)]
    [DataRow("gondor", 1.25f, 1.5f, 1.10f)]
    [DataRow("sturgia", 1.25f, 1.0f, 1.0f)]
    [DataRow("mordor", 1.5f, 1.0f, 1.40f)]
    [DataRow("isengard", 1.5f, 1.0f, 1.40f)]
    [DataRow("gundabad", 1.5f, 1.0f, 1.40f)]
    [DataRow("goblin", 1.5f, 1.0f, 1.40f)]
    [DataRow("mistymountainorcs", 1.5f, 1.0f, 1.40f)]
    [DataRow("dolguldur", 1.5f, 1.0f, 1.40f)]
    [DataRow("rivendell", 1.0f, 1.0f, 1.0f)]
    public void ShippedConfig_HasTheApprovedFactors(string culture, float renown, float influence, float xp)
    {
        var f = _sut.GetCatalog().For(culture);

        Assert.AreEqual(renown, f.Renown, 0.0001f, $"{culture} renown");
        Assert.AreEqual(influence, f.Influence, 0.0001f, $"{culture} influence");
        Assert.AreEqual(xp, f.SkillXp, 0.0001f, $"{culture} skill_xp");
    }

    [TestMethod]
    public void ShippedConfig_EveryKeyIsARealCultureId()
    {
        var known = new[] { "default", "empire", "sturgia", "aserai", "vlandia", "battania", "khuzait" }.ToList();
        foreach (var file in new[] { "taom_spcultures.xml" })
            known.AddRange(Regex.Matches(File.ReadAllText(Path.Combine(ModuleDataPath, file)), @"<Culture\s+id=""([^""]+)""")
                .Cast<Match>().Select(m => m.Groups[1].Value));

        foreach (var key in _sut.GetCatalog().CultureIds)
            CollectionAssert.Contains(known, key, $"'{key}' names no culture: Rohan is vlandia, Dale sturgia");
    }
}
