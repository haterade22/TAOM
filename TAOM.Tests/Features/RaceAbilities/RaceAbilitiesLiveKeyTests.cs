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

namespace TAOM.Tests.Features.RaceAbilities;

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
}
