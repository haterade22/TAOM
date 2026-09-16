using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SignatureStrikes;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// Pins the config file actually shipped to players. The provider is fail-soft by design, so a
/// shipped file with a bad value would revert to compiled defaults and the feature would look
/// fine while ignoring everything authored here. These tests make that loud.
/// </summary>
[TestClass]
public class ShippedSignatureStrikesConfigTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private static string ConfigPath =>
        Path.Combine(ModuleDataPath, "signature_strikes", "signature_strikes_config.json");

    private IModLogger _logger = null!;
    private SignatureStrikesConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new SignatureStrikesConfigProvider(pathService, _logger);
    }

    [TestMethod]
    public void ShippedConfig_FileExists()
        => Assert.IsTrue(File.Exists(ConfigPath), $"Shipped config missing at {ConfigPath}");

    [TestMethod]
    public void ShippedConfig_ParsesWithoutErrorOrRejection()
    {
        _sut.GetConfig();

        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_ListsSauronByBothHeroIdAndRace()
    {
        // Both axes on purpose: the hero id survives a data change that drops his race attribute,
        // the race finds him in a custom battle where the agent carries no HeroObject.
        var config = _sut.GetConfig();

        CollectionAssert.Contains(config.HeroIds, "lord_1_17", "Sauron's hero id must be listed.");
        CollectionAssert.Contains(config.Races, "sauron", "Sauron's race must be listed.");
    }

    [TestMethod]
    public void ShippedConfig_ShipsNoOtherSignatureHero()
    {
        // The Witch-king and the trolls are deliberately OUT of v1. This is the gate that stops one
        // of them arriving as a quiet data edit with no balance pass behind it.
        var config = _sut.GetConfig();

        CollectionAssert.AreEquivalent(new[] { "lord_1_17" }, config.HeroIds,
            "A hero was added to the shipped signature-strike roster. That is a balance change: " +
            "give it an issue and a control battle, and update docs/features/signature-strikes.md.");
        CollectionAssert.AreEquivalent(new[] { "sauron" }, config.Races);
    }

    [TestMethod]
    public void ShippedConfig_MapsOverheadToSlamAndSideSwingsToSweep()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual("Slam", config.Strikes["Overhead"].Kind);
        Assert.AreEqual("Sweep", config.Strikes["Left"].Kind);
        Assert.AreEqual("Sweep", config.Strikes["Right"].Kind);
        Assert.IsFalse(config.Strikes.ContainsKey("Thrust"), "A thrust is a plain hit.");
    }

    [TestMethod]
    public void ShippedConfig_SlamKnocksDownAndSweepKnocksBack()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Strikes["Overhead"].KnockDown);
        Assert.IsFalse(config.Strikes["Overhead"].KnockBack);
        Assert.IsTrue(config.Strikes["Left"].KnockBack);
        Assert.IsFalse(config.Strikes["Left"].KnockDown);
        Assert.IsTrue(config.Strikes["Right"].KnockBack);
    }

    [TestMethod]
    public void ShippedConfig_CooldownsAreTheDocumentedBalanceContract()
    {
        // Mike's standing instruction (2026-09-16): long cooldowns, Sauron must not be overpowered.
        // These two numbers are quoted in docs/features/signature-strikes.md and issue #605.
        var config = _sut.GetConfig();

        Assert.AreEqual(20f, config.SlamCooldownSeconds, 0.001f);
        Assert.AreEqual(12f, config.SweepCooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void ShippedConfig_OnlyTheSlamFiresOnAGroundHit()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Strikes["Overhead"].WorldHitBaseDamage > 0);
        Assert.AreEqual(0, config.Strikes["Left"].WorldHitBaseDamage);
        Assert.AreEqual(0, config.Strikes["Right"].WorldHitBaseDamage);
    }

    [TestMethod]
    public void ShippedConfig_SauronStillCarriesTheSauronRaceInLordsXslt()
    {
        // Cross-file: the config names a hero id and a race; lords.xslt is what binds the two. If
        // a lord regen ever drops the race attribute, the hero-id axis still finds him in a
        // campaign but the race axis stops finding him in a Custom Battle, with no error anywhere.
        var xslt = File.ReadAllText(Path.Combine(ModuleDataPath, "lords.xslt"));
        var start = xslt.IndexOf("NPCCharacter[@id='lord_1_17']", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "lords.xslt no longer has a template for lord_1_17 (Sauron).");

        var end = xslt.IndexOf("</xsl:template>", start, StringComparison.Ordinal);
        var block = xslt.Substring(start, end - start);

        StringAssert.Contains(block, "name=\"race\">sauron<",
            "lord_1_17 must keep race=\"sauron\" in lords.xslt; the shipped config lists that race.");
    }

    [TestMethod]
    public void ShippedConfig_EveryCommentKeyHasALiveSibling()
    {
        // A _comment_x whose x was renamed or removed is documentation pointing at nothing.
        var root = JObject.Parse(File.ReadAllText(ConfigPath));

        foreach (var property in root.Properties())
        {
            if (!property.Name.StartsWith("_comment_", StringComparison.Ordinal))
                continue;

            var sibling = property.Name.Substring("_comment_".Length);
            if (sibling == "feature" || sibling == "balance")
                continue;

            Assert.IsNotNull(root[sibling],
                $"{property.Name} documents a key '{sibling}' that no longer exists in the config.");
        }
    }

    [TestMethod]
    public void ShippedConfig_ContainsNoLongDashes()
    {
        // output-style.md Part 2: em and en dashes are banned in produced prose, and the comment
        // keys here are prose players read.
        var text = File.ReadAllText(ConfigPath);

        Assert.IsFalse(Regex.IsMatch(text, "[\u2013\u2014]"),
            "signature_strikes_config.json contains an em or en dash. Use a comma, colon, or new sentence.");
    }
}
