using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;

// Every race the shipped file names, as a profile key or a kin race, is registered by the installed game,
// so no profile is dead on arrival: the resolver would skip an unknown race with a warning and nothing
// would fail. Races come from skins.xml (the Armory registers TAOM's; the elf race is written one attribute
// per line, which the pattern allows). The culture keys need no install: ShippedRaceAbilitiesConfigTests.
// The spark effect is checked the same way, against the particle files a module's project.mbproj registers
// (Native's folder also holds two that never load): at run time an unknown name only logs a warning and the
// sparks stay off.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
[TestCategory("LiveInstall")]
public class RaceAbilitiesLiveKeyTests
{
    private const string Modules = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules";

    // The modules a TAOM game loads; another mod in the install folder registers nothing for this game.
    private static readonly string[] TaomGameModules =
        { "Native", "SandBoxCore", "SandBox", "CustomBattle", "StoryMode", "TAOM", "TAOM_Map", "LOTRLOME_Armory" };

    [TestMethod]
    public void EveryRaceKeyAndKinRace_IsARegisteredRace()
    {
        var skins = new[] { Path.Combine(Modules, @"LOTRLOME_Armory\ModuleData\skins.xml"), Path.Combine(Modules, @"Native\ModuleData\skins.xml") }
            .Where(File.Exists).ToArray();
        if (skins.Length == 0)
            Assert.Inconclusive("Bannerlord install not found; race registration cannot be checked here.");
        var registered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in skins)
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"<race\b[^>]*?\bid\s*=\s*""([A-Za-z_0-9]+)""", RegexOptions.Singleline))
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
    public void ShippedBurst_IsAParticleEffectTheGameLoads()
    {
        var files = TaomGameModules.Select(module => Path.Combine(Modules, module))
            .SelectMany(RegisteredParticleFiles).Where(File.Exists).ToArray();
        if (files.Length == 0)
            Assert.Inconclusive("Bannerlord install not found; particle effects cannot be checked here.");
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"<effect\b[^>]*?\bname\s*=\s*""([^""]+)""", RegexOptions.Singleline))
                declared.Add(m.Groups[1].Value);

        var burst = ShippedRaceAbilitiesConfigTests.Load(ShippedRaceAbilitiesConfigTests.ModuleDataPath, Substitute.For<IModLogger>()).Visuals.Burst;

        Assert.IsTrue(declared.Contains(burst), $"race_abilities.json's burst '{burst}' is no <effect> in the particle files the game loads");
    }

    // <file ... name="ModuleData/particle_systems_general.xml" type="particle_system" /> rows of a module's project.mbproj.
    private static IEnumerable<string> RegisteredParticleFiles(string module)
    {
        var mbproj = Path.Combine(module, "ModuleData", "project.mbproj");
        if (!File.Exists(mbproj))
            yield break;
        foreach (Match tag in Regex.Matches(File.ReadAllText(mbproj), @"<file\b[^>]*>"))
        {
            var name = Regex.Match(tag.Value, @"\bname\s*=\s*""([^""]+)""");
            if (name.Success && Regex.IsMatch(tag.Value, @"\btype\s*=\s*""particle_system"""))
                yield return Path.Combine(module, name.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
