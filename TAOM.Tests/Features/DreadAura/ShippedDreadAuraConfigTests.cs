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
using TAOM.Features.DreadAura;
using TAOM.Features.NazgulFamily;

namespace TAOM.Tests.Features.DreadAura;

/// <summary>
/// Pins the config file actually shipped to players. The provider is fail-soft by design, so a
/// shipped file with a bad value would revert to compiled defaults and the feature would look
/// fine while ignoring everything authored here. These tests make that loud.
/// </summary>
[TestClass]
public class ShippedDreadAuraConfigTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private static string ConfigPath => Path.Combine(ModuleDataPath, "dread_aura", "dread_aura_config.json");

    private IModLogger _logger = null!;
    private DreadAuraConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new DreadAuraConfigProvider(pathService, _logger);
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
        // The load-bearing one. Eight of the Nine carry no race attribute in TAOM's data, so the
        // hero-set axis is the ONLY thing that finds them. A well-meaning "simplify this to a race
        // list" refactor would silently kill the aura for eight of nine wraiths with no error,
        // no warning, and a config that still parses cleanly.
        var config = _sut.GetConfig();

        CollectionAssert.Contains(config.HeroSets, "nazgul_nine",
            "dread_aura_config.json must keep the nazgul_nine hero set. Race cannot identify the " +
            "Nazgul: see the _comment_heroSets note in the config and docs/features/dread-aura.md.");
    }

    [TestMethod]
    public void ShippedConfig_ListsSauronByBothHeroIdAndRace()
    {
        var config = _sut.GetConfig();

        CollectionAssert.Contains(config.HeroIds, "lord_1_17", "Sauron's hero id must be listed.");
        CollectionAssert.Contains(config.Races, "sauron", "Sauron's race must be listed.");
    }

    [TestMethod]
    public void ShippedConfig_ShipsNoUnintendedDreadSources()
    {
        // Trolls, Mumakil and Saruman are deliberately OUT of v1. This is the gate that stops one
        // of them arriving as a quiet data edit with no balance pass behind it.
        var config = _sut.GetConfig();

        CollectionAssert.AreEquivalent(new[] { "sauron" }, config.Races,
            "A race was added to the shipped dread sources. That is a balance change: give it an " +
            "issue and a control battle, and update the table in docs/features/dread-aura.md.");
        CollectionAssert.AreEquivalent(new[] { "lord_1_17" }, config.HeroIds);
        CollectionAssert.AreEquivalent(new[] { "nazgul_nine" }, config.HeroSets);
    }

    [TestMethod]
    public void ShippedConfig_NazgulHeroSetResolvesToTheRealWraithRoster()
    {
        // Cross-file: the config names a set, NazgulRegistry owns its membership. Assert the link
        // actually resolves rather than trusting that the name means something.
        var registry = new NazgulRegistry();

        Assert.IsTrue(registry.IsWraith("lord_1_15"), "The Witch-King must be in the wraith roster.");
        Assert.IsTrue(registry.IsWraith("lord_1_48"), "Khamul must be in the wraith roster.");
        Assert.IsFalse(registry.IsWraith("lord_1_17"), "Sauron is not a wraith; he is keyed separately.");
    }

    [TestMethod]
    public void ShippedConfig_BalanceValuesMatchTheDocumentedTable()
    {
        // These four numbers are the balance contract in docs/features/dread-aura.md and the
        // golden rout times in DreadAuraServiceTests. Changing one here without the other two
        // leaves three sources of truth disagreeing.
        var config = _sut.GetConfig();

        Assert.AreEqual(12f, config.Profile.Radius, 0.001f);
        Assert.AreEqual(4f, config.Profile.InnerRadius, 0.001f);
        Assert.AreEqual(5f, config.Profile.MoralePerSecond, 0.001f);
        Assert.AreEqual(0f, config.Profile.MoraleFloor, 0.001f);
    }

    [TestMethod]
    public void ShippedConfig_ResistTableCoversElvesAndDwarves()
    {
        var config = _sut.GetConfig();

        Assert.AreEqual(0.4f, config.RaceResist["elf"], 0.001f);
        Assert.AreEqual(0.5f, config.RaceResist["dwarf"], 0.001f);
    }

    [TestMethod]
    public void ShippedConfig_RaceKeysAreSpelledAsTheGameSpellsThem()
    {
        // "nazgul" vs "nazghul" is the exact typo class this catches: a race key that does not
        // exist is skipped with a warning at runtime, which nobody reads.
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "human", "elf", "dwarf", "orc", "goblin", "uruk", "uruk_hai", "pale_uruk", "dg_uruk",
            "berserker", "sauron", "saruman", "nazghul", "cave_troll", "hill_troll",
        };

        var config = _sut.GetConfig();

        foreach (var name in config.Races.Concat(config.RaceResist.Keys))
        {
            Assert.IsTrue(known.Contains(name),
                $"'{name}' is not a race name TAOM ships. Note the engine spelling is 'nazghul', not 'nazgul'.");
        }
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

            // Two are section headers rather than per-field notes.
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

        Assert.IsFalse(Regex.IsMatch(text, "[–—]"),
            "dread_aura_config.json contains an em or en dash. Use a comma, colon, or new sentence.");
    }
}
