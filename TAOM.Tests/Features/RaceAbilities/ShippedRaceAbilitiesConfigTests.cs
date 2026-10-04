using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Domain;
using TAOM.Tests.Infrastructure;

// Pins race_abilities.json as shipped: it loads with no warning, it says exactly what the compiled
// profiles say, and every culture it keys on is a real culture id. The provider is fail-soft, so a bad
// shipped value would revert quietly and look fine; the compiled profiles are what a player gets when the
// file is missing, so the two must not drift; and a culture key that matches nothing is silent at every
// layer (the BannerBearers lesson, rca-banner-bearers-2026-07-16.md).

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class ShippedRaceAbilitiesConfigTests
{
    // The six vanilla cultures TAOM re-skins through spcultures.xslt keep their vanilla ids.
    private static readonly string[] ReskinnedVanillaCultureIds = { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" };

    internal static string ModuleDataPath => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

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
        var names = RepoPaths.ReadSource("Main/Features/RaceAbilities/Hooks/RaceAbilityNames.cs");
        var config = Load(ModuleDataPath, Substitute.For<IModLogger>());
        var missing = config.Races.Values.Concat(config.Cultures.Values)
            .Select(p => p.AbilityId).Distinct()
            .Where(id => !names.Contains($"\"{id}\" => new TextObject(\"{{=taom_race_ability_{id}}}"))
            .ToList();

        Assert.AreEqual(0, missing.Count, "abilities with no display name row: " + string.Join(", ", missing));
    }

    // A culture is defined by a Culture element of an SPCultures document, not by any element of that name:
    // several feature configs use <Culture id> rows of their own (cc_body_properties.xml carries "dale",
    // which is no culture id). TAOM declares its own in taom_spcultures.xml and re-skins six vanilla ones.
    [TestMethod]
    public void EveryCultureKey_IsADefinedCulture()
    {
        var document = XDocument.Load(Path.Combine(ModuleDataPath, "taom_spcultures.xml"));
        Assert.AreEqual("SPCultures", document.Root!.Name.LocalName);
        var defined = new HashSet<string>(
            document.Root.Elements("Culture").Select(c => (string?)c.Attribute("id")).Where(id => id != null)!,
            StringComparer.Ordinal);
        defined.UnionWith(ReskinnedVanillaCultureIds);

        var config = Load(ModuleDataPath, Substitute.For<IModLogger>());
        var unknown = config.Cultures.Keys.Where(culture => !defined.Contains(culture)).ToList();

        Assert.AreEqual(0, unknown.Count, "race_abilities.json names cultures nothing defines: " + string.Join(", ", unknown));
    }

    [DataTestMethod]
    [DataRow("rohan")]
    [DataRow("dale")]
    [DataRow("khand")]
    [DataRow("dunland")]
    [DataRow("harad")]
    [DataRow("rhun")]
    public void NoLoreNameIsUsedAsACultureKey(string loreName) =>
        Assert.IsFalse(Load(ModuleDataPath, Substitute.For<IModLogger>()).Cultures.ContainsKey(loreName),
            $"'{loreName}' is a display name, not a culture id; it would never match");
}
