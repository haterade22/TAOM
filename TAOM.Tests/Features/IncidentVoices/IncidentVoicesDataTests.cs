using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.EncounterLines;
using TAOM.Features.IncidentVoices;
using TAOM.Features.IncidentVoices.Models;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.IncidentVoices;

/// <summary>
/// The loader's validation rules, and the shipped <c>incidents_lotr/incidents_lotr.json</c> pinned against the audit (101 vanilla
/// incidents, 55 KEEP-REWORD, 37 RESTRICT, 8 SPLIT, 1 DISABLE; PLAN.md of 2026-11-26 and the owner's decisions of the same day).
/// </summary>
[TestClass]
public class IncidentVoicesDataTests
{
    private IModLogger _logger = null!;
    private string _dir = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _dir = Path.Combine(Path.GetTempPath(), "TAOM_Incidents_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_dir, IncidentCatalogProvider.FolderName));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private IncidentCatalogProvider ProviderFor(string moduleData)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(moduleData);
        return new IncidentCatalogProvider(paths, _logger);
    }

    private IncidentCatalog Parse(string json) => ProviderFor(_dir).Parse(json);

    private static IncidentCatalog Shipped()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(RepoPath("Main", "_Module", "ModuleData"));
        return new IncidentCatalogProvider(paths, Substitute.For<IModLogger>()).GetCatalog();
    }

    // Loader

    [TestMethod]
    public void GetCatalog_MissingFile_GivesAnEmptyCatalog_AndOneWarning()
    {
        var catalog = ProviderFor(_dir).GetCatalog();

        Assert.AreEqual(0, catalog.Rules.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetCatalog_UnparseableFile_GivesAnEmptyCatalog_AndOneWarning()
    {
        File.WriteAllText(Path.Combine(_dir, IncidentCatalogProvider.FolderName, IncidentCatalogProvider.FileName), "{ not json");

        Assert.AreEqual(0, ProviderFor(_dir).GetCatalog().Rules.Count);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Parse_AGoodRow_IsKept_WithItsRestrictAndPlaces()
    {
        var catalog = Parse(@"{""vocabulary"": [""gods""], ""incidents"": [
          {""id"": ""incident_a"", ""verdict"": ""restrict"", ""versions"": [""all""], ""options"": 3, ""vars"": [""place""],
           ""restrict"": {""groups"": [""West""], ""families"": [""man""], ""versions"": [""free""]}, ""disease"": true,
           ""places"": [{""variable"": ""place"", ""kind"": ""Town""}]}]}");

        var rule = catalog.Find("incident_a")!;
        Assert.AreEqual("RESTRICT", rule.Verdict);
        CollectionAssert.AreEqual(new[] { "west" }, rule.Restrict.Groups.ToArray());
        CollectionAssert.AreEqual(new[] { "man" }, rule.Restrict.Families.ToArray());
        CollectionAssert.AreEqual(new[] { "PLACE" }, rule.Vars.ToArray());
        Assert.IsTrue(rule.Disease);
        Assert.AreEqual(new PlaceFix("PLACE", "town"), rule.Places.Single());
        CollectionAssert.AreEqual(new[] { "GODS" }, catalog.Vocabulary.ToArray());
    }

    [DataTestMethod]
    [DataRow(@"{""id"": """", ""verdict"": ""SPLIT"", ""versions"": [""free"", ""shadow""]}")]
    [DataRow(@"{""id"": ""incident_a"", ""verdict"": ""MAYBE"", ""versions"": [""all""]}")]
    [DataRow(@"{""id"": ""incident_a"", ""verdict"": ""SPLIT"", ""versions"": [""free""]}")]
    [DataRow(@"{""id"": ""incident_a"", ""verdict"": ""KEEP-REWORD"", ""versions"": []}")]
    [DataRow(@"{""id"": ""incident_a"", ""verdict"": ""KEEP-REWORD"", ""versions"": [""all""], ""options"": -1}")]
    [DataRow(@"{""id"": ""incident_a"", ""verdict"": ""RESTRICT"", ""versions"": [""all""], ""restrict"": {""versions"": [""neutral""]}}")]
    [DataRow(@"{""id"": ""incident_a"", ""verdict"": ""RESTRICT"", ""versions"": [""all""], ""places"": [{""variable"": ""PLACE"", ""kind"": ""sea""}]}")]
    public void Parse_ABadRow_IsDropped_WithAWarning(string row)
    {
        var catalog = Parse(@"{""incidents"": [" + row + @", {""id"": ""incident_ok"", ""verdict"": ""KEEP-REWORD"", ""versions"": [""all""]}]}");

        Assert.IsNull(catalog.Find("incident_a"));
        Assert.IsNotNull(catalog.Find("incident_ok"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("dropping the row")));
    }

    [TestMethod]
    public void Parse_ARepeatedId_KeepsTheFirst()
    {
        var catalog = Parse(@"{""incidents"": [
          {""id"": ""incident_a"", ""verdict"": ""KEEP-REWORD"", ""versions"": [""all""], ""options"": 2},
          {""id"": ""incident_a"", ""verdict"": ""SPLIT"", ""versions"": [""free"", ""shadow""], ""options"": 9}]}");

        Assert.AreEqual(2, catalog.Find("incident_a")!.Options);
    }

    [TestMethod]
    public void Parse_ADisableRowMayListNoVersions_AndIsDisabled()
    {
        var rule = Parse(@"{""incidents"": [{""id"": ""incident_a"", ""verdict"": ""DISABLE"", ""versions"": []}]}").Find("incident_a")!;

        Assert.IsTrue(rule.Disable);
    }

    // The shipped data

    [TestMethod]
    public void Shipped_HoldsAll101VanillaIncidents_WithTheAuditedVerdictCounts()
    {
        var rules = Shipped().Rules;

        Assert.AreEqual(101, rules.Count);
        Assert.AreEqual(55, rules.Count(r => r.Verdict == "KEEP-REWORD"));
        Assert.AreEqual(37, rules.Count(r => r.Verdict == "RESTRICT"));
        Assert.AreEqual(8, rules.Count(r => r.Verdict == "SPLIT"));
        Assert.AreEqual(1, rules.Count(r => r.Verdict == "DISABLE"));
        Assert.IsTrue(rules.All(r => r.Id.StartsWith("incident_", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Shipped_OnlyGnosticBoysIsDisabled_AndTheThreeSicknessesAreDiseases()
    {
        var catalog = Shipped();

        CollectionAssert.AreEqual(new[] { "incident_gnostic_boys" }, catalog.Rules.Where(r => r.Disable).Select(r => r.Id).ToArray());
        CollectionAssert.AreEquivalent(new[] { "incident_camp_fever", "incident_bloody_flux", "incident_lice_and_fleas" },
            catalog.Rules.Where(r => r.Disease).Select(r => r.Id).ToArray());
    }

    [TestMethod]
    public void Shipped_TheEightSplitIncidentsHaveTwoVersions_AndEveryOtherOneHasOne()
    {
        var rules = Shipped().Rules;

        CollectionAssert.AreEquivalent(
            new[] { "desperate_times", "no_country_for_the_afflicted", "saint_of_the_woods", "right_of_pillage", "honor_the_slain_foe", "no_mood_for_mercy", "sorrows_of_war", "souvenirs" },
            rules.Where(r => r.Verdict == "SPLIT").Select(r => r.ShortId).ToArray());
        Assert.IsTrue(rules.Where(r => r.Verdict == "SPLIT").All(r => r.Versions.SequenceEqual(new[] { "free", "shadow" })));
        Assert.IsTrue(rules.Where(r => r.Verdict != "SPLIT" && r.Verdict != "DISABLE").All(r => r.OneText));
    }

    [TestMethod]
    public void Shipped_OtherDevelopersTextStays_MigrantInfluxAndJobOfferWriteNoText()
    {
        var catalog = Shipped();

        Assert.IsTrue(catalog.Find("incident_migrant_influx")!.VanillaText);
        Assert.IsTrue(catalog.Find("incident_job_offer")!.VanillaText);
        Assert.AreEqual(2, catalog.Rules.Count(r => r.VanillaText));
        foreach (var id in new[] { "taom_the_fix_2KaXj3Qa", "taom_training_grounds_guEjfYhO", "taom_training_grounds_Qb3axqdZ" })
            StringAssert.Contains(File.ReadAllText(RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml")), "id=\"" + id + "\"");
    }

    [TestMethod]
    public void Shipped_TheTwoPlaceFixesAreMarketManipulationAndVintageOfVictory()
    {
        var withPlaces = Shipped().Rules.Where(r => r.Places.Count > 0).ToDictionary(r => r.Id, r => r.Places.Single());

        Assert.AreEqual(2, withPlaces.Count);
        Assert.AreEqual(new PlaceFix("PLACE", "town"), withPlaces["incident_market_manipulation"]);
        Assert.AreEqual(new PlaceFix("WINE_SETTLEMENT_NAME", "vineyard"), withPlaces["incident_vintage_of_victory"]);
    }

    [TestMethod]
    public void Shipped_EveryRestrictGroupAndFamilyExistsInTheShippedPeopleMap()
    {
        var map = IncidentRulesTests.ShippedMap();
        var rules = Shipped().Rules;

        foreach (var group in rules.SelectMany(r => r.Restrict.Groups).Distinct())
            Assert.IsNotNull(map.FamilyOfGroup(group), "unknown group " + group);
        foreach (var family in rules.SelectMany(r => r.Restrict.Families).Distinct())
            CollectionAssert.Contains(EncounterGroupMap.SpeakingFamilies.ToArray(), family);
    }

    [TestMethod]
    public void Shipped_TheRestrictGroupsMatchTheAudit()
    {
        var catalog = Shipped();
        var horse = new[] { "man", "elf" };

        foreach (var id in new[] { "an_affair_of_honor", "troops_fight_over_insult", "purebred_horse", "horsefly_plague", "glanders_outbreak", "colicky_horses", "broken_wagon", "injury_accident", "nearly_a_gift_horse" })
            CollectionAssert.AreEqual(horse, catalog.Find("incident_" + id)!.Restrict.Families.ToArray(), id);
        CollectionAssert.AreEquivalent(new[] { "easterlings", "southrons" }, catalog.Find("incident_nomads_wish_to_settle")!.Restrict.Groups.ToArray());
        CollectionAssert.AreEquivalent(new[] { "easterlings", "southrons" }, catalog.Find("incident_tiller_and_wanderer")!.Restrict.Groups.ToArray());
        CollectionAssert.AreEquivalent(new[] { "rohirrim", "northmen", "dunlendings" }, catalog.Find("incident_solider_is_wanted_criminal")!.Restrict.Groups.ToArray());
        CollectionAssert.AreEqual(new[] { "free" }, catalog.Find("incident_servants_of_mercy")!.Restrict.Versions.ToArray());
        Assert.AreEqual(17, catalog.Rules.Count(r => r.Restrict.Families.SequenceEqual(new[] { "man" })));
    }

    [TestMethod]
    public void Shipped_TheVocabularyIsTheSevenWordsOfThePlan()
        => CollectionAssert.AreEqual(new[] { "GODS", "POWER_OATH", "HOLY_PLACE", "HOLY_PERSON", "DARK_FORCE", "LORD_TITLE", "COIN" }, Shipped().Vocabulary.ToArray());

    [TestMethod]
    public void Shipped_NoVocabularyWordIsAlsoAVanillaVariable()
    {
        var vars = Shipped().Rules.SelectMany(r => r.Vars).ToHashSet();

        foreach (var word in Shipped().Vocabulary)
            Assert.IsFalse(vars.Contains(word), word + " is a vanilla variable, so the vocabulary would overwrite it");
    }

    [TestMethod]
    public void Shipped_EveryPlaceVariableIsOneVanillaSetsOnThatIncident()
    {
        foreach (var rule in Shipped().Rules.Where(r => r.Places.Count > 0))
            foreach (var fix in rule.Places)
                CollectionAssert.Contains(rule.Vars.ToArray(), fix.Variable, rule.Id);
    }

    [TestMethod]
    public void Shipped_ModuleFileTextHasNoDashes()
    {
        var text = File.ReadAllText(RepoPath("Main", "_Module", "ModuleData", "incidents_lotr", "incidents_lotr.json"));

        Assert.IsFalse(text.Contains((char)0x2013) || text.Contains((char)0x2014));
    }
}
