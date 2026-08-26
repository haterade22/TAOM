using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.NazgulFamily;
using TAOM.Features.UncapturableHeroes;

namespace TAOM.Tests.Features.UncapturableHeroes;

/// <summary>
/// Pins the config file actually shipped to players, plus the two shipped DATA facts the feature
/// silently depends on. The provider is fail-soft by design, so a shipped file with a bad value
/// would revert to compiled defaults and the feature would look fine while ignoring everything
/// authored here.
/// </summary>
[TestClass]
public class ShippedUncapturableHeroesConfigTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private static string ConfigPath => Path.Combine(
        ModuleDataPath, "uncapturable_heroes", "uncapturable_heroes_config.json");

    private static string LordsXsltPath => Path.Combine(ModuleDataPath, "lords.xslt");

    private static string LordsXmlPath => Path.Combine(ModuleDataPath, "characters", "lords.xml");

    private IModLogger _logger = null!;
    private UncapturableHeroesConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new UncapturableHeroesConfigProvider(pathService, _logger);
    }

    [TestMethod]
    public void ShippedConfig_FileExists()
        => Assert.IsTrue(File.Exists(ConfigPath), $"Shipped config missing at {ConfigPath}");

    [TestMethod]
    public void ShippedConfig_ParsesWithoutErrorOrRejection()
    {
        _sut.GetConfig();

        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void ShippedConfig_KeepsTheNazgulHeroSet()
    {
        // The load-bearing one. Six of the Nine carry no race attribute and the other three are
        // race="uruk", so the hero-set axis is the ONLY thing that covers all nine. A well-meaning
        // "simplify this to a race list" refactor would silently free six wraiths with no error,
        // no warning, and a config that still parses cleanly.
        var config = _sut.GetConfig();

        CollectionAssert.Contains(config.HeroSets, "nazgul_nine",
            "uncapturable_heroes_config.json must keep the nazgul_nine hero set. Race cannot "
            + "identify the Nazgul: see docs/features/uncapturable-heroes.md.");
    }

    [TestMethod]
    public void ShippedConfig_ListsSauronByBothHeroIdAndRace()
    {
        var config = _sut.GetConfig();

        CollectionAssert.Contains(config.HeroIds, "lord_1_17", "Sauron's hero id must be listed.");
        CollectionAssert.Contains(config.UncapturableRaces, "sauron", "Sauron's race must be listed.");
    }

    [TestMethod]
    public void ShippedConfig_ShipsNobodyUnintended()
    {
        // Trolls, Mumakil and the Mouth of Sauron are deliberately capturable. This is the gate
        // that stops one of them arriving as a quiet data edit with no balance pass behind it.
        var config = _sut.GetConfig();

        CollectionAssert.AreEquivalent(new[] { "sauron" }, config.UncapturableRaces,
            "A race was added to the shipped uncapturable list. That is a balance change: give it "
            + "an issue and update docs/features/uncapturable-heroes.md.");
        CollectionAssert.AreEquivalent(new[] { "lord_1_17" }, config.HeroIds);
        CollectionAssert.AreEquivalent(new[] { "nazgul_nine" }, config.HeroSets);
    }

    [TestMethod]
    public void ShippedConfig_ExcludeListIsEmpty()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(0, config.ExcludeHeroIds.Count,
            "The shipped exclude list must be empty; it exists for players and modders, not for us.");
    }

    [TestMethod]
    public void ShippedConfig_AnnouncesEscapesByDefault()
        => Assert.IsTrue(_sut.GetConfig().AnnounceEscape);

    [TestMethod]
    public void ShippedConfig_RaceKeysAreSpelledAsTheGameSpellsThem()
    {
        // "nazgul" vs "nazghul" is the exact typo class this catches: a race name that does not
        // exist is skipped with a warning at runtime, which nobody reads.
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "human", "elf", "dwarf", "orc", "goblin", "uruk", "uruk_hai", "pale_uruk", "dg_uruk",
            "berserker", "sauron", "saruman", "nazghul", "cave_troll", "hill_troll",
        };

        foreach (var name in _sut.GetConfig().UncapturableRaces)
        {
            Assert.IsTrue(known.Contains(name),
                $"'{name}' is not a race name TAOM ships. Note the engine spelling is 'nazghul', not 'nazgul'.");
        }
    }

    // ---- Cross-file: the shipped DATA the feature depends on ---------------

    [TestMethod]
    public void ShippedData_SauronStillCarriesTheSauronRace()
    {
        // The race rule finds exactly one hero on shipped data. If this attribute is ever dropped,
        // the rule silently matches nobody and Sauron is protected only by his heroIds entry.
        // lords.xslt emits attributes as <xsl:attribute name="race">, never as race="...".
        var xslt = File.ReadAllText(LordsXsltPath);

        StringAssert.Contains(xslt, "<xsl:attribute name=\"race\">sauron</xsl:attribute>",
            "lord_1_17 no longer emits race=\"sauron\". The uncapturableRaces rule now matches "
            + "nobody; Sauron falls back to his heroIds entry alone.");
    }

    [TestMethod]
    public void ShippedData_SauronIsStillAnOrdinaryLord_NotASpecialHero()
    {
        // MapEvent.cs:1977 skips the ENTIRE capture block for Occupation.Special, so the battle
        // seam would never run for him and this feature would look broken with no error anywhere.
        var xslt = File.ReadAllText(LordsXsltPath);
        var template = ExtractXsltTemplate(xslt, "lord_1_17");

        StringAssert.Contains(template, "<xsl:attribute name=\"occupation\">Lord</xsl:attribute>",
            "lord_1_17 is no longer occupation=Lord. MapEvent.CaptureDefeatedPartyMembers skips "
            + "the whole capture block for Occupation.Special, which silently unhooks the battle seam.");
    }

    [TestMethod]
    public void ShippedData_EveryWraithIdInTheRegistryStillExists()
    {
        // The config names a set; NazgulRegistry owns its membership; lords.xslt and lords.xml own
        // the characters. A renamed id would leave the registry pointing at nobody, silently.
        var xslt = File.ReadAllText(LordsXsltPath);
        var xml = File.ReadAllText(LordsXmlPath);

        foreach (var id in WraithIds)
        {
            var inXslt = xslt.Contains($"NPCCharacter[@id='{id}']");
            var inXml = xml.Contains($"<NPCCharacter id=\"{id}\"");

            Assert.IsTrue(inXslt || inXml,
                $"NazgulRegistry lists '{id}' but no such character is defined in lords.xslt or "
                + "characters/lords.xml. That wraith is silently capturable.");
        }
    }

    [TestMethod]
    public void ShippedData_TheWraithRosterIsTheNine()
    {
        var registry = new NazgulRegistry();

        foreach (var id in WraithIds)
            Assert.IsTrue(registry.IsWraith(id), $"'{id}' must be in the wraith roster.");

        Assert.AreEqual(9, WraithIds.Length, "There are nine Ringwraiths.");
        Assert.IsFalse(registry.IsWraith("lord_1_17"), "Sauron is not a wraith; he is keyed separately.");
    }

    // ---- Comment hygiene ---------------------------------------------------

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
            if (sibling == "feature")
                continue;

            Assert.IsNotNull(root[sibling],
                $"{property.Name} documents a key '{sibling}' that no longer exists in the config.");
        }
    }

    [TestMethod]
    public void ShippedConfig_ContainsNoLongDashes()
    {
        // output-style.md Part 2: em and en dashes are banned in produced prose, and the comment
        // keys here are prose a modder reads.
        var text = File.ReadAllText(ConfigPath);

        Assert.IsFalse(Regex.IsMatch(text, "[–—]"),
            "uncapturable_heroes_config.json contains an em or en dash. Use a comma, colon, or new sentence.");
    }

    private static readonly string[] WraithIds =
    {
        "lord_1_15", "lord_1_155", "lord_1_16", "lord_1_28", "lord_1_38",
        "lord_1_48", "lord_1_48_1", "lord_1_48_2", "lord_1_48_3",
    };

    /// <summary>Returns the body of the xsl:template matching one NPCCharacter id.</summary>
    private static string ExtractXsltTemplate(string xslt, string heroId)
    {
        var marker = $"NPCCharacter[@id='{heroId}']";
        var start = xslt.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"No xsl:template found for {heroId} in lords.xslt.");

        var end = xslt.IndexOf("</xsl:template>", start, StringComparison.Ordinal);
        Assert.IsTrue(end > start, $"Unterminated xsl:template for {heroId} in lords.xslt.");

        return xslt.Substring(start, end - start);
    }
}
