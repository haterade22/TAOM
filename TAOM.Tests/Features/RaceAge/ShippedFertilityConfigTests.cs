using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.RaceAge;
using TAOM.Features.RaceAge.Models;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.RaceAge;

/// <summary>
/// Pins the SHIPPED fertility data (#628) by loading the real files through the real providers.
///
/// Orcs and goblins overbred for two data reasons the formula tests cannot see: race modifiers of
/// 2.0 to 3.0 with fertile windows to 50, and three goblin cultures missing from the
/// start-of-campaign child generator's exclusion list, so every one of their clans started the game
/// topped up with children. The exclusion check derives the orc cultures from cultures.json rather
/// than a fixed list, because the list went stale exactly when goblin, mistymountainorcs and
/// bluecraig were added. The derivation sees only cultures listed in cultures.json; a culture that is
/// never offered at character creation escapes it.
/// </summary>
[TestClass]
public class ShippedFertilityConfigTests
{
    /// <summary>The orc-kin races. A culture whose default race is one of these is an orc culture.</summary>
    private static readonly HashSet<string> OrcFamilyRaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "orc", "goblin", "uruk", "uruk_hai", "pale_uruk", "dg_uruk", "berserker" };

    private const float MaxFertilityMod = 1.5f;
    private const int MaxOrcFertilityEnd = 45;
    private const int MaxHumanFertilityEnd = 60;

    private static readonly string ModuleDataPath = RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static IPathService ShippedPaths()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleDataPath);
        paths.ConfigPath.Returns(Path.Combine(ModuleDataPath, "configs") + Path.DirectorySeparatorChar);
        return paths;
    }

    private static RaceAgeConfig LoadShippedRaceAgeConfig(IModLogger logger = null) =>
        new RaceAgeConfigProvider(ShippedPaths(), logger ?? Substitute.For<IModLogger>()).LoadConfig();

    private static HashSet<string> ShippedExcludedCultures() =>
        new HashSet<string>(
            new InitialChildGenerationConfigProvider(ShippedPaths(), Substitute.For<IModLogger>()).LoadConfig().ExcludedCultures,
            StringComparer.OrdinalIgnoreCase);

    /// <summary>culture_id -> races[0] (the default race), from the shipped cultures.json via the production loader.</summary>
    private static Dictionary<string, string> DefaultRaceByCulture()
    {
        var cultures = new CultureCreationDataProvider(ShippedPaths(), Substitute.For<IModLogger>()).LoadCultures();
        Assert.IsTrue(cultures.Count > 0, "cultures.json parsed no entries; the file moved or its shape changed");
        return cultures
            .Where(c => c.Races.Length > 0)
            .ToDictionary(c => c.CultureId, c => c.Races[0], StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ShippedRaceAgeConfig_Load_EmitsNoValidationWarnings()
    {
        var logger = Substitute.For<IModLogger>();

        var config = LoadShippedRaceAgeConfig(logger);

        Assert.IsTrue(config.Races.Count > 0, "shipped race_age_config.json parsed no races");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedRaceAgeConfig_OrcFamilyRaces_AllPresent()
    {
        var config = LoadShippedRaceAgeConfig();

        var missing = OrcFamilyRaces.Where(r => !config.Races.ContainsKey(r)).ToList();

        Assert.AreEqual(0, missing.Count,
            "orc-family races missing from race_age_config.json (they would fall back to human): "
            + string.Join(", ", missing));
    }

    [TestMethod]
    public void ShippedRaceAgeConfig_EveryRace_FertilityModAtMostCeiling()
    {
        var config = LoadShippedRaceAgeConfig();

        var over = config.Races
            .Where(kv => kv.Value.FertilityMod > MaxFertilityMod)
            .Select(kv => $"{kv.Key}={kv.Value.FertilityMod}")
            .ToList();

        Assert.AreEqual(0, over.Count,
            $"fertilityMod above {MaxFertilityMod} (orcs overbred at 2.0 to 3.0, #628): " + string.Join(", ", over));
    }

    [TestMethod]
    public void ShippedRaceAgeConfig_OrcFamilyRaces_FertileNoLaterThanVanillaEnd()
    {
        var config = LoadShippedRaceAgeConfig();

        var late = new List<string>();
        foreach (var race in OrcFamilyRaces)
        {
            if (config.Races.TryGetValue(race, out var entry) && entry.FertilityEnd > MaxOrcFertilityEnd)
                late.Add($"{race}={entry.FertilityEnd}");
        }

        Assert.AreEqual(0, late.Count,
            $"orc-family fertilityEnd past {MaxOrcFertilityEnd}: " + string.Join(", ", late));
    }

    [TestMethod]
    public void ShippedRaceAgeConfig_Human_FertileNoLaterThanSixty()
    {
        var config = LoadShippedRaceAgeConfig();

        Assert.IsTrue(config.Races.TryGetValue("human", out var human), "no human entry in race_age_config.json");
        Assert.IsTrue(human.FertilityEnd <= MaxHumanFertilityEnd,
            $"human fertilityEnd {human.FertilityEnd} > {MaxHumanFertilityEnd}: "
            + "a 200-year lifespan must not mean a 177-year fertile window (#628)");
    }

    [TestMethod]
    public void ShippedInitialChildGeneration_EveryOrcCulture_Excluded()
    {
        var excluded = ShippedExcludedCultures();

        var orcCultures = DefaultRaceByCulture()
            .Where(kv => OrcFamilyRaces.Contains(kv.Value))
            .Select(kv => kv.Key)
            .ToList();
        var notExcluded = orcCultures.Where(c => !excluded.Contains(c)).ToList();

        Assert.IsTrue(orcCultures.Count > 0, "derived no orc cultures from cultures.json");
        Assert.AreEqual(0, notExcluded.Count,
            "orc cultures missing from initial_child_generation.json excluded_cultures, so their clans "
            + "start the campaign topped up with children (#628): " + string.Join(", ", notExcluded));
    }

    [TestMethod]
    public void ShippedInitialChildGeneration_EveryExcludedCulture_IsARealCulture()
    {
        // The reverse direction: a typo ("mistymountainorc") would exclude nothing, silently.
        var cultures = DefaultRaceByCulture();

        var unknown = ShippedExcludedCultures().Where(c => !cultures.ContainsKey(c)).ToList();

        Assert.AreEqual(0, unknown.Count,
            "excluded_cultures ids that name no culture in cultures.json (a typo excludes nothing): "
            + string.Join(", ", unknown));
    }
}
