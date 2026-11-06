using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class DutyTextTests
{
    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Night Watch")]
    [DataRow("{=taom_enlist_duty_other_title}Night Watch")]
    [DataRow("{=taom_enlist_duty_night_patrol_title")]
    [DataRow("{=taom_enlist_duty_night_patrol_title}")]
    [DataRow("{=taom_enlist_duty_night_patrol_title}   ")]
    public void Problem_MissingOrMalformedText_ReportsIt(string text)
    {
        Assert.IsNotNull(DutyText.Problem(text, "night_patrol", DutyText.TitleSuffix));
    }

    [TestMethod]
    public void Problem_KeyedEnglish_IsNullAndEnglishIsExtracted()
    {
        const string text = "{=taom_enlist_duty_night_patrol_title}Night Watch";

        Assert.IsNull(DutyText.Problem(text, "night_patrol", DutyText.TitleSuffix));
        Assert.AreEqual("Night Watch", DutyText.English(text, "night_patrol", DutyText.TitleSuffix, "generic"));
        Assert.AreEqual("generic", DutyText.English("Night Watch", "night_patrol", DutyText.TitleSuffix, "generic"));
    }

    [TestMethod]
    public void Source_ValidRowText_IsKept_AndOtherwiseTheComposedKeyWithGenericEnglish()
    {
        Assert.AreEqual("{=taom_enlist_duty_recon_sweep_title}a reconnaissance sweep",
            DutyText.Source("{=taom_enlist_duty_recon_sweep_title}a reconnaissance sweep", "recon_sweep", DutyText.TitleSuffix, "a task"));
        Assert.AreEqual("{=taom_enlist_duty_recon_sweep_title}a task",
            DutyText.Source(null, "recon_sweep", DutyText.TitleSuffix, "a task"));
        Assert.AreEqual("{=taom_enlist_duty_recon_sweep_success}It went well.",
            DutyText.Source("{=taom_enlist_duty_other_success}x", "recon_sweep", DutyText.SuccessSuffix, "It went well."));
    }

    [TestMethod]
    public void Problems_ReportsEveryBadTextAndNamesTheJsonField()
    {
        var problems = DutyText.Problems("night_patrol",
            (null, DutyText.TitleSuffix, "title"),
            ("{=taom_enlist_duty_night_patrol_body}ok", DutyText.BodySuffix, "body"),
            ("nope", DutyText.OptionASuffix, "optionA.label"));

        StringAssert.Contains(problems, "title text is missing");
        StringAssert.Contains(problems, "optionA.label text must start with");
        Assert.IsFalse(problems.Contains("body"));
        Assert.IsNull(DutyText.Problems("night_patrol", ("{=taom_enlist_duty_night_patrol_body}ok", DutyText.BodySuffix, "body")));
    }

    [TestMethod]
    public void EveryShippedRow_HasRegisteredSuccessAndFailureRows()
    {
        var moduleData = ModuleDataPath();
        var json = JObject.Parse(File.ReadAllText(Path.Combine(moduleData, "enlistment", "enlistment_duties.json")));
        var registry = XDocument.Load(Path.Combine(moduleData, "taom_enlistment_strings.xml"))
            .Descendants("string")
            .ToDictionary(e => (string)e.Attribute("id"), e => (string)e.Attribute("text"));

        var rows = json.SelectTokens("$..[?(@.id)]").OfType<JObject>().ToList();

        Assert.AreEqual(27, rows.Count);
        foreach (var row in rows)
        {
            var id = (string)row["id"];
            foreach (var (suffix, shared) in new[] { (DutyText.SuccessSuffix, DutyText.SharedSuccess), (DutyText.FailureSuffix, DutyText.SharedFailure) })
            {
                var key = DutyText.KeyFor(id, suffix);
                Assert.IsTrue(registry.TryGetValue(key, out var registered), id + " has no registered " + key + " row");
                var own = (string)row[suffix];
                var expected = own ?? "{=" + key + "}" + shared;
                Assert.AreEqual(expected, registered, key + (own == null ? " must carry the shared sentence" : " must equal its JSON string"));
            }
        }
    }

    [TestMethod]
    public void EveryShippedDutyString_MatchesTheRegisteredRowByteForByte()
    {
        var moduleData = ModuleDataPath();
        var json = JObject.Parse(File.ReadAllText(Path.Combine(moduleData, "enlistment", "enlistment_duties.json")));
        var registry = XDocument.Load(Path.Combine(moduleData, "taom_enlistment_strings.xml"))
            .Descendants("string")
            .ToDictionary(e => (string)e.Attribute("id"), e => (string)e.Attribute("text"));

        var texts = json.SelectTokens("$..[?(@.id)]").OfType<JObject>().SelectMany(row => RowTexts(row)).ToList();

        Assert.AreEqual(27 + 14 + 28 + 26, texts.Count, "title for 27 rows, body for 14 popups, label for 28 options, success and failure for 13 field duties");
        foreach (var (key, text) in texts)
        {
            Assert.IsTrue(registry.TryGetValue(key, out var registered), key + " is not registered in taom_enlistment_strings.xml");
            Assert.AreEqual(registered, text, key + " differs from the registered row");
        }
    }

    private static System.Collections.Generic.IEnumerable<(string Key, string Text)> RowTexts(JObject row)
    {
        var id = (string)row["id"];
        foreach (var (field, suffix, holder) in new[]
        {
            ("title", "title", row), ("body", "body", row),
            ("label", "opta", row["optionA"] as JObject), ("label", "optb", row["optionB"] as JObject),
            ("success", "success", row), ("failure", "failure", row),
        })
        {
            var text = (string)holder?[field];
            if (text != null)
                yield return ("taom_enlist_duty_" + id + "_" + suffix, text);
        }
    }

    private static string ModuleDataPath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "ModuleData");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.Fail("Could not locate Main/_Module/ModuleData from the test working directory.");
        return null;
    }
}
