using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;
using TAOM.Features.FactionPower;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.FactionPower;

[TestClass]
public class FactionPowerCatalogTests
{
    private static KeyValuePair<string, FactionData> F(string key, string culture, string side, bool playable, string name = "")
        => new KeyValuePair<string, FactionData>(key, new FactionData { Name = name == "" ? key : name, GameFaction = culture, Side = side, Playable = playable });

    private static IReadOnlyList<FactionPowerEntry> Build(List<string> warnings, params KeyValuePair<string, FactionData>[] factions)
        => FactionPowerCatalog.Build(factions, warnings.Add);

    [TestMethod]
    public void ADuplicateCulture_TakesThePlayableEntrysSideAndName()
    {
        var warnings = new List<string>();

        var catalog = Build(warnings,
            F("harwan", "aserai", "evil", true, "Harwan"),
            F("shaghana", "aserai", "evil", false, "Shaghana"),
            F("bellakar", "aserai", "free", false, "Bellakar"),
            F("erebor", "erebor", "free", true, "Erebor"),
            F("nurunkhizdin", "erebor", "neutral", false, "Nurunkhizdin"));

        Assert.AreEqual("evil", catalog.Single(e => e.CultureId == "aserai").Side);
        Assert.AreEqual("Harwan", catalog.Single(e => e.CultureId == "aserai").Name);
        Assert.AreEqual("free", catalog.Single(e => e.CultureId == "erebor").Side);
    }

    [TestMethod]
    public void WithNoPlayableEntry_TheOrdinalFirstKeyWins()
    {
        var catalog = Build(new List<string>(),
            F("khand", "battania", "neutral", false),
            F("enedwaith", "battania", "free", false));

        var entry = catalog.Single();
        Assert.AreEqual("enedwaith", entry.Name);
        Assert.AreEqual("free", entry.Side);
    }

    [TestMethod]
    public void TheWinnerDoesNotDependOnInputOrder()
    {
        var a = Build(new List<string>(), F("b", "x", "evil", false), F("a", "x", "free", false));
        var b = Build(new List<string>(), F("a", "x", "free", false), F("b", "x", "evil", false));

        Assert.AreEqual(a.Single().Side, b.Single().Side);
    }

    [TestMethod]
    public void ACultureWhoseEntriesDisagree_LogsOneWarning()
    {
        var warnings = new List<string>();

        Build(warnings,
            F("harwan", "aserai", "evil", true),
            F("shaghana", "aserai", "evil", false),
            F("bellakar", "aserai", "free", false),
            F("lasgalen", "mirkwood", "free", true),
            F("neldoreth", "mirkwood", "free", false));

        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "aserai");
    }

    [TestMethod]
    public void AnEmptyGameFaction_IsSkipped()
    {
        var catalog = Build(new List<string>(), F("chajaphan_of_abanissa", "", "evil", true), F("a", "  ", "evil", false));

        Assert.AreEqual(0, catalog.Count);
    }

    [DataTestMethod]
    [DataRow("evil", "evil")]
    [DataRow("EVIL", "evil")]
    [DataRow("free", "free")]
    [DataRow("neutral", "neutral")]
    [DataRow("chaotic", "neutral")]
    [DataRow("", "neutral")]
    public void ASideIsNormalised_AndAnUnknownOneReadsNeutral(string side, string expected)
    {
        var catalog = Build(new List<string>(), F("a", "x", side, true));

        Assert.AreEqual(expected, catalog.Single().Side);
    }

    [TestMethod]
    public void TheCatalog_IsSortedByCultureId_AndANewJsonEntryNeedsNoCode()
    {
        var catalog = Build(new List<string>(), F("z", "zeta", "evil", true), F("a", "alpha", "free", true), F("n", "brandnew", "evil", true));

        CollectionAssert.AreEqual(new[] { "alpha", "brandnew", "zeta" }, catalog.Select(e => e.CultureId).ToArray());
    }

    [TestMethod]
    public void APowerCulture_KeysTheSliderInPlaceOfTheGameFaction()
    {
        var shaghana = F("taskralan_of_shaghana", "aserai", "evil", false, "Shaghana");
        shaghana.Value.PowerCulture = "shaghana";
        var abanissa = F("chajaphan_of_abanissa", "", "neutral", false, "Abanissa");
        abanissa.Value.PowerCulture = "abanissa";

        var catalog = Build(new List<string>(), F("harwan", "aserai", "evil", true, "Harwan"), shaghana, abanissa);

        CollectionAssert.AreEqual(new[] { "abanissa", "aserai", "shaghana" }, catalog.Select(e => e.CultureId).ToArray());
        Assert.AreEqual("Harwan", catalog.Single(e => e.CultureId == "aserai").Name);
        Assert.AreEqual("Shaghana", catalog.Single(e => e.CultureId == "shaghana").Name);
    }

    // The slider key is the kingdom's culture id at run time (FactionPowerRealm.KeyOf), so a kingdom whose culture no
    // factions.json row names has no slider and always reads 100. Reads the TAOM kingdom file; the vanilla kingdoms the
    // XSLT renames keep vanilla culture ids, which factions.json already names.
    [TestMethod]
    public void EveryShippedKingdomCulture_HasASlider()
    {
        var moduleData = CultureDataFixture.ModuleDataPath();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(moduleData);
        var factions = new FactionConfigProvider(paths, Substitute.For<IModLogger>()).LoadFactions();
        var keys = new HashSet<string>(FactionPowerCatalog.Build(factions, null).Select(e => e.CultureId));

        var missing = XDocument.Load(Path.Combine(moduleData, "taom_spkingdoms.xml")).Descendants("Kingdom")
            .Select(k => ((string?)k.Attribute("culture") ?? "").Replace("Culture.", "").Trim())
            .Where(c => c.Length > 0 && !keys.Contains(c))
            .Distinct().OrderBy(c => c, System.StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, missing.Count,
            "These kingdom cultures have no faction power slider; give a factions.json row power_culture: " + string.Join(", ", missing));
    }
}
