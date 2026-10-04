using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;

// Pins race_abilities.json as shipped: it loads with no warning, and it says exactly what the compiled
// profiles say. The provider is fail-soft, so a bad shipped value would revert quietly and look fine; and
// the compiled profiles are what a player gets when the file is missing, so the two must not drift.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class ShippedRaceAbilitiesConfigTests
{
    internal static string ModuleDataPath => Path.Combine(
        Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..")), @"Main\_Module\ModuleData");

    internal static RaceAbilitiesConfig Load(string moduleDataPath, IModLogger logger)
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(moduleDataPath);
        return new RaceAbilitiesConfigProvider(pathService, logger).GetConfig();
    }

    [TestMethod]
    public void ShippedFile_Exists() =>
        Assert.IsTrue(File.Exists(Path.Combine(ModuleDataPath, "race_abilities", "race_abilities.json")));

    [TestMethod]
    public void ShippedFile_LoadsWithoutAWarning()
    {
        var logger = Substitute.For<IModLogger>();

        var config = Load(ModuleDataPath, logger);

        Assert.AreEqual(9, config.Races.Count);
        Assert.AreEqual(17, config.Cultures.Count);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedFile_MatchesTheCompiledProfiles()
    {
        var shipped = Load(ModuleDataPath, Substitute.For<IModLogger>());
        var compiled = Load(Path.Combine(Path.GetTempPath(), "TAOM_NoSuchDir_" + Path.GetRandomFileName()), Substitute.For<IModLogger>());

        Assert.AreEqual(JsonConvert.SerializeObject(compiled), JsonConvert.SerializeObject(shipped));
    }

    [TestMethod]
    public void ShippedFile_EveryAbilityHasADisplayNameRow()
    {
        // RaceAbilityNames maps each shipped id to a localized TextObject; an id without a row would show raw.
        var names = File.ReadAllText(Path.Combine(ModuleDataPath, "..", "..", "Features", "RaceAbilities", "Hooks", "RaceAbilityNames.cs"));
        var config = Load(ModuleDataPath, Substitute.For<IModLogger>());
        var missing = config.Races.Values.Concat(config.Cultures.Values)
            .Select(p => p.AbilityId).Distinct()
            .Where(id => !names.Contains($"\"{id}\" => new TextObject(\"{{=taom_race_ability_{id}}}"))
            .ToList();

        Assert.AreEqual(0, missing.Count, "abilities with no display name row: " + string.Join(", ", missing));
    }
}

// Every race and culture the shipped file names exists in the installed game, so no profile is dead on
// arrival. Races come from skins.xml (the Armory registers TAOM's; the elf race is written one attribute
// per line, which the pattern allows); cultures from every Culture element TAOM, TAOM_Map and SandBoxCore
// load. A key that matches nothing would be skipped silently in game (the resolver logs, nothing fails).
[TestClass]
[TestCategory("LiveInstall")]
public class RaceAbilitiesLiveKeyTests
{
    private const string Modules = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules";

    [TestMethod]
    public void EveryRaceKeyAndKinRace_IsARegisteredRace()
    {
        var skins = new[] { Path.Combine(Modules, @"LOTRLOME_Armory\ModuleData\skins.xml"), Path.Combine(Modules, @"Native\ModuleData\skins.xml") }
            .Where(File.Exists).ToArray();
        if (skins.Length == 0)
            Assert.Inconclusive("Bannerlord install not found; race registration cannot be checked here.");
        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in skins)
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"<race\b[^>]*?id\s*=\s*""([A-Za-z_0-9]+)""", RegexOptions.Singleline))
                registered.Add(m.Groups[1].Value);

        var config = ShippedRaceAbilitiesConfigTests.Load(ShippedRaceAbilitiesConfigTests.ModuleDataPath, Substitute.For<IModLogger>());
        var named = config.Races.Keys
            .Concat(config.Races.Values.Concat(config.Cultures.Values).SelectMany(p => p.KinRaces))
            .Distinct();
        var unknown = named.Where(race => !registered.Contains(race)).ToList();

        Assert.IsTrue(registered.Contains("human"), "the culture profiles need the engine's human race");
        Assert.AreEqual(0, unknown.Count, "race_abilities.json names races no skins.xml registers: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void EveryCultureKey_IsADefinedCulture()
    {
        var sources = new[]
            {
                ShippedRaceAbilitiesConfigTests.ModuleDataPath,
                Path.Combine(Modules, @"TAOM_Map\ModuleData"),
                Path.Combine(Modules, @"SandBoxCore\ModuleData"),
            }
            .Where(Directory.Exists).ToArray();
        if (!Directory.Exists(Path.Combine(Modules, @"SandBoxCore\ModuleData")))
            Assert.Inconclusive("Bannerlord install not found; vanilla cultures cannot be checked here.");
        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dir in sources)
            foreach (var file in Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories))
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"<Culture\b[^>]*?\bid\s*=\s*""([A-Za-z_0-9]+)""", RegexOptions.Singleline))
                    defined.Add(m.Groups[1].Value);

        var config = ShippedRaceAbilitiesConfigTests.Load(ShippedRaceAbilitiesConfigTests.ModuleDataPath, Substitute.For<IModLogger>());
        var unknown = config.Cultures.Keys.Where(culture => !defined.Contains(culture)).ToList();

        Assert.AreEqual(0, unknown.Count, "race_abilities.json names cultures nothing defines: " + string.Join(", ", unknown));
    }
}
