using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HeroAuras;

namespace TAOM.Tests.Features.HeroAuras;

/// <summary>
/// Pins hero_auras.json as shipped: the provider is fail-soft, so a bad value or a mistyped hero id would
/// load quietly and an aura would never appear. Every hero id must be an NPCCharacter in the repo data, and every
/// race name one the game registers.
/// </summary>
[TestClass]
public class ShippedHeroAurasConfigTests
{
    private static string RepoRoot => Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private IModLogger _logger = null!;
    private HeroAurasConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new HeroAurasConfigProvider(pathService, _logger);
    }

    [TestMethod]
    public void ShippedConfig_LoadsWithoutWarningOrError()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Auras.Count > 0, "hero_auras.json ships no aura");
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_EveryHeroIdIsACharacterInTheRepoData()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Named companions keep their rows outside characters/ (named_companions/named_companions.xml).
        foreach (var file in Directory.GetFiles(ModuleDataPath, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Contains(@"\Languages\"))
                continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(file), "<(?:NPCCharacter|Hero)\\b[^>]*?\\bid=\"([^\"]+)\""))
                ids.Add(match.Groups[1].Value);
        }

        var missing = _sut.GetConfig().Auras.SelectMany(a => a.HeroIds).Where(id => !ids.Contains(id)).ToList();

        Assert.AreEqual(0, missing.Count, "hero ids with no NPCCharacter: " + string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedConfig_RaceNamesAreSpelledAsTheGameSpellsThem()
    {
        // A misspelt race loads with one runtime warning and an aura that reaches nobody. The same list as
        // ShippedDreadAuraConfigTests: the races TAOM's skins register.
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "human", "elf", "dwarf", "orc", "goblin", "uruk", "uruk_hai", "pale_uruk", "dg_uruk",
            "berserker", "sauron", "saruman", "nazghul", "cave_troll", "hill_troll",
        };

        var unknown = _sut.GetConfig().Auras.SelectMany(a => a.Races ?? new List<string>()).Where(r => !known.Contains(r)).ToList();

        Assert.AreEqual(0, unknown.Count, "race names TAOM does not ship: " + string.Join(", ", unknown));
    }
}
